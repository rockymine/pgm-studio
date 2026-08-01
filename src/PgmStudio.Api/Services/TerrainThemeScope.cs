using PgmStudio.Minecraft;
using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Api.Services;

/// <summary>
/// Scoped terrain-paint theme resolution for the world export (docs/world-export/terrain-painting.md TP10). It
/// turns the plan-baked theme data on the <see cref="MapIntent"/> — the theme-JSON registry, the map-default id,
/// the flat <c>pieceId → themeId</c> overrides, and the piece footprints — into a per-cell
/// <c>themeAt(x, z)</c> the painter reads. A cell in a scoped piece paints that piece's theme; every other cell
/// paints the map default; a piece whose footprints overlap another takes the smaller (most specific). This is
/// the read side of the same store the plan compiler and (later) the configure UI write, the sibling of
/// <see cref="TeamTerritory"/> for team ownership — one decomposition, resolved once, read straight through.
/// </summary>
public static class TerrainThemeScope
{
    /// <summary>The per-cell theme resolver for <see cref="TerrainPainter"/>. Returns a constant map-default
    /// resolver when no scoping is present (a hand-authored intent, or a plan with no themes), so the common
    /// path allocates nothing per cell and paints exactly as before.</summary>
    public static Func<int, int, TerrainTheme> ThemeAt(MapIntent intent)
    {
        var themes = new Dictionary<string, TerrainTheme>();
        foreach (var (id, json) in intent.Themes)
            themes[id] = TerrainThemeJson.Deserialize(json);

        var mapDefault = intent.MapTheme is { } mt && themes.TryGetValue(mt, out var mapTheme)
            ? mapTheme : TerrainTheme.Default;

        if (intent.PieceThemes.Count == 0 || intent.PieceFootprints.Count == 0)
            return (_, _) => mapDefault;

        var cellToPiece = BuildCellToPiece(intent.PieceFootprints);
        return (x, z) => cellToPiece.TryGetValue((x, z), out var pieceId)
            && intent.PieceThemes.TryGetValue(pieceId, out var themeId)
            && themes.TryGetValue(themeId, out var theme) ? theme : mapDefault;
    }

    // Map every footprint cell to its piece id; on overlap the smaller-area footprint (the more specific scope)
    // wins, so a piece drawn inside another's rect claims its own cells.
    private static Dictionary<(int X, int Z), string> BuildCellToPiece(List<PieceFootprint> footprints)
    {
        var pieceOf = new Dictionary<(int X, int Z), string>();
        var areaOf = new Dictionary<(int X, int Z), long>();
        foreach (var footprint in footprints)
        {
            var rect = footprint.Block;
            long area = (long)Math.Max(0, rect.MaxX - rect.MinX) * (long)Math.Max(0, rect.MaxZ - rect.MinZ);
            for (var x = (int)Math.Floor(rect.MinX); x < (int)Math.Ceiling(rect.MaxX); x++)
            for (var z = (int)Math.Floor(rect.MinZ); z < (int)Math.Ceiling(rect.MaxZ); z++)
            {
                var cell = (x, z);
                if (!pieceOf.ContainsKey(cell) || area < areaOf[cell])
                {
                    pieceOf[cell] = footprint.PieceId;
                    areaOf[cell] = area;
                }
            }
        }
        return pieceOf;
    }
}
