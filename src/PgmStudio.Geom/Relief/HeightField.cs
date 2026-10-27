namespace PgmStudio.Geom.Relief;

/// <summary>A height field over a footprint: the continuous surface and the block surface it snaps to. Contours
/// are traced along the continuous one, and the world is built from the blocks.</summary>
public sealed class HeightField(Footprint footprint, double[] continuous, int[] blocks)
{
    public Footprint Footprint { get; } = footprint;
    public double[] Continuous { get; } = continuous;
    public int[] Blocks { get; } = blocks;

    public bool Has(int x, int z) => Footprint.Inside(x, z);
    public int At(int x, int z) => Has(x, z) ? Blocks[Footprint.Index(x, z)] : int.MinValue;

    public int Min => Footprint.Land().Select(cell => At(cell.X, cell.Z)).DefaultIfEmpty(0).Min();
    public int Max => Footprint.Land().Select(cell => At(cell.X, cell.Z)).DefaultIfEmpty(0).Max();
}
