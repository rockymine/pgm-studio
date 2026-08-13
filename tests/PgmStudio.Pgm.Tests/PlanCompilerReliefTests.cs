using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Pgm.Tests;

/// <summary>
/// A spawn or wool-room piece binds into its island's relief solve (review.md MG5). The compiler states
/// <c>relief_scope: hold</c> on the structural annotation it projects for the authored orbit image, at the
/// piece's own surface — so a relief that later rolls across the island cannot cut the room's floor, even
/// though the annotation carries no ShapeIds membership of its own (S25 keeps it out of the terrain-ring
/// lists that assume one).
/// </summary>
public sealed class PlanCompilerReliefTests
{
    // A spawn piece abutting a large plain piece at the same surface, so the two fuse into one terrain
    // shape (the S25 case) big enough for a relief to roll across it. rot_180 about the origin, so the
    // whole authored half sits at positive x/z and its image lands at negative x/z untouched.
    private const string Json = """
        {
          "plan": 1,
          "globals": { "cell": 1, "symmetry": "rot_180", "surface": 8 },
          "pieces": [
            { "id": "ground", "role": "piece", "rect": [0, 0, 80, 60] },
            { "id": "sp",      "role": "spawn", "rect": [80, 20, 20, 20] }
          ],
          "placements": { "spawns": [ { "piece": "sp", "at": [10, 10], "facing": "front" } ] }
        }
        """;

    private static (SketchLayout Layout, string SpawnShapeId) Compile()
    {
        var (layout, intent) = PlanCompiler.Compile(PlanModel.Parse(Json)!);
        var team = intent.Teams![0].Id;
        return (layout, $"spawn-{team}");
    }

    [Test]
    public async Task The_authored_spawn_shape_states_hold_at_the_piece_surface()
    {
        var (layout, shapeId) = Compile();
        var shape = layout.Layout!.Shapes.Single(s => s.Id == shapeId);

        await Assert.That(shape.ReliefScope).IsEqualTo("hold");
        await Assert.That(shape.BaseHeight).IsEqualTo(8d);
    }

    // Attaches a relief to the compiled island that climbs from 6 in the near corner to 26 in the far one,
    // crossing straight through the spawn piece (x 80..100, z 20..40) were nothing holding it back.
    private static string WithRelief(SketchLayout layout)
    {
        var islandId = layout.Layout!.Islands.Single().Id!;
        layout.Relief = new Dictionary<string, SketchReliefJson>
        {
            [islandId] = new SketchReliefJson
            {
                Base = 8,
                Marks =
                [
                    new ReliefMarkJson { Kind = "point", At = [5, 5], Heights = [6], Radius = 3 },
                    new ReliefMarkJson { Kind = "point", At = [75, 55], Heights = [26], Radius = 3 },
                ],
            },
        };
        return layout.ToJson();
    }

    [Test]
    public async Task The_spawn_rooms_own_floor_stays_flat_at_its_surface_under_a_crossing_relief()
    {
        var (layout, _) = Compile();
        var withRelief = WithRelief(layout);

        var tops = new Dictionary<(int X, int Z), int>();
        foreach (var (x, z, _, top) in SketchRasterizer.RasterizeColumns(withRelief))
            if (!tops.TryGetValue((x, z), out var current) || top > current) tops[(x, z)] = top;

        var inRoom = new List<int>();
        for (var x = 81; x < 100; x++)
            for (var z = 21; z < 40; z++)
                if (tops.TryGetValue((x, z), out var top)) inRoom.Add(top);

        await Assert.That(inRoom).IsNotEmpty();
        await Assert.That(inRoom.Distinct().Single()).IsEqualTo(8);   // the room's own stated surface, unmoved
    }

    [Test]
    public async Task The_ground_outside_the_room_still_takes_the_relief()
    {
        var (layout, _) = Compile();
        var withRelief = WithRelief(layout);

        var tops = new Dictionary<(int X, int Z), int>();
        foreach (var (x, z, _, top) in SketchRasterizer.RasterizeColumns(withRelief))
            if (!tops.TryGetValue((x, z), out var current) || top > current) tops[(x, z)] = top;

        // Near the h=26 mark, well outside the room and inside the ground piece: the solved surface reaches
        // for the mark's height rather than sitting at the flat 8 the room is pinned to.
        await Assert.That(tops[(75, 50)]).IsGreaterThan(8);
    }

    [Test]
    public async Task Without_binding_the_shape_the_solve_would_have_cut_through_the_room()
    {
        // The counterfactual: the same relief over the same footprint, but with the authored shape's
        // relief_scope stripped back to the default (inherit) — proving the room floor moving is really the
        // relief crossing it, and not some other invariant already holding it flat.
        var (layout, shapeId) = Compile();
        var shape = layout.Layout!.Shapes.Single(s => s.Id == shapeId);
        shape.ReliefScope = null;
        var withRelief = WithRelief(layout);

        var tops = new Dictionary<(int X, int Z), int>();
        foreach (var (x, z, _, top) in SketchRasterizer.RasterizeColumns(withRelief))
            if (!tops.TryGetValue((x, z), out var current) || top > current) tops[(x, z)] = top;

        var inRoom = new List<int>();
        for (var x = 81; x < 100; x++)
            for (var z = 21; z < 40; z++)
                if (tops.TryGetValue((x, z), out var top)) inRoom.Add(top);

        await Assert.That(inRoom.Distinct().Count()).IsGreaterThan(1);
    }
}
