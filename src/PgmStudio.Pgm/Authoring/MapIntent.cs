using System.Text.Json;
using System.Text.Json.Serialization;
using PgmStudio.Domain;
using PgmStudio.Geom;

namespace PgmStudio.Pgm.Authoring;

/// <summary>
/// Declarative authoring intent — the source of truth for a <b>new</b> map
/// (docs/pgm/new-map-authoring.md). The author states what they want; a generator projects it
/// into the PGM document (teams/kits/regions/filters/apply-rules/spawns). Persisted as a
/// <c>map_intent_json</c> artifact, outside the entity-replace codec (like the draft sidecar).
/// <para>Teams slice: teams, per-team spawns + protection, and the observer (<c>&lt;default&gt;</c>)
/// spawn. Build/wools slices extend this record later.</para>
/// <para>A <b>record</b> so a pass that rewrites part of an intent can say <c>intent with { … }</c> and
/// carry the rest. Rebuilding it by naming every field is how <see cref="SymmetryExpander"/> silently
/// deleted the four slices added after it was written: a whole map's cores and destroyables vanished from
/// the export, and nothing said so. Carrying is the default; transforming is the thing that must be
/// spelled out.</para>
/// </summary>
public sealed record MapIntent
{
    /// <summary>Teams to generate. Null leaves the doc's existing teams untouched (e.g. a map that was
    /// pre-seeded with teams); a non-empty list replaces them.</summary>
    public List<TeamDef>? Teams { get; init; }

    /// <summary>Shared player cap for every generated team (symmetric map → one number).</summary>
    public int MaxPlayers { get; init; } = 12;

    /// <summary>Where each team enters the map, one entry per team. A spawn names the region it stands in
    /// and the way the player faces on arriving.</summary>
    public List<SpawnIntent> Spawns { get; init; } = new();

    /// <summary>The observer / <c>&lt;default&gt;</c> spawn (pre-game + spectators). Every PGM map needs one.</summary>
    public ObserverIntent? Observer { get; init; }

    /// <summary>Buildable space (docs/pgm/new-map-authoring.md §5): the build height cap and the
    /// areas/bridges where building is allowed. The generator unions them and wires the void boundary.</summary>
    public BuildIntent? Build { get; init; }

    /// <summary>The water lanes — the gaps that open mid-match (docs/pgm/water-lanes.md). Null/empty
    /// leaves them untouched, which is every map that has none.</summary>
    public WaterLaneIntent? WaterLanes { get; init; }

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

    /// <summary>Map identity: name + authors/contributors. Version (1.0.0) and proto (1.5.0) are fixed; the
    /// gamemode and the objective text are auto-derived from which objective modules the intent carries
    /// (<see cref="MetaGenerator"/>), not authored.</summary>
    public MetaIntent? Meta { get; init; }

    /// <summary>The confirmed symmetry of the map (docs/pgm/new-map-authoring.md §3). When set, the
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
    /// (docs/generator/rules.md ST1–ST4): entrance redstone lines, iron cubes, and
    /// pre-built approach walls. Filled only by the plan compiler (all coordinates already resolved and fanned
    /// across the symmetry orbit); null on hand-authored / imported intents, which behave exactly as before.</summary>
    public StructureIntent? Structures { get; init; }

    // Terrain-paint theming is no longer carried on the intent: it lives on the sketch model (a theme registry
    // + per-shape override on SketchLayout), resolved at export by TerrainThemeScope from the sketch geometry
    // (docs/world-export/terrain-painting.md TP10).
}

/// <summary>The plan-compiled layout structures, in absolute world block coordinates already fanned across the
/// symmetry orbit (docs/generator/rules.md ST1–ST4). Consumed by the sketch world-export path.</summary>
public sealed record StructureIntent
{
    /// <summary>Which layer's surface this stands on, or null for the top one. A stacked board has a surface
    /// per layer, and a thing stated for a hall lands on the deck roofing it unless it says which layer it
    /// meant.</summary>
    public string? Layer { get; init; }

