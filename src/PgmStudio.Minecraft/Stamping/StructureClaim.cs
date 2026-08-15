namespace PgmStudio.Minecraft.Stamping;

/// <summary>
/// The columns one stamped thing owns, and what to call it: a <see cref="WorldProvenance"/> claim before it is
/// recorded, handed back by the pass that placed the blocks.
///
/// <para><b>A claim is taken from the placement, never rebuilt beside it.</b> That is the whole reason this
/// type exists rather than each pass calling <see cref="WorldProvenance.ClaimRect"/> with a rectangle it
/// derived for itself. A claim rebuilt from the author's intent cannot know what the placement refused — a
/// building dropped for standing on nothing, for overlapping something already up, or for a turn that failed —
/// so it claims ground that carries no blocks, and every reader downstream believes it: a structure render
/// draws a building that is not there, and a finder reports a structure with no blocks, both saying they are
/// certain. The rule is already stated one function away, in <c>DressingScope.GoalGroundAt</c>, which takes a
/// goal's ground from the box the stamper wrote <em>"by construction rather than by two derivations
/// agreeing"</em>; this is the same rule, given a type so the other passes can follow it.</para>
///
/// <para><see cref="Owner"/> is the claim's identity and not its material — a dressing prop's own id plus its
/// orbit image, a room, a team, a marker. Two buildings that genuinely touch are two claims, which is what
/// lets a reader group on the owner instead of flooding across contiguous claimed columns and merging them.
/// </para>
/// </summary>
/// <param name="Owner">What did the claiming, in the form a reader groups on: <c>house:h1:0</c>,
/// <c>wall:2</c>, <c>spawn:0:iron:1</c>.</param>
/// <param name="Cells">Every column the thing covers, as the placement actually wrote it.</param>
public readonly record struct StructureClaim(string Owner, IReadOnlyList<(int X, int Z)> Cells);
