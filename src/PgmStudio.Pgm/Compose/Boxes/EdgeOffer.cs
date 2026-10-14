using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Compose;

/// <summary>How the intervals of one offer group may be consumed (docs/generator/model.md §7 — the
/// offer kind). <see cref="Several"/>: each interval takes its own consumer (a hub's four edges, the double
/// frontline's two tips — two derived runs). <see cref="Joint"/>: one consumer must span the whole group flush
/// (FR6's wide face across both tips — one wide run, the inter-tip recess preserved as a hole).</summary>
public enum OfferGrouping { Joint, Several }

/// <summary>
/// An <b>offer</b> (docs/generator/model.md §7) — the outward constraint a box publishes:
/// <b>where</b> a neighbour may attach (the edge <see cref="Interval"/>, the shape-relative fact, so it
/// moves with every knob), <b>at what width</b> (<see cref="Width"/>, the lane width a run that long can
/// carry), and <b>in which grouping</b> (<see cref="Grouping"/> over the offers sharing a
/// <see cref="GroupId"/>). It is the forward twin of the derived <c>FrontlineRuns</c> / build-zone reads — the
/// box drives, the deriver verifies.
///
/// <para>Produced by the <b>hub</b>, one per free run: its <see cref="Width"/> is a <em>capacity</em> derived from
/// the length of the run it sits on. What one neighbour was actually given at one dock is its joint's
/// <see cref="BoxJoint.Width"/>, a <em>selection</em> made per consumer kind; one run can carry two docks at two
/// widths, which is why a joint's width is not the offer travelling forward.</para>
/// </summary>
/// <param name="Interval">The stretch along a box edge a neighbour may dock.</param>
/// <param name="Width">The lane width, in cells, the run can carry.</param>
/// <param name="Grouping">Whether the offer's <see cref="GroupId"/> group resolves jointly or severally.</param>
/// <param name="GroupId">Offers sharing this id resolve together under <see cref="Grouping"/>.</param>
public sealed record EdgeOffer(EdgeInterval Interval, int Width, OfferGrouping Grouping, string GroupId)
{
    /// <summary>The box edge the offer sits on.</summary>
    public BoxEdge Edge => Interval.Edge;
}
