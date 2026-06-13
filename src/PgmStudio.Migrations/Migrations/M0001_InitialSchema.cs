using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// The initial hybrid schema (plan D1/D2):
/// - entity tables with FKs for what we list/query/edit (map, team, region, filter, wool,
///   monument, spawn, kit, apply_rule, renewable, block_drop_rule, map_spawner, author);
/// - JSON columns for the polymorphic leaves (region/filter type-specific params, coords,
///   bounds, child-ref lists, apply-rule event maps, drop-item lists);
/// - flat feature tables from the parquet scans (wool_block, resource_block, chest_item,
///   spawner_block, layer_segment);
/// - map_artifact blobs for cached/document data (raw layer.parquet, islands/symmetry/config json).
///
/// PGM string ids are unique <i>per map</i>, so each registry table carries a surrogate
/// BIGINT PK plus a `*_key` column unique within the map; cross-references are stored as the
/// `*_key` strings (scalar columns or JSON arrays), resolved in the app/domain layer.
/// </summary>
[Migration(1, "Initial hybrid schema")]
public sealed class M0001_InitialSchema : Migration
{
    private const string Json = "JSON";       // MariaDB JSON (LONGTEXT + JSON_VALID check)
    private const string Text = "TEXT";
    private const string Blob = "LONGBLOB";

