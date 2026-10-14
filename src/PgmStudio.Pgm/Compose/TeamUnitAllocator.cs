using PgmStudio.Geom;
using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Compose;

/// <summary>A hub side in unit-relative (u, v) terms, before the symmetry frame maps it to a real
/// <see cref="Box"/> edge: <see cref="Front"/> is toward the axis (−u, where the frontline meets the mid),
/// <see cref="Back"/> away from it (+u), <see cref="Left"/>/<see cref="Right"/> the two lateral (±v) sides. The
/// team unit hangs its neighbours off these four sides.</summary>
public enum UnitSide { Front, Back, Left, Right }

/// <summary>The frame-independent <b>placement plan</b> of a team unit: which hub side each neighbour sits on.
/// The frontline always takes the front side; <see cref="Spawn"/> is the back or a lateral side; each of
/// <see cref="Wools"/> names its side. Geometry (dims, Rects, the offer plan) is layered on this by the
/// allocator; this is the decision layer.</summary>
public sealed record UnitPlan(UnitSide Spawn, IReadOnlyList<UnitSide> Wools);

/// <summary>
/// The partition-first team-unit allocator — a <b>clean box-model sampler</b> that decides the unit's
/// structure and lays out box footprints from the budget.
/// This layer is the frame-independent <b>placement plan</b> (<see cref="UnitPlan"/>): the wool count and
/// which hub side each neighbour takes. The <b>spawn may sit on the back or a lateral side</b>;
/// the wools are assigned <b>after</b> the spawn and around it — the two free (non-spawn, non-front) sides first,
/// back preferred, a third wool doubling up on the spawn's side ("two side wools +
/// a back wool-c" exactly when the spawn is on the back).
/// </summary>
public static class TeamUnitAllocator
{
    /// <summary>Allocate a team unit's <see cref="BoxLayout"/> from the <paramref name="env"/> budget — the
    /// geometry layer over <see cref="UnitTuning.SamplePlan"/>. Positions the hub on the (u, v) grid, <b>owns the hub-form
    /// choice</b> (model.md §4), and seats the spawn and wools on the chosen form's <b>real free-edge
    /// intervals</b> — the offerable surface the compound actually presents (§4), not its bounding box. So a
    /// non-rectangular hub (L/U/Ring/Double-hole) never leaves a neighbour docking an empty bbox stretch and only
    /// corner-touching it (a <c>t*/*t</c> pinch); the four-full-edges rectangle is just the degenerate case. The
    /// chosen form rides on the hub <see cref="Box.Form"/> for the filler to re-emit; each hub↔neighbour joint
    /// carries the hub's per-edge <b>width offer</b> (the plan <see cref="TeamUnitFiller"/> consumes). The
    /// sampled form <b>falls back to the solid <see cref="CompoundForm.I"/></b> when its free edges cannot host
    /// the plan. Returns the partition, or <c>null</c> when even the rectangle cannot host a neighbour (the box is
    /// too small — the directed "no shape fits" signal, §4).
    /// The frontline is allocated on the front side, its reach pushing the hub back so it sits between the hub
    /// and the axis; the filler fills it as a join and carries its face offer to the mid.</summary>
    public static BoxLayout? Allocate(
        ComposeEnvelope env, ComposeRng rng, CrossingDesign? crossing = null)
    {
        var frame = Frame.For(env.Symmetry);
        var laneWidth = env.LaneWidth;                          // the map-wide lane width, from the band
        // what a goal keeps clear of its neighbour. Held apart from the lane width because they are different
        // quantities — one is how wide a corridor is, the other how much ground a goal keeps around it — and
        // only the second is a distance a floor in blocks can be stated over.
        var seatGapCells = laneWidth;
        var plan = UnitTuning.SamplePlan(env, rng);

        // the budget, opened at the band's land for one team and debited as each box is sized. The hub is the
        // elastic box: the spawn, the frontline and the wools each claim a fixed share and the hub takes what
        // is left, so a bigger band buys a bigger junction rather than the same one on a bigger board.
        var budget = new LandBudget(env.UnitBudgetCells);
        // the spawn costs what a spawn box costs rather than a share of the board: it is a room and a run-up at
        // the map's lane width, the same size on every band
        var (estW, estH) = SpawnBoxEmitter.Box(CompoundForm.I, laneWidth, 2, 0);
        var claimed = estW * (double)estH
                    + budget.Share(UnitTuning.FrontlineShare)
                    + plan.Wools.Count * budget.Share(UnitTuning.WoolShare);
        var hubTarget = Math.Max(budget.Share(UnitTuning.HubMinShare), budget.Total - claimed);
        // the shape is the draw and the area is not: a sampled aspect turns the hub's share into a box that is
        // always wider than deep, so the hub tracks the budget across every band instead of two independent
        // draws out of a ladder that saturates
        var aspect = rng.NextDouble(UnitTuning.HubAspectLow, UnitTuning.HubAspectHigh);
        var (hubU, hubV) = UnitTuning.HubBoxCells(hubTarget, aspect, laneWidth);
        budget.Spend(hubU * (double)hubV);
        // The span is drawn free of parity, including under a laterally-flipping symmetry. What has to coincide
        // with its own image is the face — it carries its own parity law and is what the finished unit is centred
        // on — and the hub rides along, free to sit off the axis. Rounding it to even would spend half the size
        // ladder buying an alignment the hub does not provide.
        // the frontline sits between the hub and the axis, so its reach pushes the hub's front edge back; the +2
        // gives a staple frontline's arms room for a real bay (a shallower reach collapses them to nubs)
        var frontReach = laneWidth + 2;
        // the axis margin is the mid crossing's half-gap when the caller carries one (the composed path — the
        // mid box arithmetic decides how far the unit's front sits from the axis); the plain default otherwise
        var hubUMin = (crossing?.HalfGapCells ?? Envelope.AxisMarginCells) + frontReach;
        var hubVMin = -(hubV / 2);
        var hubRect = frame.ToRect(hubUMin, hubU, hubVMin, hubV);

        // The compound comes first, because a neighbour docks onto what the hub OFFERS rather than onto its
        // bounding box. Pick the form from the box's real dims (frame-mapped — the wide axis affords the wide
        // holed compounds), then its walls and, for a branch, its legs; the compound is a pure function of those three,
        // so reading it here and building it again downstream give the same runs without a draw between them.
        var sampled = ChooseHubForm(hubRect.Width, hubRect.Height, laneWidth, env.Cell, rng);
        var walls = ChooseHubWalls(sampled, hubRect.Width, hubRect.Height, laneWidth, env.Cell, rng);
        var arms = sampled.Arms() > 0
            ? HubBoxEmitter.SampleArms(rng, hubRect.Width, sampled.Arms(), laneWidth)
            : null;

        // the neighbour requests (spawn + wools + the frontline join), sized out of the budget the hub left and
        // against the free runs its front edge actually presents — a bay-fronted compound (a G, U or L) hands the
        // frontline a floor its face has to reach, which is the width that closes the bay into a hole
        var frontRuns = UnitSeating.FrontRuns(sampled, hubRect, frame, laneWidth, walls, arms);
        var requests = UnitRequests.Sample(
            env, rng, budget, plan, laneWidth, hubU, hubV, frontReach, frontRuns);
        // seat them on that compound; fall back to the solid rectangle (four full edges) when the offerable surface
        // cannot host. The requests carry over unchanged: the fallback is always toward MORE free surface, so a
        // request sized against a holed compound's runs fits the rectangle that replaces it.
        var seating = UnitSeating.Seat(sampled, hubRect, frame, laneWidth, env.WoolLaneWidth, seatGapCells, env.Cell, requests, rng, walls, arms);
        if (seating is null && sampled != CompoundForm.I)
            seating = UnitSeating.Seat(CompoundForm.I, hubRect, frame, laneWidth, env.WoolLaneWidth, seatGapCells, env.Cell, requests, rng);
        if (seating is not { } s) return null;

        return new BoxLayout(s.Boxes, s.Joints);
    }

