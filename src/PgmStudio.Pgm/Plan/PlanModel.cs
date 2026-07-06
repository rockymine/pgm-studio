using System.Text.Json;
using System.Text.Json.Serialization;

namespace PgmStudio.Pgm.Plan;

/// <summary>The authorable piece roles. A piece is anonymous by default (<see cref="Piece"/>); meaning is
/// derived from the assembled graph. Two roles carry intent and are kept: <see cref="WoolRoom"/> (the room
/// region — red terrain seams, bedrock floor at export) and <see cref="Spawn"/> (the spawn region — iron
/// inside it auto-renews). Retired role names (<c>lane</c>/<c>hub</c>/<c>mid</c>) and any unknown value map
/// to <see cref="Piece"/>.
///
/// <para>Roles split into two kinds. <b>Generating</b> roles (<see cref="Piece"/>/<see cref="WoolRoom"/>/
/// <see cref="Spawn"/>) produce terrain and participate in connectivity, gameplay, validation and export.
/// <b>Annotation</b> roles (the <see cref="Annotations"/> set) are informational-only marks that produce no
/// terrain and carry no graph or export effect — shown in authoring and render tools only. Two annotation
/// roles exist: <see cref="Buffer"/> — a reserved empty gap covering lane-to-lane spacing, the border
/// reservation, and holes (a hole is an enclosed buffer) — and <see cref="Connector"/>, an attachment-point
/// mark ("other structure attaches / overrides here" — a hub, a frontline, the mid) that, with buffers, lets
/// an author build reusable lane/spawn templates. New non-generating roles are added by extending
/// <see cref="Annotations"/>.</para></summary>
public static class PlanRoles
{
    public const string Piece = "piece";
    public const string WoolRoom = "wool-room";
    public const string Spawn = "spawn";
    public const string Buffer = "buffer";
    public const string Connector = "connector";

    /// <summary>The non-generating annotation roles — marks that document intent (spacing/reserved gaps and
    /// holes via <see cref="Buffer"/>; attachment points via <see cref="Connector"/>) but produce no terrain
    /// and have no gameplay/graph/export effect. Extend this to add more.</summary>
    public static readonly IReadOnlySet<string> Annotations = new HashSet<string> { Buffer, Connector };

    /// <summary>True when the role is an informational-only annotation (never rasterized, never buildable).</summary>
    public static bool IsAnnotation(string? role) => role is not null && Annotations.Contains(role);

    /// <summary>True when the role produces terrain and participates in the graph/export (everything that is
    /// not an <see cref="Annotations">annotation</see>).</summary>
    public static bool IsGenerating(string? role) => !IsAnnotation(role);

    /// <summary>The canonical role for a raw (possibly legacy or empty) value: <c>wool-room</c>, <c>spawn</c>
    /// and the annotations <c>buffer</c>/<c>connector</c> survive; everything else — including
    /// <c>lane</c>/<c>hub</c>/<c>mid</c> — is a plain piece.</summary>
    public static string Canonical(string? role) => role switch
    {
        WoolRoom => WoolRoom,
        Spawn => Spawn,
        Buffer => Buffer,
        Connector => Connector,
        _ => Piece,
    };
}

/// <summary>
/// The plan wire model (<c>*.plan.json</c>) — a mini-layout scale proxy: globals + a single team unit
/// (pieces, zones, placements) authored once, with symmetry fanning the rest. All footprint coordinates are
/// signed integer proxy cells relative to the symmetry centre; heights are blocks. It compiles one-way into a
/// <see cref="Sketch.SketchLayout"/> and a <see cref="Authoring.MapIntent"/>. camelCase by default (Web
/// options); reserved-word and snake-cased fields carry an explicit name.
/// </summary>
public sealed class PlanModel
{
    [JsonPropertyName("plan")]       public int Version { get; set; } = 1;
    [JsonPropertyName("meta")]       public PlanMeta? Meta { get; set; }
    [JsonPropertyName("globals")]    public PlanGlobals Globals { get; set; } = new();
    [JsonPropertyName("pieces")]     public List<PlanPiece> Pieces { get; set; } = [];
    [JsonPropertyName("zones")]      public List<PlanZone> Zones { get; set; } = [];
    [JsonPropertyName("placements")] public PlanPlacements Placements { get; set; } = new();
    [JsonPropertyName("cliffs")]     public List<PlanCliff> Cliffs { get; set; } = [];
    [JsonPropertyName("walls")]      public List<PlanWall> Walls { get; set; } = [];

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public string ToJson() => JsonSerializer.Serialize(this, Json);

    public static PlanModel? Parse(string json)
    {
        var model = JsonSerializer.Deserialize<PlanModel>(json, Json);
        model?.Normalize();
        return model;
    }

    /// <summary>Fold legacy/unknown piece roles down to their canonical value, so plans authored under the
    /// earlier role model (<c>lane</c>/<c>hub</c>/<c>mid</c>) load cleanly as anonymous pieces.</summary>
    private void Normalize()
    {
        foreach (var p in Pieces) p.Role = PlanRoles.Canonical(p.Role);
    }
}

public sealed class PlanMeta
{
    [JsonPropertyName("name")]  public string Name { get; set; } = "";
    [JsonPropertyName("notes")] public string? Notes { get; set; }
}

