using PgmStudio.Domain;
using PgmStudio.Geom;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Tests.Authoring;

/// <summary>
/// Moving a play piece back from the sketch, by its id. Three rules carry the weight. <b>A room's region is
/// the ground</b>, so moving it carries what is seated on it. <b>A piece moves and does not resize</b>, since a
/// marker is a fraction of its rectangle and a goal is centred on its anchor. And <b>the orbit moves
/// together</b>: every image of the authored unit moves by the displacement turned through the step between
/// the two images, so a mirrored board stays mirrored.
/// </summary>
public sealed class PlayPieceMoveTests
{
    private static MapIntent Board() => new()
    {
        Spawns =
        [
            new SpawnIntent
            {
                Id = "spawn-red", Team = "red",
                Point = new Pt(5, 12, 5),
                Protection = [new Rect(0, 0, 10, 10)],
                House = RoomHouse.Over(new Rect(2, 2, 8, 8)),
                Iron = [new Pt(1, 12, 1)],
            },
        ],
        Wools =
        [
            new WoolIntent
            {
                Id = "wool-red-blue", Owner = "red", Color = "blue",
                Spawn = new Pt(25, 12, 5),
                Protection = [new Rect(20, 0, 30, 10)],
                House = RoomHouse.Over(new Rect(22, 2, 28, 8)),
                Entries = [new Rect(20, 4, 20, 6)],
            },
        ],
        Destroyables = [new DestroyableIntent { Id = "destroyable-red", Owner = "red", Style = "cube-3", Anchor = new Pt(40, 12, 5) }],
        Cores = [new CoreIntent { Id = "core-red", Owner = "red", Anchor = new Pt(60, 12, 5) }],
    };

    /// <summary>Red's spawn and its mirror across <c>x = 50</c>, both images of the authored unit
    /// <c>spawn:home</c>.</summary>
    private static MapIntent Mirrored(string mode = "mirror_x") => new()
    {
        Symmetry = new SymmetryIntent { Mode = mode, CenterX = 50, CenterZ = 50 },
        Spawns =
        [
            new SpawnIntent
            {
                Id = "spawn-red", Team = "red", Stamp = new StampId("spawn", "home", 0),
                Point = new Pt(5, 12, 5), Protection = [new Rect(0, 0, 10, 10)], House = RoomHouse.Over(new Rect(2, 2, 8, 8)),
            },
            new SpawnIntent
            {
                Id = "spawn-blue", Team = "blue", Stamp = new StampId("spawn", "home", 1),
                Point = new Pt(95, 12, 5), Protection = [new Rect(90, 0, 100, 10)], House = RoomHouse.Over(new Rect(92, 2, 98, 8)),
            },
        ],
    };

    private static SpawnIntent Spawn(MapIntent intent, int at = 0) => intent.Spawns[at];
    private static WoolIntent Wool(MapIntent intent) => intent.Wools![0];

    [Test]
    public async Task Moving_a_spawns_region_carries_the_room_seated_on_it()
    {
        var (moved, refused) = PlayPieceMove.Apply(Board(), "spawn-red", null, new Rect(100, 50, 110, 60));

        await Assert.That(refused.Count).IsEqualTo(0);
        var spawn = Spawn(moved!);
        await Assert.That(spawn.Protection.Single()).IsEqualTo(new Rect(100, 50, 110, 60));
        // Everything on that ground travelled the same hundred and fifty blocks.
        await Assert.That(spawn.Point.X).IsEqualTo(105);
        await Assert.That(spawn.Point.Z).IsEqualTo(55);
        await Assert.That(spawn.House?.Room).IsEqualTo(new Rect(102, 52, 108, 58));
        await Assert.That(spawn.Iron.Single().X).IsEqualTo(101);
        await Assert.That(spawn.Iron.Single().Z).IsEqualTo(51);
    }

