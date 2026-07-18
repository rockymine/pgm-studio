using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Compose;

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
}
