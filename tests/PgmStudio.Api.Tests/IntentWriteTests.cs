using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

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
    /// authors are not among them, so an intent that names nobody leaves the map's people alone. Before this
    /// held, the loop a driver actually runs — state the metadata, then rebuild — answered 200 twice and lost
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
    /// about the people, so it may not clear them either.</summary>
    [Test]
    public async Task Storing_an_intent_that_names_nobody_keeps_the_authors()
    {
        var (client, slug) = await MapWithAnAuthorAsync();
        using var _ = client;

        var stored = await PutIntentAsync(client, $"/api/map/{slug}/intent", Compiled);
        await Assert.That(stored.IsSuccessStatusCode).IsTrue().Because(await stored.Content.ReadAsStringAsync());

        await Assert.That(await AuthorsOfAsync(client, slug)).IsEquivalentTo(new[] { "Opus 5" });
    }

    /// <summary>And an intent that <em>does</em> name people still states them — the guard is over a document
    /// that says nothing, not over the field itself, so nothing that could be set before can no longer be.
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
    /// author believe somebody was credited (<c>TC2</c>).</summary>
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
}
