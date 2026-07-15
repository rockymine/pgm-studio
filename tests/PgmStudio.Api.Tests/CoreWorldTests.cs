using System.Text.Json;
using PgmStudio.Api.Services;
using PgmStudio.Domain;
using PgmStudio.Minecraft;
using PgmStudio.Pgm;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Api.Tests;

using Dict = Dictionary<string, object?>;

/// <summary>
/// End-to-end DTC: a plan carrying a core marker → world + map.xml. The claims worth proving are that the
/// casing actually encloses lava (a core that leaks on its own, or never leaks, is not a goal) and that the
/// emitted region contains both — the same OB8 check the destroyable path makes.
/// </summary>
public sealed class CoreWorldTests
{
    private const string Json = """
        {
          "plan": 1,
          "meta": { "name": "DTC Probe" },
          "globals": { "cell": 5, "symmetry": "rot_180", "surface": 9, "headroom": 11 },
          "pieces": [
            { "id": "bar-w", "role": "piece", "rect": [1, -2, 4, 4], "surface": 12 }
          ],
          "placements": {
            "cores": [ { "piece": "bar-w", "at": [2, 2] } ]
          }
        }
        """;

    private const string Marker = """{ "piece": "bar-w", "at": [2, 2] }""";

    private static (VoxelWorld World, MapIntent Resolved) Build(string json)
    {
        var plan = PlanModel.Parse(json)!;
        var (layout, intent) = PlanCompiler.Compile(plan);
        var built = SketchWorldBuilder.Build(JsonSerializer.Serialize(layout, SketchLayout.Json), intent);
        return (built.World, built.ResolvedIntent);
    }

    [Test]
    public async Task The_default_casing_is_a_5x5x5_obsidian_shell_around_3x3x3_lava()
    {
        var (world, resolved) = Build(Json);
        await Assert.That(resolved.Cores!.Count).IsEqualTo(2);
        var box = resolved.Cores![0].Box!.Value;
        await Assert.That((box.Width, box.Height, box.Depth)).IsEqualTo((5, 5, 5));

        int obsidian = 0, lava = 0;
        for (var x = box.MinX; x <= box.MaxX; x++)
        for (var y = box.MinY; y <= box.MaxY; y++)
        for (var z = box.MinZ; z <= box.MaxZ; z++)
        {
            var id = world.GetBlock(x, y, z).Id;
            if (id == Blocks.Obsidian) obsidian++;
            else if (id is Blocks.Lava or Blocks.StationaryLava) lava++;
        }
        // 125 cells: a 3×3×3 lava interior fully wrapped by 98 obsidian — DC1's modal core.
        await Assert.That(lava).IsEqualTo(27);
        await Assert.That(obsidian).IsEqualTo(125 - 27);
    }

    [Test]
    public async Task The_lava_is_fully_enclosed_and_the_cap_is_on_by_default()
    {
        // 65% of corpus cores cap the lava; open-top is a real but minority style, hence a flag.
        var (world, resolved) = Build(Json);
        var box = resolved.Cores![0].Box!.Value;
        var midX = (box.MinX + box.MaxX) / 2;
        var midZ = (box.MinZ + box.MaxZ) / 2;
        await Assert.That(world.GetBlock(midX, box.MaxY, midZ).Id).IsEqualTo(Blocks.Obsidian);
        await Assert.That(world.GetBlock(midX, box.MaxY - 1, midZ).Id).IsEqualTo(Blocks.StationaryLava);
        // and the floor, or the lava would drain the moment the map loaded
        await Assert.That(world.GetBlock(midX, box.MinY, midZ).Id).IsEqualTo(Blocks.Obsidian);
    }

    [Test]
    public async Task Open_top_lifts_the_lava_to_the_rim()
    {
        var (world, resolved) = Build(Json.Replace(Marker,
            """{ "piece": "bar-w", "at": [2, 2], "openTop": true }"""));
        var box = resolved.Cores![0].Box!.Value;
        var midX = (box.MinX + box.MaxX) / 2;
        var midZ = (box.MinZ + box.MaxZ) / 2;
        await Assert.That(world.GetBlock(midX, box.MaxY, midZ).Id).IsEqualTo(Blocks.StationaryLava);
    }

    [Test]
    public async Task The_casing_floats_clear_of_the_terrain_so_leaked_lava_can_fall()
    {
        var (world, resolved) = Build(Json);
        var box = resolved.Cores![0].Box!.Value;
        await Assert.That(world.GetBlock(box.MinX, box.MinY - 1, box.MinZ).Id).IsEqualTo(Blocks.Air);
    }

    [Test]
    public async Task Every_cores_region_contains_its_casing_and_its_lava()
    {
        var (world, resolved) = Build(Json);
        var doc = new Dict();
        IntentGenerator.Apply(doc, resolved);
        var regions = (Dict)doc["regions"]!;
        var emitted = (List<object?>)doc["cores"]!;
        await Assert.That(emitted.Count).IsEqualTo(2);

        foreach (var entry in emitted.Cast<Dict>())
        {
            var region = (Dict)regions[(string)entry["region"]!]!;
            var min = (Dict)region["min"]!;
            var max = (Dict)region["max"]!;

            int obsidian = 0, lava = 0;
            for (var x = (int)(double)min["x"]!; x < (int)(double)max["x"]!; x++)
            for (var y = (int)(double)min["y"]!; y < (int)(double)max["y"]!; y++)
            for (var z = (int)(double)min["z"]!; z < (int)(double)max["z"]!; z++)
            {
                var id = world.GetBlock(x, y, z).Id;
                if (id == Blocks.Obsidian) obsidian++;
                else if (id is Blocks.Lava or Blocks.StationaryLava) lava++;
            }
            // Walked with PGM's own [min, max) semantics: the whole core, nothing clipped.
            await Assert.That(obsidian).IsEqualTo(98);
            await Assert.That(lava).IsEqualTo(27);
        }
    }

    [Test]
    public async Task The_exported_xml_carries_the_cores_and_reads_back_as_DTC()
    {
        var (_, resolved) = Build(Json);
        var doc = new Dict();
        IntentGenerator.Apply(doc, resolved);

        var xml = XmlWriter.ToXml(Deserializer.FromDict(Serializer.ToDict(Deserializer.FromDict(doc))));
        await Assert.That(xml).Contains("<cores>");

        var reparsed = MapParser.ParseXmlString(xml);
        await Assert.That(reparsed.Cores.Count).IsEqualTo(2);
        await Assert.That(reparsed.Cores.Select(c => c.Owner)).IsEquivalentTo(new[] { "red", "blue" });
        // Nothing declares a <gamemode>; the map reads as DTC off its modules alone.
        await Assert.That(reparsed.Gamemodes).IsEquivalentTo(new[] { Gamemodes.Dtc });
    }
}
