namespace PgmStudio.Vocabulary;

/// <summary>
/// The four ways a room's wall faces, as a door names its wall: the wall on the low-z side faces <c>-z</c>. The
/// intent stores a door's edge in these words, the door route takes them, and the canvas writes them.
/// </summary>
public static class EdgeWords
{
    /// <summary>The wall on the low-z side.</summary>
    public const string NegZ = "-z";

    /// <summary>The wall on the high-z side.</summary>
    public const string PosZ = "+z";

    /// <summary>The wall on the low-x side.</summary>
    public const string NegX = "-x";

    /// <summary>The wall on the high-x side.</summary>
    public const string PosX = "+x";

    /// <summary>Every edge, in the reading order an author meets them.</summary>
    public static readonly string[] All = [NegZ, PosZ, NegX, PosX];
}
