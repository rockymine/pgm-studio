namespace PgmStudio.MapGen;

/// <summary>A plan-coordinate rectangle — the bounding box of a shape or an island, computed so the relief
/// scatter's random draw has bounds to draw within without a spec ever having to state one. Not the sketch's
/// own rect type: giving this its own name keeps the two from being mistaken for each other.</summary>
internal readonly record struct Extent(double MinX, double MinZ, double MaxX, double MaxZ);
