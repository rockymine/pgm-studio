using System.Text.Json;
using PgmStudio.Export;
using PgmStudio.Minecraft;
using PgmStudio.Minecraft.Render;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Sketch;
using PgmStudio.Geom;

namespace PgmStudio.Export.Tests;

/// <summary>
/// The sketch → world assembly (no DB): a rectangular layout + a 2-team intent yields terrain, wool cages,
/// spawn cubes with auto-derived monuments, and an observer platform — plus a resolved intent whose spawns
/// are integer-snapped and whose monument locations point at the world air cells.
/// </summary>
public sealed class SketchWorldBuilderTests
{
    private const string Layout =
        """
        {"setup":{"mirror_mode":"rot_180","center":{"cx":0,"cz":0}},"layout":{"shapes":[{"id":"a","type":"rectangle","operation":"add","min_x":-40,"min_z":-40,"max_x":40,"max_z":40,"base_height":1}],"islands":[]}}
        """;

    private static MapIntent SampleIntent() => new()
    {
        Teams = [new TeamDef { Id = "red", Color = "red" }, new TeamDef { Id = "blue", Color = "blue" }],
        Spawns =
        [
            new SpawnIntent { Team = "red", Point = new Pt(-20, 1, 0), Yaw = 0 },
            new SpawnIntent { Team = "blue", Point = new Pt(20, 1, 0), Yaw = 180 },
        ],
        Wools =
        [
            new WoolIntent { Owner = "red", Color = "red", Spawn = new Pt(-10, 1, 10), Monuments = [new MonumentIntent { Team = "blue" }] },
            new WoolIntent { Owner = "blue", Color = "blue", Spawn = new Pt(10, 1, 10), Monuments = [new MonumentIntent { Team = "red" }] },
        ],
        Observer = new ObserverIntent { Point = new Pt(0, 20, 0), Yaw = 0 },
        Meta = new MetaIntent { Name = "Test", Authors = [new AuthorIntent { Name = "alice" }] },
    };

    [Test]
    public async Task Builds_terrain_cages_and_a_floating_observer_spawn()
    {
        var built = SketchWorldBuilder.Build(Layout, SampleIntent());

        // Terrain: bedrock floor at y=0 under the footprint.
        await Assert.That(built.World.GetBlock(0, 0, 0)).IsEqualTo((Blocks.Bedrock, 0));

        // Observer platform floats at the authored Y=20; world spawn stands on it (21).
        await Assert.That(built.SpawnY).IsEqualTo(21);
        await Assert.That(built.World.GetBlock(0, 20, 0)).IsEqualTo((Blocks.Bedrock, 0));   // platform floor

        // Wool cage at the (snapped) red wool spawn: 2×2 wool marker on the floor (surface top y=1).
        await Assert.That(built.World.GetBlock(-10, 1, 10)).IsEqualTo((Blocks.Wool, 14));   // red = 14
    }

    [Test]
    public async Task Every_wool_room_carries_a_sky_marker_in_its_own_wool_colour()
    {
        // B89: no Build intent is authored here, so the marker floor falls back to comfortably above the
        // tallest terrain rather than an unset build cap — it still ends up well clear of the play surface.
        var built = SketchWorldBuilder.Build(Layout, SampleIntent());
        var floorY = 1 + GoalMarkerStamper.Clearance;   // terrain tops out at y=1 across this flat sketch

        // The default 10×10 shell centres on the (snapped) wool spawn point for each room.
        await Assert.That(built.World.GetBlock(-10, floorY + 1, 10)).IsEqualTo((Blocks.Wool, 14));   // red room
        await Assert.That(built.World.GetBlock(10, floorY + 1, 10)).IsEqualTo((Blocks.Wool, 11));    // blue room
    }

