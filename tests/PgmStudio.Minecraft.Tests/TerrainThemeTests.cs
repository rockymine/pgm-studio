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

    // ── beds that dip and fold ─────────────────────────────────────────────────────────────────

    private const int BedColours = 16;

    /// <summary>Sixteen one-course beds, each its own wool colour, handing over past the last, so the colour a
    /// block reads names the bed it is in and the lowest block of that colour is where the bed begins.</summary>
    private static LayeredMaterial Beds(int from = 0, BedDip? dip = null, double fold = 0)
        => new(new BandStack([.. Enumerable.Range(0, BedColours).Select(i => new Band(new SolidMaterial(Blocks.Wool, i)))],
                             BandEnding.HandOver),
               BandAxis.Height, From: from, Dip: dip, Fold: fold);

    /// <summary>The world Y at which <paramref name="bed"/> begins in the column <c>(x, z)</c>.</summary>
    private static int BedStart(LayeredMaterial beds, int bed, int x, int z)
    {
        for (var y = -150; y < 150; y++)
            if (beds.Resolve(new BucketContext(x, y, z, TerrainBucket.Fill, 0)) == (Blocks.Wool, bed))
                return y;
        throw new InvalidOperationException($"bed {bed} is nowhere in column ({x}, {z})");
    }

    /// <summary><b>A dip of 0.5 east lifts every bed one block for each two blocks east.</b> Bed 3 begins at
    /// y 3 at the origin and at y 3 + x/2 in every even column east of it; the south component lifts along z the
    /// same way, and a negative one lowers the beds.</summary>
    [Test]
    public async Task A_dip_lifts_the_beds_along_the_plane_it_states()
    {
        var beds = Beds(dip: new BedDip(0.5, 0));
        for (var x = 0; x <= 30; x += 2)
            await Assert.That((x, BedStart(beds, 3, x, z: 0))).IsEqualTo((x, 3 + x / 2));
        await Assert.That(BedStart(beds, 3, x: 1, z: 0)).IsEqualTo(4);

        var south = Beds(dip: new BedDip(0, -0.25));
        await Assert.That(BedStart(south, 3, x: 17, z: 8)).IsEqualTo(1);
        await Assert.That(BedStart(south, 3, x: -40, z: 8)).IsEqualTo(1);
    }

    /// <summary><b>A stack stating neither reads exactly as a level one.</b> A fold of nought, an absent dip and
    /// a dip that is level all answer the bed the stack's own <c>From</c> and the block's Y say, in every column.</summary>
    [Test]
    public async Task A_stack_with_no_dip_and_no_fold_is_level()
    {
        var level = Beds(from: 5);
        foreach (var beds in new[] { Beds(from: 5, fold: 0), Beds(from: 5, dip: new BedDip(0, 0)) })
            for (var x = -60; x <= 60; x += 15)
            for (var y = 0; y < 30; y++)
            {
                var ctx = new BucketContext(x, y, x / 3, TerrainBucket.Fill, 0);
                await Assert.That(beds.Resolve(in ctx)).IsEqualTo(level.Resolve(in ctx));
                await Assert.That(level.Resolve(in ctx))
                    .IsEqualTo(y < 5 ? (Blocks.Stone, 0) : level.Stack.At(y - 5).Material?.Resolve(in ctx) ?? (Blocks.Stone, 0));
            }
    }

    /// <summary><b>A fold moves a bed by at most the fold it states, and moves it.</b> Over a wide sweep of
    /// columns no bed strays further than 6 blocks from where the level stack puts it, and the folds are not a
    /// constant: the beds sit at several different heights.</summary>
    [Test]
    public async Task A_fold_bends_the_beds_by_no_more_than_it_states()
    {
        const int Fold = 6;
        var folded = Beds(fold: Fold);
        var starts = new List<int>();
        for (var x = -120; x <= 120; x += 7)
        for (var z = -120; z <= 120; z += 11)
            starts.Add(BedStart(folded, 8, x, z) - 8);

        await Assert.That(starts.Max(Math.Abs)).IsLessThanOrEqualTo(Fold);
        await Assert.That(starts.Distinct().Count()).IsGreaterThan(3);
    }

    /// <summary>The dip and the fold are added to the datum: a column lifted by a dip and bent by a fold sits
    /// where the dip alone puts it, within the fold.</summary>
    [Test]
    public async Task A_dip_and_a_fold_add_to_one_datum()
    {
        var dipped = Beds(dip: new BedDip(0.5, 0.5));
        var both = Beds(dip: new BedDip(0.5, 0.5), fold: 4);
        for (var x = -40; x <= 40; x += 9)
        for (var z = -40; z <= 40; z += 13)
            await Assert.That(Math.Abs(BedStart(both, 5, x, z) - BedStart(dipped, 5, x, z))).IsLessThanOrEqualTo(4);
    }

    /// <summary><b>The plane and the noise are read at the folded cell.</b> Two columns of one orbit share a
    /// sample, so they resolve alike whatever their own coordinates are — the rule that keeps a mirrored board
    /// symmetric.</summary>
    [Test]
    public async Task The_dip_and_the_fold_are_read_at_the_sampled_cell()
    {
        var beds = Beds(dip: new BedDip(0.5, -0.25), fold: 5);
        for (var y = -20; y < 40; y++)
        {
            var authored = new BucketContext(14, y, 9, TerrainBucket.Fill, 0);
            var image = new BucketContext(-14, y, -9, TerrainBucket.Fill, 0) { Sample = (14, 9) };
            await Assert.That(beds.Resolve(in image)).IsEqualTo(beds.Resolve(in authored));
        }
    }
}
