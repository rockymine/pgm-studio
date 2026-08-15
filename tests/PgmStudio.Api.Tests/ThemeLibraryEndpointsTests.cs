using System.Net;
using System.Net.Http.Json;
using PgmStudio.Contracts;
using PgmStudio.Minecraft;
using PgmStudio.Minecraft.Painting;
using PgmStudio.Minecraft.Palette;

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
        using var client = ApiTestFactory.Shared.CreateClient();

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
        using var client = ApiTestFactory.Shared.CreateClient();

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
        using var client = ApiTestFactory.Shared.CreateClient();

        var rim = await (await client.PostAsJsonAsync("/api/styles", new StyleSaveRequest("quartz", "solid", "{}")))
            .Content.ReadFromJsonAsync<StyleDto>();
        var fill = await (await client.PostAsJsonAsync("/api/styles", new StyleSaveRequest("stone", "solid", "{}")))
            .Content.ReadFromJsonAsync<StyleDto>();

        var save = new ThemeSaveRequest(
            "meadow", BedrockRelative: true, BedrockValue: 1, RimEdges: RimEdgeModes.Drop, WallOnTerrainFaces: false,
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
    public async Task A_style_carries_the_picture_it_is_browsed_by()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();

        var stack = TerrainThemeJson.Serialize(new LayeredMaterial(new BandStack(
            [new Band(new SolidMaterial(Blocks.Grass), 1), new Band(new SolidMaterial(Blocks.Dirt), 2)])));
        await client.PostAsJsonAsync("/api/styles", new StyleSaveRequest("meadow top", MaterialKind.Layered, stack));

        var styles = await client.GetFromJsonAsync<List<StyleDto>>("/api/styles");
        // A stack is browsed by its section view, which is where its layers are — both blocks have to be in it.
        await Assert.That(styles![0].Preview).Contains(BlockPalette.Hex(Blocks.Grass, 0));
        await Assert.That(styles[0].Preview).Contains(BlockPalette.Hex(Blocks.Dirt, 0));
    }

    [Test]
    public async Task A_theme_is_updated_in_place_bindings_and_all()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();

        var quartz = await CreateStyle(client, "quartz", new SolidMaterial(Blocks.QuartzBlock));
        var stone = await CreateStyle(client, "stone", new SolidMaterial(Blocks.Stone));

        var created = await (await client.PostAsJsonAsync("/api/themes", new ThemeSaveRequest(
            "meadow", BedrockRelative: false, BedrockValue: 1, RimEdges: RimEdgeModes.Drop, WallOnTerrainFaces: true,
            [new ThemeBucketDto(ThemeBuckets.Rim, quartz, Depth: 1, Enabled: true)])))
            .Content.ReadFromJsonAsync<ThemeDetail>();

        var put = await client.PutAsJsonAsync($"/api/themes/{created!.Id}", new ThemeSaveRequest(
            "meadow at dusk", BedrockRelative: true, BedrockValue: 4, RimEdges: RimEdgeModes.Boundary, WallOnTerrainFaces: false,
            [new ThemeBucketDto(ThemeBuckets.Fill, stone, Depth: 0, Enabled: true)]));
        await Assert.That(put.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var reread = await client.GetFromJsonAsync<ThemeDetail>($"/api/themes/{created.Id}");
        await Assert.That(reread!.Name).IsEqualTo("meadow at dusk");
        await Assert.That(reread.RimEdges).IsEqualTo(RimEdgeModes.Boundary);
        await Assert.That(reread.Buckets.Count).IsEqualTo(1);
        await Assert.That(reread.Buckets[0].Bucket).IsEqualTo(ThemeBuckets.Fill);

        // The rim it no longer binds falls back to the shipping default rather than to nothing.
        var composed = await client.GetFromJsonAsync<ThemeJsonResponse>($"/api/themes/{created.Id}/json");
        await Assert.That(TerrainThemeJson.Deserialize(composed!.ThemeJson).Rim.Material)
            .IsEqualTo(TerrainTheme.Default.Rim.Material);

        await Assert.That((await client.PutAsJsonAsync("/api/themes/9999", new ThemeSaveRequest(
            "nowhere", false, 1, RimEdgeModes.Drop, true, []))).StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task A_style_a_theme_still_binds_cannot_be_deleted_and_the_refusal_names_the_theme()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();

        var bound = await CreateStyle(client, "quartz", new SolidMaterial(Blocks.QuartzBlock));
        var loose = await CreateStyle(client, "stone", new SolidMaterial(Blocks.Stone));
        await client.PostAsJsonAsync("/api/themes", new ThemeSaveRequest(
            "meadow", false, 1, RimEdgeModes.Drop, true, [new ThemeBucketDto(ThemeBuckets.Rim, bound, 1, true)]));

        var refused = await client.DeleteAsync($"/api/styles/{bound}");
        await Assert.That(refused.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
        await Assert.That((await refused.Content.ReadFromJsonAsync<StyleInUseDto>())!.Themes).Contains("meadow");

        // The unbound one goes without argument.
        await Assert.That((await client.DeleteAsync($"/api/styles/{loose}")).StatusCode).IsEqualTo(HttpStatusCode.NoContent);
    }

    [Test]
    public async Task A_draft_theme_previews_before_any_of_it_is_saved()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();

        var gold = await CreateStyle(client, "gold", new SolidMaterial(Blocks.GoldBlock));

        // A draft that binds only the fill: the picture shows that fill, and the buckets it leaves unbound keep
        // the built-in finish — which is what makes "reuse a rim, vary the surface" expressible at all.
        var preview = await (await client.PostAsJsonAsync("/api/themes/preview", new ThemeSaveRequest(
            "draft", false, 1, RimEdgeModes.Drop, true, [new ThemeBucketDto(ThemeBuckets.Fill, gold, 0, true)])))
            .Content.ReadFromJsonAsync<ThemePreviewDto>();

        await Assert.That(preview!.Section).Contains(BlockPalette.Hex(Blocks.GoldBlock, 0));
        await Assert.That(preview.Buckets[ThemeBuckets.Fill]).Contains(BlockPalette.Hex(Blocks.GoldBlock, 0));
        await Assert.That(preview.Buckets[ThemeBuckets.Rim]).Contains(BlockPalette.Hex(Blocks.QuartzBlock, 0));

        await Assert.That((await client.GetFromJsonAsync<List<ThemeSummary>>("/api/themes"))!.Count).IsEqualTo(0);
    }

    [Test]
    public async Task A_bucket_switches_off_without_being_bound_a_style_first()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();

        var gold = await CreateStyle(client, "gold", new SolidMaterial(Blocks.GoldBlock));

        // Surface over fill is a whole finish. The rim and the wall are switched off with no style named for
        // either — asking for one would be asking which colour the thing being refused should be.
        var save = new ThemeSaveRequest("bare", false, 1, RimEdgeModes.Drop, true,
        [
            new ThemeBucketDto(ThemeBuckets.Fill, gold, 0, Enabled: true),
            new ThemeBucketDto(ThemeBuckets.Rim, StyleId: 0, Depth: 1, Enabled: false),
            new ThemeBucketDto(ThemeBuckets.Wall, StyleId: 0, Depth: 0, Enabled: false),
        ]);
        var created = await (await client.PostAsJsonAsync("/api/themes", save)).Content.ReadFromJsonAsync<ThemeDetail>();

        // The refusals survive the round-trip: a styleless binding is a row, not an omission.
        var reread = await client.GetFromJsonAsync<ThemeDetail>($"/api/themes/{created!.Id}");
        var rim = reread!.Buckets.Single(b => b.Bucket == ThemeBuckets.Rim);
        await Assert.That(rim.StyleId).IsEqualTo(0L);
        await Assert.That(rim.Enabled).IsFalse();
        await Assert.That(reread.Buckets.Single(b => b.Bucket == ThemeBuckets.Wall).Enabled).IsFalse();

        // And the composed theme paints neither, over a gold body.
        var composed = TerrainThemeJson.Deserialize(
            (await client.GetFromJsonAsync<ThemeJsonResponse>($"/api/themes/{created.Id}/json"))!.ThemeJson);
        await Assert.That(composed.Rim.Enabled).IsFalse();
        await Assert.That(composed.WallEnabled).IsFalse();
        await Assert.That(composed.Fill).IsEqualTo((TerrainMaterial)new SolidMaterial(Blocks.GoldBlock));

        // The sample plateau shows it: with no rim and no wall the edge column is body all the way up.
        var preview = await (await client.PostAsJsonAsync("/api/themes/preview", save))
            .Content.ReadFromJsonAsync<ThemePreviewDto>();
        await Assert.That(preview!.Section).DoesNotContain(BlockPalette.Hex(Blocks.QuartzBlock, 0));
    }

    [Test]
    public async Task A_void_only_rim_survives_the_library_round_trip()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();

        var created = await (await client.PostAsJsonAsync("/api/themes", new ThemeSaveRequest(
            "outside only", false, 1, RimEdgeModes.Void, true, [])))
            .Content.ReadFromJsonAsync<ThemeDetail>();

        await Assert.That((await client.GetFromJsonAsync<ThemeDetail>($"/api/themes/{created!.Id}"))!.RimEdges)
            .IsEqualTo(RimEdgeModes.Void);
        var composed = await client.GetFromJsonAsync<ThemeJsonResponse>($"/api/themes/{created.Id}/json");
        await Assert.That(TerrainThemeJson.Deserialize(composed!.ThemeJson).RimEdges).IsEqualTo(RimEdges.Void);
    }

    private static async Task<long> CreateStyle(HttpClient client, string name, TerrainMaterial material)
        => (await (await client.PostAsJsonAsync("/api/styles", new StyleSaveRequest(
            name, TerrainThemeComposer.KindOf(material), TerrainThemeJson.Serialize(material))))
            .Content.ReadFromJsonAsync<StyleDto>())!.Id;

    [Test]
    public async Task A_malformed_theme_import_is_400_not_500()
    {
        await ApiTestFactory.ResetSchemaAsync();
        using var client = ApiTestFactory.Shared.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/themes/import", new ThemeImportRequest("bad", "{ not json"));
        await Assert.That(resp.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }
}
