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
[NotInParallel("api-db")]
public sealed class PlanEvaluateEndpointTests
{
    [Test]
    public async Task Valid_seed_scores_clean()
    {
        using var client = ApiTestFactory.Shared.CreateClient();

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
    public async Task The_validators_lint_rides_the_response_beside_the_score()
    {
        using var client = ApiTestFactory.Shared.CreateClient();

        // A piece one block over the base (delta 1, not the even step EL1 wants) is deterministic lint —
        // computed by the same Check the evaluation already runs, and now answered instead of discarded.
        const string plan = """
        { "plan":1, "globals":{"cell":5,"symmetry":"rot_180","surface":9},
          "pieces":[ {"id":"spawn","role":"spawn","rect":[-1,4,2,2],"surface":13},
                     {"id":"step","role":"piece","rect":[-1,0,2,4],"surface":10} ],
          "placements":{ "spawns":[ {"id":"spawn-1","piece":"spawn","at":[1,1],"facing":"front"} ] } }
        """;
        var resp = await client.PostAsync("/api/plan/evaluate",
            new StringContent(plan, Encoding.UTF8, "application/json"));
        await Assert.That(resp.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(body.TryGetProperty("lint", out var lint)).IsTrue();
        var rules = lint.EnumerateArray().Select(f => f.GetProperty("rule").GetString()).ToList();
        await Assert.That(rules).Contains("EL1");
    }

    [Test]
    public async Task Cramped_wool_fires_WL2_with_evidence()
    {
        using var client = ApiTestFactory.Shared.CreateClient();

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

        // The rule and the subjects sit on the violation's finding, which is the shape every gate answers in;
        // the term id and the hard/soft kind stay on the violation, because a term is which measurement
        // noticed rather than what an author broke.
        var wl2 = body.GetProperty("violations").EnumerateArray()
            .First(v => v.GetProperty("finding").GetProperty("rule").GetString() == "WL2");
        await Assert.That(wl2.GetProperty("kind").GetString()).IsEqualTo("hard");
        // the offending pair is named for the canvas highlight
        var subjects = wl2.GetProperty("finding").GetProperty("subjects").EnumerateArray()
            .Select(s => s.GetString()).ToList();
        await Assert.That(subjects).Contains("spawn");
        await Assert.That(subjects).Contains("wool");
        // and it draws itself — a measure line carries the "10 < 20" label
        var evidence = wl2.GetProperty("evidence");
        await Assert.That(evidence.GetArrayLength()).IsGreaterThan(0);
        await Assert.That(evidence.EnumerateArray().Any(e => e.GetProperty("kind").GetString() == "measure")).IsTrue();
    }

    /// <summary>A plan with no geometry is answered rather than refused — a 400 blanked the editor's score
    /// panel on every fresh plan — but the answer was <c>score 0, valid: true</c>, which is the shape of a
    /// perfect plan, about the emptiest document there is. <c>/plan/compile</c> refused the same body with
    /// <c>PL1</c> the whole time. The two now say the same thing, and this asserts they do it in one
    /// sentence: the finding is the validator's, not a second copy of it (B140).</summary>
    [Test]
    public async Task An_empty_plan_is_not_a_perfect_plan()
    {
        using var client = ApiTestFactory.Shared.CreateClient();

        var resp = await client.PostAsync("/api/plan/evaluate",
            new StringContent("""{"pieces": []}""", Encoding.UTF8, "application/json"));
        await Assert.That(resp.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(body.GetProperty("valid").GetBoolean()).IsFalse();
        var violations = body.GetProperty("violations").EnumerateArray().ToList();
        await Assert.That(violations.Any(v => v.GetProperty("termId").GetString() == "PL1")).IsTrue();
        await Assert.That(violations.First(v => v.GetProperty("termId").GetString() == "PL1")
            .GetProperty("finding").GetProperty("message").GetString()).IsNotNull();
    }

    [Test]
    public async Task Malformed_body_is_a_400_not_a_500()
    {
        using var client = ApiTestFactory.Shared.CreateClient();

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
