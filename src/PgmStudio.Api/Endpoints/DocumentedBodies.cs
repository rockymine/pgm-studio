using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace PgmStudio.Api.Endpoints;

/// <summary>
/// The worked JSON blocks in the tool documents, read out of the copies the API carries.
///
/// <para>A block's fence says what it is. <c>```json POST /api/styles</c> is a body that route takes, and the
/// API hands it out as that operation's example; <c>```json TerrainTheme</c> is a document of a shape the schema
/// names — part of a larger body, or an answer — and is held to that shape rather than posted. A fence naming
/// neither says nothing a test can hold it to.</para>
///
/// <para>The documents are embedded at build time, so the examples a running studio publishes are the ones
/// its own build was tested against. Markdown renderers take the fence's first word as the language and ignore
/// the rest, so a block still highlights as JSON.</para>
/// </summary>
public static class DocumentedBodies
{
    /// <summary>One block: where it is, what its fence names, and the JSON.</summary>
    /// <param name="Where">The document and line, <c>library.md:106</c>.</param>
    /// <param name="Verb">The verb a routed block is sent with, or null.</param>
    /// <param name="Route">The route a routed block is sent to, its query string included, or null.</param>
    /// <param name="Shape">The schema an unrouted block is a document of, or null.</param>
    /// <param name="Json">The block.</param>
    public sealed record Block(string Where, string? Verb, string? Route, string? Shape, string Json)
    {
        /// <summary>The route without its query string, which is the path the schema files it under.</summary>
        public string? Path => Route?.Split('?')[0];

        public override string ToString() => $"{Where} {Verb ?? Shape} {Route}".TrimEnd();
    }

    private const string Prefix = "docs/tools/";

    private static readonly Regex Fence = new(@"^```json(?:\s+(?:(GET|POST|PUT|PATCH|DELETE)\s+(\S+)|(\w+)))?\s*$");

    /// <summary>Every block in one document.</summary>
    public static IEnumerable<Block> In(string document, string markdown)
    {
        var lines = markdown.Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            var fence = Fence.Match(lines[index].TrimEnd('\r'));
            if (!fence.Success) continue;
            var body = new StringBuilder();
            for (var inner = index + 1; inner < lines.Length && !lines[inner].StartsWith("```"); inner++)
                body.AppendLine(lines[inner].TrimEnd('\r'));
            yield return new Block($"{document}:{index + 1}",
                fence.Groups[1].Success ? fence.Groups[1].Value : null,
                fence.Groups[2].Success ? fence.Groups[2].Value : null,
                fence.Groups[3].Success ? fence.Groups[3].Value : null,
                body.ToString());
        }
    }

    private static List<Block> Read()
    {
        var assembly = typeof(DocumentedBodies).Assembly;
        var blocks = new List<Block>();
        foreach (var name in assembly.GetManifestResourceNames().Where(name => name.StartsWith(Prefix)).Order())
        {
            using var stream = assembly.GetManifestResourceStream(name)!;
            using var reader = new StreamReader(stream);
            blocks.AddRange(In(name[Prefix.Length..], reader.ReadToEnd()));
        }
        return blocks;
    }

    /// <summary>Every block, read once. Declared after the reader's own statics, which a static initializer
    /// reads in the order they are written.</summary>
    public static IReadOnlyList<Block> All { get; } = Read();
}
