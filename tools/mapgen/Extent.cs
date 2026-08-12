namespace PgmStudio.MapGen;

/// <summary>A plan-coordinate rectangle — the extent of an island, or the ground a piece keeps scenery off.
/// It is not the sketch's own rect type: this is a bounding box the tool computes so a spec never has to
/// state one, and giving it its own name keeps the two from being mistaken for each other.</summary>
internal readonly record struct Extent(double MinX, double MinZ, double MaxX, double MaxZ);
