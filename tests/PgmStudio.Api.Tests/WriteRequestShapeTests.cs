using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PgmStudio.Contracts;

namespace PgmStudio.Api.Tests;

/// <summary>
/// The request records the write routes outside the edit tool declare, held to the handlers that read them.
/// These are declared rather than bound — the handler reads the body itself — so nothing in the compiler
/// says the record's spelling and the handler's are the same, and a record that drifts publishes a form
/// nobody reads while the route answers 200 off its defaults.
///
/// <para>Unlike the edit records there is no editor to call, so each is posted through its route and asserted
/// on what changed. Where a route accepts an absent field the assertion has to <b>discriminate</b>: the
/// import pair is posted a value that draws a <i>different</i> refusal from the one an unread key draws, so
/// a passing test cannot be a body that was ignored.</para>
///
/// <para>Runs against the <c>pgm_studio_test</c> schema, so it runs serially with the other DB suites.</para>
/// </summary>
[NotInParallel("api-db")]
public sealed class WriteRequestShapeTests
{
    /// <summary>Every frame field of a sketch origination, read back off the board it seeded. A frameless
    /// create writes no setup at all, so a board carrying one is a body that was read.</summary>
    [Test]
    public async Task A_sketch_is_originated_on_the_frame_the_record_names()
    {
        using var client = await FreshAsync();

        var made = await client.PostAsJsonAsync("/api/sketch", new SketchOriginateRequest(
            Name: "Weirgate", Width: 200, Depth: 140, Mode: "mirror_x", CenterX: 12, CenterZ: -8));
        await Assert.That(made.IsSuccessStatusCode).IsTrue().Because(await made.Content.ReadAsStringAsync());

        var slug = (await made.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("slug").GetString();
        await Assert.That(slug).IsEqualTo("weirgate");

        var setup = (await client.GetFromJsonAsync<JsonElement>($"/api/map/{slug}/sketch")).GetProperty("setup");
        var bbox = setup.GetProperty("bbox");
        await Assert.That(bbox.GetProperty("min_x").GetDouble()).IsEqualTo(-100.0);
        await Assert.That(bbox.GetProperty("max_z").GetDouble()).IsEqualTo(70.0);
        await Assert.That(setup.GetProperty("mirror_mode").GetString()).IsEqualTo("mirror_x");
        await Assert.That(setup.GetProperty("center").GetProperty("cx").GetDouble()).IsEqualTo(12.0);
        await Assert.That(setup.GetProperty("center").GetProperty("cz").GetDouble()).IsEqualTo(-8.0);
    }

    /// <summary>A plan origination is one field, and the slug is derived from it — a name that went unread
    /// originates <c>untitled-plan</c>.</summary>
    [Test]
    public async Task A_plan_is_originated_under_the_name_the_record_names()
    {
        using var client = await FreshAsync();

        var made = await client.PostAsJsonAsync("/api/plan", new PlanOriginateRequest(Name: "Weirgate"));
        await Assert.That(made.IsSuccessStatusCode).IsTrue().Because(await made.Content.ReadAsStringAsync());
        await Assert.That((await made.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("slug").GetString())
            .IsEqualTo("weirgate");
    }

    /// <summary>The symmetry an author states, read back off both routes that answer it.</summary>
    [Test]
    public async Task A_symmetry_is_confirmed_as_the_record_states_it()
    {
        using var client = await FreshAsync();
        var slug = await OriginateAsync(client);

        var patched = await client.PatchAsJsonAsync($"/api/map/{slug}/symmetry", new SymmetryPatchRequest(
            Status: "confirmed", ConfirmedType: "mirror_x", Cx: 12, Cz: -8));
        await Assert.That(patched.IsSuccessStatusCode).IsTrue().Because(await patched.Content.ReadAsStringAsync());

        var symmetry = await client.GetFromJsonAsync<JsonElement>($"/api/map/{slug}/symmetry");
        await Assert.That(symmetry.GetProperty("status").GetString()).IsEqualTo("confirmed");
        await Assert.That(symmetry.GetProperty("primary").GetProperty("type").GetString()).IsEqualTo("mirror_x");
        await Assert.That(symmetry.GetProperty("center").GetProperty("cx").GetDouble()).IsEqualTo(12.0);
        await Assert.That(symmetry.GetProperty("center").GetProperty("cz").GetDouble()).IsEqualTo(-8.0);
    }

    /// <summary>Both values of the exclusion, because the record's boolean is the whole of what distinguishes
    /// the call from its own reverse — a field that went unread would read as <c>false</c> and exclude
    /// nothing.</summary>
    [Test]
    public async Task An_island_is_excluded_and_restored_as_the_record_states_it()
    {
        using var client = await FreshAsync();
        var slug = await OriginateAsync(client);

        var excluded = await client.PatchAsJsonAsync($"/api/configure/{slug}/exclude-island",
            new ExcludeIslandRequest(IslandId: 7, Excluded: true));
        await Assert.That(excluded.IsSuccessStatusCode).IsTrue().Because(await excluded.Content.ReadAsStringAsync());

        var state = await client.GetFromJsonAsync<JsonElement>($"/api/configure/{slug}/state");
        await Assert.That(state.GetProperty("exclude_islands").EnumerateArray().Select(i => i.GetInt32()))
            .IsEquivalentTo(new[] { 7 });

        await client.PatchAsJsonAsync($"/api/configure/{slug}/exclude-island",
            new ExcludeIslandRequest(IslandId: 7, Excluded: false));
        var back = await client.GetFromJsonAsync<JsonElement>($"/api/configure/{slug}/state");
        await Assert.That(back.GetProperty("exclude_islands").EnumerateArray()).IsEmpty();
    }

    /// <summary>The import pair is asserted through a refusal the body <b>chose</b>: a plain-http url is
    /// refused for its scheme and a folder that exists nowhere is refused as not found, where a key the
    /// handler never saw is refused for being absent instead. Same call, different sentence, so a body that
    /// was ignored cannot pass.</summary>
    [Test]
    public async Task The_import_pair_is_refused_for_what_the_record_carries_rather_than_for_its_absence()
    {
        using var client = await FreshAsync();

        var url = await client.PostAsJsonAsync("/api/map/import-url",
            new ImportUrlRequest(Url: "http://example.invalid/world.zip"));
        await Assert.That((int)url.StatusCode).IsEqualTo(400);
        await Assert.That((await url.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString())
            .IsEqualTo("https url required");

        var folder = await client.PostAsJsonAsync("/api/map/import-folder",
            new ImportFolderRequest(Folder: "no-such-world"));
        await Assert.That((int)folder.StatusCode).IsEqualTo((int)HttpStatusCode.NotFound);
    }

    /// <summary>The search rectangle, asserted on the route that requires one: an absent or misspelled
    /// <c>bounds</c> is refused there by name. Its sibling over resources carries the same
    /// <see cref="Bounds2dDto"/> under the same key, and differs only in being allowed to leave it out.</summary>
    [Test]
    public async Task A_search_reads_the_rectangle_the_record_nests()
    {
        using var client = await FreshAsync();
        var slug = await OriginateAsync(client);

        var wools = await client.PostAsJsonAsync($"/api/map/{slug}/wool-sources",
            new WoolSearchRequest(new Bounds2dDto(MinX: -10, MinZ: -10, MaxX: 10, MaxZ: 10)));
        await Assert.That(wools.IsSuccessStatusCode).IsTrue().Because(await wools.Content.ReadAsStringAsync());

        var resources = await client.PostAsJsonAsync($"/api/map/{slug}/resources",
            new ResourceSearchRequest(new Bounds2dDto(MinX: -10, MinZ: -10, MaxX: 10, MaxZ: 10)));
        await Assert.That(resources.IsSuccessStatusCode).IsTrue()
            .Because(await resources.Content.ReadAsStringAsync());

        // and the refusal the record exists to make impossible
        var without = await client.PostAsJsonAsync($"/api/map/{slug}/wool-sources", new { });
        await Assert.That((int)without.StatusCode).IsEqualTo(400);
    }

    private static async Task<string> OriginateAsync(HttpClient client)
    {
        var made = await client.PostAsJsonAsync("/api/sketch", new SketchOriginateRequest(Name: "Weirgate"));
        return (await made.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("slug").GetString()!;
    }

    private static async Task<HttpClient> FreshAsync()
    {
        await ApiTestFactory.ResetSchemaAsync();
        return ApiTestFactory.Shared.CreateClient();
    }
}
