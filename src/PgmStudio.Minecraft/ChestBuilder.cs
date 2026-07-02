using fNbt;

namespace PgmStudio.Minecraft;

/// <summary>
/// Builds 1.8 chest tile entities and item stacks (string ids, <c>Count</c>/<c>Damage</c>, optional
/// enchantment tag) for placing loot into a synthesised world.
/// </summary>
public static class ChestBuilder
{
    /// <summary>An item stack for a chest slot: string <paramref name="id"/> (e.g. <c>minecraft:planks</c>),
    /// stack <paramref name="count"/>, metadata <paramref name="damage"/>, optional <c>tag</c> compound.</summary>
    public static NbtCompound Item(string id, int count, int damage = 0, NbtCompound? tag = null)
    {
        var item = new NbtCompound
        {
            new NbtString("id", id),
            new NbtByte("Count", (byte)count),
            new NbtShort("Damage", (short)damage),
        };
        if (tag is not null) item.Add(tag);
        return item;
    }

    /// <summary>A <c>bow</c> with the given enchantments (<c>(enchantId, level)</c> pairs — e.g. Power = 48,
    /// Infinity = 51).</summary>
    public static NbtCompound EnchantedBow(params (int Id, int Level)[] enchants)
    {
        var ench = new NbtList("ench", NbtTagType.Compound);
        foreach (var (id, lvl) in enchants)
            ench.Add(new NbtCompound { new NbtShort("id", (short)id), new NbtShort("lvl", (short)lvl) });
        return Item("minecraft:bow", 1, 0, new NbtCompound("tag") { ench });
    }

    /// <summary>A <c>Chest</c> tile entity at <paramref name="x"/>/<paramref name="y"/>/<paramref name="z"/>
    /// holding the given <c>(slot, item)</c> stacks.</summary>
    public static NbtCompound Chest(int x, int y, int z, IEnumerable<(int Slot, NbtCompound Item)> items)
    {
        var list = new NbtList("Items", NbtTagType.Compound);
        foreach (var (slot, item) in items)
        {
            item.Add(new NbtByte("Slot", (byte)slot));
            list.Add(item);
        }
        return new NbtCompound
        {
            new NbtString("id", "Chest"),
            new NbtInt("x", x),
            new NbtInt("y", y),
            new NbtInt("z", z),
            list,
        };
    }

    /// <summary>Fill a chest row (9 slots starting at <paramref name="row"/>×9) with copies of one stack.</summary>
    public static IEnumerable<(int Slot, NbtCompound Item)> Row(int row, Func<NbtCompound> item)
    {
        for (var i = 0; i < 9; i++) yield return (row * 9 + i, item());
    }
}
