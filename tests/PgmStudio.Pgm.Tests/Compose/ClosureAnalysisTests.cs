using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Plan;

namespace PgmStudio.Pgm.Tests.Compose;

/// <summary>
/// Closure-hole measurement over the fanned closure. The load-bearing invariant: a buffer marks EMPTY space,
/// so it must never rasterize as solid — placing one inside a rotation hole leaves the hole measured, whereas
/// a real terrain piece in the same spot would fill it in.
/// </summary>
public sealed class ClosureAnalysisTests
{
    private static PlanModel Plan(string json) => PlanModel.Parse(json)!;

    // A square annulus authored half-and-half under rot_180: a top bar + a left bar fan to a bottom + right
    // bar, enclosing a 2×2-cell void at the centre.
    private const string Ring = """
        {"id":"top","role":"piece","rect":[-3,-3,6,2]}, {"id":"left","role":"piece","rect":[-3,-1,2,2]}
        """;

    [Test]
    public async Task A_rot_180_ring_encloses_a_central_hole()
    {
        var sizes = ClosureAnalysis.HoleSizes(Plan($$"""{ "plan":1, "globals":{"cell":1,"symmetry":"rot_180"}, "pieces":[ {{Ring}} ] }"""));
        await Assert.That(sizes).IsEquivalentTo(new[] { 4 });
    }

    [Test]
    public async Task A_buffer_inside_the_hole_does_not_reduce_the_hole()
    {
        // the buffer sits exactly in the centre void; because it is an annotation it never rasterizes, so the
        // hole stays measured at its full size.
        var withBuffer = ClosureAnalysis.HoleSizes(Plan($$"""
            { "plan":1, "globals":{"cell":1,"symmetry":"rot_180"},
              "pieces":[ {{Ring}}, {"id":"buffer-hole","role":"buffer","rect":[-1,-1,2,2]} ] }
            """));
        await Assert.That(withBuffer).IsEquivalentTo(new[] { 4 });
    }

    [Test]
    public async Task A_solid_piece_in_the_same_spot_would_fill_the_hole()
    {
        // the control: an identical rect as a generating piece plugs the void → the hole vanishes. This is the
        // failure a buffer must avoid (erasing the rotation hole it documents).
        var plugged = ClosureAnalysis.HoleSizes(Plan($$"""
            { "plan":1, "globals":{"cell":1,"symmetry":"rot_180"},
              "pieces":[ {{Ring}}, {"id":"plug","role":"piece","rect":[-1,-1,2,2]} ] }
            """));
        await Assert.That(plugged).IsEmpty();
    }
}
