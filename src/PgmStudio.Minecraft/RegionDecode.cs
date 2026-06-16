using fNbt;

namespace PgmStudio.Minecraft;

/// <summary>Shared NBT scalar coercion (one home for what <c>FeatureExtractors</c>,
/// <c>MonumentSliceExtractor</c> and <c>MonumentSuggester</c> previously each re-implemented).</summary>
internal static class Nbt
{
    public static int? Int(NbtTag? tag) => tag switch
    {
        NbtByte b => b.Value, NbtShort s => s.Value, NbtInt i => i.Value,
        NbtLong l => (int)l.Value, NbtFloat f => (int)f.Value, NbtDouble d => (int)d.Value,
        NbtString s when int.TryParse(s.Value, out var v) => v, _ => null,
    };

    public static double Dbl(NbtTag? tag) => tag switch
    {
        NbtDouble d => d.Value, NbtFloat f => f.Value, NbtInt i => i.Value,
        NbtShort s => s.Value, NbtByte b => b.Value, NbtLong l => l.Value, _ => 0.0,
    };

    public static string? Str(NbtTag? tag) => tag switch
    {
        null => null, NbtString s => s.Value, NbtByte b => b.Value.ToString(),
        NbtShort s => s.Value.ToString(), NbtInt i => i.Value.ToString(), NbtLong l => l.Value.ToString(), _ => null,
    };
}

/// <summary>One pass over a chunk stream that indexes the non-air blocks, tile entities and entities a
/// monument reader needs, filtered to the cells/columns it cares about. Shared by
/// <see cref="MonumentSliceExtractor"/> and <see cref="MonumentSuggester"/> (which previously each had
/// their own copy of this loop).</summary>
internal static class RegionScan
{
    /// <summary><paramref name="wantCell"/> keeps blocks and tile entities at the coordinates the caller
    /// needs; <paramref name="wantChunk"/> (optional, chunk X/Z) skips whole chunks before decode — pass
    /// the box's chunk-intersection test, or null to scan every chunk. Entities are returned for every
    /// kept chunk (feet floored in x/z) for the caller to associate.</summary>
    public static (Dictionary<(int, int, int), (int Id, int Data)> Blocks,
                   List<(int X, int Y, int Z, NbtCompound Te)> Tiles,
                   List<(int Fx, double Fy, int Fz, NbtCompound En)> Entities)
        Read(IEnumerable<AnvilRegion.Chunk> chunks, Func<int, int, int, bool> wantCell, Func<int, int, bool>? wantChunk = null)
    {
        var blocks = new Dictionary<(int, int, int), (int, int)>();
        var tiles = new List<(int, int, int, NbtCompound)>();
        var entities = new List<(int, double, int, NbtCompound)>();

        foreach (var chunk in chunks)
        {
            if (wantChunk is not null && !wantChunk(chunk.ChunkX, chunk.ChunkZ)) continue;

            foreach (var b in AnvilRegion.Blocks(chunk))
                if (wantCell(b.X, b.Y, b.Z)) blocks[(b.X, b.Y, b.Z)] = (b.Id, b.Data);

            if (chunk.Level.Get<NbtList>("TileEntities") is { } teList)
                foreach (var teObj in teList)
                {
                    if (teObj is not NbtCompound te) continue;
                    if (Nbt.Int(te.Get("x")) is not { } x || Nbt.Int(te.Get("y")) is not { } y || Nbt.Int(te.Get("z")) is not { } z) continue;
                    if (wantCell(x, y, z)) tiles.Add((x, y, z, te));
                }

            if (chunk.Level.Get<NbtList>("Entities") is { } enList)
                foreach (var enObj in enList)
                {
                    if (enObj is not NbtCompound en || en.Get<NbtList>("Pos") is not { Count: >= 3 } pos) continue;
                    entities.Add(((int)Math.Floor(Nbt.Dbl(pos[0])), Nbt.Dbl(pos[1]), (int)Math.Floor(Nbt.Dbl(pos[2])), en));
                }
        }
        return (blocks, tiles, entities);
    }
}
