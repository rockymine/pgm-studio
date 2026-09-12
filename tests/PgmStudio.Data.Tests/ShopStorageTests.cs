using LinqToDB;
using LinqToDB.Async;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Domain;
using PgmStudio.Geom;

namespace PgmStudio.Data.Tests;

/// <summary>
/// A shop, its keepers and the item stacks in both survive storage. The invariant is the one the schema is
/// shaped for: a stack is one object wherever it is stored, so an icon's and a kit item's come back the same
/// — every field of one, not the six a column layout happened to have.
/// </summary>
[NotInParallel]
public sealed class ShopStorageTests
{
    private static ItemSpec Stick() => new()
    {
        Material = "stick", Amount = 1, Name = "`rKnockback Stick", Lore = "`cGone on death|`cReally",
        Enchantments = "knockback:1", PreventSharing = true, Hidden = ["enchantments"],
        Effects = [new PotionEffect { Type = "jump_boost", Duration = "60s", Amplifier = 2 }],
        Attributes = [new ItemAttribute { Attribute = "generic.movementSpeed", Operation = "add", Amount = 0.01 }],
        CanPlaceOn = ["all-blocks"],
    };

    private static MapXml Document() => new()
    {
        Name = "Shop Map",
        Version = "1.0.0",
        Kits = [new Kit { Id = "spawn-kit", Items = [new KitItem { Slot = 0, Item = Stick() }] }],
        Shops =
        [
            new Shop
            {
                Id = "item-shop", Name = "Items",
                Categories =
                [
                    new ShopCategory
                    {
                        Id = "blocks", FilterId = "not-blue",
                        Icon = new ItemSpec { Material = "hard clay", Name = "`aBlocks" },
                        Icons =
                        [
                            new ShopIcon
                            {
                                Item = Stick(),
                                FilterId = "has-iron-pick",
                                ActionId = "add-protection",
                                Payments =
                                [
                                    new ShopPayment { Price = 6, Currency = "gold nugget", Color = "yellow" },
                                    new ShopPayment { Price = 1, Currency = "iron pickaxe" },
                                ],
                            },
                        ],
                    },
                ],
            },
        ],
        Shopkeepers =
        [
            new Shopkeeper { ShopId = "item-shop", Name = "`b`lItems", Mob = "Villager", Location = new Vec3(2.5, 64, -39.5), Yaw = 90 },
            // States almost nothing, and stands in a region rather than at coordinates.
            new Shopkeeper { ShopId = "item-shop", RegionId = "blue-shop-stand" },
        ],
    };

    private static async Task<long> InsertMapAsync(PgmDb db) =>
        await new MapRepository(db).InsertAsync(new MapRow
        {
            Slug = "shop-map", Name = "Shop Map", Version = "1.0.0",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        });

    private static async Task AssertStackAsync(ItemSpec item)
    {
        await Assert.That(item.Material).IsEqualTo("stick");
        await Assert.That(item.Name).IsEqualTo("`rKnockback Stick");
        await Assert.That(item.Lore).IsEqualTo("`cGone on death|`cReally");
        await Assert.That(item.Enchantments).IsEqualTo("knockback:1");
        await Assert.That(item.PreventSharing).IsTrue();
        await Assert.That(item.Hidden).IsEquivalentTo(new[] { "enchantments" });
        await Assert.That(item.Effects.Single().Duration).IsEqualTo("60s");
        await Assert.That(item.Attributes.Single().Amount).IsEqualTo(0.01);
        await Assert.That(item.CanPlaceOn).IsEquivalentTo(new[] { "all-blocks" });
    }

    [Test]
    public async Task A_shop_and_its_keepers_round_trip_through_storage()
    {
        await TestDb.ResetSchemaAsync();
        await using var db = TestDb.Connect();
        var mapId = await InsertMapAsync(db);

        await new MapWriter(db).WriteEntitiesAsync(mapId, Document());
        var read = await new MapReader(db).ReadAsync(await db.Maps.FirstAsync(m => m.Id == mapId));

        var shop = read.Shops.Single();
        await Assert.That(shop.Id).IsEqualTo("item-shop");
        await Assert.That(shop.Name).IsEqualTo("Items");

        var category = shop.Categories.Single();
        await Assert.That(category.Id).IsEqualTo("blocks");
        await Assert.That(category.FilterId).IsEqualTo("not-blue");
        await Assert.That(category.Icon.Material).IsEqualTo("hard clay");

        var icon = category.Icons.Single();
        await Assert.That(icon.FilterId).IsEqualTo("has-iron-pick");
        await Assert.That(icon.ActionId).IsEqualTo("add-protection");
        await Assert.That(icon.Payments.Select(p => p.Currency)).IsEquivalentTo(new[] { "gold nugget", "iron pickaxe" });
        await Assert.That(icon.Payments[0].Color).IsEqualTo("yellow");
        await AssertStackAsync(icon.Item);

        var placed = read.Shopkeepers.Single(k => k.Location is not null);
        await Assert.That(placed.Name).IsEqualTo("`b`lItems");
        await Assert.That(placed.Mob).IsEqualTo("Villager");
        await Assert.That(placed.Location).IsEqualTo(new Vec3(2.5, 64, -39.5));
        await Assert.That(placed.Yaw).IsEqualTo(90.0);

        var standing = read.Shopkeepers.Single(k => k.Location is null);
        await Assert.That(standing.RegionId).IsEqualTo("blue-shop-stand");
        await Assert.That(standing.Mob).IsEqualTo("");
        // Null rather than nought: a keeper that stated no facing is not one facing +z.
        await Assert.That(standing.Yaw).IsNull();
    }

    /// <summary>A kit item is the same stack as a shop icon, so it comes back with all of it too.</summary>
    [Test]
    public async Task A_kit_item_carries_the_whole_stack_through_storage()
    {
        await TestDb.ResetSchemaAsync();
        await using var db = TestDb.Connect();
        var mapId = await InsertMapAsync(db);

        await new MapWriter(db).WriteEntitiesAsync(mapId, Document());
        var read = await new MapReader(db).ReadAsync(await db.Maps.FirstAsync(m => m.Id == mapId));

        var item = read.Kits.Single().Items.Single();
        await Assert.That(item.Slot).IsEqualTo(0);
        await AssertStackAsync(item.Item);
    }
}
