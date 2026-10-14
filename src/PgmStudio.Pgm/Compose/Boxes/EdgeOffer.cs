using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Compose;

/// <summary>
/// An <b>offer</b> (docs/generator/model.md §7) — the outward constraint a box publishes:
/// <b>where</b> a neighbour may attach (the edge <see cref="Interval"/>, the shape-relative fact, so it
/// moves with every knob) and <b>at what width</b> (<see cref="Width"/>, the lane width a run that long can
/// carry). It is the forward twin of the derived <c>FrontlineRuns</c> / build-zone reads — the
/// box drives, the deriver verifies.
///
/// <para>Produced by the <b>hub</b>, one per free run: its <see cref="Width"/> is a <em>capacity</em> derived from
/// the length of the run it sits on. What one neighbour was actually given at one dock is its joint's
/// <see cref="BoxJoint.Width"/>, a <em>selection</em> made per consumer kind; one run can carry two docks at two
/// widths, which is why a joint's width is not the offer travelling forward.</para>
/// </summary>
/// <param name="Interval">The stretch along a box edge a neighbour may dock.</param>
/// <param name="Width">The lane width, in cells, the run can carry.</param>
public sealed record EdgeOffer(EdgeInterval Interval, int Width)
{
    /// <summary>The box edge the offer sits on.</summary>
    public BoxEdge Edge => Interval.Edge;
}
