using System.Text.Json;

namespace PgmStudio.Api.Tests;

/// <summary>
/// Every route an endpoint table in <c>docs/tools/</c> names is a route the API has.
///
/// <para><b>A table is the surface an agent scans</b>, and a row naming a path that does not exist is worse
/// than a missing row: the reader spends a request finding out, and a 404 from a studio route reads like a
/// missing map rather than a missing route. The tables were typed by hand against a surface of 167
/// operations, so nothing but this stops one drifting.</para>
///
/// <para>It is the tables that are checked rather than the prose. A path mentioned mid-sentence is often
/// deliberately partial — a segment, a family, a shape of URL — and holding that to the same test would make
/// the prose unwriteable; a row in a table is a promise that this exact path answers.</para>
/// </summary>
[NotInParallel("api-db")]
public sealed class DocumentedRouteTests
{
    [Test]
    public async Task Every_route_in_an_endpoint_table_is_one_the_api_serves()
    {
        var served = await ServedAsync();
        var missing = TabledRoutes()
            .Where(row => !served.Contains(row.Route))
            .Select(row => $"{row.Where}: {row.Route}")
            .ToList();

        await Assert.That(missing).IsEmpty()
            .Because($"{missing.Count} documented route(s) are not in the schema:"
                     + $"{Environment.NewLine}  {string.Join($"{Environment.NewLine}  ", missing)}");
    }

    /// <summary>A guard on the harness: a reader that matched nothing would pass the test above by having
    /// nothing to check.</summary>
    [Test]
    public async Task The_documents_carry_endpoint_tables()
    {
        await Assert.That(TabledRoutes().Count).IsGreaterThan(40);
    }

    private readonly record struct Row(string Route, string Where);

    /// <summary>Every <c>/api</c> path the schema publishes, with its route parameters normalised away — a
    /// document writes <c>{slug}</c> where a route may say <c>{Slug}</c>, and neither is the point.</summary>
    private static async Task<HashSet<string>> ServedAsync()
    {
        using var client = ApiTestFactory.Shared.CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/api/openapi/v1.json"));
        return [.. document.RootElement.GetProperty("paths").EnumerateObject().Select(path => Normalize(path.Name))];
    }

    /// <summary>
    /// The route each endpoint-table row leads with — the one span carrying a verb, which is the shape every
    /// table in <c>docs/tools/</c> uses.
    ///
    /// <para>Only the leading span. A row often abbreviates its siblings after it
    /// (<c>`GET /map/{slug}/regions` · `/regions/authoring`</c>), and what those abbreviate <em>to</em> is a
    /// convention a reader resolves and a parser guesses at — the tail replaces a prefix whose length is
    /// nowhere stated. Guessing produces false failures, which is the one thing a gate over prose must not
    /// do, so the row's leading promise is what is held to.</para>
    /// </summary>
    private static List<Row> TabledRoutes()
    {
        var rows = new List<Row>();
        foreach (var path in Directory.EnumerateFiles(DocsRoot(), "*.md").OrderBy(path => path))
        {
            var lines = File.ReadAllLines(path);
            for (var i = 0; i < lines.Length; i++)
            {
                if (!lines[i].StartsWith('|')) continue;
                var cell = lines[i].Split('|', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
                var led = System.Text.RegularExpressions.Regex.Match(cell, @"`(?:GET|POST|PUT|PATCH|DELETE)\s+(/[^`\s]*)`");
                if (!led.Success) continue;
                var route = Strip(led.Groups[1].Value);
                if (route.Length > 1) rows.Add(new Row(Normalize("/api" + route), $"{Path.GetFileName(path)}:{i + 1}"));
            }
        }
        return rows;
    }

    /// <summary>A documented path carries what a caller types; the schema carries the route. A query string is
    /// the caller's, and the brackets around an optional segment (<c>teams[/{teamId}]</c>) mark a choice
    /// between two real routes — dropping them names the longer, which is the stronger thing to check.</summary>
    private static string Strip(string route)
    {
        var query = route.IndexOf('?');
        if (query >= 0) route = route[..query];
        return route.Replace("[", "").Replace("]", "").TrimEnd('.', ',');
    }

    private static string Normalize(string route) => route.ToLowerInvariant().TrimEnd('/');

    private static string DocsRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "docs", "tools")))
            dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new DirectoryNotFoundException(
            "no docs/tools above the test output — the repository layout moved"), "docs", "tools");
    }
}
