using PgmStudio.Geom;

namespace PgmStudio.Geom.Tests;

/// <summary>
/// <see cref="Polygon.CentresInside"/> answers a whole box a row at a time, and every rasterization of a shape
/// reads it where it once asked <see cref="Polygon.PointInRing"/> cell by cell — so the two must agree on every
/// cell, including the ones a vertex or an edge lands exactly on.
/// </summary>
public sealed class PolygonTests
{
    public static IEnumerable<Func<double[][]>> Rings()
    {
        // Vertices on cell centres, on cell corners and on half-cells; horizontal and vertical edges; a
        // concave notch; a ring that crosses itself; a closing repeat; a degenerate sliver.
        yield return () => [[0, 0], [10, 0], [10, 10], [0, 10]];
        yield return () => [[0.5, 0.5], [9.5, 0.5], [9.5, 3.5], [4.5, 3.5], [4.5, 8.5], [0.5, 8.5], [0.5, 0.5]];
        yield return () => [[-3.25, 2], [6, -4.75], [12.5, 2.5], [6, 2.5], [4, 9.5], [-1, 6]];
        yield return () => [[0, 0], [10, 10], [10, 0], [0, 10]];
        yield return () => [[2, 2], [8, 2.0000001], [2, 2.0000002]];
        yield return () => [.. RingBend.Draw([[0, 0], [60, 0], [60, 40], [0, 40]], wander: 3, step: 7, seed: 11)!.Value.Ring];
        yield return () => Enumerable.Range(0, 97)
            .Select(i => new[] { 20 * Math.Cos(i * 2 * Math.PI / 97) * (1 + 0.3 * Math.Sin(5 * i)),
                                 20 * Math.Sin(i * 2 * Math.PI / 97) * (1 + 0.3 * Math.Sin(5 * i)) })
            .ToArray();
    }

    [Test]
    [MethodDataSource(nameof(Rings))]
    public async Task A_box_read_a_row_at_a_time_agrees_with_every_cell_asked_alone(double[][] ring)
    {
        int minX = (int)Math.Floor(ring.Min(p => p[0])) - 2, maxX = (int)Math.Ceiling(ring.Max(p => p[0])) + 2;
        int minZ = (int)Math.Floor(ring.Min(p => p[1])) - 2, maxZ = (int)Math.Ceiling(ring.Max(p => p[1])) + 2;

        var inside = Polygon.CentresInside(ring, minX, minZ, maxX, maxZ);

        var width = maxX - minX + 1;
        var disagree = new List<(int X, int Z)>();
        for (var z = minZ; z <= maxZ; z++)
            for (var x = minX; x <= maxX; x++)
                if (inside[(z - minZ) * width + (x - minX)] != Polygon.PointInRing(x + 0.5, z + 0.5, ring))
                    disagree.Add((x, z));

        await Assert.That(inside.Length).IsEqualTo(width * (maxZ - minZ + 1));
        await Assert.That(disagree).IsEmpty();
    }

    [Test]
    public async Task A_box_the_ring_does_not_reach_is_empty()
    {
        var inside = Polygon.CentresInside([[0, 0], [4, 0], [4, 4], [0, 4]], 10, 10, 14, 14);
        await Assert.That(inside.Any(cell => cell)).IsFalse();
    }
}
