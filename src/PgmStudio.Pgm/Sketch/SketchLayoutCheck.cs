using PgmStudio.Domain;
using PgmStudio.Geom;

namespace PgmStudio.Pgm.Sketch;

/// <summary>
/// What a sketch layout says that the build cannot honour — read from the document, before any ground is
/// realized.
///
/// <para>The rasterizer is set algebra over shapes, so a shape it cannot read contributes no ground rather
/// than failing: a kind nobody has, a polygon of two vertices and a circle of no radius each rasterize to
/// nothing, and a mirror mode nobody has fans the board onto itself, which leaves a map that states two
/// halves standing on one. Every one of those builds, and the author or the agent that wrote it is told
/// nothing — the picture simply has less in it than the document asked for. That silence is what this
/// answers: the findings are <b>complaints</b>, because the board did build, and each names the field that
/// named nothing.</para>
///
/// <para>One thing here refuses. A board's cost is paid per column of its <em>extent</em>, drawn or not, so
/// an extent past <see cref="SketchRules.MaxBoardColumns"/> does not fail slowly — it takes the machine with
/// it. That is measured across the symmetry orbit, because a shape far out on one side widens the board by
/// twice its distance, and it is measured here rather than inside the rasterizer so a caller is refused
/// before the first column is walked.</para>
/// </summary>
public static class SketchLayoutCheck
{
    /// <summary>The shape kinds the rasterizer draws (<c>SketchRasterizer.RingOf</c>). Anything else rings
    /// empty, which is the same board as a shape that was never drawn.</summary>
    private static readonly string[] Kinds = ["rectangle", "circle", "polygon", "lasso", "path"];

    /// <summary>The symmetry modes <see cref="Symmetry"/> knows. An unknown one is not refused there — it
    /// answers order 2 and the identity transform — so a board asking for one is built unmirrored.</summary>
    private static readonly string[] Modes =
        ["none", "mirror_x", "mirror_z", "mirror_d1", "mirror_d2", "rot_90", "rot_180"];

    /// <summary>The world's own height, restated here because <c>Pgm</c> cannot see
    /// <c>VoxelWorld.MaxHeight</c>, and pinned to it by <c>SketchLayoutCheckPinTests</c> in the one test
    /// project that sees both.</summary>
    public const int WorldHeight = 256;

    /// <summary>Read a layout as posted. A body that is not a layout at all is not this gate's to report —
    /// that is the request's own fault (<c>RQ1</c>), answered where the body is read.</summary>
    public static Findings Check(string layoutJson)
    {
        SketchLayout? layout;
        try { layout = SketchLayout.Parse(layoutJson); }
        catch (System.Text.Json.JsonException) { return Findings.None; }
        return Check(layout);
    }

