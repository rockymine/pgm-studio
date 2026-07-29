using PgmStudio.Geom;
using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Compose;

/// <summary>The tuning constants and size ladders — where the unit's structure changes with the budget, how
/// often each shape is sampled, and the widths the seat step builds to — plus the frame-independent placement
/// plan they feed (<see cref="UnitPlan"/>).</summary>
public static class UnitTuning
{
    // ── the size ladders: where the unit's structure changes with the budget ───────────────────────────────

    /// <summary>Land per team above which the map's lane width is <b>3</b> cells rather than 2 (LN1: 15 blocks
    /// on big maps, 10 elsewhere). The one map-wide width every non-wool box builds to.</summary>
    internal const double WideLaneLand = 2500;

    /// <summary>Land per team below which a unit has <b>no frontline</b> — there is no budget for one, so the
    /// hub fronts the mid directly.</summary>
    internal const double FrontlineMinLand = 800;

    /// <summary>One unit in this many has no frontline even when the budget allows — the sampled exception that
    /// keeps the frontline from being universal.</summary>
    internal const int NoFrontlineInN = 7;

    /// <summary>Land per team below which a unit carries a <b>single</b> wool (a tiny board cannot hold two).</summary>
    internal const double TinyBoardLand = 600;

    /// <summary>Players per team at or above which the unit is a <b>full team</b>: 2–3 wools rather than 1–2.</summary>
    internal const int FullTeamPlayers = 16;

    /// <summary>Of full-team units, how often the third wool appears (it doubles onto the spawn's side).</summary>
    internal const double ThirdWoolChance = 0.4;

    /// <summary>The smallest hub dimension a <b>ring</b> fits in — a hub at least this big on both axes is a
    /// "big square" and prefers negative space to solid area.</summary>
    internal const int RingFitCells = 5;

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

    /// <summary>The hub's <b>depth</b> cap in cells (toward the axis) at <paramref name="landPerTeam"/> — how deep
    /// the budget warrants. Simplified ladder (the frontline / twin-recess / wool-c clearance floors refine it as
    /// those land); the floor is the lane width + 2 either way. The <b>lateral</b> span uses the wider
    /// <see cref="HubWideCap"/> so the hub elongates across the team's width rather than growing a bigger square.</summary>
    internal static int HubCapCells(double landPerTeam) =>
        landPerTeam >= 3000 ? 6 : landPerTeam >= 1500 ? 5 : landPerTeam >= FrontlineMinLand ? 4 : 3;

    /// <summary>The hub's <b>lateral</b> (cross-axis) span cap in cells at <paramref name="landPerTeam"/> — wider
    /// than the depth cap, so a hub grows <b>wider, not squarer</b>: the long lateral edge gives the spawn and
    /// wools room to attach with the seat gap, and at ≥ <see cref="WideHubCells"/> it affords the wide holed
    /// bodies (P, Double-hole), whose bar/ring runs are long free surface.</summary>
    internal static int HubWideCap(double landPerTeam) =>
        landPerTeam >= 3000 ? 11 : landPerTeam >= 1500 ? 9 : landPerTeam >= FrontlineMinLand ? 7 : 5;

    /// <summary>The box width at or above which a hub is <b>wide enough for the holed wide bodies</b> — the P
    /// (loop + overhanging bar) and the Double-hole (ring + docked U) both need width ≥ this (loop/ring
    /// <c>w − 2·cw ≥ 2·cw + 1</c> at <c>cw = 2</c>). Below it they directed-null and the compact menu is used.</summary>
    internal const int WideHubCells = 9;

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

    /// <summary>The wool's own corridor width in cells — a <b>w2</b> lane (docs/generator/model.md §2:
    /// "the lane to the wool is simple, w2"), independent of the map's lane width <c>w</c> (which is 3 on big
    /// boards). Keeping wool families at w2 makes them compact and lets a staple's 3-lane mouth fit a hub edge.</summary>
    internal const int WoolLaneCells = 2;

    /// <summary>The widest hub-entry a donut may sample, in cells — the min-only entry (one corridor) read as a
    /// chokepoint, so the attachment stub varies up to this along the hub edge.</summary>
    internal const int DonutEntryMaxCells = 5;

    /// <summary>The donut's enclosed hole caps, in cells: <b>along</b> the hub edge (the ring's mouth-side
    /// extent) and <b>deep</b> (outward). The min box gives the 1×2 hole; the sampled growth reaches 3×5 — the
    /// box grows and the emitter's ring absorbs it (its span derives from the box).</summary>
    internal const int DonutHoleAlongMaxCells = 3;

    internal const int DonutHoleDeepMaxCells = 5;

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
}
