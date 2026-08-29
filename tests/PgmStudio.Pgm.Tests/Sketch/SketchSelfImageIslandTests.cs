using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Pgm.Tests.Sketch;

/// <summary>
/// An island centred on the mirror is its own image, so the fan that copies it lands back on the cells it
/// came from. Between two groups the taller column wins, which is right for two islands meeting and wrong
/// here: an override add says the column is its own, floor and all, and a flight cut into a whole-board
/// island would be refilled by the reflection of the ground around it. The claim wins from either side.
/// </summary>
public sealed class SketchSelfImageIslandTests
{
    // A board-wide island with a flight cut into it: an override add starting at a lower floor, tilted from
    // one course at its foot to the island's full thickness at its head.
    private static string Board(bool mirrors) => $$"""
    {
      "setup": { "mirror_mode": "rot_180", "center": { "cx": 0, "cz": 0 } },
      "layers": [{ "id": "ground", "base_y": 0, "layout": {
        "shapes": [
          { "id": "land", "type": "rectangle", "operation": "add",
            "min_x": -40, "min_z": -40, "max_x": 40, "max_z": 40, "floor": 14, "base_height": 8 },
          { "id": "descent", "type": "polygon", "operation": "add", "override": true,
            "height_mode": "level", "skirt": 0, "floor": 8, "base_height": 14,
            "vertices": [[-22, 2], [-18, 2], [-18, 16], [-22, 16]],
            "anchor_heights": [1, 1, 14, 14] }
        ],
        "groups": [ { "id": "team", "mirrors": {{(mirrors ? "true" : "false")}},
                       "shapeIds": ["land", "descent"] } ]
      } }]
    }
    """;

    private static Dictionary<(int X, int Z), int> Tops(string json)
    {
        var tops = new Dictionary<(int X, int Z), int>();
        foreach (var (x, z, _, top, _) in SketchRasterizer.RasterizeColumns(json))
            if (!tops.TryGetValue((x, z), out var current) || top > current) tops[(x, z)] = top;
        return tops;
    }

    [Test]
    public async Task A_flight_cut_into_a_whole_board_island_survives_the_fan()
    {
        var fanned = Tops(Board(mirrors: true));
        var once = Tops(Board(mirrors: false));

        // Every course of the descent, at both ends and in the middle.
        foreach (var z in new[] { 3, 9, 15 })
            await Assert.That(fanned[(-20, z)]).IsEqualTo(once[(-20, z)]);

        // And it really is cut down — the island around it stands at its own top.
        await Assert.That(fanned[(-20, 3)]).IsLessThan(fanned[(0, 3)]);

        // The flight's own image is cut too, and to the same depth: the claim travels with the copy.
        // A cell's rot_180 image about the origin is (-1 - x, -1 - z) — a cell, not a point.
        await Assert.That(fanned[(19, -4)]).IsEqualTo(fanned[(-20, 3)]);
    }

    [Test]
    public async Task A_sunk_landform_in_a_whole_board_island_survives_the_fan()
    {
        // A `sink` is a plain add whose top is settled after the raster, so the reflection of the ground
        // around it carries a taller column than the hollow does. The settled top is a claim as much as an
        // override add's is.
        const string board = """
        {
          "setup": { "mirror_mode": "rot_180", "center": { "cx": 0, "cz": 0 } },
          "layers": [{ "id": "ground", "base_y": 0, "layout": {
            "shapes": [
              { "id": "land", "type": "rectangle", "operation": "add",
                "min_x": -40, "min_z": -40, "max_x": 40, "max_z": 40, "floor": 0, "base_height": 9 },
              { "id": "delve", "type": "rectangle", "operation": "add", "floor": 0, "base_height": 5,
                "height_mode": "sink", "skirt": 0,
                "min_x": -30, "min_z": 10, "max_x": -20, "max_z": 30 }
            ],
            "groups": [ { "id": "team", "mirrors": true, "shapeIds": ["land", "delve"] } ]
          } }]
        }
        """;
        var tops = Tops(board);
        await Assert.That(tops[(-25, 20)]).IsLessThan(tops[(0, 0)]);    // the hollow is a hollow
        await Assert.That(tops[(24, -21)]).IsEqualTo(tops[(-25, 20)]);  // and so is its image
    }

    [Test]
    public async Task An_island_that_is_not_its_own_image_is_still_fanned()
    {
        // Half a board, off the mirror centre: the copy lands on cells the primary never had, which is what
        // a fan is for. Without it the far half is bare.
        const string half = """
        {
          "setup": { "mirror_mode": "rot_180", "center": { "cx": 0, "cz": 0 } },
          "layers": [{ "id": "ground", "base_y": 0, "layout": {
            "shapes": [
              { "id": "land", "type": "rectangle", "operation": "add",
                "min_x": -40, "min_z": 10, "max_x": 40, "max_z": 40, "floor": 0, "base_height": 9 }
            ],
            "groups": [ { "id": "team", "mirrors": true, "shapeIds": ["land"] } ]
          } }]
        }
        """;
        var tops = Tops(half);
        await Assert.That(tops.ContainsKey((0, 20))).IsTrue();
        await Assert.That(tops.ContainsKey((0, -20))).IsTrue();   // the image
    }
}
