using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// A spawn keeps every region it states, each with its own yaw, as one JSON list <c>[{region, yaw}]</c> in
/// <c>regions_json</c>. Every row's one region and the spawn's yaw become that list's one entry, and a row with no
/// region an empty list; <c>region_key</c> and <c>yaw</c> go once carried. Down keeps each spawn's first region.
/// </summary>
[Migration(94, "A spawn keeps every region it states")]
public sealed class M0094_SpawnsKeepEveryRegion : Migration
{
    public override void Up()
    {
        Alter.Table("spawn").AddColumn("regions_json").AsCustom("LONGTEXT").Nullable();
        Execute.Sql(
            """
            UPDATE spawn
               SET regions_json = CASE
                     WHEN region_key IS NULL THEN '[]'
                     ELSE JSON_ARRAY(JSON_OBJECT('region', region_key, 'yaw', yaw))
                   END
            """);
        Alter.Table("spawn").AlterColumn("regions_json").AsCustom("LONGTEXT").NotNullable();
        Delete.Column("region_key").FromTable("spawn");
        Delete.Column("yaw").FromTable("spawn");
    }

    public override void Down()
    {
        Alter.Table("spawn").AddColumn("yaw").AsDouble().NotNullable().WithDefaultValue(0);
        Alter.Table("spawn").AddColumn("region_key").AsString(190).Nullable();
        Execute.Sql(
            """
            UPDATE spawn
               SET region_key = JSON_UNQUOTE(JSON_EXTRACT(regions_json, '$[0].region')),
                   yaw = COALESCE(JSON_EXTRACT(regions_json, '$[0].yaw'), 0)
            """);
        Delete.Column("regions_json").FromTable("spawn");
    }
}
