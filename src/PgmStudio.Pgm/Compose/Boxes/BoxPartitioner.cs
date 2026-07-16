namespace PgmStudio.Pgm.Compose;

/// <summary>
/// The partition-first allocator (G63): <c>budget → <see cref="BoxPartition"/></c>. Where
/// <see cref="TeamUnitGrower"/> samples a shape and lets each box's footprint <b>fall out of the fill</b> —
/// <c>PlaceArm</c>/<c>PlaceSpawn</c> emit the shape, <em>then</em> compute the host window and the spawn's (u,v)
/// frame, so the footprint is an <em>output</em> of the fill — the allocator makes the <see cref="BoxPartition"/>
/// the <b>first-class artifact a compose produces</b>: the typed boxes with their allocated <see cref="Box.Rect"/>
/// footprints and their <see cref="Box.LandTargetCells"/> land-budget halves (the two-currency budget,
/// docs/contracts/map-generation.md §8), joined by the abutments between them.
///
/// <para><b>Shipping parallel, not yet the default.</b> This is the ALLOCATOR half of the box-driven switch. The
/// switch that fills an allocated partition through <see cref="BoxFiller"/>, wires the <see cref="DockingGate"/>,
/// retires the grower and re-keys the RNG is the next step (G63-C). Until then the fill is still the grower's:
/// <see cref="Partition"/> grows one unit and reads the partition it implies via <see cref="BoxPartition.Of"/>,
/// so the emitted partition <b>round-trips through the mirror by construction</b> — "the labels drive, the mirror
/// verifies" (§5.3). Boxes may overlap; the load-bearing invariant is piece-disjointness + image clearance, which
/// the grown fill already satisfies. This is purely additive — no production path changes here, exactly as
/// <see cref="BoxPartition.Of"/> (G63-A) was.</para>
///
/// <para>What it adds over the bare mirror is the <b>seam</b> — the one named <c>budget → partition</c> entry the
/// composer routes through when the switch flips, so inverting to allocate-then-fill later changes this body and
/// not its callers — plus the <b>two-currency budget accounting</b>: <see cref="BudgetCells"/> is the land the
/// partition must balance (the land currency; the footprint currency is the boxes' Rects), and
/// <see cref="WithinBudget"/> is the balance check the directed <see cref="FillResult"/> repair drives each box's
/// land to at the switch. In this parallel stage the grower already spends land to within its area tolerance, so
/// a grown partition balances by construction — the check is that invariant made explicit ahead of the inversion.</para>
/// </summary>
public static class BoxPartitioner
{
    /// <summary>The grower's area-window half-width: a grown unit's land sits within this fraction of the team
    /// budget, so a partition read off one balances inside it. The window is asymmetric — the spawn is a small
    /// fixed box now (G84), so a unit runs <em>under</em> the quota (a less crammed map) more than over — and
    /// this covers the wider (floor) side.</summary>
    public const double BudgetTolerance = 0.40;

    /// <summary>The two-currency <b>land</b> budget in cells: the team's land target (blocks²,
    /// <see cref="ComposeEnvelope.LandPerTeam"/>) over the cell area. The total the partition's per-box
    /// <see cref="Box.LandTargetCells"/> halves sum toward — the land currency (the footprint currency is the
    /// boxes' <see cref="Box.Rect"/>s, fixed once at allocation).</summary>
    public static double BudgetCells(ComposeEnvelope env) => env.LandPerTeam / (env.Cell * (double)env.Cell);

    /// <summary>Allocate the <see cref="BoxPartition"/> a compose produces from the <paramref name="env"/> budget.
    /// Parallel stage: the geometry is grown (the grower is the filler until the switch), and the partition is
    /// read off the grown unit — each box's allocated <see cref="Box.Rect"/> is its fill's footprint, its
    /// <see cref="Box.LandTargetCells"/> the land the fill spends, the joints the footprint abutments — so the
    /// result round-trips through <see cref="BoxPartition.Of"/>. Throws <see cref="ComposeException"/> (via the
    /// grower) when no unit satisfies the invariants within the attempt budget.</summary>
    public static BoxPartition Partition(ComposeEnvelope env, ComposeRng rng) =>
        BoxPartition.Of(TeamUnitGrower.Grow(env, rng));

    /// <summary>The two-currency <b>balance</b> of an allocated partition: it is <see cref="BoxPartition.Valid"/>
    /// (so every box's land currency already fits its footprint — the footprint bounds the land) <b>and</b> its
    /// total land is within <paramref name="tolerance"/> of the team <see cref="BudgetCells">budget</see>. This is
    /// the invariant a directed fill/fragment (the switch) drives each box's land to; here it verifies the grown
    /// fill already lands inside the budget envelope.</summary>
    public static bool WithinBudget(BoxPartition partition, ComposeEnvelope env, double tolerance = BudgetTolerance)
    {
        if (!partition.Valid()) return false;
        var budget = BudgetCells(env);
        var land = partition.Boxes.Sum(b => b.LandTargetCells);
        return land >= budget * (1 - tolerance) && land <= budget * (1 + tolerance);
    }
}
