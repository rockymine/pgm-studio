using PgmStudio.Domain;
using PgmStudio.Geom;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Dressing;
using PgmStudio.Minecraft.Stamping;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Vocabulary;

namespace PgmStudio.Export.Tests;

/// <summary>
/// The views a built board suggests. What is held is what makes a suggestion worth drawing: every team's spawn
/// and objective is shown and a prop once for its orbit, the view out of a spawn stands outside it on the
/// heading players arrive facing and looks on down it, and the view of the whole board stands off its long
/// side, above its highest ground, facing its middle.
/// </summary>
public sealed class WorldViewsTests
{
    private const int Top = 20;

    /// <summary>A board 81 wide and 41 deep, flat at <see cref="Top"/> but for one column raised above it.</summary>
    private static Dictionary<(int X, int Z), int> Ground()
    {
        var ground = new Dictionary<(int X, int Z), int>();
        for (var x = -40; x <= 40; x++)
            for (var z = -20; z <= 20; z++)
                ground[(x, z)] = Top;
        ground[(5, 5)] = Top + 6;
        return ground;
    }

    private static SpawnIntent Spawn(string team, double minX, double minZ, double yaw) => new()
    {
        Team = team,
        Point = new Pt(minX + 3, Top + 1, minZ + 3),
        Footprint = new Rect(minX, minZ, minX + 6, minZ + 6),
        Yaw = yaw,
    };

    private static PlacementClaim House(int image, int x) =>
        new(new StampId(PropKinds.House, "house-1", image), ProvenancePass.Structure, [(x, 0), (x + 1, 0)]);

    private static BuiltWorld Built(MapIntent intent, params PlacementClaim[] placed) =>
        new(new VoxelWorld(), 0, Top + 1, 0, intent, new WorldProvenance(), RoomShells.BuiltIn,
            Dressing: new DressingPlacement(Claimed: placed), Ground: Ground());

    [Test]
    public async Task Every_team_s_spawn_and_objective_is_suggested_and_a_prop_once_for_its_orbit()
    {
        var intent = new MapIntent
        {
            Teams = [new TeamDef { Id = "red", Name = "Red" }, new TeamDef { Id = "blue", Name = "Blue" }],
            Spawns = [Spawn("red", -30, -3, 270), Spawn("blue", 24, -3, 90)],
            Destroyables =
            [
                new DestroyableIntent { Owner = "red", Anchor = new Pt(-10, Top, 4) },
                new DestroyableIntent { Owner = "blue", Anchor = new Pt(10, Top, -4) },
            ],
        };

        var views = WorldViews.Suggested(Built(intent, House(0, -12), House(1, 12)));

        await Assert.That(views.Count(view => view.Name.EndsWith(" spawn", StringComparison.Ordinal))).IsEqualTo(4);
        await Assert.That(views.Count(view => view.Id.StartsWith("destroyable-", StringComparison.Ordinal))).IsEqualTo(2);
        await Assert.That(views.Count(view => view.Id.StartsWith("house-", StringComparison.Ordinal))).IsEqualTo(1);
        await Assert.That(views.Select(view => view.Id).Distinct().Count()).IsEqualTo(views.Count);
    }

    [Test]
    public async Task The_view_out_of_a_spawn_stands_past_its_wall_on_the_heading_players_arrive_facing()
    {
        var intent = new MapIntent { Spawns = [Spawn("red", -30, -3, 270)] };

        var door = WorldViews.Suggested(Built(intent)).Single(view => view.Id == "spawn-0-out");

        await Assert.That(door.FromX!.Value).IsGreaterThan(-24);
        await Assert.That(door.FromZ).IsEqualTo(0);
        await Assert.That(door.LookX).IsGreaterThan(door.FromX.Value);
        await Assert.That(door.LookZ).IsEqualTo(door.FromZ);
    }

    [Test]
    public async Task The_whole_board_is_seen_from_off_its_long_side_above_its_highest_ground()
    {
        var overview = WorldViews.Suggested(Built(new MapIntent())).Single(view => view.Id == "overview");

        await Assert.That((overview.LookX, overview.LookZ)).IsEqualTo((0, 0));
        await Assert.That(overview.FromX).IsEqualTo(0);
        await Assert.That(overview.FromZ!.Value).IsGreaterThan(20);
        await Assert.That(overview.Y!.Value).IsGreaterThan(Top + 6);
    }

