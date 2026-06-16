using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// The monument-candidate store (task F9, <c>docs/contracts/monument-candidate-store.md</c>): the
/// gathered, <em>style-agnostic</em> output of <c>MonumentSuggester.Gather</c>, persisted so the authoring
/// tier scores monument suggestions as a DB query instead of re-reading the <c>.mca</c> world. One row per
/// gathered anchor emission (<c>Score</c> does the cell-merge). Mirrors the <c>spawner_block</c> feature
/// table: a surrogate PK, a cascade-delete FK to <c>map</c>, and a <c>map_id</c> index.
/// </summary>
[Migration(2, "Monument candidate store")]
public sealed class M0002_MonumentCandidate : Migration
{
    public override void Up()
    {
        Create.Table("monument_candidate")
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn("map_id").AsInt64().NotNullable()
            // candidate (air) monument cell — world coords; box filter + cell-merge key
            .WithColumn("cand_x").AsInt32().NotNullable()
            .WithColumn("cand_y").AsInt32().NotNullable()
            .WithColumn("cand_z").AsInt32().NotNullable()
            .WithColumn("source").AsString(16).NotNullable()        // sign | armorstand | geometry
            // block below / above the cell — PedestalMatches / CapMatches + ColorFromStain
            .WithColumn("pedestal_id").AsInt32().NotNullable()
            .WithColumn("pedestal_data").AsInt32().NotNullable()
            .WithColumn("cap_id").AsInt32().NotNullable()
            .WithColumn("cap_data").AsInt32().NotNullable()
            // fallback colour parsed from label text / stand head / name (stain still wins at Score)
            .WithColumn("color_hint").AsString(24).Nullable()
            // anchoring wall sign (null for non-sign sources)
            .WithColumn("sign_x").AsInt32().Nullable()
            .WithColumn("sign_y").AsInt32().Nullable()
            .WithColumn("sign_z").AsInt32().Nullable()
            .WithColumn("sign_facing").AsInt32().Nullable()         // wall-sign data nibble used to project
            .WithColumn("sign_text").AsString(256).Nullable()       // decoded label — evidence / colour
            // armour-stand evidence
            .WithColumn("stand_head_color").AsString(24).Nullable()
            .WithColumn("stand_name").AsString(256).Nullable()
            .WithColumn("evidence").AsString(256).Nullable();       // human-readable note

        Create.ForeignKey("fk_monument_candidate_map").FromTable("monument_candidate").ForeignColumn("map_id")
              .ToTable("map").PrimaryColumn("id").OnDelete(System.Data.Rule.Cascade);
        Create.Index("ix_monument_candidate_map").OnTable("monument_candidate").OnColumn("map_id").Ascending();
    }

    public override void Down() => Delete.Table("monument_candidate");
}