    /// <summary>Wool-room entrance redstone lines: a wire row with an end torch each side (ST1).</summary>
    public List<RedstoneLine> RedstoneLines { get; init; } = new();

    /// <summary>The iron cubes standing on a piece that carries no room, each already resolved to the
    /// footprint it stamps on (ST2/ST3). A marker whose cube does not fit inside its piece resolves
    /// unplaceable and is not here at all (WX9).</summary>
    public List<IronCube> IronCubes { get; init; } = new();

    /// <summary>Pre-built bedrock approach walls over a wool-lane interface seam (ST4).</summary>
    public List<WallStructure> Walls { get; init; } = new();

    /// <summary>True when every directive list is empty (no structures to stamp).</summary>
    public bool IsEmpty => RedstoneLines.Count == 0 && IronCubes.Count == 0 && Walls.Count == 0;
}

/// <summary>An entrance redstone line: a straight wire row between the two block ends (inclusive), a torch at
/// each end, laid on top of the surface.</summary>
/// <param name="X1">One end of the wire row, east–west.</param>
/// <param name="Z1">That end, north–south.</param>
/// <param name="X2">The other end, east–west.</param>
/// <param name="Z2">That end, north–south.</param>
/// <param name="Stamp">Which stamping of the structure this is, so the same line lays identically on every
/// export.</param>
public readonly record struct RedstoneLine(int X1, int Z1, int X2, int Z2, StampId Stamp = default);

/// <summary>A resolved iron cube: <see cref="RoomFrames.IronSpan"/> blocks square, standing on the surface
/// its footprint spans (ST3). The corner rather than the marker, because the marker is what an author places
/// and this is what the world gets — <see cref="RoomFrames.PlaceIron"/> is the one thing that turns one into
/// the other, and a marker whose cube would not fit its piece produces none at all (WX9).
/// <see cref="Renew"/> flags a marker inside a spawn-role piece — its cube regrows via the map.xml renewables
/// wiring (ST2).</summary>
/// <param name="MinX">The cube's minimum corner, east–west.</param>
/// <param name="MinZ">That corner, north–south.</param>
/// <param name="Renew">Whether the marker sits inside a spawn-role piece, in which case the cube regrows
/// through the map's renewables wiring.</param>
/// <param name="Stamp">Which stamping of the structure this is.</param>
public readonly record struct IronCube(int MinX, int MinZ, bool Renew, StampId Stamp = default);

/// <summary>A pre-built bedrock approach wall: a min-inclusive/max-exclusive footprint (two thick across the
/// seam, full interface width along it) rising from y=0 up to <see cref="TopY"/> inclusive, capped by one
/// course of cobweb (ST4).
/// <para><see cref="ChestOnMinFace"/> picks which of the wall's two faces its defence chests are set into —
/// the low-coordinate one across the seam, or the high one. The wall is two blocks thick precisely so one
/// face can be opened while the other stays solid bedrock, and which face that is decides which team can
/// reach the supply, so it is authored (<c>PlanWall.Side</c>) rather than left to whichever piece happened to
/// have the smaller coordinate. Recomputed per orbit image, because a reflection swaps the two faces.</para>
/// </summary>
/// <param name="MinX">Its west edge, inclusive.</param>
/// <param name="MinZ">Its north edge, inclusive.</param>
/// <param name="MaxX">Its east edge, exclusive.</param>
/// <param name="MaxZ">Its south edge, exclusive.</param>
/// <param name="TopY">The height it rises to, inclusive.</param>
/// <param name="ChestOnMinFace">Which of the wall's two faces its defence chests are set into — the
/// low-coordinate one across the seam, or the high one. The wall is two blocks thick precisely so one face
/// can be opened while the other stays solid, and which face that is decides which team can reach the
/// supply. Recomputed per orbit image, since a reflection swaps the two.</param>
/// <param name="Stamp">Which stamping of the structure this is.</param>
public readonly record struct WallStructure(
    int MinX, int MinZ, int MaxX, int MaxZ, int TopY, bool ChestOnMinFace = true, StampId Stamp = default);

