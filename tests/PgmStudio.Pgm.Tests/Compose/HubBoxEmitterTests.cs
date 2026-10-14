using PgmStudio.Geom;
using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Tests.Compose;

/// <summary>The hub box kind: a room-free <see cref="Compound"/> (Rectangle · L · U · Ring · P · Double-hole)
/// finished by the hub designation — no room, hub-labeled pieces, publishing one per-edge <see cref="EdgeOffer"/>
/// as the constraint source (the offered widths the composer's per-edge constraint).</summary>
public class HubBoxEmitterTests
{
    private static readonly Box Box6x5 = new("hub", BoxKind.Hub, new(10, 20, 6, 5), 30);

    [Test]
    public async Task Rectangle_hub_fills_the_box_with_no_room_and_hub_labeled()
    {
        var box = Box6x5;
        var hub = HubBoxEmitter.Fill(box, CompoundForm.I, laneWidth: 2)!;

        // one solid terrain piece covering the whole box, no room/room, labeled to the hub box, Bar part
        await Assert.That(hub.Pieces.Count).IsEqualTo(1);
        await Assert.That(UnitPieces.Lay(hub, box).Pieces[0].Rect).IsEqualTo(new CellRect(10, 20, 6, 5));
        await Assert.That(UnitPieces.Lay(hub, box).Pieces.All(p => p.Box!.Kind == BoxKind.Hub)).IsTrue();
        await Assert.That(hub.Pieces.Any(p => p.Part == CompoundParts.Room)).IsFalse();
    }

    [Test]
    public async Task Rectangle_hub_publishes_one_offer_per_edge_severally()
    {
        var box = new Box("hub", BoxKind.Hub, new(0, 0, 6, 5), 30);
        var hub = HubBoxEmitter.Fill(box, CompoundForm.I, laneWidth: 2)!;

        await Assert.That(HubBoxEmitter.Offers(box, hub).Count).IsEqualTo(4);
        await Assert.That(HubBoxEmitter.Offers(box, hub).Select(o => o.Edge))
            .IsEquivalentTo(new[] { BoxEdge.Top, BoxEdge.Bottom, BoxEdge.Left, BoxEdge.Right });
        await Assert.That(HubBoxEmitter.Offers(box, hub).All(o => o.Grouping == OfferGrouping.Several)).IsTrue();
        // the interval is the whole free edge: top/bottom span the width (6), left/right the height (5)
        await Assert.That(HubBoxEmitter.Offers(box, hub).Single(o => o.Edge == BoxEdge.Top).Interval.Length).IsEqualTo(6);
        await Assert.That(HubBoxEmitter.Offers(box, hub).Single(o => o.Edge == BoxEdge.Left).Interval.Length).IsEqualTo(5);
        await Assert.That(HubBoxEmitter.Offers(box, hub).Select(o => o.GroupId).Distinct().Count()).IsEqualTo(4);
    }

    [Test]
    public async Task Every_run_offers_the_width_its_own_length_can_support()
    {
        var box = new Box("hub", BoxKind.Hub, new(0, 0, 8, 5), 40);
        var hub = HubBoxEmitter.Fill(box, CompoundForm.I,
            laneWidth: 2)!;

        // the hub publishes capacity, not a per-neighbour agreement: Top/Bottom span 8, Left/Right span 5. What a
        // given neighbour was granted rides on its joint (TeamUnitFillerTests), because one run can carry two.
        await Assert.That(HubBoxEmitter.Offers(box, hub).Single(o => o.Edge == BoxEdge.Top).Width).IsEqualTo(CompoundEdges.LaneWidthOn(8));
        await Assert.That(HubBoxEmitter.Offers(box, hub).Single(o => o.Edge == BoxEdge.Bottom).Width).IsEqualTo(CompoundEdges.LaneWidthOn(8));
        await Assert.That(HubBoxEmitter.Offers(box, hub).Single(o => o.Edge == BoxEdge.Left).Width).IsEqualTo(CompoundEdges.LaneWidthOn(5));
    }

