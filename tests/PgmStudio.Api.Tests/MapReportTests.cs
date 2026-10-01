using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PgmStudio.Api.Tests;

/// <summary>
/// <c>GET /map/{slug}/report</c> — everything a drive reads back, off one build. What has to hold is that the
/// report is the reads it names and nothing else: every reading is the answer its own route gives, every
/// picture is the picture its own route draws, and the three numbers are the ones the readings carry.
///
/// <para>Driven over the two-wool seed stored from its source — two spawns and four roofed wool rooms, so the
/// report walks routes, takes transects and draws the x-ray — with a knoll, a shelf and a foot stepped down
/// the middle of it, so its ground counts as walked, scrambled and a barrier in three different numbers.</para>
/// </summary>
[NotInParallel("api-db")]
public sealed partial class MapReportTests
{
    private static async Task<string> StoredAsync(HttpClient client)
    {
        var slug = $"report-{Guid.NewGuid():N}"[..20];
        var resp = await client.PutAsJsonAsync($"/api/map/{slug}/source", new
        {
            plan = JsonDocument.Parse(Seeds.Read("base-2wool.plan.json")).RootElement,
            refinement = JsonDocument.Parse("""
                {"authors":["rockymine"],"addShapes":[
                  {"id":"knoll","type":"rectangle","operation":"add","min_x":-4,"max_x":4,"min_z":-2,"max_z":2,"base_height":16},
                  {"id":"shelf","type":"rectangle","operation":"add","min_x":4,"max_x":9,"min_z":-4,"max_z":4,"base_height":11},
                  {"id":"foot","type":"rectangle","operation":"add","min_x":9,"max_x":14,"min_z":-4,"max_z":4,"base_height":9}]}
                """).RootElement,
        });
        await Assert.That(resp.IsSuccessStatusCode).IsTrue().Because(await resp.Content.ReadAsStringAsync());
        return slug;
    }

    /// <summary>A reading's route asked alone, as the report names it: a posted read takes the stored layout.</summary>
    private static async Task<string?> AskedAsync(HttpClient client, string slug, string route)
    {
        HttpResponseMessage resp;
        if (route.StartsWith("POST ", StringComparison.Ordinal))
        {
            var layout = await client.GetStringAsync($"/api/map/{slug}/sketch");
            resp = await client.PostAsync($"/api/map/{slug}/{route[5..]}",
                new StringContent(layout, Encoding.UTF8, "application/json"));
        }
        else resp = await client.GetAsync($"/api/map/{slug}/{route}");
        return resp.IsSuccessStatusCode ? await resp.Content.ReadAsStringAsync() : null;
    }

    private static bool AnswersText(string route) =>
        route.Contains("format=text", StringComparison.Ordinal) || route is "reach" or "plan/ascii" or "plan/flow";

    [Test]
    public async Task Every_reading_is_the_answer_its_own_route_gives()
    {
        using var client = ApiTestFactory.Shared.CreateClient();
        var slug = await StoredAsync(client);

        var report = await client.GetFromJsonAsync<JsonElement>($"/api/map/{slug}/report");
        var reads = report.GetProperty("reads").EnumerateArray().ToList();

        var compared = 0;
        foreach (var read in reads)
        {
            var route = read.GetProperty("route").GetString()!;
            var name = read.GetProperty("name").GetString()!;
            await Assert.That(read.GetProperty("text").ValueKind).IsEqualTo(JsonValueKind.String)
                .Because($"{name} came back with no reading: {read.GetProperty("missing")}");
            if (!AnswersText(route)) continue;
            await Assert.That(await AskedAsync(client, slug, route)).IsEqualTo(read.GetProperty("text").GetString())
                .Because($"{name} is what {route} answers");
            compared++;
        }

        await Assert.That(compared).IsGreaterThan(20);

        // A transect each way through every spawn, and a route from every spawn to every goal.
        var intent = await client.GetFromJsonAsync<JsonElement>($"/api/map/{slug}/intent");
        var spawns = intent.GetProperty("spawns").GetArrayLength();
        var wools = intent.GetProperty("wools").GetArrayLength();
        var names = reads.Select(read => read.GetProperty("name").GetString()!).ToList();
        await Assert.That(names.Count(name => name.StartsWith("transect spawn-", StringComparison.Ordinal)))
            .IsEqualTo(2 * spawns);
        await Assert.That(names.Count(name => name.StartsWith("route spawn-", StringComparison.Ordinal)))
            .IsEqualTo(spawns * wools);
    }

