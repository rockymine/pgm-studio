using LinqToDB;
using LinqToDB.Data;
using PgmStudio.Migrations;

namespace PgmStudio.Data.Tests;

/// <summary>
/// <c>M0094</c> carries every spawn's one region and yaw into its list of regions, and a spawn with no region into
/// an empty one. Rolls the schema back and forward, so it runs apart from every other database test.
/// </summary>
[NotInParallel]
public sealed class SpawnsKeepEveryRegionMigrationTests
{
    [Test]
    public async Task A_spawns_region_and_yaw_become_its_one_listed_region()
    {
        await TestDb.ResetSchemaAsync();
        SchemaMigrator.MigrateDown(TestDb.ConnectionString, 93);
        try
        {
            await using (var db = TestDb.Connect())
            {
                db.Execute("INSERT INTO map (slug, name, version, created_at, updated_at) VALUES ('old', 'Old', '1', NOW(), NOW())");
                db.Execute("INSERT INTO spawn (map_id, is_observer, team, kit, yaw, region_key) VALUES "
                           + "((SELECT id FROM map WHERE slug = 'old'), 0, 'red', 'k', 90.5, 'red-spawn'), "
                           + "((SELECT id FROM map WHERE slug = 'old'), 1, '', NULL, 0, NULL)");
            }

            SchemaMigrator.MigrateUp(TestDb.ConnectionString, 94);
            await using (var db = TestDb.Connect())
            {
                var rows = db.Query<Row>("SELECT team AS Team, JSON_EXTRACT(regions_json, '$[0].region') AS Region, "
                                         + "JSON_EXTRACT(regions_json, '$[0].yaw') AS Yaw, JSON_LENGTH(regions_json) AS Count "
                                         + "FROM spawn ORDER BY is_observer").ToList();
                await Assert.That(rows.Select(row => (row.Team, row.Region, row.Yaw, row.Count)))
                    .IsEquivalentTo([("red", (string?)"\"red-spawn\"", (string?)"90.5", 1), ("", null, null, 0)]);
            }
        }
        finally
        {
            SchemaMigrator.MigrateUp(TestDb.ConnectionString);
        }
    }

    private sealed class Row
    {
        public string Team { get; set; } = "";
        public string? Region { get; set; }
        public string? Yaw { get; set; }
        public int Count { get; set; }
    }
}
