using PgmStudio.Contracts;
using PgmStudio.Data.Schema;
using PgmStudio.Data.Theme;
using PgmStudio.Vocabulary;

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

        var id = await store.CreateStyleAsync(new StyleRow { Name = "cobble patches", Kind = MaterialKind.Voronoi, Params = "{\"kind\":\"voronoi\"}" });
        await store.CreateStyleAsync(new StyleRow { Name = "stone", Kind = MaterialKind.Solid, Params = "{\"kind\":\"solid\"}" });

        var voronoi = await store.ListStylesAsync(MaterialKind.Voronoi);
        await Assert.That(voronoi.Count).IsEqualTo(1);
        await Assert.That(voronoi[0].Name).IsEqualTo("cobble patches");
        await Assert.That((await store.ListStylesAsync()).Count).IsEqualTo(2);
        await Assert.That((await store.GetStyleAsync(id))!.Kind).IsEqualTo(MaterialKind.Voronoi);
    }

    [Test]
    public async Task A_theme_composes_from_styles_and_cascades_on_delete()
    {
        await TestDb.ResetSchemaAsync();
        await using var db = TestDb.Connect();
        var store = new ThemeStore(db);

        var rim = await store.CreateStyleAsync(new StyleRow { Name = "quartz", Kind = MaterialKind.Solid, Params = "{}" });
        var fill = await store.CreateStyleAsync(new StyleRow { Name = "stone", Kind = MaterialKind.Solid, Params = "{}" });
        var themeId = await store.CreateThemeAsync(
            new ThemeRow { Name = "meadow", BedrockValue = 1 },
            new[]
            {
                new ThemeBucketRow { Bucket = ThemeBuckets.Rim, StyleId = rim, Depth = 1, Enabled = true },
                new ThemeBucketRow { Bucket = ThemeBuckets.Fill, StyleId = fill, Depth = 0, Enabled = true },
            });

        var joined = await store.GetBucketStylesAsync(themeId);
        await Assert.That(joined.Count).IsEqualTo(2);
        await Assert.That(joined.Any(j => j.Bucket.Bucket == ThemeBuckets.Rim && j.Style.Name == "quartz")).IsTrue();

        await store.DeleteThemeAsync(themeId);
        await Assert.That((await store.GetBucketsAsync(themeId)).Count).IsEqualTo(0);   // bindings cascaded
        await Assert.That((await store.ListStylesAsync()).Count).IsEqualTo(2);          // styles survive
    }

    [Test]
    public async Task Updating_a_theme_replaces_its_whole_set_of_bindings()
    {
        await TestDb.ResetSchemaAsync();
        await using var db = TestDb.Connect();
        var store = new ThemeStore(db);

        var quartz = await store.CreateStyleAsync(new StyleRow { Name = "quartz", Kind = MaterialKind.Solid, Params = "{}" });
        var stone = await store.CreateStyleAsync(new StyleRow { Name = "stone", Kind = MaterialKind.Solid, Params = "{}" });
        var themeId = await store.CreateThemeAsync(
            new ThemeRow { Name = "meadow", BedrockValue = 1 },
            [new ThemeBucketRow { Bucket = ThemeBuckets.Rim, StyleId = quartz, Depth = 1, Enabled = true }]);

        // The rim is rebound to another style, the fill is new, and the knobs move — one call, one transaction.
        var updated = await store.UpdateThemeAsync(themeId,
            new ThemeRow { Name = "meadow at dusk", BedrockRelative = true, BedrockValue = 4, RimEdges = RimEdgeModes.Boundary },
            [
                new ThemeBucketRow { Bucket = ThemeBuckets.Rim, StyleId = stone, Depth = 2, Enabled = false },
                new ThemeBucketRow { Bucket = ThemeBuckets.Fill, StyleId = stone, Depth = 0, Enabled = true },
            ]);
        await Assert.That(updated).IsTrue();

        var row = await store.GetThemeAsync(themeId);
        await Assert.That(row!.Name).IsEqualTo("meadow at dusk");
        await Assert.That(row.BedrockRelative).IsTrue();
        await Assert.That(row.RimEdges).IsEqualTo(RimEdgeModes.Boundary);

        var buckets = await store.GetBucketsAsync(themeId);
        await Assert.That(buckets.Count).IsEqualTo(2);
        var rim = buckets.First(b => b.Bucket == ThemeBuckets.Rim);
        await Assert.That(rim.StyleId).IsEqualTo(stone);
        await Assert.That(rim.Depth).IsEqualTo(2);
        await Assert.That(rim.Enabled).IsFalse();

        await Assert.That(await store.UpdateThemeAsync(themeId + 999, new ThemeRow { Name = "nowhere" }, [])).IsFalse();
    }

    [Test]
    public async Task A_style_knows_which_themes_still_bind_it()
    {
        await TestDb.ResetSchemaAsync();
        await using var db = TestDb.Connect();
        var store = new ThemeStore(db);

        var bound = await store.CreateStyleAsync(new StyleRow { Name = "quartz", Kind = MaterialKind.Solid, Params = "{}" });
        var loose = await store.CreateStyleAsync(new StyleRow { Name = "stone", Kind = MaterialKind.Solid, Params = "{}" });
        await store.CreateThemeAsync(new ThemeRow { Name = "meadow" },
            [new ThemeBucketRow { Bucket = ThemeBuckets.Rim, StyleId = bound, Depth = 1, Enabled = true }]);
        await store.CreateThemeAsync(new ThemeRow { Name = "desert" },
            [new ThemeBucketRow { Bucket = ThemeBuckets.Rim, StyleId = bound, Depth = 1, Enabled = true }]);

        await Assert.That((await store.ThemesUsingStyleAsync(bound)).Order()).IsEquivalentTo(new[] { "desert", "meadow" });
        await Assert.That((await store.ThemesUsingStyleAsync(loose)).Count).IsEqualTo(0);
    }
}
