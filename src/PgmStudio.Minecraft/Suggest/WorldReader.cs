using fNbt;
using PgmStudio.Domain;
using PgmStudio.Geom;
using PgmStudio.Minecraft.Anvil;
using static PgmStudio.Minecraft.Anvil.Nbt;

namespace PgmStudio.Minecraft.Suggest;

/// <summary>
/// Turning Anvil chunks into a <see cref="WorldReading"/> — the one place the format is understood on the
/// way to a derivation.
///
/// <para><b>Everything format-shaped stops here.</b> Which NBT list a 1.8 world keeps a head in and which a
/// 1.9 one does, how an item frame states the block it hangs on, how four sign lines become a sentence:
/// each is a fact about the file rather than about the map, and a monument reader that knew them would be
/// two subjects in one file. What leaves is positions, colours and words.</para>
/// </summary>
public static class WorldReader
{
    /// <summary>Read every block, sign, armour stand and wool-bearing item frame inside
    /// <paramref name="region"/>. Chunks that cannot intersect it are skipped before decode.</summary>
    public static WorldReading Read(IEnumerable<AnvilRegion.Chunk> chunks, BlockBox region)
    {
        var (blocks, tiles, entities) = RegionScan.Read(chunks, region.Contains, region.IntersectsChunk);

        var signs = tiles
            .Where(tile => Str(tile.Te.Get("id")) == "Sign")
            .Select(tile => new ReadSign(tile.X, tile.Y, tile.Z, MonumentSliceExtractor.ReadSignText(tile.Te)))
            .ToList();

        var stands = entities
            .Where(entity => Str(entity.En.Get("id")) == "ArmorStand"
                             && region.Contains(entity.Fx, (int)Math.Floor(entity.Fy), entity.Fz))
            .Select(entity => new ReadStand(
                entity.Fx, entity.Fy, entity.Fz, HeadWool(entity.En), Str(entity.En.Get("CustomName"))))
            .ToList();

        var frames = entities
            .Select(entity => FrameWool(entity.En))
            .Where(frame => frame is not null)
            .Select(frame => frame!.Value)
            .ToList();

        return new WorldReading(blocks, signs, stands, frames);
    }

    /// <summary>Which cell an item frame's monument sits over, by the direction it faces.</summary>
    private static readonly IReadOnlyDictionary<int, (int dx, int dz)> FrameSupport = new Dictionary<int, (int, int)>
    {
        [0] = (0, -1), [1] = (1, 0), [2] = (0, 1), [3] = (-1, 0),
    };

    /// <summary>An item frame entity holding a wool item → the support block it is mounted on and the wool
    /// colour, or null when it is not a wool-bearing item frame. The monument sits directly above or below
    /// the support.</summary>
    private static ReadFrame? FrameWool(NbtCompound frame)
    {
        if (Str(frame.Get("id")) is not ("ItemFrame" or "minecraft:item_frame")) return null;
        if (frame.Get("Item") is not NbtCompound item) return null;
        if (Str(item.Get("id")) is not { } id || !id.ToLowerInvariant().EndsWith("wool")) return null;
        if (Int(frame.Get("TileX")) is not { } tileX || Int(frame.Get("TileY")) is not { } tileY
            || Int(frame.Get("TileZ")) is not { } tileZ || Int(frame.Get("Facing")) is not { } facing
            || !FrameSupport.TryGetValue(facing, out var support)) return null;

        return new ReadFrame(tileX + support.dx, tileY, tileZ + support.dz,
            BlockColors.BlockColor(Int(item.Get("Damage")) ?? 0));
    }

    /// <summary>The wool colour on an armour stand's head, where it wears one. A 1.8 world keeps equipment
    /// in <c>Equipment[4]</c> and a 1.9 one in <c>ArmorItems[3]</c>; both are read, since a corpus holds
    /// worlds from either.</summary>
    private static string? HeadWool(NbtCompound stand)
    {
        foreach (var (list, head) in new[] { ("Equipment", 4), ("ArmorItems", 3) })
            if (stand.Get<NbtList>(list) is { } equipment && equipment.Count > head
                && equipment[head] is NbtCompound item)
            {
                var id = Str(item.Get("id"));
                if (id is not null && id.ToLowerInvariant().EndsWith("wool"))
                    return BlockColors.BlockColor(Int(item.Get("Damage")) ?? 0);
            }
        return null;
    }
}
