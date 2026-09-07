using System.Text.Json.Nodes;
using PgmStudio.Domain;
using PgmStudio.Geom;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Painting;
using PgmStudio.Minecraft.Palette;
using PgmStudio.Minecraft.Stamping;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Plan;

namespace PgmStudio.Export.Tests;

/// <summary>
/// <b>A room's plinth is ground, and ground is what the painter finishes</b>. A foundation levels the
/// dip under a room's footprint in stone so the floor over it spans no air, and stone is what the painter
/// rewrites (TP6) — but the painter reads a <em>surface map</em>, and the fill sits above the top that map
/// states. So the plinth is a course the painter never addresses, and the ground a room stands on stays raw
/// stone on a board that is painted everywhere else.
///
/// <para>Both halves are here. The first is the mechanism on its own: the same fill, painted against the
/// terrain's map and against the map with the plinth folded in. The second is a built board, where the plinth
/// is the ground a player walks on — a footprint too small to carry walls raises no shell (WX2), so nothing
/// covers it.</para>
/// </summary>
public sealed class RoomPlinthPaintTests
{
    private const int Quartz = 155;

    /// <summary>A board painted one distinctive material in every bucket, so a course is either finished or
    /// it is not and no bucket boundary can be mistaken for the defect.</summary>
    private static TerrainTheme Crest()
    {
        var quartz = new SolidMaterial(Quartz);
        return TerrainTheme.Default with
        {
            Surface = new TopBand(quartz, Depth: 1), Rim = new TopBand(quartz, Depth: 1),
            Fill = quartz, Wall = quartz,
        };
    }

    /// <summary>Raw stone ground over a 20×20 board, with the western half of the room's footprint standing
    /// four blocks higher — so the fill has a dip to level and the plinth is not the terrain's own top.</summary>
    private static (VoxelWorld World, Dictionary<(int X, int Z), int> Tops) Stepped(
        int minX, int minZ, int maxX, int maxZ)
    {
        var world = new VoxelWorld();
        var tops = new Dictionary<(int X, int Z), int>();
        for (var x = -5; x < 15; x++)
        for (var z = -5; z < 15; z++)
        {
            var height = x < (minX + maxX) / 2 && x >= minX && z >= minZ && z < maxZ ? 16 : 12;
            world.SetBlock(x, 0, z, Blocks.Bedrock);
            for (var y = 1; y < height; y++) world.SetBlock(x, y, z, Blocks.Stone);
            tops[(x, z)] = height;
        }
        return (world, tops);
    }

    private static int RawStoneIn(VoxelWorld world, int y, int minX, int minZ, int maxX, int maxZ)
        => StructureStamper.FoundationCells(minX, minZ, maxX, maxZ)
                           .Count(cell => world.GetBlock(cell.X, y, cell.Z) == (Blocks.Stone, 0));

    /// <summary>The mechanism, with nothing else in the world. Painting against the terrain's own surface map
    /// leaves the levelled half of the plinth raw — and the courses the painter <em>did</em> reach are buried
    /// under it, which is the tell: the paint is in the world, at the wrong height.</summary>
    [Test]
    public async Task Painted_against_the_terrains_own_map_the_plinth_stays_raw_stone()
    {
        var (world, tops) = Stepped(0, 0, 10, 10);
        StructureStamper.StampFoundation(world, tops, 0, 0, 10, 10);

        TerrainPainter.Paint(world, tops, Crest());

        // Half the footprint stood at the level already and was painted; the half the fill raised is stone.
        await Assert.That(RawStoneIn(world, 15, 0, 0, 10, 10)).IsEqualTo(50);
        // And the surface the painter did finish is a course the plinth buried.
        await Assert.That(world.GetBlock(7, 11, 5)).IsEqualTo((Quartz, 0));
    }

