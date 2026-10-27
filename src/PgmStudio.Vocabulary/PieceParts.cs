namespace PgmStudio.Vocabulary;

/// <summary>
/// The part of a play piece a move or a turn names: a room's region, by the region's own word, or the building on
/// it. The piece route reads it, and the canvas writes it from the shape a drag began on.
/// </summary>
public static class PieceParts
{
    /// <summary>Every part: the room's two region words and its building.</summary>
    public static readonly string[] All = [.. StructuralRoles.All];
}
