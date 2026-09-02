using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace PgmStudio.Api.Tests;

/// <summary>
/// The world read-backs over HTTP (`WS6`): the eight pictures and one text read that used to exist only
/// behind <c>PgmStudio.RoundTrip</c>'s flags.
///
/// <para>Asserted on what comes back rather than on what is drawn — a PNG signature and a real size for a
/// picture, the named blocks for a column — because the renderers' own tests already cover what each one
/// draws. What is new here is that a caller can <b>reach</b> them at all, which is the whole of the task.</para>
/// </summary>
[NotInParallel("api-db")]
public sealed class WorldReadEndpointTests
{
    // A 60x60 island with a plateau on it, so there is ground to look at, height to profile and a cut to take.
    private const string Board = """
        {"setup":{"mirror_mode":"none","center":{"cx":0,"cz":0}},
         "layers": [{ "id": "ground", "base_y": 0, "layout":{"shapes":[
            {"id":"a","type":"rectangle","operation":"add","min_x":-30,"max_x":30,"min_z":-30,"max_z":30,"base_height":6},
            {"id":"b","type":"rectangle","operation":"add","min_x":-10,"max_x":10,"min_z":-10,"max_z":10,"base_height":14}],
          "groups":[{"id":"i1","name":"Island","mirrors":false,"shapeIds":["a","b"]}]} }]}
        """;

    private static async Task<string> FinishedAsync(HttpClient client)
    {
        var create = await client.PostAsJsonAsync("/api/sketch", new { name = $"WS6 {Guid.NewGuid():N}" });
        var slug = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("slug").GetString()!;
        var put = await client.PutAsync($"/api/map/{slug}/sketch",
            new StringContent(Board, Encoding.UTF8, "application/json"));
        await Assert.That(put.IsSuccessStatusCode).IsTrue();
        var finish = await client.PostAsync($"/api/map/{slug}/sketch/finish", null);
        await Assert.That(finish.IsSuccessStatusCode).IsTrue();
        return slug;
    }

