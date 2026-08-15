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
    private static readonly TerrainMaterial Grass = new LayeredMaterial(new BandStack([
        new Band(new SolidMaterial(2, 0), 1),
        new Band(new SolidMaterial(3, 0), 2)]));

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
            new LayeredMaterial(new BandStack([new Band(One("verdant"), 2), new Band(One("loam"), 3),
                                 new Band(One("grey stone"), 1)])), null),
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

    // ── the inward axis: bands read as rings in from the edge rather than courses down a column ────────
    //
    // A band stack states its bands and where they run out and deliberately not the axis, so the material
    // reading it names one (BandAxis). These plateaus are the same LayeredMaterial every other plateau uses,
    // pointed inward — which is the whole argument for stating the axis rather than minting a second type.

    /// <summary>The author's own sequence, as rings in from the outline: a rim, two rings of the next block, a
    /// checkered ring, five of a grass voronoi, and then <b>nothing claimed</b> — the surface takes over.
    ///
    /// <para><see cref="BandEnding.HandOver"/> is what makes the last part sayable. Under <c>Repeat</c> the
    /// voronoi would run to the middle of the island; handing over means the stack stops after ring 8 and
    /// <c>Beyond</c> answers, which here is the plain grass the rest of the map is finished with.</para></summary>
    private static TerrainMaterial Rings() =>
        new LayeredMaterial(
            new BandStack(
                [
                    new Band(new SolidMaterial(4, 0), 1),            // ring 0 — a cobble rim
                    new Band(new SolidMaterial(98, 0), 2),          // rings 1-2 — stone brick
                    new Band(new CheckerMaterial(1, Shade(Clay, 15), Shade(Clay, 0)), 1),   // ring 3
                    new Band(GrassVoronoi(), 5),                    // rings 4-8
                ],
                BandEnding.HandOver),                               // ring 9 in — the surface shows
            BandAxis.Inward,
            Beyond: new SolidMaterial(2, 0));

    /// <summary>Greens broken into cells, for the five rings that read as a field rather than as a course.
    /// A voronoi rather than plain grass so the band is visibly a <em>pattern</em> nested inside a band of a
    /// stack read along a different axis — every part of the model composing at once.</summary>
    private static TerrainMaterial GrassVoronoi() =>
        new VoronoiMaterial(11, 7,
            [new VoronoiBand(new SolidMaterial(2, 0), 1), new VoronoiBand(Shade(Clay, 5), 1),
             new VoronoiBand(Shade(Clay, 13), 1)], Rise: 8);

    /// <summary>A theme whose <b>top course</b> is ringed and whose courses below it are not. The two axes
    /// compose rather than competing: the surface material is an ordinary depth stack whose first band is one
    /// course thick and is itself the inward stack, with dirt under it. Restricting rings to the top course
    /// therefore needs no knob — and dirt below is what keeps the grass a single course (a palette that
    /// repeats grass down every course is the most-repeated authoring mistake in this repo).
    ///
    /// <para>The rim bucket is <b>off</b>, so the top course of an edge column falls to the surface (TP12) and
    /// the inward stack owns ring 0 itself. Left on, the rim would paint that ring first and the stack's own
    /// first band would never be seen.</para></summary>
    private static TerrainTheme RingTheme() => new()
    {
        Rim = new TopBand(One("ash", 2), 1, Enabled: false),
        Surface = new TopBand(
            new LayeredMaterial(new BandStack([new Band(Rings(), 1), new Band(new SolidMaterial(3, 0), 2)])),
            3, Enabled: true),
        Wall = One("grey stone"),
        WallEnabled = true,
        Fill = One("grey stone"),
    };

    /// <summary>The plateaus that show the inward axis. A disc first, because concentric rings on a round
    /// island are the shape the reading is easiest to see in; then a cross, where the walk has to turn both
    /// kinds of corner and every inner corner seeds ring 0 as surely as the outer ones do.</summary>
    private static IReadOnlyList<(string Name, string Kind, TerrainTheme Theme)> Ringed =>
    [
        ("inward rings — disc, rim to field", "circle", RingTheme()),
        ("inward rings — cross, both corners", "cross", RingTheme()),
    ];

    /// <summary>Each plateau's paint as the layout stores it: a named theme, serialised through the same
    /// writer the studio uses, so the export reads them back exactly as it reads a real map's.</summary>
    public static (List<(string Name, string Kind, string ThemeId)> Plateaus,
                   Dictionary<string, JsonElement> Themes) Themed()
    {
        var plateaus = new List<(string, string, string)>();
        var themes = new Dictionary<string, JsonElement>();

        // The pattern plateaus, then the inward-axis pair, then the house row — one island per house, plain
        // underfoot, since on that row the plateau is not the thing being shown.
        var all = (IReadOnlyList<(string Name, string Kind, TerrainTheme Theme)>)
        [
            .. All.Select(entry => (entry.Name, entry.Kind, new TerrainTheme
            {
                Rim = new TopBand(One("ash", 2), 1, Enabled: true),
                Surface = new TopBand(entry.Surface ?? Grass, 3, Enabled: true),
                Wall = entry.Wall,
                WallEnabled = true,
                Fill = One("grey stone"),
            })),
            .. Ringed,
            .. HousePresets.All.Select(house => (house.Name, "rectangle", new TerrainTheme
            {
                Rim = new TopBand(One("ash", 2), 1, Enabled: true),
                Surface = new TopBand(Grass, 3, Enabled: true),
                Wall = One("grey stone"),
                WallEnabled = true,
                Fill = One("grey stone"),
            })),
        ];
        for (var index = 0; index < all.Count; index++)
        {
            var (name, kind, theme) = all[index];
            var themeId = $"theme-{index}";
            themes[themeId] = JsonDocument.Parse(TerrainThemeJson.Serialize(theme)).RootElement.Clone();
            plateaus.Add((name, kind, themeId));
        }
        return (plateaus, themes);
    }
}
