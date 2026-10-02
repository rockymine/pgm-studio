using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using PgmStudio.Api.Endpoints;

namespace PgmStudio.Api.Tests;

/// <summary>
/// Every worked request body in <c>docs/tools/</c>, sent with the verb and to the route its own fence names.
///
/// <para><b>The document is the fixture.</b> Nothing here holds a copy of a body: the blocks are read out of
/// the markdown the API carries (<c>DocumentedBodies</c>) — the same copies it hands out as each operation's
/// examples — the way <c>tools/deriver/figure-check.cs</c> pushes <c>model.md</c>'s ASCII figures through the
/// classifier that names them. A doc carrying an example the API stopped accepting fails
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
/// <para>The fence carries the verb and the route — <c>```json POST /api/terrain/material-preview</c> — or, for
/// a block that is part of a larger body or an answer, the shape it is: <c>```json SketchLayout</c>. Markdown
/// renderers take the first token as the language and ignore the rest, so the block still highlights as
/// JSON.</para>
/// </summary>
[NotInParallel("api-db")]
public sealed class DocumentedBodyTests
{
    /// <summary>Every example, sent. A 2xx is the assertion: these are documents a reader is being told
    /// will work, so anything else is the document lying, whichever side moved.</summary>
    [Test]
    [MethodDataSource(nameof(Examples))]
    public async Task A_documented_body_is_accepted(DocumentedBodies.Block example)
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();

        var route = example.Route!;
        if (route.Contains("{slug}"))
        {
            var slug = await OriginateMapAsync(client);
            route = route.Replace("{slug}", slug);
            if (route.Contains("/notes/{id}")) route = route.Replace("{id}", await NoteAsync(client, slug));
            if (route.Contains("/changes/{number}")) route = route.Replace("{number}", await ChangeAsync(client, slug));
        }
        if (route == "/api/notes/handoff") await NoteAsync(client, await OriginateMapAsync(client));

        using var request = new HttpRequestMessage(new HttpMethod(example.Verb!), route)
        {
            Content = new StringContent(example.Json, Encoding.UTF8, "application/json"),
        };
        var resp = await client.SendAsync(request);

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
            "/api/plan/compile", new StringContent(plan.Json, Encoding.UTF8, "application/json"));
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

    /// <summary>A note on that map, for the routes that answer in a note's thread.</summary>
    private static async Task<string> NoteAsync(HttpClient client, string slug)
    {
        var written = await client.PostAsync($"/api/map/{slug}/notes",
            new StringContent("{\"body\":\"documented-body\",\"anchor\":{\"kind\":\"map\"}}", Encoding.UTF8, "application/json"));
        return (await written.Content.ReadFromJsonAsync<Written>())!.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private sealed record Written(long Id);

    /// <summary>The map's latest change, for the routes that name one.</summary>
    private static async Task<string> ChangeAsync(HttpClient client, string slug)
    {
        var listed = await client.GetFromJsonAsync<ChangeList>($"/api/map/{slug}/changes");
        return listed!.Changes[^1].Number.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private sealed record ChangeList(IReadOnlyList<Change> Changes);

    private sealed record Change(long Number);

    /// <summary>Every block a document sends to a route.</summary>
    public static IEnumerable<DocumentedBodies.Block> Examples() =>
        DocumentedBodies.All.Where(block => block.Route is not null);

    /// <summary><b>Every block says what it is.</b> A block that names neither a route nor a shape is held to
    /// nothing, and is exactly the example that goes quietly wrong.</summary>
    [Test]
    public async Task Every_documented_block_names_a_route_or_a_shape()
    {
        await Assert.That(DocumentedBodies.All.Count).IsGreaterThanOrEqualTo(40);
        await Assert.That(DocumentedBodies.All.Where(block => block.Verb is null && block.Shape is null)
            .Select(block => block.Where)).IsEmpty();
    }

    /// <summary>A block that names a shape is that shape: every value validates against it and every key is one
    /// it declares. These are the documents that are parts of larger bodies, and answers.</summary>
    [Test]
    public async Task A_documented_block_is_the_shape_its_fence_names()
    {
        var document = await DocumentAsync();
        var untrue = new List<string>();
        foreach (var block in DocumentedBodies.All.Where(block => block.Shape is not null))
        {
            if (!document.Components.Schemas.TryGetValue(block.Shape!, out var schema))
            {
                untrue.Add($"{block} names a shape the schema does not publish");
                continue;
            }
            untrue.AddRange(SchemaTruth.Untrue(block.Json, schema).Select(said => $"{block} {said}"));
        }
        await Assert.That(untrue.Count).IsEqualTo(0)
            .Because(string.Join(Environment.NewLine, untrue));
    }

    /// <summary>And a body sent to a route is what that route's schema says it takes — accepted is not enough,
    /// because a route that complains about a field it does not read still answers 2xx.</summary>
    [Test]
    public async Task A_documented_body_is_what_its_route_takes()
    {
        var document = await DocumentAsync();
        var untrue = new List<string>();
        foreach (var block in Examples())
        {
            var operation = document.Paths.TryGetValue(block.Path!, out var route)
                            && route.TryGetValue(block.Verb!.ToLowerInvariant(), out var found) ? found : null;
            if (operation?.RequestBody?.Content.FirstOrDefault(entry => entry.Key.Contains("json")).Value?.Schema
                is not { } schema)
            {
                untrue.Add($"{block} is sent to a route that publishes no JSON body");
                continue;
            }
            untrue.AddRange(SchemaTruth.Untrue(block.Json, schema).Select(said => $"{block} {said}"));
        }
        await Assert.That(untrue.Count).IsEqualTo(0)
            .Because(string.Join(Environment.NewLine, untrue));
    }

    private static async Task<NSwag.OpenApiDocument> DocumentAsync()
    {
        using var client = ApiTestFactory.Shared.CreateClient();
        return await NSwag.OpenApiDocument.FromJsonAsync(await client.GetStringAsync("/api/openapi/v1.json"));
    }

    private sealed record Originated(string Slug);
    private sealed record CompileBody(IReadOnlyList<string>? Warnings);
}
