using System.IO.Compression;
using PgmStudio.Geom.Render;
using PgmStudio.Minecraft.Render;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// Reading sprites out of a jar. The jar here is built by the test — the real one is Mojang's and never in the
/// repository — with the folder layout the real one has: block sprites under one folder, everything else
/// beside it, and an animated block stored as a tall strip of frames.
/// </summary>
public sealed class BlockTextureSetTests
{
    [Test]
    public async Task A_jar_gives_up_its_block_sprites_and_nothing_else()
    {
        var set = BlockTextureSet.FromJar(Jar(
            ("assets/minecraft/textures/blocks/stone.png", Solid(2, 2, 0x808080)),
            ("assets/minecraft/textures/blocks/nested/odd.png", Solid(2, 2, 0x101010)),
            ("assets/minecraft/textures/items/apple.png", Solid(2, 2, 0xFF0000)),
            ("assets/minecraft/textures/blocks/stone.png.mcmeta", [1, 2, 3])));

        await Assert.That(set.Count).IsEqualTo(1);
        await Assert.That(set.Get("stone")).IsNotNull();
        await Assert.That(set.Get("apple")).IsNull();
    }

    [Test]
    public async Task An_animated_strip_keeps_its_first_frame()
    {
        var rgb = new byte[2 * 6 * 3];
        for (var row = 0; row < 6; row++)
            for (var column = 0; column < 2; column++)
                rgb[(row * 2 + column) * 3] = (byte)(row < 2 ? 200 : 20);
        var set = BlockTextureSet.FromJar(Jar(("assets/minecraft/textures/blocks/water_still.png",
                                               PngWriter.Encode(2, 6, rgb))));

        var water = set.Get("water_still")!;
        await Assert.That(water.Size).IsEqualTo(2);
        await Assert.That((water.At(0.9, 0.9) >> 16) & 0xFF).IsEqualTo(200u);
    }

    [Test]
    public async Task A_texel_is_found_by_where_on_the_face_it_falls()
    {
        var sprite = new BlockSprite(2, [255, 0, 0, 255, 0, 255, 0, 255, 0, 0, 255, 255, 9, 9, 9, 0]);

        await Assert.That(sprite.At(0.1, 0.1)).IsEqualTo(0xFFFF0000u);
        await Assert.That(sprite.At(0.9, 0.1)).IsEqualTo(0xFF00FF00u);
        await Assert.That(sprite.At(0.1, 0.9)).IsEqualTo(0xFF0000FFu);
        await Assert.That(sprite.At(0.9, 0.9) >> 24).IsEqualTo(0u);
    }

    /// <summary>A chest texture at the game's size whose every texel says where it is: red four times its
    /// column, green four times its row.</summary>
    private static byte[] Addressed()
    {
        var pixels = new byte[64 * 64 * 3];
        for (var row = 0; row < 64; row++)
            for (var column = 0; column < 64; column++)
            {
                pixels[(row * 64 + column) * 3] = (byte)(column * 4);
                pixels[(row * 64 + column) * 3 + 1] = (byte)(row * 4);
            }
        return PngWriter.Encode(64, 64, pixels);
    }

    /// <summary>The texel of the chest texture a sprite's texel at <paramref name="column"/>,
    /// <paramref name="row"/> of sixteen came from.</summary>
    private static (uint Column, uint Row) Source(BlockSprite sprite, int column, int row)
    {
        var texel = sprite.At((column + 0.5) / 16, (row + 0.5) / 16);
        return (((texel >> 16) & 0xFF) / 4, ((texel >> 8) & 0xFF) / 4);
    }

    [Test]
    public async Task A_chest_texture_is_cut_into_the_faces_its_box_shows()
    {
        var set = BlockTextureSet.FromJar(Jar(
            ("assets/minecraft/textures/entity/chest/normal.png", Addressed()),
            ("assets/minecraft/textures/entity/chest/normal_double.png", Solid(128, 64, 0x404040))));

        await Assert.That(set.Count).IsEqualTo(3);
        var top = set.Get("chest_normal_top")!;
        var side = set.Get("chest_normal_side")!;
        var front = set.Get("chest_normal_front")!;
        await Assert.That(top.Size).IsEqualTo(16);
        await Assert.That(Source(top, 1, 1)).IsEqualTo((14u, 0u));
        await Assert.That(Source(top, 14, 14)).IsEqualTo((27u, 13u));
        await Assert.That(Source(side, 1, 2)).IsEqualTo((0u, 14u));
        await Assert.That(Source(side, 1, 6)).IsEqualTo((0u, 18u));
        await Assert.That(Source(side, 1, 7)).IsEqualTo((0u, 34u));
        await Assert.That(Source(side, 14, 15)).IsEqualTo((13u, 42u));
        await Assert.That(Source(front, 1, 2)).IsEqualTo((14u, 14u));
        await Assert.That(Source(front, 7, 5)).IsEqualTo((1u, 1u));
        await Assert.That(Source(front, 8, 8)).IsEqualTo((2u, 4u));
        await Assert.That(Source(side, 0, 0)).IsEqualTo(Source(side, 1, 2));
    }

    [Test]
    public async Task A_chest_texture_not_laid_out_the_way_the_games_is_gives_no_faces()
    {
        var set = BlockTextureSet.FromJar(Jar(("assets/minecraft/textures/entity/chest/ender.png", Solid(40, 40, 0x202020))));

        await Assert.That(set.Count).IsEqualTo(0);
    }

    private static byte[] Solid(int width, int height, int rgb)
    {
        var pixels = new byte[width * height * 3];
        for (var i = 0; i < width * height; i++)
        {
            pixels[i * 3] = (byte)(rgb >> 16); pixels[i * 3 + 1] = (byte)(rgb >> 8); pixels[i * 3 + 2] = (byte)rgb;
        }
        return PngWriter.Encode(width, height, pixels);
    }

    internal static MemoryStream Jar(params (string Path, byte[] Bytes)[] entries)
    {
        var jar = new MemoryStream();
        using (var archive = new ZipArchive(jar, ZipArchiveMode.Create, leaveOpen: true))
            foreach (var (path, bytes) in entries)
            {
                using var stream = archive.CreateEntry(path).Open();
                stream.Write(bytes);
            }
        jar.Position = 0;
        return jar;
    }
}
