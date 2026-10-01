using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;

namespace PgmStudio.Api.Tests;

/// <summary>
/// Every write to a map's plan, sketch layout and intent is a change the map keeps: numbered per slug and never
/// repeating, stamped with its writer, its origin and its note, and holding the documents it wrote. What is
/// asserted is the invariant a reader leans on — numbers only count up, a document's revision is the number of
/// the change that last wrote it, and the documents a change names read back as they were written.
///
/// <para>Runs against the <c>pgm_studio_test</c> schema, so it runs serially with the other DB suites.</para>
/// </summary>
[NotInParallel("api-db")]
public sealed class MapChangeLogTests
{
    private const string Layout = """
        {"setup":{"mirror_mode":"rot_180","center":{"cx":0,"cz":0}},
         "layers":[{"base_y":0,"layout":{
           "shapes":[{"id":"s1","type":"rectangle","operation":"add",
                      "min_x":-20,"max_x":20,"min_z":-20,"max_z":20,"floor":8,"base_height":12}],
           "groups":[{"id":"i","name":"I","shapeIds":["s1"]}]}}]}
        """;

    private static readonly string Smaller = Layout.Replace("\"min_x\":-20,\"max_x\":20", "\"min_x\":-10,\"max_x\":10");

    private static object Body(object? origin = null, string? note = null) => new
    {
        plan = JsonDocument.Parse("""{"cell":9,"pieces":[]}""").RootElement,
        layout = JsonDocument.Parse(Layout).RootElement,
        intent = JsonDocument.Parse("""{"meta":{"name":"Weirgate","authors":[],"contributors":[]}}""").RootElement,
        name = "Weirgate",
        origin,
        note,
    };

    [Test]
    public async Task Every_write_to_a_document_is_a_change_and_its_revision_is_the_change_number()
    {
        using var client = await FreshAsync();
        await LoadAsync(client, Body());
        using var smaller = await client.PutAsync("/api/map/weirgate/sketch", Json(Smaller));
        using var again = await client.PutAsync("/api/map/weirgate/sketch", Json(Layout));
        await Assert.That(again.IsSuccessStatusCode).IsTrue();

        var changes = await ChangesAsync();
        await Assert.That(changes.Count).IsEqualTo(3);
        await Assert.That(changes.Zip(changes.Skip(1)).All(pair => pair.Second.Number > pair.First.Number)).IsTrue();
        await Assert.That(changes[^1].Kinds).IsEquivalentTo(["sketch_layout_json"]);
        await Assert.That(Etag(again)).IsEqualTo($"\"{changes[^1].Number}\"")
            .Because("a kept document's revision is the number of the change that last wrote it");

        var then = await DocumentsAtAsync(changes[1].Number);
        await Assert.That(MaxX(then["sketch_layout_json"])).IsEqualTo(10d);
        await Assert.That(then.Keys).IsEquivalentTo(["plan_json", "refinement_json", "sketch_layout_json", "map_intent_json"])
            .Because("the documents at a change are what the latest change at or before it wrote, per kind");
    }

    [Test]
    public async Task A_load_lands_as_one_change_carrying_its_origin_and_note()
    {
        using var client = await FreshAsync();
        await LoadAsync(client, Body(
            origin: new { repo = "rockymine/pgm-studio-mapgen", commit = "5daa56f", path = "specs/weirgate", dirty = false },
            note: "pass 5, notes 72–74"));

        var change = (await ChangesAsync()).Single();
        await Assert.That(change.Kinds).IsEquivalentTo(["map_intent_json", "plan_json", "refinement_json", "sketch_layout_json"]);
        await Assert.That(change.Note).IsEqualTo("pass 5, notes 72–74");
        var origin = JsonDocument.Parse(change.OriginJson!).RootElement;
        await Assert.That(origin.GetProperty("commit").GetString()).IsEqualTo("5daa56f");
        await Assert.That(origin.GetProperty("path").GetString()).IsEqualTo("specs/weirgate");
    }

