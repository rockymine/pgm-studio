using System.Net;
using System.Net.Http.Json;
using PgmStudio.Contracts;
using PgmStudio.Pgm.Plan;

namespace PgmStudio.Api.Tests;

/// <summary>
/// The plan store's HTTP surface: POST /api/plans saves the plan open in the editor (returning the row it now
/// holds), GET /api/plans lists them for the open-from-DB browser, GET /api/plans/{id} loads one, and DELETE
/// forgets it. A malformed plan body is answered 400, never 500. Runs against <c>pgm_studio_test</c> (override
/// with <c>PGM_STUDIO_TEST_DB</c>); each test resets the schema, so they run serially. The fork-vs-mutate
/// doctrine itself is covered store-side in <c>PlanStoreTests</c>.
/// </summary>
[NotInParallel("api-db")]
public sealed class PlanStoreEndpointsTests
{
    private static string PlanJson(string name) => new PlanModel { Meta = new PlanMeta { Name = name } }.ToJson();

    [Test]
    public async Task Save_list_get_mutate_delete_round_trip()
    {
        await ApiTestFactory.ResetSchemaAsync();
        await using var factory = new ApiTestFactory();
        using var client = factory.CreateClient();

        // Save a fresh plan → an authored row.
        var saved = await (await client.PostAsJsonAsync("/api/plans", new PlanSaveRequest(PlanJson("hand-drawn"), null)))
            .Content.ReadFromJsonAsync<PlanDetail>();
        await Assert.That(saved!.Id).IsGreaterThan(0L);
        await Assert.That(saved.Origin).IsEqualTo("authored");

        // It shows up in the browser list.
        var list = await client.GetFromJsonAsync<List<PlanSummary>>("/api/plans");
        await Assert.That(list!.Count).IsEqualTo(1);
        await Assert.That(list[0].Id).IsEqualTo(saved.Id);
        await Assert.That(list[0].Name).IsEqualTo("hand-drawn");

        // Loading it back carries the document.
        var detail = await client.GetFromJsonAsync<PlanDetail>($"/api/plans/{saved.Id}");
        await Assert.That(detail!.PlanJson).Contains("hand-drawn");

        // Saving again with the source id mutates the authored row in place.
        var mutated = await (await client.PostAsJsonAsync("/api/plans", new PlanSaveRequest(PlanJson("renamed"), saved.Id)))
            .Content.ReadFromJsonAsync<PlanDetail>();
        await Assert.That(mutated!.Id).IsEqualTo(saved.Id);
        await Assert.That(mutated.Name).IsEqualTo("renamed");
        await Assert.That((await client.GetFromJsonAsync<List<PlanSummary>>("/api/plans"))!.Count).IsEqualTo(1);

        // Delete forgets it.
        var del = await client.DeleteAsync($"/api/plans/{saved.Id}");
        await Assert.That(del.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        await Assert.That((await client.GetFromJsonAsync<List<PlanSummary>>("/api/plans"))!.Count).IsEqualTo(0);
    }

    [Test]
    public async Task A_malformed_plan_body_is_400_not_500()
    {
        await ApiTestFactory.ResetSchemaAsync();
        await using var factory = new ApiTestFactory();
        using var client = factory.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/plans", new PlanSaveRequest("{ this is not json", null));
        await Assert.That(resp.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }
}