    public static Findings Check(SketchLayout? layout)
    {
        if (layout is null) return Findings.None;

        var findings = new List<Finding>();
        var mode = layout.Setup?.MirrorMode ?? "rot_180";
        double centerX = layout.Setup?.Center?.Cx ?? 0, centerZ = layout.Setup?.Center?.Cz ?? 0;

        if (!Modes.Contains(mode))
            findings.Add(new Finding(SketchRules.NamesNothing,
                $"the board states mirror mode '{mode}', which is not a mode the studio knows, so it is "
                + "built unmirrored — every shape stands once, on one side",
                Severity.Complaint, Field: "setup.mirror_mode"));

        var shapeIds = new HashSet<string>(StringComparer.Ordinal);
        var extent = (MinX: double.MaxValue, MinZ: double.MaxValue, MaxX: double.MinValue, MaxZ: double.MinValue);

        foreach (var (shape, where) in Shapes(layout))
        {
            if (shape.Id.Length > 0) shapeIds.Add(shape.Id);
            var kind = shape.Type ?? "";

            if (!Kinds.Contains(kind))
            {
                findings.Add(new Finding(SketchRules.NamesNothing,
                    $"{Named(shape)} states kind '{kind}', which is not a kind the studio draws — it has "
                    + $"{Kinds.Length} ({string.Join(", ", Kinds)}) — so it draws no ground",
                    Severity.Complaint, Field: $"{where}.type", Subjects: Ids(shape)));
            }
            else if (Empty(shape) is { } why)
            {
                findings.Add(new Finding(SketchRules.DrawsNothing,
                    $"{Named(shape)} {why}, so it draws no ground",
                    Severity.Complaint, Field: where, Subjects: Ids(shape)));
            }

            if (Unbuildable(shape) is { } height)
                findings.Add(new Finding(SketchRules.UnbuildableHeight,
                    $"{Named(shape)} {height}, and the world is {WorldHeight} blocks tall — the column is cut "
                    + "to fit rather than built as stated",
                    Severity.Complaint, Field: where, Subjects: Ids(shape)));

            if (Bounds(shape) is not { } box) continue;
            // The shape as drawn, then one image per orbit axis. Read through OrbitAxes rather than through
            // Symmetry.Point's k, whose image index only varies for rot_90 — a mirror's k=0 is already the
            // mirrored copy, so counting images that way loses the half the author actually drew.
            Cover(box);
            foreach (var axis in Symmetry.OrbitAxes(mode)) Cover(Turned(box, axis, centerX, centerZ));
        }

        var islands = new HashSet<string>(SketchLayout.IslandIds(layout), StringComparer.Ordinal);
        foreach (var (island, where) in Islands(layout))
            foreach (var named in island.ShapeIds.Where(id => !shapeIds.Contains(id)))
                findings.Add(new Finding(SketchRules.NamesNothing,
                    $"island '{island.Id}' lists shape '{named}', which the layout does not carry",
                    Severity.Complaint, Field: $"{where}.shapeIds",
                    Subjects: island.Id is { Length: > 0 } id ? [id] : null));

        foreach (var orphan in (layout.Relief ?? []).Keys.Where(key => !islands.Contains(key)).OrderBy(key => key, StringComparer.Ordinal))
            findings.Add(new Finding(SketchRules.NamesNothing,
                $"a relief is stated for island '{orphan}', which the layout does not carry, so that "
                + "elevation is not built",
                Severity.Complaint, Field: $"relief.{orphan}"));

        // A theme scope resolves shape → map default, and a shape naming a registry entry that is not there
        // falls all the way through to whatever the map default happens to be — which paints a board and says
        // nothing, exactly the silence the three names above are reported for. Reported once per name rather
        // than once per shape: a board that mistyped one key wants one sentence, not thirty.
        var themes = new HashSet<string>(
            (IEnumerable<string>?)layout.Themes?.Keys ?? [], StringComparer.Ordinal);
        var missing = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var (shape, _) in Shapes(layout))
            if (shape.Theme is { Length: > 0 } named && !themes.Contains(named))
                (missing.TryGetValue(named, out var on) ? on : missing[named] = []).Add(shape.Id);
        foreach (var (named, on) in missing)
            findings.Add(new Finding(SketchRules.NamesNothing,
                $"{on.Count} shape{(on.Count == 1 ? "" : "s")} paint with theme '{named}', which the layout's "
                + $"registry does not carry{(themes.Count == 0 ? " (it states no themes at all)" : "")} — those "
                + "cells take the map default instead",
                Severity.Complaint, Field: "themes", Subjects: [.. on.Where(id => id.Length > 0)]));

        if (layout.MapTheme is { Length: > 0 } mapTheme && !themes.Contains(mapTheme))
            findings.Add(new Finding(SketchRules.NamesNothing,
                $"the map default is theme '{mapTheme}', which the layout's registry does not carry — every "
                + "cell no shape scope claims takes the built-in default instead",
                Severity.Complaint, Field: "mapTheme"));

        void Cover((double MinX, double MinZ, double MaxX, double MaxZ) box)
            => extent = (Math.Min(extent.MinX, box.MinX), Math.Min(extent.MinZ, box.MinZ),
                         Math.Max(extent.MaxX, box.MaxX), Math.Max(extent.MaxZ, box.MaxZ));

        // The refusal goes first: a caller that stops at the first refusal should see the one that matters.
        if (extent.MaxX > extent.MinX && Columns(extent) is { } columns && columns > SketchRules.MaxBoardColumns)
            findings.Insert(0, new Finding(SketchRules.BoardTooLarge,
                $"the board spans {Span(extent.MaxX - extent.MinX)}×{Span(extent.MaxZ - extent.MinZ)} columns "
                + $"({columns:N0}) across its symmetry orbit, more ground than the studio will realize — draw "
                + "it smaller, or nearer the symmetry centre"));

