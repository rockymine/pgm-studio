namespace PgmStudio.Pgm.Shapes;

/// <summary>The <b>parts</b> a compound's pieces play — the taxonomy every emitted piece carries, naming its
/// position in the family template so the composition rules read as properties of a part rather than raw geometry
/// (<c>docs/generator/model.md</c> §4, the piece vocabulary).
///
/// <para><b>Structural parts</b> are intrinsic to the shape and shared by every box kind: <see cref="Run"/> (a
/// corridor/spine segment), <see cref="Bar"/> (a crossing segment), <see cref="Leg"/> (a ring arm / stub).
/// Width-orthogonal; a plaza is a bar at area width.</para>
///
/// <para><see cref="Entry"/> is the docking rect — a lane's mouth, either leg of a U/H, either bar of a clamp,
/// the donut's hub stub; the target of entry shift and entry width. <see cref="Room"/> is the piece the compound
/// ends in, where the wool or spawn stands; the target of the extend-vs-side-dock rule.</para>
///
/// <para>The <b>composite</b> parts are a structural part on the way to one of those two, where a family has two
/// of a segment: <see cref="EntryRun"/>/<see cref="RoomRun"/> are the run a host docks vs the run leading to the
/// room; <see cref="EntryBar"/>/<see cref="RoomBar"/> the same for a bar. A part is a <b>template position, not
/// a property of the rectangle</b> — a scythe's <c>entry-run</c> and a donut's <c>leg</c> can be the same
/// rectangle in different parts.</para>
/// </summary>
public static class CompoundParts
{
    // structural parts — shared by every box kind
    public const string Run = "run";
    public const string Bar = "bar";
    public const string Leg = "leg";

    // the docking rect and the room
    public const string Entry = "entry";
    public const string Room = "room";

    // composite: a structural part leading to the entry or the room, where a family has two of the segment
    public const string EntryRun = "entry-run";
    public const string RoomRun = "room-run";
    public const string EntryBar = "entry-bar";
    public const string RoomBar = "room-bar";

    /// <summary>The canonical ordered part template of <paramref name="family"/> — terrain parts in emit order,
    /// the <see cref="Room"/> last — model.md §4's piece-vocabulary table as data. This is the base configuration
    /// (single donut attachment, no wool-extend, inline room); the optional donut knobs add pieces
    /// (a second attachment is another <see cref="Entry"/>, a wool-extend a <see cref="Run"/>).</summary>
    public static IReadOnlyList<string> Template(CompoundForm family) => family switch
    {
        CompoundForm.I     => [Entry, Room],
        CompoundForm.L     => [Entry, Run, Room],
        CompoundForm.Z     => [Entry, Bar, RoomRun, Room],
        CompoundForm.Scythe => [Entry, EntryRun, Bar, RoomRun, Room],
        CompoundForm.Clamp => [Entry, Entry, Room],
        CompoundForm.U     => [Bar, Entry, Entry, Room],
        CompoundForm.H     => [Bar, Entry, Entry, RoomRun, Room],
        CompoundForm.Donut => [EntryBar, Leg, Leg, Entry, RoomBar, Room],
        _ => throw new ArgumentOutOfRangeException(nameof(family), family, "no part template for this family"),
    };
}

/// <summary>Where the wool room sits relative to the approach's final segment. <see cref="Inline"/> continues
/// it straight (the plain dead-end). <see cref="SideTuck"/> turns the room off perpendicular at the end — the
/// catalog's <c>side-tuck</c>: still an <b>I</b> lane, because the categorizer excludes the room from the bend
/// read, so the straight approach reads as I while the wool hangs off its end.</summary>
public enum RoomPlacement { Inline, SideTuck }
