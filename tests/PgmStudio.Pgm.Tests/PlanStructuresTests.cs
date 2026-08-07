using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Plan;

namespace PgmStudio.Pgm.Tests;

/// <summary>
/// The plan compiler's structure directives (docs/generator/rules.md ST1–ST4): wool-room bedrock
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
        // the orbit image lands one row inside the mirrored room too: a block at index c maps to −c−1,
        // so the rot_180 row sits at z=−21 spanning the room's x — not one block off, outside the room.
        await Assert.That(s.RedstoneLines).Contains(new RedstoneLine(-10, -21, -1, -21));
    }

    [Test]
    public async Task Spawn_piece_iron_rides_the_spawn_and_loose_iron_stays_a_directive()
    {
        var (_, intent) = PlanCompiler.Compile(PlanModel.Parse(Json)!);
        var s = intent.Structures!;
        // Iron on the spawn piece is absent from the directives — it rides the spawn intent and resolves
        // beside the room at export (WX8), fanned with its spawn to both teams.
        await Assert.That(s.IronCubes.Any(c => c.Renew)).IsFalse();
        await Assert.That(intent.Spawns.Count).IsEqualTo(2);
        await Assert.That(intent.Spawns.All(sp => sp.Iron.Count == 1)).IsTrue();
        await Assert.That(intent.Spawns.Any(sp => sp.Iron[0] is { X: 25, Z: 5 })).IsTrue();
        // Loose iron on 'far' keeps the legacy marker-anchored directive, never renewed.
        await Assert.That(s.IronCubes.Any(c => c is { X: 5, Z: 5, Renew: false })).IsTrue();
        await Assert.That(s.IronCubes.Count).IsEqualTo(2);   // the loose marker + its orbit image only
    }

    [Test]
    public async Task Wall_top_comes_from_the_attack_side_the_lower_farther_lane()
    {
        var s = Structures();
        // approach = 'far' (distance 2 to the wool) over 'mid' (distance 1); far surface 9 → three courses
        // of bedrock standing over it, so the last one is at 9+2=11 (the stamper caps that with cobweb).
        await Assert.That(s.Walls.All(w => w.TopY == 11)).IsTrue();
        // not the defence side 'mid' (surface 11 → would be 13).
        await Assert.That(s.Walls.Any(w => w.TopY == 13)).IsFalse();
        // 2 thick across the z-seam at z=10 (footprint z ∈ [9, 11)).
        await Assert.That(s.Walls.Any(w => w.MinZ == 9 && w.MaxZ == 11)).IsTrue();
    }

    [Test]
    public async Task The_authored_chest_side_names_a_piece_and_the_orbit_follows_it()
    {
        // side "a" = 'mid', which lies on the high-z side of the seam at z=10, so the team-0 wall opens its
        // max face. Its rot_180 image sits at negative z with 'mid' now on the LOW side, so that wall must
        // open its min face — the same piece, the other face. Reading a face off the footprint's coordinates
        // instead would open both walls toward opposite teams.
        var s = Structures();
        await Assert.That(s.Walls.Count).IsEqualTo(2);
        await Assert.That(s.Walls.Single(w => w.MinZ == 9).ChestOnMinFace).IsFalse();
        await Assert.That(s.Walls.Single(w => w.MinZ == -11).ChestOnMinFace).IsTrue();
    }

    [Test]
    public async Task Naming_the_other_side_opens_the_other_face_of_every_orbit_image()
    {
        var flipped = PlanModel.Parse(Json.Replace(
            """{ "a": "mid", "b": "far" }""",
            """{ "a": "mid", "b": "far", "side": "b" }"""))!;
        var (_, intent) = PlanCompiler.Compile(flipped);
        var walls = intent.Structures!.Walls;
        await Assert.That(walls.Single(w => w.MinZ == 9).ChestOnMinFace).IsTrue();
        await Assert.That(walls.Single(w => w.MinZ == -11).ChestOnMinFace).IsFalse();
    }
}
