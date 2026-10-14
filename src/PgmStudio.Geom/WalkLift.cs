namespace PgmStudio.Geom;

/// <summary>
/// A height range in one cell that a player moves through vertically for nothing but the climb: a ladder or
/// a vine climbed, or a water column swum up and sunk down. <see cref="Low"/> and <see cref="High"/> are feet
/// heights, inclusive, in the same unit as <see cref="WalkPlace.Y"/>.
/// </summary>
public readonly record struct WalkLift(int Low, int High)
{
    /// <summary>A run of climbable blocks: the feet are in a ladder from its lowest block to its highest.</summary>
    public static WalkLift Climbed(int yBottom, int yTop) => new(yBottom, yTop);

    /// <summary>A run of water: the feet are in it from its lowest block, and stand on its surface one over
    /// its highest.</summary>
    public static WalkLift Swum(int yBottom, int yTop) => new(yBottom, yTop + 1);

    /// <summary>Whether a player standing at <paramref name="y"/> in this cell is on the lift: inside it, or
    /// one under its foot, which a jump reaches.</summary>
    public bool Holds(int y) => Low - Walk.FreeRise <= y && y <= High;
}
