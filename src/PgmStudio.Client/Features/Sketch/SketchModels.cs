using System.Text.Json.Serialization;

namespace PgmStudio.Client.Features.Sketch;

// The sketch layout the JS bridge (sketch-bridge.js OnLayout) pushes to the panel — compact:
// render fields + a precomputed dim label, keyed in the bridge's camelCase.

public sealed record SketchShapeRow(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("operation")] string Operation,
    [property: JsonPropertyName("override")] bool Override,
    [property: JsonPropertyName("dim")] string Dim,
    [property: JsonPropertyName("baseHeight")] double BaseHeight = 1,   // a shape is never zero-height
    [property: JsonPropertyName("floor")] double Floor = 0,
    // A path's band: half-width, how its edges are drawn, and the seed a rough edge wanders by. Empty on
    // every other kind of shape, which is what the inspector reads to know whether to offer them.
    [property: JsonPropertyName("radius")] double Radius = 0,
    [property: JsonPropertyName("pathEdge")] string PathEdge = "",
    [property: JsonPropertyName("pathSeed")] int PathSeed = 0);

public sealed record SketchIslandRow(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("mirrors")] bool Mirrors,
    [property: JsonPropertyName("shapeIds")] List<string> ShapeIds);

public sealed record SketchLayoutDto(
    [property: JsonPropertyName("islands")] List<SketchIslandRow> Islands,
    [property: JsonPropertyName("shapes")] List<SketchShapeRow> Shapes);

// One shift-marked surface-slope control vertex (from the bridge's OnSlopeControls): the vertex index +
// the height to fit the plane at. Height is mutable so the inspector's per-control input edits it in place
// before Apply.
public sealed class SketchSlopeControl
{
    [JsonPropertyName("idx")] public int Idx { get; set; }
    [JsonPropertyName("height")] public double Height { get; set; }
}

// A stacked layer row (from the bridge's OnLayers): identity + Y offset, for the Layers panel (S7b).
public sealed record SketchLayerRow(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("baseY")] double BaseY);

public sealed record SketchLayersDto(
    [property: JsonPropertyName("active")] string Active,
    [property: JsonPropertyName("layers")] List<SketchLayerRow> Layers);
