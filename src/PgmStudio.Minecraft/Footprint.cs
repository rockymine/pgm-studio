using PgmStudio.Domain;
using PgmStudio.Geom.Algorithms;

namespace PgmStudio.Minecraft;

/// <summary>
/// One straight run of a building's wall: which way its outward face points, which line it stands on, and how
/// far it runs. <see cref="Lo"/> and <see cref="Hi"/> are inclusive and count in the wall's own along axis — x
/// for a wall facing ±z, z for one facing ±x — and both ends are the corner cells, whichever kind of corner
/// ends the run.
///
/// <para><b>A facing is not an identity.</b> A rectangle has one run per facing, so naming the wall and naming
/// the direction it looks were the same thing; a plan that turns a corner has two walls looking the same way at
/// different lines, and a building addressed by facing alone cannot say which. A run is the thing a window is
/// seated in and a doorway is cut through, so it is the run that gets carried rather than the direction.</para>
/// </summary>
public readonly record struct WallSegment(RoomEdge Facing, int Fixed, int Lo, int Hi)
{
    /// <summary>Whether the wall runs east–west, so its along axis is x and the line it stands on is a z.</summary>
    public bool AlongX => Facing is RoomEdge.NegZ or RoomEdge.PosZ;

    /// <summary>How many blocks of wall the run holds, corner cells included.</summary>
    public int Length => Hi - Lo + 1;

    /// <summary>The block one step along the run stands on.</summary>
    public (int X, int Z) Cell(int along) => AlongX ? (along, Fixed) : (Fixed, along);

    /// <summary>Whether a step along the axis falls within the run.</summary>
    public bool Holds(int along) => along >= Lo && along <= Hi;

    /// <summary>The step from a cell of this wall to the cell behind it — into the building, away from the
    /// weather. What something standing against the wall rather than in it is offset by.</summary>
    public (int X, int Z) Inward => Facing switch
    {
        RoomEdge.NegZ => (0, 1),
        RoomEdge.PosZ => (0, -1),
        RoomEdge.NegX => (1, 0),
        _ => (-1, 0),
    };

    /// <summary>The stretch of wall between the two corners, which is what is left once the cells the corners
    /// themselves take are off the table.</summary>
    public (int Lo, int Hi) BetweenCorners => (Lo + 1, Hi - 1);

    /// <summary>Where an opening may actually be cut: the stretch between the corners, <b>one block further in
    /// at each end</b>. Clearing the corner cell is not enough — an opening starting in the very next cell still
    /// meets the corner, and a corner is where two walls turn and wants a block of wall beside it before
    /// anything is taken out. It is the same margin whether the building turns away from itself there or back
    /// into itself, because both are a turn.</summary>
    public (int Lo, int Hi) Seat => (Lo + 2, Hi - 2);
}

/// <summary>An opening taken out of one run of wall: which run, where along it, and how wide. A door carries its
/// run rather than a facing for the reason <see cref="WallSegment"/> gives — two walls of one building may look
/// the same way.</summary>
public readonly record struct WallOpening(WallSegment Wall, int Lo, int Width);

/// <summary>One rectangle of a building's plan. A house of a single rectangle is one wing; an L, a T or a U is
/// several touching ones, which is what lets a building turn a corner as one house under one style rather than
/// as two houses standing beside each other.
///
/// <para><see cref="Storeys"/> is how many of the style's storeys stand on this wing, and <b>nought takes them
/// all</b> — which is what a building whose wings are all of a height wants, and what one wing on its own can
/// only mean. A hall of one storey with a two-storey cross wing is the shape that needs the number.</para></summary>
/// <summary>Which way a wing's ridge runs, where its own proportions are not what should decide.</summary>
public enum RidgeAxis
{
    /// <summary>Along x, so the slopes are taken across z.</summary>
    AlongX,

    /// <summary>Along z, so the slopes are taken across x.</summary>
    AlongZ,
}

