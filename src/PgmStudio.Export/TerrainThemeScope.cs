using PgmStudio.Geom;
using PgmStudio.Minecraft;
using PgmStudio.Minecraft.Painting;
using PgmStudio.Pgm.Sketch;
using PgmStudio.Vocabulary;

namespace PgmStudio.Export;

/// <summary>
/// Scoped terrain-paint theme resolution for the world export (docs/world-export/terrain-painting.md TP10). It
/// turns the paint data on the <see cref="SketchLayout"/> — the theme-JSON registry, the map-default id, and
/// each shape's own <see cref="SketchShape.Theme"/> or <see cref="SketchShape.Material"/> — into a per-cell
/// <c>themeAt(x, z)</c> the painter reads. A cell in a painted shape paints that shape's; every other cell
/// paints the map default; where painted shapes overlap the smaller (most specific) wins, resolved through
/// <see cref="SketchRasterizer"/>.
///
/// <para>A shape's <c>material</c> arrives here as the theme it means (<see cref="TerrainTheme.OfMaterial"/>),
/// so the painter has one input and not two: what a shape is made of and what paints the ground it is part of
/// are the same answer at two grains, and only this class knows which grain a cell was stated at. A theme
/// stating <c>edgesFromGround</c> is a third: paint laid <em>over</em> a landmass rather than the landmass
/// itself, so it keeps its surface and its fill and takes the rim and the wall from the map default
/// (<see cref="TerrainTheme.OverGround"/>, TP23).</para>
/// <para>Theming lives on the sketch geometry, not the plan: the scope is the shape, rasterised fresh at export,
/// so reshaping a shape moves its paint with it. The sibling of <see cref="TeamTerritory"/> for team ownership.</para>
/// </summary>
public static class TerrainThemeScope
{
    /// <summary>The per-cell theme resolver for <see cref="TerrainPainter"/>. Returns a constant map-default
    /// resolver when nothing is themed (a plain sketch), so the common path allocates nothing per cell.</summary>
    public static Func<string, int, int, TerrainTheme> ThemeAt(string layoutJson)
    {
        var layout = SketchLayout.Parse(layoutJson);

        var themes = new Dictionary<string, TerrainTheme>();
        foreach (var (id, json) in layout?.Themes ?? new())
            themes[id] = TerrainThemeJson.Deserialize(json.GetRawText());

        var mapDefault = layout?.MapTheme is { } mt && themes.TryGetValue(mt, out var mapTheme)
            ? mapTheme : TerrainTheme.Default;

        // (layer, shapeId) → its resolved theme, over every shape in every layer that states what paints it.
        // Keyed with the layer because a shape id is unique within its layer and not across the stack: two
        // made things compiled by one tool carry the same shape ids on different layers, and a cell is owned
        // by a shape of its own layer.
        //
        // A shape states its paint at one of two grains. A `theme` names the registry, which is ground: five
        // buckets chosen per column by whether the column is an edge. A `material` says what the shape is made
        // of, which is one bucket over its whole span (TP22) — and it is read first, because it is the
        // narrower statement; a document holding both is refused before it is stored (SK24), so the order
        // settles a case that only reaches a build unstored.
        var shapeTheme = new Dictionary<(string Layer, string Shape), TerrainTheme>();
        foreach (var layer in SketchLayout.Stack(layout))
            foreach (var s in layer.Shapes)
            {
                if (MaterialOf(s) is { } material) shapeTheme[(layer.Id!, s.Id)] = TerrainTheme.OfMaterial(material, mapDefault);
                else if (s.Theme is { } tid && themes.TryGetValue(tid, out var theme)) shapeTheme[(layer.Id!, s.Id)] = theme;
            }

        if (shapeTheme.Count == 0) return (_, _, _) => mapDefault;

        // A theme saying its edges are the ground's is composed against the map default, which is the board's
        // own answer to what its landmass looks like where it stops (TP23). Composed here rather than stored
        // composed, because the same paint scoped onto a board with a different default takes that board's.
        var cellToShape = SketchRasterizer.ShapeThemeOwners(layoutJson);
        return (layer, x, z) => cellToShape.TryGetValue((layer, x, z), out var shapeId)
            && shapeTheme.TryGetValue((layer, shapeId), out var theme)
                ? TerrainTheme.OverGround(theme, mapDefault)
                : mapDefault;
    }

