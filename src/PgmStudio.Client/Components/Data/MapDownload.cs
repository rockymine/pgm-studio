using System.Net.Http.Json;
using Microsoft.JSInterop;
using PgmStudio.Contracts;

namespace PgmStudio.Client.Components;

/// <summary>Saves a map's export to disk: the world ZIP for a map with a sketch, the plain map.xml otherwise.
/// The response is fetched rather than followed as a link, so a refusal comes back as its sentence instead of
/// landing on disk as a file.</summary>
public static class MapDownload
{
    /// <summary>Download <paramref name="slug"/>'s export. Answers null when the file was handed to the browser,
    /// or the sentence saying why not.</summary>
    public static async Task<string?> SaveAsync(HttpClient http, IJSRuntime js, string slug)
    {
        HttpResponseMessage response;
        try { response = await http.GetAsync($"api/map/{slug}/export"); }
        catch (HttpRequestException) { return "Couldn't reach the studio. Check your connection and try again."; }

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

    /// <summary>The sentence a refused request carries, or a plain one where the body is not a refusal.</summary>
    public static async Task<string> RefusalAsync(HttpResponseMessage response)
    {
        try
        {
            var refusal = await response.Content.ReadFromJsonAsync<RefusalDto>();
            if (refusal?.Message is { Length: > 0 } message) return message;
            if (refusal?.Error is { Length: > 0 } label) return label;
        }
        catch (Exception fault) when (fault is System.Text.Json.JsonException or NotSupportedException) { }
        return $"Couldn't download the map (HTTP {(int)response.StatusCode}). Try again.";
    }
}
