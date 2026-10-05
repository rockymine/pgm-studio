using PgmStudio.Vocabulary;

namespace PgmStudio.Analysis.Playability;

/// <summary>The edit-zone rule ids the read-back cites. Like the relief rules beside them these are
/// <b>complaints</b>: where a player may build is the author's design, and the studio measures it rather than
/// overruling it.</summary>
public static class EditZoneRules
{
    /// <summary>A column of ground outside every protection has no build region over it and no block at height
    /// 0.</summary>
    /// <remarks>Either add a build region to <c>build.areas</c> over the column, or add a shape to <c>shapes</c>
    /// that holds a block at height 0 under it.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Terrain, RuleConcern.Intent)]
    public const string DeadGround = "EZ1";

    /// <summary>The void between a coast and a build region is less than 10 blocks wide, and the plan put ground in
    /// it.</summary>
    /// <remarks>Either widen the <c>rect</c> of the build region in <c>zones</c> over the void, or add a build
    /// region to <c>zones</c> across the void.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Terrain, RuleConcern.Intent)]
    public const string BuildZoneGap = "EZ2";
}