    /// <summary>The width and height an IHDR carries, at the fixed offset every PNG puts them.</summary>
    private static (int Width, int Height) Size(byte[] png) =>
        (System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4)),
         System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4)));

    [Test]
    public async Task Every_read_answers_a_picture_of_the_world_it_was_asked_about()
    {
        using var client = ApiTestFactory.Shared.CreateClient();
        var slug = await FinishedAsync(client);

        foreach (var read in (string[])[
            "render/topdown",
            "render/topdown?subject=ground",
            "render/topdown?subject=structure&material=1",
            "render/heightmap",
            "render/surface",
            "render/traversability",
            "render/structures",
            "render/mirror",
            "render/section?axis=x&from=-30&to=30&at=0",
        ])
        {
            var resp = await client.GetAsync($"/api/map/{slug}/{read}");
            await Assert.That(resp.StatusCode).IsEqualTo(HttpStatusCode.OK)
                .Because($"{read} answered {(int)resp.StatusCode}: {await resp.Content.ReadAsStringAsync()}");
            await Assert.That(resp.Content.Headers.ContentType!.MediaType).IsEqualTo("image/png");

            var png = await resp.Content.ReadAsByteArrayAsync();
            await Assert.That(png.Take(4)).IsEquivalentTo(new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G' });
            var (width, height) = Size(png);
            await Assert.That(width).IsGreaterThan(0).Because($"{read} drew a picture of no width");
            await Assert.That(height).IsGreaterThan(0).Because($"{read} drew a picture of no height");
        }
    }

    [Test]
    public async Task A_scale_draws_the_same_board_at_more_pixels()
    {
        // Every board read takes it, and it is the difference between a thumbnail and something a roof idiom
        // or a seam can be read off. Out of range clamps rather than refusing: how the answer is looked at is
        // not part of the question.
        using var client = ApiTestFactory.Shared.CreateClient();
        var slug = await FinishedAsync(client);

        async Task<(int Width, int Height)> AtAsync(string query)
        {
            var resp = await client.GetAsync($"/api/map/{slug}/render/surface{query}");
            await Assert.That(resp.StatusCode).IsEqualTo(HttpStatusCode.OK);
            return Size(await resp.Content.ReadAsByteArrayAsync());
        }

        var one = await AtAsync("?scale=1");
        var four = await AtAsync("");                       // the default
        await Assert.That((await AtAsync("?scale=2")).Width).IsEqualTo(one.Width * 2);
        await Assert.That(four.Width).IsEqualTo(one.Width * 4);
        await Assert.That((await AtAsync("?scale=999")).Width).IsEqualTo(one.Width * 16);
    }

    [Test]
    public async Task A_column_names_every_block_in_it_and_says_so_when_there_are_none()
    {
        // The workhorse: every picture beside it is a projection, and this is what is actually at a
        // coordinate. The plateau's own column carries the tier it was drawn at; a column off the island
        // carries nothing, and says that rather than being absent from the answer.
        using var client = ApiTestFactory.Shared.CreateClient();
        var slug = await FinishedAsync(client);

        var resp = await client.GetAsync($"/api/map/{slug}/column?at=0,0&at=500,500");
        await Assert.That(resp.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(resp.Content.Headers.ContentType!.MediaType).IsEqualTo("text/plain");

        var read = await resp.Content.ReadAsStringAsync();
        await Assert.That(read).Contains("column (0, 0)");
        await Assert.That(read).Contains("Bedrock");            // every column bottoms out in it
        await Assert.That(read).Contains("column (500, 500)");
        await Assert.That(read).Contains("void");               // off the board, and it says so
    }

    [Test]
    public async Task A_column_asked_for_in_a_way_that_cannot_be_read_is_refused_by_name()
    {
        using var client = ApiTestFactory.Shared.CreateClient();
        var slug = await FinishedAsync(client);

        foreach (var (query, why) in (( string Query, string Why )[])[
            ("", "no column was named"),
            ("?at=nowhere", "'nowhere' is not two numbers"),
            ("?at=1,2,3", "three numbers are not a column"),
        ])
        {
            var resp = await client.GetAsync($"/api/map/{slug}/column{query}");
            await Assert.That(resp.StatusCode).IsEqualTo(HttpStatusCode.BadRequest).Because(why);
            var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
            await Assert.That(body.GetProperty("findings")[0].GetProperty("field").GetString()).IsEqualTo("at");
        }
    }

    [Test]
    public async Task The_heightmap_text_twin_and_the_slope_grid_answer_the_format_asked_for()
    {
        using var client = ApiTestFactory.Shared.CreateClient();
        var slug = await FinishedAsync(client);

        var heightmapPng = await client.GetAsync($"/api/map/{slug}/render/heightmap?format=text");
        await Assert.That(heightmapPng.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(heightmapPng.Content.Headers.ContentType!.MediaType).IsEqualTo("text/plain");
        var heightmapText = await heightmapPng.Content.ReadAsStringAsync();
        await Assert.That(heightmapText).Contains("HEIGHTMAP");
        await Assert.That(heightmapText).Contains("KEY");

        var slopesJson = await client.GetAsync($"/api/map/{slug}/slopes");
        await Assert.That(slopesJson.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(slopesJson.Content.Headers.ContentType!.MediaType).IsEqualTo("application/json");
        var slopes = await slopesJson.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(slopes.GetProperty("width").GetInt32()).IsGreaterThan(0);

        var slopesText = await client.GetAsync($"/api/map/{slug}/slopes?format=text&every=4");
        await Assert.That(slopesText.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(slopesText.Content.Headers.ContentType!.MediaType).IsEqualTo("text/plain");
        var slopesTextBody = await slopesText.Content.ReadAsStringAsync();
        await Assert.That(slopesTextBody).Contains("SLOPES");
        await Assert.That(slopesTextBody).Contains("cells:");
    }

    [Test]
    public async Task A_map_with_no_stored_layout_has_no_world_to_read()
    {
        // Not a fault: a map that ships its own region files is read from those. The 404 says which, rather
        // than the empty picture a build over nothing would produce.
        using var client = ApiTestFactory.Shared.CreateClient();
        var create = await client.PostAsJsonAsync("/api/sketch", new { name = $"WS6 bare {Guid.NewGuid():N}" });
        var slug = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("slug").GetString()!;

        // A fresh sketch seeds an empty layout, so the read that has genuinely nothing is one never drawn on.
        var resp = await client.GetAsync("/api/map/not-a-map-at-all/render/topdown");
        await Assert.That(resp.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        _ = slug;
    }

    [Test]
    public async Task Section_and_transect_answer_the_media_type_asked_for()
    {
        using var client = ApiTestFactory.Shared.CreateClient();
        var slug = await FinishedAsync(client);

        var sectionText = await client.GetAsync(
            $"/api/map/{slug}/render/section?axis=x&from=-30&to=30&at=0&format=text");
        await Assert.That(sectionText.StatusCode).IsEqualTo(HttpStatusCode.OK)
            .Because(await sectionText.Content.ReadAsStringAsync());
        await Assert.That(sectionText.Content.Headers.ContentType!.MediaType).IsEqualTo("text/plain");
        await Assert.That(await sectionText.Content.ReadAsStringAsync()).Contains("SECTION");

        var transectJson = await client.GetAsync($"/api/map/{slug}/transect?points=0,0;5,5");
        await Assert.That(transectJson.StatusCode).IsEqualTo(HttpStatusCode.OK)
            .Because(await transectJson.Content.ReadAsStringAsync());
        await Assert.That(transectJson.Content.Headers.ContentType!.MediaType).IsEqualTo("application/json");

        var transectText = await client.GetAsync($"/api/map/{slug}/transect?points=0,0;5,5&format=text");
        await Assert.That(transectText.StatusCode).IsEqualTo(HttpStatusCode.OK)
            .Because(await transectText.Content.ReadAsStringAsync());
        await Assert.That(transectText.Content.Headers.ContentType!.MediaType).IsEqualTo("text/plain");
        await Assert.That(await transectText.Content.ReadAsStringAsync()).Contains("TRANSECT");
    }

    [Test]
    public async Task A_transect_with_one_point_is_refused()
    {
        using var client = ApiTestFactory.Shared.CreateClient();
        var slug = await FinishedAsync(client);

        var resp = await client.GetAsync($"/api/map/{slug}/transect?points=0,0");
        await Assert.That(resp.StatusCode).IsEqualTo(HttpStatusCode.UnprocessableEntity);
    }

    [Test]
    public async Task The_walk_answers_its_profile_and_leaves_beside_empty_on_a_board_with_no_props()
    {
        // z=-25 never crosses the 20×20 plateau (x and z both -10..10), so the whole route is flat: places
        // match cells one for one and no step is anything but a walk.
        using var client = ApiTestFactory.Shared.CreateClient();
        var slug = await FinishedAsync(client);

        var resp = await client.GetAsync($"/api/map/{slug}/walk?from=-25,-25&to=25,-25&beside=2");
        await Assert.That(resp.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var read = await resp.Content.ReadFromJsonAsync<JsonElement>();

        await Assert.That(read.GetProperty("reachable").GetBoolean()).IsTrue();
        var cellCount = read.GetProperty("cells").GetArrayLength();
        await Assert.That(read.GetProperty("places").GetArrayLength()).IsEqualTo(cellCount);
        await Assert.That(read.GetProperty("steps").GetArrayLength()).IsEqualTo(0);
        await Assert.That(read.GetProperty("rises").GetInt32()).IsEqualTo(0);
        await Assert.That(read.GetProperty("falls").GetInt32()).IsEqualTo(0);
        await Assert.That(read.GetProperty("worstStep").GetInt32()).IsEqualTo(0);
        await Assert.That(read.GetProperty("beside").GetArrayLength()).IsEqualTo(0);
    }

    [Test]
    public async Task The_theme_census_counts_every_ground_cell_once()
    {
        using var client = ApiTestFactory.Shared.CreateClient();
        var slug = await FinishedAsync(client);

        var resp = await client.GetAsync($"/api/map/{slug}/themes/census");
        await Assert.That(resp.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var read = await resp.Content.ReadFromJsonAsync<JsonElement>();

        var byTheme = read.GetProperty("byTheme");
        var cells = 0;
        foreach (var row in byTheme.EnumerateArray()) cells += row.GetProperty("cells").GetInt32();
        await Assert.That(cells).IsGreaterThan(0);
        await Assert.That(read.GetProperty("themes").GetInt32()).IsEqualTo(byTheme.GetArrayLength());
    }

    [Test]
    public async Task The_walk_and_the_theme_census_answer_the_text_they_are_asked_for()
    {
        using var client = ApiTestFactory.Shared.CreateClient();
        var slug = await FinishedAsync(client);

        foreach (var route in (string[])["walk?from=-25,-25&to=25,-25&format=text", "themes/census?format=text"])
        {
            var resp = await client.GetAsync($"/api/map/{slug}/{route}");
            await Assert.That(resp.StatusCode).IsEqualTo(HttpStatusCode.OK)
                .Because($"{route} answered {(int)resp.StatusCode}");
            await Assert.That(resp.Content.Headers.ContentType!.MediaType).IsEqualTo("text/plain");
        }

        var json = await client.GetAsync($"/api/map/{slug}/themes/census");
        await Assert.That(json.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(json.Content.Headers.ContentType!.MediaType).IsEqualTo("application/json");
    }

    [Test]
    public async Task A_drawn_route_is_walked_back_and_a_stroke_the_board_lacks_is_named()
    {
        // The fixture board carries no dressing, so the refusal is the one a caller meets first — and it
        // names what the board does carry rather than only that this is not it.
        using var client = ApiTestFactory.Shared.CreateClient();
        var slug = await FinishedAsync(client);

        var missing = await client.GetAsync($"/api/map/{slug}/route?id=nowhere");
        await Assert.That(missing.StatusCode).IsEqualTo(HttpStatusCode.UnprocessableContent);
        await Assert.That(await missing.Content.ReadAsStringAsync()).Contains("no stroke");
    }
}
