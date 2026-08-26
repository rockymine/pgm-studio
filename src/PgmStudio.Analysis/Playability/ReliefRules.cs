using PgmStudio.Vocabulary;

namespace PgmStudio.Analysis.Playability;

/// <summary>The relief rule ids the read-back cites — stable names for what a finding about a solved surface
/// is about. Both are <b>complaints</b>: a relief is authored ground and the studio does not overrule an
/// author about what their map looks like. What it does is measure, and say when the measurement and the
/// statement disagree.</summary>
public static class ReliefRules
{
    /// <summary>The ground is not the landform the island says it is. The island states what it is meant to
    /// be — a plain, rolling ground, hills, a mountain — and the solved surface measures as something else,
    /// which is a board that got away from its author between the marks and the field.</summary>
    /// <remarks>Move the marks or the pushes until the ground is what the island says, or change what it says. The measure is the range over the square root of the island's cells: elevation for the board's own size, since twenty-eight blocks of range is a mountain on a small board and a slope on a big one.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Terrain)]
    public const string LandformMismatch = "RL1";

    /// <summary>The elevation is there and was never graded. Ground that rolls keeps more two-block scrambles
    /// than barriers; ground where every height change is a wall keeps the reverse, whatever its range — a
    /// quarry and a mountainside carry the same elevation and are not the same ground.</summary>
    /// <remarks>Smooth it: fewer, further-apart marks, a wider falloff on the pushes, or a relaxation pass between them. A relief whose steps are all barriers was not shaped, it was cut.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Terrain)]
    public const string NotSmoothed = "RL2";
}
