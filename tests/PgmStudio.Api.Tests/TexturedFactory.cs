using System.IO.Compression;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using PgmStudio.Geom.Render;

namespace PgmStudio.Api.Tests;

/// <summary>A host whose <c>Textures:Jar</c> names a jar of a few flat sprites, written once for the run.</summary>
internal sealed class TexturedFactory : WebApplicationFactory<Program>
{
    public static TexturedFactory Shared { get; } = new();

    private static readonly string JarPath = WriteJar();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:PgmStudio"] = ApiTestFactory.ConnectionString,
            ["Access:Mode"] = "open",
            ["Textures:Jar"] = JarPath,
        }));
    }

    private static string WriteJar()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pgm-studio-test-textures-{Guid.NewGuid():N}.jar");
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (name, rgb) in (ReadOnlySpan<(string, int)>)
                 [("grass_top", 0x7FB238), ("grass_side", 0x8A6743), ("dirt", 0x8A6743), ("stone", 0x7E7E7E)])
        {
            var pixels = new byte[4 * 4 * 3];
            for (var i = 0; i < 16; i++)
            {
                pixels[i * 3] = (byte)(rgb >> 16); pixels[i * 3 + 1] = (byte)(rgb >> 8); pixels[i * 3 + 2] = (byte)rgb;
            }
            using var stream = archive.CreateEntry($"assets/minecraft/textures/blocks/{name}.png").Open();
            stream.Write(PngWriter.Encode(4, 4, pixels));
        }
        return path;
    }
}