    [Test]
    public async Task L_hub_is_a_spine_with_one_end_arm_its_side_wall_one_merged_run()
    {
        // spine [0,0,8,2] + one arm [0,2,2,4]; the left edge (spine corner + arm) merges to one full-height run
        var box = new Box("hub", BoxKind.Hub, new(0, 0, 8, 6), 48);
        var hub = HubBoxEmitter.Fill(box, CompoundForm.L, laneWidth: 2)!;

        await Assert.That(hub.Pieces.Count).IsEqualTo(2);
        await Assert.That(UnitPieces.Lay(hub, box).Pieces.All(p => p.Box!.Kind == BoxKind.Hub)).IsTrue();
        await Assert.That(HubBoxEmitter.Offers(box, hub).Count).IsEqualTo(4);                                     // top, bottom, left, right — one run each
        await Assert.That(HubBoxEmitter.Offers(box, hub).Single(o => o.Edge == BoxEdge.Left).Interval.Length).IsEqualTo(6);
    }

    [Test]
    public async Task U_hub_opens_a_bay_two_offers_on_the_open_edge()
    {
        // spine [0,0,8,2] + arms [0,2,2,4] and [6,2,2,4]; the bottom edge is the bay — two arm-tip runs with a gap
        var box = new Box("hub", BoxKind.Hub, new(0, 0, 8, 6), 48);
        var hub = HubBoxEmitter.Fill(box, CompoundForm.U, laneWidth: 2)!;

        await Assert.That(hub.Pieces.Count).IsEqualTo(3);
        await Assert.That(HubBoxEmitter.Offers(box, hub).Count(o => o.Edge == BoxEdge.Bottom)).IsEqualTo(2);
        await Assert.That(HubBoxEmitter.Offers(box, hub).Count(o => o.Edge == BoxEdge.Top)).IsEqualTo(1);
        await Assert.That(HubBoxEmitter.Offers(box, hub).Where(o => o.Edge == BoxEdge.Bottom).All(o => o.Interval.Length == 2)).IsTrue();
    }

    [Test]
    public async Task Ring_hub_walls_a_hole_and_offers_four_full_edges()
    {
        var box = new Box("hub", BoxKind.Hub, new(0, 0, 9, 9), 81);
        var hub = HubBoxEmitter.Fill(box, CompoundForm.Donut, laneWidth: 2)!;

        await Assert.That(hub.Pieces.Count).IsEqualTo(4);                                     // four ring bars
        // every outer edge is a solid wall → one full-length run each (corner + leg + corner merged)
        await Assert.That(HubBoxEmitter.Offers(box, hub).Count).IsEqualTo(4);
        await Assert.That(HubBoxEmitter.Offers(box, hub).All(o => o.Interval.Length == 9)).IsTrue();
    }

    [Test]
    public async Task DoubleHole_hub_builds_two_holes_hub_labeled()
    {
        var box = new Box("hub", BoxKind.Hub, new(0, 0, 12, 8), 96);
        var hub = HubBoxEmitter.Fill(box, CompoundForm.DoubleHole, laneWidth: 2)!;

        await Assert.That(hub.Pieces.Count).IsEqualTo(7);                                     // ring (4) + U (top/bottom arms + outer wall)
        await Assert.That(UnitPieces.Lay(hub, box).Pieces.All(p => p.Box!.Kind == BoxKind.Hub)).IsTrue();
        await Assert.That(HubBoxEmitter.Offers(box, hub).Count).IsGreaterThan(0);
    }

    [Test]
    public async Task Too_small_a_box_is_a_directed_null_not_a_throw()
    {
        // a 3x3 box cannot hold a ring at laneWidth 2 (needs 2cw+1 = 5 each way)
        await Assert.That(HubBoxEmitter.Fill(new Box("hub", BoxKind.Hub, new(0, 0, 3, 3), 9), CompoundForm.Donut, laneWidth: 2))
            .IsNull();
    }