    public override void Up()
    {
        // ── map ───────────────────────────────────────────────────────────────────────
        Create.Table("map")
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn("slug").AsString(190).NotNullable().Unique()       // dir/identifier, used in /api/map/{slug}
            .WithColumn("name").AsString(255).NotNullable().WithDefaultValue("")
            .WithColumn("version").AsString(64).Nullable()
            .WithColumn("gamemode").AsString(64).Nullable()
            .WithColumn("objective").AsCustom(Text).Nullable()
            .WithColumn("max_build_height").AsDouble().Nullable()
            .WithColumn("created_at").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentDateTime)
            .WithColumn("updated_at").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentDateTime);

        // ── author ────────────────────────────────────────────────────────────────────
        Create.Table("author")
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn("map_id").AsInt64().NotNullable()
            .WithColumn("uuid").AsString(64).NotNullable().WithDefaultValue("")
            .WithColumn("role").AsString(64).NotNullable().WithDefaultValue("")
            .WithColumn("contribution").AsCustom(Text).Nullable()
            .WithColumn("name").AsString(255).Nullable();

        // ── team ──────────────────────────────────────────────────────────────────────
        Create.Table("team")
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn("map_id").AsInt64().NotNullable()
            .WithColumn("team_key").AsString(190).NotNullable()
            .WithColumn("name").AsString(255).NotNullable().WithDefaultValue("")
            .WithColumn("color").AsString(64).NotNullable().WithDefaultValue("")
            .WithColumn("dye_color").AsString(64).NotNullable().WithDefaultValue("")
            .WithColumn("max_players").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("min_players").AsInt32().NotNullable().WithDefaultValue(0);

        // ── kit + items + armor ─────────────────────────────────────────────────────────
        Create.Table("kit")
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn("map_id").AsInt64().NotNullable()
            .WithColumn("kit_key").AsString(190).NotNullable();

        Create.Table("kit_item")
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn("kit_id").AsInt64().NotNullable()
            .WithColumn("slot").AsInt32().Nullable()
            .WithColumn("material").AsString(190).NotNullable().WithDefaultValue("")
            .WithColumn("amount").AsInt32().Nullable()
            .WithColumn("damage").AsInt32().Nullable()
            .WithColumn("unbreakable").AsBoolean().Nullable()
            .WithColumn("team_color").AsBoolean().Nullable()
            .WithColumn("enchantments").AsCustom(Text).Nullable();

        Create.Table("kit_armor")
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn("kit_id").AsInt64().NotNullable()
            .WithColumn("slot_name").AsString(64).NotNullable().WithDefaultValue("")
            .WithColumn("material").AsString(190).NotNullable().WithDefaultValue("")
            .WithColumn("unbreakable").AsBoolean().Nullable()
            .WithColumn("team_color").AsBoolean().Nullable()
            .WithColumn("enchantments").AsCustom(Text).Nullable();

        // ── region (id-keyed registry; polymorphic coords/bounds/children as JSON) ───────
        Create.Table("region")
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn("map_id").AsInt64().NotNullable()
            .WithColumn("region_key").AsString(190).NotNullable()
            .WithColumn("type").AsString(64).NotNullable()
            .WithColumn("bounds_json").AsCustom(Json).Nullable()          // {min:{x,z},max:{x,z}}
            .WithColumn("coords_json").AsCustom(Json).Nullable()          // min/max/base/center/origin/position/radius/height/normal/offset/y/ref_id
            .WithColumn("child_ref_ids_json").AsCustom(Json).Nullable()   // compound children: [region_key,...]
            .WithColumn("source_id").AsString(190).Nullable();            // transform source region_key

        // ── filter (composite via children/child; atomic params as JSON) ────────────────
        Create.Table("filter")
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn("map_id").AsInt64().NotNullable()
            .WithColumn("filter_key").AsString(190).NotNullable()
            .WithColumn("type").AsString(64).NotNullable()
            .WithColumn("child_ref_ids_json").AsCustom(Json).Nullable()   // all/any/one: [filter_key,...]
            .WithColumn("child_key").AsString(190).Nullable()             // not/deny/allow
            .WithColumn("region_key").AsString(190).Nullable()            // blocks/region
            .WithColumn("params_json").AsCustom(Json).Nullable();         // atomic leaf params

        // ── wool (grouped by colour) + monuments ────────────────────────────────────────
        Create.Table("wool")
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn("map_id").AsInt64().NotNullable()
            .WithColumn("wool_key").AsString(190).NotNullable()           // colour slug = id
            .WithColumn("color").AsString(64).NotNullable().WithDefaultValue("")
            .WithColumn("location_json").AsCustom(Json).Nullable()
            .WithColumn("wool_room_region_key").AsString(190).Nullable()
            .WithColumn("team").AsString(190).Nullable();                 // derived owner

        Create.Table("monument")
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn("wool_id").AsInt64().NotNullable()
            .WithColumn("monument_key").AsString(190).NotNullable().WithDefaultValue("")  // colour-team
            .WithColumn("team").AsString(190).NotNullable().WithDefaultValue("")
            .WithColumn("location_json").AsCustom(Json).Nullable()
            .WithColumn("monument_region_key").AsString(190).Nullable();

        // ── spawn (team + observer via flag; region as key or inline json) ──────────────
        Create.Table("spawn")
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn("map_id").AsInt64().NotNullable()
            .WithColumn("is_observer").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("team").AsString(190).NotNullable().WithDefaultValue("")
            .WithColumn("kit").AsString(190).Nullable()
            .WithColumn("yaw").AsDouble().NotNullable().WithDefaultValue(0)
            .WithColumn("region_key").AsString(190).Nullable()
            .WithColumn("region_json").AsCustom(Json).Nullable();         // inline region (legacy)

        // ── map_spawner (<spawner> module — distinct from world-scanned spawner blocks) ──
        Create.Table("map_spawner")
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn("map_id").AsInt64().NotNullable()
            .WithColumn("spawn_region_key").AsString(190).Nullable()
            .WithColumn("player_region_key").AsString(190).Nullable()
            .WithColumn("delay").AsString(64).Nullable()
            .WithColumn("max_entities").AsInt32().Nullable()
            .WithColumn("items_json").AsCustom(Json).Nullable();          // [DropItem,...]

        // ── renewable ───────────────────────────────────────────────────────────────────
        Create.Table("renewable")
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn("map_id").AsInt64().NotNullable()
            .WithColumn("region_key").AsString(190).Nullable()
            .WithColumn("rate").AsDouble().Nullable()
            .WithColumn("renew_filter").AsString(190).Nullable()
            .WithColumn("replace_filter").AsString(190).Nullable()
            .WithColumn("grow").AsBoolean().Nullable();

        // ── block_drop_rule ─────────────────────────────────────────────────────────────
        Create.Table("block_drop_rule")
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn("map_id").AsInt64().NotNullable()
            .WithColumn("region_key").AsString(190).Nullable()
            .WithColumn("filter_key").AsString(190).Nullable()
            .WithColumn("replacement").AsString(190).Nullable()
            .WithColumn("wrong_tool").AsBoolean().Nullable()
            .WithColumn("items_json").AsCustom(Json).Nullable();

        // ── apply_rule (event map as JSON: enter/leave/block/.../message/velocity/kit) ──
        Create.Table("apply_rule")
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn("map_id").AsInt64().NotNullable()
            .WithColumn("rule_key").AsString(190).Nullable()
            .WithColumn("region_key").AsString(190).Nullable()
            .WithColumn("events_json").AsCustom(Json).Nullable();

        // ── feature tables (parquet scans → relational rows) ────────────────────────────
        Create.Table("wool_block")
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn("map_id").AsInt64().NotNullable()
            .WithColumn("world_x").AsInt32().NotNullable()
            .WithColumn("world_z").AsInt32().NotNullable()
            .WithColumn("world_y").AsInt32().NotNullable()
            .WithColumn("color").AsString(32).NotNullable();

        Create.Table("resource_block")
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn("map_id").AsInt64().NotNullable()
            .WithColumn("world_x").AsInt32().NotNullable()
            .WithColumn("world_z").AsInt32().NotNullable()
            .WithColumn("world_y").AsInt32().NotNullable()
            .WithColumn("resource_type").AsString(32).NotNullable();

        Create.Table("chest_item")
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn("map_id").AsInt64().NotNullable()
            .WithColumn("world_x").AsInt32().NotNullable()
            .WithColumn("world_z").AsInt32().NotNullable()
            .WithColumn("world_y").AsInt32().NotNullable()
            .WithColumn("chest_type").AsString(32).NotNullable()
            .WithColumn("slot").AsInt32().NotNullable()
            .WithColumn("item_id").AsString(190).NotNullable()
            .WithColumn("item_damage").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("count").AsInt32().NotNullable().WithDefaultValue(1);

        Create.Table("spawner_block")
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn("map_id").AsInt64().NotNullable()
            .WithColumn("world_x").AsInt32().NotNullable()
            .WithColumn("world_z").AsInt32().NotNullable()
            .WithColumn("world_y").AsInt32().NotNullable()
            .WithColumn("entity_id").AsString(190).Nullable()
            .WithColumn("spawns_wool").AsBoolean().Nullable()
            .WithColumn("spawn_item_id").AsString(190).Nullable()
            .WithColumn("spawn_item_damage").AsInt32().Nullable()
            .WithColumn("spawn_count").AsInt32().Nullable()
            .WithColumn("spawn_range").AsInt32().Nullable()
            .WithColumn("min_spawn_delay").AsInt32().Nullable()
            .WithColumn("max_spawn_delay").AsInt32().Nullable()
            .WithColumn("required_player_range").AsInt32().Nullable()
            .WithColumn("max_nearby_entities").AsInt32().Nullable();

        Create.Table("layer_segment")
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn("map_id").AsInt64().NotNullable()
            .WithColumn("world_x").AsInt32().NotNullable()
            .WithColumn("world_z").AsInt32().NotNullable()
            .WithColumn("world_y_start").AsInt32().NotNullable()
            .WithColumn("world_y_end").AsInt32().NotNullable();

        // ── map_artifact (cached/document blobs keyed by kind) ──────────────────────────
        Create.Table("map_artifact")
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn("map_id").AsInt64().NotNullable()
            .WithColumn("kind").AsString(64).NotNullable()   // layer_parquet | islands_json | symmetry_json | map_config_json
            .WithColumn("data").AsCustom(Blob).NotNullable();

        CreateForeignKeysAndIndexes();

        // FluentMigrator's MySql generator emits utf8mb3 varchars; convert every table to
        // utf8mb4 so map text (names, messages, objectives) can hold 4-byte chars / emoji.
        // The 190-char key columns stay within InnoDB's index-prefix limit at 4 bytes/char.
        foreach (var t in AllTables)
            Execute.Sql($"ALTER TABLE `{t}` CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
    }

    private static readonly string[] AllTables =
    {
        "map", "author", "team", "kit", "kit_item", "kit_armor", "region", "filter", "wool",
        "monument", "spawn", "map_spawner", "renewable", "block_drop_rule", "apply_rule",
        "wool_block", "resource_block", "chest_item", "spawner_block", "layer_segment", "map_artifact",
    };

    private void CreateForeignKeysAndIndexes()
    {
        // map-scoped tables: FK → map(id) with cascade delete (clean re-import)
        foreach (var t in new[]
                 {
                     "author", "team", "kit", "region", "filter", "wool", "spawn", "map_spawner",
                     "renewable", "block_drop_rule", "apply_rule", "wool_block", "resource_block",
                     "chest_item", "spawner_block", "layer_segment", "map_artifact",
                 })
        {
            Create.ForeignKey($"fk_{t}_map").FromTable(t).ForeignColumn("map_id")
                  .ToTable("map").PrimaryColumn("id").OnDelete(System.Data.Rule.Cascade);
            Create.Index($"ix_{t}_map").OnTable(t).OnColumn("map_id").Ascending();
        }

        Create.ForeignKey("fk_kit_item_kit").FromTable("kit_item").ForeignColumn("kit_id")
              .ToTable("kit").PrimaryColumn("id").OnDelete(System.Data.Rule.Cascade);
        Create.ForeignKey("fk_kit_armor_kit").FromTable("kit_armor").ForeignColumn("kit_id")
              .ToTable("kit").PrimaryColumn("id").OnDelete(System.Data.Rule.Cascade);
        Create.ForeignKey("fk_monument_wool").FromTable("monument").ForeignColumn("wool_id")
              .ToTable("wool").PrimaryColumn("id").OnDelete(System.Data.Rule.Cascade);

        // registry uniqueness: a PGM key is unique within its map (a well-formed map's invariant;
        // a map with duplicate/empty ids — e.g. two empty-id teams — is rejected as malformed).
        Create.Index("ux_team_map_key").OnTable("team").OnColumn("map_id").Ascending().OnColumn("team_key").Ascending().WithOptions().Unique();
        Create.Index("ux_region_map_key").OnTable("region").OnColumn("map_id").Ascending().OnColumn("region_key").Ascending().WithOptions().Unique();
        Create.Index("ux_filter_map_key").OnTable("filter").OnColumn("map_id").Ascending().OnColumn("filter_key").Ascending().WithOptions().Unique();
        Create.Index("ux_wool_map_key").OnTable("wool").OnColumn("map_id").Ascending().OnColumn("wool_key").Ascending().WithOptions().Unique();
        Create.Index("ux_kit_map_key").OnTable("kit").OnColumn("map_id").Ascending().OnColumn("kit_key").Ascending().WithOptions().Unique();
        Create.Index("ux_artifact_map_kind").OnTable("map_artifact").OnColumn("map_id").Ascending().OnColumn("kind").Ascending().WithOptions().Unique();
    }

    public override void Down()
    {
        // drop in reverse-dependency order
        foreach (var t in new[]
                 {
                     "map_artifact", "layer_segment", "spawner_block", "chest_item", "resource_block",
                     "wool_block", "apply_rule", "block_drop_rule", "renewable", "map_spawner",
                     "spawn", "monument", "wool", "filter", "region", "kit_armor", "kit_item", "kit",
                     "team", "author", "map",
                 })
        {
            Delete.Table(t);
        }
    }
}