public readonly record struct Wing(
    int MinX, int MinZ, int MaxX, int MaxZ, int Storeys = 0,
    RoofForm? Form = null, int Pitch = 0, int? RoofSlab = null, RidgeAxis? Ridge = null)
{
    public int Width => MaxX - MinX + 1;
    public int Depth => MaxZ - MinZ + 1;

    public bool Holds(int x, int z) => x >= MinX && x <= MaxX && z >= MinZ && z <= MaxZ;

    /// <summary>The roof this wing wears, where it does not simply wear the building's. A wing carries its own
    /// form, its own rise per block, and its own slab — naming a slab is what makes a roof climb in halves, so
    /// a half-stepping wing is one that names one. Unset takes the style's, which is what a building whose
    /// wings are roofed alike wants and what one wing on its own can only mean.</summary>
    public RoofForm FormOr(RoofForm fallback) => Form ?? fallback;

    /// <inheritdoc cref="FormOr"/>
    public int PitchOr(int fallback) => Pitch > 0 ? Pitch : fallback;

    /// <inheritdoc cref="FormOr"/>
    public int SlabOr(int fallback) => RoofSlab ?? fallback;

    /// <summary>Whether this wing is still standing at a storey, counting from nought at the ground.</summary>
    public bool Reaches(int level) => Storeys <= 0 || level < Storeys;

    /// <summary>Whether the ridge runs east–west. A roof is pitched across the <b>shorter</b> side, so by
    /// default the ridge lies along the longer one — the same choice <see cref="RoofField"/> makes, and the
    /// reason it is a property of the wing is that the two ends it names are the wing's gable ends.
    ///
    /// <para><b>Proportions alone cannot answer this for a building of several wings.</b> Whether two wings
    /// make a junction at all is whether their ridges <em>cross</em>, and read from each wing on its own they
    /// may easily not: a 10 × 5 hall and a 7 × 6 wing are both wider than deep, so both ridges run along x and
    /// the two roofs meet in a gutter rather than a valley. A <b>square</b> wing is the sharper case — it has
    /// no longer side, the comparison ties, and it can therefore never cross anything. So a wing may state its
    /// axis, and <see cref="Ridge"/> overrides the proportions when it does. Note the rise then follows the
    /// span actually crossed, which is the point: a ridge forced along the shorter side gives the longer one
    /// its slope.</para></summary>
    public bool RidgeAlongX => Ridge switch
    {
        RidgeAxis.AlongX => true,
        RidgeAxis.AlongZ => false,
        _ => MaxZ - MinZ <= MaxX - MinX,
    };

    /// <summary>The two lines the wing's roof plan ends on — its gable ends. A ridge running along x ends at
    /// the ±x walls and one running along z at the ±z walls, so these are the faces a gable is drawn on.</summary>
    public (int Low, int High) GableEnds => RidgeAlongX ? (MinX, MaxX) : (MinZ, MaxZ);

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
/// <para>A corner comes in two kinds. An <see cref="OnCorner">outer</see> one is where the building turns away
/// from itself — the four of a rectangle. An <see cref="OnInnerCorner">inner</see> one is where two wings meet
/// and it turns back into itself, which no run of wall passes through because the building surrounds it on all
/// four sides. Both carry a post, so an L stands on six; only an outer one throws a beam, since an inner one
/// has no direction to throw it in that is not the building itself.</para>
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

    /// <summary>
    /// The plan at one storey — the wings still standing there, as a footprint of their own.
    ///
    /// <para><b>A storey is its own plan, and that is the whole of how a building of unequal wings works.</b>
    /// Where a one-storey hall meets a two-storey cross wing, the ground is one plan over both and the storey
    /// above is a plan over the wing alone; the wall the cross wing needs against the hall's roof is not a new
    /// rule but the ordinary outline of the storey it belongs to, since at that height the hall is not there to
    /// be met. Everything a storey does — its walls, its posts, its corners of both kinds, its window runs, the
    /// steps in from its wall — is then asked of that plan, and none of it knows it is above anything.</para>
    ///
    /// <para>Null where no wing reaches the storey. The ground is always the whole plan.</para></summary>
    public Footprint? At(int level)
    {
        if (level <= 0) return this;
        storeys ??= [];
        if (storeys.TryGetValue(level, out var standing)) return standing;

        var reaching = wings.Where(wing => wing.Reaches(level)).ToArray();
        // A plan none of whose wings stop below the storey is that plan, not a copy of it, so the walk and the
        // runs measured on the ground serve every storey above it too.
        standing = reaching.Length == 0 ? null
            : reaching.Length == wings.Length ? this
            : new Footprint(reaching);
        storeys[level] = standing;
        return standing;
    }

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

    /// <summary>Whether the cell is on the outline: held, with open ground anywhere beside it. This is where a
    /// wall stands, so a wing's edge buried inside another wing is not on it.
    ///
    /// <para><b>Diagonally beside it counts</b>, and the cell where two wings meet is why. Its four orthogonal
    /// neighbours are all building — two walls and two rooms — so an orthogonal test calls it interior and
    /// leaves it open, and the two walls running into it then pass each other touching along nothing but a
    /// vertical edge. That is not a corner: the building has no block where it turns, and the room behind shows
    /// through the seam at a glancing angle. A flood fill will not find it, because nothing can step
    /// diagonally.</para></summary>
    public bool OnPerimeter(int x, int z)
    {
        if (!Holds(x, z)) return false;
        for (var dx = -1; dx <= 1; dx++)
            for (var dz = -1; dz <= 1; dz++)
                if ((dx != 0 || dz != 0) && !Holds(x + dx, z + dz)) return true;
        return false;
    }

    /// <summary>Whether the building turns <b>away</b> from itself here — the four corners of a rectangle, and
    /// where a corner post stands. It is the cell that has no held neighbour opposite it on either axis, which
    /// is what makes it a corner rather than a stretch of wall: a wall carries on through a cell on one axis
    /// and a corner carries on through none.</summary>
    public bool OnCorner(int x, int z)
        => Holds(x, z)
           && !(Holds(x - 1, z) && Holds(x + 1, z))
           && !(Holds(x, z - 1) && Holds(x, z + 1));

    /// <summary>Whether the building turns <b>back into</b> itself here — the cell where two wings meet. The
    /// building surrounds it on all four sides, so no wall runs <em>through</em> it and it belongs to no run,
    /// but a diagonal is open and that open diagonal is the turn. It is the corner the two walls running into
    /// it turn at, and a post stands on it exactly as one stands at a corner the building turns away at: an L
    /// carries six, not five.</summary>
    public bool OnInnerCorner(int x, int z)
    {
        if (!Holds(x, z)) return false;
        if (!Holds(x - 1, z) || !Holds(x + 1, z) || !Holds(x, z - 1) || !Holds(x, z + 1)) return false;
        foreach (var (dx, dz) in Diagonals)
            if (!Holds(x + dx, z + dz)) return true;
        return false;
    }

    private static readonly (int X, int Z)[] Diagonals = [(-1, -1), (1, -1), (-1, 1), (1, 1)];

    /// <summary>Whether the cell lies just outside the plan with the building beside it — the ring one block
    /// proud of the outline that a sill runs round. Diagonals count, so the ring closes at a corner rather than
    /// leaving a gap where the two sides pass each other, and it follows the plan into the crook of two wings
    /// instead of filling the notch a bounding box would.</summary>
    public bool Borders(int x, int z)
    {
        if (Holds(x, z)) return false;
        for (var dx = -1; dx <= 1; dx++)
            for (var dz = -1; dz <= 1; dz++)
                if ((dx != 0 || dz != 0) && Holds(x + dx, z + dz)) return true;
        return false;
    }

    /// <summary>Whether the cell is the plan or within <paramref name="reach"/> blocks of it — what a roof and
    /// its overhang are allowed to cover. A rectangle's roof plan is its box grown by the overhang, so this
    /// takes in the whole of it; a plan that turns a corner has a box holding ground the building never stood
    /// on, and nothing may be laid over that.</summary>
    public bool Near(int x, int z, int reach)
    {
        for (var dx = -reach; dx <= reach; dx++)
            for (var dz = -reach; dz <= reach; dz++)
                if (Holds(x + dx, z + dz)) return true;
        return false;
    }

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
        if (OnCorner(x, z) || OnInnerCorner(x, z)) return GridBoundary.RunsBothWays;
        var arc = Arc(x, z);
        return arc < 0 ? 0 : Perimeter.Run[arc];
    }

    /// <summary>The runs of wall the plan stands in, measured once on first use: every maximal stretch of held
    /// cells with open ground on one side of it. A rectangle answers four, one per facing, which is what lets a
    /// caller that used to name a wall by its direction keep the answers it had; an L answers six and a T eight,
    /// because a wall ends wherever the building turns — away from itself or back into itself.
    ///
    /// <para>Ordered by facing, then by the line the wall stands on, then along it, so the order is a property
    /// of the plan rather than of how the wings were listed.</para></summary>
    public IReadOnlyList<WallSegment> Segments => segments ??= SplitWalls();

    /// <summary>The run of wall on one side of the building that a thing placed <paramref name="about"/> a place
    /// along that side belongs to: the longest of those looking that way whose stretch reaches it, or simply the
    /// longest where none does. A rectangle has one run per side and answers it whatever the rule; the rule is
    /// what a plan that turns a corner needs, and length is the tiebreak because the wall a building is entered
    /// by is its face rather than its return. Null only where the plan looks nowhere in that direction.</summary>
    public WallSegment? WallFacing(RoomEdge facing, int about)
    {
        WallSegment? best = null;
        foreach (var wall in Segments)
        {
            if (wall.Facing != facing) continue;
            if (best is not { } chosen) { best = wall; continue; }
            if (wall.Holds(about) != chosen.Holds(about)) { if (wall.Holds(about)) best = wall; continue; }
            if (wall.Length > chosen.Length) best = wall;
        }
        return best;
    }

    private WallSegment[] SplitWalls()
    {
        var found = new List<WallSegment>();
        foreach (var facing in new[] { RoomEdge.NegZ, RoomEdge.PosZ, RoomEdge.NegX, RoomEdge.PosX })
        {
            var alongX = facing is RoomEdge.NegZ or RoomEdge.PosZ;
            var (lineLo, lineHi) = alongX ? (MinZ, MaxZ) : (MinX, MaxX);
            var (alongLo, alongHi) = alongX ? (MinX, MaxX) : (MinZ, MaxZ);
            var (outX, outZ) = facing switch
            {
                RoomEdge.NegZ => (0, -1),
                RoomEdge.PosZ => (0, 1),
                RoomEdge.NegX => (-1, 0),
                _ => (1, 0),
            };

            for (var line = lineLo; line <= lineHi; line++)
            {
                var from = int.MinValue;
                // One step past the end closes a run that reaches it, so a wall running to the far edge of the
                // plan is not left open.
                for (var along = alongLo; along <= alongHi + 1; along++)
                {
                    var (x, z) = alongX ? (along, line) : (line, along);
                    var faces = along <= alongHi && Holds(x, z) && !Holds(x + outX, z + outZ);
                    if (faces && from == int.MinValue) from = along;
                    else if (!faces && from != int.MinValue)
                    {
                        found.Add(new WallSegment(facing, line, from, along - 1));
                        from = int.MinValue;
                    }
                }
            }
        }
        return [.. found];
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
    private WallSegment[]? segments;
    private Dictionary<int, Footprint?>? storeys;

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
