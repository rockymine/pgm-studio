using PgmStudio.Minecraft;
using PgmStudio.Minecraft.Painting;
using PgmStudio.Vocabulary;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// The theme ↔ library-pieces round-trip (B44): decomposing a theme into per-bucket styles + knobs and
/// recomposing it yields the exact theme the painter consumes, the buckets carry the right kinds, and a
/// pattern material survives the trip.
/// </summary>
public sealed class TerrainThemeComposerTests
{
    [Test]
    public async Task The_default_theme_round_trips_losslessly()
    {
        var recomposed = TerrainThemeComposer.Compose(TerrainThemeComposer.Decompose(TerrainTheme.Default));
        await Assert.That(TerrainThemeJson.Serialize(recomposed))
            .IsEqualTo(TerrainThemeJson.Serialize(TerrainTheme.Default));
    }

    [Test]
    public async Task Decompose_yields_one_binding_per_themeable_bucket_with_its_kind()
    {
        var decomposed = TerrainThemeComposer.Decompose(TerrainTheme.Default);
        await Assert.That(decomposed.Buckets.Count).IsEqualTo(4);

        string Kind(TerrainBucket bucket) => decomposed.Buckets.First(b => b.Bucket == bucket).Kind;
        await Assert.That(Kind(TerrainBucket.Rim)).IsEqualTo("solid");        // quartz
        await Assert.That(Kind(TerrainBucket.Surface)).IsEqualTo("layered");  // grass over two dirt
        await Assert.That(Kind(TerrainBucket.Wall)).IsEqualTo("teamTint");    // team-tinted clay
        await Assert.That(Kind(TerrainBucket.Fill)).IsEqualTo("solid");       // stone
    }

    [Test]
    public async Task Bucket_depth_and_toggle_survive_the_trip()
    {
        var decomposed = TerrainThemeComposer.Decompose(TerrainTheme.Default);
        var surface = decomposed.Buckets.First(b => b.Bucket == TerrainBucket.Surface);
        await Assert.That(surface.Depth).IsEqualTo(3);          // grass over two dirt, three deep
        await Assert.That(surface.Enabled).IsTrue();
        var wall = decomposed.Buckets.First(b => b.Bucket == TerrainBucket.Wall);
        await Assert.That(wall.Enabled).IsTrue();               // WallEnabled default
    }

    [Test]
    public async Task A_binding_that_names_no_style_still_switches_its_bucket_off()
    {
        // What a theme with no rim decomposes to: a binding carrying the toggle and nothing else. The bucket
        // keeps the built-in material — it simply never paints it — so the refusal costs no style.
        var decomposed = new DecomposedTheme(BedrockRelative: false, BedrockValue: 1, RimEdges.Drop,
            WallOnTerrainFaces: true,
            [
                new ThemeStyleBinding(TerrainBucket.Rim, Kind: null, MaterialJson: null, Depth: 1, Enabled: false),
                new ThemeStyleBinding(TerrainBucket.Wall, Kind: null, MaterialJson: null, Depth: 0, Enabled: false),
            ]);
        var theme = TerrainThemeComposer.Compose(decomposed);

        await Assert.That(theme.Rim.Enabled).IsFalse();
        await Assert.That(theme.WallEnabled).IsFalse();
        await Assert.That(theme.Rim.Material).IsEqualTo(TerrainTheme.Default.Rim.Material);
        await Assert.That(theme.Wall).IsEqualTo(TerrainTheme.Default.Wall);
        // The buckets it names nothing about are untouched — a missing binding is still "keep the default".
        await Assert.That(theme.Surface.Enabled).IsTrue();
        await Assert.That(theme.Fill).IsEqualTo(TerrainTheme.Default.Fill);
    }

    [Test]
    public async Task The_rim_edge_mode_survives_the_trip()
    {
        var theme = TerrainTheme.Default with { RimEdges = RimEdges.Void };
        var recomposed = TerrainThemeComposer.Compose(TerrainThemeComposer.Decompose(theme));
        await Assert.That(recomposed.RimEdges).IsEqualTo(RimEdges.Void);
    }

    [Test]
    public async Task A_pattern_material_survives_the_trip()
    {
        var theme = TerrainTheme.Default with
        {
            Surface = new TopBand(new VoronoiMaterial(7, 8,
                [new VoronoiBand(new SolidMaterial(1), 1), new VoronoiBand(new SolidMaterial(24), 1)]), Depth: 2),
        };
        var decomposed = TerrainThemeComposer.Decompose(theme);
        await Assert.That(decomposed.Buckets.First(b => b.Bucket == TerrainBucket.Surface).Kind).IsEqualTo("voronoi");
        var recomposed = TerrainThemeComposer.Compose(decomposed);
        await Assert.That(TerrainThemeJson.Serialize(recomposed)).IsEqualTo(TerrainThemeJson.Serialize(theme));
    }
}
