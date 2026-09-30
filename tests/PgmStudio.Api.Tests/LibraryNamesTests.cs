using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

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
        await Assert.That(layout.GetProperty("themes").GetProperty("heath").GetProperty("surface").GetProperty("material")
            .GetProperty("id").GetInt32()).IsEqualTo(12);
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
        await Assert.That(heath.GetProperty("wall").GetProperty("id").GetInt32()).IsEqualTo(12)
            .Because("a name inside a theme stands for a material");
        await Assert.That(heath.GetProperty("surface").GetProperty("material").GetProperty("id").GetInt32()).IsEqualTo(12)
            .Because("what was not stated beside the name is the row's");
    }

    [Test]
    public async Task A_name_naming_no_row_or_several_refuses_the_source_and_an_id_names_one()
    {
        using var client = await FreshAsync();
        var (sand, dunes) = await LibraryAsync(client);
        await ThemeAsync(client, "weir-dunes", sand);

        var none = await client.PutAsJsonAsync(Source, Body(new { themes = new { heath = new { library = "weir-dunez" } } }));
        var several = await client.PutAsJsonAsync(Source, Body(new { themes = new { heath = new { library = "weir-dunes" } } }));
        foreach (var (refused, says) in new[] { (none, "'weir-dunes'"), (several, $"#{dunes}") })
        {
            var text = await refused.Content.ReadAsStringAsync();
            await Assert.That((int)refused.StatusCode).IsEqualTo(422).Because(text);
            var finding = JsonDocument.Parse(text).RootElement.GetProperty("findings")[0];
            await Assert.That(finding.GetProperty("rule").GetString()).IsEqualTo("SR6");
            await Assert.That(finding.GetProperty("message").GetString()).Contains(says);
        }

        var byId = await client.PutAsJsonAsync(Source, Body(new { themes = new { heath = new { library = dunes } } }));
        await Assert.That(byId.IsSuccessStatusCode).IsTrue().Because(await byId.Content.ReadAsStringAsync());
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
            new { name = "weir-sand", kind = "solid", @params = """{"kind":"solid","id":24}""" });
        await Assert.That(edited.IsSuccessStatusCode).IsTrue().Because(await edited.Content.ReadAsStringAsync());

        var behind = (await StateAsync(client)).GetProperty("behind").EnumerateArray().Single();
        await Assert.That((behind.GetProperty("path").GetString(), behind.GetProperty("kind").GetString()))
            .IsEqualTo(("themes.heath", "theme"));
        await Assert.That(behind.GetProperty("row").GetInt64()).IsEqualTo(dunes);
        var layout = await client.GetFromJsonAsync<JsonElement>("/api/map/weirgate/sketch");
        await Assert.That(layout.GetProperty("themes").GetProperty("heath").GetProperty("surface").GetProperty("material")
            .GetProperty("id").GetInt32()).IsEqualTo(12).Because("a library edit never rebuilds a stored board");

        var again = await client.PutAsJsonAsync(Source, body);
        await Assert.That(again.IsSuccessStatusCode).IsTrue().Because(await again.Content.ReadAsStringAsync());
        var edits = (await again.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("edits").EnumerateArray()
            .Select(edit => (edit.GetProperty("document").GetString(), edit.GetProperty("path").GetString())).ToList();
        await Assert.That(edits).Contains(("refinement", "themes.heath.hash"));
        await Assert.That((await StateAsync(client)).GetProperty("behind").GetArrayLength()).IsEqualTo(0);
    }

    /// <summary>A material, <c>weir-sand</c>, and a theme, <c>weir-dunes</c>, surfacing with it. The studio seeds a
    /// library of its own, so the names are ones it does not seed.</summary>
    private static async Task<(long Sand, long Dunes)> LibraryAsync(HttpClient client)
    {
        var sand = await client.PostAsJsonAsync("/api/styles",
            new { name = "weir-sand", kind = "solid", @params = """{"kind":"solid","id":12}""" });
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

    private static Task<JsonElement> StateAsync(HttpClient client) =>
        client.GetFromJsonAsync<JsonElement>("/api/map/weirgate/state");

    private static async Task<HttpClient> FreshAsync()
    {
        await ApiTestFactory.ResetSchemaAsync();
        return ApiTestFactory.Shared.CreateClient();
    }
}
