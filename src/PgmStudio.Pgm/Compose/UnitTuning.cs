using PgmStudio.Geom;
using PgmStudio.Pgm.Shapes;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Compose;

/// <summary>The tuning constants and size ladders — where the unit's structure changes with the budget, how
/// often each shape is sampled, and the widths the seat step builds to — plus the frame-independent placement
/// plan they feed (<see cref="UnitPlan"/>).</summary>
public static class UnitTuning
{
    // ── the size ladders: the widths and shares a band buys ───────────────────────────────────────────────

    /// <summary>The map-wide corridor width in <b>blocks</b>, per size band — measured as where a CTW map's
    /// ground actually sits (<c>docs/world-scan/map-size-ladder.md</c>): the modal local thickness runs
    /// 8 · 10 · 14 · 16 · 16 from the smallest maps up, and the quartile a working lane sits at is
    /// 8 · 12 · 14 · 16 · 17. Every non-wool box builds to this width (LN1).</summary>
    internal static int CorridorBlocks(string band) => band switch
    {
        SizeBands.Micro => 14,
        SizeBands.Milli => 16,
        SizeBands.Centi => 16,
        SizeBands.Hecto => 22,
        _ => 12,
    };

    /// <summary>The map-wide corridor width in cells: <see cref="CorridorBlocks"/> laid on a
    /// <paramref name="cell"/>-block grid, rounded to the nearest whole cell and never under two. Rounded
    /// rather than ceilinged because the width is a target and not a floor — a 10-block lane is nearer a
    /// 12-block target than a 15-block one — and the floor of two keeps every grid at a corridor a player
    /// can fight in.</summary>
    internal static int CorridorCells(string band, int cell) =>
        Math.Max(2, (int)Math.Round(CorridorBlocks(SizeBands.Canonical(band)) / (double)cell,
                                    MidpointRounding.AwayFromZero));

    /// <summary>The <b>wool approach's</b> corridor width in blocks, per band — one rung below the map's
    /// (<see cref="CorridorBlocks"/>), because the ground beside a goal is the tightest on the board and the
    /// corpus's narrow quartile is where a goal approach sits.</summary>
    internal static int WoolCorridorBlocks(string band) => band switch
    {
        SizeBands.Micro => 12,
        SizeBands.Milli => 14,
        SizeBands.Centi => 14,
        SizeBands.Hecto => 18,
        _ => 10,
    };

    /// <summary>The wool approach's corridor width in cells on a <paramref name="cell"/>-block grid, never
    /// under <see cref="WoolLaneFloorCells"/>.</summary>
    internal static int WoolCorridorCells(string band, int cell) =>
        Math.Max(WoolLaneFloorCells,
                 (int)Math.Round(WoolCorridorBlocks(SizeBands.Canonical(band)) / (double)cell,
                                 MidpointRounding.AwayFromZero));

    /// <summary>One unit in this many has no frontline — the sampled exception that keeps the frontline from
    /// being universal. Every band's budget affords one, so nothing else withholds it.</summary>
    internal const int NoFrontlineInN = 7;

    /// <summary>How often a nano unit carries a <b>second</b> wool. One wool a team is what 57% of nano maps
    /// carry; two is the rest.</summary>
    internal const double SecondWoolChance = 0.4;

    /// <summary>Of micro-and-up units, how often the <b>third</b> wool appears (it doubles onto the spawn's
    /// side). Two is what 56–82% of those maps carry.</summary>
    internal const double ThirdWoolChance = 0.4;

    // ── the budget: what each box takes out of it ──────────────────────────────────────────────────────────

    /// <summary>The share a <b>frontline</b> is entitled to — the ground the mid is met on, and the second
    /// biggest claim after the hub.</summary>
    internal const double FrontlineShare = 0.22;

    /// <summary>The share <b>each wool</b> is entitled to: its approach, its room and whatever ring or legs
    /// its family wraps them in.</summary>
    internal const double WoolShare = 0.12;

    /// <summary>The least of the budget the <b>hub</b> takes however many neighbours claim their share — it is
    /// the junction every lane originates from, so a unit that spent itself on its edges has no middle.</summary>
    internal const double HubMinShare = 0.30;

    /// <summary>The narrowest the hub is drawn relative to its depth. The long lateral edge is what the spawn
    /// and the wools dock, so a hub is always wider than deep.</summary>
    internal const double HubAspectLow = 1.3;

    /// <summary>The widest. A hub sampled between the two varies in shape while holding the area its share
    /// bought — the aspect is the draw, the spend is not.</summary>
    internal const double HubAspectHigh = 2.4;

    /// <summary>The share of a box's footprint that comes out as <b>land</b> once a body is emitted into it:
    /// a solid rectangle spends all of it, a ring, a branch or a donut keeps a hole. The allocator can only
    /// aim in footprints, so a unit's footprint allowance is its land budget over this.</summary>
    internal const double FootprintLandYield = 0.75;

