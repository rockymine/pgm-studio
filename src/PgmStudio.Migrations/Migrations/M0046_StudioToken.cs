using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// Tokens for callers without a browser. Each row acts as one person on the whitelist, named by the Minecraft
/// uuid <c>studio_user</c> keys them by, and keeps only the SHA-256 of the token, so the table holds nothing a
/// request could be signed in with.
/// </summary>
[Migration(46, "Tokens for callers without a browser")]
public sealed class M0046_StudioToken : Migration
{
    public override void Up() =>
        Create.Table("studio_token")
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn("user_uuid").AsString(36).NotNullable().Indexed("ix_studio_token_user")
            .WithColumn("hash").AsFixedLengthString(64).NotNullable().Unique("ux_studio_token_hash")
            .WithColumn("label").AsString(100).NotNullable()
            .WithColumn("created_at").AsDateTime().NotNullable()
            .WithColumn("last_used_at").AsDateTime().Nullable();

    public override void Down() => Delete.Table("studio_token");
}
