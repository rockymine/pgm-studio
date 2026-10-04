using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using PgmStudio.Geom.Render;

namespace PgmStudio.Api.Tests;

/// <summary>
/// <c>render/eye</c> over HTTP. Its drawing is <c>EyeScene</c>'s and tested there; what is new here is
/// the door: without Minecraft's textures the read says why and what to set, and with a jar named it answers
/// a picture and its text twin. The jar is one the test builds — the real one is Mojang's and never in the
/// repository.
/// </summary>
[NotInParallel("api-db")]
public sealed class EyeReadEndpointTests
{
    private const string Board = """
        {"setup":{"mirror_mode":"none","center":{"cx":0,"cz":0}},
         "layers": [{ "id": "ground", "base_y": 0, "layout":{"shapes":[
            {"id":"a","type":"rectangle","operation":"add","min_x":-30,"max_x":30,"min_z":-30,"max_z":30,"base_height":6},
            {"id":"b","type":"rectangle","operation":"add","min_x":-4,"max_x":4,"min_z":-4,"max_z":4,"base_height":10}],
          "groups":[{"id":"i1","name":"Island","mirrors":false,"shapeIds":["a","b"]}]} }]}
        """;

    private static async Task<string> FinishedAsync(HttpClient client)
    {
        var create = await client.PostAsJsonAsync("/api/sketch", new { name = $"WS76 {Guid.NewGuid():N}" });
        var slug = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("slug").GetString()!;
        var put = await client.PutAsync($"/api/map/{slug}/sketch", new StringContent(Board, Encoding.UTF8, "application/json"));
        await Assert.That(put.IsSuccessStatusCode).IsTrue();
        var finish = await client.PostAsync($"/api/map/{slug}/sketch/finish", null);
        await Assert.That(finish.IsSuccessStatusCode).IsTrue();
        return slug;
    }

    [Test]
    public async Task Without_the_textures_the_read_says_what_to_set()
    {
        using var client = ApiTestFactory.Shared.CreateClient();
        var slug = await FinishedAsync(client);

        var resp = await client.GetAsync($"/api/map/{slug}/render/eye?look=0,0");

        await Assert.That(resp.StatusCode).IsEqualTo(HttpStatusCode.ServiceUnavailable);
        var finding = JsonDocument.Parse(await resp.Content.ReadAsStringAsync()).RootElement.GetProperty("findings")[0];
        await Assert.That(finding.GetProperty("rule").GetString()).IsEqualTo("RQ10");
        await Assert.That(finding.GetProperty("message").GetString()).Contains("Textures:AcceptMojangEula");
    }

    [Test]
    public async Task With_a_jar_named_it_answers_the_picture_asked_for_and_what_is_in_it()
    {
        using var client = TexturedFactory.Shared.CreateClient();
        var slug = await FinishedAsync(client);

        var picture = await client.GetAsync($"/api/map/{slug}/render/eye?from=0,20&yaw=180&pitch=20&width=320&height=180");
        await Assert.That(picture.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(picture.Content.Headers.ContentType!.MediaType).IsEqualTo("image/png");
        var png = PngReader.Decode(await picture.Content.ReadAsByteArrayAsync());
        await Assert.That((png.Width, png.Height)).IsEqualTo((320, 180));

        var text = await client.GetStringAsync($"/api/map/{slug}/render/eye?look=0,15&format=text");
        await Assert.That(text).Contains("placed to see 0,15");
        await Assert.That(text).Contains("share   block");
    }

    [Test]
    public async Task A_read_that_names_neither_where_to_stand_nor_what_to_see_is_refused()
    {
        using var client = TexturedFactory.Shared.CreateClient();
        var slug = await FinishedAsync(client);

        var resp = await client.GetAsync($"/api/map/{slug}/render/eye");

        await Assert.That(resp.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task An_area_drawn_past_the_boards_edge_names_the_void_it_covers_apart_from_the_ground()
    {
        using var client = TexturedFactory.Shared.CreateClient();
        var slug = await FinishedAsync(client);

        var area = await client.GetFromJsonAsync<JsonElement>(
            $"/api/map/{slug}/render/eye/pick?from=0,20&yaw=0&pitch=20&width=320&height=180&box=0,0,319,179");

        var ground = area.GetProperty("columns").EnumerateArray().Select(cell => (cell[0].GetInt32(), cell[2].GetInt32())).ToHashSet();
        var overVoid = area.GetProperty("overVoid").EnumerateArray().ToList();
        await Assert.That(ground.Count).IsGreaterThan(0);
        await Assert.That(overVoid.Count).IsGreaterThan(0);
        // The island's rectangle stands on x and z from -30 up to, and not including, 30.
        static bool OnTheIsland(int at) => at >= -30 && at < 30;
        await Assert.That(overVoid.All(cell => !OnTheIsland(cell[0].GetInt32()) || !OnTheIsland(cell[2].GetInt32()))).IsTrue();
        await Assert.That(overVoid.Select(cell => cell[1].GetInt32()).Distinct().Count()).IsEqualTo(1);
        await Assert.That(overVoid.Any(cell => ground.Contains((cell[0].GetInt32(), cell[2].GetInt32())))).IsFalse();
    }

    [Test]
    public async Task A_pick_casts_the_pictures_own_ray_and_names_the_camera_exactly()
    {
        using var client = TexturedFactory.Shared.CreateClient();
        var slug = await FinishedAsync(client);
        const string Picture = "from=0,20&yaw=180&pitch=30&width=320&height=180";

        var point = await client.GetFromJsonAsync<JsonElement>($"/api/map/{slug}/render/eye/pick?{Picture}&at=160,120");
        var hit = point.GetProperty("hit");
        await Assert.That(hit.GetProperty("z").GetInt32()).IsLessThan(20);
        await Assert.That(point.GetProperty("ground").GetProperty("y").GetInt32()).IsLessThanOrEqualTo(hit.GetProperty("y").GetInt32());
        await Assert.That(point.GetProperty("camera").GetProperty("x").GetDouble()).IsEqualTo(0.5);
        await Assert.That(point.GetProperty("standing").GetInt32())
            .IsEqualTo((int)Math.Round(point.GetProperty("camera").GetProperty("y").GetDouble() - 2.62));

        var again = await client.GetAsync($"/api/map/{slug}/render/eye?{point.GetProperty("query").GetString()}");
        await Assert.That(again.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(PngReader.Decode(await again.Content.ReadAsByteArrayAsync()).Width).IsEqualTo(320);

        var area = await client.GetFromJsonAsync<JsonElement>($"/api/map/{slug}/render/eye/pick?{Picture}&lasso=100,100;220,100;160,170");
        await Assert.That(area.GetProperty("columns").GetArrayLength()).IsGreaterThan(0);
        await Assert.That(area.GetProperty("hit").ValueKind).IsEqualTo(JsonValueKind.Null);

        var outside = await client.GetAsync($"/api/map/{slug}/render/eye/pick?{Picture}&at=400,10");
        await Assert.That(outside.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);

        // Every view listed carries the camera it resolves to, so a view that leaves the eye to find its own
        // place can still be drawn where it stands.
        var views = (await client.GetFromJsonAsync<JsonElement>($"/api/map/{slug}/views")).GetProperty("views");
        var straightDown = views.EnumerateArray().First();
        await Assert.That(straightDown.GetProperty("id").GetString()).IsEqualTo("above");
        await Assert.That(straightDown.GetProperty("eye").GetProperty("pitch").GetDouble()).IsEqualTo(90.0);
    }
}
