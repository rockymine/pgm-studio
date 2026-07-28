using System.Text.Json.Serialization;
using System.Text.Json;
using PgmStudio.Geom;

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

/// <summary>The kinds an authored <see cref="PlanBox"/> may carry — the partition's typed box vocabulary
/// (docs/generator/model.md §4) as authoring strings. A box names <b>what its pieces realize</b>: the
/// <see cref="Spawn"/> and <see cref="Wool"/> approaches (each a terminal capping a corridor), the
/// <see cref="Hub"/> body they seat on, the <see cref="Frontline"/> that fronts it, and the <see cref="Mid"/>
/// between the fanned images.</summary>
public static class PlanBoxKinds
{
    public const string Spawn = "spawn";
    public const string Hub = "hub";
    public const string Wool = "wool";
    public const string Frontline = "frontline";
    public const string Mid = "mid";

    public static readonly IReadOnlySet<string> All =
        new HashSet<string> { Spawn, Hub, Wool, Frontline, Mid };

    /// <summary>The canonical kind for a raw (possibly unknown) value. An unrecognised kind folds to
    /// <see cref="Mid"/> — the same fallback a box read off an unexpected piece already takes — so a box
    /// authored under a vocabulary this build does not know still loads as a group, just an unclassified
    /// one.</summary>
    public static string Canonical(string? kind) =>
        kind is not null && All.Contains(kind) ? kind : Mid;
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

    /// <summary>Optional authoring annotation: the typed <see cref="PlanBox"/> envelopes grouping the pieces
    /// into the partition they realize. Like <see cref="Reference"/> it is read by authoring and reporting
    /// tools only — the compiler, the validator and the derivers ignore it, so a plan's compiled output does
    /// not depend on whether its boxes were drawn.</summary>
    [JsonPropertyName("boxes")]      public List<PlanBox> Boxes { get; set; } = [];

    /// <summary>Optional provenance: the real map this plan was traced over, and where its top-down render
    /// sat under the grid. Purely authoring metadata — the compiler never reads it, so it has no effect on the
    /// compiled layout/intent. Absent for genuinely new (untraced) plans.</summary>
    [JsonPropertyName("reference")]  public PlanReference? Reference { get; set; }

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
    /// earlier role model (<c>lane</c>/<c>hub</c>/<c>mid</c>) load cleanly as anonymous pieces, and unknown
    /// box kinds down to theirs.</summary>
    private void Normalize()
    {
        foreach (var p in Pieces) p.Role = PlanRoles.Canonical(p.Role);
        foreach (var b in Boxes) b.Kind = PlanBoxKinds.Canonical(b.Kind);
    }
}

public sealed class PlanMeta
{
    [JsonPropertyName("name")]  public string Name { get; set; } = "";
    [JsonPropertyName("notes")] public string? Notes { get; set; }
}

/// <summary>Tracing provenance for a plan drawn over a real map's top-down render (authoring-only; ignored by
/// the compiler). <see cref="Map"/> is the source map's slug. The render is auto-centred on the symmetry
/// centre, then adjusted by <see cref="Offset"/> (a <c>[x, z]</c> nudge in proxy cells), <see cref="Scale"/>
/// (about the centre) and <see cref="Opacity"/> (0–1 backdrop strength).</summary>
public sealed class PlanReference
{
    [JsonPropertyName("map")]     public string Map { get; set; } = "";
    [JsonPropertyName("offset")]  public double[] Offset { get; set; } = [0, 0];
    [JsonPropertyName("scale")]   public double Scale { get; set; } = 1;
    [JsonPropertyName("opacity")] public double Opacity { get; set; } = 0.5;
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
    [JsonPropertyName("rect")]    public CellRect Rect { get; set; }
    [JsonPropertyName("surface")] public int? Surface { get; set; }
    [JsonPropertyName("mirrors")] public bool? Mirrors { get; set; }

    [JsonIgnore] public bool MirrorsOrDefault => Mirrors ?? true;
}

/// <summary>A build zone: a plain rect (<c>[x, z, w, h]</c> cells) where building is allowed, with optional
/// no-build <see cref="Holes"/> (a rect list in the same units).</summary>
public sealed class PlanZone
{
    [JsonPropertyName("id")]    public string Id { get; set; } = "";
    [JsonPropertyName("rect")]  public CellRect Rect { get; set; }
    [JsonPropertyName("holes")] public List<CellRect> Holes { get; set; } = [];
}

/// <summary>A <b>box</b>: the typed envelope grouping the pieces that realize one part of the partition —
/// a wool approach, the spawn, the hub body, the frontline, the mid. <see cref="Rect"/> is <c>[x, z, w, h]</c>
/// in cells, drawn around its members rather than filled by them (boxes may overlap; a box's contents need
/// not fill it solid).
///
/// <para>Membership is by <b>containment</b> — every generating piece wholly inside the rect — unless
/// <see cref="Members"/> names the pieces explicitly, which a composed partition writes so a picked board
/// opens with exactly the grouping that produced it. Authoring annotation throughout: nothing downstream of
/// the editor reads it (see <see cref="PlanModel.Boxes"/>), so drawing a box can never change what a plan
/// compiles to.</para></summary>
public sealed class PlanBox
{
    [JsonPropertyName("id")]      public string Id { get; set; } = "";
    [JsonPropertyName("kind")]    public string Kind { get; set; } = PlanBoxKinds.Mid;
    [JsonPropertyName("rect")]    public CellRect Rect { get; set; }

