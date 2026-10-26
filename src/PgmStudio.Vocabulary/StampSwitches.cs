namespace PgmStudio.Vocabulary;

/// <summary>
/// The stamps a play piece can have switched off, each by the word a piece's <c>stampsOff</c> lists and the setting its
/// rail writes. Every stamp runs unless its piece names it here, which is what a world built elsewhere with its own
/// marker, outline or wall asks for.
/// </summary>
public static class StampSwitches
{
    /// <summary>The marker hanging over a wool room, a destroyable, a core or a control point.</summary>
    public const string Marker = "marker";

    /// <summary>The redstone outline round a build area.</summary>
    public const string Outline = "outline";

    /// <summary>An approach wall's own bedrock.</summary>
    public const string Wall = "wall";

    /// <summary>The chests of a wool room.</summary>
    public const string Loot = "loot";

    /// <summary>A destroyable's or a core's say in the pickaxe the kit carries.</summary>
    public const string KitPairing = "kitPairing";

    /// <summary>The check that a destroyable or a core floats its float over the ground and has ground at y 0.</summary>
    public const string FloatCheck = "floatCheck";

    /// <summary>Every switch, in the order a rail offers them.</summary>
    public static readonly string[] All = [Marker, Outline, Wall, Loot, KitPairing, FloatCheck];

    /// <summary>The stamps a piece of <paramref name="kind"/> (<see cref="PlayPieceKinds"/>) has, in the order a rail
    /// offers them; none for a kind that has no stamp to switch.</summary>
    public static string[] Of(string kind) => kind switch
    {
        PlayPieceKinds.WoolRoom => [Marker, Loot],
        PlayPieceKinds.Destroyable or PlayPieceKinds.Core => [Marker, KitPairing, FloatCheck],
        PlayPieceKinds.ControlPoint => [Marker],
        PlayPieceKinds.BuildArea => [Outline],
        PlayPieceKinds.Wall => [Wall],
        _ => [],
    };

    /// <summary>Whether the stamp <paramref name="word"/> runs for a piece switching off <paramref name="off"/>.</summary>
    public static bool On(IEnumerable<string>? off, string word) => off is null || !off.Contains(word);
}
