using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// A change records the earlier changes it dropped: a source applied over changes it had not seen names the ones
/// it replaces, and the change it lands as keeps the list. Every stored change dropped none, which is what an
/// absent list says.
/// </summary>
[Migration(54, "A change records the earlier changes it dropped")]
public sealed class M0054_ChangeDiscards : Migration
{
    public override void Up() =>
        Alter.Table("map_change").AddColumn("discarded_json").AsCustom("JSON").Nullable();

    public override void Down() => Delete.Column("discarded_json").FromTable("map_change");
}
