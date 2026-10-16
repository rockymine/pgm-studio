using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace PgmStudio.Api.Tests;

/// <summary>
/// <c>GET</c> and <c>PUT /map/{slug}/places</c>: the map's places stored as they were sent, each write a change in
/// the map's history naming the places document, and a document that cannot be drawn or names a piece the map
/// lacks refused before anything is stored. Runs against the <c>pgm_studio_test</c> schema, so it runs serially
/// with the other DB suites.
/// </summary>
[NotInParallel("api-db")]
public sealed class PlacesEndpointsTests
{
    private const string Board = """
        {"meta":{"name":"Weirgate"},
         "teams":[{"id":"red","name":"Red","color":"red","size":8}],
         "spawns":[{"team":"red","point":{"x":5,"y":12,"z":5},
                    "protection":[{"minX":0,"minZ":0,"maxX":10,"maxZ":10}]}]}
        """;

    private const string Places = """
        {"places":[{"id":"home","name":"Home","shape":"box","points":[[0,0],[10,10]],"holds":"spawn-red"},
                   {"id":"track","name":"Hill track","shape":"line","points":[[5,10],[5,40],[20,60]]}]}
        """;

    private static async Task<(HttpClient Client, string Slug)> BoardAsync()
    {
        await ApiTestFactory.ResetSchemaAsync();
        var client = ApiTestFactory.Shared.CreateClient();
        var create = await client.PostAsJsonAsync("/api/sketch", new { name = "Weirgate" });
        var slug = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("slug").GetString()!;
        var stored = await client.PutAsync($"/api/map/{slug}/intent", new StringContent(Board, Encoding.UTF8, "application/json"));
        await Assert.That(stored.IsSuccessStatusCode).IsTrue().Because(await stored.Content.ReadAsStringAsync());
        return (client, slug);
    }

    private static Task<HttpResponseMessage> PutAsync(HttpClient client, string slug, string json, string query = "") =>
        client.PutAsync($"/api/map/{slug}/places{query}", new StringContent(json, Encoding.UTF8, "application/json"));

    [Test]
    public async Task Places_are_stored_and_read_back_as_a_change_in_the_history()
    {
        var (client, slug) = await BoardAsync();
        var empty = await client.GetFromJsonAsync<JsonElement>($"/api/map/{slug}/places");
        await Assert.That(empty.EnumerateObject().Any()).IsFalse();

        var put = await PutAsync(client, slug, Places, "?note=the%20first%20plan");
        await Assert.That(put.StatusCode).IsEqualTo(HttpStatusCode.OK).Because(await put.Content.ReadAsStringAsync());

        var read = await client.GetFromJsonAsync<JsonElement>($"/api/map/{slug}/places");
        await Assert.That(read.GetProperty("places").GetArrayLength()).IsEqualTo(2);
        await Assert.That(read.GetProperty("places")[0].GetProperty("holds").GetString()).IsEqualTo("spawn-red");

        var changes = await client.GetFromJsonAsync<JsonElement>($"/api/map/{slug}/changes");
        var last = changes.GetProperty("changes").EnumerateArray().Last();
        await Assert.That(last.GetProperty("documents").EnumerateArray().Select(word => word.GetString()!))
            .IsEquivalentTo(new[] { "places" });
        await Assert.That(last.GetProperty("note").GetString()).IsEqualTo("the first plan");
    }

    [Test]
    public async Task A_place_that_cannot_be_drawn_or_holds_no_piece_is_refused()
    {
        var (client, slug) = await BoardAsync();
        foreach (var (body, field) in new[]
        {
            ("""{"places":[{"id":"a","name":"A","shape":"ring","points":[[0,0],[4,0]]}]}""", "places[0].points"),
            ("""{"places":[{"id":"a","name":"A","shape":"blob","points":[[0,0]]}]}""", "places[0].shape"),
            ("""{"places":[{"id":"a","name":"A","shape":"point","points":[[0,0]]},{"id":"a","name":"B","shape":"point","points":[[1,1]]}]}""", "places[1].id"),
            ("""{"places":[{"id":"a","name":"A","shape":"point","points":[[0,0]],"holds":"core-blue"}]}""", "places[0].holds"),
            ("""{"places":[],"verdict":"maybe"}""", "verdict"),
        })
        {
            var put = await PutAsync(client, slug, body);
            var text = await put.Content.ReadAsStringAsync();
            await Assert.That(put.StatusCode).IsEqualTo(HttpStatusCode.BadRequest).Because(text);
            await Assert.That(text).Contains(field);
        }
        var read = await client.GetFromJsonAsync<JsonElement>($"/api/map/{slug}/places");
        await Assert.That(read.EnumerateObject().Any()).IsFalse().Because("a refused write stores nothing");
    }

    [Test]
    public async Task A_note_pins_to_a_place_and_a_note_on_its_ground_is_tagged_with_it()
    {
        var (client, slug) = await BoardAsync();
        var put = await PutAsync(client, slug, Places);
        await Assert.That(put.IsSuccessStatusCode).IsTrue().Because(await put.Content.ReadAsStringAsync());

        var pinned = await client.PostAsJsonAsync($"/api/map/{slug}/notes",
            new { body = "Is the hill tall enough?", anchor = new { kind = "place", place = "home" } });
        await Assert.That(pinned.StatusCode).IsEqualTo(HttpStatusCode.OK).Because(await pinned.Content.ReadAsStringAsync());

        var nowhere = await client.PostAsJsonAsync($"/api/map/{slug}/notes",
            new { body = "Here?", anchor = new { kind = "place", place = "the-abbey" } });
        await Assert.That(nowhere.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);

        var camera = new { x = 5, y = 30, z = 5, yaw = 0, pitch = 60, fov = 70 };
        var onGround = await client.PostAsJsonAsync($"/api/map/{slug}/notes", new
        {
            body = "This block.",
            anchor = new
            {
                kind = "point", viewId = "above", viewName = "Above", camera, width = 640, height = 360,
                marks = new[] { new { x = 320, y = 180 } }, ground = new { x = 4, y = 12, z = 6 },
            },
        });
        var written = await onGround.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(onGround.StatusCode).IsEqualTo(HttpStatusCode.OK).Because(written.ToString());
        await Assert.That(written.GetProperty("anchor").GetProperty("place").GetString()).IsEqualTo("home")
            .Because("the block it names is inside the box the place is drawn as");
    }
}
