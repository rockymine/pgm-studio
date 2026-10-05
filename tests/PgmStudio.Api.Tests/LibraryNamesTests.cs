using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace PgmStudio.Api.Tests;

/// <summary>
/// <see cref="Services.LibraryNames"/> through <c>PUT /map/{slug}/source</c> — a refinement naming a library row
/// instead of copying it. What is asserted is that the board holds a copy of the row with what was stated beside
/// it laid over, that the refinement the map keeps records the row and what was copied, that a name naming no
/// single row refuses the source, and that the map's state names a row that has moved on since.
///
/// <para>Runs against the <c>pgm_studio_test</c> schema, so it runs serially with the other DB suites.</para>
/// </summary>
[NotInParallel("api-db")]
public sealed class LibraryNamesTests
{
    private const string Layout = """
        {"setup":{"mirror_mode":"rot_180","center":{"cx":0,"cz":0}},
         "layers":[{"id":"ground","base_y":0,"layout":{
           "shapes":[{"id":"s1","type":"rectangle","operation":"add",
                      "min_x":-20,"max_x":20,"min_z":-20,"max_z":20,"floor":8,"base_height":12}],
           "groups":[{"id":"i","name":"I","shapeIds":["s1"]}]}}]}
        """;

    private const string Source = "/api/map/weirgate/source";

    private static object Body(object refinement) => new
    {
        layout = JsonDocument.Parse(Layout).RootElement,
        intent = JsonDocument.Parse("""{"meta":{"name":"Weirgate","authors":[],"contributors":[]}}""").RootElement,
        refinement,
    };

    [Test]
    public async Task A_theme_named_from_the_library_is_copied_into_the_board_and_its_row_recorded()
    {
        using var client = await FreshAsync();
        var (_, dunes) = await LibraryAsync(client);

        var stored = await client.PutAsJsonAsync(Source, Body(new { themes = new { heath = new { library = "weir-dunes" } } }));
        await Assert.That(stored.IsSuccessStatusCode).IsTrue().Because(await stored.Content.ReadAsStringAsync());

        var layout = await client.GetFromJsonAsync<JsonElement>("/api/map/weirgate/sketch");
        await Assert.That(FirstStop(layout.GetProperty("themes").GetProperty("heath").GetProperty("surface")
            .GetProperty("material"))).IsEqualTo(12);
        await Assert.That(layout.GetProperty("themeSources").GetProperty("heath").GetInt64()).IsEqualTo(dunes)
            .Because("the Sketch tool says which row a copied theme came from");
        var kept = (await client.GetFromJsonAsync<JsonElement>("/api/map/weirgate/refinement"))
            .GetProperty("themes").GetProperty("heath");
        await Assert.That(kept.GetProperty("library").GetString()).IsEqualTo("weir-dunes")
            .Because("the map keeps the name, not the copy");
        await Assert.That(kept.GetProperty("row").GetInt64()).IsEqualTo(dunes);
        await Assert.That(kept.GetProperty("hash").GetString()).IsNotNull();
    }

    [Test]
    public async Task What_is_stated_beside_a_name_is_laid_over_the_copy_and_a_material_inside_is_named_too()
    {
        using var client = await FreshAsync();
        await LibraryAsync(client);

        var stored = await client.PutAsJsonAsync(Source, Body(new
        {
            themes = new { heath = new { library = "weir-dunes", rimEdges = "boundary", wall = new { library = "weir-sand" } } },
        }));
        await Assert.That(stored.IsSuccessStatusCode).IsTrue().Because(await stored.Content.ReadAsStringAsync());

        var heath = (await client.GetFromJsonAsync<JsonElement>("/api/map/weirgate/sketch"))
            .GetProperty("themes").GetProperty("heath");
        await Assert.That(heath.GetProperty("rimEdges").GetString()).IsEqualTo("boundary");
        await Assert.That(FirstStop(heath.GetProperty("wall"))).IsEqualTo(12)
            .Because("a name inside a theme stands for a material");
        await Assert.That(FirstStop(heath.GetProperty("surface").GetProperty("material"))).IsEqualTo(12)
            .Because("what was not stated beside the name is the row's");
    }

