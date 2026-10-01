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
            .Where(operation => operation.Success.Count == 0 && !NoBody.Contains(operation.Name)
                                && !operation.Codes.Contains("302"))
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
        "DELETE /api/biome-patterns/{id}",
        "DELETE /api/map/{slug}",
        "DELETE /api/boulder-styles/{id}",
        "DELETE /api/plans/{id}",
        "DELETE /api/porch-styles/{id}",
        "DELETE /api/roof-styles/{id}",
        "DELETE /api/room-styles/{id}",
        "DELETE /api/storey-styles/{id}",
        "DELETE /api/styles/{id}",
        "DELETE /api/tree-styles/{id}",
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

    /// <summary>
    /// <b>A field says what it is, not only what type it is.</b> The schema publishes the docstrings the
    /// records carry, so a field with no <c>&lt;param&gt;</c> reaches a caller as a name and a type — and the
    /// type's own prose doing the fields' work reads as documented while telling an author nothing about the
    /// one field they have to fill.
    ///
    /// <para>Both directions are held to it: the records a driver <b>posts</b>, where the cost of guessing is
    /// a refusal, and the records it <b>reads</b>, where the cost is a caller that cannot tell what it was
    /// given. A route added tomorrow is covered by whichever half it touches.</para>
    /// </summary>
    [Test]
    public async Task Every_field_of_a_crossing_shape_says_what_it_is()
    {
        var document = await DocumentAsync();
        var schemas = document.GetProperty("components").GetProperty("schemas");

        var silent = new List<string>();
        var fields = 0;
        foreach (var name in Posted(document).Concat(Answered(document)).Distinct(StringComparer.Ordinal))
        {
            if (!schemas.TryGetProperty(name, out var schema)) continue;
            if (!schema.TryGetProperty("properties", out var properties)) continue;
            foreach (var field in properties.EnumerateObject())
            {
                fields++;
                if (field.Value.TryGetProperty("description", out _)) continue;
                if (Synthesised.Contains($"{name}.{field.Name}")) continue;
                silent.Add($"{name}.{field.Name}");
            }
        }

        await Assert.That(fields).IsGreaterThan(1000);  // a document that lost its shapes passes vacuously
        await Assert.That(silent.Order(StringComparer.Ordinal)).IsEmpty();
    }

    /// <summary>The count of fields still publishing no shape at all, and it is zero: a field held as raw JSON
    /// names the type it holds (<c>CarriedShapes</c>), and the few whose encoding is open by design are named in
    /// <see cref="Open"/>. A field added as raw JSON with no type named pushes it up and fails here.</summary>
    private const int StillShapeless = 0;

    /// <summary><b>A field says what it holds.</b> A field published with no type is one a generated client types
    /// <c>object</c> and <c>/api-docs</c> offers nothing under, so a caller learns what it holds by being refused
    /// — and a map of them, or a list, is the same hole one level down.</summary>
    [Test]
    public async Task Every_field_says_what_it_holds()
    {
        var schemas = (await DocumentAsync()).GetProperty("components").GetProperty("schemas");

        var shapeless = schemas.EnumerateObject()
            .SelectMany(schema => Fields(schema.Value).Select(field => (Name: $"{schema.Name}.{field.Name}", field.Value)))
            .Where(field => Shapeless(field.Value) && !Open.Contains(field.Name))
            .Select(field => field.Name)
            .Order(StringComparer.Ordinal)
            .ToList();

        await Assert.That(shapeless.Count).IsLessThanOrEqualTo(StillShapeless)
            .Because($"{shapeless.Count} field(s) publish no shape:{Environment.NewLine}  "
                     + string.Join($"{Environment.NewLine}  ", shapeless));
    }

    /// <summary>And the open list stays honest from the other side: a field on it that gains a shape belongs to
    /// the count instead, and one that stops existing would otherwise leave a name nothing checks.</summary>
    [Test]
    public async Task The_fields_open_by_design_publish_no_shape()
    {
        var schemas = (await DocumentAsync()).GetProperty("components").GetProperty("schemas");
        var fields = schemas.EnumerateObject()
            .SelectMany(schema => Fields(schema.Value).Select(field => (Name: $"{schema.Name}.{field.Name}", field.Value)))
            .ToDictionary(field => field.Name, field => field.Value, StringComparer.Ordinal);

        foreach (var name in Open)
        {
            await Assert.That(fields.ContainsKey(name)).IsTrue().Because($"{name} is not in the document");
            await Assert.That(Shapeless(fields[name])).IsTrue().Because($"{name} publishes a shape and is named open");
        }
    }

    /// <summary>The fields whose encoding is open by design. The map document's are the <c>map.xml</c> codec's
    /// own encodings, which <c>MapDocumentDto</c> keeps open rather than walk them a second time; the other three
    /// hold any value a field can.</summary>
    private static readonly string[] Open =
    [
        "MapDocumentDto.kits", "MapDocumentDto.spawners", "MapDocumentDto.renewables",
        "MapDocumentDto.block_drop_rules", "MapDocumentDto.filters", "MapDocumentDto.regions",
        "MapDocumentDto.apply_rules", "MapDocumentDto.destroyables", "MapDocumentDto.cores", "MapDocumentDto.modes",
        "MapSpawnDto.region", "MapWoolDto.location", "MapMonumentDto.location",
        "DocumentEdit.before", "DocumentEdit.value", "MaterialFieldDto.default",
    ];

    /// <summary>A schema's own fields, and those of the part of its <c>allOf</c> that is not its base.</summary>
    private static IEnumerable<JsonProperty> Fields(JsonElement schema)
    {
        if (schema.TryGetProperty("properties", out var own))
            foreach (var field in own.EnumerateObject()) yield return field;
        if (schema.TryGetProperty("allOf", out var parts))
            foreach (var part in parts.EnumerateArray())
                if (part.TryGetProperty("properties", out var inherited))
                    foreach (var field in inherited.EnumerateObject()) yield return field;
    }

    /// <summary>Whether a field's schema says nothing about what it holds — or is a map or a list of such.</summary>
    private static bool Shapeless(JsonElement field)
    {
        string[] shaping = ["type", "$ref", "oneOf", "anyOf", "allOf", "enum", "properties"];
        if (!shaping.Any(key => field.TryGetProperty(key, out _))) return true;
        if (field.TryGetProperty("additionalProperties", out var values) && values.ValueKind == JsonValueKind.Object
            && Shapeless(values)) return true;
        return field.TryGetProperty("items", out var items) && Shapeless(items);
    }

    /// <summary>The count of query words a route reads and does not publish, and it only moves down.</summary>
    private const int StillUnpublishedWords = 0;

    /// <summary>
    /// <b>Every word a route reads off the query string is a parameter the schema publishes.</b> A word read
    /// straight off the request rather than bound to a record reaches no parameter list unless the route
    /// declares it, so a caller reading the schema is told nothing of the knob — the compose feed's
    /// <c>wools</c>, <c>hub</c> and <c>front</c>, a suggestion's <c>box</c>, a probe's nine dimensions.
    ///
    /// <para>The reads are taken from the source (<see cref="EndpointSource"/>), because a route reads a word
    /// only on the path that reaches it and no request reaches them all.</para>
    /// </summary>
    [Test]
    public async Task Every_query_word_a_route_reads_is_published()
    {
        using var client = ApiTestFactory.Shared.CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/api/openapi/v1.json"));
        var published = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var path in document.RootElement.GetProperty("paths").EnumerateObject())
            foreach (var verb in path.Value.EnumerateObject())
            {
                if (verb.Name is "parameters" or "summary" or "description" or "servers") continue;
                published[$"{verb.Name.ToUpperInvariant()} {path.Name}"] =
                    verb.Value.TryGetProperty("parameters", out var parameters)
                        ? parameters.EnumerateArray()
                            .Where(parameter => parameter.GetProperty("in").GetString() == "query")
                            .Select(parameter => parameter.GetProperty("name").GetString()!).ToHashSet()
                        : [];
            }

        var endpoints = EndpointSource.All();
        var routes = endpoints.SelectMany(endpoint => endpoint.Routes).ToList();
        await Assert.That(routes.Count(published.ContainsKey)).IsGreaterThan(200)
            .Because("the source names the routes the document serves");

        var unpublished = endpoints
            .SelectMany(endpoint => endpoint.Routes.Where(published.ContainsKey).SelectMany(route =>
                endpoint.Words.Where(word => !published[route].Contains(word)).Select(word => $"{route} ?{word}")))
            .Distinct().Order(StringComparer.Ordinal).ToList();
        await Assert.That(unpublished.Count).IsLessThanOrEqualTo(StillUnpublishedWords)
            .Because($"{unpublished.Count} query word(s) are read and not published:{Environment.NewLine}  "
                     + string.Join($"{Environment.NewLine}  ", unpublished));
    }

    /// <summary><b>An operation is named after its route</b>, which is what a generated client calls the method:
    /// the verb, the path's words, and <c>by</c> a trailing parameter. Unique, because two operations sharing a
    /// name are one method in a client.</summary>
    [Test]
    [Arguments("PUT", "/api/map/{slug}/source", "putMapSource")]
    [Arguments("GET", "/api/map/{slug}", "getMapBySlug")]
    [Arguments("GET", "/api/map/{slug}/sketch/props", "getMapSketchProps")]
    [Arguments("DELETE", "/api/styles/{id}", "deleteStylesById")]
    public async Task An_operation_is_named_after_its_route(string verb, string path, string name)
    {
        using var client = ApiTestFactory.Shared.CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/api/openapi/v1.json"));
        var names = document.RootElement.GetProperty("paths").EnumerateObject()
            .SelectMany(route => route.Value.EnumerateObject()
                .Where(operation => operation.Value.ValueKind == JsonValueKind.Object
                                    && operation.Value.TryGetProperty("operationId", out _))
                .Select(operation => (Route: $"{operation.Name.ToUpperInvariant()} {route.Name}",
                    Name: operation.Value.GetProperty("operationId").GetString()!)))
            .ToList();

        await Assert.That(names.Single(entry => entry.Route == $"{verb} {path}").Name).IsEqualTo(name);
        await Assert.That(names.Select(entry => entry.Name).Distinct().Count()).IsEqualTo(names.Count)
            .Because("two operations sharing a name are one method in a generated client");
    }

    /// <summary><b>A write is signed in with the studio token, and the document says what that token is.</b>
    /// It is opaque — the studio looks it up — so the scheme is a bearer of no stated format, and every operation
    /// that needs signing in names it.</summary>
    [Test]
    public async Task A_write_is_signed_with_an_opaque_studio_token()
    {
        using var client = ApiTestFactory.Shared.CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/api/openapi/v1.json"));
        var schemes = document.RootElement.GetProperty("components").GetProperty("securitySchemes");

        var scheme = schemes.EnumerateObject().Single();
        await Assert.That(scheme.Name).IsEqualTo("token");
        await Assert.That(scheme.Value.GetProperty("scheme").GetString()).IsEqualTo("bearer");
        await Assert.That(scheme.Value.TryGetProperty("bearerFormat", out _)).IsFalse()
            .Because("a studio token is opaque, not a JWT");

        var signed = document.RootElement.GetProperty("paths").EnumerateObject()
            .SelectMany(route => route.Value.EnumerateObject())
            .Where(operation => operation.Value.ValueKind == JsonValueKind.Object
                                && operation.Value.TryGetProperty("security", out var security)
                                && security.ValueKind == JsonValueKind.Array)
            .SelectMany(operation => operation.Value.GetProperty("security").EnumerateArray())
            .SelectMany(requirement => requirement.EnumerateObject().Select(named => named.Name))
            .ToList();
        await Assert.That(signed.Count).IsGreaterThan(100);
        await Assert.That(signed.Distinct()).IsEquivalentTo(["token"]);
    }

    /// <summary>The count of fields publishing a default the code states, and it only moves up: a default
    /// the code drops leaves the document with it.</summary>
    private const int PublishedDefaults = 149;

    /// <summary><b>A field the code gives a default publishes it</b>, written the way the wire writes it — what
    /// a body that leaves the field out is read as.</summary>
    [Test]
    [Arguments("ControlPointIntent", "size", "7")]
    [Arguments("SketchSetup", "mirror_mode", "\"rot_180\"")]
    [Arguments("Finding", "severity", "\"refusal\"")]
    [Arguments("PlanReference", "opacity", "0.5")]
    public async Task A_stated_default_is_published(string record, string field, string published)
    {
        var schemas = (await DocumentAsync()).GetProperty("components").GetProperty("schemas");
        var fields = Fields(schemas.GetProperty(record)).ToDictionary(entry => entry.Name, entry => entry.Value);

        await Assert.That(fields[field].GetProperty("default").GetRawText()).IsEqualTo(published);
        var count = schemas.EnumerateObject()
            .SelectMany(schema => Fields(schema.Value))
            .Count(entry => entry.Value.TryGetProperty("default", out _));
        await Assert.That(count).IsGreaterThanOrEqualTo(PublishedDefaults);
    }

    /// <summary>The count of write routes whose schema carries no example body, and it only moves down: a
    /// tool document's worked body, routed in its fence, becomes its route's example once a test has posted it
    /// (<c>DocumentedExamples</c>).</summary>
    private const int StillWithoutExample = 85;

    /// <summary><b>Every write route's schema carries a body known to be accepted.</b> An example is what a
    /// reader copies first, so the one the document hands out is one <c>DocumentedBodyTests</c> has posted and
    /// held to the route's schema rather than one written to look right.</summary>
    [Test]
    public async Task Every_write_route_carries_a_body_known_to_be_accepted()
    {
        var document = await DocumentAsync();
        var without = new List<string>();
        foreach (var path in document.GetProperty("paths").EnumerateObject())
            foreach (var verb in path.Value.EnumerateObject())
            {
                if (verb.Name is not ("post" or "put" or "patch")) continue;
                if (!verb.Value.TryGetProperty("requestBody", out var body)) continue;
                var exampled = body.GetProperty("content").EnumerateObject()
                    .Any(media => media.Value.TryGetProperty("examples", out var examples)
                                  && examples.EnumerateObject().Any());
                if (!exampled) without.Add($"{verb.Name.ToUpperInvariant()} {path.Name}");
            }

        await Assert.That(without.Count).IsLessThanOrEqualTo(StillWithoutExample)
            .Because($"{without.Count} write route(s) carry no example body:{Environment.NewLine}  "
                     + string.Join($"{Environment.NewLine}  ", without.Order(StringComparer.Ordinal)));
    }

    /// <summary>The fields with no docstring to read, because they have no declaration: a polymorphic base
    /// publishes a discriminator the generator synthesises, and no property carries it. Named rather than
    /// counted, so a genuinely undocumented field cannot hide behind them.</summary>
    private static readonly string[] Synthesised =
        ["TerrainMaterial.kind", "PlacedProp.kind", "PropStyle.kind", "BiomeField.kind"];

    /// <summary>Every schema a route answers on a 2xx, following the references down — a nested record is
    /// read with the answer that carries it.</summary>
    private static IEnumerable<string> Answered(JsonElement document)
    {
        var found = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in document.GetProperty("paths").EnumerateObject())
            foreach (var verb in path.Value.EnumerateObject())
            {
                if (verb.Name is "parameters" or "summary" or "description" or "servers") continue;
                if (!verb.Value.TryGetProperty("responses", out var responses)) continue;
                foreach (var response in responses.EnumerateObject())
                    if (response.Name.StartsWith('2')) Referenced(response.Value, found);
            }

        // The transitive half: a record named by an answer is answered, and so is everything it carries.
        var schemas = document.GetProperty("components").GetProperty("schemas");
        for (var grew = true; grew;)
        {
            grew = false;
            foreach (var name in found.ToList())
                if (schemas.TryGetProperty(name, out var schema))
                {
                    var nested = new HashSet<string>(StringComparer.Ordinal);
                    Referenced(schema, nested);
                    foreach (var reference in nested) grew |= found.Add(reference);
                }
        }
        return found;
    }

    /// <summary>Every schema a write route reads a body as, following the references down — a request record
    /// is a shape only because something posts it, and the nested records it carries are posted with
    /// it.</summary>
    private static IEnumerable<string> Posted(JsonElement document)
    {
        var found = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in document.GetProperty("paths").EnumerateObject())
            foreach (var verb in path.Value.EnumerateObject())
            {
                if (verb.Name is not ("post" or "put" or "patch")) continue;
                if (verb.Value.TryGetProperty("requestBody", out var body)) Referenced(body, found);
            }
        return found;
    }

    /// <summary>Every schema named anywhere under a node, by following <c>$ref</c>.</summary>
    private static void Referenced(JsonElement node, HashSet<string> into)
    {
        if (node.ValueKind == JsonValueKind.Object)
            foreach (var member in node.EnumerateObject())
                if (member.NameEquals("$ref") && member.Value.GetString() is { } reference)
                    into.Add(reference.Split('/')[^1]);
                else Referenced(member.Value, into);
        else if (node.ValueKind == JsonValueKind.Array)
            foreach (var item in node.EnumerateArray()) Referenced(item, into);
    }

    private static async Task<JsonElement> DocumentAsync()
    {
        using var client = ApiTestFactory.Shared.CreateClient();
        return JsonDocument.Parse(await client.GetStringAsync("/api/openapi/v1.json")).RootElement.Clone();
    }

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
        "POST /api/auth/sign-out",
        "POST /api/users/{uuid}/invite",
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

        // The world read-backs: the eight pictures and one text read that used to exist only behind
        // PgmStudio.RoundTrip's flags. Each is the last read an agent takes — look at what was built.
        ("GET /api/map/{slug}/render/topdown", "image/png"),
        ("GET /api/map/{slug}/render/section", "image/png"),
        ("GET /api/map/{slug}/render/heightmap", "image/png"),
        ("GET /api/map/{slug}/render/heightmap", "text/plain"),
        ("GET /api/map/{slug}/slopes", "text/plain"),
        ("GET /api/map/{slug}/render/surface", "image/png"),
        ("GET /api/map/{slug}/render/traversability", "image/png"),
        ("GET /api/map/{slug}/render/structures", "image/png"),
        ("GET /api/map/{slug}/render/mirror", "image/png"),
        ("GET /api/map/{slug}/render/section", "text/plain"),
        ("GET /api/map/{slug}/column", "text/plain"),
        ("GET /api/map/{slug}/transect", "text/plain"),
        ("GET /api/map/{slug}/walk", "text/plain"),
        ("GET /api/map/{slug}/themes/census", "text/plain"),
        ("GET /api/map/{slug}/stroke", "text/plain"),

        ("GET /api/plans/{id}/ascii", "text/plain"),
        ("GET /api/map/{slug}/plan/ascii", "text/plain"),
        ("GET /api/map/{slug}/plan/flow", "text/plain"),
        ("POST /api/map/{slug}/sketch/dressing", "text/plain"),
        ("POST /api/map/{slug}/sketch/seats", "text/plain"),

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
            .GetProperty("enum").EnumerateArray().Select(value => value.GetString()!).ToList();
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

    /// <summary>
    /// A route that answers a picture beside its JSON says how to ask for one.
    ///
    /// <para>The three words are read straight off the query string rather than bound to a request record,
    /// which is right — a magnification is not part of the question a preview is asked — and it is exactly
    /// why they reach no parameter list unless something puts them there. So the document said a picture
    /// could come back over an empty <c>parameters</c>, which leaves the one instruction an authoring brief
    /// cannot drop — read the schema, not a document — false at the routes that draw a picture.</para>
    ///
    /// <summary>
    /// <b>Every word a read-back's CLI flag names is a query word its route declares.</b>
    /// <c>WorldReadCatalog</c> is one text serving two surfaces — the CLI prints it as <c>--help</c> and each
    /// route publishes it as its own summary — so a flag naming a word the route does not take documents a
    /// form that fails, and names the wrong argument while doing it (<c>B266</c>).
    ///
    /// <para>Read out of the published schema rather than out of the endpoints, because the schema is what an
    /// agent acts on: a word that reaches the document is a word a caller may send.</para>
    /// </summary>
    [Test]
    public async Task Every_read_back_flag_names_a_word_its_route_takes()
    {
        using var client = ApiTestFactory.Shared.CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/api/openapi/v1.json"));

        var checked_ = 0;
        foreach (var read in PgmStudio.Minecraft.Render.WorldReadCatalog.All)
        {
            if (read.Flag is not { Length: > 0 } flag) continue;

            // A flag reads "--topdown --subject …": the leading flag names the read, each `--word` after it
            // names a query word, and the route answers under /api/map/{slug}/<route>.
            var words = flag.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Skip(1).Where(part => part.StartsWith("--", StringComparison.Ordinal))
                .Select(part => part[2..]).ToList();
            if (words.Count == 0) continue;

            var path = $"/api/map/{{slug}}/{read.Route}";
            if (!document.RootElement.GetProperty("paths").TryGetProperty(path, out var route)) continue;

            var declared = route.GetProperty("get").TryGetProperty("parameters", out var parameters)
                ? parameters.EnumerateArray().Select(p => p.GetProperty("name").GetString()).ToHashSet()
                : [];
            checked_++;

            foreach (var word in words)
                await Assert.That(declared.Contains(word)).IsTrue()
                    .Because($"{read.Route}'s flag names --{word}, and the route takes "
                             + $"{string.Join(", ", declared.Order())}");
        }

        await Assert.That(checked_).IsGreaterThan(0).Because("no read-back flag named a query word");
    }

    /// <para>Asserted over the whole surface rather than route by route, for the same reason as everything
    /// else here: a preview added tomorrow inherits the fault by default.</para>
    /// </summary>
    [Test]
    public async Task Every_route_that_also_answers_a_picture_says_how_to_ask_for_one()
    {
        using var client = ApiTestFactory.Shared.CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/api/openapi/v1.json"));

        var checked_ = 0;
        foreach (var path in document.RootElement.GetProperty("paths").EnumerateObject())
            foreach (var verb in path.Value.EnumerateObject())
            {
                if (verb.Name is "parameters" or "summary" or "description" or "servers") continue;
                // A route that answers *both* — the picture is a way of asking for the same answer. One that
                // only ever draws a picture has nothing to ask for and declares no format.
                var success = verb.Value.GetProperty("responses").EnumerateObject()
                    .Where(response => response.Name.StartsWith('2'))
                    .SelectMany(response => response.Value.TryGetProperty("content", out var content)
                        ? content.EnumerateObject().Select(entry => entry.Name) : [])
                    .ToList();
                if (!success.Contains("image/png") || !success.Any(m => m.Contains("json"))) continue;

                var name = $"{verb.Name.ToUpperInvariant()} {path.Name}";
                var parameters = verb.Value.TryGetProperty("parameters", out var declared)
                    ? declared.EnumerateArray().ToDictionary(p => p.GetProperty("name").GetString()!)
                    : [];
                checked_++;

                await Assert.That(parameters.ContainsKey("format"))
                    .IsTrue().Because($"{name} answers a picture and does not declare format");
                await Assert.That(parameters.ContainsKey("scale"))
                    .IsTrue().Because($"{name} answers a picture and does not declare scale");

                // The view names are published as the closed set they are, not as a bare string an agent
                // learns by being refused one. A route drawing a single picture declares no view at all.
                if (!parameters.TryGetValue("view", out var view)) continue;
                var views = view.GetProperty("schema").GetProperty("enum").EnumerateArray()
                    .Select(entry => entry.GetString()).ToList();
                await Assert.That(views.Count).IsGreaterThan(1)
                    .Because($"{name} declares a view parameter over {views.Count} name(s)");
            }

        await Assert.That(checked_).IsEqualTo(7)
            .Because($"{checked_} route(s) answer a picture beside their JSON, and seven do");
    }

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
