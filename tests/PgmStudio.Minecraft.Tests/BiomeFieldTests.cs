using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Painting;
using PgmStudio.Minecraft.Palette;
using fNbt;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// The biome a column carries. The claims that matter are the ones the design rests on: a field answers per
/// column in block coordinates, so what lands is a pattern rather than a grid of chunk-sized rectangles; the
/// bytes reach the region file; and a world that states no field is the plains world every board already
/// exported as.
/// </summary>
public sealed class BiomeFieldTests
{
    [Test]
    public async Task An_unstated_field_leaves_every_chunk_plains()
    {
        var world = new VoxelWorld();
        world.SetBlock(0, 64, 0, Blocks.Stone);
        BiomeScopeStandIn.Paint(world, field: null);

        await Assert.That(BiomeOf(world, 0, 0)).IsEqualTo(Biome.Plains);
    }

    /// <summary>Every one of a chunk's 256 bytes is the field's answer — the assertion that the bytes reach
    /// the region file, rather than the plains the array was filled with before there was a field.</summary>
    [Test]
    public async Task A_chunks_every_byte_is_the_fields_answer()
    {
        var world = new VoxelWorld();
        world.SetBlock(0, 64, 0, Blocks.Stone);
        BiomeScopeStandIn.Paint(world, new SolidBiome(Biome.Mesa));

        var biomes = BiomesOf(world, 0, 0);
        await Assert.That(biomes.Length).IsEqualTo(256);
        await Assert.That(biomes.All(b => b == Biome.Mesa)).IsTrue();
    }

    /// <summary>**A chunk holds a pattern, not a colour.** The field is asked per column, so a region boundary
    /// crossing a chunk is inside that chunk's own 256 bytes — which is the whole difference between a pattern
    /// and a grid of chunk-sized rectangles, and cannot happen if the field is asked once per chunk.</summary>
    [Test]
    public async Task A_boundary_crossing_a_chunk_is_inside_that_chunks_own_bytes()
    {
        var world = new VoxelWorld();
        world.SetBlock(0, 64, 0, Blocks.Stone);
        // Cells eight blocks across: a sixteen-block chunk cannot hold only one of them.
        BiomeScopeStandIn.Paint(world, new CellBiome(7, CellSize: 8, Jitter: 100,
            [Biome.Forest, Biome.Desert, Biome.River]));

        await Assert.That(BiomesOf(world, 0, 0).Distinct().Count()).IsGreaterThan(1);
    }

    /// <summary>A column takes the field's answer at its own block coordinates, and lands at the index the
    /// format stores it under — <c>(z &amp; 15) * 16 + (x &amp; 15)</c>. A transposed index would still pass
    /// every "is it varied" assertion while putting the pattern down rotated.</summary>
    [Test]
    public async Task A_column_lands_at_its_own_index()
    {
        var world = new VoxelWorld();
        world.SetBlock(0, 64, 0, Blocks.Stone);
        // A field that answers desert on one column of the chunk and taiga everywhere else.
        BiomeScopeStandIn.Paint(world, new OneColumn(X: 3, Z: 5, Biome.Desert, Biome.Taiga));

        var biomes = BiomesOf(world, 0, 0);
        await Assert.That(biomes[(5 << 4) | 3]).IsEqualTo(Biome.Desert);
        await Assert.That(biomes.Count(b => b == Biome.Desert)).IsEqualTo(1);
    }

    private sealed record OneColumn(int X, int Z, byte Hit, byte Miss) : BiomeField
    {
        public override byte At(int x, int z) => x == X && z == Z ? Hit : Miss;
    }

    /// <summary>A cell field takes each region's biome from its palette, so over enough ground every entry
    /// shows and nothing outside the palette does.</summary>
    [Test]
    public async Task A_cell_field_answers_only_from_its_palette_and_uses_all_of_it()
    {
        var field = new CellBiome(3, CellSize: 32, Jitter: 80, [Biome.Forest, Biome.Swampland, Biome.River]);
        var seen = new HashSet<byte>();
        for (var x = -192; x <= 192; x += 8)
            for (var z = -192; z <= 192; z += 8)
                seen.Add(field.At(x, z));

        await Assert.That(seen).IsEquivalentTo(new[] { Biome.Forest, Biome.Swampland, Biome.River });
    }

    /// <summary>A cell size is in <b>blocks</b>, and a region holds its biome over that many of them — which
    /// is what makes the number legible and what stops a field flickering block to block.</summary>
    [Test]
    public async Task A_cell_size_holds_a_biome_over_that_many_blocks()
    {
        var field = new CellBiome(11, CellSize: 48, Jitter: 40, [Biome.Forest, Biome.Desert]);
        var runs = 0;
        for (var x = -320; x < 320; x++)
            if (field.At(x, 0) == field.At(x + 1, 0)) runs++;

        // Forty-eight-block regions over a 640-block walk: neighbours almost always agree.
        await Assert.That(runs).IsGreaterThan(600);
    }

    [Test]
    public async Task A_solid_field_is_one_biome_everywhere()
    {
        var field = new SolidBiome(Biome.Mesa);
        await Assert.That(field.At(0, 0)).IsEqualTo(Biome.Mesa);
        await Assert.That(field.At(-99, 41)).IsEqualTo(Biome.Mesa);
    }

    /// <summary>A field is polymorphic on <c>kind</c>, and it is authored by hand and over the API — so what
    /// it round-trips as is part of the contract.</summary>
    [Test]
    public async Task A_field_round_trips_through_its_json()
    {
        BiomeField field = new NoiseBiome(5, Scale: 64, Octaves: 3, [Biome.Forest, Biome.Plains, Biome.Swampland]);
        var json = System.Text.Json.JsonSerializer.Serialize(field, TerrainThemeJson.Options);
        await Assert.That(json).Contains("\"kind\":\"noise\"");
        await Assert.That(TerrainThemeJson.DeserializeBiome(json)).IsEqualTo(field);
    }

    [Test]
    public async Task A_field_with_an_empty_palette_answers_plains_rather_than_throwing()
    {
        await Assert.That(new CellBiome(1, 32, 50, []).At(3, 4)).IsEqualTo(Biome.Plains);
        await Assert.That(new NoiseBiome(1, 32, 3, []).At(3, 4)).IsEqualTo(Biome.Plains);
    }

    /// <summary>The chunk's Biomes array as the region file would carry it — read through the same public
    /// path a world read-back uses, so what is asserted is what a loader would find.</summary>
    private static byte[] BiomesOf(VoxelWorld world, int cx, int cz)
        => AnvilRegion.FromWorld(world).First(c => c.ChunkX == cx && c.ChunkZ == cz)
            .Level.Get<NbtByteArray>("Biomes")!.Value;

    private static byte BiomeOf(VoxelWorld world, int cx, int cz) => BiomesOf(world, cx, cz)[0];

    /// <summary>The export's own pass lives in <c>PgmStudio.Export</c>, which the Minecraft tests do not
    /// reference — so the walk is restated here in the few lines it is, over the same public surface the real
    /// one uses and with no fold, which is the export's own concern.</summary>
    private static class BiomeScopeStandIn
    {
        public static void Paint(VoxelWorld world, BiomeField? field)
        {
            if (field is null) return;
            foreach (var (cx, cz) in world.ChunkCoords.ToList())
                for (var dz = 0; dz < 16; dz++)
                    for (var dx = 0; dx < 16; dx++)
                        world.SetBiome((cx << 4) + dx, (cz << 4) + dz, field.At((cx << 4) + dx, (cz << 4) + dz));
        }
    }
}