    [Test]
    public async Task A_form_off_the_hub_menu_throws()
    {
        // TwoUOnI is a compound form but not a hub form (the hub menu is the compact + wide-holed compounds; P and
        // Double-hole are on it, TwoUOnI is not)
        await Assert.That(() => HubBoxEmitter.Fill(Box6x5, CompoundForm.TwoU, laneWidth: 2))
            .Throws<ComposeException>();
    }

    [Test]
    public async Task The_wide_holed_forms_fill_a_wide_box_and_fall_back_below_width_9()
    {
        // P (loop + overhanging bar), Double-hole (ring + full-height U) and G (ring + L) need width >= 9 at laneWidth 2;
        // on an 11x6 box all build — room-free, hub-labeled, each with an enclosed hole — and offer a long run
        var wide = new Box("hub", BoxKind.Hub, new(0, 0, 11, 6), 66);
        foreach (var form in new[] { CompoundForm.P, CompoundForm.DoubleHole, CompoundForm.G })
        {
            var box = wide;
            var hub = HubBoxEmitter.Fill(box, form, laneWidth: 2)!;
            await Assert.That(hub).IsNotNull().Because($"{form} should fill an 11x6 hub");
            await Assert.That(UnitPieces.Lay(hub, box).Pieces.All(p => p.Box!.Kind == BoxKind.Hub)).IsTrue();
            await Assert.That(hub.Pieces.Any(p => p.Part == CompoundParts.Room)).IsFalse();
            await Assert.That(HubBoxEmitter.Offers(box, hub).Max(o => o.Interval.Length)).IsGreaterThanOrEqualTo(6);
        }
        // below width 9 all three directed-null (the box is too small for the loop/ring), not throw
        await Assert.That(HubBoxEmitter.Fill(Box6x5, CompoundForm.P, laneWidth: 2)).IsNull();
        await Assert.That(HubBoxEmitter.Fill(Box6x5, CompoundForm.DoubleHole, laneWidth: 2)).IsNull();
        await Assert.That(HubBoxEmitter.Fill(Box6x5, CompoundForm.G, laneWidth: 2)).IsNull();
    }

    // ── the branch hub's leg sampler ──────────────────────────────────────────────────────────────────────

    private const int LaneWidth = 2;   // FillProfiles.HubLaneWidthFloor

    /// <summary>The leg laws, over the sampler's whole range on a spine wide enough to exercise it: every leg is
    /// at least a corridor and at most two and a half of one, and what is left beside it — the L's notch, the U's
    /// bay — is never under a corridor. Nothing is required to be even: a hub leg sits away from the symmetry
    /// axis, so unlike the frontline's face it has no parity to answer to.</summary>
    [Test]
    public async Task Sampled_hub_legs_keep_the_width_cap_and_leave_a_corridor_beside_them()
    {
        var widths = new HashSet<int>();
        foreach (var arms in new[] { 1, 2 })
            for (ulong seed = 0; seed < 400; seed++)
            {
                const int spine = 12;
                if (HubBoxEmitter.SampleArms(new ComposeRng(seed), spine, arms, LaneWidth) is not { } legs) continue;
                await Assert.That(legs.Count).IsEqualTo(arms);
                foreach (var (start, width) in legs)
                {
                    widths.Add(width);
                    await Assert.That(width).IsGreaterThanOrEqualTo(LaneWidth).Because("a leg is a corridor at least");
                    await Assert.That(width).IsLessThanOrEqualTo(5).Because("2.5 corridors is the cap at laneWidth 2");
                    await Assert.That(start).IsGreaterThanOrEqualTo(0);
                    await Assert.That(start + width).IsLessThanOrEqualTo(spine);
                }
                if (arms == 1)
                {
                    // the L's notch: a corridor at least, but free to run long — it is a corner recess, not a bay
                    await Assert.That(spine - legs[0].Width).IsGreaterThanOrEqualTo(LaneWidth);
                }
                else
                {
                    // the U's bay is bounded both ways, which is what forces a wide spine into the legs
                    var bay = legs[1].Start - (legs[0].Start + legs[0].Width);
                    await Assert.That(bay).IsGreaterThanOrEqualTo(LaneWidth);
                    await Assert.That(bay).IsLessThanOrEqualTo(2 * LaneWidth).Because($"a bay of {bay} exceeds two lanes");
                }
            }

        await Assert.That(widths).Contains(5).Because("the cap is reachable, not just an unused bound");
        await Assert.That(widths.Any(w => w % 2 != 0)).IsTrue().Because("leg widths need not be even");
    }

