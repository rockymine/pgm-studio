using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Palette;
namespace PgmStudio.Minecraft.Stamping;

/// <summary>
/// Stamps the build region's outline into the world as an unpowered redstone line at y=1 (docs/generator/
/// rules.md ST5) — a marker read from below or in an editor, telling where players may build without putting
/// a block anywhere near the play surface.
/// <para>The line keeps one air block clear of the build region, so it sits two blocks out from its edge, and
/// it holds that same clearance from terrain — which is what pulls a line back where terrain overhangs the
/// region. Only the edges that face the void carry a line: an edge docked against terrain is already marked
/// by the terrain. Where two void-facing edges meet at a convex corner the two lines turn into each other, so
/// a region hanging free in the void is ringed rather than bracketed.</para>
/// <para>Coordinates are absolute world blocks — the caller has already fanned the areas across the symmetry
/// orbit, which is what makes the marker symmetric with them.</para>
/// </summary>
public static class BuildMarkerStamper
{
    /// <summary>Air blocks kept between the marker and both the build region and the terrain.</summary>
    public const int Clearance = 1;

    /// <summary>How far out from a build edge the marker sits — the clearance plus the marker's own block.</summary>
    public const int Offset = Clearance + 1;

    /// <summary>The marker's Y: one above the world floor, far below every play surface.</summary>
    public const int MarkerY = 1;

    // West, east, north, south — the four sides of a build cell.
    private static readonly (int X, int Z)[] Sides = [(-1, 0), (1, 0), (0, -1), (0, 1)];

    // The perpendicular side pairs, as indices into Sides: the four corners a build cell can turn.
    private static readonly (int First, int Second)[] Corners = [(0, 2), (0, 3), (1, 2), (1, 3)];

    /// <summary>Lay the marker for <paramref name="areas"/> (minus <paramref name="holes"/>) into the world.
    /// Rects are min-inclusive, max-exclusive world blocks; <paramref name="surfaceTop"/> keys are the terrain
    /// footprint.</summary>
    public static void Stamp(
        VoxelWorld world,
        IEnumerable<(int MinX, int MinZ, int MaxX, int MaxZ)> areas,
        IEnumerable<(int MinX, int MinZ, int MaxX, int MaxZ)> holes,
        IReadOnlyDictionary<(int X, int Z), int> surfaceTop)
    {
        foreach (var (x, z) in MarkerCells(areas, holes, surfaceTop))
            world.SetBlock(x, MarkerY, z, Blocks.RedstoneWire, 0);   // data 0 — an unpowered marker, not a signal
    }

    /// <summary>The columns the marker occupies, in a stable order (by Z, then X). Pure, so the outline can be
    /// checked without building a world.</summary>
    public static IReadOnlyList<(int X, int Z)> MarkerCells(
        IEnumerable<(int MinX, int MinZ, int MaxX, int MaxZ)> areas,
        IEnumerable<(int MinX, int MinZ, int MaxX, int MaxZ)> holes,
        IReadOnlyDictionary<(int X, int Z), int> surfaceTop)
    {
        var build = Fill(areas);
        foreach (var hole in Fill(holes)) build.Remove(hole);
        if (build.Count == 0) return [];

        var marker = new HashSet<(int X, int Z)>();
        var facesVoid = new bool[Sides.Length];
        foreach (var cell in build)
        {
            for (var side = 0; side < Sides.Length; side++)
                facesVoid[side] = FacesVoid(cell, Sides[side], build, surfaceTop);

            for (var side = 0; side < Sides.Length; side++)
                if (facesVoid[side]) marker.Add(Step(cell, Sides[side], Offset));

            foreach (var (first, second) in Corners)
                if (facesVoid[first] && facesVoid[second]) AddCornerTurn(marker, cell, Sides[first], Sides[second]);
        }

        // The clearance is a property of every marker block, not just of the edge that generated it: a line
        // drawn for one edge can still run up against terrain, or against another part of the region.
        marker.RemoveWhere(cell => Crowds(cell, build.Contains) || Crowds(cell, surfaceTop.ContainsKey));
        return [.. marker.OrderBy(cell => cell.Z).ThenBy(cell => cell.X)];
    }

    /// <summary>A build cell's side faces the void when the block across it is neither more build region (an
    /// interior face) nor terrain (a docked edge, which the terrain itself already marks).</summary>
    private static bool FacesVoid(
        (int X, int Z) cell, (int X, int Z) side,
        HashSet<(int X, int Z)> build, IReadOnlyDictionary<(int X, int Z), int> surfaceTop)
    {
        var across = Step(cell, side, 1);
        return !build.Contains(across) && !surfaceTop.ContainsKey(across);
    }

    /// <summary>Turn one line into the other around a convex corner: the quarter-arc out to the diagonal cell
    /// at (<see cref="Offset"/>, <see cref="Offset"/>), which is where the two offset lines would meet.</summary>
    private static void AddCornerTurn(
        HashSet<(int X, int Z)> marker, (int X, int Z) cell, (int X, int Z) first, (int X, int Z) second)
    {
        for (var step = 1; step <= Offset; step++)
        {
            marker.Add(Step(Step(cell, first, step), second, Offset));
            marker.Add(Step(Step(cell, first, Offset), second, step));
        }
    }

    /// <summary>Whether <paramref name="cell"/> sits inside <paramref name="occupied"/> or within
    /// <see cref="Clearance"/> blocks of it — the test that leaves an air block between the two.</summary>
    private static bool Crowds((int X, int Z) cell, Func<(int X, int Z), bool> occupied)
    {
        for (var dx = -Clearance; dx <= Clearance; dx++)
        for (var dz = -Clearance; dz <= Clearance; dz++)
            if (occupied((cell.X + dx, cell.Z + dz))) return true;
        return false;
    }

    private static (int X, int Z) Step((int X, int Z) cell, (int X, int Z) side, int distance)
        => (cell.X + side.X * distance, cell.Z + side.Z * distance);

    private static HashSet<(int X, int Z)> Fill(IEnumerable<(int MinX, int MinZ, int MaxX, int MaxZ)> rects)
    {
        var cells = new HashSet<(int X, int Z)>();
        foreach (var (minX, minZ, maxX, maxZ) in rects)
            for (var x = minX; x < maxX; x++)
            for (var z = minZ; z < maxZ; z++)
                cells.Add((x, z));
        return cells;
    }
}
