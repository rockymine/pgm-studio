using System.Globalization;
using fNbt;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Dressing;
using PgmStudio.Minecraft.Palette;

namespace PgmStudio.Minecraft.Stamping;

/// <summary>
/// Builds 1.8 chest tile entities and item stacks (string ids, <c>Count</c>/<c>Damage</c>, optional
/// enchantment tag) for placing loot into a synthesised world, and places the chest that holds them — the one
/// placer every chest in a built world goes through, whoever decided what is in it.
/// </summary>
public static class ChestBuilder
{
    /// <summary>Set a chest at <paramref name="x"/>/<paramref name="y"/>/<paramref name="z"/> fronting
    /// <paramref name="facing"/>, holding <paramref name="items"/>: the block and its tile entity, and nothing
    /// else — what stands around it is the caller's.</summary>
    public static void Place(VoxelWorld world, int x, int y, int z, int facing, IEnumerable<(int Slot, NbtCompound Item)> items)
    {
        world.SetBlock(x, y, z, Blocks.Chest, facing);
        world.AddTileEntity(x, z, Chest(x, y, z, items));
    }

    /// <summary>A chest's stated stacks as the slots they fill: a stack naming a slot takes it, and the rest
    /// take the free slots in order. The document has already refused a slot out of range or taken twice, an
    /// empty item and an enchantment neither PGM nor the game names (<see cref="EnchantmentId"/>).</summary>
    public static IEnumerable<(int Slot, NbtCompound Item)> Contents(IReadOnlyList<ChestItem> items)
    {
        var taken = items.Where(item => item.Slot is not null).Select(item => item.Slot!.Value).ToHashSet();
        var next = 0;
        foreach (var item in items)
        {
            var slot = item.Slot ?? NextFree();
            var id = item.Item.Contains(':') ? item.Item : $"minecraft:{item.Item}";
            var enchanted = item.Enchantments.Select(enchantment => (EnchantmentId(enchantment.Name)!.Value, enchantment.Level)).ToArray();
            yield return (slot, enchanted.Length > 0
                ? Enchanted(id, item.Count, item.Damage, enchanted)
                : Item(id, item.Count, item.Damage));
        }

        int NextFree()
        {
            while (taken.Contains(next)) next++;
            taken.Add(next);
            return next;
        }
    }

    /// <summary>The 1.8 number of an enchantment named the way PGM names it (<c>power</c>, <c>sharpness</c>),
    /// by its Bukkit name (<c>ARROW_DAMAGE</c>), or by the number itself; null for a name neither knows.</summary>
    public static int? EnchantmentId(string name)
    {
        var key = name.Trim().Replace('-', '_').Replace(' ', '_').ToLowerInvariant();
        if (int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)) return number;
        return EnchantmentIds.TryGetValue(key, out var id) ? id : null;
    }

    private static readonly Dictionary<string, int> EnchantmentIds = new()
    {
        ["protection"] = 0, ["protection_environmental"] = 0,
        ["fire_protection"] = 1, ["protection_fire"] = 1,
        ["feather_falling"] = 2, ["protection_fall"] = 2,
        ["blast_protection"] = 3, ["protection_explosions"] = 3,
        ["projectile_protection"] = 4, ["protection_projectile"] = 4,
        ["respiration"] = 5, ["oxygen"] = 5,
        ["aqua_affinity"] = 6, ["water_worker"] = 6,
        ["thorns"] = 7,
        ["depth_strider"] = 8,
        ["sharpness"] = 16, ["damage_all"] = 16,
        ["smite"] = 17, ["damage_undead"] = 17,
        ["bane_of_arthropods"] = 18, ["damage_arthropods"] = 18,
        ["knockback"] = 19,
        ["fire_aspect"] = 20,
        ["looting"] = 21, ["loot_bonus_mobs"] = 21,
        ["efficiency"] = 32, ["dig_speed"] = 32,
        ["silk_touch"] = 33,
        ["unbreaking"] = 34, ["durability"] = 34,
        ["fortune"] = 35, ["loot_bonus_blocks"] = 35,
        ["power"] = 48, ["arrow_damage"] = 48,
        ["punch"] = 49, ["arrow_knockback"] = 49,
        ["flame"] = 50, ["arrow_fire"] = 50,
        ["infinity"] = 51, ["arrow_infinite"] = 51,
        ["luck_of_the_sea"] = 61, ["luck"] = 61,
        ["lure"] = 62,
    };

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
