using System.Text.Json.Serialization;

namespace PgmStudio.Client.Pages.Sketch;

// The sketch layout the JS bridge (sketch-bridge.js OnLayout) pushes to the panel — compact:
// render fields + a precomputed dim label, keyed in the bridge's camelCase.

public sealed record SketchShapeRow(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("operation")] string Operation,
    [property: JsonPropertyName("override")] bool Override,
    [property: JsonPropertyName("dim")] string Dim,
    [property: JsonPropertyName("baseHeight")] double BaseHeight = 0,
    [property: JsonPropertyName("floor")] double Floor = 0);

public sealed record SketchIslandRow(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("mirrors")] bool Mirrors,
    [property: JsonPropertyName("shapeIds")] List<string> ShapeIds);

public sealed record SketchLayoutDto(
    [property: JsonPropertyName("islands")] List<SketchIslandRow> Islands,
    [property: JsonPropertyName("shapes")] List<SketchShapeRow> Shapes);

// A shape-library palette entry (from the JS catalog via the bridge's getLibrary): identity + a
// thumbnail (SVG path `d` + viewBox in cell units) the palette renders.
public sealed record LibraryItem(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("thumbD")] string ThumbD,
    [property: JsonPropertyName("thumbVB")] string ThumbVB);
