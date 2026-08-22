using System.Text.Json;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Tests;

/// <summary>
/// Every operation in the generated document says what it answers — a shape, and the media type it comes
/// back as — and every write route says what it takes.
///
/// <para><b>An undeclared route does not say nothing; it says the wrong thing.</b> An endpoint with no
/// declared response type is published as <b>204 No Content</b>, which is the generator's default and is a
/// claim rather than a silence, and a caller reading the schema to decide how to call something is not left
/// guessing but misled — <c>/api-docs</c> renders the same claim as an expandable route with nothing to
/// expand.</para>
///
/// <para><see cref="NoBody"/> is the short list for which that 204 is true. Every other operation declares
/// what it answers, and the count below holds it there.</para>
///
/// <para>The request side fails the other way: a route with no declared request type publishes no
/// <c>requestBody</c> at all, so the document is silent rather than wrong — and silence is what a caller
/// cannot act on. <see cref="StillUntyped"/> is how many are left, and it only moves down.</para>
///
/// <para>These assert over the <b>whole surface</b> rather than route by route, because the failure is one a
/// new route inherits by default: an endpoint that declares no response type states nothing for the generator
/// to publish, and nothing else fails when it does. A route added tomorrow with no declaration fails here.</para>
/// </summary>
[NotInParallel("api-db")]
public sealed class SchemaCompletenessTests
{
    /// <summary>The count still to declare, and it is zero: every operation on the surface says what it
    /// answers. A route added without a response type pushes it up and fails here, which is the whole point
    /// of a number rather than a list of exceptions.</summary>
    private const int StillUndeclared = 0;

    /// <summary>The <b>success</b> shape, specifically. Every route publishes the refusal envelope from one
    /// place, so a test asking only whether an operation declares anything at all would pass on a surface
    /// that says nothing about what a request actually returns.</summary>
    [Test]
    public async Task Every_operation_declares_what_it_answers()
    {
        var undeclared = (await OperationsAsync())
            .Where(operation => operation.Success.Count == 0 && !NoBody.Contains(operation.Name))
            .Select(operation => operation.Name)
            .ToList();

        await Assert.That(undeclared.Count).IsLessThanOrEqualTo(StillUndeclared)
            .Because($"{undeclared.Count} operation(s) publish a 204 they do not answer:"
                     + $"{Environment.NewLine}  {string.Join($"{Environment.NewLine}  ", undeclared)}");
    }

    /// <summary>The routes that genuinely answer no body, and the only ones for which the default 204 is the
    /// truth. Every one is a delete whose whole answer is that the thing is gone — there is nothing to hand
    /// back that the caller did not already name in the path.</summary>
    private static readonly string[] NoBody =
    [
        "DELETE /api/plans/{id}",
        "DELETE /api/porch-styles/{id}",
        "DELETE /api/roof-styles/{id}",
        "DELETE /api/room-styles/{id}",
        "DELETE /api/storey-styles/{id}",
        "DELETE /api/styles/{id}",
        "DELETE /api/themes/{id}",
    ];

    /// <summary>And the list stays honest from the other side: a route on it that grows a body is a route
    /// whose published 204 has become a fiction, and it would otherwise leave the count silently.</summary>
    [Test]
    public async Task The_routes_that_answer_no_body_publish_only_that()
    {
        var operations = await OperationsAsync();

        foreach (var name in NoBody)
        {
            var operation = operations.FirstOrDefault(operation => operation.Name == name);
            await Assert.That(operation.Name).IsEqualTo(name).Because($"{name} is not in the document");
            await Assert.That(operation.Success).IsEmpty()
                .Because($"{name} answers {string.Join(", ", operation.Success)} and is listed as answering "
                         + "no body");
            await Assert.That(operation.Codes).Contains("204");
        }
    }

