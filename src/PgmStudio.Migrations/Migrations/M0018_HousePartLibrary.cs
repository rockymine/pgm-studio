using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// A house is composed from part styles (B71, docs/world-export/structures.md §8).
///
/// <para>M0012 made a room style a first-class row, but a flat one: every knob of the whole building lived on
/// it, and <c>room_style_course</c> bound materials per part. A <b>part</b> therefore had no identity. A
/// shingled roof with its pitch, its overhang and its capped ridge could not be reused — it was re-entered
/// house by house — and a stack of storeys could only be a count of identical ones.</para>
///
/// <para>This is the level <c>style</c> → <c>theme</c> already has, applied to buildings: a
/// <c>roof_style</c>, a <c>storey_style</c> and a <c>porch_style</c> each own a coherent set of knobs plus
/// their own course stacks, and a <c>room_style</c> becomes the thing that binds them — its foundation and
/// its door, one roof, an optional porch, and an <b>ordered</b> list of storeys through
/// <c>room_style_storey</c>. Three entities rather than one per part: the rest of a house's parts are a
/// single material each, and a row that wrapped one style would add a name and nothing else.</para>
///
/// <para>Every existing room style keeps building what it built. The new bindings are nullable and a room
/// style with none of them falls back to the columns it already carries, so nothing is migrated and nothing
/// moves; the old columns are read as the defaults a house uses when it binds no part.</para>
/// </summary>
[Migration(18, "Roof, storey and porch styles a house is composed from")]
public sealed class M0018_HousePartLibrary : Migration
{
    public override void Up()
    {
        // ── the roof: everything above the eave ───────────────────────────────────────────────────────
        Create.Table("roof_style")
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn("name").AsString(255).NotNullable().WithDefaultValue("")
            .WithColumn("form").AsString(16).NotNullable().WithDefaultValue("gable")
            .WithColumn("thickness").AsInt32().NotNullable().WithDefaultValue(1)
            .WithColumn("pitch").AsInt32().NotNullable().WithDefaultValue(1)
            .WithColumn("overhang").AsInt32().NotNullable().WithDefaultValue(1)
            .WithColumn("roof_hole").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("ridge_cap").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("created_at").AsDateTime().NotNullable();
        Utf8(SchemaRoof);

        // ── the storey: one room, from its own floor to the slab over it ──────────────────────────────
        Create.Table("storey_style")
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn("name").AsString(255).NotNullable().WithDefaultValue("")
            .WithColumn("clear").AsInt32().NotNullable().WithDefaultValue(3)
            .WithColumn("border_width").AsInt32().NotNullable().WithDefaultValue(1)
            .WithColumn("inlay_inset").AsInt32().NotNullable().WithDefaultValue(2)
            .WithColumn("window_form").AsString(16).NotNullable().WithDefaultValue("none")
            .WithColumn("window_block").AsInt32().NotNullable().WithDefaultValue(102)
            .WithColumn("window_data").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("window_sill").AsInt32().NotNullable().WithDefaultValue(2)
            .WithColumn("window_width").AsInt32().NotNullable().WithDefaultValue(2)
            .WithColumn("window_height").AsInt32().NotNullable().WithDefaultValue(2)
            .WithColumn("window_spacing").AsInt32().NotNullable().WithDefaultValue(3)
            .WithColumn("created_at").AsDateTime().NotNullable();
        Utf8(SchemaStorey);

        // ── the porch: a strip of the footprint the walls give up ─────────────────────────────────────
        // No course table: a porch's materials are the deck it stands on and the canopy over it, both of
        // which belong to parts it does not own. What is left is its shape and its rail.
        Create.Table("porch_style")
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn("name").AsString(255).NotNullable().WithDefaultValue("")
            .WithColumn("depth").AsInt32().NotNullable().WithDefaultValue(2)
            .WithColumn("inset").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("edge").AsString(16).NotNullable().WithDefaultValue("front")
            .WithColumn("roof_form").AsString(16).NotNullable().WithDefaultValue("shed")
            .WithColumn("rail_block").AsInt32().NotNullable().WithDefaultValue(85)
            .WithColumn("created_at").AsDateTime().NotNullable();
        Utf8(SchemaPorch);

        // Each part's courses, in the shape room_style_course already has: one style bound to one part at one
        // position in that part's stack. Two tables rather than one polymorphic one, so each keeps a real
        // foreign key to the row it belongs to and dies with it.
        PartCourses("roof_style_course", SchemaRoof);         // roof | verge | gable
        PartCourses("storey_style_course", SchemaStorey);     // wall | post | field | border | inlay

        // ── what a room style binds ───────────────────────────────────────────────────────────────────
        // Nullable, and restrict rather than cascade on delete: a part still bound by a house is a part the
        // library must refuse to forget, the same answer a style already gives a theme.
        Alter.Table("room_style")
            .AddColumn("roof_style_id").AsInt64().Nullable()
            .AddColumn("porch_style_id").AsInt64().Nullable();
        Create.ForeignKey("fk_room_style_roof").FromTable("room_style").ForeignColumn("roof_style_id")
              .ToTable(SchemaRoof).PrimaryColumn("id");
        Create.ForeignKey("fk_room_style_porch").FromTable("room_style").ForeignColumn("porch_style_id")
              .ToTable(SchemaPorch).PrimaryColumn("id");

        // The stack, in order. Its own clear overrides the storey style's where it is set, so one preset can
        // be a tall ground floor in one house and an ordinary room in another without a second row.
        Create.Table("room_style_storey")
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn("room_style_id").AsInt64().NotNullable()
            .WithColumn("ordinal").AsInt32().NotNullable()       // 0 = the ground storey
            .WithColumn("storey_style_id").AsInt64().NotNullable()
            .WithColumn("clear").AsInt32().NotNullable().WithDefaultValue(0);   // 0 = the storey style's own

        Create.ForeignKey("fk_room_storey_owner").FromTable("room_style_storey").ForeignColumn("room_style_id")
              .ToTable("room_style").PrimaryColumn("id").OnDelete(System.Data.Rule.Cascade);
        Create.ForeignKey("fk_room_storey_style").FromTable("room_style_storey").ForeignColumn("storey_style_id")
              .ToTable(SchemaStorey).PrimaryColumn("id");
        Create.Index("ux_room_style_storey").OnTable("room_style_storey").OnColumn("room_style_id").Ascending()
              .OnColumn("ordinal").Ascending().WithOptions().Unique();
        Create.Index("ix_room_style_storey_style").OnTable("room_style_storey").OnColumn("storey_style_id").Ascending();
    }

