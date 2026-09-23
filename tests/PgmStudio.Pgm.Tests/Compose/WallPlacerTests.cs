using PgmStudio.Domain;
using PgmStudio.Geom;
using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Tests.Compose;

/// <summary>The defence walls the composer seats: crossed rather than rounded, never against the room, on a
/// straight lane at <c>ST8</c>'s standoff, and across both legs of an approach that goes round a hole.</summary>
public sealed class WallPlacerTests
{
    private static readonly BoxRef WoolBox = new("wool-a", BoxKind.Wool);

    // a frontline, a hub, a straight lane six cells long and a room at its end, in one column on a 4-block grid
    private static GrownUnit Straight() => new(
        [
            new GrownPiece("frontline-t1", new CellRect(0, 0, 9, 3), Box: new BoxRef("frontline", BoxKind.Frontline)),
            new GrownPiece("hub-t1", new CellRect(0, 3, 9, 6), Box: new BoxRef("hub", BoxKind.Hub)),
            new GrownPiece("wool-a-t1", new CellRect(3, 9, 3, 6), Box: WoolBox),
            new GrownPiece("wool-a-room", new CellRect(3, 15, 3, 2), PlanRoles.WoolRoom, Box: WoolBox),
        ],
        new GrownSpawn("hub-t1", [1, 1], "front"),
        [new GrownWool("wool-a-room", [1, 1])]);

    [Test]
    public async Task A_straight_lane_is_cut_at_the_standoff_and_walled_across_the_cut()
    {
        var (unit, walls) = WallPlacer.Place(Straight(), cell: 4);

        await Assert.That(walls.Count).IsEqualTo(1);
        var outer = unit.Pieces.Single(p => p.Id == walls[0].Outer);
        var inner = unit.Pieces.Single(p => p.Id == walls[0].Inner);
        // the lane keeps its width on both sides of the wall, and the wall stands off the room by ST8's window
        await Assert.That(outer.Rect.Width).IsEqualTo(inner.Rect.Width);
        var standoff = (15 - inner.Rect.Z) * 4;
        await Assert.That(standoff).IsBetween(PlanValidator.WallStandoffMinBlocks, PlanValidator.WallStandoffMaxBlocks);
        // and on the lane, never at the hub's side, where the hub runs past both ends
        await Assert.That(outer.Id).IsNotEqualTo("hub-t1");
    }

    [Test]
    public async Task A_lane_too_short_for_the_standoff_is_left_unwalled()
    {
        var unit = Straight() with
        {
            Pieces =
            [
                .. Straight().Pieces.Where(p => p.Id is "frontline-t1" or "hub-t1"),
                new GrownPiece("wool-a-t1", new CellRect(3, 9, 3, 2), Box: WoolBox),
                new GrownPiece("wool-a-room", new CellRect(3, 11, 3, 2), PlanRoles.WoolRoom, Box: WoolBox),
            ],
        };
        await Assert.That(WallPlacer.Place(unit, cell: 4).Walls).IsEmpty();
    }

    private static readonly BoxRef DonutBox = new("wool-b", BoxKind.Wool);

    // a donut docked on the hub's right: an entry stub reaching past the entry bar beside the near leg, the two
    // legs, and the room at the far corner of the room bar
    private static GrownUnit Donut() => new(
        [
            new GrownPiece("frontline-t1", new CellRect(-4, 6, 8, 6), Box: new BoxRef("frontline", BoxKind.Frontline)),
            new GrownPiece("hub-t1", new CellRect(-4, 12, 9, 12), Box: new BoxRef("hub", BoxKind.Hub)),
            new GrownPiece("wool-b-t4", new CellRect(5, 14, 3, 5), Slot: ApproachSlots.Entry, Box: DonutBox),
            new GrownPiece("wool-b-t1", new CellRect(8, 16, 9, 3), Slot: ApproachSlots.EntryBar, Box: DonutBox),
            new GrownPiece("wool-b-t2", new CellRect(8, 12, 3, 4), Slot: ApproachSlots.Leg, Box: DonutBox),
            new GrownPiece("wool-b-t3", new CellRect(14, 12, 3, 4), Slot: ApproachSlots.Leg, Box: DonutBox),
            new GrownPiece("wool-b-t5", new CellRect(8, 9, 9, 3), Slot: ApproachSlots.RoomBar, Box: DonutBox),
            new GrownPiece("wool-b-room", new CellRect(17, 9, 2, 3), PlanRoles.WoolRoom, Box: DonutBox),
        ],
        new GrownSpawn("hub-t1", [1, 1], "front"),
        [new GrownWool("wool-b-room", [1, 1])]);

    [Test]
    public async Task A_donut_is_walled_across_each_leg_as_near_the_entry_bar_as_it_stands()
    {
        var (unit, walls) = WallPlacer.Place(Donut(), cell: 4);

        await Assert.That(walls.Count).IsEqualTo(2);
        var legs = walls.Select(w => w.Outer).OrderBy(id => id).ToList();
        await Assert.That(legs).IsEquivalentTo(new[] { "wool-b-t2", "wool-b-t3" });
        foreach (var wall in walls)
            await Assert.That(wall.Inner).IsEqualTo(wall.Outer + WallPlacer.InnerSuffix);
        // the far leg is clear of the stub, so its wall stands one cell off the entry bar
        var far = unit.Pieces.Single(p => p.Id == "wool-b-t3").Rect;
        await Assert.That((far.Z + far.Height, far.Height)).IsEqualTo((16, 1));
        // the near leg's last cells beside the stub would leave ground past the wall's end, so it stands deeper
        var near = unit.Pieces.Single(p => p.Id == "wool-b-t2").Rect;
        await Assert.That(near.Z).IsEqualTo(13);
    }

    [Test]
    public async Task Composed_walls_are_one_per_wool_or_two_round_a_hole_and_draw_no_wall_finding()
    {
        var walled = 0;
        var wools = 0;
        foreach (var players in new[] { 8, 20, 30 })
            for (ulong seed = 0; seed < 30; seed++)
            {
                var plan = Composer.Compose(new ComposeRequest(players, seed: seed));
                wools += plan.Placements.Wools.Count;
                var perWool = plan.Walls.GroupBy(w => string.Join("-", w.A.Split('-').Take(2))).ToList();
                walled += perWool.Count;
                foreach (var wool in perWool)
                    await Assert.That(wool.Count()).IsLessThanOrEqualTo(2).Because($"p{players} seed {seed} {wool.Key}");
                var findings = PlanValidator.Check(plan)
                    .Where(f => f.Rule is PlanRules.WallAtJunction or PlanRules.WallWithoutInterface or PlanRules.WallOnWoolRoom)
                    .Select(f => f.Message).ToList();
                await Assert.That(findings).IsEmpty().Because($"p{players} seed {seed}");
            }
        // every straight lane is built long enough to hold one, so all but a rare deep staple is walled
        await Assert.That(walled).IsGreaterThanOrEqualTo(wools * 95 / 100);
    }
}
