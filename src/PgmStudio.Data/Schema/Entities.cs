using LinqToDB.Mapping;

namespace PgmStudio.Data.Schema;

// linq2db row mappings for the hybrid schema (see PgmStudio.Migrations M0001_InitialSchema).
// `*Json` columns hold serialized JSON strings; `*Key` columns hold PGM string ids (unique per map).

[Table("map")]
public sealed class MapRow
{
    [PrimaryKey, Identity, Column("id")] public long Id { get; set; }
    [Column("slug"), NotNull] public string Slug { get; set; } = "";
    [Column("name"), NotNull] public string Name { get; set; } = "";
    [Column("version")] public string? Version { get; set; }
    [Column("gamemode")] public string? Gamemode { get; set; }
    [Column("objective")] public string? Objective { get; set; }
    [Column("max_build_height")] public double? MaxBuildHeight { get; set; }
    // Lifecycle stage: plan | sketch | configure | edit (see Contracts.MapStage). Drives the staged dashboard.
    [Column("stage"), NotNull] public string Stage { get; set; } = "edit";
    // For a stage=plan map authored from a generator candidate: the source `plan` row id (provenance).
    [Column("plan_source_id")] public long? PlanSourceId { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
    /// <summary>Which version of this map's document the row holds — bumped by every write that replaces it,
    /// answered as an <c>ETag</c>, and what an <c>If-Match</c> is checked against. Not
    /// <see cref="Version"/>, which is the version string PGM reads.</summary>
    [Column("revision"), NotNull] public long Revision { get; set; } = 1;
}

[Table("author")]
public sealed class AuthorRow
{
    [PrimaryKey, Identity, Column("id")] public long Id { get; set; }
    [Column("map_id"), NotNull] public long MapId { get; set; }
    [Column("uuid"), NotNull] public string Uuid { get; set; } = "";
    [Column("role"), NotNull] public string Role { get; set; } = "";
    [Column("contribution")] public string? Contribution { get; set; }
    [Column("name")] public string? Name { get; set; }
}

[Table("team")]
public sealed class TeamRow
{
    [PrimaryKey, Identity, Column("id")] public long Id { get; set; }
    [Column("map_id"), NotNull] public long MapId { get; set; }
    [Column("team_key"), NotNull] public string TeamKey { get; set; } = "";
    [Column("name"), NotNull] public string Name { get; set; } = "";
    [Column("color"), NotNull] public string Color { get; set; } = "";
    [Column("dye_color"), NotNull] public string DyeColor { get; set; } = "";
    [Column("max_players"), NotNull] public int MaxPlayers { get; set; }
    [Column("min_players"), NotNull] public int MinPlayers { get; set; }
}

[Table("kit")]
public sealed class KitRow
{
    [PrimaryKey, Identity, Column("id")] public long Id { get; set; }
    [Column("map_id"), NotNull] public long MapId { get; set; }
    [Column("kit_key"), NotNull] public string KitKey { get; set; } = "";
    [Column("force")] public bool? Force { get; set; }
    [Column("clear")] public bool? Clear { get; set; }
    [Column("effects_json")] public string? EffectsJson { get; set; }   // [{type,duration,amplifier}]
}

[Table("kit_item")]
public sealed class KitItemRow
{
    [PrimaryKey, Identity, Column("id")] public long Id { get; set; }
    [Column("kit_id"), NotNull] public long KitId { get; set; }
    [Column("slot")] public int? Slot { get; set; }
    [Column("material"), NotNull] public string Material { get; set; } = "";
    [Column("amount")] public int? Amount { get; set; }
    [Column("damage")] public int? Damage { get; set; }
    [Column("unbreakable")] public bool? Unbreakable { get; set; }
    [Column("team_color")] public bool? TeamColor { get; set; }
    [Column("enchantments")] public string? Enchantments { get; set; }
}

[Table("kit_armor")]
public sealed class KitArmorRow
{
    [PrimaryKey, Identity, Column("id")] public long Id { get; set; }
    [Column("kit_id"), NotNull] public long KitId { get; set; }
    [Column("slot_name"), NotNull] public string SlotName { get; set; } = "";
    [Column("material"), NotNull] public string Material { get; set; } = "";
    [Column("unbreakable")] public bool? Unbreakable { get; set; }
    [Column("team_color")] public bool? TeamColor { get; set; }
    [Column("enchantments")] public string? Enchantments { get; set; }
}

[Table("region")]
public sealed class RegionRow
{
    [PrimaryKey, Identity, Column("id")] public long Id { get; set; }
    [Column("map_id"), NotNull] public long MapId { get; set; }
    [Column("region_key"), NotNull] public string RegionKey { get; set; } = "";
    [Column("type"), NotNull] public string Type { get; set; } = "";
    [Column("bounds_json")] public string? BoundsJson { get; set; }
    [Column("coords_json")] public string? CoordsJson { get; set; }
    [Column("child_ref_ids_json")] public string? ChildRefIdsJson { get; set; }
    [Column("source_id")] public string? SourceId { get; set; }
}

[Table("filter")]
public sealed class FilterRow
{
    [PrimaryKey, Identity, Column("id")] public long Id { get; set; }
    [Column("map_id"), NotNull] public long MapId { get; set; }
    [Column("filter_key"), NotNull] public string FilterKey { get; set; } = "";
    [Column("type"), NotNull] public string Type { get; set; } = "";
    [Column("child_ref_ids_json")] public string? ChildRefIdsJson { get; set; }
    [Column("child_key")] public string? ChildKey { get; set; }
    [Column("region_key")] public string? RegionKey { get; set; }
    [Column("params_json")] public string? ParamsJson { get; set; }
}

[Table("wool")]
public sealed class WoolRow
{
    [PrimaryKey, Identity, Column("id")] public long Id { get; set; }
    [Column("map_id"), NotNull] public long MapId { get; set; }
    [Column("wool_key"), NotNull] public string WoolKey { get; set; } = "";
    [Column("color"), NotNull] public string Color { get; set; } = "";
    [Column("location_json")] public string? LocationJson { get; set; }
    [Column("wool_room_region_key")] public string? WoolRoomRegionKey { get; set; }
    [Column("team")] public string? Team { get; set; }
}

[Table("monument")]
public sealed class MonumentRow
{
    [PrimaryKey, Identity, Column("id")] public long Id { get; set; }
    [Column("wool_id"), NotNull] public long WoolId { get; set; }
    [Column("monument_key"), NotNull] public string MonumentKey { get; set; } = "";
    [Column("team"), NotNull] public string Team { get; set; } = "";
    [Column("location_json")] public string? LocationJson { get; set; }
    [Column("monument_region_key")] public string? MonumentRegionKey { get; set; }
}

// The DTM/DTC objectives hang off map_id, not off wool: a destroyable has no wool, and MonumentRow's
// wool_id FK would make one unrepresentable. "Monument" here always means the CTW wool monument.
[Table("destroyable")]
public sealed class DestroyableRow
{
    [PrimaryKey, Identity, Column("id")] public long Id { get; set; }
    [Column("map_id"), NotNull] public long MapId { get; set; }
    [Column("destroyable_key"), NotNull] public string DestroyableKey { get; set; } = "";
    [Column("name"), NotNull] public string Name { get; set; } = "";
    [Column("owner"), NotNull] public string Owner { get; set; } = "";
    [Column("region_key")] public string? RegionKey { get; set; }
    [Column("materials"), NotNull] public string Materials { get; set; } = "";
    [Column("completion")] public double? Completion { get; set; }
    [Column("show"), NotNull] public bool Show { get; set; } = true;
    [Column("mode_changes"), NotNull] public bool ModeChanges { get; set; }
    [Column("modes_json")] public string? ModesJson { get; set; }
}

[Table("core")]
public sealed class CoreRow
{
    [PrimaryKey, Identity, Column("id")] public long Id { get; set; }
    [Column("map_id"), NotNull] public long MapId { get; set; }
    [Column("core_key"), NotNull] public string CoreKey { get; set; } = "";
    [Column("name")] public string? Name { get; set; }
    [Column("owner"), NotNull] public string Owner { get; set; } = "";
    [Column("region_key")] public string? RegionKey { get; set; }
    [Column("material")] public string? Material { get; set; }
    [Column("leak")] public int? Leak { get; set; }
    [Column("mode_changes"), NotNull] public bool ModeChanges { get; set; }
    [Column("modes_json")] public string? ModesJson { get; set; }
}

[Table("mode")]
public sealed class ModeRow
{
    [PrimaryKey, Identity, Column("id")] public long Id { get; set; }
    [Column("map_id"), NotNull] public long MapId { get; set; }
    [Column("mode_key"), NotNull] public string ModeKey { get; set; } = "";
    [Column("name")] public string? Name { get; set; }
    [Column("after"), NotNull] public string After { get; set; } = "";
    [Column("material")] public string? Material { get; set; }
    [Column("show_before")] public string? ShowBefore { get; set; }
    [Column("filter_key")] public string? FilterKey { get; set; }
    [Column("action_key")] public string? ActionKey { get; set; }
}

[Table("spawn")]
public sealed class SpawnRow
{
    [PrimaryKey, Identity, Column("id")] public long Id { get; set; }
    [Column("map_id"), NotNull] public long MapId { get; set; }
    [Column("is_observer"), NotNull] public bool IsObserver { get; set; }
    [Column("team"), NotNull] public string Team { get; set; } = "";
    [Column("kit")] public string? Kit { get; set; }
    [Column("yaw"), NotNull] public double Yaw { get; set; }
    [Column("region_key")] public string? RegionKey { get; set; }
    [Column("region_json")] public string? RegionJson { get; set; }
}

[Table("map_spawner")]
public sealed class MapSpawnerRow
{
    [PrimaryKey, Identity, Column("id")] public long Id { get; set; }
    [Column("map_id"), NotNull] public long MapId { get; set; }
    [Column("spawn_region_key")] public string? SpawnRegionKey { get; set; }
    [Column("player_region_key")] public string? PlayerRegionKey { get; set; }
    [Column("delay")] public string? Delay { get; set; }
    [Column("max_entities")] public int? MaxEntities { get; set; }
    [Column("items_json")] public string? ItemsJson { get; set; }
}

[Table("renewable")]
public sealed class RenewableRow
{
    [PrimaryKey, Identity, Column("id")] public long Id { get; set; }
    [Column("map_id"), NotNull] public long MapId { get; set; }
    [Column("region_key")] public string? RegionKey { get; set; }
    [Column("rate")] public double? Rate { get; set; }
    [Column("renew_filter")] public string? RenewFilter { get; set; }
    [Column("replace_filter")] public string? ReplaceFilter { get; set; }
    [Column("grow")] public bool? Grow { get; set; }
}

[Table("block_drop_rule")]
public sealed class BlockDropRuleRow
{
    [PrimaryKey, Identity, Column("id")] public long Id { get; set; }
    [Column("map_id"), NotNull] public long MapId { get; set; }
    [Column("region_key")] public string? RegionKey { get; set; }
    [Column("filter_key")] public string? FilterKey { get; set; }
    [Column("replacement")] public string? Replacement { get; set; }
    [Column("wrong_tool")] public bool? WrongTool { get; set; }
    [Column("items_json")] public string? ItemsJson { get; set; }
}

[Table("apply_rule")]
public sealed class ApplyRuleRow
{
    [PrimaryKey, Identity, Column("id")] public long Id { get; set; }
    [Column("map_id"), NotNull] public long MapId { get; set; }
    [Column("rule_key")] public string? RuleKey { get; set; }
    [Column("region_key")] public string? RegionKey { get; set; }
    [Column("events_json")] public string? EventsJson { get; set; }
}

[Table("wool_block")]
public sealed class WoolBlockRow
{
    [PrimaryKey, Identity, Column("id")] public long Id { get; set; }
    [Column("map_id"), NotNull] public long MapId { get; set; }
    [Column("world_x"), NotNull] public int WorldX { get; set; }
    [Column("world_z"), NotNull] public int WorldZ { get; set; }
    [Column("world_y"), NotNull] public int WorldY { get; set; }
    [Column("color"), NotNull] public string Color { get; set; } = "";
}

[Table("resource_block")]
public sealed class ResourceBlockRow
{
    [PrimaryKey, Identity, Column("id")] public long Id { get; set; }
    [Column("map_id"), NotNull] public long MapId { get; set; }
    [Column("world_x"), NotNull] public int WorldX { get; set; }
    [Column("world_z"), NotNull] public int WorldZ { get; set; }
    [Column("world_y"), NotNull] public int WorldY { get; set; }
    [Column("resource_type"), NotNull] public string ResourceType { get; set; } = "";
}

[Table("chest_item")]
public sealed class ChestItemRow
{
    [PrimaryKey, Identity, Column("id")] public long Id { get; set; }
    [Column("map_id"), NotNull] public long MapId { get; set; }
    [Column("world_x"), NotNull] public int WorldX { get; set; }
    [Column("world_z"), NotNull] public int WorldZ { get; set; }
    [Column("world_y"), NotNull] public int WorldY { get; set; }
    [Column("chest_type"), NotNull] public string ChestType { get; set; } = "";
    [Column("slot"), NotNull] public int Slot { get; set; }
    [Column("item_id"), NotNull] public string ItemId { get; set; } = "";
    [Column("item_damage"), NotNull] public int ItemDamage { get; set; }
    [Column("count"), NotNull] public int Count { get; set; }
}

[Table("spawner_block")]
public sealed class SpawnerBlockRow
{
    [PrimaryKey, Identity, Column("id")] public long Id { get; set; }
    [Column("map_id"), NotNull] public long MapId { get; set; }
    [Column("world_x"), NotNull] public int WorldX { get; set; }
    [Column("world_z"), NotNull] public int WorldZ { get; set; }
    [Column("world_y"), NotNull] public int WorldY { get; set; }
    [Column("entity_id")] public string? EntityId { get; set; }
    [Column("spawns_wool")] public bool? SpawnsWool { get; set; }
    [Column("spawn_item_id")] public string? SpawnItemId { get; set; }
    [Column("spawn_item_damage")] public int? SpawnItemDamage { get; set; }
    [Column("spawn_count")] public int? SpawnCount { get; set; }
    [Column("spawn_range")] public int? SpawnRange { get; set; }
    [Column("min_spawn_delay")] public int? MinSpawnDelay { get; set; }
    [Column("max_spawn_delay")] public int? MaxSpawnDelay { get; set; }
    [Column("required_player_range")] public int? RequiredPlayerRange { get; set; }
    [Column("max_nearby_entities")] public int? MaxNearbyEntities { get; set; }
}

/// <summary>A proposed DTC core (<c>core_candidate</c>) — the ingest output of <c>CoreSuggester.Gather</c>,
/// stored because the world it was read from is discarded straight afterwards. Mirrors
/// <c>CoreSuggestion</c> (PgmStudio.Minecraft) plus its map.</summary>
[Table("core_candidate")]
public sealed class CoreCandidateRow
{
    [PrimaryKey, Identity, Column("id")] public long Id { get; set; }
    [Column("map_id"), NotNull] public long MapId { get; set; }
    [Column("min_x"), NotNull] public int MinX { get; set; }
    [Column("min_y"), NotNull] public int MinY { get; set; }
    [Column("min_z"), NotNull] public int MinZ { get; set; }
    [Column("max_x"), NotNull] public int MaxX { get; set; }
    [Column("max_y"), NotNull] public int MaxY { get; set; }
    [Column("max_z"), NotNull] public int MaxZ { get; set; }
    [Column("lava_blocks"), NotNull] public int LavaBlocks { get; set; }
    [Column("shell"), NotNull] public int Shell { get; set; }
    [Column("float_blocks"), NotNull] public int FloatBlocks { get; set; }
    [Column("open_top"), NotNull] public bool OpenTop { get; set; }
}

/// <summary>A gathered monument candidate (F9, <c>monument_candidate</c>) — the style-agnostic ingest
/// output of <c>MonumentSuggester.Gather</c>; the authoring <c>Score</c> reads these back per map + box.
/// Mirrors <c>MonumentCandidate</c> (PgmStudio.Minecraft) minus <c>Id</c>/<c>MapId</c>.</summary>
[Table("monument_candidate")]
public sealed class MonumentCandidateRow
{
    [PrimaryKey, Identity, Column("id")] public long Id { get; set; }
    [Column("map_id"), NotNull] public long MapId { get; set; }
    [Column("cand_x"), NotNull] public int CandX { get; set; }
    [Column("cand_y"), NotNull] public int CandY { get; set; }
    [Column("cand_z"), NotNull] public int CandZ { get; set; }
    [Column("source"), NotNull] public string Source { get; set; } = "";
    [Column("pedestal_id"), NotNull] public int PedestalId { get; set; }
    [Column("pedestal_data"), NotNull] public int PedestalData { get; set; }
    [Column("cap_id"), NotNull] public int CapId { get; set; }
    [Column("cap_data"), NotNull] public int CapData { get; set; }
    [Column("color_hint")] public string? ColorHint { get; set; }
    [Column("sign_x")] public int? SignX { get; set; }
    [Column("sign_y")] public int? SignY { get; set; }
    [Column("sign_z")] public int? SignZ { get; set; }
    [Column("sign_facing")] public int? SignFacing { get; set; }
    [Column("sign_text")] public string? SignText { get; set; }
    [Column("stand_head_color")] public string? StandHeadColor { get; set; }
    [Column("stand_name")] public string? StandName { get; set; }
    [Column("evidence")] public string? Evidence { get; set; }
}

[Table("segment")]
public sealed class SegmentRow
{
    [PrimaryKey, Identity, Column("id")] public long Id { get; set; }
    [Column("map_id"), NotNull] public long MapId { get; set; }
    [Column("world_x"), NotNull] public int WorldX { get; set; }
    [Column("world_z"), NotNull] public int WorldZ { get; set; }
    [Column("world_y_start"), NotNull] public int WorldYStart { get; set; }
    [Column("world_y_end"), NotNull] public int WorldYEnd { get; set; }
}

[Table("map_artifact")]
public sealed class MapArtifactRow
{
    [PrimaryKey, Identity, Column("id")] public long Id { get; set; }
    [Column("map_id"), NotNull] public long MapId { get; set; }
    [Column("kind"), NotNull] public string Kind { get; set; } = "";
    [Column("data"), NotNull] public byte[] Data { get; set; } = [];
    /// <summary>Which version of this artifact the row holds — bumped by every save, answered as an
    /// <c>ETag</c>, and what an <c>If-Match</c> is checked against.</summary>
    [Column("revision"), NotNull] public long Revision { get; set; } = 1;
}

/// <summary>Map symmetry (confirmed in Configure's World phase, docs/tools/configure.md) — promoted from the
/// <c>symmetry_json</c> artifact to a first-class row (one per map). The scalars are what consumers query
/// (orbit, counterpart, team-count, the World step); <c>ModesJson</c> is the irregular candidate list;
/// <c>center_cell</c> and the <c>primary</c> projection are derived on read. <c>ExcludedIslandsJson</c> /
/// <c>DetectionLayer</c> are the authoring World-step inputs (populated by N01; null for existing maps).</summary>
[Table("symmetry")]
public sealed class SymmetryRow
{
    [PrimaryKey, Column("map_id")] public long MapId { get; set; }
    [Column("status"), NotNull] public string Status { get; set; } = "unconfirmed";  // unconfirmed | confirmed | none
    [Column("center_x")] public double? CenterX { get; set; }
    [Column("center_z")] public double? CenterZ { get; set; }
    [Column("primary_type")] public string? PrimaryType { get; set; }
    [Column("primary_confidence")] public double? PrimaryConfidence { get; set; }
    [Column("primary_user_override"), NotNull] public bool PrimaryUserOverride { get; set; }
    [Column("modes_json"), NotNull] public string ModesJson { get; set; } = "[]";     // [{type,detected,confidence}]
    [Column("excluded_islands_json")] public string? ExcludedIslandsJson { get; set; } // §6b authoring input
    [Column("detection_layer")] public string? DetectionLayer { get; set; }            // §6b: cleanbase|bedrock|y0
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
}

/// <summary>Well-known <see cref="MapArtifactRow.Kind"/> values.</summary>
public static class ArtifactKind
{
    public const string SurfaceParquet = "surface_parquet";
    public const string IslandsJson = "islands_json";
    // symmetry_json was promoted to the `symmetry` table (M0003).
    public const string MapConfigJson = "map_config_json";
    // Editor-only sidecar: {region_key: editor_step} for freshly drawn, not-yet-wired regions (E10).
    // Lives outside the entity-replace codec so it survives MapWriter.SaveDocAsync; never part of the
    // PGM map document. Pruned against live regions on read; entries graduate out once a region's
    // derived category is no longer "other".
    public const string RegionDraftsJson = "region_drafts_json";
    // Declarative authoring intent for NEW maps (docs/pgm/new-map-authoring.md): the source of
    // truth the generator projects into regions/filters/apply-rules. Like the draft sidecar it lives
    // outside the entity-replace codec, so it survives MapWriter.SaveDocAsync and is never part of the
    // PGM document. Presence of this artifact is what makes a map "intent-authored".
    public const string MapIntentJson = "map_intent_json";
    // Sketch tool authoring source (docs/tools/sketch.md): the drawn layout (setup +
    // shapes + island metadata, the browser's JS-origin blob) for a draft map. Like the intent/draft
    // sidecars it lives outside the entity-replace codec. A draft map with this artifact but no
    // layer_parquet is a sketch-in-progress; "finish" rasterizes it into the geometry artifacts (S2e).
    public const string SketchLayoutJson = "sketch_layout_json";
    // Plan tool authoring source (docs/tools/plan.md): the plan blob (cell-grid layout +
    // globals) for a map at stage=plan. Like the sketch layout it lives outside the entity-replace codec.
    // An authored plan is a map row with this artifact; the map's plan_source_id links back to the
    // generator `plan` candidate it was authored from.
    public const string PlanJson = "plan_json";
    // The Douglas-Peucker simplified island outlines of an EXISTING map, in the sketch layout format
    // (one "add" polygon per island + a "subtract" per hole). Derived from islands_json — distinct from the
    // authored SketchLayoutJson so it neither re-stages the map to Sketch nor clobbers a real draft sketch.
    public const string IslandSketchJson = "island_sketch_json";
    // Hand-cut lane decompositions gathered with the retired decompose surface (island_sketch outlines
    // cut into role-tagged lane polygons, sketch layout format). Stored data kept; no writer remains.
    public const string LaneDecompositionJson = "lane_decomposition_json";
}

/// <summary>A persisted layout plan (see M0008_Plan). A standalone corpus row — no map FK. <see cref="Origin"/>
/// is generated | authored | imported; the descriptor columns (<see cref="RequestJson"/>/<see cref="Seed"/>/
/// <see cref="ComposerVersion"/>) are populated only for generated rows. <see cref="ParentId"/> is the
/// fork-provenance back-reference.</summary>
[Table("plan")]
public sealed class PlanRow
{
    [PrimaryKey, Identity, Column("id")] public long Id { get; set; }
    [Column("name"), NotNull] public string Name { get; set; } = "";
    [Column("origin"), NotNull] public string Origin { get; set; } = "";
    [Column("plan_json"), NotNull] public string PlanJson { get; set; } = "";
    [Column("content_hash"), NotNull] public string ContentHash { get; set; } = "";
    [Column("parent_id")] public long? ParentId { get; set; }
    [Column("request_json")] public string? RequestJson { get; set; }
    [Column("seed")] public ulong? Seed { get; set; }
    [Column("composer_version")] public string? ComposerVersion { get; set; }
    [Column("structure")] public string? Structure { get; set; }   // canonical StructureSummary bucket key (generated rows)
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
}

/// <summary>Well-known <see cref="PlanRow.Origin"/> values.</summary>
public static class PlanOrigin
{
    public const string Generated = "generated";
    public const string Authored = "authored";
    public const string Imported = "imported";
}

/// <summary>A reusable terrain-paint <see cref="Style"/> — one named material recipe (see M0011). <see cref="Kind"/>
/// is the material discriminator (<c>MaterialKind</c>); <see cref="Params"/> is one serialized
/// <c>TerrainMaterial</c>, the polymorphic subtree kept in the leaf. The unit a library is browsed and reused by.</summary>
[Table("style")]
public sealed class StyleRow
{
    [PrimaryKey, Identity, Column("id")] public long Id { get; set; }
    [Column("name"), NotNull] public string Name { get; set; } = "";
    [Column("kind"), NotNull] public string Kind { get; set; } = "";
    [Column("params_json"), NotNull] public string Params { get; set; } = "";
    [Column("created_at")] public DateTime CreatedAt { get; set; }
}

/// <summary>A terrain-paint theme's geometry knobs (see M0011). The per-bucket materials live in
/// <see cref="ThemeBucketRow"/> — a theme is a composition of styles, not a monolith.</summary>
[Table("theme")]
public sealed class ThemeRow
{
    [PrimaryKey, Identity, Column("id")] public long Id { get; set; }
    [Column("name"), NotNull] public string Name { get; set; } = "";
    [Column("bedrock_relative")] public bool BedrockRelative { get; set; }
    [Column("bedrock_value")] public int BedrockValue { get; set; }
    [Column("rim_edges"), NotNull] public string RimEdges { get; set; } = "drop";   // void | drop | boundary
    [Column("wall_on_terrain_faces")] public bool WallOnTerrainFaces { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
}

/// <summary>One bucket binding of a <see cref="ThemeRow"/> (see M0011): the themeable bucket
/// (<c>ThemeBuckets</c>), the <see cref="StyleRow"/> that fills it, and the bucket's depth (rim/surface)
/// and toggle. A null <see cref="StyleId"/> binds no style (M0013): the bucket keeps the built-in material
/// and the row is carried for its depth and toggle alone, which is how a theme stores "no rim".
/// Unique per (theme, bucket); cascades with its theme, restricts its style.</summary>
[Table("theme_bucket")]
public sealed class ThemeBucketRow
{
    [PrimaryKey, Identity, Column("id")] public long Id { get; set; }
    [Column("theme_id"), NotNull] public long ThemeId { get; set; }
    [Column("bucket"), NotNull] public string Bucket { get; set; } = "";
    [Column("style_id")] public long? StyleId { get; set; }
    [Column("depth")] public int Depth { get; set; }
    [Column("enabled")] public bool Enabled { get; set; }
}

/// <summary>A room shell's finish (see M0012): the per-part extents plus the knobs that are not materials —
/// the eave, the roof hole, and the door. The materials live in <see cref="RoomStyleCourseRow"/>, because a
/// shell's part is a stack of them rather than one.</summary>
[Table("room_style")]
public sealed class RoomStyleRow
{
    [PrimaryKey, Identity, Column("id")] public long Id { get; set; }
    [Column("name"), NotNull] public string Name { get; set; } = "";
    [Column("floor_depth")] public int FloorDepth { get; set; } = 1;
    [Column("wall_height")] public int WallHeight { get; set; } = 7;
    [Column("roof_form"), NotNull] public string RoofForm { get; set; } = "flat";
    [Column("pitch")] public int Pitch { get; set; } = 1;
    [Column("overhang")] public int Overhang { get; set; }
    [Column("roof_hole")] public bool RoofHole { get; set; } = true;
    [Column("ridge_cap")] public bool RidgeCap { get; set; }