    [Test]
    public async Task Room_floor_bedrock_goes_under_the_room_not_over_its_pad()
    {
        // The plan's ST1 room floor fills the whole wool-room piece solid to the surface. Its top block is the
        // course the room's own floor and wool pad occupy, so stamping order decides which survives: laid last
        // it buries both, and the room reads as a shell of bedrock with nothing to break out of it.
        // A raised plateau, so the sunk floor is a real course of ground rather than clamped off the world
        // bottom: solid to y=4, first air at y=5, so the room's floor is the platform's own top block.
        const string plateau =
            """
            {"setup":{"mirror_mode":"none","center":{"cx":0,"cz":0}},"layout":{"shapes":[{"id":"a","type":"rectangle","operation":"add","min_x":-40,"min_z":-40,"max_x":40,"max_z":40,"base_height":5}],"islands":[]}}
            """;
        var intent = SampleIntent();
        var piece = new Rect(-16, 4, -4, 16);
        intent = new MapIntent
        {
            Teams = intent.Teams, Spawns = intent.Spawns, Observer = intent.Observer, Meta = intent.Meta,
            Wools =
            [
                new WoolIntent
                {
                    Owner = "red", Color = "red", Spawn = new Pt(-10, 1, 10), Piece = piece,
                    Entries = [new Rect(piece.MinX, piece.MaxZ, piece.MaxX, piece.MaxZ)],
                    Monuments = [new MonumentIntent { Team = "blue" }],
                },
            ],
            Structures = new StructureIntent { RoomFloors = [piece] },
        };

        var built = SketchWorldBuilder.Build(plateau, intent);
        var pad = built.ResolvedIntent.Wools![0].Spawn;
        await Assert.That(pad.Y).IsEqualTo(4);

        // The pad is wool at the floor course, and bedrock stops one below it rather than one above.
        await Assert.That(built.World.GetBlock((int)pad.X, (int)pad.Y, (int)pad.Z))
            .IsEqualTo((Blocks.Wool, 14));
        await Assert.That(built.World.GetBlock((int)pad.X, (int)pad.Y - 1, (int)pad.Z).Id)
            .IsEqualTo(Blocks.Bedrock);

        // …and it still tops out at the floor course outside the shell, where the entrance line runs: the fill
        // is the ground under the whole piece, not just under the building inset into it.
        await Assert.That(built.World.GetBlock((int)piece.MinX, (int)pad.Y, (int)piece.MinZ).Id)
            .IsEqualTo(Blocks.Bedrock);
    }

    [Test]
    public async Task Team_tint_owns_terrain_by_island_and_leaves_anchorless_land_neutral()
    {
        // Three separate islands (no mirroring): a red-spawn island, a blue-spawn island, and one with no
        // anchor. Each is a base_height-5 plateau, so the painter walls their edges.
        const string layout =
            """
            {"setup":{"mirror_mode":"none","center":{"cx":0,"cz":0}},"layout":{"shapes":[
              {"id":"a","type":"rectangle","operation":"add","min_x":-30,"max_x":-10,"min_z":-10,"max_z":10,"base_height":5},
              {"id":"b","type":"rectangle","operation":"add","min_x":10,"max_x":30,"min_z":-10,"max_z":10,"base_height":5},
              {"id":"c","type":"rectangle","operation":"add","min_x":-5,"max_x":5,"min_z":30,"max_z":50,"base_height":5}
            ],"islands":[]}}
            """;
        var intent = new MapIntent
        {
            Teams = [new TeamDef { Id = "red", Color = "red" }, new TeamDef { Id = "blue", Color = "blue" }],
            Spawns =
            [
                new SpawnIntent { Team = "red", Point = new Pt(-20, 5, 0), Yaw = 0 },
                new SpawnIntent { Team = "blue", Point = new Pt(20, 5, 0), Yaw = 180 },
            ],
            Observer = new ObserverIntent { Point = new Pt(0, 30, 0), Yaw = 0 },
            Meta = new MetaIntent { Name = "Islands", Authors = [] },
        };

        var w = SketchWorldBuilder.Build(layout, intent).World;
        // A left edge (x=-30) → red clay wall (14); B right edge (x=29) → blue clay (11);
        // the anchorless island C edge → neutral light-grey clay (8).
        await Assert.That(w.GetBlock(-30, 2, 0)).IsEqualTo((Blocks.StainedClay, 14));
        await Assert.That(w.GetBlock(29, 2, 0)).IsEqualTo((Blocks.StainedClay, 11));
        await Assert.That(w.GetBlock(-5, 2, 40)).IsEqualTo((Blocks.StainedClay, 8));
        // A rim stays quartz regardless of team; the interior stays grass.
        await Assert.That(w.GetBlock(-30, 4, 0)).IsEqualTo((Blocks.QuartzBlock, 0));
    }

