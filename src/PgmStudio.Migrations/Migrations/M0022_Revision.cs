using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// A counter on every document two writers can reach, so a write can say which version of it it was written
/// against.
///
/// <para>The studio stores by replacing: an edit reads the whole map document, patches it and writes the whole
/// document back, and an artifact is one row that a save deletes and re-inserts. Two callers doing that at
/// once keep only the second, and there is nothing in either request or either response to notice it — no
/// conflict, no finding, no trace. One person in one browser tab never met it; an agent driving the API while
/// a tab is open on the same slug meets it on the first collision.</para>
///
/// <para><b>Two counters, because they are two documents.</b> A map's entities and its sketch layout are
/// written by different routes for different reasons, and a caller holding a version of one has said nothing
/// about the other — a single counter would refuse a metadata patch because someone saved a drawing.</para>
///
/// <para>It is <c>revision</c> and not <c>version</c> because <c>map.version</c> is taken, by the version
/// string PGM reads out of the map document.</para>
/// </summary>
[Migration(22, "A revision counter on the map document and on each artifact")]
public sealed class M0022_Revision : Migration
{
    public override void Up()
    {
        // 1 rather than 0, so every stored document already has a revision a caller can hold and compare —
        // there is no "unversioned" state for a reader to have to handle.
        Create.Column("revision").OnTable("map").AsInt64().NotNullable().WithDefaultValue(1);
        Create.Column("revision").OnTable("map_artifact").AsInt64().NotNullable().WithDefaultValue(1);
    }

    public override void Down()
    {
        Delete.Column("revision").FromTable("map_artifact");
        Delete.Column("revision").FromTable("map");
    }
}
