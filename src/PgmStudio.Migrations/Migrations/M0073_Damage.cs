using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// The damage module: one row per map, the module as one JSON document in the shape the map document carries
/// it (<c>Serializer.EncodeDamage</c>) — its filters in order and its two actions.
///
/// <para>The table is new and no stored row states one, so there is nothing to carry forward; a map imported
/// before holds its damage module again when it is imported again.</para>
/// </summary>
[Migration(73, "Damage")]
public sealed class M0073_Damage : Migration
{
    private const string Table = "damage";

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