    /// <summary>What a marker stands on is measured by the world build against the terrain it laid, so a Y
    /// carried here would be a second answer to a question already settled.</summary>
    [Test]
    public async Task A_moved_marker_keeps_the_height_it_was_stated_at()
    {
        var (moved, _) = PlayPieceMove.Apply(Board(), "spawn-red", null, new Rect(100, 50, 110, 60));

        await Assert.That(Spawn(moved!).Point.Y).IsEqualTo(12);
    }

    [Test]
    public async Task Moving_a_wools_region_carries_its_marker_footprint_and_entries()
    {
        var (moved, refused) = PlayPieceMove.Apply(Board(), "wool-red-blue", null, new Rect(20, 40, 30, 50));

        await Assert.That(refused.Count).IsEqualTo(0);
        var wool = Wool(moved!);
        await Assert.That(wool.Protection.Single()).IsEqualTo(new Rect(20, 40, 30, 50));
        await Assert.That(wool.Spawn.Z).IsEqualTo(45);
        await Assert.That(wool.House?.Room).IsEqualTo(new Rect(22, 42, 28, 48));
        await Assert.That(wool.Entries.Single()).IsEqualTo(new Rect(20, 44, 20, 46));
    }

    [Test]
    public async Task Moving_a_building_moves_the_house_and_nothing_else()
    {
        var (moved, refused) = PlayPieceMove.Apply(Board(), "spawn-red", StructuralRoles.Building, new Rect(1, 1, 7, 7));

        await Assert.That(refused.Count).IsEqualTo(0);
        var spawn = Spawn(moved!);
        await Assert.That(spawn.House?.Room).IsEqualTo(new Rect(1, 1, 7, 7));
        await Assert.That(spawn.Protection.Single()).IsEqualTo(new Rect(0, 0, 10, 10));
        await Assert.That(spawn.Point.X).IsEqualTo(5);
    }

    [Test]
    public async Task Dragging_a_joined_wing_moves_the_whole_house()
    {
        var board = Board();
        var house = new RoomHouse { Wings = [.. RoomHouse.Over(new Rect(2, 2, 6, 8)).Wings, .. RoomHouse.Over(new Rect(6, 4, 9, 8)).Wings] };
        board = board with { Spawns = [Spawn(board) with { House = house }] };

        var (moved, refused) = PlayPieceMove.Apply(board, "spawn-red", StructuralRoles.Building, new Rect(5, 3, 8, 7), wing: 1);

        await Assert.That(refused.Count).IsEqualTo(0);
        await Assert.That(Spawn(moved!).House!.Rects()).IsEquivalentTo(new[] { new Rect(1, 1, 5, 7), new Rect(5, 3, 8, 7) })
            .Because("both wings shift by the one wing's move");
        await Assert.That(PlayPieceMove.Apply(board, "spawn-red", StructuralRoles.Building, new Rect(5, 3, 8, 7), wing: 2).Refused.Count)
            .IsGreaterThan(0).Because("the house has no third wing");
    }

    [Test]
    public async Task A_building_carried_off_its_region_is_refused()
    {
        var (moved, refused) = PlayPieceMove.Apply(Board(), "spawn-red", StructuralRoles.Building, new Rect(6, 6, 12, 12));

        await Assert.That(moved).IsNull();
        await Assert.That(refused.Single().Rule).IsEqualTo(RoomFrameRules.FootprintOffPiece);
    }

    /// <summary>A goal's one rectangle is its structure, from its first block's edge to past its last: a
    /// <c>cube-3</c> on the anchor <c>(40, 5)</c> covers blocks 39–41, so it stands at 39 to 42.</summary>
    [Test]
    public async Task Moving_a_destroyable_moves_its_anchor_by_whole_blocks()
    {
        var board = Board();
        board = board with
        {
            Destroyables = [board.Destroyables![0] with { Box = new BlockBox(39, 14, 4, 41, 16, 6) }],
        };
        var (moved, refused) = PlayPieceMove.Apply(board, "destroyable-red", null, new Rect(49, 14, 52, 17));

        await Assert.That(refused.Count).IsEqualTo(0);
        var goal = moved!.Destroyables![0];
        await Assert.That(goal.Anchor).IsEqualTo(new Pt(50, 12, 15));
        // The resolved box was measured over the ground it stood on, so it is resolved again over the new.
        await Assert.That(goal.Box).IsNull();
    }

