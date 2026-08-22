using System.Net;
using System.Text;

namespace PgmStudio.Api.Tests;

/// <summary>
/// The plan as a grid of characters, from a stored map and from a posted document. It is the one render a
/// caller with no image reader can act on, so it answers <c>text/plain</c> rather than a JSON wrapper; these
/// cover that contract, the downsample, a map without a plan being refused rather than answered as an empty
/// board, and — the reason <c>POST /plan/ascii</c> exists — that the same grid comes back without a map row.
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

    /// <summary>
    /// The same grid off a posted document, with nothing stored. Every other read of a plan answers a posted
    /// one; this had needed a map row, which is what put a reimplementation of the renderer in the driver
    /// next door.
    /// </summary>
    [Test]
    public async Task A_posted_plan_answers_the_same_grid_with_nothing_stored()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();

        var posted = await client.PostAsync("/api/plan/ascii",
            new StringContent(Plan, Encoding.UTF8, "application/json"));

        await Assert.That(posted.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(posted.Content.Headers.ContentType?.MediaType).IsEqualTo("text/plain");

        var grid = await posted.Content.ReadAsStringAsync();
        await Assert.That(grid).Contains("slab");
        await Assert.That(grid).Contains("20 × 12 cells = 100 × 60 blocks");

        // And it is the same render, not a second one: the stored route draws the identical board.
        var (stored, slug) = await SeedAsync();
        var fromMap = await (await stored.GetAsync($"/api/map/{slug}/plan/ascii")).Content.ReadAsStringAsync();
        await Assert.That(grid).IsEqualTo(fromMap);
        stored.Dispose();
    }

    /// <summary>A body that is not a plan is the request's own fault, answered as <c>RQ1</c> like every other
    /// posted-plan route rather than as an empty board.</summary>
    [Test]
    public async Task A_posted_body_that_is_not_a_plan_is_refused()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();

        var posted = await client.PostAsync("/api/plan/ascii",
            new StringContent("not a plan", Encoding.UTF8, "application/json"));

        await Assert.That(posted.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
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
