using PgmStudio.Vocabulary;

namespace PgmStudio.Minecraft.Houses;

/// <summary>
/// What a library house style may be called: lowercase words joined by hyphens, every one of them a
/// <see cref="Describing"/> word but the last, which is one of the <see cref="Buildings"/> — the materials and
/// the form that set it apart, then the kind of building it is. <c>brick-roofed-stone-cottage</c>,
/// <c>oak-stilt-house</c>, <c>hay-gambrel-barn</c>.
///
/// <para>Both lists are closed, and what keeps a word out of them is that it says where a style was first used
/// rather than what it is: a board's name, a map role a room plays (spawn, cage, wool), an occupation, a
/// place. A word belongs in <see cref="Describing"/> when a player looking at the building could say it, and in
/// <see cref="Buildings"/> when it names a kind of building rather than what is done in one.</para>
/// </summary>
public static class HouseNames
{
    /// <summary>The kinds of building a name ends on.</summary>
    public static readonly IReadOnlySet<string> Buildings = new SortedSet<string>(StringComparer.Ordinal)
    {
        "barn", "bothy", "boathouse", "bungalow", "cabin", "chalet", "chapel", "cottage", "farmhouse", "gatehouse",
        "granary", "hall", "house", "hut", "keep", "lodge", "longhouse", "manor", "mill", "minehead", "pavilion",
        "shelter", "storehouse", "tower", "townhouse", "villa", "warehouse",
    };

    /// <summary>What a building is made of and how it is shaped: woods, stones, colours, roof forms, and the
    /// handful of words that say how the parts are put together.</summary>
    public static readonly IReadOnlySet<string> Describing = new SortedSet<string>(StringComparer.Ordinal)
    {
        // woods, and what is made of them
        "oak", "spruce", "birch", "jungle", "acacia", "dark", "wood", "wooden", "log", "plank", "timber",
        "timbered", "half", "framed", "beamed",
        // stone, brick and the other masonry
        "stone", "cobblestone", "cobble", "mossy", "cracked", "chiselled", "polished", "smooth", "brick", "andesite",
        "diorite", "granite", "sandstone", "quartz", "clay", "terracotta", "prismarine", "nether", "end", "obsidian",
        "gravel", "sand", "flint", "plaster", "limewash", "rubble",
        // what else a wall or a roof is laid in
        "hay", "thatch", "thatched", "wool", "glass", "iron",
        // colours a clay, a wool or a glass is dyed
        "white", "orange", "magenta", "light", "yellow", "lime", "pink", "gray", "grey", "silver", "cyan", "purple",
        "blue", "brown", "green", "red", "black", "pale",
        // roofs
        "roofed", "gabled", "hipped", "gambrel", "saltbox", "pyramid", "flat", "steep", "low",
        // how the building is put together and how big it is
        "banded", "checkered", "striped", "panelled", "latticed", "trimmed", "porched", "veranda", "stilt", "stilted",
        "tall", "long", "wide", "narrow", "small", "large", "squat", "single", "two", "three", "four", "storey",
        "tower", "twin", "quay", "mountain", "and",
    };

    /// <summary>The words of <paramref name="name"/> that are in neither list where they stand, and whether the
    /// name has the shape at all — at least two words, the last of them a building. Empty for a good name.</summary>
    public static Findings Check(string name)
    {
        var words = (name ?? "").Split('-');
        if (words.Length < 2 || words.Any(word => word.Length == 0 || !word.All(char.IsAsciiLetterLower)))
            return Complain(name ?? "", "is not lowercase words joined by hyphens, at least two of them");

        var last = words[^1];
        var strangers = words[..^1].Where(word => !Describing.Contains(word)).Distinct().ToList();
        var problems = new List<string>();
        if (!Buildings.Contains(last)) problems.Add($"ends on '{last}', which is not a kind of building");
        if (strangers.Count > 0)
            problems.Add($"describes the building with {string.Join(", ", strangers.Select(word => $"'{word}'"))}, "
                         + "which the describing words do not hold");
        return problems.Count == 0 ? Findings.None : Complain(name!, string.Join(", and ", problems));
    }

    private static Findings Complain(string name, string why) => Findings.Of(new Finding(HouseStyleRules.LibraryName,
        $"'{name}' {why}. A name is the materials and the form that set the building apart, then the kind of "
        + "building it is — brick-roofed-stone-cottage, oak-stilt-house — from the two lists "
        + "GET /api/room-styles/name-words answers.",
        Severity.Complaint, Field: "name"));
}