    [Test]
    public async Task A_name_naming_no_row_refuses_the_source_and_a_name_is_read_without_case_or_by_id()
    {
        using var client = await FreshAsync();
        var (_, dunes) = await LibraryAsync(client);

        var none = await client.PutAsJsonAsync(Source, Body(new { themes = new { heath = new { library = "weir-dunez" } } }));
        var text = await none.Content.ReadAsStringAsync();
        await Assert.That((int)none.StatusCode).IsEqualTo(422).Because(text);
        var finding = JsonDocument.Parse(text).RootElement.GetProperty("findings")[0];
        await Assert.That(finding.GetProperty("rule").GetString()).IsEqualTo("SR6");
        await Assert.That(finding.GetProperty("message").GetString()).Contains("names library entry 'weir-dunez', which the library does not have");
        // The entry it was nearest to rides as the edit that names it instead.
        var edit = finding.GetProperty("edit");
        await Assert.That(edit.GetProperty("document").GetString()).IsEqualTo("refinement");
        await Assert.That(edit.GetProperty("path").GetString()).IsEqualTo("themes.heath.library");
        await Assert.That(edit.GetProperty("op").GetString()).IsEqualTo("set");
        await Assert.That(edit.GetProperty("value").GetString()).IsEqualTo("weir-dunes");

        foreach (var named in new object[] { "WEIR-DUNES", dunes })
        {
            var stored = await client.PutAsJsonAsync(Source, Body(new { themes = new { heath = new { library = named } } }));
            await Assert.That(stored.IsSuccessStatusCode).IsTrue().Because(await stored.Content.ReadAsStringAsync());
        }
    }

