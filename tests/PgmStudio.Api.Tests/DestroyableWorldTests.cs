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
/// End-to-end DTM: a plan carrying a destroyable marker → world + map.xml. The claim worth proving is OB8 —
/// that the region the XML emits actually contains the blocks the world stamped. PGM builds a destroyable's
/// goal from the blocks matching <c>materials</c> <i>inside</i> its region, and a region that misses them
/// yields a zero-health goal it accepts with nothing louder than a log warning. So the check here is the one
/// the export gate makes: at least one matching block inside the region.
/// </summary>
public sealed class DestroyableWorldTests
{
    // rot_180 → two teams, each getting one destroyable from the single authored marker. A generous piece
    // keeps the structure clear of the terrain edge.
    private const string Json = """
        {
          "plan": 1,
          "meta": { "name": "DTM Probe" },
          "globals": { "cell": 5, "symmetry": "rot_180", "surface": 9, "headroom": 11 },
          "pieces": [
            { "id": "bar-w", "role": "piece", "rect": [1, -2, 4, 4], "surface": 12 }
          ],
          "placements": {
            "destroyables": [ { "piece": "bar-w", "at": [2, 2] } ]
          }
        }
        """;

    private static (VoxelWorld World, MapIntent Resolved) Build(string json)
    {
        var plan = PlanModel.Parse(json)!;
        var (layout, intent) = PlanCompiler.Compile(plan);
        var built = SketchWorldBuilder.Build(JsonSerializer.Serialize(layout, SketchLayout.Json), intent);
        return (built.World, built.ResolvedIntent);
    }

    [Test]
    public async Task Every_destroyables_region_contains_its_stamped_blocks()
    {
        var (world, resolved) = Build(Json);
        await Assert.That(resolved.Destroyables!.Count).IsEqualTo(2);

        var doc = new Dict();
        IntentGenerator.Apply(doc, resolved);
        var regions = (Dict)doc["regions"]!;
        var emitted = (List<object?>)doc["destroyables"]!;
        await Assert.That(emitted.Count).IsEqualTo(2);

        foreach (var entry in emitted.Cast<Dict>())
        {
            var region = (Dict)regions[(string)entry["region"]!]!;
            var min = (Dict)region["min"]!;
            var max = (Dict)region["max"]!;

            // Walk the region exactly as PGM does — [min, max) on every axis — and count obsidian.
            var matching = 0;
            for (var x = (int)(double)min["x"]!; x < (int)(double)max["x"]!; x++)
            for (var y = (int)(double)min["y"]!; y < (int)(double)max["y"]!; y++)
            for (var z = (int)(double)min["z"]!; z < (int)(double)max["z"]!; z++)
                if (world.GetBlock(x, y, z).Id == Blocks.Obsidian) matching++;

            // The default pillar-3 is exactly 3 blocks, and all 3 must be inside — anything less means the
            // region and the stamp disagree, which is the silent failure OB8 exists to rule out.
            await Assert.That(matching).IsEqualTo(3);
        }
    }

    [Test]
    public async Task The_structure_floats_clear_of_the_terrain()
    {
        // The gap is the point: a destroyable sits above the surface so breaking it means committing to the
        // climb. If the box ever rested on the ground this would find the terrain block instead of air.
        var (world, resolved) = Build(Json);
        var box = resolved.Destroyables![0].Box!.Value;
        await Assert.That(world.GetBlock(box.MinX, box.MinY - 1, box.MinZ).Id).IsEqualTo(Blocks.Air);
        await Assert.That(world.GetBlock(box.MinX, box.MinY, box.MinZ).Id).IsEqualTo(Blocks.Obsidian);
        await Assert.That(box.Height).IsEqualTo(3);
    }

    [Test]
    public async Task The_material_the_author_named_is_the_block_that_lands()
    {
        var (world, resolved) = Build(Json.Replace(
            """{ "piece": "bar-w", "at": [2, 2] }""",
            """{ "piece": "bar-w", "at": [2, 2], "style": "cube-3", "materials": "emerald block" }"""));
        var box = resolved.Destroyables![0].Box!.Value;
        await Assert.That(world.GetBlock(box.MinX, box.MinY, box.MinZ).Id).IsEqualTo(Blocks.EmeraldBlock);
        await Assert.That((box.Width, box.Height, box.Depth)).IsEqualTo((3, 3, 3));
    }

    [Test]
    public async Task The_exported_xml_carries_the_destroyables_and_reads_back()
    {
        var (_, resolved) = Build(Json);
        var doc = new Dict();
        IntentGenerator.Apply(doc, resolved);

        var xml = XmlWriter.ToXml(Deserializer.FromDict(Serializer.ToDict(Deserializer.FromDict(doc))));
        await Assert.That(xml).Contains("<destroyables>");
        await Assert.That(xml).Contains("Red Monument");
        await Assert.That(xml).Contains("Blue Monument");

        // The round trip must survive a real parse, and the map must read as DTM off its modules alone —
        // nothing declares a <gamemode>.
        var reparsed = MapParser.ParseXmlString(xml);
        await Assert.That(reparsed.Destroyables.Count).IsEqualTo(2);
        await Assert.That(reparsed.Destroyables.All(d => d.IsObjective)).IsTrue();
        await Assert.That(reparsed.Gamemodes).Contains(Gamemodes.Dtm);
    }
}
