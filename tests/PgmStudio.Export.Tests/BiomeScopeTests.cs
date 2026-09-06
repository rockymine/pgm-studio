using fNbt;
using PgmStudio.Export;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Dressing;
using PgmStudio.Minecraft.Painting;
using PgmStudio.Minecraft.Palette;

namespace PgmStudio.Export.Tests;

/// <summary>
/// The biome pass over a whole board: what a column answers when the map states a field, when a patch is
/// drawn over it, and when both. The claims are the resolution order and the fold — a patch is answered before
/// the map, the last-drawn patch wins where two overlap, and a column and its image answer alike without a
/// patch ever being fanned.
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

    private static BiomePatch Square(string id, int minX, int minZ, int maxX, int maxZ, byte biome) => new()
    {
        Id = id,
        Points = [[minX, minZ], [maxX, minZ], [maxX, maxZ], [minX, maxZ]],
        Field = new SolidBiome(biome),
    };

    /// <summary>The chunk's Biomes array as a region file would carry it, so what is asserted is what a
    /// loader would find.</summary>
    private static byte BiomeAt(VoxelWorld world, int x, int z)
        => AnvilRegion.FromWorld(world).First(c => c.ChunkX == (x >> 4) && c.ChunkZ == (z >> 4))
            .Level.Get<NbtByteArray>("Biomes")!.Value[((z & 15) << 4) | (x & 15)];

    [Test]
    public async Task A_board_stating_neither_a_field_nor_a_patch_stays_plains()
    {
        var world = Board();
        BiomeScope.Paint(world, field: null, patches: []);

        await Assert.That(BiomeAt(world, 0, 0)).IsEqualTo(Biome.Plains);
    }

    /// <summary>A patch answers where the map's own field would have. Without it the whole board is the map's,
    /// which is what the first assertion pins.</summary>
    [Test]
    public async Task A_patch_answers_over_the_maps_field()
    {
        var world = Board();
        BiomeScope.Paint(world, new SolidBiome(Biome.Forest),
                         [Square("a", 0, 0, 16, 16, Biome.Desert)]);

        await Assert.That(BiomeAt(world, -8, -8)).IsEqualTo(Biome.Forest);
        await Assert.That(BiomeAt(world, 8, 8)).IsEqualTo(Biome.Desert);
    }

    /// <summary>A board with patches and no map field leaves everything outside them as the plains the chunk
    /// was created with — the patches are laid on the default rather than replacing it.</summary>
    [Test]
    public async Task A_patch_with_no_map_field_leaves_the_rest_plains()
    {
        var world = Board();
        BiomeScope.Paint(world, field: null, [Square("a", 0, 0, 16, 16, Biome.Jungle)]);

        await Assert.That(BiomeAt(world, 8, 8)).IsEqualTo(Biome.Jungle);
        await Assert.That(BiomeAt(world, -8, -8)).IsEqualTo(Biome.Plains);
    }

    /// <summary>Paint laid later covers paint laid earlier, which is what makes the drawing order worth
    /// keeping.</summary>
    [Test]
    public async Task The_last_patch_drawn_over_a_column_is_the_one_it_answers()
    {
        var world = Board();
        BiomeScope.Paint(world, field: null,
        [
            Square("under", 0, 0, 16, 16, Biome.Jungle),
            Square("over", 4, 4, 12, 12, Biome.Mesa),
        ]);

        await Assert.That(BiomeAt(world, 8, 8)).IsEqualTo(Biome.Mesa);
        await Assert.That(BiomeAt(world, 2, 2)).IsEqualTo(Biome.Jungle);
    }

    /// <summary>The fold is applied before the patches are asked, so an area drawn on the primary half already
    /// answers at its image and is never fanned. Asserted against a half-turn, where the image of (8, 8) is
    /// (−8, −8).</summary>
    [Test]
    public async Task A_patch_drawn_on_one_half_answers_on_both()
    {
        var world = Board();
        var symmetry = new DressingSymmetry("rot_180", 0, 0);
        BiomeScope.Paint(world, field: null, [Square("a", 0, 0, 16, 16, Biome.Desert)], symmetry.Canonical);

        await Assert.That(BiomeAt(world, 8, 8)).IsEqualTo(BiomeAt(world, -8, -8));
    }

    /// <summary>An outline of fewer than three points encloses no column, so it holds nothing rather than
    /// holding everything.</summary>
    [Test]
    public async Task An_outline_of_two_points_holds_nothing()
    {
        var world = Board();
        BiomeScope.Paint(world, field: null,
            [new BiomePatch { Id = "thin", Points = [[0, 0], [16, 16]], Field = new SolidBiome(Biome.Desert) }]);

        await Assert.That(BiomeAt(world, 8, 8)).IsEqualTo(Biome.Plains);
    }

    /// <summary>The patches come off the layout's dressing document, which is where they are stored.</summary>
    [Test]
    public async Task The_patches_are_read_off_the_layouts_dressing()
    {
        const string layout = """
        {"layers":[{"base_y":0,"layout":{"shapes":[]}}],
         "dressing":{"props":[],"biomes":[
           {"id":"biome-1","points":[[0,0],[8,0],[8,8],[0,8]],"field":{"kind":"solid","id":2}}]}}
        """;

        var patches = BiomeScope.PatchesOf(layout);

        await Assert.That(patches.Count).IsEqualTo(1);
        await Assert.That(patches[0].Id).IsEqualTo("biome-1");
        await Assert.That(patches[0].Field.At(4, 4)).IsEqualTo(Biome.Desert);
    }
}
