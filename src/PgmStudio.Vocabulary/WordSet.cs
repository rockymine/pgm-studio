using System.Reflection;

namespace PgmStudio.Vocabulary;

/// <summary>
/// Names the closed set of words a wire field carries, so the published schema can say so.
///
/// <para>The sets in this project exist because three parties have to spell a <c>map.stage</c>, a
/// <c>style.kind</c>, a theme bucket, a room part or a roof form identically, and they are
/// <c>const string</c>s rather than C# enums because the party that writes one furthest down —
/// <c>Minecraft</c> writing a <c>style.kind</c> — cannot see <c>Contracts</c> and the words are stored and
/// exported as strings. A record's docstring could say which set a field takes and several do, but prose is
/// not something a schema generator or a generated client can read: the field crosses as a bare
/// <c>string</c>, so an agent learns the four stages by being refused one.</para>
///
/// <para>This is what makes the tie machine-legible. The attribute names the class the words are declared in
/// and <see cref="Words.Of"/> reads them off it, so the set is still stated once, where it is stated now.</para>
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class WordSetAttribute(Type declaring) : Attribute
{
    /// <summary>The class declaring the words — <c>MapStage</c>, <c>RoofForms</c>.</summary>
    public Type Declaring => declaring;
}

/// <summary>Reads a word set off the class that declares it.</summary>
public static class Words
{
    /// <summary>Every word the class publishes, in its own order. A set publishes them as a
    /// <c>string[]</c> or, where each word has a label for a picker, as a <c>(string Id, string Name)[]</c>;
    /// the id is the word either way. A class with no <c>All</c> is a set that has not published one, which
    /// is a mistake at the declaration rather than something to paper over, so it is empty.</summary>
    public static IReadOnlyList<string> Of(Type declaring) =>
        declaring.GetField("All", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) switch
        {
            string[] words => words,
            // A labelled set is a value-tuple array, which is not an IEnumerable<object> — a value type gets
            // no covariance — so it is read through the non-generic sequence and each word boxed out of it.
            System.Collections.IEnumerable pairs => [.. pairs.Cast<object>().Select(Id).OfType<string>()],
            _ => [],
        };

    /// <summary>The word out of one entry: the entry itself where the set is a plain sequence of words, or
    /// the first half of its pair where each word carries a label.</summary>
    private static string? Id(object entry) =>
        entry as string ?? entry.GetType().GetField("Item1")?.GetValue(entry) as string;
}
