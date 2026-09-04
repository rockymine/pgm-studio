using PgmStudio.Domain;
using PgmStudio.Pgm.Plan;

namespace PgmStudio.Pgm.Tests;

/// <summary>
/// Which walls each team's spawn hall opens through once the authored unit has been fanned round the orbit.
/// A door is a <b>direction</b>, so it turns with the image the way the yaw, the region, the footprint and the
/// iron beside it do — a side named in the authored unit's frame is the wrong side on every other image, and
/// the stamper reads these words as they stand. Which walls the authored unit earns is
/// <see cref="PieceDoorsTests"/>'s question; this is what happens to them afterwards.
/// </summary>
public sealed class PlanCompilerSpawnDoorsTests
{
    private static Dictionary<string, string[]> DoorsByTeam(string json)
    {
        var (_, intent) = PlanCompiler.Compile(PlanModel.Parse(json)!);
        return intent.Spawns.ToDictionary(s => s.Team, s => s.Doors.ToArray(), StringComparer.Ordinal);
    }

    // A corner spawn on a board with an arm along each of its inner walls — Quatrefoil's shape, quarter-turn
    // symmetric about the origin. The authored corner is the north-west one; it meets the board on its +x
    // wall (the north arm) and its +z wall (the west arm), so those are the two the hall is cut with.
    private const string Quarters = """
        {
          "plan": 2,
          "globals": { "cell": 1, "symmetry": "rot_90" },
          "pieces": [
            { "id": "mid",   "role": "piece", "rect": [-20, -20, 40, 40] },
            { "id": "arm-x", "role": "piece", "rect": [-40, -20, 20, 40] },
            { "id": "arm-z", "role": "piece", "rect": [-20, -40, 40, 20] },
            { "id": "sp",    "role": "spawn", "rect": [-40, -40, 20, 20] }
          ],
          "placements": { "spawns": [ { "piece": "sp", "at": [10, 10], "facing": "back-right" } ] }
        }
        """;

    [Test]
    public async Task Each_quarter_turn_image_opens_toward_the_middle()
    {
        var doors = DoorsByTeam(Quarters);
        await Assert.That(doors.Count).IsEqualTo(4);

        // Every hall stands in its own corner and every one of them opens on the two walls that face the
        // middle. Read as a set: which of the two is cut first is PieceDoors' ordering, not this rule's.
        await Assert.That(doors["red"].Order(StringComparer.Ordinal)).IsEquivalentTo(new[] { "+x", "+z" });
        await Assert.That(doors["blue"].Order(StringComparer.Ordinal)).IsEquivalentTo(new[] { "+z", "-x" }.Order(StringComparer.Ordinal));
        await Assert.That(doors["yellow"].Order(StringComparer.Ordinal)).IsEquivalentTo(new[] { "-x", "-z" }.Order(StringComparer.Ordinal));
        await Assert.That(doors["green"].Order(StringComparer.Ordinal)).IsEquivalentTo(new[] { "-z", "+x" }.Order(StringComparer.Ordinal));
    }

    /// <summary>The invariant behind the four answers above, stated the way a reader can check it: a door's
    /// outward normal, taken from the hall's own centre, has to point at the board's middle. Yellow's is the
    /// case that failed — both its doors named the walls red's had, which on the far corner is the void.
    /// </summary>
    [Test]
    public async Task No_teams_door_opens_away_from_the_middle()
    {
        var (_, intent) = PlanCompiler.Compile(PlanModel.Parse(Quarters)!);
        foreach (var spawn in intent.Spawns)
        foreach (var word in spawn.Doors)
        {
            var (ox, oz) = RoomEdges.OfWord(word)!.Value.Outward();
            // The hall sits in a corner, so the step toward the middle is minus the sign of its own position.
            await Assert.That(ox * -Math.Sign(spawn.Point.X) + oz * -Math.Sign(spawn.Point.Z))
                .IsGreaterThan(0)
                .Because($"{spawn.Team}'s '{word}' door should face the middle from ({spawn.Point.X}, {spawn.Point.Z})");
        }
    }

    /// <summary>A two-team board mirrors rather than turns, and the same rule reads as the reflection: the
    /// authored hall opens north onto the board and its image opens south onto the same board.</summary>
    [Test]
    public async Task A_reflected_image_opens_on_the_reflected_wall()
    {
        var halves = """
            {
              "plan": 2,
              "globals": { "cell": 1, "symmetry": "rot_180" },
              "pieces": [
                { "id": "mid", "role": "piece", "rect": [-20, -10, 40, 20] },
                { "id": "sp",  "role": "spawn", "rect": [-10, -30, 20, 20] }
              ],
              "placements": { "spawns": [ { "piece": "sp", "at": [10, 10], "facing": "back" } ] }
            }
            """;
        var doors = DoorsByTeam(halves);
        await Assert.That(doors["red"]).IsEquivalentTo(new[] { "+z" });
        await Assert.That(doors["blue"]).IsEquivalentTo(new[] { "-z" });
    }
}
