using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PgmStudio.E2e.Tests.Harness;

/// <summary>What the API answered, whatever its status — the refusal specs read the status rather than throw on it.</summary>
public sealed record ApiAnswer(int Status, string Text, JsonNode? Json);

/// <summary>
/// JSON over HTTP against the app under test. A body is sent as-is when it is a string, serialized when it is
/// anything else, and omitted (with no content type) when it is null.
/// </summary>
public sealed class StudioApi
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(5) };

    private readonly string root;

    public StudioApi(string baseUrl) => root = $"{baseUrl}/api";

    public Task<JsonNode?> Get(string path) => Call(HttpMethod.Get, path);
    public Task<JsonNode?> Post(string path, object? body = null) => Call(HttpMethod.Post, path, body);
    public Task<JsonNode?> Put(string path, object? body) => Call(HttpMethod.Put, path, body);

    /// <summary>Sends the request and parses the answer, throwing with the body on a non-2xx.</summary>
    public async Task<JsonNode?> Call(HttpMethod method, string path, object? body = null)
    {
        var answer = await Raw(method, path, body);
        if (answer.Status is < 200 or > 299)
        {
            var excerpt = answer.Text.Length > 300 ? answer.Text[..300] : answer.Text;
            throw new HttpRequestException($"{method} /api{path} -> {answer.Status}: {excerpt}");
        }
        return answer.Text.Length == 0 ? null : JsonNode.Parse(answer.Text);
    }

    /// <summary>Like <see cref="Call"/>, but hands back the status instead of throwing.</summary>
    public async Task<ApiAnswer> Raw(HttpMethod method, string path, object? body = null)
    {
        using var request = new HttpRequestMessage(method, $"{root}{path}");
        if (body != null)
        {
            var text = body switch
            {
                string raw => raw,
                JsonNode node => node.ToJsonString(),
                _ => JsonSerializer.Serialize(body),
            };
            request.Content = new StringContent(text, Encoding.UTF8);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        }

        using var response = await Http.SendAsync(request);
        var answer = await response.Content.ReadAsStringAsync();
        JsonNode? json = null;
        try { json = answer.Length == 0 ? null : JsonNode.Parse(answer); }
        catch (JsonException) { json = null; }
        return new ApiAnswer((int)response.StatusCode, answer, json);
    }

    /// <summary>Whether <paramref name="baseUrl"/> answers its health route.</summary>
    public static async Task<bool> IsHealthy(string baseUrl)
    {
        try
        {
            using var response = await Http.GetAsync($"{baseUrl}/api/health");
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException) { return false; }
        catch (TaskCanceledException) { return false; }
    }
}
