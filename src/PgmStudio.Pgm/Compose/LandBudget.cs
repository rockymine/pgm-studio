namespace PgmStudio.Pgm.Compose;

/// <summary>
/// The land a team unit may spend, in cells, and what is left as each box takes its bite. The envelope opens
/// one per attempt at <see cref="ComposeEnvelope.BudgetCells"/>; the allocator debits the hub, the spawn, the
/// frontline and each wool as it sizes them, and what is still <see cref="Remaining"/> at the end is land the
/// unit failed to place.
///
/// <para>Spending is the <em>aim</em>, in box <b>footprints</b>, which is all the allocator has before
/// anything is filled — so the allowance the boxes share out is the land budget over
/// <see cref="UnitTuning.FootprintLandYield"/>. What the unit actually built is read off the filled pieces
/// and held against <see cref="UnitTuning.SpendFloor"/>/<see cref="UnitTuning.SpendCeiling"/>: a box's
/// footprint is not its land once a hole is cut in it, so the two are different readings and only the second
/// is the contract.</para>
/// </summary>
internal sealed class LandBudget
{
    public LandBudget(double landCells)
    {
        Land = Math.Max(0, landCells);
        Total = Remaining = Land / UnitTuning.FootprintLandYield;
    }

    /// <summary>The land the unit must come out holding — the band's budget, and what the spend gate reads.</summary>
    public double Land { get; }

    /// <summary>The footprint the boxes may claim between them.</summary>
    public double Total { get; }

    /// <summary>What no box has taken yet.</summary>
    public double Remaining { get; private set; }

    /// <summary>A fixed fraction of the whole budget — what a kind of box is entitled to before any other has
    /// been sized, so the shares do not depend on the order the boxes happen to be drawn in.</summary>
    public double Share(double fraction) => Total * Math.Max(0, fraction);

    /// <summary>Debit a box's footprint. Floors at nothing left rather than going negative: a box that
    /// overruns leaves the next one with nothing, which the spend gate then reads as an overspend.</summary>
    public double Spend(double cells)
    {
        var taken = Math.Max(0, cells);
        Remaining = Math.Max(0, Remaining - taken);
        return taken;
    }
}
