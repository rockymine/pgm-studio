using System.Text.Json;
using System.Text.Json.Serialization;

namespace PgmStudio.Pgm.Sketch;

/// <summary>
/// The sketch layout wire model — the authoring blob (<c>{setup, layout:{shapes, islands}}</c>) shared by
/// the rasterizer that reads it and the generators that write it. camelCase by default (Web options);
/// snake_case and reserved-word fields carry an explicit name. Kept as the single definition so a
/// generated layout and a hand-drawn one parse through exactly the same shape.
/// </summary>
public sealed class SketchLayout
{
    [JsonPropertyName("setup")]  public SketchSetup? Setup { get; set; }
    [JsonPropertyName("layout")] public SketchShapes? Layout { get; set; }   // legacy single-layer (pre-S7)
    [JsonPropertyName("layers")] public List<SketchLayer>? Layers { get; set; }

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public string ToJson() => JsonSerializer.Serialize(this, Json);
    public static SketchLayout? Parse(string json) => JsonSerializer.Deserialize<SketchLayout>(json, Json);
}

/// <summary>A stacked slab (S7): its shapes/islands at a Y offset. The whole 2-D editor authors one layer;
/// the rasterizer stacks them — a cell's column is the layer's <c>[floor, top]</c> shifted by <c>base_y</c>.</summary>
public sealed class SketchLayer
{
    [JsonPropertyName("id")]     public string? Id { get; set; }
    [JsonPropertyName("name")]   public string? Name { get; set; }
    [JsonPropertyName("base_y")] public double BaseY { get; set; }
    [JsonPropertyName("layout")] public SketchShapes? Layout { get; set; }
}

/// <summary>The mirror mode + centre that fan a mirroring island's shapes onto their orbit images, plus the
/// optional working bounds the editor frames the canvas to (hand-drawn sketches carry it; the rasterizer
/// ignores it and reads only the centre + mode).</summary>
public sealed class SketchSetup
{
    [JsonPropertyName("mirror_mode")] public string MirrorMode { get; set; } = "rot_180";
    [JsonPropertyName("center")]      public SketchCenter? Center { get; set; }
    [JsonPropertyName("bbox")]        public SketchBbox? Bbox { get; set; }
}

public sealed class SketchCenter
{
    [JsonPropertyName("cx")] public double Cx { get; set; }
    [JsonPropertyName("cz")] public double Cz { get; set; }
}

/// <summary>The editor's working bounds — the square the canvas fits to on open.</summary>
public sealed class SketchBbox
{
    [JsonPropertyName("min_x")] public double MinX { get; set; }
    [JsonPropertyName("max_x")] public double MaxX { get; set; }
    [JsonPropertyName("min_z")] public double MinZ { get; set; }
    [JsonPropertyName("max_z")] public double MaxZ { get; set; }
}

public sealed class SketchShapes
{
    [JsonPropertyName("shapes")]  public List<SketchShape> Shapes { get; set; } = [];
    [JsonPropertyName("islands")] public List<SketchIsland> Islands { get; set; } = [];
}

/// <summary>Groups shapes into a landmass and records whether the group is copied onto the mirror.</summary>
public sealed class SketchIsland
{
    [JsonPropertyName("id")]       public string? Id { get; set; }
    [JsonPropertyName("name")]     public string? Name { get; set; }
    [JsonPropertyName("mirrors")]  public bool Mirrors { get; set; } = true;
    [JsonPropertyName("shapeIds")] public List<string> ShapeIds { get; set; } = [];
}

/// <summary>Bézier control points for a polygon edge (the segment leaving / arriving at a vertex).</summary>
public sealed class SketchControl
{
    [JsonPropertyName("in")]  public double[]? In { get; set; }
    [JsonPropertyName("out")] public double[]? Out { get; set; }
}

/// <summary>One shape: a rectangle / circle / polygon (or lasso) with its set-algebra role.</summary>
public sealed class SketchShape
{
    [JsonPropertyName("id")]        public string Id { get; set; } = "";
    [JsonPropertyName("type")]      public string Type { get; set; } = "";
    [JsonPropertyName("operation")] public string Operation { get; set; } = "add";
    [JsonPropertyName("override")]  public bool Override { get; set; }
    [JsonPropertyName("min_x")] public double? MinX { get; set; }
    [JsonPropertyName("min_z")] public double? MinZ { get; set; }
    [JsonPropertyName("max_x")] public double? MaxX { get; set; }
    [JsonPropertyName("max_z")] public double? MaxZ { get; set; }
    [JsonPropertyName("center_x")] public double? CenterX { get; set; }
    [JsonPropertyName("center_z")] public double? CenterZ { get; set; }
    [JsonPropertyName("radius")]   public double? Radius { get; set; }
    [JsonPropertyName("vertices")] public double[][]? Vertices { get; set; }
    [JsonPropertyName("controls")] public Dictionary<string, SketchControl>? Controls { get; set; }

    // Height. Floor = the shape's elevation (where its base sits), BaseHeight = its thickness: the column
    // spans [Floor, Floor + BaseHeight]. For a polygon/lasso whose AnchorHeights line up with its Vertices,
    // the thickness varies per vertex (TIN-interpolated across the footprint). All optional; absent = the
    // flat one-block Y=0 behaviour.
    [JsonPropertyName("base_height")]    public double? BaseHeight { get; set; }
    [JsonPropertyName("anchor_heights")] public double[]? AnchorHeights { get; set; }
    [JsonPropertyName("floor")]          public double? Floor { get; set; }
}