    [Test]
    public async Task Moving_a_core_moves_its_anchor()
    {
        var board = Board();
        var size = board.Cores![0].Size;
        var (minX, minZ, _, _) = ObjectiveFootprint.Centred(60, 5, size, size);
        var (moved, refused) = PlayPieceMove.Apply(
            board, "core-red", null, new Rect(minX + 3, minZ - 2, minX + 3 + size, minZ - 2 + size));

        await Assert.That(refused.Count).IsEqualTo(0);
        await Assert.That(moved!.Cores![0].Anchor).IsEqualTo(new Pt(63, 12, 3));
    }

    [Test]
    public async Task An_objective_has_no_building()
    {
        var (moved, refused) = PlayPieceMove.Apply(Board(), "destroyable-red", StructuralRoles.Building, new Rect(39, 4, 42, 7));

        await Assert.That(moved).IsNull();
        await Assert.That(refused.Single().Field).IsEqualTo("part");
    }

    [Test]
    [Arguments("spawn-red", null, 0, 0, 12, 10)]                         // wider
    [Arguments("spawn-red", null, 0, 0, 10, 8)]                          // shallower
    [Arguments("spawn-red", StructuralRoles.Building, 2, 2, 9, 8)]       // the building too
    [Arguments("destroyable-red", null, 39, 4, 43, 7)]                   // and a goal
    public async Task A_move_that_resizes_is_refused(string id, string? part, int minX, int minZ, int maxX, int maxZ)
    {
        var (moved, refused) = PlayPieceMove.Apply(Board(), id, part, new Rect(minX, minZ, maxX, maxZ));

        await Assert.That(moved).IsNull();
        await Assert.That(refused.Single().Rule).IsEqualTo(RequestRules.Unreadable);
        await Assert.That(refused.Single().Message).Contains("of the rectangle it replaces");
    }

    [Test]
    public async Task A_region_of_several_rectangles_has_no_rectangle_to_move()
    {
        var board = Board();
        var spawn = board.Spawns[0] with { Protection = [new Rect(0, 0, 10, 10), new Rect(10, 0, 20, 10)] };
        var (moved, refused) = PlayPieceMove.Apply(board with { Spawns = [spawn] }, "spawn-red", null, new Rect(100, 50, 110, 60));

        await Assert.That(moved).IsNull();
        await Assert.That(refused.Single().Field).IsEqualTo("protection");
    }

    [Test]
    public async Task An_id_naming_no_piece_is_a_missing_subject()
    {
        var (moved, refused) = PlayPieceMove.Apply(Board(), "spawn-green", null, new Rect(0, 0, 10, 10));

        await Assert.That(moved).IsNull();
        await Assert.That(refused.Single().Rule).IsEqualTo(RequestRules.NoSuchSubject);
        await Assert.That(refused.Single().SubjectIds.Single()).IsEqualTo("spawn-green");
    }

    [Test]
    [Arguments("spawn-red", StructuralRoles.WoolRoom)]
    [Arguments("spawn-red", "lane")]
    [Arguments("wool-red-blue", StructuralRoles.Spawn)]
    [Arguments("core-red", StructuralRoles.Spawn)]
    public async Task A_part_the_piece_does_not_have_is_refused(string id, string part)
    {
        var (moved, refused) = PlayPieceMove.Apply(Board(), id, part, new Rect(0, 0, 10, 10));

        await Assert.That(moved).IsNull();
        await Assert.That(refused.Single().Field).IsEqualTo("part");
    }

    /// <summary>A room's own region word names the region, so a client may send the role of the shape it
    /// dragged.</summary>
    [Test]
    public async Task A_rooms_region_word_moves_the_region()
    {
        var (moved, refused) = PlayPieceMove.Apply(Board(), "spawn-red", StructuralRoles.Spawn, new Rect(100, 50, 110, 60));

        await Assert.That(refused.Count).IsEqualTo(0);
        await Assert.That(Spawn(moved!).Protection.Single()).IsEqualTo(new Rect(100, 50, 110, 60));
    }

