namespace PgmStudio.Vocabulary;

/// <summary>
/// The part of a play piece a move or a turn names: a room's region, by the region's own word, or the
/// building on it; or one end of an approach wall. The piece route reads it, and the canvas writes it from the
/// shape or the grip a drag began on.
/// </summary>
public static class PieceParts
{
    /// <summary>A wall's first end, <c>(x1, z1)</c>.</summary>
    public const string Start = "start";

    /// <summary>A wall's second end, <c>(x2, z2)</c>.</summary>
    public const string End = "end";

    /// <summary>Every part: the room's two region words and its building, then a wall's two ends.</summary>
    public static readonly string[] All = [.. StructuralRoles.All, Start, End];
}
