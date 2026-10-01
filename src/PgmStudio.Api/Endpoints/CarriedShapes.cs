using System.Reflection;
using System.Text.Json.Serialization;
using Namotion.Reflection;
using NJsonSchema;
using NJsonSchema.Generation;
using PgmStudio.Contracts;
using PgmStudio.Geom;
using PgmStudio.Minecraft.Dressing;
using PgmStudio.Minecraft.Houses;
using PgmStudio.Minecraft.Painting;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Api.Endpoints;

/// <summary>How a raw field holds what it holds: the whole field is one, it maps keys to them, or it lists them.</summary>
internal enum CarriedForm { Whole, Values, Items }

/// <summary>How a field's types combine: it is any one of them, or all of them at once — a shape with fields
/// stated beside its own.</summary>
internal enum CarriedJoin { AnyOf, AllOf }

/// <summary>A field declared as raw JSON, and what it holds.</summary>
/// <param name="Declaring">The record the field is declared on.</param>
/// <param name="Property">The field, by its C# name.</param>
/// <param name="Form">Whether the field is one of what it holds, a map of them, or a list.</param>
/// <param name="Shapes">The types its JSON is read as, wherever the studio reads it.</param>
/// <param name="Join">Whether the field is any one of <paramref name="Shapes"/>, or all of them at once.</param>
/// <param name="Null">Whether a stated <c>null</c> is a value of its own, for a field whose type does not say
/// so.</param>
internal sealed record Carried(
    Type Declaring, string Property, CarriedForm Form, Type[] Shapes,
    CarriedJoin Join = CarriedJoin.AnyOf, bool Null = false);

/// <summary>
/// Publishes a field as what its JSON holds where its declared type says something else.
///
/// <para>A field is raw JSON where its type lives in a project the declaring one cannot reach: a layout is
/// declared in <c>Pgm</c> and its finish is <c>Minecraft</c>'s, and an answer in <c>Contracts</c> that carries
/// a whole document cannot name the document's type. Left alone the document says nothing under such a field.
/// The composition root reaches every project, so the reference is made here, and the field keeps its own
/// description. A field whose encoding is open by design — the <c>map.xml</c> codec's, a diff's value — is
/// not here, and <c>SchemaCompletenessTests</c> names each one.</para>
///
/// <para>A converter is the other way a declared type stops saying what crosses: a relief mark's height is a
/// number or a list, a plan's rectangle is four numbers in a row, and an author is a bare name or a record.
/// Those are published here too, as the converter reads them.</para>
/// </summary>
internal sealed class CarriedShapes : ISchemaProcessor
{
    internal static readonly IReadOnlyList<Carried> All =
    [
        // A layout's finish, whose types are Minecraft's.
        new(typeof(SketchLayout), nameof(SketchLayout.Dressing), CarriedForm.Whole, [typeof(DressingDoc)]),
        new(typeof(SketchLayout), nameof(SketchLayout.Biome), CarriedForm.Whole, [typeof(BiomeField)]),
        new(typeof(SketchLayout), nameof(SketchLayout.Themes), CarriedForm.Values, [typeof(TerrainTheme)]),
        new(typeof(SketchRoomStyles), nameof(SketchRoomStyles.Wool), CarriedForm.Whole, [typeof(HouseStyle)], Null: true),
        new(typeof(SketchRoomStyles), nameof(SketchRoomStyles.Spawn), CarriedForm.Whole, [typeof(HouseStyle)], Null: true),
        new(typeof(SketchShape), nameof(SketchShape.Material), CarriedForm.Whole, [typeof(TerrainMaterial)]),

        // The documents an answer carries whole, whose types Contracts cannot name.
        new(typeof(CompiledPlanDto), nameof(CompiledPlanDto.Layout), CarriedForm.Whole, [typeof(SketchLayout)]),
        new(typeof(CompiledPlanDto), nameof(CompiledPlanDto.Intent), CarriedForm.Whole, [typeof(MapIntent)]),
        new(typeof(MapSourceDto), nameof(MapSourceDto.Layout), CarriedForm.Whole, [typeof(SketchLayout)]),
        new(typeof(MapSourceDto), nameof(MapSourceDto.Intent), CarriedForm.Whole, [typeof(MapIntent)]),
        new(typeof(MapChangeDocumentsDto), nameof(MapChangeDocumentsDto.Plan), CarriedForm.Whole, [typeof(PlanModel)]),
        new(typeof(MapChangeDocumentsDto), nameof(MapChangeDocumentsDto.Refinement), CarriedForm.Whole, [typeof(Refinement)]),
        new(typeof(MapChangeDocumentsDto), nameof(MapChangeDocumentsDto.Layout), CarriedForm.Whole, [typeof(SketchLayout)]),
        new(typeof(MapChangeDocumentsDto), nameof(MapChangeDocumentsDto.Intent), CarriedForm.Whole, [typeof(MapIntent)]),

        // What a refinement states, each as the type it lands in the layout or the intent as — or a name for one.
        new(typeof(Refinement), nameof(Refinement.Materials), CarriedForm.Values, [typeof(TerrainMaterial), typeof(StatedName)]),
        new(typeof(Refinement), nameof(Refinement.Themes), CarriedForm.Values, [typeof(TerrainTheme), typeof(StatedName)]),
        new(typeof(Refinement), nameof(Refinement.Biome), CarriedForm.Whole, [typeof(BiomeField), typeof(StatedName)]),
        new(typeof(Refinement), nameof(Refinement.Dressing), CarriedForm.Whole, [typeof(DressingDoc)]),
        new(typeof(Refinement), nameof(Refinement.AddShapes), CarriedForm.Items,
            [typeof(SketchShape), typeof(ShapeJoin)], CarriedJoin.AllOf),
        new(typeof(Refinement), nameof(Refinement.ShapePropsById), CarriedForm.Values, [typeof(SketchShape)]),
        new(typeof(Refinement), nameof(Refinement.ShapePropsByHeight), CarriedForm.Values, [typeof(SketchShape)]),
        new(typeof(Refinement), nameof(Refinement.Authors), CarriedForm.Items, [typeof(string), typeof(AuthorIntent)]),
        new(typeof(StatedName), nameof(StatedName.Library), CarriedForm.Whole, [typeof(string), typeof(long)]),

        // What a converter reads in more than one shape: a spot height or a ridgeline's, and a person by name or
        // by record.
        new(typeof(ReliefMarkJson), nameof(ReliefMarkJson.Heights), CarriedForm.Whole, [typeof(double), typeof(double[])]),
        new(typeof(PlanMeta), nameof(PlanMeta.Authors), CarriedForm.Items, [typeof(string), typeof(AuthorIntent)]),
        new(typeof(PlanMeta), nameof(PlanMeta.Contributors), CarriedForm.Items, [typeof(string), typeof(AuthorIntent)]),
        new(typeof(MetaIntent), nameof(MetaIntent.Authors), CarriedForm.Items, [typeof(string), typeof(AuthorIntent)]),
        new(typeof(MetaIntent), nameof(MetaIntent.Contributors), CarriedForm.Items, [typeof(string), typeof(AuthorIntent)]),

        // A region's numbers: each a number, or the infinity map.xml spells as a word.
        new(typeof(RegionExtentDto), nameof(RegionExtentDto.MinX), CarriedForm.Whole, [typeof(double), typeof(string)]),
        new(typeof(RegionExtentDto), nameof(RegionExtentDto.MinZ), CarriedForm.Whole, [typeof(double), typeof(string)]),
        new(typeof(RegionExtentDto), nameof(RegionExtentDto.MaxX), CarriedForm.Whole, [typeof(double), typeof(string)]),
        new(typeof(RegionExtentDto), nameof(RegionExtentDto.MaxZ), CarriedForm.Whole, [typeof(double), typeof(string)]),
        new(typeof(RegionNodeDto), nameof(RegionNodeDto.Coords), CarriedForm.Values, [typeof(double), typeof(string)]),
    ];

