namespace PgmStudio.Pgm.Shapes;

/// <summary>A stretch along one edge of a box: the <see cref="Edge"/> it lies on, its <see cref="Start"/> along
/// that edge in box-local cells (x for a top or bottom edge, z for a left or right one) and its
/// <see cref="Length"/> in cells. Where two boxes touch, where a hub offers a dock, where a negative space opens
/// and where a piece meets its box's edge are all one. <see cref="Part"/> is the part of the piece presenting
/// the stretch, where it was read off a compound.</summary>
public sealed record EdgeInterval(BoxEdge Edge, int Start, int Length, string? Part = null)
{
    /// <summary>The cell just past the stretch's far end.</summary>
    public int End => Start + Length;

    /// <summary>Whether the two stretches share at least one cell.</summary>
    public bool Overlaps(EdgeInterval other) => Start < other.End && other.Start < End;
}
