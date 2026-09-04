using System.Text;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PgmStudio.Contracts;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;

namespace PgmStudio.Api.Tests;

/// <summary>
/// <c>POST /map/from-documents</c> — the way back in for a map authored against another studio. It is the
/// only route that turns the three documents back into a map, so what it has to prove is that the map it
/// leaves behind is a whole one: the plan is there to re-plan from, the drawing has been rasterized into
/// geometry, the intent has been projected into the document, and the authors survived that projection.
///
/// <para>The last of those is what makes one request enough: a compiled intent names nobody, so the credits
/// are stated beside the three documents rather than in a second call. That the projection does not clear
/// people it was never given is <c>IntentWriteTests</c>' — here the question is only that a body stating an
/// author leaves a map credited to them.</para>
///
/// <para>Runs against the <c>pgm_studio_test</c> schema, so it runs serially with the other DB suites.</para>
/// </summary>
[NotInParallel("api-db")]
public sealed class MapFromDocumentsTests
{
    private const string Layout = """
        {"setup":{"mirror_mode":"rot_180","center":{"cx":0,"cz":0}},
         "layers":[{"base_y":0,"layout":{
           "shapes":[{"id":"s1","type":"rectangle","operation":"add",
                      "min_x":-20,"max_x":20,"min_z":-20,"max_z":20,"floor":8,"base_height":12}],
           "groups":[{"id":"i","name":"I","shapeIds":["s1"]}]}}]}
        """;

    private static object Body(object? authors = null, string? name = "Weirgate", string? slug = null) => new
    {
        plan = JsonDocument.Parse("""{"cell":9,"pieces":[]}""").RootElement,
        layout = JsonDocument.Parse(Layout).RootElement,
        intent = JsonDocument.Parse("""{"meta":{"name":"Weirgate","authors":[],"contributors":[]}}""").RootElement,
        name,
        slug,
        authors,
    };

    [Test]
    public async Task The_documents_become_a_map_with_its_geometry_written()
    {
        using var client = await FreshAsync();

        var resp = await client.PostAsJsonAsync("/api/map/from-documents", Body());
        await Assert.That(resp.IsSuccessStatusCode).IsTrue().Because(await resp.Content.ReadAsStringAsync());

        var loaded = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var slug = loaded.GetProperty("slug").GetString()!;
        await Assert.That(slug).IsEqualTo("weirgate");
        await Assert.That(loaded.GetProperty("replaced").GetBoolean()).IsFalse();
        await Assert.That(loaded.GetProperty("cells").GetInt32()).IsGreaterThan(0);
        await Assert.That(loaded.GetProperty("islands").GetInt32()).IsEqualTo(1);

        // The four layers a whole map carries — the plan to re-plan from, the drawing, the world geometry the
        // finish wrote, and the intent. An imported world would have only the third.
        var state = await client.GetFromJsonAsync<MapState>($"/api/map/{slug}/state");
        await Assert.That(state!.Artifacts.Plan).IsTrue();
        await Assert.That(state.Artifacts.Sketch).IsTrue();
        await Assert.That(state.Artifacts.World).IsTrue();
        await Assert.That(state.Artifacts.Intent).IsTrue();

        // And a map holding all four is offered the export, which a map holding none of them is not.
        await Assert.That(state.Moves.Select(move => move.Route)).Contains("GET /api/map/{slug}/export");
    }

    /// <summary>A compiled intent's <c>meta.authors</c> is empty, so the credits cannot come from it. Stated
    /// in the body, the map is credited to them by the same request that loads it.</summary>
    [Test]
    public async Task The_authors_survive_the_intent_projection()
    {
        using var client = await FreshAsync();

        var resp = await client.PostAsJsonAsync("/api/map/from-documents", Body(authors: new object[] { "Opus 5" }));
        await Assert.That(resp.IsSuccessStatusCode).IsTrue().Because(await resp.Content.ReadAsStringAsync());

        var doc = await client.GetFromJsonAsync<JsonElement>("/api/map/weirgate");
        var authors = doc.GetProperty("authors").EnumerateArray().ToList();
        await Assert.That(authors.Count).IsEqualTo(1);
        // A bare string is a pseudonym: PGM takes a person as an account or a name, and this one has no uuid.
        await Assert.That(authors[0].GetProperty("name").GetString()).IsEqualTo("Opus 5");
    }

    /// <summary>The documents name one map, so loading them twice is a reload rather than a second map.</summary>
    [Test]
    public async Task A_second_load_replaces_the_first()
    {
        using var client = await FreshAsync();

        await client.PostAsJsonAsync("/api/map/from-documents", Body(authors: new object[] { "Opus 5" }));
        var again = await client.PostAsJsonAsync("/api/map/from-documents", Body(authors: new object[] { "Fable 5" }));
        await Assert.That(again.IsSuccessStatusCode).IsTrue().Because(await again.Content.ReadAsStringAsync());

        var loaded = await again.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(loaded.GetProperty("slug").GetString()).IsEqualTo("weirgate");
        await Assert.That(loaded.GetProperty("replaced").GetBoolean()).IsTrue();

        // One map, and it carries what the second load stated rather than a merge of the two.
        var maps = await client.GetFromJsonAsync<JsonElement>("/api/maps");
        await Assert.That(maps.EnumerateArray().Count(m => m.GetProperty("slug").GetString() == "weirgate")).IsEqualTo(1);
        var doc = await client.GetFromJsonAsync<JsonElement>("/api/map/weirgate");
        await Assert.That(doc.GetProperty("authors")[0].GetProperty("name").GetString()).IsEqualTo("Fable 5");
    }

