using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// The portals module and the score boxes: a row per portal and per box, each one JSON document in the shape the
/// map document carries it (<c>Serializer.EncodePortal</c>, <c>Serializer.EncodeScoreBox</c>), read back in
/// insertion order, which is document order.
///
/// <para>The tables are new and no stored row states either, so there is nothing to carry forward; a map
/// imported before holds them again when it is imported again.</para>
/// </summary>
[Migration(72, "Portals and score boxes")]
public sealed class M0072_PortalsAndScoreBoxes : Migration
{
    private static readonly string[] Tables = ["portal", "score_box"];

    public override void Up()
    {
        foreach (var table in Tables)
        {
            Create.Table(table)
                .WithColumn("id").AsInt64().PrimaryKey().Identity()
                .WithColumn("map_id").AsInt64().NotNullable()
                .WithColumn("spec_json").AsCustom("TEXT").NotNullable();
            Execute.Sql($"ALTER TABLE `{table}` CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
            Create.ForeignKey($"fk_{table}_map").FromTable(table).ForeignColumn("map_id")
                  .ToTable("map").PrimaryColumn("id").OnDelete(System.Data.Rule.Cascade);
            Create.Index($"ix_{table}_map").OnTable(table).OnColumn("map_id").Ascending();
        }
    }

    public override void Down()
    {
        foreach (var table in Tables) Delete.Table(table);
    }
}
