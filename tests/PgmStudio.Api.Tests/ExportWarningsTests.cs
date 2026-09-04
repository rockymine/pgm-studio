using System.IO.Compression;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PgmStudio.Api.Tests;

/// <summary>
/// <c>GET /map/{slug}/export</c> and <c>GET /map/{slug}/xml</c> — what the two routes that build the artifact
/// say about the work the build did not do.
///
/// <para>Both run the same dressing pass as <c>POST …/sketch/columns</c>, so the same props drop; neither
/// answers JSON, so neither has a <c>warnings</c> key to put them in. The export wrote them to
/// <c>region/dressing-report.json</c> inside the zip and the XML route reported nothing at all — so the
/// loudest fault on the surface reached a caller only if it unzipped, and on one route not even then. The
/// header is what makes the answer readable without opening anything.</para>
///
/// <para>Runs against the <c>pgm_studio_test</c> schema, so it runs serially with the other DB suites.</para>
/// </summary>
[NotInParallel("api-db")]
public sealed class ExportWarningsTests
{
    /// <summary>A tree far off every drawn shape. It has no column to rest on, which is <c>DR-SITE</c>, and
    /// the whole prop is declined — the one finding this suite needs the build to produce.</summary>
    private const string OffTheBoard =
        """{"props":[{"kind":"tree","id":"t-void","x":4000,"z":4000,"species":"oak","height":8,"seed":1}]}""";

    /// <summary>Drive the chain a sketch map is built by, with one prop that cannot land, and answer the
    /// slug. The plan seed is what makes the map exportable at all: it compiles to an intent carrying the
    /// teams, spawns and wools the export gates ask for.</summary>
    private static async Task<string> DressedMapAsync(HttpClient client, string name)
    {
        var compile = await client.PostAsync("/api/plan/compile",
            new StringContent(ReadSeed("base-2wool.plan.json"), Encoding.UTF8, "application/json"));
        await Assert.That(compile.IsSuccessStatusCode).IsTrue().Because(await compile.Content.ReadAsStringAsync());
        var compiled = await compile.Content.ReadFromJsonAsync<JsonElement>();

        var layout = JsonNode.Parse(compiled.GetProperty("layout").GetRawText())!.AsObject();
        layout["dressing"] = JsonNode.Parse(OffTheBoard);

        var create = await client.PostAsJsonAsync("/api/sketch", new { name });
        var slug = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("slug").GetString()!;

        var stored = await client.PutAsync($"/api/map/{slug}/sketch",
            new StringContent(layout.ToJsonString(), Encoding.UTF8, "application/json"));
        await Assert.That(stored.IsSuccessStatusCode).IsTrue().Because(await stored.Content.ReadAsStringAsync());

        var finish = await client.PostAsync($"/api/map/{slug}/sketch/finish", null);
        await Assert.That(finish.IsSuccessStatusCode).IsTrue().Because(await finish.Content.ReadAsStringAsync());

        var intent = await client.PutAsync($"/api/map/{slug}/intent",
            new StringContent(Authored(compiled.GetProperty("intent")), Encoding.UTF8, "application/json"));
        await Assert.That(intent.IsSuccessStatusCode).IsTrue().Because(await intent.Content.ReadAsStringAsync());

        return slug;
    }

    /// <summary>The compiled intent with an author on it. A map naming nobody is `EX6` — the observer
    /// platform's authors board is left off rather than stamped blank — and this suite is about what the
    /// <em>dressing</em> pass dropped, so the fixture states the one thing that would otherwise ride along in
    /// the header beside it.</summary>
    private static string Authored(JsonElement intent)
    {
        var node = JsonNode.Parse(intent.GetRawText())!.AsObject();
        var meta = node["meta"]?.AsObject() ?? [];
        meta["authors"] = new JsonArray(new JsonObject { ["name"] = "rockymine" });
        node["meta"] = meta;
        return node.ToJsonString();
    }

    private static string? Warnings(HttpResponseMessage resp) =>
        resp.Headers.TryGetValues("Pgm-Warnings", out var values) ? values.FirstOrDefault() : null;

