namespace PgmStudio.Vocabulary;

/// <summary>
/// How a sketch shape's top is decided once its group carries a relief. Absent, a shape is ordinary ground and
/// the relief decides it; each of these makes it stand out of the field instead. The gate, the rasterizer and
/// the canvas all spell them, so they are one set.
/// </summary>
public static class HeightModes
{
    /// <summary>A flat top at an absolute height: a mesa, whose faces are cliffs.</summary>
    public const string Level = "level";

    /// <summary>A fixed amount above the median of the ground it covers: a monolith or a plinth, one flat-topped
    /// thing that keeps its prominence wherever it is dragged.</summary>
    public const string Raise = "raise";

    /// <summary>The same amount below that median: a quarry, a sunken arena.</summary>
    public const string Sink = "sink";

    /// <summary>A fixed amount above the ground at <em>each</em> cell: a field wall, a hedge, a kerb, laid over
    /// the hillside and climbing with it.</summary>
    public const string Drape = "drape";

    /// <summary>The four, in the order a picker offers them.</summary>
    public static readonly string[] All = [Level, Raise, Sink, Drape];
}
