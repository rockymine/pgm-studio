namespace PgmStudio.Vocabulary;

/// <summary>
/// The shape a place is drawn as: a point, a box between two corners, a ring of three points or more, or a line
/// of two or more. The places document states one, the client draws by it and the studio reads its points by it.
/// </summary>
public static class PlaceShapes
{
    /// <summary>One point.</summary>
    public const string Point = "point";

    /// <summary>A box, between two corners.</summary>
    public const string Box = "box";

    /// <summary>A closed ring of three points or more.</summary>
    public const string Ring = "ring";

    /// <summary>An open line of two points or more.</summary>
    public const string Line = "line";

    /// <summary>Every shape.</summary>
    public static readonly string[] All = [Point, Box, Ring, Line];

    /// <summary>The fewest points <paramref name="shape"/> takes, and the most; null where it is no shape.</summary>
    public static (int Least, int Most)? Points(string? shape) => shape switch
    {
        Point => (1, 1),
        Box => (2, 2),
        Ring => (3, int.MaxValue),
        Line => (2, int.MaxValue),
        _ => null,
    };
}
