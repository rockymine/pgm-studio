using System.Net.Http.Json;
using PgmStudio.Client.Components;

namespace PgmStudio.Client.Models;

/// <summary>
/// Every write the Edit tool makes to a stored map, each named for the operation and each carrying its
/// <b>whole</b> route.
///
/// <para>A route written as a prefix here and a tail at the call site is not a route: nothing can read it,
/// so <c>ClientRouteTests</c> — the one gate over the last hand-written half of the contract — cannot check
/// it against the schema, and a typo reaches the browser as a 404 that reads like a missing map. Naming the
/// operation is what puts the literal in one place, and it is also what a reader of a phase now sees instead
/// of a string built two lines apart.</para>
///
/// <para><see cref="RefusedAsync"/> is the other half a caller needs: the sentence a refusal answers, or
/// null where it did not. What a phase does with that sentence — where it shows, what it re-renders — is
/// component state and stays in the component.</para>
/// </summary>
public static class MapEdits
{
    // ── the map itself ──────────────────────────────────────────────────────────

    public static Task<HttpResponseMessage> SetMetadata(HttpClient http, string slug, object body) =>
        http.PatchAsJsonAsync($"api/map/{slug}/metadata", body);

    // ── regions ─────────────────────────────────────────────────────────────────

    public static Task<HttpResponseMessage> PatchRegion(HttpClient http, string slug, string regionId, object body) =>
        http.PatchAsJsonAsync($"api/map/{slug}/regions/{regionId}", body);

    public static Task<HttpResponseMessage> DeleteRegion(HttpClient http, string slug, string regionId) =>
        http.DeleteAsync($"api/map/{slug}/regions/{regionId}");

    // ── teams ───────────────────────────────────────────────────────────────────

    public static Task<HttpResponseMessage> AddTeam(HttpClient http, string slug, object body) =>
        http.PostAsJsonAsync($"api/map/{slug}/teams", body);

    public static Task<HttpResponseMessage> PatchTeam(HttpClient http, string slug, string teamId, object body) =>
        http.PatchAsJsonAsync($"api/map/{slug}/teams/{teamId}", body);

    public static Task<HttpResponseMessage> DeleteTeam(HttpClient http, string slug, string teamId) =>
        http.DeleteAsync($"api/map/{slug}/teams/{teamId}");

    // ── spawns ──────────────────────────────────────────────────────────────────

    public static Task<HttpResponseMessage> AddSpawn(HttpClient http, string slug, object body) =>
        http.PostAsJsonAsync($"api/map/{slug}/spawns", body);

    public static Task<HttpResponseMessage> PatchSpawn(HttpClient http, string slug, string regionId, object body) =>
        http.PatchAsJsonAsync($"api/map/{slug}/spawns/{regionId}", body);

    public static Task<HttpResponseMessage> DeleteSpawn(HttpClient http, string slug, string regionId) =>
        http.DeleteAsync($"api/map/{slug}/spawns/{regionId}");

    public static Task<HttpResponseMessage> SetObserverSpawn(HttpClient http, string slug, object body) =>
        http.PatchAsJsonAsync($"api/map/{slug}/observer-spawn", body);

    public static Task<HttpResponseMessage> DeleteObserverSpawn(HttpClient http, string slug) =>
        http.DeleteAsync($"api/map/{slug}/observer-spawn");

    // ── wools and their monuments ───────────────────────────────────────────────

    public static Task<HttpResponseMessage> AddWool(HttpClient http, string slug, object body) =>
        http.PostAsJsonAsync($"api/map/{slug}/wools", body);

    public static Task<HttpResponseMessage> PatchWool(HttpClient http, string slug, string woolId, object body) =>
        http.PatchAsJsonAsync($"api/map/{slug}/wools/{woolId}", body);

    public static Task<HttpResponseMessage> DeleteWool(HttpClient http, string slug, string woolId) =>
        http.DeleteAsync($"api/map/{slug}/wools/{woolId}");

    public static Task<HttpResponseMessage> AddMonument(HttpClient http, string slug, string woolId, object body) =>
        http.PostAsJsonAsync($"api/map/{slug}/wools/{woolId}/monuments", body);

    public static Task<HttpResponseMessage> PatchMonument(
        HttpClient http, string slug, string woolId, string monumentId, object body) =>
        http.PatchAsJsonAsync($"api/map/{slug}/wools/{woolId}/monuments/{monumentId}", body);

    public static Task<HttpResponseMessage> DeleteMonument(
        HttpClient http, string slug, string woolId, string monumentId) =>
        http.DeleteAsync($"api/map/{slug}/wools/{woolId}/monuments/{monumentId}");

    // ── what a caller does with the answer ──────────────────────────────────────

    /// <summary>The sentence a refusal answered, or null where the write landed. Picking it is shared
    /// because every phase picks the same one; showing it is not, because where an error goes and what
    /// re-renders around it is the component's own.</summary>
    public static async Task<string?> RefusedAsync(Task<HttpResponseMessage> call)
    {
        using var response = await call;
        return response.IsSuccessStatusCode ? null : await ServerRefusal.SentenceAsync(response);
    }
}
