using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// Who may write to the studio, and who a map belongs to.
///
/// <para><c>studio_user</c> is the whitelist: one row per invited person, keyed by the Minecraft uuid the
/// studio already credits authors by, with the role that decides what they may do. <c>map.owner_uuid</c> is
/// the person who brought the map into existence; it is null for a map originated where nobody signs in.</para>
/// </summary>
[Migration(42, "Studio users and map owners")]
public sealed class M0042_StudioUser : Migration
{
    public override void Up()
    {
        Create.Table("studio_user")
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn("uuid").AsString(36).NotNullable().Unique("ux_studio_user_uuid")
            .WithColumn("name").AsString(16).NotNullable()
            .WithColumn("role").AsString(16).NotNullable()
            .WithColumn("created_at").AsDateTime().NotNullable();

        Alter.Table("map").AddColumn("owner_uuid").AsString(36).Nullable();
    }

    public override void Down()
    {
        Delete.Column("owner_uuid").FromTable("map");
        Delete.Table("studio_user");
    }
}
