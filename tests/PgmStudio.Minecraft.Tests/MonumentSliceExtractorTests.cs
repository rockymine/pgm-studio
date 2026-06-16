using fNbt;
using PgmStudio.Minecraft;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// Exercises the monument-slice extractor against a synthetic chunk (no real game files, per
/// convention). A single monument at (5,8,5) with the patterns CTW authors use: an air placement
/// cell, bedrock directly below, a labelling sign (1.8 JSON text component), a stained-glass pedestal,
/// and a wool-indicator armour stand standing below whose head reaches into the slice. Corpus-level
/// validation (thunder, pigland, dragons_hearth) lives in the RoundTrip <c>--monument-slices</c> harness.
/// </summary>
public class MonumentSliceExtractorTests
{
    private static int Idx(int x, int y, int z) => (y << 8) | (z << 4) | x;

    private static void SetNibble(byte[] packed, int index, int value)
    {
        var b = index >> 1;
        packed[b] = (index & 1) == 0
            ? (byte)((packed[b] & 0xF0) | (value & 0x0F))
            : (byte)((packed[b] & 0x0F) | ((value & 0x0F) << 4));
    }

    // Monument block at (5,8,5). Center is air; bedrock at (5,7,5); a sign at (5,7,6); green stained
    // glass at (6,8,5); an armour stand standing at feet y=5 holding green wool on its head.
    private const int Cx = 5, Cy = 8, Cz = 5;

    private static AnvilRegion.Chunk BuildChunk()
    {
        var blocks = new byte[4096];
        var data = new byte[2048];

        blocks[Idx(Cx, Cy - 1, Cz)] = 7;             // bedrock directly below the monument
        blocks[Idx(Cx, Cy - 1, Cz + 1)] = 63;        // sign block (tile entity carries the text)
        blocks[Idx(Cx + 1, Cy, Cz)] = 95;            // stained glass at dx+1, dy0
        SetNibble(data, Idx(Cx + 1, Cy, Cz), 13);    // damage 13 → green
        // (Cx,Cy,Cz) is left as 0 → air, the monument placement cell.

        var section = new NbtCompound
        {
            new NbtByte("Y", 0),
            new NbtByteArray("Blocks", blocks),
            new NbtByteArray("Data", data),
        };

        var sign = new NbtCompound
        {
            new NbtString("id", "Sign"),
            new NbtInt("x", Cx), new NbtInt("y", Cy - 1), new NbtInt("z", Cz + 1),
            new NbtString("Text1", "{\"text\":\"\"}"),
            new NbtString("Text2", "{\"extra\":[{\"bold\":true,\"color\":\"dark_green\",\"text\":\"Green Wool\"}],\"text\":\"\"}"),
            new NbtString("Text3", "{\"extra\":[\"monument\"],\"text\":\"\"}"),
            new NbtString("Text4", "{\"text\":\"\"}"),
        };

        // Full-size armour stand, feet at y=5 (3 below the monument block) but its head (with the wool)
        // reaches up to ~y=7 → should attach to the in-slice cell at (dx0, dy-1, dz0).
        var armorStand = new NbtCompound
        {
            new NbtString("id", "ArmorStand"),
            new NbtList("Pos", new[] { new NbtDouble(Cx + 0.5), new NbtDouble(5.0), new NbtDouble(Cz + 0.5) }),
            new NbtString("CustomName", "Place GREEN WOOL here!"),
            new NbtList("Equipment", new[]
            {
                new NbtCompound(), new NbtCompound(), new NbtCompound(), new NbtCompound(),   // hand, boots, legs, chest
                new NbtCompound { new NbtString("id", "minecraft:wool"), new NbtShort("Damage", 13) },   // head
            }),
        };

        var level = new NbtCompound("Level")
        {
            new NbtList("Sections", new[] { section }),
            new NbtList("TileEntities", new[] { sign }),
            new NbtList("Entities", new[] { armorStand }),
        };
        return new AnvilRegion.Chunk(0, 0, level);
    }

    private static readonly MonumentTarget Target =
        new("testmap", "green", "green", "green-blue", "blue", Cx, Cy, Cz);

    private static List<MonumentSliceCell> Extract() =>
        MonumentSliceExtractor.Extract([BuildChunk()], [Target]);

    private static MonumentSliceCell Cell(List<MonumentSliceCell> cells, int dx, int dy, int dz) =>
        cells.Single(c => c.Dx == dx && c.Dy == dy && c.Dz == dz);

    [Test]
    public async Task Slice_IsAFixed_3x3x5_Tensor_TaggedWithItsMonument()
    {
        var cells = Extract();
        await Assert.That(cells.Count).IsEqualTo(MonumentSliceExtractor.CellsPerMonument);   // 45
        await Assert.That(cells.Count).IsEqualTo(45);
        await Assert.That(cells.All(c => c.MonumentId == "green-blue" && c.WoolColor == "green" && c.Team == "blue")).IsTrue();
        // every (dx,dy,dz) in [-1,1]×[-2,2]×[-1,1] appears exactly once
        await Assert.That(cells.Select(c => (c.Dx, c.Dy, c.Dz)).Distinct().Count()).IsEqualTo(45);
        await Assert.That(cells.All(c => c.Dx is >= -1 and <= 1 && c.Dz is >= -1 and <= 1 && c.Dy is >= -2 and <= 2)).IsTrue();
    }

    [Test]
    public async Task MonumentBlock_IsTheAirCentre()
    {
        var center = Cell(Extract(), 0, 0, 0);
        await Assert.That(center.IsMonument).IsTrue();
        await Assert.That(center.IsAir).IsTrue();
        await Assert.That(center.BlockId).IsEqualTo(0);
        await Assert.That((center.WorldX, center.WorldY, center.WorldZ)).IsEqualTo((Cx, Cy, Cz));
    }

    [Test]
    public async Task BedrockBelow_And_StainedGlass_AreSampledWithData()
    {
        var cells = Extract();
        var below = Cell(cells, 0, -1, 0);
        await Assert.That(below.BlockId).IsEqualTo(7);
        await Assert.That(below.IsAir).IsFalse();

        var glass = Cell(cells, 1, 0, 0);
        await Assert.That(glass.BlockId).IsEqualTo(95);
        await Assert.That(glass.BlockData).IsEqualTo(13);
        await Assert.That(glass.BlockName).IsEqualTo("Green Stained Glass");
    }

    [Test]
    public async Task Sign_TextComponentsDecodeToPlainText()
    {
        var sign = Cell(Extract(), 0, -1, 1);
        await Assert.That(sign.TileEntityId).IsEqualTo("Sign");
        await Assert.That(sign.SignText!).Contains("Green Wool");
        await Assert.That(sign.SignText!).Contains("monument");
        await Assert.That(sign.TileNbtJson!).Contains("Sign");
    }

    [Test]
    public async Task ArmorStand_BelowMonument_AttachesByVerticalReach_WithFullNbt()
    {
        var cells = Extract();
        // feet at y=5 (dy-3) but head reaches the slice → attached at (0,-1,0).
        var attached = Cell(cells, 0, -1, 0);
        await Assert.That(attached.EntityIds).IsEqualTo("ArmorStand");
        await Assert.That(attached.EntityNbtJson!).Contains("Place GREEN WOOL here!");
        await Assert.That(attached.EntityNbtJson!).Contains("minecraft:wool");
        // no other cell picked up the entity
        await Assert.That(cells.Count(c => c.EntityIds is not null)).IsEqualTo(1);
    }
}
