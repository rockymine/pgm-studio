using PgmStudio.Geom.Relief;

namespace PgmStudio.Geom.Tests.Relief;

/// <summary>
/// The scope rule: a relief is solved over the fused group rather than per shape, and a shape can leave that
/// fusion in one of two ways that differ in which of the two is holding the other still.
/// </summary>
public sealed class ReliefGroupTests
{
    private static double[][] Rect(double minX, double minZ, double maxX, double maxZ) =>
        [[minX, minZ], [maxX, minZ], [maxX, maxZ], [minX, maxZ]];

    private static readonly double[][] North = Rect(0, 0, 40, 24);
    private static readonly double[][] South = Rect(0, 24, 40, 50);
    private static readonly double[][] Compound = Rect(10, 28, 28, 44);

    // Deliberately no rim: a rim pins each piece's own boundary — the seam included — which would hide the
    // artefact this file exists to measure. The two marks sit one per piece, so a piece solved alone sees
    // only its own.
    private static ReliefSpec Spec() => new()
    {
        Base = 6,
        Marks = [new PointMark(8, 6, 20, 4), new PointMark(32, 44, 6, 4)],
    };

    private static int WorstStepAcross(HeightField field, int z)
    {
        var worst = 0;
        for (var x = field.Footprint.MinX; x < field.Footprint.MinX + field.Footprint.Width; x++)
            if (field.Has(x, z) && field.Has(x, z + 1))
                worst = Math.Max(worst, Math.Abs(field.At(x, z + 1) - field.At(x, z)));
        return worst;
    }

    [Test]
    public async Task Solving_per_shape_leaves_a_seam_that_the_fusion_does_not()
    {
        // A mark outside a shape says nothing to it, so the two sides of a piece boundary settle
        // independently — which is a step, and a large one.
        var perShapeSeam = 0;
        var pieces = new[] { North, South };
        var heights = new Dictionary<(int X, int Z), int>();
        foreach (var ring in pieces)
        {
            var piece = Footprint.Of([(ring, false)]);
            var solved = ReliefSolver.Solve(piece, Spec());
            foreach (var (x, z) in piece.Land()) heights[(x, z)] = solved.At(x, z);
        }
        for (var x = 0; x < 40; x++)
            if (heights.TryGetValue((x, 23), out var above) && heights.TryGetValue((x, 24), out var below))
                perShapeSeam = Math.Max(perShapeSeam, Math.Abs(below - above));

        var fused = new ReliefGroup([new ReliefShape(North), new ReliefShape(South)], Spec()).Build();
        var fusedSeam = WorstStepAcross(fused, 23);

        await Assert.That(fusedSeam).IsLessThanOrEqualTo(1);
        await Assert.That(perShapeSeam).IsGreaterThan(5);
    }

    [Test]
    public async Task Only_a_held_shapes_height_reaches_the_ground_around_it()
    {
        // The distinction, stated as the thing that is actually different. Both kinds change the surrounding
        // field, because both are boundaries — an excluded shape is a hole the field bends around, and a hole
        // has an outline. What separates them is whether the *height* travels: solved at two very different
        // heights, a held compound moves the land around it and an excluded one does not move it at all.
        ReliefGroup Group(Participation how, double height) => new(
            [new ReliefShape(North), new ReliefShape(South), new ReliefShape(Compound, how, height)], Spec());

        int OutsideDifferences(Participation how)
        {
            var low = Group(how, 6).Build();
            var high = Group(how, 20).Build();
            return low.Footprint.Land()
                .Where(cell => !Polygon.PointInRing(cell.X + 0.5, cell.Z + 0.5, Compound))
                .Count(cell => low.At(cell.X, cell.Z) != high.At(cell.X, cell.Z));
        }

        await Assert.That(OutsideDifferences(Participation.Hold)).IsGreaterThan(0);
        await Assert.That(OutsideDifferences(Participation.Exclude)).IsEqualTo(0);
    }

    [Test]
    public async Task A_held_shape_is_flat_at_the_height_it_holds()
    {
        // Flatness in a sloping field has to be paid for at the edge — a flat pad across a gradient must step
        // somewhere, whichever way it is bound. What Hold buys is not a smaller step but a surrounding
        // surface that was solved knowing the pad is there.
        var held = new ReliefGroup(
            [new ReliefShape(North), new ReliefShape(South), new ReliefShape(Compound, Participation.Hold, 14)],
            Spec()).Build();

        await Assert.That(held.Footprint.Land()
            .Where(cell => Polygon.PointInRing(cell.X + 0.5, cell.Z + 0.5, Compound))
            .All(cell => held.At(cell.X, cell.Z) == 14)).IsTrue();
    }

    [Test]
    public async Task An_excluded_shape_is_not_solved_over()
    {
        var group = new ReliefGroup(
            [new ReliefShape(North), new ReliefShape(South), new ReliefShape(Compound, Participation.Exclude, 12)],
            Spec());

        var solved = group.Solve();
        var covered = solved.Footprint.Land()
            .Count(cell => Polygon.PointInRing(cell.X + 0.5, cell.Z + 0.5, Compound));
        await Assert.That(covered).IsEqualTo(0);
    }

    // The worst step between a ring's own cells and the land immediately outside it.
    private static int EdgeStep(HeightField field, double[][] ring)
    {
        var worst = 0;
        foreach (var (x, z) in field.Footprint.Land())
        {
            if (!Polygon.PointInRing(x + 0.5, z + 0.5, ring)) continue;
            foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                if (!field.Has(x + dx, z + dz)) continue;
                if (Polygon.PointInRing(x + dx + 0.5, z + dz + 0.5, ring)) continue;
                worst = Math.Max(worst, Math.Abs(field.At(x + dx, z + dz) - field.At(x, z)));
            }
        }
        return worst;
    }
}
