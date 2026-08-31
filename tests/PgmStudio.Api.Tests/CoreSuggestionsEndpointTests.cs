using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LinqToDB;
using LinqToDB.Async;
using Microsoft.Extensions.DependencyInjection;
using PgmStudio.Data.Features;
using PgmStudio.Data.Schema;
using PgmStudio.Domain;
using PgmStudio.Minecraft;
using PgmStudio.Geom;
using PgmStudio.Analysis.Suggest;

namespace PgmStudio.Api.Tests;

/// <summary>
/// GET /api/map/{slug}/core-suggestions — the configure tool's read of the cores the ingest scan proposed.
/// It is the only way back to them: the world is deleted after import, so a suggestion the gather stored is
/// all the tier ever sees. What is asserted here is that every measured parameter survives that read, that a
/// drawn box narrows the list, and that the casing defaults ride along — the client has no other source for
/// them and would otherwise carry a second copy of <see cref="ObjectiveDefaults"/>.
/// </summary>
[NotInParallel("api-db")]
public sealed class CoreSuggestionsEndpointTests
{
    private static async Task<(HttpClient Client, string Slug, long MapId)> SetUpAsync()
    {
        await ApiTestFactory.ResetSchemaAsync();
        var client = ApiTestFactory.Shared.CreateClient();

        var slug = (await (await client.PostAsJsonAsync("/api/sketch", new { name = "Core Map" }))
            .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("slug").GetString()!;

        using var scope = ApiTestFactory.Shared.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PgmDb>();
        var mapId = await db.Maps.Where(m => m.Slug == slug).Select(m => m.Id).FirstAsync();
        return (client, slug, mapId);
    }

    private static async Task WriteAsync(long mapId, params CoreSuggestion[] cores)
    {
        using var scope = ApiTestFactory.Shared.Services.CreateScope();
        await CoreCandidateStore.WriteAsync(scope.ServiceProvider.GetRequiredService<PgmDb>(), mapId, cores);
    }

    private static CoreSuggestion Core(int x, int z, int size = 5, int shell = 1, int rise = 6, bool openTop = false)
        => new(new BlockBox(x, 20, z, x + size - 1, 24, z + size - 1), 27, shell, rise, openTop);

    [Test]
    public async Task Every_measured_parameter_survives_the_read()
    {
        var (client, slug, mapId) = await SetUpAsync();
        await WriteAsync(mapId, Core(100, 200, size: 7, shell: 2, rise: 9, openTop: true));

        var body = await client.GetFromJsonAsync<JsonElement>($"/api/map/{slug}/core-suggestions");
        var core = body.GetProperty("cores").EnumerateArray().Single();

        var box = core.GetProperty("box");
        await Assert.That(box.GetProperty("minX").GetInt32()).IsEqualTo(100);
        await Assert.That(box.GetProperty("minY").GetInt32()).IsEqualTo(20);
        await Assert.That(box.GetProperty("maxZ").GetInt32()).IsEqualTo(206);
        // Footprint and height are read off the box, so a confirmed suggestion states the casing that is
        // actually there rather than the generator's default.
        await Assert.That(core.GetProperty("size").GetInt32()).IsEqualTo(7);
        await Assert.That(core.GetProperty("height").GetInt32()).IsEqualTo(5);
        await Assert.That(core.GetProperty("shell").GetInt32()).IsEqualTo(2);
        await Assert.That(core.GetProperty("float").GetInt32()).IsEqualTo(9);
        await Assert.That(core.GetProperty("openTop").GetBoolean()).IsTrue();
        await Assert.That(core.GetProperty("lava").GetInt32()).IsEqualTo(27);
    }

    [Test]
    public async Task A_drawn_box_narrows_the_list_to_the_casings_it_touches()
    {
        var (client, slug, mapId) = await SetUpAsync();
        await WriteAsync(mapId, Core(0, 0), Core(500, 500));

        var all = await client.GetFromJsonAsync<JsonElement>($"/api/map/{slug}/core-suggestions");
        await Assert.That(all.GetProperty("cores").GetArrayLength()).IsEqualTo(2);

        var boxed = await client.GetFromJsonAsync<JsonElement>(
            $"/api/map/{slug}/core-suggestions?box=-20,0,-20,20,255,20");
        var only = boxed.GetProperty("cores").EnumerateArray().Single();
        await Assert.That(only.GetProperty("box").GetProperty("minX").GetInt32()).IsEqualTo(0);
    }

    [Test]
    public async Task A_box_that_cannot_be_read_is_refused_rather_than_ignored()
    {
        // The filter is optional, so a failed parse must not read as an absent one: skipping it answers every
        // casing the map has under a 200, which is a mistyped box reading as "this volume holds them all".
        // Absent means no filter; stated and unreadable is RQ1, the same fault the monument route names.
        var (client, slug, mapId) = await SetUpAsync();
        await WriteAsync(mapId, Core(0, 0), Core(500, 500));

        var refused = await client.GetAsync($"/api/map/{slug}/core-suggestions?box=garbage");
        await Assert.That(refused.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);

        var body = await refused.Content.ReadFromJsonAsync<JsonElement>();
        var finding = body.GetProperty("findings").EnumerateArray().Single();
        await Assert.That(finding.GetProperty("rule").GetString()).IsEqualTo("RQ1");
        await Assert.That(finding.GetProperty("field").GetString()).IsEqualTo("box");
    }

    [Test]
    public async Task The_casing_defaults_come_back_even_when_nothing_was_found()
    {
        // A map with no proposals still needs them: a core placed by hand takes its size, shell, float and
        // leak from here, and the client cannot reach ObjectiveDefaults.
        var (client, slug, _) = await SetUpAsync();

        var body = await client.GetFromJsonAsync<JsonElement>($"/api/map/{slug}/core-suggestions");
        await Assert.That(body.GetProperty("cores").GetArrayLength()).IsEqualTo(0);

        var defaults = body.GetProperty("defaults");
        await Assert.That(defaults.GetProperty("lava").GetInt32()).IsEqualTo(ObjectiveDefaults.CoreLava);
        await Assert.That(defaults.GetProperty("lavaHeight").GetInt32()).IsEqualTo(ObjectiveDefaults.CoreLavaHeight);
        await Assert.That(defaults.GetProperty("float").GetInt32()).IsEqualTo(ObjectiveDefaults.CoreFloat);
        await Assert.That(defaults.GetProperty("leak").GetInt32()).IsEqualTo(ObjectiveDefaults.CoreLeak);
    }

    [Test]
    public async Task An_unknown_map_is_not_found()
    {
        var (client, _, _) = await SetUpAsync();
        var resp = await client.GetAsync("/api/map/no-such-map/core-suggestions");
        await Assert.That(resp.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }
}
