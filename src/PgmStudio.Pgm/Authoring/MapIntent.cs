using PgmStudio.Domain;

namespace PgmStudio.Pgm.Authoring;

/// <summary>
/// Declarative authoring intent — the source of truth for a <b>new</b> map
/// (docs/contracts/new-map-authoring.md). The author states what they want; a generator projects it
/// into the PGM document (teams/kits/regions/filters/apply-rules/spawns). Persisted as a
/// <c>map_intent_json</c> artifact, outside the entity-replace codec (like the draft sidecar).
/// <para>Teams slice: teams, per-team spawns + protection, and the observer (<c>&lt;default&gt;</c>)
/// spawn. Build/wools slices extend this record later.</para>
/// </summary>
public sealed class MapIntent
{
    /// <summary>Teams to generate. Null leaves the doc's existing teams untouched (e.g. a map that was
    /// pre-seeded with teams); a non-empty list replaces them.</summary>
    public List<TeamDef>? Teams { get; init; }

    /// <summary>Shared player cap for every generated team (symmetric map → one number).</summary>
    public int MaxPlayers { get; init; } = 12;

    public List<SpawnIntent> Spawns { get; init; } = new();

    /// <summary>The observer / <c>&lt;default&gt;</c> spawn (pre-game + spectators). Every PGM map needs one.</summary>
    public ObserverIntent? Observer { get; init; }

    /// <summary>Buildable space (docs/contracts/new-map-authoring.md §5): the build height cap and the
    /// areas/bridges where building is allowed. The generator unions them and wires the void boundary.</summary>
    public BuildIntent? Build { get; init; }

    /// <summary>The objective wools. One per defending team on a symmetric map; each is captured by the
    /// other N−1 teams (one monument each). Null/empty leaves objectives untouched.</summary>
    public List<WoolIntent>? Wools { get; init; }

    /// <summary>The destroyable (DTM) objectives. One per defending team on a symmetric map, broken by the
    /// other N−1 teams — so unlike a wool there are no per-capturing-team monuments to map. Null/empty leaves
    /// them untouched, which is every CTW map.</summary>
    public List<DestroyableIntent>? Destroyables { get; init; }

    /// <summary>The core (DTC) objectives. One per defending team on a symmetric map, breached by the others.
    /// Null/empty leaves them untouched.</summary>
    public List<CoreIntent>? Cores { get; init; }

    /// <summary>Map identity: name + authors/contributors. Version (1.0.0), proto (1.5.0), gamemode (ctw)
    /// and the objective text are auto-derived by the generator, not authored.</summary>
    public MetaIntent? Meta { get; init; }

    /// <summary>The confirmed symmetry of the map (docs/contracts/new-map-authoring.md §4). When set, the
    /// generator <b>orbit-fills by default</b>: the author defines one orbit unit (team 0's spawn, one
    /// wool) and <see cref="SymmetryExpander"/> rotates/reflects it onto the other teams before projection,
    /// mapping orbit positions to <see cref="Teams"/> <i>in list order</i>. Null → no fill (author states
    /// every team's units explicitly).</summary>
    public SymmetryIntent? Symmetry { get; init; }

    /// <summary>Authoring aid (Teams step): island id → team id, colour-coding which team each island
    /// belongs to. Consumed by the <b>Spawn step</b> (not the generator): a spawn placed on a tagged island
    /// takes that island's team, and each orbit-filled spawn is (re)assigned by the island it lands on —
    /// making team inference + the orbit more accurate. Untagged islands stay neutral (e.g. a contested
    /// centre). Persisted with the intent.</summary>
    public Dictionary<string, string> IslandTeams { get; init; } = new();

    /// <summary>Block-coordinate structure directives the world-export path stamps into the synthesised world
    /// (docs/contracts/layout-rules.md ST1–ST4): wool-room floors, entrance redstone lines, iron cubes, and
    /// pre-built approach walls. Filled only by the plan compiler (all coordinates already resolved and fanned
    /// across the symmetry orbit); null on hand-authored / imported intents, which behave exactly as before.</summary>
    public StructureIntent? Structures { get; init; }
}

/// <summary>The plan-compiled layout structures, in absolute world block coordinates already fanned across the
/// symmetry orbit (docs/contracts/layout-rules.md ST1–ST4). Consumed by the sketch world-export path.</summary>
public sealed class StructureIntent
{
    /// <summary>Wool-room footprints stamped as solid bedrock from y=0 to the surface (ST1).</summary>
    public List<Rect> RoomFloors { get; init; } = new();

    /// <summary>Wool-room entrance redstone lines: a wire row with an end torch each side (ST1).</summary>
    public List<RedstoneLine> RedstoneLines { get; init; } = new();

    /// <summary>4×4×4 iron cubes resting on the surface, centred on each iron marker (ST2/ST3).</summary>
    public List<IronCube> IronCubes { get; init; } = new();

    /// <summary>Pre-built bedrock approach walls over a wool-lane interface seam (ST4).</summary>
    public List<WallStructure> Walls { get; init; } = new();

