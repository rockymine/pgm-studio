namespace PgmStudio.Vocabulary;

/// <summary>
/// How a map states which biome each of its columns carries. The word is the <b>type discriminator</b> the
/// field is deserialized by — each kind is its own record with its own fields, and <c>BiomeField</c>'s
/// <c>[JsonDerivedType]</c> attributes name these constants, so the hierarchy and this set cannot say
/// different things.
///
/// <para>They are named after the material kinds on purpose: an author who knows what a cell does to a wall
/// knows what it does here. Three parties spell them — the pass that answers a column, the control that
/// authors one, and the refusal that lists them when a document names one that is not here.</para>
/// </summary>
public static class BiomeKinds
{
    /// <summary>One biome over the whole area — what an unstated field is, and the plainest thing to say.</summary>
    public const string Solid = "solid";

    /// <summary>Jittered regions, each taking one biome from a palette.</summary>
    public const string Cell = "cell";

    /// <summary>A fractal field cut into bands, one biome per band, so regions wander into one another.</summary>
    public const string Noise = "noise";

    /// <summary>The three, in the order a control offers them.</summary>
    public static readonly string[] All = [Solid, Cell, Noise];

    /// <summary>The name a picker offers each kind under.</summary>
    public static string Name(string? kind) => Canonical(kind) switch
    {
        Cell => "Patches",
        Noise => "Blended",
        _ => "One biome",
    };

    /// <summary>What a kind does, in one sentence — what a picker reads under the name.</summary>
    public static string Describe(string? kind) => Canonical(kind) switch
    {
        Cell => "Jittered patches, each taking one biome from the list.",
        Noise => "Regions that drift into one another, one biome each.",
        _ => "One biome over the whole area.",
    };

    /// <summary>The word, or <see cref="Solid"/> for one that is not a kind — the plainest field, and what an
    /// unstated one means.</summary>
    public static string Canonical(string? kind) => All.Contains(kind) ? kind! : Solid;
}
