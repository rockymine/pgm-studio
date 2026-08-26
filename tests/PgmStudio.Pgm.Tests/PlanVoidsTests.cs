using PgmStudio.Geom;
using PgmStudio.Pgm.Plan;

namespace PgmStudio.Pgm.Tests;

/// <summary>
/// The compile step that declares a plan's negative space: every enclosed void becomes a buffer piece, ground
/// another piece lays into is not a void, and the step is idempotent — an author need not draw the buffers,
/// and one deleted from a plan comes back on the next declare.
/// </summary>
public sealed class PlanVoidsTests
{
    private static PlanModel Plan(string json) => PlanModel.Parse(json)!;

    // Four pieces in a square frame around a one-cell hole at cell (1,1).
    private const string Ring = """
        "pieces":[ {"id":"n","role":"piece","rect":[0,0,3,1]}, {"id":"s","role":"piece","rect":[0,2,3,1]},
                   {"id":"w","role":"piece","rect":[0,1,1,1]}, {"id":"e","role":"piece","rect":[2,1,1,1]}
        """;

    private static PlanModel RingPlan(string extraPieces = "") => Plan($$"""
        { "plan":1, "globals":{"symmetry":"rot_180","cell":5,"surface":9}, {{Ring}}{{extraPieces}} ] }
        """);

    private static List<PlanPiece> Buffers(PlanModel plan) =>
        [.. plan.Pieces.Where(p => p.Role == PlanRoles.Buffer)];

    [Test]
    public async Task The_void_a_ring_of_pieces_encircles_becomes_a_buffer()
    {
        var declared = PlanVoids.Declare(RingPlan());
        var buffer = Buffers(declared).Single();
        await Assert.That(buffer.Rect).IsEqualTo(new CellRect(1, 1, 1, 1));
        // the terrain pieces are untouched — declaring adds, it never edits
        await Assert.That(declared.Pieces.Count).IsEqualTo(5);
    }

    [Test]
    public async Task A_ring_whose_pieces_stand_at_different_heights_encircles_the_same_hole()
    {
        // The hole a donut leaves is a hole whatever height its arms are at: it is players who walk round it,
        // and a stair round a yard is still a yard. Stating a surface per piece is how a composed board is
        // made paintable, and it must not be how its negative space stops being stated.
        var stepped = Plan(
            """
            { "plan":1, "globals":{"symmetry":"rot_180","cell":5,"surface":9},
              "pieces":[ {"id":"n","role":"piece","rect":[0,0,3,1],"surface":9},
                         {"id":"s","role":"piece","rect":[0,2,3,1],"surface":12},
                         {"id":"w","role":"piece","rect":[0,1,1,1],"surface":10},
                         {"id":"e","role":"piece","rect":[2,1,1,1],"surface":11} ] }
            """);

        var buffer = Buffers(PlanVoids.Declare(stepped)).Single();
        await Assert.That(buffer.Rect).IsEqualTo(new CellRect(1, 1, 1, 1));
    }

    [Test]
    public async Task A_plan_that_encloses_nothing_is_returned_unchanged()
    {
        var plan = Plan("""
        { "plan":1, "globals":{"symmetry":"rot_180","cell":5,"surface":9},
          "pieces":[ {"id":"a","role":"piece","rect":[0,0,2,4]}, {"id":"b","role":"piece","rect":[0,4,2,4]} ] }
        """);
        // the same instance back, so a caller can persist unconditionally and write only on a real change
        await Assert.That(PlanVoids.Declare(plan)).IsSameReferenceAs(plan);
    }

    [Test]
    public async Task Declaring_twice_changes_nothing()
    {
        var once = PlanVoids.Declare(RingPlan());
        await Assert.That(PlanVoids.Declare(once).ToJson()).IsEqualTo(once.ToJson());
        await Assert.That(PlanVoids.Declare(once)).IsSameReferenceAs(once);
    }

    [Test]
    public async Task A_buffer_the_author_deletes_comes_back()
    {
        var declared = PlanVoids.Declare(RingPlan());
        var stripped = PlanModel.Parse(declared.ToJson())!;
        stripped.Pieces.RemoveAll(p => p.Role == PlanRoles.Buffer);
        await Assert.That(PlanVoids.Declare(stripped).ToJson()).IsEqualTo(declared.ToJson());
    }

    [Test]
    public async Task A_void_the_author_already_declared_is_not_declared_twice()
    {
        var authored = RingPlan(""", {"id":"mine","role":"buffer","rect":[1,1,1,1]}""");
        await Assert.That(PlanVoids.Declare(authored)).IsSameReferenceAs(authored);
        await Assert.That(Buffers(authored).Single().Id).IsEqualTo("mine");
    }

    [Test]
    public async Task A_void_a_plateau_lays_ground_into_is_not_a_void()
    {
        // a raised piece seated in the hole: the surface-9 union still encircles it, but it is a plateau
        var stepped = RingPlan(""", {"id":"mid","role":"piece","rect":[1,1,1,1],"surface":13}""");
        await Assert.That(PlanVoids.Declare(stepped)).IsSameReferenceAs(stepped);
    }

    [Test]
    public async Task A_buffer_takes_the_mirror_flag_of_the_body_that_encloses_it()
    {
        // a non-mirroring ring: its void must not fan either, or the orbit copy would carve live terrain
        var neutral = Plan("""
        { "plan":1, "globals":{"symmetry":"rot_180","cell":5,"surface":9},
          "pieces":[ {"id":"n","role":"piece","rect":[0,0,3,1],"mirrors":false},
                     {"id":"s","role":"piece","rect":[0,2,3,1],"mirrors":false},
                     {"id":"w","role":"piece","rect":[0,1,1,1],"mirrors":false},
                     {"id":"e","role":"piece","rect":[2,1,1,1],"mirrors":false} ] }
        """);
        await Assert.That(Buffers(PlanVoids.Declare(neutral)).Single().MirrorsOrDefault).IsFalse();
        await Assert.That(Buffers(PlanVoids.Declare(RingPlan())).Single().MirrorsOrDefault).IsTrue();
    }
}