    [Test]
    public async Task The_three_numbers_are_the_ones_the_readings_carry()
    {
        using var client = ApiTestFactory.Shared.CreateClient();
        var slug = await StoredAsync(client);

        var report = await client.GetFromJsonAsync<JsonElement>($"/api/map/{slug}/report");
        var headline = report.GetProperty("headline");
        string Reading(string name) => report.GetProperty("reads").EnumerateArray()
            .Single(read => read.GetProperty("name").GetString() == name).GetProperty("text").GetString()!;

        var slopes = await client.GetFromJsonAsync<JsonElement>($"/api/map/{slug}/slopes");
        foreach (var count in (string[])["walked", "scrambled", "barrier"])
            await Assert.That(headline.GetProperty(count).GetInt32()).IsEqualTo(slopes.GetProperty(count).GetInt32());

        var claimed = Claimed().Match(Reading("claims"));
        await Assert.That(headline.GetProperty("placed").GetInt32()).IsEqualTo(int.Parse(claimed.Groups[1].Value));
        await Assert.That(headline.GetProperty("declined").GetInt32()).IsEqualTo(int.Parse(claimed.Groups[2].Value));

        var steps = report.GetProperty("reads").EnumerateArray()
            .Where(read => read.GetProperty("name").GetString()!.StartsWith("route ", StringComparison.Ordinal))
            .Select(read => (Name: read.GetProperty("name").GetString()!,
                             Step: int.Parse(WorstStep().Match(read.GetProperty("text").GetString()!).Groups[1].Value)))
            .ToList();
        var worst = steps.Max(route => route.Step);
        await Assert.That(headline.GetProperty("worstStep").GetInt32()).IsEqualTo(worst);
        await Assert.That(steps.First(route => route.Step == worst).Name)
            .IsEqualTo(headline.GetProperty("worstRoute").GetString());
    }

    [Test]
    public async Task Every_picture_is_the_one_its_own_route_draws()
    {
        using var client = TexturedFactory.Shared.CreateClient();
        var slug = await StoredAsync(client);

        var report = await client.GetFromJsonAsync<JsonElement>($"/api/map/{slug}/report?pictures=true");
        var pictures = report.GetProperty("pictures").EnumerateArray().ToList();

        foreach (var picture in pictures)
        {
            var route = picture.GetProperty("route").GetString()!;
            await Assert.That(picture.GetProperty("png").ValueKind).IsEqualTo(JsonValueKind.String)
                .Because($"{route} was not drawn: {picture.GetProperty("missing")}");
            var drawn = await client.GetByteArrayAsync($"/api/map/{slug}/{route}");
            await Assert.That(Convert.FromBase64String(picture.GetProperty("png").GetString()!).SequenceEqual(drawn))
                .IsTrue().Because($"the report's {picture.GetProperty("name").GetString()} is what {route} draws");
        }
        var routes = pictures.Select(picture => picture.GetProperty("route").GetString()!).ToList();
        await Assert.That(routes).Contains("render/xray").Because("a wool room is a roofed room worth seeing");
        await Assert.That(routes.Any(route => route.StartsWith("render/eye?", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    public async Task Unasked_the_pictures_are_named_and_not_drawn_and_the_text_carries_the_numbers_first()
    {
        using var client = ApiTestFactory.Shared.CreateClient();
        var slug = await StoredAsync(client);

        var report = await client.GetFromJsonAsync<JsonElement>($"/api/map/{slug}/report");
        foreach (var picture in report.GetProperty("pictures").EnumerateArray())
            await Assert.That(picture.GetProperty("png").ValueKind).IsEqualTo(JsonValueKind.Null);

        var resp = await client.GetAsync($"/api/map/{slug}/report?format=text");
        await Assert.That(resp.Content.Headers.ContentType!.MediaType).IsEqualTo("text/plain");
        var lines = (await resp.Content.ReadAsStringAsync()).Split('\n');
        await Assert.That(lines[0]).IsEqualTo($"REPORT  {slug}  change 1");
        await Assert.That(lines[2]).StartsWith("  ground   ");
        await Assert.That(lines[3]).StartsWith("  props    ");
        await Assert.That(lines[4]).StartsWith("  routes   worst step ");
        await Assert.That(lines).Contains("== slopes   (slopes?format=text)");
    }

    [Test]
    public async Task A_map_with_no_world_has_no_report()
    {
        using var client = ApiTestFactory.Shared.CreateClient();

        var resp = await client.GetAsync("/api/map/not-a-map-at-all/report");

        await Assert.That(resp.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [GeneratedRegex(@"placed (\d+), declined (\d+)")]
    private static partial Regex Claimed();

    [GeneratedRegex(@"worst step (\d+)")]
    private static partial Regex WorstStep();
}
