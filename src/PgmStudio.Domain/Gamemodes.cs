namespace PgmStudio.Domain;

/// <summary>
/// A map's gamemodes, derived from which objective modules it carries. This is <b>the</b> gamemode — PGM
/// never reads the <c>&lt;gamemode&gt;</c> element to decide which modules run; each module contributes a
/// tag when it parses, and the mode falls out of which ones did.
/// <para>"Not authoritative for deciding modules" must never be read as "unvalidated free text": PGM still
/// parses <c>&lt;gamemode&gt;</c> as a <b>repeated</b> element holding one id each, resolves every one
/// against its own closed enum (<c>Gamemode.byId</c>, matched case-insensitively, no splitting), and
/// <c>MapInfoImpl.parseGamemodes</c> throws <c>InvalidXMLException("Unknown gamemode")</c> — failing the
/// whole map to load — the moment one does not match. <see cref="IsKnownId"/> holds that closed set for the
/// studio's own check on the way out.</para>
/// <para>The canonical rule lives here and nowhere else: it has two callers reaching it from different
/// directions — <see cref="MapXml.Gamemodes"/> from a parsed map, and the persistence tier from its
/// objective rows — and a second copy would be free to drift.</para>
/// </summary>
public static class Gamemodes
{
    public const string Ctw = "ctw";
    public const string Dtm = "dtm";
    public const string Dtc = "dtc";

    /// <summary>Every id PGM's own <c>Gamemode</c> enum recognizes (<c>Gamemode.java</c>), matched
    /// case-insensitively the same way <c>Gamemode.byId</c> does. A <c>&lt;gamemode&gt;</c> element outside
    /// this set is not a stricter or looser label — it is a map <c>MapInfoImpl.parseGamemodes</c> refuses to
    /// load.</summary>
    private static readonly HashSet<string> KnownIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "arcade", "ad", "bedwars", "blitz", "br", "bridge", "ctf", "ctw", "cp", "tdm", "dtc", "dtm",
        "5cp", "ffb", "ffa", "infection", "kotf", "koth", "mixed", "payload", "rfw", "rage", "scorebox",
        "skywars", "sg",
    };

    public static bool IsKnownId(string id) => KnownIds.Contains(id);

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
