using PgmStudio.Geom;
using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Compose;

/// <summary>A hub side in unit-relative (u, v) terms, before the symmetry frame maps it to a real
/// <see cref="Box"/> edge: <see cref="Front"/> is toward the axis (−u, where the frontline meets the mid),
/// <see cref="Back"/> away from it (+u), <see cref="Left"/>/<see cref="Right"/> the two lateral (±v) sides. The
/// team unit hangs its neighbours off these four sides.</summary>
public enum UnitSide { Front, Back, Left, Right }

/// <summary>The frame-independent <b>placement plan</b> of a team unit (G63-C.2): which hub side each neighbour
/// sits on. <see cref="Frontline"/> is the front side or <c>null</c> (no frontline); <see cref="Spawn"/> is the
/// back or a lateral side; each of <see cref="Wools"/> names its side. Geometry (dims, Rects, the offer plan)
/// is layered on this by the allocator; this is the decision layer.</summary>
public sealed record UnitPlan(UnitSide? Frontline, UnitSide Spawn, IReadOnlyList<UnitSide> Wools);

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
    /// <summary>Allocate a team unit's <see cref="BoxPartition"/> from the <paramref name="env"/> budget — the
    /// geometry layer over <see cref="UnitTuning.SamplePlan"/>. Positions the hub on the (u, v) grid, <b>owns the hub-form
    /// choice</b> (map-generation.md §5.5), and seats the spawn and wools on the chosen form's <b>real free-edge
    /// intervals</b> — the offerable surface the body actually presents (§1.13), not its bounding box. So a
    /// non-rectangular hub (L/U/Ring/Double-hole) never leaves a neighbour docking an empty bbox stretch and only
    /// corner-touching it (a <c>t*/*t</c> pinch); the four-full-edges rectangle is just the degenerate case. The
    /// chosen form rides on the hub <see cref="Box.Form"/> for the filler to re-emit; each hub↔neighbour joint
    /// carries the hub's per-edge <b>width offer</b> (the plan <see cref="TeamUnitFiller"/> consumes). The
    /// sampled form <b>falls back to the solid <see cref="Compound.Rectangle"/></b> when its free edges cannot host
    /// the plan. Returns the partition + the spawn facing (<see cref="Frame.TowardAxis"/>), or <c>null</c> when
    /// even the rectangle cannot host a neighbour (the box is too small — the directed "no shape fits" signal, §4).
    /// When the plan carries a frontline it is allocated on the front side, its reach pushing the hub back so it
    /// sits between the hub and the axis; the filler fills it as a join and carries its face offer to the mid.</summary>
    public static (BoxPartition Partition, string SpawnFacing)? Allocate(
        ComposeEnvelope env, ComposeRng rng, CrossingDesign? crossing = null)
    {
        var frame = Frame.For(env.Symmetry);
        var laneWidthCells = env.LandPerTeam > UnitTuning.WideLaneLand ? 3 : 2;               // the map-wide lane width
        // the frontline is the default when there is budget for it; none is the sampled exception
        var hasFrontline = env.LandPerTeam >= UnitTuning.FrontlineMinLand && rng.NextInt(0, UnitTuning.NoFrontlineInN) > 0;
        var plan = UnitTuning.SamplePlan(env, rng, hasFrontline);

        var depthCap = UnitTuning.HubCapCells(env.LandPerTeam);
        var wideCap = UnitTuning.HubWideCap(env.LandPerTeam);
        var floor = laneWidthCells + 2;
        var hubU = rng.NextInt(floor, Math.Max(floor, depthCap) + 1);    // depth toward the axis — kept compact
        var hubV = rng.NextInt(floor, Math.Max(floor, wideCap) + 1);     // lateral span — elongates across the team's width
        // the span is drawn free of parity. It used to be rounded to even under a laterally-flipping symmetry so
        // the hub would coincide with its own image and the two sides' fronts line up — but the thing that has
        // to coincide is the face, which carries its own parity law and is what the finished unit is centred on.
        // The hub rides along and is free to sit off the axis, so half the size ladder no longer has to be spent
        // buying an alignment it does not provide.
        // the frontline sits between the hub and the axis, so its reach pushes the hub's front edge back; the +2
        // gives a staple frontline's arms room for a real bay (a shallower reach collapses them to nubs)
        var frontReach = hasFrontline ? laneWidthCells + 2 : 0;
        // the axis margin is the mid crossing's half-gap when the caller carries one (the composed path — the
        // mid box arithmetic decides how far the unit's front sits from the axis); the plain default otherwise
        var hubUMin = (crossing?.HalfGapCells ?? Envelope.AxisMarginCells) + frontReach;
        var hubVMin = -(hubV / 2);
        var hubRect = frame.ToRect(hubUMin, hubU, hubVMin, hubV);

        // the neighbour requests (spawn + wools + the frontline join), sized from the budget before the form is chosen
        var requests = UnitRequests.Sample(env, rng, plan, laneWidthCells, hubU, hubV, frontReach);

        // pick the hub form from the box's real dims (frame-mapped — the wide axis afford the wide holed bodies),
        // seat the requests on its free edges; fall back to the solid rectangle (four full edges) when the offerable
        // surface can't host
        var sampled = ChooseHubForm(hubRect.Width, hubRect.Height, rng);
        var walls = ChooseHubWalls(sampled, hubRect.Width, hubRect.Height, FillProfiles.HubWallCells, rng);
        // a branch hub's legs, drawn here rather than at fill time because the body is emitted twice — once to
        // read the runs it offers, once to build it — and a second draw would not agree with the first
        var arms = sampled.Form == Compound.SpineArms
            ? HubBoxEmitter.SampleArms(rng, hubRect.Width, sampled.Arms, FillProfiles.HubWallCells)
            : null;
        var seating = UnitSeating.Seat(sampled, hubRect, frame, laneWidthCells, requests, rng, noFront: !hasFrontline, walls, arms);
        if (seating is null && sampled.Form != Compound.Rectangle)
            seating = UnitSeating.Seat(new CompoundRead(Compound.Rectangle), hubRect, frame, laneWidthCells, requests, rng, noFront: !hasFrontline);
        if (seating is not { } s) return null;

        return (new BoxPartition(s.Boxes, s.Joints), frame.TowardAxis);
    }

    /// <summary>Choose the hub form for a <paramref name="boxW"/>×<paramref name="boxH"/> box (real cell dims, the
    /// frame having mapped the wide lateral axis onto width). A <b>wide</b> box (width ≥ <see cref="UnitTuning.WideHubCells"/>,
    /// height ≥ <see cref="UnitTuning.RingFitCells"/>) affords the <b>wide holed bodies</b> — the P (a loop on a long overhanging
    /// bar), the Double-hole (a ring + a docked U, two equal holes), and the G (a ring + an L, the ring's hole plus a
    /// frontline-sealed bay — asymmetric holes), whose long runs are free surface — sampled alongside the elongated
    /// ring. A <b>big square-ish</b> box (both ≥ <see cref="UnitTuning.RingFitCells"/>) is too much solid area for the
    /// budget, so it prefers negative space: mostly the ring, else a branch body. A small or thin box stays the
    /// compact solid/branch menu (the wider forms would directed-null and fall back).</summary>
    internal static CompoundRead ChooseHubForm(int boxW, int boxH, ComposeRng rng)
    {
        if (boxW >= UnitTuning.WideHubCells && boxH >= UnitTuning.RingFitCells)
            return rng.Pick(new[]
            {
                new CompoundRead(Compound.P), new CompoundRead(Compound.DoubleHole),
                new CompoundRead(Compound.G), new CompoundRead(Compound.Ring),
                // the U belongs here too: with the bay bounded above, a wide box spends its span on the legs, so
                // the two-legged hub comes out with real legs rather than stubs either side of a gap
                new CompoundRead(Compound.SpineArms, 2),
            });
        if (boxW >= UnitTuning.RingFitCells && boxH >= UnitTuning.RingFitCells)
            return rng.NextBool(UnitTuning.RingChance) ? new CompoundRead(Compound.Ring)
                : rng.Pick(HubBoxEmitter.Forms.Where(f => f.Form is Compound.SpineArms).ToList());
        return rng.Pick(HubBoxEmitter.Forms.Where(f => f.Form is Compound.Rectangle or Compound.SpineArms).ToList());
    }

    /// <summary>Choose the four wall widths of a ring-bodied <paramref name="form"/> filling a
    /// <paramref name="boxW"/>×<paramref name="boxH"/> box at corridor width <paramref name="cw"/>, or
    /// <c>null</c> for an even-walled ring (and for every form without one).
    ///
    /// <para>Widening <b>spends the box's slack</b>: a wall thickens and the hole loses those cells, the box does
    /// not grow — so the sampler only offers it where the hole can afford it and still stay a corridor wide. One
    /// side is widened, drawn evenly from the four; the amount is capped so the widest wall is never more than
    /// twice the narrowest, the same spread law the frontline's arms keep.</para></summary>
    internal static RingWalls? ChooseHubWalls(CompoundRead form, int boxW, int boxH, int corridorCells, ComposeRng rng)
    {
        // the ring inside each form: the Ring is the box, the docked forms (P/DoubleHole/G) keep a bar's width
        // beside it — the same arithmetic the hub filler builds them with
        var (ringW, ringH) = form.Form switch
        {
            Compound.Ring => (boxW, boxH),
            Compound.P or Compound.DoubleHole or Compound.G => (boxW - 2 * corridorCells, boxH),
            _ => (0, 0),                                        // no ring to widen
        };
        if (ringW <= 0 || !rng.NextBool(UnitTuning.WidenedRingChance)) return null;

        // the slack on each axis: what the hole keeps beyond a corridor of its own once both walls are paid for
        int slackW = ringW - 2 * corridorCells - corridorCells, slackH = ringH - 2 * corridorCells - corridorCells;
        var sides = new List<(int Side, int Room)>();
        if (slackW > 0) { sides.Add((1, slackW)); sides.Add((3, slackW)); }   // right, left
        if (slackH > 0) { sides.Add((0, slackH)); sides.Add((2, slackH)); }   // top, bottom
        if (sides.Count == 0) return null;

        var (side, room) = rng.Pick(sides);
        var extra = rng.NextInt(1, Math.Min(room, corridorCells) + 1);     // ≤ the corridor keeps the widest within 2× the narrowest
        return side switch
        {
            0 => new RingWalls(corridorCells + extra, corridorCells, corridorCells, corridorCells),
            1 => new RingWalls(corridorCells, corridorCells + extra, corridorCells, corridorCells),
            2 => new RingWalls(corridorCells, corridorCells, corridorCells + extra, corridorCells),
            _ => new RingWalls(corridorCells, corridorCells, corridorCells, corridorCells + extra),
        };
    }
}
