using System.Text.Json;
using System.Text.Json.Serialization;

namespace PgmStudio.Vocabulary;

/// <summary>Writes <see cref="RuleCategory"/> as the lowercase word the wire and the client both read it
/// as.</summary>
public sealed class RuleCategoryConverter() : JsonStringEnumConverter<RuleCategory>(JsonNamingPolicy.CamelCase);

/// <summary>Writes <see cref="RuleConcern"/> as the lowercase word the wire and the client both read it
/// as.</summary>
public sealed class RuleConcernConverter() : JsonStringEnumConverter<RuleConcern>(JsonNamingPolicy.CamelCase);

/// <summary>
/// <b>What a caller does about a finding, in one word.</b> A rule id is specific, stable and for a reader; a
/// category is the small closed set an agent branches on without knowing the id. Each word is defined by the
/// action it implies rather than by how the fault sounds, so a caller can act before it has read a sentence.
/// </summary>
[JsonConverter(typeof(RuleCategoryConverter))]
public enum RuleCategory
{
    /// <summary>The studio could not read what it was given, so nothing downstream ran. <i>Fix the shape.</i></summary>
    Malformed,

    /// <summary>Everything read, and a name in it resolves to nothing the studio has. <i>Fix the name.</i></summary>
    Unknown,

    /// <summary>Everything read and resolved, and two things cannot both be true: the request against what is
    /// stored, or two authored things against each other over one place. <i>Choose which wins.</i></summary>
    Conflict,

    /// <summary>Everything read, resolved and consistent, and what is asked for cannot be produced: past a
    /// stated limit, outside what an emitter builds, no geometry that fits. <i>Change the design.</i></summary>
    Unsatisfiable,

    /// <summary>It can be produced, and the map that results cannot be played as intended — nothing wins it,
    /// nobody can enter it, part is unreachable, a goal cannot be contested. <i>Change the map.</i></summary>
    Unplayable,

    /// <summary>Everything read and resolved, and the studio will not do it. Not a fault in the request and
    /// not a limit of what can be built, but a policy the studio holds. <i>Ask for something else.</i></summary>
    Forbidden,

    /// <summary>Something outside the studio did not answer, or did not answer with what was asked for.
    /// Nothing the author wrote is wrong and nothing here is broken. <i>Try again, or look
    /// upstream.</i></summary>
    Unavailable,

    /// <summary>The studio's own defect, or its own stored data. Nothing the author can change fixes it.
    /// <i>Report it.</i></summary>
    Internal,
}

/// <summary>
/// <b>What a rule is about.</b> A rule concerns a combination — <c>WX6</c> is a plan, a structure and an
/// objective at once — and a family prefix is one token, so it names the loudest of them and the rest goes
/// unsaid. The list is uncapped: a rule about five things says five.
///
/// <para>Deliberately not called <i>subjects</i>. <see cref="Finding.Subjects"/> already holds instance ids —
/// which piece, which prop — and one name over a kind and an instance is the duplication <c>CLAUDE.md</c>
/// names under <i>One type, one responsibility</i>.</para>
/// </summary>
[JsonConverter(typeof(RuleConcernConverter))]
public enum RuleConcern
{
    /// <summary>The call itself.</summary>
    Request,

    /// <summary>The rough geometry, and where the intent is stated. A piece's stated height is plan geometry,
    /// not <see cref="Terrain"/>.</summary>
    Plan,

    /// <summary>How the map is meant to be played; says nothing about how it looks.</summary>
    Intent,

    /// <summary>What is contested — single-action (a destroyable is broken, a core is leaked) or staged (a
    /// wool is touched, then captured).</summary>
    Objective,

    /// <summary>Where players enter.</summary>
    Spawn,

    /// <summary>The height — the relief, as drawn or as built. Never a piece's stated surface, which is
    /// <see cref="Plan"/>.</summary>
    Terrain,

    /// <summary>What is built as a building: a house, a wool cage, a spawn hall.</summary>
    Structure,

    /// <summary>What is scattered on the ground: boulders, trees, paths, rivers.</summary>
    Feature,

    /// <summary>A block in a role.</summary>
    Material,

    /// <summary>How materials assemble into a pattern.</summary>
    Style,

    /// <summary>Styles combined.</summary>
    Theme,

    /// <summary>The built voxels and the <c>map.xml</c> a server reads.</summary>
    World,

    /// <summary>The tool's own limits.</summary>
    Studio,
}

/// <summary>
/// The two machine-legible things a rule carries beyond its id: what a caller does about it, and what it is
/// about. Written beside the <c>const</c> that declares the id, so the classification lives where the rule
/// does and <c>RuleCatalog</c> reads it the same way it reads the docstring — there is no table to fall out
/// of step with.
///
/// <para><b>Both belong to the rule, not to the finding.</b> A category is fixed by the id: the 76 constants
/// are raised from 96 sites, so a field on <see cref="Finding"/> would have 24 sites restating a fact another
/// site already fixed, with nothing checking they agree. A caller joins on the id, which is what
/// <c>/api/rules</c> is for.</para>
///
/// <para>A rule that <b>refuses nothing</b> — one stating how something is derived, which a document cites
/// and no finding raises — takes the concerns-only form and has no <see cref="Category"/>: there is no caller
/// to branch and nothing to do about it.</para>
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class RuleAttribute : Attribute
{
    /// <summary>A rule a finding cites: what to do about it, and what it is about.</summary>
    public RuleAttribute(RuleCategory category, params RuleConcern[] concerns)
    {
        Category = category;
        Concerns = concerns;
    }

    /// <summary>A rule nothing raises: what it is about, and no category, because no caller branches on
    /// it.</summary>
    public RuleAttribute(params RuleConcern[] concerns) => Concerns = concerns;

    /// <summary>What a caller does about a finding citing this rule. Null where nothing raises it.</summary>
    public RuleCategory? Category { get; }

    /// <summary>What the rule is about, in declaration order.</summary>
    public RuleConcern[] Concerns { get; }
}
