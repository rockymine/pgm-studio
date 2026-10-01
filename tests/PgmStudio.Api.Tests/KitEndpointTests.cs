using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using PgmStudio.Api.Endpoints;

namespace PgmStudio.Api.Tests;

/// <summary>
/// <c>GET /api/kit.py</c> — the Python kit the studio writes from its own schema (<c>PythonKit</c>).
///
/// <para>What is asserted is what a caller relies on: the kit is served under the schema's hash and answers
/// <c>304</c> to a caller already holding it; it compiles; its client has a method for every route; its search
/// finds what the schema says; and every worked body the tool documents carry — posted ones and shaped ones
/// alike — rebuilds through its constructors to the same document, so a constructor that drops, renames or
/// refuses a field the studio takes fails here.</para>
///
/// <para>The kit is run with the <c>python3</c> on the path. Runs against the <c>pgm_studio_test</c> schema, so
/// it runs serially with the other DB suites.</para>
/// </summary>
[NotInParallel("api-db")]
public sealed class KitEndpointTests
{
    [Test]
    public async Task The_kit_is_served_under_the_schema_hash_and_a_caller_holding_it_is_answered_304()
    {
        using var client = ApiTestFactory.Shared.CreateClient();
        var served = await client.GetAsync("/api/kit.py");
        await Assert.That(served.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(served.Content.Headers.ContentType?.MediaType).IsEqualTo("text/x-python");
        var tag = served.Headers.ETag?.Tag;
        await Assert.That(tag).IsNotNull();
        await Assert.That(await served.Content.ReadAsStringAsync()).Contains($"SCHEMA = {tag}");

        using var again = new HttpRequestMessage(HttpMethod.Get, "/api/kit.py");
        again.Headers.IfNoneMatch.Add(served.Headers.ETag!);
        await Assert.That((await client.SendAsync(again)).StatusCode).IsEqualTo(HttpStatusCode.NotModified);
    }

    [Test]
    public async Task The_kit_compiles_names_every_route_and_finds_what_the_schema_says()
    {
        var (folder, document) = await KitAsync();
        var operations = document.RootElement.GetProperty("paths").EnumerateObject()
            .Sum(path => path.Value.EnumerateObject().Count(verb => verb.Value.ValueKind == JsonValueKind.Object
                                                                       && verb.Value.TryGetProperty("responses", out _)));

        var run = await PythonAsync(folder, $$"""
            import py_compile, kit
            py_compile.compile("kit.py", doraise=True)
            methods = [name for name in dir(kit.Studio) if not name.startswith("_") and callable(getattr(kit.Studio, name))]
            assert len(methods) == {{operations}}, f"{len(methods)} methods for {{operations}} routes"
            assert kit.find("protection", show=False), "find answered nothing for a word the schema uses"
            assert kit.SolidMaterial(id=12) == {"kind": "solid", "id": 12}
            try:
                kit.StrokeProp(style="dashed")
                raise AssertionError("a word outside the set was accepted")
            except ValueError as refused:
                assert "solid" in str(refused), str(refused)
            print("ok")
            """);
        await Assert.That(run.Exit).IsEqualTo(0).Because(run.Output);
    }

    /// <summary>Every worked body in the tool documents, rebuilt through the kit as the shape its route takes or
    /// its fence names.</summary>
    [Test]
    public async Task Every_documented_body_rebuilds_through_the_kit()
    {
        var (folder, document) = await KitAsync();
        var bodies = new JsonArray();
        foreach (var block in DocumentedBodies.All)
            if ((block.Shape ?? Requested(document, block)) is { } shape)
                bodies.Add(new JsonObject
                {
                    ["where"] = block.ToString(), ["shape"] = shape, ["json"] = JsonNode.Parse(block.Json),
                });
        await Assert.That(bodies.Count).IsGreaterThan(30);
        await File.WriteAllTextAsync(Path.Combine(folder, "bodies.json"), bodies.ToJsonString());

        var run = await PythonAsync(folder, """
            import json, kit
            wrong = []
            for body in json.load(open("bodies.json")):
                try:
                    built = kit.build(body["shape"], body["json"])
                    if built != body["json"]:
                        wrong.append(f"{body['where']}: rebuilt as {json.dumps(built)[:300]}")
                except Exception as refused:
                    wrong.append(f"{body['where']}: {type(refused).__name__} {refused}")
            print("\n".join(wrong) or "ok")
            raise SystemExit(1 if wrong else 0)
            """);
        await Assert.That(run.Exit).IsEqualTo(0).Because(run.Output);
    }

    /// <summary>The schema a routed block's route reads its body as, where the route names one.</summary>
    private static string? Requested(JsonDocument document, DocumentedBodies.Block block)
    {
        if (block.Verb is null || !document.RootElement.GetProperty("paths").TryGetProperty(block.Path!, out var route)
            || !route.TryGetProperty(block.Verb.ToLowerInvariant(), out var operation)
            || !operation.TryGetProperty("requestBody", out var body)) return null;
        foreach (var media in body.GetProperty("content").EnumerateObject())
            if (media.Value.TryGetProperty("schema", out var schema) && schema.TryGetProperty("$ref", out var reference))
                return reference.GetString()!.Split('/')[^1];
        return null;
    }

    private static async Task<(string Folder, JsonDocument Document)> KitAsync()
    {
        using var client = ApiTestFactory.Shared.CreateClient();
        var folder = Directory.CreateTempSubdirectory("pgm-kit-").FullName;
        await File.WriteAllTextAsync(Path.Combine(folder, "kit.py"), await client.GetStringAsync("/api/kit.py"));
        return (folder, JsonDocument.Parse(await client.GetStringAsync("/api/openapi/v1.json")));
    }

    private static async Task<(int Exit, string Output)> PythonAsync(string folder, string script)
    {
        await File.WriteAllTextAsync(Path.Combine(folder, "check.py"), script);
        using var python = Process.Start(new ProcessStartInfo("python3", "check.py")
        {
            WorkingDirectory = folder, RedirectStandardOutput = true, RedirectStandardError = true,
        })!;
        var output = await python.StandardOutput.ReadToEndAsync();
        var errors = await python.StandardError.ReadToEndAsync();
        await python.WaitForExitAsync();
        return (python.ExitCode, output + errors);
    }
}
