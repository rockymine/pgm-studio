using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// A doorway states how wide it is.
///
/// <para>A <c>Doorway</c> carries a width and a height, and only the height had a column — so a house saved
/// through the library came back with the record's own default of 2 whatever its author asked for, and a
/// preset stamped from code and one composed from its rows were different buildings at the one opening a
/// player walks through. Two is the default the record already holds and the width every stored row was
/// built at, so an existing row reads back as exactly the house it was.</para>
/// </summary>
[Migration(27, "A doorway states its width")]
public sealed class M0027_DoorwayWidth : Migration
{
    public override void Up()
        => Create.Column("door_width").OnTable("room_style").AsInt32().NotNullable().WithDefaultValue(2);

    public override void Down()
        => Delete.Column("door_width").FromTable("room_style");
}
