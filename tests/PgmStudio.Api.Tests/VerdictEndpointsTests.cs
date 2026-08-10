using System.Net;
using System.Net.Http.Json;
using PgmStudio.Contracts;

namespace PgmStudio.Api.Tests;

/// <summary>
/// The verdict surface (G118): POST /api/compose/verdict casts or refines the up/down judgment of a browse
/// board — a persistence act that stores the plan unpinned (labeled corpus, not the tray) with the
/// evaluator's reading at vote time; GET /api/verdicts lists the vote map; DELETE /api/verdicts/{planId}
/// retracts, taking a never-pinned plan with it; GET /api/verdicts/export is the JSONL dataset. Runs against
/// <c>pgm_studio_test</c>; each test resets the schema.
/// </summary>
[NotInParallel("api-db")]
public sealed class VerdictEndpointsTests
{
    private static async Task<ComposeCard> FirstCard(HttpClient client)
    {
        var page = await client.GetFromJsonAsync<ComposePage>("/api/compose?players=12&symmetry=rot_180&seedStart=0&count=1");
        return page!.Cards[0];
    }

    [Test]
    public async Task Vote_persists_unpinned_refines_in_place_and_lists()
    {
        await ApiTestFactory.ResetSchemaAsync();
        await using var factory = new ApiTestFactory();
        using var client = factory.CreateClient();
        var card = await FirstCard(client);

        var cast = await (await client.PostAsJsonAsync("/api/compose/verdict",
            new VerdictSaveRequest(card.Descriptor, "down", ["flat-front"], "meh"))).Content.ReadFromJsonAsync<VerdictDto>();
        await Assert.That(cast!.Verdict).IsEqualTo("down");
        await Assert.That(cast.Tags).Contains("flat-front");
        await Assert.That(cast.EvaluatorVersion).IsNotEmpty();
        await Assert.That(cast.Structure).Contains("|seat:");      // the bucket key rode along

        // voted, not pinned: the tray view stays empty, the corpus row exists
        var tray = await client.GetFromJsonAsync<List<PlanSummary>>("/api/plans?origin=generated&pinned=true");
        await Assert.That(tray!.Count).IsEqualTo(0);
        var all = await client.GetFromJsonAsync<List<PlanSummary>>("/api/plans?origin=generated");
        await Assert.That(all!.Count).IsEqualTo(1);

        // re-voting refines the same judgment rather than stacking a second one
        var refined = await (await client.PostAsJsonAsync("/api/compose/verdict",
            new VerdictSaveRequest(card.Descriptor, "up"))).Content.ReadFromJsonAsync<VerdictDto>();
        await Assert.That(refined!.PlanId).IsEqualTo(cast.PlanId);
        await Assert.That(refined.Verdict).IsEqualTo("up");

        var listed = await client.GetFromJsonAsync<List<VerdictDto>>("/api/verdicts");
        await Assert.That(listed!.Count).IsEqualTo(1);
        await Assert.That(listed[0].Descriptor!.Seed).IsEqualTo(card.Descriptor.Seed);
    }

    [Test]
    public async Task Retract_deletes_a_never_pinned_plan_and_spares_a_pinned_one()
    {
        await ApiTestFactory.ResetSchemaAsync();
        await using var factory = new ApiTestFactory();
        using var client = factory.CreateClient();
        var card = await FirstCard(client);

        // vote only → retract → the plan row goes too
        var cast = await (await client.PostAsJsonAsync("/api/compose/verdict",
            new VerdictSaveRequest(card.Descriptor, "up"))).Content.ReadFromJsonAsync<VerdictDto>();
        await client.DeleteAsync($"/api/verdicts/{cast!.PlanId}");
        await Assert.That((await client.GetFromJsonAsync<List<PlanSummary>>("/api/plans?origin=generated"))!.Count).IsEqualTo(0);

        // pin + vote → retract → the pin survives
        await client.PostAsJsonAsync("/api/compose/pin", card.Descriptor);
        var voted = await (await client.PostAsJsonAsync("/api/compose/verdict",
            new VerdictSaveRequest(card.Descriptor, "up"))).Content.ReadFromJsonAsync<VerdictDto>();
        await client.DeleteAsync($"/api/verdicts/{voted!.PlanId}");
        var tray = await client.GetFromJsonAsync<List<PlanSummary>>("/api/plans?origin=generated&pinned=true");
        await Assert.That(tray!.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Unpinning_a_voted_plan_keeps_the_label_out_of_the_tray()
    {
        await ApiTestFactory.ResetSchemaAsync();
        await using var factory = new ApiTestFactory();
        using var client = factory.CreateClient();
        var card = await FirstCard(client);

        await client.PostAsJsonAsync("/api/compose/pin", card.Descriptor);
        var cast = await (await client.PostAsJsonAsync("/api/compose/verdict",
            new VerdictSaveRequest(card.Descriptor, "down"))).Content.ReadFromJsonAsync<VerdictDto>();

        await client.DeleteAsync($"/api/plans/{cast!.PlanId}");    // the tray's unpin

        await Assert.That((await client.GetFromJsonAsync<List<PlanSummary>>("/api/plans?origin=generated&pinned=true"))!.Count)
            .IsEqualTo(0);
        await Assert.That((await client.GetFromJsonAsync<List<VerdictDto>>("/api/verdicts"))!.Count)
            .IsEqualTo(1).Because("the labeled corpus outlives the tray");
    }

    [Test]
    public async Task Export_is_one_json_line_per_verdict()
    {
        await ApiTestFactory.ResetSchemaAsync();
        await using var factory = new ApiTestFactory();
        using var client = factory.CreateClient();
        var card = await FirstCard(client);

        await client.PostAsJsonAsync("/api/compose/verdict",
            new VerdictSaveRequest(card.Descriptor, "down", ["crammed-mid"], null));

        var resp = await client.GetAsync("/api/verdicts/export");
        await Assert.That(resp.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var body = await resp.Content.ReadAsStringAsync();
        var lines = body.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        await Assert.That(lines.Length).IsEqualTo(1);
        await Assert.That(lines[0]).Contains("\"crammed-mid\"");
        await Assert.That(lines[0]).Contains("\"terms\"");
        await Assert.That(lines[0]).Contains("\"descriptor\"");
    }

    [Test]
    public async Task A_bad_direction_is_400()
    {
        await ApiTestFactory.ResetSchemaAsync();
        await using var factory = new ApiTestFactory();
        using var client = factory.CreateClient();
        var card = await FirstCard(client);

        var resp = await client.PostAsJsonAsync("/api/compose/verdict",
            new VerdictSaveRequest(card.Descriptor, "sideways"));
        await Assert.That(resp.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }
}
