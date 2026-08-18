using System.Net;
using System.Text;

namespace PgmStudio.Api.Tests;

/// <summary>
/// GET /api/map/{slug}/plan/ascii — the plan as a grid of characters. It is the one render a caller with no
/// image reader can act on, so it answers <c>text/plain</c> rather than a JSON wrapper; these cover that
/// contract, the downsample, and a map without a plan being refused rather than answered as an empty board.
/// </summary>
[NotInParallel("api-db")]
public sealed class PlanAsciiEndpointTests
{
    private const string Plan = """
        {"plan":1,"globals":{"cell":5,"symmetry":"none","surface":9},
         "pieces":[{"id":"slab","role":"piece","rect":[0,0,20,12]}]}
        """;

    [Test]
    public async Task A_stored_plan_answers_a_grid_as_plain_text()
    {
        var (client, slug) = await SeedAsync();

        var response = await client.GetAsync($"/api/map/{slug}/plan/ascii");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(response.Content.Headers.ContentType?.MediaType).IsEqualTo("text/plain");

        var grid = await response.Content.ReadAsStringAsync();
        await Assert.That(grid).Contains("slab").Because("the key names every piece it drew");
        await Assert.That(grid).Contains("20 × 12 cells = 100 × 60 blocks");
    }

    [Test]
    public async Task Every_downsamples_and_still_states_the_true_size()
    {
        var (client, slug) = await SeedAsync();

        var full = await client.GetStringAsync($"/api/map/{slug}/plan/ascii");
        var half = await client.GetStringAsync($"/api/map/{slug}/plan/ascii?every=2");

        await Assert.That(half.Length).IsLessThan(full.Length);
        await Assert.That(half).Contains("1:2");
        await Assert.That(half).Contains("20 × 12 cells").Because("the board's real size does not change");
    }

    [Test]
    public async Task An_unknown_map_is_a_404_and_a_blank_plan_says_it_drew_nothing()
    {
        var (client, slug) = await SeedAsync(storePlan: false);

        await Assert.That((await client.GetAsync("/api/map/no-such-map/plan/ascii")).StatusCode)
            .IsEqualTo(HttpStatusCode.NotFound);

        // originating a plan leaves an empty document behind, so the map has one — it just draws nothing,
        // which is a different answer from having none and worth saying out loud rather than as a blank body
        var blank = await client.GetAsync($"/api/map/{slug}/plan/ascii");
        await Assert.That(blank.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(await blank.Content.ReadAsStringAsync()).Contains("draws nothing");
    }

    /// <summary>A fresh schema, one map originated through the real endpoint, and its plan stored.</summary>
    private static async Task<(HttpClient Client, string Slug)> SeedAsync(bool storePlan = true)
    {
        await ApiTestFactory.ResetSchemaAsync();
        var client = ApiTestFactory.Shared.CreateClient();

        var created = await client.PostAsync("/api/plan",
            new StringContent("""{"name":"Ascii Seed"}""", Encoding.UTF8, "application/json"));
        created.EnsureSuccessStatusCode();
        var slug = System.Text.Json.JsonDocument.Parse(await created.Content.ReadAsStringAsync())
            .RootElement.GetProperty("slug").GetString()!;

        if (storePlan)
        {
            var put = await client.PutAsync($"/api/map/{slug}/plan",
                new StringContent(Plan, Encoding.UTF8, "application/json"));
            put.EnsureSuccessStatusCode();
        }
        return (client, slug);
    }
}
