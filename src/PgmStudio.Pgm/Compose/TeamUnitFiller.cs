using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Compose;

/// <summary>What filling an allocated partition produces (G63-C.1): the <see cref="GrownUnit"/> the composer
/// assembles (pieces + spawn/wool placements) plus the frontline's <see cref="FrontlineFace"/> offers the mid
/// consumes (<c>mid = f(frontline)</c>). A drop-in for a <see cref="TeamUnitGrower"/> grow, with the frontline
/// offers additionally carried out for <see cref="MidCarver"/>.</summary>
public sealed record FilledUnit(GrownUnit Unit, IReadOnlyList<EdgeOffer> FrontlineFace);

/// <summary>
/// Fills an allocated team-unit partition <b>hub-first</b> (G63-C.1) — the fill half of the box-driven switch
/// (allocate-then-fill). The hub emits first as the <b>constraint source</b> (<see cref="HubBoxEmitter"/>), and
/// each neighbour box <b>consumes the hub's <see cref="EdgeOffer"/></b> on the edge it docks: the offered
/// <see cref="EdgeOffer.WidthClass"/> <em>is</em> the neighbour's corridor width. This replaces
/// <see cref="TeamUnitGrower"/>'s grow-then-derive authoring — the geometry is filled into footprints the
/// allocator positions (C.2), not grown — and it is what retires the grower (C.3).
///
/// <para>This first slice is the <b>offer-consumption seam</b>: a hub fill's per-edge offer driving a spawn or
/// wool neighbour's width. The full partition-topology orchestration — which neighbour docks which edge,
/// assembling the <see cref="GrownUnit"/> + placements, and the frontline's face dock — builds on it.</para>
/// </summary>
public static class TeamUnitFiller
{
    /// <summary>The corridor width a neighbour reads from the hub's offer on <paramref name="hubEdge"/> — the
    /// constraint-source contract: the hub sources the width, the neighbour builds to it. Throws
    /// <see cref="ComposeException"/> when the hub published no offer on that edge (the allocator only docks a
    /// neighbour where the hub offered).</summary>
    public static int ConsumedCw(EmittedHub hub, BoxEdge hubEdge) =>
        hub.Offers.FirstOrDefault(o => o.Edge == hubEdge)?.WidthClass
        ?? throw new ComposeException($"the hub publishes no offer on its {hubEdge} edge to consume.");

    /// <summary>Fill a <b>spawn</b> box docking the hub's <paramref name="hubEdge"/>, at the width that edge
    /// offers (the spawn's mouth is <paramref name="spawnMouth"/>, the edge facing the hub). <c>null</c> when the
    /// footprint is too small — a directed signal the allocator resizes against.</summary>
    public static EmittedSpawn? FillSpawn(
        EmittedHub hub, BoxEdge hubEdge, Box spawnBox, BoxEdge spawnMouth, ShapeFamily family, bool flip, string roomId) =>
        SpawnBoxEmitter.Fill(spawnBox, spawnMouth, family, ConsumedCw(hub, hubEdge), flip, roomId);

    /// <summary>Fill a <b>wool</b> box docking the hub's <paramref name="hubEdge"/>, at the width that edge
    /// offers — routed through the profile-gated <see cref="BoxFiller"/> so the dock is gated legal and the
    /// family is checked against the wool width menu (the offered width chooses the menu row).</summary>
    public static FillResult FillWool(
        EmittedHub hub, BoxEdge hubEdge, Box woolBox, BoxEdge woolMouth, ShapeFamily family, bool flip, string roomId) =>
        BoxFiller.Fill(woolBox, woolMouth, ConsumedCw(hub, hubEdge), family, flip, roomId);

    /// <summary>The hub's wall/corridor width for the branch and holed forms (the solid Rectangle ignores it).</summary>
    private const int HubCw = 2;