    [Test]
    public async Task Resolves_snapped_spawns_and_derived_monument_locations()
    {
        var built = SketchWorldBuilder.Build(Layout, SampleIntent());
        var resolved = built.ResolvedIntent;

        // Spawns snapped to whole integers, Y = floor + 1 (standing on the cube floor).
        await Assert.That(resolved.Spawns[0].Point).IsEqualTo(new Pt(-20, 2, 0));

        // The red wool (captured by blue) has its monument located inside blue's spawn cube (near x=20),
        // and the world has a bedrock pedestal below + glass cap above that air cell.
        var mon = resolved.Wools![0].Monuments[0];
        await Assert.That(mon.Team).IsEqualTo("blue");
        await Assert.That(Math.Abs(mon.Location.X - 20) <= 4).IsTrue();

        var (mx, my, mz) = ((int)mon.Location.X, (int)mon.Location.Y, (int)mon.Location.Z);
        await Assert.That(built.World.GetBlock(mx, my, mz)).IsEqualTo((Blocks.Air, 0));           // placement cell
        await Assert.That(built.World.GetBlock(mx, my - 1, mz)).IsEqualTo((Blocks.Bedrock, 0));   // pedestal
        await Assert.That(built.World.GetBlock(mx, my + 1, mz).Id).IsEqualTo(Blocks.StainedGlass); // cap
    }

    [Test]
    public async Task A_capturer_without_a_spawn_gets_no_monument_rather_than_one_at_the_origin()
    {
        // green has no spawn cube, so it has no placement cell; the auto-wired wool must not emit a
        // green monument at (0,0,0).
        var intent = new MapIntent
        {
            Teams = [new TeamDef { Id = "red", Color = "red" }, new TeamDef { Id = "blue", Color = "blue" },
                     new TeamDef { Id = "green", Color = "green" }],
            Spawns =
            [
                new SpawnIntent { Team = "red", Point = new Pt(-20, 1, 0), Yaw = 0 },
                new SpawnIntent { Team = "blue", Point = new Pt(20, 1, 0), Yaw = 180 },
            ],
            Wools = [new WoolIntent { Owner = "red", Color = "red", Spawn = new Pt(-10, 1, 10), Monuments = [] }],
            Observer = new ObserverIntent { Point = new Pt(0, 20, 0), Yaw = 0 },
            Meta = new MetaIntent { Name = "Test", Authors = [new AuthorIntent { Name = "alice" }] },
        };

        var mons = SketchWorldBuilder.Build(Layout, intent).ResolvedIntent.Wools![0].Monuments;

        await Assert.That(mons.Any(m => m.Team == "blue")).IsTrue();           // blue has a spawn → kept
        await Assert.That(mons.Any(m => m.Team == "green")).IsFalse();         // green has no spawn → skipped
        await Assert.That(mons.Any(m => m.Location == new Pt(0, 0, 0))).IsFalse();  // no phantom at origin
    }

