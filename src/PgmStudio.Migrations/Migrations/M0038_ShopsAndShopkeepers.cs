using FluentMigrator;

namespace PgmStudio.Migrations.Migrations;

/// <summary>
/// Shops, their keepers, and the one item shape they share with kits.
///
/// <para><c>shop</c> keeps its categories as one JSON document instead of two further tables. A category
/// has no life outside its shop and an icon none outside its category; nothing queries into either; and the
/// studio's UI does not edit them. The precedent is <c>kit.effects_json</c> and
/// <c>map_spawner.items_json</c> — a nested list is stored as the list it is.</para>
///
/// <para><c>shopkeeper.shop_key</c> is a reference and <b>not</b> a foreign key, which is the whole reason
/// the table is shaped this way: 15 corpus maps state keepers whose shops arrive from an
/// <c>&lt;include&gt;</c> the studio reads without splicing, so a keeper naming a shop no row holds is a
/// complete map rather than a broken one. <c>location_json</c> and <c>region_key</c> are the two forms PGM's
/// point provider takes and exactly one is set.</para>
///
/// <para><c>kit_item</c> and <c>kit_armor</c> lose their per-attribute columns for one <c>spec_json</c>. An
/// item stack is one statement — <c>ItemSpec</c>, nineteen parts of it — and a shop icon stores the same
/// object inside <c>shop.categories_json</c>; a column layout here and a JSON object there is one type under
/// two storage shapes, which is how the two come to disagree about what an item is. The six columns dropped
/// held a third of what the parser now reads off an item.</para>
/// </summary>
[Migration(38, "Shops, shopkeepers, and one item shape")]
public sealed class M0038_ShopsAndShopkeepers : Migration
{
    private const string Text = "TEXT";

    public override void Up()
    {
        Create.Table("shop")
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn("map_id").AsInt64().NotNullable()
            .WithColumn("shop_key").AsString(190).NotNullable()     // referenced by a keeper and by an action
            .WithColumn("name").AsString(255).Nullable()            // NULL = PGM shows the id
            .WithColumn("categories_json").AsCustom(Text).NotNullable();

        Create.Table("shopkeeper")
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn("map_id").AsInt64().NotNullable()
            .WithColumn("shop_key").AsString(190).NotNullable()
            .WithColumn("name").AsString(255).Nullable()            // NULL = PGM labels it with the shop id
            .WithColumn("mob").AsString(64).Nullable()              // NULL = PGM's villager
            .WithColumn("location_json").AsCustom(Text).Nullable()  // {x,y,z}, or NULL when it names a region
            .WithColumn("region_key").AsString(190).Nullable()
            .WithColumn("yaw").AsDouble().Nullable();               // NULL = the map stated no facing

        foreach (var table in new[] { "shop", "shopkeeper" })
        {
            Execute.Sql($"ALTER TABLE `{table}` CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
            Create.ForeignKey($"fk_{table}_map").FromTable(table).ForeignColumn("map_id")
                  .ToTable("map").PrimaryColumn("id").OnDelete(System.Data.Rule.Cascade);
            Create.Index($"ix_{table}_map").OnTable(table).OnColumn("map_id").Ascending();
        }
        Create.Index("ux_shop_map_key").OnTable("shop")
              .OnColumn("map_id").Ascending().OnColumn("shop_key").Ascending().WithOptions().Unique();

        Alter.Table("kit_item").AddColumn("spec_json").AsCustom(Text).NotNullable();
        Alter.Table("kit_armor").AddColumn("spec_json").AsCustom(Text).NotNullable();
        foreach (var column in new[] { "material", "amount", "damage", "unbreakable", "team_color", "enchantments" })
            Delete.Column(column).FromTable("kit_item");
        foreach (var column in new[] { "material", "unbreakable", "team_color", "enchantments" })
            Delete.Column(column).FromTable("kit_armor");
    }

    public override void Down()
    {
        Delete.Table("shopkeeper");
        Delete.Table("shop");
        Delete.Column("spec_json").FromTable("kit_item");
        Delete.Column("spec_json").FromTable("kit_armor");
        Alter.Table("kit_item")
            .AddColumn("material").AsString(190).NotNullable().WithDefaultValue("")
            .AddColumn("amount").AsInt32().Nullable()
            .AddColumn("damage").AsInt32().Nullable()
            .AddColumn("unbreakable").AsBoolean().Nullable()
            .AddColumn("team_color").AsBoolean().Nullable()
            .AddColumn("enchantments").AsCustom(Text).Nullable();
        Alter.Table("kit_armor")
            .AddColumn("material").AsString(190).NotNullable().WithDefaultValue("")
            .AddColumn("unbreakable").AsBoolean().Nullable()
            .AddColumn("team_color").AsBoolean().Nullable()
            .AddColumn("enchantments").AsCustom(Text).Nullable();
    }
}
