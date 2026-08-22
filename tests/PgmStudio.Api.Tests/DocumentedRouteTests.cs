using System.Text.Json;

namespace PgmStudio.Api.Tests;

/// <summary>
/// Every route an endpoint table in <c>docs/tools/</c> names is a route the API has.
///
/// <para><b>A table is the surface an agent scans</b>, and a row naming a path that does not exist is worse
/// than a missing row: the reader spends a request finding out, and a 404 from a studio route reads like a
/// missing map rather than a missing route. The tables were typed by hand against a surface of 149
/// operations, so nothing but this stops one drifting.</para>
///
/// <para>Only the route a row <b>leads</b> with. A row often names a family after it
/// (<c>`GET`·`POST` /roof-styles[/{id}]` · `…/storey-styles`</c>), and what those stand for is a convention a
/// reader resolves and a parser guesses at. Guessing produces false failures, which is the one thing a gate
/// over prose must not do, so the row's leading promise is what is held here — and
/// <see cref="DocumentedFailureTests"/> reads the family loosely for the opposite check, where a guess can
/// only weaken it.</para>
/// </summary>
[NotInParallel("api-db")]
public sealed class DocumentedRouteTests
{
    [Test]
    public async Task Every_route_in_an_endpoint_table_is_one_the_api_serves()
    {
        var served = await ServedAsync();
        var missing = EndpointTables.Rows()
            .Where(row => row.LeadPath.Length > 0 && !served.Contains(row.LeadPath))
            .Select(row => $"{row.Where}: {row.LeadPath}")
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
        await Assert.That(EndpointTables.Rows().Count(row => row.LeadPath.Length > 0)).IsGreaterThan(40);
    }

    /// <summary>Every <c>/api</c> path the schema publishes.</summary>
    private static async Task<HashSet<string>> ServedAsync()
    {
        using var client = ApiTestFactory.Shared.CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/api/openapi/v1.json"));
        return
        [
            .. document.RootElement.GetProperty("paths").EnumerateObject()
                .Select(path => EndpointTables.Normalize(path.Name))
        ];
    }
}