    [Test]
    public async Task Moving_one_piece_leaves_every_other_piece_where_it_was()
    {
        var board = Board();
        var (moved, _) = PlayPieceMove.Apply(board, "spawn-red", null, new Rect(100, 50, 110, 60));

        await Assert.That(Wool(moved!)).IsEqualTo(Wool(board));
        await Assert.That(moved!.Destroyables![0]).IsEqualTo(board.Destroyables![0]);
    }

    /// <summary>Red's room moved five blocks east and three south; across a mirror in x, blue's moves five
    /// west and three south, so the two stay each other's reflection.</summary>
    [Test]
    public async Task A_mirror_image_moves_by_the_reflected_displacement()
    {
        var (moved, refused) = PlayPieceMove.Apply(Mirrored(), "spawn-red", null, new Rect(5, 3, 15, 13));

        await Assert.That(refused.Count).IsEqualTo(0);
        var blue = Spawn(moved!, 1);
        await Assert.That(blue.Protection.Single()).IsEqualTo(new Rect(85, 3, 95, 13));
        await Assert.That(blue.Point).IsEqualTo(new Pt(90, 12, 8));
        await Assert.That(blue.House?.Room).IsEqualTo(new Rect(87, 5, 93, 11));
    }

    /// <summary>Under a half turn about <c>(50, 50)</c> the image's displacement is the authored one
    /// reversed on both axes, whichever of the two images the drag was on.</summary>
    [Test]
    public async Task A_half_turn_image_moves_by_the_reversed_displacement()
    {
        var board = Mirrored("rot_180");
        board = board with
        {
            Spawns =
            [
                board.Spawns[0],
                board.Spawns[1] with { Point = new Pt(95, 12, 95), Protection = [new Rect(90, 90, 100, 100)], House = RoomHouse.Over(new Rect(92, 92, 98, 98)) },
            ],
        };
        var (moved, refused) = PlayPieceMove.Apply(board, "spawn-blue", null, new Rect(88, 91, 98, 101));

        await Assert.That(refused.Count).IsEqualTo(0);
        await Assert.That(Spawn(moved!, 0).Protection.Single()).IsEqualTo(new Rect(2, -1, 12, 9));
    }

    /// <summary>A building is held inside its own region on every image the orbit carries, not only on the
    /// one the request named: an image hand-corrected to stand nearer its edge refuses the whole move.</summary>
    [Test]
    public async Task A_building_carried_off_its_region_on_an_image_refuses_the_move()
    {
        var board = Mirrored();
        board = board with { Spawns = [board.Spawns[0], board.Spawns[1] with { House = RoomHouse.Over(new Rect(90, 2, 96, 8)) }] };

        var (moved, refused) = PlayPieceMove.Apply(board, "spawn-red", StructuralRoles.Building, new Rect(4, 2, 10, 8));

        await Assert.That(moved).IsNull();
        await Assert.That(refused.Single().Rule).IsEqualTo(RoomFrameRules.FootprintOffPiece);
        await Assert.That(refused.Single().SubjectIds.Single()).IsEqualTo("spawn-blue");
    }

    /// <summary>A piece stating no stamp is its own authored unit — two spawns written by hand are two
    /// statements — so it moves alone.</summary>
    [Test]
    public async Task A_piece_with_no_stamp_moves_alone()
    {
        var board = Mirrored();
        board = board with { Spawns = [.. board.Spawns.Select(spawn => spawn with { Stamp = default })] };

        var (moved, _) = PlayPieceMove.Apply(board, "spawn-red", null, new Rect(5, 3, 15, 13));

        await Assert.That(Spawn(moved!, 1)).IsEqualTo(board.Spawns[1]);
    }

    // ── turning ─────────────────────────────────────────────────────────────────────────────────────

