using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// A room style can ask for a gable (M0012 widened).
///
/// <para>The stamper has built two roofs for a while — a flat lid and two slopes meeting at a ridge — but a
/// stored style had no word for the second, so every saved shell was a lid whatever the building wanted.
/// <c>roof_form</c> is that word, and <c>pitch</c> is how steeply the slopes climb: one course of rise per
/// block travelled inward is the vanilla 45°, two is an alpine roof. Both default to what every existing row
/// already is, so nothing that was saved changes shape.</para>
///
/// <para><c>eave</c> becomes <c>overhang</c>, which is the number it always was. Flush is none and overlap is
/// one block, and a word that can only say those two cannot say what a roof reaching two blocks past its wall
/// does — which is what a real map's houses do on at least one side. Keeping it a word would have meant
/// replacing it again the first time a roof wanted more.</para>
///
/// <para>The three materials a house has and a shell does not — its corner posts, its sill, its verge — need
/// no column: <c>room_style_course.part</c> is a free string, so they are three more parts a course may bind,
/// and a style that binds none of them is the unframed shell it was before.</para>
/// </summary>
[Migration(15, "A room style can ask for a gable, a pitch and an overhang")]
public sealed class M0015_RoomStyleRoofForm : Migration
{
    public override void Up()
    {
        Alter.Table("room_style")
            .AddColumn("roof_form").AsString(16).NotNullable().WithDefaultValue("flat")     // flat | gable
            .AddColumn("pitch").AsInt32().NotNullable().WithDefaultValue(1)
            .AddColumn("overhang").AsInt32().NotNullable().WithDefaultValue(0);

        Execute.Sql("UPDATE `room_style` SET `overhang` = 1 WHERE `eave` = 'overlap'");
        Delete.Column("eave").FromTable("room_style");
    }

    public override void Down()
    {
        Alter.Table("room_style")
            .AddColumn("eave").AsString(16).NotNullable().WithDefaultValue("flush");
        // Anything reaching further than a block has no word to go back to, so it lands on the nearest one.
        Execute.Sql("UPDATE `room_style` SET `eave` = 'overlap' WHERE `overhang` >= 1");

        Delete.Column("overhang").FromTable("room_style");
        Delete.Column("pitch").FromTable("room_style");
        Delete.Column("roof_form").FromTable("room_style");
    }
}
