using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// The revision a map's artifacts are written above. A load from documents replaces a map by deleting its row
/// and inserting another, and the new row's artifacts would start again at revision 1 — so a tab holding a
/// revision read before the reload could name the rebuilt board and save over it. The floor is the highest
/// artifact revision the replaced row held, and every artifact of the new row is written above it.
/// </summary>
[Migration(51, "A floor under a map's artifact revisions, carried across a reload")]
public sealed class M0051_ArtifactRevisionFloor : Migration
{
    // 0 on every stored map, which leaves each artifact's revision where it is.
    public override void Up() =>
        Create.Column("artifact_revision_floor").OnTable("map").AsInt64().NotNullable().WithDefaultValue(0);

    public override void Down() => Delete.Column("artifact_revision_floor").FromTable("map");
}
