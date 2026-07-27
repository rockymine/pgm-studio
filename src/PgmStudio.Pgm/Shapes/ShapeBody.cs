namespace PgmStudio.Pgm.Shapes;

/// <summary>
/// A <b>terminal-free</b> rectilinear compound: the structural-slotted <see cref="Pieces"/> of a shape and the
/// negative space they leave (<see cref="Vacancies"/>). It carries <b>no terminal, marker, or id</b> — those are
/// stamped by a <em>designation</em> (the approach's wool/spawn room, the hub's per-edge interfaces, the
/// frontline's face). The body is the shared layer beneath every box kind: one Staple body serves both a U and a
/// Y, a solid rectangle serves a straight lane and a hub. It is what <see cref="ShapeEmitter.Body"/> produces and
/// each designation finishes. See <c>docs/generator/model.md</c> §5.3/§5.4.
/// </summary>
public sealed record ShapeBody(
    IReadOnlyList<(int[] Rect, string Slot)> Pieces, IReadOnlyList<ShapeVacancy> Vacancies);

/// <summary>
/// The per-side widths of a ring's four walls, in cells — the widening knob every ring-bodied form shares
/// (the Ring, the P's loop, the G's ring, the Double-hole's, and the donut's).
///
/// <para>A ring built at one corridor width has four equal walls; a ring built at four is the same shape with
/// one side wider than the rest, which is how an author says more play flows through that side. Every emitter
/// takes a single <c>cw</c> today, so <see cref="Uniform"/> is what they all pass and the widened cases are the
/// reason this type exists at all.</para>
/// </summary>
public readonly record struct RingWalls(int Top, int Right, int Bottom, int Left)
{
    /// <summary>All four walls at one corridor width — the single-<c>cw</c> ring every form builds today.</summary>
    public static RingWalls Uniform(int cw) => new(cw, cw, cw, cw);

    /// <summary>The narrowest wall — what a corridor-minimum check reads.</summary>
    public int Min => Math.Min(Math.Min(Top, Right), Math.Min(Bottom, Left));

    /// <summary>The widest wall — the numerator of the spread ratio.</summary>
    public int Max => Math.Max(Math.Max(Top, Right), Math.Max(Bottom, Left));
}
