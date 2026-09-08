using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// The destroyable-candidate store: the output of <c>DestroyableSuggester.Gather</c>, persisted for the same
/// reason <c>core_candidate</c> is — the <c>.mca</c> files are discarded after import, so a candidate not
/// written during that single pass cannot be recovered.
///
/// <para>One row per proposed structure, carrying the block box and the two neighbourhood readings that
/// proposed it. Those readings are stored rather than recomputed because they are what a person confirming a
/// suggestion is actually judging: a mass with nothing else of its material within ten blocks, standing five
/// above the ground around it, is a goal, and the row says so instead of asking the reader to take the
/// proposal on trust.</para>
///
/// <para>Same shape as <c>core_candidate</c>: surrogate PK, cascade-delete FK to <c>map</c>, and a
/// <c>map_id</c> index, so a re-scan is a delete-then-insert and a deleted map takes its candidates with
/// it.</para>
/// </summary>
[Migration(34, "Destroyable candidate store")]
public sealed class M0034_DestroyableCandidate : Migration
{
    public override void Up()
    {
        Create.Table("destroyable_candidate")
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn("map_id").AsInt64().NotNullable()
            // the connected mass's block box — the goal's blocks, never a region drawn around them (OB12)
            .WithColumn("min_x").AsInt32().NotNullable()
            .WithColumn("min_y").AsInt32().NotNullable()
            .WithColumn("min_z").AsInt32().NotNullable()
            .WithColumn("max_x").AsInt32().NotNullable()
            .WithColumn("max_y").AsInt32().NotNullable()
            .WithColumn("max_z").AsInt32().NotNullable()
            // what it is made of, as DestroyableMaterials spells it, and how many blocks it holds
            .WithColumn("materials").AsString(64).NotNullable()
            .WithColumn("blocks").AsInt32().NotNullable()
            // the two readings that proposed it: isolation, and height over the ring of terrain around it
            .WithColumn("same_nearby").AsInt32().NotNullable()
            .WithColumn("elevation").AsInt32().NotNullable();

        Create.ForeignKey("fk_destroyable_candidate_map").FromTable("destroyable_candidate").ForeignColumn("map_id")
              .ToTable("map").PrimaryColumn("id").OnDelete(System.Data.Rule.Cascade);
        Create.Index("ix_destroyable_candidate_map").OnTable("destroyable_candidate").OnColumn("map_id").Ascending();
    }

    public override void Down() => Delete.Table("destroyable_candidate");
}