/// <summary>The confirmed map symmetry: a <see cref="Mode"/> (<c>mirror_x</c>/<c>mirror_z</c>/
/// <c>mirror_d1</c>/<c>mirror_d2</c>/<c>rot_180</c>/<c>rot_90</c>) about the centre (<see cref="CenterX"/>,
/// <see cref="CenterZ"/>) in world XZ. Drives orbit-fill and the suggested team count
/// (<c>rot_90</c>→4, everything else→2).</summary>
public sealed record SymmetryIntent
{
    /// <summary>How the map folds — <c>mirror_x</c>, <c>mirror_z</c>, <c>mirror_d1</c>, <c>mirror_d2</c>,
    /// <c>rot_180</c> or <c>rot_90</c>. It drives the orbit fill and the suggested team count.</summary>
    public string Mode { get; init; } = "";

    /// <summary>Where it folds, east–west.</summary>
    public double CenterX { get; init; }

    /// <summary>Where it folds, north–south.</summary>
    public double CenterZ { get; init; }
}

/// <summary>An authored author/contributor: a Minecraft <b>username</b> plus an optional contribution
/// note; the endpoint resolves the username to a uuid via <c>MojangClient</c> before saving.
/// <para>Reads a bare string as the name it always was — see <see cref="AuthorIntentJson"/>.</para></summary>
[JsonConverter(typeof(AuthorIntentJson))]
public sealed record AuthorIntent
{
    /// <summary>The author's Minecraft username. The export resolves it to the uuid the contract stores.</summary>
    public string Name { get; init; } = "";

    /// <summary>What they contributed, or absent for a plain credit.</summary>
    public string? Contribution { get; init; }
}

/// <summary>
/// Reads an author written before the note existed. A stored intent from then carries
/// <c>"authors": ["rockymine", "Ruediger_LP"]</c> — the username alone — and today's record is an object, so
/// the whole document failed to deserialize: not the intent endpoint, not the 3-D preview's build, not the
/// export. One map's authors made everything else about it unreadable.
///
/// <para>Upgrading on read is the discipline this studio already keeps for stored documents
/// (<c>HouseStyleJson.Upgrade</c>, <c>TerrainThemeJson.Upgrade</c>, and the reason <c>RQ3</c> is a complaint
/// rather than a refusal): a snapshot outlives the shape it was written in, and the alternative is a
/// migration that fixes the rows in one database and still cannot read a document exported before it. Writing
/// is unchanged — the object goes out, so a document is stored in today's shape the first time it is saved.</para>
/// </summary>
public sealed class AuthorIntentJson : JsonConverter<AuthorIntent>
{
    public override AuthorIntent Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
            return new AuthorIntent { Name = reader.GetString() ?? "" };

        string name = "", contribution = "";
        var depth = reader.CurrentDepth;
        while (reader.Read() && !(reader.TokenType == JsonTokenType.EndObject && reader.CurrentDepth == depth))
        {
            if (reader.TokenType != JsonTokenType.PropertyName) continue;
            var property = reader.GetString();
            reader.Read();
            if (string.Equals(property, "name", StringComparison.OrdinalIgnoreCase))
                name = reader.TokenType == JsonTokenType.String ? reader.GetString() ?? "" : "";
            else if (string.Equals(property, "contribution", StringComparison.OrdinalIgnoreCase))
                contribution = reader.TokenType == JsonTokenType.String ? reader.GetString() ?? "" : "";
            else reader.Skip();
        }
        return new AuthorIntent { Name = name, Contribution = contribution.Length > 0 ? contribution : null };
    }

    public override void Write(Utf8JsonWriter writer, AuthorIntent value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("name", value.Name);
        if (value.Contribution is { Length: > 0 } note) writer.WriteString("contribution", note);
        writer.WriteEndObject();
    }
}

