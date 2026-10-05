using System.Text;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PgmStudio.Contracts;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;

namespace PgmStudio.Api.Tests;

/// <summary>
/// <c>PUT /map/{slug}/source</c> — a map stored from its source. What it has to prove is that the map it leaves
/// behind is a whole one: the plan is there to re-plan from, the drawing has been rasterized into geometry, the
/// intent has been projected into the document, and the credits survived that projection; that a plan is
/// compiled and its refinement applied in the same request; and that everything a source can be refused for is
/// decided before the board it would replace is touched.
///
/// <para>Runs against the <c>pgm_studio_test</c> schema, so it runs serially with the other DB suites.</para>
/// </summary>
[NotInParallel("api-db")]
public sealed class MapSourceTests
{
    private const string Layout = """
        {"setup":{"mirror_mode":"rot_180","center":{"cx":0,"cz":0}},
         "layers":[{"base_y":0,"layout":{
           "shapes":[{"id":"s1","type":"rectangle","operation":"add",
                      "min_x":-20,"max_x":20,"min_z":-20,"max_z":20,"floor":8,"base_height":12}],
           "groups":[{"id":"i","name":"I","shapeIds":["s1"]}]}}]}
        """;

    private const string Source = "/api/map/weirgate/source";

    private static object Body(object? authors = null, string? name = "Weirgate") => new
    {
        plan = JsonDocument.Parse("""{"cell":9,"pieces":[]}""").RootElement,
        layout = JsonDocument.Parse(Layout).RootElement,
        intent = JsonDocument.Parse("""{"meta":{"name":"Weirgate","authors":[],"contributors":[]}}""").RootElement,
        refinement = authors is null ? (object?)null : new { authors },
        name,
    };

    [Test]
    public async Task The_documents_become_a_map_with_its_geometry_written()
    {
        using var client = await FreshAsync();

        var resp = await client.PutAsJsonAsync(Source, Body());
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

        var resp = await client.PutAsJsonAsync(Source, Body(authors: new object[] { "Opus 5" }));
        await Assert.That(resp.IsSuccessStatusCode).IsTrue().Because(await resp.Content.ReadAsStringAsync());

        var doc = await client.GetFromJsonAsync<JsonElement>("/api/map/weirgate");
        var authors = doc.GetProperty("authors").EnumerateArray().ToList();
        await Assert.That(authors.Count).IsEqualTo(1);
        // A bare string is a pseudonym: PGM takes a person as an account or a name, and this one has no uuid.
        await Assert.That(authors[0].GetProperty("name").GetString()).IsEqualTo("Opus 5");

        // And into the intent, which is the half the world reads: the observer platform's board is stamped
        // from meta.authors, so a map credited on its rows alone is exported carrying EX6 and no sign.
        var intent = await client.GetFromJsonAsync<JsonElement>("/api/map/weirgate/intent");
        await Assert.That(intent.GetProperty("meta").GetProperty("authors")[0].GetProperty("name").GetString())
            .IsEqualTo("Opus 5");
    }

    /// <summary>The other form a person is stated in. An entry arrives as JSON, so its fields are
    /// <c>JsonElement</c>s rather than strings, and a reader that takes only the latter drops the whole
    /// object form on a 200 — credited nowhere, complained about nowhere.</summary>
    [Test]
    public async Task An_author_stated_as_an_object_is_credited_with_their_contribution()
    {
        using var client = await FreshAsync();

        var stated = new object[]
        {
            new { name = "Opus 5", contribution = "layout" },
            new { name = "Fable 5", role = "contributor", contribution = "relief" },
        };
        var resp = await client.PutAsJsonAsync(Source, Body(authors: stated));
        await Assert.That(resp.IsSuccessStatusCode).IsTrue().Because(await resp.Content.ReadAsStringAsync());

        var doc = await client.GetFromJsonAsync<JsonElement>("/api/map/weirgate");
        var rows = doc.GetProperty("authors").EnumerateArray()
            .ToDictionary(a => a.GetProperty("name").GetString()!, a => a);
        await Assert.That(rows.Count).IsEqualTo(2);
        await Assert.That(rows["Opus 5"].GetProperty("contribution").GetString()).IsEqualTo("layout");
        await Assert.That(rows["Fable 5"].GetProperty("role").GetString()).IsEqualTo("contributor");

        // The role splits the two in the intent, which is the shape the export reads them in.
        var meta = (await client.GetFromJsonAsync<JsonElement>("/api/map/weirgate/intent")).GetProperty("meta");
        await Assert.That(meta.GetProperty("authors")[0].GetProperty("name").GetString()).IsEqualTo("Opus 5");
        await Assert.That(meta.GetProperty("authors")[0].GetProperty("contribution").GetString()).IsEqualTo("layout");
        await Assert.That(meta.GetProperty("contributors")[0].GetProperty("name").GetString()).IsEqualTo("Fable 5");
    }

