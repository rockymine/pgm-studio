using System.Text.Json;

namespace PgmStudio.Api.Tests;

/// <summary>
/// Every operation in the generated document says what it answers — a shape, and the media type it comes
/// back as.
///
/// <para><b>A path and a verb are not a description of a route.</b> A caller reading the schema to decide how
/// to call something learns nothing from an operation with no response content: it cannot tell a JSON route
/// from one that returns a PNG, an ASCII board or a world ZIP without sending the request and looking, which
/// is the one thing having a schema was supposed to remove. <c>/api-docs</c> renders from the same document,
/// so an undeclared route is also a route whose answer a person cannot expand and read.</para>
///
/// <para>These assert over the <b>whole surface</b> rather than route by route, because the failure is one a
/// new route inherits by default: an endpoint that declares no response type states nothing for the generator
/// to publish, and nothing else fails when it does. A route added tomorrow with no declaration fails here.</para>
/// </summary>
[NotInParallel("api-db")]
public sealed class SchemaCompletenessTests
{
    /// <summary>The <b>success</b> shape, specifically. Every route publishes the refusal envelope from one
    /// place, so a test asking only whether an operation declares anything at all would pass on a surface
    /// that says nothing about what a request actually returns.</summary>
    /// <summary>The count still to declare. It only ever goes down: a route added without a response type
    /// pushes it up and fails here, which is the whole point of a number rather than a list of exceptions.</summary>
    private const int StillUndeclared = 94;

    [Test]
    public async Task Every_operation_declares_what_it_answers()
    {
        var undeclared = (await OperationsAsync())
            .Where(operation => operation.Success.Count == 0)
            .Select(operation => operation.Name)
            .ToList();

        await Assert.That(undeclared.Count).IsLessThanOrEqualTo(StillUndeclared)
            .Because($"{undeclared.Count} operation(s) publish a path and a verb and nothing about the "
                     + $"answer:{Environment.NewLine}  {string.Join($"{Environment.NewLine}  ", undeclared)}");
    }

    /// <summary>And the media type is the one the route actually writes. A schema that calls every answer
    /// JSON is worse than one that says nothing, because a caller believes it.</summary>
    [Test]
    public async Task The_routes_that_do_not_answer_json_say_so()
    {
        var operations = await OperationsAsync();

        foreach (var (name, expected) in NotJson)
        {
            var operation = operations.FirstOrDefault(operation => operation.Name == name);
            await Assert.That(operation.Name).IsEqualTo(name).Because($"{name} is not in the document");
            await Assert.That(operation.Success).Contains(expected)
                .Because($"{name} writes {expected} and the document offers {string.Join(", ", operation.Success)}");
        }
    }

    /// <summary>The routes whose answer is not a JSON document, and what each writes. Every one is a read an
    /// author or an agent reaches for directly — a rendered board, a drawn preview, the world itself — which
    /// is exactly why guessing at it from a path is not good enough.</summary>
    private static readonly (string Name, string Media)[] NotJson =
    [
        // Six answer PNG on ?format=png beside their SVG-in-JSON default; the seventh only ever draws one.
        ("POST /api/terrain/material-preview", "image/png"),
        ("POST /api/terrain/theme-preview", "image/png"),
        ("POST /api/terrain/prop-preview", "image/png"),
        ("POST /api/room-styles/preview", "image/png"),
        ("POST /api/room-styles/preview-snapshot", "image/png"),
        ("GET /api/map/{slug}/coverage", "image/png"),
        ("GET /api/plans/{id}/png", "image/png"),

        ("GET /api/plans/{id}/ascii", "text/plain"),
        ("GET /api/map/{slug}/plan/ascii", "text/plain"),
        ("GET /api/map/{slug}/plan/flow", "text/plain"),

        // One route, two worlds: a map that ships its own gets the bare XML, a sketch-originated one gets
        // the world built around it.
        ("GET /api/map/{slug}/xml", "application/xml"),
        ("GET /api/map/{slug}/export", "application/xml"),
        ("GET /api/map/{slug}/export", "application/zip"),
    ];

    /// <summary>Every refusal in the studio is the one envelope, and the middleware in front of the endpoints
    /// guarantees it — so the document says so once for every route rather than leaving a caller to discover
    /// the shape from a failure.</summary>
    [Test]
    public async Task Every_operation_declares_the_refusal_envelope()
    {
        var silent = (await OperationsAsync())
            .Where(operation => !operation.Codes.Contains("400") || !operation.Codes.Contains("500"))
            .Select(operation => operation.Name)
            .ToList();

        await Assert.That(silent).IsEmpty()
            .Because($"{silent.Count} operation(s) do not publish the envelope a refusal arrives in");
    }

    /// <param name="Media">Every media type the operation declares, at any status.</param>
    /// <param name="Success">The media types it declares on a 2xx — what a caller actually receives.</param>
    /// <param name="Codes">The status codes it declares at all.</param>
    private readonly record struct Operation(
        string Name, IReadOnlyList<string> Media, IReadOnlyList<string> Success, IReadOnlyList<string> Codes);

    private static async Task<List<Operation>> OperationsAsync()
    {
        using var client = ApiTestFactory.Shared.CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/api/openapi/v1.json"));

        var operations = new List<Operation>();
        foreach (var path in document.RootElement.GetProperty("paths").EnumerateObject())
            foreach (var verb in path.Value.EnumerateObject())
            {
                if (verb.Name is "parameters" or "summary" or "description" or "servers") continue;
                var media = new List<string>();
                var success = new List<string>();
                var codes = new List<string>();
                if (verb.Value.TryGetProperty("responses", out var responses))
                    foreach (var response in responses.EnumerateObject())
                    {
                        codes.Add(response.Name);
                        if (!response.Value.TryGetProperty("content", out var content)) continue;
                        var types = content.EnumerateObject().Select(entry => entry.Name).ToList();
                        media.AddRange(types);
                        if (response.Name.StartsWith('2')) success.AddRange(types);
                    }
                operations.Add(new Operation($"{verb.Name.ToUpperInvariant()} {path.Name}", media, success, codes));
            }
        return operations;
    }
}
