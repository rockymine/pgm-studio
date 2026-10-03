using System.Text.Json.Serialization;

namespace PgmStudio.Client.Features.Plan;

/// <summary>What the canvas has selected, as the bridge pushes it (<c>plan-bridge.js</c> <c>OnSelect</c>): one
/// piece, zone, box, marker or building, or a count of several. Only the fields its <see cref="Kind"/> uses
/// are filled.</summary>
public sealed class PlanSelection
{
    [JsonPropertyName("kind")] public string Kind { get; set; } = "";
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("role")] public string Role { get; set; } = "";
    [JsonPropertyName("surface")] public int Surface { get; set; }
    [JsonPropertyName("surfaceSet")] public bool SurfaceSet { get; set; }
    [JsonPropertyName("mirrors")] public bool Mirrors { get; set; }
    [JsonPropertyName("markerKind")] public string MarkerKind { get; set; } = "";
    [JsonPropertyName("index")] public int Index { get; set; }
    [JsonPropertyName("piece")] public string Piece { get; set; } = "";
    [JsonPropertyName("at")] public double[]? At { get; set; }
    [JsonPropertyName("facing")] public string Facing { get; set; } = "";
    // Objective-marker structure fields. Null means the author never set it, so the inspector shows the
    // generator's default — a marker states only what it varies.
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("style")] public string? Style { get; set; }
    [JsonPropertyName("materials")] public string? Materials { get; set; }
    [JsonPropertyName("lava")] public int? Lava { get; set; }
    [JsonPropertyName("lavaHeight")] public int? LavaHeight { get; set; }
    [JsonPropertyName("float")] public int? Float { get; set; }
    [JsonPropertyName("leak")] public int? Leak { get; set; }
    [JsonPropertyName("openTop")] public bool? OpenTop { get; set; }
    /// <summary>The building on a role piece, <c>[x, z, w, h]</c> in blocks from the piece's minimum
    /// corner, on a <c>footprint</c> selection.</summary>
    [JsonPropertyName("footprint")] public double[]? Footprint { get; set; }
    [JsonPropertyName("color")] public string? Color { get; set; }
    [JsonPropertyName("boxKind")] public string BoxKind { get; set; } = "";
    [JsonPropertyName("zoneKind")] public string ZoneKind { get; set; } = "";
    [JsonPropertyName("members")] public List<string>? Members { get; set; }
    [JsonPropertyName("membersNamed")] public bool MembersNamed { get; set; }
    /// <summary>How many pieces and zones a <c>multi</c> selection holds.</summary>
    [JsonPropertyName("count")] public int Count { get; set; }
}
