using System.Text.Json;
using PgmStudio.Minecraft;

namespace PatternMap;

/// <summary>
/// What each of the twenty-five plateaus is showing, and the theme that paints it.
///
/// <para>The themes are built as <see cref="TerrainTheme"/> objects and then serialised, because that is the
/// form a layout carries them in: a map stores named theme JSON and a shape names one, so the export resolves
/// a cell's paint through <c>TerrainThemeScope</c> exactly as a real map does. Building them in C# and
/// handing the painter a resolver would test the painter but not the wiring.</para>
/// </summary>
public static class Plateaus
{
    // Grass, podzol and mycelium carry a distinct top over dirt sides, so they read only where exactly one
    // course of them is seen from above. Stacked in a layer or turned side-on in a wall they repeat as banded
    // dirt — and they sit inside the verdant, loam and mauve families, so a pattern filled from a whole family
    // would scatter them down a riser. They come out here, once, for every caller.
    private static bool TopFaced(int id, int data) => (id, data) is (2, 0) or (3, 2) or (110, 0);

    private static IReadOnlyList<TerrainMaterial> Family(string name, int take = 99)
    {
        var family = TerrainPalette.Families.First(entry => entry.Name == name);
        return [.. family.Blocks.Where(block => !TopFaced(block.Id, block.Data)).Take(take)
                                .Select(block => (TerrainMaterial)new SolidMaterial(block.Id, block.Data))];
    }

    private static TerrainMaterial One(string family, int index = 0) => Family(family)[index];

    private static IReadOnlyList<WallStripe> Stripes(string family, int width, int take = 3) =>
        [.. Family(family, take).Select(material => new WallStripe(material, width))];

    // A shade out of one of the three sixteen-shade colour rows. Not a tone family: a family is a ground an
    // author reaches for, a shade row is one block in sixteen colours — which is what a pattern needs when its
    // two materials must read apart rather than read as one ground.
    private const int Clay = 159, Wool = 35, Glass = 95;
    private static TerrainMaterial Shade(int block, int colour) => new SolidMaterial(block, colour);

    // The area patterns, each used for both the ground and the riser. A rise makes the field
    // three-dimensional, so a wall carries the same fabric its surface does instead of streaking vertically.
    private static TerrainMaterial Voronoi() =>
        new VoronoiMaterial(1, 9, [.. Family("cobble").Select(m => new VoronoiBand(m, 1))], Rise: 8);
    private static TerrainMaterial Cells() => new CellMaterial(2, 10, 55, 4, Family("sand"), Rise: 8);
    private static TerrainMaterial Noise() => new NoiseMaterial(3, 14, 3, Family("verdant"), Rise: 8);
    private static TerrainMaterial Turbulence() => new TurbulenceMaterial(4, 14, 3, Family("rust"), Rise: 8);
    private static TerrainMaterial Electric() => new ElectricMaterial(5, 16, 3, Family("grey stone"), Rise: 8);

    private static TerrainMaterial Frame(int angle) =>
        new WallFrameMaterial(Shade(Clay, 15), Shade(Clay, 0), angle, Thickness: 1);

    /// <summary>A voronoi whose boundary band is one block — so it draws a clean grid of lines — and whose
    /// middle is handed to whatever is passed in. The grid stays legible because one material cannot vary.</summary>
    private static TerrainMaterial RimAround(TerrainMaterial inside) =>
        new VoronoiMaterial(7, 13, [new VoronoiBand(Shade(Clay, 15), 1), new VoronoiBand(inside, 1)], Rise: 8);

    /// <summary>One course of grass over dirt, and only one course — the only place a top-faced block reads.</summary>
    private static readonly TerrainMaterial Grass = new LayeredMaterial([
        new MaterialLayer(new SolidMaterial(2, 0), 1),
        new MaterialLayer(new SolidMaterial(3, 0), 2)]);