    /// <summary>A name is not guessed. The intent's own <c>meta.name</c> answers for it where the body says
    /// nothing, and a body that says neither is refused rather than given a stand-in.</summary>
    [Test]
    public async Task The_name_falls_back_to_the_intent_and_is_refused_where_neither_states_one()
    {
        using var client = await FreshAsync();

        var named = await client.PostAsJsonAsync("/api/map/from-documents", Body(name: null));
        await Assert.That(named.IsSuccessStatusCode).IsTrue().Because(await named.Content.ReadAsStringAsync());
        await Assert.That((await named.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("slug").GetString())
            .IsEqualTo("weirgate");

        var nameless = await client.PostAsJsonAsync("/api/map/from-documents", new
        {
            plan = JsonDocument.Parse("{}").RootElement,
            layout = JsonDocument.Parse(Layout).RootElement,
            intent = JsonDocument.Parse("{}").RootElement,
        });
        await Assert.That(nameless.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    /// <summary>A drawing that rasterizes to nothing is refused, and the half-written map goes with it —
    /// a refusal never leaves a row behind for the next load to trip over.</summary>
    [Test]
    public async Task A_layout_that_draws_nothing_is_refused_and_leaves_no_map()
    {
        using var client = await FreshAsync();

        var resp = await client.PostAsJsonAsync("/api/map/from-documents", new
        {
            plan = JsonDocument.Parse("{}").RootElement,
            layout = JsonDocument.Parse("""{"layers":[{"base_y":0,"layout":{"shapes":[],"groups":[]}}]}""").RootElement,
            intent = JsonDocument.Parse("""{"meta":{"name":"Empty"}}""").RootElement,
        });

        await Assert.That((int)resp.StatusCode).IsEqualTo(422);
        var refusal = await resp.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(refusal.GetProperty("findings")[0].GetProperty("rule").GetString()).IsEqualTo("SK7");

        var maps = await client.GetFromJsonAsync<JsonElement>("/api/maps");
        await Assert.That(maps.EnumerateArray().Any(m => m.GetProperty("slug").GetString() == "empty")).IsFalse();
    }

    /// <summary>Three documents in one body, so a name none of them can keep is named with the member it was
    /// posted under. A single-document write answers <c>RQ3</c> for its own body; this one has to say which of
    /// the three said it, or the complaint cannot be acted on.</summary>
    [Test]
    public async Task Every_document_answers_for_the_fields_it_could_not_keep()
    {
        using var client = await FreshAsync();

        var resp = await client.PostAsJsonAsync("/api/map/from-documents", new
        {
            plan = JsonDocument.Parse("""{"cell":9,"pieces":[],"celll":9}""").RootElement,
            layout = JsonDocument.Parse(Layout.Replace("\"setup\"", "\"setupp\":{},\"setup\"")).RootElement,
            intent = JsonDocument.Parse("""{"meta":{"name":"Weirgate"},"teamz":[]}""").RootElement,
            name = "Weirgate",
        });
        await Assert.That(resp.IsSuccessStatusCode).IsTrue().Because(await resp.Content.ReadAsStringAsync());

        var loaded = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var named = loaded.GetProperty("warnings").EnumerateArray()
            .Where(warning => warning.GetProperty("rule").GetString() == "RQ3")
            .Select(warning => warning.GetProperty("field").GetString())
            .ToList();

        await Assert.That(named).Contains("plan.celll");
        await Assert.That(named).Contains("layout.setupp");
        await Assert.That(named).Contains("intent.teamz");
    }

    /// <summary>The layout and the intent are the load, and both are read as raw JSON — so a body omitting
    /// one arrives as a <c>default(JsonElement)</c> whose every reader throws. Refused as the request's own
    /// fault, naming the document that is missing, rather than answered as the studio's (`RQ2`) which is the
    /// opposite of true.</summary>
    [Test]
    [Arguments("layout", """{"slug":"nodoc","name":"nodoc","intent":{"meta":{"name":"nodoc"}}}""")]
    [Arguments("intent", """{"slug":"nodoc","name":"nodoc","layout":{"layers":[]}}""")]
    public async Task A_load_missing_one_of_its_two_documents_names_it(string field, string body)
    {
        using var client = await FreshAsync();
        var refused = await client.PostAsync("/api/map/from-documents",
            new StringContent(body, Encoding.UTF8, "application/json"));

        await Assert.That(refused.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        var answer = await refused.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(answer.GetProperty("error").GetString()).IsEqualTo("no document given");
        var finding = answer.GetProperty("findings")[0];
        await Assert.That(finding.GetProperty("rule").GetString()).IsEqualTo("RQ1");
        await Assert.That(finding.GetProperty("field").GetString()).IsEqualTo(field);
    }

    private static async Task<HttpClient> FreshAsync()
    {
        await ApiTestFactory.ResetSchemaAsync();
        return ApiTestFactory.Shared.CreateClient();
    }
}
