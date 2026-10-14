namespace PgmStudio.Pgm.Shapes;

/// <summary>
/// The compound forms the hub and front line take (docs/generator/model.md §4) — the named recombinations
/// of the rectangle atom, read by topology alone (the §4 feature axes: bends · fold · branch · void), never by a
/// room. It is the derive-side identity of a <see cref="Compound"/>: <see cref="ShapeClassifier.ClassifyCompound"/>
/// reads a compound back to one of these, closing the emit↔derive mirror for the compounds <see cref="CompoundEmitter"/>
/// builds. Distinct from <see cref="ShapeFamily"/>, the forms a wool or spawn compound takes, which end in a room.
///
/// <list type="bullet">
/// <item><see cref="Rectangle"/> — one rectangle (a spine / the solid hub): no feature.</item>
/// <item><see cref="SpineArms"/> — a spine plus <c>K</c> perpendicular arms (the branch family), K capped at 3:
/// K=1 is L/T, K=2 is Π/F, K=3 is E. The arm count is the identity; where the arms sit is a placement knob.</item>
/// <item><see cref="Ring"/> — four bars around one enclosed void (the donut compound).</item>
/// <item><see cref="DoubleHole"/> — a ring with a U docked on its edge, its bay the second void; the two holes may
/// be the <em>same size</em> (a full-height U) or variant (a shorter, slid U), kept apart by a <em>solid</em> ring
/// wall (contrast <see cref="TwoUOnI"/>).</item>
/// <item><see cref="P"/> — a ring whose bottom bar runs longer than the loop, so the loop slides along it; one
/// void with an overhang (the P/b/d glyph).</item>
/// <item><see cref="G"/> — a ring with an L docked on its edge: one enclosed void (the ring) plus an <em>open</em>
/// bay (the L's recess, three-walled) that a docking frontline seals into a taller hole — the asymmetric,
/// open-bay cousin of <see cref="DoubleHole"/> (the G glyph).</item>
/// <item><see cref="TwoUOnI"/> — two loops sharing one baseline: two voids kept apart by an <em>open</em> channel.</item>
/// </list>
/// </summary>
public enum CompoundForm { Rectangle, SpineArms, Ring, DoubleHole, P, G, TwoUOnI }

/// <summary>A compound's derive-side read: the <see cref="CompoundForm"/> it classifies to and, for
/// <see cref="CompoundForm.SpineArms"/>, the arm count <see cref="Arms"/> (0 for every other form). The mirror closes
/// when this equals what <see cref="CompoundEmitter"/> was asked to build.</summary>
public sealed record CompoundRead(CompoundForm Form, int Arms = 0);
