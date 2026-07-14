using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PgmStudio.Api.Tests;

/// <summary>
/// POST /api/plan/evaluate — the plan editor's live rule-evaluator score + lint feed. A valid plan returns the
/// summed score, a valid flag, and every fired term (hard-first) with its rule id, subjects and cell-space
/// evidence; a malformed body is answered 400, never 500. The endpoint is DB-free, so a bare host suffices.
/// </summary>
public sealed class PlanEvaluateEndpointTests
{
    [Test]
    public async Task Valid_seed_scores_clean()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var resp = await client.PostAsync("/api/plan/evaluate",
            new StringContent(ReadSeed("base-2wool.plan.json"), Encoding.UTF8, "application/json"));
        await Assert.That(resp.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        foreach (var key in new[] { "score", "valid", "violations" })
            await Assert.That(body.TryGetProperty(key, out _)).IsTrue();

        // an authored positive is well-formed — no hard term fires
        await Assert.That(body.GetProperty("valid").GetBoolean()).IsTrue();
    }

    [Test]
    public async Task Cramped_wool_fires_WL2_with_evidence()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        // A spawn and a wool two cells apart (10 blocks by traversal) violate WL2's 20-block floor. Single-unit
        // symmetry keeps it a bare minimal pair.
        const string plan = """
        { "plan":1, "globals":{"cell":5,"symmetry":"none"},
          "pieces":[ {"id":"spawn","role":"spawn","rect":[0,0,2,2]},
                     {"id":"wool","role":"wool-room","rect":[2,0,2,2]} ],
          "placements":{
            "spawns":[ {"piece":"spawn","at":[1,1],"facing":"front"} ],
            "wools":[ {"piece":"wool","at":[1,1]} ],
            "iron":[] } }
        """;
        var resp = await client.PostAsync("/api/plan/evaluate", new StringContent(plan, Encoding.UTF8, "application/json"));
        await Assert.That(resp.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(body.GetProperty("valid").GetBoolean()).IsFalse();

        var wl2 = body.GetProperty("violations").EnumerateArray().First(v => v.GetProperty("ruleId").GetString() == "WL2");
        await Assert.That(wl2.GetProperty("kind").GetString()).IsEqualTo("hard");
        // the offending pair is named for the canvas highlight
        var subjects = wl2.GetProperty("subjects").EnumerateArray().Select(s => s.GetString()).ToList();
        await Assert.That(subjects).Contains("spawn");
        await Assert.That(subjects).Contains("wool");
        // and it draws itself — a measure line carries the "10 < 20" label
        var evidence = wl2.GetProperty("evidence");
        await Assert.That(evidence.GetArrayLength()).IsGreaterThan(0);
        await Assert.That(evidence.EnumerateArray().Any(e => e.GetProperty("kind").GetString() == "measure")).IsTrue();
    }

    [Test]
    public async Task Malformed_body_is_a_400_not_a_500()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var resp = await client.PostAsync("/api/plan/evaluate", new StringContent("not a plan", Encoding.UTF8, "application/json"));
        await Assert.That(resp.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    private static string ReadSeed(string file)
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            var candidate = Path.Combine(dir, "tools", "seeds", file);
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            dir = Directory.GetParent(dir)?.FullName;
        }
        throw new FileNotFoundException($"seed {file} not found above the test binary");
    }
}
