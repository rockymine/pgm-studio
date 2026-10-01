using PgmStudio.Geom;
using PgmStudio.Geom.Render;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Stamping;
using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Export.Tests;

/// <summary>
/// A board drawn in the round. The isometric cannot see under a roof; the x-ray washes out the ground and the
/// buildings in the way and leaves a made thing or a placed prop standing, so a lamp hanging in a chamber
/// shows through a hillside while it is one of those; and the void scan names a chamber by its own blocks,
/// sealed until something opens it to the sky.
///
/// <para>Driven over one chamber: a stone slab ten blocks square and ten deep on a ground layer, a 4×4×4 room
/// hollowed out of it at x, y and z 3 to 6, and a gold lamp hanging in the room at (4, 5, 4) on a layer of its
/// own.</para>
/// </summary>
public sealed class BoardIsometricTests
{
    private const int Stone = 1, Gold = 41, Scale = 4;

    private static BuiltWorld Chamber(bool lampIsMade = true, Action<VoxelWorld, List<ColumnSegment>, WorldProvenance>? also = null)
    {
        var provenance = new WorldProvenance();
        var world = new VoxelWorld();
        var segments = new List<ColumnSegment>();
        for (var x = 0; x < 10; x++)
            for (var z = 0; z < 10; z++)
            {
                var room = x is >= 3 and <= 6 && z is >= 3 and <= 6;
                for (var y = 0; y <= 9; y++)
                    if (!(room && y is >= 3 and <= 6)) world.SetBlock(x, y, z, Stone);
                if (room)
                {
                    segments.Add(new ColumnSegment(x, z, 0, 3, "ground"));
                    segments.Add(new ColumnSegment(x, z, 7, 10, "ground"));
                }
                else segments.Add(new ColumnSegment(x, z, 0, 10, "ground"));
            }
        world.SetBlock(4, 5, 4, Gold);
        segments.Add(new ColumnSegment(4, 4, 5, 6, "lamp"));
        also?.Invoke(world, segments, provenance);
        return new BuiltWorld(world, 0, 11, 0, new MapIntent(), provenance, RoomShells.BuiltIn,
            Columns: segments, MadeLayers: lampIsMade ? new HashSet<string> { "lamp" } : new HashSet<string>());
    }

    /// <summary>How many pixels of the picture are the gold's own yellow, or near enough to it that a reader
    /// sees gold — what a lamp drawn under one pale skin of veil still is, and a lamp washed out is not.</summary>
    private static int Gilded(byte[] png)
    {
        var image = PngReader.Decode(png);
        var count = 0;
        for (var offset = 0; offset + 3 < image.Rgba.Length; offset += 4)
        {
            int red = image.Rgba[offset], green = image.Rgba[offset + 1], blue = image.Rgba[offset + 2];
            if (red - blue > 80 && green - blue > 70) count++;
        }
        return count;
    }

    [Test]
    public async Task The_isometric_cannot_see_into_a_roofed_room()
    {
        var png = BoardIsometric.Isometric(Chamber(), 0, Scale, "chamber");

        await Assert.That(png).IsNotNull();
        await Assert.That(Gilded(png!)).IsEqualTo(0);
    }

    [Test]
    public async Task The_x_ray_washes_out_the_ground_over_a_room_and_leaves_a_made_thing_in_it()
    {
        var made = BoardIsometric.XRay(Chamber(lampIsMade: true), 0, Scale, "chamber");
        var ground = BoardIsometric.XRay(Chamber(lampIsMade: false), 0, Scale, "chamber");

        await Assert.That(Gilded(made!.Value.Png)).IsGreaterThan(0);
        await Assert.That(Gilded(ground!.Value.Png)).IsEqualTo(0);
    }

    /// <summary>A lamp no layer drew is a placed prop's where a prop claimed its column, and stands; where a
    /// building claimed it, it is the building's, and washes out with the roof it hangs from.</summary>
    [Test]
    public async Task The_x_ray_leaves_a_placed_prop_standing_and_washes_out_a_building()
    {
        static BuiltWorld Hung(ProvenancePass pass) => Chamber(also: (_, segments, provenance) =>
        {
            segments.RemoveAll(segment => segment.Layer == "lamp");
            provenance.Claim(4, 4, pass);
        });

        await Assert.That(Gilded(BoardIsometric.XRay(Hung(ProvenancePass.Prop), 0, Scale, "chamber")!.Value.Png))
            .IsGreaterThan(0);
        await Assert.That(Gilded(BoardIsometric.XRay(Hung(ProvenancePass.Structure), 0, Scale, "chamber")!.Value.Png))
            .IsEqualTo(0);
    }