    private static MapIntent Turnable() => new()
    {
        Spawns =
        [
            new SpawnIntent
            {
                Id = "spawn-red", Team = "red", Point = new Pt(2, 12, 3), Yaw = 180, Doors = ["-z"],
                Protection = [new Rect(0, 0, 10, 6)], House = RoomHouse.Over(new Rect(1, 1, 5, 5)), Iron = [new Pt(9, 12, 5)],
            },
        ],
    };

    /// <summary>A quarter turn clockwise seen from above, about the region's middle (5, 3): the 10 by 6 region
    /// stands 6 by 10, and everything on it turns with it — north becomes east, and a player who faced south
    /// faces west.</summary>
    [Test]
    public async Task A_quarter_turn_turns_the_room_and_everything_on_it()
    {
        var (turned, refused) = PlayPieceMove.Turn(Turnable(), "spawn-red", null, 1);

        await Assert.That(refused.Count).IsEqualTo(0);
        var spawn = Spawn(turned!);
        await Assert.That(spawn.Protection.Single()).IsEqualTo(new Rect(2, -2, 8, 8));
        await Assert.That((spawn.Point.X, spawn.Point.Z)).IsEqualTo((5.0, 0.0));
        await Assert.That(spawn.House?.Room).IsEqualTo(new Rect(3, -1, 7, 3));
        await Assert.That(spawn.Doors).IsEquivalentTo(new[] { "+x" });
        await Assert.That(spawn.Yaw).IsEqualTo(270);
    }

    [Test]
    public async Task Four_quarter_turns_are_no_turn()
    {
        var board = Turnable();
        var turned = board;
        for (var k = 0; k < 4; k++) turned = PlayPieceMove.Turn(turned, "spawn-red", null, 1).Intent!;

        var (was, now) = (Spawn(board), Spawn(turned));
        await Assert.That(now.Protection.Single()).IsEqualTo(was.Protection.Single());
        await Assert.That(now.Point).IsEqualTo(was.Point);
        await Assert.That(now.House?.Room).IsEqualTo(was.House?.Room);
        await Assert.That(now.Doors).IsEquivalentTo(was.Doors);
        await Assert.That(now.Yaw).IsEqualTo(was.Yaw);
    }

    /// <summary>A region whose sides differ by an odd number of blocks has no turn about its middle that stays
    /// on the block grid, so the whole room is shifted the half block that puts it back.</summary>
    [Test]
    public async Task A_turned_region_stays_on_the_block_grid()
    {
        var board = Turnable() with
        {
            Spawns = [Turnable().Spawns[0] with { Protection = [new Rect(0, 0, 5, 2)], House = null, Iron = [] }],
        };
        var region = Spawn(PlayPieceMove.Turn(board, "spawn-red", null, 1).Intent!).Protection.Single();

        await Assert.That(region).IsEqualTo(new Rect(1, -2, 3, 3));
    }

    /// <summary>A mirror image turns the other way, so the two stay each other's reflection: red's north door
    /// turning east is blue's turning west.</summary>
    [Test]
    public async Task A_mirror_image_turns_the_other_way()
    {
        var board = Mirrored();
        board = board with { Spawns = [.. board.Spawns.Select(spawn => spawn with { Doors = ["-z"] })] };

        var turned = PlayPieceMove.Turn(board, "spawn-red", null, 1).Intent!;

        await Assert.That(Spawn(turned, 0).Doors).IsEquivalentTo(new[] { "+x" });
        await Assert.That(Spawn(turned, 1).Doors).IsEquivalentTo(new[] { "-x" });
    }

    [Test]
    public async Task Alone_moves_or_turns_the_named_image_and_no_other()
    {
        var board = Mirrored();

        var moved = PlayPieceMove.Apply(board, "spawn-red", null, new Rect(5, 3, 15, 13), alone: true).Intent!;
        var turned = PlayPieceMove.Turn(board, "spawn-red", null, 1, alone: true).Intent!;

        await Assert.That(Spawn(moved, 1)).IsEqualTo(board.Spawns[1]);
        await Assert.That(Spawn(turned, 1)).IsEqualTo(board.Spawns[1]);
        await Assert.That(Spawn(turned, 0)).IsNotEqualTo(board.Spawns[0]);
    }

