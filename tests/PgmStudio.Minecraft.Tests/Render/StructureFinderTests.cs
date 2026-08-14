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

    // A themed map paints its ground the same material it builds with: a flat 6x6 planked plaza (top y6)
    // with a 2x2 planked pillar (top y9) standing on it. Same material throughout — the one thing that
    // separated them before the elevation-step flood was nothing.
    private static VoxelWorld PlazaWithPillar()
    {
        var world = new VoxelWorld();
        for (var x = 0; x < 6; x++)
            for (var z = 0; z < 6; z++)
            {
                world.SetBlock(x, 5, z, Blocks.Stone);   // natural ground under the paint
                world.SetBlock(x, 6, z, 5);              // the painted plaza floor, planks
            }
        for (var x = 2; x < 4; x++)
            for (var z = 2; z < 4; z++)
                for (var y = 7; y <= 9; y++)
                    world.SetBlock(x, y, z, 5);          // the pillar, same material, standing over the plaza

        return world;
    }

    [Test]
    public async Task A_step_within_tolerance_still_merges_the_plaza_and_the_pillar_standing_on_it()
    {
        // With no step limit (a large maximumStep), material alone still decides — this is the old
        // behaviour, reproduced here to prove the fix below actually changes something: without it,
        // the flat plaza and the pillar on it read as one 40-cell blob that is neither.
        var result = StructureFinder.Render(AnvilRegion.FromWorld(PlazaWithPillar()), minimumArea: 4, maximumStep: 1000);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Structures.Count).IsEqualTo(1);
        await Assert.That(result.Structures[0].Area).IsEqualTo(36);
    }

    [Test]
    public async Task A_step_over_tolerance_separates_the_pillar_from_the_plaza_it_shares_a_material_with()
    {
        // The pillar's roof (y9) stands 3 above the plaza floor it grows from (y6); a maximumStep of 2
        // is too little to bridge that, so the flood reports the pillar and the surrounding plaza ring
        // as two structures despite neither having a material to tell them apart.
        var result = StructureFinder.Render(AnvilRegion.FromWorld(PlazaWithPillar()), minimumArea: 4, maximumStep: 2);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Structures.Count).IsEqualTo(2);
        var pillar = result.Structures.Single(structure => structure.Area == 4);
        var plaza = result.Structures.Single(structure => structure.Area == 32);
        await Assert.That(pillar.RoofHigh).IsEqualTo(9);
        await Assert.That(plaza.RoofHigh).IsEqualTo(6);
    }

    [Test]
    public async Task Run_appends_a_scale_legend_that_grows_the_written_png()
    {
        var outPng = Path.Combine(Path.GetTempPath(), $"structures-legend-{Guid.NewGuid():N}.png");
        try
        {
            var exit = StructureFinder.Run(PlazaWithPillar(), outPng, scale: 2, minimumArea: 4);
            await Assert.That(exit).IsEqualTo(0);

            var (width, height) = PngTestUtil.Dimensions(File.ReadAllBytes(outPng));
            await Assert.That(width).IsEqualTo(12);       // 6 columns wide at scale 2
            await Assert.That(height).IsGreaterThan(12);  // 6 rows at scale 2, plus the legend strip
        }
        finally { File.Delete(outPng); }
    }
}
