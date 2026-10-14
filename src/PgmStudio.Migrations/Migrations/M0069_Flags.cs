using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// The flags module: a row per flag, per post declared beside the flags, and per net declared beside them,
/// each one JSON document in the shape the map document carries it (<c>Serializer.EncodeFlag</c> and its two
/// siblings). A flag's own post, a composite's places and a flag's nested nets live inside its document: none
/// of them has a life outside the flag, nothing queries into them, and a column layout beside the document
/// shape would be one type under two storage shapes. Rows read back in insertion order, which is document
/// order, which is the order PGM resolves posts in.
///
/// <para>The tables are new and no stored row states a flag, so there is nothing to carry forward.</para>
/// </summary>
[Migration(69, "Flags, posts and nets")]
public sealed class M0069_Flags : Migration
{
    private const string Text = "TEXT";
    private static readonly string[] Tables = ["flag", "flag_post", "flag_net"];

    public override void Up()
    {
        foreach (var table in Tables)
        {
            Create.Table(table)
                .WithColumn("id").AsInt64().PrimaryKey().Identity()
                .WithColumn("map_id").AsInt64().NotNullable()
                .WithColumn("spec_json").AsCustom(Text).NotNullable();
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
