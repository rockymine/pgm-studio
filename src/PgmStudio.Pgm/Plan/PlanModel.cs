using System.Text.Json.Serialization;
using System.Text.Json;
using PgmStudio.Geom;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Plan;

/// <summary>The authorable piece roles. A piece is anonymous by default (<see cref="Piece"/>); meaning is
/// derived from the assembled graph. Two roles carry intent and are kept: <see cref="WoolRoom"/> (the room
/// region — red terrain seams, bedrock floor at export) and <see cref="Spawn"/> (the spawn region — iron
/// inside it auto-renews). Retired role names (<c>lane</c>/<c>hub</c>/<c>mid</c>/<c>connector</c>) and any
/// unknown value map to <see cref="Piece"/>.
///
/// <para>Roles split into two kinds. <b>Generating</b> roles (<see cref="Piece"/>/<see cref="WoolRoom"/>/
/// <see cref="Spawn"/>) produce terrain and participate in connectivity, gameplay, validation and export.
/// <b>Annotation</b> roles (the <see cref="Annotations"/> set) produce no terrain of their own and take no
/// part in connectivity. The one such role is <see cref="Buffer"/>, and it does reach the export, as the one
/// thing an annotation can say there: <b>this ground is not terrain</b>. It states negative space —
/// lane-to-lane spacing, the border reservation, and holes (a hole is an enclosed buffer) — which the
/// compiler carves out of the union outline that would otherwise fill it (<see cref="Plan.PlanVoids"/>). It
/// never takes terrain from a generating piece: over one it stays inert, so a buffer can declare a void but
/// never destroy ground. New non-generating roles are added by extending
/// <see cref="Annotations"/>.</para></summary>
public static class PlanRoles
{
    public const string Piece = "piece";
    public const string WoolRoom = "wool-room";
    public const string Spawn = "spawn";
    public const string Buffer = "buffer";

    /// <summary>The four a piece may carry, generating first.</summary>
    public static readonly string[] All = [Piece, WoolRoom, Spawn, Buffer];

    /// <summary>The non-generating annotation roles — marks that document intent (spacing, reserved gaps and
    /// holes via <see cref="Buffer"/>) rather than produce terrain. Extend this to add more.</summary>
    public static readonly IReadOnlySet<string> Annotations = new HashSet<string> { Buffer };

    /// <summary>True when the role is an annotation — never terrain of its own, never buildable, and outside
    /// the graph. A <see cref="Buffer"/> still reaches the export as negative space (see the type remarks).</summary>
    public static bool IsAnnotation(string? role) => role is not null && Annotations.Contains(role);

    /// <summary>True when the role produces terrain and participates in the graph/export (everything that is
    /// not an <see cref="Annotations">annotation</see>).</summary>
    public static bool IsGenerating(string? role) => !IsAnnotation(role);

