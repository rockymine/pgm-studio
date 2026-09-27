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
