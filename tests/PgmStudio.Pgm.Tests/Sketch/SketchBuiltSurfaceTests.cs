using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Pgm.Tests.Sketch;

/// <summary>
/// The surface the contour overlay draws for ground no relief solves: each group's highest built top, across
/// every layer. Anchor heights, a height mode and a layer's own <c>base_y</c> all shape ground without a
/// relief, and an overlay that only drew reliefs said nothing about any of them.
/// </summary>
public sealed class SketchBuiltSurfaceTests
{
    // A 30-wide ramp on the ground layer, its anchors rising from 8 in the west to 20 in the east, and a
    // 2-wide wall draped 1 over the ground running across it — on the ground layer, or on a layer of its own.
    private static string Layout(bool wallOnItsOwnLayer) => $$"""
    {
      "setup": { "mirror_mode": "none", "center": { "cx": 0, "cz": 0 } },
      "layers": [
        { "id": "ground", "base_y": 0, "layout": {
          "shapes": [
            { "id": "ramp", "type": "polygon", "operation": "add",
              "vertices": [[0, 0], [30, 0], [30, 10], [0, 10]], "anchor_heights": [8, 20, 20, 8],
              "base_height": 8, "floor": 0 }
            {{(wallOnItsOwnLayer ? "" : "," + Wall)}}
          ],
          "groups": [ { "id": "land", "mirrors": false, "shapeIds": ["ramp"] }
                      {{(wallOnItsOwnLayer ? "" : ", { \"id\": \"wall\", \"mirrors\": false, \"shapeIds\": [\"w\"] }")}} ]
        } }
        {{(wallOnItsOwnLayer ? ", { \"id\": \"walls\", \"base_y\": 8, \"layout\": { \"shapes\": [" + Wall + "], \"groups\": [ { \"id\": \"wall\", \"mirrors\": false, \"shapeIds\": [\"w\"] } ] } }" : "")}}
      ]
    }
    """;

    private const string Wall = """
        { "id": "w", "type": "rectangle", "operation": "add", "min_x": 2, "max_x": 28, "min_z": 4, "max_z": 5,
          "height_mode": "drape", "base_height": 1, "floor": 0 }
        """;

    private static readonly IReadOnlySet<string> NoneSolved = new HashSet<string>();

    [Test]
    public async Task Ground_shaped_by_anchor_heights_is_drawn_with_the_slope_it_builds()
    {
        var land = SketchRasterizer.BuiltSurfaces(Layout(wallOnItsOwnLayer: true), NoneSolved)["land"];

        await Assert.That(land.Max - land.Min).IsGreaterThanOrEqualTo(10);
    }

    [Test]
    public async Task A_draped_wall_on_its_own_layer_is_drawn_flat_because_it_builds_flat()
    {
        // Drape reads the ground of its own layer, and a layer of its own has none: the wall stands one block
        // over an empty layer at base_y 8, the whole way across a ramp that climbs twelve.
        var wall = SketchRasterizer.BuiltSurfaces(Layout(wallOnItsOwnLayer: true), NoneSolved)["wall"];

        await Assert.That(wall.Min).IsEqualTo(wall.Max);
    }

    [Test]
    public async Task A_draped_wall_on_the_ground_layer_is_drawn_climbing_the_ramp()
    {
        var wall = SketchRasterizer.BuiltSurfaces(Layout(wallOnItsOwnLayer: false), NoneSolved)["wall"];

        await Assert.That(wall.Max - wall.Min).IsGreaterThanOrEqualTo(10);
    }

    [Test]
    public async Task A_column_a_wall_stands_on_is_the_walls_and_not_the_grounds()
    {
        var surfaces = SketchRasterizer.BuiltSurfaces(Layout(wallOnItsOwnLayer: false), NoneSolved);

        await Assert.That(surfaces["wall"].Has(15, 4)).IsTrue();
        await Assert.That(surfaces["land"].Has(15, 4)).IsFalse();
        await Assert.That(surfaces["land"].Has(15, 8)).IsTrue();
    }

    [Test]
    public async Task A_group_a_relief_solves_is_left_to_the_relief()
    {
        var surfaces = SketchRasterizer.BuiltSurfaces(Layout(wallOnItsOwnLayer: true), new HashSet<string> { "land" });

        await Assert.That(surfaces.ContainsKey("land")).IsFalse();
        await Assert.That(surfaces.ContainsKey("wall")).IsTrue();
    }

    [Test]
    public async Task The_traced_surface_sits_half_a_block_above_each_top()
    {
        // Half a block up, a contour at a whole level crosses midway between a cell below it and one standing
        // at it — the edge of the step — rather than through the centre of the upper cell.
        var land = SketchRasterizer.BuiltSurfaces(Layout(wallOnItsOwnLayer: true), NoneSolved)["land"];
        var footprint = land.Footprint;
        foreach (var (x, z) in footprint.Land())
            await Assert.That(land.Continuous[footprint.Index(x, z)]).IsEqualTo(land.At(x, z) + 0.5);
    }
}