/// <summary>Authored map identity.</summary>
public sealed record MetaIntent
{
    /// <summary>The map's title.</summary>
    public string Name { get; init; } = "";

    /// <summary>When the map was made, as <c>yyyy-mm-dd</c>. Stated by whoever authors the map — the studio
    /// has no way to know it and does not invent one, so an intent that says nothing writes no
    /// <c>&lt;created&gt;</c>.</summary>
    public string Created { get; init; } = "";

    /// <summary>Who made it.</summary>
    public List<AuthorIntent> Authors { get; init; } = new();

    /// <summary>Who helped, credited apart from the authors.</summary>
    public List<AuthorIntent> Contributors { get; init; } = new();
}

/// <summary>Where players may build. <see cref="Areas"/> are the buildable rectangles (the over-void
/// bridges/platforms — the islands' terrain is auto-buildable via the void filter, so it needs no rect,
/// see new-map-authoring.md §5); they're unioned and the void boundary is wired automatically.
/// <see cref="Holes"/> are no-build cutouts subtracted from that union (PGM <c>complement</c>) — genuine
/// authored intent, unlike incidental union overlaps (which PGM ignores).
/// <para><see cref="VoidEnforcement"/> is a second, independent knob: whether the void may be bridged is
/// stated on its own, not inferred from whether <see cref="Areas"/> is empty. A board with no declared build
/// area still states it wants (or does not want) the void permanent.</para></summary>
public sealed record BuildIntent
{
    /// <summary>Y cap above which no block placement is allowed (null = no ceiling).</summary>
    public int? MaxHeight { get; init; }

    /// <summary>The buildable rectangles (over-void footprints/bridges), unioned by the generator.</summary>
    public List<Rect> Areas { get; init; } = new();

    /// <summary>No-build cutouts subtracted from the area union (emitted as a <c>complement</c>). Empty →
    /// no holes (plain union). Orbited alongside <see cref="Areas"/> on symmetric maps.</summary>
    public List<Rect> Holes { get; init; } = new();

    /// <summary>Standalone void enforcement (docs/pgm/new-map-authoring.md §5b, corpus idiom
    /// <c>alpine_mining_ii</c>): null → no standalone enforcement, which is every map today and stays the
    /// default for a map that states nothing — the corpus itself is split (100/112 <c>dtcm</c> maps enforce
    /// something, but by a hard <c>never</c> region as often as by the void), so the studio does not guess
    /// which an author wants. Non-null → wired even with <see cref="Areas"/> empty, and alongside it when
    /// both are declared: the two are independent decisions, exactly as the corpus keeps them.</summary>
    public VoidEnforcementIntent? VoidEnforcement { get; init; }
}

/// <summary>Standalone void enforcement, scoped to everywhere minus <see cref="Exclusions"/> — the
/// <c>alpine_mining_ii</c> idiom (<c>complement(everywhere, obs-spawn)</c>, here built as
/// <c>negative(exclusion…)</c>, the same region with no <c>everywhere</c> node needed). Empty
/// <see cref="Exclusions"/> → enforced over the whole map. A player may still <b>break</b> a block hanging
/// over the void; only placing is denied (<c>block-place</c>, not <c>block</c>), which is what lets a
/// destroyable float over open void without the studio blocking its own goal from being broken.</summary>
public sealed record VoidEnforcementIntent
{
    /// <summary>Rectangles excluded from enforcement — an observer platform floating near the void, the way
    /// <c>alpine_mining_ii</c> excludes its <c>obs-spawn</c> so a player who reaches it cannot be blocked
    /// from editing there. Empty → no exclusions. Orbited alongside <see cref="BuildIntent.Areas"/> on
    /// symmetric maps.</summary>
    public List<Rect> Exclusions { get; init; } = new();
}