    /// <summary>The building turns about its own middle and is held inside its region as a move holds it.</summary>
    [Test]
    public async Task A_building_turned_off_its_region_is_refused()
    {
        var board = Turnable() with
        {
            Spawns = [Turnable().Spawns[0] with { House = RoomHouse.Over(new Rect(1, 0, 9, 4)) }],
        };

        var (turned, refused) = PlayPieceMove.Turn(board, "spawn-red", StructuralRoles.Building, 1);

        await Assert.That(turned).IsNull();
        await Assert.That(refused.Single().Rule).IsEqualTo(RoomFrameRules.FootprintOffPiece);
    }

    [Test]
    public async Task An_objective_states_no_facing_to_turn()
    {
        var (turned, refused) = PlayPieceMove.Turn(Board(), "core-red", null, 1);

        await Assert.That(turned).IsNull();
        await Assert.That(refused.Single().Field).IsEqualTo("turn");
    }

    /// <summary>Two build areas mirrored across <c>x = 50</c>, a lane beside them, an iron cube and a wall
    /// stamped as images of one unit each, and the observer; the intent states no symmetry, as a plan's
    /// compiled one does, so the board's comes from the sketch.</summary>
    private static MapIntent Fanned() => new()
    {
        Build = new BuildIntent { Areas = [new PieceArea(10, 20, 20, 30, "build-1"), new PieceArea(80, 20, 90, 30, "build-2")] },
        WaterLanes = new WaterLaneIntent { Rects = [new PieceArea(30, 0, 34, 8, "water-lane-1")] },
        Structures = new StructureIntent
        {
            IronCubes = [new IronCube(5, 5, true, new StampId("ironcube", "0", 0), "iron-1"),
                         new IronCube(92, 5, true, new StampId("ironcube", "0", 1), "iron-2")],
            Walls = [new WallStructure(41, 10, 41, 18, 20, Stamp: new StampId("wall", "0", 0), Id: "wall-1"),
                     new WallStructure(59, 10, 59, 18, 20, Stamp: new StampId("wall", "0", 1), Id: "wall-2")],
        },
        Observer = new ObserverIntent { Point = new Pt(50.5, 30, 50.5) },
    };

    private static readonly SymmetryIntent MirrorX = new() { Mode = "mirror_x", CenterX = 50, CenterZ = 50 };

    [Test]
    public async Task A_build_area_moves_and_its_image_where_the_board_carries_it()
    {
        var (moved, refused) = PlayPieceMove.Apply(Fanned(), "build-1", null, new Rect(12, 24, 22, 34), board: MirrorX);

        await Assert.That(refused.Count).IsEqualTo(0);
        await Assert.That(moved!.Build!.Areas[0]).IsEqualTo(new PieceArea(12, 24, 22, 34, "build-1"));
        await Assert.That(moved.Build.Areas[1]).IsEqualTo(new PieceArea(78, 24, 88, 34, "build-2"))
            .Because("a mirror image moves by the reflected displacement");
    }

    [Test]
    public async Task With_no_symmetry_anywhere_an_area_moves_alone()
    {
        var (moved, _) = PlayPieceMove.Apply(Fanned(), "build-1", null, new Rect(12, 24, 22, 34));

        await Assert.That(moved!.Build!.Areas[1]).IsEqualTo(new PieceArea(80, 20, 90, 30, "build-2"));
    }

    [Test]
    public async Task An_area_turns_about_its_middle()
    {
        var (turned, _) = PlayPieceMove.Turn(Fanned(), "water-lane-1", null, 1, board: MirrorX);

        await Assert.That(turned!.WaterLanes!.Rects.Single()).IsEqualTo(new PieceArea(28, 2, 36, 6, "water-lane-1"));
    }

