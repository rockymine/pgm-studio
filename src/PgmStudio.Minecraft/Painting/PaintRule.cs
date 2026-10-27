using PgmStudio.Geom.Relief;

namespace PgmStudio.Minecraft.Painting;

/// <summary>
/// One of a board's paint rules (<c>docs/world-export/terrain-painting.md</c>, TP28): a material laid over a
/// column's top courses where the ground is what the rule asks for. A column takes the first rule it meets, read
/// in order, and every condition a rule states must hold; a rule stating none paints everywhere. A column no rule
/// meets keeps its theme.
/// </summary>
/// <param name="Material">What the rule lays.</param>
/// <param name="Depth">How many top courses it lays, at least one.</param>
/// <param name="SlopeAbove">The ground must be steeper than this, in degrees.</param>
/// <param name="SlopeUpTo">The ground must be at most this steep, in degrees.</param>
/// <param name="HeightFrom">The column's top course must stand at this y or above.</param>
/// <param name="HeightTo">The column's top course must stand at this y or below.</param>
/// <param name="Noise">Where noise must read above a share, so a rule lays patches rather than a field.</param>
/// <param name="Cells">The only cells it may lay on, or null for any: the ground near a modifier.</param>
public sealed record PaintRule(
    TerrainMaterial Material, int Depth = 1, int? SlopeAbove = null, int? SlopeUpTo = null, int? HeightFrom = null,
    int? HeightTo = null, PaintNoise? Noise = null, IReadOnlySet<(int X, int Z)>? Cells = null)
{
    /// <summary>Whether the rule lays on a column whose top course is <c>top − 1</c>, read at
    /// <paramref name="sample"/> for its noise so a mirrored board's halves meet the same rules.</summary>
    public bool Meets((int X, int Z) cell, in ColumnProfile column, (int X, int Z) sample)
    {
        var top = column.SurfaceTop - 1;
        if (SlopeAbove is { } above && column.Slope <= above) return false;
        if (SlopeUpTo is { } upTo && column.Slope > upTo) return false;
        if (HeightFrom is { } from && top < from) return false;
        if (HeightTo is { } to && top > to) return false;
        if (Cells is not null && !Cells.Contains(cell)) return false;
        if (Noise is { } noise
            && 0.5 + 0.5 * ValueNoise.Fractal(sample.X + 0.5, sample.Z + 0.5, noise.Cell, 2, noise.Seed) <= noise.Above)
            return false;
        return true;
    }

    /// <summary>The first of <paramref name="rules"/> a column meets, or null.</summary>
    public static PaintRule? First(IReadOnlyList<PaintRule>? rules, (int X, int Z) cell, in ColumnProfile column,
                                   (int X, int Z) sample)
    {
        if (rules is not { Count: > 0 } || column.Covered || column.Structure) return null;
        foreach (var rule in rules)
            if (rule.Meets(cell, column, sample)) return rule;
        return null;
    }
}

/// <summary>Where a <see cref="PaintRule"/> lays by noise: value noise <paramref name="Cell"/> blocks across, read
/// 0 to 1, laying where it reads above <paramref name="Above"/>.</summary>
/// <param name="Cell">The width of a patch, in blocks.</param>
/// <param name="Above">The share the noise must read above: 0.5 lays about half the ground.</param>
/// <param name="Seed">The noise row it reads.</param>
public readonly record struct PaintNoise(double Cell, double Above, int Seed = 1);
