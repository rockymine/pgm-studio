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
    // ── the size ladders: where the unit's structure changes with the budget ───────────────────────────────

    /// <summary>Land per team above which the map's lane width is <b>3</b> cells rather than 2 (LN1: 15 blocks
    /// on big maps, 10 elsewhere). The one map-wide width every non-wool box builds to.</summary>
    private const double WideLaneLand = 2500;

    /// <summary>Land per team below which a unit has <b>no frontline</b> — there is no budget for one, so the
    /// hub fronts the mid directly.</summary>
    private const double FrontlineMinLand = 800;

    /// <summary>One unit in this many has no frontline even when the budget allows — the sampled exception that
    /// keeps the frontline from being universal.</summary>
    private const int NoFrontlineInN = 7;

    /// <summary>Land per team below which a unit carries a <b>single</b> wool (a tiny board cannot hold two).</summary>
    private const double TinyBoardLand = 600;

    /// <summary>Players per team at or above which the unit is a <b>full team</b>: 2–3 wools rather than 1–2.</summary>
    private const int FullTeamPlayers = 16;

    /// <summary>Of full-team units, how often the third wool appears (it doubles onto the spawn's side).</summary>
    private const double ThirdWoolChance = 0.4;

    /// <summary>The smallest hub dimension a <b>ring</b> fits in — a hub at least this big on both axes is a
    /// "big square" and prefers negative space to solid area.</summary>
    private const int RingFitCells = 5;

    /// <summary>Of big-square hubs, how often the form is the <b>ring</b> specifically rather than another
    /// negative-space body. The ring's void always fits and survives a frontline, so it carries most of them.</summary>
    private const double RingChance = 0.85;

    /// <summary>Of ring-bodied hubs whose box has the slack for it, how often <b>one</b> wall comes out wider than
    /// the other three. The uniform ring stays the common case; a widened side reads as a deliberate variation —
    /// the author saying more play flows through there — rather than the house style.</summary>
    private const double WidenedRingChance = 0.3;

    /// <summary>How long a wool may run relative to its room dimension before it reads as a <b>too-long
    /// single-entry corridor</b> — the wool length rule. A lane past this bound tucks its room to the side
    /// instead, and it also caps the depth of every compact fallback.</summary>
    private const int WoolLengthRatio = 3;

    /// <summary>The widest a budget-sized wool lane may be, in lanes — the along-extent the budget share is
    /// spread over before it turns into depth.</summary>
    private const int WoolAlongCapLanes = 3;

    /// <summary>The hub's <b>depth</b> cap in cells (toward the axis) at <paramref name="landPerTeam"/> — how deep
    /// the budget warrants. Simplified ladder (the frontline / twin-recess / wool-c clearance floors refine it as
    /// those land); the floor is the lane width + 2 either way. The <b>lateral</b> span uses the wider
    /// <see cref="HubWideCap"/> so the hub elongates across the team's width rather than growing a bigger square.</summary>
    private static int HubCapCells(double landPerTeam) =>
        landPerTeam >= 3000 ? 6 : landPerTeam >= 1500 ? 5 : landPerTeam >= FrontlineMinLand ? 4 : 3;

    /// <summary>The hub's <b>lateral</b> (cross-axis) span cap in cells at <paramref name="landPerTeam"/> — wider
    /// than the depth cap, so a hub grows <b>wider, not squarer</b>: the long lateral edge gives the spawn and
    /// wools room to attach with the seat gap, and at ≥ <see cref="WideHubCells"/> it affords the wide holed
    /// bodies (P, Double-hole), whose bar/ring runs are long free surface.</summary>
    private static int HubWideCap(double landPerTeam) =>
        landPerTeam >= 3000 ? 11 : landPerTeam >= 1500 ? 9 : landPerTeam >= FrontlineMinLand ? 7 : 5;

    /// <summary>The box width at or above which a hub is <b>wide enough for the holed wide bodies</b> — the P
    /// (loop + overhanging bar) and the Double-hole (ring + docked U) both need width ≥ this (loop/ring
    /// <c>w − 2·cw ≥ 2·cw + 1</c> at <c>cw = 2</c>). Below it they directed-null and the compact menu is used.</summary>
    private const int WideHubCells = 9;

    // ── the shape mix: how often each wool shape is sampled ────────────────────────────────────────────────

    /// <summary>How often a wool takes a bent <c>L</c> (the seat-and-shift) rather than an <c>I</c> — the shape
    /// variety, decoupled from the length rule so an <c>L</c> appears on any wool, not just the long ones. When
    /// the L's overhang cannot fit a crowded hub the seat falls back to a compact inline <c>I</c>.</summary>
    private const double BentWoolChance = 0.4;

    /// <summary>Of the rich wools, how often a <b>donut</b> (a ring the wool sits in, reached around both ways)
    /// rather than a bent <c>L</c> — kept low because the ring is a big, deep footprint that mostly wants a
    /// less-crowded hub (else the overhang falls back to a compact inline <c>I</c>).</summary>
    private const double DonutChance = 0.25;

    /// <summary>Of the non-donut rich wools, how often a <b>staple-class</b> two-leg wool (<c>U</c>/<c>H</c>/
    /// <c>clamp</c> — the wool reached by two legs off one mouth) rather than a bent <c>L</c>. It docks its full
    /// mouth (~3 lanes), so it needs a hub edge as wide as its mouth; where the edge is too narrow it demotes to
    /// an <c>L</c>, so the staple lands mostly on the wider hubs.</summary>
    private const double StapleChance = 0.4;

    /// <summary>Of the clamp wools, how often the <b>adjacent/corner</b> variant (an <c>L+I</c> gripping the wool
    /// in a fold) rather than the <b>centered</b> one (two straight legs, <c>I+I</c>). Both dock the same full
    /// mouth; this only changes which two-leg shape clamps the cut-cell wool inside.</summary>
    private const double ClampAdjacentChance = 0.4;

    /// <summary>Of the donut wools, how often the wool is <b>integrated at the ring's corner</b> rather than
    /// hung off its bottom-right on a trailing room. The corner wool costs no width past the ring, so the box
    /// loses the trailing <c>rd</c> — a squarer ring instead of the stretched min-box sliver.</summary>
    private const double DonutCornerWoolChance = 0.5;

    /// <summary>How often a non-<c>L</c> wool tucks its room to the <b>side</b> (a compact side-room) rather than
    /// a plain inline back-room lane — for the three shapes to read in a balanced mix. A wool that would run long
    /// side-tucks regardless (the length rule).</summary>
    private const double SideRoomChance = 0.4;

    // ── geometry: the widths and clearances the seat step builds to ────────────────────────────────────────

    /// <summary>The wool's own corridor width in cells — a <b>w2</b> lane (docs/generator/model.md §4:
    /// "the lane to the wool is simple, w2"), independent of the map's lane width <c>w</c> (which is 3 on big
    /// boards). Keeping wool families at w2 makes them compact and lets a staple's 3-lane mouth fit a hub edge.</summary>
    internal const int WoolLaneCells = 2;

    /// <summary>The widest hub-entry a donut may sample, in cells — the min-only entry (one corridor) read as a
    /// chokepoint, so the attachment stub varies up to this along the hub edge.</summary>
    internal const int DonutEntryMaxCells = 5;

    /// <summary>The donut's enclosed hole caps, in cells: <b>along</b> the hub edge (the ring's mouth-side
    /// extent) and <b>deep</b> (outward). The min box gives the 1×2 hole; the sampled growth reaches 3×5 — the
    /// box grows and the emitter's ring absorbs it (its span derives from the box).</summary>
    private const int DonutHoleAlongMaxCells = 3;
    private const int DonutHoleDeepMaxCells = 5;

    /// <summary>The clearance kept between a docked neighbour and each hub <b>corner</b>, in cells. Zero under the
    /// mass-level corner law: two neighbours on adjacent hub sides meet only at the hub's own corner cell, which
    /// the hub fills — a ¾-solid bridged corner, never a pinch — so no clearance is needed and the neighbours may
    /// use the hub's full edge (which the side-tuck wool and the wide frontline face want).</summary>
    private const int CornerClearanceCells = 0;

    /// <summary>How often the frontline still takes the hub's <b>full</b> front width (G123). The pinned face
    /// stays the common case; a partial front is the deliberate exception, not the new default.</summary>
    private const double FullFaceChance = 0.6;

    /// <summary>The narrowest sampled frontline face, in cells. Below two lanes a front reads as a nub stuck to
    /// the hub rather than a front the mid can meet.</summary>
    private const int FaceMinCells = 4;

    /// <summary>How far a sampled face may <b>overhang</b> the hub's front edge, in cells (total across both
    /// sides). The frontline is the one neighbour allowed to be wider than the edge it docks: its face is what
    /// the mid meets, and a face that reaches past the hub is what turns the front into a funnel.</summary>
    private const int FaceOverhangMaxCells = 2;

    /// <summary>How often the frontline slides off the centre of the hub's front edge, rather than sitting
    /// symmetric on it. Two knobs, because width and position are different decisions: the face may be partial
    /// and still centred. A slid face is what costs the mid band slack, so it stays the minority draw.</summary>
    private const double ShiftedFaceChance = 0.35;

    // ── the plan: how many wools, and which side each neighbour takes ──────────────────────────────────────

    /// <summary>The wool-box count: 2–3 for a full team, one for a tiny board, else 1–2.</summary>
    public static int WoolCount(ComposeEnvelope env, ComposeRng rng) =>
        env.PlayersPerTeam >= FullTeamPlayers ? (rng.NextBool(ThirdWoolChance) ? 3 : 2)
        : env.LandPerTeam < TinyBoardLand ? 1
        : rng.NextInt(1, 3);

    /// <summary>Assign each of <paramref name="woolCount"/> wools a hub side, given the <paramref name="spawn"/>'s
    /// side. The two free body sides (back and the sides, minus the spawn's, <b>back first</b>) take a wool each;
    /// a third wool doubles up on the spawn's side. Front is never a wool side (it is the frontline's).</summary>
    public static IReadOnlyList<UnitSide> AssignWools(UnitSide spawn, int woolCount)
    {
        var free = new[] { UnitSide.Back, UnitSide.Left, UnitSide.Right }.Where(s => s != spawn).ToArray();
        var wools = new UnitSide[woolCount];
        for (var i = 0; i < woolCount; i++) wools[i] = i < free.Length ? free[i] : spawn;
        return wools;
    }

    /// <summary>Sample a unit's placement plan: the wool count, the spawn's side (back or a lateral side), and
    /// the wools around it. <paramref name="hasFrontline"/> reserves the front side for the frontline.</summary>
    public static UnitPlan SamplePlan(ComposeEnvelope env, ComposeRng rng, bool hasFrontline)
    {
        var woolCount = WoolCount(env, rng);
        var spawn = new[] { UnitSide.Back, UnitSide.Left, UnitSide.Right }[rng.NextInt(0, 3)];
        return new UnitPlan(hasFrontline ? UnitSide.Front : null, spawn, AssignWools(spawn, woolCount));
    }

    /// <summary>A neighbour box to seat against the hub: the hub <see cref="Side"/> it docks, its box
    /// <see cref="Kind"/>, its outward <see cref="Depth"/> (perpendicular to the hub edge) and along-edge
    /// <see cref="Along"/> extent (cells), and its <see cref="Id"/>. Sizing is frame- and form-independent (it
    /// reads the budget); only the seat position is not — so the whole set is fixed before the form is chosen.</summary>
    private sealed record Demand(UnitSide Side, BoxKind Kind, int Depth, int Along, string Id, WoolFill? Wool = null);

    /// <summary>A wool family the <b>seat-and-shift</b> docks: a single-entry approach whose one narrow entry
    /// lands on a hub run while the body overhangs. The dual-entry staple/branch (<c>U</c>/<c>H</c>) is <b>not</b>
    /// one — both its entries must land on the host, so it docks its full mouth (the plain seat path), never an
    /// overhang (an overhang would strand the second entry off the hub — a pinch).</summary>
    private static bool Overhangs(ShapeFamily family) => family is ShapeFamily.L or ShapeFamily.Donut;

    /// <summary>
    /// How a neighbour docks its host. The three styles are indexed by <b>how much is known about where the
    /// shape's entries are</b>, which is what makes them three rather than an arbitrary list:
    /// <list type="bullet">
    /// <item><see cref="FullMouth"/> — nothing is known, so require the <em>whole</em> along-extent to sit
    /// inside one free run; every entry then lands wherever they are. This is why the dual-entry staples
    /// (<c>U</c>/<c>H</c>/<c>Clamp</c>) dock here — an overhang would strand their second entry off the
    /// host.</item>
    /// <item><see cref="Overhang"/> — the family has exactly one entry and the emitter can say where, so only
    /// that interval must land and the body may hang past the run.</item>
    /// <item><see cref="ContactPatch"/> — the frontline is a face, not a corridor, so it has no entry at all;
    /// what must hold is that every stretch where it meets a run is at least a lane wide.</item>
    /// </list>
    /// <b>Derived, never sampled</b> — see <see cref="StyleOf"/>. The style falls out of the family roll that
    /// already happened in <see cref="WoolDemand"/>; there is no "which dock style" draw anywhere.
    /// </summary>
    private enum DockStyle { FullMouth, Overhang, ContactPatch }

    /// <summary>The dock style a demand implies. A single-entry rich wool overhangs; the frontline takes the
    /// contact patch; everything else takes the shape-agnostic full mouth. Note this is the style a demand
    /// <em>starts</em> at: an overhang that finds no clear placement is demoted to the compact <c>I</c> and
    /// re-dispatched as a <see cref="DockStyle.FullMouth"/>.</summary>
    private static DockStyle StyleOf(Demand d) =>
        d.Wool is { } wool && Overhangs(wool.Family) ? DockStyle.Overhang
        : d.Kind == BoxKind.Frontline ? DockStyle.ContactPatch
        : DockStyle.FullMouth;

    /// <summary>Allocate a team unit's <see cref="BoxPartition"/> from the <paramref name="env"/> budget — the
    /// geometry layer over <see cref="SamplePlan"/>. Positions the hub on the (u, v) grid, <b>owns the hub-form
    /// choice</b> (map-generation.md §5.5), and seats the spawn and wools on the chosen form's <b>real free-edge
    /// intervals</b> — the offerable surface the body actually presents (§1.13), not its bounding box. So a
    /// non-rectangular hub (L/U/Ring/Double-hole) never leaves a neighbour docking an empty bbox stretch and only
    /// corner-touching it (a <c>t*/*t</c> pinch); the four-full-edges rectangle is just the degenerate case. The
    /// chosen form rides on the hub <see cref="Box.Form"/> for the filler to re-emit; each hub↔neighbour joint
    /// carries the hub's per-edge <b>w-width offer</b> (the plan <see cref="TeamUnitFiller"/> consumes). The
    /// sampled form <b>falls back to the solid <see cref="Compound.Rectangle"/></b> when its free edges cannot host
    /// the plan. Returns the partition + the spawn facing (<see cref="Frame.TowardAxis"/>), or <c>null</c> when
    /// even the rectangle cannot host a neighbour (the box is too small — the directed "no shape fits" signal, §4).
    /// When the plan carries a frontline it is allocated on the front side, its reach pushing the hub back so it
    /// sits between the hub and the axis; the filler fills it as a join and carries its face offer to the mid.</summary>
    public static (BoxPartition Partition, string SpawnFacing)? Allocate(
        ComposeEnvelope env, ComposeRng rng, CrossingDesign? crossing = null)
    {
        var frame = Frame.For(env.Symmetry);
        var w = env.LandPerTeam > WideLaneLand ? 3 : 2;               // the map-wide lane width
        // the frontline is the default when there is budget for it; none is the sampled exception
        var hasFrontline = env.LandPerTeam >= FrontlineMinLand && rng.NextInt(0, NoFrontlineInN) > 0;
        var plan = SamplePlan(env, rng, hasFrontline);

        var depthCap = HubCapCells(env.LandPerTeam);
        var wideCap = HubWideCap(env.LandPerTeam);
        var floor = w + 2;
        var hubU = rng.NextInt(floor, Math.Max(floor, depthCap) + 1);    // depth toward the axis — kept compact
        var hubV = rng.NextInt(floor, Math.Max(floor, wideCap) + 1);     // lateral span — elongates across the team's width
        // the span is drawn free of parity. It used to be rounded to even under a laterally-flipping symmetry so
        // the hub would coincide with its own image and the two sides' fronts line up — but the thing that has
        // to coincide is the face, which carries its own parity law and is what the finished unit is centred on.
        // The hub rides along and is free to sit off the axis, so half the size ladder no longer has to be spent
        // buying an alignment it does not provide.
        // the frontline sits between the hub and the axis, so its reach pushes the hub's front edge back; the +2
        // gives a staple frontline's arms room for a real bay (a shallower reach collapses them to nubs)
        var frontReach = hasFrontline ? w + 2 : 0;
        // the axis margin is the mid crossing's half-gap when the caller carries one (the composed path — the
        // mid box arithmetic decides how far the unit's front sits from the axis); the plain default otherwise
        var hubUMin = (crossing?.HalfGapCells ?? Envelope.AxisMarginCells) + frontReach;
        var hubVMin = -(hubV / 2);
        var hubRect = frame.ToRect(hubUMin, hubU, hubVMin, hubV);

        // the neighbour demands (spawn + wools + the frontline join), sized from the budget before the form is chosen
        var demands = Demands(env, rng, plan, w, hubU, hubV, frontReach);

        // pick the hub form from the box's real dims (frame-mapped — the wide axis afford the wide holed bodies),
        // seat the demands on its free edges; fall back to the solid rectangle (four full edges) when the offerable
        // surface can't host
        var sampled = ChooseHubForm(hubRect.Width, hubRect.Height, rng);
        var walls = ChooseHubWalls(sampled, hubRect.Width, hubRect.Height, FillProfiles.HubWallCells, rng);
        // a branch hub's legs, drawn here rather than at fill time because the body is emitted twice — once to
        // read the runs it offers, once to build it — and a second draw would not agree with the first
        var arms = sampled.Form == Compound.SpineArms
            ? HubBoxEmitter.SampleArms(rng, hubRect.Width, sampled.Arms, FillProfiles.HubWallCells)
            : null;
        var seating = Seat(sampled, hubRect, frame, w, demands, rng, noFront: !hasFrontline, walls, arms);
        if (seating is null && sampled.Form != Compound.Rectangle)
            seating = Seat(new CompoundRead(Compound.Rectangle), hubRect, frame, w, demands, rng, noFront: !hasFrontline);
        if (seating is not { } s) return null;

        return (new BoxPartition(s.Boxes, s.Joints), frame.TowardAxis);
    }

    /// <summary>Choose the hub form for a <paramref name="boxW"/>×<paramref name="boxH"/> box (real cell dims, the
    /// frame having mapped the wide lateral axis onto width). A <b>wide</b> box (width ≥ <see cref="WideHubCells"/>,
    /// height ≥ <see cref="RingFitCells"/>) affords the <b>wide holed bodies</b> — the P (a loop on a long overhanging
    /// bar), the Double-hole (a ring + a docked U, two equal holes), and the G (a ring + an L, the ring's hole plus a
    /// frontline-sealed bay — asymmetric holes), whose long runs are free surface — sampled alongside the elongated
    /// ring. A <b>big square-ish</b> box (both ≥ <see cref="RingFitCells"/>) is too much solid area for the
    /// budget, so it prefers negative space: mostly the ring, else a branch body. A small or thin box stays the
    /// compact solid/branch menu (the wider forms would directed-null and fall back).</summary>
    private static CompoundRead ChooseHubForm(int boxW, int boxH, ComposeRng rng)
    {
        if (boxW >= WideHubCells && boxH >= RingFitCells)
            return rng.Pick(new[]
            {
                new CompoundRead(Compound.P), new CompoundRead(Compound.DoubleHole),
                new CompoundRead(Compound.G), new CompoundRead(Compound.Ring),
                // the U belongs here too: with the bay bounded above, a wide box spends its span on the legs, so
                // the two-legged hub comes out with real legs rather than stubs either side of a gap
                new CompoundRead(Compound.SpineArms, 2),
            });
        if (boxW >= RingFitCells && boxH >= RingFitCells)
            return rng.NextBool(RingChance) ? new CompoundRead(Compound.Ring)
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
    internal static RingWalls? ChooseHubWalls(CompoundRead form, int boxW, int boxH, int cw, ComposeRng rng)
    {
        // the ring inside each form: the Ring is the box, the docked forms (P/DoubleHole/G) keep a bar's width
        // beside it — the same arithmetic the hub filler builds them with
        var (ringW, ringH) = form.Form switch
        {
            Compound.Ring => (boxW, boxH),
            Compound.P or Compound.DoubleHole or Compound.G => (boxW - 2 * cw, boxH),
            _ => (0, 0),                                        // no ring to widen
        };
        if (ringW <= 0 || !rng.NextBool(WidenedRingChance)) return null;

        // the slack on each axis: what the hole keeps beyond a corridor of its own once both walls are paid for
        int slackW = ringW - 2 * cw - cw, slackH = ringH - 2 * cw - cw;
        var sides = new List<(int Side, int Room)>();
        if (slackW > 0) { sides.Add((1, slackW)); sides.Add((3, slackW)); }   // right, left
        if (slackH > 0) { sides.Add((0, slackH)); sides.Add((2, slackH)); }   // top, bottom
        if (sides.Count == 0) return null;

        var (side, room) = rng.Pick(sides);
        var extra = rng.NextInt(1, Math.Min(room, cw) + 1);     // ≤ cw keeps the widest within 2× the narrowest
        return side switch
        {
            0 => new RingWalls(cw + extra, cw, cw, cw),
            1 => new RingWalls(cw, cw + extra, cw, cw),
            2 => new RingWalls(cw, cw, cw + extra, cw),
            _ => new RingWalls(cw, cw, cw, cw + extra),
        };
    }

    /// <summary>The neighbour boxes to seat: the spawn (a straight I for now — cross = entry width, seats
    /// cleanly; the L's overhanging foot lands next), the budget-share-sized wools, each on its planned side
    /// (the free sides first, a third doubling into the spawn's edge), and — when the plan carries one — the
    /// frontline join on the front side (reach × a face spanning the hub front). The spawn size is the one RNG
    /// draw here; the wool sizes read the budget (generic, no per-family solve), so the whole set is fixed before
    /// the form is chosen and is identical across a fallback re-seat.</summary>
    private static IReadOnlyList<Demand> Demands(
        ComposeEnvelope env, ComposeRng rng, UnitPlan plan, int w, int hubU, int hubV, int frontReach)
    {
        var demands = new List<Demand>();

        var iSizes = FillProfiles.SpawnSizes.Where(sz => sz.Family == ShapeFamily.I).ToList();
        var size = iSizes[rng.NextInt(0, iSizes.Count)];
        var (spW, spH) = SpawnBoxEmitter.Box(size.Family, w, size.RunCells, size.TurnCells);
        demands.Add(new Demand(plan.Spawn, BoxKind.Spawn, spH, spW, "spawn"));

        // the flexible budget left after the hub, split into a rough share per wool (the spawn takes one too)
        var budgetCells = env.LandPerTeam / (env.Cell * (double)env.Cell);
        var flexible = Math.Max(0.0, budgetCells - hubU * hubV);
        var woolShare = flexible / (plan.Wools.Count + 1.0);
        for (var i = 0; i < plan.Wools.Count; i++)
        {
            var side = plan.Wools[i];
            var edgeLen = side is UnitSide.Front or UnitSide.Back ? hubV : hubU;
            var (fill, along, depth) = WoolDemand(rng, edgeLen, woolShare);
            demands.Add(new Demand(side, BoxKind.Wool, depth, along, $"wool-{(char)('a' + i)}", fill));
        }

        // the frontline join: it docks the hub's front edge with a face spanning it (corner clearance aside) and
        // reaches `frontReach` toward the axis; the filler picks its form (Bar / single / twin) and orientation
        if (plan.Frontline is { } front)
        {
            // G123: the face is no longer pinned to the hub's full front width. A sampled width — seated anywhere
            // along the edge and free to overhang it — is the funnel: the mid meets only part of the hub front,
            // so the two onward routes around the front cost differently. The full face stays the common draw.
            var full = Math.Max(w, hubV - 2 * CornerClearanceCells);
            var faceWidth = rng.NextBool(FullFaceChance)
                ? full
                : rng.NextInt(Math.Min(FaceMinCells, full), full + FaceOverhangMaxCells + 1);
            // the face-parity law. Under a laterally-flipping symmetry the opposing image reflects v about the
            // axis point, so a face spanning [lo, hi) meets its own image only where [lo, hi) and [-hi, -lo)
            // overlap — which is exact when lo = -hi, i.e. when the span is EVEN and the face is centred. The hub
            // is already forced even for this reason; an odd face lands half a cell off the centre the seat aims
            // at and the band has to reach past it. Parity is all that is required — no lane multiple, and the
            // rule reads the same in blocks as in cells because the cell size is odd.
            if (MidCarver.LateralFlip(env.Symmetry) && faceWidth % 2 != 0) faceWidth--;
            demands.Add(new Demand(front, BoxKind.Frontline, frontReach, faceWidth, "frontline"));
        }
        return demands;
    }

    /// <summary>Choose one wool's <b>shape and footprint</b> — the whole per-wool decision in one place. Three
    /// outcomes, in the order they are sampled:
    /// <list type="bullet">
    /// <item><b>rich</b> — a donut or a full-mouth staple (<c>U</c>/<c>H</c>/clamp), else a bent <c>L</c>; sized at
    /// the family's mouth box. A staple whose mouth the hub edge (<paramref name="edgeLen"/>) cannot hold demotes
    /// to the <c>L</c>, which the seat-and-shift docks at any width.</item>
    /// <item><b>side-tuck</b> — a compact side-room <c>I</c>, taken when the budget lane would run long (the wool
    /// length rule) or simply by chance.</item>
    /// <item><b>back-room lane</b> — a short inline <c>I</c>, its depth the budget share capped under the same
    /// length rule.</item>
    /// </list>
    /// The wool lane is always <see cref="WoolLaneCells"/> (§4), never the map's <c>w</c>.</summary>
    private static (WoolFill Fill, int Along, int Depth) WoolDemand(ComposeRng rng, int edgeLen, double woolShare)
    {
        var cw = WoolLaneCells;
        if (rng.NextBool(BentWoolChance))
        {
            var family = rng.NextBool(DonutChance) ? ShapeFamily.Donut
                : rng.NextBool(StapleChance) ? rng.Pick(new[] { ShapeFamily.U, ShapeFamily.H, ShapeFamily.Clamp })
                : ShapeFamily.L;
            // clamp: adjacent vs centered; donut: the wool at the ring's corner vs on a trailing room
            var woolAtEnd = family switch
            {
                ShapeFamily.Clamp => rng.NextBool(ClampAdjacentChance),
                ShapeFamily.Donut => rng.NextBool(DonutCornerWoolChance),
                _ => false,
            };
            var (along, depth) = WoolBoxEmitter.MouthBox(family, cw, woolAtEnd: woolAtEnd);
            // the donut's growth knobs: the hub-entry width (the min-only one-corridor entry read as a real
            // chokepoint) and the enclosed hole up to the along × deep caps — the box grows and the emitter's
            // ring absorbs it. The min box stays the floor, so a crowded hub falls back exactly as before.
            var attachW = 0;
            if (family == ShapeFamily.Donut)
            {
                attachW = rng.NextInt(cw, DonutEntryMaxCells + 1);
                var holeAlong = rng.NextInt(1, DonutHoleAlongMaxCells + 1);
                var holeDeep = rng.NextInt(cw, DonutHoleDeepMaxCells + 1);
                depth += holeDeep - cw;
                along = Math.Max(along, Math.Max(2 * cw + holeAlong, attachW + cw));
            }
            if (!Overhangs(family) && along > edgeLen)
                (family, woolAtEnd, (along, depth)) = (ShapeFamily.L, false, WoolBoxEmitter.MouthBox(ShapeFamily.L, cw));
            return (new WoolFill(family, RoomPlacement.Inline, false, woolAtEnd, attachW), along, depth);
        }

        // the budget's rough lane: the share spread over a narrow along-extent, the rest becoming depth
        var rd = ShapeEmitter.RoomDepthCells;
        var maxDepth = WoolLengthRatio * Math.Max(cw, rd) - 1;
        var narrowAlong = Math.Clamp((int)Math.Round(Math.Sqrt(woolShare)), cw, Math.Min(WoolAlongCapLanes * cw, edgeLen));
        var budgetDepth = (int)Math.Round(woolShare / narrowAlong);

        // NB the short-circuit is load-bearing: a lane that would run long side-tucks WITHOUT consuming a draw
        if (budgetDepth > maxDepth || rng.NextBool(SideRoomChance))
        {
            var tuck = new WoolFill(ShapeFamily.I, RoomPlacement.SideTuck, false);
            var (along, depth) = WoolBoxEmitter.MouthBox(tuck.Family, cw, tuck.Placement);
            return (tuck, along, depth);
        }

        return (new WoolFill(ShapeFamily.I, RoomPlacement.Inline, false),
            cw, Math.Clamp(budgetDepth, rd + 1, maxDepth));
    }

    /// <summary>Seat every demand on <paramref name="form"/>'s real free-edge intervals, seated on the hub
    /// <paramref name="hubRect"/>. Builds the body once (<see cref="HubBoxEmitter"/>) — the same body the filler
    /// re-emits, so both read the same runs — and reads its per-edge free runs off the emitted offers (the
    /// offerable surface, §1.13). Returns the hub box (carrying <paramref name="form"/> for the filler) plus the
    /// seated neighbour boxes and their hub joints, or <c>null</c> when the box is too small for the form or a
    /// demand finds no free run to dock (the directed signal the caller answers by falling back / resampling).</summary>
    private static (List<Box> Boxes, List<BoxJoint> Joints)? Seat(
        CompoundRead form, CellRect hubRect, Frame frame, int w, IReadOnlyList<Demand> demands, ComposeRng rng,
        bool noFront, RingWalls? walls = null, IReadOnlyList<(int Start, int Width)>? arms = null)
    {
        int boxW = hubRect.Width, boxH = hubRect.Height;
        var frontEdge = SideEdge(frame, UnitSide.Front);
        // orient the form so its open feet face the unused front (SP: the frontline's side) and its solid edges
        // cover the demanded back/laterals — a vertical flip when the front is the box's top edge (every z-frame);
        // symmetric forms (Rectangle, Ring) are unaffected, so this is safe to apply uniformly
        var flipV = frontEdge == BoxEdge.Top;
        var hubBox = new Box("hub", BoxKind.Hub, hubRect, boxW * boxH, form, flipV,
            HubWalls: walls, HubArms: arms);
        if (HubBoxEmitter.Fill(hubBox, form, FillProfiles.HubWallCells, flipV: flipV, ringWalls: walls,
                armLayout: arms) is not { } hub)
            return null;   // too small

        // the offerable surface: the contiguous free runs on each hub edge (box-local along-coords), read off the
        // emitted body's per-edge offers — one offer per free run, so a bay simply yields no run over its stretch
        var runsByEdge = hub.Offers.GroupBy(o => o.Edge).ToDictionary(
            g => g.Key,
            g => (IReadOnlyList<(int Start, int Len)>)g.Select(o => (o.Interval.Start, o.Interval.LengthCells)).ToList());

        var boxes = new List<Box> { hubBox };
        var joints = new List<BoxJoint>();
        // seats left flush with the front — handed to the FrontGuard post-pass once every neighbour is seated
        var flushSeats = new List<FrontGuard.FlushSeat>();

        // the seat-step separation law: no spawn/wool neighbour may seat within the gap (the map lane width — w2 =
        // 10 blocks, w3 = 15 on wide boards) of another. Each already-seated spawn/wool projects onto the edge
        // being seated as a forbidden along-interval, so SeatInRuns samples a legal position directly — one pass
        // covering same-edge abut and adjacent-edge corner meetings alike. The frontline keeps no such gap (its
        // wool clearance is a build-zone rule, not this one).
        List<(int Start, int Len)> Blocked(BoxEdge edge, int depth) => boxes
            .Where(b => b.Kind is BoxKind.Spawn or BoxKind.Wool)
            .Select(b => ProjectOntoEdge(edge, hubRect, depth, b.Rect, w))
            .Where(iv => iv is not null).Select(iv => iv!.Value).ToList();

        // record a seated neighbour: its box, and the joint granting it its corridor width. One place, so the
        // three dock styles differ only in how they FOUND the rect — never in what they record.
        void Seated(Demand nb, CellRect rect, BoxInterface abutment, int cw)
        {
            boxes.Add(new Box(nb.Id, nb.Kind, rect, nb.Along * nb.Depth, Wool: nb.Wool));
            joints.Add(HubJointFrom("hub", nb.Id, abutment, cw));
        }

        foreach (var demand in demands)
        {
            var d = demand;
            var edge = SideEdge(frame, d.Side);
            var edgeLen = edge is BoxEdge.Top or BoxEdge.Bottom ? boxW : boxH;
            if (!runsByEdge.TryGetValue(edge, out var runs)) return null;      // the form leaves this edge empty
            var offerW = d.Kind == BoxKind.Wool ? WoolLaneCells : w;           // the wool lane is w2; spawn/frontline read w
            var style = StyleOf(d);

            if (style is DockStyle.Overhang && d.Wool is { } rich)
            {
                // no frontline ⇒ prefer the overhang placement furthest behind the front face (bent back / flipped),
                // not spiking across the empty no-man's-land in front of the hub
                var guardFront = noFront ? frontEdge : (BoxEdge?)null;
                if (SeatOverhang(runs, edgeLen, d, rich, edge, hubRect, boxes, offerW, w, guardFront, rng) is { } placed)
                {
                    Seated(d with { Wool = rich with { Flip = placed.Flip } }, placed.Box, placed.Iface, offerW);
                    continue;
                }
                // no clear overhang placement on this hub (crowded / narrow): demote to the compact I and
                // re-dispatch as a full mouth. The demotion IS the fallback ladder — stated, not fallen through.
                d = Compact(d, offerW);
                style = DockStyle.FullMouth;
            }

            if (style is DockStyle.ContactPatch)
            {
                if (SeatFront(runs, edgeLen, d, edge, hubRect, boxes, w, rng) is not { } placed) return null;
                Seated(d, placed.Box, placed.Iface, offerW);
                continue;
            }

            if (SeatFullMouth(runs, edgeLen, d, edge, hubRect, Blocked, w, offerW, noFront, frontEdge, rng)
                is not { } dock)
            {
                // a wool that no longer fits with the seat gap (the third wool doubling onto the spawn's own edge
                // cannot clear the gap on a small hub — it only ever fit by touching) is dropped rather than
                // failing the whole unit, so long as a wool already seated: the unit keeps its objectives, one
                // fewer. The spawn and frontline are not droppable — a demand they cannot seat is a real too-small
                // signal the caller answers by falling back / resampling.
                if (d.Kind == BoxKind.Wool && boxes.Any(b => b.Kind == BoxKind.Wool)) continue;
                return null;
            }
            if (dock.Flush is { } flush) flushSeats.Add(flush);
            Seated(dock.Demand, dock.Box, dock.Iface, offerW);   // dock.Demand — a full mouth may have demoted it
        }

        // FrontGuard.Resolve — the post-pass over the seating: the seats the immediate slide could not bring
        // off the front are shifted / relocated / dropped there, deterministically (no draws). A residue on a
        // non-rectangle form is the directed "cannot host" signal — the caller's rectangle fallback re-seats on
        // four full edges, which usually hold a lawful off-front seat the form's runs could not; only the
        // rectangle itself keeps the flush seat, the flagged residue of a truly saturated hub.
        if (flushSeats.Count > 0)
        {
            var (rBoxes, rJoints, residue) = FrontGuard.Resolve(boxes, joints, flushSeats, hubRect, frontEdge, w, runsByEdge);
            if (residue > 0 && form.Form != Compound.Rectangle) return null;
            (boxes, joints) = (rBoxes, rJoints);
        }
        return (boxes, joints);
    }

    /// <summary>What a full-mouth dock produced: the placed <see cref="Box"/> rect and the
    /// <see cref="Iface"/> it abuts the hub over, the <see cref="Demand"/> <b>as it ended up</b> (a wool whose
    /// full mouth found no run is demoted to the compact <c>I</c>, so this may differ from the one passed in),
    /// and the <see cref="Flush"/> seat to hand <see cref="FrontGuard.Resolve"/> when the immediate slide found
    /// no backward position. <c>null</c> from <see cref="SeatFullMouth"/> means no legal seat at all.</summary>
    private sealed record FullMouthDock(
        CellRect Box, BoxInterface Iface, Demand Demand, FrontGuard.FlushSeat? Flush);

    /// <summary>
    /// Seat a neighbour by <b>full mouth</b>: its whole along-extent must lie inside one of the hub's free
    /// <paramref name="runs"/>. The shape-agnostic rule — it assumes nothing about where the shape's entries
    /// are, so it serves the spawn, the plain <c>I</c> wools and the dual-entry staples alike (an overhang
    /// would strand a staple's second entry off the hub).
    ///
    /// <para>A wool whose mouth no run holds is demoted once to the compact <c>I</c> and retried; the demand
    /// that comes back on <see cref="FullMouthDock.Demand"/> is the one the caller must build the box from.
    /// The no-frontline front guard then slides a lateral seat backward off the hub's front face
    /// (deterministic, no draw); a seat no backward position can hold is returned as a
    /// <see cref="FrontGuard.FlushSeat"/> for the post-pass rather than failing here.</para>
    ///
    /// <para><paramref name="blocked"/> is the caller's projection of the already-seated spawn/wool boxes onto
    /// an edge — passed as a delegate because it closes over the boxes seated so far, which grows as the loop
    /// runs.</para>
    /// </summary>
    private static FullMouthDock? SeatFullMouth(
        IReadOnlyList<(int Start, int Len)> runs, int edgeLen, Demand demand, BoxEdge edge, CellRect hubRect,
        Func<BoxEdge, int, List<(int Start, int Len)>> blocked, int w, int offerW,
        bool noFront, BoxEdge frontEdge, ComposeRng rng)
    {
        var d = demand;
        var seatGap = d.Kind is BoxKind.Spawn or BoxKind.Wool ? w : 0;
        List<(int Start, int Len)> blk = seatGap > 0 ? blocked(edge, d.Depth) : [];
        var seat = SeatInRuns(runs, blk, edgeLen, d.Along, CornerClearanceCells, seatGap, rng);
        if (seat is null && d.Kind == BoxKind.Wool)   // a staple's full mouth found no run — the compact I will
        {
            d = Compact(d, offerW);
            blk = blocked(edge, d.Depth);
            seat = SeatInRuns(runs, blk, edgeLen, d.Along, CornerClearanceCells, seatGap, rng);
        }
        if (seat is not { } s) return null;

        // no-frontline front guard: a lateral seat flush with the hub front face slides back to the nearest
        // clear off-front position (deterministic — no draw, so a seat already off the front re-seats
        // bit-identically); a seat no backward position can hold yet (the separation gap blocks the whole edge)
        // is handed to the FrontGuard.Resolve post-pass instead.
        FrontGuard.FlushSeat? flush = null;
        if (noFront && d.Kind is BoxKind.Spawn or BoxKind.Wool
            && edge != frontEdge && edge != Opposite(frontEdge))
        {
            if (FrontGuard.ShiftOffFront(runs, blk, edgeLen, d.Along, seatGap, s,
                    frontAtLow: frontEdge is BoxEdge.Top or BoxEdge.Left) is { } offFront)
                s = offFront;
            else flush = new FrontGuard.FlushSeat(d.Id, d.Kind, d.Depth, d.Along, edge, edgeLen, runs, seatGap);
        }
        return new FullMouthDock(
            NeighbourRect(edge, s, d.Depth, d.Along, hubRect), new BoxInterface(edge, s, d.Along), d, flush);
    }

    /// <summary>Demote a wool demand to the <b>compact inline <c>I</c></b> — the always-seatable shape: a
    /// one-lane mouth at the hub's offered width, its depth capped under the wool length rule. Both seat failures
    /// land here (an overhang with no clear placement, a full mouth no run holds) rather than failing the unit.</summary>
    private static Demand Compact(Demand d, int offerW) =>
        d with
        {
            Along = offerW,
            Depth = Math.Min(d.Depth, WoolLengthRatio * ShapeEmitter.RoomDepthCells - 1),
            Wool = new WoolFill(ShapeFamily.I, RoomPlacement.Inline, false),
        };

    /// <summary>The plan-cell rect of a <paramref name="depth"/>×<paramref name="along"/> box seated at box-local
    /// along-coord <paramref name="seat"/> on the hub's <paramref name="edge"/>: its depth reaches outward from
    /// that edge, its along-extent runs along it. Frame-free — the (u, v) frame chose the edge; the box then
    /// follows the edge's outward normal (Top −z, Bottom +z, Left −x, Right +x), so the seating needs no per-mode
    /// branch and stays correct where a (u, v)→box-local run mapping would reverse.</summary>
    internal static CellRect NeighbourRect(BoxEdge edge, int seat, int depth, int along, CellRect hub)
    {
        int hx = hub.X, hz = hub.Z, hw = hub.Width, hh = hub.Height;
        return edge switch
        {
            BoxEdge.Top => new(hx + seat, hz - depth, along, depth),
            BoxEdge.Bottom => new(hx + seat, hz + hh, along, depth),
            BoxEdge.Left => new(hx - depth, hz + seat, depth, along),
            _ => new(hx + hw, hz + seat, depth, along),                  // Right
        };
    }

    /// <summary>Project a <paramref name="seated"/> box onto <paramref name="edge"/> as the forbidden along-interval
    /// (box-local) a candidate of outward <paramref name="depth"/> must keep the seat gap clear of — but only when
    /// the box lies within <paramref name="gap"/> of that edge on the <b>perpendicular</b> axis (else it is too far
    /// out to constrain this edge and returns <c>null</c>). The along-gap itself is applied by <see cref="SeatInRuns"/>'s
    /// inflation, so this returns the box's raw along-extent. A same-edge neighbour projects to its own dock interval
    /// (perpendicular distance 0); an adjacent-edge neighbour projects only when it hugs the shared corner — so the
    /// one mechanism covers both the same-edge abut and the cross-edge corner meeting exactly (the along + perp
    /// conditions reproduce <see cref="TooClose"/>), and a legal seat is sampled directly, never single-sample
    /// rejected.</summary>
    internal static (int Start, int Len)? ProjectOntoEdge(BoxEdge edge, CellRect hub, int depth, CellRect seated, int gap)
    {
        int hx = hub.X, hz = hub.Z, hw = hub.Width, hh = hub.Height;
        int bx0 = seated.X, bz0 = seated.Z, bx1 = seated.X + seated.Width, bz1 = seated.Z + seated.Height;
        var (perpNear, aStart, aEnd) = edge switch
        {
            BoxEdge.Top => (bz0 - gap < hz && hz - depth < bz1 + gap, bx0 - hx, bx1 - hx),
            BoxEdge.Bottom => (bz0 - gap < hz + hh + depth && hz + hh < bz1 + gap, bx0 - hx, bx1 - hx),
            BoxEdge.Left => (bx0 - gap < hx && hx - depth < bx1 + gap, bz0 - hz, bz1 - hz),
            _ => (bx0 - gap < hx + hw + depth && hx + hw < bx1 + gap, bz0 - hz, bz1 - hz),   // Right
        };
        return perpNear ? (aStart, aEnd - aStart) : null;
    }

    /// <summary>Two plan-cell rects lie within <paramref name="gap"/> cells of each other by rectilinear
    /// nearest-approach — touching (gap 0) and corner-touching included. Equivalent to inflating one rect by the
    /// gap on all four sides and testing overlap, so a diagonal corner meeting is caught, not only a shared edge.
    /// The seat-step separation law reads it: no two neighbour bodies may sit this close (<paramref name="gap"/>
    /// is the map's lane width — w2 = 10 blocks, w3 = 15 on wide boards).</summary>
    internal static bool TooClose(CellRect a, CellRect b, int gap) =>
        a.X - gap < b.X + b.Width && b.X < a.X + a.Width + gap &&
        a.Z - gap < b.Z + b.Height && b.Z < a.Z + a.Height + gap;

    /// <summary>A free box-local along-position for an <paramref name="along"/>-wide dock among the edge's
    /// <paramref name="runs"/> (its offerable surface), avoiding the <paramref name="occupied"/> intervals (each
    /// inflated by <paramref name="gap"/> — the inter-seat separation law, so two neighbours on one edge never
    /// abut) and an <paramref name="inset"/>-cell clearance at each <b>box corner</b> — a run end coinciding with
    /// along-coord 0 or <paramref name="edgeLen"/>, so no neighbour seats at a hub corner and corner-touches a
    /// neighbour on the adjacent side; an internal run end (a bay boundary) is no box corner and needs no inset.
    /// Sampled within a randomly chosen fitting gap, or null when no gap holds it. The <paramref name="gap"/> is
    /// a neighbour↔neighbour clearance only — distinct from the corner law (corners keep <paramref name="inset"/>
    /// 0; the mass-level pinch gate owns the hub's own corners).</summary>
    private static int? SeatInRuns(
        IReadOnlyList<(int Start, int Len)> runs, List<(int Start, int Len)> occupied,
        int edgeLen, int along, int inset, int gap, ComposeRng rng)
    {
        var gaps = new List<(int Lo, int Hi)>();
        foreach (var (rs, rl) in runs)
        {
            int lo = rs, hi = rs + rl;
            if (lo == 0) lo += inset;                                 // a box corner at the low end
            if (hi == edgeLen) hi -= inset;                          // a box corner at the high end
            var cursor = lo;
            foreach (var (os, ol) in occupied
                .Where(o => o.Start - gap < hi && o.Start + o.Len + gap > lo).OrderBy(o => o.Start))
            {
                if (os - gap - cursor >= along) gaps.Add((cursor, os - gap));   // keep gap cells clear of the seat
                cursor = Math.Max(cursor, os + ol + gap);
            }
            if (hi - cursor >= along) gaps.Add((cursor, hi));
        }
        if (gaps.Count == 0) return null;
        var (glo, ghi) = gaps[rng.NextInt(0, gaps.Count)];
        return glo + rng.NextInt(0, ghi - glo - along + 1);
    }

    /// <summary>
    /// Seat the frontline on the hub's front edge by its <b>contact patch</b> (G123). Every other neighbour docks
    /// by fitting wholly inside a free run; the frontline does not have to, because its face is what the mid
    /// meets rather than a corridor the hub must hold. So a position is legal when the face abuts the hub over at
    /// least <paramref name="cw"/> contiguous cells of one free run — which admits a face narrower than the edge
    /// (seated anywhere along it) and one wider than the edge (overhanging either end).
    ///
    /// <para>Returns the plan-cell box and the <b>real</b> hub↔frontline interface, clipped to the abutment and
    /// so narrower than the box whenever it overhangs — the filler reads the offer off this, not off the face
    /// width. A face that overhangs must keep the neighbour separation gap from every seated spawn/wool: past
    /// the hub's corner there is no hub cell bridging the meeting, so a frontline corner and a wool corner would
    /// meet as a bare diagonal pinch, which the corner law forbids. (A full-width face meets those neighbours at
    /// the hub's own corner, which the hub fills — that is why the pinned face never needed this.) <c>null</c>
    /// when no position gives a patch — the directed signal the caller answers by falling back.</para>
    /// </summary>
    private static (CellRect Box, BoxInterface Iface)? SeatFront(
        IReadOnlyList<(int Start, int Len)> runs, int edgeLen, Demand d, BoxEdge edge, CellRect hubRect,
        IReadOnlyList<Box> seated, int cw, ComposeRng rng)
    {
        var placements = new List<(int Seat, CellRect Box, BoxInterface Iface)>();
        for (var seat = -(d.Along - cw); seat <= edgeLen - cw; seat++)
        {
            int lo = seat, hi = seat + d.Along;
            if (!Docks(runs, lo, hi, cw)) continue;
            if (PinchesAtEnd(runs, seat, seat + d.Along)) continue;
            var box = NeighbourRect(edge, seat, d.Depth, d.Along, hubRect);
            var overhangs = seat < 0 || seat + d.Along > edgeLen;
            if (seated.Any(b => b.Kind is BoxKind.Spawn or BoxKind.Wool
                    && (overhangs ? TooClose(b.Rect, box, cw) : Overlap(b.Rect, box)))) continue;
            if (BoxPartition.SharedEdge(hubRect, box) is { } iface) placements.Add((seat, box, iface));
        }
        if (placements.Count == 0) return null;

        // (see PinchesAtEnd for the end-alignment law the loop above applies)

        // Centred by default. Sliding the face along the edge is the funnel, and it costs the mid band slack
        // (Composer.FrontHullSlackCells) — so it is a sampled exception, not what every seat does. Without this
        // even a full-width face would land off-centre, since every overhanging position is legal too.
        if (!rng.NextBool(ShiftedFaceChance))
        {
            var centre = (edgeLen - d.Along) / 2.0;
            var best = placements.OrderBy(p => Math.Abs(p.Seat - centre)).ThenBy(p => p.Seat).First();
            return (best.Box, best.Iface);
        }
        var pick = placements[rng.NextInt(0, placements.Count)];
        return (pick.Box, pick.Iface);
    }

    /// <summary>
    /// The <b>spanning dock</b> (G123): whether a face covering edge-local <c>[lo, hi)</c> holds the hub properly.
    /// Its contact patches are where the face meets the edge's free <paramref name="runs"/>; it docks when there
    /// is at least one and <b>every</b> patch is at least <paramref name="cw"/> wide.
    ///
    /// <para>"Every", not "any", is the whole law. A face wide enough to reach across a bay-fronted hub's bay
    /// (a G, U or L) rests on a <b>shoulder each side of the hole</b>, and a shoulder thinner than a corridor is
    /// a sliver — the face is cantilevered over the bay, held by one side. Requiring the width per patch is what
    /// turns "the face happens to touch the far run" into "the face is anchored on both shoulders", which is what
    /// seals the bay into a declared hole rather than leaving a lip hanging over it.</para>
    ///
    /// <para>On a solid front there is one patch and this reduces to the single-patch rule.</para>
    /// </summary>
    private static bool Docks(IReadOnlyList<(int Start, int Len)> runs, int lo, int hi, int cw)
    {
        var patches = runs
            .Select(r => Math.Min(hi, r.Start + r.Len) - Math.Max(lo, r.Start))
            .Where(len => len > 0).ToList();
        return patches.Count > 0 && patches.All(len => len >= cw);
    }

    /// <summary>
    /// Whether a frontline spanning edge-local <c>[lo, hi)</c> would meet the hub's own edge terrain as a bare
    /// <b>diagonal pinch</b> at either of its ends — the corner law, applied where the face stops.
    ///
    /// <para>The frontline's spine is solid across its span, so along the shared edge the two masses meet cell by
    /// cell. The bad alignment is an end that lands where the hub's edge goes from <em>filled</em> just outside
    /// the face to <em>empty</em> just inside it (a hub bay starting exactly at the face's end): the face's end
    /// cell and the hub's last filled cell then touch only at a corner, with both orthogonal neighbours empty.
    /// An end inside a run is fine (the hub is filled under both sides), and so is an end at a run's start (both
    /// sides empty) or clear of the hub entirely (an overhang).</para>
    ///
    /// <para>This is why the pinned full-width face never needed the check: it ended at the hub's own corners,
    /// where there is no edge terrain beyond it to meet.</para>
    /// </summary>
    private static bool PinchesAtEnd(IReadOnlyList<(int Start, int Len)> runs, int lo, int hi)
    {
        bool Filled(int i) => runs.Any(r => r.Start <= i && i < r.Start + r.Len);
        return (Filled(lo - 1) && !Filled(lo)) || (Filled(hi) && !Filled(hi - 1));
    }

    /// <summary>Seat a <b>rich</b> wool by the seat-and-shift: probe the family's narrow <b>entry</b> on its mouth,
    /// place the box so that entry lands on a hub <paramref name="runs"/> interval while the wider body <b>overhangs</b>
    /// the edge, and reject any placement whose box overlaps a seated box. Both handednesses are tried (the body
    /// overhanging either way), so a crowded side does not sink the dock. Returns the plan-cell box, the actual
    /// hub↔box interface (the abutment — narrower than the box when it overhangs), and the chosen flip; or
    /// <c>null</c> when no clear placement exists (a directed signal the caller falls back on).</summary>
    private static (CellRect Box, BoxInterface Iface, bool Flip)? SeatOverhang(
        IReadOnlyList<(int Start, int Len)> runs, int edgeLen, Demand d, WoolFill fill, BoxEdge edge,
        CellRect hubRect, IReadOnlyList<Box> seated, int w, int gap, BoxEdge? guardFront, ComposeRng rng)
    {
        var mouth = Opposite(edge);
        var probeRect = edge is BoxEdge.Top or BoxEdge.Bottom ? new CellRect(0, 0, d.Along, d.Depth) : new CellRect(0, 0, d.Depth, d.Along);
        var placements = new List<(CellRect Box, bool Flip)>();
        foreach (var flip in new[] { false, true })
        {
            if (BoxFiller.EntryOn(new Box("probe", BoxKind.Wool, probeRect, 0), mouth, w, fill.Family, flip,
                    fill.Placement, fill.WoolAtEnd, fill.AttachmentWidth) is not { } e)
                continue;
            // the box's along-start (seat) values for which the entry [seat+e0, +eLen] lands within a run; the box
            // must abut the hub, never overlap a seated box, and keep the seat gap from any seated spawn/wool
            foreach (var (rs, rl) in runs)
                for (var seat = rs - e.Start; seat <= rs + rl - e.Start - e.Len; seat++)
                {
                    var box = NeighbourRect(edge, seat, d.Depth, d.Along, hubRect);
                    if (BoxPartition.SharedEdge(hubRect, box) is not null
                        && !seated.Any(b => Overlap(b.Rect, box))
                        && !seated.Any(b => b.Kind is BoxKind.Spawn or BoxKind.Wool && TooClose(b.Rect, box, gap)))
                        placements.Add((box, flip));
                }
        }
        if (placements.Count == 0) return null;

        // no-frontline front guard, overhang side: only placements buffered behind the hub front face
        // (≥ FrontGuard.BufferCells back) are kept, so the overhang bends back instead of spiking toward — or
        // sitting flush with — the front, where it would extend the face into one long flat frontier. When no
        // buffered placement exists (a tight hub) the dock falls to the compact I, which the full-mouth guard
        // seats off the front; sampling within the surviving placements keeps variety.
        if (guardFront is { } gf)
        {
            var tier = placements.Where(p => FrontGuard.Backness(p.Box, gf, hubRect) >= FrontGuard.BufferCells).ToList();
            if (tier.Count == 0) return null;
            placements = tier;
        }

        var (chosen, chosenFlip) = placements[rng.NextInt(0, placements.Count)];
        return (chosen, BoxPartition.SharedEdge(hubRect, chosen)!, chosenFlip);
    }

    /// <summary>Two plan-cell rects overlap iff they intersect on both axes (abutment is not overlap).</summary>
    private static bool Overlap(CellRect a, CellRect b) =>
        a.X < b.X + b.Width && b.X < a.X + a.Width && a.Z < b.Z + b.Height && b.Z < a.Z + a.Height;

    /// <summary>The box edge opposite <paramref name="e"/> — a neighbour's mouth faces the hub across it.</summary>
    internal static BoxEdge Opposite(BoxEdge e) => e switch
    {
        BoxEdge.Top => BoxEdge.Bottom, BoxEdge.Bottom => BoxEdge.Top,
        BoxEdge.Left => BoxEdge.Right, _ => BoxEdge.Left,
    };

    /// <summary>The hub↔neighbour joint over a ready-made <paramref name="iface"/> (the abutment of an overhanging
    /// dock), granting the consumer <paramref name="w"/> as its corridor width across it. The width is the
    /// consumer's selection, not the hub's published capacity — see <see cref="BoxJoint.Grant"/>.</summary>
    private static BoxJoint HubJointFrom(string hubId, string nbId, BoxInterface iface, int w)
    {
        var grant = new EdgeOffer(iface.Edge, new EdgeInterval(iface.Start, iface.WidthCells, ApproachSlots.Bar),
            w, OfferGrouping.Several, $"hub-{iface.Edge}");
        return new BoxJoint(hubId, nbId, iface, grant);
    }

    /// <summary>The hub↔neighbour joint on <paramref name="edge"/>: the interface interval where they touch, and
    /// the <see cref="BoxJoint.Grant"/> across it — width <paramref name="w"/>, which the neighbour reads as its
    /// <c>cw</c> (severally — each neighbour its own dock). <paramref name="w"/> is chosen by the consumer's kind
    /// upstream (the w2 wool lane, or the map lane width); the hub's own per-run capacity is a separate figure and
    /// is not what is recorded here.</summary>
    internal static BoxJoint HubJoint(string hubId, string nbId, BoxEdge edge, int alongStart, int along, int w) =>
        HubJointFrom(hubId, nbId, new BoxInterface(edge, alongStart, along), w);

    /// <summary>The hub's box edge facing <paramref name="side"/> — the (u, v) outward direction mapped through
    /// the <see cref="Frame"/> to a box-local edge (min-z Top, max-z Bottom, min-x Left, max-x Right).</summary>
    private static BoxEdge SideEdge(Frame frame, UnitSide side)
    {
        var (du, dv) = side switch
        {
            UnitSide.Front => (-1.0, 0.0),
            UnitSide.Back => (1.0, 0.0),
            UnitSide.Left => (0.0, -1.0),
            _ => (0.0, 1.0),                                          // Right
        };
        var (ox, oz) = frame.ToPoint(0, 0);
        var (px, pz) = frame.ToPoint(du, dv);
        double dx = px - ox, dz = pz - oz;
        if (dz < 0) return BoxEdge.Top;
        if (dz > 0) return BoxEdge.Bottom;
        return dx < 0 ? BoxEdge.Left : BoxEdge.Right;
    }
}
