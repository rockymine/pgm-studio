using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// A kit carries what it states that the studio does not read — its other attributes and elements, kept as
/// written — in one column, null where it states nothing else. No stored row held them, so nothing is carried
/// into it.
/// </summary>
[Migration(93, "A kit carries what it states")]
public sealed class M0093_KitsCarryWhatTheyState : Migration
{
    public override void Up() => Alter.Table("kit").AddColumn("carried_json").AsCustom("LONGTEXT").Nullable();

    public override void Down() => Delete.Column("carried_json").FromTable("kit");
}
