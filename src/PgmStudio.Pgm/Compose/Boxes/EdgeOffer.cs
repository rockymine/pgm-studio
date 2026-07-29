using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Compose;

/// <summary>How the intervals of one offer group may be consumed (docs/generator/model.md §7 — the
/// offer kind). <see cref="Several"/>: each interval takes its own consumer (a hub's four edges, the double
/// frontline's two tips — two derived runs). <see cref="Joint"/>: one consumer must span the whole group flush
/// (FR6's wide face across both tips — one wide run, the inter-tip recess preserved as a hole).</summary>
public enum OfferGrouping { Joint, Several }

/// <summary>
/// An <b>offer</b> (docs/generator/model.md §7) — the outward constraint a designation publishes:
/// <b>where</b> a neighbour may attach (the edge <see cref="Interval"/>, the G93 shape-relative fact, so it
/// moves with every knob), <b>at what width</b> (<see cref="WidthClass"/>, the w2/w4/w6 rung a consumer's fill
/// menu reads as its <c>cw</c>), and <b>in which grouping</b> (<see cref="Grouping"/> over the offers sharing a
/// <see cref="GroupId"/>). It is the forward twin of the derived <c>FrontlineRuns</c> / build-zone reads — the
/// designation drives, the deriver verifies.
///
/// <para>Produced by the <b>hub</b> designation (per-edge width offers — the constraint source; a consumed
/// width is the neighbour's menu <c>cw</c>) and, ahead, the <b>frontline</b> designation (the face offer the mid
/// consumes, with the inter-tip recess simply not offered).</para>
///
/// <para>This record serves <b>two roles</b>, and they carry different quantities — do not read one as a copy of
/// the other. As an <b>offer</b> it is published by a designation before any consumer exists, and its
/// <see cref="WidthClass"/> is a <em>capacity</em> derived from the length of the run it sits on. As a
/// <see cref="BoxJoint.Grant"/> it records what one consumer was actually given at one dock, and its
/// <see cref="WidthClass"/> is a <em>selection</em> made per consumer kind. One run can carry two docks at two
/// widths, which is exactly why a grant is not the offer travelling forward.</para>
/// </summary>
/// <param name="Edge">The box edge the offer sits on.</param>
/// <param name="Interval">The stretch along the edge a neighbour may dock (the G93 <see cref="EdgeInterval"/>).</param>
/// <param name="WidthClass">The w2/w4/w6 rung the offer sources — the consumer reads it as its corridor width.</param>
/// <param name="Grouping">Whether the offer's <see cref="GroupId"/> group resolves jointly or severally.</param>
/// <param name="GroupId">Offers sharing this id resolve together under <see cref="Grouping"/>.</param>
public sealed record EdgeOffer(
    BoxEdge Edge, EdgeInterval Interval, int WidthClass, OfferGrouping Grouping, string GroupId);
