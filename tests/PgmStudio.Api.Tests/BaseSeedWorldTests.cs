using System.Text.Json;
using PgmStudio.Api.Services;
using PgmStudio.Minecraft;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Api.Tests;

/// <summary>
/// Drives the committed base 2-island seed (<c>tools/seeds/base-2island.*.json</c>) through the export
/// pipeline — rasterize → <see cref="SketchWorldBuilder"/> → world — and asserts the world invariants the
/// live end-to-end run checks: islands are bedrock-floored, spawn/wool cubes rest on the terrain surface
/// (their 2×2 markers sit at the authored anchor), and the observer platform floats at the authored Y.
/// Expectations are derived from the seed's own intent, so the test survives geometry edits to the seed.
/// </summary>
public sealed class BaseSeedWorldTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private static (string Layout, MapIntent Intent) LoadSeed(string name)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "tools", "seeds", $"{name}.layout.json")))
            dir = dir.Parent;
        if (dir is null) throw new FileNotFoundException($"{name} seed files not found above " + AppContext.BaseDirectory);
        var seeds = Path.Combine(dir.FullName, "tools", "seeds");
        var layout = File.ReadAllText(Path.Combine(seeds, $"{name}.layout.json"));
        var intent = JsonSerializer.Deserialize<MapIntent>(File.ReadAllText(Path.Combine(seeds, $"{name}.intent.json")), Web)!;
        return (layout, intent);
    }

    private static int Snap(double v) => (int)Math.Round(v, MidpointRounding.AwayFromZero);

    [Test]
    [Arguments("base-2island")]
    [Arguments("base-4team")]
    public async Task Seed_builds_a_world_with_the_expected_structure_invariants(string seed)
    {
        var (layout, intent) = LoadSeed(seed);
        var built = SketchWorldBuilder.Build(layout, intent);
        var w = built.World;

        // Surface top per column, recomputed the same way the builder does.
        var surface = SketchTerrainBuilder.Build(SketchRasterizer.RasterizeColumns(layout)).SurfaceTop;
        int Surf(int x, int z) => surface.GetValueOrDefault((x, z), 1);

        // Each spawn: on a real island (surface > the bedrock fallback), bedrock floor at y0, terrain solid
        // directly under the cube floor, and the 2×2 wool marker at the anchor on the surface.
        foreach (var s in intent.Spawns)
        {
            int ax = Snap(s.Point.X), az = Snap(s.Point.Z), fy = Surf(ax, az);
            await Assert.That(fy).IsGreaterThan(1);
            await Assert.That(w.GetBlock(ax, 0, az).Id).IsEqualTo(Blocks.Bedrock);
            await Assert.That(w.GetBlock(ax, fy - 1, az).Id).IsNotEqualTo(Blocks.Air);   // cube rests on terrain
            await Assert.That(w.GetBlock(ax, fy, az).Id).IsEqualTo(Blocks.Wool);         // 2×2 marker
        }

        // Each wool cage: same — marker at the anchor on the surface, resting on solid terrain.
        foreach (var wl in intent.Wools!)
        {
            int ax = Snap(wl.Spawn.X), az = Snap(wl.Spawn.Z), fy = Surf(ax, az);
            await Assert.That(w.GetBlock(ax, fy, az).Id).IsEqualTo(Blocks.Wool);
            await Assert.That(w.GetBlock(ax, fy - 1, az).Id).IsNotEqualTo(Blocks.Air);
        }

        // Observer platform: bedrock at the authored (floating) Y, air directly below (it floats).
        var ob = intent.Observer!;
        int ox = Snap(ob.Point.X), oz = Snap(ob.Point.Z), pf = Math.Max(1, Snap(ob.Point.Y));
        await Assert.That(w.GetBlock(ox, pf, oz).Id).IsEqualTo(Blocks.Bedrock);
        await Assert.That(w.GetBlock(ox, pf - 1, oz).Id).IsEqualTo(Blocks.Air);

        // Resolved intent: spawns snapped to integers, monuments auto-wired (every wool gets capturers).
        await Assert.That(built.ResolvedIntent.Spawns.All(s => s.Point.X == Math.Round(s.Point.X))).IsTrue();
        await Assert.That(built.ResolvedIntent.Wools!.All(x => x.Monuments.Count > 0)).IsTrue();

        // Auto-derived regions encase the 8×8 cubes: protection / room cover the anchor ± 4 footprint.
        foreach (var s in built.ResolvedIntent.Spawns)
        {
            int ax = Snap(s.Point.X), az = Snap(s.Point.Z);
            var r = s.Protection.Single();
            await Assert.That(r.MinX <= ax - 4 && r.MaxX >= ax + 4 && r.MinZ <= az - 4 && r.MaxZ >= az + 4).IsTrue();
        }
        foreach (var wl in built.ResolvedIntent.Wools!)
        {
            int wx = Snap(wl.Spawn.X), wz = Snap(wl.Spawn.Z);
            var r = wl.Room.Single();
            await Assert.That(r.MinX <= wx - 4 && r.MaxX >= wx + 4 && r.MinZ <= wz - 4 && r.MaxZ >= wz + 4).IsTrue();
        }
    }

    [Test]
    [Arguments("base-2island")]
    [Arguments("base-4team")]
    public async Task Seed_world_round_trips_through_anvil(string seed)
    {
        var (layout, intent) = LoadSeed(seed);
        var built = SketchWorldBuilder.Build(layout, intent);

        var dir = Path.Combine(Path.GetTempPath(), "seedworld_" + Guid.NewGuid().ToString("N"));
        try
        {
            AnvilRegionWriter.Write(built.World, dir);

            var read = new Dictionary<(int, int, int), (int Id, int Data)>();
            foreach (var mca in Directory.GetFiles(dir, "*.mca"))
                foreach (var ch in AnvilRegion.ReadChunks(mca))
                    foreach (var b in AnvilRegion.Blocks(ch))
                        read[(b.X, b.Y, b.Z)] = (b.Id, b.Data);

            (int Id, int Data) At(int x, int y, int z) => read.GetValueOrDefault((x, y, z), (0, 0));

            var surface = SketchTerrainBuilder.Build(SketchRasterizer.RasterizeColumns(layout)).SurfaceTop;

            // A spawn marker and the observer floor must survive the region-file serialisation.
            var s = intent.Spawns[0];
            int ax = Snap(s.Point.X), az = Snap(s.Point.Z), fy = surface.GetValueOrDefault((ax, az), 1);
            await Assert.That(At(ax, fy, az).Id).IsEqualTo(Blocks.Wool);
            await Assert.That(At(ax, 0, az).Id).IsEqualTo(Blocks.Bedrock);

            var ob = intent.Observer!;
            await Assert.That(At(Snap(ob.Point.X), Math.Max(1, Snap(ob.Point.Y)), Snap(ob.Point.Z)).Id).IsEqualTo(Blocks.Bedrock);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
