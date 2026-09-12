using PgmStudio.Domain;
using PgmStudio.Pgm;

namespace PgmStudio.Pgm.Tests;

/// <summary>
/// A shop survives both codecs unchanged — XML → MapXml → XML, and MapXml → JSON → MapXml. What is under
/// test is that nothing is materialised and nothing is dropped: an icon that states only a material comes
/// back stating only a material, and the one that states everything comes back with all of it.
/// </summary>
public sealed class ShopRoundTripTests
{
    private const string Xml = """
        <?xml version="1.0"?>
        <map proto="1.5.0">
          <name>Shop Map</name>
          <version>1.0.0</version>
          <objective>Buy what you need</objective>
          <teams>
            <team id="red" color="red" max="8">Red</team>
            <team id="blue" color="blue" max="8">Blue</team>
          </teams>
          <shops>
            <shop id="item-shop" name="Items">
              <category id="blocks" name="`aBlocks" material="hard clay">
                <item material="wood" amount="32" price="1" currency="gold nugget"/>
                <item material="stained clay" team-color="true" amount="16" price="1" currency="gold nugget"/>
                <item material="stick" name="`rKnockback Stick" enchantment="knockback:1" price="64"
                      currency="gold nugget" lore="`cThis item will disappear upon death!" prevent-sharing="true"/>
              </category>
              <category id="tools" name="`aTools" material="stone pickaxe">
                <item material="gold pickaxe" unbreakable="true" filter="has-iron-pick">
                  <payment price="6" currency="gold nugget"/>
                  <payment price="1" currency="iron pickaxe"/>
                </item>
                <item material="iron helmet" name="`rIron Helmet" price="8" currency="gold nugget"
                      kit="ironhelmet-kit" color="white"/>
              </category>
            </shop>
            <shop id="upgrade-shop">
              <category id="upgrades" material="beacon">
                <item material="anvil" name="`e`lProtection I" price="64" currency="gold nugget" action="add-protection"/>
              </category>
            </shop>
          </shops>
          <shopkeepers>
            <shopkeepers name="`b`lItem Shop" shop="item-shop">
              <shopkeeper yaw="-90">131.5,14,339.5</shopkeeper>
              <shopkeeper yaw="90">-142.5,14,333.5</shopkeeper>
            </shopkeepers>
            <shopkeeper shop="upgrade-shop" mob="Witch">
              <region id="blue-shop-stand" yaw="180"/>
            </shopkeeper>
          </shopkeepers>
          <regions>
            <point id="blue-shop-stand">131.5,14,333.5</point>
          </regions>
        </map>
        """;

    private static MapXml Parsed() => MapParser.ParseXmlString(Xml);

    private static async Task AssertWholeShopAsync(MapXml map)
    {
        await Assert.That(map.Shops.Select(s => s.Id)).IsEquivalentTo(new[] { "item-shop", "upgrade-shop" });

        var items = map.Shops.Single(s => s.Id == "item-shop");
        await Assert.That(items.Name).IsEqualTo("Items");
        await Assert.That(items.Categories.Select(c => c.Id)).IsEquivalentTo(new[] { "blocks", "tools" });

        var blocks = items.Categories.Single(c => c.Id == "blocks");
        await Assert.That(blocks.Icon.Material).IsEqualTo("hard clay");
        await Assert.That(blocks.Icon.Name).IsEqualTo("`aBlocks");
        await Assert.That(blocks.Icons.Count).IsEqualTo(3);

        // The one that states nearly everything an icon can.
        var stick = blocks.Icons.Single(i => i.Item.Material == "stick");
        await Assert.That(stick.Item.Name).IsEqualTo("`rKnockback Stick");
        await Assert.That(stick.Item.Lore).IsEqualTo("`cThis item will disappear upon death!");
        await Assert.That(stick.Item.Enchantments).IsEqualTo("knockback:1");
        await Assert.That(stick.Item.PreventSharing).IsTrue();
        await Assert.That(stick.Payments.Single().Price).IsEqualTo(64);

        // And the one that states almost nothing beyond its price: no amount, no name, no flags.
        var wood = blocks.Icons.Single(i => i.Item.Material == "wood");
        await Assert.That(wood.Item.Name).IsEqualTo("");
        await Assert.That(wood.Item.Amount).IsEqualTo(32);
        await Assert.That(wood.Item.Unbreakable).IsFalse();
        await Assert.That(wood.Item.Effects).IsEmpty();

        var tools = items.Categories.Single(c => c.Id == "tools");
        var pickaxe = tools.Icons.Single(i => i.Item.Material == "gold pickaxe");
        await Assert.That(pickaxe.FilterId).IsEqualTo("has-iron-pick");
        await Assert.That(pickaxe.Payments.Count).IsEqualTo(2);
        await Assert.That(pickaxe.Payments.Select(p => p.Currency)).IsEquivalentTo(new[] { "gold nugget", "iron pickaxe" });

        // A kit reference is an action reference: PGM resolves both through one lookup.
        await Assert.That(tools.Icons.Single(i => i.Item.Material == "iron helmet").ActionId).IsEqualTo("ironhelmet-kit");

        await Assert.That(map.Shopkeepers.Count).IsEqualTo(3);
        var first = map.Shopkeepers.First(k => k.ShopId == "item-shop");
        await Assert.That(first.Name).IsEqualTo("`b`lItem Shop");
        await Assert.That(first.Location).IsEqualTo(new PgmStudio.Geom.Vec3(131.5, 14, 339.5));
        await Assert.That(first.Yaw).IsEqualTo(-90.0);

        var witch = map.Shopkeepers.Single(k => k.ShopId == "upgrade-shop");
        await Assert.That(witch.Mob).IsEqualTo("Witch");
        await Assert.That(witch.RegionId).IsEqualTo("blue-shop-stand");
        await Assert.That(witch.Location).IsNull();
    }

    [Test]
    public async Task The_document_survives_the_xml_round_trip()
    {
        await AssertWholeShopAsync(MapParser.ParseXmlString(XmlWriter.ToXml(Parsed())));
    }

    [Test]
    public async Task The_document_survives_the_json_round_trip()
    {
        await AssertWholeShopAsync(Deserializer.FromDict(Serializer.ToDict(Parsed())));
    }

    /// <summary>
    /// A keeper's region reference survives to the emitted document — the writer has to know that a region
    /// named only from a shopkeeper is still referenced, or the regions block drops it and PGM refuses the
    /// map for an unknown id.
    /// </summary>
    [Test]
    public async Task A_region_a_keeper_stands_in_is_still_written()
    {
        await Assert.That(XmlWriter.ToXml(Parsed())).Contains("blue-shop-stand");
    }

    /// <summary>
    /// A map with no shop writes no <c>&lt;shops&gt;</c> and no <c>&lt;shopkeepers&gt;</c>. An empty block
    /// is not the same document: PGM builds no shop module for a map with no shops, and one with an empty
    /// <c>&lt;shops/&gt;</c> would carry the <c>shops</c> tag for a menu nobody can open.
    /// </summary>
    [Test]
    public async Task A_map_with_no_shop_writes_no_block()
    {
        var xml = XmlWriter.ToXml(new MapXml { Name = "Plain", Version = "1.0.0" });
        await Assert.That(xml).DoesNotContain("<shops");
        await Assert.That(xml).DoesNotContain("<shopkeeper");
    }
}
