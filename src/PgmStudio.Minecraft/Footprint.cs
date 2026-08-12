using PgmStudio.Geom.Algorithms;

namespace PgmStudio.Minecraft;

/// <summary>One rectangle of a building's plan. A house of a single rectangle is one wing; an L, a T or a U is
/// several touching ones, which is what lets a building turn a corner as one house under one style rather than
/// as two houses standing beside each other.</summary>
public readonly record struct Wing(int MinX, int MinZ, int MaxX, int MaxZ)
{
    public int Width => MaxX - MinX + 1;
    public int Depth => MaxZ - MinZ + 1;

    public bool Holds(int x, int z) => x >= MinX && x <= MaxX && z >= MinZ && z <= MaxZ;
}

/// <summary>
/// The plan a house stands on, and the ring the stamper paints against: which cells it holds, which of them are
/// on its outline, how far in from that outline a cell stands, and how far round it — the arc, bend and
/// direction a wall-run material reads. A house has two of these once it has a porch (what the walls keep and
/// what the deck took), which is why the ring a cell is painted against is passed rather than assumed.
///
/// <para><b>A footprint is one or more touching rectangles</b>, so every question it answers is asked of the
/// cells rather than of a min and a max. A rectangle can answer most of them in closed form and a shape that
/// turns a corner cannot, and keeping both would be two implementations of one idea — so there is one, and a
/// single-rectangle building is the case where it happens to agree with arithmetic.</para>
///
/// <para><b>The outline is walked</b> (<see cref="GridBoundary"/>), at the window the terrain painter measures
/// its own edges over, so a wall and the plateau beside it cannot answer a wall-run material differently.</para>
///
/// <para>A corner comes in two kinds and they are not interchangeable. An <see cref="OnCorner">outer</see> one
/// is where the building turns away from itself — the four of a rectangle, and where a post stands. An
/// <see cref="OnInnerCorner">inner</see> one is where two wings meet and the outline turns back into the
/// building; nothing stands in it, and what it is for is keeping an opening off the turn, the same margin an
/// outer corner keeps.</para>
/// </summary>
public sealed class Footprint
{
    private readonly Wing[] wings;

    /// <summary>A building of one rectangle, whose walls occupy the cells between the two corners inclusive.</summary>
    public Footprint(int minX, int minZ, int maxX, int maxZ)
        : this([new Wing(minX, minZ, maxX, maxZ)]) { }

    /// <summary>A building of one or more wings. They are expected to touch — the outline is walked as one
    /// landmass, so a wing standing clear of the rest is not part of the ring the walls are painted against.</summary>
    public Footprint(IReadOnlyList<Wing> wings)
    {
        ArgumentOutOfRangeException.ThrowIfZero(wings.Count);
        this.wings = [.. wings];
        MinX = wings.Min(wing => wing.MinX);
        MinZ = wings.Min(wing => wing.MinZ);
        MaxX = wings.Max(wing => wing.MaxX);
        MaxZ = wings.Max(wing => wing.MaxZ);
    }

    /// <summary>The rectangles the plan is made of, in the order they were given.</summary>
    public IReadOnlyList<Wing> Wings => wings;

    /// <summary>The box the whole plan fits in. On a single-wing building it is the building; on one that turns
    /// a corner it is the box drawn round it, so a caller walking it has to ask <see cref="Holds"/>.</summary>
    public int MinX { get; }

    /// <inheritdoc cref="MinX"/>
    public int MinZ { get; }

    /// <inheritdoc cref="MinX"/>
    public int MaxX { get; }

    /// <inheritdoc cref="MinX"/>
    public int MaxZ { get; }

    public int Width => MaxX - MinX + 1;
    public int Depth => MaxZ - MinZ + 1;

    /// <summary>Whether the plan covers this cell — true where any wing does.</summary>
    public bool Holds(int x, int z)
    {
        foreach (var wing in wings)
            if (wing.Holds(x, z)) return true;
        return false;
    }

    /// <summary>Whether the cell is on the outline: held, with open ground orthogonally beside it. This is where
    /// a wall stands, so a wing's edge buried inside another wing is not on it.</summary>
    public bool OnPerimeter(int x, int z)
        => Holds(x, z)
           && (!Holds(x - 1, z) || !Holds(x + 1, z) || !Holds(x, z - 1) || !Holds(x, z + 1));

    /// <summary>Whether the building turns <b>away</b> from itself here — the four corners of a rectangle, and
    /// where a corner post stands. It is the cell that has no held neighbour opposite it on either axis, which
    /// is what makes it a corner rather than a stretch of wall: a wall carries on through a cell on one axis
    /// and a corner carries on through none.</summary>
    public bool OnCorner(int x, int z)
        => Holds(x, z)
           && !(Holds(x - 1, z) && Holds(x + 1, z))
           && !(Holds(x, z - 1) && Holds(x, z + 1));

