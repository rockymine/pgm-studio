using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// No map keeps a region draft bucket: the <c>region_drafts_json</c> artifact named the editor step a drawn but
/// unwired region came from, and nothing draws from it. Its rows are deleted; every region they named is still in
/// the map's document. Forward-only.
/// </summary>
[Migration(91, "No region draft bucket")]
public sealed class M0091_NoRegionDrafts : Migration
{
    public override void Up() => Execute.Sql("DELETE FROM map_artifact WHERE kind = 'region_drafts_json'");

    public override void Down() { }
}
