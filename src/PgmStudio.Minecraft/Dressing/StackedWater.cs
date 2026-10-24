using PgmStudio.Geom;
using PgmStudio.Minecraft.Painting;

namespace PgmStudio.Minecraft.Dressing;

/// <summary>
/// The water a sketch's modifier stack made, as the dressing pass lays it: every wet column with the height its
/// surface stands at and the body of water it belongs to (<see cref="WetColumn"/>), the bed each body is laid
/// over, and the floor water pours down to where it runs off the edge of the ground.
/// </summary>
/// <param name="Columns">Each wet cell. The surface is in the unit of a column's top, so the fill reaches the
/// block under it.</param>
/// <param name="Beds">The material under a body's water, by <see cref="WetColumn.Body"/>. A body not named here lies
/// on the fluid prop's default bank.</param>
/// <param name="FallFloor">The lowest y falling water reaches: the bottom of the ground the board built.</param>
public sealed record StackedWater(
    IReadOnlyDictionary<(int X, int Z), WetColumn> Columns,
    IReadOnlyDictionary<string, TerrainMaterial>? Beds = null,
    int FallFloor = 0);
