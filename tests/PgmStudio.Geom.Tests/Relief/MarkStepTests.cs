using PgmStudio.Geom.Relief;

namespace PgmStudio.Geom.Tests.Relief;

/// <summary>
/// The block quantum a mark's own ground finishes at. A step is the width of the smallest landform the ground
/// can have, and that is a fact about a landform rather than about a landmass — worked terraces and a walkable
/// ramp want different numbers and are drawn on one island. A cell takes the step of the last mark to claim it,
/// which is the territory the seam reading is already taken over; ground no mark claimed takes the group's.
/// </summary>
public sealed class MarkStepTests
{
    private const int Span = 60;

    private static Footprint Board() =>
        Footprint.Of([([[0, 0], [Span, 0], [Span, Span], [0, Span]], false)]);

    private static double[][] Rect(double minX, double minZ, double maxX, double maxZ) =>
        [[minX, minZ], [maxX, minZ], [maxX, maxZ], [minX, maxZ]];

    private static ReliefSpec Spec(int groupStep, params Mark[] marks) => new()
    {
        Base = 0, Reach = 0, Step = groupStep, Marks = marks,
    };

    [Test]
    public async Task Two_marks_on_one_landmass_finish_at_their_own_quanta()
    {
        // A bench and a road drawn on one island. Both state the same height; only the quantum differs, so
        // what separates them in the built surface is entirely the step each states.
        var terrace = new AreaMark(Rect(4, 4, 26, 56), [10]) { Id = "terrace", Step = 3 };
        var road = new AreaMark(Rect(34, 4, 56, 56), [10]) { Id = "road" };

        var field = ReliefSolver.Solve(Board(), Spec(1, terrace, road));

        await Assert.That(field.At(10, 30)).IsEqualTo(9);    // 10 snapped to the terrace's own three
        await Assert.That(field.At(45, 30)).IsEqualTo(10);   // the road follows the field cell by cell
    }

    [Test]
    public async Task Ground_no_mark_claimed_finishes_at_the_groups_step()
    {
        // A mark states the quantum of the ground it claims and says nothing about the rest of the island,
        // which is what makes the group's step a fallback rather than a ceiling.
        var summit = new PointMark(10, 10, 20, 4) { Id = "summit", Step = 1 };

        var field = ReliefSolver.Solve(Board(), Spec(4, summit));

        await Assert.That(field.At(10, 10)).IsEqualTo(20);           // the summit's own, exactly
        await Assert.That(field.At(50, 50) % 4).IsEqualTo(0);        // the group's, away from it
    }

    [Test]
    public async Task A_marks_quantum_folds_with_the_surface_it_finishes()
    {
        // A mark authored on one half states the ground on both. Folding the surface and not the quantum
        // rounds one continuous height two ways, which is the whole-block disagreement between the halves
        // that the fold exists to prevent — and nothing else about the board would look wrong.
        var bench = new AreaMark(Rect(10, 4, 50, 24), [13]) { Id = "bench", Step = 3 };
        var spec = Spec(1, bench) with { FoldMode = "rot_180", FoldCentreX = 30, FoldCentreZ = 30 };

        var field = ReliefSolver.Solve(Board(), spec);

        foreach (var (x, z) in field.Footprint.Land())
        {
            int imageX = Span - 1 - x, imageZ = Span - 1 - z;
            if (!field.Has(imageX, imageZ)) continue;
            await Assert.That(field.At(x, z)).IsEqualTo(field.At(imageX, imageZ));
        }

        // And the quantum it carried is the one the ground came out on, on the half it was drawn on.
        await Assert.That(field.At(30, 14) % 3).IsEqualTo(0);
    }
}
