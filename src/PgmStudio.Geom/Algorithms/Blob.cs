namespace PgmStudio.Geom.Algorithms;

/// <summary>
/// One lobe of a blob: an axis-aligned ellipsoid centred on <paramref name="Center"/>, with a noise
/// <paramref name="Erosion"/> that pushes its surface in and out. Erosion 0 is a clean ellipsoid — the read of
/// a dropped prop; raise it and the surface breaks up into the angular, weathered outline a rock actually has.
/// </summary>
public readonly record struct BlobLobe(Vec3 Center, Vec3 Radii, double Erosion);

/// <summary>
/// The soft volume behind a boulder, an outcrop and a leaf cluster alike: one or more eroded ellipsoids,
/// tested point by point. A lobe stays a <em>test</em> rather than a list of cells because every caller wants
/// a different grid — a rock fills world blocks, a leaf cluster resolves ownership against its neighbours
/// first, a preview rasterises a section — and a shared cell list would serve none of them without being
/// rebuilt.
///
/// <para>Stacking lobes is what gives shape families for free: one wide flat lobe is an outcrop, three
/// shrinking lobes up an axis are a cairn, one round lobe is a boulder. There is no separate code path per
/// family — a family is a list of lobes.</para>
/// </summary>
public static class Blob
{
    /// <summary>Whether a point is inside a lobe. The erosion term is smooth noise sampled in the lobe's own
    /// frame, so the same lobe erodes identically wherever it is placed, and a stack of lobes does not develop
    /// a seam where two of them meet.</summary>
    public static bool Contains(in BlobLobe lobe, Vec3 point, uint seed)
    {
        var d = point - lobe.Center;
        var quadric = Sq(d.X / Math.Max(0.01, lobe.Radii.X))
                    + Sq(d.Y / Math.Max(0.01, lobe.Radii.Y))
                    + Sq(d.Z / Math.Max(0.01, lobe.Radii.Z));
        if (lobe.Erosion > 0)
        {
            // Sampled at three times the block rate so the bite is per-block-ish rather than a smooth bulge.
            var noise = PatternNoise.Value((int)Math.Round(d.X * 3), (int)Math.Round(d.Y * 3), (int)Math.Round(d.Z * 3), seed, 3);
            quadric += (noise - 0.5) * lobe.Erosion;
        }
        return quadric <= 1;
    }

    /// <summary>Whether a point is inside any lobe of a blob — the union that makes a cairn one prop.</summary>
    public static bool Contains(IReadOnlyList<BlobLobe> lobes, Vec3 point, uint seed)
    {
        for (var i = 0; i < lobes.Count; i++)
            if (Contains(lobes[i], point, seed)) return true;
        return false;
    }

    /// <summary>The integer block range a blob can possibly touch, so a caller loops that instead of a guess.
    /// Erosion can only push the surface out by half its amount, which the bound includes.</summary>
    public static (Vec3 Min, Vec3 Max) Bounds(IReadOnlyList<BlobLobe> lobes)
    {
        if (lobes.Count == 0) return (default, default);
        Vec3 min = new(double.MaxValue, double.MaxValue, double.MaxValue);
        Vec3 max = new(double.MinValue, double.MinValue, double.MinValue);
        foreach (var lobe in lobes)
        {
            // A quadric of 1 − erosion/2 is the furthest the eroded surface reaches, and it scales each radius.
            var grow = 1 + lobe.Erosion / 2;
            var reach = new Vec3(lobe.Radii.X * grow, lobe.Radii.Y * grow, lobe.Radii.Z * grow);
            min = new Vec3(Math.Min(min.X, lobe.Center.X - reach.X), Math.Min(min.Y, lobe.Center.Y - reach.Y), Math.Min(min.Z, lobe.Center.Z - reach.Z));
            max = new Vec3(Math.Max(max.X, lobe.Center.X + reach.X), Math.Max(max.Y, lobe.Center.Y + reach.Y), Math.Max(max.Z, lobe.Center.Z + reach.Z));
        }
        return (min, max);
    }

    private static double Sq(double v) => v * v;
}
