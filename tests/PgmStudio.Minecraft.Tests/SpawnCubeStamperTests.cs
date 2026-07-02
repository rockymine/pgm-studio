using fNbt;
using PgmStudio.Minecraft;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// Spawn cube + auto-wired monuments: the shell is stamped, and each captured wool becomes a bedrock
/// pedestal + air cell + wool-colour glass cap + label sign, placed by count (door-wall corners first,
/// then back-wall corners). Anchor (0,0), floor y=64, door facing -Z.
/// </summary>
public sealed class SpawnCubeStamperTests
{
    [Test]
    public async Task One_wool_places_a_single_monument_at_a_door_wall_corner()
    {
        var w = new VoxelWorld();
        var placed = SpawnCubeStamper.Stamp(w, 0, 0, 64, teamColor: 11 /*blue*/, Facing.NegZ, ["red"]);

        await Assert.That(placed.Count).IsEqualTo(1);
        var m = placed[0];
        await Assert.That(m.WoolSlug).IsEqualTo("red");
        await Assert.That(m.Y).IsEqualTo(66);   // air cell = floor + 2

        // Pedestal below the air cell, glass cap above — cap coloured with the wool (red = 14).
        await Assert.That(w.GetBlock(m.X, 65, m.Z)).IsEqualTo((Blocks.Bedrock, 0));
        await Assert.That(w.GetBlock(m.X, 66, m.Z)).IsEqualTo((Blocks.Air, 0));
        await Assert.That(w.GetBlock(m.X, 67, m.Z)).IsEqualTo((Blocks.StainedGlass, 14));

        // Door-wall corner: near the -Z wall (local z=1 → world z=-3).
        await Assert.That(m.Z).IsEqualTo(-3);
    }

    [Test]
    public async Task Sign_faces_the_room_and_reads_place_the_colour_wool_here()
    {
        var w = new VoxelWorld();
        var placed = SpawnCubeStamper.Stamp(w, 0, 0, 64, teamColor: 11, Facing.NegZ, ["light_blue"]);
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
        var placed = SpawnCubeStamper.Stamp(w, 0, 0, 64, teamColor: 11, Facing.NegZ, ["red", "green", "yellow"]);

        await Assert.That(placed.Count).IsEqualTo(3);
        // Door-wall corners sit near z=-3; the third (back wall) sits near z=+2.
        var zs = placed.Select(p => p.Z).OrderBy(z => z).ToArray();
        await Assert.That(zs[0]).IsEqualTo(-3);
        await Assert.That(zs[1]).IsEqualTo(-3);
        await Assert.That(zs[2]).IsEqualTo(2);
        // Distinct corners (perp columns differ).
        await Assert.That(placed.Select(p => (p.X, p.Z)).Distinct().Count()).IsEqualTo(3);
    }
}
