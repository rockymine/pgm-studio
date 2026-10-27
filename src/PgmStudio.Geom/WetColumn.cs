namespace PgmStudio.Geom;

/// <summary>One column the modifier stack's water stands in: the height its surface stands at, in the unit of a
/// column's top, so the water fills the blocks under it, and the id of the modifier whose water it is. Water
/// that touches settles to one surface, and a column whose surface was taken from a neighbour belongs to the
/// modifier it took it from.</summary>
public readonly record struct WetColumn(int Surface, string Body);
