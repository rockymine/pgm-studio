using PgmStudio.Vocabulary;

namespace PgmStudio.Analysis.Playability;

/// <summary>The relief rule ids the read-back cites — stable names for what a finding about a solved surface
/// is about. Both are <b>complaints</b>: a relief is authored ground and the studio does not overrule an
/// author about what their map looks like. What it does is measure, and say when the measurement and the
/// statement disagree.</summary>
public static class ReliefRules
{
    /// <summary>A group states one landform, and the range of its ground over the square root of its cells measures
    /// as another.</summary>
    /// <remarks>Either set the <c>landform</c> of the group to the one its ground measures as, or change the
    /// <c>h</c> of a mark or the <c>amount</c> of a push until it measures as the landform it states.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Terrain)]
    public const string LandformMismatch = "RL1";

    /// <summary>The ground of a group that measures as more than a plain has no more than 1 scramble for every
    /// barrier.</summary>
    /// <remarks>Either delete a mark from <c>marks</c>, or set the <c>falloff</c> of a push in <c>pushes</c> to a
    /// higher value, until the group has more than 1 scramble for every barrier.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Terrain)]
    public const string NotSmoothed = "RL2";

    /// <summary>The ground of one mark stands 3 or more blocks above or below the ground of another where they
    /// share an edge.</summary>
    /// <remarks>Either change the <c>h</c> of one of the two marks in <c>marks</c> until the step is at most 2
    /// blocks, or set the <c>tread</c> of the later line mark to less than its <c>r</c>.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Terrain)]
    public const string MarksMeetOnAStep = "RL3";

    /// <summary>A mark overlaps no ground of its group.</summary>
    /// <remarks>Either move the mark in <c>marks</c> onto the ground of its group, or delete the mark from
    /// <c>marks</c>.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Terrain)]
    public const string MarkPinsNothing = "RL4";

    /// <summary>The share of a group's ground that is level is less than 30 percent.</summary>
    /// <remarks>Either set the <c>tread</c> of a line mark in <c>marks</c> to its <c>r</c>, or set the <c>bevel</c>
    /// of an area mark in <c>marks</c> to 0, until the share is at least 30 percent.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Terrain)]
    public const string NowhereLevel = "RL5";

    /// <summary>A push climbs its skirt and its crown at rates, in blocks of rise per block of run, more than 2
    /// times apart.</summary>
    /// <remarks>Either change the <c>falloff</c> of the push in <c>pushes</c>, or change its <c>crown</c>, until
    /// the two rates are at most 2 times apart.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Terrain)]
    public const string PushGradesDisagree = "RL6";
}
