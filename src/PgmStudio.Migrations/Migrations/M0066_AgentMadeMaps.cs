using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// A map knows whether an agent made it, so the studio can keep agent-made maps to the admins and the map's
/// owner (<c>docs/access.md</c>, <i>Agent-made maps</i>).
///
/// <para>The studio sets <c>agent_made</c> on a map a token originates from here on. A stored map is marked
/// where everyone it credits as an author is a name with no account behind it, the form an agent is credited
/// in; a map crediting nobody, or anyone with an account, is left unmarked, and an admin can mark it.</para>
/// </summary>
[Migration(66, "A map knows whether an agent made it")]
public sealed class M0066_AgentMadeMaps : Migration
{
    public override void Up()
    {
        Alter.Table("map").AddColumn("agent_made").AsBoolean().NotNullable().WithDefaultValue(false);
        Execute.Sql("""
            UPDATE map m SET m.agent_made = 1
            WHERE EXISTS (SELECT 1 FROM author a WHERE a.map_id = m.id AND a.role = 'author')
              AND NOT EXISTS (SELECT 1 FROM author a WHERE a.map_id = m.id AND a.role = 'author'
                              AND a.uuid IS NOT NULL AND a.uuid <> '')
            """);
    }

    public override void Down() => Delete.Column("agent_made").FromTable("map");
}
