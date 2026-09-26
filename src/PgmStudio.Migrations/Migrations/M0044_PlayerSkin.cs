using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// A player's skin, kept beside their name: the PNG Mojang's texture server answered and when it was
/// fetched. The studio serves it from its own origin, so a browser draws a player's head without asking any
/// third party. Existing rows carry forward with no skin, fetched on first ask.
/// </summary>
[Migration(44, "Player skin cache")]
public sealed class M0044_PlayerSkin : Migration
{
    public override void Up()
    {
        Alter.Table("minecraft_player")
            .AddColumn("skin_png").AsCustom("MEDIUMBLOB").Nullable()
            .AddColumn("skin_fetched_at").AsDateTime().Nullable();
    }

    public override void Down() =>
        Delete.Column("skin_fetched_at").Column("skin_png").FromTable("minecraft_player");
}
