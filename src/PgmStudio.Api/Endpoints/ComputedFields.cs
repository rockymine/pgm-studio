using System.Reflection;
using System.Text.Json.Serialization;
using Namotion.Reflection;
using NJsonSchema;
using NSwag.Generation.Processors;
using NSwag.Generation.Processors.Contexts;

namespace PgmStudio.Api.Endpoints;

/// <summary>
/// Publishes a field a record computes rather than stores — a get-only property, which the wire writes like
/// any other — as a read-only member of the record's schema, and marks one the generator already lists the
/// same way.
///
/// <para>The operation generator drops every member without a setter from a record any route takes as a
/// body, and the record's schema is the one every answer names too, so a computed answer such as an intent's
/// <c>gamemodes</c> crossed the wire under no name the document knew. This runs over the finished document,
/// after the operations, and puts each one back. Read-only is the claim a caller acts on: the studio writes
/// the field and reads nothing from it.</para>
/// </summary>
internal sealed class ComputedFields : IDocumentProcessor
{
    public void Process(DocumentProcessorContext context)
    {
        var records = Records();
        foreach (var (name, schema) in context.Document.Components.Schemas)
        {
            if (!records.TryGetValue(name, out var type)) continue;
            if (Holder(schema) is not { } holder) continue;
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (!Computed(property)) continue;
                var wire = SchemaFields.OnTheWire(property);
                if (holder.Properties.TryGetValue(wire, out var published))
                {
                    published.IsReadOnly = true;
                    continue;
                }
                // An override is published on the base that declares it.
                if (property.GetMethod!.GetBaseDefinition().DeclaringType != type) continue;

                var field = context.SchemaGenerator.GenerateWithReferenceAndNullability<JsonSchemaProperty>(
                    property.PropertyType.ToContextualType(), isNullable: false, context.SchemaResolver);
                field.IsReadOnly = true;
                field.Description = property.ToContextualProperty().GetXmlDocsSummary();
                holder.Properties[wire] = field;
            }
        }
    }

    /// <summary>The studio's own types by the short name the document files their schemas under; a name two
    /// types share is left out rather than guessed between.</summary>
    private static Dictionary<string, Type> Records() =>
        AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => assembly.GetName().Name?.StartsWith("PgmStudio.", StringComparison.Ordinal) == true)
            .SelectMany(assembly => assembly.GetExportedTypes())
            .Where(type => !type.IsGenericTypeDefinition && !type.IsEnum && !type.IsInterface)
            .GroupBy(type => type.Name, StringComparer.Ordinal)
            .Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal);

    /// <summary>A public get-only property the serializer writes.</summary>
    private static bool Computed(PropertyInfo property) =>
        property.GetMethod is { IsPublic: true, IsStatic: false }
        && property.SetMethod is null
        && property.GetIndexParameters().Length == 0
        && property.GetCustomAttribute<JsonIgnoreAttribute>() is not { Condition: JsonIgnoreCondition.Always };

    /// <summary>Where the type's own fields are published: the schema itself for a record, the part of its
    /// <c>allOf</c> that is not the base for a record that inherits, and nowhere for a schema that is not an
    /// object — a value the generator renders as a string keeps that shape.</summary>
    private static JsonSchema? Holder(JsonSchema schema) =>
        schema.AllOf.FirstOrDefault(part => !part.HasReference && part.Type.HasFlag(JsonObjectType.Object))
        ?? (schema.Type.HasFlag(JsonObjectType.Object) && schema.AdditionalPropertiesSchema is null ? schema : null);
}
