using PgmStudio.Pgm.Plan;

namespace PgmStudio.Pgm.Tests;

/// <summary>
/// Where a spawn's player looks. The compiler does not fan the authored facing blindly: one aimed over the
/// void is turned onto the board the piece actually meets, and where it meets it on two sides that is the
/// corner between them. The redirect is resolved once on the authored (team-0) piece and fanned exactly like
/// the authored facing, so it reflects and rotates correctly with the orbit. The doors are a separate
/// question — <see cref="PieceDoorsTests"/> — and a facing that names no wall is exactly why.
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
    [Arguments("front-right", 225d)]
    [Arguments("back-right", 315d)]
    [Arguments("back-left", 45d)]
    [Arguments("front-left", 135d)]
    public async Task A_diagonal_facing_carries_a_45_degree_yaw(string facing, double yaw)
    {
        // A corner hall's player looks between its two exits, which is not a multiple of 90 and is why no
        // door can be read back off a yaw. The piece is open on both the walls each diagonal leans into, so
        // nothing is redirected and the authored word survives as the angle it names.
        var open = $$"""
        {
          "plan": 2,
          "globals": { "cell": 1, "symmetry": "rot_180" },
          "pieces": [
            { "id": "west",  "role": "piece", "rect": [0, 10, 10, 10] },
            { "id": "east",  "role": "piece", "rect": [20, 10, 10, 10] },
            { "id": "north", "role": "piece", "rect": [10, 0, 10, 10] },
            { "id": "south", "role": "piece", "rect": [10, 20, 10, 10] },
            { "id": "sp",    "role": "spawn", "rect": [10, 10, 10, 10] }
          ],
          "placements": { "spawns": [ { "piece": "sp", "at": [5, 5], "facing": "{{facing}}" } ] }
        }
        """;
        await Assert.That(RedYaw(open)).IsEqualTo(yaw);
    }

    [Test]
    public async Task A_facing_over_the_void_is_turned_onto_the_corner_where_two_walls_open()
    {
        // The piece meets the board on its east and south walls and nowhere else, and the player was aimed
        // north over the drop. Neither open wall is more the board than the other, so they are summed: the
        // player is turned onto the corner between the two doors they can actually leave by.
        var corner = """
        {
          "plan": 2,
          "globals": { "cell": 1, "symmetry": "rot_180" },
          "pieces": [
            { "id": "sp",    "role": "spawn", "rect": [0, 0, 20, 20] },
            { "id": "east",  "role": "piece", "rect": [20, 0, 10, 20] },
            { "id": "south", "role": "piece", "rect": [0, 20, 20, 10] }
          ],
          "placements": { "spawns": [ { "piece": "sp", "at": [10, 10], "facing": "front" } ] }
        }
        """;
        await Assert.That(RedYaw(corner)).IsEqualTo(315d);   // +x and +z summed — "back-right"
    }

    [Test]
    public async Task The_doors_the_compiler_derives_travel_on_the_intent()
    {
        // The exporter never re-derives a door from the yaw: a diagonal yaw names no wall, so the walls the
        // compiler cut are stated, in the order it cut them.
        var (_, intent) = PlanCompiler.Compile(PlanModel.Parse(Plan("front"))!);
        await Assert.That(intent.Spawns.Single(s => s.Team == "red").Doors).IsEquivalentTo(new[] { "-x" });
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
