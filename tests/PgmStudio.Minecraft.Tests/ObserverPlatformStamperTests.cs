using fNbt;
using PgmStudio.Minecraft;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// Observer platform: a solid 6×6 bedrock floor, four inward info boards (bedrock wall + 2 signs each),
/// map-name sign on the left, authors on the right. Anchor (0,0) → world x/z ∈ [-3, 2], floor y=90.
/// </summary>
public sealed class ObserverPlatformStamperTests
{
    [Test]
    public async Task Solid_floor_and_four_boards_with_eight_signs()
    {
        var w = new VoxelWorld();
        ObserverPlatformStamper.Stamp(w, 0, 0, 90, "Outback", ["alice", "bob"]);

        // Solid floor: every 6×6 cell at y=90 is bedrock.
        await Assert.That(w.GetBlock(-3, 90, -3)).IsEqualTo((Blocks.Bedrock, 0));
        await Assert.That(w.GetBlock(2, 90, 2)).IsEqualTo((Blocks.Bedrock, 0));
        await Assert.That(w.GetBlock(0, 90, 0)).IsEqualTo((Blocks.Bedrock, 0));

        // Min-z board: bedrock wall on the edge (local x2 → world -1, z0 → world -3), sign one cell inward.
        await Assert.That(w.GetBlock(-1, 91, -3)).IsEqualTo((Blocks.Bedrock, 0));
        await Assert.That(w.GetBlock(-1, 91, -2).Id).IsEqualTo(Blocks.WallSign);

        var dir = Path.Combine(Path.GetTempPath(), "obs_" + Guid.NewGuid().ToString("N"));
        try
        {
            AnvilRegionWriter.Write(w, dir);
            var signs = new List<NbtCompound>();
            foreach (var mca in Directory.GetFiles(dir, "*.mca"))
                foreach (var chunk in AnvilRegion.ReadChunks(mca))
                    if (chunk.Level.Get<NbtList>("TileEntities") is { } te)
                        signs.AddRange(te.OfType<NbtCompound>().Where(t => t.Get<NbtString>("id")?.Value == "Sign"));

            // 4 boards × 2 signs.
            await Assert.That(signs.Count).IsEqualTo(8);

            static string[] Lines(NbtCompound s) => Enumerable.Range(1, 4).Select(i => s.Get<NbtString>($"Text{i}")!.Value).ToArray();
            var mapSigns = signs.Where(s => Lines(s)[1].Contains("[CTW]")).ToList();
            var authorSigns = signs.Where(s => Lines(s)[0].Contains("made by")).ToList();

            await Assert.That(mapSigns.Count).IsEqualTo(4);
            await Assert.That(authorSigns.Count).IsEqualTo(4);
            await Assert.That(Lines(mapSigns[0])[2]).Contains("Outback");
            await Assert.That(Lines(mapSigns[0])[2]).Contains("\"bold\":true");
            await Assert.That(Lines(authorSigns[0])[0]).Contains("\"italic\":true");
            await Assert.That(Lines(authorSigns[0])[1]).Contains("alice");
            await Assert.That(Lines(authorSigns[0])[2]).Contains("bob");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