    /// <summary>The canonical role for a raw (possibly legacy or empty) value: <c>wool-room</c>, <c>spawn</c>
    /// and the annotation <c>buffer</c> survive; everything else — including the retired
    /// <c>lane</c>/<c>hub</c>/<c>mid</c>/<c>connector</c> — is a plain piece.</summary>
    public static string Canonical(string? role) => role switch
    {
        WoolRoom => WoolRoom,
        Spawn => Spawn,
        Buffer => Buffer,
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
    /// <summary>The document's own shape version. <see cref="CurrentVersion"/> is what this build reads;
    /// anything else is refused rather than guessed at, because the units a number is in are not visible in
    /// the number.</summary>
    [JsonPropertyName("plan")]       public int Version { get; set; } = CurrentVersion;

    /// <summary>The shape version this build reads. Marker offsets are blocks from the piece's minimum
    /// corner; a version-1 document stated them in cells and is converted rather than accepted.</summary>
    public const int CurrentVersion = 2;

    /// <summary>What the plan is called and any note beside it, or absent for an unnamed one.</summary>
    [JsonPropertyName("meta")]       public PlanMeta? Meta { get; set; }

    /// <summary>The board-wide parameters every piece is measured against: the cell scale, the symmetry the
    /// unit is fanned by, the player count and the base surface height.</summary>
    [JsonPropertyName("globals")]    public PlanGlobals Globals { get; set; } = new();

    /// <summary>The team-0 unit's rectangles — the ground the board is made of. Symmetry fans the rest, so
    /// only one team's are authored.</summary>
    [JsonPropertyName("pieces")]     public List<PlanPiece> Pieces { get; set; } = [];

    /// <summary>The rects over the void where players may bridge, build zones and water lanes alike.</summary>
    [JsonPropertyName("zones")]      public List<PlanZone> Zones { get; set; } = [];

    /// <summary>Where the team-0 unit's spawns, wools, iron, destroyables and cores stand.</summary>
    [JsonPropertyName("placements")] public PlanPlacements Placements { get; set; } = new();

    /// <summary>The barriers stamped along a piece interface — the one thing a plan authors deliberately at a
    /// seam, since a cliff is whatever two heights meeting produce.</summary>
    [JsonPropertyName("walls")]      public List<PlanWall> Walls { get; set; } = [];

    /// <summary>Optional authoring annotation: the typed <see cref="PlanBox"/> envelopes grouping the pieces
    /// into the partition they realize. Like <see cref="Reference"/> it is read by authoring and reporting
    /// tools only — the compiler, the validator and the derivers ignore it, so a plan's compiled output does
    /// not depend on whether its boxes were drawn.</summary>
    [JsonPropertyName("boxes")]      public List<PlanBox> Boxes { get; set; } = [];

    // Terrain-paint theming lives on the sketch model, not here (docs/world-export/terrain-painting.md TP10):
    // the plan is the structural layer a generator emits, and paint is authored on the sketch where the
    // geometry is final. Any `themes`/`mapTheme`/`themeScopes` on a plan blob are simply ignored on parse.

    /// <summary>Optional provenance: the real map this plan was traced over, and where its top-down render
    /// sat under the grid. Purely authoring metadata — the compiler never reads it, so it has no effect on the
    /// compiled layout/intent. Absent for genuinely new (untraced) plans.</summary>
    [JsonPropertyName("reference")]  public PlanReference? Reference { get; set; }

    /// <summary>The zones open from the first tick. Everything that asks where players may build, which
    /// pieces front a gap, or how the board is connected reads this rather than <see cref="Zones"/> — a water
    /// lane is none of those things until it opens.</summary>
    [JsonIgnore] public IEnumerable<PlanZone> BuildZones => Zones.Where(z => z.IsBuild);

    /// <summary>The zones that open mid-match — the authored water lanes (<c>docs/pgm/water-lanes.md</c>).
    /// They export as the lane region plus the shared include, and take no part in the starting board.</summary>
    [JsonIgnore] public IEnumerable<PlanZone> WaterLanes => Zones.Where(z => z.IsWaterLane);

    /// <summary>How a plan is written, byte for byte, on every machine that writes one.
    ///
    /// <para><c>NewLine</c> is pinned for the same reason <c>Program.cs</c> pins the culture: a plan is a wire
    /// format and a stored artifact, and neither may depend on the host it was written from. Indented output
    /// defaults to <see cref="Environment.NewLine"/>, so the same plan serialized on Windows and on Linux
    /// differs in every line ending while meaning exactly the same thing — enough to make one plan two
    /// documents to anything comparing or digesting the bytes.</para>
    ///
    /// <para>The repository's <c>eol=lf</c> does not cover this. That governs files git tracks; a plan is
    /// hashed as a string and stored as a database artifact, and neither passes through git to be normalised.
    /// Left unpinned it took every composer fingerprint on a Windows checkout out of step with a record taken
    /// on Linux.</para></summary>
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        NewLine = "\n",
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public string ToJson() => JsonSerializer.Serialize(this, Json);

    public static PlanModel? Parse(string json)
    {
        var model = JsonSerializer.Deserialize<PlanModel>(json, Json);
        model?.Normalize();
        return model;
    }

    /// <summary>What a body states as a plan, or null where it states none. A body that will not read as one
    /// is the request's own fault (<c>RQ1</c>), answered where the body is read — so a reader only asking
    /// what the plan says takes it this way, and <see cref="Parse"/> stays the read that raises.</summary>
    public static PlanModel? Stated(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return Parse(json); }
        catch (JsonException) { return null; }
    }

    /// <summary>Fold legacy/unknown piece roles down to their canonical value, so plans authored under the
    /// earlier role model (<c>lane</c>/<c>hub</c>/<c>mid</c>) load cleanly as anonymous pieces, and unknown
    /// box kinds and zone kinds down to theirs.</summary>
    private void Normalize()
    {
        foreach (var p in Pieces) p.Role = PlanRoles.Canonical(p.Role);
        foreach (var b in Boxes) b.Kind = PlanBoxKinds.Canonical(b.Kind);
        foreach (var z in Zones) z.Kind = PlanZoneKinds.Stored(z.Kind);
        MintMarkerIds();
    }

