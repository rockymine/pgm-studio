using fNbt;

namespace PgmStudio.Minecraft.Stamping;

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

    /// <summary>An item stack carrying an <c>ench</c> list — the given <c>(enchantId, level)</c> pairs (e.g.
    /// Efficiency = 32, Power = 48, Infinity = 51). The base game reads levels above the vanilla cap fine.</summary>
    public static NbtCompound Enchanted(string id, int count, int damage, params (int Id, int Level)[] enchants)
    {
        var ench = new NbtList("ench", NbtTagType.Compound);
        foreach (var (eid, lvl) in enchants)
            ench.Add(new NbtCompound { new NbtShort("id", (short)eid), new NbtShort("lvl", (short)lvl) });
        return Item(id, count, damage, new NbtCompound("tag") { ench });
    }

    /// <summary>A <c>bow</c> with the given enchantments (<c>(enchantId, level)</c> pairs — e.g. Power = 48,
    /// Infinity = 51).</summary>
    public static NbtCompound EnchantedBow(params (int Id, int Level)[] enchants)
        => Enchanted("minecraft:bow", 1, 0, enchants);

    /// <summary>Split a total item count into stacks of at most <paramref name="perSlot"/>, each built by
    /// <paramref name="stack"/> from its own count — how a bulk supply (384 planks) spreads across several
    /// slots. <paramref name="stack"/> takes the count so a partial last stack carries the remainder.</summary>
    public static IEnumerable<NbtCompound> Stacks(int total, int perSlot, Func<int, NbtCompound> stack)
    {
        var per = Math.Max(1, perSlot);
        for (var left = total; left > 0; left -= per) yield return stack(Math.Min(per, left));
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
