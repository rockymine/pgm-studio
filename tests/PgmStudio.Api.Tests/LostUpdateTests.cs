using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace PgmStudio.Api.Tests;

/// <summary>
/// Two writers to one document, and what the second one costs.
///
/// <para><b>The failure is silent, which is why it needs a test rather than a report.</b> Every write in the
/// studio replaces: an edit reads the whole map document, patches it and writes the whole document back, and
/// an artifact is one row a save replaces. Two callers doing that at once keep only the second — same status,
/// same body, nothing in either response saying a change was dropped. One person in one browser tab never met
/// it; an agent driving the API while a tab is open on the same slug meets it on the first collision.</para>
///
/// <para><b>Both halves are asserted.</b> A guard that refused every second write would satisfy the conflict
/// test and make the studio unusable, so what is asserted just as hard is that an unguarded write still
/// writes, that a guarded write against the current revision lands, and that the revision a read answers is
/// the one the next write may state.</para>
/// </summary>
[NotInParallel("api-db")]
public sealed class LostUpdateTests
{
    private const string First = """{"setup":{"mirror_mode":"rot_180"},"mapTheme":"first"}""";
    private const string Second = """{"setup":{"mirror_mode":"rot_180"},"mapTheme":"second"}""";

    /// <summary>The case the whole thing exists for: two callers read the same layout, both write, and the
    /// second is told rather than winning in silence.</summary>
    [Test]
    public async Task A_write_against_a_layout_someone_else_replaced_is_refused()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();
        var slug = await OriginateAsync(client);

        var read = await client.GetAsync($"/api/map/{slug}/sketch");
        var held = Etag(read);
        await Assert.That(held).IsNotNull().Because("a read answers the revision its next write may state");

        // The other caller gets there first, holding the same revision.
        var theirs = await client.PutAsync($"/api/map/{slug}/sketch", Json(First));
        await Assert.That(theirs.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var mine = await client.SendAsync(Guarded(slug, Second, held!));

        await Assert.That(mine.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
        var refusal = await mine.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(refusal.GetProperty("error").GetString()).IsEqualTo("stale write");
        await Assert.That(refusal.GetProperty("findings")[0].GetProperty("rule").GetString()).IsEqualTo("RQ5");

        // And the refusal means it: the first caller's write is what the map still holds.
        await Assert.That(await StoredAsync(client, slug)).IsEqualTo("first");
    }

    /// <summary>A write guarded against the revision the document is actually at lands, and answers the one
    /// it is at now — so a caller can write twice in a row without re-reading.</summary>
    [Test]
    public async Task A_write_against_the_current_revision_lands_and_answers_the_next_one()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();
        var slug = await OriginateAsync(client);

        var held = Etag(await client.GetAsync($"/api/map/{slug}/sketch"))!;
        var first = await client.SendAsync(Guarded(slug, First, held));

        await Assert.That(first.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var next = Etag(first);
        await Assert.That(next).IsNotNull().Because("a write answers the revision it landed at");
        await Assert.That(next).IsNotEqualTo(held);

        var second = await client.SendAsync(Guarded(slug, Second, next!));
        await Assert.That(second.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(await StoredAsync(client, slug)).IsEqualTo("second");
    }

    /// <summary>A write that states no precondition writes, exactly as it did before there was a revision to
    /// state. Protection is opted into by having read first, which is the only way it can mean anything —
    /// requiring it would make every caller read before every write.</summary>
    [Test]
    public async Task A_write_stating_no_precondition_still_writes()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();
        var slug = await OriginateAsync(client);

        await client.PutAsync($"/api/map/{slug}/sketch", Json(First));
        var second = await client.PutAsync($"/api/map/{slug}/sketch", Json(Second));

        await Assert.That(second.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(await StoredAsync(client, slug)).IsEqualTo("second");
    }

    /// <summary>An <c>If-Match</c> that is not a revision at all cannot match one, so it is refused rather
    /// than read as an absent header — a caller that meant to guard its write is not quietly unguarded.</summary>
    [Test]
    public async Task An_if_match_that_names_no_revision_is_refused_rather_than_ignored()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();
        var slug = await OriginateAsync(client);

        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/map/{slug}/sketch") { Content = Json(Second) };
        request.Headers.TryAddWithoutValidation("If-Match", "\"not-a-revision\"");

        await Assert.That((await client.SendAsync(request)).StatusCode).IsEqualTo(HttpStatusCode.Conflict);
    }

    /// <summary>The map document carries its own revision, separate from any artifact's — a caller holding
    /// one of the sketch layout has said nothing about the map, and a metadata patch must not be refused
    /// because someone saved a drawing.</summary>
    [Test]
    public async Task The_map_document_and_an_artifact_are_versioned_apart()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();
        var slug = await OriginateAsync(client);

        var mapHeld = Etag(await client.GetAsync($"/api/map/{slug}"))!;

        // A sketch write moves the layout's revision and leaves the map's alone.
        await client.PutAsync($"/api/map/{slug}/sketch", Json(First));

        var patch = new HttpRequestMessage(HttpMethod.Patch, $"/api/map/{slug}/metadata")
        { Content = Json("""{"name":"renamed"}""") };
        patch.Headers.TryAddWithoutValidation("If-Match", mapHeld);

        await Assert.That((await client.SendAsync(patch)).StatusCode).IsEqualTo(HttpStatusCode.OK)
            .Because("the sketch write did not touch the map document the If-Match names");
    }

    private static HttpRequestMessage Guarded(string slug, string body, string etag)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/map/{slug}/sketch") { Content = Json(body) };
        request.Headers.TryAddWithoutValidation("If-Match", etag);
        return request;
    }

    private static string? Etag(HttpResponseMessage response) =>
        response.Headers.TryGetValues("ETag", out var values) ? values.FirstOrDefault() : null;

    /// <summary>Which of the two writers the stored layout came from.</summary>
    private static async Task<string?> StoredAsync(HttpClient client, string slug) =>
        (await (await client.GetAsync($"/api/map/{slug}/sketch"))
            .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("mapTheme").GetString();

    private static async Task<string> OriginateAsync(HttpClient client) =>
        (await (await client.PostAsJsonAsync("/api/sketch", new { name = "lost update" }))
            .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("slug").GetString()!;

    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");
}