    /// <summary>The documents name one map, so loading them twice is a reload rather than a second map.</summary>
    [Test]
    public async Task A_second_load_replaces_the_first()
    {
        using var client = await FreshAsync();

        await client.PutAsJsonAsync(Source, Body(authors: new object[] { "Opus 5" }));
        var again = await client.PutAsJsonAsync(Source, Body(authors: new object[] { "Fable 5" }));
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

    /// <summary><b>A reload never answers a revision a read before it answered.</b> A drive makes the same writes
    /// on every run, so a board rebuilt over its own slug and written as many times again would stand at the
    /// revision a tab read before the rebuild, and that tab's save would pass its guard and write the old board
    /// back over the new one.</summary>
    [Test]
    public async Task A_tab_holding_a_revision_from_before_a_reload_cannot_save_over_the_rebuilt_board()
    {
        using var client = await FreshAsync();
        var smaller = Layout.Replace("\"min_x\":-20,\"max_x\":20", "\"min_x\":-10,\"max_x\":10");

        await client.PutAsJsonAsync(Source, Body());
        await client.PutAsync("/api/map/weirgate/sketch", Json(Layout));
        await client.PutAsync("/api/map/weirgate/sketch", Json(Layout));
        var held = Etag(await client.GetAsync("/api/map/weirgate/sketch"));
        await Assert.That(held).IsNotNull();

        // The next drive rebuilds the board and makes the same writes again.
        var reloaded = await client.PutAsJsonAsync(Source, Body());
        await Assert.That(reloaded.IsSuccessStatusCode).IsTrue().Because(await reloaded.Content.ReadAsStringAsync());
        await client.PutAsync("/api/map/weirgate/sketch", Json(smaller));
        await client.PutAsync("/api/map/weirgate/sketch", Json(smaller));

        var stale = new HttpRequestMessage(HttpMethod.Put, "/api/map/weirgate/sketch") { Content = Json(Layout) };
        stale.Headers.TryAddWithoutValidation("If-Match", held);
        var saved = await client.SendAsync(stale);

        await Assert.That(saved.StatusCode).IsEqualTo(HttpStatusCode.Conflict)
            .Because("the tab read the board before the reload, and the revision it holds names that board");
        await Assert.That(await StoredMaxXAsync(client)).IsEqualTo(10d);
    }

    /// <summary><b>A reload the studio refuses leaves the board it would have replaced.</b> Everything a load is
    /// refused for is decided from the documents alone — a drawing with no ground, a person nobody could be
    /// called — so it is decided before the stored map is touched.</summary>
    [Test]
    public async Task A_refused_reload_leaves_the_stored_board_as_it_was()
    {
        using var client = await FreshAsync();
        var first = await client.PutAsJsonAsync(Source, Body());
        await Assert.That(first.IsSuccessStatusCode).IsTrue().Because(await first.Content.ReadAsStringAsync());

        var nothingDrawn = await client.PutAsJsonAsync(Source, new
        {
            plan = JsonDocument.Parse("{}").RootElement,
            layout = JsonDocument.Parse("""{"layers":[{"base_y":0,"layout":{"shapes":[],"groups":[]}}]}""").RootElement,
            intent = JsonDocument.Parse("""{"meta":{"name":"Weirgate"}}""").RootElement,
            name = "Weirgate",
        });
        await Assert.That((int)nothingDrawn.StatusCode).IsEqualTo(422);

        var unnamable = await client.PutAsJsonAsync(Source, new
        {
            plan = JsonDocument.Parse("""{"cell":9,"pieces":[]}""").RootElement,
            layout = JsonDocument.Parse(Layout).RootElement,
            intent = JsonDocument.Parse("""{"meta":{"name":"Weirgate","authors":[{"name":" not<a>name "}]}}""").RootElement,
            name = "Weirgate",
        });
        await Assert.That((int)unnamable.StatusCode).IsEqualTo(400);

        await Assert.That(await StoredMaxXAsync(client)).IsEqualTo(20d)
            .Because("neither refused reload may take the board it would have replaced");
        var state = await client.GetFromJsonAsync<MapState>("/api/map/weirgate/state");
        await Assert.That(state!.Artifacts.World).IsTrue();
        await Assert.That(state.Artifacts.Intent).IsTrue();
    }

    /// <summary>A name is not guessed. The intent's own <c>meta.name</c> answers for it where the body says
    /// nothing, and a body that says neither is refused rather than given a stand-in.</summary>
    [Test]
    public async Task The_name_falls_back_to_the_intent_and_is_refused_where_neither_states_one()
    {
        using var client = await FreshAsync();

        var named = await client.PutAsJsonAsync("/api/map/weir/source", Body(name: null));
        await Assert.That(named.IsSuccessStatusCode).IsTrue().Because(await named.Content.ReadAsStringAsync());
        var maps = await client.GetFromJsonAsync<JsonElement>("/api/maps");
        await Assert.That(maps.EnumerateArray().Single(m => m.GetProperty("slug").GetString() == "weir")
            .GetProperty("name").GetString()).IsEqualTo("Weirgate").Because("the slug is the route's and the name the intent's");

        var nameless = await client.PutAsJsonAsync(Source, new
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

        var resp = await client.PutAsJsonAsync("/api/map/empty/source", new
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

    /// <summary>Four documents in one body, so a name none of them can keep is named with the member it was
    /// posted under. A single-document write answers <c>RQ3</c> for its own body; this one has to say which of
    /// the four said it, or the complaint cannot be acted on — and a member the source itself does not have is
    /// named bare.</summary>
    [Test]
    public async Task Every_document_answers_for_the_fields_it_could_not_keep()
    {
        using var client = await FreshAsync();

        var resp = await client.PutAsJsonAsync(Source, new
        {
            plan = JsonDocument.Parse("""{"cell":9,"pieces":[],"celll":9}""").RootElement,
            layout = JsonDocument.Parse(Layout.Replace("\"setup\"", "\"setupp\":{},\"setup\"")).RootElement,
            intent = JsonDocument.Parse("""{"meta":{"name":"Weirgate"},"teamz":[]}""").RootElement,
            refinement = JsonDocument.Parse("""{"themeByHeigth":{"12":"heath"}}""").RootElement,
            refinment = JsonDocument.Parse("{}").RootElement,
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
        await Assert.That(named).Contains("refinement.themeByHeigth");
        await Assert.That(named).Contains("refinment");
    }

    /// <summary>A source states its base: a plan, or a drawn layout together with the intent it is played for.
    /// Half a drawn pair, or no base at all, is refused as the request's own fault, naming the member that is
    /// missing — and nothing is stored.</summary>
    [Test]
    [Arguments("layout", """{"name":"nodoc","intent":{"meta":{"name":"nodoc"}}}""")]
    [Arguments("intent", """{"name":"nodoc","plan":{"cell":9,"pieces":[]},"layout":{"layers":[]}}""")]
    [Arguments("plan", """{"name":"nodoc","refinement":{"created":"2026-09-30"}}""")]
    public async Task A_source_stating_no_whole_base_names_what_it_lacks(string field, string body)
    {
        using var client = await FreshAsync();
        var refused = await client.PutAsync("/api/map/nodoc/source",
            new StringContent(body, Encoding.UTF8, "application/json"));

        await Assert.That(refused.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        var answer = await refused.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(answer.GetProperty("error").GetString()).IsEqualTo("no base");
        var finding = answer.GetProperty("findings")[0];
        await Assert.That(finding.GetProperty("rule").GetString()).IsEqualTo("RQ1");
        await Assert.That(finding.GetProperty("field").GetString()).IsEqualTo(field);
        var maps = await client.GetFromJsonAsync<JsonElement>("/api/maps");
        await Assert.That(maps.EnumerateArray().Any(m => m.GetProperty("slug").GetString() == "nodoc")).IsFalse();
    }

    /// <summary>One field the binder cannot read is a refusal naming it, not a default intent with no teams,
    /// spawns or objectives stored at 200 — and nothing is stored, so the export gate has no empty map to
    /// open on. <c>modes</c> is a real field; it takes objects, not the bare words posted here.</summary>
    [Test]
    public async Task An_intent_field_the_binder_cannot_read_is_refused_by_its_path()
    {
        using var client = await FreshAsync();

        var resp = await client.PutAsJsonAsync(Source, new
        {
            plan = JsonDocument.Parse("""{"cell":9,"pieces":[]}""").RootElement,
            layout = JsonDocument.Parse(Layout).RootElement,
            intent = JsonDocument.Parse("""{"meta":{"name":"Weirgate"},"modes":["dtm"]}""").RootElement,
            name = "Weirgate",
        });
        var text = await resp.Content.ReadAsStringAsync();
        await Assert.That(resp.StatusCode).IsEqualTo(HttpStatusCode.BadRequest).Because(text);

        var finding = JsonDocument.Parse(text).RootElement.GetProperty("findings")[0];
        await Assert.That(finding.GetProperty("rule").GetString()).IsEqualTo("RQ1");
        await Assert.That(finding.GetProperty("field").GetString()).IsEqualTo("intent.modes[0]");

        var maps = await client.GetFromJsonAsync<JsonElement>("/api/maps");
        await Assert.That(maps.EnumerateArray().Any(m => m.GetProperty("slug").GetString() == "weirgate")).IsFalse();
    }

    /// <summary>The same binder stands behind every intent write, so the map's own intent route refuses the
    /// field by the path the document states it at.</summary>
    [Test]
    public async Task An_intent_put_the_binder_cannot_read_is_refused_by_its_path()
    {
        using var client = await FreshAsync();
        var loaded = await client.PutAsJsonAsync(Source, Body());
        await Assert.That(loaded.IsSuccessStatusCode).IsTrue().Because(await loaded.Content.ReadAsStringAsync());

        var resp = await client.PutAsync("/api/map/weirgate/intent", new StringContent(
            """{"meta":{"name":"Weirgate"},"modes":["dtm"]}""", Encoding.UTF8, "application/json"));
        var text = await resp.Content.ReadAsStringAsync();
        await Assert.That(resp.StatusCode).IsEqualTo(HttpStatusCode.BadRequest).Because(text);
        var finding = JsonDocument.Parse(text).RootElement.GetProperty("findings")[0];
        await Assert.That(finding.GetProperty("field").GetString()).IsEqualTo("modes[0]");
    }

    /// <summary><b>The scan follows the drawing.</b> Every read of a board's ground loads the scan the finish
    /// wrote, and a layout written after it — a shape redrawn, a coast pulled in — leaves that scan describing
    /// the board before the edit. The read brings the scan up to the stored layout first.</summary>
    [Test]
    public async Task A_layout_written_after_the_finish_is_what_the_reads_answer_for()
    {
        using var client = await FreshAsync();
        var loaded = await client.PutAsJsonAsync(Source, Body());
        await Assert.That(loaded.IsSuccessStatusCode).IsTrue().Because(await loaded.Content.ReadAsStringAsync());

        var smaller = Layout.Replace("\"min_x\":-20,\"max_x\":20", "\"min_x\":-10,\"max_x\":10");
        var put = await client.PutAsync("/api/map/weirgate/sketch", new StringContent(smaller, Encoding.UTF8, "application/json"));
        await Assert.That(put.IsSuccessStatusCode).IsTrue().Because(await put.Content.ReadAsStringAsync());

        var read = await client.GetFromJsonAsync<JsonElement>("/api/map/weirgate/editability");
        var box = read.GetProperty("bbox");
        await Assert.That((box.GetProperty("min_x").GetInt32(), box.GetProperty("max_x").GetInt32()))
            .IsEqualTo((-10, 9)).Because("the scan was rasterized again from the shape as it is drawn now");
    }

    /// <summary><b>Every route that reads the scan reads the same one.</b> After a layout is shrunk past the
    /// finish, the side view, a single column, the islands, the surface overlay and the region frame all answer
    /// for the board as drawn — and the island the author excluded in Configure stays excluded, since a scan
    /// written again carries what the author set.</summary>
    [Test]
    public async Task Every_scan_read_answers_for_the_layout_as_drawn()
    {
        using var client = await FreshAsync();
        var loaded = await client.PutAsJsonAsync(Source, Body());
        await Assert.That(loaded.IsSuccessStatusCode).IsTrue().Because(await loaded.Content.ReadAsStringAsync());
        var excluded = await client.PatchAsJsonAsync("/api/configure/weirgate/exclude-island",
            new { island_id = 0, excluded = true });
        await Assert.That(excluded.IsSuccessStatusCode).IsTrue();

        var smaller = Layout.Replace("\"min_x\":-20,\"max_x\":20", "\"min_x\":-10,\"max_x\":10");
        var put = await client.PutAsync("/api/map/weirgate/sketch", new StringContent(smaller, Encoding.UTF8, "application/json"));
        await Assert.That(put.IsSuccessStatusCode).IsTrue().Because(await put.Content.ReadAsStringAsync());

        var column = await client.GetFromJsonAsync<JsonElement>("/api/map/weirgate/column-floor?x=15&z=0");
        await Assert.That(column.GetProperty("y").ValueKind).IsEqualTo(JsonValueKind.Null)
            .Because("x 15 was ground before the edit and is not now");

        var islands = await client.GetFromJsonAsync<JsonElement>("/api/map/weirgate/islands");
        var bounds = islands[0].GetProperty("bounds");
        await Assert.That((bounds[0].GetDouble(), bounds[2].GetDouble())).IsEqualTo((-10d, 10d));

        var surface = await client.GetFromJsonAsync<JsonElement>("/api/map/weirgate/top-surface");
        await Assert.That((surface.GetProperty("min_x").GetInt32(), surface.GetProperty("max_x").GetInt32()))
            .IsEqualTo((-10, 9));

        var tree = await client.GetFromJsonAsync<JsonElement>("/api/map/weirgate/regions/tree");
        await Assert.That(tree.GetProperty("bounding_box").GetProperty("max_x").GetDouble()).IsEqualTo(9d);

        var state = await client.GetFromJsonAsync<JsonElement>("/api/configure/weirgate/state");
        await Assert.That(state.GetProperty("exclude_islands").EnumerateArray().Select(id => id.GetInt32()))
            .IsEquivalentTo([0]);
    }

    /// <summary><b>The findings list judges the board as drawn.</b> Two halves, laid <c>rot_180</c>, meet a
    /// frontline build zone at x −5..5 across ground the plan states. Pulled back to x −10 after Finish, each
    /// coast leaves void between it and the zone, and the findings read names that (<c>EZ2</c>) without another
    /// Finish.</summary>
    [Test]
    public async Task The_findings_list_names_a_coast_pulled_back_after_the_finish()
    {
        const string halves = """
            {"setup":{"mirror_mode":"rot_180","center":{"cx":0,"cz":0}},
             "layers":[{"base_y":0,"layout":{
               "shapes":[{"id":"s1","type":"rectangle","operation":"add",
                          "min_x":-40,"max_x":-5,"min_z":-20,"max_z":20,"floor":0,"base_height":12}],
               "groups":[{"id":"i","name":"I","shapeIds":["s1"]}]}}]}
            """;
        using var client = await FreshAsync();
        var body = new
        {
            plan = JsonDocument.Parse("""{"cell":4,"pieces":[{"id":"field","role":"piece","rect":[-10,-5,20,10]}]}""").RootElement,
            layout = JsonDocument.Parse(halves).RootElement,
            intent = JsonDocument.Parse("""
                {"meta":{"name":"Weirgate","authors":[],"contributors":[]},
                 "build":{"areas":[{"minX":-5,"minZ":-20,"maxX":5,"maxZ":20}],"holes":[]}}
                """).RootElement,
            name = "Weirgate",
        };
        var loaded = await client.PutAsJsonAsync(Source, body);
        await Assert.That(loaded.IsSuccessStatusCode).IsTrue().Because(await loaded.Content.ReadAsStringAsync());
        await Assert.That(await RulesAsync(client)).DoesNotContain("EZ2");

        var pulled = halves.Replace("\"max_x\":-5", "\"max_x\":-9");
        var put = await client.PutAsync("/api/map/weirgate/sketch", new StringContent(pulled, Encoding.UTF8, "application/json"));
        await Assert.That(put.IsSuccessStatusCode).IsTrue().Because(await put.Content.ReadAsStringAsync());

        await Assert.That(await RulesAsync(client)).Contains("EZ2");

        static async Task<List<string>> RulesAsync(HttpClient client) =>
        [
            .. (await client.GetFromJsonAsync<JsonElement>("/api/map/weirgate/findings"))
                .GetProperty("findings").EnumerateArray().Select(finding => finding.GetProperty("rule").GetString()!),
        ];
    }

    /// <summary><b>A plan is the base, and its refinement lands in the request that stores it.</b> The board the
    /// plan compiles to comes back with the relief, the date and the credits a plan has no words for, and the map
    /// keeps all of it as one change carrying the note it was stated with.</summary>
    [Test]
    public async Task A_plan_is_compiled_and_refined_in_the_one_change_that_stores_it()
    {
        using var client = await FreshAsync();

        var resp = await client.PutAsJsonAsync("/api/map/twowool/source", new
        {
            plan = JsonDocument.Parse(Seeds.Read("base-2wool.plan.json")).RootElement,
            refinement = JsonDocument.Parse("""
                {"relief":{"*":{"base":3}},"created":"2026-09-30","authors":["Opus 5"]}
                """).RootElement,
            note = "the first pass",
        });
        var text = await resp.Content.ReadAsStringAsync();
        await Assert.That(resp.IsSuccessStatusCode).IsTrue().Because(text);
        await Assert.That(JsonDocument.Parse(text).RootElement.GetProperty("change").GetInt64()).IsEqualTo(1);

        var layout = await client.GetFromJsonAsync<JsonElement>("/api/map/twowool/sketch");
        var groups = layout.GetProperty("layers")[0].GetProperty("layout").GetProperty("groups").EnumerateArray()
            .Select(group => group.GetProperty("id").GetString()!).ToList();
        await Assert.That(groups).IsNotEmpty();
        foreach (var group in groups)
            await Assert.That(layout.GetProperty("relief").GetProperty(group).GetProperty("base").GetDouble())
                .IsEqualTo(3d).Because($"'*' reaches every group of the compiled ground, {group} among them");

        var meta = (await client.GetFromJsonAsync<JsonElement>("/api/map/twowool/intent")).GetProperty("meta");
        await Assert.That(meta.GetProperty("created").GetString()).IsEqualTo("2026-09-30");
        await Assert.That(meta.GetProperty("authors")[0].GetProperty("name").GetString()).IsEqualTo("Opus 5");
        var maps = await client.GetFromJsonAsync<JsonElement>("/api/maps");
        await Assert.That(maps.EnumerateArray().Single(m => m.GetProperty("slug").GetString() == "twowool")
            .GetProperty("name").GetString()).IsEqualTo("Base 2-Wool").Because("a compiled intent is named by its plan");

        var change = (await client.GetFromJsonAsync<JsonElement>("/api/map/twowool/changes"))
            .GetProperty("changes").EnumerateArray().Single();
        await Assert.That(change.GetProperty("note").GetString()).IsEqualTo("the first pass");
        await Assert.That(change.GetProperty("documents").EnumerateArray().Select(document => document.GetString()!))
            .IsEquivalentTo(["plan", "refinement", "layout", "intent"]);
    }

    /// <summary><b>A dry run is decided and not stored.</b> It answers what the source would change in the documents
    /// the map holds — every one of them stated for the first time where no map is stored — and the documents it
    /// would store, and leaves the map as it was.</summary>
    [Test]
    public async Task A_dry_run_answers_what_the_source_would_change_and_stores_nothing()
    {
        using var client = await FreshAsync();

        var first = await (await client.PutAsJsonAsync($"{Source}?dry=true", Body(authors: new object[] { "Opus 5" })))
            .Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(first.GetProperty("change").ValueKind).IsEqualTo(JsonValueKind.Null);
        await Assert.That(Edits(first).Select(edit => edit.Document).Distinct())
            .IsEquivalentTo(["plan", "refinement", "layout", "intent"]);
        await Assert.That(first.GetProperty("layout").GetProperty("layers")[0].GetProperty("layout")
            .GetProperty("shapes")[0].GetProperty("id").GetString()).IsEqualTo("s1");
        await Assert.That(first.GetProperty("intent").GetProperty("meta").GetProperty("authors")[0]
            .GetProperty("name").GetString()).IsEqualTo("Opus 5").Because("the documents answered are the refined ones");
        var maps = await client.GetFromJsonAsync<JsonElement>("/api/maps");
        await Assert.That(maps.EnumerateArray().Any(m => m.GetProperty("slug").GetString() == "weirgate")).IsFalse();

        var stored = await client.PutAsJsonAsync(Source, Body());
        var storedAnswer = await stored.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(stored.IsSuccessStatusCode).IsTrue().Because(storedAnswer.ToString());
        await Assert.That(storedAnswer.TryGetProperty("layout", out _)).IsFalse()
            .Because("a store's documents are read from the map");
        var smaller = Layout.Replace("\"max_x\":20", "\"max_x\":10");
        var dry = await client.PutAsJsonAsync($"{Source}?dry=true", new
        {
            plan = JsonDocument.Parse("""{"cell":9,"pieces":[]}""").RootElement,
            layout = JsonDocument.Parse(smaller).RootElement,
            intent = JsonDocument.Parse("""{"meta":{"name":"Weirgate","authors":[],"contributors":[]}}""").RootElement,
        });
        var answer = await dry.Content.ReadFromJsonAsync<JsonElement>();

        await Assert.That(Edits(answer).Select(edit => (edit.Document, edit.Path, edit.Op)))
            .IsEquivalentTo([("layout", "layers[0].layout.shapes[s1].max_x", "set")])
            .Because("the plan and the intent are the ones the map holds");
        await Assert.That(answer.GetProperty("replaced").GetBoolean()).IsTrue();
        await Assert.That(await StoredMaxXAsync(client)).IsEqualTo(20d);
        var changes = await client.GetFromJsonAsync<JsonElement>("/api/map/weirgate/changes");
        await Assert.That(changes.GetProperty("changes").GetArrayLength()).IsEqualTo(1);

        static List<(string Document, string Path, string Op)> Edits(JsonElement answer) =>
        [
            .. answer.GetProperty("edits").EnumerateArray().Select(edit => (
                edit.GetProperty("document").GetString()!, edit.GetProperty("path").GetString()!,
                edit.GetProperty("op").GetString()!)),
        ];
    }

    /// <summary><b>A refinement that does not say what it means refuses the whole source.</b> A point edit naming
    /// no index is not applied a guess at a time: the source is answered 422 by the rule, and nothing is stored —
    /// while one naming a shape the board does not have is said, and the rest is stored.</summary>
    [Test]
    public async Task A_refinement_edit_naming_no_point_refuses_and_one_naming_no_shape_is_said()
    {
        using var client = await FreshAsync();

        var refused = await client.PutAsJsonAsync(Source, new
        {
            plan = JsonDocument.Parse("""{"cell":9,"pieces":[]}""").RootElement,
            layout = JsonDocument.Parse(Layout).RootElement,
            intent = JsonDocument.Parse("""{"meta":{"name":"Weirgate"}}""").RootElement,
            refinement = JsonDocument.Parse("""{"editShapes":{"s1":[{"x":1,"z":1}]}}""").RootElement,
        });
        var text = await refused.Content.ReadAsStringAsync();
        await Assert.That((int)refused.StatusCode).IsEqualTo(422).Because(text);
        await Assert.That(JsonDocument.Parse(text).RootElement.GetProperty("findings")[0].GetProperty("rule").GetString())
            .IsEqualTo("SR4");
        var maps = await client.GetFromJsonAsync<JsonElement>("/api/maps");
        await Assert.That(maps.EnumerateArray().Any(m => m.GetProperty("slug").GetString() == "weirgate")).IsFalse();

        var said = await client.PutAsJsonAsync(Source, new
        {
            plan = JsonDocument.Parse("""{"cell":9,"pieces":[]}""").RootElement,
            layout = JsonDocument.Parse(Layout).RootElement,
            intent = JsonDocument.Parse("""{"meta":{"name":"Weirgate"}}""").RootElement,
            refinement = JsonDocument.Parse("""{"themeById":{"s9":"heath","s1":"heath"}}""").RootElement,
        });
        var answer = await said.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(said.IsSuccessStatusCode).IsTrue().Because(answer.ToString());
        await Assert.That(answer.GetProperty("warnings").EnumerateArray().Select(warning => warning.GetProperty("rule").GetString()))
            .Contains("SR2");
        var layout = await client.GetFromJsonAsync<JsonElement>("/api/map/weirgate/sketch");
        await Assert.That(layout.GetProperty("layers")[0].GetProperty("layout").GetProperty("shapes")[0]
            .GetProperty("theme").GetString()).IsEqualTo("heath");
    }

    /// <summary>The slug is the route's, and a route segment that is not one is refused rather than stored under
    /// the slug it would become.</summary>
    [Test]
    public async Task A_route_naming_no_slug_is_refused_naming_it()
    {
        using var client = await FreshAsync();

        var refused = await client.PutAsJsonAsync("/api/map/Weir%20Gate/source", Body());
        var text = await refused.Content.ReadAsStringAsync();

        await Assert.That(refused.StatusCode).IsEqualTo(HttpStatusCode.BadRequest).Because(text);
        await Assert.That(JsonDocument.Parse(text).RootElement.GetProperty("findings")[0].GetProperty("message").GetString())
            .Contains("slug 'Weir Gate' has characters other than");
    }

    /// <summary><b>A source is not applied over a hand edit it has not seen.</b> A map made from a refinement,
    /// edited in the Sketch tool after its source was applied, refuses the next apply — the dry run as well — and
    /// hands the edit over as the refinement would state it, naming the change; the hand edit stays.</summary>
    [Test]
    public async Task A_source_over_a_hand_edit_it_has_not_seen_is_refused_and_hands_the_edit_over()
    {
        using var client = await HandEditedAsync();

        foreach (var route in (string[])[$"{Source}?dry=true", Source])
        {
            var refused = await client.PutAsJsonAsync(route, Body(authors: new object[] { "Opus 5" }));
            var text = await refused.Content.ReadAsStringAsync();
            await Assert.That(refused.StatusCode).IsEqualTo(HttpStatusCode.Conflict).Because(text);

            var finding = JsonDocument.Parse(text).RootElement.GetProperty("findings").EnumerateArray().Single();
            await Assert.That(finding.GetProperty("rule").GetString()).IsEqualTo("SR1");
            await Assert.That(finding.GetProperty("subjects")[0].GetString()).IsEqualTo("2");
            var edit = finding.GetProperty("edit");
            await Assert.That((edit.GetProperty("document").GetString(), edit.GetProperty("path").GetString()))
                .IsEqualTo(("refinement", "shapePropsById.s1.max_x"));
            await Assert.That(edit.GetProperty("value").GetDouble()).IsEqualTo(10d);
        }
        await Assert.That(await StoredMaxXAsync(client)).IsEqualTo(10d).Because("the hand edit is still the board");
    }

    /// <summary>A source that states it was built after the hand edit, or names the edit to drop, is applied — and
    /// the change it lands as records what it dropped.</summary>
    [Test]
    public async Task A_source_that_has_seen_the_change_or_drops_it_is_applied()
    {
        using var client = await HandEditedAsync();
        var seen = await client.PutAsJsonAsync(Source, new
        {
            plan = JsonDocument.Parse("""{"cell":9,"pieces":[]}""").RootElement,
            layout = JsonDocument.Parse(Layout).RootElement,
            intent = JsonDocument.Parse("""{"meta":{"name":"Weirgate","authors":[],"contributors":[]}}""").RootElement,
            refinement = new { authors = new object[] { "Opus 5" } },
            after = 2,
        });
        await Assert.That(seen.IsSuccessStatusCode).IsTrue().Because(await seen.Content.ReadAsStringAsync());

        await client.PutAsync("/api/map/weirgate/sketch", Json(Layout.Replace("\"max_x\":20", "\"max_x\":12")));
        var dropped = await client.PutAsJsonAsync($"{Source}?discard=4", Body(authors: new object[] { "Opus 5" }));
        await Assert.That(dropped.IsSuccessStatusCode).IsTrue().Because(await dropped.Content.ReadAsStringAsync());
        await Assert.That(await StoredMaxXAsync(client)).IsEqualTo(20d);

        var changes = (await client.GetFromJsonAsync<JsonElement>("/api/map/weirgate/changes")).GetProperty("changes");
        await Assert.That(changes[changes.GetArrayLength() - 1].GetProperty("discarded").EnumerateArray()
            .Select(number => number.GetInt64())).IsEquivalentTo([4L]);
        await Assert.That(changes[0].GetProperty("discarded").GetArrayLength()).IsEqualTo(0);
    }

    /// <summary>A map whose source stated no refinement is its drawing, and a source replaces a hand edit of it
    /// without asking.</summary>
    [Test]
    public async Task A_map_made_without_a_refinement_is_never_refused()
    {
        using var client = await FreshAsync();
        await client.PutAsJsonAsync(Source, Body());
        await client.PutAsync("/api/map/weirgate/sketch", Json(Layout.Replace("\"max_x\":20", "\"max_x\":10")));

        var again = await client.PutAsJsonAsync(Source, Body());

        await Assert.That(again.IsSuccessStatusCode).IsTrue().Because(await again.Content.ReadAsStringAsync());
    }

    /// <summary>A change is named by a number the map has, and a dropped one by a number the source has not
    /// seen.</summary>
    [Test]
    public async Task An_after_or_a_discard_naming_no_change_it_could_be_is_refused()
    {
        using var client = await HandEditedAsync();

        var future = await client.PutAsJsonAsync(Source, new
        {
            plan = JsonDocument.Parse("""{"cell":9,"pieces":[]}""").RootElement,
            layout = JsonDocument.Parse(Layout).RootElement,
            intent = JsonDocument.Parse("""{"meta":{"name":"Weirgate"}}""").RootElement,
            after = 9,
        });
        var stray = await client.PutAsJsonAsync($"{Source}?discard=1", Body(authors: new object[] { "Opus 5" }));
        var unreadable = await client.PutAsJsonAsync($"{Source}?discard=two", Body(authors: new object[] { "Opus 5" }));

        foreach (var (refused, field) in new[] { (future, "after"), (stray, "discard"), (unreadable, "discard") })
        {
            var text = await refused.Content.ReadAsStringAsync();
            await Assert.That(refused.StatusCode).IsEqualTo(HttpStatusCode.BadRequest).Because(text);
            await Assert.That(JsonDocument.Parse(text).RootElement.GetProperty("findings")[0].GetProperty("field")
                .GetString()).IsEqualTo(field);
        }
    }

    /// <summary>The refinement a source stated is one of the map's documents: read back as it was stated, kept on
    /// the change, and absent from a map no source has been applied to.</summary>
    [Test]
    public async Task The_refinement_a_source_states_is_kept_as_the_maps_document()
    {
        using var client = await FreshAsync();
        await client.PostAsJsonAsync("/api/sketch", new { name = "Drawn" });
        var none = await client.GetAsync("/api/map/drawn/refinement");
        await Assert.That(none.StatusCode).IsEqualTo(HttpStatusCode.NotFound);

        await client.PutAsJsonAsync(Source, Body(authors: new object[] { "Opus 5" }));
        var kept = await client.GetFromJsonAsync<JsonElement>("/api/map/weirgate/refinement");
        await Assert.That(kept.GetProperty("authors")[0].GetString()).IsEqualTo("Opus 5");
        var atChange = await client.GetFromJsonAsync<JsonElement>("/api/map/weirgate/changes/1");
        await Assert.That(atChange.GetProperty("refinement").GetProperty("authors")[0].GetString()).IsEqualTo("Opus 5");
    }

    /// <summary>Weirgate stored from a source with a refinement, then its plate narrowed to x −10..10 in the Sketch
    /// tool: changes 1 and 2.</summary>
    private static async Task<HttpClient> HandEditedAsync()
    {
        var client = await FreshAsync();
        var stored = await client.PutAsJsonAsync(Source, Body(authors: new object[] { "Opus 5" }));
        await Assert.That(stored.IsSuccessStatusCode).IsTrue().Because(await stored.Content.ReadAsStringAsync());
        var edited = await client.PutAsync("/api/map/weirgate/sketch",
            Json(Layout.Replace("\"min_x\":-20,\"max_x\":20", "\"min_x\":-20,\"max_x\":10")));
        await Assert.That(edited.IsSuccessStatusCode).IsTrue().Because(await edited.Content.ReadAsStringAsync());
        return client;
    }

    private static async Task<HttpClient> FreshAsync()
    {
        await ApiTestFactory.ResetSchemaAsync();
        return ApiTestFactory.Shared.CreateClient();
    }

    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");

    private static string? Etag(HttpResponseMessage response) =>
        response.Headers.TryGetValues("ETag", out var values) ? values.FirstOrDefault() : null;

    /// <summary>The east edge of the one shape the stored layout draws, which tells the two boards apart.</summary>
    private static async Task<double> StoredMaxXAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<JsonElement>("/api/map/weirgate/sketch"))
            .GetProperty("layers")[0].GetProperty("layout").GetProperty("shapes")[0].GetProperty("max_x").GetDouble();
}
