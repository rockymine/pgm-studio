using System.Text.Json.Nodes;
using NJsonSchema;
using NJsonSchema.Validation;

namespace PgmStudio.Api.Tests;

/// <summary>
/// What a JSON document says that the schema it is held to does not: a value the validator refuses, and a key
/// the schema does not declare.
///
/// <para>The validator alone sees neither half under a polymorphic field — it holds a material to the base
/// every leaf shares, never to the leaf its <c>kind</c> names — and it cannot see an undeclared key at all,
/// because no record closes itself: the studio complains about a field it does not read rather than refusing
/// it. So the document is walked here, through compositions and into the leaf a discriminator names, which is
/// validated whole where it stands. A refinement's name — an object stating <c>library</c> or <c>use</c> —
/// stands for whatever it is stated in, with the fields beside it laid over the copy, so the walk stops
/// there.</para>
/// </summary>
internal static class SchemaTruth
{
    /// <summary>Everything <paramref name="json"/> says that <paramref name="schema"/> does not, one line each.</summary>
    public static IEnumerable<string> Untrue(string json, JsonSchema schema) =>
        Errors(schema.Validate(json, SchemaType.OpenApi3), "#")
            .Concat(Walk(JsonNode.Parse(json), schema, "#"))
            .Distinct();

    private static IEnumerable<string> Errors(ICollection<ValidationError> errors, string at) =>
        Flattened(errors).Select(error => $"{error.Kind} {at}{error.Path?.TrimStart('#')}");

    /// <summary>The validator nests the failures under each part of a composition; the leaves are what was
    /// wrong.</summary>
    private static IEnumerable<ValidationError> Flattened(IEnumerable<ValidationError> errors) =>
        errors.SelectMany(error => error is ChildSchemaValidationError child
            ? Flattened(child.Errors.Values.SelectMany(inner => inner))
            : [error]);

    private static IEnumerable<string> Walk(JsonNode? value, JsonSchema schema, string at)
    {
        var actual = schema.ActualSchema;
        switch (value)
        {
            case JsonObject members:
                if (members.ContainsKey("library") || members.ContainsKey("use")) yield break;
                if (Leaf(actual, members, []) is { } leaf)
                    foreach (var said in Errors(leaf.Validate(members.ToJsonString(), SchemaType.OpenApi3), at))
                        yield return said;

                var declared = new Dictionary<string, JsonSchema>(StringComparer.Ordinal);
                Declare(actual, members, declared, []);
                var values = Values(actual, []);
                if (declared.Count == 0 && values is null) yield break;
                foreach (var (key, child) in members)
                {
                    var under = declared.GetValueOrDefault(key) ?? values;
                    if (under is null) { yield return $"undeclared {at}.{key}"; continue; }
                    foreach (var found in Walk(child, under, $"{at}.{key}")) yield return found;
                }
                break;
            case JsonArray items when Items(actual, []) is { } itemSchema:
                for (var index = 0; index < items.Count; index++)
                    foreach (var found in Walk(items[index], itemSchema, $"{at}[{index}]")) yield return found;
                break;
        }
    }

    /// <summary>The fields a schema declares for this value: its own, its compositions', and — where it is a
    /// polymorphic base — the fields of the leaf the value's discriminator names.</summary>
    private static void Declare(
        JsonSchema schema, JsonObject value, Dictionary<string, JsonSchema> into, HashSet<JsonSchema> seen)
    {
        var actual = schema.ActualSchema;
        if (!seen.Add(actual)) return;
        foreach (var (name, field) in actual.Properties) into.TryAdd(name, field);
        foreach (var part in actual.AllOf.Concat(actual.OneOf).Concat(actual.AnyOf)) Declare(part, value, into, seen);
        if (Named(actual, value) is { } leaf) Declare(leaf, value, into, seen);
    }

    /// <summary>The leaf a polymorphic base, or a composition holding one, hands this value to.</summary>
    private static JsonSchema? Leaf(JsonSchema schema, JsonObject value, HashSet<JsonSchema> seen)
    {
        var actual = schema.ActualSchema;
        if (!seen.Add(actual)) return null;
        return Named(actual, value)
               ?? actual.AllOf.Concat(actual.OneOf).Concat(actual.AnyOf)
                   .Select(part => Leaf(part, value, seen)).FirstOrDefault(found => found is not null);
    }

    private static JsonSchema? Named(JsonSchema actual, JsonObject value) =>
        actual.DiscriminatorObject is { PropertyName: { } discriminatorName } discriminator
        && value[discriminatorName] is JsonValue kind && kind.TryGetValue<string>(out var word)
        && discriminator.Mapping.TryGetValue(word, out var leaf)
            ? leaf.ActualSchema
            : null;

    private static JsonSchema? Values(JsonSchema schema, HashSet<JsonSchema> seen)
    {
        var actual = schema.ActualSchema;
        if (!seen.Add(actual)) return null;
        return actual.AdditionalPropertiesSchema
               ?? actual.AllOf.Concat(actual.OneOf).Select(part => Values(part, seen)).FirstOrDefault(found => found is not null);
    }

    private static JsonSchema? Items(JsonSchema schema, HashSet<JsonSchema> seen)
    {
        var actual = schema.ActualSchema;
        if (!seen.Add(actual)) return null;
        return actual.Item
               ?? actual.AllOf.Concat(actual.OneOf).Select(part => Items(part, seen)).FirstOrDefault(found => found is not null);
    }
}