    [Test]
    public async Task A_gold_team_gets_an_orange_cube_not_white()
    {
        // "gold" is a chat colour with no wool of its own; it must coerce to orange (damage 1), not the
        // damage-0 white fallback. Its spawn (yaw 0 → door on the +Z wall) leaves the -Z wall strip intact.
        var intent = new MapIntent
        {
            Teams = [new TeamDef { Id = "red", Color = "red" }, new TeamDef { Id = "gold", Color = "gold" }],
            Spawns =
            [
                new SpawnIntent { Team = "red", Point = new Pt(-20, 1, 0), Yaw = 180 },
                new SpawnIntent { Team = "gold", Point = new Pt(20, 1, 0), Yaw = 0 },
            ],
            Wools = [new WoolIntent { Owner = "red", Color = "red", Spawn = new Pt(-10, 1, 10), Monuments = [new MonumentIntent { Team = "gold" }] }],
            Observer = new ObserverIntent { Point = new Pt(0, 20, 0), Yaw = 0 },
            Meta = new MetaIntent { Name = "Test", Authors = [new AuthorIntent { Name = "alice" }] },
        };

        var built = SketchWorldBuilder.Build(Layout, intent);
        // Gold cube anchored at (20,0), floor y=1: the -Z wall strip (layer 4 → y=5) is orange stained clay.
        await Assert.That(built.World.GetBlock(19, 5, -4)).IsEqualTo((Blocks.StainedClay, 1));
    }

    [Test]
    public async Task An_author_elevated_island_caps_the_cube_floor_instead_of_throwing()
    {
        // A very tall island pushes the surface to the world ceiling; the cube floor must clamp so its roof
        // stays under y=256 rather than throwing ArgumentOutOfRangeException mid-export.
        const string tall =
            """
            {"setup":{"mirror_mode":"rot_180","center":{"cx":0,"cz":0}},"layout":{"shapes":[{"id":"a","type":"rectangle","operation":"add","min_x":-40,"min_z":-40,"max_x":40,"max_z":40,"base_height":300}],"islands":[]}}
            """;
        var intent = new MapIntent
        {
            Teams = [new TeamDef { Id = "red", Color = "red" }],
            Spawns = [new SpawnIntent { Team = "red", Point = new Pt(10, 1, 0), Yaw = 0 }],
            Wools = [],
            Observer = new ObserverIntent { Point = new Pt(0, 20, 0), Yaw = 0 },
            Meta = new MetaIntent { Name = "Tall", Authors = [new AuthorIntent { Name = "alice" }] },
        };

        var built = SketchWorldBuilder.Build(tall, intent);   // must not throw
        // Floor clamped to MaxHeight - RoofLayer - 1 = 247; the 2×2 wool marker sits there.
        await Assert.That(built.World.GetBlock(10, 247, 0).Id).IsEqualTo(Blocks.Wool);
    }

    [Test]
    public async Task No_observer_stands_the_world_spawn_on_real_terrain_not_the_void()
    {
        // An off-origin footprint with no observer: the world spawn must land on an actual terrain column,
        // not float at (0, y, 0) over the void.
        const string offset =
            """
            {"setup":{"mirror_mode":"rot_180","center":{"cx":0,"cz":0}},"layout":{"shapes":[{"id":"a","type":"rectangle","operation":"add","min_x":10,"min_z":10,"max_x":35,"max_z":35,"base_height":5}],"islands":[]}}
            """;
        var intent = new MapIntent
        {
            Teams = [new TeamDef { Id = "red", Color = "red" }],
            Spawns = [new SpawnIntent { Team = "red", Point = new Pt(20, 1, 20), Yaw = 0 }],
            Wools = [],
            Observer = null,
            Meta = new MetaIntent { Name = "NoObs", Authors = [new AuthorIntent { Name = "alice" }] },
        };

        var built = SketchWorldBuilder.Build(offset, intent);

        await Assert.That(built.World.GetBlock(0, 0, 0).Id).IsEqualTo(Blocks.Air);                 // origin is off-island
        await Assert.That((built.SpawnX, built.SpawnZ)).IsNotEqualTo((0, 0));                       // not the naive origin
        await Assert.That(built.World.GetBlock(built.SpawnX, 0, built.SpawnZ).Id).IsEqualTo(Blocks.Bedrock);  // real column
    }

