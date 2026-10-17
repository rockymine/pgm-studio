using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using LinqToDB;
using LinqToDB.Async;
using Microsoft.Extensions.DependencyInjection;
using PgmStudio.Data.Schema;

namespace PgmStudio.Api.Tests;

/// <summary>
/// <c>PUT /map/{slug}/intent</c> and <c>…/intent/from-plan</c> — what storing an intent may and may not
/// overwrite in the map it projects into.
///
/// <para>The intent owns the map's structure: its teams, spawns, wools and build zones are replaced from it,
/// which is what makes a deletion in Configure stick. It does <b>not</b> own who wrote the map — that is
/// stated through <c>PATCH …/metadata</c> and lives in the map's own rows — so a projection that clears the
/// credits is overwriting an answer it was never given. The rebuild drawer promises exactly that in so many
/// words: <i>Keeps your terrain themes, room shells and placed dressing, and the map's authors.</i></para>
///
/// <para>Runs against the <c>pgm_studio_test</c> schema, so it runs serially with the other DB suites.</para>
/// </summary>
[NotInParallel("api-db")]
public sealed class IntentWriteTests
{
    /// <summary>A compiled intent as the compiler writes one: a <c>meta</c> naming the map, and no people in
    /// it. Both lists are present and empty, which is the shape that matters — an intent with no <c>meta</c>
    /// at all was already left alone.</summary>
    private const string Compiled = """{"meta":{"name":"Weirgate","authors":[],"contributors":[]}}""";

    private static async Task<(HttpClient Client, string Slug)> MapWithAnAuthorAsync()
    {
        await ApiTestFactory.ResetSchemaAsync();
        var client = ApiTestFactory.Shared.CreateClient();

        var create = await client.PostAsJsonAsync("/api/sketch", new { name = "Weirgate" });
        await Assert.That(create.IsSuccessStatusCode).IsTrue().Because(await create.Content.ReadAsStringAsync());
        var slug = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("slug").GetString()!;

        var stated = await client.PatchAsJsonAsync($"/api/map/{slug}/metadata", new
        {
            authors = new object[] { new { name = "Opus 5" } },
        });
        await Assert.That(stated.IsSuccessStatusCode).IsTrue().Because(await stated.Content.ReadAsStringAsync());
        await Assert.That(await AuthorsOfAsync(client, slug)).IsEquivalentTo(new[] { "Opus 5" });

        return (client, slug);
    }

    private static async Task<List<string>> AuthorsOfAsync(HttpClient client, string slug)
    {
        var doc = await client.GetFromJsonAsync<JsonElement>($"/api/map/{slug}");
        return [.. doc.GetProperty("authors").EnumerateArray()
            .Select(author => author.GetProperty("name").GetString() ?? "")];
    }

    private static Task<HttpResponseMessage> PutIntentAsync(HttpClient client, string route, string intent) =>
        client.PutAsync(route, new StringContent(intent, Encoding.UTF8, "application/json"));

    /// <summary>
    /// <b>The rebuild keeps the credits.</b> A plan rebuild replaces everything the plan states and the
    /// authors are not among them, so an intent that names nobody leaves the map's people alone. Were it
    /// not so, the loop a driver actually runs — state the metadata, then rebuild — would answer 200 twice and lose
    /// the author on the second call, with nothing in either body saying so.
    /// </summary>
    [Test]
    public async Task A_rebuild_from_the_plan_keeps_the_authors_it_does_not_state()
    {
        var (client, slug) = await MapWithAnAuthorAsync();
        using var _ = client;

        var rebuilt = await PutIntentAsync(client, $"/api/map/{slug}/intent/from-plan", Compiled);
        await Assert.That(rebuilt.IsSuccessStatusCode).IsTrue().Because(await rebuilt.Content.ReadAsStringAsync());

        await Assert.That(await AuthorsOfAsync(client, slug)).IsEquivalentTo(new[] { "Opus 5" });
    }

