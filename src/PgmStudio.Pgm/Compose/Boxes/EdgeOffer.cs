using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Compose;

/// <summary>How the intervals of one offer group may be consumed (docs/contracts/map-generation.md §1.14 — the
/// offer kind). <see cref="Several"/>: each interval takes its own consumer (a hub's four edges, the double
/// frontline's two tips — two derived runs). <see cref="Joint"/>: one consumer must span the whole group flush
/// (FR6's wide face across both tips — one wide run, the inter-tip recess preserved as a hole).</summary>
public enum OfferGrouping { Joint, Several }

/// <summary>
/// An <b>offer</b> (docs/contracts/map-generation.md §1.14) — the outward constraint a designation publishes:
/// <b>where</b> a neighbour may attach (the edge <see cref="Interval"/>, the G93 shape-relative fact, so it
/// moves with every knob), <b>at what width</b> (<see cref="WidthClass"/>, the w2/w4/w6 rung a consumer's fill
/// menu reads as its <c>cw</c>), and <b>in which grouping</b> (<see cref="Grouping"/> over the offers sharing a
/// <see cref="GroupId"/>). It is the forward twin of the derived <c>FrontlineRuns</c> / build-zone reads — the
/// designation drives, the deriver verifies.
///
/// <para>Produced by the <b>hub</b> designation (per-edge width offers — the constraint source; a consumed
/// width is the neighbour's menu <c>cw</c>) and, ahead, the <b>frontline</b> designation (the face offer the mid
/// consumes, with the inter-tip recess simply not offered). Carried on a <see cref="BoxJoint"/> as provenance so
/// the partitioner places consumers only on an offered interval; <c>BoxPartition.Of</c> mirrors it back.</para>
/// </summary>
/// <param name="Edge">The box edge the offer sits on.</param>
/// <param name="Interval">The stretch along the edge a neighbour may dock (the G93 <see cref="EdgeInterval"/>).</param>
/// <param name="WidthClass">The w2/w4/w6 rung the offer sources — the consumer reads it as its corridor width.</param>
/// <param name="Grouping">Whether the offer's <see cref="GroupId"/> group resolves jointly or severally.</param>
/// <param name="GroupId">Offers sharing this id resolve together under <see cref="Grouping"/>.</param>
public sealed record EdgeOffer(
    BoxEdge Edge, EdgeInterval Interval, int WidthClass, OfferGrouping Grouping, string GroupId);
