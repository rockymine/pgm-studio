namespace PgmStudio.Pgm.Shapes;

/// <summary>The approach <b>slot roles</b> — the taxonomy every emitted piece carries, naming its position in
/// the family template so the composition rules read as properties of a slot rather than raw geometry. Two
/// layers are conflated here, split conceptually per <c>docs/contracts/shape-vocabulary.md</c> §8:
///
/// <para><b>Structural slots</b> — a rectangle's role <em>in the compound</em>, intrinsic to the shape and
/// shared by every box kind: <see cref="Run"/> (a corridor/spine segment), <see cref="Bar"/> (a crossing
/// segment), <see cref="Leg"/> (a ring arm / stub). Width-orthogonal; a plaza is a bar at area width.</para>
///
/// <para><b>Designation marks</b> — what a box kind stamps onto the compound: <see cref="Entry"/> (the docking
/// rect — a lane's mouth, either leg of a U/H, either bar of a clamp, the donut's hub stub; the target of entry
/// shift and entry width) and <see cref="Room"/> (the terminal — the wool/spawn room, the target of the
/// extend-vs-side-dock rule). The hub's per-edge interfaces and the frontline's <c>face</c> are the other box
/// kinds' marks, arriving with those designations.</para>
///
/// <para>The <b>composite</b> labels are a structural slot that also carries a mark, where a family has two of
/// a segment: <see cref="EntryRun"/>/<see cref="RoomRun"/> are the run a host docks vs the run leading to the
/// terminal; <see cref="EntryBar"/>/<see cref="RoomBar"/> the same for a bar. A role is a <b>template slot, not
/// a property of the rectangle</b> — a scythe's <c>entry-run</c> and a donut's <c>leg</c> can be the same
/// rectangle in different slots. See the piece vocabulary in <c>docs/contracts/map-generation.md</c> §5.</summary>
public static class ApproachSlots
{
    // structural slots — the rectangle's role in the compound (shared by every designation)
    public const string Run = "run";
    public const string Bar = "bar";
    public const string Leg = "leg";

    // designation marks — what the approach designation stamps (the docking rect and the terminal)
    public const string Entry = "entry";
    public const string Room = "room";

    // composite: a structural slot carrying a mark, where a family has two of the segment
    public const string EntryRun = "entry-run";
    public const string RoomRun = "room-run";
    public const string EntryBar = "entry-bar";
    public const string RoomBar = "room-bar";

    /// <summary>The canonical ordered slot template of <paramref name="family"/> — terrain slots in emit order,
    /// the <see cref="Room"/> last — the §5.3 piece-vocabulary table as data. This is the base configuration
    /// (single donut attachment, no wool-extend, inline room); the optional donut knobs add pieces
    /// (a second attachment is another <see cref="Entry"/>, a wool-extend a <see cref="Run"/>).</summary>
    public static IReadOnlyList<string> Template(ShapeFamily family) => family switch
    {
        ShapeFamily.I     => [Entry, Room],
        ShapeFamily.L     => [Entry, Run, Room],
        ShapeFamily.Z     => [Entry, Bar, RoomRun, Room],
        ShapeFamily.Scythe => [Entry, EntryRun, Bar, RoomRun, Room],
        ShapeFamily.Clamp => [Entry, Entry, Room],
        ShapeFamily.U     => [Bar, Entry, Entry, Room],
        ShapeFamily.H     => [Bar, Entry, Entry, RoomRun, Room],
        ShapeFamily.Donut => [EntryBar, Leg, Leg, Entry, RoomBar, Room],
        _ => throw new ArgumentOutOfRangeException(nameof(family), family, "no slot template for this family"),
    };
}

/// <summary>Where the wool room sits relative to the approach's final segment. <see cref="Inline"/> continues
/// it straight (the plain dead-end). <see cref="SideTuck"/> turns the room off perpendicular at the end — the
/// catalog's <c>side-tuck</c>: still an <b>I</b> lane, because the categorizer excludes the room from the bend
/// read, so the straight approach reads as I while the wool hangs off its end.</summary>
public enum RoomPlacement { Inline, SideTuck }
