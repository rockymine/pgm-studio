using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// Drops the stored island-review flags.
///
/// <para>The flag marked a map whose detected island sketch looked wrong, so a reviewer working the decompose
/// queue could come back to it. The queue is gone, the routes that read and wrote the flag are gone, and the
/// island classification it was triage for is gone with them — a row here is a note addressed to a workflow
/// nothing can reach.</para>
///
/// <para>It is a data delete rather than a schema change: <c>map_artifact</c> is keyed by a free-form
/// <c>kind</c>, so the rows would otherwise sit in the table forever, counted by every artifact query and
/// explained by nothing in the code.</para>
/// </summary>
[Migration(23, "Delete the island-review artifacts left by the retired decompose queue")]
public sealed class M0023_DropIslandReview : Migration
{
    public override void Up() =>
        Execute.Sql("DELETE FROM map_artifact WHERE kind = 'island_review_json';");

    /// <summary>Nothing to undo. The rows carried a reviewer's note about a surface that no longer exists, so
    /// there is no state to restore them to and no reader that would look.</summary>
    public override void Down() { }
}
