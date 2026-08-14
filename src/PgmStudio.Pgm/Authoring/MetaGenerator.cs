using PgmStudio.Domain;

namespace PgmStudio.Pgm.Authoring;

using Dict = Dictionary<string, object?>;

/// <summary>
/// Map-identity slice of the declarative generator (new-map-authoring.md): sets the map name and the
/// auto-derived fields — version, gamemode, and the objective text. Version is fixed; gamemode and the
/// objective both follow which objective modules the intent actually carries (wools, destroyables, cores),
/// the same rule <see cref="Gamemodes"/> applies to a parsed map (destroyables-and-cores.md OB7) — a
/// destroy or core board is never labelled a capture map just because the studio's boilerplate used to say
/// so.
/// <para>Authors/contributors are <b>not</b> set here — they're Minecraft usernames that need async
/// resolution to uuids (<c>MojangClient</c>), which the intent endpoint does before saving. Proto
/// (1.5.0) is an XML-export concern, not a persisted column.</para>
/// </summary>
public static class MetaGenerator
{
    public const string Version = "1.0.0";
    public const string Proto = "1.5.0";   // emitted at XML export, not stored on the map

    public static void Apply(Dict doc, MapIntent intent)
    {
        if (intent.Meta is not { } m) return;
        if (!string.IsNullOrWhiteSpace(m.Name)) doc["name"] = m.Name.Trim();
        doc["version"] = Version;
        doc["gamemode"] = DeclaredGamemode(intent).ToList<object?>();
        doc["objective"] = Objective(intent);
    }

    // <gamemode> does not decide which objective modules run (OB7) — but "not authoritative" is not "free
    // text": PGM still parses it as one repeated element, each resolved against its own closed enum, and
    // refuses the whole map when one does not match. One element per module the intent actually carries,
    // never several ids joined into one element PGM cannot parse; a map with none of the three (no meta-only
    // board should reach export, but nothing here assumes it can't) gets no elements at all — matching the
    // 68 of 150 corpus maps that declare no <gamemode> rather than a wrong one.
    private static List<string> DeclaredGamemode(MapIntent intent) => [.. Gamemodes.From(
        intent.Wools is { Count: > 0 },
        intent.Destroyables is { Count: > 0 },
        intent.Cores is { Count: > 0 })];

    // Corpus objectives are short imperative lines, one clause per objective kind the board actually
    // carries, joined rather than assumed — "Capture the wool!" and "Destroy the enemy's monument!" are
    // both real corpus phrasings, and so is naming both on a mixed board ("Capture the enemy team's wool
    // and destroy their monument!"). A board with none of the three (no meta-only board should reach
    // export) gets the empty string rather than a claim about an objective it does not have.
    private static string Objective(MapIntent intent)
    {
        var wools = intent.Wools?.Count ?? 0;
        var destroyables = intent.Destroyables?.Count ?? 0;
        var cores = intent.Cores?.Count ?? 0;

        var clauses = new List<string>();
        if (wools == 1) clauses.Add("capture the wool");
        else if (wools > 1) clauses.Add("capture the enemies' wools");

        if (destroyables == 1) clauses.Add("destroy the enemy's monument");
        else if (destroyables > 1) clauses.Add("destroy the enemy's monuments");

        if (cores == 1) clauses.Add("leak the enemy's core");
        else if (cores > 1) clauses.Add("leak the enemy's cores");

        if (clauses.Count == 0) return "";

        var joined = clauses.Count == 1
            ? clauses[0]
            : string.Join(", ", clauses.Take(clauses.Count - 1)) + " and " + clauses[^1];
        return char.ToUpperInvariant(joined[0]) + joined[1..] + "!";
    }
}
