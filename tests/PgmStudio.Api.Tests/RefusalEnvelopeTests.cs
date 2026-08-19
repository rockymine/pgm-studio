using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace PgmStudio.Api.Tests;

/// <summary>
/// Every way the API says no says it in one shape: <c>{error, message, findings[]}</c>, and every finding
/// carries a rule id <c>GET /api/rules</c> can explain.
///
/// <para>The point of one envelope is that a caller writes one parser for a refusal, and that holds only while
/// every route keeps it — a single route answering a bare <c>{error}</c>, FastEndpoints' own <c>{errors}</c>
/// or a bodiless 404 costs the caller the guarantee, whatever the other hundred and sixty do. So the
/// assertion is over the <em>kinds</em> of failure rather than over one route: a request that cannot be read,
/// a subject that does not exist, a parameter outside a closed set, a world the studio will not fetch, and a
/// board with nothing on it.</para>
/// </summary>
[NotInParallel("api-db")]
public sealed class RefusalEnvelopeTests
{
    [Test]
    [Arguments("GET", "/api/map/no-such-map/sketch", null, 404, "RQ4")]
    [Arguments("POST", "/api/map/import-folder", """{"folder":"no-such-world"}""", 404, "RQ4")]
    [Arguments("POST", "/api/map/import-url", """{"url":"http://example.com/w.zip"}""", 400, "RQ1")]
    [Arguments("POST", "/api/map/import-url", """{"url":"https://not-allowed.example/w.zip"}""", 403, "IM1")]
    [Arguments("POST", "/api/terrain/material-preview?format=png&view=nope",
               """{"kind":"solid","id":1,"data":0}""", 400, "RQ1")]
    public async Task Every_refusal_answers_the_envelope(
        string verb, string route, string? body, int status, string rule)
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();

        var request = new HttpRequestMessage(new HttpMethod(verb), route);
        if (body is not null) request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        var response = await client.SendAsync(request);

        await Assert.That((int)response.StatusCode).IsEqualTo(status)
            .Because($"{verb} {route} answered {await response.Content.ReadAsStringAsync()}");
        await Envelope(response, rule);
    }

    [Test]
    public async Task Finishing_a_board_with_nothing_drawn_is_refused_by_name()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();

        var slug = (await (await client.PostAsJsonAsync("/api/sketch", new { name = "Blank" }))
            .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("slug").GetString()!;

        var finished = await client.PostAsync($"/api/map/{slug}/sketch/finish", null);
        await Assert.That(finished.StatusCode).IsEqualTo(HttpStatusCode.UnprocessableContent);
        await Envelope(finished, "SK7");
    }

    /// <summary>The three keys, in the one order, with a rule id on every finding — and the summary line
    /// carrying the sentences, so a client that renders nothing else still has something to show.</summary>
    private static async Task Envelope(HttpResponseMessage response, string rule)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(body.TryGetProperty("error", out _)).IsTrue();
        await Assert.That(body.GetProperty("message").GetString()).IsNotNullOrEmpty();

        var findings = body.GetProperty("findings").EnumerateArray().ToList();
        await Assert.That(findings.Count).IsGreaterThan(0);
        await Assert.That(findings.Select(finding => finding.GetProperty("rule").GetString()).ToList())
            .Contains(rule).Because($"answered {body}");
        foreach (var finding in findings)
        {
            await Assert.That(finding.GetProperty("rule").GetString()).IsNotNullOrEmpty();
            await Assert.That(finding.GetProperty("severity").GetString()).IsEqualTo("refusal");
        }
    }

    /// <summary>A rule id nothing explains is half a refusal: the id is only worth carrying because
    /// <c>GET /api/rules</c> answers what it means and what to do about it, and a family added at the edge
    /// without a docstring reads as an id the studio made up.</summary>
    [Test]
    public async Task Every_rule_the_edge_cites_is_in_the_catalogue()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();

        var rules = await client.GetFromJsonAsync<JsonElement>("/api/rules");
        var known = rules.EnumerateArray()
            .ToDictionary(r => r.GetProperty("rule").GetString()!, r => r.GetProperty("means").GetString() ?? "");

        foreach (var id in (string[])["RQ1", "RQ2", "RQ3", "RQ4", "RQ5", "RQ6",
                                      "IM1", "IM2", "IM3", "IM4", "IM5", "IM6", "CO1", "SK6", "SK7"])
        {
            await Assert.That(known.ContainsKey(id)).IsTrue().Because($"{id} is cited by an endpoint");
            await Assert.That(known[id]).IsNotNullOrEmpty().Because($"{id} answers no sentence");
        }
    }
}