/// <summary>
/// The water lanes (docs/pgm/water-lanes.md): gaps that become bridgeable part-way through the match,
/// adding a late route to the wool. <see cref="Rects"/> are the footprints, and they carry no Y because a lane
/// is always the single block layer at <c>y=0</c> — the one PGM's void filter reads.
/// <para>Deliberately separate from <see cref="BuildIntent.Areas"/>, which it is the opposite of: a build area
/// is open from the first tick, and a lane is closed until it opens. The generator emits the region and the
/// shared include, and nothing else — the fragment the server resolves supplies the whole mechanism, so
/// authoring one is naming a place under an agreed id.</para>
/// </summary>
public sealed record WaterLaneIntent
{
    /// <summary>The lane footprints in map coordinates, orbited on symmetric maps. Empty → no lanes.</summary>
    public List<Rect> Rects { get; init; } = new();
}

/// <summary>A team to generate. <see cref="Id"/> is the stable identifier rules/spawns reference and the
/// source of the naming slug; <see cref="Color"/> is the display colour (may be multi-word, e.g. "dark red").</summary>
public sealed record TeamDef
{
    /// <summary>What the rest of the intent names the team by.</summary>
    public string Id { get; init; } = "";

    /// <summary>The team as a player reads it.</summary>
    public string Name { get; init; } = "";

    /// <summary>The colour it plays in.</summary>
    public string Color { get; init; } = "";
}

/// <summary>One team's spawn: where players materialise (<see cref="Point"/>), the region that ground is
/// (<see cref="Protection"/>) and the building raised on it (<see cref="Footprint"/>). The kit is the fixed
/// Standard preset (not author-selectable yet — see <c>TeamsGenerator</c>), so it isn't part of the intent.</summary>
public sealed record SpawnIntent
{
    /// <summary>Which layer's surface this stands on, or null for the top one. A stacked board has a surface
    /// per layer, and a thing stated for a hall lands on the deck roofing it unless it says which layer it
    /// meant.</summary>
    public string? Layer { get; init; }

    /// <summary>Which authored unit this is an image of, and which image — set by whoever fanned the orbit,
    /// carried through to the stamper, and recorded as the column's owner. The stamper cannot derive it: it
    /// receives an entry out of an already-fanned list and can only count.</summary>
    public StampId Stamp { get; init; }

    /// <summary>The team that enters here, by id.</summary>
    public string Team { get; init; } = "";

    /// <summary>Where players materialise.</summary>
    public Pt Point { get; init; }

    /// <summary>The region the spawn owns, as a union of rectangles (empty = unprotected). It is both the
    /// anti-grief zone the generator emits and <b>the ground the room stands on</b> — the bounds every marker
    /// on it is held to, and what a footprint must lie inside (docs/world-export/structures.md WX1/WX12). A
    /// plan-compiled spawn states the one rectangle of its piece; an author drawing a complex zone states
    /// several, and the room is framed on what they enclose. Tolerates a legacy single-object blob on read
    /// (see the converter).</summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(RectListJsonConverter))]
    public List<Rect> Protection { get; init; } = new();

    /// <summary>Which way players face on arriving, in degrees. A hall opening on two walls faces the corner
    /// between them, so this is not a multiple of 90 on every board and no door is derivable from it.</summary>
    public double Yaw { get; init; }

    /// <summary>The walls the hall opens through, as edge words (<c>-z</c>, <c>+z</c>, <c>-x</c>, <c>+x</c>),
    /// in the order they are cut — the door the team walks out of first. Where a piece meets the board on two
    /// sides it earns two (docs/world-export/structures.md <c>WX6</c>). Empty on a hand-authored intent, which
    /// leaves the room a door on the wall its yaw leans into.</summary>
    public List<string> Doors { get; init; } = new();

