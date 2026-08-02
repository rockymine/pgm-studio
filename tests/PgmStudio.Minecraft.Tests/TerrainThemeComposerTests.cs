using PgmStudio.Minecraft;

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
    public async Task A_pattern_material_survives_the_trip()
    {
        var theme = TerrainTheme.Default with
        {
            Surface = new TopBand(new VoronoiMaterial(7, 8, [new SolidMaterial(1), new SolidMaterial(24)]), Depth: 2),
        };
        var decomposed = TerrainThemeComposer.Decompose(theme);
        await Assert.That(decomposed.Buckets.First(b => b.Bucket == TerrainBucket.Surface).Kind).IsEqualTo("voronoi");
        var recomposed = TerrainThemeComposer.Compose(decomposed);
        await Assert.That(TerrainThemeJson.Serialize(recomposed)).IsEqualTo(TerrainThemeJson.Serialize(theme));
    }
}
