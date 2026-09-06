using fNbt;
using PgmStudio.Export;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Dressing;
using PgmStudio.Minecraft.Painting;
using PgmStudio.Minecraft.Palette;

namespace PgmStudio.Export.Tests;

/// <summary>
/// The biome pass over a whole board: what a column answers, and that a mirrored board answers the same at a
/// cell and at its image. The field is map-wide, so what is worth asserting is the fold — the one thing the
/// pass adds over reading the field.
/// </summary>
public sealed class BiomeScopeTests
{
    private static VoxelWorld Board()
    {
        var world = new VoxelWorld();
        for (var x = -20; x <= 20; x += 4)
            for (var z = -20; z <= 20; z += 4)
                world.SetBlock(x, 64, z, Blocks.Stone);
        return world;
    }

    /// <summary>The chunk's Biomes array as a region file would carry it, so what is asserted is what a
    /// loader would find.</summary>
    private static byte BiomeAt(VoxelWorld world, int x, int z)
        => AnvilRegion.FromWorld(world).First(c => c.ChunkX == (x >> 4) && c.ChunkZ == (z >> 4))
            .Level.Get<NbtByteArray>("Biomes")!.Value[((z & 15) << 4) | (x & 15)];

    [Test]
    public async Task A_board_stating_no_field_stays_plains()
    {
        var world = Board();
        BiomeScope.Paint(world, field: null);

        await Assert.That(BiomeAt(world, 0, 0)).IsEqualTo(Biome.Plains);
    }

    [Test]
    public async Task Every_column_answers_the_stated_field()
    {
        var world = Board();
        BiomeScope.Paint(world, new SolidBiome(Biome.Mesa));

        await Assert.That(BiomeAt(world, 8, 8)).IsEqualTo(Biome.Mesa);
        await Assert.That(BiomeAt(world, -8, -8)).IsEqualTo(Biome.Mesa);
    }

    /// <summary>The fold is applied before the field is asked, so a mirrored board answers one biome at a cell
    /// and at its image rather than putting a desert against a forest across the axis. Asserted over a field
    /// that genuinely varies — a solid one would pass without a fold at all.</summary>
    [Test]
    public async Task A_mirrored_board_answers_the_same_biome_at_a_cell_and_at_its_image()
    {
        var world = Board();
        var symmetry = new DressingSymmetry("rot_180", 0, 0);
        BiomeScope.Paint(world, new CellBiome(7, CellSize: 8, Jitter: 100, Palette: [Biome.Desert, Biome.Taiga]),
                         symmetry.Canonical);

        for (var at = 4; at <= 20; at += 4)
            await Assert.That(BiomeAt(world, at, at)).IsEqualTo(BiomeAt(world, -at, -at));
    }

    /// <summary>The field comes off the layout's own key, which is where a map states it.</summary>
    [Test]
    public async Task The_field_is_read_off_the_layout()
    {
        const string layout = """
        {"layers":[{"base_y":0,"layout":{"shapes":[]}}],
         "biome":{"kind":"solid","id":2}}
        """;

        await Assert.That(BiomeScope.FieldOf(layout)?.At(4, 4)).IsEqualTo(Biome.Desert);
        await Assert.That(BiomeScope.FieldOf("""{"layers":[]}""")).IsNull();
    }
}
