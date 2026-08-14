using System.Text;
using PgmStudio.Analysis.Layer;
using PgmStudio.Analysis.Playability;
using PgmStudio.Domain;
using PgmStudio.Minecraft.Dressing;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Export;

using Dict = Dictionary<string, object?>;

/// <summary>The outcome of composing a map for export: either a structured error (HTTP status + JSON body)
/// or the composed <c>map.xml</c>. For a sketch-originated map <see cref="World"/> also carries the
/// synthesised voxel world so the caller can bundle its region files.</summary>
public sealed record ExportComposition(int? ErrorStatus, Dict? ErrorBody, string? Xml, SketchWorld? World)
{
    public bool IsError => ErrorStatus is not null;
}

/// <summary>
/// The shared pipeline behind <c>GET /map/{slug}/xml</c> and <c>GET /map/{slug}/export</c>: the
/// traversability gate, sketch-intent resolution, and XML composition — so the two routes can't drift, and
/// the reviewed XML is exactly what ships. For a sketch map it builds the world and re-projects the resolved
/// intent (snapped spawns + auto-derived monument locations) so the XML agrees with the world; the build +
/// compose run under one guard, surfacing any failure as a structured error.
///
/// <para>Pure — no DB, no IO. Everything the store would otherwise supply (whether the map is intent-authored,
/// the traversability segments, the stored authoring intent for a sketch map, the cached surface palette and
/// spawn-ore renewables for everything else) is loaded by the caller and handed in, which is what lets a
/// headless driver reach this without reaching a database.</para>
/// </summary>
public static class MapExportComposer
{
    /// <param name="isIntent">Whether the map was authored through the declarative intent model — gates the
    /// traversability check below (corpus maps have none and export unconditionally).</param>
    /// <param name="segments">The map's scanned layer segments, for the traversability check. Only read when
    /// <paramref name="isIntent"/> is true; null when the map was never scanned.</param>
    /// <param name="intent">The stored authoring intent. Required (and always non-null in practice) when
    /// <paramref name="layoutBytes"/> is not null — a sketch-originated map resolves it against the world it
    /// builds; ignored otherwise.</param>
    /// <param name="surfacePalette">The cached scanned surface palette, for a non-sketch intent map's CTW
    /// boilerplate. Ignored for a sketch map, which has no scanned surface.</param>
    /// <param name="resources">The cached resource blocks, for a non-sketch intent map's renewables. Ignored
    /// for a sketch map, which derives its own renewable cubes from the world it just built.</param>
    public static ExportComposition Compose(
        Dict doc, byte[]? layoutBytes, bool isIntent, SegmentIndex? segments, MapIntent? intent,
        IReadOnlySet<int>? surfacePalette, IReadOnlyList<(string Type, int X, int Y, int Z)> resources)
    {
        // Playability gate: intent-authored maps must be traversable before they can export (§9).
        if (isIntent)
        {
            var trav = Traversability.Check(doc, segments?.SurfaceColumns(), segments?.Y0Columns());
            if (!trav.Connected)
                return new(409, new Dict
                {
                    ["error"] = "not traversable",
                    ["message"] = trav.Message,
                    ["isolated"] = trav.Isolated.Select(i => new Dict { ["kind"] = i.Kind, ["name"] = i.Name }).ToList(),
                }, null, null);
        }

        try
        {
            // Sketch-originated: synthesise the world, re-project the resolved intent so the XML agrees with
            // it, then compose (no scanned surface/resources for a sketch map).
            if (layoutBytes is not null)
            {
                var layoutJson = Encoding.UTF8.GetString(layoutBytes);
                var built = SketchWorldBuilder.Build(layoutJson, intent!);
                var goals = built.ResolvedIntent;

                // OB17 — asked again here against the ground the rasterizer actually produced, not the plan's
                // rectangles: a subtract cut, a relief solve, or a sketch edited after its compile all move
                // where ground is, and none of them re-enters the compile gate. A map begun in Sketch never
                // passes that gate at all, so this is the only place every export is checked.
                if (RefuseObjectivePlacement(layoutJson, goals) is { } placementRefusal) return placementRefusal;

                IntentGenerator.Apply(doc, goals);

                // OB19 — a tree, boulder or building may not stand inside a goal's clearance. Refused rather
                // than dropped: these three are authored, and dropping one would silently discard a placement
                // the author can see on the canvas.
                if (RefuseGoalClearance(layoutJson, goals) is { } clearanceRefusal) return clearanceRefusal;

                var renewCubes = SketchWorldBuilder.RenewableCubeFootprints(goals);
                var sketchXml = MapXmlComposer.Compose(doc, isIntent: true, surfaceBlockIds: null, resources: [], renewCubes);
                return new(null, null, sketchXml, built);
            }

            // Other maps get plain XML (they already ship a world). Intent maps additionally get the cached
            // surface palette + spawn-ore renewables — cache-only, never triggering a world scan on export.
            var xml = MapXmlComposer.Compose(doc, isIntent, surfacePalette, resources);
            return new(null, null, xml, null);
        }
        catch (DressingParseException ex)
        {
            // A malformed dressing document is an authoring error, not a reason to export the map bare —
            // refused by name rather than silently read as though nothing had been placed.
            return new(422, new Dict
            {
                ["error"] = "dressing document invalid",
                ["rule"] = DressingParseException.Rule,
                ["message"] = ex.Message,
                ["subject"] = ex.Subject,
                ["field"] = ex.Field,
            }, null, null);
        }
        catch (Exception ex)
        {
            return new(500, new Dict { ["error"] = ex.Message }, null, null);
        }
    }