    [Test]
    public async Task An_iron_cube_and_a_wall_move_their_orbit_by_whole_blocks()
    {
        var (cubes, _) = PlayPieceMove.Apply(Fanned(), "iron-1", null,
            new Rect(6, 7, 6 + RoomFrames.IronSpan, 7 + RoomFrames.IronSpan), board: MirrorX);
        await Assert.That(cubes!.Structures!.IronCubes.Select(cube => (cube.MinX, cube.MinZ)))
            .IsEquivalentTo(new[] { (6, 7), (91, 7) });

        var (walls, _) = PlayPieceMove.Apply(Fanned(), "wall-1", null, new Rect(41, 12, 43, 20), board: MirrorX);
        await Assert.That(walls!.Structures!.Walls[1]).IsEqualTo(Fanned().Structures!.Walls[1] with
        {
            X1 = 58, Z1 = 12, X2 = 58, Z2 = 20,
        });
    }

    [Test]
    public async Task A_walls_end_moves_alone_and_the_matching_end_of_its_image_follows()
    {
        var (moved, refused) = PlayPieceMove.Apply(Fanned(), "wall-1", PieceParts.End, new Rect(44, 22, 44, 22), board: MirrorX);

        await Assert.That(refused.Count).IsEqualTo(0);
        await Assert.That(moved!.Structures!.Walls[0]).IsEqualTo(Fanned().Structures!.Walls[0] with { X2 = 44, Z2 = 22 });
        await Assert.That(moved.Structures.Walls[1]).IsEqualTo(Fanned().Structures!.Walls[1] with { X2 = 56, Z2 = 22 })
            .Because("the mirror image's end at (59, 18) is the image of the moved one, and moves the reflected way");

        var (start, _) = PlayPieceMove.Apply(Fanned(), "wall-2", PieceParts.Start, new Rect(62, 8, 62, 8), board: MirrorX);
        await Assert.That(start!.Structures!.Walls[0]).IsEqualTo(Fanned().Structures!.Walls[0] with { X1 = 38, Z1 = 8 });

        var (alone, _) = PlayPieceMove.Apply(Fanned(), "wall-1", PieceParts.End, new Rect(44, 22, 44, 22), alone: true, board: MirrorX);
        await Assert.That(alone!.Structures!.Walls[1]).IsEqualTo(Fanned().Structures!.Walls[1]);
    }

    [Test]
    public async Task A_walls_end_is_one_block_corner_apart_from_the_other()
    {
        var (spanned, why) = PlayPieceMove.Apply(Fanned(), "wall-1", PieceParts.End, new Rect(44, 22, 45, 23));
        await Assert.That(spanned).IsNull();
        await Assert.That(why.Single().Field).IsEqualTo("placed");

        var (offGrid, _) = PlayPieceMove.Apply(Fanned(), "wall-1", PieceParts.End, new Rect(44.5, 22, 44.5, 22));
        await Assert.That(offGrid).IsNull();

        var (collapsed, _) = PlayPieceMove.Apply(Fanned(), "wall-1", PieceParts.End, new Rect(41, 10, 41, 10));
        await Assert.That(collapsed).IsNull();

        var (building, part) = PlayPieceMove.Apply(Fanned(), "wall-1", StructuralRoles.Building, new Rect(41, 12, 43, 20));
        await Assert.That(building).IsNull();
        await Assert.That(part.Single().Field).IsEqualTo("part");
    }

    [Test]
    public async Task The_observer_moves_by_its_block()
    {
        var (moved, _) = PlayPieceMove.Apply(Fanned(), PlayIds.Observer, null, new Rect(53, 47, 54, 48));

        await Assert.That(moved!.Observer!.Point).IsEqualTo(new Pt(53.5, 30, 47.5));
    }

    [Test]
    public async Task A_wall_resized_or_turned_is_refused()
    {
        var (resized, why) = PlayPieceMove.Apply(Fanned(), "wall-1", null, new Rect(40, 10, 43, 18));
        await Assert.That(resized).IsNull();
        await Assert.That(why.Single().Field).IsEqualTo("placed");

        var (turned, refused) = PlayPieceMove.Turn(Fanned(), "wall-1", null, 1);
        await Assert.That(turned).IsNull();
        await Assert.That(refused.Single().Field).IsEqualTo("turn");
    }
}
