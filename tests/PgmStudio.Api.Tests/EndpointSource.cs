using System.Text.RegularExpressions;

namespace PgmStudio.Api.Tests;

/// <summary>
/// The endpoints as source: the routes each endpoint class serves and the query words it reads off the request.
///
/// <para>A query word is read three ways — <c>Query&lt;T&gt;("word")</c>, <c>Request.Query["word"]</c>, and a
/// helper handed the word, <c>Csv("wools")</c> or <c>OptionalInt("ymax")</c> — and a class inherits what its
/// base reads. A read through a key the source does not spell (a shared reader's constant) is not seen; those
/// are the readers whose words a processor publishes for every route that uses them.</para>
/// </summary>
internal static class EndpointSource
{
    /// <summary>One endpoint class: the routes it serves, as <c>VERB /api/path</c>, and the words it reads.</summary>
    public sealed record Endpoint(string Class, IReadOnlyList<string> Routes, IReadOnlySet<string> Words);

    public static IReadOnlyList<Endpoint> All()
    {
        var files = Directory.EnumerateFiles(Root(), "*.cs", SearchOption.AllDirectories)
            .Select(File.ReadAllText).ToList();
        var helpers = files.SelectMany(Helpers).ToHashSet(StringComparer.Ordinal);

        var chunks = files.SelectMany(Chunks).ToList();
        var bases = chunks.GroupBy(chunk => chunk.Name, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        return
        [
            .. chunks.Select(chunk => new Endpoint(chunk.Name, Routes(chunk.Body),
                Inherited(chunk, bases, helpers, depth: 0)))
        ];
    }

    private sealed record Chunk(string Name, string? Base, string Body);

    // A class declaration at the start of a line, with what it derives from where that is a class of its own.
    private static readonly Regex ClassStart = new(
        @"^(?:public|internal)\s+(?:sealed\s+|abstract\s+|static\s+|partial\s+)*class\s+(\w+)[^\n:]*(?::\s*(\w+))?",
        RegexOptions.Multiline);

    private static IEnumerable<Chunk> Chunks(string source)
    {
        var starts = ClassStart.Matches(source).ToList();
        for (var index = 0; index < starts.Count; index++)
        {
            var end = index + 1 < starts.Count ? starts[index + 1].Index : source.Length;
            var start = starts[index];
            yield return new Chunk(start.Groups[1].Value,
                start.Groups[2].Success ? start.Groups[2].Value : null, source[start.Index..end]);
        }
    }

    private static readonly Regex Route = new(@"\b(Get|Post|Put|Patch|Delete)\(""([^""]+)""");

    // A route parameter's constraint is the binder's business, not part of the path the document names.
    private static List<string> Routes(string body) =>
        [.. Route.Matches(body).Select(match => $"{match.Groups[1].Value.ToUpperInvariant()} /api"
            + Regex.Replace(match.Groups[2].Value, @"\{(\w+):[^}]+\}", "{$1}"))];

    private static readonly Regex Direct = new(
        @"Query<[^>]+>\(""(\w+)""|Request\.Query\[""(\w+)""\]|Request\.Query\.(?:TryGetValue|ContainsKey)\(""(\w+)""");

    // A member handed the word as its first parameter and reading the query with it.
    private static readonly Regex HelperDeclaration =
        new(@"\b(\w+)(?:<[^>()]*>)?\(string\s+(\w+)[^)]*\)\s*(?:where[^{=;]*)?(?:=>|\{)");

    private static IEnumerable<string> Helpers(string source)
    {
        foreach (Match declaration in HelperDeclaration.Matches(source))
        {
            var parameter = Regex.Escape(declaration.Groups[2].Value);
            var body = source.Substring(declaration.Index, Math.Min(400, source.Length - declaration.Index));
            if (Regex.IsMatch(body, $@"Query<[^>]+>\({parameter}\b|Request\.Query\[{parameter}\]"))
                yield return declaration.Groups[1].Value;
        }
    }

    private static HashSet<string> Inherited(
        Chunk chunk, Dictionary<string, Chunk> bases, HashSet<string> helpers, int depth)
    {
        var words = Direct.Matches(chunk.Body)
            .Select(match => match.Groups.Values.Skip(1).First(group => group.Success).Value)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var helper in helpers)
            foreach (Match call in Regex.Matches(chunk.Body, $@"\b{Regex.Escape(helper)}(?:<[^>()]*>)?\(""(\w+)"""))
                words.Add(call.Groups[1].Value);
        if (depth < 4 && chunk.Base is { } name && bases.TryGetValue(name, out var parent))
            words.UnionWith(Inherited(parent, bases, helpers, depth + 1));
        return words;
    }

    /// <summary>The endpoint sources sit beside the tests in the repository; the root is found by walking up
    /// to it, as the documents are.</summary>
    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "PgmStudio.Api", "Endpoints")))
            dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new DirectoryNotFoundException(
            "no src/PgmStudio.Api/Endpoints above the test output — the repository layout moved"),
            "src", "PgmStudio.Api", "Endpoints");
    }
}
