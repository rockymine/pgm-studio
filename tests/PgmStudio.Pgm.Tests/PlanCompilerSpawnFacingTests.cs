using PgmStudio.Pgm.Plan;

namespace PgmStudio.Pgm.Tests;

/// <summary>
/// A spawn's yaw decides which wall its door is cut through (review.md MG4). The compiler no longer fans the
/// authored facing blindly: on a piece whose stated door wall opens onto the void, the door is redirected to
/// a wall that actually opens onto more board. The redirect is resolved once on the authored (team-0) piece
/// and fanned exactly like the authored facing, so it reflects and rotates correctly with the orbit.
/// </summary>
public sealed class PlanCompilerSpawnFacingTests
{
    private static double RedYaw(string json)
    {
        var (_, intent) = PlanCompiler.Compile(PlanModel.Parse(json)!);
        return intent.Spawns.Single(s => s.Team == "red").Yaw;
    }

    // A spawn piece abutting a board piece on exactly one side (its west edge, x = 20) — the spawn's only
    // open wall is left (dx=-1, dz=0), which FacingDir maps to yaw 90.
    private static string Plan(string facing) => $$"""
        {
          "plan": 2,
          "globals": { "cell": 1, "symmetry": "rot_180" },
          "pieces": [
            { "id": "board", "role": "piece", "rect": [0, 0, 20, 20] },
            { "id": "sp",    "role": "spawn", "rect": [20, 5, 10, 10] }
          ],
          "placements": { "spawns": [ { "piece": "sp", "at": [5, 5], "facing": "{{facing}}" } ] }
        }
        """;

    [Test]
    public async Task A_door_authored_toward_the_void_is_redirected_to_the_open_wall()
    {
        // "front" is (0,-1) — north, straight over the void on every side of 'sp' but its west (board) wall.
        await Assert.That(RedYaw(Plan("front"))).IsEqualTo(90d);   // left/west, the only wall with ground beyond it
    }

    [Test]
    public async Task A_door_already_authored_toward_the_board_is_left_alone()
    {
        // "left" already opens onto 'board' — nothing to correct.
        await Assert.That(RedYaw(Plan("left"))).IsEqualTo(90d);
    }

    [Test]
    public async Task An_authored_door_on_a_piece_with_several_open_walls_keeps_the_authors_pick()
    {
        // A spawn piece boxed in on three sides: the authored facing lands on a real wall (east), so it is
        // kept even though a different open wall (north) also exists and sits earlier in the fallback's own
        // reading order — the redirect only overrides a facing that is actually void, not the author's
        // choice among several safe ones.
        var p = """
        {
          "plan": 2,
          "globals": { "cell": 1, "symmetry": "rot_180" },
          "pieces": [
            { "id": "west",  "role": "piece", "rect": [0, 5, 10, 10] },
            { "id": "east",  "role": "piece", "rect": [20, 5, 10, 10] },
            { "id": "north", "role": "piece", "rect": [10, 0, 10, 5] },
            { "id": "sp",    "role": "spawn", "rect": [10, 5, 10, 10] }
          ],
          "placements": { "spawns": [ { "piece": "sp", "at": [5, 5], "facing": "right" } ] }
        }
        """;
        // "right" (1,0) opens onto 'east' — yaw 270, the author's own pick, not the reading-order fallback
        // (which would have picked "front"/north, yaw 180).
        await Assert.That(RedYaw(p)).IsEqualTo(270d);
    }

    [Test]
    public async Task A_spawn_with_no_interface_at_all_keeps_the_authored_facing()
    {
        // An isolated spawn piece — no piece and no build zone touches it — has no safer wall to redirect to,
        // so the authored facing survives untouched.
        var p = """
        {
          "plan": 2,
          "globals": { "cell": 1, "symmetry": "rot_180" },
          "pieces": [ { "id": "sp", "role": "spawn", "rect": [0, 0, 10, 10] } ],
          "placements": { "spawns": [ { "piece": "sp", "at": [5, 5], "facing": "front" } ] }
        }
        """;
        await Assert.That(RedYaw(p)).IsEqualTo(180d);   // "front" on team-0, unmirrored
    }

    [Test]
    public async Task The_redirect_is_resolved_before_fanning_so_the_orbit_image_faces_its_own_board()
    {
        // The mirror image of 'sp' sits west of its own board piece, so its own open wall is EAST — the
        // opposite compass direction from red's — proving the fix is stated in the piece's local frame and
        // fanned, not written as a fixed world direction.
        var (_, intent) = PlanCompiler.Compile(PlanModel.Parse(Plan("front"))!);
        var blue = intent.Spawns.Single(s => s.Team == "blue");
        await Assert.That(blue.Yaw).IsEqualTo(270d);   // right/east — rot_180's image of red's west-facing door
    }
}