    /// <summary>The building raised on the region — the footprint the shell is stamped on, which the plan
    /// states and the author resizes. Null leaves it to the default: the region inset by a block on every
    /// side and by up to the door gap in front of each door (WX1).</summary>
    public Rect? Footprint { get; init; }

    /// <summary>Iron markers on the spawn's ground (fanned world points). Each resolves to a renewable iron
    /// cube standing clear of the room shell in the ring around it (WX8), or to an unplaceable marker that
    /// stamps nothing and is flagged at validation (WX9). Empty on hand-authored intents and on spawns whose
    /// iron rides <see cref="StructureIntent.IronCubes"/> instead.</summary>
    public List<Pt> Iron { get; init; } = new();
}

/// <summary>The observer/default spawn point.</summary>
public sealed record ObserverIntent
{
    /// <summary>Where observers watch from.</summary>
    public Pt Point { get; init; }

    /// <summary>Which way they face on arriving, in degrees.</summary>
    public double Yaw { get; init; }
}

/// <summary>One objective wool: defended by <see cref="Owner"/> in its <see cref="Protection"/> region,
/// dispensed at <see cref="Spawn"/> (a point — the wool's <c>location</c> is the int-floored version), and
/// captured by the teams in <see cref="Monuments"/> (one each, the non-owners).</summary>
public sealed record WoolIntent
{
    /// <summary>Which layer's surface this stands on, or null for the top one. A stacked board has a surface
    /// per layer, and a thing stated for a hall lands on the deck roofing it unless it says which layer it
    /// meant.</summary>
    public string? Layer { get; init; }

    /// <summary>Which authored unit this is an image of, and which image — set by whoever fanned the orbit,
    /// carried through to the stamper, and recorded as the column's owner. The stamper cannot derive it: it
    /// receives an entry out of an already-fanned list and can only count.</summary>
    public StampId Stamp { get; init; }

    /// <summary>The team that defends this wool, by id.</summary>
    public string Owner { get; init; } = "";
    /// <summary>Dye colour (slug, e.g. <c>light_blue</c>). Empty → defaults to the owner team's colour.</summary>
    public string Color { get; init; } = "";
    /// <summary>The region the wool room owns, as a union of rectangles — the same field a spawn carries
    /// under the same name, because it is the same thing. It is both the room region the generator emits and
    /// <b>the ground the cage stands on</b> (WX1/WX12). Empty until the author draws it (partial intent is
    /// tolerated, new-map-authoring.md §7): a roomless wool still generates its objective + monuments, just
    /// not the room region / spawner / room wiring. Tolerates a legacy single-object blob on read (see the
    /// converter).</summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(RectListJsonConverter))]
    public List<Rect> Protection { get; init; } = new();
    /// <summary>Where the wool is dispensed. The objective's <c>location</c> is this floored to whole
    /// blocks.</summary>
    public Pt Spawn { get; init; }

    /// <summary>Where it may be placed to score — one per capturing team, which is every team but the
    /// owner.</summary>
    public List<MonumentIntent> Monuments { get; init; } = new();

    /// <summary>The cage raised on the region — the footprint the shell is stamped on, which the plan states
    /// and the author resizes. Null leaves it to the default: the region inset by a block on every side
    /// (WX1). A wool room names no door, so no side of it is opened wider than the rest.</summary>
    public Rect? Footprint { get; init; }

    /// <summary>The room's entry interfaces (WX6), as degenerate rects on the region's boundary (zero
    /// thickness across the seam), already fanned: every terrain↔room land seam plus every abutting
    /// build-zone edge. The exporter cuts the cage doors and lays the entrance redstone on exactly these.
    /// Empty where the plan derived none, and a cage with no entry keeps a door per wall.</summary>
    public List<Rect> Entries { get; init; } = new();
}

