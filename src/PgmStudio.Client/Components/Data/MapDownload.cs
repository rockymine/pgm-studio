using Microsoft.JSInterop;
using PgmStudio.Contracts;

namespace PgmStudio.Client.Components;

/// <summary>Saves a map's export to disk: the world ZIP for a map with a sketch, the plain map.xml otherwise.
/// The response is fetched rather than followed as a link, so a refusal comes back as its sentence instead of
/// landing on disk as a file.</summary>
public static class MapDownload
{
    /// <summary>The sentence for a request that never reached the studio.</summary>
    public const string Unreachable = "Couldn't reach the studio. Check your connection and try again.";

    /// <summary>Download <paramref name="slug"/>'s export. Answers null when the file was handed to the browser,
    /// or the refusal saying why not.</summary>
    public static async Task<RefusalDto?> SaveAsync(HttpClient http, IJSRuntime js, string slug)
    {
        HttpResponseMessage response;
        try { response = await http.GetAsync($"api/map/{slug}/export"); }
        catch (HttpRequestException) { return ServerRefusal.Unanswered(Unreachable); }

        if (!response.IsSuccessStatusCode) return await RefusalAsync(response);

        var filename = response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
            ?? (response.Content.Headers.ContentType?.MediaType == "application/zip" ? $"{slug}.zip" : $"{slug}.xml");
        var mime = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
        var bytes = await response.Content.ReadAsByteArrayAsync();
        using var stream = new MemoryStream(bytes);
        using var streamRef = new DotNetStreamReference(stream);
        await js.InvokeVoidAsync("studio.downloadStream", filename, streamRef, mime);
        return null;
    }

    /// <summary>The refusal a refused request carries, with a plain sentence where the body is not a refusal.</summary>
    public static Task<RefusalDto> RefusalAsync(HttpResponseMessage response) =>
        ServerRefusal.ReadAsync(response, $"Couldn't download the map (HTTP {(int)response.StatusCode}). Try again.");
}
