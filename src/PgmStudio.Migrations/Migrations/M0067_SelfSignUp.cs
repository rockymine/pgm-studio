using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// People sign themselves up, and a library entry belongs to whoever made it (<c>docs/access.md</c>).
///
/// <para>The whitelist keeps the Discord name a person signed in with, which is what an admin recognises them
/// by before approving them. Each of the nine library tables gains <c>owner_uuid</c>, the account that made the
/// row. A seeded row stays ownerless, and so does every row already stored, since nothing recorded who made it:
/// an ownerless row that is not seeded is an admin's to change.</para>
/// </summary>
[Migration(67, "People sign themselves up, and a library entry belongs to whoever made it")]
public sealed class M0067_SelfSignUp : Migration
{
    private static readonly string[] Library =
        ["style", "biome_pattern", "theme", "room_style", "roof_style", "storey_style", "porch_style", "tree_style",
         "boulder_style"];

    public override void Up()
    {
        Alter.Table("studio_user").AddColumn("discord_name").AsString(64).Nullable();
        foreach (var table in Library) Alter.Table(table).AddColumn("owner_uuid").AsString(36).Nullable();
    }

    public override void Down()
    {
        foreach (var table in Library) Delete.Column("owner_uuid").FromTable(table);
        Delete.Column("discord_name").FromTable("studio_user");
    }
}
