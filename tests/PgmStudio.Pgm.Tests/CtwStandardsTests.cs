using PgmStudio.Domain;
using PgmStudio.Pgm;
using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Pgm.Tests;

/// <summary>
/// The standard CTW boilerplate added to generated maps at export (<see cref="CtwStandards"/>): item/tool
/// rules derived from the spawn kit, the kill-reward include, and hunger off — and that the writer emits
/// them as PGM elements.
/// </summary>
public sealed class CtwStandardsTests
{
    private static MapXml MapWithKit() => new()
    {
        Name = "Test", Version = "1.0.0",
        Kits =
        [
            new Kit
            {
                Id = "spawn-kit",
                Items =
                [
                    new KitItem { Slot = 0, Material = "iron sword" },
                    new KitItem { Slot = 1, Material = "bow" },
                    new KitItem { Slot = 2, Material = "iron pickaxe" },
                    new KitItem { Slot = 3, Material = "iron axe" },
                    new KitItem { Slot = 4, Material = "iron spade" },
                    new KitItem { Slot = 5, Material = "shears" },
                    new KitItem { Slot = 6, Material = "golden apple" },
                    new KitItem { Slot = 7, Material = "wood", Amount = 64 },
                    new KitItem { Slot = 8, Material = "arrow" },
                ],
                Armor =
                [
                    new KitArmor { SlotName = "helmet", Material = "leather helmet" },
                    new KitArmor { SlotName = "chestplate", Material = "leather chestplate" },
                    new KitArmor { SlotName = "leggings", Material = "chainmail leggings" },
                    new KitArmor { SlotName = "boots", Material = "leather boots" },
                ],
            },
        ],
    };

    [Test]
    public async Task ItemKeep_is_every_non_armor_kit_item()
    {
        var m = MapWithKit();
        CtwStandards.Apply(m);
        await Assert.That(m.ItemKeep).Contains("iron sword");
        await Assert.That(m.ItemKeep).Contains("golden apple");
        await Assert.That(m.ItemKeep).Contains("wood");
        await Assert.That(m.ItemKeep).Contains("arrow");
        await Assert.That(m.ItemKeep).DoesNotContain("leather helmet");   // armor is removed, not kept
    }

    [Test]
    public async Task ToolRepair_is_only_the_tools_and_weapons()
    {
        var m = MapWithKit();
        CtwStandards.Apply(m);
        foreach (var t in new[] { "iron sword", "bow", "iron pickaxe", "iron axe", "iron spade", "shears" })
            await Assert.That(m.ToolRepair).Contains(t);
        await Assert.That(m.ToolRepair).DoesNotContain("golden apple");
        await Assert.That(m.ToolRepair).DoesNotContain("arrow");          // not durable
        await Assert.That(m.ToolRepair).DoesNotContain("wood");
    }

    [Test]
    public async Task ItemRemove_is_the_armor()
    {
        var m = MapWithKit();
        CtwStandards.Apply(m);
        await Assert.That(m.ItemRemove).Contains("leather helmet");
        await Assert.That(m.ItemRemove).Contains("chainmail leggings");
        await Assert.That(m.ItemRemove).DoesNotContain("iron sword");
    }

    [Test]
    public async Task Adds_killreward_include_and_hunger_off()
    {
        var m = MapWithKit();
        CtwStandards.Apply(m);
        await Assert.That(m.Includes).Contains("gapple-kill-reward");
        await Assert.That(m.HungerDepletion).IsEqualTo("off");
    }

    [Test]
    public async Task Writer_emits_the_elements_and_reparses()
    {
        var m = MapWithKit();
        CtwStandards.Apply(m);
        var xml = XmlWriter.ToXml(m);

        await Assert.That(xml).Contains("<include id=\"gapple-kill-reward\"/>");
        await Assert.That(xml).Contains("<itemkeep>");
        await Assert.That(xml).Contains("<toolrepair>");
        await Assert.That(xml).Contains("<tool>bow</tool>");
        await Assert.That(xml).Contains("<itemremove>");
        await Assert.That(xml).Contains("<depletion>off</depletion>");
        // PGM conventions: self-close without a space, and end with a newline.
        await Assert.That(xml).DoesNotContain(" />");
        await Assert.That(xml.EndsWith("</map>\n")).IsTrue();

        // still a loadable document
        var reparsed = Serializer.ToDict(MapParser.ParseXmlString(xml));
        await Assert.That(((List<object?>)reparsed["kits"]!).Count).IsEqualTo(1);
    }
}