    /// <summary>The plateaus in grid order: what it shows, the shape it is drawn as, and its paint.</summary>
    public static IReadOnlyList<(string Name, string Kind, TerrainMaterial Wall, TerrainMaterial? Surface)> All =>
    [
        // area patterns — on the ground and down the riser both
        ("voronoi — cobble family",              "rectangle", Voronoi(), Voronoi()),
        ("cell — sand family",                   "rectangle", Cells(), Cells()),
        ("noise — verdant family",               "rectangle", Noise(), Noise()),
        ("turbulence — rust family",             "rectangle", Turbulence(), Turbulence()),
        ("electric — grey stone family",         "rectangle", Electric(), Electric()),

        // the plain kinds, and the board
        ("solid — grey stone",                   "rectangle", One("grey stone"), null),
        ("layered — verdant/loam/stone",         "rectangle",
            new LayeredMaterial([new MaterialLayer(One("verdant"), 2), new MaterialLayer(One("loam"), 3),
                                 new MaterialLayer(One("grey stone"), 1)]), null),
        ("team tint — clay by team",             "rectangle", new TeamTintedMaterial(Clay, One("ash")), null),
        ("checkerboard 1 — clay black/white",    "rectangle",
            new CheckerMaterial(1, Shade(Clay, 15), Shade(Clay, 0)),
            new CheckerMaterial(1, Shade(Clay, 15), Shade(Clay, 0))),
        ("checkerboard 3 — wool cyan/white",     "rectangle",
            new CheckerMaterial(3, Shade(Wool, 9), Shade(Wool, 0)),
            new CheckerMaterial(3, Shade(Wool, 9), Shade(Wool, 0))),

        // stripes along the wall, upright and sheared
        ("wall stripes — azure family",          "rectangle", new WallRunMaterial(Stripes("azure", 3)), null),
        ("diagonal slope 1 — clay yellow/black", "rectangle",
            new WallDiagonalMaterial([new WallStripe(Shade(Clay, 4), 2), new WallStripe(Shade(Clay, 15), 2)], 1), null),
        ("diagonal slope 2 — wool red/white",    "rectangle",
            new WallDiagonalMaterial([new WallStripe(Shade(Wool, 14), 3), new WallStripe(Shade(Wool, 0), 3)], 2), null),
        ("diagonal slope -1 — glass magenta/lime", "rectangle",
            new WallDiagonalMaterial([new WallStripe(Shade(Glass, 2), 2), new WallStripe(Shade(Glass, 5), 2)], -1), null),
        ("diagonal slope 3 — loam family",       "rectangle", new WallDiagonalMaterial(Stripes("loam", 3, 4), 3), null),

        // the frame, and what decides a corner
        ("frame 45 — rectangle, 4 corners",      "rectangle", Frame(45), null),
        ("frame 45 — disc, no corner",           "circle",    Frame(45), null),
        ("frame 45 — octagon, at threshold",     "octagon",   Frame(45), null),
        ("frame 45 — cross, in and out",         "cross",     Frame(45), null),
        ("frame 80 — only the sharpest",         "wedge",     Frame(80), null),

        // patterns stacked inside patterns: every band, stop, stripe and square is itself a material
        ("voronoi rim, noise inside",            "rectangle", RimAround(Noise()), RimAround(Noise())),
        ("voronoi rim, cells inside",            "rectangle", RimAround(Cells()), RimAround(Cells())),
        ("frame ink, turbulence panel",          "rectangle",
            new WallFrameMaterial(Shade(Clay, 15), Turbulence(), 45, Thickness: 1), null),
        ("checker: plain against voronoi",       "rectangle",
            new CheckerMaterial(6, Shade(Clay, 0), Voronoi()), new CheckerMaterial(6, Shade(Clay, 0), Voronoi())),
        ("stripes, each a different kind",       "rectangle",
            new WallRunMaterial([new WallStripe(Shade(Clay, 15), 2), new WallStripe(Noise(), 6),
                                 new WallStripe(Shade(Clay, 0), 2), new WallStripe(Turbulence(), 6)]), null),
    ];

    /// <summary>Each plateau's paint as the layout stores it: a named theme, serialised through the same
    /// writer the studio uses, so the export reads them back exactly as it reads a real map's.</summary>
    public static (List<(string Name, string Kind, string ThemeId)> Plateaus,
                   Dictionary<string, JsonElement> Themes) Themed()
    {
        var plateaus = new List<(string, string, string)>();
        var themes = new Dictionary<string, JsonElement>();

        // The pattern plateaus, then the house row after them — one island per house, plain underfoot, since
        // on that row the plateau is not the thing being shown.
        var all = (IReadOnlyList<(string Name, string Kind, TerrainMaterial Wall, TerrainMaterial? Surface)>)
            [.. All, .. HousePresets.All.Select(house =>
                (house.Name, "rectangle", One("grey stone"), (TerrainMaterial?)Grass))];
        for (var index = 0; index < all.Count; index++)
        {
            var (name, kind, wall, surface) = all[index];
            var themeId = $"theme-{index}";
            var theme = new TerrainTheme
            {
                Rim = new TopBand(One("ash", 2), 1, Enabled: true),
                Surface = new TopBand(surface ?? Grass, 3, Enabled: true),
                Wall = wall,
                WallEnabled = true,
                Fill = One("grey stone"),
            };
            themes[themeId] = JsonDocument.Parse(TerrainThemeJson.Serialize(theme)).RootElement.Clone();
            plateaus.Add((name, kind, themeId));
        }
        return (plateaus, themes);
    }
}
