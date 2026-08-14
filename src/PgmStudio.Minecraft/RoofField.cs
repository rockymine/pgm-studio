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
/// <para><b>A pitched roof has three kinds of edge and they do not behave alike.</b> The <b>ridge</b> is where
/// the slopes meet. An <b>eave</b> is the bottom edge of a slope — level, at the wall's top, and where water
/// leaves the roof — so its overhang is one solid course running the length. A <b>verge</b> is the raked edge
/// at a gable end, climbing eave to ridge, and its overhang is a triangle that is <b>open beneath</b>: nothing
/// stands under it, because nothing sheds onto it. Which of a wing's four sides are which follows from
/// <see cref="Reach"/> — the slopes are taken across the shorter span, so the two edges that span it are the
/// eaves and the two the ridge ends on are the verges. This type answers heights and leaves the edge a cell
/// stands on to the stamper, which is the only place the <em>building's</em> outline is known: one rectangle's
/// rim is not an L's, and a cell can be the edge of a wing and the middle of a house.</para>
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
    private readonly bool inHalves;
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
    /// <param name="pitch">Rise per block travelled inward — in whole courses, or in <em>half</em> courses when
    /// <paramref name="inHalves"/> is set.</param>
    /// <param name="front">The wall a shed falls to and a saltbox turns its steep side toward. Every other form
    /// ignores it: a gable and a hip are symmetric, and a flat lid has no slope to point.</param>
    /// <param name="inHalves">Whether the roof climbs in half blocks, laying a slab on every odd step. The
    /// field is measured in halves either way; a whole-course roof is simply one that steps two at a time, so
    /// the forms' arithmetic is untouched and a roof laid in cubes answers exactly what it always did.</param>
    public RoofField(
        RoofForm form, int wallMinX, int wallMinZ, int wallMaxX, int wallMaxZ,
        int overhang, int baseY, int pitch, RoomEdge front, bool inHalves = false)
    {
        this.form = form;
        (this.wallMinX, this.wallMinZ, this.wallMaxX, this.wallMaxZ) = (wallMinX, wallMinZ, wallMaxX, wallMaxZ);
        this.overhang = Math.Max(0, overhang);
        this.baseY = baseY;
        this.pitch = Math.Max(1, pitch);
        this.inHalves = inHalves;
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
        new(form, wallMinX, wallMinZ, wallMaxX, wallMaxZ, overhang, baseY + courses, pitch, front, inHalves);

    /// <summary>The plan rectangle the roof draws over: the walls plus the overhang on every side.</summary>
    public int MinX => wallMinX - overhang;
    public int MinZ => wallMinZ - overhang;
    public int MaxX => wallMaxX + overhang;
    public int MaxZ => wallMaxZ + overhang;

    /// <summary>The highest and lowest course any of the roof's columns tops out at.</summary>
    public int Peak { get; }

    public int Trough { get; }

    public bool Covers(int x, int z) => x >= MinX && x <= MaxX && z >= MinZ && z <= MaxZ;

    /// <summary>The course the column at this cell tops out at. Where that top block is a
    /// <see cref="Half"/> it is the cell the slab sits in, so the roof's surface is the middle of it rather
    /// than the top.</summary>
    public int Crown(int x, int z) => baseY + FloorHalf(1 + Rise(x, z));

    /// <summary>Whether the block at the <see cref="Crown"/> is a <b>slab</b> — the odd half-step of a roof
    /// that climbs half a block at a time. A roof laid in whole courses never answers yes: it steps two halves
    /// at once and so is never caught between them.</summary>
    public bool Half(int x, int z) => inHalves && (((Rise(x, z) % 2) + 2) % 2) == 1;

    /// <summary>How far the roof's own surface stands above its base plane at this cell, in <b>half</b> blocks.
    /// Every form answers here, and the only difference a slab roof makes is that it travels one half per block
    /// where a cube roof travels two.
    ///
    /// <para>Halves rather than blocks because a slope of half a block per block is the roof a slab is actually
    /// for: laid in cubes it would step a whole block at a time and want stairs, and laid in slabs at a whole
    /// block of rise it leaves an open half between every pair that the roof can be seen straight through.</para>
    /// </summary>
    private int Rise(int x, int z)
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

        // Whole courses are two halves at a time; a slab roof travels one. Applying the step here rather than
        // inside each form is what leaves the six formulas untouched — they answer in blocks travelled and the
        // roof decides what a block travelled is worth.
        var step = inHalves ? pitch : 2 * pitch;
        return form switch
        {
            RoofForm.Flat => 0,
            RoofForm.Shed => Reach(fromFront) * step,
            RoofForm.Hip => Math.Min(Math.Min(westward, eastward), Math.Min(northward, southward)) * step,
            RoofForm.Gambrel => Gambrel(Math.Min(acrossLow, acrossHigh), step),
            // Both slopes rise from their own eave, and the ridge is simply where they meet: at one rate they
            // meet in the middle and it is a gable, at two different rates the steeper side reaches the other
            // first and the ridge slides toward it, which is the whole of a saltbox.
            //
            // Taken across the shorter side exactly as a gable is, so the front decides which slope is the
            // steep one and nothing about how high the roof stands. Measuring it from the front instead lets
            // both slopes run the length of a hall, where each saturates on its own and they never meet — the
            // ridge is then the long side's, which is the one thing no roof's height may be.
            RoofForm.Saltbox => Math.Min(SteepSide(acrossLow, acrossHigh) * (step + 1),
                                         ShallowSide(acrossLow, acrossHigh) * step),
            _ => Math.Min(acrossLow, acrossHigh) * step,
        };
    }

    /// <summary>Halve, rounding <b>down</b> rather than toward zero. The eave falls below the roof's own base
    /// plane, so the rise goes negative there, and a truncating divide would round those cells back up and lift
    /// the overhang half a block clear of the slope it belongs to.</summary>
    private static int FloorHalf(int halves) => halves >= 0 ? halves / 2 : -((-halves + 1) / 2);

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

    /// <summary>The line the slopes meet on. A flat lid has none — every cell of it would answer yes, and a
    /// ridge that is the whole roof is not a ridge.</summary>
    public bool OnRidge(int x, int z) => form != RoofForm.Flat && Crown(x, z) == Peak;

    /// <summary>A barn roof: steep for the first courses in from the eave, then shallow to the ridge. The break
    /// sits a quarter of the span in, so the steep skirt is half of each slope and the shallow cap the other
    /// half — the proportion that reads as a gambrel rather than as a gable with a kink.</summary>
    private int Gambrel(int inward, int step)
    {
        var span = (acrossZ ? wallMaxZ - wallMinZ : wallMaxX - wallMinX) + 1;
        var brk = Math.Max(1, span / 4);
        var steep = step + 1;
        return inward <= brk ? inward * steep : brk * steep + (inward - brk) * step;
    }
}
