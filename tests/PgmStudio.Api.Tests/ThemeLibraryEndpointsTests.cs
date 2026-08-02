using System.Net;
using System.Net.Http.Json;
using PgmStudio.Contracts;
using PgmStudio.Minecraft;

namespace PgmStudio.Api.Tests;

/// <summary>
/// The theme/style library's HTTP surface (B44). The load-bearing case is the whole round-trip: POST
/// /api/themes/import lifts a painter theme JSON into the library as one style per bucket + a theme binding them,
/// and GET /api/themes/{id}/json reassembles it back to the identical JSON. Styles are browsable by kind, a theme
/// created from existing styles reads back with its bindings, and a malformed import is 400 not 500. Runs against
/// <c>pgm_studio_test</c>; each test resets the schema, so they run serially.
/// </summary>
[NotInParallel("api-db")]
public sealed class ThemeLibraryEndpointsTests
{
    private sealed record ThemeJsonResponse(string ThemeJson);
    private sealed record ImportResponse(long Id);

    [Test]
    public async Task Import_then_compose_round_trips_the_theme_json()
    {
        await ApiTestFactory.ResetSchemaAsync();
        await using var factory = new ApiTestFactory();
        using var client = factory.CreateClient();

        // The painter's built-in default, serialized exactly as a map's applied theme would be.
        var themeJson = TerrainThemeJson.Serialize(TerrainTheme.Default);

        // Lift it into the library: one theme + one style per themeable bucket (rim/surface/wall/fill).
        var imported = await (await client.PostAsJsonAsync("/api/themes/import", new ThemeImportRequest("Meadow", themeJson)))
            .Content.ReadFromJsonAsync<ImportResponse>();
        await Assert.That(imported!.Id).IsGreaterThan(0L);

        await Assert.That((await client.GetFromJsonAsync<List<ThemeSummary>>("/api/themes"))!.Count).IsEqualTo(1);
        await Assert.That((await client.GetFromJsonAsync<List<StyleDto>>("/api/styles"))!.Count).IsEqualTo(4);

        // The theme reads back with a binding per bucket, each pointing at a real style.
        var detail = await client.GetFromJsonAsync<ThemeDetail>($"/api/themes/{imported.Id}");
        await Assert.That(detail!.Buckets.Count).IsEqualTo(4);
        await Assert.That(detail.Buckets.All(b => b.StyleId > 0)).IsTrue();

        // Reassembled from its rows, the theme is byte-for-byte the JSON it was imported from.
        var composed = await client.GetFromJsonAsync<ThemeJsonResponse>($"/api/themes/{imported.Id}/json");
        await Assert.That(composed!.ThemeJson).IsEqualTo(themeJson);
    }

    [Test]
    public async Task Styles_are_browsable_by_kind_over_http()
    {
        await ApiTestFactory.ResetSchemaAsync();
        await using var factory = new ApiTestFactory();
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/styles", new StyleSaveRequest("cobble patches", "voronoi", "{\"kind\":\"voronoi\"}"));
        await client.PostAsJsonAsync("/api/styles", new StyleSaveRequest("stone", "solid", "{\"kind\":\"solid\"}"));

        await Assert.That((await client.GetFromJsonAsync<List<StyleDto>>("/api/styles"))!.Count).IsEqualTo(2);
        var voronoi = await client.GetFromJsonAsync<List<StyleDto>>("/api/styles?kind=voronoi");
        await Assert.That(voronoi!.Count).IsEqualTo(1);
        await Assert.That(voronoi[0].Name).IsEqualTo("cobble patches");
    }

    [Test]
    public async Task A_theme_created_from_styles_reads_back_and_deletes_with_cascade()
    {
        await ApiTestFactory.ResetSchemaAsync();
        await using var factory = new ApiTestFactory();
        using var client = factory.CreateClient();

        var rim = await (await client.PostAsJsonAsync("/api/styles", new StyleSaveRequest("quartz", "solid", "{}")))
            .Content.ReadFromJsonAsync<StyleDto>();
        var fill = await (await client.PostAsJsonAsync("/api/styles", new StyleSaveRequest("stone", "solid", "{}")))
            .Content.ReadFromJsonAsync<StyleDto>();

        var save = new ThemeSaveRequest(
            "meadow", BedrockRelative: true, BedrockValue: 1, Closed: false, WallOnTerrainFaces: false,
            new[]
            {
                new ThemeBucketDto("rim", rim!.Id, Depth: 1, Enabled: true),
                new ThemeBucketDto("fill", fill!.Id, Depth: 0, Enabled: true),
            });
        var created = await (await client.PostAsJsonAsync("/api/themes", save)).Content.ReadFromJsonAsync<ThemeDetail>();
        await Assert.That(created!.Buckets.Count).IsEqualTo(2);

        var detail = await client.GetFromJsonAsync<ThemeDetail>($"/api/themes/{created.Id}");
        await Assert.That(detail!.Buckets.Any(b => b.Bucket == "rim" && b.StyleId == rim.Id)).IsTrue();

        // Deleting the theme cascades its bindings but leaves the styles in the library.
        await Assert.That((await client.DeleteAsync($"/api/themes/{created.Id}")).StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        await Assert.That((await client.GetFromJsonAsync<List<ThemeSummary>>("/api/themes"))!.Count).IsEqualTo(0);
        await Assert.That((await client.GetFromJsonAsync<List<StyleDto>>("/api/styles"))!.Count).IsEqualTo(2);
    }

    [Test]
    public async Task A_malformed_theme_import_is_400_not_500()
    {
        await ApiTestFactory.ResetSchemaAsync();
        await using var factory = new ApiTestFactory();
        using var client = factory.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/themes/import", new ThemeImportRequest("bad", "{ not json"));
        await Assert.That(resp.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }
}