/// <summary>Board-wide parameters. <see cref="Cell"/> is the blocks-per-proxy-cell scale; <see cref="Surface"/>
/// is the base island height and <see cref="Headroom"/> the build cap above it (cap = surface + headroom).
/// <see cref="ObserverY"/> overrides the derived observer height (default surface + 15).</summary>
public sealed class PlanGlobals
{
    [JsonPropertyName("cell")]       public int Cell { get; set; } = 5;
    [JsonPropertyName("symmetry")]   public string Symmetry { get; set; } = "rot_180";
    [JsonPropertyName("maxPlayers")] public int MaxPlayers { get; set; } = 12;
    [JsonPropertyName("surface")]    public int Surface { get; set; } = 9;
    [JsonPropertyName("headroom")]   public int Headroom { get; set; } = 11;
    [JsonPropertyName("observerY")]  public int? ObserverY { get; set; }
}

/// <summary>A rectangular piece on the proxy grid. <see cref="Rect"/> is <c>[x, z, w, h]</c> in cells (x,z =
/// the min corner). <see cref="Surface"/> overrides the global surface (a plateau). <see cref="Mirrors"/>
/// (default true) marks the piece as fanned by symmetry; an on-axis neutral piece sets it false.</summary>
public sealed class PlanPiece
{
    [JsonPropertyName("id")]      public string Id { get; set; } = "";
    [JsonPropertyName("role")]    public string Role { get; set; } = PlanRoles.Piece;
    [JsonPropertyName("rect")]    public int[] Rect { get; set; } = [0, 0, 0, 0];
    [JsonPropertyName("surface")] public int? Surface { get; set; }
    [JsonPropertyName("mirrors")] public bool? Mirrors { get; set; }

    public bool MirrorsOrDefault => Mirrors ?? true;
}

/// <summary>A build zone: a plain rect (<c>[x, z, w, h]</c> cells) where building is allowed, with optional
/// no-build <see cref="Holes"/> (a rect list in the same units).</summary>
public sealed class PlanZone
{
    [JsonPropertyName("id")]    public string Id { get; set; } = "";
    [JsonPropertyName("rect")]  public int[] Rect { get; set; } = [0, 0, 0, 0];
    [JsonPropertyName("holes")] public List<int[]> Holes { get; set; } = [];
}

/// <summary>The team-0 unit's objective markers; the compiler fans orbit images. Positions are piece-relative
/// cells.</summary>
public sealed class PlanPlacements
{
    [JsonPropertyName("spawns")] public List<SpawnPlacement> Spawns { get; set; } = [];
    [JsonPropertyName("wools")]  public List<WoolPlacement> Wools { get; set; } = [];
    [JsonPropertyName("iron")]   public List<IronPlacement> Iron { get; set; } = [];
}

/// <summary>A spawn on <see cref="Piece"/> at piece-relative cell offset <see cref="At"/>, facing
/// <see cref="Facing"/> — absolute board directions (<c>front</c>=−z, <c>back</c>=+z, <c>left</c>=−x,
/// <c>right</c>=+x), fanned per orbit image. The offset
/// is in cells on a half-cell lattice (0.5 steps) so a marker can sit at the middle of a 2×2-cell block; whole
/// integers (the common case) round-trip verbatim.</summary>
public sealed class SpawnPlacement
{
    [JsonPropertyName("piece")]  public string Piece { get; set; } = "";
    [JsonPropertyName("at")]     public double[] At { get; set; } = [0, 0];
    [JsonPropertyName("facing")] public string Facing { get; set; } = "front";
}

/// <summary>A wool on <see cref="Piece"/> at half-cell offset <see cref="At"/>. <see cref="Color"/> is optional;
/// empty = auto (the team's first wool takes the team colour, later wools take distinct dyes).</summary>
public sealed class WoolPlacement
{
    [JsonPropertyName("piece")] public string Piece { get; set; } = "";
    [JsonPropertyName("at")]    public double[] At { get; set; } = [0, 0];
    [JsonPropertyName("color")] public string? Color { get; set; }
}

/// <summary>An iron (resource) marker on <see cref="Piece"/> at half-cell offset <see cref="At"/>.</summary>
public sealed class IronPlacement
{
    [JsonPropertyName("piece")] public string Piece { get; set; } = "";
    [JsonPropertyName("at")]    public double[] At { get; set; } = [0, 0];
}

/// <summary>A land interface between pieces <see cref="A"/> and <see cref="B"/> forced to a one-way drop
/// (a cliff), suppressing the step-terrace an elevation delta would otherwise require.</summary>
public sealed class PlanCliff
{
    [JsonPropertyName("a")] public string A { get; set; } = "";
    [JsonPropertyName("b")] public string B { get; set; } = "";
}

/// <summary>A land interface between pieces <see cref="A"/> and <see cref="B"/> marked as a pre-built approach
/// wall (stamped as a full-lane-width bedrock barrier at export). The pair must actually share a land
/// interface — a wall on a non-interface pair is a validation error.</summary>
public sealed class PlanWall
{
    [JsonPropertyName("a")] public string A { get; set; } = "";
    [JsonPropertyName("b")] public string B { get; set; } = "";
}