    /// <summary>True when every directive list is empty (no structures to stamp).</summary>
    public bool IsEmpty => RoomFloors.Count == 0 && RedstoneLines.Count == 0
        && IronCubes.Count == 0 && Walls.Count == 0;
}

/// <summary>An entrance redstone line: a straight wire row between the two block ends (inclusive), a torch at
/// each end, laid on top of the surface.</summary>
public readonly record struct RedstoneLine(int X1, int Z1, int X2, int Z2);

/// <summary>An iron cube anchored on a (whole-block) marker; a 4×4×4 iron structure resting on the surface.
/// <see cref="Renew"/> flags a marker inside a spawn-role piece — its cube regrows via the map.xml renewables
/// wiring (ST2).</summary>
public readonly record struct IronCube(int X, int Z, bool Renew);

/// <summary>A pre-built bedrock approach wall: a min-inclusive/max-exclusive footprint (two thick across the
/// seam, full interface width along it) rising from y=0 up to <see cref="TopY"/> inclusive (ST4).</summary>
public readonly record struct WallStructure(int MinX, int MinZ, int MaxX, int MaxZ, int TopY);

/// <summary>The confirmed map symmetry: a <see cref="Mode"/> (<c>mirror_x</c>/<c>mirror_z</c>/
/// <c>mirror_d1</c>/<c>mirror_d2</c>/<c>rot_180</c>/<c>rot_90</c>) about the centre (<see cref="CenterX"/>,
/// <see cref="CenterZ"/>) in world XZ. Drives orbit-fill and the suggested team count
/// (<c>rot_90</c>→4, everything else→2).</summary>
public sealed class SymmetryIntent
{
    public string Mode { get; init; } = "";
    public double CenterX { get; init; }
    public double CenterZ { get; init; }
}

/// <summary>An authored author/contributor: a Minecraft <b>username</b> plus an optional contribution
/// note; the endpoint resolves the username to a uuid via <c>MojangClient</c> before saving.</summary>
public sealed class AuthorIntent
{
    public string Name { get; init; } = "";
    public string? Contribution { get; init; }
}

/// <summary>Authored map identity.</summary>
public sealed class MetaIntent
{
    public string Name { get; init; } = "";
    public List<AuthorIntent> Authors { get; init; } = new();
    public List<AuthorIntent> Contributors { get; init; } = new();
}

/// <summary>Where players may build. <see cref="Areas"/> are the buildable rectangles (the over-void
/// bridges/platforms — the islands' terrain is auto-buildable via the void filter, so it needs no rect,
/// see new-map-authoring.md §6); they're unioned and the void boundary is wired automatically.
/// <see cref="Holes"/> are no-build cutouts subtracted from that union (PGM <c>complement</c>) — genuine
/// authored intent, unlike incidental union overlaps (which PGM ignores).</summary>
public sealed class BuildIntent
{
    /// <summary>Y cap above which no block placement is allowed (null = no ceiling).</summary>
    public int? MaxHeight { get; init; }

    /// <summary>The buildable rectangles (over-void footprints/bridges), unioned by the generator.</summary>
    public List<Rect> Areas { get; init; } = new();

    /// <summary>No-build cutouts subtracted from the area union (emitted as a <c>complement</c>). Empty →
    /// no holes (plain union). Orbited alongside <see cref="Areas"/> on symmetric maps.</summary>
    public List<Rect> Holes { get; init; } = new();
}

/// <summary>A team to generate. <see cref="Id"/> is the stable identifier rules/spawns reference and the
/// source of the naming slug; <see cref="Color"/> is the display colour (may be multi-word, e.g. "dark red").</summary>
public sealed class TeamDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Color { get; init; } = "";
}

/// <summary>One team's spawn: where players materialise (<see cref="Point"/>) and, optionally, the
/// anti-grief zone around it (<see cref="Protection"/>). The kit is the fixed Standard preset
/// (not author-selectable yet — see <c>TeamsGenerator</c>), so it isn't part of the intent.</summary>
public sealed class SpawnIntent
{
    public string Team { get; init; } = "";
    public Pt Point { get; init; }
    /// <summary>The anti-grief zone around the spawn, as a union of rectangles (empty = unprotected). A
    /// simple spawn is one rect; a complex footprint needs several. The generator unions them into the
    /// team's spawn-protection region. Tolerates a legacy single-object blob on read (see the converter).</summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(RectListJsonConverter))]
    public List<Rect> Protection { get; init; } = new();
    public double Yaw { get; init; }
}

/// <summary>The observer/default spawn point.</summary>
public sealed class ObserverIntent
{
    public Pt Point { get; init; }
    public double Yaw { get; init; }
}

