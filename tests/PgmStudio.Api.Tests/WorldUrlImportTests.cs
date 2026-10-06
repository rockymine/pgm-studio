using System.IO.Compression;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using PgmStudio.Minecraft.Anvil;

namespace PgmStudio.Api.Tests;

/// <summary>
/// <c>POST /map/{slug}/import-url</c> — a new download read over the world an imported map already has. The
/// map is the same map afterwards: its slug, its intent and every other map are untouched, and only what the
/// scan read out of the world changes. A download the import will not take changes nothing at all.
/// </summary>
[NotInParallel("api-db")]
public sealed class WorldUrlImportTests
{
    /// <summary>A zipped world holding one 16 × 16 bedrock platform from y0 up to <paramref name="top"/>, served
    /// by the test studio's archive host at <paramref name="path"/>.</summary>
    private static string Serve(string path, int top)
    {
        var world = new VoxelWorld();
        for (var x = 0; x < 16; x++)
            for (var z = 0; z < 16; z++)
                for (var y = 0; y <= top; y++) world.SetBlock(x, y, z, 7);

        var regionDir = Path.Combine(Path.GetTempPath(), $"pgm-test-world-{Guid.NewGuid():N}");
        try
        {
            AnvilRegionWriter.Write(world, regionDir);
            using var bytes = new MemoryStream();
            using (var archive = new ZipArchive(bytes, ZipArchiveMode.Create, leaveOpen: true))
                foreach (var file in Directory.GetFiles(regionDir, "*.mca"))
                    archive.CreateEntryFromFile(file, $"world/region/{Path.GetFileName(file)}");
            ApiTestFactory.Archives[path] = bytes.ToArray();
        }
        finally { Directory.Delete(regionDir, recursive: true); }
        return ApiTestFactory.ArchiveHost + path;
    }

    private static async Task<(HttpClient Client, string Slug)> ImportedAsync(int top)
    {
        await ApiTestFactory.ResetSchemaAsync();
        if (Directory.Exists(ApiTestFactory.ImportRoot)) Directory.Delete(ApiTestFactory.ImportRoot, recursive: true);
        var client = ApiTestFactory.Shared.CreateClient();

        var imported = await client.PostAsJsonAsync("/api/map/import-url",
            new { url = Serve("/first.zip", top), slug = "fields" });
        await Assert.That(imported.IsSuccessStatusCode).IsTrue().Because(await imported.Content.ReadAsStringAsync());
        return (client, "fields");
    }

    private static async Task<bool> ClearAsync(HttpClient client, string slug, int y) =>
        (await client.GetFromJsonAsync<JsonElement>($"/api/map/{slug}/block-seat?x=5&y={y}&z=5"))
            .GetProperty("clear").GetBoolean();

    private static Task<HttpResponseMessage> ReimportAsync(HttpClient client, string slug, string url) =>
        client.PostAsJsonAsync($"/api/map/{slug}/import-url", new { url });

    [Test]
    public async Task A_new_world_replaces_the_scan_and_keeps_the_map()
    {
        var (client, slug) = await ImportedAsync(top: 4);
        var stated = await client.PutAsync($"/api/map/{slug}/intent", new StringContent(
            """{"meta":{"name":"Fields","authors":[{"name":"Opus 5"}],"contributors":[]}}""", Encoding.UTF8, "application/json"));
        await Assert.That(stated.IsSuccessStatusCode).IsTrue().Because(await stated.Content.ReadAsStringAsync());
        var intent = await client.GetStringAsync($"/api/map/{slug}/intent");
        await Assert.That(await ClearAsync(client, slug, 7)).IsTrue();

        var reimported = await ReimportAsync(client, slug, Serve("/second.zip", top: 9));

        await Assert.That(reimported.IsSuccessStatusCode).IsTrue().Because(await reimported.Content.ReadAsStringAsync());
        await Assert.That((await reimported.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("slug").GetString())
            .IsEqualTo(slug);
        await Assert.That(await ClearAsync(client, slug, 7)).IsFalse();
        await Assert.That(await client.GetStringAsync($"/api/map/{slug}/intent")).IsEqualTo(intent);
        var maps = await client.GetFromJsonAsync<JsonElement>("/api/maps");
        await Assert.That(maps.GetArrayLength()).IsEqualTo(1);
        await Assert.That(Directory.GetFiles(Path.Combine(ApiTestFactory.ImportRoot, slug, "region"), "*.mca").Length)
            .IsEqualTo(1);
    }

    [Test]
    [Arguments("/missing.zip", 502, "IM2")]
    [Arguments("/not-a-zip.zip", 415, "IM4")]
    public async Task A_download_the_import_will_not_take_leaves_the_world_it_had(string path, int status, string rule)
    {
        var (client, slug) = await ImportedAsync(top: 4);
        ApiTestFactory.Archives["/not-a-zip.zip"] = "<html>sign in</html>"u8.ToArray();

        var refused = await ReimportAsync(client, slug, ApiTestFactory.ArchiveHost + path);
        var answer = await refused.Content.ReadFromJsonAsync<JsonElement>();

        await Assert.That((int)refused.StatusCode).IsEqualTo(status);
        await Assert.That(answer.GetProperty("findings")[0].GetProperty("rule").GetString()).IsEqualTo(rule);
        await Assert.That(await ClearAsync(client, slug, 7)).IsTrue();
        await Assert.That(await ClearAsync(client, slug, 4)).IsFalse();
    }

    [Test]
    public async Task A_sketch_map_takes_no_world()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();
        var slug = (await (await client.PostAsJsonAsync("/api/sketch", new { name = "Drawn" }))
            .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("slug").GetString()!;

        var refused = await ReimportAsync(client, slug, Serve("/drawn.zip", top: 4));
        var answer = await refused.Content.ReadFromJsonAsync<JsonElement>();

        await Assert.That((int)refused.StatusCode).IsEqualTo(409);
        await Assert.That(answer.GetProperty("findings")[0].GetProperty("rule").GetString()).IsEqualTo("IM7");
    }
}
