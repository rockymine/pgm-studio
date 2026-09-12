using PgmStudio.Domain;
using PgmStudio.Geom;
using PgmStudio.Pgm;
using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Pgm.Tests;

using Dict = Dictionary<string, object?>;

/// <summary>
/// The shop slice of the declarative generator: the catalogue an author states, and one keeper per shop at
/// every team's spawn. The placement is the part with a rule behind it — the keeper stands on the spawn's
/// own floor, beside the point players arrive on, turned to face them, and inside the spawn's room.
/// </summary>
public sealed class ShopGeneratorTests
{
    private static ShopIntent Shop(string id = "item-shop", ShopkeeperIntent? keeper = null) => new()
    {
        Id = id,
        Name = "Items",
        Keeper = keeper ?? new ShopkeeperIntent { Name = "`b`lItem Shop" },
        Categories =
        [
            new ShopCategoryIntent
            {
                Id = "blocks", Material = "hard clay", Name = "`aBlocks",
                Items =
                [
                    new ShopItemIntent { Material = "wood", Amount = 32, Price = 1, Currency = "gold nugget" },
                    new ShopItemIntent { Material = "stained clay", Amount = 16, Price = 1, Currency = "gold nugget", TeamColor = true },
                ],
            },
        ],
    };

    // A spawn facing +z (yaw 0) at the origin, in a 20 x 20 room: wide enough that nothing is clamped.
    private static SpawnIntent Spawn(string team, double x, double z, double yaw = 0) => new()
    {
        Team = team, Point = new Pt(x, 64, z), Yaw = yaw,
        Protection = [new Rect(x - 10, z - 10, x + 10, z + 10)],
    };

    private static Dict Generate(MapIntent intent)
    {
        var doc = new Dict();
        ShopGenerator.Apply(doc, intent);
        return doc;
    }

    private static MapIntent Board(params ShopIntent[] shops) => new()
    {
        Spawns = [Spawn("red", 0, -40), Spawn("blue", 0, 40, yaw: 180)],
        Shops = [.. shops],
    };

    private static List<object?> Shops(Dict doc) => (List<object?>)doc["shops"]!;
    private static List<object?> Keepers(Dict doc) => (List<object?>)doc["shopkeepers"]!;
    private static Dict At(List<object?> list, int i) => (Dict)list[i]!;

    // ── the catalogue ───────────────────────────────────────────────────────────────
    [Test]
    public async Task The_stated_shop_becomes_a_menu_with_its_categories_and_items()
    {
        var shop = At(Shops(Generate(Board(Shop()))), 0);
        await Assert.That(shop["id"]).IsEqualTo("item-shop");
        await Assert.That(shop["name"]).IsEqualTo("Items");

        var category = (Dict)((List<object?>)shop["categories"]!)[0]!;
        await Assert.That(category["id"]).IsEqualTo("blocks");
        await Assert.That(((Dict)category["icon"]!)["material"]).IsEqualTo("hard clay");

        var icons = (List<object?>)category["icons"]!;
        await Assert.That(icons.Count).IsEqualTo(2);
        var wood = (Dict)((Dict)icons[0]!)["item"]!;
        await Assert.That(wood["material"]).IsEqualTo("wood");
        await Assert.That(wood["amount"]).IsEqualTo(32);
        var payment = (Dict)((List<object?>)((Dict)icons[0]!)["payments"]!)[0]!;
        await Assert.That(payment["price"]).IsEqualTo(1);
        await Assert.That(payment["currency"]).IsEqualTo("gold nugget");
    }

    // PGM refuses a shop with no category, so a shop that states none is left out rather than written as a
    // menu that cannot open.
    [Test]
    public async Task A_shop_with_no_category_is_not_written()
    {
        var doc = Generate(Board(new ShopIntent { Id = "empty" }));
        await Assert.That(doc.ContainsKey("shops")).IsFalse();
    }

    [Test]
    public async Task A_board_stating_no_shop_leaves_none_behind()
    {
        var doc = Generate(Board(Shop()));
        ShopGenerator.Apply(doc, new MapIntent());
        await Assert.That(doc.ContainsKey("shops")).IsFalse();
        await Assert.That(doc.ContainsKey("shopkeepers")).IsFalse();
    }

    // ── the keepers ─────────────────────────────────────────────────────────────────
    [Test]
    public async Task One_keeper_stands_at_every_spawn()
    {
        var keepers = Keepers(Generate(Board(Shop())));
        await Assert.That(keepers.Count).IsEqualTo(2);
        await Assert.That(keepers.Select(k => ((Dict)k!)["shop"])).IsEquivalentTo(new object?[] { "item-shop", "item-shop" });
    }