        return findings;
    }

    // Every shape the layout carries, with the path to it — the layers a stacked sketch holds, and the
    // single top-level layout a legacy one does (the same two the island walk reads).
    private static IEnumerable<(SketchShape Shape, string Where)> Shapes(SketchLayout layout)
    {
        foreach (var (layer, index) in (layout.Layers ?? []).Select((layer, index) => (layer, index)))
            foreach (var (shape, at) in (layer.Layout?.Shapes ?? []).Select((shape, at) => (shape, at)))
                yield return (shape, $"layers[{index}].layout.shapes[{at}]");
        foreach (var (shape, at) in (layout.Layout?.Shapes ?? []).Select((shape, at) => (shape, at)))
            yield return (shape, $"layout.shapes[{at}]");
    }

    private static IEnumerable<(SketchIsland Island, string Where)> Islands(SketchLayout layout)
    {
        foreach (var (layer, index) in (layout.Layers ?? []).Select((layer, index) => (layer, index)))
            foreach (var (island, at) in (layer.Layout?.Islands ?? []).Select((island, at) => (island, at)))
                yield return (island, $"layers[{index}].layout.islands[{at}]");
        foreach (var (island, at) in (layout.Layout?.Islands ?? []).Select((island, at) => (island, at)))
            yield return (island, $"layout.islands[{at}]");
    }

    // Why a shape of a known kind still draws nothing, or null where it draws something.
    private static string? Empty(SketchShape shape) => shape.Type switch
    {
        "polygon" or "lasso" => shape.Vertices is { Length: >= 3 }
            ? null
            : $"is a {shape.Type} with {shape.Vertices?.Length ?? 0} vertices, under the three that enclose ground",
        "circle" => shape.Radius > 0 ? null : $"is a circle of radius {shape.Radius ?? 0:0.##}",
        "path" => shape.Radius > 0
            ? shape.Vertices is { Length: >= 2 } ? null : $"is a path of {shape.Vertices?.Length ?? 0} points"
            : $"is a path of width {shape.Radius ?? 0:0.##}",
        "rectangle" => (shape.MaxX ?? 0) - (shape.MinX ?? 0) != 0 && (shape.MaxZ ?? 0) - (shape.MinZ ?? 0) != 0
            ? null
            : "is a rectangle with no area",
        _ => null,
    };

    // A column the world cannot hold, said as the fact that makes it one.
    private static string? Unbuildable(SketchShape shape)
    {
        double floor = shape.Floor ?? 0, top = floor + (shape.BaseHeight ?? 0);
        if (shape.BaseHeight < 0) return $"states a base_height of {shape.BaseHeight:0.##}, which is below nothing";
        if (floor < 0) return $"stands its floor at y={floor:0.##}";
        if (top >= WorldHeight) return $"reaches y={top:0.##}";
        return null;
    }

    // The ground a shape covers, before the orbit fans it — its own outline's bounding box.
    private static (double MinX, double MinZ, double MaxX, double MaxZ)? Bounds(SketchShape shape) => shape.Type switch
    {
        "rectangle" => (Math.Min(shape.MinX ?? 0, shape.MaxX ?? 0), Math.Min(shape.MinZ ?? 0, shape.MaxZ ?? 0),
                        Math.Max(shape.MinX ?? 0, shape.MaxX ?? 0), Math.Max(shape.MinZ ?? 0, shape.MaxZ ?? 0)),
        "circle" => Around(shape.CenterX ?? 0, shape.CenterZ ?? 0, Math.Abs(shape.Radius ?? 0)),
        "polygon" or "lasso" or "path" => shape.Vertices is { Length: > 0 } vertices
            ? (vertices.Min(v => v[0]) - Reach(shape), vertices.Min(v => v[1]) - Reach(shape),
               vertices.Max(v => v[0]) + Reach(shape), vertices.Max(v => v[1]) + Reach(shape))
            : null,
        _ => null,
    };

    // One orbit image of a box: transform its four corners about the centre and re-bound, since a rotation
    // turns a rectangle into a new axis-aligned one.
    private static (double MinX, double MinZ, double MaxX, double MaxZ) Turned(
        (double MinX, double MinZ, double MaxX, double MaxZ) box, string axis, double cx, double cz)
    {
        (double X, double Z)[] corners =
        [
            Symmetry.Apply(box.MinX, box.MinZ, axis, cx, cz), Symmetry.Apply(box.MinX, box.MaxZ, axis, cx, cz),
            Symmetry.Apply(box.MaxX, box.MinZ, axis, cx, cz), Symmetry.Apply(box.MaxX, box.MaxZ, axis, cx, cz),
        ];
        return (corners.Min(c => c.X), corners.Min(c => c.Z), corners.Max(c => c.X), corners.Max(c => c.Z));
    }

    private static double Reach(SketchShape shape) => shape.Type == "path" ? Math.Abs(shape.Radius ?? 0) : 0;

    private static (double, double, double, double) Around(double x, double z, double radius)
        => (x - radius, z - radius, x + radius, z + radius);

    // The columns an extent covers, saturating rather than overflowing: a board stated in millions of blocks
    // per side is refused for its size, not answered with a wrapped negative.
    private static double? Columns((double MinX, double MinZ, double MaxX, double MaxZ) extent)
    {
        var columns = (extent.MaxX - extent.MinX) * (extent.MaxZ - extent.MinZ);
        return double.IsFinite(columns) ? columns : double.MaxValue;
    }

    private static string Span(double side) => double.IsFinite(side) ? side.ToString("N0") : "∞";

    private static string Named(SketchShape shape) => shape.Id.Length > 0 ? $"shape '{shape.Id}'" : "a shape";

    private static IReadOnlyList<string>? Ids(SketchShape shape) => shape.Id.Length > 0 ? [shape.Id] : null;
}
