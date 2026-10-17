using System.Text.Json;
using PgmStudio.Domain;
using PgmStudio.Export;
using PgmStudio.Minecraft.Houses;
using PgmStudio.Minecraft;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Sketch;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Palette;
using PgmStudio.Minecraft.Stamping;

namespace PgmStudio.Export.Tests;

/// <summary>
/// End-to-end structure stamping (docs/generator/rules.md ST1–ST4): compiles the isolated-spawn plan
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

    /// <summary>The Y an iron cube rests at: the surface across its footprint, as the stamper resolves it —
    /// probing one column of a footprint disagrees across the symmetry orbit.</summary>
    private static int IronBase(IronResolution iron, IReadOnlyDictionary<(int X, int Z), int> surface)
        => PositionSnap.SurfaceYOver(surface, iron.MinX, iron.MinZ,
            iron.MinX + iron.Size - 1, iron.MinZ + iron.Size - 1, 1);

    /// <summary>The first spawn-side renewable iron placement of the resolved intent (WX8).</summary>
    private static IronResolution SpawnIron(MapIntent resolved)
        => resolved.Spawns.SelectMany(s => WorldBuilder.SpawnRoom(s, HouseStyle.Spawn).Iron).First(i => i.Placeable);

    private static (VoxelWorld World, MapIntent Resolved, IReadOnlyDictionary<(int X, int Z), int> Surface) Build()
    {
        var plan = PlanModel.Parse(File.ReadAllText(SeedPath()))!;
        var (layout, intent) = PlanCompiler.Compile(plan);
        var layoutJson = JsonSerializer.Serialize(layout, SketchLayout.Json);
        var built = WorldBuilder.Build(layoutJson, intent);
        var surface = TerrainBuilder.Build(SketchRasterizer.RasterizeColumns(layoutJson)).SurfaceTop;
        return (built.World, built.ResolvedIntent, surface);
    }

    [Test]
    public async Task All_three_structure_kinds_land_in_the_built_world()
    {
        var (w, resolved, surface) = Build();
        var s = resolved.Structures!;
        int Surf(int x, int z) => surface.GetValueOrDefault((x, z), 1);

        // ST1 — the entrance redstone: torch at each end, wire between, on the surface.
        var line = s.RedstoneLines[0];
        await Assert.That(w.GetBlock(line.X1, Surf(line.X1, line.Z1), line.Z1).Id).IsEqualTo(Blocks.RedstoneTorch);
        await Assert.That(w.GetBlock(line.X2, Surf(line.X2, line.Z2), line.Z2).Id).IsEqualTo(Blocks.RedstoneTorch);
        var midX = (line.X1 + line.X2) / 2;
        var midZ = (line.Z1 + line.Z2) / 2;
        await Assert.That(w.GetBlock(midX, Surf(midX, midZ), midZ).Id).IsEqualTo(Blocks.RedstoneWire);

        // ST2/ST3 — the spawn's renewable iron cube (WX8) rests beside the room on the surface its
        // footprint spans, clear of the shell.
        var iron = SpawnIron(resolved);
        int ibase = IronBase(iron, surface);
        await Assert.That(w.GetBlock(iron.MinX, ibase, iron.MinZ).Id).IsEqualTo(Blocks.IronBlock);
        await Assert.That(w.GetBlock(iron.MinX + iron.Size - 1, ibase + iron.Size - 1, iron.MinZ + iron.Size - 1).Id)
            .IsEqualTo(Blocks.IronBlock);

        // ST4 — the approach wall rises to its top bedrock course over the highest ground it crosses (not the
        // plan tier's TopY), with nothing above it, and its chest stands on the ground beside it.
        var wall = s.Walls[0];
        var cells = wall.Cells().ToList();
        var (wx, wz) = cells[0];
        var top = StructureStamper.WallTop(surface, cells, wall.TopY);
        var highestGround = cells.Select(cell => Surf(cell.X, cell.Z)).Max();
        await Assert.That(top).IsEqualTo(highestGround + RoomFrames.WallCourses - 1);
        await Assert.That(w.GetBlock(wx, 0, wz).Id).IsEqualTo(Blocks.Bedrock);
        await Assert.That(w.GetBlock(wx, top, wz).Id).IsEqualTo(Blocks.Bedrock);
        await Assert.That(w.GetBlock(wx, top + 1, wz).Id).IsEqualTo(Blocks.Air);
        var chest = s.Chests[0];
        await Assert.That(cells).DoesNotContain((chest.X, chest.Z));
        await Assert.That(w.GetBlock(chest.X, Surf(chest.X, chest.Z), chest.Z).Id).IsEqualTo(Blocks.Chest);
    }

    [Test]
    public async Task Renewable_iron_cube_survives_the_anvil_round_trip()
    {
        var (world, resolved, surface) = Build();
        var iron = SpawnIron(resolved);
        var (ix, iz) = (iron.MinX, iron.MinZ);
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