    /// <summary>Choose the hub form for a <paramref name="boxW"/>×<paramref name="boxH"/> box (real cell dims, the
    /// frame having mapped the wide lateral axis onto width) at lane width <paramref name="laneWidth"/>.
    /// A <b>wide</b> box (width ≥ <see cref="UnitTuning.WideHubCells"/>,
    /// height ≥ <see cref="UnitTuning.RingFitCells"/>) affords the <b>wide holed compounds</b> — the P (a loop on a long overhanging
    /// bar), the Double-hole (a ring + a docked U, two equal holes), and the G (a ring + an L, the ring's hole plus a
    /// frontline-sealed bay — asymmetric holes), whose long runs are free surface — sampled alongside the elongated
    /// ring. A <b>big square-ish</b> box (both ≥ <see cref="UnitTuning.RingFitCells"/>) is too much solid area for the
    /// budget, so it prefers negative space: mostly the ring, else a branch compound. A small or thin box stays the
    /// compact solid/branch menu (the wider forms would directed-null and fall back).</summary>
    internal static CompoundForm ChooseHubForm(int boxW, int boxH, int laneWidth, int cell, ComposeRng rng)
    {
        if (boxW >= UnitTuning.WideHubCells(laneWidth, cell) && boxH >= UnitTuning.RingFitCells(laneWidth, cell))
            return rng.Pick(new[]
            {
                CompoundForm.P, CompoundForm.DoubleHole,
                CompoundForm.G, CompoundForm.Donut,
                // the U belongs here too: with the bay bounded above, a wide box spends its span on the legs, so
                // the two-legged hub comes out with real legs rather than stubs either side of a gap
                CompoundForm.U,
            });
        if (boxW >= UnitTuning.RingFitCells(laneWidth, cell) && boxH >= UnitTuning.RingFitCells(laneWidth, cell))
            return rng.NextBool(UnitTuning.RingChance) ? CompoundForm.Donut
                : rng.Pick(FillMenu.For(BoxKind.Hub).Where(f => f.Arms() > 0).ToList());
        return rng.Pick(FillMenu.For(BoxKind.Hub).Where(f => f is CompoundForm.I || f.Arms() > 0).ToList());
    }