    /// <summary>Whether the building turns <b>back into</b> itself here — where two wings meet and one wall runs
    /// into another. The cell carries on straight along its own wall, so it is no outer corner, but the plan
    /// reaches round the open side of it: a diagonal neighbour is held that the wall cannot be walked to
    /// orthogonally. On a raster that turn is a <b>pair</b> of cells meeting corner to corner, one on each wall,
    /// and both answer here. An opening keeps its margin off one exactly as it does off an outer corner, which
    /// is what the kind is for — nothing stands on it.</summary>
    public bool OnInnerCorner(int x, int z)
    {
        if (!OnPerimeter(x, z)) return false;
        foreach (var (dx, dz) in Diagonals)
            if (Holds(x + dx, z + dz) && (!Holds(x + dx, z) || !Holds(x, z + dz))) return true;
        return false;
    }

    private static readonly (int X, int Z)[] Diagonals = [(-1, -1), (1, -1), (-1, 1), (1, 1)];

    /// <summary>How many blocks in from the nearest wall a cell stands — 0 on the outline itself, −1 off the
    /// plan. What the floor's zones are cut by, and it is a step count rather than a subtraction because on a
    /// plan that turns a corner the nearest wall is not the nearest edge of a box.</summary>
    public int Ring(int x, int z) => Inset.GetValueOrDefault((x, z), -1);

    /// <summary>How far round the outline a cell sits, clockwise from the cell nearest −x/−z, or −1 off the
    /// ring. A wall is a closed loop, so a wall-run pattern reads this exactly as it reads a plateau's outer
    /// edge — one material stripes both.</summary>
    public int Arc(int x, int z) => Perimeter.Arc.GetValueOrDefault((x, z), -1);

    /// <summary>How sharply the ring bends where a cell sits, in degrees, to the same scale a painted wall
    /// reads. A right angle measures ninety on the corner itself and ramps to nothing a window away, so a
    /// material thresholding it frames a band either side of each corner.</summary>
    public int Turn(int arc) => arc >= 0 && arc < Perimeter.Turn.Length ? Perimeter.Turn[arc] : 0;

    /// <summary>Which axis the wall runs along at this cell — what a block with a direction of its own is laid
    /// along, so a log on its side shows bark to the outside rather than its sawn end. The chord across the ring
    /// answers it everywhere but a corner, where a wall has an exposed face on both axes and no horizontal
    /// direction can serve them both; that one the plan names outright, because a walk sees a corner as a
    /// staircase of short runs rather than as a cell.</summary>
    public int Run(int x, int z)
    {
        if (OnCorner(x, z)) return GridBoundary.RunsBothWays;
        var arc = Arc(x, z);
        return arc < 0 ? 0 : Perimeter.Run[arc];
    }

    /// <summary>Every cell the plan holds, in ascending x then z.</summary>
    public IEnumerable<(int X, int Z)> Cells()
    {
        for (var x = MinX; x <= MaxX; x++)
            for (var z = MinZ; z <= MaxZ; z++)
                if (Holds(x, z)) yield return (x, z);
    }

    private PerimeterTrace? perimeter;
    private Dictionary<(int X, int Z), int>? inset;

    /// <summary>The walked outline, measured once on first use. A wall reads its arc, bend and direction from
    /// the same walk of the same outline that a plateau's edge reads them from — one measurement, so a building
    /// and the terrain beside it cannot answer a wall-run material differently. Walking it is also what holds on
    /// a short wall: a side under twice the window carries two corners inside one window, and the bend there is
    /// the pair's together rather than the nearer one's alone.</summary>
    private PerimeterTrace Perimeter => perimeter ??= Trace();

    /// <summary>Steps in from the outline, measured once on first use, breadth-first from every wall cell at
    /// once so a cell in the crook of two wings counts to the wall that is actually nearest it.</summary>
    private Dictionary<(int X, int Z), int> Inset => inset ??= Step();

    private PerimeterTrace Trace()
    {
        var arc = GridBoundary.TracePerimeter(Cells());
        var loop = GridBoundary.Loop(arc);
        var turn = new int[loop.Length];
        var run = new int[loop.Length];
        for (var index = 0; index < loop.Length; index++)
        {
            turn[index] = (int)Math.Round(GridBoundary.TurnAt(loop, index, GridBoundary.CornerWindow));
            run[index] = GridBoundary.RunAt(loop, index, GridBoundary.CornerWindow);
        }
        return new PerimeterTrace(arc, turn, run);
    }

    private Dictionary<(int X, int Z), int> Step()
    {
        var steps = new Dictionary<(int X, int Z), int>();
        var queue = new Queue<(int X, int Z)>();
        foreach (var (x, z) in Cells())
            if (OnPerimeter(x, z)) { steps[(x, z)] = 0; queue.Enqueue((x, z)); }

        while (queue.Count > 0)
        {
            var (x, z) = queue.Dequeue();
            var next = steps[(x, z)] + 1;
            foreach (var (nx, nz) in new[] { (x - 1, z), (x + 1, z), (x, z - 1), (x, z + 1) })
                if (Holds(nx, nz) && !steps.ContainsKey((nx, nz)))
                {
                    steps[(nx, nz)] = next;
                    queue.Enqueue((nx, nz));
                }
        }
        return steps;
    }

    /// <summary>One walk of an outline: which arc index each boundary cell holds, and the bend and direction
    /// measured at each of those indices.</summary>
    private sealed record PerimeterTrace(Dictionary<(int X, int Z), int> Arc, int[] Turn, int[] Run);
}