    // The storeys stacked inside (M0017). One is the building every row was before there were storeys;
    // storey_clear 0 defers to wall_height, so a single-storey row is untouched by either column.
    [Column("storeys")] public int Storeys { get; set; } = 1;
    [Column("storey_clear")] public int StoreyClear { get; set; }
    [Column("door"), NotNull] public string Door { get; set; } = "stained-glass-pane";
    [Column("door_height")] public int DoorHeight { get; set; } = 3;

    // The floor's top course in plan (M0016): how wide the ring hugging the walls is and how far in the
    // centred plate starts. Their materials are courses bound to the border/field/inlay parts, so only the
    // two distances are columns.
    [Column("border_width")] public int BorderWidth { get; set; } = 1;
    [Column("inlay_inset")] public int InlayInset { get; set; } = 2;

    // The windows (M0016). The block is an id rather than a bound style because its metadata is geometry.
    [Column("window_form"), NotNull] public string WindowForm { get; set; } = "none";
    [Column("window_block")] public int WindowBlock { get; set; } = 102;
    [Column("window_data")] public int WindowData { get; set; }
    [Column("window_sill")] public int WindowSill { get; set; } = 2;
    [Column("window_width")] public int WindowWidth { get; set; } = 2;
    [Column("window_height")] public int WindowHeight { get; set; } = 2;
    [Column("window_spacing")] public int WindowSpacing { get; set; } = 3;

