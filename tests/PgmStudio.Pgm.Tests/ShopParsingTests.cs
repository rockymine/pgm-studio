using PgmStudio.Domain;
using PgmStudio.Pgm;

namespace PgmStudio.Pgm.Tests;

/// <summary>
/// The shop XML surface. A shop is a menu and a keeper is an entity PGM spawns from the element, so
/// everything under test here is what the reader carries out of the document —
/// <c>docs/pgm/shops.md</c> is the contract and the corpus measurements these cases are drawn from.
/// </summary>
public sealed class ShopParsingTests
{
    private static MapXml Parse(string body) => MapParser.ParseXmlString(
        "<?xml version=\"1.0\"?><map proto=\"1.5.0\"><name>m</name><version>1</version><objective>o</objective>"
        + body + "</map>");

    // ── the catalogue ───────────────────────────────────────────────────────────────
    [Test]
    public async Task A_shop_carries_its_categories_and_their_icons()
    {
        var shop = Parse("""
            <shops>
              <shop id="item-shop" name="Items">
                <category id="blocks" name="`aBlocks" material="hard clay">
                  <item material="wood" amount="32" price="1" currency="gold nugget"/>
                  <item material="cobblestone" amount="32" price="1" currency="gold nugget"/>
                </category>
              </shop>
            </shops>
            """).Shops.Single();

        await Assert.That(shop.Id).IsEqualTo("item-shop");
        await Assert.That(shop.Name).IsEqualTo("Items");

        var category = shop.Categories.Single();
        await Assert.That(category.Id).IsEqualTo("blocks");
        await Assert.That(category.Icon.Material).IsEqualTo("hard clay");
        await Assert.That(category.Icon.Name).IsEqualTo("`aBlocks");
        await Assert.That(category.Icons.Count).IsEqualTo(2);
        await Assert.That(category.Icons[0].Item.Material).IsEqualTo("wood");
        await Assert.That(category.Icons[0].Item.Amount).IsEqualTo(32);
    }

    // A price written on the icon element is one payment: PGM reads the icon itself as a payment when it
    // carries no <payment> child, which is the spelling 766 of the corpus's 907 icons use.
    [Test]
    public async Task A_price_on_the_icon_is_one_payment()
    {
        var icon = OneIcon("""<item material="arrow" amount="16" price="1" currency="gold nugget" color="yellow"/>""");
        var payment = icon.Payments.Single();
        await Assert.That(payment.Price).IsEqualTo(1);
        await Assert.That(payment.Currency).IsEqualTo("gold nugget");
        await Assert.That(payment.Color).IsEqualTo("yellow");
    }

    // `color` on an icon is the price's, never the stack's. PGM reads a leather dye only off leather armour
    // and a payment colour always, and a colour name is not a hex — so an icon stating one has stated a
    // payment colour whatever the material is.
    [Test]
    public async Task The_icons_colour_belongs_to_the_price_and_not_to_the_stack()
    {
        var icon = OneIcon("""<item material="leather boots" price="4" currency="emerald" color="dark green"/>""");
        await Assert.That(icon.Item.Color).IsEqualTo("");
        await Assert.That(icon.Payments.Single().Color).IsEqualTo("dark green");
    }

    [Test]
    public async Task Several_payments_are_several_currencies_at_once()
    {
        var icon = OneIcon("""
            <item material="gold pickaxe" unbreakable="true">
              <payment price="6" currency="gold nugget"/>
              <payment price="1" currency="iron pickaxe"/>
            </item>
            """);
        await Assert.That(icon.Payments.Select(p => p.Currency)).IsEquivalentTo(new[] { "gold nugget", "iron pickaxe" });
        await Assert.That(icon.Payments.Single(p => p.Currency == "gold nugget").Price).IsEqualTo(6);
        await Assert.That(icon.Item.Unbreakable).IsTrue();
    }

    // A free icon is one with nothing to pay, which is an empty payment list rather than a zero-price entry.
    [Test]
    public async Task An_icon_that_states_no_price_costs_nothing()
    {
        var icon = OneIcon("""<item material="stick"/>""");
        await Assert.That(icon.Payments.Count).IsEqualTo(0);
    }

