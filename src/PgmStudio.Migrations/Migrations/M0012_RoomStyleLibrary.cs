using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// The room-style library (G34b, docs/world-export/structures.md §7). A shell's finish became data in G34a —
/// floor, walls and roof are each a stack of material courses — but the only two styles were written in C#.
/// These tables make a room style the same first-class, browsable thing a <c>theme</c> already is.
/// <para>A <c>room_style</c> carries the per-part extents (how deep the floor, how tall the walls, how thick
/// the roof) and the knobs that are not materials at all: the eave, the roof hole, and the door — the last
/// constrained to the <c>Domain.DoorMaterials</c> vocabulary, since the wool-room block filter is built from
/// the same table and a door it does not name could not be broken.</para>
/// <para>A <c>room_style_course</c> binds one <c>style</c> to one part at one position in that part's stack.
/// It is <c>theme_bucket</c>'s sibling with one difference that is the whole point: a bucket takes a single
/// style, a part takes an ordered several, because a wall is a band over bedrock over a slit and that is a
/// stack rather than a material. A part with no courses keeps the built-in finish.</para>
/// </summary>
[Migration(12, "The room-style library")]
public sealed class M0012_RoomStyleLibrary : Migration
{
    public override void Up()
    {
        Create.Table("room_style")
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn("name").AsString(255).NotNullable().WithDefaultValue("")
            .WithColumn("floor_depth").AsInt32().NotNullable().WithDefaultValue(1)
            .WithColumn("wall_height").AsInt32().NotNullable().WithDefaultValue(7)
            .WithColumn("roof_thickness").AsInt32().NotNullable().WithDefaultValue(1)
            .WithColumn("eave").AsString(16).NotNullable().WithDefaultValue("flush")     // flush | overlap
            .WithColumn("roof_hole").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn("door").AsString(32).NotNullable().WithDefaultValue("stained-glass-pane")
            .WithColumn("door_height").AsInt32().NotNullable().WithDefaultValue(3)
            .WithColumn("created_at").AsDateTime().NotNullable();
        Execute.Sql("ALTER TABLE `room_style` CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");

        Create.Table("room_style_course")
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn("room_style_id").AsInt64().NotNullable()
            .WithColumn("part").AsString(16).NotNullable()          // floor | wall | roof
            .WithColumn("ordinal").AsInt32().NotNullable()          // 0 = nearest the part's own base
            .WithColumn("style_id").AsInt64().NotNullable()
            .WithColumn("height").AsInt32().NotNullable().WithDefaultValue(1);

        // A room style's courses die with it; a style may not be deleted while a course binds it — the same
        // cascade/restrict pair theme_bucket uses, for the same reason.
        Create.ForeignKey("fk_room_course_style_owner").FromTable("room_style_course").ForeignColumn("room_style_id")
              .ToTable("room_style").PrimaryColumn("id").OnDelete(System.Data.Rule.Cascade);
        Create.ForeignKey("fk_room_course_style").FromTable("room_style_course").ForeignColumn("style_id")
              .ToTable("style").PrimaryColumn("id");

        Create.Index("ux_room_style_course").OnTable("room_style_course").OnColumn("room_style_id").Ascending()
              .OnColumn("part").Ascending().OnColumn("ordinal").Ascending().WithOptions().Unique();
        Create.Index("ix_room_style_course_style").OnTable("room_style_course").OnColumn("style_id").Ascending();
    }

    public override void Down()
    {
        Delete.Table("room_style_course");
        Delete.Table("room_style");
    }
}
