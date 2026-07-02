using fNbt;

namespace PgmStudio.Minecraft;

/// <summary>
/// A mutable numeric-format (1.8–1.12) block volume: sparse per-chunk 16×16×16 sections plus tile
/// entities and entities. Blocks are the old numeric id + 4-bit data value. Feeds
/// <see cref="AnvilRegionWriter"/>, which encodes it into <c>r.X.Z.mca</c> region files.
/// </summary>
public sealed class VoxelWorld
{
    public const int MaxHeight = 256;

    /// <summary>One chunk's 16 sections (lazily allocated) + its tile entities / entities.</summary>
    internal sealed class ChunkData
    {
        public readonly ushort[]?[] Ids = new ushort[16][];
        public readonly byte[]?[] Data = new byte[16][];
        public readonly List<NbtCompound> TileEntities = [];
        public readonly List<NbtCompound> Entities = [];
    }

    private readonly Dictionary<(int Cx, int Cz), ChunkData> _chunks = [];

    internal IReadOnlyDictionary<(int Cx, int Cz), ChunkData> Chunks => _chunks;

    public bool IsEmpty => _chunks.Count == 0;

    /// <summary>Set the block at world <paramref name="x"/>/<paramref name="y"/>/<paramref name="z"/>.</summary>
    public void SetBlock(int x, int y, int z, int id, int data = 0)
    {
        if (y is < 0 or >= MaxHeight) throw new ArgumentOutOfRangeException(nameof(y), y, "y must be 0..255");
        var chunk = ChunkAt(x, z, create: true)!;
        var sy = y >> 4;
        var ids = chunk.Ids[sy] ??= new ushort[4096];
        var dat = chunk.Data[sy] ??= new byte[4096];
        var idx = ((y & 15) << 8) | ((z & 15) << 4) | (x & 15);
        ids[idx] = (ushort)id;
        dat[idx] = (byte)(data & 0xF);
    }

    /// <summary>The block at the given coords, or <c>(0, 0)</c> (air) if unset / out of range.</summary>
    public (int Id, int Data) GetBlock(int x, int y, int z)
    {
        if (y is < 0 or >= MaxHeight) return (0, 0);
        var chunk = ChunkAt(x, z, create: false);
        var ids = chunk?.Ids[y >> 4];
        if (ids is null) return (0, 0);
        var idx = ((y & 15) << 8) | ((z & 15) << 4) | (x & 15);
        return (ids[idx], chunk!.Data[y >> 4]?[idx] ?? 0);
    }

    /// <summary>Attach a tile entity (sign, chest, …) to the chunk containing <paramref name="x"/>/<paramref name="z"/>.
    /// The compound must already carry its own <c>x</c>/<c>y</c>/<c>z</c> + <c>id</c> tags.</summary>
    public void AddTileEntity(int x, int z, NbtCompound tile)
        => ChunkAt(x, z, create: true)!.TileEntities.Add(tile);

    /// <summary>Attach an entity (armour stand, …) to the chunk containing <paramref name="x"/>/<paramref name="z"/>.</summary>
    public void AddEntity(int x, int z, NbtCompound entity)
        => ChunkAt(x, z, create: true)!.Entities.Add(entity);

    private ChunkData? ChunkAt(int x, int z, bool create)
    {
        var key = (x >> 4, z >> 4);
        if (_chunks.TryGetValue(key, out var c)) return c;
        if (!create) return null;
        return _chunks[key] = new ChunkData();
    }
}
