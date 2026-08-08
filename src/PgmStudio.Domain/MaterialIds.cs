namespace PgmStudio.Domain;

/// <summary>
/// PGM material-match strings → numeric (1.8–1.12) block ids.
///
/// <para>A match is <c>;</c>-separated patterns, each <c>name[:data]</c>. Only the name selects a block id
/// here — the data nibble is a colour or a variant, and a rule that erases "stained glass" erases it whatever
/// the dye. Names are matched case- and separator-insensitively (<c>stained glass</c>, <c>STAINED_GLASS</c>
/// and <c>stained-glass</c> are one name), which is how PGM's own material lookup behaves.
/// </para>
///
/// <para>This is a <b>partial</b> table and says so: it covers the vocabulary the corpus actually uses in the
/// rules that read it, plus the neighbours an author would reach for next. An unrecognised name resolves to
/// nothing rather than to a guess, and every caller is written so that resolving to nothing is the safe
/// direction — a block that cannot be named is left exactly as the world has it. Add a row when a real map
/// needs one; do not transcribe the block table speculatively.</para>
/// </summary>
public static class MaterialIds
{
    private static readonly Dictionary<string, int> ById = new(StringComparer.OrdinalIgnoreCase)
    {
        ["air"] = 0,
        ["stone"] = 1,
        ["grass"] = 2,
        ["dirt"] = 3,
        ["cobblestone"] = 4,
        ["wood"] = 5,                  // planks
        ["sapling"] = 6,
        ["bedrock"] = 7,
        ["water"] = 8,
        ["stationary water"] = 9,
        ["lava"] = 10,
        ["stationary lava"] = 11,
        ["sand"] = 12,
        ["gravel"] = 13,
        ["log"] = 17,
        ["leaves"] = 18,
        ["glass"] = 20,
        ["sandstone"] = 24,
        ["web"] = 30,
        ["cobweb"] = 30,
        ["long grass"] = 31,
        ["wool"] = 35,
        ["piston moving piece"] = 36,
        ["gold block"] = 41,
        ["iron block"] = 42,
        ["obsidian"] = 49,
        ["chest"] = 54,
        ["redstone wire"] = 55,
        ["diamond block"] = 57,
        ["fence"] = 85,
        ["stained glass"] = 95,
        ["iron fence"] = 101,          // the 1.8 name for iron bars
        ["thin glass"] = 102,
        ["end stone"] = 121,
        ["ender stone"] = 121,
        ["emerald block"] = 133,
        ["spruce wood stairs"] = 134,
        ["quartz block"] = 155,
        ["leaves 2"] = 161,
        ["log 2"] = 162,
        ["stained clay"] = 159,
        ["hard clay"] = 172,
    };

    /// <summary>The block ids a PGM material match selects. Empty when the match names nothing this table
    /// knows — which every caller must treat as "cannot say", never as "matches nothing".</summary>
    public static IReadOnlySet<int> Resolve(string materials)
    {
        var ids = new HashSet<int>();
        foreach (var pattern in materials.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var name = Normalize(pattern.Split(':')[0]);
            if (name.Length > 0 && ById.TryGetValue(name, out var id)) ids.Add(id);
        }
        return ids;
    }

    /// <summary>Whether every pattern in the match resolves — so a caller can tell "no ids" apart from "some
    /// ids and one name I could not read".</summary>
    public static bool ResolvesFully(string materials)
    {
        var patterns = materials.Split(';', StringSplitOptions.RemoveEmptyEntries);
        return patterns.Length > 0
            && patterns.All(pattern => ById.ContainsKey(Normalize(pattern.Split(':')[0])));
    }

    private static string Normalize(string name) =>
        string.Join(' ', name.Replace('_', ' ').Replace('-', ' ')
                             .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}
