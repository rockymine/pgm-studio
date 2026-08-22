using System.Reflection;
using System.Text.Json.Serialization;
using NJsonSchema.Generation;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Endpoints;

/// <summary>
/// Publishes a closed word set as the schema <c>enum</c> of the field that carries it.
///
/// <para>A field marked <see cref="WordSetAttribute"/> takes one of a handful of words and crosses as a
/// <c>string</c>, because the words are <c>const string</c>s that <c>Minecraft</c> and <c>Pgm</c> both write
/// and neither can see <c>Contracts</c>. Left alone the document says <c>"type": "string"</c> and a caller
/// learns the words by being refused one; here the same schema says which words, so <c>/api-docs</c> offers
/// them and a generated client types the field.</para>
///
/// <para>The words come off the declaring class rather than being restated, so the schema cannot drift from
/// what the studio accepts. A word set with a <b>label</b> per word — a picker's list — publishes the ids,
/// which are what crosses.</para>
/// </summary>
internal sealed class WordSetSchemas : ISchemaProcessor
{
    public void Process(SchemaProcessorContext context)
    {
        foreach (var property in context.ContextualType.Type
                     .GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (Declared(property) is not { } declaring) continue;
            if (!context.Schema.Properties.TryGetValue(OnTheWire(property), out var schema)) continue;

            schema.Enumeration.Clear();
            foreach (var word in Words.Of(declaring)) schema.Enumeration.Add(word);
        }
    }

    /// <summary>The class whose words this property takes, read off the property or off the record's
    /// positional parameter of the same name — a positional record carries the attribute on the parameter
    /// unless it is written <c>[property: …]</c>, and both spellings mean the same thing.</summary>
    private static Type? Declared(PropertyInfo property) =>
        (property.GetCustomAttribute<WordSetAttribute>()
         ?? Parameter(property)?.GetCustomAttribute<WordSetAttribute>())?.Declaring;

    private static ParameterInfo? Parameter(PropertyInfo property) =>
        property.DeclaringType?.GetConstructors()
            .SelectMany(constructor => constructor.GetParameters())
            .FirstOrDefault(parameter => parameter.Name == property.Name);

    /// <summary>The name the field crosses under, which is the key the schema holds it by: what the property
    /// states, or the serializer's camelCase.</summary>
    private static string OnTheWire(PropertyInfo property) =>
        property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
        ?? (property.Name.Length == 0
            ? property.Name
            : char.ToLowerInvariant(property.Name[0]) + property.Name[1..]);
}
