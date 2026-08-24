namespace PgmStudio.Geom;

/// <summary>
/// Somewhere a player stands: a cell, and the height they stand at in it.
///
/// <para>A cell is a column of the board seen from above; a place is a cell <em>and a storey of it</em>. A
/// column with two standable surfaces — a gallery under a deck, a hall under a terrace — holds two places,
/// and they are different nodes: a route reaching one has not reached the other, the ramp between them is an
/// edge, and a sealed mine is a place nothing connects to. Keyed on a cell alone none of those can be said,
/// because the two storeys are the same node.</para>
///
/// <para><see cref="Y"/> is where the player's feet are — the first air over the surface — not the surface
/// itself, so a rise from one place to another is the difference between them.</para>
/// </summary>
public readonly record struct WalkPlace(int X, int Z, int Y)
{
    /// <summary>The cell this place is a storey of, for a read that projects the board to one picture.</summary>
    public (int X, int Z) Cell => (X, Z);

    public override string ToString() => $"({X}, {Z}) @{Y}";
}