    [Test]
    public async Task A_build_area_over_the_void_is_outlined_in_redstone_at_y_one()
    {
        // A buildable platform clear of the terrain (which ends at x=40): the outline is the whole marker, and
        // the export path is what has to carry it — the geometry itself is BuildMarkerStamper's.
        var intent = SampleIntent();
        var built = SketchWorldBuilder.Build(Layout, new MapIntent
        {
            Teams = intent.Teams, Spawns = intent.Spawns, Wools = intent.Wools,
            Observer = intent.Observer, Meta = intent.Meta,
            Build = new BuildIntent { MaxHeight = 40, Areas = [new Rect(50, 0, 58, 8)] },
        });

        // Ringed two blocks out on the west side, with the air gap and the platform itself left empty.
        await Assert.That(built.World.GetBlock(48, 1, 0)).IsEqualTo((Blocks.RedstoneWire, 0));
        await Assert.That(built.World.GetBlock(48, 1, -2)).IsEqualTo((Blocks.RedstoneWire, 0));   // the corner turn
        await Assert.That(built.World.GetBlock(49, 1, 0).Id).IsEqualTo(Blocks.Air);
        await Assert.That(built.World.GetBlock(54, 1, 4).Id).IsEqualTo(Blocks.Air);
    }

    // ── provenance (B133) ────────────────────────────────────────────────────────────────────────────
    // "A block does not know what placed it" — these prove the build itself records the answer instead,
    // against the exact fault the bug report names: a stamped room reads Structure whatever it is built
    // from, and terrain the painter finishes in a built-looking material stays Ground because nothing after
    // the rasterizer claimed it.

    [Test]
    public async Task A_wool_rooms_footprint_is_claimed_structure_regardless_of_what_it_is_built_from()
    {
        var built = SketchWorldBuilder.Build(Layout, SampleIntent());

        // The default 10×10 shell centred on the (snapped) red wool spawn (-10, 10) — well inside its frame.
        await Assert.That(built.Provenance.LayerAt(-10, 10)).IsEqualTo(ProvenanceLayer.Structure);
        await Assert.That(built.Provenance.LayerAt(20, 0)).IsEqualTo(ProvenanceLayer.Structure);   // blue's spawn cube
    }

    [Test]
    public async Task Plain_terrain_the_painter_finishes_in_a_built_looking_material_stays_ground()
    {
        // A map-wide theme whose surface is stone brick (98) — exactly the material RenderCategories reads
        // as Structure by default (RenderCategoriesTests), and exactly what a town terrace or a mesa hull is
        // painted in on Ashen Quarry. Nothing stamps anything here: the whole board is one flat plaza.
        var themedLayout = new SketchLayout
        {
            Setup = new SketchSetup { MirrorMode = "rot_180", Center = new SketchCenter { Cx = 0, Cz = 0 } },
            Layout = new SketchShapes
            {
                Shapes = [new SketchShape
                {
                    Id = "a", Type = "rectangle", Operation = "add",
                    MinX = -40, MinZ = -40, MaxX = 40, MaxZ = 40, BaseHeight = 4,
                }],
                Islands = [],
            },
            Themes = new Dictionary<string, JsonElement>
            {
                ["map"] = JsonSerializer.Deserialize<JsonElement>(
                    TerrainThemeJson.Serialize(TerrainTheme.Default with { Surface = new TopBand(new SolidMaterial(98), 1) })),
            },
            MapTheme = "map",
        }.ToJson();

        var intent = new MapIntent
        {
            Teams = [new TeamDef { Id = "red", Color = "red" }],
            Spawns = [new SpawnIntent { Team = "red", Point = new Pt(30, 4, -30), Yaw = 0 }],
            Wools = [],
            Observer = new ObserverIntent { Point = new Pt(0, 20, 0), Yaw = 0 },
            Meta = new MetaIntent { Name = "Plaza", Authors = [] },
        };
        var built = SketchWorldBuilder.Build(themedLayout, intent);

        // The material alone reads Structure — proving the theme actually painted the built-looking block.
        // Surface top is y=4 (base_height 4), the one-block bedrock floor takes y=0, so the painted surface
        // course — the one this theme names stone brick — is the topmost solid block, y=3.
        var (blockId, _) = built.World.GetBlock(-20, 3, 20);
        await Assert.That(blockId).IsEqualTo(98);
        await Assert.That(RenderCategories.Of(blockId)).IsEqualTo(RenderCategory.Structure);

        // The recorded provenance disagrees, correctly: nothing stamped this column, so it is Ground.
        await Assert.That(built.Provenance.LayerAt(-20, 20)).IsEqualTo(ProvenanceLayer.Ground);

        // Well away from the spawn cube it stamped, which IS Structure.
        await Assert.That(built.Provenance.LayerAt(30, -30)).IsEqualTo(ProvenanceLayer.Structure);
    }