    [Test]
    public async Task The_state_names_a_row_that_has_moved_on_and_the_next_apply_takes_it()
    {
        using var client = await FreshAsync();
        var (sand, dunes) = await LibraryAsync(client);
        var body = Body(new { themes = new { heath = new { library = "weir-dunes" } } });
        await client.PutAsJsonAsync(Source, body);
        await Assert.That((await StateAsync(client)).GetProperty("behind").GetArrayLength()).IsEqualTo(0);

        var edited = await client.PutAsJsonAsync($"/api/styles/{sand}",
            new { name = "weir-sand", kind = "noise", @params = Field(24) });
        await Assert.That(edited.IsSuccessStatusCode).IsTrue().Because(await edited.Content.ReadAsStringAsync());

        var behind = (await StateAsync(client)).GetProperty("behind").EnumerateArray().Single();
        await Assert.That((behind.GetProperty("path").GetString(), behind.GetProperty("kind").GetString()))
            .IsEqualTo(("themes.heath", "theme"));
        await Assert.That(behind.GetProperty("row").GetInt64()).IsEqualTo(dunes);
        var layout = await client.GetFromJsonAsync<JsonElement>("/api/map/weirgate/sketch");
        await Assert.That(FirstStop(layout.GetProperty("themes").GetProperty("heath").GetProperty("surface")
            .GetProperty("material"))).IsEqualTo(12).Because("a library edit never rebuilds a stored board");

        var again = await client.PutAsJsonAsync(Source, body);
        await Assert.That(again.IsSuccessStatusCode).IsTrue().Because(await again.Content.ReadAsStringAsync());
        var edits = (await again.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("edits").EnumerateArray()
            .Select(edit => (edit.GetProperty("document").GetString(), edit.GetProperty("path").GetString())).ToList();
        await Assert.That(edits).Contains(("refinement", "themes.heath.hash"));
        await Assert.That((await StateAsync(client)).GetProperty("behind").GetArrayLength()).IsEqualTo(0);
    }

    /// <summary>A house prop states its style in place, and a name there is a room style: the prop holds the shell
    /// the row composes to, with what is stated beside the name laid over it, as a spawn's room style holds it.
    /// Before the rule, the name was read as a material and the source refused <c>SR6</c>.</summary>
    [Test]
    public async Task A_house_props_own_style_named_from_the_library_is_the_shell_the_row_composes_to()
    {
        using var client = await FreshAsync();
        using (var scope = ApiTestFactory.Shared.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<Services.LibrarySeed>().SeedAsync();

        var stored = await client.PutAsJsonAsync(Source, Body(JsonDocument.Parse("""
            {"roomStyles": {"spawn": {"library": "brick-roofed-stone-and-dark-oak-house"}},
             "dressing": {"props": [{"id": "store", "kind": "house", "seed": 1, "wings": [{"corners": [[2, 2], [9, 8]]}],
                                     "style": {"library": "brick-roofed-stone-and-dark-oak-house", "doorway": {"height": 4}}}]}}
            """).RootElement));
        await Assert.That(stored.IsSuccessStatusCode).IsTrue().Because(await stored.Content.ReadAsStringAsync());

        var layout = await client.GetFromJsonAsync<JsonElement>("/api/map/weirgate/sketch");
        var spawn = layout.GetProperty("roomStyles").GetProperty("spawn");
        var prop = layout.GetProperty("dressing").GetProperty("props")[0].GetProperty("style");
        await Assert.That(prop.TryGetProperty("library", out _)).IsFalse().Because("the prop holds the copy, not the name");
        await Assert.That(prop.GetProperty("roof").GetRawText()).IsEqualTo(spawn.GetProperty("roof").GetRawText());
        await Assert.That(prop.GetProperty("doorway").GetProperty("height").GetInt32()).IsEqualTo(4)
            .Because("what is stated beside the name is laid over the copy");
        var kept = (await client.GetFromJsonAsync<JsonElement>("/api/map/weirgate/refinement"))
            .GetProperty("dressing").GetProperty("props")[0].GetProperty("style");
        await Assert.That(kept.GetProperty("library").GetString()).IsEqualTo("brick-roofed-stone-and-dark-oak-house");
        await Assert.That(kept.GetProperty("row").GetInt64()).IsGreaterThan(0);
    }

    /// <summary>A pattern, <c>weir-sand</c> — sand mottled with red sand — and a theme, <c>weir-dunes</c>, surfacing
    /// with it. The studio seeds a library of its own, so the names are ones it does not seed.</summary>
    private static async Task<(long Sand, long Dunes)> LibraryAsync(HttpClient client)
    {
        var sand = await client.PostAsJsonAsync("/api/styles",
            new { name = "weir-sand", kind = "noise", @params = Field(12) });
        await Assert.That(sand.IsSuccessStatusCode).IsTrue().Because(await sand.Content.ReadAsStringAsync());
        var sandId = (await sand.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();
        return (sandId, await ThemeAsync(client, "weir-dunes", sandId));
    }

    private static async Task<long> ThemeAsync(HttpClient client, string name, long surface)
    {
        var theme = await client.PostAsJsonAsync("/api/themes", new
        {
            name, bedrockRelative = false, bedrockValue = 0, rimEdges = "drop", wallOnTerrainFaces = false,
            buckets = new[] { new { bucket = "surface", styleId = surface, depth = 1, enabled = true } },
        });
        await Assert.That(theme.IsSuccessStatusCode).IsTrue().Because(await theme.Content.ReadAsStringAsync());
        return (await theme.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();
    }

    /// <summary>A field of one block mottled with its own second variant.</summary>
    private static string Field(int block) =>
        $$"""{"kind":"noise","seed":5,"scale":2,"octaves":1,"stops":[{"kind":"solid","id":{{block}},"data":0},{"kind":"solid","id":{{block}},"data":1}],"rise":3}""";

    /// <summary>The block a copied field lays first.</summary>
    private static int FirstStop(JsonElement material) =>
        material.GetProperty("stops")[0].GetProperty("id").GetInt32();

    private static Task<JsonElement> StateAsync(HttpClient client) =>
        client.GetFromJsonAsync<JsonElement>("/api/map/weirgate/state");

    private static async Task<HttpClient> FreshAsync()
    {
        await ApiTestFactory.ResetSchemaAsync();
        return ApiTestFactory.Shared.CreateClient();
    }
}
