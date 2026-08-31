using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// The players a lookup has already resolved.
///
/// <para>An author is stored in the <c>map.xml</c> as a <c>uuid</c> wherever one exists, and nobody
/// remembers their own uuid — so the studio resolves a typed name through Mojang, and it resolves the same
/// handful of names over and over: the person authoring the map, and the two or three people helping. Every
/// intent write asks again, once per author. A resolved pair is a fact that barely changes, so it is kept.
///
/// <para>The uuid is the key because it is the identity: a player renames and the row is updated in place
/// rather than duplicated. The name is indexed too, since a lookup arrives as either.</para>
/// </summary>
[Migration(28, "Cache the players a lookup resolved")]
public sealed class M0028_PlayerNameCache : Migration
{
    public override void Up()
    {
        Create.Table("minecraft_player")
            .WithColumn("uuid").AsString(36).NotNullable().PrimaryKey()
            .WithColumn("name").AsString(32).NotNullable()
            .WithColumn("fetched_at").AsDateTime().NotNullable();

        Create.Index("ix_minecraft_player_name").OnTable("minecraft_player").OnColumn("name").Ascending();
    }

    public override void Down() => Delete.Table("minecraft_player");
}
