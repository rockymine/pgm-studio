using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using PgmStudio.Geom.Render;

namespace PgmStudio.Api.Tests;

/// <summary>
/// <c>render/eye</c> over HTTP. Its drawing is <c>EyeScene</c>'s and tested there; what is new here is
/// the door: without Minecraft's textures the read says why and what to set, and with a jar named it answers
/// a picture and its text twin. The jar is one the test builds — the real one is Mojang's and never in the
/// repository.
/// </summary>
[NotInParallel("api-db")]
public sealed class EyeReadEndpointTests
{
    private const string Board = """
        {"setup":{"mirror_mode":"none","center":{"cx":0,"cz":0}},
         "layers": [{ "id": "ground", "base_y": 0, "layout":{"shapes":[
            {"id":"a","type":"rectangle","operation":"add","min_x":-30,"max_x":30,"min_z":-30,"max_z":30,"base_height":6},
            {"id":"b","type":"rectangle","operation":"add","min_x":-4,"max_x":4,"min_z":-4,"max_z":4,"base_height":10}],
          "groups":[{"id":"i1","name":"Island","mirrors":false,"shapeIds":["a","b"]}]} }]}
        """;

    private static async Task<string> FinishedAsync(HttpClient client)
    {
        var create = await client.PostAsJsonAsync("/api/sketch", new { name = $"WS76 {Guid.NewGuid():N}" });
        var slug = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("slug").GetString()!;
        var put = await client.PutAsync($"/api/map/{slug}/sketch", new StringContent(Board, Encoding.UTF8, "application/json"));
        await Assert.That(put.IsSuccessStatusCode).IsTrue();
        var finish = await client.PostAsync($"/api/map/{slug}/sketch/finish", null);
        await Assert.That(finish.IsSuccessStatusCode).IsTrue();
        return slug;
    }

    [Test]
    public async Task Without_the_textures_the_read_says_what_to_set()
    {
        using var client = ApiTestFactory.Shared.CreateClient();
        var slug = await FinishedAsync(client);

        var resp = await client.GetAsync($"/api/map/{slug}/render/eye?look=0,0");

        await Assert.That(resp.StatusCode).IsEqualTo(HttpStatusCode.ServiceUnavailable);
        var finding = JsonDocument.Parse(await resp.Content.ReadAsStringAsync()).RootElement.GetProperty("findings")[0];
        await Assert.That(finding.GetProperty("rule").GetString()).IsEqualTo("RQ10");
        await Assert.That(finding.GetProperty("message").GetString()).Contains("Textures:AcceptMojangEula");
    }

    [Test]
    public async Task With_a_jar_named_it_answers_the_picture_asked_for_and_what_is_in_it()
    {
        using var client = TexturedFactory.Shared.CreateClient();
        var slug = await FinishedAsync(client);

        var picture = await client.GetAsync($"/api/map/{slug}/render/eye?from=0,20&yaw=180&pitch=20&width=320&height=180");
        await Assert.That(picture.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(picture.Content.Headers.ContentType!.MediaType).IsEqualTo("image/png");
        var png = PngReader.Decode(await picture.Content.ReadAsByteArrayAsync());
        await Assert.That((png.Width, png.Height)).IsEqualTo((320, 180));

        var text = await client.GetStringAsync($"/api/map/{slug}/render/eye?look=0,15&format=text");
        await Assert.That(text).Contains("placed to see 0,15");
        await Assert.That(text).Contains("share   block");
    }

    [Test]
    public async Task A_read_that_names_neither_where_to_stand_nor_what_to_see_is_refused()
    {
        using var client = TexturedFactory.Shared.CreateClient();
        var slug = await FinishedAsync(client);

        var resp = await client.GetAsync($"/api/map/{slug}/render/eye");

        await Assert.That(resp.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    /// <summary>A host whose <c>Textures:Jar</c> names a jar of a few flat sprites, written once for the run.</summary>
    private sealed class TexturedFactory : WebApplicationFactory<Program>
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
}
