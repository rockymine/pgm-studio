using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PgmStudio.Api.Tests;

/// <summary>
/// POST /api/plan/inspect — the plan editor's live derived-structure + lint feed. A valid plan body returns
/// findings (with subject ids) and block-space overlay geometry (interfaces / gapLinks / frontline); a
/// malformed body is answered 400, never 500. The endpoint is DB-free, so a bare host suffices.
/// </summary>
public sealed class PlanInspectEndpointTests
{
    [Test]
    public async Task Valid_plan_returns_findings_and_derived_geometry()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var resp = await client.PostAsync("/api/plan/inspect",
            new StringContent(ReadSeed("base-2wool.plan.json"), Encoding.UTF8, "application/json"));
        await Assert.That(resp.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        // the four derived arrays are always present
        foreach (var key in new[] { "findings", "interfaces", "gapLinks", "frontline" })
            await Assert.That(body.TryGetProperty(key, out _)).IsTrue();

        // land interfaces carry a segment + border length
        var interfaces = body.GetProperty("interfaces");
        await Assert.That(interfaces.GetArrayLength()).IsGreaterThan(0);
        var land = interfaces.EnumerateArray().First(s => s.GetProperty("kind").GetString() == "land");
        await Assert.That(land.GetProperty("length").GetInt32()).IsGreaterThanOrEqualTo(10);

        // the bridge zone gap-links the east bar to a wool room, with a segment + hop distance
        var gaps = body.GetProperty("gapLinks");
        await Assert.That(gaps.GetArrayLength()).IsGreaterThan(0);
        await Assert.That(gaps.EnumerateArray().Any(g => g.TryGetProperty("hop", out _) && g.TryGetProperty("x1", out _))).IsTrue();

        await Assert.That(body.GetProperty("frontline").GetArrayLength()).IsGreaterThan(0);
    }

    [Test]
    public async Task Findings_carry_subject_ids()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        // a sliver contact → an error finding naming both pieces
        const string plan = """
        { "plan":1, "globals":{"cell":1},
          "pieces":[ {"id":"a","role":"lane","rect":[0,0,10,9]}, {"id":"b","role":"lane","rect":[10,0,10,10]} ] }
        """;
        var resp = await client.PostAsync("/api/plan/inspect", new StringContent(plan, Encoding.UTF8, "application/json"));
        await Assert.That(resp.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var findings = (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("findings");
        var sliver = findings.EnumerateArray().First(f => f.GetProperty("severity").GetString() == "error");
        var subjects = sliver.GetProperty("subjects").EnumerateArray().Select(s => s.GetString()).ToList();
        await Assert.That(subjects).Contains("a");
        await Assert.That(subjects).Contains("b");
    }

    [Test]
    public async Task Interfaces_carry_wool_room_and_wall_flags()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        // a (piece) — b (wool-room) is a terrain↔room seam; a — c abut and carry a wall mark.
        const string plan = """
        { "plan":1, "globals":{"cell":1},
          "pieces":[ {"id":"a","role":"piece","rect":[0,0,10,20]},
                     {"id":"b","role":"wool-room","rect":[10,0,10,10]},
                     {"id":"c","role":"piece","rect":[10,10,10,10]} ],
          "walls":[ {"a":"a","b":"c"} ] }
        """;
        var resp = await client.PostAsync("/api/plan/inspect", new StringContent(plan, Encoding.UTF8, "application/json"));
        await Assert.That(resp.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var interfaces = (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("interfaces");
        var ab = interfaces.EnumerateArray().First(s =>
            new[] { s.GetProperty("a").GetString(), s.GetProperty("b").GetString() }.Order().SequenceEqual(new[] { "a", "b" }));
        var ac = interfaces.EnumerateArray().First(s =>
            new[] { s.GetProperty("a").GetString(), s.GetProperty("b").GetString() }.Order().SequenceEqual(new[] { "a", "c" }));
        await Assert.That(ab.GetProperty("woolRoom").GetBoolean()).IsTrue();
        await Assert.That(ab.GetProperty("wall").GetBoolean()).IsFalse();
        await Assert.That(ac.GetProperty("wall").GetBoolean()).IsTrue();
    }

    [Test]
    public async Task Malformed_body_is_a_400_not_a_500()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var resp = await client.PostAsync("/api/plan/inspect", new StringContent("not a plan", Encoding.UTF8, "application/json"));
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
