using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Pgm.Tests;

/// <summary>
/// A spawn or wool-room piece stands out of its island's modifier stack: the room the compiler projects for the
/// authored orbit image keeps the plan's surface however the stack reshapes the ground round it, though the
/// annotation carries no ShapeIds membership of its own (it is kept out of the terrain-ring lists that assume
/// one).
/// </summary>
public sealed class PlanCompilerReliefTests
{
    // A spawn piece abutting a large plain piece at the same surface, so the two fuse into one terrain shape
    // big enough for a modifier to move. rot_180 about the origin, so the whole authored half sits at positive
    // x/z and its image lands at negative x/z untouched.
    private const string Json = """
        {
          "plan": 2,
          "globals": { "cell": 1, "symmetry": "rot_180", "surface": 8 },
          "pieces": [
            { "id": "ground", "role": "piece", "rect": [0, 0, 80, 60] },
            { "id": "sp",      "role": "spawn", "rect": [80, 20, 20, 20] }
          ],
          "placements": { "spawns": [ { "piece": "sp", "at": [10, 10], "facing": "front" } ] }
        }
        """;

    /// <summary>The compiled board as a world build reads it, with the rooms drawn in from the compiled
    /// intent.</summary>
    private static SketchLayout Compile()
    {
        var (layout, intent) = PlanCompiler.Compile(PlanModel.Parse(Json)!);
        return SketchLayout.Parse(RoomShapes.Project(layout.ToJson(), intent))!;
    }

    // A Hill over the whole board, twelve blocks at its deepest, crossing straight through the spawn piece
    // (x 80..100, z 20..40) were nothing holding it back.
    private static string WithHill(SketchLayout layout)
    {
        layout.Modifiers =
        [
            new SketchModifierJson
            {
                Id = "massif", Op = "mound", Rise = 12,
                Mask = new ModifierMaskJson
                {
                    Mirror = false,
                    Areas = [new MaskAreaJson
                    {
                        Kind = "polygon", Points = [[-60, -60], [160, -60], [160, 120], [-60, 120]],
                    }],
                },
            },
        ];
        return layout.ToJson();
    }

    private static Dictionary<(int X, int Z), int> Tops(string layoutJson)
    {
        var tops = new Dictionary<(int X, int Z), int>();
        foreach (var (x, z, _, top, _) in SketchRasterizer.RasterizeColumns(layoutJson))
            if (!tops.TryGetValue((x, z), out var current) || top > current) tops[(x, z)] = top;
        return tops;
    }

    [Test]
    public async Task The_spawn_rooms_floor_keeps_the_plan_surface_under_the_stack()
    {
        var tops = Tops(WithHill(Compile()));

        var inRoom = new List<int>();
        for (var x = 81; x < 100; x++)
            for (var z = 21; z < 40; z++)
                if (tops.TryGetValue((x, z), out var top)) inRoom.Add(top);

        await Assert.That(inRoom).IsNotEmpty();
        await Assert.That(inRoom.Distinct().Single()).IsEqualTo(8)
            .Because("a room is a room: one height across the whole floor, the plan's, whatever the hill does round it");
    }

    [Test]
    public async Task The_ground_outside_the_room_still_takes_the_stack()
    {
        var plain = Tops(Compile().ToJson());
        var hilled = Tops(WithHill(Compile()));

        await Assert.That(plain[(40, 30)]).IsEqualTo(8);
        await Assert.That(hilled[(40, 30)]).IsGreaterThan(8);
    }
}
