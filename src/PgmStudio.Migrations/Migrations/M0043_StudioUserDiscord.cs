using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// How a person on the whitelist signs in: the Discord account bound to them, and the one-time invitation that
/// binds it. The invitation is stored as the SHA-256 of its code, so the table never holds a link that works.
/// Existing rows carry forward unbound and uninvited.
/// </summary>
[Migration(43, "Studio user Discord binding")]
public sealed class M0043_StudioUserDiscord : Migration
{
    public override void Up()
    {
        Alter.Table("studio_user")
            .AddColumn("discord_id").AsString(32).Nullable().Unique("ux_studio_user_discord")
            .AddColumn("invite_hash").AsFixedLengthString(64).Nullable().Unique("ux_studio_user_invite")
            .AddColumn("invite_expires_at").AsDateTime().Nullable();
    }

    public override void Down()
    {
        Delete.Index("ux_studio_user_invite").OnTable("studio_user");
        Delete.Index("ux_studio_user_discord").OnTable("studio_user");
        Delete.Column("invite_expires_at").Column("invite_hash").Column("discord_id").FromTable("studio_user");
    }
}
