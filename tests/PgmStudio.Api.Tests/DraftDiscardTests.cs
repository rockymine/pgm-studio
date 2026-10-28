using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace PgmStudio.Api.Tests;

/// <summary>
/// <c>DELETE /api/map/{slug}/discard-if-empty</c> throws away a draft "New sketch" made and nobody worked on, and
/// nothing else: a draft with a name or a shape in it is kept.
/// </summary>
[NotInParallel("api-db")]
public sealed class DraftDiscardTests
{
    [Test]
    public async Task An_untouched_sketch_is_discarded_and_is_gone()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();

        var slug = await OriginateAsync(client, "/api/sketch", "Untitled sketch");
        await Assert.That(await DiscardAsync(client, slug)).IsTrue();
        using var gone = await client.GetAsync($"/api/map/{slug}");
        await Assert.That(gone.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task A_named_draft_is_kept()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();

        var slug = await OriginateAsync(client, "/api/sketch", "Untitled sketch");
        using var named = await client.PatchAsJsonAsync($"/api/map/{slug}/metadata", new { name = "Weirgate" });
        await Assert.That(named.IsSuccessStatusCode).IsTrue().Because(await named.Content.ReadAsStringAsync());

        await Assert.That(await DiscardAsync(client, slug)).IsFalse();
    }

    [Test]
    public async Task A_slug_no_map_is_stored_under_is_not_discarded_and_not_refused()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();
        await Assert.That(await DiscardAsync(client, "nothing-here")).IsFalse();
    }

    private static async Task<string> OriginateAsync(HttpClient client, string route, string name)
    {
        using var resp = await client.PostAsJsonAsync(route, new { name });
        var text = await resp.Content.ReadAsStringAsync();
        await Assert.That(resp.IsSuccessStatusCode).IsTrue().Because(text);
        return JsonDocument.Parse(text).RootElement.GetProperty("slug").GetString()!;
    }

    private static async Task<bool> DiscardAsync(HttpClient client, string slug)
    {
        using var resp = await client.DeleteAsync($"/api/map/{slug}/discard-if-empty");
        var text = await resp.Content.ReadAsStringAsync();
        await Assert.That(resp.IsSuccessStatusCode).IsTrue().Because(text);
        return JsonDocument.Parse(text).RootElement.GetProperty("discarded").GetBoolean();
    }
}
