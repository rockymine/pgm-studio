namespace PgmStudio.Pgm.Shapes;

/// <summary>
/// A <b>terminal-free</b> rectilinear compound: the structural-slotted <see cref="Pieces"/> of a shape and the
/// negative space they leave (<see cref="Vacancies"/>). It carries <b>no terminal, marker, or id</b> — those are
/// stamped by a <em>designation</em> (the approach's wool/spawn room, the hub's per-edge interfaces, the
/// frontline's face). The body is the shared layer beneath every box kind: one Staple body serves both a U and a
/// Y, a solid rectangle serves a straight lane and a hub. It is what <see cref="ShapeEmitter.Body"/> produces and
/// each designation finishes. See <c>docs/contracts/shape-vocabulary.md</c> §8/§9.
/// </summary>
public sealed record ShapeBody(
    IReadOnlyList<(int[] Rect, string Slot)> Pieces, IReadOnlyList<ShapeVacancy> Vacancies);
