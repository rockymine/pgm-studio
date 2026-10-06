using PgmStudio.Geom;
using PgmStudio.Geom.Render;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Render;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// A structure card is the volume seen from outside with its blocks' own sprites: the eye is placed by the box
/// and the picture shows what is inside it and nothing outside.
/// </summary>
public sealed class StructurePictureTests
{
    private const int Stone = 1, GoldBlock = 41;
    private const int Wide = 96, High = 72;

    private static BlockTextureSet Sprites() => BlockTextureSet.Of(new Dictionary<string, BlockSprite>
    {
        ["stone"] = Uniform(120, 120, 120),
        ["gold_block"] = Uniform(250, 220, 0),
    });

    private static BlockSprite Uniform(byte red, byte green, byte blue)
    {
        var rgba = new byte[2 * 2 * 4];
        for (var i = 0; i < 4; i++) { rgba[i * 4] = red; rgba[i * 4 + 1] = green; rgba[i * 4 + 2] = blue; rgba[i * 4 + 3] = 255; }
        return new BlockSprite(2, rgba);
    }

    /// <summary>A 5×5 stone pad with a 3-block gold column on it.</summary>
    private static VoxelWorld Cairn()
    {
        var world = new VoxelWorld();
        for (var x = 0; x < 5; x++)
            for (var z = 0; z < 5; z++)
                world.SetBlock(x, 0, z, Stone);
        for (var y = 1; y <= 3; y++) world.SetBlock(2, y, 2, GoldBlock);
        return world;
    }

    private static readonly BlockBox WholeCairn = new(0, 0, 0, 4, 3, 4);

    [Test]
    public async Task A_structure_draws_as_a_picture_of_the_asked_size_holding_its_own_blocks()
    {
        var picture = PngReader.Decode(StructurePicture.Png(Cairn(), WholeCairn, Sprites(), Wide, High));

        var gold = 0;
        var stone = 0;
        for (var i = 0; i < picture.Width * picture.Height; i++)
        {
            var (red, green) = (picture.Rgba[i * 4], picture.Rgba[i * 4 + 1]);
            if (red > 150 && green > 130 && picture.Rgba[i * 4 + 2] < 60) gold++;
            else if (Math.Abs(red - green) < 6 && red is > 40 and < 140) stone++;
        }

        await Assert.That(picture.Width).IsEqualTo(Wide);
        await Assert.That(picture.Height).IsEqualTo(High);
        await Assert.That(gold).IsGreaterThan(40);
        await Assert.That(stone).IsGreaterThan(40);
    }

    [Test]
    public async Task A_box_cuts_the_structure_to_what_it_holds()
    {
        var padOnly = PngReader.Decode(StructurePicture.Png(Cairn(), new BlockBox(0, 0, 0, 4, 0, 4), Sprites(), Wide, High));

        var gold = 0;
        for (var i = 0; i < padOnly.Width * padOnly.Height; i++)
            if (padOnly.Rgba[i * 4] > 150 && padOnly.Rgba[i * 4 + 1] > 130 && padOnly.Rgba[i * 4 + 2] < 60) gold++;

        await Assert.That(gold).IsEqualTo(0);
    }

    [Test]
    public async Task The_eye_sits_further_back_from_a_larger_box()
    {
        var small = StructurePicture.Camera(WholeCairn, Wide, High);
        var large = StructurePicture.Camera(new BlockBox(0, 0, 0, 24, 12, 24), Wide, High);

        double Distance(EyeCamera camera, BlockBox box) => Math.Sqrt(
            Math.Pow(camera.X - (box.MinX + box.MaxX + 1) / 2.0, 2) + Math.Pow(camera.Y - (box.MinY + box.MaxY + 1) / 2.0, 2)
            + Math.Pow(camera.Z - (box.MinZ + box.MaxZ + 1) / 2.0, 2));

        await Assert.That(Distance(large, new BlockBox(0, 0, 0, 24, 12, 24)))
            .IsGreaterThan(Distance(small, WholeCairn) * 3);
    }
}