/// <summary>A capture point: the team that captures this wool, and where they place it.</summary>
public sealed record MonumentIntent
{
    /// <summary>Which layer's surface this stands on, or null for the top one. A stacked board has a surface
    /// per layer, and a thing stated for a hall lands on the deck roofing it unless it says which layer it
    /// meant.</summary>
    public string? Layer { get; init; }

    /// <summary>The team that captures the wool here, by id.</summary>
    public string Team { get; init; } = "";

    /// <summary>The block the wool is placed on.</summary>
    public Pt Location { get; init; }
}

/// <summary>
/// One destroyable (DTM) objective: the <see cref="Materials"/> blocks of a <see cref="Style"/> structure
/// standing over <see cref="Anchor"/>, defended by <see cref="Owner"/> and broken by every other team.
/// <para>The structure parameters are already resolved here — the plan's optional fields are defaulted by the
/// compiler, so a consumer never re-decides them. <see cref="Anchor"/> is the marker column, not the
/// structure's corner: the box is centred on it and floats <see cref="Float"/> blocks above the surface its
/// footprint spans, so its Y is a function of the terrain rather than an authored number.</para>
/// <para><see cref="Anchor"/>.Y carries the plan's flat nominal height (or the global surface, for a marker
/// placed with no piece) purely as information for a caller with no built world to read yet, such as
/// the plan editor's own preview; it is never authoritative and no consumer downstream of the world build may
/// read it. <see cref="Box"/>.MinY, once the world is built, is the real answer — resolved by
/// <c>WorldBuilder</c> from the terrain the relief actually solved under the column, plus
/// <see cref="Float"/> — and the only place a compiled-but-unbuilt intent's height claim should be trusted at
/// all is that it names the right ground column (Anchor.X/Z), not the right ground level.</para>
/// </summary>
public sealed record DestroyableIntent
{
    /// <summary>Which layer's surface this stands on, or null for the top one. A stacked board has a surface
    /// per layer, and a thing stated for a hall lands on the deck roofing it unless it says which layer it
    /// meant.</summary>
    public string? Layer { get; init; }

    /// <summary>Which authored unit this is an image of, and which image — set by whoever fanned the orbit,
    /// carried through to the stamper, and recorded as the column's owner. The stamper cannot derive it: it
    /// receives an entry out of an already-fanned list and can only count.</summary>
    public StampId Stamp { get; init; }

    /// <summary>The DEFENDING team — the same meaning as <see cref="WoolIntent.Owner"/>.</summary>
    public string Owner { get; init; } = "";
    /// <summary>Required by PGM, which rejects a nameless destroyable; the compiler auto-names from the owner
    /// and index rather than pushing the burden to the author.</summary>
    public string Name { get; init; } = "";
    /// <summary>pillar-1|2|3 · cube-3 · cube-4 · column-plus.</summary>
    public string Style { get; init; } = DestroyableStyles.Slug(ObjectiveDefaults.Style);
    /// <summary>A PGM material match — the goal is these blocks, not the region that holds them.</summary>
    public string Materials { get; init; } = ObjectiveDefaults.Materials;

    /// <summary>The marker column. The structure is centred on it and floats above the terrain, so no Y is
    /// authored.</summary>
    public Pt Anchor { get; init; }
    /// <summary>Blocks of air between the ground the world build solves under <see cref="Anchor"/>'s column and
    /// the structure's underside — an offset over the ground as built, not the plan's flat nominal
    /// surface.</summary>
    public int Float { get; init; } = ObjectiveDefaults.DestroyableFloat;

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
/// <para><see cref="Anchor"/>.Y is informational only, the same as a destroyable's
/// (<see cref="DestroyableIntent"/>) — the casing's real floor is <see cref="Box"/>.MinY, resolved from the
/// terrain the world build actually solved under the column plus <see cref="Float"/>.</para>
/// </summary>
public sealed record CoreIntent
{
    /// <summary>Which layer's surface this stands on, or null for the top one. A stacked board has a surface
    /// per layer, and a thing stated for a hall lands on the deck roofing it unless it says which layer it
    /// meant.</summary>
    public string? Layer { get; init; }

