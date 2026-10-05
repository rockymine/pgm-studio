using PgmStudio.Vocabulary;
namespace PgmStudio.Domain;

/// <summary>
/// The rule ids a destroyable or a core is refused by, catalogued in <c>docs/pgm/destroyables-and-cores.md</c>.
///
/// <para>These are the <b>objective's own</b> rules rather than the plan's, which is why they live here and
/// not beside the validator that happens to ask them: the same rule is asked at the compile gate against a
/// plan's pieces and at the export gate against the ground the rasterizer produced, and a rule that changed
/// its name between the two would be two rules. Stable forever, and kept apart from any task-tracking id.</para>
/// </summary>
public static class ObjectiveRules
{
    /// <summary>The lava footprint or the lava height of a core is not between 2 and 5 blocks.</summary>
    /// <remarks>Set the <c>lava</c> and the <c>lavaHeight</c> of the core in <c>placements.cores</c> to between 2
    /// and 5 blocks.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Objective, RuleConcern.Style)]
    public const string Casing = "DC1";

    /// <summary>A monument's material is not one of obsidian, emerald block, gold block or ender stone, or an
    /// obsidian monument has more than 3 blocks.</summary>
    /// <remarks>Set the <c>materials</c> of the monument in <c>placements.destroyables</c> to obsidian on a pillar,
    /// or to emerald block, gold block or ender stone.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Objective, RuleConcern.Material)]
    public const string StyleMaterial = "DC3";

    /// <summary>A core has a leak and no float, or a float and no leak.</summary>
    /// <remarks>Either set the missing one of the <c>float</c> and the <c>leak</c> of the core in
    /// <c>placements.cores</c>, or set the other to <c>null</c>.</remarks>
    [Rule(RuleCategory.Malformed, RuleConcern.Objective)]
    public const string PairedKnobs = "DC2";

    /// <summary>A plan has a monument or core, and its symmetry gives a number of teams other than 2.</summary>
    /// <remarks>Either set the <c>globals.symmetry</c> of the plan to <c>rot_180</c> or a mirror, or delete the
    /// monuments from <c>placements.destroyables</c> and the cores from <c>placements.cores</c>.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Objective, RuleConcern.Intent)]
    public const string TwoTeamOnly = "OB14";

    /// <summary>A monument or core has a part of its footprint over void.</summary>
    /// <remarks>Move the monument or core in <c>placements.destroyables</c> or <c>placements.cores</c> until its
    /// footprint lies wholly on ground.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Objective, RuleConcern.Terrain)]
    public const string Placement = "OB17";

    /// <summary>A monument or core overlaps a spawn room or a wool room.</summary>
    /// <remarks>Move the monument or core in <c>placements.destroyables</c> or <c>placements.cores</c> until its
    /// footprint overlaps no spawn room or wool room.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Objective, RuleConcern.Structure)]
    public const string GoalInARoom = "OB30";

    /// <summary>A wool monument has no ground under it.</summary>
    /// <remarks>Move the room piece of the spawn in <c>pieces</c> until the monument stands on ground.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Objective, RuleConcern.Terrain)]
    public const string WoolMonumentOverVoid = "OB31";

    /// <summary>A tree, boulder or building stands within 4 blocks of an objective's structure, or less than 10
    /// blocks from its marker.</summary>
    /// <remarks>Either move the tree, boulder or building in <c>dressing.props</c> farther from the objective, or
    /// move the objective's marker farther from it.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Objective, RuleConcern.Feature)]
    public const string PropInClearance = "OB19";

    /// <summary>The float of a monument or core is more than 12 blocks.</summary>
    /// <remarks>Set the <c>float</c> of the monument or core in <c>placements.destroyables</c> or
    /// <c>placements.cores</c> to at most 12 blocks.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Objective)]
    public const string FloatCap = "OB22";

    /// <summary>A monument or core tops out more than 20 blocks above the average height of the ground.</summary>
    /// <remarks>Change the <c>float</c> of the monument or core in <c>placements.destroyables</c> or
    /// <c>placements.cores</c> until its top is at most 20 blocks above the average height of the ground.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Objective, RuleConcern.World)]
    public const string OverBuildCeiling = "OB23";

    /// <summary>A map names a game mode that PGM does not have.</summary>
    /// <remarks>Either change the unknown entry in <c>gamemode</c> to a game mode that PGM has, or delete it from
    /// <c>gamemode</c>.</remarks>
    [Rule(RuleCategory.Unknown, RuleConcern.Intent, RuleConcern.World)]
    public const string UnknownGamemode = "OB20";

    /// <summary>A monument or core overlaps another monument or core, or a symmetry copy of one.</summary>
    /// <remarks>Move the monument or core in <c>placements.destroyables</c> or <c>placements.cores</c> until it no
    /// longer overlaps the other or its symmetry copy.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Objective, RuleConcern.World)]
    public const string GoalsShareGround = "OB24";

    /// <summary>The game settings state a wool monument on one block, and the world builds it on another.</summary>
    /// <remarks>Set the <c>location</c> of the monument in <c>monuments</c> to the block the world builds it
    /// on.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Objective, RuleConcern.World)]
    public const string MonumentDerived = "OB25";

    /// <summary>A map has a monument or core, and no mode ladder that changes it.</summary>
    /// <remarks>Add an entry to <c>modes</c>, then set the <c>mode_changes</c> of each monument and core to
    /// <c>true</c>.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Objective, RuleConcern.Intent)]
    public const string NoModeLadder = "OB26";

    /// <summary>A capture point has no required setting.</summary>
    /// <remarks>Set the <c>required</c> of each capture point in <c>control_points</c> to <c>false</c>.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Objective)]
    public const string PointEndsTheMatch = "OB27";

    /// <summary>A map has a capture point that pays its owner and has no score element.</summary>
    /// <remarks>Set the <c>scoreLimit</c> of the game settings to at least 1.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Objective)]
    public const string PointScoresIntoNothing = "OB28";

    /// <summary>A capture point's capture pad or objective marker has no block that takes the colour of the team
    /// holding the point.</summary>
    /// <remarks>Delete the road or made thing that covers the capture pad from <c>dressing.props</c> or
    /// <c>layers</c>.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Objective, RuleConcern.Material)]
    public const string PointNeverChangesColour = "OB29";
}
