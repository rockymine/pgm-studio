using System.Text.Json.Nodes;

namespace PgmStudio.Client.Components;

/// <summary>
/// The dressing JSON's property names and per-part defaults. Same contract as <see cref="ThemeFields"/> holds
/// for a theme: the node the form edits <b>is</b> the wire format the pass deserializes, so naming every field
/// once here is what keeps the two from drifting. The defaults mirror the C# record's own, so a part switched
/// on in the form grows what the pass would have grown for it.
/// </summary>
public static class DressingFields
{
    // the three parts of a recipe; an absent part grows nothing
    public const string Flora = "flora";
    public const string Boulders = "boulders";
    public const string Trees = "trees";

    public const string Seed = "seed";

    // flora
    public const string Coverage = "coverage";
    public const string Scale = "scale";
    public const string Octaves = "octaves";
    public const string FernShare = "fernShare";
    public const string FlowerShare = "flowerShare";
    public const string FlowerScale = "flowerScale";
    public const string TallShare = "tallShare";

    // boulders
    public const string Density = "density";
    public const string SizeSpread = "sizeSpread";
    public const string Form = "form";
    public const string BlockId = "blockId";
    public const string BlockData = "blockData";
    public const string Mossy = "mossy";

    // trees
    public const string GroveScale = "groveScale";
    public const string GroveThreshold = "groveThreshold";
    public const string Species = "species";

    /// <summary>The boulder shape families, with the label the form offers each under.</summary>
    public static readonly (string Id, string Name)[] Forms =
    [
        ("round", "Boulder"),
        ("angular", "Angular"),
        ("outcrop", "Outcrop"),
        ("cairn", "Cairn"),
    ];

    public static JsonObject NewFlora() => new()
    {
        [Coverage] = 0.45, [Scale] = 12, [Octaves] = 3,
        [FernShare] = 0.25, [FlowerShare] = 0.18, [FlowerScale] = 18, [TallShare] = 0.0,
        [Seed] = 7,
    };

    public static JsonObject NewBoulders() => new()
    {
        [Density] = 0.35, [SizeSpread] = 0.5, [Form] = "round",
        [BlockId] = 1, [BlockData] = 0, [Mossy] = true,
        [Seed] = 17,
    };

    public static JsonObject NewTrees() => new()
    {
        [Density] = 0.3, [GroveScale] = 28, [GroveThreshold] = 0.45, [SizeSpread] = 0.4,
        [Species] = new JsonArray(),
        [Seed] = 23,
    };

    /// <summary>A fresh dressing: a light meadow and nothing else. The twin of the JS
    /// <c>defaultDressingJson</c> the bridge seeds a new one with — flora alone, because it reads at any
    /// density, it is entirely cosmetic so defining a dressing cannot change a map's fairness, and it is the
    /// cheapest thing to have to undo.</summary>
    public static JsonObject NewDressing() => new() { [Flora] = NewFlora() };
}

/// <summary>What one part of a dressing is, in the words the form shows — the sibling of
/// <see cref="ThemeBucketInfo"/> for what grows on the ground rather than what it is made of.</summary>
/// <param name="Id">The part's wire field (<see cref="DressingFields"/>).</param>
/// <param name="Affects">Whether the part changes how the map plays. This is not decoration on the label: a
/// part that affects play is generated on the authored unit and mirrored across the symmetry orbit, and one
/// that does not is scattered freely — so the form says which, because it is the difference between a fair map
/// and an unfair one.</param>
public sealed record DressingPartInfo(string Id, string Title, string Blurb, bool Affects, Func<JsonObject> New)
{
    /// <summary>The parts in the order the form lays them out — the ground cover first, since it is the one
    /// every dressing starts with, then the props that stand on it.</summary>
    public static readonly IReadOnlyList<DressingPartInfo> All =
    [
        new(DressingFields.Flora, "Ground cover",
            "Grass, fern and flowers scattered over the soil, thinning into clearings. Masked by the paint beneath — nothing grows on a plaza's quartz or a monument's wool.",
            Affects: false, DressingFields.NewFlora),
        new(DressingFields.Boulders, "Boulders",
            "Rock half-buried in the ground, evenly scattered and clear of anything the map is played through.",
            Affects: true, DressingFields.NewBoulders),
        new(DressingFields.Trees, "Trees",
            "Grown trunk and crown, clumped into groves with clearings between rather than spaced like an orchard.",
            Affects: true, DressingFields.NewTrees),
    ];
}
