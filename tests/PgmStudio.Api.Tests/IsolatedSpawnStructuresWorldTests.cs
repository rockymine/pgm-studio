using System.Text.Json;
using PgmStudio.Api.Services;
using PgmStudio.Minecraft;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Api.Tests;

/// <summary>
/// End-to-end structure stamping (docs/contracts/layout-rules.md ST1–ST4): compiles the isolated-spawn plan
/// seed → world and reads block ids back to confirm each directive landed — a wool-room bedrock column, the
/// entrance redstone row + torches, the renewable iron cube, and the approach wall rising to approach+4. The
/// compiled <see cref="StructureIntent"/> supplies the coordinates, so the assertions track the seed geometry.
/// </summary>
public sealed class IsolatedSpawnStructuresWorldTests
{
    private static string SeedPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "tools", "seeds", "isolated-spawn.plan.json")))
            dir = dir.Parent;
        if (dir is null) throw new FileNotFoundException("isolated-spawn.plan.json not found above " + AppContext.BaseDirectory);
        return Path.Combine(dir.FullName, "tools", "seeds", "isolated-spawn.plan.json");
    }

    /// <summary>The Y an iron cube rests at: the surface across its footprint, as the stamper resolves it. Not
    /// the anchor column's surface — the anchor is a grid line, and probing one side of it disagrees across the
    /// symmetry orbit (the seed's own marker sits at a terrain edge, where it read the y=1 fallback).</summary>
    private static int IronBase(IronCube iron, IReadOnlyDictionary<(int X, int Z), int> surface)
    {
        var (minX, minZ, maxX, maxZ) = StructureStamper.IronCubeFootprint(iron.X, iron.Z);
        return PositionSnap.SurfaceYOver(surface, minX, minZ, maxX, maxZ, 1);
    }

    private static (VoxelWorld World, StructureIntent Structures, IReadOnlyDictionary<(int X, int Z), int> Surface) Build()
    {
        var plan = PlanModel.Parse(File.ReadAllText(SeedPath()))!;
        var (layout, intent) = PlanCompiler.Compile(plan);
        var layoutJson = JsonSerializer.Serialize(layout, SketchLayout.Json);
        var built = SketchWorldBuilder.Build(layoutJson, intent);
        var surface = SketchTerrainBuilder.Build(SketchRasterizer.RasterizeColumns(layoutJson)).SurfaceTop;
        return (built.World, built.ResolvedIntent.Structures!, surface);
    }

    [Test]
    public async Task All_four_structure_kinds_land_in_the_built_world()
    {
        var (w, s, surface) = Build();
        int Surf(int x, int z) => surface.GetValueOrDefault((x, z), 1);

        // ST1 — a wool-room footprint is solid bedrock from the floor up through the surface block.
        var floor = s.RoomFloors[0];
        int fx = (int)floor.MinX, fz = (int)floor.MinZ;
        await Assert.That(w.GetBlock(fx, 0, fz).Id).IsEqualTo(Blocks.Bedrock);
        await Assert.That(w.GetBlock(fx, Surf(fx, fz) - 1, fz).Id).IsEqualTo(Blocks.Bedrock);

        // ST1 — the entrance redstone: torch at each end, wire between, on the surface.
        var line = s.RedstoneLines[0];
        await Assert.That(w.GetBlock(line.X1, Surf(line.X1, line.Z1), line.Z1).Id).IsEqualTo(Blocks.RedstoneTorch);
        await Assert.That(w.GetBlock(line.X2, Surf(line.X2, line.Z2), line.Z2).Id).IsEqualTo(Blocks.RedstoneTorch);
        var midX = (line.X1 + line.X2) / 2;
        var midZ = (line.Z1 + line.Z2) / 2;
        await Assert.That(w.GetBlock(midX, Surf(midX, midZ), midZ).Id).IsEqualTo(Blocks.RedstoneWire);

        // ST2/ST3 — the renewable iron cube rests on the surface its footprint spans.
        var iron = s.IronCubes.First(c => c.Renew);
        var (ix, iz, _, _) = StructureStamper.IronCubeFootprint(iron.X, iron.Z);
        int ibase = IronBase(iron, surface);
        await Assert.That(w.GetBlock(ix, ibase, iz).Id).IsEqualTo(Blocks.IronBlock);
        await Assert.That(w.GetBlock(iron.X, ibase + 3, iron.Z).Id).IsEqualTo(Blocks.IronBlock);

        // ST4 — the approach wall rises to its top height, nothing above it.
        var wall = s.Walls[0];
        int wx = wall.MinX, wz = wall.MinZ;
        await Assert.That(w.GetBlock(wx, 0, wz).Id).IsEqualTo(Blocks.Bedrock);
        await Assert.That(w.GetBlock(wx, wall.TopY, wz).Id).IsEqualTo(Blocks.Bedrock);
        await Assert.That(w.GetBlock(wx, wall.TopY + 1, wz).Id).IsEqualTo(Blocks.Air);
    }

    [Test]
    public async Task Renewable_iron_cube_survives_the_anvil_round_trip()
    {
        var (world, s, surface) = Build();
        var iron = s.IronCubes.First(c => c.Renew);
        var (ix, iz, _, _) = StructureStamper.IronCubeFootprint(iron.X, iron.Z);
        int ibase = IronBase(iron, surface);

        var dir = Path.Combine(Path.GetTempPath(), "isostruct_" + Guid.NewGuid().ToString("N"));
        try
        {
            AnvilRegionWriter.Write(world, dir);
            var read = new Dictionary<(int, int, int), (int Id, int Data)>();
            foreach (var mca in Directory.GetFiles(dir, "*.mca"))
                foreach (var ch in AnvilRegion.ReadChunks(mca))
                    foreach (var b in AnvilRegion.Blocks(ch))
                        read[(b.X, b.Y, b.Z)] = (b.Id, b.Data);

            read.TryGetValue((ix, ibase, iz), out var block);
            await Assert.That(block.Id).IsEqualTo(Blocks.IronBlock);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
