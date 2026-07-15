namespace PgmStudio.Domain;

/// <summary>The destroyable structure families worth generating. The pillar is the default.</summary>
public enum DestroyableStyle
{
    /// <summary>1×1×1 — a single block, and the single most common destroyable in the corpus.</summary>
    Pillar1,
    /// <summary>1×2×1.</summary>
    Pillar2,
    /// <summary>1×3×1.</summary>
    Pillar3,
    /// <summary>3×3×3, optionally hollowed by a 1×1×1 bedrock centre.</summary>
    Cube3,
    /// <summary>4×4×4, optionally hollowed by a 2×2×2 bedrock centre.</summary>
    Cube4,
    /// <summary>A 3×3 plus-section column: 5 blocks a layer, corners left open.</summary>
    ColumnPlus,
}

/// <summary>
/// The slug vocabulary for <see cref="DestroyableStyle"/> — the names a plan and an intent spell it with.
/// Lives beside the enum, and below both the plan layer and the world stamper, so the string a plan is
/// validated against and the enum the stamper switches on can never name different sets.
/// </summary>
public static class DestroyableStyles
{
    /// <summary>Over half the corpus, and the safe structure for a goal: opaque, blast-resistant, unmistakable.</summary>
    public const string DefaultMaterials = "obsidian";

    /// <summary>The 1×3×1 pillar — tall enough to read as a monument, small enough to break in a raid.</summary>
    public const DestroyableStyle Default = DestroyableStyle.Pillar3;

    private static readonly Dictionary<string, DestroyableStyle> BySlug = new(StringComparer.OrdinalIgnoreCase)
    {
        ["pillar-1"] = DestroyableStyle.Pillar1,
        ["pillar-2"] = DestroyableStyle.Pillar2,
        ["pillar-3"] = DestroyableStyle.Pillar3,
        ["cube-3"] = DestroyableStyle.Cube3,
        ["cube-4"] = DestroyableStyle.Cube4,
        ["column-plus"] = DestroyableStyle.ColumnPlus,
    };

    /// <summary>Every style slug, in enum order — the authoring vocabulary.</summary>
    public static IReadOnlyList<string> All { get; } =
        ["pillar-1", "pillar-2", "pillar-3", "cube-3", "cube-4", "column-plus"];

    public static string Slug(DestroyableStyle style) => All[(int)style];

    /// <summary>Resolve a slug, or false when it names no style. Never guesses: an unknown slug is an
    /// authoring error to report, not a value to silently replace with the default.</summary>
    public static bool TryParse(string? slug, out DestroyableStyle style)
    {
        style = Default;
        return !string.IsNullOrEmpty(slug) && BySlug.TryGetValue(slug, out style);
    }

    /// <summary>True when <paramref name="slug"/> names a style.</summary>
    public static bool IsKnown(string? slug) => TryParse(slug, out _);
}
