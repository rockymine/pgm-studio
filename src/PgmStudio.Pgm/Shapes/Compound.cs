using PgmStudio.Geom;

namespace PgmStudio.Pgm.Shapes;

/// <summary>A rectangle of cells and the part it plays in its compound (<see cref="CompoundParts"/>).</summary>
public sealed record Piece(CellRect Rect, string Part);

/// <summary>
/// A shape combined out of rectangle <see cref="Pieces"/>, and the bays, holes and notches they leave
/// (<see cref="Vacancies"/>). A compound that ends in a room carries it as one of its pieces, the one whose part is
/// <see cref="CompoundParts.Room"/>, and the <see cref="Marker"/> where its spawn or wool stands. Its
/// <see cref="CompoundForm"/> is what the pieces make, and one compound serves several box kinds: a solid rectangle
/// is both a straight lane and a hub. See <c>docs/generator/model.md</c> §4.
/// </summary>
/// <param name="Pieces">The pieces, each with its part; a room comes last.</param>
/// <param name="Vacancies">The empty space the pieces leave inside their box.</param>
/// <param name="Marker">Where the spawn or wool stands, in box-local cells inside the room piece; null for a
/// compound with no room.</param>
public sealed record Compound(
    IReadOnlyList<Piece> Pieces, IReadOnlyList<ShapeVacancy> Vacancies, (double X, double Z)? Marker = null)
{
    /// <summary>The piece the compound ends in, or null for a compound with no room.</summary>
    public Piece? Room => Pieces.FirstOrDefault(piece => piece.Part == CompoundParts.Room);

    /// <summary>The same compound without its room and marker: the ground that leads to the room.</summary>
    public Compound WithoutRoom() =>
        new([.. Pieces.Where(piece => piece.Part != CompoundParts.Room)], Vacancies);

    /// <summary>The compound laid back onto its <paramref name="width"/> × <paramref name="height"/> box by
    /// <paramref name="turn"/>: every piece, vacancy and vacancy mouth, and the marker, with it.</summary>
    public Compound Turned(BoxTurn turn, int width, int height) => turn == BoxTurn.None ? this : new(
        [.. Pieces.Select(piece => piece with { Rect = turn.Rect(piece.Rect, width, height) })],
        [.. Vacancies.Select(vacancy => vacancy with
        {
            Rect = turn.Rect(vacancy.Rect, width, height),
            Mouth = vacancy.Mouth?.Turned(turn),
        })],
        Marker is { } marker ? turn.Point(marker.X, marker.Z, width, height) : null);
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
