namespace PgmStudio.Vocabulary;

/// <summary>What a control point's pad is laid as, seen from above. Both are what the corpus builds: a filled square
/// and a disc, each centred on the point so PGM's progress pie sweeps about the pad's middle.</summary>
public static class ControlPointShapes
{
    /// <summary>A square pad, its side the point's size.</summary>
    public const string Square = "square";

    /// <summary>A round pad, its diameter the point's size, captured inside a cylinder over it.</summary>
    public const string Disc = "disc";

    /// <summary>The two, in the order a picker offers them.</summary>
    public static readonly string[] All = [Square, Disc];
}