    /// <summary>The member piece ids, when the grouping is stated rather than inferred; <c>null</c>/empty
    /// leaves membership to containment.</summary>
    [JsonPropertyName("members")] public List<string>? Members { get; set; }
}

/// <summary>The team-0 unit's objective markers; the compiler fans orbit images. Positions are piece-relative
/// cells.</summary>
public sealed class PlanPlacements
{
    [JsonPropertyName("spawns")]       public List<SpawnPlacement> Spawns { get; set; } = [];
    [JsonPropertyName("wools")]        public List<WoolPlacement> Wools { get; set; } = [];
    [JsonPropertyName("iron")]         public List<IronPlacement> Iron { get; set; } = [];
    [JsonPropertyName("destroyables")] public List<DestroyablePlacement> Destroyables { get; set; } = [];
    [JsonPropertyName("cores")]        public List<CorePlacement> Cores { get; set; } = [];
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

/// <summary>
/// A destroyable (DTM objective) marker on <see cref="Piece"/> at half-cell offset <see cref="At"/>, owned by
/// the authored team-0 unit and fanned to one per orbit image — the wool marker's shape, since a destroyable is
/// likewise a goal one team defends. The marker is the structure's <b>anchor column</b>; the box itself floats
/// <see cref="Float"/> blocks above the surface it spans, so no Y is authored.
/// <para>Every structure parameter is optional and defaulted by the compiler, because the defaults are the
/// corpus's own centre of mass — a bare <c>{ piece, at }</c> is a valid, typical destroyable.</para>
/// </summary>
public sealed class DestroyablePlacement
{
    [JsonPropertyName("piece")]     public string Piece { get; set; } = "";
    [JsonPropertyName("at")]        public double[] At { get; set; } = [0, 0];
    /// <summary>pillar-1|2|3 · cube-3 · cube-4 · column-plus; empty = pillar-3.</summary>
    [JsonPropertyName("style")]     public string? Style { get; set; }
    /// <summary>A PGM material match; empty = obsidian, over half the corpus.</summary>
    [JsonPropertyName("materials")] public string? Materials { get; set; }
    /// <summary>Blocks of air under the structure; null = 4.</summary>
    [JsonPropertyName("float")]     public int? Float { get; set; }
    /// <summary>Overrides the owner-and-index auto-name (<c>Red Monument</c>, <c>Red Monument 2</c>).</summary>
    [JsonPropertyName("name")]      public string? Name { get; set; }
}

/// <summary>
/// A core (DTC objective) marker on <see cref="Piece"/> at half-cell offset <see cref="At"/> — the destroyable
/// marker's shape, since a core is likewise one team's goal to defend, fanned to one per orbit image. The
/// marker is the casing's anchor column; the box floats <see cref="Float"/> blocks above the surface.
/// <para><see cref="Float"/> and <see cref="Leak"/> are one knob (DC2): escaping lava free-falls to the
/// terrain at <c>B − float</c> while the core leaks at <c>y ≤ B − leak</c>, so together they say how far
/// players must dig — <c>max(0, leak − float)</c>. Setting one without the other says nothing, so authoring
/// either requires both.</para>
/// </summary>
public sealed class CorePlacement
{
    [JsonPropertyName("piece")]    public string Piece { get; set; } = "";
    [JsonPropertyName("at")]       public double[] At { get; set; } = [0, 0];
    /// <summary>Casing width/depth in blocks; null = 5, the dominant corpus casing.</summary>
    [JsonPropertyName("size")]     public int? Size { get; set; }
    /// <summary>Casing height in blocks; null = 5.</summary>
    [JsonPropertyName("height")]   public int? Height { get; set; }
    /// <summary>Casing thickness; null = 1 (65% of corpus cores).</summary>
    [JsonPropertyName("shell")]    public int? Shell { get; set; }
    /// <summary>Omit the cap so the lava sits flush with the rim; null = false — a real but minority style,
    /// so it is a flag rather than the default.</summary>
    [JsonPropertyName("openTop")]  public bool? OpenTop { get; set; }
    /// <summary>Blocks of air under the casing; null = 6. Pairs with <see cref="Leak"/> (DC2).</summary>
    [JsonPropertyName("float")]    public int? Float { get; set; }
    /// <summary>How far lava must fall below the casing to count as leaked; null = 5, PGM's own default.
    /// Pairs with <see cref="Float"/> (DC2).</summary>
    [JsonPropertyName("leak")]     public int? Leak { get; set; }
    /// <summary>Optional — PGM auto-names a core per team (<c>Core</c>, <c>Core 2</c>), unlike a destroyable,
    /// which it rejects nameless.</summary>
    [JsonPropertyName("name")]     public string? Name { get; set; }
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
