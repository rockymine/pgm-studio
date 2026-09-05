using PgmStudio.Domain;
using PgmStudio.Pgm;
using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Pgm.Tests;

/// <summary>
/// The standard boilerplate added to generated maps at export (<see cref="MapStandards"/>): item/tool
/// rules derived from the spawn kit, the kill-reward include, and hunger off — and that the writer emits
/// them as PGM elements.
/// </summary>
public sealed class MapStandardsTests
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
                    new KitItem { Slot = 9, Material = "stained clay", Amount = 32, TeamColor = true },
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
    public async Task ItemKeep_is_the_non_armor_items_including_blocks()
    {
        var m = MapWithKit();
        MapStandards.Apply(m);
        await Assert.That(m.ItemKeep).Contains("iron sword");
        await Assert.That(m.ItemKeep).Contains("golden apple");
        await Assert.That(m.ItemKeep).Contains("arrow");
        await Assert.That(m.ItemKeep).Contains("wood");                  // build blocks are kept (template)
        await Assert.That(m.ItemKeep).Contains("stained clay");
        await Assert.That(m.ItemKeep).DoesNotContain("leather helmet");  // armor is removed, not kept
    }

    [Test]
    public async Task Build_blocks_are_kept_and_their_place_drops_suppressed()
    {
        var m = MapWithKit();
        MapStandards.Apply(m);

        // build blocks are kept on death (template), not dropped — only the armour is removed
        await Assert.That(m.ItemKeep).Contains("wood");
        await Assert.That(m.ItemKeep).Contains("stained clay");
        await Assert.That(m.ItemRemove).DoesNotContain("wood");
        await Assert.That(m.ItemRemove).DoesNotContain("stained clay");
        await Assert.That(m.ItemRemove).Contains("leather helmet");

        // a block-drops rule still suppresses their place-and-break drop (chance 0) — placed blocks can't be farmed
        await Assert.That(m.BlockDropRules.Count).IsEqualTo(1);
        var rule = m.BlockDropRules[0];
        await Assert.That(rule.FilterMaterials).Contains("wood");
        await Assert.That(rule.FilterMaterials).Contains("stained clay");
        await Assert.That(rule.Items.Count).IsEqualTo(1);
        await Assert.That(rule.Items[0].Chance).IsEqualTo(0.0);

        // and it serializes to the expected block-drops xml
        var xml = XmlWriter.ToXml(m);
        await Assert.That(xml).Contains("<block-drops>");
        await Assert.That(xml).Contains("<material>wood</material>");
        await Assert.That(xml).Contains("<material>stained clay</material>");
        await Assert.That(xml).Contains("chance=\"0\"");
    }

    [Test]
    public async Task ToolRepair_is_only_the_tools_and_weapons()
    {
        var m = MapWithKit();
        MapStandards.Apply(m);
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
        MapStandards.Apply(m);
        await Assert.That(m.ItemRemove).Contains("leather helmet");
        await Assert.That(m.ItemRemove).Contains("chainmail leggings");
        await Assert.That(m.ItemRemove).DoesNotContain("iron sword");
        await Assert.That(m.ItemRemove).DoesNotContain("string");   // no surface ids → armor only
    }

    [Test]
    public async Task ItemRemove_extends_with_surface_terrain_drops()
    {
        var m = MapWithKit();
        // surface palette: cobweb (30), tall grass (31), leaves (18), gravel (13), quartz (155, no drop)
        MapStandards.Apply(m, new HashSet<int> { 30, 31, 18, 13, 155 });

        await Assert.That(m.ItemRemove).Contains("leather helmet");   // armor still there
        await Assert.That(m.ItemRemove).Contains("string");           // cobweb
        await Assert.That(m.ItemRemove).Contains("seeds");            // tall grass
        await Assert.That(m.ItemRemove).Contains("long grass");      // tall grass block item
        await Assert.That(m.ItemRemove).Contains("sapling");         // leaves
        await Assert.That(m.ItemRemove).Contains("apple");           // oak leaves
        await Assert.That(m.ItemRemove).Contains("flint");           // gravel
        // de-duped, and a block with no mapped drop adds nothing
        await Assert.That(m.ItemRemove.Distinct().Count()).IsEqualTo(m.ItemRemove.Count);
    }

    [Test]
    public async Task Adds_killreward_include_and_hunger_off()
    {
        var m = MapWithKit();
        MapStandards.Apply(m);
        await Assert.That(m.Includes).Contains("gapple-kill-reward");
        await Assert.That(m.HungerDepletion).IsEqualTo("off");
    }

    [Test]
    public async Task Adds_a_block_kill_reward_from_the_kit_blocks()
    {
        var m = MapWithKit();
        MapStandards.Apply(m);

        await Assert.That(m.KillRewards.Count).IsEqualTo(1);
        var items = m.KillRewards[0].Items;
        var wood = items.Single(i => i.Material == "wood");
        var clay = items.Single(i => i.Material == "stained clay");
        await Assert.That(wood.Amount).IsEqualTo(16);        // neutral block
        await Assert.That(wood.TeamColor).IsFalse();
        await Assert.That(clay.Amount).IsEqualTo(8);         // team-coloured block
        await Assert.That(clay.TeamColor).IsTrue();

        var xml = XmlWriter.ToXml(m);
        await Assert.That(xml).Contains("<kill-rewards>");
        await Assert.That(xml).Contains("<item material=\"wood\" amount=\"16\"/>");
        await Assert.That(xml).Contains("<item material=\"stained clay\" amount=\"8\" team-color=\"true\"/>");
    }

    [Test]
    public async Task Writer_emits_the_elements_and_reparses()
    {
        var m = MapWithKit();
        MapStandards.Apply(m);
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

    // ── what a destroy objective drops (PG4) ─────────────────────────────────────────────────────────

    /// <summary><b>The obsidian an attacker's pick drops is what the defending team rebuilds with.</b> PGM
    /// lets the owner repair a destroyable unless the map says otherwise, and a core cannot say otherwise at
    /// all — it has no `repairable`, and a block put back into its casing passes every check. Cancelling the
    /// item where it spawns is upstream of both, and it is what 202 of the 313 corpus DTM/DTC maps do.
    /// <para>The ladder counts too: an objective that becomes a gold block and then glass drops those, so all
    /// three are removed. `alpine_mining_ii` removes obsidian, beacon and coal block for exactly this
    /// reason.</para></summary>
    [Test]
    public async Task Every_material_an_objective_ever_is_is_removed_as_a_drop()
    {
        var m = MapWithKit();
        m.Destroyables = [new Destroyable { Id = "d", Name = "Monument", Owner = "red", Materials = "obsidian" }];
        m.Cores = [new Core { Id = "c", Owner = "blue" }];          // no material stated: obsidian
        m.Modes =
        [
            new ObjectiveMode { Id = "mode-gold-block", After = "15m", Material = "gold block" },
            new ObjectiveMode { Id = "mode-glass", After = "20m", Material = "glass" },
        ];

        MapStandards.Apply(m);

        await Assert.That(m.ItemRemove).Contains("obsidian");        // what both objectives start as
        await Assert.That(m.ItemRemove).Contains("gold block");      // and what the ladder turns them into
        await Assert.That(m.ItemRemove).Contains("glass");
        await Assert.That(m.ItemRemove).Contains("leather helmet");  // the armour is still there
        await Assert.That(m.ItemRemove.Count).IsEqualTo(m.ItemRemove.Distinct().Count());
    }

    /// <summary>A material is named without its data nibble, because an <c>&lt;item&gt;</c> removes a dropped
    /// stack and a stack does not carry the pattern the objective matched on.</summary>
    [Test]
    public async Task A_materials_data_nibble_is_not_carried_into_the_drop()
    {
        var m = MapWithKit();
        m.Destroyables = [new Destroyable { Id = "d", Name = "M", Owner = "red", Materials = "stained clay:4" }];

        MapStandards.Apply(m);

        await Assert.That(m.ItemRemove).Contains("stained clay");
        await Assert.That(m.ItemRemove).DoesNotContain("stained clay:4");
    }

    /// <summary>And a map with nothing to break removes nothing extra. A wool is carried rather than broken,
    /// so a CTW map's obsidian — if it has any — is terrain like any other.</summary>
    [Test]
    public async Task A_map_with_no_destroy_objective_removes_no_objective_drop()
    {
        var m = MapWithKit();

        MapStandards.Apply(m);

        await Assert.That(m.ItemRemove).DoesNotContain("obsidian");
    }
}
