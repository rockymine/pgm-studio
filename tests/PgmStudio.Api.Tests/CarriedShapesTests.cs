using System.Text.Json;

namespace PgmStudio.Api.Tests;

/// <summary>
/// A field held as raw JSON publishes the type it holds (<c>PgmStudio.Api.Endpoints.CarriedShapes</c>).
///
/// <para>A layout's finish is raw JSON in the record, because its types live in a project the layout's cannot
/// reach, and an answer that carries a whole document cannot name the document's type. What is asserted is the
/// document a caller reads: the field names its type, the type is published whole, and the field keeps the
/// description its own record gives it.</para>
///
/// <para>Runs against the <c>pgm_studio_test</c> schema, so it runs serially with the other DB suites.</para>
/// </summary>
[NotInParallel("api-db")]
public sealed class CarriedShapesTests
{
    [Test]
    [Arguments("SketchLayout", "dressing", "DressingDoc")]
    [Arguments("SketchLayout", "biome", "BiomeField")]
    [Arguments("SketchRoomStyles", "wool", "HouseStyle")]
    [Arguments("SketchShape", "material", "TerrainMaterial")]
    [Arguments("CompiledPlanDto", "layout", "SketchLayout")]
    [Arguments("MapSourceDto", "intent", "MapIntent")]
    [Arguments("MapChangeDocumentsDto", "refinement", "Refinement")]
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

    /// <summary>A refinement's registry entry is the thing or a name standing for one, and a shape it draws is a
    /// shape with where it joins stated beside it — each published as both types, combined the way the
    /// refinement combines them.</summary>
    [Test]
    public async Task A_refinement_states_a_thing_or_a_name_and_a_shape_with_where_it_joins()
    {
        var refinement = (await DocumentAsync()).GetProperty("components").GetProperty("schemas")
            .GetProperty("Refinement").GetProperty("properties");

        var theme = refinement.GetProperty("themes").GetProperty("additionalProperties");
        await Assert.That(Named(theme, "anyOf")).IsEquivalentTo(["TerrainTheme", "StatedName"]);
        var drawn = refinement.GetProperty("addShapes").GetProperty("items");
        await Assert.That(Named(drawn, "allOf")).IsEquivalentTo(["SketchShape", "ShapeJoin"]);
    }

    private static List<string> Named(JsonElement schema, string combined) =>
        [.. schema.GetProperty(combined).EnumerateArray().Select(part => part.GetProperty("$ref").GetString()!.Split('/')[^1])];

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
