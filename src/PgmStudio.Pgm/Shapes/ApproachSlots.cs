namespace PgmStudio.Pgm.Shapes;

/// <summary>The wool-approach <b>slot roles</b> — the shape-internal taxonomy every emitted piece carries,
/// naming its position in the family template so the composition rules read as properties of a slot rather
/// than raw geometry. <see cref="Entry"/> is the universal hub-attach (a lane's mouth, either leg of a U/H,
/// either bar of a clamp, the donut's hub stub) — the target of entry shift and entry width;
/// <see cref="Room"/> is the wool room — the target of the extend-vs-side-dock rule; <see cref="Run"/>/
/// <see cref="Bar"/> are corridor/crossing segments, qualified <see cref="EntryRun"/>/<see cref="RoomRun"/>
/// and <see cref="EntryBar"/>/<see cref="RoomBar"/> where a family has two; <see cref="Leg"/> is a donut ring
/// arm. A role is a <b>template slot, not a property of the rectangle</b> — a scythe's <c>entry-run</c> and a
/// donut's <c>leg</c> can be the same rectangle in different slots. See the piece vocabulary in
/// <c>docs/contracts/map-generation.md</c> §5.</summary>
public static class ApproachSlots
{
    public const string Entry = "entry";
    public const string Run = "run";
    public const string Bar = "bar";
    public const string Leg = "leg";
    public const string Room = "room";
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
