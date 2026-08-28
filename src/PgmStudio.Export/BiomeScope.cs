using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Painting;
using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Export;

/// <summary>
/// The biome each chunk of the exported world carries. A biome places nothing and costs nothing — it is one
/// byte a client reads to tint grass, leaves and water — so this is the cheapest colour a board has.
///
/// <para>Like <see cref="DressingScope"/> and unlike <see cref="TerrainThemeScope"/> there is no scope to
/// resolve: the field is map-wide, so reading it is reading one object. What the pass adds is the fold — a
/// mirrored board must answer the same biome on both halves, for the reason the painter folds every cell
/// before a pattern samples it (TP21). A field sampled unfolded would put a forest against a desert across
/// the axis and make the two halves read as different ground.</para>
/// </summary>
public static class BiomeScope
{
    /// <summary>The field a layout states, or null where it states none — which is plains everywhere, and what
    /// every board exported as before there was a field to state.</summary>
    public static BiomeField? FieldOf(string layoutJson)
    {
        var biome = SketchLayout.Parse(layoutJson)?.Biome;
        return biome is null ? null : TerrainThemeJson.DeserializeBiome(biome.Value.GetRawText());
    }

    /// <summary>
    /// Give every column of every chunk the world holds its biome. Per column, because that is what the
    /// format stores: asking once per chunk would quantise any field into chunk-sized rectangles, and a board
    /// a few chunks across has too few of them to carry a pattern at all.
    ///
    /// <para>Each column is folded through <paramref name="foldCell"/> before the field is asked, so a
    /// mirrored board answers the same biome at a cell and at its image — the reason the painter folds every
    /// cell before a pattern samples it (TP21). Without it a rot_180 board puts a desert against a forest
    /// across the axis and the two halves read as different ground.</para>
    /// </summary>
    public static void Paint(VoxelWorld world, BiomeField? field, Func<int, int, (int X, int Z)>? foldCell = null)
    {
        if (field is null) return;
        foreach (var (cx, cz) in world.ChunkCoords.ToList())
            for (var dz = 0; dz < 16; dz++)
                for (var dx = 0; dx < 16; dx++)
                {
                    int x = (cx << 4) + dx, z = (cz << 4) + dz;
                    var (fx, fz) = foldCell?.Invoke(x, z) ?? (x, z);
                    world.SetBiome(x, z, field.At(fx, fz));
                }
    }
}
