using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Pgm.Tests.Sketch;

/// <summary>
/// A shape leaving its island's relief. The island is the unit a relief is solved over, because a relief
/// solved per shape leaves a seam wherever two of them meet and disagree about the height they share — but
/// the fusion is not always what an author wants, and the case that decides it is a built thing standing on
/// the ground, whose floor is not terrain.
/// </summary>
public sealed class SketchReliefScopeTests
{
    // A 60×60 island whose relief climbs from 6 in one corner to 26 in the other, with a 16×16 compound
    // sitting in the middle of that slope. The compound's own stated top is 14.
    private static string Layout(string? scope) => $$"""
    {
      "layers": [{ "id": "ground", "base_y": 0, "layout": {
        "shapes": [
          { "id": "land", "type": "rectangle", "operation": "add",
            "min_x": 0, "min_z": 0, "max_x": 60, "max_z": 60, "base_height": 5, "floor": 0 },
          { "id": "town", "type": "rectangle", "operation": "add",
            "min_x": 22, "min_z": 22, "max_x": 38, "max_z": 38, "base_height": 14, "floor": 0
            {{(scope is null ? "" : $", \"relief_scope\": \"{scope}\"")}} }
        ],
        "groups": [ { "id": "i1", "mirrors": false, "shapeIds": ["land", "town"] } ]
      } }],
      "relief": {
        "i1": {
          "base": 6,
          "marks": [
            { "kind": "point", "at": [3, 3], "h": 6, "r": 3 },
            { "kind": "point", "at": [57, 57], "h": 26, "r": 3 }
          ]
        }
      }
    }
    """;

    private static Dictionary<(int X, int Z), int> Tops(string layoutJson)
    {
        var tops = new Dictionary<(int X, int Z), int>();
        foreach (var (x, z, _, top, _) in SketchRasterizer.RasterizeColumns(layoutJson))
            if (!tops.TryGetValue((x, z), out var current) || top > current) tops[(x, z)] = top;
        return tops;
    }

    // How much the ground varies across the compound's footprint — the seam a fused solve puts under a wall.
    private static int SpreadAcross(Dictionary<(int X, int Z), int> tops)
    {
        var inside = new List<int>();
        for (var x = 23; x < 38; x++)
            for (var z = 23; z < 38; z++)
                if (tops.TryGetValue((x, z), out var top)) inside.Add(top);
        return inside.Max() - inside.Min();
    }

    [Test]
    public async Task Without_the_word_a_shape_is_part_of_the_island_and_the_field_rolls_through_it()
    {
        // The default has to stay the default: a shape drawn to make a landmass is part of the landmass, and
        // the slope crossing it keeps crossing it — 3 blocks of it over these 15 cells.
        var tops = Tops(Layout(null));

        await Assert.That(SpreadAcross(tops)).IsGreaterThan(0);
    }

    [Test]
    public async Task A_held_shape_is_flat_at_the_height_it_states()
    {
        // A city floor is a statement about the map, not a suggestion the terrain averages against.
        var tops = Tops(Layout("hold"));

        await Assert.That(SpreadAcross(tops)).IsEqualTo(0);
        await Assert.That(tops[(30, 30)]).IsEqualTo(14);
    }

    [Test]
    public async Task A_held_shape_moves_the_ground_around_it()
    {
        // What separates holding from excluding is whether the shape's height reaches the land: a hold pins
        // it, so the whole surrounding surface is solved knowing where it has to arrive.
        var fused = Tops(Layout(null));
        var held  = Tops(Layout("hold"));

        var moved = 0;
        for (var x = 0; x < 60; x++)
            for (var z = 0; z < 60; z++)
            {
                var outside = x is < 22 or > 38 || z is < 22 or > 38;
                if (outside && fused.TryGetValue((x, z), out var before)
                            && held.TryGetValue((x, z), out var after) && before != after) moved++;
            }

        await Assert.That(moved).IsGreaterThan(0);
    }

