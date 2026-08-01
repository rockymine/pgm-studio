using PgmStudio.Api.Services;
using PgmStudio.Minecraft;
using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Api.Tests;

/// <summary>
/// Scoped theme resolution for the export (docs/world-export/terrain-painting.md TP10): <see cref="TerrainThemeScope"/>
/// turns the plan-baked intent fields into a per-cell theme — a scoped piece paints its theme, everything else
/// the map default, and the smaller footprint wins an overlap. Pure (no DB): themes are built from real theme
/// JSON and read back through their fill material.
/// </summary>
public sealed class TerrainThemeScopeTests
{
    private static string FillTheme(int id) => TerrainThemeJson.Serialize(TerrainTheme.Default with { Fill = new SolidMaterial(id) });
    private static int FillId(TerrainTheme theme) => theme.Fill.Resolve(new BucketContext(0, 0, 0, TerrainBucket.Fill, 0)).Id;

    [Test]
    public async Task No_scoping_resolves_the_builtin_default_everywhere()
    {
        var at = TerrainThemeScope.ThemeAt(new MapIntent());
        await Assert.That(at(3, 4)).IsEqualTo(TerrainTheme.Default);
        await Assert.That(at(-99, 99)).IsEqualTo(TerrainTheme.Default);
    }

    [Test]
    public async Task A_scoped_piece_paints_its_theme_and_the_rest_the_map_default()
    {
        var intent = new MapIntent
        {
            Themes = { ["map"] = FillTheme(100), ["red"] = FillTheme(200) },
            MapTheme = "map",
            PieceThemes = { ["p1"] = "red" },
            PieceFootprints =
            {
                new PieceFootprint("p1", new Rect(0, 0, 4, 4)),
                new PieceFootprint("p2", new Rect(10, 10, 14, 14)),   // present but unthemed
            },
        };
        var at = TerrainThemeScope.ThemeAt(intent);
        await Assert.That(FillId(at(2, 2))).IsEqualTo(200);     // inside p1 → red theme
        await Assert.That(FillId(at(12, 12))).IsEqualTo(100);   // inside p2, no scope → map default
        await Assert.That(FillId(at(50, 50))).IsEqualTo(100);   // no piece → map default
    }

    [Test]
    public async Task Smallest_footprint_wins_an_overlapped_cell()
    {
        var intent = new MapIntent
        {
            Themes = { ["big"] = FillTheme(400), ["small"] = FillTheme(300) },
            PieceThemes = { ["big"] = "big", ["small"] = "small" },
            PieceFootprints =
            {
                new PieceFootprint("big", new Rect(0, 0, 10, 10)),
                new PieceFootprint("small", new Rect(1, 1, 3, 3)),
            },
        };
        var at = TerrainThemeScope.ThemeAt(intent);
        await Assert.That(FillId(at(2, 2))).IsEqualTo(300);   // in both → the smaller (more specific) piece
        await Assert.That(FillId(at(6, 6))).IsEqualTo(400);   // only the big piece
    }

    [Test]
    public async Task An_unknown_map_theme_id_falls_back_to_the_builtin_default()
    {
        var intent = new MapIntent { MapTheme = "missing" };
        await Assert.That(TerrainThemeScope.ThemeAt(intent)(0, 0)).IsEqualTo(TerrainTheme.Default);
    }
}
