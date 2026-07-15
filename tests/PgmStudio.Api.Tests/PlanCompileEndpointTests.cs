using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PgmStudio.Api.Tests;

/// <summary>
/// POST /api/plan/compile — the plan editor's compile step. A valid plan compiles to the pair the draft
/// pipeline consumes ({ layout, intent }); a plan with structural errors is answered 422 with the error
/// findings; a malformed body is answered 400, never 500. The endpoint is DB-free, so a bare host suffices.
/// </summary>
[NotInParallel("api-db")]
public sealed class PlanCompileEndpointTests
{
    [Test]
    public async Task Valid_plan_compiles_to_a_layout_and_intent()
    {
        await using var factory = new ApiTestFactory();
        using var client = factory.CreateClient();

        var resp = await client.PostAsync("/api/plan/compile",
            new StringContent(ReadSeed("base-2wool.plan.json"), Encoding.UTF8, "application/json"));
        await Assert.That(resp.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(body.TryGetProperty("layout", out var layout)).IsTrue();
        await Assert.That(body.TryGetProperty("intent", out var intent)).IsTrue();

        // The layout is a real sketch blob: setup + shapes + islands.
        await Assert.That(layout.GetProperty("setup").TryGetProperty("mirror_mode", out _)).IsTrue();
        await Assert.That(layout.GetProperty("layout").GetProperty("shapes").GetArrayLength()).IsGreaterThan(0);
        await Assert.That(layout.GetProperty("layout").GetProperty("islands").GetArrayLength()).IsGreaterThan(0);

        // The intent carries teams + spawns + wools fanned from the authored unit.
        await Assert.That(intent.GetProperty("teams").GetArrayLength()).IsGreaterThan(0);
        await Assert.That(intent.GetProperty("spawns").GetArrayLength()).IsGreaterThan(0);
        await Assert.That(intent.GetProperty("wools").GetArrayLength()).IsGreaterThan(0);
    }

    [Test]
    public async Task Structural_errors_block_the_compile_with_422()
    {
        await using var factory = new ApiTestFactory();
        using var client = factory.CreateClient();

        // A different-surface overlap is a structural error → the compile is blocked, findings returned.
        const string plan = """
        { "plan":1, "globals":{"cell":1,"surface":9},
          "pieces":[ {"id":"a","role":"lane","rect":[0,0,10,10]}, {"id":"b","role":"mid","rect":[5,5,10,10],"surface":13} ] }
        """;
        var resp = await client.PostAsync("/api/plan/compile", new StringContent(plan, Encoding.UTF8, "application/json"));
        await Assert.That((int)resp.StatusCode).IsEqualTo(422);

        var findings = (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("findings");
        await Assert.That(findings.GetArrayLength()).IsGreaterThan(0);
        await Assert.That(findings.EnumerateArray().All(f => f.GetProperty("severity").GetString() == "error")).IsTrue();
        var subjects = findings[0].GetProperty("subjects").EnumerateArray().Select(s => s.GetString()).ToList();
        await Assert.That(subjects).Contains("a");
        await Assert.That(subjects).Contains("b");
    }

    [Test]
    public async Task Lint_alone_does_not_block_the_compile()
    {
        await using var factory = new ApiTestFactory();
        using var client = factory.CreateClient();

        // The seeds intentionally trip lint rules (which never block); they must still compile 200.
        var resp = await client.PostAsync("/api/plan/compile",
            new StringContent(ReadSeed("base-2island.plan.json"), Encoding.UTF8, "application/json"));
        await Assert.That(resp.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Malformed_body_is_a_400_not_a_500()
    {
        await using var factory = new ApiTestFactory();
        using var client = factory.CreateClient();

        var resp = await client.PostAsync("/api/plan/compile", new StringContent("not a plan", Encoding.UTF8, "application/json"));
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