    private const string SchemaRoof = "roof_style";
    private const string SchemaStorey = "storey_style";
    private const string SchemaPorch = "porch_style";

    /// <summary>A part's course table: the same five columns, the same cascade to its owner and restrict to
    /// the style it binds, and the same unique (owner, part, ordinal).</summary>
    private void PartCourses(string table, string owner)
    {
        var ownerColumn = $"{owner}_id";
        Create.Table(table)
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn(ownerColumn).AsInt64().NotNullable()
            .WithColumn("part").AsString(16).NotNullable()
            .WithColumn("ordinal").AsInt32().NotNullable()       // 0 = nearest the part's own base
            .WithColumn("style_id").AsInt64().NotNullable()
            .WithColumn("height").AsInt32().NotNullable().WithDefaultValue(1);

        Create.ForeignKey($"fk_{table}_owner").FromTable(table).ForeignColumn(ownerColumn)
              .ToTable(owner).PrimaryColumn("id").OnDelete(System.Data.Rule.Cascade);
        Create.ForeignKey($"fk_{table}_style").FromTable(table).ForeignColumn("style_id")
              .ToTable("style").PrimaryColumn("id");
        Create.Index($"ux_{table}").OnTable(table).OnColumn(ownerColumn).Ascending()
              .OnColumn("part").Ascending().OnColumn("ordinal").Ascending().WithOptions().Unique();
        Create.Index($"ix_{table}_style").OnTable(table).OnColumn("style_id").Ascending();
    }

    private void Utf8(string table)
        => Execute.Sql($"ALTER TABLE `{table}` CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");

    public override void Down()
    {
        Delete.Table("room_style_storey");
        Delete.ForeignKey("fk_room_style_roof").OnTable("room_style");
        Delete.ForeignKey("fk_room_style_porch").OnTable("room_style");
        Delete.Column("roof_style_id").FromTable("room_style");
        Delete.Column("porch_style_id").FromTable("room_style");
        Delete.Table("storey_style_course");
        Delete.Table("roof_style_course");
        Delete.Table("porch_style");
        Delete.Table("storey_style");
        Delete.Table("roof_style");
    }
}
