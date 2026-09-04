using System.Text.Json;
using PgmStudio.Domain;
using PgmStudio.Export;
using PgmStudio.Minecraft;
using PgmStudio.Pgm;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Sketch;
using PgmStudio.Vocabulary;

namespace PgmStudio.Export.Tests;

using Dict = Dictionary<string, object?>;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Palette;
using PgmStudio.Minecraft.Stamping;

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
          "plan": 2,
          "meta": { "name": "DTM Probe" },
          "globals": { "cell": 5, "symmetry": "rot_180", "surface": 9, "headroom": 11 },
          "pieces": [
            { "id": "bar-w", "role": "piece", "rect": [1, -2, 4, 4], "surface": 12 }
          ],
          "placements": {
            "destroyables": [ { "piece": "bar-w", "at": [10, 10] } ]
          }
        }
        """;

    private static (VoxelWorld World, MapIntent Resolved) Build(string json)
    {
        var built = Built(json);
        return (built.World, built.ResolvedIntent);
    }

    private static BuiltWorld Built(string json)
    {
        var plan = PlanModel.Parse(json)!;
        var (layout, intent) = PlanCompiler.Compile(plan);
        return WorldBuilder.Build(JsonSerializer.Serialize(layout, SketchLayout.Json), intent);
    }

    // The authored marker, and the same marker restated with fields on it.
    private const string Marker = """{ "piece": "bar-w", "at": [10, 10] }""";

    // ── the material a goal's size is built for (DC3) ──────────────────────────────────────────────
    [Test]
    public async Task A_cube_asking_for_obsidian_is_built_in_ender_stone_and_says_so()
    {
        // The fault this closes: three of the corpus's own generated boards carry 27 obsidian on a cube-3,
        // which is a grind rather than a raid. The world is built and the goal stands — in ender stone — so
        // it is a complaint, and the map.xml declares what was actually laid.
        var built = Built(Json.Replace(Marker,
            """{ "piece": "bar-w", "at": [10, 10], "style": "cube-3", "materials": "obsidian" }"""));

        var complaints = built.Declines.Where(f => f.Rule == ObjectiveRules.StyleMaterial).ToList();
        await Assert.That(complaints.Count).IsEqualTo(2);          // one per orbit image
        await Assert.That(complaints.All(f => f.Severity == Severity.Complaint)).IsTrue();
        await Assert.That(complaints[0].Message).Contains("27 blocks");

        // The correction rides into the resolved intent, so the XML declares the blocks that were laid.
        await Assert.That(built.ResolvedIntent.Destroyables!.All(
            destroyable => destroyable.Materials == DestroyableMaterials.Bulk)).IsTrue();
        var box = built.ResolvedIntent.Destroyables![0].Box!.Value;
        await Assert.That(built.World.GetBlock(box.MinX, box.MinY, box.MinZ).Id).IsEqualTo(Blocks.EndStone);
    }

    [Test]
    public async Task A_pillar_in_obsidian_draws_no_complaint()
    {
        // The default board: a pillar-3 in obsidian is exactly what obsidian is for, and half the corpus.
        var built = Built(Json);
        await Assert.That(built.Declines.Any(f => f.Rule == ObjectiveRules.StyleMaterial)).IsFalse();
    }

    [Test]
    public async Task A_material_the_studio_cannot_build_is_corrected_rather_than_written_through()
    {
        var built = Built(Json.Replace(Marker,
            """{ "piece": "bar-w", "at": [10, 10], "materials": "diamond block" }"""));

        var complaint = built.Declines.First(f => f.Rule == ObjectiveRules.StyleMaterial);
        await Assert.That(complaint.Message).Contains("diamond block");
        // A declared material matching nothing inside its own region is a goal at zero health (OB3).
        await Assert.That(built.ResolvedIntent.Destroyables!.All(
            destroyable => destroyable.Materials == "obsidian")).IsTrue();
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
            """{ "piece": "bar-w", "at": [10, 10] }""",
            """{ "piece": "bar-w", "at": [10, 10], "style": "cube-3", "materials": "emerald block" }"""));
        var box = resolved.Destroyables![0].Box!.Value;
        await Assert.That(world.GetBlock(box.MinX, box.MinY, box.MinZ).Id).IsEqualTo(Blocks.EmeraldBlock);
        await Assert.That((box.Width, box.Height, box.Depth)).IsEqualTo((3, 3, 3));
    }

    [Test]
    public async Task Each_destroyable_stands_on_a_buried_5x5_bedrock_platform_with_a_chest_beside_it()
    {
        // MG23/B88: a one-block-thick 5×5 plate, seated three courses beneath the ground under the goal, so
        // the goal cannot be undermined from below — and a defence chest standing on the ground over it.
        var (world, resolved) = Build(Json);
        await Assert.That(resolved.Destroyables!.Count).IsEqualTo(2);

        foreach (var destroyable in resolved.Destroyables!)
        {
            var anchorX = (int)Math.Round(destroyable.Anchor.X, MidpointRounding.AwayFromZero);
            var anchorZ = (int)Math.Round(destroyable.Anchor.Z, MidpointRounding.AwayFromZero);
            // The piece surfaces at y=12, so the ground's own top block is y=11 and the buried plate is y=8.
            var count = 0;
            for (var x = anchorX - 2; x <= anchorX + 2; x++)
            for (var z = anchorZ - 2; z <= anchorZ + 2; z++)
                if (world.GetBlock(x, 8, z).Id == Blocks.Bedrock) count++;
            await Assert.That(count).IsEqualTo(25);

            // The chest stands on the ground beside the monument, with the course over it carved so the lid
            // opens — walked up to rather than dug for. The space the plate's depth opened stays terrain.
            await Assert.That(world.GetBlock(anchorX, 12, anchorZ).Id).IsEqualTo(Blocks.Chest);
            await Assert.That(world.GetBlock(anchorX, 13, anchorZ).Id).IsEqualTo(Blocks.Air);
            await Assert.That(world.GetBlock(anchorX, 9, anchorZ).Id).IsNotEqualTo(Blocks.Chest);
            await Assert.That(world.GetBlock(anchorX, 11, anchorZ).Id).IsNotEqualTo(Blocks.Bedrock);
            await Assert.That(world.GetBlock(anchorX, 11, anchorZ).Id).IsNotEqualTo(Blocks.Air);
        }
    }

    [Test]
    public async Task Each_destroyable_carries_a_sky_marker_in_its_owning_teams_colour_above_the_build_cap()
    {
        // MG24/B89: the marker floats clear of the build cap, out of build reach by construction. The cap is
        // the one the world build derived and wrote onto the intent, so this asserts the marker against what
        // the map actually declares rather than against a number restated here — the two agreeing is the
        // point. What the cap itself must be is asserted in BuildCeilingTests.
        var (world, resolved) = Build(Json);
        var floorY = resolved.Build!.MaxHeight!.Value + BuildCeiling.MarkerOver;

        foreach (var destroyable in resolved.Destroyables!)
        {
            var anchorX = (int)Math.Round(destroyable.Anchor.X, MidpointRounding.AwayFromZero);
            var anchorZ = (int)Math.Round(destroyable.Anchor.Z, MidpointRounding.AwayFromZero);
            var expectedDamage = destroyable.Owner == "red" ? 14 : 11;   // red / blue wool
            await Assert.That(world.GetBlock(anchorX, floorY + 1, anchorZ)).IsEqualTo((Blocks.Wool, expectedDamage));
        }
    }

    // B128: the demonstration. A destroyable rides an authored landform that carries no plan piece at all —
    // the mesa's own shape, added the way Sketch actually authors one, with the marker naming only an
    // absolute board position. Under the old code this destroyable would not exist in the compiled intent
    // (the piece lookup for an empty id returned null and the whole marker was dropped); here it lands at the
    // height the "relief" left, not at any plan-nominal surface, because there is no piece to nominate one.
    [Test]
    public async Task A_destroyable_needs_no_plan_piece_when_it_rides_an_authored_landform()
    {
        const string json = """
            {
              "plan": 2,
              "meta": { "name": "Mesa Probe" },
              "globals": { "cell": 5, "symmetry": "rot_180", "surface": 9, "headroom": 11 },
              "pieces": [],
              "placements": {
                "destroyables": [ { "piece": "", "at": [10, 10] } ]
              }
            }
            """;

        var plan = PlanModel.Parse(json)!;
        var (_, intent) = PlanCompiler.Compile(plan);
        await Assert.That(intent.Destroyables!.Count).IsEqualTo(2);

        // The mesa: one polygon added by hand after compile, standing for a Sketch-authored `raise` shape —
        // no plan piece behind it, no plan tier manufactured to carry the marker.
        const string layoutJson = """
            {"setup":{"mirror_mode":"rot_180","center":{"cx":0,"cz":0}},
             "layers": [{ "id": "ground", "base_y": 0, "layout":{"shapes":[
               {"id":"mesa","type":"rectangle","operation":"add",
                "min_x":-30,"min_z":-30,"max_x":30,"max_z":30,"base_height":30}
             ],"groups":[]} }]}
            """;

        var built = WorldBuilder.Build(layoutJson, intent);
        await Assert.That(built.ResolvedIntent.Destroyables!.Count).IsEqualTo(2);

        foreach (var destroyable in built.ResolvedIntent.Destroyables!)
        {
            var box = destroyable.Box!.Value;
            // The mesa's surface top is y=30 (its base_height); the default float is 4, so the goal floats
            // at 34 — the offset counted from the ground actually solved there, not from any plan surface
            // (the plan carried no piece, so it had none to offer).
            await Assert.That(box.MinY).IsEqualTo(34);
        }
    }

    // The same demonstration against Ashen Quarry's own geometry (specs/ashen_quarry/ashen_quarry.plan.json):
    // its mesa is the map the review found this defect on — a `raise` shape pushed back into the plan as a
    // rectangular tier at surface 58 purely so `destroyable-2` had a piece to ride, then promoted back to a
    // polygon to recover the outline it already had. Here the mesa is never a plan piece at all: its rect and
    // surface stand for the authored polygon directly, and the marker names only the absolute position it
    // held on the mesa (`piece: "mesa", at: [25, 27]`, cell 1 → block (-93, -67)).
    [Test]
    public async Task Ashen_Quarrys_mesa_goal_needs_no_manufactured_tier()
    {
        const string json = """
            {
              "plan": 2,
              "meta": { "name": "Ashen Quarry mesa probe" },
              "globals": { "cell": 1, "symmetry": "rot_180", "surface": 41, "headroom": 28 },
              "pieces": [],
              "placements": {
                "destroyables": [
                  { "piece": "", "at": [-93, -67], "style": "cube-4", "materials": "gold block",
                    "float": 4, "name": "Mesa Monument" }
                ]
              }
            }
            """;

        var plan = PlanModel.Parse(json)!;
        var (_, intent) = PlanCompiler.Compile(plan);
        await Assert.That(intent.Destroyables!.Count).IsEqualTo(2);

        // The mesa's own footprint and surface (`mesa`: rect [-118,-94,50,54] → block [-118,-94]..[-68,-40],
        // surface 58), authored as the polygon it actually is rather than demoted into the plan.
        const string layoutJson = """
            {"setup":{"mirror_mode":"rot_180","center":{"cx":0,"cz":0}},
             "layers": [{ "id": "ground", "base_y": 0, "layout":{"shapes":[
               {"id":"mesa","type":"rectangle","operation":"add",
                "min_x":-118,"min_z":-94,"max_x":-68,"max_z":-40,"base_height":58}
             ],"groups":[]} }]}
            """;

        var built = WorldBuilder.Build(layoutJson, intent);
        await Assert.That(built.ResolvedIntent.Destroyables!.Count).IsEqualTo(2);

        foreach (var mesaGoal in built.ResolvedIntent.Destroyables!)
        {
            await Assert.That(mesaGoal.Name).IsEqualTo("Mesa Monument");
            // The mesa's surface, plus the default float — resolved from the polygon's own ground, with no
            // plan piece in the picture at all.
            await Assert.That(mesaGoal.Box!.Value.MinY).IsEqualTo(58 + 4);
        }
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
    // ── the plate under a goal, and the ground it is measured from ─────────────────────────────────

    /// <summary>Two pieces eighteen courses apart, the goal standing on the low one two blocks from the seam,
    /// so its plate's fixed 5×5 reaches onto the high one. The plate is a square the goal did not choose, and
    /// a plate resolving its own depth over that square takes the tallest column in it — which on
    /// <c>opus5-tiefkreuz</c> put a 5×5 sheet of bedrock at y26 over a monument at y11 and left the monument
    /// with no plate at all.</summary>
    private const string Shelf = """
        {
          "plan": 2,
          "meta": { "name": "Shelf Probe" },
          "globals": { "cell": 5, "symmetry": "rot_180", "surface": 9, "headroom": 24 },
          "pieces": [
            { "id": "low",  "role": "piece", "rect": [2, -2, 5, 4], "surface": 12 },
            { "id": "high", "role": "piece", "rect": [1, -2, 2, 4], "surface": 30 }
          ],
          "placements": {
            "destroyables": [ { "piece": "low", "at": [6, 15], "style": "pillar-3" } ]
          }
        }
        """;

    [Test]
    public async Task The_plate_is_buried_under_the_ground_the_goal_stands_on_and_not_under_the_tallest_column_it_spans()
    {
        var built = Built(Shelf);
        var world = built.World;
        var goals = built.ResolvedIntent.Destroyables!;
        await Assert.That(goals.Count).IsEqualTo(2).Because("rot_180 gives the authored marker two images");
        var spanned = 0;

        foreach (var goal in goals)
        {
            var box = goal.Box!.Value;
            var groundTop = box.MinY - goal.Float;                       // the surface the goal resolved on
            var plateY = groundTop - 1 - StructureStamper.PlatformDepth;
            var (minX, minZ, maxX, maxZ) = ObjectiveFootprint.Centred(
                box.MinX, box.MinZ, StructureStamper.PlatformSize, StructureStamper.PlatformSize);

            // The board this claim needs: the goal on the low shelf and the plate's own square reaching the
            // high one. Read over the whole column, because the shelf that lifted the plate stands well above
            // the goal — a read stopping at the goal's own floor is exactly the read that cannot see it.
            var tallest = groundTop;
            for (var x = minX; x <= maxX; x++)
            for (var z = minZ; z <= maxZ; z++)
                tallest = Math.Max(tallest, Top(world, x, z, VoxelWorld.MaxHeight - 1) + 1);
            if (tallest >= groundTop + 18) spanned++;

            for (var x = minX; x <= maxX; x++)
            for (var z = minZ; z <= maxZ; z++)
                await Assert.That(world.GetBlock(x, plateY, z).Id).IsEqualTo(Blocks.Bedrock)
                    .Because("the plate is one course, at the goal's own depth, over its whole square");

            await Assert.That(plateY).IsLessThan(box.MinY)
                .Because("a plate is buried under its goal, never over it");
            await Assert.That(world.GetBlock(box.MinX, tallest - 1 - StructureStamper.PlatformDepth, box.MinZ).Id)
                .IsNotEqualTo(Blocks.Bedrock)
                .Because("the tallest column the square spans is not the ground this goal stands on");
        }

        await Assert.That(spanned).IsEqualTo(goals.Count)
            .Because("both images stand two blocks from the seam, which is what makes the two readings differ");
    }

    private static int Top(VoxelWorld world, int x, int z, int ymax)
    {
        for (var y = ymax; y >= 0; y--)
            if (world.GetBlock(x, y, z).Id != Blocks.Air) return y;
        return -1;
    }
}