    /// <summary>
    /// Give every marker an id, keeping the ones a document already carries. Minting on load is what lets a
    /// plan written before markers had identity read cleanly and gain it — the same self-healing a piece id
    /// gets. Ids are unique across the whole placement set, not per kind, so a finding naming one is
    /// unambiguous about which marker it means.
    /// </summary>
    private void MintMarkerIds()
    {
        var taken = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (_, marker) in Placements.All())
            if (marker.Id.Length > 0 && !taken.Add(marker.Id)) marker.Id = "";   // a duplicate is not an id
        foreach (var (kind, marker) in Placements.All())
        {
            if (marker.Id.Length > 0) continue;
            var next = 1;
            while (!taken.Add($"{kind}-{next}")) next++;
            marker.Id = $"{kind}-{next}";
        }
    }
}

/// <summary>What a plan is called, for the editor's title bar and the library list.</summary>
public sealed class PlanMeta
{
    /// <summary>The plan's name. It is the author's word rather than a key — a stored plan is addressed by
    /// its row id.</summary>
    [JsonPropertyName("name")]  public string Name { get; set; } = "";

    /// <summary>A free note the author keeps beside it. Nothing reads it.</summary>
    [JsonPropertyName("notes")] public string? Notes { get; set; }
}

/// <summary>Tracing provenance for a plan drawn over a real map's top-down render (authoring-only; ignored by
/// the compiler). <see cref="Map"/> is the source map's slug. The render is auto-centred on the symmetry
/// centre, then adjusted by <see cref="Offset"/> (a <c>[x, z]</c> nudge in proxy cells), <see cref="Scale"/>
/// (about the centre) and <see cref="Opacity"/> (0–1 backdrop strength).</summary>
public sealed class PlanReference
{
    /// <summary>The source map's slug — the map whose top-down render sits under the grid.</summary>
    [JsonPropertyName("map")]     public string Map { get; set; } = "";

    /// <summary>An <c>[x, z]</c> nudge in proxy cells, applied after the render is auto-centred on the
    /// symmetry centre.</summary>
    [JsonPropertyName("offset")]  public double[] Offset { get; set; } = [0, 0];

    /// <summary>How much the render is scaled, about the centre. 1 is its own size.</summary>
    [JsonPropertyName("scale")]   public double Scale { get; set; } = 1;

    /// <summary>How strongly the backdrop shows through, 0–1.</summary>
    [JsonPropertyName("opacity")] public double Opacity { get; set; } = 0.5;
}

/// <summary>Board-wide parameters. <see cref="Cell"/> is the blocks-per-proxy-cell scale and
/// <see cref="Surface"/> the base island height. <see cref="ObserverY"/> overrides the derived observer
/// height (default surface + 15).
///
/// <para><b>The build ceiling is not here, and that is the point.</b> It was <c>headroom</c>, a slack added
/// to <see cref="Surface"/>, so the cap was computed from the plan's <b>flat nominal</b> world — a ground
/// level the relief solve then abandons, which produced boards with a ceiling under their own terrain. The
/// author's rule measures it where the answer exists instead: twenty blocks over the highest ground the world
/// actually builds, derived in <c>WorldBuilder</c> (<see cref="PgmStudio.Domain.BuildCeiling"/>). A
/// plan-level number would be a second source for one value, and the one that gets overwritten.
/// <see cref="Surface"/> stays exactly as it was — load-bearing and correct as a plan-space concept.</para></summary>
public sealed class PlanGlobals
{
    /// <summary>Blocks per proxy cell — the scale every rect in the document is measured in.</summary>
    [JsonPropertyName("cell")]       public int Cell { get; set; } = 5;

    /// <summary>How the authored unit is fanned into the rest of the board: <c>rot_180</c>, <c>rot_90</c>,
    /// <c>mirror_x</c> or <c>mirror_z</c>.</summary>
    [JsonPropertyName("symmetry")]   public string Symmetry { get; set; } = "rot_180";

    /// <summary>How many players the board is sized for <b>per team</b> — the cap the export writes into
    /// every team's <c>max</c>, so a board's total is this times the number of teams. It is what the seed
    /// envelopes judge a board's proportions against, which read land per team against players per team.
    /// </summary>
    [JsonPropertyName("maxPlayers")] public int MaxPlayers { get; set; } = 12;

    /// <summary>The base island height, in blocks — the ground a piece stands at unless it states a plateau
    /// of its own.</summary>
    [JsonPropertyName("surface")]    public int Surface { get; set; } = 9;

