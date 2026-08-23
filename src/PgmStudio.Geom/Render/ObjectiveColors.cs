namespace PgmStudio.Geom.Render;

/// <summary>
/// One colour per kind of place a match is played between, so a spawn is the same green in every picture that
/// draws one and a reader carries a single key between renders rather than relearning it per image.
///
/// <para>A destroyable and a core are drawn apart. They are both a destroy goal and they are not the same
/// place — one is a monument the attacker breaks and the other a container the attacker leaks — and a picture
/// that gives them one colour cannot answer which of the two a board is short of.</para>
/// </summary>
public static class ObjectiveColors
{
    public const int Spawn = 0x34D399;
    public const int Wool = 0xFBBF24;
    public const int Destroyable = 0xFB923C;
    public const int Core = 0xF472B6;

    /// <summary>Where the two teams meet — the derived origin a coverage read walks from, which is a place on
    /// the board rather than something the document declares.</summary>
    public const int Crossing = 0x60A5FA;

    /// <summary>Anything named that is none of the above.</summary>
    public const int Other = 0xFFFFFF;

    /// <summary>The colour for a <c>NavPoint.Kind</c>.</summary>
    public static int Of(string kind) => kind switch
    {
        "spawn" => Spawn,
        "wool" => Wool,
        "destroyable" => Destroyable,
        "core" => Core,
        "crossing" => Crossing,
        _ => Other,
    };

    /// <summary>The same colour as a CSS hex string, for a raster that paints from a colour table.</summary>
    public static string Css(string kind) => $"#{Of(kind):x6}";
}
