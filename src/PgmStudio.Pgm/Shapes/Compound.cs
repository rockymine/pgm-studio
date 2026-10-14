using PgmStudio.Geom;

namespace PgmStudio.Pgm.Shapes;

/// <summary>
/// A shape combined out of rectangle pieces: the slotted <see cref="Pieces"/> and the bays, holes and notches they
/// leave (<see cref="Vacancies"/>). It carries <b>no room, marker or id</b> — a <em>designation</em> adds those (an
/// <see cref="ApproachShape"/>'s wool or spawn room, the hub's per-edge interfaces, the frontline's face). Its
/// <see cref="CompoundForm"/> is what the pieces make, and one compound serves several uses: a staple is both a U and
/// a Y, a solid rectangle both a straight lane and a hub. See <c>docs/generator/model.md</c> §4.
/// </summary>
public sealed record Compound(
    IReadOnlyList<(CellRect Rect, string Slot)> Pieces, IReadOnlyList<ShapeVacancy> Vacancies)
{
    /// <summary>The compound laid back onto its <paramref name="width"/> × <paramref name="height"/> box by
    /// <paramref name="turn"/>: every piece and vacancy rect, and every vacancy mouth, with it.</summary>
    public Compound Turned(BoxTurn turn, int width, int height) => turn == BoxTurn.None ? this : new(
        [.. Pieces.Select(piece => (turn.Rect(piece.Rect, width, height), piece.Slot))],
        [.. Vacancies.Select(vacancy => vacancy with
        {
            Rect = turn.Rect(vacancy.Rect, width, height),
            Mouth = vacancy.Mouth?.Turned(turn),
        })]);
}

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
