using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using PgmStudio.Minecraft.Painting;
using PgmStudio.Minecraft.Houses;
using PgmStudio.Minecraft.Palette;
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

/// <summary>
/// The words the editor spells an axis and an ending with are the words the painter reads.
///
/// <para>The client cannot see <c>BandAxis</c> or <c>BandEnding</c> — they live in <c>Minecraft</c>, which
/// WASM does not reference — so the offered words live in <c>Vocabulary</c>, which both reach. Two lists for
/// one closed set is exactly the drift this pins: a word added to the enum and not to the set is a reading
/// the editor cannot author, and one added to the set alone is a theme the painter answers stone for.</para>
/// </summary>
public sealed class BandWordTests
{
    [Test]
    public async Task Every_axis_the_painter_reads_is_a_word_the_editor_offers()
    {
        var enumWords = Enum.GetNames<BandAxis>().Select(JsonNamingPolicy.CamelCase.ConvertName).Order();
        await Assert.That(PgmStudio.Vocabulary.BandAxes.All.Order()).IsEquivalentTo(enumWords);
    }

    [Test]
    public async Task Every_ending_the_painter_reads_is_a_word_the_editor_offers()
    {
        var enumWords = Enum.GetNames<BandEnding>().Select(JsonNamingPolicy.CamelCase.ConvertName).Order();
        await Assert.That(PgmStudio.Vocabulary.BandEndings.All.Order()).IsEquivalentTo(enumWords);
    }

    [Test]
    [Arguments(PgmStudio.Vocabulary.BandAxes.Inward, BandAxis.Inward)]
    [Arguments(PgmStudio.Vocabulary.BandAxes.Height, BandAxis.Height)]
    [Arguments(PgmStudio.Vocabulary.BandAxes.Depth, BandAxis.Depth)]
    public async Task An_axis_word_deserializes_to_the_axis_it_names(string word, BandAxis axis)
    {
        var json = """{"kind":"layered","axis":"AXIS","stack":{"ending":"handOver","bands":[]}}"""
            .Replace("AXIS", word);
        var read = JsonSerializer.Deserialize<TerrainMaterial>(json, TerrainThemeJson.Options);
        await Assert.That(((LayeredMaterial)read!).Axis).IsEqualTo(axis);
        await Assert.That(((LayeredMaterial)read!).Stack.Ending).IsEqualTo(BandEnding.HandOver);
    }
}

/// <summary>
/// The JSON the Theme editor writes for a layered material is the JSON the painter reads.
///
/// <para>The editor speaks the wire form directly — <c>axis</c> and <c>beyond</c> on the material, the bands
/// and their <c>ending</c> under <c>stack</c> — rather than the flat <c>layers</c> list a stored theme may
/// still carry. The upgrade that reads the old list forward always ends it <c>repeat</c>, because that is what
/// every list written before there was an ending meant, and it is the reason a ring stack could not be
/// authored through it: an inward stack is the first thing that has to hand over.</para>
/// </summary>
public sealed class LayeredAuthoringTests
{
    private static readonly BucketContext Ctx = new(
        4, 9, 4, TerrainBucket.Surface, DepthFromTop: 0, TeamData: -1,
        PerimeterArc: 0, HeightFromBottom: 0, PerimeterTurn: 0, PerimeterRun: 0, Inset: 0);

    private static LayeredMaterial Read(string json)
        => (LayeredMaterial)JsonSerializer.Deserialize<TerrainMaterial>(json, TerrainThemeJson.Options)!;

    [Test]
    public async Task A_rim_two_rings_in_then_the_ground_is_what_the_editor_writes()
    {
        // The author's own sequence: a cobble rim, two rings of stone brick, then whatever is under it.
        var material = Read("""
            {"kind":"layered","axis":"inward","beyond":{"kind":"solid","id":2,"data":0},
             "stack":{"ending":"handOver","bands":[
               {"material":{"kind":"solid","id":4,"data":0},"thickness":1},
               {"material":{"kind":"solid","id":98,"data":0},"thickness":2}]}}
            """);

        await Assert.That(material.Axis).IsEqualTo(BandAxis.Inward);
        await Assert.That(material.Stack.Ending).IsEqualTo(BandEnding.HandOver);
        await Assert.That(material.Resolve(Ctx with { Inset = 0 }).Id).IsEqualTo(4);    // the rim
        await Assert.That(material.Resolve(Ctx with { Inset = 1 }).Id).IsEqualTo(98);   // ring one
        await Assert.That(material.Resolve(Ctx with { Inset = 2 }).Id).IsEqualTo(98);   // ring two
        await Assert.That(material.Resolve(Ctx with { Inset = 3 }).Id).IsEqualTo(2);    // handed over
        await Assert.That(material.Resolve(Ctx with { Inset = 40 }).Id).IsEqualTo(2);
    }

    [Test]
    public async Task A_stored_flat_list_still_reads_and_still_repeats()
    {
        // What a theme written before the stack had an ending carries. The editor no longer writes this, and
        // reading it forward is why it may not: every such list owned its whole space, so it ends `repeat`.
        var theme = TerrainThemeJson.Deserialize("""
            {"fill":{"kind":"layered",
              "layers":[{"material":{"kind":"solid","id":2,"data":0},"thickness":1}]}}
            """);
        var material = (LayeredMaterial)theme.Fill;

        await Assert.That(material.Axis).IsEqualTo(BandAxis.Depth);
        await Assert.That(material.Stack.Ending).IsEqualTo(BandEnding.Repeat);
        await Assert.That(material.Resolve(Ctx with { DepthFromTop = 9 }).Id).IsEqualTo(2);
    }
}
