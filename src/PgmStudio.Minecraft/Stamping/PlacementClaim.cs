using PgmStudio.Domain;
using PgmStudio.Minecraft.Anvil;

namespace PgmStudio.Minecraft.Stamping;

/// <summary>
/// The columns one placed thing owns, what to call it, and which pass put it there: a
/// <see cref="WorldProvenance"/> claim before it is recorded, handed back by the pass that placed the blocks.
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
/// <para><see cref="Owner"/> is the claim's identity and not its material. Two buildings that genuinely touch
/// are two claims, which is what lets a reader group on the owner instead of flooding across contiguous
/// claimed columns and merging them — and because a <see cref="StampId"/> names the authored unit and the
/// orbit image apart, a building and its own mirror are one identity seen twice rather than two findings a
/// reader has to guess are related.</para>
/// </summary>
/// <param name="Owner">What did the claiming: the kind, the authored unit, and which image of it this is.</param>
/// <param name="Layer">Which pass this was: a building and every stamped shell are
/// <see cref="ProvenanceLayer.Structure"/>, while a tree, a boulder, a path, a water course and a bed of
/// flora are <see cref="ProvenanceLayer.Prop"/> — placed rather than built, and they must not read as
/// buildings.</param>
/// <param name="Cells">Every column the thing covers, as the placement actually wrote it.</param>
public readonly record struct PlacementClaim(
    StampId Owner, ProvenanceLayer Layer, IReadOnlyList<(int X, int Z)> Cells);
