using PgmStudio.Domain;

namespace PgmStudio.Minecraft;

/// <summary>
/// A roof as a <b>height field over its plan</b>: for every cell it covers, the course its topmost block sits
/// at, how many courses that column has to write to close the step down to its neighbours, and whether the cell
/// is on the roof's border or on its ridge.
///
/// <para>Every form is one formula over the same two distances — how far a cell stands from each wall line —
/// so the stamper writes one loop and the forms differ only in what they answer here. A flat lid answers a
/// constant, a gable the smaller distance across the building, a hip the smaller of all four, and the rest are
/// that shape bent: a gambrel steepens the first courses in from the eave, a saltbox steepens one side alone
/// so the ridge slides off centre, a shed drops the ridge onto one wall and slopes toward the opposite one.</para>
///
/// <para><b>Every rise is measured over the building's shorter side</b> (<see cref="Reach"/>). The forms
/// measured across the building get that for nothing; the two measured from the front wall are held to it, so
/// a long hall carries a long roof rather than a tall one.</para>
///
/// <para><b>Distances are measured from the wall line, not from the roof's own edge, and are allowed to go
/// negative.</b> That is what makes the overhang part of the slope: the course over the wall rests on the wall,
/// and every course outward from there keeps falling at the same rate, so the eave hangs below its neighbour
/// instead of running the last blocks flat exactly where the roof is most visible.</para>
/// </summary>
public sealed class RoofField
{
    private readonly RoofForm form;
    private readonly int wallMinX, wallMinZ, wallMaxX, wallMaxZ;
    private readonly int overhang, baseY, pitch;
    private readonly RoomEdge front;

    /// <summary>Whether the slopes are taken across Z — a gable's ridge runs the length of a building, so the
    /// slope is taken across its <em>shorter</em> side. Pitching the long way would put the ridge on the short
    /// axis and give a long building two enormous faces and no length.</summary>
    private readonly bool acrossZ;

    /// <summary>The shorter of the two wall spans, in blocks — the run every form's rise is measured over, and
    /// the reason a long building is a long building rather than a tall one.</summary>
    private readonly int shortSpan;

    /// <param name="form">Which roof this is.</param>
    /// <param name="overhang">How far past the walls the roof reaches, in blocks.</param>
    /// <param name="baseY">The course the roof stands at over the wall line — one above the wall's last.</param>
    /// <param name="pitch">Courses of rise per block travelled inward.</param>
    /// <param name="front">The wall a shed falls to and a saltbox turns its steep side toward. Every other form
    /// ignores it: a gable and a hip are symmetric, and a flat lid has no slope to point.</param>
    public RoofField(
        RoofForm form, int wallMinX, int wallMinZ, int wallMaxX, int wallMaxZ,
        int overhang, int baseY, int pitch, RoomEdge front)
    {
        this.form = form;
        (this.wallMinX, this.wallMinZ, this.wallMaxX, this.wallMaxZ) = (wallMinX, wallMinZ, wallMaxX, wallMaxZ);
        this.overhang = Math.Max(0, overhang);
        this.baseY = baseY;
        this.pitch = Math.Max(1, pitch);
        this.front = front;
        acrossZ = wallMaxZ - wallMinZ <= wallMaxX - wallMinX;
        shortSpan = Math.Max(1, Math.Min(wallMaxZ - wallMinZ, wallMaxX - wallMinX) + 1);

        var (lowest, highest) = (int.MaxValue, int.MinValue);
        for (var x = MinX; x <= MaxX; x++)
            for (var z = MinZ; z <= MaxZ; z++)
            {
                var crown = Crown(x, z);
                lowest = Math.Min(lowest, crown);
                highest = Math.Max(highest, crown);
            }
        (Trough, Peak) = (lowest, highest);
    }

    /// <summary>The same roof standing <paramref name="courses"/> higher or lower — how a porch's canopy is
    /// seated. A lean-to is placed by where its <em>ridge</em> has to land (under the house's eave) rather than
    /// by where its plane starts, and the ridge's offset from the base is what the form decides, so the base is
    /// solved for after the field is built rather than guessed before it.</summary>
    public RoofField Raised(int courses) =>
        new(form, wallMinX, wallMinZ, wallMaxX, wallMaxZ, overhang, baseY + courses, pitch, front);

    /// <summary>The plan rectangle the roof draws over: the walls plus the overhang on every side.</summary>
    public int MinX => wallMinX - overhang;
    public int MinZ => wallMinZ - overhang;
    public int MaxX => wallMaxX + overhang;
    public int MaxZ => wallMaxZ + overhang;

    /// <summary>The highest and lowest course any of the roof's columns tops out at.</summary>
    public int Peak { get; }

    public int Trough { get; }

    public bool Covers(int x, int z) => x >= MinX && x <= MaxX && z >= MinZ && z <= MaxZ;

