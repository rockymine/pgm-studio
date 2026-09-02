using PgmStudio.Minecraft;
using PgmStudio.Pgm.Sketch;
using PgmStudio.Minecraft.Painting;

namespace PgmStudio.Export;

/// <summary>
/// Scoped terrain-paint theme resolution for the world export (docs/world-export/terrain-painting.md TP10). It
/// turns the theme data on the <see cref="SketchLayout"/> — the theme-JSON registry, the map-default id, and
/// each shape's <see cref="SketchShape.Theme"/> override — into a per-cell <c>themeAt(x, z)</c> the painter
/// reads. A cell in a themed shape paints that shape's theme; every other cell paints the map default; where
/// themed shapes overlap the smaller (most specific) wins, resolved through <see cref="SketchRasterizer"/>.
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

        if (themes.Count == 0) return (_, _, _) => mapDefault;

        // (layer, shapeId) → its resolved theme, over every shape in every layer that carries a known theme
        // id. Keyed with the layer because a shape id is unique within its layer and not across the stack:
        // two made things compiled by one tool carry the same shape ids on different layers, and a cell is
        // owned by a shape of its own layer.
        var shapeTheme = new Dictionary<(string Layer, string Shape), TerrainTheme>();
        foreach (var layer in SketchLayout.Stack(layout))
            foreach (var s in layer.Shapes)
                if (s.Theme is { } tid && themes.TryGetValue(tid, out var theme)) shapeTheme[(layer.Id!, s.Id)] = theme;

        if (shapeTheme.Count == 0) return (_, _, _) => mapDefault;

        var cellToShape = SketchRasterizer.ShapeThemeOwners(layoutJson);
        return (layer, x, z) => cellToShape.TryGetValue((layer, x, z), out var shapeId)
            && shapeTheme.TryGetValue((layer, shapeId), out var theme) ? theme : mapDefault;
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