    /// <summary>
    /// Fill an allocated <paramref name="partition"/> into a team unit, hub-first (the allocate↔fill contract).
    /// The <b>allocator (C.2) provides</b>: the positioned boxes (plan-cell <see cref="Box.Rect"/>s — one hub,
    /// one spawn, 1–3 wools, 0–1 frontline), the joints between them with the hub's <b>per-edge width plan carried
    /// as the joints' <see cref="BoxJoint.Offer"/>s</b>, and the spawn's <paramref name="spawnFacing"/> (the one
    /// board-frame value — every other output is plan-cell or piece-relative). This <b>filler</b> derives the
    /// hub's edge widths from the joint offers, samples a fitting hub form and emits the hub first (its offers
    /// realized at those widths), then for each hub joint fills the neighbour box — the spawn/wool consuming the
    /// offered width as its <c>cw</c>, docking the edge facing the hub — and assembles the pieces + placements.
    /// <c>null</c> on any directed fill failure (a form or family that does not fit), which the composer
    /// resamples. The frontline (a join, not a placement — its face offer feeds the mid) joins next.
    /// </summary>
    public static FilledUnit? Fill(BoxPartition partition, string spawnFacing, ComposeRng rng)
    {
        var hubBox = partition.Boxes.FirstOrDefault(b => b.Kind == BoxKind.Hub);
        if (hubBox is null) return null;
        var hubJoints = partition.JointsOf(hubBox.Id);

        // the hub emits first: its per-edge width plan is the offers the allocator put on its joints
        var edgeWidths = new Dictionary<BoxEdge, int>();
        foreach (var j in hubJoints)
            if (j.Offer is { } offer) edgeWidths[HubEdge(j, hubBox.Id)] = offer.WidthClass;

        var fitting = FillProfiles.HubForms.Where(f => HubBoxEmitter.Fill(hubBox, f, HubCw, edgeWidths) is not null).ToList();
        if (fitting.Count == 0) return null;
        var hub = HubBoxEmitter.Fill(hubBox, fitting[rng.NextInt(0, fitting.Count)], HubCw, edgeWidths)!;

        var pieces = new List<GrownPiece>(hub.Pieces);
        GrownSpawn? spawn = null;
        var wools = new List<GrownWool>();

        foreach (var j in hubJoints)
        {
            var neighbour = partition.ById(Other(j, hubBox.Id));
            if (neighbour is null) continue;
            var hubEdge = HubEdge(j, hubBox.Id);
            var mouth = Opposite(hubEdge);
            var roomId = $"{neighbour.Id}-room";

            switch (neighbour.Kind)
            {
                case BoxKind.Spawn:
                    var fitS = SpawnBoxEmitter.Families
                        .Where(f => FillSpawn(hub, hubEdge, neighbour, mouth, f, flip: false, roomId) is not null).ToList();
                    if (fitS.Count == 0) return null;
                    var es = FillSpawn(hub, hubEdge, neighbour, mouth, fitS[rng.NextInt(0, fitS.Count)], flip: false, roomId)!;
                    pieces.AddRange(es.Pieces);
                    spawn = new GrownSpawn(roomId, es.MarkerAt, spawnFacing);
                    break;

                case BoxKind.Wool:
                    var fitW = BoxFiller.FittingFamilies(neighbour, mouth, ConsumedCw(hub, hubEdge));
                    if (fitW.Count == 0) return null;
                    if (FillWool(hub, hubEdge, neighbour, mouth, fitW[rng.NextInt(0, fitW.Count)], flip: false, roomId)
                            is not FillResult.Ok ok)
                        return null;
                    pieces.AddRange(ok.Approach.Terrain);
                    pieces.Add(ok.Approach.WoolRoom);
                    wools.Add(new GrownWool(roomId, ok.Approach.At));
                    break;
            }
        }

        if (spawn is null) return null;   // a team unit is anchored by its spawn
        return new FilledUnit(new GrownUnit(pieces, spawn, wools), []);
    }

    /// <summary>The box edge opposite <paramref name="e"/> — a neighbour docks the hub with the edge facing it.</summary>
    private static BoxEdge Opposite(BoxEdge e) => e switch
    {
        BoxEdge.Top => BoxEdge.Bottom,
        BoxEdge.Bottom => BoxEdge.Top,
        BoxEdge.Left => BoxEdge.Right,
        _ => BoxEdge.Left,
    };

    /// <summary>The hub's edge a <paramref name="j"/>oint touches — the interface edge when the hub is
    /// <see cref="BoxJoint.BoxA"/>, else its opposite (the interface is read on <c>BoxA</c>'s frame).</summary>
    private static BoxEdge HubEdge(BoxJoint j, string hubId) =>
        j.BoxA == hubId ? j.Interface.Edge : Opposite(j.Interface.Edge);

    private static string Other(BoxJoint j, string id) => j.BoxA == id ? j.BoxB : j.BoxA;
}