    // PGM resolves `action` and `kit` through one call against one feature namespace, so the two spellings
    // are one reference and the reader keeps one field for it.
    [Test]
    public async Task An_action_and_a_kit_are_one_reference()
    {
        await Assert.That(OneIcon("""<item material="anvil" action="add-protection"/>""").ActionId).IsEqualTo("add-protection");
        await Assert.That(OneIcon("""<item material="iron helmet" kit="ironhelmet-kit"/>""").ActionId).IsEqualTo("ironhelmet-kit");
    }

    // ── the stack ───────────────────────────────────────────────────────────────────
    [Test]
    public async Task An_icons_stack_carries_everything_the_element_states()
    {
        var item = OneIcon("""
            <item material="potion" name="`rJump Boost II (1:00)" damage="11" amount="1" lore="`cOne minute|`cOnly"
                  prevent-sharing="true" show-enchantments="false" enchantment="power:1;infinity">
              <effect duration="60s" amplifier="2">jump_boost</effect>
              <attribute operation="add" amount="0.01">generic.movementSpeed</attribute>
            </item>
            """).Item;

        await Assert.That(item.Material).IsEqualTo("potion");
        await Assert.That(item.Damage).IsEqualTo(11);
        await Assert.That(item.Name).IsEqualTo("`rJump Boost II (1:00)");
        // The lore keeps its separator: PGM splits on it, so the value is the lines and the bar between them.
        await Assert.That(item.Lore).IsEqualTo("`cOne minute|`cOnly");
        await Assert.That(item.PreventSharing).IsTrue();
        await Assert.That(item.Hidden).IsEquivalentTo(new[] { "enchantments" });
        await Assert.That(item.Enchantments).IsEqualTo("power:1,infinity:1");

        var effect = item.Effects.Single();
        await Assert.That(effect.Type).IsEqualTo("jump_boost");
        await Assert.That(effect.Duration).IsEqualTo("60s");
        await Assert.That(effect.Amplifier).IsEqualTo(2);

        var attribute = item.Attributes.Single();
        await Assert.That(attribute.Attribute).IsEqualTo("generic.movementSpeed");
        await Assert.That(attribute.Operation).IsEqualTo("add");
        await Assert.That(attribute.Amount).IsEqualTo(0.01);
    }

    // A material matcher names a class of blocks or a material, and the reader keeps the word either way.
    [Test]
    public async Task A_can_place_on_matcher_keeps_the_word_it_names()
    {
        var all = OneIcon("""<item material="tnt"><can-place-on><all-blocks/></can-place-on></item>""").Item;
        await Assert.That(all.CanPlaceOn).IsEquivalentTo(new[] { "all-blocks" });

        var one = OneIcon("""<item material="lever"><can-place-on><material>gold block</material></can-place-on></item>""").Item;
        await Assert.That(one.CanPlaceOn).IsEquivalentTo(new[] { "gold block" });
    }

    // ── the keepers ─────────────────────────────────────────────────────────────────
    // The corpus writes the shared shop and label once on a container and gives each keeper its coordinates,
    // which is the same attribute cascade every other group has.
    [Test]
    public async Task A_keeper_takes_its_shop_and_label_from_the_container()
    {
        var m = Parse("""
            <shopkeepers>
              <shopkeepers name="`b`lItem Shop" shop="item-shop">
                <shopkeeper yaw="-90">131.5,14,339.5</shopkeeper>
                <shopkeeper yaw="90">-142.5,14,333.5</shopkeeper>
              </shopkeepers>
            </shopkeepers>
            """);

        await Assert.That(m.Shopkeepers.Count).IsEqualTo(2);
        await Assert.That(m.Shopkeepers.Select(k => k.ShopId).Distinct()).IsEquivalentTo(new[] { "item-shop" });
        await Assert.That(m.Shopkeepers[0].Name).IsEqualTo("`b`lItem Shop");
        await Assert.That(m.Shopkeepers[0].Location).IsEqualTo(new PgmStudio.Geom.Vec3(131.5, 14, 339.5));
        await Assert.That(m.Shopkeepers[0].Yaw).IsEqualTo(-90.0);
        await Assert.That(m.Shopkeepers[1].Yaw).IsEqualTo(90.0);
    }

