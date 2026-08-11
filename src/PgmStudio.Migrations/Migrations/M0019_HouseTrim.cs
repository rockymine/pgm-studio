using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// The knobs a house grew after M0018 and the row had no column for: its beams, a roof that steps in slabs, a
/// window in the gable face, a beam over the door, and the band a window is cut into.
///
/// <para>Every one of them was reachable by the stamper and unreachable by the library, so a preset stored and
/// read back came out a quieter building than it went in — the seeder's own round trip is what named them. A
/// knob the stamper has and the row does not is worse than a missing feature: the library says it can hold a
/// style and then holds most of one.</para>
///
/// <para>All nullable-by-default: <c>-1</c> and <c>none</c> mean "this building has none", which is what every
/// stored row already was, so nothing is migrated and every existing style builds exactly what it built.</para>
/// </summary>
[Migration(19, "Beams, slab roofs, gable windows, door heads and window host bands")]
public sealed class M0019_HouseTrim : Migration
{
    public override void Up()
    {
        Alter.Table("room_style")
            // The log ends that run past the corners where two storeys meet. -1 is a building without them.
            .AddColumn("beam_block").AsInt32().NotNullable().WithDefaultValue(-1)
            .AddColumn("beam_data").AsInt32().NotNullable().WithDefaultValue(0)
            .AddColumn("beam_reach").AsInt32().NotNullable().WithDefaultValue(1)

            // The slab a roof steps in. -1 is a roof laid in whole blocks, which is every roof there was.
            .AddColumn("roof_slab").AsInt32().NotNullable().WithDefaultValue(-1)
            .AddColumn("roof_slab_data").AsInt32().NotNullable().WithDefaultValue(0)

            // The window in the gable face. Its own columns rather than the wall windows' because a gable is
            // not a wall: one is cut per face and centred, and its sill counts from the wall top.
            .AddColumn("gable_window_form").AsString(16).NotNullable().WithDefaultValue("none")
            .AddColumn("gable_window_block").AsInt32().NotNullable().WithDefaultValue(102)
            .AddColumn("gable_window_data").AsInt32().NotNullable().WithDefaultValue(0)
            .AddColumn("gable_window_sill").AsInt32().NotNullable().WithDefaultValue(1)
            .AddColumn("gable_window_width").AsInt32().NotNullable().WithDefaultValue(1)
            .AddColumn("gable_window_height").AsInt32().NotNullable().WithDefaultValue(1)

            // The beam over the doorway. Separate from `door` because they answer different questions: the
            // door is what fills the opening and is the set the wool room's break rule whitelists, while the
            // head is what carries the wall over it and is never walked through.
            .AddColumn("door_head_form").AsString(16).NotNullable().WithDefaultValue("none")
            .AddColumn("door_head_block").AsInt32().NotNullable().WithDefaultValue(53)
            .AddColumn("door_head_fill").AsString(16).NotNullable().WithDefaultValue("upperSlab")
            .AddColumn("door_head_fill_block").AsInt32().NotNullable().WithDefaultValue(126)
            .AddColumn("door_head_fill_data").AsInt32().NotNullable().WithDefaultValue(0)

            // The block a window may be cut into, so a banded wall seats its windows in one band rather than
            // across the seam between two. -1 cuts wherever one fits, which is what every stored style did.
            .AddColumn("window_host_block").AsInt32().NotNullable().WithDefaultValue(-1)
            .AddColumn("window_host_data").AsInt32().NotNullable().WithDefaultValue(0);

        // A storey carries its own windows, so it carries their host too.
        Alter.Table("storey_style")
            .AddColumn("window_host_block").AsInt32().NotNullable().WithDefaultValue(-1)
            .AddColumn("window_host_data").AsInt32().NotNullable().WithDefaultValue(0);
    }

    public override void Down()
    {
        foreach (var column in new[]
                 {
                     "beam_block", "beam_data", "beam_reach", "roof_slab", "roof_slab_data",
                     "gable_window_form", "gable_window_block", "gable_window_data",
                     "gable_window_sill", "gable_window_width", "gable_window_height",
                     "door_head_form", "door_head_block", "door_head_fill",
                     "door_head_fill_block", "door_head_fill_data",
                     "window_host_block", "window_host_data",
                 })
            Delete.Column(column).FromTable("room_style");

        Delete.Column("window_host_block").FromTable("storey_style");
        Delete.Column("window_host_data").FromTable("storey_style");
    }
}
