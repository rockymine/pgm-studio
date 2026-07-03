using PgmStudio.Minecraft;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// Layout-structure stamps (docs/contracts/layout-rules.md ST1–ST4): a wool-room bedrock floor column, an
/// entrance redstone row with end torches, a 4×4×4 iron cube on the surface, and a bedrock approach wall to a
/// fixed top height. Each stamp reads a per-column surface top so it sits on the real terrain.
/// </summary>
public sealed class StructureStamperTests
{
    // A flat surface (first air Y) at the given height across a rectangle of columns.
    private static Dictionary<(int X, int Z), int> FlatSurface(int minX, int minZ, int maxX, int maxZ, int top)
    {
        var d = new Dictionary<(int X, int Z), int>();
        for (var x = minX; x <= maxX; x++)
        for (var z = minZ; z <= maxZ; z++)
            d[(x, z)] = top;
        return d;
    }

    [Test]
    public async Task Room_floor_fills_bedrock_from_zero_to_the_surface()
    {
        var w = new VoxelWorld();
        var surf = FlatSurface(0, 0, 10, 10, top: 13);
        StructureStamper.StampRoomFloor(w, surf, minX: 2, minZ: 2, maxX: 6, maxZ: 6);

        // Solid bedrock through the whole column [0, 13); air at and above the surface top.
        await Assert.That(w.GetBlock(2, 0, 2).Id).IsEqualTo(Blocks.Bedrock);
        await Assert.That(w.GetBlock(3, 7, 3).Id).IsEqualTo(Blocks.Bedrock);
        await Assert.That(w.GetBlock(5, 12, 5).Id).IsEqualTo(Blocks.Bedrock);   // top solid block
        await Assert.That(w.GetBlock(5, 13, 5).Id).IsEqualTo(Blocks.Air);       // surface cell left open
        // The max bound is exclusive — column x=6 is outside the footprint.
        await Assert.That(w.GetBlock(6, 0, 3).Id).IsEqualTo(Blocks.Air);
    }

    [Test]
    public async Task Redstone_line_lays_wire_between_torch_ends_on_the_surface()
    {
        var w = new VoxelWorld();
        var surf = FlatSurface(0, 0, 20, 5, top: 9);
        // A row along x from (4,3) to (9,3).
        StructureStamper.StampRedstoneLine(w, surf, x1: 4, z1: 3, x2: 9, z2: 3);

        await Assert.That(w.GetBlock(4, 9, 3).Id).IsEqualTo(Blocks.RedstoneTorch);   // end
        await Assert.That(w.GetBlock(9, 9, 3).Id).IsEqualTo(Blocks.RedstoneTorch);   // end
        await Assert.That(w.GetBlock(5, 9, 3).Id).IsEqualTo(Blocks.RedstoneWire);    // interior
        await Assert.That(w.GetBlock(8, 9, 3).Id).IsEqualTo(Blocks.RedstoneWire);
        // sits on top of the surface (y = surface top), not below it
        await Assert.That(w.GetBlock(6, 8, 3).Id).IsEqualTo(Blocks.Air);
    }

    [Test]
    public async Task Iron_cube_is_a_4x4x4_block_resting_on_the_surface_centred_on_the_anchor()
    {
        var w = new VoxelWorld();
        var surf = FlatSurface(-4, -4, 8, 8, top: 13);
        StructureStamper.StampIronCube(w, surf, anchorX: 3, anchorZ: 4);

        // Footprint centres on the anchor: [anchor-2, anchor+1] = x∈[1,4], z∈[2,5]; base at surface, 4 tall.
        var (minX, minZ, maxX, maxZ) = StructureStamper.IronCubeFootprint(3, 4);
        await Assert.That((minX, minZ, maxX, maxZ)).IsEqualTo((1, 2, 4, 5));
        await Assert.That(w.GetBlock(1, 13, 2).Id).IsEqualTo(Blocks.IronBlock);   // base corner
        await Assert.That(w.GetBlock(4, 16, 5).Id).IsEqualTo(Blocks.IronBlock);   // top far corner (13+3)
        await Assert.That(w.GetBlock(3, 14, 4).Id).IsEqualTo(Blocks.IronBlock);   // interior
        await Assert.That(w.GetBlock(3, 17, 4).Id).IsEqualTo(Blocks.Air);         // above the cube
        await Assert.That(w.GetBlock(0, 13, 2).Id).IsEqualTo(Blocks.Air);         // outside footprint
    }

    [Test]
    public async Task Wall_rises_bedrock_from_zero_to_the_top_height_inclusive()
    {
        var w = new VoxelWorld();
        // Footprint 2 thick across z, 10 wide across x; top at y=13.
        StructureStamper.StampWall(w, minX: -30, minZ: 39, maxX: -20, maxZ: 41, topY: 13);

        await Assert.That(w.GetBlock(-30, 0, 39).Id).IsEqualTo(Blocks.Bedrock);    // reaches the floor
        await Assert.That(w.GetBlock(-25, 13, 40).Id).IsEqualTo(Blocks.Bedrock);   // top course
        await Assert.That(w.GetBlock(-25, 14, 40).Id).IsEqualTo(Blocks.Air);       // nothing above the top
        // 2 thick across the seam (z 39,40); z=41 is the exclusive bound.
        await Assert.That(w.GetBlock(-25, 5, 40).Id).IsEqualTo(Blocks.Bedrock);
        await Assert.That(w.GetBlock(-25, 5, 41).Id).IsEqualTo(Blocks.Air);
        // full width across x, exclusive at maxX.
        await Assert.That(w.GetBlock(-21, 5, 39).Id).IsEqualTo(Blocks.Bedrock);
        await Assert.That(w.GetBlock(-20, 5, 39).Id).IsEqualTo(Blocks.Air);
    }
}
