using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace PgmStudio.Api.Tests;

/// <summary>
/// <c>DELETE /api/map/{slug}/discard-if-empty</c> throws away a draft "New sketch" or "New plan" made and
/// nobody worked on, and nothing else: a draft with a name, a saved document or a shape in it is kept.
/// </summary>
[NotInParallel("api-db")]
public sealed class DraftDiscardTests
{
    private const string SavedPlan = """{"plan":2,"meta":{"name":"Untitled plan"},"pieces":[]}""";

    [Test]
    public async Task An_untouched_sketch_is_discarded_and_its_slug_is_free_again()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();

        var slug = await OriginateAsync(client, "/api/sketch", "Untitled sketch");
        await Assert.That(await DiscardAsync(client, slug)).IsTrue();
        await Assert.That(await OriginateAsync(client, "/api/sketch", "Untitled sketch")).IsEqualTo(slug);
    }

    [Test]
    public async Task An_untouched_plan_is_discarded_and_its_slug_is_free_again()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();

        var slug = await OriginateAsync(client, "/api/plan", "Untitled plan");
        await Assert.That(await DiscardAsync(client, slug)).IsTrue();
        await Assert.That(await OriginateAsync(client, "/api/plan", "Untitled plan")).IsEqualTo(slug);
    }

    [Test]
    public async Task A_saved_plan_is_kept()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();

        var slug = await OriginateAsync(client, "/api/plan", "Untitled plan");
        using var saved = await client.PutAsync($"/api/map/{slug}/plan",
            new StringContent(SavedPlan, Encoding.UTF8, "application/json"));
        await Assert.That(saved.IsSuccessStatusCode).IsTrue().Because(await saved.Content.ReadAsStringAsync());

        await Assert.That(await DiscardAsync(client, slug)).IsFalse();
    }

    [Test]
    [Arguments("/api/plan", "Untitled plan")]
    [Arguments("/api/sketch", "Untitled sketch")]
    public async Task A_named_draft_is_kept(string route, string untitled)
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();

        var slug = await OriginateAsync(client, route, untitled);
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
