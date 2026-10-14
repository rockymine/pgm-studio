using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Compose;

/// <summary>
/// The one profile-driven fill entry point over the box scaffold — the spine the partitioner drives.
/// Given a positioned <see cref="Box"/> (its footprint <see cref="Box.Rect"/> + its <see cref="Box.LandTargetCells"/>
/// land target) and the edge it docks its host through, it reads the box's <see cref="FillProfiles">profile</see>
/// to decide which families are legal, emits one into the footprint (the room taking the box kind's role), and
/// reports the <b>land</b> the fill produced against the target — the two-currency accounting (footprint is
/// fixed once, land is what fragment later spends). "No shape fits" is a <see cref="FillRejection"/>, never a
/// throw, so an over-constrained box feeds a directed box change up a level.
///
/// <para>Today it fills the <b>wool</b> box (over <see cref="WoolBoxEmitter.Fill"/>) — the kind where the
/// footprint gate decides whether a share reaches a donut / U / H. The spawn box already fills through the
/// shared emitter in its own growth frame (<see cref="SpawnBoxEmitter"/>); the hub and frontline kinds,
/// and multi-interface docking, are not dispatched here. Single-mouth docking (a top or bottom
/// edge); the box footprint is the input, not a by-product of a budget-share solve.</para>
/// </summary>
public static class BoxFiller
{
    /// <summary>Fill <paramref name="box"/> with a specific <paramref name="family"/>, docking
    /// <paramref name="mouth"/>: the compound, box-local, or <c>null</c> with <paramref name="rejection"/> set when
    /// the family is outside the box's profile, does not fit the footprint, or would dock illegally.</summary>
    public static Compound? Fill(
        Box box, BoxEdge mouth, int laneWidth, CompoundForm family, out FillRejection? rejection,
        bool flip = false, RoomPlacement roomPlacement = RoomPlacement.Inline, bool woolAtEnd = false,
        int attachmentWidth = 0, RingWalls? ringWalls = null)
    {
        var menu = FillProfiles.Families(box.Kind, laneWidth, laneWidth);
        if (!menu.Contains(family))
        {
            rejection = new FillRejection.NotOnMenu([.. menu.Select(f => f.ToString())]);
            return null;
        }
        if (box.Kind != BoxKind.Wool)
            throw new ComposeException(
                $"BoxFiller fills wool boxes; the {box.Kind} box docks through its own binding.");
        if (WoolBoxEmitter.Fill(box, mouth, family, laneWidth, out rejection, flip, roomPlacement, woolAtEnd,
                attachmentWidth, ringWalls) is not { } compound)
            return null;

        // the docking law: the host docks the mouth, so the mouth edge must be a legal dock. Read the box's edges
        // off the fill and gate them — an illegal dock (a sealed wool or a non-entry edge) is a directed
        // rejection, never a placement.
        if (DockingGate.CheckMouth(BoxInterfaces.Of(compound, box.Rect.Width, box.Rect.Height), mouth) is { } dock)
        {
            rejection = new FillRejection.IllegalDock(dock, mouth);
            return null;
        }
        return compound;
    }

    /// <summary>The <b>entry</b> interval a <paramref name="family"/> fill presents on its <paramref name="mouth"/>
    /// edge — box-local along-coords (x for a top/bottom mouth, z for a left/right one). This is the narrow dock
    /// an <b>overhanging</b> placement aligns to the host: the entry lands on a hub run while the wider compound
    /// overhangs the edge into free space. <c>null</c> when the family does not fill the box or exposes no entry
    /// on that mouth.</summary>
    public static EdgeInterval? EntryOn(
        Box box, BoxEdge mouth, int laneWidth, CompoundForm family, bool flip = false,
        RoomPlacement roomPlacement = RoomPlacement.Inline, bool woolAtEnd = false, int attachmentWidth = 0)
    {
        if (Fill(box, mouth, laneWidth, family, out _, flip, roomPlacement, woolAtEnd, attachmentWidth)
            is not { } compound)
            return null;
        var edge = BoxInterfaces.Of(compound, box.Rect.Width, box.Rect.Height).FirstOrDefault(e => e.Edge == mouth);
        return edge?.Intervals.FirstOrDefault(i => i.Part == CompoundParts.Entry);
    }

    /// <summary>The families the box's profile admits that <b>actually fill</b> its footprint (the emit
    /// succeeds) — the legal fill set for this box, footprint gate included.</summary>
    public static IReadOnlyList<CompoundForm> FittingFamilies(Box box, BoxEdge mouth, int laneWidth) =>
        FillProfiles.Families(box.Kind, laneWidth, laneWidth)
            .Where(f => Fill(box, mouth, laneWidth, f, out _) is not null)
            .ToList();

    /// <summary>The <b>land</b> (walkable ground) a compound spends, in cells — its room included.</summary>
    public static int Land(Compound compound) => compound.Pieces.Sum(p => p.Rect.Width * p.Rect.Height);
}
