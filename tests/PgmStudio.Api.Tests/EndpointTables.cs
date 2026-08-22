using System.Text.RegularExpressions;

namespace PgmStudio.Api.Tests;

/// <summary>
/// The endpoint tables in <c>docs/tools/</c>, read as rows. One reader, because a row is one thing and the
/// three tests over it — that every path a row names is served, that every route served is in some row, that
/// a row's failure codes are its operation's — would otherwise each parse the same markdown their own way
/// and disagree about what a row says before disagreeing about whether it is true.
///
/// <para>It is the tables that are read rather than the prose. A path mentioned mid-sentence is often
/// deliberately partial — a segment, a family, a shape of URL — and holding that to a test would make the
/// prose unwriteable; a row in a table is a promise that this exact path answers.</para>
/// </summary>
internal static class EndpointTables
{
    /// <summary>Codes no row carries, because every operation publishes them from one place: the two the
    /// global configurator declares, and the successes.</summary>
    public static readonly int[] Everywhere = [200, 201, 204, 400, 500];

    /// <summary>
    /// One endpoint-table row: the verbs and paths its leading cell names, the one route it leads with, where
    /// it is, and — only where the table has a <c>Fails with</c> column — the codes that column claims.
    ///
    /// <para>A row names one route or a family of them. <c>`GET`·`POST`·`PUT`·`DELETE /roof-styles[/{id}]` ·
    /// `…/storey-styles`</c> is one row over twelve operations, and writing it out would be twelve rows
    /// saying the same sentence. So <see cref="Covers"/> takes a path as standing for what sits under it,
    /// which is how a reader takes it too. <see cref="LeadPath"/> is the other reading, and empty on such a
    /// row: only a row leading with one verb and one path states a route of its own.</para>
    /// </summary>
    public readonly record struct Row(
        string Verb, string LeadPath, IReadOnlyList<string> Verbs, IReadOnlyList<string> Paths,
        HashSet<int>? Claimed, string Where)
    {
        public bool Covers(string verb, string path)
        {
            if (Verbs.Count > 0 && !Verbs.Contains(verb)) return false;
            var wanted = Normalize(path);
            return Paths.Any(under => wanted == under || wanted.StartsWith(under + '/'));
        }
    }

    /// <summary>Every table row in <c>docs/tools/</c> whose leading cell names a route.</summary>
    public static List<Row> Rows()
    {
        var rows = new List<Row>();
        foreach (var path in Directory.EnumerateFiles(Root(), "*.md").OrderBy(path => path))
        {
            var lines = File.ReadAllLines(path);
            var fails = -1;
            for (var i = 0; i < lines.Length; i++)
            {
                if (!lines[i].StartsWith('|')) { fails = -1; continue; }
                var cells = Cells(lines[i]);
                var header = Array.FindIndex(cells,
                    cell => cell.StartsWith("Fails with", StringComparison.OrdinalIgnoreCase));
                if (header >= 0) { fails = header; continue; }

                var verbs = Regex.Matches(cells[0], @"\b(?:GET|POST|PUT|PATCH|DELETE)\b")
                    .Select(match => match.Value).Distinct().ToList();
                var paths = Regex.Matches(cells[0], "`([^`]+)`")
                    .SelectMany(match => Paths(match.Groups[1].Value))
                    .Select(candidate => Normalize("/api" + candidate))
                    .Distinct().ToList();
                if (paths.Count == 0) continue;

                var claimed = fails >= 0 && fails < cells.Length
                    ? Regex.Matches(cells[fails], @"(?<![\w.])([1-5]\d\d)(?![\w.])")
                        .Select(match => int.Parse(match.Value))
                        .Where(code => !Everywhere.Contains(code)).ToHashSet()
                    : null;
                var lead = Regex.Match(cells[0], @"^[^`]*`(GET|POST|PUT|PATCH|DELETE)\s+(/[^`\s]*)`");
                var where = $"{Path.GetFileName(path)}:{i + 1}";
                rows.Add(lead.Success
                    ? new Row(lead.Groups[1].Value, Normalize("/api" + Paths(lead.Groups[2].Value).Last()),
                        verbs, paths, claimed, where)
                    : new Row("", "", verbs, paths, null, where));
            }
        }
        return rows;
    }

    /// <summary>A markdown row, split on the pipes that are cell walls rather than the one a cell escapes —
    /// a query naming two parameters writes <c>\|</c> between them.</summary>
    private static string[] Cells(string line) =>
        [.. Regex.Split(line.Trim().Trim('|'), @"(?<!\\)\|").Select(cell => cell.Trim())];

    /// <summary>What a documented span names, as paths — plural, because the brackets round an optional
    /// segment mark a choice between two real routes and a row naming <c>/teams[/{teamId}]</c> promises both.
    /// A verb leads the span, a query string is the caller's, and an ellipsis stands for the family the row is
    /// already inside. A span naming no path at all — the bare <c>`POST`</c> a multi-verb row leads with —
    /// yields none.</summary>
    private static IEnumerable<string> Paths(string span)
    {
        span = Regex.Replace(span.Trim(), @"^(?:GET|POST|PUT|PATCH|DELETE)\s+", "").Replace("…", "");
        var query = span.IndexOf('?');
        if (query >= 0) span = span[..query];
        var optional = span.IndexOf('[');
        if (optional > 1 && span.StartsWith('/')) yield return span[..optional].TrimEnd('.', ',', '/');
        span = span.Replace("[", "").Replace("]", "").TrimEnd('.', ',');
        if (span.StartsWith('/') && span.Length > 1) yield return span;
    }

    /// <summary>A document writes <c>{slug}</c> where a route may say <c>{Slug}</c>, and neither is the
    /// point.</summary>
    public static string Normalize(string route) => route.ToLowerInvariant().TrimEnd('/');

    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "docs", "tools")))
            dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new DirectoryNotFoundException(
            "no docs/tools above the test output — the repository layout moved"), "docs", "tools");
    }
}