    /// <summary>
    /// The placement rule, stated in coordinates. The red spawn is at <c>(0, 64, −40)</c> facing +z, so the
    /// axis across the way players face is x, the keeper stands two blocks along it from the block the spawn
    /// point is in — centre <c>0.5</c>, so block <c>2</c>, centre <c>2.5</c> — at the spawn's own height, and
    /// it looks back down that axis at the arriving player.
    /// </summary>
    [Test]
    public async Task A_keeper_stands_beside_the_spawn_point_facing_it()
    {
        var keeper = At(Keepers(Generate(Board(Shop()))), 0);
        var at = (Dict)keeper["location"]!;
        await Assert.That(at["x"]).IsEqualTo(2.5);
        await Assert.That(at["y"]).IsEqualTo(64d);
        await Assert.That(at["z"]).IsEqualTo(-39.5);
        // Facing −x, which is where the spawn point is from there.
        await Assert.That(keeper["yaw"]).IsEqualTo(90d);
        await Assert.That(keeper["name"]).IsEqualTo("`b`lItem Shop");
    }

    /// <summary>A second shop stands on the other side rather than inside the first.</summary>
    [Test]
    public async Task Two_shops_flank_the_spawn_point()
    {
        var keepers = Keepers(Generate(Board(Shop(), Shop("upgrade-shop"))));
        await Assert.That(keepers.Count).IsEqualTo(4);

        var red = keepers.Take(2).Select(k => (Dict)((Dict)k!)["location"]!).ToList();
        await Assert.That(red[0]["x"]).IsEqualTo(2.5);
        await Assert.That(red[1]["x"]).IsEqualTo(-1.5);
        await Assert.That(((Dict)keepers[1]!)["yaw"]).IsEqualTo(-90d);
    }

    /// <summary>
    /// A room too narrow for the offset pulls the keeper in rather than putting it through a wall — and
    /// never onto the spawn point itself, where it would be standing in the players.
    /// </summary>
    [Test]
    public async Task A_keeper_is_held_inside_the_spawns_room()
    {
        var narrow = new MapIntent
        {
            Spawns = [new SpawnIntent { Team = "red", Point = new Pt(0, 64, 0), Yaw = 0, Footprint = new Rect(-2, -4, 2, 4) }],
            Shops = [Shop()],
        };
        var at = (Dict)At(Keepers(Generate(narrow)), 0)["location"]!;
        // The footprint spans blocks x −2…2 and its wall takes the outer course, so the innermost floor
        // block on that side is x = 1.
        await Assert.That(at["x"]).IsEqualTo(1.5);
    }

    /// <summary>A shop with no keeper is a shop opened some other way — the catalogue is written and
    /// nothing stands anywhere.</summary>
    [Test]
    public async Task A_shop_with_no_keeper_puts_nobody_at_the_spawn()
    {
        var doc = Generate(Board(Shop() with { Keeper = null }));
        await Assert.That(Shops(doc).Count).IsEqualTo(1);
        await Assert.That(doc.ContainsKey("shopkeepers")).IsFalse();
    }

    /// <summary>
    /// The whole slice through the document codec: what the generator writes is what the XML writer emits
    /// and the parser reads back. A generator that wrote a key no decoder names would pass every assertion
    /// above and produce a map with no shop in it.
    /// </summary>
    [Test]
    public async Task The_generated_document_emits_a_shop_and_its_keepers()
    {
        var doc = Generate(Board(Shop(keeper: new ShopkeeperIntent { Name = "Shop", Mob = "Villager" })));
        doc["name"] = "Shop Board";
        doc["version"] = "1.0.0";

        var map = MapParser.ParseXmlString(XmlWriter.ToXml(Deserializer.FromDict(doc)));
        var shop = map.Shops.Single();
        await Assert.That(shop.Id).IsEqualTo("item-shop");
        await Assert.That(shop.Categories.Single().Icons.Count).IsEqualTo(2);
        await Assert.That(shop.Categories.Single().Icons[1].Item.TeamColor).IsTrue();

        await Assert.That(map.Shopkeepers.Count).IsEqualTo(2);
        await Assert.That(map.Shopkeepers[0].Mob).IsEqualTo("Villager");
        await Assert.That(map.Shopkeepers[0].Location).IsEqualTo(new Vec3(2.5, 64, -39.5));
    }
}
