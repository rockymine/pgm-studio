using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// The layout <c>plan</c> store — the persisted half of the generator's feedback loop. A plan enters this
/// table only when a human acts on it (saved from the editor, and later pinned/voted); the browsing feed
/// itself is ephemeral. Rows are a standalone corpus of pre-map layout artifacts, so there is deliberately
/// <b>no</b> <c>map_id</c> FK — a plan is authored before any map exists.
/// <para><c>origin</c> distinguishes <c>generated</c> (the composer's output) from <c>authored</c> (drawn
/// or edited by hand) and <c>imported</c> (a <c>*.plan.json</c> file). Generated rows are immutable: editing
/// one forks a new <c>authored</c> row whose <c>parent_id</c> points back, so the labeled corpus can't be
/// contaminated after the fact — <c>parent_id</c> is the fork provenance. <c>content_hash</c> (SHA-256 of the
/// normalized document) is the dedup + import-identity key; it is <b>not</b> unique, because a fork may
/// legitimately match its source's content across origins — dedup is a store-level lookup, not a constraint.</para>
/// <para>The generated-only descriptor columns (<c>request_json</c>, <c>seed</c>, <c>composer_version</c>)
/// hold the canonical versioned request that reproduces the row within a composer version; they are NULL for
/// authored/imported rows.</para>
/// </summary>
[Migration(8, "The layout plan store")]
public sealed class M0008_Plan : Migration
{
    private const string Json = "JSON";

    public override void Up()
    {
        Create.Table("plan")
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn("name").AsString(255).NotNullable().WithDefaultValue("")   // from meta.name, for listing
            .WithColumn("origin").AsString(16).NotNullable()                       // generated | authored | imported
            .WithColumn("plan_json").AsCustom(Json).NotNullable()                  // the *.plan.json document
            .WithColumn("content_hash").AsFixedLengthString(64).NotNullable()      // SHA-256 hex — dedup + import identity
            .WithColumn("parent_id").AsInt64().Nullable()                          // fork provenance (self-ref)
            .WithColumn("request_json").AsCustom(Json).Nullable()                  // ComposeDescriptor — generated only
            .WithColumn("seed").AsCustom("BIGINT UNSIGNED").Nullable()             // seed cursor — generated only
            .WithColumn("composer_version").AsString(32).Nullable()                // generated only
            .WithColumn("created_at").AsDateTime().NotNullable()
            .WithColumn("updated_at").AsDateTime().NotNullable();

        Execute.Sql("ALTER TABLE `plan` CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");

        // Self-referential fork link: deleting a parent orphans its forks rather than cascading them away —
        // a fork's own labels outlive its source.
        Create.ForeignKey("fk_plan_parent").FromTable("plan").ForeignColumn("parent_id")
              .ToTable("plan").PrimaryColumn("id").OnDelete(System.Data.Rule.SetNull);

        Create.Index("ix_plan_origin").OnTable("plan").OnColumn("origin").Ascending();
        Create.Index("ix_plan_content_hash").OnTable("plan").OnColumn("content_hash").Ascending();
        Create.Index("ix_plan_parent").OnTable("plan").OnColumn("parent_id").Ascending();
    }

    public override void Down() => Delete.Table("plan");
}
