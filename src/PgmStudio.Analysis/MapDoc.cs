namespace PgmStudio.Analysis;

using Dict = Dictionary<string, object?>;

/// <summary>
/// Reading the map document, which reaches this project as a loosely-typed dictionary and has to be coaxed
/// into shapes before anything can be derived from it. One reader, so a field two derivations both look at
/// cannot be understood two ways — a number that is a <c>long</c> in one place and a <c>double</c> in another
/// is the document's business, not each caller's.
/// </summary>
internal static class MapDoc
{
    public static Dict AsDict(object? value) => value as Dict ?? [];

    public static List<object?> AsList(object? value) => value as List<object?> ?? [];

    /// <summary>A JSON number whatever the deserializer made of it.</summary>
    public static double? Num(object? value) => value switch
    {
        double d => d,
        long l => l,
        int i => i,
        float f => f,
        _ => null,
    };

    /// <summary>A flag the document may state as a boolean or as text. <c>"1"</c> counts, because a document
    /// round-tripped through a writer that renders booleans as digits still means what it said.</summary>
    public static bool Truthy(object? value) => value is true
        || (value is string text && (text.Equals("true", StringComparison.OrdinalIgnoreCase) || text == "1"));

    /// <summary>A material or id as the document spells it, reduced to the one spelling a lookup uses.</summary>
    public static string Normalize(string value) => value.Trim().ToLowerInvariant().Replace('_', ' ');
}
