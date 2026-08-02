using PgmStudio.Data.Schema;
using PgmStudio.Data.Theme;

namespace PgmStudio.Data.Tests;

/// <summary>
/// The theme/style library store (M0011, B44): styles are browsable by kind, a theme is created with its bucket
/// bindings in one go, its buckets join to the styles they reference, and deleting a theme cascades its bindings
/// while leaving the styles. Runs against <c>pgm_studio_test</c>; each test resets the schema.
/// </summary>
[NotInParallel]
public sealed class ThemeStoreTests
{
    [Test]
    public async Task Styles_are_browsable_by_kind()
    {
        await TestDb.ResetSchemaAsync();
        await using var db = TestDb.Connect();
        var store = new ThemeStore(db);

        var id = await store.CreateStyleAsync(new StyleRow { Name = "cobble patches", Kind = StyleKind.Voronoi, Params = "{\"kind\":\"voronoi\"}" });
        await store.CreateStyleAsync(new StyleRow { Name = "stone", Kind = StyleKind.Solid, Params = "{\"kind\":\"solid\"}" });

        var voronoi = await store.ListStylesAsync(StyleKind.Voronoi);
        await Assert.That(voronoi.Count).IsEqualTo(1);
        await Assert.That(voronoi[0].Name).IsEqualTo("cobble patches");
        await Assert.That((await store.ListStylesAsync()).Count).IsEqualTo(2);
        await Assert.That((await store.GetStyleAsync(id))!.Kind).IsEqualTo(StyleKind.Voronoi);
    }

    [Test]
    public async Task A_theme_composes_from_styles_and_cascades_on_delete()
    {
        await TestDb.ResetSchemaAsync();
        await using var db = TestDb.Connect();
        var store = new ThemeStore(db);

        var rim = await store.CreateStyleAsync(new StyleRow { Name = "quartz", Kind = StyleKind.Solid, Params = "{}" });
        var fill = await store.CreateStyleAsync(new StyleRow { Name = "stone", Kind = StyleKind.Solid, Params = "{}" });
        var themeId = await store.CreateThemeAsync(
            new ThemeRow { Name = "meadow", BedrockValue = 1 },
            new[]
            {
                new ThemeBucketRow { Bucket = ThemeBucket.Rim, StyleId = rim, Depth = 1, Enabled = true },
                new ThemeBucketRow { Bucket = ThemeBucket.Fill, StyleId = fill, Depth = 0, Enabled = true },
            });

        var joined = await store.GetBucketStylesAsync(themeId);
        await Assert.That(joined.Count).IsEqualTo(2);
        await Assert.That(joined.Any(j => j.Bucket.Bucket == ThemeBucket.Rim && j.Style.Name == "quartz")).IsTrue();

        await store.DeleteThemeAsync(themeId);
        await Assert.That((await store.GetBucketsAsync(themeId)).Count).IsEqualTo(0);   // bindings cascaded
        await Assert.That((await store.ListStylesAsync()).Count).IsEqualTo(2);          // styles survive
    }
}
