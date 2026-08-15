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
    /// <summary>A core casing has no interior left to hold lava — a solid casing is a goal that can never
    /// leak, so the match it is the objective of cannot be won.</summary>
    public const string Casing = "DC1";

    /// <summary>A core's <c>float</c> and <c>leak</c> are one knob: together they say how far players must dig
    /// under it, and either alone says nothing.</summary>
    public const string PairedKnobs = "DC2";

    /// <summary>A destroyable and a core are authored at orbit order 2 only — one team's to defend and every
    /// other team's to break, which only means anything with two teams.</summary>
    public const string TwoTeamOnly = "OB14";

    /// <summary>The three places a goal may not stand: over the void, inside a spawn room, or inside a wool
    /// room — everywhere the map's own rules would leave its blocks unbreakable.</summary>
    public const string Placement = "OB17";

    /// <summary>A tree, boulder or building stands inside a goal's clearance.</summary>
    public const string PropInClearance = "OB19";

    /// <summary>A declared <c>&lt;gamemode&gt;</c> is outside PGM's own closed enum, so the map fails to load
    /// however clean everything else is.</summary>
    public const string UnknownGamemode = "OB20";
}
