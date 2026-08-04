namespace PgmStudio.Geom.Algorithms;

/// <summary>
/// Makes a per-cell decision the same for every cell in a symmetry orbit, by asking it of one member instead
/// of each. <see cref="Canonical"/> answers which member that is — a fixed choice among the orbit's images, so
/// every image resolves to it and therefore to the same answer.
///
/// <para>This is the whole of "fair by construction" for scattered dressing. A prop that affects play — a
/// boulder giving cover, a tree breaking a sightline — must exist for both teams or neither, and filtering a
/// free scatter afterwards cannot fix that: the images that survive are the ones that happened to agree.
/// Deciding on the representative removes the question. Cosmetic dressing does not go through here at all,
/// because two identical flower beds read as a rendering artefact and mirroring buys nothing.</para>
/// </summary>
public static class OrbitScatter
{
    /// <summary>The cell whose answer stands for the whole orbit of <paramref name="x"/>/<paramref name="z"/>
    /// under a symmetry <paramref name="mode"/> about a centre: the image lowest in a fixed total order.
    /// Which image is picked does not matter, only that every image picks the same one.</summary>
    public static (int X, int Z) Canonical(int x, int z, string? mode, double centerX, double centerZ)
    {
        var best = (X: x, Z: z);
        var order = Symmetry.Order(mode);
        for (var k = 1; k < order; k++)
        {
            // The orbit acts on positions, and a cell's position is its middle — mirroring a cell's corner
            // lands on the boundary between two cells and rounds to whichever side floating point prefers.
            var (px, pz) = Symmetry.Point(x + 0.5, z + 0.5, mode, centerX, centerZ, k);
            var image = (X: (int)Math.Floor(px), Z: (int)Math.Floor(pz));
            if (Precedes(image, best)) best = image;
        }
        return best;
    }

    /// <summary>A canonicaliser bound to one map's symmetry — what <see cref="BlueNoise.Sites"/> takes.</summary>
    public static Func<int, int, (int X, int Z)> CanonicalizerFor(string? mode, double centerX, double centerZ)
        => Symmetry.Order(mode) <= 1
            ? (x, z) => (x, z)                                   // no symmetry: a cell is its own orbit
            : (x, z) => Canonical(x, z, mode, centerX, centerZ);

    // Any total order will do; z-then-x is the one the grid is already walked in.
    private static bool Precedes((int X, int Z) a, (int X, int Z) b)
        => a.Z != b.Z ? a.Z < b.Z : a.X < b.X;
}