    /// <summary>Where the observers watch from, or absent for the derived height (surface + 15).</summary>
    [JsonPropertyName("observerY")]  public int? ObserverY { get; set; }
}

/// <summary>A rectangular piece on the proxy grid. <see cref="Rect"/> is <c>[x, z, w, h]</c> in cells (x,z =
/// the min corner). <see cref="Surface"/> overrides the global surface (a plateau). <see cref="Mirrors"/>
/// (default true) marks the piece as fanned by symmetry; an on-axis neutral piece sets it false.</summary>
public sealed class PlanPiece
{
    /// <summary>What the rest of the document calls this piece — a marker names the piece it stands on, and a
    /// finding indicts one by this.</summary>
    [JsonPropertyName("id")]      public string Id { get; set; } = "";

    /// <summary>What the piece is for. A piece is anonymous by default and its meaning is derived from the
    /// assembled graph; the three that are not carry intent the graph cannot show — a <c>wool-room</c> and a
    /// <c>spawn</c> become regions the export treats specially, and a <c>buffer</c> makes no terrain at all
    /// and states negative space. An unknown word reads as <c>piece</c>.</summary>
    [WordSet(typeof(PlanRoles))]
    [JsonPropertyName("role")]    public string Role { get; set; } = PlanRoles.Piece;

    /// <summary>The footprint, <c>[x, z, w, h]</c> in proxy cells, with <c>x,z</c> the minimum corner and the
    /// origin at the symmetry centre.</summary>
    [JsonPropertyName("rect")]    public CellRect Rect { get; set; }

    /// <summary>The height this piece's ground stands at — a plateau — or absent to take the board's own
    /// surface.</summary>
    [JsonPropertyName("surface")] public int? Surface { get; set; }

    /// <summary>Whether symmetry fans this piece into the other teams' images. Absent means it does; a
    /// neutral piece lying on the axis sets it false, so it is not doubled onto itself.</summary>
    [JsonPropertyName("mirrors")] public bool? Mirrors { get; set; }

    [JsonIgnore] public bool MirrorsOrDefault => Mirrors ?? true;
}

/// <summary>The kinds a <see cref="PlanZone"/> may be. Both are a rect over the void that says where players
/// may bridge; they differ in <b>when</b>. A <see cref="Build"/> zone is open from the first tick — the
/// generator adds it to the buildable region. A <see cref="WaterLane"/> is closed at the first tick and opens
/// mid-match, so it is deliberately left <i>out</i> of the buildable region: PGM's void rule keeps players out
/// of it until water lands at <c>y=0</c> and the columns stop reading as void.</summary>
public static class PlanZoneKinds
{
    public const string Build = "build";
    public const string WaterLane = "water-lane";

    public static readonly IReadOnlySet<string> All = new HashSet<string> { Build, WaterLane };

    /// <summary>The canonical kind for a raw value. Absent or unknown folds to <see cref="Build"/> — every
    /// zone authored before the kind existed is a build zone, so the default keeps those plans reading
    /// exactly as they did.</summary>
    public static string Canonical(string? kind) =>
        kind is not null && All.Contains(kind) ? kind : Build;

    /// <summary>The value a kind is stored as: <c>null</c> for the default, so it is left out of the JSON
    /// entirely. Folding an unknown kind to <c>null</c> here is what makes a reload idempotent.</summary>
    public static string? Stored(string? kind) => Canonical(kind) == Build ? null : Canonical(kind);
}

/// <summary>A zone: a plain rect (<c>[x, z, w, h]</c> cells) over the void where players may bridge, with
/// optional no-build <see cref="Holes"/> (a rect list in the same units).
///
/// <para><see cref="Kind"/> says when it opens (see <see cref="PlanZoneKinds"/>). A build zone is buildable
/// from the start and is what the gap-connectivity derivation reads; a water lane opens part-way through the
/// match, so it contributes a late route rather than a starting one and is left out of that derivation
/// entirely — treating it as a connection would tell the lint a map is joined up at a tick when it is
/// not.</para></summary>
public sealed class PlanZone
{
    /// <summary>What the rest of the document calls this zone.</summary>
    [JsonPropertyName("id")]    public string Id { get; set; } = "";

    /// <summary>The zone's extent, <c>[x, z, w, h]</c> in proxy cells.</summary>
    [JsonPropertyName("rect")]  public CellRect Rect { get; set; }