    /// <summary>The count of write routes still saying nothing about what they take, and it is falling.
    /// A route that declares no request type publishes no <c>requestBody</c> at all, so <c>/api-docs</c>
    /// offers no field to fill and a generated client types the body <c>object</c> — the caller learns the
    /// shape by being refused. Three of the sixty-seven POST/PUT/PATCH routes are there today, and all three
    /// take a material or a style, whose hierarchy the generator cannot render (`RP41`). The number only
    /// moves down, and a route added without a declaration pushes it up and fails here.</summary>
    private const int StillUntyped = 0;

    /// <summary>The other half of the contract: what a route <b>takes</b>.</summary>
    [Test]
    public async Task Every_write_route_declares_what_it_takes()
    {
        var untyped = (await OperationsAsync())
            .Where(operation => operation.Name.StartsWith("POST") || operation.Name.StartsWith("PUT")
                                || operation.Name.StartsWith("PATCH"))
            .Where(operation => !operation.Takes && !TakesNoBody.Contains(operation.Name))
            .Select(operation => operation.Name)
            .ToList();

        await Assert.That(untyped.Count).IsLessThanOrEqualTo(StillUntyped)
            .Because($"{untyped.Count} write route(s) publish no request shape:"
                     + $"{Environment.NewLine}  {string.Join($"{Environment.NewLine}  ", untyped)}");
    }

    /// <summary>The write routes that read no body at all — everything they act on is in the path. For these
    /// the empty <c>requestBody</c> is the truth rather than a gap, and they are named so the count above
    /// cannot be reached by a route that quietly stopped reading one.</summary>
    private static readonly string[] TakesNoBody =
    [
        "POST /api/map/{slug}/scan-world",
        "POST /api/map/{slug}/sketch/finish",
        "POST /api/plan/{planId}/author",
    ];

    /// <summary>And that list stays honest from the other side: a route on it that grows a declared body has
    /// stopped being bodyless and belongs in the count instead.</summary>
    [Test]
    public async Task The_write_routes_that_read_no_body_declare_none()
    {
        var operations = await OperationsAsync();

        foreach (var name in TakesNoBody)
        {
            var operation = operations.FirstOrDefault(operation => operation.Name == name);
            await Assert.That(operation.Name).IsEqualTo(name).Because($"{name} is not in the document");
            await Assert.That(operation.Takes).IsFalse()
                .Because($"{name} declares a request body and is listed as reading none");
        }
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

    /// <summary>A closed set on the wire is published as the words the wire actually carries. The generator
    /// reads a string enum off its converter attribute but takes the naming policy from the serializer
    /// options, so a document listing <c>Refusal</c> beside a body writing <c>refusal</c> is the failure this
    /// catches — and a generated client would fail against it before anyone read the page.</summary>
    [Test]
    public async Task A_closed_set_is_published_in_the_words_the_wire_carries()
    {
        using var client = ApiTestFactory.Shared.CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/api/openapi/v1.json"));

        var published = document.RootElement
            .GetProperty("components").GetProperty("schemas").GetProperty("Severity")
            .GetProperty("enum").EnumerateArray().Select(value => value.GetString()).ToList();
        var written = Enum.GetValues<Severity>()
            .Select(severity => JsonSerializer.Serialize(severity).Trim('"'))
            .ToList();

        await Assert.That(published).IsEquivalentTo(written);
    }

    /// <param name="Media">Every media type the operation declares, at any status.</param>
    /// <param name="Success">The media types it declares on a 2xx — what a caller actually receives.</param>
    /// <param name="Codes">The status codes it declares at all.</param>
    /// <param name="Takes">Whether the operation publishes a request body.</param>
    private readonly record struct Operation(
        string Name, IReadOnlyList<string> Media, IReadOnlyList<string> Success, IReadOnlyList<string> Codes,
        bool Takes);

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
                operations.Add(new Operation($"{verb.Name.ToUpperInvariant()} {path.Name}", media, success, codes,
                    verb.Value.TryGetProperty("requestBody", out _)));
            }
        return operations;
    }
}