    /// <summary>A wool room eight by eleven on the board's ground: walls six high round its edge, a roof over
    /// the whole of it, and — where <paramref name="marked"/> — a marker three wide floating over its middle,
    /// as the built-in room carries one.</summary>
    private static BuiltWorld WoolRoom(bool marked)
    {
        var world = new VoxelWorld();
        for (var x = 10; x <= 17; x++)
            for (var z = -5; z <= 5; z++)
            {
                var edge = x is 10 or 17 || z is -5 or 5;
                for (var y = Top + 1; y <= Top + 6; y++)
                    if (edge) world.SetBlock(x, y, z, 5);
                world.SetBlock(x, Top + 7, z, 5);
            }
        if (marked)
            for (var x = 12; x <= 14; x++)
                for (var z = -1; z <= 1; z++)
                    for (var y = Top + 26; y <= Top + 28; y++)
                        world.SetBlock(x, y, z, 35, 14);
        var intent = new MapIntent { Wools = [new WoolIntent { Owner = "red", Footprint = new Rect(10, -5, 17, 5) }] };
        return new BuiltWorld(world, 0, Top + 1, 0, intent, new WorldProvenance(), RoomShells.BuiltIn, Ground: Ground());
    }

    [Test]
    public async Task A_marker_floating_over_a_room_does_not_move_the_view_of_it()
    {
        var bare = WorldViews.Suggested(WoolRoom(marked: false)).Single(view => view.Id == "wool-0");
        var marked = WorldViews.Suggested(WoolRoom(marked: true)).Single(view => view.Id == "wool-0");

        await Assert.That(marked).IsEqualTo(bare);
        await Assert.That(bare.Pitch!.Value).IsGreaterThan(-15.0);
    }

    /// <summary>A monument three wide and three tall floating four blocks over the board at (−20, 0), built in
    /// the box the intent carries. Where <paramref name="marked"/> its sky marker hangs twenty-two blocks
    /// over it; where <paramref name="walled"/> a ring of rock eight high stands round it five blocks out, so
    /// no eye on the ground sees in.</summary>
    private static BuiltWorld Monument(bool marked, bool walled = false)
    {
        var world = new VoxelWorld();
        var box = new BlockBox(-21, Top + 5, -1, -19, Top + 7, 1);
        for (var x = box.MinX; x <= box.MaxX; x++)
            for (var z = box.MinZ; z <= box.MaxZ; z++)
                for (var y = box.MinY; y <= box.MaxY; y++)
                    world.SetBlock(x, y, z, 49);
        if (marked)
            for (var y = Top + 29; y <= Top + 31; y++)
                world.SetBlock(-20, y, 0, 35, 14);
        if (walled)
            for (var x = -27; x <= -13; x++)
                for (var z = -7; z <= 7; z++)
                    if (Math.Max(Math.Abs(x + 20), Math.Abs(z)) is 6 or 7)
                        for (var y = Top + 1; y <= Top + 8; y++)
                            world.SetBlock(x, y, z, 1);
        var intent = new MapIntent
        {
            Destroyables = [new DestroyableIntent { Owner = "red", Anchor = new Pt(-20, Top, 0), Box = box }],
        };
        return new BuiltWorld(world, 0, Top + 1, 0, intent, new WorldProvenance(), RoomShells.BuiltIn, Ground: Ground());
    }

    [Test]
    public async Task A_goal_is_framed_on_the_box_it_was_built_in_and_not_on_its_sky_marker()
    {
        var bare = WorldViews.Suggested(Monument(marked: false)).Single(view => view.Id == "destroyable-0");
        var marked = WorldViews.Suggested(Monument(marked: true)).Single(view => view.Id == "destroyable-0");

        await Assert.That(marked).IsEqualTo(bare);
        await Assert.That(marked.FromX).IsNotNull();
        // Level with the monument's body, not tipped up at a marker twenty-two blocks over it.
        await Assert.That(Math.Abs(marked.Pitch!.Value)).IsLessThan(15.0);
    }

    [Test]
    public async Task A_goal_no_stand_on_the_ground_sees_is_looked_down_on_from_the_air()
    {
        var pit = WorldViews.Suggested(Monument(marked: true, walled: true)).Single(view => view.Id == "destroyable-0");

        await Assert.That(pit.FromX).IsNotNull();
        await Assert.That(pit.Y!.Value).IsGreaterThan(Top + 8.0);
        await Assert.That(pit.Pitch!.Value).IsGreaterThan(0.0);
    }

    [Test]
    public async Task A_view_is_drawn_by_the_query_words_it_states()
    {
        await Assert.That(new WorldView("v", "A", 3, -4).Query).IsEqualTo("look=3,-4");
        await Assert.That(new WorldView("v", "A", 3, -4, 10, 12, 71.5).Query).IsEqualTo("look=3,-4&from=10,12&y=71.5");
    }
}
