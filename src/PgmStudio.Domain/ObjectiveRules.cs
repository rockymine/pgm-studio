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
    /// <remarks>Raise the casing's <c>height</c> or lower its <c>shell</c> until an interior remains. A shell as thick as half the casing leaves solid stone, and a core with no lava inside it can never leak.</remarks>
    public const string Casing = "DC1";

    /// <summary>A core's <c>float</c> and <c>leak</c> are one knob: together they say how far players must dig
    /// under it, and either alone says nothing.</summary>
    /// <remarks>State <c>float</c> and <c>leak</c> together, or neither. Their difference is how far players must dig under the core, so one without the other says nothing about the objective.</remarks>
    public const string PairedKnobs = "DC2";

    /// <summary>A destroyable and a core are authored at orbit order 2 only — one team's to defend and every
    /// other team's to break, which only means anything with two teams.</summary>
    /// <remarks>Author the destroyable or core on a two-team map. On more teams "one team's to defend and everyone else's to break" has no single answer, and the studio does not guess one.</remarks>
    public const string TwoTeamOnly = "OB14";

    /// <summary>The three places a goal may not stand: over the void, inside a spawn room, or inside a wool
    /// room — everywhere the map's own rules would leave its blocks unbreakable.</summary>
    /// <remarks>Move the goal onto ground and out of every spawn and wool room. The finding names which of the three it hit; a goal over void has nothing to stand on, and one inside a room stands where the map's own rules make its blocks unbreakable.</remarks>
    public const string Placement = "OB17";

    /// <summary>A tree, boulder or building stands inside a goal's clearance: the ground its structure covers
    /// grown by four blocks, and never nearer than ten blocks to the marker itself (the author's numbers).</summary>
    /// <remarks>Move the tree, boulder or building the finding names, or move the goal. Nothing is dropped for you, because a prop deleted silently is a placement the author can still see on the canvas.</remarks>
    public const string PropInClearance = "OB19";

    /// <summary>A declared <c>&lt;gamemode&gt;</c> is outside PGM's own closed enum, so the map fails to load
    /// however clean everything else is.</summary>
    /// <remarks>Use an id from PGM's own enum. The studio derives this from what the intent carries, so the usual cause is a hand-edited document — remove the element and let the generator write it.</remarks>
    public const string UnknownGamemode = "OB20";

    /// <summary>A tree or building stands in a door's approach: the ground in front of a spawn room's door —
    /// twenty blocks out from the stamped building's own face, the wall's width — or in front of a wool
    /// room's entries, ten blocks out (the author's numbers). The lane players walk out through is part of
    /// what the door is for, and a prop standing in it turns the way out into an ambush. A boulder is
    /// permitted: low cover leaves the sightline the rule protects.</summary>
    /// <remarks>Move the tree or building out of the approach lane the finding names, or turn the spawn so its door faces open ground. The approach is measured from the stamped room's face — the building, not the protection region around it.</remarks>
    public const string ApproachBlocked = "OB21";
}
