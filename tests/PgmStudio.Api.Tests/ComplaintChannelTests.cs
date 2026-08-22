using System.Net.Http.Json;
using System.Text.Json;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;

namespace PgmStudio.Api.Tests;

/// <summary>
/// The <c>warnings</c> key is written by middleware and declared by an operation processor, and nothing in
/// the compiler connects the two: the rule lives in <c>Complaints</c>, the claim lives in
/// <c>ComplaintChannel</c>, and a change to either leaves the other stating something that is no longer
/// true. So this asserts the claim against a real answer — a write posted with a field the reader has
/// nowhere to keep, which is the cheapest way to make a gate remark on something.
///
/// <para>Runs against the <c>pgm_studio_test</c> schema, so it runs serially with the other DB suites.</para>
/// </summary>
[NotInParallel("api-db")]
public sealed class ComplaintChannelTests
{
    /// <summary>A success that carried a complaint answers it under the key the document names, and restates
    /// it in the header the document names — both halves, on one response.</summary>
    [Test]
    public async Task A_success_that_remarked_answers_the_key_and_the_header()
    {
        using var client = await SeedAsync("warnmap");

        var resp = await client.PutAsJsonAsync("/api/map/warnmap/plan",
            new { plan = 1, meta = new { name = "Warn Map" }, thisFieldIsNotRead = 7 });
        var body = await resp.Content.ReadAsStringAsync();

        await Assert.That(resp.IsSuccessStatusCode).IsTrue().Because(body);

        var warnings = JsonDocument.Parse(body).RootElement.GetProperty("warnings");
        await Assert.That(warnings.EnumerateArray().Select(f => f.GetProperty("rule").GetString()))
            .Contains("RQ3");

        await Assert.That(resp.Headers.TryGetValues("Pgm-Warnings", out var restated)).IsTrue();
        await Assert.That(restated!.Single()).Contains("RQ3");
    }

    /// <summary>And a success that remarked on nothing carries no key at all — which is what makes an absent
    /// one readable, and what the schema's "present only when" means.</summary>
    [Test]
    public async Task A_success_that_remarked_on_nothing_carries_no_key()
    {
        using var client = await SeedAsync("quietmap");

        var resp = await client.PutAsJsonAsync("/api/map/quietmap/plan",
            new { plan = 1, meta = new { name = "Quiet Map" }, pieces = Array.Empty<object>() });
        var body = await resp.Content.ReadAsStringAsync();

        await Assert.That(resp.IsSuccessStatusCode).IsTrue().Because(body);
        await Assert.That(body).IsEqualTo("{}");
        await Assert.That(resp.Headers.Contains("Pgm-Warnings")).IsFalse();
    }

    /// <summary>The document says so for every answer that can carry one. The key goes on a 2xx whose body
    /// is a JSON object — where <c>Complaints</c> can put one — and the header on every 2xx, since
    /// complaints are handed over before a response starts and no status or media type stops that. Counted
    /// rather than listed: a route added tomorrow is covered by the rule or it is a hole.</summary>
    [Test]
    public async Task Every_answer_that_can_carry_one_says_so()
    {
        var document = JsonDocument.Parse(await DocumentAsync()).RootElement;

        var objectAnswers = new List<string>();
        var silentBody = new List<string>();
        var silentHeader = new List<string>();

        foreach (var path in document.GetProperty("paths").EnumerateObject())
            foreach (var verb in path.Value.EnumerateObject())
            {
                if (verb.Name is "parameters" or "summary" or "description" or "servers") continue;
                if (!verb.Value.TryGetProperty("responses", out var responses)) continue;

                foreach (var response in responses.EnumerateObject())
                {
                    if (!response.Name.StartsWith('2')) continue;
                    var where = $"{verb.Name.ToUpperInvariant()} {path.Name} {response.Name}";

                    if (!Names(response.Value, "headers", "Pgm-Warnings")) silentHeader.Add(where);
                    if (!IsJsonObject(response.Value, document)) continue;

                    objectAnswers.Add(where);
                    if (!Carries(response.Value)) silentBody.Add(where);
                }
            }

        await Assert.That(objectAnswers.Count).IsGreaterThan(50);   // a document that lost them passes vacuously
        await Assert.That(silentBody.Order(StringComparer.Ordinal)).IsEmpty();
        await Assert.That(silentHeader.Order(StringComparer.Ordinal)).IsEmpty();
    }

    private static bool Names(JsonElement response, string section, string name) =>
        response.TryGetProperty(section, out var members) && members.TryGetProperty(name, out _);

    /// <summary>Whether the answer's JSON body is an object — following the one <c>$ref</c> the extended
    /// shape wraps, since that is where the declared type's own kind is stated.</summary>
    private static bool IsJsonObject(JsonElement response, JsonElement document)
    {
        if (!response.TryGetProperty("content", out var content)) return false;
        if (!content.TryGetProperty("application/json", out var json)) return false;
        if (!json.TryGetProperty("schema", out var schema)) return false;

        if (schema.TryGetProperty("allOf", out var all) && all.EnumerateArray().FirstOrDefault() is { } first)
            schema = Resolved(first, document);
        else schema = Resolved(schema, document);

        return schema.TryGetProperty("type", out var type) && type.GetString() == "object";
    }

    private static JsonElement Resolved(JsonElement schema, JsonElement document) =>
        schema.TryGetProperty("$ref", out var reference) && reference.GetString() is { } target
            ? document.GetProperty("components").GetProperty("schemas").GetProperty(target.Split('/')[^1])
            : schema;

    private static bool Carries(JsonElement response) =>
        response.GetProperty("content").GetProperty("application/json").GetProperty("schema")
            .TryGetProperty("properties", out var properties)
        && properties.TryGetProperty("warnings", out _);

    private static async Task<string> DocumentAsync()
    {
        using var client = ApiTestFactory.Shared.CreateClient();
        return await client.GetStringAsync("/api/openapi/v1.json");
    }

    /// <summary>Reset the test schema, seed one empty map, and return a client onto it.</summary>
    private static async Task<HttpClient> SeedAsync(string slug)
    {
        await ApiTestFactory.ResetSchemaAsync();
        await using (var db = new PgmDb(PgmDataOptions.ForConnectionString(ApiTestFactory.ConnectionString)))
        {
            await new MapRepository(db).InsertAsync(new MapRow
            {
                Slug = slug, Name = "Warn Map", Version = "1.0.0", Gamemode = "ctw",
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });
        }
        return ApiTestFactory.Shared.CreateClient();
    }
}
