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

    /// <summary>Two marks' ground meets on a step taller than a player can scramble. A mark pins every cell in
    /// its band exactly, so two placed to describe one slope describe a wall instead — and the wall reads back
    /// as terrain, a step and a face and a barrier cell, with no mark's name on any of it. Neither mark looks
    /// wrong on its own, which is why this has to be said rather than seen.</summary>
    /// <remarks>State a `tread` on the later of the two, narrower than its `r`: the band past the tread then grades into whatever the earlier mark put there instead of ending on it. The shoulder's width sets the grade — the difference over the number of cells between the tread and the reach — so a gentler seam means a narrower tread or a longer reach. Where the step is what the map is for, a `scarp` states a drop outright and is not reported here.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Terrain)]
    public const string MarksMeetOnAStep = "RL3";

    /// <summary>A mark pinned no cell at all. It was placed off its group's footprint, or over ground a shape
    /// took out of the solve, so the surface is the surface it would have been without it — and nothing else
    /// says so, since a mark that did nothing leaves nothing behind to notice.</summary>
    /// <remarks>Move it onto the group's own ground, or delete it. A mark is clipped to the footprint rather than confined to it, so one placed past an edge is legal and useful — a summit whose centre sits off the board raises the corner — and this fires only where the overlap is empty.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Terrain)]
    public const string MarkPinsNothing = "RL4";

    /// <summary>The elevation was graded everywhere and left nowhere to stand. <c>RL2</c>'s twin: where that
    /// one names ground whose every height change is a wall, this names ground that is all transition — under
    /// a third of it level, so a player crosses the whole board and can stop nowhere on it. It is an angle
    /// rather than a step, and that is why nothing else reports it: a surface graded end to end is walkable
    /// end to end, one connected place with no ledge, and the step histogram calls it perfect.</summary>
    /// <remarks>Take the tread off the marks meant to be flat to their edge, or widen them. A tread grades a mark's shoulder into its neighbour, so a tread on every mark builds a board of nothing but shoulders — measured on `opus5-thwaite-ghyll`, whose five marks all carried one: 25.4% level and no face at all, against 43.6% level and 70 faces with the same marks and the treads taken off. A map is played on its flat ground, and a face is what decides where players go.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Terrain)]
    public const string NowhereLevel = "RL5";

    /// <summary>A push climbs at two different rates and the landform has a step at its own outline. A push
    /// rises at <c>amount / falloff</c> over its skirt, from the ground outside in to the drawn ring, and
    /// again at <c>crown / deepest</c> from that ring in to its medial axis — and where the two disagree what
    /// stands there is a cliff with a hill on top of it, whatever its height. Nothing else says so: the
    /// surface reads back as a face, correctly, with no push's name on it, and both numbers are inside knobs
    /// an author set one at a time.</summary>
    /// <remarks>Bring the two rates together — the ratio between them is the number actually being chosen. A skirt steeper than the crown is a plateau with a bank round it; a crown steeper than the skirt is a spike on an apron. Widen the `falloff` to soften the skirt, or lower the `crown` to soften the top, until the two are within about twice each other. A push with no crown states one rate and is not read here.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Terrain)]
    public const string PushGradesDisagree = "RL6";
}
