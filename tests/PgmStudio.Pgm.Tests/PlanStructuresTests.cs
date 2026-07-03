using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Plan;

namespace PgmStudio.Pgm.Tests;

/// <summary>
/// The plan compiler's structure directives (docs/contracts/layout-rules.md ST1–ST4): wool-room bedrock
/// floors, entrance redstone rows, iron cubes with the spawn-piece renew flag, and approach walls whose top
/// height comes from the attack side (the wall pair member farther from the nearest wool marker). All in block
/// coordinates, fanned across the symmetry orbit.
/// </summary>
public sealed class PlanStructuresTests
{
    // A wool room (surface 13) over a two-step lane (mid 11, far 9); a wall on the mid|far elevation seam; a
    // separate spawn island carrying iron (renews) and a loose iron marker on 'far' (does not).
    private const string Json = """
        {
          "plan": 1,
          "globals": { "cell": 5, "symmetry": "rot_180", "surface": 9 },
          "pieces": [
            { "id": "wool", "role": "wool-room", "rect": [0, 4, 2, 2], "surface": 13 },
            { "id": "mid",  "role": "lane",      "rect": [0, 2, 2, 2], "surface": 11 },
            { "id": "far",  "role": "lane",      "rect": [0, 0, 2, 2] },
            { "id": "sp",   "role": "spawn",     "rect": [4, 0, 2, 2] }
          ],
          "placements": {
            "spawns": [ { "piece": "sp", "at": [1, 1], "facing": "front" } ],
            "wools":  [ { "piece": "wool", "at": [1, 1] } ],
            "iron":   [ { "piece": "sp", "at": [1, 1] }, { "piece": "far", "at": [1, 1] } ]
          },
          "walls": [ { "a": "mid", "b": "far" } ]
        }
        """;

    private static StructureIntent Structures()
    {
        var (_, intent) = PlanCompiler.Compile(PlanModel.Parse(Json)!);
        return intent.Structures!;
    }

    [Test]
    public async Task Wool_room_gets_a_bedrock_floor_footprint()
    {
        var s = Structures();
        // team-0 room footprint = the wool piece rect in blocks, plus its rot_180 image.
        await Assert.That(s.RoomFloors).Contains(new Rect(0, 20, 10, 30));
        await Assert.That(s.RoomFloors).Contains(new Rect(-10, -30, 0, -20));
    }

    [Test]
    public async Task Entrance_redstone_lies_one_row_inside_the_room_along_the_seam()
    {
        var s = Structures();
        // seam at z=20 (wool's front), the last room row; ends where the torches sit.
        await Assert.That(s.RedstoneLines).Contains(new RedstoneLine(0, 20, 9, 20));
    }

    [Test]
    public async Task Iron_cube_renews_only_inside_a_spawn_piece()
    {
        var s = Structures();
        await Assert.That(s.IronCubes.Any(c => c is { X: 25, Z: 5, Renew: true })).IsTrue();   // on the spawn piece
        await Assert.That(s.IronCubes.Any(c => c is { X: 5, Z: 5, Renew: false })).IsTrue();   // loose on 'far'
        // both fanned to the opposite team (2 renew images total).
        await Assert.That(s.IronCubes.Count(c => c.Renew)).IsEqualTo(2);
    }

    [Test]
    public async Task Wall_top_comes_from_the_attack_side_the_lower_farther_lane()
    {
        var s = Structures();
        // approach = 'far' (distance 2 to the wool) over 'mid' (distance 1); far surface 9 → top 9+4=13.
        await Assert.That(s.Walls.All(w => w.TopY == 13)).IsTrue();
        // not the defence side 'mid' (surface 11 → would be 15).
        await Assert.That(s.Walls.Any(w => w.TopY == 15)).IsFalse();
        // 2 thick across the z-seam at z=10 (footprint z ∈ [9, 11)).
        await Assert.That(s.Walls.Any(w => w.MinZ == 9 && w.MaxZ == 11)).IsTrue();
    }
}