    /// <summary>The course the column at this cell tops out at.</summary>
    public int Crown(int x, int z)
    {
        int westward = x - wallMinX, eastward = wallMaxX - x;
        int northward = z - wallMinZ, southward = wallMaxZ - z;
        int acrossLow = acrossZ ? northward : westward;
        int acrossHigh = acrossZ ? southward : eastward;
        var (fromFront, fromBack) = front switch
        {
            RoomEdge.NegZ => (northward, southward),
            RoomEdge.PosZ => (southward, northward),
            RoomEdge.NegX => (westward, eastward),
            _ => (eastward, westward),
        };

        return baseY + form switch
        {
            RoofForm.Flat => 0,
            RoofForm.Shed => Reach(fromFront) * pitch,
            RoofForm.Hip => Math.Min(Math.Min(westward, eastward), Math.Min(northward, southward)) * pitch,
            RoofForm.Gambrel => Gambrel(Math.Min(acrossLow, acrossHigh)),
            // Both slopes rise from their own eave, and the ridge is simply where they meet: at one rate they
            // meet in the middle and it is a gable, at two different rates the steeper side reaches the other
            // first and the ridge slides toward it, which is the whole of a saltbox.
            //
            // Taken across the shorter side exactly as a gable is, so the front decides which slope is the
            // steep one and nothing about how high the roof stands. Measuring it from the front instead lets
            // both slopes run the length of a hall, where each saturates on its own and they never meet — the
            // ridge is then the long side's, which is the one thing no roof's height may be.
            RoofForm.Saltbox => Math.Min(SteepSide(acrossLow, acrossHigh) * (pitch + 1),
                                         ShallowSide(acrossLow, acrossHigh) * pitch),
            _ => Math.Min(acrossLow, acrossHigh) * pitch,
        };
    }

    /// <summary>How far a slope that falls toward the <em>front</em> is allowed to climb: the short side's own
    /// run, whatever the run it actually has.
    ///
    /// <para><b>No roof climbs with the long side of a building.</b> A gable, a hip and a gambrel are measured
    /// across the shorter side already, so the rule costs them nothing. A shed and a saltbox are measured from
    /// the front wall, and the front is wherever the doors are — so on a building longer than it is deep they
    /// would climb the long way, and a 10×60 hall would carry a lean-to sixty courses over its wall. Held to the
    /// short side, a shed climbs at its pitch and then <b>runs flat</b> the rest of the way, which is a lean-to
    /// that levels off, and a saltbox's long shallow slope stops short of where it would otherwise have met the
    /// steep one.</para></summary>
    private int Reach(int distance) => Math.Min(distance, shortSpan - 1);

    /// <summary>Which of a saltbox's two across-distances belongs to the steep slope: the one on the front's own
    /// side of the building. Where the front wall is an end rather than a side it lies on neither, and the low
    /// side is taken — a saltbox has to face <em>somewhere</em>, and a stable choice beats one that flips with
    /// a door the slope does not run toward anyway.</summary>
    private int SteepSide(int acrossLow, int acrossHigh) => FrontIsHigh ? acrossHigh : acrossLow;

    private int ShallowSide(int acrossLow, int acrossHigh) => FrontIsHigh ? acrossLow : acrossHigh;

    private bool FrontIsHigh => acrossZ ? front == RoomEdge.PosZ : front == RoomEdge.PosX;

    /// <summary>How many courses the column writes. A step of more than one course leaves the slope open between
    /// its treads, so each column carries its own riser as well as its tread — as deep as the deepest step down
    /// to a neighbour the roof also covers. A cell with nothing lower beside it is the eave and stays one.</summary>
    public int Riser(int x, int z)
    {
        var crown = Crown(x, z);
        var drop = 0;
        foreach (var (nextX, nextZ) in new[] { (x - 1, z), (x + 1, z), (x, z - 1), (x, z + 1) })
            if (Covers(nextX, nextZ)) drop = Math.Max(drop, crown - Crown(nextX, nextZ));
        return Math.Max(1, drop);
    }

    /// <summary>The lowest course the column occupies — what a wall climbs to meet.</summary>
    public int Underside(int x, int z) => Crown(x, z) - Riser(x, z) + 1;

    /// <summary>The roof's own outermost ring — its eave course and its verges. This is the edge of the roof,
    /// not the part of it that oversails the wall: a roof ending flush still has an edge, and it is the edge
    /// that is trimmed.</summary>
    public bool OnBorder(int x, int z) => x == MinX || x == MaxX || z == MinZ || z == MaxZ;

    /// <summary>The line the slopes meet on. A flat lid has none — every cell of it would answer yes, and a
    /// ridge that is the whole roof is not a ridge.</summary>
    public bool OnRidge(int x, int z) => form != RoofForm.Flat && Crown(x, z) == Peak;

    /// <summary>A barn roof: steep for the first courses in from the eave, then shallow to the ridge. The break
    /// sits a quarter of the span in, so the steep skirt is half of each slope and the shallow cap the other
    /// half — the proportion that reads as a gambrel rather than as a gable with a kink.</summary>
    private int Gambrel(int inward)
    {
        var span = (acrossZ ? wallMaxZ - wallMinZ : wallMaxX - wallMinX) + 1;
        var brk = Math.Max(1, span / 4);
        var steep = pitch + 1;
        return inward <= brk ? inward * steep : brk * steep + (inward - brk) * pitch;
    }
}
