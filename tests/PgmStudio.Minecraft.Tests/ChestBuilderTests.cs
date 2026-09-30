using fNbt;
using PgmStudio.Minecraft.Dressing;
using PgmStudio.Minecraft.Stamping;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// A chest's stated stacks as the slots they fill, and an enchantment named the way PGM names it.
/// </summary>
public sealed class ChestBuilderTests
{
    [Test]
    public async Task A_stack_stating_a_slot_takes_it_and_the_rest_fill_the_free_slots_in_order()
    {
        var contents = ChestBuilder.Contents(
        [
            new ChestItem { Item = "arrow", Count = 32 },
            new ChestItem { Item = "minecraft:bow", Slot = 0, Enchantments = [new ChestEnchantment { Name = "power", Level = 1 }] },
            new ChestItem { Item = "golden_apple", Count = 2, Slot = 4 },
            new ChestItem { Item = "stone", Count = 16 },
        ]).ToList();

        await Assert.That(contents.Select(stack => stack.Slot)).IsEquivalentTo([1, 0, 4, 2]);
        await Assert.That(contents[0].Item.Get<NbtString>("id")!.Value).IsEqualTo("minecraft:arrow");
        await Assert.That(contents[0].Item.Get<NbtByte>("Count")!.Value).IsEqualTo((byte)32);
        var enchantment = contents[1].Item.Get<NbtCompound>("tag")!.Get<NbtList>("ench")!.OfType<NbtCompound>().Single();
        await Assert.That(enchantment.Get<NbtShort>("id")!.Value).IsEqualTo((short)48);
        await Assert.That(enchantment.Get<NbtShort>("lvl")!.Value).IsEqualTo((short)1);
    }

    [Test]
    [Arguments("power", 48)]
    [Arguments("ARROW_DAMAGE", 48)]
    [Arguments("Infinity", 51)]
    [Arguments("sharpness", 16)]
    [Arguments("bane of arthropods", 18)]
    [Arguments("34", 34)]
    public async Task An_enchantment_is_read_by_PGMs_name_the_Bukkit_name_or_the_number(string name, int id)
        => await Assert.That(ChestBuilder.EnchantmentId(name)).IsEqualTo(id);

    [Test]
    public async Task A_name_neither_knows_is_no_enchantment()
        => await Assert.That(ChestBuilder.EnchantmentId("vorpal")).IsNull();
}
