using fNbt;
using PgmStudio.Domain;
using PgmStudio.Minecraft;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// Spawn cube + auto-wired monuments: the shell is stamped over its frame, and each captured wool becomes
/// a bedrock pedestal + air cell + wool-colour glass cap + label sign, seated by
/// <see cref="RoomFrames.MonumentSlots"/> (door-wall corners first, then back-wall corners, then the walls
/// fill). Baseline frame: 10×10 piece centred on (0,0), floor y=64, door facing -Z.
/// </summary>
public sealed class SpawnStructureStamperTests
{
    private static RoomFrame Baseline() => RoomFrames.Resolve(-5, -5, 5, 5, 0, 0, [], RoomEdge.NegZ, out _)!;

    [Test]
    public async Task One_wool_places_a_single_monument_at_a_door_wall_corner()
    {
        var w = new VoxelWorld();
        var placed = SpawnStructureStamper.Stamp(w, new SpawnStructure
        { Frame = Baseline(), FloorY = 64, TeamColor = 11, CapturedWools = ["red"] }).Monuments;

        await Assert.That(placed.Count).IsEqualTo(1);
        var m = placed[0];
        await Assert.That(m.WoolSlug).IsEqualTo("red");
        await Assert.That(m.Y).IsEqualTo(66);   // air cell = floor + 2

        // Pedestal below the air cell, glass cap above — cap coloured with the wool (red = 14).
        await Assert.That(w.GetBlock(m.X, 65, m.Z)).IsEqualTo((Blocks.Bedrock, 0));
        await Assert.That(w.GetBlock(m.X, 66, m.Z)).IsEqualTo((Blocks.Air, 0));
        await Assert.That(w.GetBlock(m.X, 67, m.Z)).IsEqualTo((Blocks.StainedGlass, 14));

        // Door-wall corner: the interior row against the -Z wall (world z=-3).
        await Assert.That(m.Z).IsEqualTo(-3);
    }

    [Test]
    public async Task Sign_faces_the_room_and_reads_place_the_colour_wool_here()
    {
        var w = new VoxelWorld();
        var placed = SpawnStructureStamper.Stamp(w, new SpawnStructure
        { Frame = Baseline(), FloorY = 64, TeamColor = 11, CapturedWools = ["light_blue"] }).Monuments;
        var m = placed[0];

        // Sign is one cell toward centre from the pedestal (+Z), at pedestal height, facing south (data 3).
        await Assert.That(w.GetBlock(m.X, 65, m.Z + 1)).IsEqualTo((Blocks.WallSign, 3));

        var dir = Path.Combine(Path.GetTempPath(), "spawn_" + Guid.NewGuid().ToString("N"));
        try
        {
            AnvilRegionWriter.Write(w, dir);
            var signs = new List<NbtCompound>();
            foreach (var mca in Directory.GetFiles(dir, "*.mca"))
                foreach (var chunk in AnvilRegion.ReadChunks(mca))
                    if (chunk.Level.Get<NbtList>("TileEntities") is { } te)
                        signs.AddRange(te.OfType<NbtCompound>().Where(t => t.Get<NbtString>("id")?.Value == "Sign"));

            await Assert.That(signs.Count).IsEqualTo(1);
            var text = Enumerable.Range(1, 4).Select(i => signs[0].Get<NbtString>($"Text{i}")!.Value).ToArray();
            await Assert.That(text[0]).Contains("Place the");
            await Assert.That(text[1]).Contains("Light Blue");   // display name, bolded
            await Assert.That(text[1]).Contains("\"bold\":true");
            await Assert.That(text[2]).Contains("Wool");
            await Assert.That(text[3]).Contains("here!");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Test]
    public async Task Three_wools_use_both_door_corners_and_one_back_corner()
    {
        var w = new VoxelWorld();
        var placed = SpawnStructureStamper.Stamp(w, new SpawnStructure
        { Frame = Baseline(), FloorY = 64, TeamColor = 11, CapturedWools = ["red", "green", "yellow"] }).Monuments;

        await Assert.That(placed.Count).IsEqualTo(3);
        // Door-wall corners sit near z=-3; the third (back wall) sits near z=+2.
        var zs = placed.Select(p => p.Z).OrderBy(z => z).ToArray();
        await Assert.That(zs[0]).IsEqualTo(-3);
        await Assert.That(zs[1]).IsEqualTo(-3);
        await Assert.That(zs[2]).IsEqualTo(2);
        // Distinct corners (perp columns differ).
        await Assert.That(placed.Select(p => (p.X, p.Z)).Distinct().Count()).IsEqualTo(3);
    }

    [Test]
    public async Task Minimum_room_seats_six_and_truncates_beyond_capacity()
    {
        // The 8×8-piece minimum: 4×4 interior, 2-wide door → 4 corners + 2 back-wall mids = 6 seats.
        var frame = RoomFrames.Resolve(0, 0, 8, 8, 4, 4, [], RoomEdge.NegZ, out _)!;
        var w = new VoxelWorld();
        var placed = SpawnStructureStamper.Stamp(w, new SpawnStructure
        { Frame = frame, FloorY = 64, TeamColor = 11, CapturedWools = ["red", "green", "yellow", "orange", "cyan", "purple", "lime"] }).Monuments;

        await Assert.That(placed.Count).IsEqualTo(6);
        // Every seat hugs a wall row of the interior [2,6): z ∈ {2,5}.
        await Assert.That(placed.All(p => p.Z is 2 or 5)).IsTrue();
        await Assert.That(placed.Select(p => (p.X, p.Z)).Distinct().Count()).IsEqualTo(6);
    }
}
