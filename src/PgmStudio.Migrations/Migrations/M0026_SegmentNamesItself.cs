using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// Drops the <c>scan_</c> prefix from the segment table, so the scanned-feature family spells its rows the
/// same way throughout.
///
/// <para>Every table a world scan writes is named for what a row is — <c>wool_block</c>,
/// <c>resource_block</c>, <c>chest_item</c>, <c>spawner_block</c>, <c>monument_candidate</c>,
/// <c>core_candidate</c> — and none of them says which pass produced it, because they all come from the same
/// one. <c>segment</c> is the word for a contiguous solid run in a column, and it is the table's whole
/// subject.</para>
/// </summary>
[Migration(26, "The segment table names what a row is, not which pass wrote it")]
public sealed class M0026_SegmentNamesItself : Migration
{
    public override void Up() => Rename.Table("scan_segment").To("segment");

    public override void Down() => Rename.Table("segment").To("scan_segment");
}
