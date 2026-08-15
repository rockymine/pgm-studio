namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// Bands along a distance — the one rule a layer stack, a room part and (before them) two hand-written
/// traversals each spelled for themselves. What is tested here is the traversal and its <b>ending</b>, since
/// the ending is the half that had been assumed: a stack that owns its whole space repeats, and one that is a
/// band inside a larger space hands over, and neither is implied by the axis.
/// </summary>
public sealed class BandStackTests
{
    private static readonly TerrainMaterial First = new SolidMaterial(1);
    private static readonly TerrainMaterial Second = new SolidMaterial(2);

    [Test]
    public async Task A_step_lands_in_the_band_that_claims_it_and_says_how_far_into_it_it_is()
    {
        var stack = new BandStack([new Band(First, 2), new Band(Second, 3)]);

        await Assert.That(stack.At(0)).IsEqualTo(((TerrainMaterial?)First, 0));
        await Assert.That(stack.At(1)).IsEqualTo(((TerrainMaterial?)First, 1));
        await Assert.That(stack.At(2)).IsEqualTo(((TerrainMaterial?)Second, 0));
        await Assert.That(stack.At(4)).IsEqualTo(((TerrainMaterial?)Second, 2));
    }

    /// <summary>The ending is the whole of the difference past the last band, and the step keeps counting —
    /// a repeating band is one field read further along, not the same read again.</summary>
    [Test]
    public async Task Past_the_last_band_a_repeating_stack_repeats_and_a_handing_one_claims_nothing()
    {
        var repeats = new BandStack([new Band(First, 2), new Band(Second, 1)]);
        var hands = repeats with { Ending = BandEnding.HandOver };

        await Assert.That(repeats.At(5)).IsEqualTo(((TerrainMaterial?)Second, 2));
        await Assert.That(hands.At(5)).IsEqualTo(((TerrainMaterial?)null, 2));
    }

    /// <summary>A stack with no bands claims nothing whatever its ending says, because there is nothing to
    /// repeat. What shows there is the caller's answer, and the two callers give different ones — a room part
    /// is air, a layer stack is stone — which is exactly why the stack does not invent one.</summary>
    [Test]
    public async Task A_stack_with_no_bands_claims_nothing()
    {
        await Assert.That(new BandStack([]).At(0).Material).IsNull();
        await Assert.That(new RoomPart(new BandStack([]), 3).At(0).Material).IsEqualTo(new SolidMaterial(Blocks.Air));
    }

    /// <summary>Band for band, not list for list — the comparison a snapshot round trip is made of, and the
    /// reason both holders can now use their own generated equality.</summary>
    [Test]
    public async Task Two_stacks_of_the_same_bands_are_the_same_stack()
    {
        await Assert.That(new BandStack([new Band(First, 2)])).IsEqualTo(new BandStack([new Band(First, 2)]));
        await Assert.That(new RoomPart(new BandStack([new Band(First, 2)]), 5))
            .IsEqualTo(new RoomPart(new BandStack([new Band(First, 2)]), 5));
        await Assert.That(new LayeredMaterial(new BandStack([new Band(First)])))
            .IsEqualTo(new LayeredMaterial(new BandStack([new Band(First)])));
    }

    /// <summary>The ending is not part of the bands: two stacks of identical bands that end differently are
    /// different stacks, or a stored rim would read back as a rim repeating forever.</summary>
    [Test]
    public async Task An_ending_is_part_of_what_a_stack_is()
    {
        var bands = new[] { new Band(First, 2) };
        await Assert.That(new BandStack(bands)).IsNotEqualTo(new BandStack(bands, BandEnding.HandOver));
    }

    /// <summary>A repeating band does not reseed a pattern. The seed is on the material and the field is
    /// sampled from world coordinates, so the second block of a repeat is the same field read one step
    /// further along rather than a second field.</summary>
    [Test]
    public async Task A_repeating_band_reads_one_field_rather_than_starting_a_second()
    {
        var pattern = new VoronoiMaterial(7, 8, [new VoronoiBand(First, 1), new VoronoiBand(Second, 1)]);
        var stack = new LayeredMaterial(new BandStack([new Band(pattern, 1)]));

        // Two columns of the same stack, four courses deep: the answer at a cell is the pattern's answer at
        // that cell, whichever band of the repeat it fell in.
        for (var depth = 0; depth < 4; depth++)
        {
            var ctx = new BucketContext(11, 64 - depth, 5, TerrainBucket.Surface, depth);
            await Assert.That(stack.Resolve(in ctx)).IsEqualTo(pattern.Resolve(in ctx));
        }
    }
}