    /// <summary>The plain write is the same rule: Configure edits the teams and the spawns, and says nothing
    /// about the people, so it may not clear them either — and the intent it stores names them too, since the
    /// export reads the intent and would otherwise say the map is by nobody.</summary>
    [Test]
    public async Task Storing_an_intent_that_names_nobody_keeps_the_authors()
    {
        var (client, slug) = await MapWithAnAuthorAsync();
        using var _ = client;

        var stored = await PutIntentAsync(client, $"/api/map/{slug}/intent", Compiled);
        await Assert.That(stored.IsSuccessStatusCode).IsTrue().Because(await stored.Content.ReadAsStringAsync());

        await Assert.That(await AuthorsOfAsync(client, slug)).IsEquivalentTo(new[] { "Opus 5" });
        var intent = await client.GetFromJsonAsync<JsonElement>($"/api/map/{slug}/intent");
        await Assert.That(intent.GetProperty("meta").GetProperty("authors").EnumerateArray()
            .Select(author => author.GetProperty("name").GetString() ?? "").ToList())
            .IsEquivalentTo(new[] { "Opus 5" });
    }

    /// <summary>And an intent that <em>does</em> name people still states them — the guard is over a document
    /// that says nothing, not over the field itself, so nothing that could be set can stop being settable.
    /// </summary>
    [Test]
    public async Task An_intent_that_names_people_states_them()
    {
        var (client, slug) = await MapWithAnAuthorAsync();
        using var _ = client;

        var stored = await PutIntentAsync(client, $"/api/map/{slug}/intent",
            """{"meta":{"name":"Weirgate","authors":["Fable 5"],"contributors":[]}}""");
        await Assert.That(stored.IsSuccessStatusCode).IsTrue().Because(await stored.Content.ReadAsStringAsync());

        await Assert.That(await AuthorsOfAsync(client, slug)).IsEquivalentTo(new[] { "Fable 5" });
    }

    /// <summary><b>A person the intent names under a string nobody could be called is refused, not dropped.</b>
    /// A name no Minecraft account carries is a pseudonym and stores; a string that is not a name at all
    /// cannot, and the answer says which person it meant. A row that vanished from a 200 is what makes an
    /// author believe somebody was credited.</summary>
    [Test]
    [Arguments("<script>alert(1)</script>", "meta.authors[0].name")]
    [Arguments("two  spaces", "meta.authors[0].name")]
    [Arguments("a whole paragraph of prose pasted into the name field by accident", "meta.authors[0].name")]
    public async Task A_person_named_something_that_is_not_a_name_refuses_the_write(string stated, string field)
    {
        var (client, slug) = await MapWithAnAuthorAsync();
        using var _ = client;

        var body = JsonSerializer.Serialize(new
        {
            meta = new { name = "Weirgate", authors = new object[] { new { name = stated } } },
        });
        var stored = await PutIntentAsync(client, $"/api/map/{slug}/intent", body);

        await Assert.That((int)stored.StatusCode).IsEqualTo(400);
        var refusal = await stored.Content.ReadFromJsonAsync<JsonElement>();
        var finding = refusal.GetProperty("findings")[0];
        await Assert.That(finding.GetProperty("rule").GetString()).IsEqualTo("RQ1");
        await Assert.That(finding.GetProperty("field").GetString()).IsEqualTo(field);

        await Assert.That(await AuthorsOfAsync(client, slug)).IsEquivalentTo(new[] { "Opus 5" })
            .Because("a refused write changed nothing");
    }

    /// <summary>The pseudonym itself is the other half, at this tier: a name Mojang cannot answer for is
    /// stored as the person it names, with no account behind it and no complaint about that.</summary>
    [Test]
    public async Task A_pseudonym_reaches_the_map_with_no_account_behind_it()
    {
        var (client, slug) = await MapWithAnAuthorAsync();
        using var _ = client;

        var stored = await PutIntentAsync(client, $"/api/map/{slug}/intent",
            """{"meta":{"name":"Weirgate","authors":[{"name":"Haiku 4.5"},{"name":"O'Brien"}]}}""");
        await Assert.That(stored.IsSuccessStatusCode).IsTrue().Because(await stored.Content.ReadAsStringAsync());

        await Assert.That(await AuthorsOfAsync(client, slug)).IsEquivalentTo(new[] { "Haiku 4.5", "O'Brien" });
    }