/// <summary>One objective wool: defended by <see cref="Owner"/> in its <see cref="Room"/>, dispensed at
/// <see cref="Spawn"/> (a point — the wool's <c>location</c> is the int-floored version), and captured by
/// the teams in <see cref="Monuments"/> (one each, the non-owners).</summary>
public sealed class WoolIntent
{
    public string Owner { get; init; } = "";
    /// <summary>Dye colour (slug, e.g. <c>light_blue</c>). Empty → defaults to the owner team's colour.</summary>
    public string Color { get; init; } = "";
    /// <summary>The wool-room footprint, as a union of rectangles. Empty until the author draws it (partial
    /// intent is tolerated, new-map-authoring.md §11): a roomless wool still generates its objective +
    /// monuments, just not the room region / spawner / room wiring. A simple room is one rect; a complex
    /// footprint needs several, which the generator unions into the room region. Tolerates a legacy
    /// single-object blob on read (see the converter).</summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(RectListJsonConverter))]
    public List<Rect> Room { get; init; } = new();
    public Pt Spawn { get; init; }
    public List<MonumentIntent> Monuments { get; init; } = new();
}

/// <summary>A capture point: the team that captures this wool, and where they place it.</summary>
public sealed class MonumentIntent
{
    public string Team { get; init; } = "";
    public Pt Location { get; init; }
}

/// <summary>
/// One destroyable (DTM) objective: the <see cref="Materials"/> blocks of a <see cref="Style"/> structure
/// standing over <see cref="Anchor"/>, defended by <see cref="Owner"/> and broken by every other team.
/// <para>The structure parameters are already resolved here — the plan's optional fields are defaulted by the
/// compiler, so a consumer never re-decides them. <see cref="Anchor"/> is the marker column, not the
/// structure's corner: the box is centred on it and floats <see cref="Float"/> blocks above the surface its
/// footprint spans, so its Y is a function of the terrain rather than an authored number.</para>
/// </summary>
public sealed class DestroyableIntent
{
    /// <summary>The DEFENDING team — the same meaning as <see cref="WoolIntent.Owner"/>.</summary>
    public string Owner { get; init; } = "";
    /// <summary>Required by PGM, which rejects a nameless destroyable; the compiler auto-names from the owner
    /// and index rather than pushing the burden to the author.</summary>
    public string Name { get; init; } = "";
    /// <summary>pillar-1|2|3 · cube-3 · cube-4 · column-plus.</summary>
    public string Style { get; init; } = "";
    /// <summary>A PGM material match — the goal is these blocks, not the region that holds them.</summary>
    public string Materials { get; init; } = "";
    public Pt Anchor { get; init; }
    /// <summary>Blocks of air between the terrain and the structure's underside.</summary>
    public int Float { get; init; }

    /// <summary>
    /// The structure's resolved block volume — filled by the world-export path, which is the only place that
    /// knows the terrain the box floats over. Null on an intent that has not been through it.
    /// <para>The generator emits this box verbatim as the destroyable's <c>&lt;region&gt;</c>, so the goal's
    /// blocks and the region that scopes them come from the one box the stamper used (OB8). Deriving the
    /// region independently is how a region misses its structure and PGM yields a silent zero-health
    /// goal.</para>
    /// </summary>
    public BlockBox? Box { get; init; }
}

/// <summary>
/// One core (DTC) objective: an obsidian casing enclosing lava, defended by <see cref="Owner"/> and breached
/// by every other team. The destroyable's shape with the casing's own knobs.
/// <para>The material is not a knob: obsidian is effectively universal in the corpus (DC1), and PGM defaults
/// to it, so the generator emits no <c>material</c> attribute at all.</para>
/// <para><see cref="Float"/> and <see cref="Leak"/> are one knob (DC2) — together they state how far players
/// must dig under the core to make its lava leak (<see cref="DigDepth"/>). Neither means anything alone.</para>
/// </summary>
public sealed class CoreIntent
{
    /// <summary>The DEFENDING team. Emitted as the XML's <c>team</c> attribute, not <c>owner</c> — a PGM
    /// inconsistency we mirror in the XML while naming the field for what it means.</summary>
    public string Owner { get; init; } = "";
    /// <summary>Optional: PGM auto-names a core per team, unlike a destroyable, which it rejects nameless.</summary>
    public string Name { get; init; } = "";
    /// <summary>The marker column. The casing is centred on it and floats above the terrain, so no Y is authored.</summary>
    public Pt Anchor { get; init; }
    public int Size { get; init; }
    public int Height { get; init; }
    public int Shell { get; init; }
    /// <summary>Omit the cap layer so the lava reaches the casing rim.</summary>
    public bool OpenTop { get; init; }
    public int Float { get; init; }
    public int Leak { get; init; }

    /// <summary>The casing's resolved block volume — filled by the world-export path, the only place that
    /// knows the terrain it floats over. The generator emits it verbatim as the core's <c>&lt;region&gt;</c>,
    /// so the blocks and the region scoping them come from the one box the stamper used (OB8). Null on an
    /// intent that has not been through the world build.</summary>
    public BlockBox? Box { get; init; }

    /// <summary>How many blocks players must dig into the terrain under the core before its lava can leak.
    /// Zero when breaching the casing is enough on its own.</summary>
    public int DigDepth => Math.Max(0, Leak - Float);
}

/// <summary>A world point (spawn location).</summary>
public readonly record struct Pt(double X, double Y, double Z);

/// <summary>A footprint rectangle in world XZ.</summary>
public readonly record struct Rect(double MinX, double MinZ, double MaxX, double MaxZ);
