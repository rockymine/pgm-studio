namespace PgmStudio.Vocabulary;

/// <summary>
/// The terraform operations a sketch's <c>modifiers[]</c> may run, each one change to the ground already
/// built, inside its mask, in list order (<c>docs/tools/sketch.md</c>, <i>Terraform</i>). The word is what the
/// document stores; the screen calls each by the name beside it.
/// </summary>
public static class ModifierOps
{
    /// <summary>A Hill: the ground raised inside the mask, highest where the mask is deepest and falling to
    /// nothing at its rim.</summary>
    public const string Mound = "mound";

    /// <summary>A Hollow: a bowl sunk to a floor, flat in its middle and rounded into the ground over its
    /// melt.</summary>
    public const string Crater = "crater";

    /// <summary>A Level: the ground eased to one height inside the mask and back out over its skirt.</summary>
    public const string Level = "level";

    /// <summary>Roughness: noise in one or more sizes, added inside the mask.</summary>
    public const string Noise = "noise";

    public static readonly string[] All = [Mound, Crater, Level, Noise];

    /// <summary>What the screen calls an operation.</summary>
    public static string Label(string? op) => op switch
    {
        Mound => "Hill",
        Crater => "Hollow",
        Level => "Level",
        Noise => "Roughness",
        _ => op ?? "",
    };
}

/// <summary>Which half of a modifier's change is kept. Absent keeps both.</summary>
public static class ModifierModes
{
    /// <summary>Only what raises the ground is kept.</summary>
    public const string Lift = "lift";

    /// <summary>Only what lowers the ground is kept: a Hill with a negative rise is then a dish of its own shape.</summary>
    public const string Cut = "cut";

    public static readonly string[] All = [Lift, Cut];
}

/// <summary>The outlines a modifier's mask is drawn as.</summary>
public static class MaskKinds
{
    /// <summary>A centre, two radii and a turn.</summary>
    public const string Ellipse = "ellipse";

    /// <summary>A closed outline of points, its depth measured from its rim inward.</summary>
    public const string Polygon = "polygon";

    /// <summary>All the ground there is, at full strength everywhere.</summary>
    public const string Board = "board";

    public static readonly string[] All = [Ellipse, Polygon, Board];
}
