using System.Reflection;
using System.Text.Json.Serialization;
using Namotion.Reflection;
using NJsonSchema;
using NJsonSchema.Generation;
using PgmStudio.Minecraft.Dressing;
using PgmStudio.Minecraft.Painting;
using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Api.Endpoints;

/// <summary>How a raw field holds its type: the whole field is one, it maps keys to them, or it lists them.</summary>
internal enum CarriedForm { Whole, Values, Items }

/// <summary>A field declared as raw JSON, and the type it holds.</summary>
/// <param name="Declaring">The record the field is declared on.</param>
/// <param name="Property">The field, by its C# name.</param>
/// <param name="Shape">The type the field's JSON is read as, wherever the studio reads it.</param>
/// <param name="Form">Whether the field is one of <paramref name="Shape"/>, a map of them, or a list.</param>
internal sealed record Carried(Type Declaring, string Property, Type Shape, CarriedForm Form = CarriedForm.Whole);

/// <summary>
/// Publishes a field held as raw JSON as the type it holds.
///
/// <para>A field is raw JSON where its type lives in a project the declaring one cannot reach: a layout is
/// declared in <c>Pgm</c>, and its dressing and biome are <c>Minecraft</c>'s. Left alone the document says
/// nothing under such a field, so a reader who starts from the document finds a hole where the part routes
/// publish a whole model. The composition root reaches both projects, so the reference is made here, and the
/// field keeps its own description.</para>
/// </summary>
internal sealed class CarriedShapes : ISchemaProcessor
{
    internal static readonly IReadOnlyList<Carried> All =
    [
        new(typeof(SketchLayout), nameof(SketchLayout.Dressing), typeof(DressingDoc)),
        new(typeof(SketchLayout), nameof(SketchLayout.Biome), typeof(BiomeField)),
    ];

    public void Process(SchemaProcessorContext context)
    {
        // The processors also see every schema that only points at a type already generated; the fields are
        // on the one it points at.
        if (context.Schema.HasReference) return;
        foreach (var carried in All)
        {
            if (carried.Declaring != context.ContextualType.Type) continue;
            var property = carried.Declaring.GetProperty(carried.Property)
                           ?? throw new InvalidOperationException(
                               $"{carried.Declaring.Name} declares no {carried.Property}");
            if (!context.Schema.Properties.TryGetValue(SchemaFields.OnTheWire(property), out var field))
                throw new InvalidOperationException(
                    $"{carried.Declaring.Name}.{carried.Property} is not in the published schema");

            var shape = context.Generator.GenerateWithReferenceAndNullability<JsonSchema>(
                carried.Shape.ToContextualType(), isNullable: false, context.Resolver);
            switch (carried.Form)
            {
                case CarriedForm.Values:
                    field.Type = JsonObjectType.Object;
                    field.AdditionalPropertiesSchema = shape;
                    break;
                case CarriedForm.Items:
                    field.Type = JsonObjectType.Array;
                    field.Item = shape;
                    break;
                default:
                    field.OneOf.Clear();
                    field.OneOf.Add(shape);
                    break;
            }
        }
    }
}

/// <summary>What the published schema calls a field.</summary>
internal static class SchemaFields
{
    /// <summary>The name the field crosses under, which is the key the schema holds it by: what the property
    /// states, or the serializer's camelCase.</summary>
    public static string OnTheWire(PropertyInfo property) =>
        property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
        ?? (property.Name.Length == 0
            ? property.Name
            : char.ToLowerInvariant(property.Name[0]) + property.Name[1..]);
}