    /// <summary>A reload replaces the map's row, and the history is the slug's, so it carries on: the board the
    /// first load stored is still there to read at its number.</summary>
    [Test]
    public async Task A_reload_carries_the_boards_history_on()
    {
        using var client = await FreshAsync();
        await LoadAsync(client, Body());
        var first = (await ChangesAsync()).Single().Number;
        await client.PutAsync("/api/map/weirgate/sketch", Json(Smaller));
        await LoadAsync(client, Body());

        var changes = await ChangesAsync();
        await Assert.That(changes.Count).IsEqualTo(3);
        await Assert.That(changes[^1].Number).IsGreaterThan(changes[^2].Number);
        await Assert.That(MaxX((await DocumentsAtAsync(first))["sketch_layout_json"])).IsEqualTo(20d);
        await Assert.That(MaxX((await DocumentsAtAsync(changes[1].Number))["sketch_layout_json"])).IsEqualTo(10d);
    }

    [Test]
    public async Task A_stale_write_opens_no_change()
    {
        using var client = await FreshAsync();
        await LoadAsync(client, Body());
        var held = Etag(await client.GetAsync("/api/map/weirgate/sketch"))!;
        await client.PutAsync("/api/map/weirgate/sketch", Json(Smaller));
        var before = (await ChangesAsync()).Count;

        using var stale = new HttpRequestMessage(HttpMethod.Put, "/api/map/weirgate/sketch") { Content = Json(Layout) };
        stale.Headers.TryAddWithoutValidation("If-Match", held);
        using var refused = await client.SendAsync(stale);

        await Assert.That(refused.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
        await Assert.That((await ChangesAsync()).Count).IsEqualTo(before);
    }

    /// <summary>A deleted map's history goes with it, and the slug's last number does not: a map made again under
    /// the slug numbers on, so a revision a reader held of the deleted one names nothing in the new one.</summary>
    [Test]
    public async Task Deleting_a_map_forgets_its_history_and_a_map_made_again_numbers_on()
    {
        using var client = await FreshAsync();
        await LoadAsync(client, Body());
        await client.PutAsync("/api/map/weirgate/sketch", Json(Smaller));
        var last = (await ChangesAsync())[^1].Number;

        using var deleted = await client.DeleteAsync("/api/map/weirgate");
        await Assert.That(deleted.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        await Assert.That(await ChangesAsync()).IsEmpty();

        await LoadAsync(client, Body());
        await Assert.That((await ChangesAsync()).Single().Number).IsGreaterThan(last);
    }

    [Test]
    public async Task A_note_longer_than_a_change_keeps_is_refused_before_anything_is_stored()
    {
        using var client = await FreshAsync();
        using var refused = await client.PutAsJsonAsync("/api/map/weirgate/source", Body(note: new string('x', 1001)));

        await Assert.That(refused.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        var finding = (await refused.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("findings")[0];
        await Assert.That(finding.GetProperty("field").GetString()).IsEqualTo("note");
        await Assert.That(await ChangesAsync()).IsEmpty();
    }

    private static async Task LoadAsync(HttpClient client, object body)
    {
        using var loaded = await client.PutAsJsonAsync("/api/map/weirgate/source", body);
        await Assert.That(loaded.IsSuccessStatusCode).IsTrue().Because(await loaded.Content.ReadAsStringAsync());
    }

    private static async Task<IReadOnlyList<MapChange>> ChangesAsync()
    {
        await using var db = new PgmDb(PgmDataOptions.ForConnectionString(ApiTestFactory.ConnectionString));
        return await new MapChangeLog(db).ListAsync("weirgate");
    }

    private static async Task<IReadOnlyDictionary<string, byte[]>> DocumentsAtAsync(long number)
    {
        await using var db = new PgmDb(PgmDataOptions.ForConnectionString(ApiTestFactory.ConnectionString));
        return await new MapChangeLog(db).DocumentsAtAsync("weirgate", number);
    }

    private static double MaxX(byte[] layout) =>
        JsonDocument.Parse(layout).RootElement.GetProperty("layers")[0].GetProperty("layout")
            .GetProperty("shapes")[0].GetProperty("max_x").GetDouble();

    private static async Task<HttpClient> FreshAsync()
    {
        await ApiTestFactory.ResetSchemaAsync();
        return ApiTestFactory.Shared.CreateClient();
    }

    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");

    private static string? Etag(HttpResponseMessage response) =>
        response.Headers.TryGetValues("ETag", out var values) ? values.FirstOrDefault() : null;
}
