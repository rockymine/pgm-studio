using fNbt;
using PgmStudio.Minecraft;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Palette;
using PgmStudio.Minecraft.Stamping;

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
        ObserverPlatformStamper.Stamp(w, 0, 0, 90, "Outback", ["alice", "bob"], ["ctw"]);

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

    /// <summary><b>The board names what the map is played as</b>, out of the same set the <c>&lt;gamemode&gt;</c>
    /// elements are written from — so a map with a monument does not greet every player with the label of a
    /// mode it does not have. Two modes are one label, because a map carrying a wool and a monument is played
    /// as both.</summary>
    [Test]
    [Arguments(new[] { "ctw" }, "[CTW]")]
    [Arguments(new[] { "dtm" }, "[DTM]")]
    [Arguments(new[] { "dtm", "dtc" }, "[DTM/DTC]")]
    [Arguments(new[] { "ctw", "dtm", "dtc" }, "[CTW/DTM/DTC]")]
    public async Task The_map_sign_names_the_modes_the_map_carries(string[] modes, string label)
    {
        var world = new VoxelWorld();
        ObserverPlatformStamper.Stamp(world, 0, 0, 90, "Outback", ["alice"], modes);

        var lines = await SignLinesAt(world, -1, 91, -2);
        await Assert.That(lines[1]).Contains(label);
        await Assert.That(lines[2]).Contains("Outback");
    }

    /// <summary>A board with nothing to win names no mode rather than inventing one: the line is left out and
    /// the name moves up into it.</summary>
    [Test]
    public async Task A_map_with_no_objective_names_no_mode()
    {
        var world = new VoxelWorld();
        ObserverPlatformStamper.Stamp(world, 0, 0, 90, "Outback", ["alice"], []);

        var lines = await SignLinesAt(world, -1, 91, -2);
        await Assert.That(lines[1]).Contains("Outback");
        await Assert.That(string.Join("", lines)).DoesNotContain("[");
    }

    /// <summary>And a map naming nobody gets no authors board at all. A heading over three blank lines is
    /// what every agent-built board carried, and it reads as a map whose author is called nothing; the studio
    /// has no name of its own to write there, so it writes none and says so (<c>EX6</c>).</summary>
    [Test]
    public async Task A_map_naming_no_author_gets_no_authors_sign()
    {
        var world = new VoxelWorld();
        ObserverPlatformStamper.Stamp(world, 0, 0, 90, "Outback", [], ["ctw"]);

        // The map-name sign still stands on every board; the authors cell beside it is empty.
        await Assert.That(world.GetBlock(-1, 91, -2).Id).IsEqualTo(Blocks.WallSign);
        await Assert.That(world.GetBlock(0, 91, -2).Id).IsEqualTo(Blocks.Air);
    }

    /// <summary>The lines of the sign at one cell, read back out of the written region the way a server would.
    /// </summary>
    private static async Task<string[]> SignLinesAt(VoxelWorld world, int x, int y, int z)
    {
        var dir = Path.Combine(Path.GetTempPath(), "obs_" + Guid.NewGuid().ToString("N"));
        try
        {
            AnvilRegionWriter.Write(world, dir);
            foreach (var mca in Directory.GetFiles(dir, "*.mca"))
                foreach (var chunk in AnvilRegion.ReadChunks(mca))
                    if (chunk.Level.Get<NbtList>("TileEntities") is { } te)
                        foreach (var tile in te.OfType<NbtCompound>())
                        {
                            if (tile.Get<NbtString>("id")?.Value != "Sign") continue;
                            if (tile.Get<NbtInt>("x")!.Value != x || tile.Get<NbtInt>("y")!.Value != y
                                || tile.Get<NbtInt>("z")!.Value != z) continue;
                            return [.. Enumerable.Range(1, 4).Select(i => tile.Get<NbtString>($"Text{i}")!.Value)];
                        }
            await Assert.That(false).IsTrue().Because($"no sign at ({x}, {y}, {z})");
            return [];
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    /// <summary>
    /// <b>The platform seats above whatever the board builds under it.</b> Its floor course is bedrock
    /// written over the cells it covers, so a height derived from a nominal ground level would put that
    /// bedrock through a bridge or a keep standing at the board's centre — and stand the observers inside it.
    /// </summary>
    [Test]
    public async Task The_floor_clears_what_is_already_built_under_it()
    {
        var world = new VoxelWorld();

        // A bridge deck across the middle of the footprint, well above the height the intent asked for.
        for (var x = -3; x <= 2; x++)
            world.SetBlock(x, 30, 0, Blocks.Cobblestone);

        await Assert.That(ObserverPlatformStamper.ClearFloorAt(world, 0, 0, 24)).IsEqualTo(31);
    }

    /// <summary>Nothing over the footprint leaves the stated height exactly, so a board with an empty centre
    /// puts its platform where the intent put it.</summary>
    [Test]
    public async Task An_empty_footprint_keeps_the_height_it_was_given()
    {
        var world = new VoxelWorld();
        world.SetBlock(40, 60, 40, Blocks.Cobblestone);      // outside the 6x6

        await Assert.That(ObserverPlatformStamper.ClearFloorAt(world, 0, 0, 24)).IsEqualTo(24);
    }

    /// <summary>Ground below the stated height is not a reason to move: the platform floats, and only what
    /// reaches into its own courses can bury it.</summary>
    [Test]
    public async Task Ground_below_the_stated_height_does_not_lift_it()
    {
        var world = new VoxelWorld();
        for (var x = -3; x <= 2; x++)
        for (var z = -3; z <= 2; z++)
            world.SetBlock(x, 9, z, Blocks.Stone);

        await Assert.That(ObserverPlatformStamper.ClearFloorAt(world, 0, 0, 24)).IsEqualTo(24);
    }
}
