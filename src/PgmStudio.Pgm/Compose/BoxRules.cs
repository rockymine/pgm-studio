using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Compose;

/// <summary>The producibility read's own rule ids: whether the generator could have built a box the author drew,
/// and the arrangement of the unit the boxes make. A box the generator cannot build is still a box an author may
/// draw, so every one reaches a caller as a complaint.</summary>
public static class BoxRules
{
    /// <summary>A box has no pieces.</summary>
    /// <remarks>Either change the <c>rect</c> of the box in <c>boxes</c> until it covers a piece, or delete the box
    /// from <c>boxes</c>.</remarks>
    [Rule(RuleCategory.Malformed, RuleConcern.Plan)]
    public const string BoxEmpty = "BX1";

    /// <summary>A box is less than 2 cells across at its narrowest, the lane width the generator builds.</summary>
    /// <remarks>Widen the <c>rect</c> of the narrowest piece of the box in <c>pieces</c> until the box is at
    /// least 2 cells across everywhere.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Plan)]
    public const string BoxTooNarrow = "BX2";

    /// <summary>A box has a kind that no form the generator builds covers.</summary>
    /// <remarks>Change the <c>kind</c> of the box in <c>boxes</c> to one of <c>wool</c>, <c>spawn</c>,
    /// <c>hub</c> or <c>frontline</c>.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Plan)]
    public const string NoFormForKind = "BX3";

    /// <summary>A box is not the ground any form the generator builds lays down, and the nearest form differs
    /// in the cells the finding names.</summary>
    /// <remarks>Change the <c>rect</c> of the pieces of the box in <c>pieces</c> until they cover the cells the
    /// nearest form builds.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Plan)]
    public const string NoFormReproduces = "BX4";

    /// <summary>A box has the shape of a form the generator builds, and a part of it is not the one lane width
    /// that form builds every part at.</summary>
    /// <remarks>Change the <c>rect</c> of the wider or narrower piece of the box in <c>pieces</c> until every
    /// part of the box is the width of its narrowest.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Plan)]
    public const string ProportionsOutOfReach = "BX5";

    /// <summary>A box is less than the smallest footprint any form the generator builds fits.</summary>
    /// <remarks>Widen the <c>rect</c> of the box in <c>boxes</c> until it is at least the footprint the finding
    /// names.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Plan)]
    public const string BoxTooSmall = "BX6";

    /// <summary>A box has no form the generator builds that takes it, for the reason the finding names.</summary>
    /// <remarks>Change the <c>rect</c> of the box in <c>boxes</c>, or of its pieces in <c>pieces</c>, until a
    /// form no longer refuses it.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Plan)]
    public const string EveryFormRefuses = "BX7";

    /// <summary>The mid build region over the unit's front runs more than 4 cells past the front it
    /// docks.</summary>
    /// <remarks>Move the <c>rect</c> of the front pieces in <c>pieces</c> closer to the symmetry axis until the
    /// mid build region runs at most 4 cells past the front.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Plan)]
    public const string FrontOffAxis = "BX8";

    /// <summary>A front line box meets the front of its hub box in no patch, or in a patch less than 2 cells
    /// wide.</summary>
    /// <remarks>Change the <c>rect</c> of the front line box in <c>boxes</c> until every patch where it meets the
    /// front of the hub is at least 2 cells wide.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Plan)]
    public const string FrontlinePatchNarrow = "BX9";

    /// <summary>Two spawn or wool boxes stand less than 2 cells apart, the gap the generator keeps between
    /// them.</summary>
    /// <remarks>Move the <c>rect</c> of one of the two boxes in <c>boxes</c> until they stand at least 2 cells
    /// apart.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Plan)]
    public const string SeatsTooClose = "BX10";
}