    /// <summary>Choose the four wall widths of a ring-bodied <paramref name="form"/> filling a
    /// <paramref name="boxW"/>×<paramref name="boxH"/> box at lane width <c>laneWidth</c>, or
    /// <c>null</c> for an even-walled ring (and for every form without one).
    ///
    /// <para>Widening <b>spends the box's slack</b>: a wall thickens and the hole loses those cells, the box does
    /// not grow — so the sampler only offers it where the hole can afford it and still stay a corridor wide. One
    /// side is widened, drawn evenly from the four; the amount is capped so the widest wall is never more than
    /// twice the narrowest, the same spread law the frontline's arms keep. The hole it leaves stays at least a
    /// corridor and at least <see cref="UnitTuning.HubHoleCells"/>.</para></summary>
    internal static RingWalls? ChooseHubWalls(CompoundForm form, int boxW, int boxH, int laneWidth, int cell, ComposeRng rng)
    {
        // the ring inside each form: the Ring is the box, the docked forms (P/DoubleHole/G) keep a bar's width
        // beside it — the same arithmetic the hub filler builds them with
        var (ringW, ringH) = form switch
        {
            CompoundForm.Donut => (boxW, boxH),
            CompoundForm.P or CompoundForm.DoubleHole or CompoundForm.G => (boxW - 2 * laneWidth, boxH),
            _ => (0, 0),                                        // no ring to widen
        };
        if (ringW <= 0 || !rng.NextBool(UnitTuning.WidenedRingChance)) return null;

        // the slack on each axis: what the hole keeps beyond its floor once both walls are paid for
        var holeFloor = Math.Max(laneWidth, UnitTuning.HubHoleCells(cell));
        int slackW = ringW - 2 * laneWidth - holeFloor, slackH = ringH - 2 * laneWidth - holeFloor;
        var sides = new List<(int Side, int Room)>();
        if (slackW > 0) { sides.Add((1, slackW)); sides.Add((3, slackW)); }   // right, left
        if (slackH > 0) { sides.Add((0, slackH)); sides.Add((2, slackH)); }   // top, bottom
        if (sides.Count == 0) return null;

        var (side, room) = rng.Pick(sides);
        var extra = rng.NextInt(1, Math.Min(room, laneWidth) + 1);     // ≤ the corridor keeps the widest within 2× the narrowest
        return side switch
        {
            0 => new RingWalls(laneWidth + extra, laneWidth, laneWidth, laneWidth),
            1 => new RingWalls(laneWidth, laneWidth + extra, laneWidth, laneWidth),
            2 => new RingWalls(laneWidth, laneWidth, laneWidth + extra, laneWidth),
            _ => new RingWalls(laneWidth, laneWidth, laneWidth, laneWidth + extra),
        };
    }
}
