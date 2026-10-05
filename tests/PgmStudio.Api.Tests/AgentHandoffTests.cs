using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using PgmStudio.Api.Services;
using PgmStudio.Contracts;
using PgmStudio.Domain;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Tests;

/// <summary>Handing the author's notes to an agent: the Routine's <c>/fire</c> is called as its documentation states
/// it, a refusal says why and records nothing, and a studio naming no agent refuses the route.</summary>
[NotInParallel("api-db")]
public sealed class AgentHandoffTests
{
    private const string Fire = "https://api.anthropic.com/v1/claude_code/routines/trig_test/fire";

    private sealed class Stub(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public HttpRequestMessage? Seen;
        public string? SeenBody;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Seen = request;
            SeenBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(status) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };
        }
    }

    private sealed class Clients(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private static AgentHandoff Agent(Stub stub, bool named = true) => new(
        new ConfigurationBuilder().AddInMemoryCollection(named
            ? new Dictionary<string, string?> { ["Notes:Agent:Fire"] = Fire, ["Notes:Agent:Token"] = "sk-test" }
            : []).Build(),
        new Clients(stub));

    [Test]
    public async Task A_hand_off_fires_the_routine_with_its_token_and_beta_and_keeps_the_session()
    {
        var stub = new Stub(HttpStatusCode.OK,
            """{"type":"routine_fire","claude_code_session_id":"session_1","claude_code_session_url":"https://claude.ai/code/session_1"}""");
        var agent = Agent(stub);

        var (session, why) = await agent.HandAsync("2 open notes", CancellationToken.None);

        await Assert.That(why).IsNull();
        await Assert.That(session).IsEqualTo("https://claude.ai/code/session_1");
        await Assert.That(agent.Last?.Session).IsEqualTo("https://claude.ai/code/session_1");
        await Assert.That(stub.Seen!.RequestUri!.ToString()).IsEqualTo(Fire);
        await Assert.That(stub.Seen.Headers.Authorization!.ToString()).IsEqualTo("Bearer sk-test");
        await Assert.That(stub.Seen.Headers.GetValues("anthropic-beta").Single()).IsEqualTo(AgentHandoff.Beta);
        await Assert.That(stub.SeenBody).IsEqualTo("""{"text":"2 open notes"}""");
    }

    [Test]
    public async Task A_refused_hand_off_says_why_and_records_nothing()
    {
        var agent = Agent(new Stub(HttpStatusCode.Unauthorized, "{}"));

        var (session, why) = await agent.HandAsync("x", CancellationToken.None);

        await Assert.That(session).IsNull();
        await Assert.That(why).Contains("401");
        await Assert.That(agent.Last).IsNull();
    }

    [Test]
    public async Task A_studio_naming_no_agent_is_not_ready_and_says_why()
    {
        var agent = Agent(new Stub(HttpStatusCode.OK, "{}"), named: false);

        var (session, why) = await agent.HandAsync("x", CancellationToken.None);

        await Assert.That(agent.Ready).IsFalse();
        await Assert.That(session).IsNull();
        await Assert.That(why).IsEqualTo("the studio has no agent to hand notes to");
    }

    [Test]
    public async Task A_hand_off_is_taken_once_until_a_note_is_written_or_it_is_asked_again()
    {
        using var client = await SketchBoard.FreshAsync();
        // A note is stored to the second, so it is written a second clear of any hand-off an earlier test took.
        await Task.Delay(TimeSpan.FromSeconds(1.1));
        await client.PostAsJsonAsync($"/api/map/{SketchBoard.Slug}/notes",
            new MapNoteRequest("Waiting.", new NoteAnchorDto(NoteAnchors.Map)));

        var handed = await client.PostAsJsonAsync("/api/notes/handoff", new NoteHandoffRequest());
        var standing = await handed.Content.ReadFromJsonAsync<NoteHandoffDto>();
        await Assert.That(standing!.Ready).IsTrue();
        await Assert.That((standing.Waiting, standing.Fresh)).IsEqualTo((1, 0));
        await Assert.That(standing.Session).IsEqualTo("https://claude.ai/code/session_test");

        using var twice = await client.PostAsJsonAsync("/api/notes/handoff", new NoteHandoffRequest());
        await Assert.That(twice.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
        using var again = await client.PostAsJsonAsync("/api/notes/handoff", new NoteHandoffRequest(Again: true));
        await Assert.That(again.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }
}