    /// <summary>Rects cut out of it where players may not build, in the same cells.</summary>
    [JsonPropertyName("holes")] public List<CellRect> Holes { get; set; } = [];

    /// <summary>The authored kind, absent for the default. A build zone writes no <c>kind</c> at all, so a plan
    /// of build zones serialises byte-for-byte as it did before the field existed — which matters because a
    /// composed plan's JSON is its identity (<see cref="Compose.ComposerFingerprint"/>), and a field that
    /// appeared on every zone would read as a geometry change in every board.</summary>
    [WordSet(typeof(PlanZoneKinds))]
    [JsonPropertyName("kind")]  public string? Kind { get; set; }

    [JsonIgnore] public string KindOrDefault => PlanZoneKinds.Canonical(Kind);
    [JsonIgnore] public bool IsWaterLane => KindOrDefault == PlanZoneKinds.WaterLane;
    [JsonIgnore] public bool IsBuild => !IsWaterLane;
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
    /// <summary>What the editor and the reporting tools call this box.</summary>
    [JsonPropertyName("id")]      public string Id { get; set; } = "";

    /// <summary>Which part of the partition its pieces realize — a spawn or wool approach, the hub body they
    /// seat on, the frontline that fronts it, or the mid between the fanned images.</summary>
    [WordSet(typeof(PlanBoxKinds))]
    [JsonPropertyName("kind")]    public string Kind { get; set; } = PlanBoxKinds.Mid;

    /// <summary>The envelope, <c>[x, z, w, h]</c> in proxy cells, drawn around its members rather than filled
    /// by them.</summary>
    [JsonPropertyName("rect")]    public CellRect Rect { get; set; }

    /// <summary>The member piece ids, when the grouping is stated rather than inferred; <c>null</c>/empty
    /// leaves membership to containment.</summary>
    [JsonPropertyName("members")] public List<string>? Members { get; set; }
}

/// <summary>
/// What every placement has in common: an <see cref="Id"/> that names it, the piece it stands on, and where
/// on that piece. Markers were the one thing in a plan without identity — a piece and a zone each have an id,
/// and a marker was addressed only by its index in a list. That is enough to draw one and not enough to
/// <em>refer</em> to one: a validator finding could name only the piece a marker sat on, and an agent holding
/// "the second core" loses its reference the moment a different core is deleted.
/// <para><see cref="Piece"/> is empty for a destroyable or a core placed by absolute board position (B128,
/// <see cref="DestroyablePlacement"/>/<see cref="CorePlacement"/>) — the one exception to "the piece it
/// stands on", since those two need not stand on a piece at all.</para>
/// </summary>
public interface IPlanMarker
{
    string Id { get; set; }
    string Piece { get; }
    double[] At { get; }
}

/// <summary>The team-0 unit's objective markers; the compiler fans orbit images. Positions are piece-relative
/// cells.</summary>
public sealed class PlanPlacements
{
    /// <summary>Where the team enters the board.</summary>
    [JsonPropertyName("spawns")]       public List<SpawnPlacement> Spawns { get; set; } = [];

    /// <summary>The wools this team must capture, each on the piece that holds its room.</summary>
    [JsonPropertyName("wools")]        public List<WoolPlacement> Wools { get; set; } = [];

    /// <summary>The resource markers — iron inside a spawn piece renews itself.</summary>
    [JsonPropertyName("iron")]         public List<IronPlacement> Iron { get; set; } = [];

    /// <summary>The DTM goals this team defends, each an anchor column the structure floats above.</summary>
    [JsonPropertyName("destroyables")] public List<DestroyablePlacement> Destroyables { get; set; } = [];

    /// <summary>The DTC goals this team defends, each an anchor column the structure floats above.</summary>
    [JsonPropertyName("cores")]        public List<CorePlacement> Cores { get; set; } = [];

    /// <summary>Every marker with the word for its kind — the order ids are minted in, and the one place a
    /// pass over "all the markers" is written.</summary>
    public IEnumerable<(string Kind, IPlanMarker Marker)> All()
    {
        foreach (var spawn in Spawns) yield return ("spawn", spawn);
        foreach (var wool in Wools) yield return ("wool", wool);
        foreach (var iron in Iron) yield return ("iron", iron);
        foreach (var destroyable in Destroyables) yield return ("destroyable", destroyable);
        foreach (var core in Cores) yield return ("core", core);
    }
}

