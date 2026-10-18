using PgmStudio.Domain;
using PgmStudio.Geom;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Pgm.Tests.Sketch;

/// <summary>
/// A wool stating no door is given one, on the side of its region meeting the most ground, the side facing the
/// board's middle on a tie; a wool stating any keeps what it states. The region is 10 blocks square at x 20..30,
/// z −5..5, east of the middle.
/// </summary>
public sealed class WoolDoorsTests
{
    private static readonly Rect Region = new(20, -5, 30, 5);

    private static Func<(int X, int Z), bool> Strip(int minX, int minZ, int maxX, int maxZ) =>
        cell => cell.X >= minX && cell.X < maxX && cell.Z >= minZ && cell.Z < maxZ;

    [Test]
    public async Task The_side_meeting_the_most_ground_takes_the_door()
    {
        // A lane arrives along the +z side only; the middle lies to the −x.
        await Assert.That(WoolDoors.Side([Region], Strip(18, 5, 32, 30), (0, 0))).IsEqualTo(RoomEdge.PosZ);
    }

    [Test]
    public async Task A_tie_goes_to_the_side_facing_the_middle()
    {
        await Assert.That(WoolDoors.Side([Region], _ => false, (0, 0))).IsEqualTo(RoomEdge.NegX);
        await Assert.That(WoolDoors.Side([Region], Strip(0, -40, 60, 40), (0, 0))).IsEqualTo(RoomEdge.NegX)
            .Because("ground on every side is a tie the middle breaks");
    }

    [Test]
    public async Task A_wool_stating_a_door_keeps_it_and_one_stating_none_gets_one()
    {
        const string layout = """
            {"layers":[{"id":"l0","layout":{"shapes":[
              {"id":"lane","type":"rectangle","operation":"add","min_x":18,"min_z":5,"max_x":32,"max_z":30,"base_height":9}]}}]}
            """;
        var stated = new WoolIntent { Id = "stated", Protection = [Region], Doors = [StatedDoor.Centred(RoomEdge.NegZ)] };
        var bare = new WoolIntent { Id = "bare", Protection = [Region] };

        var settled = WoolDoors.Settled(new MapIntent { Wools = [stated, bare] }, layout).Wools!;

        await Assert.That(settled[0].Doors).IsEquivalentTo(stated.Doors);
        await Assert.That(settled[1].Doors).IsEquivalentTo(new[] { StatedDoor.Centred(RoomEdge.PosZ) });
    }
}