    /// <summary>A room's building is its house, so a room stating a <c>footprint</c> is refused by name rather
    /// than stood in the default building, and a house the build cannot raise is refused naming its field.</summary>
    [Test]
    [Arguments("""{"spawns":[{"team":"red","footprint":{"minX":2,"minZ":2,"maxX":8,"maxZ":8}}]}""", "spawns[0].footprint")]
    [Arguments("""{"spawns":[{"team":"red","house":{"wings":[]}}]}""", "spawns[0].house.wings")]
    [Arguments("""{"wools":[{"owner":"red","house":{"wings":[{"corners":[[2,2]]}]}}]}""", "wools[0].house.wings[0]")]
    public async Task A_room_house_the_build_cannot_raise_is_refused_naming_its_field(string body, string field)
    {
        var (client, slug) = await MapWithAnAuthorAsync();
        using var _ = client;

        var stored = await PutIntentAsync(client, $"/api/map/{slug}/intent", body);

        await Assert.That((int)stored.StatusCode).IsEqualTo(400);
        var finding = (await stored.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("findings")[0];
        await Assert.That(finding.GetProperty("rule").GetString()).IsEqualTo("RQ1");
        await Assert.That(finding.GetProperty("field").GetString()).IsEqualTo(field);
    }

    private static string WoolIntent(string monuments) => $$"""
        {"meta":{"name":"Dressed","authors":[],"contributors":[]},
         "gamemodes":["ctw"],
         "teams":[{"id":"red-team","name":"Red","color":"red"},{"id":"blue-team","name":"Blue","color":"blue"}],
         "wools":[{"owner":"blue-team","color":"blue","spawn":{"x":10,"y":21,"z":0},"monuments":[{{monuments}}]}]}
        """;

    [Test]
    public async Task An_intent_the_projection_refuses_is_not_stored()
    {
        // A wool stating two monuments for one team: the projection refuses it, and the map keeps the intent its
        // document was last projected from rather than one it never took.
        using var client = await SketchBoard.FreshAsync();
        var route = $"/api/map/{SketchBoard.Slug}/intent";
        const string one = """{"team":"red-team","location":{"x":-10,"y":21,"z":0}}""";
        const string repeated = one + """,{"team":"red-team","location":{"x":-10,"y":22,"z":0}}""";

        var accepted = await client.PutAsync(route, new StringContent(WoolIntent(one), Encoding.UTF8, "application/json"));
        await Assert.That(accepted.IsSuccessStatusCode).IsTrue().Because(await accepted.Content.ReadAsStringAsync());
        var before = await client.GetStringAsync(route);

        var refused = await client.PutAsync(route, new StringContent(WoolIntent(repeated), Encoding.UTF8, "application/json"));
        var answer = JsonDocument.Parse(await refused.Content.ReadAsStringAsync()).RootElement;

        await Assert.That((int)refused.StatusCode).IsEqualTo(422);
        await Assert.That(answer.GetProperty("findings")[0].GetProperty("rule").GetString())
            .IsEqualTo(PgmStudio.Domain.ObjectiveRules.OneMonumentPerTeam);
        await Assert.That(await client.GetStringAsync(route)).IsEqualTo(before);
    }

    /// <summary>A wool whose monument names its own defender is stored, since Configure passes through such a
    /// state, and the answer says so under <c>OB34</c>; the right teams answer nothing.</summary>
    [Test]
    public async Task A_wool_monument_for_its_own_defender_is_stored_and_complained_of()
    {
        using var client = await SketchBoard.FreshAsync();
        var route = $"/api/map/{SketchBoard.Slug}/intent";

        var wrong = await client.PutAsync(route, new StringContent(
            WoolIntent("""{"team":"blue-team","location":{"x":-10,"y":21,"z":0}}"""), Encoding.UTF8, "application/json"));
        var text = await wrong.Content.ReadAsStringAsync();
        await Assert.That(wrong.IsSuccessStatusCode).IsTrue().Because(text);
        var rules = JsonDocument.Parse(text).RootElement.GetProperty("warnings").EnumerateArray()
            .Select(warning => warning.GetProperty("rule").GetString()).ToList();
        await Assert.That(rules).Contains(PgmStudio.Domain.ObjectiveRules.WoolCapturers);

        var right = await client.PutAsync(route, new StringContent(
            WoolIntent("""{"team":"red-team","location":{"x":-10,"y":21,"z":0}}"""), Encoding.UTF8, "application/json"));
        var rightText = await right.Content.ReadAsStringAsync();
        var rightWarnings = JsonDocument.Parse(rightText).RootElement.TryGetProperty("warnings", out var carried)
            ? carried.EnumerateArray().Select(warning => warning.GetProperty("rule").GetString()).ToList() : [];
        await Assert.That(rightWarnings).DoesNotContain(PgmStudio.Domain.ObjectiveRules.WoolCapturers);
    }

    /// <summary>A map read from a world rather than drawn: no layout, and a scan holding one bedrock column at
    /// (5, 5) from y0 to y9.</summary>
    private static async Task<(HttpClient Client, string Slug)> ScannedMapAsync()
    {
        await ApiTestFactory.ResetSchemaAsync();
        var client = ApiTestFactory.Shared.CreateClient();
        var slug = (await (await client.PostAsJsonAsync("/api/sketch", new { name = "Scanned" }))
            .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("slug").GetString()!;

        using var scope = ApiTestFactory.Shared.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PgmDb>();
        var mapId = await db.Maps.Where(m => m.Slug == slug).Select(m => m.Id).FirstAsync();
        await db.Artifacts.Where(a => a.MapId == mapId && a.Kind == ArtifactKind.SketchLayoutJson).DeleteAsync();
        await db.InsertAsync(new SegmentRow { MapId = mapId, WorldX = 5, WorldZ = 5, WorldYStart = 0, WorldYEnd = 9 });
        return (client, slug);
    }

    [Test]
    [Arguments(9, "is a solid block")]
    [Arguments(30, "touches no block")]
    public async Task A_monument_no_wool_can_be_put_into_is_refused_and_not_stored(int y, string says)
    {
        var (client, slug) = await ScannedMapAsync();
        var route = $"/api/map/{slug}/intent";
        var before = await client.GetStringAsync(route);

        var refused = await PutIntentAsync(client, route,
            WoolIntent($$$"""{"team":"red-team","location":{"x":5,"y":{{{y}}},"z":5}}"""));
        var finding = JsonDocument.Parse(await refused.Content.ReadAsStringAsync()).RootElement
            .GetProperty("findings")[0];

        await Assert.That((int)refused.StatusCode).IsEqualTo(422);
        await Assert.That(finding.GetProperty("rule").GetString())
            .IsEqualTo(PgmStudio.Domain.ObjectiveRules.MonumentNotSeated);
        await Assert.That(finding.GetProperty("message").GetString()).Contains($"(5, {y}, 5) {says}");
        await Assert.That(await client.GetStringAsync(route)).IsEqualTo(before);
    }

    [Test]
    public async Task A_monument_on_its_pedestal_is_stored()
    {
        var (client, slug) = await ScannedMapAsync();

        var stored = await PutIntentAsync(client, $"/api/map/{slug}/intent",
            WoolIntent("""{"team":"red-team","location":{"x":5,"y":10,"z":5}}"""));

        await Assert.That(stored.IsSuccessStatusCode).IsTrue().Because(await stored.Content.ReadAsStringAsync());
    }

    /// <summary>Every play piece an intent stores carries an id, minted where the caller stated none and kept
    /// where it did, so the piece can be addressed by it; two pieces stating one id are refused under
    /// <c>RQ5</c> and nothing is stored.</summary>
    [Test]
    public async Task A_stored_play_piece_carries_an_id_and_two_may_not_share_one()
    {
        var (client, slug) = await MapWithAnAuthorAsync();
        using var _ = client;
        const string Teams = """[{"id":"red-team","name":"Red","color":"red"},{"id":"blue-team","name":"Blue","color":"blue"}]""";

        var stored = await PutIntentAsync(client, $"/api/map/{slug}/intent", $$$"""
            {"teams":{{{Teams}}},
             "spawns":[{"team":"red-team","point":{"x":0,"y":10,"z":-20}},
                       {"id":"blue-home","team":"blue-team","point":{"x":0,"y":10,"z":20}}]}
            """);
        await Assert.That(stored.IsSuccessStatusCode).IsTrue().Because(await stored.Content.ReadAsStringAsync());
        var intent = await client.GetFromJsonAsync<JsonElement>($"/api/map/{slug}/intent");
        await Assert.That(intent.GetProperty("spawns").EnumerateArray().Select(spawn => spawn.GetProperty("id").GetString() ?? ""))
            .IsEquivalentTo(new[] { "spawn-red-team", "blue-home" });

        var twice = await PutIntentAsync(client, $"/api/map/{slug}/intent", $$$"""
            {"teams":{{{Teams}}},
             "spawns":[{"id":"home","team":"red-team","point":{"x":0,"y":10,"z":-20}},
                       {"id":"home","team":"blue-team","point":{"x":0,"y":10,"z":20}}]}
            """);
        await Assert.That((int)twice.StatusCode).IsEqualTo(409);
        await Assert.That(await twice.Content.ReadAsStringAsync()).Contains("RQ5");
    }
}