    /// <summary>Which authored unit this is an image of, and which image — set by whoever fanned the orbit,
    /// carried through to the stamper, and recorded as the column's owner. The stamper cannot derive it: it
    /// receives an entry out of an already-fanned list and can only count.</summary>
    public StampId Stamp { get; init; }

    /// <summary>The DEFENDING team. Emitted as the XML's <c>team</c> attribute, not <c>owner</c> — a PGM
    /// inconsistency we mirror in the XML while naming the field for what it means.</summary>
    public string Owner { get; init; } = "";
    /// <summary>Optional: PGM auto-names a core per team, unlike a destroyable, which it rejects nameless.</summary>
    public string Name { get; init; } = "";
    /// <summary>The marker column. The casing is centred on it and floats above the terrain, so no Y is authored.</summary>
    public Pt Anchor { get; init; }
    /// <summary>The lava's own footprint, in blocks (2–5). A core is stated by its <b>interior</b> rather
    /// than by a casing size and a wall thickness: those are two numbers that can disagree, and an author
    /// who typed a 5 against a 3 got a solid block of obsidian nothing could leak. The casing follows.</summary>
    public int Lava { get; init; } = ObjectiveDefaults.CoreLava;

    /// <summary>How many courses of lava stand inside it (2–5).</summary>
    public int LavaHeight { get; init; } = ObjectiveDefaults.CoreLavaHeight;

    /// <summary>Omit the cap layer so the lava reaches the casing rim.</summary>
    public bool OpenTop { get; init; }

    /// <summary>The casing's width and depth, in blocks — the lava walled on both sides.</summary>
    [JsonIgnore] public int Size => ObjectiveDefaults.CoreCasing(Lava, LavaHeight, OpenTop).Size;

    /// <summary>Its height: the lava's courses, its floor, and the cap where there is one.</summary>
    [JsonIgnore] public int Height => ObjectiveDefaults.CoreCasing(Lava, LavaHeight, OpenTop).Height;

    /// <summary>How thick its wall is. Fixed rather than authored — the lava footprint is measured inside
    /// it, so a second thickness knob would only be a way of contradicting the first number.</summary>
    [JsonIgnore] public int Shell => ObjectiveDefaults.CoreShell;
    /// <summary>Blocks of air between the ground the world build solves under <see cref="Anchor"/>'s column and
    /// the casing's underside — an offset over the ground as built, not the plan's flat nominal surface.
    /// Pairs with <see cref="Leak"/> (DC2).</summary>
    public int Float { get; init; } = ObjectiveDefaults.CoreFloat;

    /// <summary>How far the lava must fall below the casing to count as leaked. Together with
    /// <see cref="Float"/> it says how far players must dig — <c>max(0, leak − float)</c>.</summary>
    public int Leak { get; init; } = ObjectiveDefaults.CoreLeak;

    /// <summary>The casing's resolved block volume — filled by the world-export path, the only place that
    /// knows the terrain it floats over. The generator emits it verbatim as the core's <c>&lt;region&gt;</c>,
    /// so the blocks and the region scoping them come from the one box the stamper used (OB8). Null on an
    /// intent that has not been through the world build.</summary>
    public BlockBox? Box { get; init; }

    /// <summary>How many blocks players must dig into the terrain under the core before its lava can leak.
    /// Zero when breaching the casing is enough on its own.</summary>
    public int DigDepth => ObjectiveDefaults.DigDepth(Leak, Float);
}

/// <summary>A world point (spawn location).</summary>
/// <param name="X">Its east–west position, in blocks.</param>
/// <param name="Y">Its height.</param>
/// <param name="Z">Its north–south position.</param>
public readonly record struct Pt(double X, double Y, double Z);

