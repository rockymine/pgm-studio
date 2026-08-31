using System.Text.Json;
using PgmStudio.Domain;
using PgmStudio.Geom;
using PgmStudio.Export;
using PgmStudio.Minecraft;
using PgmStudio.Pgm;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Export.Tests;

using Dict = Dictionary<string, object?>;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Palette;
using PgmStudio.Minecraft.Stamping;

/// <summary>
/// End-to-end DTC: a plan carrying a core marker → world + map.xml. The claims worth proving are that the
/// casing actually encloses lava (a core that leaks on its own, or never leaks, is not a goal) and that the
/// emitted region contains both — the same OB8 check the destroyable path makes.
/// </summary>
public sealed class CoreWorldTests
{
    private const string Json = """
        {
          "plan": 2,
          "meta": { "name": "DTC Probe" },
          "globals": { "cell": 5, "symmetry": "rot_180", "surface": 9, "headroom": 11 },
          "pieces": [
            { "id": "bar-w", "role": "piece", "rect": [1, -2, 4, 4], "surface": 12 }
          ],
          "placements": {
            "cores": [ { "piece": "bar-w", "at": [10, 10] } ]
          }
        }
        """;

    private const string Marker = """{ "piece": "bar-w", "at": [10, 10] }""";

    private static (VoxelWorld World, MapIntent Resolved) Build(string json)
    {
        var plan = PlanModel.Parse(json)!;
        var (layout, intent) = PlanCompiler.Compile(plan);
        var built = WorldBuilder.Build(JsonSerializer.Serialize(layout, SketchLayout.Json), intent);
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
            """{ "piece": "bar-w", "at": [10, 10], "openTop": true }"""));
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
    public async Task Each_core_carries_a_sky_marker_in_its_owning_teams_colour_above_the_build_cap()
    {
        // MG24/B89: the marker floats clear of BuildIntent.MaxHeight (globals surface 9 + headroom 11 = 20
        // here) — above build height, so it cannot be reached or griefed.
        var (world, resolved) = Build(Json);
        var floorY = resolved.Build!.MaxHeight!.Value + BuildCeiling.MarkerOver;

        foreach (var core in resolved.Cores!)
        {
            var anchorX = (int)Math.Round(core.Anchor.X, MidpointRounding.AwayFromZero);
            var anchorZ = (int)Math.Round(core.Anchor.Z, MidpointRounding.AwayFromZero);
            var expectedDamage = core.Owner == "red" ? 14 : 11;   // red / blue wool
            await Assert.That(world.GetBlock(anchorX, floorY + 1, anchorZ)).IsEqualTo((Blocks.Wool, expectedDamage));
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
        await Assert.That(reparsed.Cores.Select(c => c.Owner)).IsEquivalentTo(new[] { "red-team", "blue-team" });
        // Nothing declares a <gamemode>; the map reads as DTC off its modules alone.
        await Assert.That(reparsed.Gamemodes).IsEquivalentTo(new[] { Gamemodes.Dtc });
    }
    /// <summary>A core assembled by hand — not through <see cref="PlanCompiler"/>, which fills every knob —
    /// casts the same casing. The record's own defaults are the ones its plan-placement schema documents, so
    /// a caller that omits a key gets the modal core rather than a casing of no size: a <c>&lt;core&gt;</c>
    /// over a region holding nothing is a goal at zero health, and the export answers 200 for it
    /// (<c>WE15</c>).</summary>
    [Test]
    public async Task A_core_built_by_hand_casts_the_casing_its_schema_documents()
    {
        var core = new CoreIntent { Owner = "red-team", Anchor = new Pt(0, 12, 0) };

        await Assert.That(core.Lava).IsEqualTo(ObjectiveDefaults.CoreLava);
        await Assert.That(core.LavaHeight).IsEqualTo(ObjectiveDefaults.CoreLavaHeight);
        // and the casing those two imply, which is the 5×5×5 obsidian DC1 names.
        await Assert.That(core.Size).IsEqualTo(5);
        await Assert.That(core.Height).IsEqualTo(5);
        await Assert.That(core.Shell).IsEqualTo(ObjectiveDefaults.CoreShell);
        await Assert.That(core.Float).IsEqualTo(ObjectiveDefaults.CoreFloat);
        await Assert.That(core.Leak).IsEqualTo(ObjectiveDefaults.CoreLeak);
    }

    /// <summary>The same for a hand-built destroyable: a style and a material it is made of, and the air
    /// under it that keeps it off the ground. An empty style is a structure the stamper cannot resolve, and
    /// a float of zero puts the goal on the terrain, where covering it is trivial.</summary>
    [Test]
    public async Task A_destroyable_built_by_hand_carries_a_style_a_material_and_its_float()
    {
        var destroyable = new DestroyableIntent { Owner = "red-team", Anchor = new Pt(0, 12, 0) };

        await Assert.That(destroyable.Style).IsEqualTo(DestroyableStyles.Slug(ObjectiveDefaults.Style));
        await Assert.That(destroyable.Materials).IsEqualTo(ObjectiveDefaults.Materials);
        await Assert.That(destroyable.Float).IsEqualTo(ObjectiveDefaults.DestroyableFloat);
    }
}