    /// <summary>The same world, with the plinth folded into the map the painter reads. Every column of the
    /// footprint is finished at the level the fill left it.</summary>
    [Test]
    public async Task Folding_the_plinth_into_the_surface_map_finishes_it()
    {
        var (world, tops) = Stepped(0, 0, 10, 10);
        var plinth = StructureStamper.FoundationTops(tops, 0, 0, 10, 10);
        StructureStamper.StampFoundation(world, tops, 0, 0, 10, 10);

        var surface = new Dictionary<(int X, int Z), int>(tops);
        foreach (var (cell, top) in plinth) surface[cell] = top;
        TerrainPainter.Paint(world, surface, Crest());

        await Assert.That(RawStoneIn(world, 15, 0, 0, 10, 10)).IsEqualTo(0);
        await Assert.That(world.GetBlock(2, 15, 5)).IsEqualTo((Quartz, 0));   // the levelled half
        await Assert.That(world.GetBlock(7, 15, 5)).IsEqualTo((Quartz, 0));   // the half already at the level
    }

    /// <summary>And on a built board. A wool room whose footprint straddles a step in the ground, on a map
    /// whose theme is quartz: its floor course is the room's pad and the board's own ground, with no raw
    /// stone in it. The footprint is 5×5 — under <c>RoomFrames.MinSpan</c> with a wall — so the room raises
    /// no shell (WX2) and the plinth is what a player stands on.</summary>
    [Test]
    public async Task A_built_rooms_floor_course_is_the_boards_ground_and_not_raw_stone()
    {
        var plan = PlanModel.Parse("""
            { "plan":2, "globals":{"symmetry":"mirror_z","surface":9,"cell":5},
              "pieces":[
                {"id":"spawn-a","role":"spawn","rect":[-8,-14,6,6],"surface":12},
                {"id":"lane","role":"lane","rect":[-8,-8,16,16],"surface":20},
                {"id":"wool-a","role":"wool-room","rect":[2,8,6,6],"surface":12}
              ],
              "placements":{
                "spawns":[{"piece":"spawn-a","at":[2.5,2.5],"facing":"front"}],
                "wools":[{"piece":"wool-a","at":[2.5,2.5]}]
              } }
            """)!;
        var (layout, intent) = PlanCompiler.Compile(plan);

        var doc = JsonNode.Parse(layout.ToJson())!.AsObject();
        doc["themes"] = new JsonObject { ["crest"] = JsonNode.Parse(TerrainThemeJson.Serialize(Crest()))! };
        doc["mapTheme"] = "crest";
        // A step through the wool region, so the room's own footprint spans two heights.
        doc["layers"]![0]!["layout"]!["shapes"]!.AsArray().Add(JsonNode.Parse(
            """
            {"id":"step","type":"rectangle","operation":"add","min_x":11,"min_z":41,
             "max_x":25,"max_z":69,"floor":0,"base_height":16}
            """)!);

        var framed = intent with
        {
            Wools = [.. intent.Wools!.Select((w, i) => i == 0
                ? w with { Footprint = new Rect(23, 50, 28, 55) } : w)],
        };
        var built = WorldBuilder.Build(doc.ToJsonString(), framed);
        var frame = WorldBuilder.WoolFrame(framed.Wools![0], shellBound: true);
        var floor = WorldBuilder.FrameFloor(frame, built.Surface);

        // A footprint this small carries no walls, so the plinth is the floor rather than something under it.
        await Assert.That(frame.Wall).IsEqualTo(0);

        var raw = RawStoneIn(built.World, floor, frame.MinX, frame.MinZ, frame.MaxX, frame.MaxZ);
        await Assert.That(raw).IsEqualTo(0);

        // What is there instead: twenty-five cells of the board's own ground, with the room's 3×3 wool pad
        // set into it. Before the plinth was folded in, twelve of the sixteen were raw stone.
        var course = StructureStamper.FoundationCells(frame.MinX, frame.MinZ, frame.MaxX, frame.MaxZ)
            .CountBy(cell => built.World.GetBlock(cell.X, floor, cell.Z))
            .ToDictionary();
        await Assert.That(course[(Quartz, 0)]).IsEqualTo(16);
        await Assert.That(course[(Blocks.Wool, BlockColors.BlockDamage("red"))]).IsEqualTo(9);
    }
}
