using System.Text.Json;

namespace PgmStudio.Api.Tests;

/// <summary>
/// The endpoint tables in <c>docs/tools/</c> and the generated schema describe one surface, and these hold
/// them to each other in the direction <see cref="DocumentedRouteTests"/> does not: that every route the API
/// serves is <b>in</b> a table, and that a row's <c>Fails with</c> column names the codes its operation
/// publishes.
///
/// <para><b>Generating the tables would be the wrong fix.</b> The <c>Answers</c> and <c>Does</c> columns are
/// editorial prose written for that tool's reader — what a route is *for*, which of two bodies it takes, why
/// a field is not carried — and a sentence assembled from a schema would say less in more words. So the
/// tables stay hand-written and are checked instead, which leaves the prose alone and still cannot drift.
/// </para>
///
/// <para><see cref="EndpointTables"/> is the one reader both this and <see cref="DocumentedRouteTests"/>
/// parse a row with, so the two cannot disagree about what a row says before disagreeing about whether it is
/// true.</para>
///
/// <para><b>The two checks read a row differently on purpose, and the asymmetry is the point.</b> Coverage
/// asks whether a route is documented anywhere, so it reads a row loosely — every backticked path in the
/// leading cell, each verb the cell names, and a path standing for the family under it. A loose reader can
/// only ever call a route documented when it is not, which weakens the check; it can never fail a route that
/// is. The failure codes are read strictly — one row, its own operation — because there the reader's mistake
/// would be a false failure over prose, which is the one thing a gate over prose must not do.</para>
/// </summary>
[NotInParallel("api-db")]
public sealed class DocumentedFailureTests
{
    /// <summary>The routes deliberately in no tool's table, and why each is. A tool document describes a
    /// tool; these three belong to something else, and a row in one of the eight would be filed under the
    /// wrong subject rather than found.</summary>
    private static readonly Dictionary<string, string> Unlisted = new()
    {
        ["GET /api/health"] = "the liveness probe — an operational route, not one an author or an agent drives",
        ["GET /api/rules"] = "the rule catalogue, whose document is docs/refusals.md: every gate's ids in one "
                           + "place is what a caller reads it for, and no single tool owns them",
        ["GET /api/rules/terms"] = "the evaluator's term catalogue, whose document is "
                                 + "docs/generator/evaluator.md beside the deriver measurables it lists",
    };

    [Test]
    public async Task Every_route_the_api_serves_is_in_an_endpoint_table()
    {
        var rows = EndpointTables.Rows();
        var undocumented = (await OperationsAsync())
            .Where(operation => !Unlisted.ContainsKey(operation.Name))
            .Where(operation => !rows.Any(row => row.Covers(operation.Verb, operation.Path)))
            .Select(operation => operation.Name)
            .ToList();

        await Assert.That(undocumented).IsEmpty()
            .Because($"{undocumented.Count} route(s) are in no endpoint table and on no named list:"
                     + $"{Environment.NewLine}  {string.Join($"{Environment.NewLine}  ", undocumented)}");
    }

    /// <summary>And the list stays honest from the other side: a route named there that has since been
    /// tabled is an exception nobody needs, and one the API no longer serves is a note about nothing.</summary>
    [Test]
    public async Task The_deliberately_unlisted_are_still_unlisted_and_still_served()
    {
        var rows = EndpointTables.Rows();
        var operations = await OperationsAsync();

        foreach (var (name, why) in Unlisted)
        {
            var operation = operations.FirstOrDefault(operation => operation.Name == name);
            await Assert.That(operation.Name).IsEqualTo(name).Because($"{name} is not a route the API serves");
            await Assert.That(rows.Any(row => row.Covers(operation.Verb, operation.Path))).IsFalse()
                .Because($"{name} is in an endpoint table and is also listed as deliberately unlisted ({why})");
        }
    }

    /// <summary>
    /// What a row promises a caller must be ready for. The 400 and 500 every route publishes from one place
    /// are not in it — a row naming them would say the same thing 149 times — so the column carries exactly
    /// the refusals that are this route's own, and the set has to match rather than merely overlap: a row
    /// claiming a code the route cannot answer sends a caller writing a branch it will never take, and one
    /// missing a code the route does answer is the 404 nobody handled.
    /// </summary>
    [Test]
    public async Task A_rows_failure_codes_are_the_codes_its_operation_publishes()
    {
        var operations = (await OperationsAsync()).ToDictionary(operation => operation.Name);
        var wrong = new List<string>();

        foreach (var row in EndpointTables.Rows().Where(row => row.Claimed is not null))
        {
            var name = $"{row.Verb} {row.LeadPath}";
            if (!operations.TryGetValue(name, out var operation)) continue;
            if (!row.Claimed!.SetEquals(operation.Refusals))
                wrong.Add($"{row.Where}: {name} — the row says "
                          + $"[{string.Join(", ", row.Claimed.Order())}], the schema publishes "
                          + $"[{string.Join(", ", operation.Refusals.Order())}]");
        }

        await Assert.That(wrong).IsEmpty()
            .Because($"{wrong.Count} row(s) name failure codes the schema does not:"
                     + $"{Environment.NewLine}  {string.Join($"{Environment.NewLine}  ", wrong)}");
    }

    /// <summary>A guard on the harness: a reader that matched nothing would pass all three by having nothing
    /// to check.</summary>
    [Test]
    public async Task The_documents_carry_rows_with_failure_codes()
    {
        var rows = EndpointTables.Rows();
        await Assert.That(rows.Count).IsGreaterThan(80);
        await Assert.That(rows.Count(row => row.Claimed is { Count: > 0 })).IsGreaterThan(40);
    }

    /// <summary>One operation as both checks need it: the name the tests report, the two halves that name is
    /// made of, and the refusals it publishes beyond the envelope every route carries.</summary>
    private readonly record struct Operation(string Verb, string Path, IReadOnlySet<int> Refusals)
    {
        public string Name => $"{Verb} {Path}";
    }

    private static async Task<List<Operation>> OperationsAsync()
    {
        using var client = ApiTestFactory.Shared.CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/api/openapi/v1.json"));
        return
        [
            .. from path in document.RootElement.GetProperty("paths").EnumerateObject()
               from verb in path.Value.EnumerateObject()
               select new Operation(verb.Name.ToUpperInvariant(), path.Name,
                   verb.Value.TryGetProperty("responses", out var responses)
                       ? [.. from code in responses.EnumerateObject()
                             where int.TryParse(code.Name, out var status)
                                && !EndpointTables.Everywhere.Contains(status)
                             select int.Parse(code.Name)]
                       : new HashSet<int>())
        ];
    }
}
