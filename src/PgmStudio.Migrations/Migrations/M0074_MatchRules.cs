using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// A map's match rules: one row per map, its time limit, respawn rules, blitz, rage and kill rewards as one JSON
/// document under the keys the map document carries them at (<c>Serializer.EncodeMatchRules</c>).
///
/// <para>The table is new and no stored row states one, so there is nothing to carry forward; a map imported
/// before holds its match rules again when it is imported again.</para>
/// </summary>
[Migration(74, "MatchRules")]
public sealed class M0074_MatchRules : Migration
{
    private const string Table = "match_rules";

    public override void Up()
    {
        Create.Table(Table)
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn("map_id").AsInt64().NotNullable()
            .WithColumn("spec_json").AsCustom("TEXT").NotNullable();
        Execute.Sql($"ALTER TABLE `{Table}` CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
        Create.ForeignKey($"fk_{Table}_map").FromTable(Table).ForeignColumn("map_id")
              .ToTable("map").PrimaryColumn("id").OnDelete(System.Data.Rule.Cascade);
        Create.Index($"ix_{Table}_map").OnTable(Table).OnColumn("map_id").Ascending();
    }

    public override void Down() => Delete.Table(Table);
}