/// <summary>A spawn on <see cref="Piece"/> at piece-relative block offset <see cref="At"/>, facing
/// <see cref="Facing"/> — absolute board directions (<c>front</c>=−z, <c>back</c>=+z, <c>left</c>=−x,
/// <c>right</c>=+x), fanned per orbit image. The offset
/// is in blocks on a half-block lattice (0.5 steps) so a marker can sit at a block centre; whole
/// integers (the common case) round-trip verbatim.</summary>
public sealed class SpawnPlacement : IPlanMarker
{
    /// <summary>What a finding indicts this marker by, and what an agent holding a reference to it names.
    /// Minted by the editor where the author does not state one.</summary>
    [JsonPropertyName("id")]     public string Id { get; set; } = "";

    /// <summary>The piece it stands on, by id.</summary>
    [JsonPropertyName("piece")]  public string Piece { get; set; } = "";

    /// <summary>Where on that piece, as an <c>[x, z]</c> offset in cells from its minimum corner. The lattice
    /// is half-block, so a marker can sit on a block grid line or at a block centre.</summary>
    [JsonPropertyName("at")]     public double[] At { get; set; } = [0, 0];

    /// <summary>Which way the player faces on arriving, in absolute board directions: <c>front</c> is −z,
    /// <c>back</c> +z, <c>left</c> −x, <c>right</c> +x. It is fanned per orbit image, so the authored unit's
    /// word is turned rather than repeated.</summary>
    [JsonPropertyName("facing")] public string Facing { get; set; } = "front";

    /// <summary>The building on the piece, as an <c>[x, z, w, h]</c> rect in blocks from the piece's minimum
    /// corner — the room the shell is stamped on, where the piece itself is only the region holding it.
    /// Absent leaves it to the default: the piece inset by a block on every side, and by up to
    /// <see cref="PgmStudio.Domain.RoomFrames.DefaultDoorGap"/> in front of the door.</summary>
    [JsonPropertyName("footprint")] public double[]? Footprint { get; set; }
}

/// <summary>A wool on <see cref="Piece"/> at piece-relative block offset <see cref="At"/>. <see cref="Color"/> is optional;
/// empty = auto (the team's first wool takes the team colour, later wools take distinct dyes).</summary>
public sealed class WoolPlacement : IPlanMarker
{
    /// <summary>What a finding indicts this marker by, and what an agent holding a reference to it names.
    /// Minted by the editor where the author does not state one.</summary>
    [JsonPropertyName("id")]     public string Id { get; set; } = "";

    /// <summary>The piece it stands on, by id.</summary>
    [JsonPropertyName("piece")] public string Piece { get; set; } = "";

    /// <summary>Where on that piece, as an <c>[x, z]</c> offset in cells from its minimum corner. The lattice
    /// is half-block, so a marker can sit on a block grid line or at a block centre.</summary>
    [JsonPropertyName("at")]    public double[] At { get; set; } = [0, 0];

    /// <summary>The wool's colour, or absent to have one chosen: the team's first wool takes the team colour
    /// and later wools take distinct dyes.</summary>
    [JsonPropertyName("color")] public string? Color { get; set; }

    /// <summary>The building on the piece, as an <c>[x, z, w, h]</c> rect in blocks from the piece's minimum
    /// corner — the room the shell is stamped on, where the piece itself is only the region holding it.
    /// Absent leaves it to the default: the piece inset by a block on every side, and by up to
    /// <see cref="PgmStudio.Domain.RoomFrames.DefaultDoorGap"/> in front of the door.</summary>
    [JsonPropertyName("footprint")] public double[]? Footprint { get; set; }
}

/// <summary>An iron (resource) marker on <see cref="Piece"/> at piece-relative block offset <see cref="At"/>.</summary>
public sealed class IronPlacement : IPlanMarker
{
    /// <summary>What a finding indicts this marker by, and what an agent holding a reference to it names.
    /// Minted by the editor where the author does not state one.</summary>
    [JsonPropertyName("id")]     public string Id { get; set; } = "";

    /// <summary>The piece it stands on, by id.</summary>
    [JsonPropertyName("piece")] public string Piece { get; set; } = "";

    /// <summary>Where on that piece, as an <c>[x, z]</c> offset in cells from its minimum corner. The lattice
    /// is half-block, so a marker can sit on a block grid line or at a block centre.</summary>
    [JsonPropertyName("at")]    public double[] At { get; set; } = [0, 0];
}