    /// <summary>The types a converter writes as something other than their members, and what it writes.</summary>
    private static readonly Dictionary<Type, Func<JsonSchema>> Written = new()
    {
        [typeof(CellRect)] = () => new JsonSchema
        {
            Type = JsonObjectType.Array,
            Item = new JsonSchema { Type = JsonObjectType.Integer },
            MinItems = 4,
            MaxItems = 4,
            Description = "A rectangle of cells as `[x, z, w, h]`: its min-corner cell, then how many cells it spans "
                          + "along x and along z.",
        },
    };

    public void Process(SchemaProcessorContext context)
    {
        // The processors also see every schema that only points at a type already generated; the fields are
        // on the one it points at.
        if (context.Schema.HasReference) return;
        if (Written.TryGetValue(context.ContextualType.Type, out var written))
        {
            var shape = written();
            context.Schema.Properties.Clear();
            context.Schema.RequiredProperties.Clear();
            context.Schema.Type = shape.Type;
            context.Schema.Item = shape.Item;
            context.Schema.MinItems = shape.MinItems;
            context.Schema.MaxItems = shape.MaxItems;
            context.Schema.Description = shape.Description;
            return;
        }
        foreach (var carried in All)
        {
            if (carried.Declaring != context.ContextualType.Type) continue;
            var property = carried.Declaring.GetProperty(carried.Property)
                           ?? throw new InvalidOperationException(
                               $"{carried.Declaring.Name} declares no {carried.Property}");
            if (!context.Schema.Properties.TryGetValue(SchemaFields.OnTheWire(property), out var field))
                throw new InvalidOperationException(
                    $"{carried.Declaring.Name}.{carried.Property} is not in the published schema");

            var held = Held(carried, context);
            switch (carried.Form)
            {
                case CarriedForm.Values:
                    field.Type = JsonObjectType.Object;
                    field.AdditionalPropertiesSchema = held;
                    break;
                case CarriedForm.Items:
                    field.Type = JsonObjectType.Array;
                    field.Item = held;
                    break;
                default:
                    field.Type = JsonObjectType.None;
                    field.Item = null;
                    field.OneOf.Clear();
                    field.OneOf.Add(held);
                    if (carried.Null) field.IsNullableRaw = true;
                    break;
            }
        }
    }

    /// <summary>One value of the field: its one type, or its types combined.</summary>
    private static JsonSchema Held(Carried carried, SchemaProcessorContext context)
    {
        var shapes = carried.Shapes
            .Select(shape => context.Generator.GenerateWithReferenceAndNullability<JsonSchema>(
                shape.ToContextualType(), isNullable: false, context.Resolver))
            .ToList();
        if (shapes.Count == 1) return shapes[0];

        var combined = new JsonSchema();
        foreach (var shape in shapes)
            (carried.Join == CarriedJoin.AllOf ? combined.AllOf : combined.AnyOf).Add(shape);
        return combined;
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
