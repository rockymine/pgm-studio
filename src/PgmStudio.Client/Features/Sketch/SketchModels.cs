using System.Text.Json.Serialization;
using PgmStudio.Vocabulary;

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
    // How the top is decided once the group carries a relief (docs/world-export/relief.md §7). Empty is ordinary
    // ground; level / raise / sink make the shape something standing IN the terrain rather than being it.
    [property: JsonPropertyName("heightMode")] string HeightMode = "",
    // How far in from its outline an erected shape eases into the ground it meets. Zero is a sheer face.
    [property: JsonPropertyName("skirt")] double Skirt = 0,
    // Whether the shape's ground joins its group's relief. Empty inherits; hold pins it at its own level and
    // the land is solved to meet it; exclude takes it out of the solve entirely.
    [property: JsonPropertyName("reliefScope")] string ReliefScope = "",
    // A path's band: half-width, how its edges are drawn, and the seed a rough edge wanders by. Empty on
    // every other kind of shape, which is what the inspector reads to know whether to offer them.
    [property: JsonPropertyName("radius")] double Radius = 0,
    [property: JsonPropertyName("strokeEdge")] string StrokeEdge = "",
    [property: JsonPropertyName("strokeSeed")] int StrokeSeed = 0);

/// <summary>The plan piece a click picked (sketch-bridge.js <c>OnStructuralSelected</c>): a spawn or wool
/// room's region, or the building footprint inside one. It is not one of <see cref="SketchShapeRow"/> — the
/// bridge keeps the plan's pieces apart from the shapes an author drew — so the row carries what it states
/// rather than an id to look up.
///
/// <para><see cref="HeightAuthored"/> is the fact the rail exists for. A region's <see cref="BaseHeight"/>
/// tracks the plan's flat surface on every compile until an author corrects it; the flag is what tells the
/// next recompile to carry the stored number forward instead. A <c>building</c> carries no height of its
/// own — the region it stands in is what a group's relief is held against — so its fields read empty.</para>
/// </summary>
public sealed record SketchStructuralRow(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("intentRef")] string? IntentRef,
    [property: JsonPropertyName("color")] string? Color,
    [property: JsonPropertyName("baseHeight")] double? BaseHeight,
    [property: JsonPropertyName("heightAuthored")] bool HeightAuthored,
    [property: JsonPropertyName("minX")] double MinX,
    [property: JsonPropertyName("minZ")] double MinZ,
    [property: JsonPropertyName("maxX")] double MaxX,
    [property: JsonPropertyName("maxZ")] double MaxZ)
{
    /// <summary>Whether this piece is a region — the shape that carries a height and an intent entity — as
    /// against the building footprint drawn inside one.</summary>
    public bool IsRegion => Role != StructuralRoles.Building;

    /// <summary>The piece's extent in blocks, the way every other selection on this canvas reads one.</summary>
    public string Dim => $"{MaxX - MinX:0.#} × {MaxZ - MinZ:0.#}";

    /// <summary>Who it belongs to: a spawn's team, a wool's dye colour. What the canvas label says.</summary>
    public string Who => Role == StructuralRoles.Spawn ? (IntentRef ?? "") : (Color ?? "");
}

public sealed record SketchGroupRow(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("mirrors")] bool Mirrors,
    [property: JsonPropertyName("shapeIds")] List<string> ShapeIds);

public sealed record SketchLayoutDto(
    [property: JsonPropertyName("groups")] List<SketchGroupRow> Groups,
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
