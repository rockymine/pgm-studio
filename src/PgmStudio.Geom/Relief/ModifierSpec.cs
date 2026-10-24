namespace PgmStudio.Geom.Relief;

/// <summary>Which terraform operation a <see cref="ModifierSpec"/> runs.</summary>
public enum ModifierOp
{
    /// <summary>Ground raised inside the mask, highest at its middle and falling to nothing at its rim.</summary>
    Mound,

    /// <summary>A bowl sunk to a floor, flat in its middle, its wall rounded into the ground by a melt.</summary>
    Crater,

    /// <summary>The ground eased to one height inside the mask and back out over a skirt.</summary>
    Level,

    /// <summary>Noise in one or more sizes, added inside the mask.</summary>
    Noise,
}

/// <summary>Which way a modifier's change may move the ground.</summary>
public enum ModifierMode
{
    /// <summary>Either way: whatever the operation computes is applied.</summary>
    Unrestricted,

    /// <summary>The ground only rises.</summary>
    Lift,

    /// <summary>The ground only falls.</summary>
    Cut,
}

/// <summary>One size of a Roughness: the width of its largest feature, how many finer octaves ride on it, and
/// how far it moves the ground either way.</summary>
public readonly record struct NoiseSize(double Cell, int Octaves, double Rise);

/// <summary>
/// One terraform operation over a field of ground heights: which operation, how it may move the ground, where it
/// works, and the numbers it reads. A number left null takes <see cref="ModifierDefaults"/>.
/// </summary>
public sealed record ModifierSpec
{
    public ModifierOp Op { get; init; } = ModifierOp.Mound;
    public ModifierMode Mode { get; init; } = ModifierMode.Unrestricted;

    /// <summary>Where it works, every area already standing where it applies.</summary>
    public IReadOnlyList<MaskArea> Areas { get; init; } = [];

    /// <summary>A Mound's height at its middle, in blocks.</summary>
    public double? Rise { get; init; }

    /// <summary>A Mound's shoulder: above one it holds its height further out, below one it peaks.</summary>
    public double? Power { get; init; }

    /// <summary>The share of the mask, in from its rim, over which the change fades in.</summary>
    public double? Soft { get; init; }

    /// <summary>The exponent areas of one Mound or Crater are combined by; zero keeps the greater change.</summary>
    public double? Join { get; init; }

    /// <summary>A Crater's floor, as a height.</summary>
    public double? Floor { get; init; }

    /// <summary>How many blocks out from its middle a Crater's floor stays flat.</summary>
    public double? Flat { get; init; }

    /// <summary>How many blocks a Crater's wall rises for each block out from its flat floor.</summary>
    public double? Slope { get; init; }

    /// <summary>Over how many blocks a Crater's rim is rounded into the ground.</summary>
    public double? Melt { get; init; }

    /// <summary>A Level's height; absent, the median of the ground inside its inner area.</summary>
    public double? Top { get; init; }

    /// <summary>Where a Level stops being held flat, in <see cref="MaskArea.E"/>.</summary>
    public double? Inner { get; init; }

    /// <summary>Where a Level's skirt has eased all the way back to the ground, in <see cref="MaskArea.E"/>.</summary>
    public double? Outer { get; init; }

    /// <summary>A Noise's sizes, each its own fractal added to the others.</summary>
    public IReadOnlyList<NoiseSize>? Sizes { get; init; }

    /// <summary>The noise row it reads; a size takes the seed plus thirty-seven times its place.</summary>
    public int Seed { get; init; } = 1;
}

/// <summary>What a modifier reads where its document states nothing — the one place the numbers are set.</summary>
public static class ModifierDefaults
{
    public const double MoundRise = 8;
    public const double MoundPower = 1.6;
    public const double CraterFloor = 0;
    public const double CraterFlat = 0;
    public const double CraterSlope = 1;
    public const double CraterMelt = 0;
    public const double Join = 0;
    public const double LevelInner = 1;
    public const double LevelOuter = 1.5;

    /// <summary>The one size a Noise with none stated is made of.</summary>
    public static readonly NoiseSize Size = new(12, 2, 1);
}
