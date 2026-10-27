using PgmStudio.Geom.Relief;

namespace PgmStudio.Geom.Tests.Relief;

/// <summary>A rectangular field of columns for the terraform operations to run over, each standing at its cell's
/// centre, with a water array the operations that make water write into.</summary>
internal sealed class ReliefField
{
    public int Width { get; }
    public int Depth { get; }
    public double[] Heights { get; }
    public double[] Xs { get; }
    public double[] Zs { get; }
    public double[] Original { get; }
    public double[] Waters { get; }

    public ReliefField(int width, int depth, Func<int, int, double> ground)
    {
        (Width, Depth) = (width, depth);
        Heights = new double[width * depth];
        Xs = new double[width * depth];
        Zs = new double[width * depth];
        for (var z = 0; z < depth; z++)
            for (var x = 0; x < width; x++)
            {
                Heights[z * width + x] = ground(x, z);
                Xs[z * width + x] = x + 0.5;
                Zs[z * width + x] = z + 0.5;
            }
        Original = (double[])Heights.Clone();
        Waters = new double[width * depth];
        Array.Fill(Waters, double.NaN);
    }

    public static ReliefField Flat(int width, int depth, double height) => new(width, depth, (_, _) => height);

    public double At(int x, int z) => Heights[z * Width + x];
    public double Was(int x, int z) => Original[z * Width + x];
    public double WaterAt(int x, int z) => Waters[z * Width + x];
    public void Run(ModifierSpec spec) => Modifiers.Apply(spec, Heights, Xs, Zs, Waters);

    /// <summary>The indices of the columns whose centre lies at least <paramref name="from"/> out in the area's measure.</summary>
    public IEnumerable<int> Outside(MaskArea area, double from) =>
        Enumerable.Range(0, Heights.Length).Where(index => area.E(Xs[index], Zs[index]) >= from);

    /// <summary>The indices of the columns whose centre lies within <paramref name="within"/> of the area's middle.</summary>
    public IEnumerable<int> Inside(MaskArea area, double within) =>
        Enumerable.Range(0, Heights.Length).Where(index => area.E(Xs[index], Zs[index]) <= within);
}
