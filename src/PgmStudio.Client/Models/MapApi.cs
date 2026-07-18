using System.Net.Http.Json;
using System.Text.Json;

namespace PgmStudio.Client.Models;

/// <summary>The map editor's write API — the POST/PATCH/DELETE trio against <c>api/map/{slug}/…</c> shared
/// by the editor activities, plus the one error-body decode they all repeated. Each call resolves to the
/// server's error message (<c>null</c> on success); the activity owns surfacing it (its own <c>error</c>
/// field + <c>StateHasChanged</c>), matching the <see cref="RegionEdits"/> static-helper pattern.</summary>
public static class MapApi
{
    private static string Url(string slug, string path) => $"api/map/{slug}/{path}";

    /// <summary>POST a body; null on success, else the decoded error.</summary>
    public static Task<string?> PostAsync(HttpClient http, string slug, string path, object body)
        => SendAsync(http.PostAsJsonAsync(Url(slug, path), body));

    /// <summary>PATCH a body; null on success, else the decoded error.</summary>
    public static Task<string?> PatchAsync(HttpClient http, string slug, string path, object body)
        => SendAsync(http.PatchAsJsonAsync(Url(slug, path), body));

    /// <summary>DELETE; null on success, else the decoded error.</summary>
    public static Task<string?> DeleteAsync(HttpClient http, string slug, string path)
        => SendAsync(http.DeleteAsync(Url(slug, path)));

    /// <summary>Await a call and reduce it to an error message — null on success, else the response's
    /// <c>error</c> field (or <c>error {status}</c> when the body has none / can't be read).</summary>
    public static async Task<string?> SendAsync(Task<HttpResponseMessage> call)
    {
        var resp = await call;
        if (resp.IsSuccessStatusCode) return null;
        try
        {
            var d = await resp.Content.ReadFromJsonAsync<JsonElement>();
            return d.TryGetProperty("error", out var e) ? e.GetString() : $"error {(int)resp.StatusCode}";
        }
        catch { return $"error {(int)resp.StatusCode}"; }
    }

    /// <summary>Read a response that carries a JSON body on success (e.g. group returns the new id):
    /// <c>(Body, null)</c> on success, <c>(null, error)</c> on failure — exactly one is non-null.</summary>
    public static async Task<(JsonElement? Body, string? Error)> ReadAsync(HttpResponseMessage resp)
    {
        var el = await resp.Content.ReadFromJsonAsync<JsonElement>();
        if (resp.IsSuccessStatusCode) return (el, null);
        return (null, el.TryGetProperty("error", out var e) ? e.GetString() : $"error {(int)resp.StatusCode}");
    }
}
