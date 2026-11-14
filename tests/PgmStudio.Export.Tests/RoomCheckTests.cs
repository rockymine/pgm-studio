using PgmStudio.Domain;
using PgmStudio.Export;
using PgmStudio.Geom;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Plan;
using PgmStudio.Vocabulary;

namespace PgmStudio.Export.Tests;

/// <summary>
/// The rooms the export stamps, asked before it does: a red spawn on a 20-block region with a 12-block house and
/// a wool cage beside it frame cleanly, and each rule is made to fire by one change to them.
/// </summary>
public sealed class RoomCheckTests
{
    private static SpawnIntent Spawn() => new()
    {
        Id = "spawn-red", Team = "red", Stamp = new StampId("spawn", "0", 0), Doors = [new StatedDoor("+x")],
        Point = new Pt(10.5, 12, 10.5), Protection = [new Rect(0, 0, 20, 20)], House = RoomHouse.Over(new Rect(4, 4, 16, 16)),
    };

    private static WoolIntent Wool(string id, int image = 0) => new()
    {
        Id = id, Owner = "blue", Stamp = new StampId("wool", id, image), Spawn = new Pt(40.5, 12, 10.5),
        Protection = [new Rect(32, 2, 50, 18)], House = RoomHouse.Over(new Rect(35, 5, 47, 15)),
        Monuments = [new MonumentIntent { Team = "red", Location = new Pt(10, 12, 10) }],
    };

    private static MapIntent Clean() => new() { Spawns = [Spawn()], Wools = [Wool("wool-lime")] };

    private static IEnumerable<string> Rules(MapIntent intent) => RoomCheck.Check(intent, RoomShells.BuiltIn).Select(finding => finding.Rule);

    [Test]
    public async Task Rooms_that_frame_on_their_regions_break_nothing()
    {
        await Assert.That(Rules(Clean())).IsEmpty();
    }

    [Test]
    public async Task A_house_reaching_off_its_region_is_refused_and_named_by_its_room()
    {
        var off = Clean() with { Spawns = [Spawn() with { House = RoomHouse.Over(new Rect(12, 4, 24, 16)) }] };

        var found = RoomCheck.Check(off, RoomShells.BuiltIn).Single();

        await Assert.That(found.Rule).IsEqualTo(RoomFrameRules.FootprintOffPiece);
        await Assert.That(found.Message).StartsWith("the spawn room of 'spawn-red' has a footprint");
        await Assert.That(found.Subjects!).IsEquivalentTo(new[] { "spawn-red" });
    }

    [Test]
    public async Task A_wool_room_whose_stated_doors_name_no_wall_has_no_door()
    {
        var closed = Clean() with { Wools = [Wool("wool-lime") with { Doors = [new StatedDoor("up", 40, 8)] }] };

        await Assert.That(Rules(closed)).IsEquivalentTo(new[] { RoomFrameRules.RoomUnreachable });
    }

    [Test]
    public async Task Iron_with_no_place_for_its_cube_is_complained_of()
    {
        var stray = Clean() with { Spawns = [Spawn() with { Iron = [new Pt(30.5, 12, 10.5)] }] };

        await Assert.That(Rules(stray)).IsEquivalentTo(new[] { RoomFrameRules.IronFit });
    }

    [Test]
    public async Task A_spawn_room_seating_fewer_monuments_than_its_team_captures_is_refused()
    {
        var many = Clean() with { Wools = [.. Enumerable.Range(0, 40).Select(at => Wool($"wool-{at}"))] };

        await Assert.That(Rules(many)).Contains(PlanRules.MonumentSeats);
        await Assert.That(Rules(Clean())).DoesNotContain(PlanRules.MonumentSeats);
    }

    [Test]
    public async Task A_stated_seat_on_a_wall_or_a_chest_on_the_pad_is_refused_and_named_by_its_room()
    {
        var walled = Clean() with { Spawns = [Spawn() with { House = Spawn().House! with { Monuments = [[0, 3]] } }] };
        var padded = Clean() with { Wools = [Wool("wool-lime") with { House = Wool("wool-lime").House! with { Chests = [[1, 1], [5, 5]] } }] };

        var seat = RoomCheck.Check(walled, RoomShells.BuiltIn).Single();
        var chest = RoomCheck.Check(padded, RoomShells.BuiltIn).Single();

        await Assert.That(seat.Rule).IsEqualTo(RoomFrameRules.InteriorCellOutside);
        await Assert.That(seat.Message).StartsWith("the spawn room of 'spawn-red' states monument seat 0 at (0, 3)");
        await Assert.That(chest.Rule).IsEqualTo(RoomFrameRules.InteriorCellOutside);
        await Assert.That(chest.Subjects!).IsEquivalentTo(new[] { "wool-lime" });
    }

    [Test]
    public async Task A_house_stating_fewer_seats_than_its_team_captures_is_refused_by_its_own_rule()
    {
        var none = Clean() with { Spawns = [Spawn() with { House = Spawn().House! with { Monuments = [] } }] };

        await Assert.That(Rules(none)).IsEquivalentTo(new[] { RoomFrameRules.MonumentSeatsStated });
    }

    [Test]
    public async Task A_rooms_interior_is_answered_on_its_frame_with_the_wools_it_seats()
    {
        var stated = Clean() with { Spawns = [Spawn() with { House = Spawn().House! with { Monuments = [[2, 1], [1, 9]] } }] };

        var built = RoomCheck.Interior(stated, "spawn-red", RoomShells.BuiltIn)!;
        var derived = RoomCheck.Interior(Clean(), "wool-lime", RoomShells.BuiltIn)!;

        await Assert.That(built.Stated).IsTrue();
        await Assert.That(built.Seated).IsEqualTo(1);
        await Assert.That(built.Interior.Monuments.Select(seat => (seat.X, seat.Z))).IsEquivalentTo(new[] { (6, 5), (5, 13) });
        await Assert.That(derived.Stated).IsFalse();
        await Assert.That(derived.Interior.Chests.Count).IsEqualTo(4);
        await Assert.That(RoomCheck.Interior(Clean(), "spawn-green", RoomShells.BuiltIn)).IsNull();
    }

    [Test]
    public async Task An_image_the_symmetry_carried_is_not_asked_again()
    {
        var mirrored = Clean() with { Wools = [Wool("wool-lime"), Wool("wool-lime", image: 1) with { Doors = [new StatedDoor("up", 40, 8)] }] };

        await Assert.That(Rules(mirrored)).IsEmpty();
    }
}
