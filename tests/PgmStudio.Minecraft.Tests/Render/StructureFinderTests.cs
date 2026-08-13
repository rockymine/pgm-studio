using PgmStudio.Minecraft.Render;

namespace PgmStudio.Minecraft.Tests.Render;

/// <summary>The structures stage image: built columns found by material, independent of any theme.</summary>
public sealed class StructureFinderTests
{
    [Test]
    public async Task Render_finds_one_component_over_its_true_footprint()
    {
        var world = new VoxelWorld();
        // A natural-ground floor everywhere, then a 3x2 planked patch that stands over it.
        for (var x = 0; x < 6; x++)
            for (var z = 0; z < 6; z++)
                world.SetBlock(x, 5, z, Blocks.Stone);
        for (var x = 2; x < 5; x++)
            for (var z = 2; z < 4; z++)
                world.SetBlock(x, 6, z, 5);   // planks

        var result = StructureFinder.Render(AnvilRegion.FromWorld(world), minimumArea: 4);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Structures.Count).IsEqualTo(1);
        var structure = result.Structures[0];
        await Assert.That(structure.Area).IsEqualTo(6);
        await Assert.That((structure.MinX, structure.MaxX, structure.MinZ, structure.MaxZ)).IsEqualTo((2, 4, 2, 3));
    }

    [Test]
    public async Task Render_drops_a_patch_smaller_than_the_minimum_area()
    {
        var world = new VoxelWorld();
        world.SetBlock(0, 5, 0, Blocks.Stone);
        world.SetBlock(0, 6, 0, 5);   // a lone planked column: area 1

        var result = StructureFinder.Render(AnvilRegion.FromWorld(world), minimumArea: 4);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Structures.Count).IsEqualTo(0);
    }
}