    // ── OB17 — objective placement, over the ground the rasterizer actually produced ──────────────────────

    private static ExportComposition? RefuseObjectivePlacement(string layoutJson, MapIntent goals)
    {
        var groundColumns = SketchRasterizer.RasterizeColumns(layoutJson).Select(c => (c.X, c.Z)).ToHashSet();
        bool IsLand(int x, int z) => groundColumns.Contains((x, z));
        var findings = ObjectivePlacement.Check(PlacedGoals(goals), IsLand, KeepOuts(goals));
        if (findings.Count == 0) return null;

        return new(409, new Dict
        {
            ["error"] = "objective placement",
            ["message"] = string.Join("; ", findings.Select(f => f.Message)),
            ["findings"] = findings.Select(f => new Dict
            {
                ["rule"] = f.Rule, ["message"] = f.Message, ["subjects"] = f.Subjects,
            }).ToList(),
        }, null, null);
    }

    // Every destroyable/core as ground rather than as a marker, from the resolved intent's own stamped box —
    // the ground kept open is then the ground the structure occupies by construction (OB8), the same reason
    // the compile-time reading resolves the footprint from the piece rather than trusting the marker.
    private static IEnumerable<PlacedGoal> PlacedGoals(MapIntent goals)
    {
        foreach (var destroyable in goals.Destroyables ?? [])
            if (destroyable.Box is { } box)
                yield return new PlacedGoal("destroyable", GoalName(destroyable.Name, destroyable.Owner), Rect(box));
        foreach (var core in goals.Cores ?? [])
            if (core.Box is { } box)
                yield return new PlacedGoal("core", GoalName(core.Name, core.Owner), Rect(box));
    }

    // The stamped rooms a goal may not reach into: every spawn's and every wool's resolved frame, read
    // through the same public frame resolution SketchWorldBuilder itself stamps by, so the export gate can
    // never disagree with the world it just built.
    private static List<GoalKeepOut> KeepOuts(MapIntent goals)
    {
        var keepOuts = new List<GoalKeepOut>();
        foreach (var spawn in goals.Spawns)
        {
            var frame = SketchWorldBuilder.SpawnRoom(spawn).Frame;
            keepOuts.Add(new GoalKeepOut("spawn", spawn.Team, new BlockRect(frame.MinX, frame.MinZ, frame.MaxX, frame.MaxZ)));
        }
        foreach (var wool in goals.Wools ?? [])
        {
            var frame = SketchWorldBuilder.WoolFrame(wool);
            keepOuts.Add(new GoalKeepOut("wool room", wool.Owner, new BlockRect(frame.MinX, frame.MinZ, frame.MaxX, frame.MaxZ)));
        }
        return keepOuts;
    }

    private static string GoalName(string name, string owner) => name.Length > 0 ? name : owner;

    // ObjectiveFootprint/ObjectivePlacement speak the stamper's inclusive block box; BlockRect's max is
    // exclusive (the same conversion PlanValidator's compile-time reading makes).
    private static BlockRect Rect(BlockBox box) => new(box.MinX, box.MinZ, box.MaxX + 1, box.MaxZ + 1);

    // ── OB19 — a tree, boulder or building may not stand inside a goal's clearance ────────────────────────

    private static ExportComposition? RefuseGoalClearance(string layoutJson, MapIntent goals)
    {
        var violations = DressingScope.GoalClearanceViolations(layoutJson, goals);
        if (violations.Count == 0) return null;

        return new(409, new Dict
        {
            ["error"] = "prop in goal clearance",
            ["message"] = string.Join("; ", violations.Select(v =>
                $"{v.Kind} '{v.PropId}' at ({v.X}, {v.Z}) stands inside a goal's clearance")),
            ["rule"] = DressingScope.Rule,
            ["props"] = violations.Select(v => new Dict
            {
                ["kind"] = v.Kind, ["id"] = v.PropId, ["x"] = v.X, ["z"] = v.Z,
            }).ToList(),
        }, null, null);
    }
}
