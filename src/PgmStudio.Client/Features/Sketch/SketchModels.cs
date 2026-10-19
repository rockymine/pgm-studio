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
/// <para><see cref="HeightAuthored"/> says the region's <see cref="BaseHeight"/> is a number the author
/// corrected rather than the one the layout was written with. A <c>building</c> carries no height of its own —
/// the region it stands in is what a group's relief is held against — so its fields read empty.</para>
///
/// <para><see cref="Carried"/> says the piece is an orbit's image rather than the one authored: it is drawn and
/// listed, and never picked.</para>
/// </summary>
public sealed record SketchStructuralRow(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("color")] string? Color,
    [property: JsonPropertyName("piece")] bool Piece,
    [property: JsonPropertyName("baseHeight")] double? BaseHeight,
    [property: JsonPropertyName("heightAuthored")] bool HeightAuthored,
    [property: JsonPropertyName("minX")] double MinX,
    [property: JsonPropertyName("minZ")] double MinZ,
    [property: JsonPropertyName("maxX")] double MaxX,
    [property: JsonPropertyName("maxZ")] double MaxZ,
    [property: JsonPropertyName("carried")] bool Carried = false)
{
    /// <summary>Whether this piece is a region — the shape that carries a height — as against the building
    /// footprint drawn inside one or a play piece that is not a room.</summary>
    public bool IsRegion => !Piece && Role != StructuralRoles.Building;

    /// <summary>Whether the piece turns: a room, its building, a build area, a water lane and a defence chest.</summary>
    public bool Turns => !Piece || Role is PlayPieceKinds.BuildArea or PlayPieceKinds.WaterLane or PlayPieceKinds.Chest;

    /// <summary>Whether the piece is a room's point — where a spawn's team arrives or a wool is dispensed —
    /// which stands where its room does and is neither added nor deleted alone.</summary>
    public bool IsPoint => Role is PlayPieceKinds.SpawnPoint or PlayPieceKinds.WoolSpawn;

    /// <summary>The point at the middle of the piece, in blocks.</summary>
    public (double X, double Z) Middle => ((MinX + MaxX) / 2, (MinZ + MaxZ) / 2);

    /// <summary>The piece's extent in blocks, the way every other selection on this canvas reads one.</summary>
    public string Dim => $"{MaxX - MinX:0.#} × {MaxZ - MinZ:0.#}";

    /// <summary>Who it belongs to: a spawn's team colour, a wool's dye.</summary>
    public string Who => Color ?? "";

    /// <summary>The icon a piece of its kind is shown under, in the inspector and the Layout list.</summary>
    public string Icon => Role switch
    {
        PlayPieceKinds.Destroyable => "gem",
        PlayPieceKinds.Core => "flame",
        PlayPieceKinds.IronCube => "pickaxe",
        PlayPieceKinds.Wall => "brick-wall",
        PlayPieceKinds.Chest => "box",
        PlayPieceKinds.Observer => "eye",
        PlayPieceKinds.SpawnPoint or PlayPieceKinds.WoolSpawn => "crosshair",
        StructuralRoles.Building => "house",
        _ => "square-dashed",
    };

    /// <summary>The name a piece of its kind is shown under, in the inspector and the Layout list.</summary>
    public string Label => Role switch
    {
        StructuralRoles.Spawn => "Spawn region",
        StructuralRoles.WoolRoom => "Wool room",
        StructuralRoles.Building => "Building",
        PlayPieceKinds.Destroyable => "Destroyable",
        PlayPieceKinds.Core => "Core",
        PlayPieceKinds.BuildArea => "Build area",
        PlayPieceKinds.WaterLane => "Water lane",
        PlayPieceKinds.IronCube => "Iron marker",
        PlayPieceKinds.Wall => "Wall",
        PlayPieceKinds.Chest => "Defence chest",
        PlayPieceKinds.Observer => "Observer",
        PlayPieceKinds.SpawnPoint => "Spawn point",
        PlayPieceKinds.WoolSpawn => "Wool spawn",
        _ => Role,
    };
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
