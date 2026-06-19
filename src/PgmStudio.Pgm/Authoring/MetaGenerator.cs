namespace PgmStudio.Pgm.Authoring;

using Dict = Dictionary<string, object?>;

/// <summary>
/// Map-identity slice of the declarative generator (new-map-authoring.md): sets the map name and the
/// auto-derived fields — version, gamemode, and the objective text. Version/gamemode are fixed for new
/// CTW maps; the objective is generated from the wool count.
/// <para>Authors/contributors are <b>not</b> set here — they're Minecraft usernames that need async
/// resolution to uuids (<c>MojangClient</c>), which the intent endpoint does before saving. Proto
/// (1.5.0) is an XML-export concern, not a persisted column.</para>
/// </summary>
public static class MetaGenerator
{
    public const string Version = "1.0.0";
    public const string Gamemode = "ctw";
    public const string Proto = "1.5.0";   // emitted at XML export, not stored on the map

    public static void Apply(Dict doc, MapIntent intent)
    {
        if (intent.Meta is not { } m) return;
        if (!string.IsNullOrWhiteSpace(m.Name)) doc["name"] = m.Name.Trim();
        doc["version"] = Version;
        doc["gamemode"] = Gamemode;
        doc["objective"] = Objective(intent);
    }

    // Corpus objectives are short "Capture…" lines; phrasing tracks wool count more than team count.
    private static string Objective(MapIntent intent) =>
        (intent.Wools?.Count ?? 0) == 1 ? "Capture the wool!" : "Capture the enemies' wools!";
}
