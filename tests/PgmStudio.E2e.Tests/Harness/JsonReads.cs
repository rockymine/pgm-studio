using System.Text.Json.Nodes;

namespace PgmStudio.E2e.Tests.Harness;

/// <summary>Null-tolerant reads over an API answer, the way a spec walks a document it did not type.</summary>
public static class JsonReads
{
    /// <summary>The array at <paramref name="key"/>, or an empty one where there is none.</summary>
    public static IReadOnlyList<JsonNode> Items(this JsonNode? node, string key) =>
        node?[key] is JsonArray array ? [.. array.Where(item => item != null).Select(item => item!)] : [];

    /// <summary>The string at <paramref name="key"/>, or null where it is absent or not a string.</summary>
    public static string? Text(this JsonNode? node, string key) =>
        node?[key] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    /// <summary>The number at <paramref name="key"/>, or null where it is absent or not a number.</summary>
    public static double? Number(this JsonNode? node, string key) =>
        node?[key] is JsonValue value && value.TryGetValue<double>(out var number) ? number : null;

    /// <summary>Whether the value at <paramref name="key"/> is JavaScript-truthy.</summary>
    public static bool Truthy(this JsonNode? node, string key) => node?[key] switch
    {
        null => false,
        JsonValue value when value.TryGetValue<bool>(out var flag) => flag,
        JsonValue value when value.TryGetValue<double>(out var number) => number != 0,
        JsonValue value when value.TryGetValue<string>(out var text) => text.Length > 0,
        _ => true,
    };

    /// <summary>The terrain (role-less) shapes of every layer in a sketch layout.</summary>
    public static IReadOnlyList<JsonNode> TerrainShapes(this JsonNode? layout) =>
        [.. layout.Items("layers").SelectMany(layer => layer["layout"].Items("shapes")).Where(shape => !shape.Truthy("role"))];

    /// <summary>A key list the way a spec reports one: <c>["a","b"]</c>.</summary>
    public static string Keys(this JsonNode? node) =>
        "[" + string.Join(",", (node as JsonObject)?.Select(pair => $"\"{pair.Key}\"") ?? []) + "]";
}