    /// <summary>
    /// <b>The zip says what it dropped without being opened.</b> The report inside it is still the full
    /// record — the rule, the cell and the prop for each — and the header is the one line that tells a
    /// caller there is something in there to read.
    /// </summary>
    [Test]
    public async Task The_export_answers_what_the_build_declined()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();
        var slug = await DressedMapAsync(client, "Dropped Export");

        var export = await client.GetAsync($"/api/map/{slug}/export");
        await Assert.That(export.IsSuccessStatusCode).IsTrue().Because(await export.Content.ReadAsStringAsync());
        await Assert.That(export.Content.Headers.ContentType?.MediaType).IsEqualTo("application/zip");

        // The count first, then each rule once — enough to answer "did anything drop, and what kind" from
        // the header alone.
        await Assert.That(Warnings(export)).IsEqualTo("1 DR-SITE");
    }

    /// <summary>The XML route builds the same world through the same pass, so it drops the same prop — and
    /// it has no sidecar at all. Before the header it reported nothing anywhere.</summary>
    [Test]
    public async Task The_xml_route_answers_the_same_declines()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();
        var slug = await DressedMapAsync(client, "Dropped Xml");

        var xml = await client.GetAsync($"/api/map/{slug}/xml");
        await Assert.That(xml.IsSuccessStatusCode).IsTrue().Because(await xml.Content.ReadAsStringAsync());
        await Assert.That(xml.Content.Headers.ContentType?.MediaType).IsEqualTo("application/xml");

        await Assert.That(Warnings(xml)).IsEqualTo("1 DR-SITE");
    }

    /// <summary>A build that dropped nothing answers no header, the same rule the <c>warnings</c> key keeps:
    /// the absence is readable only because it means one thing.</summary>
    [Test]
    public async Task A_build_that_dropped_nothing_answers_no_header()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();

        var compile = await client.PostAsync("/api/plan/compile",
            new StringContent(ReadSeed("base-2wool.plan.json"), Encoding.UTF8, "application/json"));
        var compiled = await compile.Content.ReadFromJsonAsync<JsonElement>();

        var create = await client.PostAsJsonAsync("/api/sketch", new { name = "Clean Export" });
        var slug = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("slug").GetString()!;
        await client.PutAsync($"/api/map/{slug}/sketch",
            new StringContent(compiled.GetProperty("layout").GetRawText(), Encoding.UTF8, "application/json"));
        await client.PostAsync($"/api/map/{slug}/sketch/finish", null);
        await client.PutAsync($"/api/map/{slug}/intent",
            new StringContent(Authored(compiled.GetProperty("intent")), Encoding.UTF8, "application/json"));

        var export = await client.GetAsync($"/api/map/{slug}/export");
        await Assert.That(export.IsSuccessStatusCode).IsTrue().Because(await export.Content.ReadAsStringAsync());
        await Assert.That(Warnings(export)).IsNull();
    }

    /// <summary>The archive holds a world directory's own contents at its top. What a server is handed is the
    /// directory, so an entry under a folder named for the slug is one every caller unwraps before it can be
    /// used (<c>RP58</c>).</summary>
    [Test]
    public async Task The_export_zip_holds_the_world_at_its_top()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();
        var slug = await DressedMapAsync(client, "Flat Export");

        var export = await client.GetAsync($"/api/map/{slug}/export");
        await Assert.That(export.IsSuccessStatusCode).IsTrue().Because(await export.Content.ReadAsStringAsync());

        using var archive = new ZipArchive(new MemoryStream(await export.Content.ReadAsByteArrayAsync()));
        var names = archive.Entries.Select(entry => entry.FullName).ToList();

        await Assert.That(names).Contains("map.xml");
        await Assert.That(names).Contains("level.dat");
        await Assert.That(names.Any(name => name.StartsWith("region/", StringComparison.Ordinal))).IsTrue()
            .Because($"the region files are at the top: {string.Join(", ", names)}");
        await Assert.That(names.Any(name => name.StartsWith($"{slug}/", StringComparison.Ordinal))).IsFalse()
            .Because($"nothing is nested under the slug: {string.Join(", ", names)}");
    }

    private static string ReadSeed(string file)
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            var candidate = Path.Combine(dir, "tools", "seeds", file);
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            dir = Directory.GetParent(dir)?.FullName;
        }
        throw new FileNotFoundException($"seed not found: {file}");
    }
}
