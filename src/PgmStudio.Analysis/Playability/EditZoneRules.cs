using PgmStudio.Vocabulary;

namespace PgmStudio.Analysis.Playability;

/// <summary>The edit-zone rule ids the read-back cites. Like the relief rules beside them these are
/// <b>complaints</b>: where a player may build is the author's design, and the studio measures it rather than
/// overruling it.</summary>
public static class EditZoneRules
{
    /// <summary>Ground a player can stand on and nobody can edit, outside the zones a map protects on
    /// purpose. A spawn and a wool room are sealed by design and are excluded by the rule that seals them —
    /// they restrict entry, and a place you may not walk into is not a place you were expecting to build in.
    /// What is left is ground the author can reach, cannot touch, and did not say so: a canopy hanging over
    /// the void outside every build zone, a crag the void rule closed along with the air around it, a
    /// platform whose supporting column never reached y=0.</summary>
    /// <remarks>Decide which it is. If the area is meant to be inert, nothing here is wrong and the complaint is the record of that choice. If it is not, the map needs either a build zone reaching it, ground under it at y=0, or a break exception for what stands there — which is what the corpus carves for leaves and logs so a tree over the void can still be cut down.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Terrain, RuleConcern.Intent)]
    public const string DeadGround = "EZ1";
}