    [Test]
    public async Task An_excluded_shape_keeps_its_own_height_and_leaves_the_land_alone()
    {
        // A citadel on a plinth: it pins nothing, so the land is the land that outline would have produced
        // whatever height the shape is finally stamped at.
        var tops = Tops(Layout("exclude"));

        await Assert.That(SpreadAcross(tops)).IsEqualTo(0);
        await Assert.That(tops[(30, 30)]).IsEqualTo(14);
    }

    [Test]
    public async Task Holding_and_excluding_leave_different_land_around_them()
    {
        // Both are boundaries and the surrounding field differs under either, because a hole has an outline
        // the relaxation must bend around. The difference is whether the height reaches the ground.
        var held     = Tops(Layout("hold"));
        var excluded = Tops(Layout("exclude"));

        var differing = 0;
        for (var x = 0; x < 60; x++)
            for (var z = 0; z < 60; z++)
            {
                var outside = x is < 22 or > 38 || z is < 22 or > 38;
                if (outside && held.TryGetValue((x, z), out var a)
                            && excluded.TryGetValue((x, z), out var b) && a != b) differing++;
            }

        await Assert.That(differing).IsGreaterThan(0);
    }

    [Test]
    public async Task An_erected_shape_does_not_get_a_say()
    {
        // A raise reads the ground under its own footprint to know where to stand, so excluding that footprint
        // would leave it reading its own plate — the word is ignored rather than honoured into a wrong answer.
        const string Raised = """
        {
          "layers": [{ "id": "ground", "base_y": 0, "layout": {
            "shapes": [
              { "id": "land", "type": "rectangle", "operation": "add",
                "min_x": 0, "min_z": 0, "max_x": 60, "max_z": 60, "base_height": 5, "floor": 0 },
              { "id": "rock", "type": "rectangle", "operation": "add",
                "min_x": 22, "min_z": 22, "max_x": 38, "max_z": 38, "base_height": 8, "floor": 0,
                "height_mode": "raise", "relief_scope": "SCOPE" }
            ],
            "groups": [ { "id": "i1", "mirrors": false, "shapeIds": ["land", "rock"] } ]
          } }],
          "relief": { "i1": { "base": 6, "marks": [
              { "kind": "point", "at": [3, 3], "h": 6, "r": 3 },
              { "kind": "point", "at": [57, 57], "h": 26, "r": 3 } ] } }
        }
        """;

        var inherit  = Tops(Raised.Replace("SCOPE", "inherit"));
        var excluded = Tops(Raised.Replace("SCOPE", "exclude"));

        await Assert.That(excluded[(30, 30)]).IsEqualTo(inherit[(30, 30)]);
    }

    [Test]
    public async Task An_erected_shape_stands_on_both_sides_of_a_mirror()
    {
        // The image gets the same passes the primary got. Read back through the mirror instead, an erected
        // shape would take the relief's answer for its height — one team a mesa and the other a hillside.
        const string Mirrored = """
        {
          "setup": { "mirror_mode": "mirror_x", "center": { "cx": 50, "cz": 50 } },
          "layers": [{ "id": "ground", "base_y": 0, "layout": {
            "shapes": [
              { "id": "land", "type": "rectangle", "operation": "add",
                "min_x": 4, "min_z": 20, "max_x": 46, "max_z": 80, "base_height": 5, "floor": 0 },
              { "id": "mesa", "type": "rectangle", "operation": "add",
                "min_x": 14, "min_z": 40, "max_x": 30, "max_z": 60,
                "base_height": 24, "floor": 0, "height_mode": "level" }
            ],
            "groups": [ { "id": "i1", "mirrors": true, "shapeIds": ["land", "mesa"] } ]
          } }],
          "relief": { "i1": { "base": 6, "marks": [
              { "kind": "point", "at": [8, 24], "h": 6, "r": 3 },
              { "kind": "point", "at": [44, 76], "h": 18, "r": 3 } ] } }
        }
        """;

        var tops = Tops(Mirrored);

        await Assert.That(tops[(22, 50)]).IsEqualTo(24);
        await Assert.That(tops[(99 - 22, 50)]).IsEqualTo(24);
    }
}
