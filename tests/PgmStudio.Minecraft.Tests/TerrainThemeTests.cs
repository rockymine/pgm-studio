using PgmStudio.Minecraft.Painting;
using PgmStudio.Minecraft.Palette;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// The built-in theme (docs/world-export/terrain-painting.md §5): unthemed ground states no finish, so every
/// themeable bucket resolves to stone, the same block the fill already claims whatever no bucket paints.
/// </summary>
public sealed class TerrainThemeTests
{
    [Test]
    [Arguments(TerrainBucket.Rim)]
    [Arguments(TerrainBucket.Surface)]
    [Arguments(TerrainBucket.Wall)]
    [Arguments(TerrainBucket.Fill)]
    public async Task The_default_resolves_every_themeable_bucket_to_stone(TerrainBucket bucket)
    {
        var material = TerrainTheme.Default.MaterialFor(bucket);
        var ctx = new BucketContext(0, 0, 0, bucket, DepthFromTop: 0);
        await Assert.That(material.Resolve(in ctx)).IsEqualTo((Blocks.Stone, 0));
    }
}
