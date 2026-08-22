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
    /// <summary>A core casing has no interior left to hold lava — a solid casing is a goal that can never
    /// leak, so the match it is the objective of cannot be won.</summary>
    /// <remarks>Raise the casing's <c>height</c> or lower its <c>shell</c> until an interior remains. A shell as thick as half the casing leaves solid stone, and a core with no lava inside it can never leak.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Objective, RuleConcern.Style)]
    public const string Casing = "DC1";

    /// <summary>A destroyable is built in a material its size is wrong for: obsidian is worth at most three
    /// blocks, so a cube or a plus-section column carrying it is a grind rather than a raid — or the material
    /// names nothing the studio builds at all, which writes into the map.xml verbatim while the blocks come
    /// out obsidian, and a declared material matching nothing in its own region is a goal at zero health
    /// (OB3).</summary>
    /// <remarks>Name a material the goal's size is built for: obsidian for a pillar, ender stone, gold or emerald for a cube or a column. This is a <b>complaint</b> — the world is built and the goal stands, in ender stone rather than in what was named, and the map.xml declares what was actually laid.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Objective, RuleConcern.Material)]
    public const string StyleMaterial = "DC3";

    /// <summary>A core's <c>float</c> and <c>leak</c> are one knob: together they say how far players must dig
    /// under it, and either alone says nothing.</summary>
    /// <remarks>State <c>float</c> and <c>leak</c> together, or neither. Their difference is how far players must dig under the core, so one without the other says nothing about the objective.</remarks>
    [Rule(RuleCategory.Malformed, RuleConcern.Objective)]
    public const string PairedKnobs = "DC2";

    /// <summary>A destroyable and a core are authored at orbit order 2 only — one team's to defend and every
    /// other team's to break, which only means anything with two teams.</summary>
    /// <remarks>Author the destroyable or core on a two-team map. On more teams "one team's to defend and everyone else's to break" has no single answer, and the studio does not guess one.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Objective, RuleConcern.Intent)]
    public const string TwoTeamOnly = "OB14";

    /// <summary>The three places a goal may not stand: over the void, inside a spawn room, or inside a wool
    /// room — everywhere the map's own rules would leave its blocks unbreakable.</summary>
    /// <remarks>Move the goal onto ground and out of every spawn and wool room. The finding names which of the three it hit; a goal over void has nothing to stand on, and one inside a room stands where the map's own rules make its blocks unbreakable.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Objective, RuleConcern.Structure, RuleConcern.Terrain)]
    public const string Placement = "OB17";

    /// <summary>A tree, boulder or building stands inside a goal's clearance: the ground its structure covers
    /// grown by four blocks, and never nearer than ten blocks to the marker itself (the author's numbers). The
    /// prop is declined — a goal is what the map is for, and a prop is removable, so the map is built without
    /// it rather than refused for it.</summary>
    /// <remarks>Move the tree, boulder or building the finding names, or move the goal. Nothing was built where the finding points: the prop is not in the world, and the placement is still on the canvas to correct.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Objective, RuleConcern.Feature)]
    public const string PropInClearance = "OB19";

    /// <summary>A goal is authored floating further over the ground than a goal may float. The stated
    /// <c>float</c> is a distance from whatever the relief leaves under the column, and without a maximum it
    /// puts a goal anywhere — including where reaching it is a build project rather than a raid.</summary>
    /// <remarks>Lower the goal's <c>float</c>. The floor is what makes a goal read as a monument rather than as terrain; the ceiling is how far a player will climb for it.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Objective)]
    public const string FloatCap = "OB22";

    /// <summary>A goal's own structure reaches above the height players may build to — the build ceiling over
    /// the terrain the map actually builds. The blocks above it can still be broken, so the map is not
    /// unwinnable; a goal standing over the line players may reach by building is a goal contested from
    /// nowhere.</summary>
    /// <remarks>Lower the goal's <c>float</c>, shorten its structure, or raise the ground under it. This is a <b>complaint</b> on a built world: the finding carries the goal's own top course and the ceiling it passed.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Objective, RuleConcern.World)]
    public const string OverBuildCeiling = "OB23";

    /// <summary>A declared <c>&lt;gamemode&gt;</c> is outside PGM's own closed enum, so the map fails to load
    /// however clean everything else is.</summary>
    /// <remarks>Use an id from PGM's own enum. The studio derives this from what the intent carries, so the usual cause is a hand-edited document — remove the element and let the generator write it.</remarks>
    [Rule(RuleCategory.Unknown, RuleConcern.Intent, RuleConcern.World)]
    public const string UnknownGamemode = "OB20";

    /// <summary>Two goals are built into the same blocks. A destroyable stamped inside a core's casing is one
    /// structure serving two objectives: breaking either is breaking the other, and the blocks a match is
    /// played for belong to whichever stamper ran last. The usual cause is the symmetry rather than the
    /// author's hand — a goal drawn at one position and a second at the position the orbit maps the first
    /// onto, so each lands on the other's image and nothing in the plan looks wrong.</summary>
    /// <remarks>Move one of the two. Under a mirror or a rotation a goal occupies its own position and every image of it, so check a second goal against the images of the first and not only against where it was drawn — POST /api/plan/inspect answers goalDistances over the fanned closure.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Objective, RuleConcern.World)]
    public const string GoalsShareGround = "OB24";
}
