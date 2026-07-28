using PgmStudio.Geom;
using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Compose;

/// <summary>The rect and edge arithmetic the seat step reads: building a neighbour's rect from a seat,
/// projecting seated boxes onto an edge, clearance tests, and the hub joint each dock records.</summary>
public static class SeatGeometry
{
    /// <summary>The plan-cell rect of a <paramref name="depth"/>×<paramref name="along"/> box seated at box-local
    /// along-coord <paramref name="seat"/> on the hub's <paramref name="edge"/>: its depth reaches outward from
    /// that edge, its along-extent runs along it. Frame-free — the (u, v) frame chose the edge; the box then
    /// follows the edge's outward normal (Top −z, Bottom +z, Left −x, Right +x), so the seating needs no per-mode
    /// branch and stays correct where a (u, v)→box-local run mapping would reverse.</summary>
    internal static CellRect NeighbourRect(BoxEdge edge, int seat, int depth, int along, CellRect hub)
    {
        int hx = hub.X, hz = hub.Z, hw = hub.Width, hh = hub.Height;
        return edge switch
        {
            BoxEdge.Top => new(hx + seat, hz - depth, along, depth),
            BoxEdge.Bottom => new(hx + seat, hz + hh, along, depth),
            BoxEdge.Left => new(hx - depth, hz + seat, depth, along),
            _ => new(hx + hw, hz + seat, depth, along),                  // Right
        };
    }

    /// <summary>Project a <paramref name="seated"/> box onto <paramref name="edge"/> as the forbidden along-interval
    /// (box-local) a candidate of outward <paramref name="depth"/> must keep the seat gap clear of — but only when
    /// the box lies within <paramref name="gap"/> of that edge on the <b>perpendicular</b> axis (else it is too far
    /// out to constrain this edge and returns <c>null</c>). The along-gap itself is applied by <see cref="UnitSeating.SeatInRuns"/>'s
    /// inflation, so this returns the box's raw along-extent. A same-edge neighbour projects to its own dock interval
    /// (perpendicular distance 0); an adjacent-edge neighbour projects only when it hugs the shared corner — so the
    /// one mechanism covers both the same-edge abut and the cross-edge corner meeting exactly (the along + perp
    /// conditions reproduce <see cref="TooClose"/>), and a legal seat is sampled directly, never single-sample
    /// rejected.</summary>
    internal static (int Start, int Len)? ProjectOntoEdge(BoxEdge edge, CellRect hub, int depth, CellRect seated, int separationCells)
    {
        int hx = hub.X, hz = hub.Z, hw = hub.Width, hh = hub.Height;
        int bx0 = seated.X, bz0 = seated.Z, bx1 = seated.X + seated.Width, bz1 = seated.Z + seated.Height;
        var (perpNear, aStart, aEnd) = edge switch
        {
            BoxEdge.Top => (bz0 - separationCells < hz && hz - depth < bz1 + separationCells, bx0 - hx, bx1 - hx),
            BoxEdge.Bottom => (bz0 - separationCells < hz + hh + depth && hz + hh < bz1 + separationCells, bx0 - hx, bx1 - hx),
            BoxEdge.Left => (bx0 - separationCells < hx && hx - depth < bx1 + separationCells, bz0 - hz, bz1 - hz),
            _ => (bx0 - separationCells < hx + hw + depth && hx + hw < bx1 + separationCells, bz0 - hz, bz1 - hz),   // Right
        };
        return perpNear ? (aStart, aEnd - aStart) : null;
    }

    /// <summary>Two plan-cell rects lie within <paramref name="separationCells"/> cells of each other by rectilinear
    /// nearest-approach — touching (a separation of 0) and corner-touching included. Equivalent to inflating one
    /// rect by the separation on all four sides and testing overlap, so a diagonal corner meeting is caught, not
    /// only a shared edge. The seat-step separation law reads it: no two neighbour bodies may sit this close
    /// (<paramref name="separationCells"/> is the map's lane width — w2 = 10 blocks, w3 = 15 on wide
    /// boards).</summary>
    internal static bool TooClose(CellRect first, CellRect second, int separationCells) =>
        first.X - separationCells < second.X + second.Width && second.X < first.X + first.Width + separationCells &&
        first.Z - separationCells < second.Z + second.Height && second.Z < first.Z + first.Height + separationCells;

    /// <summary>Two plan-cell rects overlap iff they intersect on both axes (abutment is not overlap).</summary>
    internal static bool Overlap(CellRect first, CellRect second) =>
        first.X < second.X + second.Width && second.X < first.X + first.Width && first.Z < second.Z + second.Height && second.Z < first.Z + first.Height;

    /// <summary>The box edge opposite <paramref name="e"/> — a neighbour's mouth faces the hub across it.</summary>
    internal static BoxEdge Opposite(BoxEdge edge) => edge switch
    {
        BoxEdge.Top => BoxEdge.Bottom, BoxEdge.Bottom => BoxEdge.Top,
        BoxEdge.Left => BoxEdge.Right, _ => BoxEdge.Left,
    };

    /// <summary>The hub↔neighbour joint over a ready-made <paramref name="abutment"/> (the abutment of an overhanging
    /// dock), granting the consumer <paramref name="w"/> as its corridor width across it. The width is the
    /// consumer's selection, not the hub's published capacity — see <see cref="BoxJoint.Grant"/>.</summary>
    internal static BoxJoint HubJointFrom(string hubId, string nbId, BoxAbutment abutment, int grantedWidthCells)
    {
        var grant = new EdgeOffer(abutment.Edge, new EdgeInterval(abutment.Start, abutment.WidthCells, ApproachSlots.Bar),
            grantedWidthCells, OfferGrouping.Several, $"hub-{abutment.Edge}");
        return new BoxJoint(hubId, nbId, abutment, grant);
    }

    /// <summary>The hub↔neighbour joint on <paramref name="edge"/>: the interface interval where they touch, and
    /// the <see cref="BoxJoint.Grant"/> across it — width <paramref name="grantedWidthCells"/>, which the neighbour reads as its
    /// <c>cw</c> (severally — each neighbour its own dock). <paramref name="grantedWidthCells"/> is chosen by the consumer's kind
    /// upstream (the w2 wool lane, or the map lane width); the hub's own per-run capacity is a separate figure and
    /// is not what is recorded here.</summary>
    internal static BoxJoint HubJoint(string hubId, string nbId, BoxEdge edge, int alongStart, int along, int grantedWidthCells) =>
        HubJointFrom(hubId, nbId, new BoxAbutment(edge, alongStart, along), grantedWidthCells);

    /// <summary>The hub's box edge facing <paramref name="side"/> — the (u, v) outward direction mapped through
    /// the <see cref="Frame"/> to a box-local edge (min-z Top, max-z Bottom, min-x Left, max-x Right).</summary>
    internal static BoxEdge SideEdge(Frame frame, UnitSide side)
    {
        var (du, dv) = side switch
        {
            UnitSide.Front => (-1.0, 0.0),
            UnitSide.Back => (1.0, 0.0),
            UnitSide.Left => (0.0, -1.0),
            _ => (0.0, 1.0),                                          // Right
        };
        var (ox, oz) = frame.ToPoint(0, 0);
        var (px, pz) = frame.ToPoint(du, dv);
        double dx = px - ox, dz = pz - oz;
        if (dz < 0) return BoxEdge.Top;
        if (dz > 0) return BoxEdge.Bottom;
        return dx < 0 ? BoxEdge.Left : BoxEdge.Right;
    }
}
