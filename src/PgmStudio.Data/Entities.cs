using LinqToDB.Mapping;

namespace PgmStudio.Data;

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
    // Lifecycle stage: sketch | configure | edit (see Contracts.MapStage). Drives the staged dashboard.
    [Column("stage"), NotNull] public string Stage { get; set; } = "edit";
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
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

[Table("layer_segment")]
public sealed class LayerSegmentRow
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
}

/// <summary>Map symmetry (docs/contracts/new-map-authoring.md §6b) — promoted from the
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
    public const string LayerParquet = "layer_parquet";
    public const string IslandsJson = "islands_json";
    // symmetry_json was promoted to the `symmetry` table (M0003, new-map-authoring.md §6b).
    public const string MapConfigJson = "map_config_json";
    // Editor-only sidecar: {region_key: editor_step} for freshly drawn, not-yet-wired regions (E10).
    // Lives outside the entity-replace codec so it survives MapWriter.SaveDocAsync; never part of the
    // PGM map document. Pruned against live regions on read; entries graduate out once a region's
    // derived category is no longer "other".
    public const string RegionDraftsJson = "region_drafts_json";
    // Declarative authoring intent for NEW maps (docs/contracts/new-map-authoring.md): the source of
    // truth the generator projects into regions/filters/apply-rules. Like the draft sidecar it lives
    // outside the entity-replace codec, so it survives MapWriter.SaveDocAsync and is never part of the
    // PGM document. Presence of this artifact is what makes a map "intent-authored".
    public const string MapIntentJson = "map_intent_json";
    // Sketch tool authoring source (docs/contracts/sketch-authoring.md): the drawn layout (setup +
    // shapes + island metadata, the browser's JS-origin blob) for a draft map. Like the intent/draft
    // sidecars it lives outside the entity-replace codec. A draft map with this artifact but no
    // layer_parquet is a sketch-in-progress; "finish" rasterizes it into the geometry artifacts (S2e).
    public const string SketchLayoutJson = "sketch_layout_json";
}
