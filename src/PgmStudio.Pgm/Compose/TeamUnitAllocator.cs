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

    /// <summary>The wool's own corridor width in cells — a <b>w2</b> lane (docs/contracts/map-generation.md §4:
    /// "the lane to the wool is simple, w2"), independent of the map's lane width <c>w</c> (which is 3 on big
    /// boards). Keeping wool families at w2 makes them compact and lets a staple's 3-lane mouth fit a hub edge.</summary>
    internal const int WoolLaneCells = 2;

    /// <summary>The widest hub-entry a donut may sample, in cells — the min-only entry (one corridor) read as a
    /// chokepoint, so the attachment stub varies up to this along the hub edge.</summary>
    private const int DonutEntryMaxCells = 5;

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
        // under a laterally-flipping symmetry (rot_180/rot_90) the opposing image mirrors v, so the hub must sit
        // symmetric about the axis centre for the two fronts to align parallel — an even span at -(hubV/2) is;
        // an odd draw rounds up within the cap (down at it), keeping the sampled spread
        if (MidCarver.LateralFlip(env.Symmetry) && hubV % 2 != 0)
            hubV = hubV + 1 <= Math.Max(floor, wideCap) ? hubV + 1 : hubV - 1;
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
        var sampled = ChooseHubForm(hubRect[2], hubRect[3], rng);
        var seating = Seat(sampled, hubRect, frame, w, demands, rng, noFront: !hasFrontline);
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
            });
        if (boxW >= RingFitCells && boxH >= RingFitCells)
            return rng.NextBool(RingChance) ? new CompoundRead(Compound.Ring)
                : rng.Pick(HubBoxEmitter.Forms.Where(f => f.Form is Compound.SpineArms).ToList());
        return rng.Pick(HubBoxEmitter.Forms.Where(f => f.Form is Compound.Rectangle or Compound.SpineArms).ToList());
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
            var faceWidth = Math.Max(w, hubV - 2 * CornerClearanceCells);
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
        CompoundRead form, int[] hubRect, Frame frame, int w, IReadOnlyList<Demand> demands, ComposeRng rng,
        bool noFront)
    {
        int boxW = hubRect[2], boxH = hubRect[3];
        var frontEdge = SideEdge(frame, UnitSide.Front);
        // orient the form so its open feet face the unused front (SP: the frontline's side) and its solid edges
        // cover the demanded back/laterals — a vertical flip when the front is the box's top edge (every z-frame);
        // symmetric forms (Rectangle, Ring) are unaffected, so this is safe to apply uniformly
        var flipV = frontEdge == BoxEdge.Top;
        var hubBox = new Box("hub", BoxKind.Hub, hubRect, boxW * boxH, form, flipV);
        if (HubBoxEmitter.Fill(hubBox, form, FillProfiles.HubWallCells, flipV: flipV) is not { } hub) return null;   // too small

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

        foreach (var demand in demands)
        {
            var d = demand;
            var edge = SideEdge(frame, d.Side);
            var edgeLen = edge is BoxEdge.Top or BoxEdge.Bottom ? boxW : boxH;
            if (!runsByEdge.TryGetValue(edge, out var runs)) return null;      // the form leaves this edge empty
            var offerW = d.Kind == BoxKind.Wool ? WoolLaneCells : w;           // the wool lane is w2; spawn/frontline read w

            // a single-entry rich wool docks by the seat-and-shift: its narrow entry lands on a run and the wider
            // body overhangs into free space. Every other neighbour (I, the dual-entry staple, spawn, frontline)
            // docks its full along-edge.
            if (d.Wool is { } rich && Overhangs(rich.Family))
            {
                // no frontline ⇒ prefer the overhang placement furthest behind the front face (bent back / flipped),
                // not spiking across the empty no-man's-land in front of the hub
                var guardFront = noFront ? frontEdge : (BoxEdge?)null;
                if (SeatOverhang(runs, edgeLen, d, rich, edge, hubRect, boxes, offerW, w, guardFront, rng) is { } placed)
                {
                    var (box, iface, flip) = placed;
                    boxes.Add(new Box(d.Id, d.Kind, box, d.Along * d.Depth, Wool: rich with { Flip = flip }));
                    joints.Add(HubJointFrom("hub", d.Id, iface, offerW));
                    continue;
                }
                d = Compact(d, offerW);   // no clear overhang placement on this hub (crowded / narrow)
            }

            var seatGap = d.Kind is BoxKind.Spawn or BoxKind.Wool ? w : 0;     // the frontline seats its full face
            var blocked = seatGap > 0 ? Blocked(edge, d.Depth) : [];
            var seat = SeatInRuns(runs, blocked, edgeLen, d.Along, CornerClearanceCells, seatGap, rng);
            if (seat is null && d.Kind == BoxKind.Wool)   // a staple's full mouth found no run — the compact I will
            {
                d = Compact(d, offerW);
                blocked = Blocked(edge, d.Depth);
                seat = SeatInRuns(runs, blocked, edgeLen, d.Along, CornerClearanceCells, seatGap, rng);
            }
            if (seat is not { } s)
            {
                // a wool that no longer fits with the seat gap (the third wool doubling onto the spawn's own edge
                // cannot clear the gap on a small hub — it only ever fit by touching) is dropped rather than
                // failing the whole unit, so long as a wool already seated: the unit keeps its objectives, one
                // fewer. The spawn and frontline are not droppable — a demand they cannot seat is a real too-small
                // signal the caller answers by falling back / resampling.
                if (d.Kind == BoxKind.Wool && boxes.Any(b => b.Kind == BoxKind.Wool)) continue;
                return null;
            }
            // no-frontline front guard, full-mouth side: a lateral seat flush with the hub front face slides
            // back to the nearest clear off-front position (deterministic — no draw, so a seat already off the
            // front re-seats bit-identically); a seat no backward position can hold yet (the separation gap
            // blocks the whole edge) is recorded for the FrontGuard.Resolve post-pass below.
            if (noFront && d.Kind is BoxKind.Spawn or BoxKind.Wool
                && edge != frontEdge && edge != Opposite(frontEdge))
            {
                if (FrontGuard.ShiftOffFront(runs, blocked, edgeLen, d.Along, seatGap, s,
                        frontAtLow: frontEdge is BoxEdge.Top or BoxEdge.Left) is { } offFront)
                    s = offFront;
                else flushSeats.Add(new FrontGuard.FlushSeat(d.Id, d.Kind, d.Depth, d.Along, edge, edgeLen, runs, seatGap));
            }
            boxes.Add(new Box(d.Id, d.Kind, NeighbourRect(edge, s, d.Depth, d.Along, hubRect), d.Along * d.Depth, Wool: d.Wool));
            joints.Add(HubJoint("hub", d.Id, edge, s, d.Along, offerW));
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
    internal static int[] NeighbourRect(BoxEdge edge, int seat, int depth, int along, int[] hub)
    {
        int hx = hub[0], hz = hub[1], hw = hub[2], hh = hub[3];
        return edge switch
        {
            BoxEdge.Top => [hx + seat, hz - depth, along, depth],
            BoxEdge.Bottom => [hx + seat, hz + hh, along, depth],
            BoxEdge.Left => [hx - depth, hz + seat, depth, along],
            _ => [hx + hw, hz + seat, depth, along],                  // Right
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
    internal static (int Start, int Len)? ProjectOntoEdge(BoxEdge edge, int[] hub, int depth, int[] seated, int gap)
    {
        int hx = hub[0], hz = hub[1], hw = hub[2], hh = hub[3];
        int bx0 = seated[0], bz0 = seated[1], bx1 = seated[0] + seated[2], bz1 = seated[1] + seated[3];
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
    private static bool TooClose(int[] a, int[] b, int gap) =>
        a[0] - gap < b[0] + b[2] && b[0] < a[0] + a[2] + gap &&
        a[1] - gap < b[1] + b[3] && b[1] < a[1] + a[3] + gap;

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

    /// <summary>Seat a <b>rich</b> wool by the seat-and-shift: probe the family's narrow <b>entry</b> on its mouth,
    /// place the box so that entry lands on a hub <paramref name="runs"/> interval while the wider body <b>overhangs</b>
    /// the edge, and reject any placement whose box overlaps a seated box. Both handednesses are tried (the body
    /// overhanging either way), so a crowded side does not sink the dock. Returns the plan-cell box, the actual
    /// hub↔box interface (the abutment — narrower than the box when it overhangs), and the chosen flip; or
    /// <c>null</c> when no clear placement exists (a directed signal the caller falls back on).</summary>
    private static (int[] Box, BoxInterface Iface, bool Flip)? SeatOverhang(
        IReadOnlyList<(int Start, int Len)> runs, int edgeLen, Demand d, WoolFill fill, BoxEdge edge,
        int[] hubRect, IReadOnlyList<Box> seated, int w, int gap, BoxEdge? guardFront, ComposeRng rng)
    {
        var mouth = Opposite(edge);
        var probeRect = edge is BoxEdge.Top or BoxEdge.Bottom ? new[] { 0, 0, d.Along, d.Depth } : new[] { 0, 0, d.Depth, d.Along };
        var placements = new List<(int[] Box, bool Flip)>();
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
    private static bool Overlap(int[] a, int[] b) =>
        a[0] < b[0] + b[2] && b[0] < a[0] + a[2] && a[1] < b[1] + b[3] && b[1] < a[1] + a[3];

    /// <summary>The box edge opposite <paramref name="e"/> — a neighbour's mouth faces the hub across it.</summary>
    internal static BoxEdge Opposite(BoxEdge e) => e switch
    {
        BoxEdge.Top => BoxEdge.Bottom, BoxEdge.Bottom => BoxEdge.Top,
        BoxEdge.Left => BoxEdge.Right, _ => BoxEdge.Left,
    };

    /// <summary>The hub↔neighbour joint over a ready-made <paramref name="iface"/> (the abutment of an overhanging
    /// dock), carrying the hub's <paramref name="w"/>-width offer over it.</summary>
    private static BoxJoint HubJointFrom(string hubId, string nbId, BoxInterface iface, int w)
    {
        var offer = new EdgeOffer(iface.Edge, new EdgeInterval(iface.Start, iface.WidthCells, ApproachSlots.Bar),
            w, OfferGrouping.Several, $"hub-{iface.Edge}");
        return new BoxJoint(hubId, nbId, iface, offer);
    }

    /// <summary>The hub↔neighbour joint carrying the hub's offer on <paramref name="edge"/>: the interface
    /// interval where they touch, and an <see cref="EdgeOffer"/> whose width is the lane width
    /// <paramref name="w"/> the neighbour reads as its <c>cw</c> (severally — each neighbour its own dock).</summary>
    internal static BoxJoint HubJoint(string hubId, string nbId, BoxEdge edge, int alongStart, int along, int w)
    {
        var iface = new BoxInterface(edge, alongStart, along);
        var offer = new EdgeOffer(edge, new EdgeInterval(alongStart, along, ApproachSlots.Bar), w, OfferGrouping.Several, $"hub-{edge}");
        return new BoxJoint(hubId, nbId, iface, offer);
    }

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