    // The porch (M0016). Depth 0 is no porch at all, which is why it needs no flag of its own.
    [Column("porch_depth")] public int PorchDepth { get; set; }
    [Column("porch_inset")] public int PorchInset { get; set; }
    [Column("porch_edge"), NotNull] public string PorchEdge { get; set; } = "front";
    [Column("porch_roof"), NotNull] public string PorchRoof { get; set; } = "shed";
    [Column("porch_rail_block")] public int PorchRailBlock { get; set; } = 85;

    // The part styles this house is composed from (M0018). Each is optional and each, when bound, takes over
    // from the columns above that describe the same part — so an unbound house is exactly the building its own
    // columns always described, and no row had to be migrated to gain the level.
    [Column("beam_block")] public int BeamBlock { get; set; } = -1;
    [Column("beam_data")] public int BeamData { get; set; }
    [Column("beam_reach")] public int BeamReach { get; set; } = 1;
    [Column("roof_slab")] public int RoofSlab { get; set; } = -1;
    [Column("roof_slab_data")] public int RoofSlabData { get; set; }
    [Column("gable_window_form"), NotNull] public string GableWindowForm { get; set; } = "none";
    [Column("gable_window_block")] public int GableWindowBlock { get; set; } = 102;
    [Column("gable_window_data")] public int GableWindowData { get; set; }
    [Column("gable_window_sill")] public int GableWindowSill { get; set; } = 1;
    [Column("gable_window_width")] public int GableWindowWidth { get; set; } = 1;
    [Column("gable_window_height")] public int GableWindowHeight { get; set; } = 1;
    [Column("door_head_form"), NotNull] public string DoorHeadForm { get; set; } = "none";
    [Column("door_head_block")] public int DoorHeadBlock { get; set; } = 53;
    [Column("door_head_fill"), NotNull] public string DoorHeadFill { get; set; } = "upperSlab";
    [Column("door_head_fill_block")] public int DoorHeadFillBlock { get; set; } = 126;
    [Column("door_head_fill_data")] public int DoorHeadFillData { get; set; }
    [Column("window_host_block")] public int WindowHostBlock { get; set; } = -1;
    [Column("window_host_data")] public int WindowHostData { get; set; }
    [Column("roof_style_id")] public long? RoofStyleId { get; set; }
    [Column("porch_style_id")] public long? PorchStyleId { get; set; }

