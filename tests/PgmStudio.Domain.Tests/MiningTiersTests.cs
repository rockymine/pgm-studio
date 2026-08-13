using PgmStudio.Domain;

namespace PgmStudio.Domain.Tests;

/// <summary>The pickaxe tier a PGM material match needs, and the tier a pickaxe item grants.</summary>
public sealed class MiningTiersTests
{
    [Test]
    public async Task Obsidian_needs_diamond()
    {
        await Assert.That(MiningTiers.Required("obsidian")).IsEqualTo("diamond");
    }

    [Test]
    public async Task Gold_and_emerald_need_iron()
    {
        await Assert.That(MiningTiers.Required("gold block")).IsEqualTo("iron");
        await Assert.That(MiningTiers.Required("emerald block")).IsEqualTo("iron");
    }

    [Test]
    public async Task Ender_stone_needs_only_wood()
    {
        await Assert.That(MiningTiers.Required("ender stone")).IsEqualTo("wood");
        await Assert.That(MiningTiers.Required("end_stone")).IsEqualTo("wood");   // separator-insensitive
    }

    [Test]
    public async Task A_material_this_table_does_not_know_resolves_to_null()
    {
        // "cannot say", not "no tool needed" — a caller must not treat this as confirmed-breakable.
        await Assert.That(MiningTiers.Required("wool")).IsNull();
        await Assert.That(MiningTiers.Required("not a block")).IsNull();
    }

    [Test]
    public async Task A_compound_match_takes_the_hardest_pattern()
    {
        await Assert.That(MiningTiers.Required("gold block;obsidian")).IsEqualTo("diamond");
        await Assert.That(MiningTiers.Required("emerald block;wool")).IsEqualTo("iron");
    }

    [Test]
    public async Task Higher_picks_the_stronger_of_two_tiers()
    {
        await Assert.That(MiningTiers.Higher("iron", "diamond")).IsEqualTo("diamond");
        await Assert.That(MiningTiers.Higher("diamond", "wood")).IsEqualTo("diamond");
        await Assert.That(MiningTiers.Higher("stone", "stone")).IsEqualTo("stone");
    }

    [Test]
    public async Task TierOfPickaxe_reads_the_leading_word()
    {
        await Assert.That(MiningTiers.TierOfPickaxe("diamond pickaxe")).IsEqualTo("diamond");
        await Assert.That(MiningTiers.TierOfPickaxe("iron pickaxe")).IsEqualTo("iron");
    }

    [Test]
    public async Task TierOfPickaxe_is_null_for_a_non_pickaxe_item()
    {
        await Assert.That(MiningTiers.TierOfPickaxe("iron sword")).IsNull();
        await Assert.That(MiningTiers.TierOfPickaxe("golden apple")).IsNull();
    }
}