    /// <summary>The point of the sampler: a hub leg can be wider than the corridor, which is what makes the L
    /// read as an L on a small board rather than as a bar with a nub.</summary>
    [Test]
    public async Task A_hub_leg_can_emit_wider_than_one_corridor()
    {
        var box = new Box("hub", BoxKind.Hub, new(0, 0, 12, 5), 60);
        var wide = HubBoxEmitter.Fill(box, CompoundForm.L, LaneWidth,
            armLayout: [(0, 5)])!;
        var leg = wide.Pieces.Single(p => p.Part == CompoundParts.Leg);
        await Assert.That(leg.Rect.Width).IsEqualTo(5);

        // and with no layout given it stays the uniform default it always was
        var plain = HubBoxEmitter.Fill(box, CompoundForm.L, LaneWidth)!;
        await Assert.That(plain.Pieces.Single(p => p.Part == CompoundParts.Leg).Rect.Width).IsEqualTo(LaneWidth);
    }

    /// <summary>A spine with no room for a corridor beside the leg is refused rather than built with a sliver
    /// notch — the same size floor the sampler applies, enforced on the default layout too.</summary>
    [Test]
    public async Task A_spine_too_short_for_a_notch_refuses_the_branch_form()
    {
        // an L needs leg + notch = 2 corridors, a U needs legs + bay = 3
        await Assert.That(HubBoxEmitter.Fill(
            new Box("hub", BoxKind.Hub, new(0, 0, 3, 5), 15), CompoundForm.L, LaneWidth)).IsNull();
        await Assert.That(HubBoxEmitter.Fill(
            new Box("hub", BoxKind.Hub, new(0, 0, 5, 5), 25), CompoundForm.U, LaneWidth)).IsNull();
        await Assert.That(HubBoxEmitter.Fill(
            new Box("hub", BoxKind.Hub, new(0, 0, 4, 5), 20), CompoundForm.L, LaneWidth)).IsNotNull();
    }

    /// <summary>The point of bounding the bay above: on a spine too wide for two corridor legs and a small gap,
    /// the legs are what must absorb the span, so a big box cannot come out as two stubs either side of a chasm.
    /// This is what lets the two-legged hub onto the larger boards at all.</summary>
    [Test]
    public async Task A_wide_spine_forces_at_least_one_wide_leg_on_a_two_legged_hub()
    {
        const int spine = 11;
        for (ulong seed = 0; seed < 200; seed++)
        {
            if (HubBoxEmitter.SampleArms(new ComposeRng(seed), spine, 2, LaneWidth) is not { } legs) continue;
            var bay = legs[1].Start - (legs[0].Start + legs[0].Width);
            await Assert.That(bay).IsLessThanOrEqualTo(2 * LaneWidth);
            // spine 11 less a bay of at most 4 leaves 7+ for two legs, so one of them is at least 4
            await Assert.That(legs.Max(l => l.Width)).IsGreaterThan(LaneWidth)
                .Because($"legs {legs[0].Width}+{legs[1].Width} with bay {bay} on a {spine} spine");
        }

        // and the default layout obeys the same bound rather than being a shape the composer could never draw
        var fallback = HubBoxEmitter.DefaultArms(spine, 2, LaneWidth)!;
        await Assert.That(fallback[1].Start - (fallback[0].Start + fallback[0].Width)).IsLessThanOrEqualTo(2 * LaneWidth);
        await Assert.That(fallback.Max(l => l.Width)).IsGreaterThan(LaneWidth);

        // a spine so long the capped bay cannot be absorbed has no lawful layout at all
        await Assert.That(HubBoxEmitter.DefaultArms(20, 2, LaneWidth)).IsNull();
    }
}
