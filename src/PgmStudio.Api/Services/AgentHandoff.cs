using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace PgmStudio.Api.Services;

/// <summary>
/// Handing the author's open notes to an agent: a Claude Code Routine started through its API trigger, named by
/// <c>Notes:Agent:Fire</c> and <c>Notes:Agent:Token</c>. Nothing runs between hand-offs; one hand-off is one
/// session. The last hand-off is remembered for as long as the studio runs, which is what tells a note written
/// since it from one already handed over.
/// </summary>
public sealed class AgentHandoff(IConfiguration configuration, IHttpClientFactory clients)
{
    public const string ClientName = "agent";

    /// <summary>The beta the Routine's <c>/fire</c> route ships under.</summary>
    public const string Beta = "experimental-cc-routine-2026-04-01";

    /// <summary>A hand-off taken: when, and the session it started.</summary>
    public sealed record Handed(DateTime At, string? Session);

    /// <summary>The last hand-off taken, or null before the first.</summary>
    public Handed? Last { get; private set; }

    /// <summary>Whether this studio names an agent to hand notes to.</summary>
    public bool Ready => Fire is not null && Token is not null;

    private string? Fire => configuration["Notes:Agent:Fire"] is { Length: > 0 } url ? url : null;
    private string? Token => configuration["Notes:Agent:Token"] is { Length: > 0 } token ? token : null;

    /// <summary>Start the agent with <paramref name="text"/> as its run's context. Answers the session it started,
    /// or why it could not: the studio names no agent, the service could not be reached, or it refused.</summary>
    public async Task<(string? Session, string? Why)> HandAsync(string text, CancellationToken ct)
    {
        if (Fire is not { } fire || Token is not { } token)
            return (null, "this studio names no agent to hand notes to — set Notes:Agent:Fire and Notes:Agent:Token");
        using var request = new HttpRequestMessage(HttpMethod.Post, fire)
        {
            Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(new { text }), System.Text.Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("anthropic-beta", Beta);
        request.Headers.Add("anthropic-version", "2023-06-01");
        try
        {
            using var answer = await clients.CreateClient(ClientName).SendAsync(request, ct);
            if (!answer.IsSuccessStatusCode)
                return (null, $"the agent's service refused the hand-off with {(int)answer.StatusCode}"
                    + (answer.StatusCode == System.Net.HttpStatusCode.Unauthorized ? " — the token was revoked or regenerated" : ""));
            var fired = await answer.Content.ReadFromJsonAsync<Fired>(ct);
            Last = new Handed(DateTime.UtcNow, fired?.Session);
            return (fired?.Session, null);
        }
        catch (Exception fault) when (fault is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            return (null, "the agent's service could not be reached");
        }
    }

    private sealed record Fired([property: JsonPropertyName("claude_code_session_url")] string? Session);
}
