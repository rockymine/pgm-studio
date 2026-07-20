namespace PgmStudio.Pgm.Shapes;

/// <summary>
/// The <b>terminal-free compound</b> taxonomy (docs/contracts/shape-vocabulary.md §5) — the named recombinations
/// of the rectangle atom, read by topology alone (the §4 feature axes: bends · fold · branch · void), never by a
/// terminal. It is the derive-side identity of a <see cref="ShapeBody"/>: <see cref="ShapeClassifier.ClassifyBody"/>
/// reads a body back to one of these, closing the emit↔derive mirror for the bodies <see cref="BodyEmitter"/>
/// builds. Distinct from <see cref="ShapeFamily"/> — that is the terminal-<em>capped</em> approach taxonomy; a
/// compound plus a designation (a terminal) becomes an approach.
///
/// <list type="bullet">
/// <item><see cref="Rectangle"/> — one rectangle (a spine / the solid hub): no feature.</item>
/// <item><see cref="SpineArms"/> — a spine plus <c>K</c> perpendicular arms (the branch family), K capped at 3:
/// K=1 is L/T, K=2 is Π/F, K=3 is E. The arm count is the identity; where the arms sit is a placement knob.</item>
/// <item><see cref="Ring"/> — four bars around one enclosed void (the donut body).</item>
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
public enum Compound { Rectangle, SpineArms, Ring, DoubleHole, P, G, TwoUOnI }

/// <summary>A body's derive-side read: the <see cref="Compound"/> it classifies to and, for
/// <see cref="Compound.SpineArms"/>, the arm count <see cref="Arms"/> (0 for every other form). The mirror closes
/// when this equals what <see cref="BodyEmitter"/> was asked to build.</summary>
public sealed record CompoundRead(Compound Form, int Arms = 0);
