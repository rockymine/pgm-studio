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
                    new ShopItemIntent { Material = "wood", Amount = 32, Payments = [new ShopPaymentIntent(1, "gold nugget")] },
                    new ShopItemIntent { Material = "stained clay", Amount = 16, Payments = [new ShopPaymentIntent(1, "gold nugget")], TeamColor = true },
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

    /// <summary>PGM refuses a category with no icon, so a tab whose every item falls away is left out — and a
    /// shop left with no tab goes with it, which is the same rule one level up.</summary>
    [Test]
    public async Task A_tab_with_nothing_to_buy_is_not_written_and_takes_an_empty_shop_with_it()
    {
        var hollow = new ShopCategoryIntent { Id = "blocks", Material = "hard clay", Items = [] };
        var doc = Generate(Board(Shop() with { Categories = [hollow] }));

        await Assert.That(doc.ContainsKey("shops")).IsFalse();
        await Assert.That(doc.ContainsKey("shopkeepers")).IsFalse();
    }

    /// <summary>A shop keeping one good tab and losing another writes the one that loads.</summary>
    [Test]
    public async Task A_shop_writes_the_tabs_that_can_load_and_drops_the_rest()
    {
        var hollow = new ShopCategoryIntent { Id = "empty", Material = "stone", Items = [] };
        var shop = Shop();
        var doc = Generate(Board(shop with { Categories = [.. shop.Categories, hollow] }));

        var categories = (List<object?>)At(Shops(doc), 0)["categories"]!;
        await Assert.That(categories.Count).IsEqualTo(1);
        await Assert.That(((Dict)categories[0]!)["id"]).IsEqualTo("blocks");
    }

    // ── what an icon costs, and what buying it does ─────────────────────────────────
    /// <summary>Two payments are a price in two currencies at once — the upgrade ladder, where a tier costs
    /// the previous tier plus a coin. Both are written, in order.</summary>
    [Test]
    public async Task An_icon_carries_every_payment_it_states()
    {
        var ladder = new ShopItemIntent
        {
            Material = "diamond pickaxe",
            Payments = [new ShopPaymentIntent(1, "gold pickaxe"), new ShopPaymentIntent(8, "emerald", "green")],
        };
        var doc = Generate(Board(WithItems(ladder)));

        var payments = (List<object?>)At(Icons(doc), 0)["payments"]!;
        await Assert.That(payments.Count).IsEqualTo(2);
        await Assert.That(((Dict)payments[0]!)["currency"]).IsEqualTo("gold pickaxe");
        await Assert.That(((Dict)payments[1]!)["price"]).IsEqualTo(8);
        await Assert.That(((Dict)payments[1]!)["color"]).IsEqualTo("green");
    }

    /// <summary>An icon that names an action triggers it instead of handing over the stack, and the stack is
    /// still what the menu draws.</summary>
    [Test]
    public async Task An_icon_carries_the_action_it_triggers()
    {
        var upgrade = new ShopItemIntent
        {
            Material = "anvil", Name = "`e`lProtection I", Action = "add-protection",
            Payments = [new ShopPaymentIntent(4, "emerald")],
        };
        var icon = At(Icons(Generate(Board(WithItems(upgrade)))), 0);

        await Assert.That(icon["action"]).IsEqualTo("add-protection");
        await Assert.That(((Dict)icon["item"]!)["material"]).IsEqualTo("anvil");
    }

    /// <summary>An icon that states no payment at all is free, which is what an empty payment list already
    /// means to PGM — so nothing is written rather than a price of nought.</summary>
    [Test]
    public async Task An_icon_with_no_payment_is_written_free()
    {
        var gift = new ShopItemIntent { Material = "golden apple" };
        var icon = At(Icons(Generate(Board(WithItems(gift)))), 0);

        await Assert.That(icon.ContainsKey("payments")).IsFalse();
    }

    private static ShopIntent WithItems(params ShopItemIntent[] items) => Shop() with
    {
        Categories = [new ShopCategoryIntent { Id = "blocks", Material = "hard clay", Items = [.. items] }],
    };

    private static List<object?> Icons(Dict doc) =>
        (List<object?>)((Dict)((List<object?>)At(Shops(doc), 0)["categories"]!)[0]!)["icons"]!;

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

    // ── a keeper that names its own place ───────────────────────────────────────────
    /// <summary>A keeper standing somewhere the board named stands there <b>once</b>: a shop building in the
    /// middle of a map is one shop for everybody, not a villager per spawn. The coordinates land on the
    /// block's centre, because PGM spawns the entity exactly where the point says.</summary>
    [Test]
    public async Task A_keeper_with_a_place_of_its_own_stands_there_once()
    {
        var market = new ShopkeeperIntent { Name = "Market", At = new Pt(12, 70, -6), Yaw = 135 };
        var keepers = Keepers(Generate(Board(Shop(keeper: market))));

        await Assert.That(keepers.Count).IsEqualTo(1);
        var at = (Dict)At(keepers, 0)["location"]!;
        await Assert.That(at["x"]).IsEqualTo(12.5);
        await Assert.That(at["y"]).IsEqualTo(70d);
        await Assert.That(at["z"]).IsEqualTo(-5.5);
        await Assert.That(At(keepers, 0)["yaw"]).IsEqualTo(135d);
    }

    /// <summary>The other spelling: a region the map already holds, which is how twelve of the corpus's
    /// keepers state where they stand.</summary>
    [Test]
    public async Task A_keeper_can_name_the_region_it_stands_in()
    {
        var named = new ShopkeeperIntent { Region = "lime-spawn-shop-1" };
        var keeper = At(Keepers(Generate(Board(Shop(keeper: named)))), 0);

        await Assert.That(keeper["region"]).IsEqualTo("lime-spawn-shop-1");
        await Assert.That(keeper.ContainsKey("location")).IsFalse();
    }

    /// <summary>A keeper carrying both is answered by the coordinate, which resolves on its own where a
    /// region id is a reference that has to be found.</summary>
    [Test]
    public async Task A_coordinate_answers_a_keeper_that_states_both()
    {
        var both = new ShopkeeperIntent { At = new Pt(4, 64, 4), Region = "somewhere" };
        var keeper = At(Keepers(Generate(Board(Shop(keeper: both)))), 0);

        await Assert.That(keeper.ContainsKey("location")).IsTrue();
        await Assert.That(keeper.ContainsKey("region")).IsFalse();
    }

    /// <summary>A stated facing wins on a derived place too — the board keeps the spawn-side placement and
    /// says which way the villager looks.</summary>
    [Test]
    public async Task A_stated_facing_wins_over_the_derived_one()
    {
        var facing = new ShopkeeperIntent { Name = "Shop", Yaw = 45 };
        var keepers = Keepers(Generate(Board(Shop(keeper: facing))));

        await Assert.That(keepers.Count).IsEqualTo(2);
        await Assert.That(At(keepers, 0)["yaw"]).IsEqualTo(45d);
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
