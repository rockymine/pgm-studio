namespace PgmStudio.Domain;

/// <summary>
/// The minimum pickaxe tier vanilla Minecraft requires to <b>drop</b> a PGM material match as an item —
/// covering the closed vocabulary a destroyable or core actually names (docs/pgm/destroyables-and-cores.md
/// DT1: obsidian, emerald, gold, ender stone dominate). This is not the tier needed to <i>break</i> the block:
/// vanilla breaks any block with any tool (or bare hands) given enough time — what a tool tier below this one
/// changes is only whether the block drops, not whether it comes apart. A miss means "this table cannot say",
/// never "no tool needed" — the same partial-table convention <see cref="MaterialIds"/> follows, and for the
/// same reason: a caller that wants to know whether a goal drops loot at all must treat an unresolved material
/// as unconfirmed, not as safe.
/// </summary>
public static class MiningTiers
{
    /// <summary>Pickaxe tiers, weakest first — index doubles as rank, so comparing two tiers is comparing
    /// their position here.</summary>
    public static readonly string[] Order = ["wood", "stone", "iron", "diamond"];

    private static readonly Dictionary<string, string> RequiredTier = new(StringComparer.OrdinalIgnoreCase)
    {
        ["obsidian"] = "diamond",
        ["gold block"] = "iron",
        ["gold ore"] = "iron",
        ["emerald block"] = "iron",
        ["emerald ore"] = "iron",
        ["diamond block"] = "iron",
        ["diamond ore"] = "iron",
        ["redstone block"] = "wood",
        ["redstone ore"] = "wood",
        ["iron block"] = "stone",
        ["iron ore"] = "stone",
        ["end stone"] = "wood",
        ["ender stone"] = "wood",
    };

    /// <summary>Where a tier sits in <see cref="Order"/>, or -1 for a word this table does not know.</summary>
    public static int Rank(string tier) => Array.IndexOf(Order, Normalize(tier));

    /// <summary>The stronger of two tiers.</summary>
    public static string Higher(string a, string b) => Rank(a) >= Rank(b) ? a : b;

    /// <summary>The pickaxe tier a PGM material match (<c>;</c>-separated, optionally <c>:data</c>-suffixed —
    /// the same grammar <see cref="MaterialIds"/> reads) needs, taking the hardest of every pattern named.
    /// Null when every pattern is either unknown to this table or names a block no pickaxe tier gates.</summary>
    public static string? Required(string materials)
    {
        string? hardest = null;
        foreach (var pattern in materials.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var name = Normalize(pattern.Split(':')[0]);
            if (!RequiredTier.TryGetValue(name, out var tier)) continue;
            hardest = hardest is null ? tier : Higher(hardest, tier);
        }
        return hardest;
    }

    /// <summary>The tier a pickaxe item material (e.g. <c>"diamond pickaxe"</c>) grants, by its leading word —
    /// null for an item that is not a pickaxe at all.</summary>
    public static string? TierOfPickaxe(string itemMaterial)
    {
        var words = Normalize(itemMaterial).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length != 2 || words[1] != "pickaxe") return null;
        return Rank(words[0]) >= 0 ? words[0] : null;
    }

    private static string Normalize(string name) =>
        string.Join(' ', name.Replace('_', ' ').Replace('-', ' ')
                             .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
              .ToLowerInvariant();
}
