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

    /// <summary>
    /// <b>A solid turns the direction it states with the image it is painted on.</b> A ladder stated looking
    /// north on the authored half looks south on a half-turned image, and a stair stated climbing east climbs
    /// west there; with no turn, and for a block that faces no way at all, the solid is exactly what it
    /// states.
    /// </summary>
    [Test]
    public async Task A_solid_turns_what_it_states_with_the_image_it_is_painted_on()
    {
        Func<int, int, (int X, int Z)> halfTurn = (dx, dz) => (-dx, -dz);
        var authored = new BucketContext(0, 0, 0, TerrainBucket.Fill, 0);
        var image = authored with { Turn = halfTurn };
        const int LookingNorth = 2, LookingSouth = 3, ClimbingEast = 0, ClimbingWest = 1, UpsideDown = 4;

        await Assert.That(new SolidMaterial(Blocks.Ladder, LookingNorth).Resolve(in authored)).IsEqualTo((Blocks.Ladder, LookingNorth));
        await Assert.That(new SolidMaterial(Blocks.Ladder, LookingNorth).Resolve(in image)).IsEqualTo((Blocks.Ladder, LookingSouth));
        await Assert.That(new SolidMaterial(Blocks.OakStairs, ClimbingEast | UpsideDown).Resolve(in image))
            .IsEqualTo((Blocks.OakStairs, ClimbingWest | UpsideDown));
        await Assert.That(new SolidMaterial(Blocks.Wool, 14).Resolve(in image)).IsEqualTo((Blocks.Wool, 14));
    }
}
