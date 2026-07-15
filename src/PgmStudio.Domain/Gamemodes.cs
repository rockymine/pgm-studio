namespace PgmStudio.Domain;

/// <summary>
/// A map's gamemodes, derived from which objective modules it carries. This is <b>the</b> gamemode — PGM
/// never reads the <c>&lt;gamemode&gt;</c> element to decide it; each module contributes a tag when it
/// parses, and the mode falls out of which ones did. That element is a free-text label most maps omit.
/// <para>The canonical rule lives here and nowhere else: it has two callers reaching it from different
/// directions — <see cref="MapXml.Gamemodes"/> from a parsed map, and the persistence tier from its
/// objective rows — and a second copy would be free to drift.</para>
/// </summary>
public static class Gamemodes
{
    public const string Ctw = "ctw";
    public const string Dtm = "dtm";
    public const string Dtc = "dtc";

    /// <summary>
    /// The gamemodes implied by the objective modules present, in a stable order. It is a <b>set</b>, not a
    /// scalar: CTW, DTM and DTC coexist in real maps, so nothing may assume there is exactly one — an empty
    /// result is also legitimate and means the map carries no objective module we read.
    /// <para><paramref name="hasRealDestroyable"/> is the one deliberate deviation from PGM: it tags a map
    /// DTM the moment <c>DestroyableModule</c> parses anything, but a map whose every destroyable is a
    /// phantom is not DTM whatever PGM's tag says — those are pure CTW maps that happen to script their
    /// build floor with the destroyable element. Cores need no such carve-out: they have no <c>show</c>
    /// attribute, so every core is an objective.</para>
    /// </summary>
    public static IReadOnlyList<string> From(bool hasWools, bool hasRealDestroyable, bool hasCores)
    {
        var modes = new List<string>(3);
        if (hasWools) modes.Add(Ctw);
        if (hasRealDestroyable) modes.Add(Dtm);
        if (hasCores) modes.Add(Dtc);
        return modes;
    }
}
