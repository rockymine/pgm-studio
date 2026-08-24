namespace PgmStudio.Geom;

/// <summary>
/// One solid run of a column, and the layer that drew it: <c>[YFloor, YTop)</c> at <c>(X, Z)</c>, half-open
/// in Y so a segment's thickness is <c>YTop - YFloor</c>.
///
/// <para>A cell holds one segment per layer it appears on, which is what makes a stacked board readable at
/// all: a gallery under a deck is two segments at one <c>(X, Z)</c>, and every read that projects them to a
/// single cell is answering about one storey while claiming to answer about the board. <see cref="Layer"/> is
/// what says which storey a segment belongs to — it is never empty, because the reader that produces these
/// names a layer that did not name itself.</para>
/// </summary>
/// <param name="Layer">The id of the layer this run was drawn on.</param>
public readonly record struct ColumnSegment(int X, int Z, int YFloor, int YTop, string Layer)
{
    /// <summary>The cell this segment stands in, for a caller that wants the footprint and not the run.</summary>
    public (int X, int Z) Cell => (X, Z);
}