    /// <summary>The band a unit's built land must fall in against its budget, as a fraction of it — below
    /// <see cref="SpendFloor"/> the attempt left land unplaced, above <see cref="SpendCeiling"/> it built a
    /// board bigger than the band. Either resamples. The band is wide because a box's footprint is the only
    /// currency the allocator can aim in and a holed body's land is a fifth less than its footprint.</summary>
    public const double SpendFloor = 0.70;

    /// <summary>The upper end of that band.</summary>
    public const double SpendCeiling = 1.30;

    // ── the shape mix: which bodies a box may be ───────────────────────────────────────────────────────────

    /// <summary>The smallest hub dimension a <b>ring</b> fits in at corridor width
    /// <paramref name="corridorCells"/> — two walls and a hole one corridor wide. A hub at least this big on
    /// both axes is a "big square" and prefers negative space to solid area.</summary>
    internal static int RingFitCells(int corridorCells) => 2 * corridorCells + 1;

    /// <summary>Of big-square hubs, how often the form is the <b>ring</b> specifically rather than another
    /// negative-space body. The ring's void always fits and survives a frontline, so it carries most of them.</summary>
    internal const double RingChance = 0.85;

    /// <summary>Of ring-bodied hubs whose box has the slack for it, how often <b>one</b> wall comes out wider than
    /// the other three. The uniform ring stays the common case; a widened side reads as a deliberate variation —
    /// the author saying more play flows through there — rather than the house style.</summary>
    internal const double WidenedRingChance = 0.3;

    /// <summary>How long a wool may run relative to its room dimension before it reads as a <b>too-long
    /// single-entry corridor</b> — the wool length rule. A lane past this bound tucks its room to the side
    /// instead, and it also caps the depth of every compact fallback.</summary>
    internal const int WoolLengthRatio = 3;

    /// <summary>The widest a budget-sized wool lane may be, in lanes — the along-extent the budget share is
    /// spread over before it turns into depth.</summary>
    internal const int WoolAlongCapLanes = 3;

    /// <summary>The hub's box dims in cells for a hub owed <paramref name="hubTargetCells"/> of footprint at
    /// lateral-to-depth <paramref name="aspect"/>, on a board whose corridor is
    /// <paramref name="corridorCells"/>: the lateral (cross-axis) <c>Wide</c> span and the <c>Deep</c> depth
    /// toward the axis. The aspect is what varies between boards and the area is what the share fixed, so a
    /// hub spends what it was given whatever shape it comes out.
    ///
    /// <para>Depth is floored at <b>three corridors</b> — a wall, a corridor and a wall — because that is what
    /// a hub needs to come out a loop rather than a slab, and the lateral span is capped so the floor never
    /// pushes the box past its share. A shallower hub can only be solid however holed a form it is handed: its
    /// hole would be narrower than a lane.</para></summary>
    internal static (int Deep, int Wide) HubBoxCells(double hubTargetCells, double aspect, int corridorCells)
    {
        var target = Math.Max(1.0, hubTargetCells);
        var minDeep = 3 * corridorCells;
        var minWide = corridorCells + 2;
        var wide = Math.Clamp((int)Math.Round(Math.Sqrt(target * Math.Max(1.0, aspect))),
                              minWide, Math.Max(minWide, (int)(target / minDeep)));
        var deep = Math.Max(minDeep, (int)Math.Round(target / wide));
        return (deep, wide);
    }

    /// <summary>The box width at or above which a hub is <b>wide enough for the holed wide bodies</b> at
    /// corridor width <paramref name="corridorCells"/> — the P (loop + overhanging bar) and the Double-hole
    /// (ring + docked U) each keep a bar beside their ring, so <c>w − 2·cw ≥ 2·cw + 1</c>. Below it they
    /// directed-null and the compact menu is used.</summary>
    internal static int WideHubCells(int corridorCells) => 4 * corridorCells + 1;

    // ── the shape mix: how often each wool shape is sampled ────────────────────────────────────────────────

    /// <summary>How often a wool takes a bent <c>L</c> (the seat-and-shift) rather than an <c>I</c> — the shape
    /// variety, decoupled from the length rule so an <c>L</c> appears on any wool, not just the long ones. When
    /// the L's overhang cannot fit a crowded hub the seat falls back to a compact inline <c>I</c>.</summary>
    internal const double BentWoolChance = 0.4;

    /// <summary>Of the rich wools, how often a <b>donut</b> (a ring the wool sits in, reached around both ways)
    /// rather than a bent <c>L</c> — kept low because the ring is a big, deep footprint that mostly wants a
    /// less-crowded hub (else the overhang falls back to a compact inline <c>I</c>).</summary>
    internal const double DonutChance = 0.25;

    /// <summary>Of the non-donut rich wools, how often a <b>staple-class</b> two-leg wool (<c>U</c>/<c>H</c>/
    /// <c>clamp</c> — the wool reached by two legs off one mouth) rather than a bent <c>L</c>. It docks its full
    /// mouth (~3 lanes), so it needs a hub edge as wide as its mouth; where the edge is too narrow it demotes to
    /// an <c>L</c>, so the staple lands mostly on the wider hubs.</summary>
    internal const double StapleChance = 0.4;

