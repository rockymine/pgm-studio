using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// A library row the seed folder states carries the key of the entry it holds — a pattern's, a house's or a
/// theme's name in the folder, a part's content, a copied tree's cut, a template's species, a biome's id. The row
/// is the folder's: the seed rewrites it on every start and retires it when its entry leaves, and the studio
/// refuses to edit it. A row with no key is an author's. Every stored row starts with none, and the next seed
/// finds the rows that hold its entries and keys them.
/// </summary>
[Migration(61, "A seeded library row carries the key of the folder entry it holds")]
public sealed class M0061_SeedKeys : Migration
{
    private static readonly string[] Tables =
    [
        "style", "theme", "roof_style", "storey_style", "porch_style", "room_style", "tree_style", "boulder_style",
        "biome_pattern",
    ];

    public override void Up()
    {
        foreach (var table in Tables)
        {
            Alter.Table(table).AddColumn("seed_key").AsString(191).Nullable();
            Create.Index($"ux_{table}_seed_key").OnTable(table).OnColumn("seed_key").Ascending().WithOptions().Unique();
        }
    }

    public override void Down()
    {
        foreach (var table in Tables)
        {
            Delete.Index($"ux_{table}_seed_key").OnTable(table);
            Delete.Column("seed_key").FromTable(table);
        }
    }
}
