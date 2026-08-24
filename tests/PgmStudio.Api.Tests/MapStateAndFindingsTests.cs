using System.Net.Http.Json;
using System.Text;
using PgmStudio.Contracts;

namespace PgmStudio.Api.Tests;

/// <summary>
/// The two reads that make a driver's loop <em>act, then ask</em>: what is wrong with this map right now,
/// and what may be done to it next.
///
/// <para>Both are asserted on what they <b>say</b> rather than on which gate said it — the point of the
/// findings read is that it calls the gates rather than restating them, so a test naming a rule the gate
/// happens to raise today would pin the summary to the gate's wording instead of to its own promise.</para>
/// </summary>
[NotInParallel("api-db")]
public sealed class MapStateAndFindingsTests
{
    /// <summary>A blank plan has no land, which is the one thing a plan cannot be without — so the read
    /// refuses, and says so by the same name every other gate answers.</summary>
    [Test]
    public async Task A_plan_with_nothing_in_it_is_refused_by_the_findings_read()
    {
        using var client = await FreshAsync();
        var slug = await PlanAsync(client, "Weirgate");

        var found = await client.GetFromJsonAsync<MapFindingsDto>($"/api/map/{slug}/findings");

        await Assert.That(found!.Stage).IsEqualTo("plan");
        await Assert.That(found.Refuses).IsTrue()
            .Because($"a plan with no pieces cannot build: {string.Join("; ", found.Findings.Select(f => f.Message))}");
    }

    /// <summary>
    /// The gates a read cannot reach are <b>named</b>, and each says where it is asked instead.
    ///
    /// <para>This is the whole contract of not paying for a build. A list that answered what it could and
    /// said nothing about the rest would read as "nothing is wrong" to a caller who has no way to know the
    /// export gates exist — which is exactly the failure the read is for.</para>
    /// </summary>
    [Test]
    public async Task The_gates_a_read_cannot_reach_are_named_with_where_to_ask_them()
    {
        using var client = await FreshAsync();
        var slug = await PlanAsync(client, "Weirgate");

        var found = await client.GetFromJsonAsync<MapFindingsDto>($"/api/map/{slug}/findings");

        await Assert.That(found!.Unasked).IsNotEmpty();
        foreach (var gate in found.Unasked)
        {
            await Assert.That(gate.Gate).IsNotEmpty();
            await Assert.That(gate.Why).IsNotEmpty();
            await Assert.That(gate.Ask).StartsWith("GET /api/map/");
        }
    }

    /// <summary>A map's own read says where it has got to, what it holds and what may be done from there —
    /// and every move it offers is a route the API serves, since a move naming a path that does not answer
    /// is worse than no move at all.</summary>
    [Test]
    public async Task Every_move_a_map_offers_is_a_route_the_api_serves()
    {
        using var client = await FreshAsync();
        var slug = await PlanAsync(client, "Weirgate");

        var state = await client.GetFromJsonAsync<MapState>($"/api/map/{slug}/state");

        await Assert.That(state!.Stage).IsEqualTo("plan");
        await Assert.That(state.Artifacts.Plan).IsTrue();
        await Assert.That(state.Moves).IsNotEmpty();

        var served = await ServedAsync(client);
        foreach (var move in state.Moves)
        {
            var path = move.Route.Split(' ')[1].ToLowerInvariant().TrimEnd('/');
            await Assert.That(served.Contains(path)).IsTrue().Because($"{move.Route} is not a route the API serves");
        }
    }

    /// <summary>A move is offered because the documents it reads are stored, not because the stage is right —
    /// so a plan-stage map is already offered the rebuild that reads its plan, and a map holding no drawing is
    /// not offered the write that replaces one.</summary>
    [Test]
    public async Task A_move_is_offered_on_what_is_stored_rather_than_on_the_stage()
    {
        using var client = await FreshAsync();
        var slug = await PlanAsync(client, "Weirgate");

        var state = await client.GetFromJsonAsync<MapState>($"/api/map/{slug}/state");
        var routes = state!.Moves.Select(move => move.Route).ToList();

        await Assert.That(routes).Contains("PUT /api/map/{slug}/sketch/from-plan");
        await Assert.That(routes).DoesNotContain("PUT /api/map/{slug}/sketch");
    }

    private static async Task<HashSet<string>> ServedAsync(HttpClient client)
    {
        using var document = System.Text.Json.JsonDocument.Parse(
            await client.GetStringAsync("/api/openapi/v1.json"));
        return
        [
            .. document.RootElement.GetProperty("paths").EnumerateObject()
                .Select(path => path.Name.ToLowerInvariant().TrimEnd('/'))
        ];
    }

    private static async Task<string> PlanAsync(HttpClient client, string name)
    {
        var made = await client.PostAsync("/api/plan",
            new StringContent($$"""{"name":"{{name}}"}""", Encoding.UTF8, "application/json"));
        return (await made.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>())
            .GetProperty("slug").GetString()!;
    }

    private static async Task<HttpClient> FreshAsync()
    {
        await ApiTestFactory.ResetSchemaAsync();
        return ApiTestFactory.Shared.CreateClient();
    }
}
