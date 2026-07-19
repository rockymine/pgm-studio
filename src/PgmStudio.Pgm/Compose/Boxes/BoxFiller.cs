using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Compose;

/// <summary>
/// The one profile-driven fill entry point over the box scaffold — the spine the partitioner (G63) drives.
/// Given a positioned <see cref="Box"/> (its footprint <see cref="Box.Rect"/> + its <see cref="Box.LandTargetCells"/>
/// land target) and the edge it docks its host through, it reads the box's <see cref="FillProfiles">profile</see>
/// to decide which families are legal, emits one into the footprint (the room taking the box kind's role), and
/// reports the <b>land</b> the fill produced against the target — the two-currency accounting (footprint is
/// fixed once, land is what fragment later spends). "No shape fits" is a <see cref="FillResult"/> data channel,
/// never a throw, so an over-constrained box feeds a directed box change up a level.
///
/// <para>Today it fills the <b>wool</b> box (over <see cref="WoolBoxEmitter.Fill"/>) — the kind where the
/// footprint gate decides whether a share reaches a donut / U / H. The spawn box already fills through the
/// shared emitter in its own growth frame (<see cref="SpawnBoxEmitter"/>, G78); the hub and frontline kinds,
/// and multi-interface docking, join this dispatch at G41-C / G80. Single-mouth docking (a top or bottom
/// edge); the box footprint is the input, not a by-product of a budget-share solve.</para>
/// </summary>
public static class BoxFiller
{
    /// <summary>Fill <paramref name="box"/> with a specific <paramref name="family"/>, docking
    /// <paramref name="mouth"/>. Rejects (as a <see cref="FillResult"/>) when the family is outside the box's
    /// profile or its minimum box does not fit the footprint. On success carries the emission + vacancies.</summary>
    public static FillResult Fill(
        Box box, BoxEdge mouth, int corridorWidth, ShapeFamily family, bool flip = false, string? roomId = null,
        RoomPlacement roomPlacement = RoomPlacement.Inline, bool woolAtEnd = false)
    {
        var menu = FillProfiles.Families(box.Kind, corridorWidth);
        if (!menu.Contains(family)) return new FillResult.NoFamilyFits(menu);
        var result = box.Kind switch
        {
            BoxKind.Wool => WoolBoxEmitter.Fill(box, mouth, family, corridorWidth, flip, roomId, roomPlacement, woolAtEnd),
            _ => throw new ComposeException(
                $"BoxFiller fills wool boxes; the {box.Kind} box docks through its own binding " +
                "(spawn via SpawnBoxEmitter, G78; hub/frontline at G41-C)."),
        };
        if (result is not FillResult.Ok ok) return result;

        // the docking law (G80): the host docks the mouth, so the mouth edge must be a legal dock. Read the
        // box's edges off the placed fill (shape-relative) and gate them — an illegal dock (a sealed wool or a
        // non-entry edge) is a directed rejection, never a placement.
        var edges = BoxInterfaces.Of(BoxLocal(ok.Approach, box), box.Rect[2], box.Rect[3]);
        return DockingGate.CheckMouth(edges, mouth) is { } rejection
            ? new FillResult.IllegalDock(rejection, family, mouth)
            : ok;
    }

    /// <summary>Recover the box-local <see cref="EmittedShape"/> of a placed fill — the slot-typed terrain and
    /// room rects shifted back to the box origin — so <see cref="BoxInterfaces.Of"/> can read the edge facts the
    /// docking gate maps. (Only the geometry the gate reads is reconstructed; the marker offset and vacancies
    /// ride along unused.)</summary>
    private static EmittedShape BoxLocal(EmittedApproach a, Box box)
    {
        int ox = box.Rect[0], oz = box.Rect[1];
        int[] Local(int[] r) => [r[0] - ox, r[1] - oz, r[2], r[3]];
        var terrain = a.Terrain.Select(p => (Local(p.Rect), p.Slot!)).ToList();
        return new EmittedShape(terrain, Local(a.WoolRoom.Rect), a.At, a.Vacancies);
    }

    /// <summary>The <b>entry</b> interval a <paramref name="family"/> fill presents on its <paramref name="mouth"/>
    /// edge — box-local along-coords (x for a top/bottom mouth, z for a left/right one). This is the narrow dock
    /// an <b>overhanging</b> placement aligns to the host: the entry lands on a hub run while the wider body
    /// overhangs the edge into free space. <c>null</c> when the family does not fill the box or exposes no entry
    /// on that mouth.</summary>
    public static (int Start, int Len)? EntryOn(
        Box box, BoxEdge mouth, int corridorWidth, ShapeFamily family, bool flip = false,
        RoomPlacement roomPlacement = RoomPlacement.Inline, bool woolAtEnd = false)
    {
        if (Fill(box, mouth, corridorWidth, family, flip, "probe", roomPlacement, woolAtEnd) is not FillResult.Ok ok)
            return null;
        var edge = BoxInterfaces.Of(BoxLocal(ok.Approach, box), box.Rect[2], box.Rect[3])
            .FirstOrDefault(e => e.Edge == mouth);
        var entry = edge?.Intervals.FirstOrDefault(i => i.Slot == ApproachSlots.Entry);
        return entry is null ? null : (entry.Start, entry.LengthCells);
    }

    /// <summary>The families the box's profile admits that <b>actually fill</b> its footprint (the emit
    /// succeeds) — the legal fill set for this box, footprint gate included.</summary>
    public static IReadOnlyList<ShapeFamily> FittingFamilies(Box box, BoxEdge mouth, int corridorWidth) =>
        FillProfiles.Families(box.Kind, corridorWidth)
            .Where(f => Fill(box, mouth, corridorWidth, f) is FillResult.Ok)
            .ToList();

    /// <summary>Pick a legal family for the box by <paramref name="roll"/> (indexing the fitting set) and fill.
    /// The profile-driven selection: a roll over the families the kind admits and the footprint holds, falling
    /// back to the directed <see cref="FillResult.NoFamilyFits"/> signal when none fit.</summary>
    public static FillResult Fill(Box box, BoxEdge mouth, int corridorWidth, int roll, bool flip = false, string? roomId = null)
    {
        var fitting = FittingFamilies(box, mouth, corridorWidth);
        if (fitting.Count == 0) return new FillResult.NoFamilyFits(FillProfiles.Families(box.Kind, corridorWidth));
        return Fill(box, mouth, corridorWidth, fitting[((roll % fitting.Count) + fitting.Count) % fitting.Count], flip, roomId);
    }

    /// <summary>The <b>land</b> (walkable terrain) an emitted approach spends, in cells — the box's land
    /// currency (its footprint is fixed by the box; this is what fragment converts to build to hit the
    /// target).</summary>
    public static int Land(EmittedApproach a) =>
        a.Terrain.Sum(p => p.Rect[2] * p.Rect[3]) + a.WoolRoom.Rect[2] * a.WoolRoom.Rect[3];

    /// <summary>True when the fill's land is within the box's land target — the two-currency balance a fill
    /// under budget satisfies directly; over budget is what fragment spends down (G63).</summary>
    public static bool WithinLandTarget(Box box, EmittedApproach a) => Land(a) <= box.LandTargetCells;
}
