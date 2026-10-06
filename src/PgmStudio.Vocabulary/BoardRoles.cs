namespace PgmStudio.Vocabulary;

/// <summary>
/// The roles a board picture's pieces are told apart by. The renderer writes each as a <c>role-*</c> class on the
/// piece it draws, and the Generator page spells the same words to tint one role on request, so the two read one
/// list. Only <see cref="Spawn"/> and <see cref="Wool"/> are drawn in an ink of their own; the rest are ground.
/// </summary>
public static class BoardRoles
{
    public const string Hub = "hub";
    public const string Frontline = "frontline";
    public const string Approach = "approach";
    public const string Spawn = "spawn";
    public const string Wool = "wool";
    public const string Other = "other";

    /// <summary>A build zone, drawn as a dashed outline rather than a piece.</summary>
    public const string Zone = "zone";
}
