using System.Text.Json.Serialization;

namespace PgmStudio.Client.Pages.Sketch;

// The sketch layout the JS bridge (sketch-bridge.js OnLayout) pushes to the panel — compact:
// render fields + a precomputed dim label, keyed in the bridge's camelCase.

public sealed record SketchShapeRow(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("operation")] string Operation,
    [property: JsonPropertyName("override")] bool Override,
    [property: JsonPropertyName("dim")] string Dim);

public sealed record SketchIslandRow(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("mirrors")] bool Mirrors,
    [property: JsonPropertyName("shapeIds")] List<string> ShapeIds);

public sealed record SketchLayoutDto(
    [property: JsonPropertyName("islands")] List<SketchIslandRow> Islands,
    [property: JsonPropertyName("shapes")] List<SketchShapeRow> Shapes);
