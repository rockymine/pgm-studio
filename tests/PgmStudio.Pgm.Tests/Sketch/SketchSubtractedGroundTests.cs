using PgmStudio.Pgm.Sketch;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Tests.Sketch;

/// <summary>
/// What a subtract states and what a later add does to it (<c>SK13</c>). A subtract is a board's negative
/// space — the void a plan's buffer pieces compile to, the hole a composed footprint leaves — and a hole is
/// never scenery, so an add that <b>fills</b> one is refused and an add that draws <b>nothing</b> there
/// complains.
///
/// <para>What separates that from a donut is the <b>order, within one layer</b>. A body and the hole cut out
/// of it are written in that order, so a subtract following an add on that add's own layer is its hole and
/// says nothing. Across layers the order is a height rather than a sequence, so an add on another layer is a
/// fill wherever it sits.</para>
///
/// <para>What separates a fill from a <b>lid</b> is the floor. A layer holds one span per column, so an
/// override add resting above the subtract's floor moves that span up and records nothing under it: a deck
/// over a cut, with the void still under it.</para>
/// </summary>
public sealed class SketchSubtractedGroundTests
{
    private const string Setup = @"""setup"":{""mirror_mode"":""none"",""center"":{""cx"":0,""cz"":0}}";

    private static string Rect(string id, string operation, int minX, int minZ, int maxX, int maxZ,
                               bool over = false, int floor = 0) =>
        $@"{{""id"":""{id}"",""type"":""rectangle"",""operation"":""{operation}"",""override"":{(over ? "true" : "false")},"
        + $@"""min_x"":{minX},""min_z"":{minZ},""max_x"":{maxX},""max_z"":{maxZ},""floor"":{floor},""base_height"":8}}";

    private static string Layer(string id, params string[] shapes) =>
        $@"{{""id"":""{id}"",""base_y"":0,""layout"":{{""shapes"":[{string.Join(",", shapes)}],""groups"":[]}}}}";

    private static string Board(params string[] layers) =>
        "{" + Setup + @",""layers"":[" + string.Join(",", layers) + "]}";

    private static SketchLayout? Read(string json) => SketchLayout.Parse(json);

    [Test]
    public async Task A_hole_cut_out_of_the_body_it_belongs_to_says_nothing()
    {
        // The donut: an exterior, then the ring taken out of it. This is how every simplified island and
        // every hand-drawn hole is written, so reading it as a fault would flag the ordinary case.
        var board = Read(Board(Layer("ground",
            Rect("island", "add", 0, 0, 40, 40),
            Rect("hole", "subtract", 10, 10, 20, 20))));

        await Assert.That(SketchRasterizer.AddsOverSubtracts(board)).IsEmpty();
    }

    [Test]
    public async Task A_plain_add_drawn_over_a_subtract_draws_nothing_and_is_reported()
    {
        var board = Read(Board(Layer("ground",
            Rect("island", "add", 0, 0, 40, 40),
            Rect("hole", "subtract", 10, 10, 20, 20),
            Rect("pool", "add", 12, 12, 18, 18))));

        var over = SketchRasterizer.AddsOverSubtracts(board).Single();
        await Assert.That(over.Add).IsEqualTo("pool");
        await Assert.That(over.Subtract).IsEqualTo("hole");
        await Assert.That(over.Survives).IsFalse();
        await Assert.That(over.Cells).IsEqualTo(6 * 6);   // a rectangle is max-exclusive

        var finding = SketchLayoutCheck.Check(board).Single(f => f.Rule == SketchRules.DrawnOverSubtraction);
        await Assert.That(finding.Severity).IsEqualTo(Severity.Complaint);   // the board builds without it
        await Assert.That(finding.Message).Contains("draws nothing");
        await Assert.That(finding.Subjects).IsEquivalentTo(new[] { "pool", "hole" });
    }

    [Test]
    public async Task An_override_add_over_a_subtract_puts_the_ground_back()
    {
        var board = Read(Board(Layer("ground",
            Rect("island", "add", 0, 0, 40, 40),
            Rect("hole", "subtract", 10, 10, 20, 20),
            Rect("pool", "add", 12, 12, 18, 18, over: true))));

        var over = SketchRasterizer.AddsOverSubtracts(board).Single();
        await Assert.That(over.Survives).IsTrue();

        // A hole is never scenery, so putting ground back in one is refused rather than remarked on.
        var finding = SketchLayoutCheck.Check(board).Single(f => f.Rule == SketchRules.DrawnOverSubtraction);
        await Assert.That(finding.Severity).IsEqualTo(Severity.Refusal);
        await Assert.That(finding.Message).Contains("fills");
    }

    [Test]
    public async Task An_add_on_another_layer_over_a_subtract_puts_the_ground_back_too()
    {
        // A subtract reaches only the layer it is on, so a slab over a composed hole fills it whatever the
        // override flag says — and wherever the layer sits in the document, since a layer's place in the stack
        // is a height and a slab written first is written below.
        var board = Read(Board(
            Layer("pool", Rect("water", "add", 12, 12, 18, 18)),
            Layer("ground", Rect("island", "add", 0, 0, 40, 40), Rect("hole", "subtract", 10, 10, 20, 20))));

        var over = SketchRasterizer.AddsOverSubtracts(board).Single();
        await Assert.That(over.Add).IsEqualTo("water");
        await Assert.That(over.AddLayer).IsEqualTo("pool");
        await Assert.That(over.SubtractLayer).IsEqualTo("ground");
        await Assert.That(over.Survives).IsTrue();
    }

    [Test]
    public async Task An_override_add_standing_above_the_hole_is_a_lid_and_says_nothing()
    {
        // A deck over a cut. The column's one span moves up to the deck and nothing is recorded beneath it,
        // so the negative space the subtract states is still negative — only the roof over it is new.
        var board = Read(Board(Layer("ground",
            Rect("island", "add", 0, 0, 40, 40),
            Rect("gullet", "subtract", 10, 10, 20, 20),
            Rect("deck", "add", 12, 12, 18, 18, over: true, floor: 13))));

        await Assert.That(SketchRasterizer.AddsOverSubtracts(board)).IsEmpty();
    }

    [Test]
    public async Task An_override_add_resting_at_the_holes_own_floor_fills_it()
    {
        // The same deck with its floor left at the bottom: the span runs from the world's floor to its top,
        // which is the channel refilled as ordinary ground rather than bridged.
        var board = Read(Board(Layer("ground",
            Rect("island", "add", 0, 0, 40, 40),
            Rect("gullet", "subtract", 10, 10, 20, 20),
            Rect("deck", "add", 12, 12, 18, 18, over: true))));

        await Assert.That(SketchRasterizer.AddsOverSubtracts(board).Single().Survives).IsTrue();
    }

    [Test]
    public async Task An_add_clear_of_every_subtract_says_nothing()
    {
        var board = Read(Board(Layer("ground",
            Rect("island", "add", 0, 0, 40, 40),
            Rect("hole", "subtract", 10, 10, 20, 20),
            Rect("shed", "add", 30, 30, 35, 35))));

        await Assert.That(SketchRasterizer.AddsOverSubtracts(board)).IsEmpty();
    }
}