/// <summary>
/// A destroyable (DTM objective) marker on <see cref="Piece"/> at piece-relative block offset <see cref="At"/>, owned by
/// the authored team-0 unit and fanned to one per orbit image — the wool marker's shape, since a destroyable is
/// likewise a goal one team defends. The marker is the structure's <b>anchor column</b>; the box itself floats
/// <see cref="Float"/> blocks above the ground the relief actually leaves under that column, so no Y is
/// authored.
/// <para><see cref="Piece"/> may be empty (B128): a destroyable is the one marker kind that need not ride a
/// plan piece at all. With a piece, <see cref="At"/> is a block offset from its minimum corner, same as every
/// other marker; with none, <see cref="At"/> is an absolute block offset from the symmetry centre — the frame a
/// piece's own <c>rect</c> is authored in — so a goal can stand on ground that exists only as an authored
/// sketch shape, with no plan piece manufactured to carry it.</para>
/// <para>Every structure parameter is optional and defaulted by the compiler, because the defaults are the
/// corpus's own centre of mass — a bare <c>{ piece, at }</c> or <c>{ at }</c> is a valid, typical
/// destroyable.</para>
/// </summary>
public sealed class DestroyablePlacement : IPlanMarker
{
    /// <summary>What a finding indicts this marker by, and what an agent holding a reference to it names.
    /// Minted by the editor where the author does not state one.</summary>
    [JsonPropertyName("id")]     public string Id { get; set; } = "";

    /// <summary>The piece it stands on, by id, or empty to place it by absolute board position instead —
    /// which is what lets a goal stand on ground that exists only as an authored sketch shape.</summary>
    [JsonPropertyName("piece")]     public string Piece { get; set; } = "";

    /// <summary>Where it stands, as an <c>[x, z]</c> offset in half-blocks: from the piece's minimum corner
    /// where one is named, and from the symmetry centre where none is.</summary>
    [JsonPropertyName("at")]        public double[] At { get; set; } = [0, 0];
    /// <summary>Which layer's surface this stands on, or null for the top one. A stacked board has a surface
    /// per layer, and a goal stated for a hall lands on the deck roofing it unless it says which layer it
    /// meant. Carried straight through to the intent the compiler writes.</summary>
    [JsonPropertyName("layer")]    public string? Layer { get; set; }

    /// <summary>pillar-1|2|3 · cube-3 · cube-4 · column-plus; empty = pillar-3.</summary>
    [JsonPropertyName("style")]     public string? Style { get; set; }
    /// <summary>A PGM material match; empty = obsidian, over half the corpus.</summary>
    [JsonPropertyName("materials")] public string? Materials { get; set; }
    /// <summary>Blocks of air between the ground the relief solves under this column and the structure's
    /// underside; null = 4. An offset over the ground as built, not a plan-nominal height — it survives a
    /// relief pass moving that ground, which an authored world Y would not.</summary>
    [JsonPropertyName("float")]     public int? Float { get; set; }
    /// <summary>Overrides the owner-and-index auto-name (<c>Red Monument</c>, <c>Red Monument 2</c>).</summary>
    [JsonPropertyName("name")]      public string? Name { get; set; }
}

/// <summary>
/// A core (DTC objective) marker on <see cref="Piece"/> at piece-relative block offset <see cref="At"/> — the destroyable
/// marker's shape, since a core is likewise one team's goal to defend, fanned to one per orbit image. The
/// marker is the casing's anchor column; the box floats <see cref="Float"/> blocks above the ground the relief
/// actually leaves under that column.
/// <para><see cref="Piece"/> may be empty, the same absolute addressing a destroyable takes (B128): with a
/// piece, <see cref="At"/> is a block offset from its minimum corner; with none, an absolute block offset from
/// the symmetry centre, so a core can ride an authored sketch landform with no plan piece carrying it.</para>
/// <para><see cref="Float"/> and <see cref="Leak"/> are one knob (DC2): escaping lava free-falls to the
/// terrain at <c>B − float</c> while the core leaks at <c>y ≤ B − leak</c>, so together they say how far
/// players must dig — <c>max(0, leak − float)</c>. Setting one without the other says nothing, so authoring
/// either requires both.</para>
/// </summary>
public sealed class CorePlacement : IPlanMarker
{
    /// <summary>What a finding indicts this marker by, and what an agent holding a reference to it names.
    /// Minted by the editor where the author does not state one.</summary>
    [JsonPropertyName("id")]     public string Id { get; set; } = "";

    /// <summary>The piece it stands on, by id, or empty to place it by absolute board position instead —
    /// which is what lets a goal stand on ground that exists only as an authored sketch shape.</summary>
    [JsonPropertyName("piece")]    public string Piece { get; set; } = "";