    // ── provenance owner (B139) ──────────────────────────────────────────────────────────────────────
    // "A claim wants an owner" — every stamp that claims Structure names which thing it is claiming for,
    // so a build with several stamped things records several distinct owners rather than one flat layer.

    [Test]
    public async Task Two_different_wool_rooms_claim_two_different_owners()
    {
        var built = SketchWorldBuilder.Build(Layout, SampleIntent());

        var redOwner = built.Provenance.OwnerAt(-10, 10);    // red wool room
        var blueOwner = built.Provenance.OwnerAt(10, 10);    // blue wool room

        await Assert.That(redOwner).IsNotNull();
        await Assert.That(redOwner).IsNotEqualTo(WorldProvenance.NoOwner);
        await Assert.That(redOwner).IsNotEqualTo(blueOwner);
    }

    [Test]
    public async Task A_wool_room_and_a_spawn_cube_claim_different_owners_even_though_both_are_structure()
    {
        var built = SketchWorldBuilder.Build(Layout, SampleIntent());

        var woolOwner = built.Provenance.OwnerAt(-10, 10);   // red wool room
        var spawnOwner = built.Provenance.OwnerAt(20, 0);    // blue's spawn cube

        await Assert.That(built.Provenance.LayerAt(-10, 10)).IsEqualTo(ProvenanceLayer.Structure);
        await Assert.That(built.Provenance.LayerAt(20, 0)).IsEqualTo(ProvenanceLayer.Structure);
        await Assert.That(woolOwner).IsNotEqualTo(spawnOwner);
    }

    [Test]
    public async Task Every_column_of_a_ground_claim_shares_the_no_owner_reading()
    {
        var built = SketchWorldBuilder.Build(Layout, SampleIntent());
        // Terrain far from every stamp: still plain rasterized ground, so its owner is the shared "nothing
        // identified" reading rather than a per-column identity nobody assigned it.
        await Assert.That(built.Provenance.LayerAt(0, -30)).IsEqualTo(ProvenanceLayer.Ground);
        await Assert.That(built.Provenance.OwnerAt(0, -30)).IsEqualTo(WorldProvenance.NoOwner);
    }

    [Test]
    public async Task Every_slice_of_the_authored_intent_survives_the_resolve()
    {
        // The resolve rebuilds the intent to hand back snapped spawns and derived monuments. It named its
        // fields once, and a named rebuild drops whatever was added to the record after it was written —
        // WaterLanes was, so a lane an author stored reached the resolve and never left it, and the export
        // clears the lane region before rewriting from this copy. Asserted over the whole record rather than
        // over that one field, so the next slice added cannot go the same way unnoticed.
        var intent = SampleIntent() with
        {
            WaterLanes = new WaterLaneIntent { Rects = [new Rect(-5, -5, 5, 5)] },
        };

        var built = SketchWorldBuilder.Build(Layout, intent);

        await Assert.That(built.ResolvedIntent.WaterLanes).IsNotNull();
        await Assert.That(built.ResolvedIntent.WaterLanes!.Rects.Count).IsEqualTo(1);
    }