    [Column("created_at")] public DateTime CreatedAt { get; set; }
}

/// <summary>Everything above the eave (M0018): which form the roof takes, how steeply it climbs, how far it
/// oversails and whether it carries a hole or a capped ridge. Its materials — the roof body, the verge and
/// the gable face — are courses in <see cref="RoofStyleCourseRow"/>.</summary>
[Table("roof_style")]
public sealed class RoofStyleRow
{
    [PrimaryKey, Identity, Column("id")] public long Id { get; set; }
    [Column("name"), NotNull] public string Name { get; set; } = "";
    [Column("form"), NotNull] public string Form { get; set; } = "gable";
    [Column("roof_slab")] public int RoofSlab { get; set; } = -1;
    [Column("roof_slab_data")] public int RoofSlabData { get; set; }
    [Column("pitch")] public int Pitch { get; set; } = 1;
    [Column("overhang")] public int Overhang { get; set; } = 1;
    [Column("roof_hole")] public bool RoofHole { get; set; }
    [Column("ridge_cap")] public bool RidgeCap { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
}

/// <summary>One room (M0018): the air a player stands in, the windows through its walls and how its own floor
/// is divided in plan. Its materials — the wall stack, the corner posts and the three floor zones — are
/// courses in <see cref="StoreyStyleCourseRow"/>. A house stacks these through
/// <see cref="RoomStyleStoreyRow"/>.</summary>
[Table("storey_style")]
public sealed class StoreyStyleRow
{
    [PrimaryKey, Identity, Column("id")] public long Id { get; set; }
    [Column("name"), NotNull] public string Name { get; set; } = "";
    [Column("clear")] public int Clear { get; set; } = 3;
    [Column("border_width")] public int BorderWidth { get; set; } = 1;
    [Column("inlay_inset")] public int InlayInset { get; set; } = 2;
    [Column("window_form"), NotNull] public string WindowForm { get; set; } = "none";
    [Column("window_block")] public int WindowBlock { get; set; } = 102;
    [Column("window_data")] public int WindowData { get; set; }
    [Column("window_sill")] public int WindowSill { get; set; } = 2;
    [Column("window_width")] public int WindowWidth { get; set; } = 2;
    [Column("window_height")] public int WindowHeight { get; set; } = 2;
    [Column("window_spacing")] public int WindowSpacing { get; set; } = 3;
    [Column("window_host_block")] public int WindowHostBlock { get; set; } = -1;
    [Column("window_host_data")] public int WindowHostData { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
}

/// <summary>The strip of footprint the walls give up (M0018): how deep, how far in from each end, which wall,
/// what the canopy over it is shaped like and what the rail along its open edges is made of. No course table —
/// a porch stands on the house's own floor and under a canopy in the roof's own material, so what is left to
/// it is its shape.</summary>
[Table("porch_style")]
public sealed class PorchStyleRow
{
    [PrimaryKey, Identity, Column("id")] public long Id { get; set; }
    [Column("name"), NotNull] public string Name { get; set; } = "";
    [Column("depth")] public int Depth { get; set; } = 2;
    [Column("inset")] public int Inset { get; set; }
    [Column("edge"), NotNull] public string Edge { get; set; } = "front";
    [Column("roof_form"), NotNull] public string RoofForm { get; set; } = "shed";
    [Column("rail_block")] public int RailBlock { get; set; } = 85;
    [Column("created_at")] public DateTime CreatedAt { get; set; }
}

/// <summary>One course of a <see cref="RoofStyleRow"/>'s part (M0018) — <c>roof</c>, <c>verge</c> or
/// <c>gable</c>. <see cref="RoomStyleCourseRow"/>'s shape exactly; its own table so it keeps a real foreign
/// key to the roof it belongs to and dies with it.</summary>
[Table("roof_style_course")]
public sealed class RoofStyleCourseRow
{
    [PrimaryKey, Identity, Column("id")] public long Id { get; set; }
    [Column("roof_style_id"), NotNull] public long RoofStyleId { get; set; }
    [Column("part"), NotNull] public string Part { get; set; } = "";
    [Column("ordinal")] public int Ordinal { get; set; }
    [Column("style_id"), NotNull] public long StyleId { get; set; }
    [Column("height")] public int Height { get; set; } = 1;
}

/// <summary>One course of a <see cref="StoreyStyleRow"/>'s part (M0018) — <c>wall</c>, <c>post</c>,
/// <c>field</c>, <c>border</c> or <c>inlay</c>.</summary>
[Table("storey_style_course")]
public sealed class StoreyStyleCourseRow
{
    [PrimaryKey, Identity, Column("id")] public long Id { get; set; }
    [Column("storey_style_id"), NotNull] public long StoreyStyleId { get; set; }
    [Column("part"), NotNull] public string Part { get; set; } = "";
    [Column("ordinal")] public int Ordinal { get; set; }
    [Column("style_id"), NotNull] public long StyleId { get; set; }
    [Column("height")] public int Height { get; set; } = 1;
}

/// <summary>One storey of a house (M0018): which <see cref="StoreyStyleRow"/> fills it and where in the stack
/// it sits, <see cref="Ordinal"/> 0 being the ground. <see cref="Clear"/> overrides the storey style's own
/// where it is set, so one preset is a tall ground floor in one house and an ordinary room in another without
/// a second row of it. Unique per (room style, ordinal); cascades with its house, restricts its storey
/// style.</summary>
[Table("room_style_storey")]
public sealed class RoomStyleStoreyRow
{
    [PrimaryKey, Identity, Column("id")] public long Id { get; set; }
    [Column("room_style_id"), NotNull] public long RoomStyleId { get; set; }
    [Column("ordinal")] public int Ordinal { get; set; }
    [Column("storey_style_id"), NotNull] public long StoreyStyleId { get; set; }
    [Column("clear")] public int Clear { get; set; }
}

/// <summary>One course of a <see cref="RoomStyleRow"/>'s part (see M0012): which part, where in that part's
/// stack (<see cref="Ordinal"/> 0 = nearest the part's own base), the <see cref="StyleRow"/> it resolves
/// through, and how many courses it runs. Unique per (room style, part, ordinal); cascades with its room
/// style, restricts its style.</summary>
[Table("room_style_course")]
public sealed class RoomStyleCourseRow
{
    [PrimaryKey, Identity, Column("id")] public long Id { get; set; }
    [Column("room_style_id"), NotNull] public long RoomStyleId { get; set; }
    [Column("part"), NotNull] public string Part { get; set; } = "";
    [Column("ordinal")] public int Ordinal { get; set; }
    [Column("style_id"), NotNull] public long StyleId { get; set; }
    [Column("height")] public int Height { get; set; } = 1;
}

// The well-known values of style.kind, theme_bucket.bucket, the three *_course.part columns and the roof-form
// columns are PgmStudio.Contracts' MaterialKind, ThemeBuckets, RoomParts and RoofForms; room_style.door is
// PgmStudio.Domain's DoorMaterials. They are not restated here: the same strings have to satisfy the column,
// the wire and the client's editor, and a second copy next to the column is exactly how they would come to
// disagree.
