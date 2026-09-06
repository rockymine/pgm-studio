using PgmStudio.Minecraft.Dressing;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Painting;
using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Export;

/// <summary>
/// The biome each chunk of the exported world carries. A biome places nothing and costs nothing — it is one
/// byte a client reads to tint grass, leaves and water — so this is the cheapest colour a board has.
///
/// <para><b>The map states one field and a patch may state another over it.</b> The map's field is what
/// every column answers to, and the areas an author drew in the Dressing phase sit on top of it — so a corner
/// of the board reads as desert against a map that is otherwise forest and river. A column inside a patch
/// answers that patch, and the last-drawn patch holding a column is the one it answers, the way paint laid
/// later covers paint laid earlier.</para>
///
/// <para>What the pass adds beyond that is the fold — a mirrored board must answer the same biome on both
/// halves, for the reason the painter folds every cell before a pattern samples it (TP21). A field sampled
/// unfolded would put a forest against a desert across the axis and make the two halves read as different
/// ground. The patches are held to the same fold, so an area drawn on the primary half already answers at its
/// image and is never fanned.</para>
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

    /// <summary>The biome patches a layout's dressing draws, in the order they were drawn. Read through
    /// <see cref="DressingScope"/> because that is where the dressing document's place in a layout is
    /// stated.</summary>
    public static IReadOnlyList<BiomePatch> PatchesOf(string layoutJson) => DressingScope.DocOf(layoutJson).Biomes;

    /// <summary>
    /// Give every column of every chunk the world holds its biome. Per column, because that is what the
    /// format stores: asking once per chunk would quantise any field into chunk-sized rectangles, and a board
    /// a few chunks across has too few of them to carry a pattern at all.
    ///
    /// <para>Each column is folded through <paramref name="foldCell"/> before anything is asked, so a
    /// mirrored board answers the same biome at a cell and at its image. A column is then offered to the
    /// patches from the last drawn back, and falls to <paramref name="field"/> where none holds it.</para>
    ///
    /// <para>A board that states no field and draws no patch is left alone entirely, which is the plains
    /// every chunk is created with.</para>
    /// </summary>
    public static void Paint(VoxelWorld world, BiomeField? field,
                             IReadOnlyList<BiomePatch>? patches = null,
                             Func<int, int, (int X, int Z)>? foldCell = null)
    {
        var drawn = patches is { Count: > 0 } ? patches : null;
        if (field is null && drawn is null) return;
        foreach (var (cx, cz) in world.ChunkCoords.ToList())
            for (var dz = 0; dz < 16; dz++)
                for (var dx = 0; dx < 16; dx++)
                {
                    int x = (cx << 4) + dx, z = (cz << 4) + dz;
                    var (fx, fz) = foldCell?.Invoke(x, z) ?? (x, z);
                    if (At(field, drawn, fx, fz) is { } biome) world.SetBiome(x, z, biome);
                }
    }

    /// <summary>The biome a folded column answers: the last-drawn patch holding it, else the map's own field,
    /// else nothing — which leaves the chunk the plains it was created with.</summary>
    private static byte? At(BiomeField? field, IReadOnlyList<BiomePatch>? patches, int x, int z)
    {
        if (patches is not null)
            for (var at = patches.Count - 1; at >= 0; at--)
                if (patches[at].Holds(x, z)) return patches[at].Field.At(x, z);
        return field?.At(x, z);
    }
}
