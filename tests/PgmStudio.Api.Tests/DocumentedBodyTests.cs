using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PgmStudio.Api.Tests;

/// <summary>
/// Every worked request body in <c>docs/tools/</c>, posted to the route its own fence names.
///
/// <para><b>The document is the fixture.</b> Nothing here holds a copy of a body: the blocks are read out of
/// the markdown at run time, the same way <c>tools/deriver/figure-check.cs</c> pushes <c>model.md</c>'s ASCII
/// figures through the classifier that names them. A doc carrying an example the API stopped accepting fails
/// this test, and an example added to a document is covered the moment it is written — neither of which is
/// true of a body copied into a test file, which drifts from the prose beside it and says nothing when it
/// does.</para>
///
/// <para>It exists because the alternative was tried and failed inside one session. The seven body shapes
/// these documents describe were each read off the record or the reader that parses it, six were right, and
/// the seventh said the search rectangle was the body when the parse reads it nested under <c>bounds</c> —
/// a mistake made by trusting the endpoint's own refusal message over its parse. Posting them is what found
/// it.</para>
///
/// <para>The fence carries the route: <c>```json POST /api/terrain/material-preview</c>. Markdown renderers
/// take the first token as the language and ignore the rest, so the block still highlights as JSON.</para>
/// </summary>
[NotInParallel("api-db")]
public sealed class DocumentedBodyTests
{
    /// <summary>Every example, posted. A 2xx is the assertion: these are documents a reader is being told
    /// will work, so anything else is the document lying, whichever side moved.</summary>
    [Test]
    [MethodDataSource(nameof(Examples))]
    public async Task A_documented_body_is_accepted(DocumentedBody example)
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();

        var route = example.Route.Contains("{slug}")
            ? example.Route.Replace("{slug}", await OriginateMapAsync(client))
            : example.Route;

        var resp = await client.PostAsync(route, new StringContent(example.Body, Encoding.UTF8, "application/json"));

        await Assert.That(resp.IsSuccessStatusCode)
            .IsTrue()
            .Because($"{example.Where} documents this body for {example.Route}, and it answered "
                     + $"{(int)resp.StatusCode}: {await resp.Content.ReadAsStringAsync()}");
    }

    /// <summary>The one example whose document makes a claim beyond "this is accepted". `plan.md` says its
    /// worked plan compiles with no errors and no warnings, which is a stronger promise than a 200 and the
    /// reason that block is worth carrying at all — a plan showing every element the format has is only
    /// useful as a starting point if it is actually clean.</summary>
    [Test]
    public async Task The_worked_plan_compiles_with_no_warnings()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();
        var plan = Examples().First(e => e.Route == "/api/plan/compile");

        var resp = await client.PostAsync(
            "/api/plan/compile", new StringContent(plan.Body, Encoding.UTF8, "application/json"));
        var body = await resp.Content.ReadFromJsonAsync<CompileBody>();

        await Assert.That(resp.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(body!.Warnings ?? []).IsEmpty()
            .Because("plan.md says this document compiles with no errors and no warnings");
    }

    /// <summary>A guard on the harness rather than on the API: a parser that silently matched nothing would
    /// make every test above pass by having nothing to run.</summary>
    [Test]
    public async Task The_documents_carry_worked_bodies()
    {
        await Assert.That(Examples()).IsNotEmpty();
    }

    /// <summary>A map row for the routes that name one. The examples are about the body, not about what is
    /// stored, so the cheapest real slug will do.</summary>
    private static async Task<string> OriginateMapAsync(HttpClient client)
    {
        var created = await client.PostAsync(
            "/api/sketch", new StringContent("{\"name\":\"documented-body\"}", Encoding.UTF8, "application/json"));
        return (await created.Content.ReadFromJsonAsync<Originated>())!.Slug;
    }

    public static IEnumerable<DocumentedBody> Examples()
    {
        foreach (var path in Directory.EnumerateFiles(DocsRoot(), "*.md").OrderBy(p => p))
        {
            var lines = File.ReadAllLines(path);
            for (var i = 0; i < lines.Length; i++)
            {
                var fence = System.Text.RegularExpressions.Regex.Match(
                    lines[i], @"^```json\s+(GET|POST|PUT|PATCH)\s+(\S+)\s*$");
                if (!fence.Success) continue;

                var body = new StringBuilder();
                for (var j = i + 1; j < lines.Length && !lines[j].StartsWith("```"); j++) body.AppendLine(lines[j]);
                yield return new DocumentedBody(
                    fence.Groups[1].Value, fence.Groups[2].Value, body.ToString(),
                    $"{Path.GetFileName(path)}:{i + 1}");
            }
        }
    }

    /// <summary>The docs sit beside the solution, and a test runs from its own output directory — so the root
    /// is found by walking up to the repository rather than by counting <c>..</c> segments, which breaks the
    /// first time the output path changes depth.</summary>
    private static string DocsRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "docs", "tools")))
            dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new DirectoryNotFoundException(
            "no docs/tools above the test output — the repository layout moved"), "docs", "tools");
    }

    public sealed record DocumentedBody(string Verb, string Route, string Body, string Where)
    {
        /// <summary>What TUnit prints for the case, so a failure names the document and line rather than an
        /// index into a list nobody can map back.</summary>
        public override string ToString() => $"{Where} {Verb} {Route}";
    }

    private sealed record Originated(string Slug);
    private sealed record CompileBody(IReadOnlyList<string>? Warnings);
}
