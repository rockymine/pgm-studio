using PgmStudio.Domain;
using PgmStudio.Pgm.Derive;
using PgmStudio.Pgm.Plan;

namespace PgmStudio.Pgm.Tests;

/// <summary>
/// Which walls a spawn hall opens through (WX6): the sides its piece meets more board on, capped at
/// <see cref="PieceDoors.SpawnMax"/>. A door that opens onto the void is a door nobody walks through, and a
/// hall with one on every wall is a crossroads.
/// </summary>
public sealed class PieceDoorsTests
{
    private static List<RoomEdge> Doors(string json, string facing = "front") =>
        PieceDoors.ForSpawn(ContactGraph.Build(PlanModel.Parse(json)!), "sp", facing);

    // A spawn in the board's north-west corner: 'east' abuts its +x wall, 'south' its +z wall, and the other
    // two sides are void. Widths differ so the ordering has something to sort by.
    private const string Corner = """
        {
          "plan": 2,
          "globals": { "cell": 1, "symmetry": "rot_180" },
          "pieces": [
            { "id": "sp",    "role": "spawn", "rect": [0, 0, 20, 20] },
            { "id": "east",  "role": "piece", "rect": [20, 0, 10, 14] },
            { "id": "south", "role": "piece", "rect": [0, 20, 8, 10] }
          ],
          "placements": { "spawns": [ { "piece": "sp", "at": [10, 10], "facing": "back-right" } ] }
        }
        """;

    [Test]
    public async Task A_corner_piece_earns_a_door_on_each_side_it_abuts()
    {
        // Both walls it meets the board on, widest first: 'east' shares 14 blocks and 'south' 8.
        await Assert.That(Doors(Corner)).IsEquivalentTo(new[] { RoomEdge.PosX, RoomEdge.PosZ });
    }

    [Test]
    public async Task A_piece_meeting_the_board_on_one_side_earns_one_door()
    {
        var oneSided = """
            {
              "plan": 2,
              "globals": { "cell": 1, "symmetry": "rot_180" },
              "pieces": [
                { "id": "sp",   "role": "spawn", "rect": [0, 0, 20, 20] },
                { "id": "east", "role": "piece", "rect": [20, 0, 10, 20] }
              ],
              "placements": { "spawns": [ { "piece": "sp", "at": [10, 10], "facing": "front" } ] }
            }
            """;
        await Assert.That(Doors(oneSided)).IsEquivalentTo(new[] { RoomEdge.PosX });
    }

    [Test]
    public async Task A_piece_open_on_three_sides_is_still_cut_with_two()
    {
        // The cap is the point: a hall a team can be flanked through on every wall is not a hall. The two
        // widest win — 'east' at 20 and 'north' at 16, with 'south' at 6 left shut.
        var threeSided = """
            {
              "plan": 2,
              "globals": { "cell": 1, "symmetry": "rot_180" },
              "pieces": [
                { "id": "sp",    "role": "spawn", "rect": [0, 20, 20, 20] },
                { "id": "east",  "role": "piece", "rect": [20, 20, 10, 20] },
                { "id": "north", "role": "piece", "rect": [0, 4, 16, 16] },
                { "id": "south", "role": "piece", "rect": [0, 40, 6, 10] }
              ],
              "placements": { "spawns": [ { "piece": "sp", "at": [10, 10], "facing": "front" } ] }
            }
            """;
        var doors = Doors(threeSided);
        await Assert.That(doors.Count).IsEqualTo(PieceDoors.SpawnMax);
        await Assert.That(doors).IsEquivalentTo(new[] { RoomEdge.PosX, RoomEdge.NegZ });
    }

    [Test]
    public async Task An_island_piece_still_opens_on_the_wall_its_player_looks_at()
    {
        // Nothing abuts it, so no wall is more the board than another; the facing is all that is left to go
        // on, and a hall with no door at all is worse than one opening over the drop.
        var island = """
            {
              "plan": 2,
              "globals": { "cell": 1, "symmetry": "rot_180" },
              "pieces": [ { "id": "sp", "role": "spawn", "rect": [0, 0, 20, 20] } ],
              "placements": { "spawns": [ { "piece": "sp", "at": [10, 10], "facing": "back" } ] }
            }
            """;
        await Assert.That(Doors(island, "back")).IsEquivalentTo(new[] { RoomEdge.PosZ });
    }

    [Test]
    public async Task The_door_the_player_walks_out_of_is_cut_first()
    {
        // Two walls open equally wide, so the tie falls to the facing: whichever the player looks more nearly
        // at is Doors[0], which is the one the chests, the monuments and the iron cube all read.
        var square = """
            {
              "plan": 2,
              "globals": { "cell": 1, "symmetry": "rot_180" },
              "pieces": [
                { "id": "sp",    "role": "spawn", "rect": [0, 0, 20, 20] },
                { "id": "east",  "role": "piece", "rect": [20, 0, 10, 20] },
                { "id": "south", "role": "piece", "rect": [0, 20, 20, 10] }
              ],
              "placements": { "spawns": [ { "piece": "sp", "at": [10, 10], "facing": "right" } ] }
            }
            """;
        await Assert.That(Doors(square, "right")[0]).IsEqualTo(RoomEdge.PosX);
        await Assert.That(Doors(square, "back")[0]).IsEqualTo(RoomEdge.PosZ);
    }
}
