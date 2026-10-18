using System.Net;
using System.Net.Http.Json;
using PgmStudio.Contracts;
using PgmStudio.Pgm.Plan;

namespace PgmStudio.Api.Tests;

/// <summary>
/// The stored plan rows' HTTP surface, kept read-only as history: GET /api/plans lists them, GET /api/plans/{id}
/// answers one with its document and GET /api/plans/{id}/svg draws it, and nothing writes one. Runs against
/// <c>pgm_studio_test</c> (override with <c>PGM_STUDIO_TEST_DB</c>); each test resets the schema, so they run
/// serially.
/// </summary>
[NotInParallel("api-db")]
public sealed class PlanStoreEndpointsTests
{
    [Test]
    public async Task A_stored_row_is_listed_read_and_drawn()
    {
        await ApiTestFactory.ResetSchemaAsync();
        var plan = new PlanModel { Meta = new PlanMeta { Name = "kept" } };
        plan.Pieces.Add(new PlanPiece { Id = "ground", Role = "piece", Rect = new(0, 2, 4, 3) });
        await ApiTestFactory.ExecuteAsync(
            "INSERT INTO plan (name, origin, plan_json, content_hash, seed, composer_version, created_at, updated_at) "
            + $"VALUES ('kept', 'generated', '{plan.ToJson().Replace("'", "''")}', '{new string('0', 64)}', 7, 'walled-5', NOW(), NOW())");
        using var client = ApiTestFactory.Shared.CreateClient();

        var list = (await client.GetFromJsonAsync<List<PlanSummary>>("/api/plans?origin=generated"))!;
        await Assert.That(list.Single().Name).IsEqualTo("kept");
        var id = list[0].Id;

        var detail = await client.GetFromJsonAsync<PlanDetail>($"/api/plans/{id}");
        await Assert.That(detail!.PlanJson).Contains("ground");
        await Assert.That(await client.GetStringAsync($"/api/plans/{id}/svg")).Contains("<svg");
    }

    [Test]
    [Arguments("POST", "/api/plans")]
    [Arguments("DELETE", "/api/plans/1")]
    public async Task Nothing_writes_a_stored_row(string method, string route)
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();

        var resp = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), route)
        {
            Content = method == "POST" ? JsonContent.Create(new { planJson = "{}" }) : null,
        });
        await Assert.That(resp.StatusCode).IsEqualTo(HttpStatusCode.MethodNotAllowed);
    }
}