    // ── a claim covers what the stamp wrote and no more (B203) ───────────────────────────────────────
    /// <summary>A plateau and a single room floor, with no spawn or wool over it, so the only Structure claim
    /// on the board is the foundation's own. <b>Probed at y=2</b>, not y=0: the world carries a bedrock floor
    /// at y=0 everywhere, so the bottom course cannot tell a foundation from open ground. At y=2 a filled
    /// column is bedrock and a natural one is the plateau's dirt.</summary>
    private static SketchWorld RoomFloorOnly()
    {
        const string plateau =
            """
            {"setup":{"mirror_mode":"none","center":{"cx":0,"cz":0}},"layout":{"shapes":[{"id":"a","type":"rectangle","operation":"add","min_x":-40,"min_z":-40,"max_x":40,"max_z":40,"base_height":5}],"islands":[]}}
            """;
        return SketchWorldBuilder.Build(plateau, new MapIntent
        {
            Teams = [new TeamDef { Id = "red", Color = "red" }],
            Observer = new ObserverIntent { Point = new Pt(0, 20, 0), Yaw = 0 },
            Meta = new MetaIntent { Name = "Test", Authors = [new AuthorIntent { Name = "alice" }] },
            Structures = new StructureIntent { RoomFloors = [new Rect(-16, 4, -4, 16)] },
        });
    }

    [Test]
    public async Task A_room_floors_claim_stops_where_its_bedrock_does()
    {
        // The stamp's footprint is max-EXCLUSIVE — StampFoundation fills [minX, maxX) — and ClaimRect's is
        // max-inclusive, so a rect carried across by hand claimed one column past the bedrock on each axis.
        // Live on every wool room, and the same fault B202 fixed for a house: a Structure claim over ground
        // the pass never wrote.
        var built = RoomFloorOnly();

        // The last column inside the footprint carries the foundation and the claim that names it.
        await Assert.That(built.World.GetBlock(-5, 2, 15).Id).IsEqualTo(Blocks.Bedrock);
        await Assert.That(built.Provenance.OwnerAt(-5, 15)).IsEqualTo("roomfloor:0");

        // One column further on either axis is past the max-exclusive bound: no foundation, and no claim.
        foreach (var (x, z) in new[] { (-4, 15), (-5, 16), (-4, 16) })
        {
            await Assert.That((x, z, built.World.GetBlock(x, 2, z).Id)).IsNotEqualTo((x, z, Blocks.Bedrock));
            await Assert.That((x, z, built.Provenance.OwnerAt(x, z))).IsNotEqualTo((x, z, "roomfloor:0"));
        }
    }

    [Test]
    public async Task A_room_floors_claim_names_only_columns_its_foundation_filled()
    {
        // The invariant behind the entry, over the whole footprint rather than at a corner: every column the
        // claim names carries the foundation, and there are exactly as many as the stamp filled. A rect one
        // wider on either axis fails this along the whole edge — 169 claimed against 144 filled.
        var built = RoomFloorOnly();

        var claimed = 0;
        for (var x = -25; x <= 5; x++)
        for (var z = -5; z <= 25; z++)
        {
            if (built.Provenance.OwnerAt(x, z) != "roomfloor:0") continue;
            claimed++;
            await Assert.That((x, z, built.World.GetBlock(x, 2, z).Id)).IsEqualTo((x, z, Blocks.Bedrock));
        }

        await Assert.That(claimed).IsEqualTo(12 * 12);   // [-16,-4) x [4,16), max-exclusive on both axes
    }
}
