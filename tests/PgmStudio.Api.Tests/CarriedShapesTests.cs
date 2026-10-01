using System.Text.Json;

namespace PgmStudio.Api.Tests;

/// <summary>
/// A field held as raw JSON publishes the type it holds (<c>PgmStudio.Api.Endpoints.CarriedShapes</c>).
///
/// <para>A layout's dressing and biome are raw JSON in the record, because their types live in a project
/// the layout's cannot reach. What is asserted is the document a caller reads: the field names its type, the
/// type is published whole, and the field keeps the description its own record gives it.</para>
///
/// <para>Runs against the <c>pgm_studio_test</c> schema, so it runs serially with the other DB suites.</para>
/// </summary>
[NotInParallel("api-db")]
public sealed class CarriedShapesTests
{
    [Test]
    [Arguments("SketchLayout", "dressing", "DressingDoc")]
    [Arguments("SketchLayout", "biome", "BiomeField")]
    public async Task A_raw_field_publishes_the_type_it_holds(string record, string field, string shape)
    {
        var schemas = (await DocumentAsync()).GetProperty("components").GetProperty("schemas");
        var published = schemas.GetProperty(record).GetProperty("properties").GetProperty(field);

        await Assert.That(Referenced(published)).IsEqualTo(shape)
            .Because($"{record}.{field} holds a {shape}, and the document says {published.GetRawText()}");
        await Assert.That(schemas.TryGetProperty(shape, out _)).IsTrue();
        await Assert.That(published.TryGetProperty("description", out _)).IsTrue()
            .Because("the field keeps what its own record says of it");
    }

    /// <summary>The type a field's schema names, however the generator wrapped it: a direct reference, or the
    /// one entry of a <c>oneOf</c> beside <c>nullable</c>.</summary>
    private static string? Referenced(JsonElement field)
    {
        if (field.TryGetProperty("$ref", out var direct)) return direct.GetString()!.Split('/')[^1];
        if (!field.TryGetProperty("oneOf", out var choices)) return null;
        return choices.EnumerateArray()
            .Select(choice => choice.TryGetProperty("$ref", out var reference) ? reference.GetString() : null)
            .SingleOrDefault(reference => reference is not null)?.Split('/')[^1];
    }

    private static async Task<JsonElement> DocumentAsync()
    {
        using var client = ApiTestFactory.Shared.CreateClient();
        return JsonDocument.Parse(await client.GetStringAsync("/api/openapi/v1.json")).RootElement.Clone();
    }
}
