namespace PgmStudio.Geom.Algorithms;

/// <summary>
/// Evenly spaced scatter with no two sites touching, from a hash field alone: a cell is a site when its hashed
/// value beats every other cell within a radius. No point list, no rejection loop, no state — asking whether
/// one cell is a site reads only its neighbourhood, so the answer is the same however the caller iterates and
/// whatever it iterates over.
///
/// <para>That is what separates this from "place a prop where a random roll clears a threshold", which is what
/// white noise gives: clumps and bare patches, because independent rolls have no idea what their neighbours
/// did. A local-maximum test has that idea built in — the winner of a neighbourhood suppresses the whole
/// neighbourhood — so boulders and trees land spaced rather than piled.</para>
/// </summary>
public static class BlueNoise
{
    /// <summary>Whether the cell is a scatter site: the strict local maximum of the hash field over the disc of
    /// <paramref name="radius"/> around it. Ties lose, so two cells with the same hashed value both yield and
    /// neither is a site — a miss rather than a pair of touching props.</summary>
    public static bool IsSite(int x, int z, uint seed, int radius)
    {
        if (radius < 1) return true;
        var self = PatternNoise.Unit(x, z, seed);
        for (var dz = -radius; dz <= radius; dz++)
        for (var dx = -radius; dx <= radius; dx++)
        {
            if (dx == 0 && dz == 0) continue;
            if (dx * dx + dz * dz > radius * radius) continue;
            if (PatternNoise.Unit(x + dx, z + dz, seed) >= self) return false;
        }
        return true;
    }

    /// <summary>The sites among <paramref name="cells"/>, in the order given. A caller that already has the
    /// footprint it may place on iterates that rather than a bounding box, so the scan cost follows the land.
    /// <para><paramref name="representativeOf"/> is what makes a scatter symmetric: it maps a cell to the cell
    /// whose site-ness decides for the whole orbit (<see cref="OrbitScatter.Canonical"/>). Every image of an
    /// orbit then answers the same question and they are placed or skipped together. Left null, each cell
    /// answers for itself — the free scatter, correct for anything that does not affect play.</para></summary>
    public static IEnumerable<(int X, int Z)> Sites(
        IEnumerable<(int X, int Z)> cells, uint seed, int radius, Func<int, int, (int X, int Z)>? representativeOf = null)
    {
        foreach (var (x, z) in cells)
        {
            var (rx, rz) = representativeOf is null ? (x, z) : representativeOf(x, z);
            if (IsSite(rx, rz, seed, radius)) yield return (x, z);
        }
    }
}