    [Test]
    public async Task A_sealed_chamber_is_named_by_its_own_blocks()
    {
        var (cavities, blocks) = BoardIsometric.Scan(Chamber());

        await Assert.That(cavities.Count).IsEqualTo(1);
        // Sixty-four blocks of room, one of them the lamp.
        await Assert.That(cavities[0]).IsEqualTo(new BoardIsometric.Cavity(63, (3, 3, 3), (6, 6, 6), Sealed: true));
        await Assert.That(blocks).IsEqualTo(10 * 10 * 10 - 64 + 1);
        await Assert.That(BoardIsometric.VoidText(cavities, blocks)).Contains("SEALED");
    }

    [Test]
    public async Task A_shaft_to_the_sky_opens_the_chamber_and_takes_its_own_column_out_of_it()
    {
        var shafted = Chamber(also: (world, _, _) =>
        {
            for (var y = 7; y <= 9; y++) world.SetBlock(6, y, 6, 0);
        });

        var (cavities, _) = BoardIsometric.Scan(shafted);

        // The shaft's column has nothing over it, so its four blocks of room are open air rather than roofed.
        await Assert.That(cavities.Single()).IsEqualTo(new BoardIsometric.Cavity(59, (3, 3, 3), (6, 6, 6), Sealed: false));
    }

    [Test]
    public async Task Air_under_a_cloud_is_not_a_room_and_a_pocket_too_small_to_stand_in_is_not_named()
    {
        var clouded = Chamber(also: (world, _, _) =>
        {
            for (var x = 0; x < 10; x++)
                for (var z = 0; z < 10; z++)
                    world.SetBlock(x, 10 + BoardIsometric.TallestRoom + 1, z, Stone);
            for (var y = 4; y < 4 + BoardIsometric.SmallestCavity - 1; y++) world.SetBlock(8, y, 8, 0);
        });

        var (cavities, _) = BoardIsometric.Scan(clouded);

        await Assert.That(cavities.Count).IsEqualTo(1);
        await Assert.That(cavities[0].Cells).IsEqualTo(63);
    }

    [Test]
    public async Task The_void_scan_names_the_world_s_own_blocks_from_every_corner()
    {
        var built = Chamber(also: (world, _, _) =>
        {
            for (var y = 7; y <= 9; y++) world.SetBlock(6, y, 6, 0);
        });
        var (scanned, _) = BoardIsometric.Scan(built);

        for (var quarter = 1; quarter < BoardIsometric.Corners.Count; quarter++)
            await Assert.That(BoardIsometric.XRay(built, quarter, Scale, "chamber")!.Value.Cavities)
                .IsEquivalentTo(scanned).Because($"from the {BoardIsometric.Corners[quarter]}");
    }

    /// <summary>Twenty rooms of one size, side by side along x: the scan meets them west to east, and two voids
    /// of one size keep that order, so a board reads back the same way twice.</summary>
    [Test]
    public async Task Voids_of_one_size_come_back_in_the_order_the_scan_meets_them()
    {
        var world = new VoxelWorld();
        for (var x = 0; x < 82; x++)
            for (var z = 0; z < 6; z++)
                for (var y = 0; y <= 6; y++)
                    if (!(x % 4 is 2 or 3 && z is 2 or 3 && y is 3 or 4)) world.SetBlock(x, y, z, Stone);
        var built = new BuiltWorld(world, 0, 7, 0, new MapIntent(), new WorldProvenance(), RoomShells.BuiltIn);

        var (cavities, _) = BoardIsometric.Scan(built);

        await Assert.That(cavities.Count).IsEqualTo(20);
        await Assert.That(cavities.Select(cavity => cavity.Min.X)
            .SequenceEqual(Enumerable.Range(0, 20).Select(room => 2 + 4 * room))).IsTrue();
    }

    [Test]
    public async Task A_world_with_no_block_draws_nothing()
    {
        var empty = new BuiltWorld(new VoxelWorld(), 0, 1, 0, new MapIntent(), new WorldProvenance(), RoomShells.BuiltIn);

        await Assert.That(BoardIsometric.Isometric(empty, 0, Scale, "empty")).IsNull();
        await Assert.That(BoardIsometric.XRay(empty, 0, Scale, "empty")).IsNull();
        await Assert.That(BoardIsometric.Scan(empty).Blocks).IsEqualTo(0);
    }
}