    /// <summary>Of the clamp wools, how often the <b>adjacent/corner</b> variant (an <c>L+I</c> gripping the wool
    /// in a fold) rather than the <b>centered</b> one (two straight legs, <c>I+I</c>). Both dock the same full
    /// mouth; this only changes which two-leg shape clamps the cut-cell wool inside.</summary>
    internal const double ClampAdjacentChance = 0.4;

    /// <summary>Of the donut wools, how often the wool is <b>integrated at the ring's corner</b> rather than
    /// hung off its bottom-right on a trailing room. The corner wool costs no width past the ring, so the box
    /// loses the trailing <c>rd</c> — a squarer ring instead of the stretched min-box sliver.</summary>
    internal const double DonutCornerWoolChance = 0.5;

    /// <summary>How often a non-<c>L</c> wool tucks its room to the <b>side</b> (a compact side-room) rather than
    /// a plain inline back-room lane — for the three shapes to read in a balanced mix. A wool that would run long
    /// side-tucks regardless (the length rule).</summary>
    internal const double SideRoomChance = 0.4;

    // ── geometry: the widths and clearances the seat step builds to ────────────────────────────────────────

    /// <summary>The narrowest a wool lane may be at all, in cells — the <b>producible floor</b> a plan is
    /// gated against, not the width a composed wool is built to (that is
    /// <see cref="WoolCorridorCells"/>, which rises with the band). Two cells is one corridor a player can
    /// fight in on any grid.</summary>
    internal const int WoolLaneFloorCells = 2;

    /// <summary>The widest hub-entry a donut may sample, in cells — the min-only entry (one corridor) read as a
    /// chokepoint, so the attachment stub varies up to this along the hub edge. Never under the wool lane it is
    /// sampled from: the lane derives from the band and the grid, so a ceiling stated in cells can fall below it,
    /// and an empty sample range throws rather than refusing the attempt.</summary>
    internal static int DonutEntryMaxCells(int woolLaneCells) => Math.Max(woolLaneCells, 5);

    /// <summary>The donut's enclosed hole cap <b>along</b> the hub edge (the ring's mouth-side extent), in cells.
    /// Sampled from one, so it needs no lane floor.</summary>
    internal const int DonutHoleAlongMaxCells = 3;

    /// <summary>The donut's enclosed hole cap <b>deep</b> (outward), in cells. The min box gives the 1×2 hole;
    /// the sampled growth reaches 3×5 — the box grows and the emitter's ring absorbs it (its span derives from
    /// the box). Floored at the wool lane for the same reason
    /// <see cref="DonutEntryMaxCells"/> is.</summary>
    internal static int DonutHoleDeepMaxCells(int woolLaneCells) => Math.Max(woolLaneCells, 5);

    /// <summary>The clearance kept between a docked neighbour and each hub <b>corner</b>, in cells. Zero under the
    /// mass-level corner law: two neighbours on adjacent hub sides meet only at the hub's own corner cell, which
    /// the hub fills — a ¾-solid bridged corner, never a pinch — so no clearance is needed and the neighbours may
    /// use the hub's full edge (which the side-tuck wool and the wide frontline face want).</summary>
    internal const int CornerClearanceCells = 0;

    /// <summary>How often the frontline still takes the hub's <b>full</b> front width (G123). The pinned face
    /// stays the common case; a partial front is the deliberate exception, not the new default.</summary>
    internal const double FullFaceChance = 0.6;

    /// <summary>The narrowest sampled frontline face, in cells. Below two lanes a front reads as a nub stuck to
    /// the hub rather than a front the mid can meet.</summary>
    internal const int FaceMinCells = 4;

    /// <summary>How far a sampled face may <b>overhang</b> the hub's front edge, in cells (total across both
    /// sides). The frontline is the one neighbour allowed to be wider than the edge it docks: its face is what
    /// the mid meets, and a face that reaches past the hub is what turns the front into a funnel.</summary>
    internal const int FaceOverhangMaxCells = 2;

    /// <summary>How often the frontline slides off the centre of the hub's front edge, rather than sitting
    /// symmetric on it. Two knobs, because width and position are different decisions: the face may be partial
    /// and still centred. A slid face is what costs the mid band slack, so it stays the minority draw.</summary>
    internal const double ShiftedFaceChance = 0.35;

    // ── the plan: how many wools, and which side each neighbour takes ──────────────────────────────────────

    /// <summary>The wool-box count for the envelope's size band: one at nano, sometimes two; two from micro
    /// up, sometimes three (the third doubles onto the spawn's side).</summary>
    public static int WoolCount(ComposeEnvelope env, ComposeRng rng) =>
        env.Band == SizeBands.Nano
            ? (rng.NextBool(SecondWoolChance) ? 2 : 1)
            : (rng.NextBool(ThirdWoolChance) ? 3 : 2);

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
}