    [Test]
    public async Task A_keeper_states_its_place_as_a_point_or_as_a_region()
    {
        var point = Parse("""
            <shopkeepers><shopkeeper name="`cGholoth" shop="wrath-shop" mob="Zombie">
              <point yaw="135">2.5,6,-46.5</point>
            </shopkeeper></shopkeepers>
            """).Shopkeepers.Single();
        await Assert.That(point.Mob).IsEqualTo("Zombie");
        await Assert.That(point.Location).IsEqualTo(new PgmStudio.Geom.Vec3(2.5, 6, -46.5));
        await Assert.That(point.Yaw).IsEqualTo(135.0);

        var region = Parse("""
            <shopkeepers><shopkeeper shop="balls-shop">
              <region id="lime-spawn-shop-1" yaw="135"/>
            </shopkeeper></shopkeepers>
            """).Shopkeepers.Single();
        await Assert.That(region.Location).IsNull();
        await Assert.That(region.RegionId).IsEqualTo("lime-spawn-shop-1");
        await Assert.That(region.Yaw).IsEqualTo(135.0);
    }

    // A map whose shops come from an <include> states keepers for shops this document does not hold. That is
    // a whole map — 15 corpus maps are exactly it — so the reference is carried, not resolved.
    [Test]
    public async Task A_keeper_may_name_a_shop_the_document_does_not_hold()
    {
        var m = Parse("""
            <include id="8-team-bedwars"/>
            <shopkeepers><shopkeepers shop="item-shop"><shopkeeper yaw="0">-102.5,19,24.5</shopkeeper></shopkeepers></shopkeepers>
            """);
        await Assert.That(m.Shops.Count).IsEqualTo(0);
        await Assert.That(m.Shopkeepers.Single().ShopId).IsEqualTo("item-shop");
    }

    // ── what it refuses ─────────────────────────────────────────────────────────────
    // A shop is not an objective, so nothing about the map's goal is at stake; what is, is that a bedwars
    // board whose blocks are all bought is unplayable with an icon missing.
    [Test]
    public async Task A_shop_stating_something_unreadable_refuses_the_map()
    {
        await Assert.That(() => Parse("""
            <shops><shop id="s"><category id="c" material="book">
              <item material="written book"><title>Rules</title></item>
            </category></shop></shops>
            """)).Throws<UnsupportedMapException>();

        await Assert.That(() => Parse("""
            <shops><shop id="s"><category id="c" material="tnt">
              <item material="tnt" grenade="true"/>
            </category></shop></shops>
            """)).Throws<UnsupportedMapException>();
    }

    [Test]
    public async Task A_keeper_whose_place_cannot_be_read_refuses_the_map()
    {
        await Assert.That(() => Parse("""
            <shopkeepers><shopkeeper shop="s"><cuboid min="0,0,0" max="2,2,2"/></shopkeeper></shopkeepers>
            """)).Throws<UnsupportedMapException>();

        await Assert.That(() => Parse("""
            <shopkeepers><shopkeeper shop="s">not a vector</shopkeeper></shopkeepers>
            """)).Throws<UnsupportedMapException>();
    }

    // A category element IS an item, so it carries an item's own children beside the icons under it — the
    // two corpus maps that enchant a category icon parse because of this.
    [Test]
    public async Task A_category_may_carry_the_children_of_the_stack_it_is()
    {
        var category = Parse("""
            <shops><shop id="s"><category id="c" material="diamond sword">
              <enchantment level="3">sharpness</enchantment>
              <item material="stick"/>
            </category></shop></shops>
            """).Shops.Single().Categories.Single();

        await Assert.That(category.Icon.Enchantments).IsEqualTo("sharpness:3");
        await Assert.That(category.Icons.Count).IsEqualTo(1);
    }

    private static ShopIcon OneIcon(string itemXml) => Parse(
        $"""<shops><shop id="s" name="S"><category id="c" material="chest">{itemXml}</category></shop></shops>""")
        .Shops.Single().Categories.Single().Icons.Single();
}