    /// <summary>SK23 — every themed shape whose theme cannot show itself on it, because the shape has no
    /// interior column and so is rim and wall the whole way. A theme decides which of its buckets a block
    /// takes from whether that block's column is an edge; a shape every one of whose columns touches the void
    /// is an edge under every <c>rimEdges</c> setting, so the surface and the fill are unreachable and the
    /// pattern the theme was chosen for cannot appear at any size.
    ///
    /// <para>Only where the theme's <b>rim is enabled</b>: with it off the top course falls to the surface
    /// (TP12) and the theme shows exactly as it was written, which is the honest way to paint thin ground.
    /// And only for a shape stating a <c>theme</c> — one stating a <c>material</c> has a single bucket and
    /// nothing to hide.</para>
    ///
    /// <para>The footprint an interior is judged against is the <b>layer's</b>, not the shape's: a stilt in
    /// the middle of a platform they share has interior columns and a stilt on a layer of its own does not,
    /// and that difference is the whole of what makes the paint appear or not.</para></summary>
    public static Findings Check(string layoutJson)
    {
        var layout = SketchLayout.Parse(layoutJson);
        if (layout is null) return Findings.None;

        var themes = new Dictionary<string, TerrainTheme>();
        foreach (var (id, theme) in ThemesOf(layoutJson)) themes[id] = theme;
        if (themes.Count == 0) return Findings.None;

        // The shapes worth measuring: a stated theme, no material beside it, and a rim that paints.
        var watched = new Dictionary<(string Layer, string Shape), string>();
        foreach (var layer in SketchLayout.Stack(layout))
            foreach (var shape in layer.Shapes)
                if (shape.Material is null && shape.Role is null && shape.Theme is { } id
                    && themes.TryGetValue(id, out var theme) && theme.Rim.Enabled)
                    watched[(layer.Id ?? "", shape.Id)] = id;
        if (watched.Count == 0) return Findings.None;

        var footprint = new Dictionary<string, HashSet<(int X, int Z)>>();
        foreach (var segment in SketchRasterizer.RasterizeColumns(layout))
        {
            if (!footprint.TryGetValue(segment.Layer, out var cells))
                footprint[segment.Layer] = cells = [];
            cells.Add((segment.X, segment.Z));
        }

        var owned = new Dictionary<(string Layer, string Shape), List<(int X, int Z)>>();
        foreach (var ((layer, x, z), shape) in SketchRasterizer.ShapeThemeOwners(layoutJson))
        {
            if (!watched.ContainsKey((layer, shape))) continue;
            if (!owned.TryGetValue((layer, shape), out var cells)) owned[(layer, shape)] = cells = [];
            cells.Add((x, z));
        }

        // Reported per (layer, theme) and not per shape: the fix is one decision for all of them — turn that
        // theme's rim off, or say what those shapes are made of — and a board drawn out of small pieces has
        // hundreds, `opus5-slipway` 1,760 of them against eleven groups.
        var groups = new Dictionary<(string Layer, string Theme), (int Shapes, int Cells, int X, int Z, List<string> Ids)>();
        foreach (var ((layer, shape), cells) in owned.OrderBy(e => e.Key.Layer, StringComparer.Ordinal)
                                                     .ThenBy(e => e.Key.Shape, StringComparer.Ordinal))
        {
            var here = footprint.GetValueOrDefault(layer) ?? [];
            if (cells.Any(cell => Interior(here, cell))) continue;
            var (x, z) = cells.OrderBy(c => c.X).ThenBy(c => c.Z).First();
            var key = (layer, watched[(layer, shape)]);
            if (groups.TryGetValue(key, out var seen))
            {
                if (seen.Ids.Count < NamedShapes && shape.Length > 0) seen.Ids.Add(shape);
                groups[key] = seen with { Shapes = seen.Shapes + 1, Cells = seen.Cells + cells.Count };
            }
            else groups[key] = (1, cells.Count, x, z, shape.Length > 0 ? [shape] : []);
        }

        var findings = new List<Finding>();
        foreach (var ((layer, theme), (shapes, cells, x, z, ids)) in
                 groups.OrderByDescending(e => e.Value.Shapes).ThenBy(e => e.Key.Layer, StringComparer.Ordinal))
            findings.Add(new Finding(SketchRules.ThemeShowsOnlyItsEdge,
                $"{shapes} shape(s) on layer '{layer}' are themed '{theme}' and not one of their {cells} "
                + $"column(s) has ground on all eight sides — from ({x}, {z}) — so every column is an edge, "
                + "the rim and the wall are the only buckets that paint them, and the theme's surface is "
                + "nowhere on any of them"
                + (ids.Count > 0 ? $" ({string.Join(", ", ids)}{(shapes > ids.Count ? ", …" : "")})" : ""),
                Severity.Complaint, Field: $"layers.{layer}.shapes",
                Subjects: ids.Count > 0 ? ids : null));
        return findings;
    }

    /// <summary>How many of a group's shapes are named in its finding, before the rest become an ellipsis. A
    /// handful is what an author needs to find the group on the canvas; the count is what says how big it
    /// is.</summary>
    private const int NamedShapes = 5;

    // Ground on all eight sides — the narrowest edge test there is (`rimEdges: void`), so a column failing it
    // is an edge under `drop` and `boundary` too and the answer holds whatever the theme asked for.
    private static bool Interior(HashSet<(int X, int Z)> footprint, (int X, int Z) cell)
    {
        for (var dx = -1; dx <= 1; dx++)
        for (var dz = -1; dz <= 1; dz++)
            if ((dx != 0 || dz != 0) && !footprint.Contains((cell.X + dx, cell.Z + dz))) return false;
        return true;
    }

    /// <summary>The material a shape states it is made of, or null where it states none. A blob that will not
    /// read as a material is dropped exactly as an unreadable theme is: the gate names it before the layout is
    /// stored, and a build is not the place a document is judged.</summary>
    public static TerrainMaterial? MaterialOf(SketchShape shape)
    {
        if (shape.Material is not { } json) return null;
        try { return TerrainThemeJson.DeserializeMaterial(json.GetRawText()); }
        catch (System.Text.Json.JsonException) { return null; }
    }

    /// <summary>Every theme a layout carries, by the id it is registered under. What a gate reading the
    /// registry walks — the same deserialization the painter does, so a theme that will not parse as one is
    /// left out here exactly as it is dropped there.</summary>
    public static IEnumerable<(string Id, TerrainTheme Theme)> ThemesOf(string layoutJson)
    {
        var layout = SketchLayout.Parse(layoutJson);
        foreach (var (id, json) in layout?.Themes ?? new())
        {
            TerrainTheme theme;
            try { theme = TerrainThemeJson.Deserialize(json.GetRawText()); }
            catch (System.Text.Json.JsonException) { continue; }
            yield return (id, theme);
        }
    }

}
