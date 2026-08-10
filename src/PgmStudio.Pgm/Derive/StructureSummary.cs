using PgmStudio.Geom;
using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Derive;

/// <summary>
/// The finished structural read of a composed unit — the third mirror surfaced as a browse/duel fact: the
/// sorted wool <b>approach families</b>, the hub <b>body form</b>, the frontline <b>body form</b> (or
/// none), and the <b>dock arrangement</b> (which hub side each neighbour seats on). Derived from the
/// generator's own labeled artifacts (each piece's <see cref="GrownPiece.Box"/> +
/// <see cref="GrownPiece.Slot"/>), never from a finished map's welded terrain, so it reads uniformly off a
/// bare unit and needs no labels stored back. It is the canonical filter fact for the browse sieve (G117) and
/// the bucket key for verdicts (G118) / duels (G120): the same read the user sees on the card.
/// <para>Reuses the two validated classifier entry points: <see cref="ShapeClassifier.Classify"/> (approach
/// family, per wool box) and <see cref="ShapeClassifier.ClassifyBody"/> (body form, per hub/frontline box).
/// The wool-family scan is the pattern promoted from the box gallery tool.</para>
/// </summary>
public sealed record StructureSummary(
    IReadOnlyList<ShapeFamily> Wools,   // sorted (deterministic canonical order)
    CompoundRead? Hub,                  // the hub body form (null only if a unit somehow has no hub box)
    CompoundRead? Frontline,            // the frontline body form, or null = none (a real, sampled outcome)
    DockArrangement? Seats)             // which hub side each neighbour docks (null only without a hub/spawn)
{
    public static StructureSummary Derive(GrownUnit unit) => new(
        WoolFamilies(unit).OrderBy(f => f).ToList(),
        BoxForm(unit, BoxKind.Hub),
        BoxForm(unit, BoxKind.Frontline),
        DockArrangement.Of(unit));

    /// <summary>Classify each wool box to its emitted approach family (box scope — its own pieces + room).
    /// Public because the box gallery tool shares this exact read.</summary>
    public static List<ShapeFamily> WoolFamilies(GrownUnit unit)
    {
        var fams = new List<ShapeFamily>();
        foreach (var boxId in WoolBoxIds(unit))
        {
            var boxPieces = unit.Pieces.Where(p => p.Box?.Id == boxId).ToList();
            var room = boxPieces.FirstOrDefault(p => p.Slot == ApproachSlots.Room);
            if (room is null) continue;
            var filled = new HashSet<(int, int)>();
            var roomCells = new HashSet<(int, int)>();
            foreach (var p in boxPieces)
                foreach (var c in CellsOf(p.Rect))
                {
                    filled.Add(c);
                    if (p.Id == room.Id) roomCells.Add(c);
                }
            fams.Add(ShapeClassifier.Classify(filled, roomCells).Family);
        }
        return fams;
    }

    private static IEnumerable<string> WoolBoxIds(GrownUnit unit) =>
        unit.Pieces.Where(p => p.Box?.Kind == BoxKind.Wool).Select(p => p.Box!.Id).Distinct();

    /// <summary>The body form of the single box of <paramref name="kind"/> (hub or frontline), or null when a
    /// unit has none (a no-frontline unit).</summary>
    private static CompoundRead? BoxForm(GrownUnit unit, BoxKind kind)
    {
        var boxId = unit.Pieces.Where(p => p.Box?.Kind == kind).Select(p => p.Box!.Id).Distinct().FirstOrDefault();
        if (boxId is null) return null;
        var cells = new HashSet<(int, int)>();
        foreach (var p in unit.Pieces.Where(p => p.Box?.Id == boxId))
            foreach (var c in CellsOf(p.Rect))
                cells.Add(c);
        return cells.Count == 0 ? null : ShapeClassifier.ClassifyBody(cells);
    }

    private static IEnumerable<(int, int)> CellsOf(CellRect rect)
    {
        for (var x = rect.X; x < rect.X + rect.Width; x++)
            for (var z = rect.Z; z < rect.Z + rect.Height; z++)
                yield return (x, z);
    }

    /// <summary>A stable, lowercase, order-independent key:
    /// <c>wools:donut,l|hub:ring|front:none|seat:canonical</c>. Persisted on a pinned plan and used as the
    /// verdict/duel bucket key.</summary>
    public string Canonical() =>
        $"wools:{string.Join(",", Wools.Select(StructureNames.Family))}" +
        $"|hub:{StructureNames.Form(Hub)}" +
        $"|front:{StructureNames.Form(Frontline)}" +
        $"|seat:{StructureNames.Seat(Seats)}";
}