    /// <summary>Where it stands, as an <c>[x, z]</c> offset in half-blocks: from the piece's minimum corner
    /// where one is named, and from the symmetry centre where none is.</summary>
    [JsonPropertyName("at")]       public double[] At { get; set; } = [0, 0];
    /// <summary>Which layer's surface this stands on, or null for the top one. A stacked board has a surface
    /// per layer, and a goal stated for a hall lands on the deck roofing it unless it says which layer it
    /// meant. Carried straight through to the intent the compiler writes.</summary>
    [JsonPropertyName("layer")]    public string? Layer { get; set; }

    /// <summary>The lava's own footprint in blocks, 2–5; null = 3, which leaves the 5×5×5 obsidian casing
    /// that dominates the corpus. A core states its <b>interior</b>: a casing size and a wall thickness are
    /// two numbers that can contradict each other, and this one cannot.</summary>
    [JsonPropertyName("lava")]       public int? Lava { get; set; }
    /// <summary>How many courses of lava stand inside it, 2–5; null = 3.</summary>
    [JsonPropertyName("lavaHeight")] public int? LavaHeight { get; set; }
    /// <summary>Omit the cap so the lava sits flush with the rim; null = false — a real but minority style,
    /// so it is a flag rather than the default.</summary>
    [JsonPropertyName("openTop")]  public bool? OpenTop { get; set; }
    /// <summary>Blocks of air between the ground the relief solves under this column and the casing's
    /// underside; null = 6. An offset over the ground as built, not a plan-nominal height. Pairs with
    /// <see cref="Leak"/> (DC2).</summary>
    [JsonPropertyName("float")]    public int? Float { get; set; }
    /// <summary>How far lava must fall below the casing to count as leaked; null = 5, PGM's own default.
    /// Pairs with <see cref="Float"/> (DC2).</summary>
    [JsonPropertyName("leak")]     public int? Leak { get; set; }
    /// <summary>Optional — PGM auto-names a core per team (<c>Core</c>, <c>Core 2</c>), unlike a destroyable,
    /// which it rejects nameless.</summary>
    [JsonPropertyName("name")]     public string? Name { get; set; }
}

/// <summary>A land interface between pieces <see cref="A"/> and <see cref="B"/> marked as a pre-built approach
/// wall (stamped as a full-lane-width bedrock barrier at export). The pair must actually share a land
/// interface — a wall on a non-interface pair is a validation error.</summary>
public sealed class PlanWall
{
    /// <summary>One of the two pieces the wall stands between, by id — the one the author marked it
    /// from.</summary>
    [JsonPropertyName("a")] public string A { get; set; } = "";

    /// <summary>The piece on the other side of it, by id. The wall is stamped along the interval the two
    /// share.</summary>
    [JsonPropertyName("b")] public string B { get; set; } = "";

}

/// <summary>Where a marker actually is. A placement states its position as an <c>at</c> offset from the
/// minimum corner of the piece it rides, in <b>blocks</b> on the half-block lattice — the same lattice the
/// export snaps to (<c>PositionSnap.SnapHalfXZ</c>), so a whole offset is a block grid line and a <c>.5</c> a
/// block centre. Cell rects state the board's ground and blocks state what stands on it, so the two frames
/// are named rather than converted at each call site.</summary>
public static class PlanMarkers
{
    /// <summary>The marker's block position on a piece whose rect is already in blocks.</summary>
    public static (double X, double Z) Block(BlockRect piece, double[] at) =>
        (piece.MinX + at[0], piece.MinZ + at[1]);

    /// <summary>The same position in cells, for a reader working on the plan's own grid.</summary>
    public static (double X, double Z) Cell(CellRect rect, double[] at, int cell) =>
        (rect.X + at[0] / (double)cell, rect.Z + at[1] / (double)cell);

    /// <summary>The stated building on a piece whose rect is already in blocks, or null where the placement
    /// states none and the default answers. The four numbers are <c>[x, z, w, h]</c> from the piece's
    /// minimum corner, so the rect is anchored the same way a marker's <c>at</c> is.</summary>
    public static BlockRect? Footprint(BlockRect piece, double[]? footprint)
    {
        if (footprint is not { Length: >= 4 }) return null;
        var minX = (int)(piece.MinX + footprint[0]);
        var minZ = (int)(piece.MinZ + footprint[1]);
        return new BlockRect(minX, minZ, minX + (int)footprint[2], minZ + (int)footprint[3]);
    }
}
