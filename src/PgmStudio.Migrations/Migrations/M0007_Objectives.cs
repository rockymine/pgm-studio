using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// The DTM and DTC objectives — <c>destroyable</c>, <c>core</c> — and the <c>mode</c> registry they
/// reference. All three hang off <c>map_id</c>, per the hybrid rule: real columns for what we list and edit,
/// JSON only for the irregular leaf (a mode-id list).
/// <para>They deliberately do <b>not</b> reuse <c>monument</c>, whose <c>wool_id</c> is NOT NULL with a
/// cascade to <c>wool</c> — a destroyable has no wool, so the existing FK makes a wool-less objective
/// unrepresentable. "Monument" is also already the CTW wool monument throughout this schema.</para>
/// <para>Nothing may assume a map has one objective type: CTW, DTM and DTC coexist, so these tables sit
/// beside <c>wool</c> rather than excluding it.</para>
/// </summary>
[Migration(7, "Destroyable + core objectives and the mode registry")]
public sealed class M0007_Objectives : Migration
{
    private const string Json = "JSON";

    public override void Up()
    {
        // A scheduled material change, referenced by id from a destroyable/core. `after` is the only
        // attribute PGM requires; the rest default (show_before to 60s) or are absent.
        Create.Table("mode")
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn("map_id").AsInt64().NotNullable()
            .WithColumn("mode_key").AsString(190).NotNullable()          // XML id, generated when unauthored
            .WithColumn("name").AsString(255).Nullable()                 // may carry `-prefixed colour codes
            .WithColumn("after").AsString(64).NotNullable()
            .WithColumn("material").AsString(190).Nullable()             // absent when the mode has an action
            .WithColumn("show_before").AsString(64).Nullable()           // NULL = PGM's 60s default
            .WithColumn("filter_key").AsString(190).Nullable()
            .WithColumn("action_key").AsString(190).Nullable();          // refs <actions>, which we don't parse

        // The blocks matching `materials` inside `region_key` — the region is a loose box around the
        // structure, not the structure, so it holds mostly air by design.
        Create.Table("destroyable")
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn("map_id").AsInt64().NotNullable()
            .WithColumn("destroyable_key").AsString(190).NotNullable()   // XML id, generated when unauthored
            .WithColumn("name").AsString(255).NotNullable()              // PGM requires it
            .WithColumn("owner").AsString(190).NotNullable()             // the DEFENDING team
            .WithColumn("region_key").AsString(190).Nullable()
            .WithColumn("materials").AsString(255).NotNullable()         // ';'-separated match patterns
            .WithColumn("completion").AsDouble().Nullable()              // NULL = 1.0; a fraction, not a %
            // false ⇒ not an objective but a scripted block-swap region. Queryable, because a map whose
            // every destroyable is hidden is not DTM, and nothing may present one as a goal.
            .WithColumn("show").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn("mode_changes").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("modes_json").AsCustom(Json).Nullable();         // an explicit set; NULL = none/all

        // Obsidian casing enclosing lava. The owning attribute is spelled `team` in the XML and `owner`
        // everywhere else — a PGM inconsistency we mirror on the wire but not in the schema.
        Create.Table("core")
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn("map_id").AsInt64().NotNullable()
            .WithColumn("core_key").AsString(190).NotNullable()
            .WithColumn("name").AsString(255).Nullable()                 // NULL = PGM auto-names per team
            .WithColumn("owner").AsString(190).NotNullable()
            .WithColumn("region_key").AsString(190).Nullable()
            .WithColumn("material").AsString(190).Nullable()             // NULL = obsidian
            .WithColumn("leak").AsInt32().Nullable()                     // NULL = 5
            .WithColumn("mode_changes").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("modes_json").AsCustom(Json).Nullable();

        foreach (var t in Tables)
        {
            Execute.Sql($"ALTER TABLE `{t}` CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
            Create.ForeignKey($"fk_{t}_map").FromTable(t).ForeignColumn("map_id")
                  .ToTable("map").PrimaryColumn("id").OnDelete(System.Data.Rule.Cascade);
            Create.Index($"ix_{t}_map").OnTable(t).OnColumn("map_id").Ascending();
        }

        // A PGM feature id is unique within its map; the parser generates one when the XML omits it, so
        // these are never empty.
        Create.Index("ux_mode_map_key").OnTable("mode").OnColumn("map_id").Ascending().OnColumn("mode_key").Ascending().WithOptions().Unique();
        Create.Index("ux_destroyable_map_key").OnTable("destroyable").OnColumn("map_id").Ascending().OnColumn("destroyable_key").Ascending().WithOptions().Unique();
        Create.Index("ux_core_map_key").OnTable("core").OnColumn("map_id").Ascending().OnColumn("core_key").Ascending().WithOptions().Unique();
    }

    private static readonly string[] Tables = { "mode", "destroyable", "core" };

    public override void Down()
    {
        foreach (var t in new[] { "core", "destroyable", "mode" }) Delete.Table(t);
    }
}