/// <summary>
/// The unit's <b>dock arrangement</b>: which hub side the spawn and each wool box seat on, in the unit's own
/// compass (the frontline side is <em>front</em>, the spawn's facing direction — <see cref="GrownSpawn.Facing"/>
/// is the absolute board direction toward the axis, so the read needs no symmetry string). A board property in
/// its own right — not the hub's body form and not the approach family — with measured consequences: the
/// <b>canonical</b> arrangement (spawn back, wools flanking on the laterals) roughly halves the
/// spawn-distance imbalance against the <b>lopsided</b> one (spawn lateral, a wool taking the back), and is
/// the arrangement built maps converge on (docs/match-flow.md §3.3).
/// </summary>
public sealed record DockArrangement(UnitSide Spawn, IReadOnlyList<UnitSide> Wools)
{
    /// <summary>Spawn on the back with the wools flanking — the arrangement built maps converge on. The spawn
    /// side alone decides it: the seating never puts the spawn on the front, and a wool takes the back exactly
    /// when the spawn does not hold it (or, third-wool case, doubles onto the spawn's own back edge).</summary>
    public bool Canonical => Spawn == UnitSide.Back;

    /// <summary>Read the arrangement off a grown unit: group its pieces into boxes
    /// (<see cref="BoxPartition.Of"/>), take each spawn/wool box's joint against the hub, and name the hub edge
    /// it docks in compass terms. <c>null</c> when the unit has no hub box or the spawn never docks it (not a
    /// composed unit's shape).</summary>
    public static DockArrangement? Of(GrownUnit unit)
    {
        var partition = BoxPartition.Of(unit);
        var hub = partition.Boxes.FirstOrDefault(b => b.Kind == BoxKind.Hub);
        if (hub is null) return null;

        var frame = FacingFrame(unit.Spawn.Facing);
        var sides = new[] { UnitSide.Front, UnitSide.Back, UnitSide.Left, UnitSide.Right }
            .ToDictionary(side => SeatGeometry.SideEdge(frame, side), side => side);

        UnitSide? spawnSide = null;
        var woolSides = new List<UnitSide>();
        foreach (var joint in partition.JointsOf(hub.Id))
        {
            var neighbour = partition.ById(joint.BoxA == hub.Id ? joint.BoxB : joint.BoxA)!;
            if (neighbour.Kind is not (BoxKind.Spawn or BoxKind.Wool)) continue;
            // the abutment is read on BoxA's frame; when the hub is BoxB its own edge is the opposite one
            var hubEdge = joint.BoxA == hub.Id ? joint.Abutment.Edge : SeatGeometry.Opposite(joint.Abutment.Edge);
            if (neighbour.Kind == BoxKind.Spawn) spawnSide = sides[hubEdge];
            else woolSides.Add(sides[hubEdge]);
        }
        return spawnSide is { } spawn ? new DockArrangement(spawn, woolSides.OrderBy(s => s).ToList()) : null;
    }

    /// <summary>The frame whose <see cref="Frame.TowardAxis"/> is <paramref name="facing"/> — the inverse of
    /// that mapping, so the compass read reuses <see cref="SeatGeometry.SideEdge"/> instead of restating it.</summary>
    private static Frame FacingFrame(string facing) => facing switch
    {
        "back" => new Frame('z', -1),
        "left" => new Frame('x', +1),
        "right" => new Frame('x', -1),
        _ => new Frame('z', +1),                                      // "front"
    };
}

/// <summary>The display/filter tokens for the structural vocabulary — one mapping shared by the card badges,
/// the filter chips, the canonical bucket key, and the sieve query. Lowercase and stable.</summary>
public static class StructureNames
{
    /// <summary>A wool approach family token: <c>donut</c>, <c>l</c>, <c>i</c>, <c>u</c>, <c>h</c>, <c>clamp</c>…</summary>
    public static string Family(ShapeFamily f) => f.ToString().ToLowerInvariant();

    /// <summary>The dock-arrangement token: <c>canonical</c> (spawn back, wools flanking), <c>lopsided</c>
    /// (spawn lateral, a wool on the back), or <c>none</c> when the unit gave no read.</summary>
    public static string Seat(DockArrangement? seats) =>
        seats is null ? "none" : seats.Canonical ? "canonical" : "lopsided";

    /// <summary>A body form token: <c>ring</c>, <c>g</c>, <c>p</c>, <c>double-hole</c>, <c>two-u</c>, <c>bar</c>
    /// (solid), <c>single</c> / <c>twin</c> (one/two spine arms), <c>arms-N</c> otherwise, or <c>none</c>.</summary>
    public static string Form(CompoundRead? r) => r is null ? "none" : r.Form switch
    {
        Compound.Rectangle => "bar",
        Compound.SpineArms => r.Arms == 1 ? "single" : r.Arms == 2 ? "twin" : $"arms-{r.Arms}",
        Compound.Ring => "ring",
        Compound.DoubleHole => "double-hole",
        Compound.P => "p",
        Compound.G => "g",
        Compound.TwoUOnI => "two-u",
        _ => r.Form.ToString().ToLowerInvariant(),
    };
}
