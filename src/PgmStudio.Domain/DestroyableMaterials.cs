namespace PgmStudio.Domain;

/// <summary>
/// What a generated destroyable can be made of — the authoring vocabulary, and the set the world-export
/// stamper can actually build.
///
/// <para>It is short for a reason. PGM accepts any material match on a <c>&lt;destroyable&gt;</c>, but the
/// generator only knows how to place these four, and the corpus says they are the ones that matter: obsidian,
/// emerald, gold and ender stone carry 84% of declared destroyables (<c>docs/world-scan/
/// objective-suggestion.md</c> §3). Anything outside the list falls back to obsidian, which is the right
/// fallback and the wrong thing to let an author pick unknowingly — so the vocabulary is published, and a
/// picker offers these rather than a free-text field whose typos silently become obsidian.</para>
/// </summary>
public static class DestroyableMaterials
{
    /// <summary>The buildable matches, default first.</summary>
    public static IReadOnlyList<string> All { get; } = ["obsidian", "emerald block", "gold block", "ender stone"];

    /// <summary>The block a match names, or obsidian when it names nothing this vocabulary builds. Resolves
    /// through <see cref="MaterialIds"/>, so the spellings PGM treats as one name (<c>end_stone</c>,
    /// <c>ender stone</c>, <c>ENDER-STONE</c>) are one material here too.</summary>
    public static int BlockId(string? materials)
    {
        if (string.IsNullOrWhiteSpace(materials)) return Obsidian;
        foreach (var id in MaterialIds.Resolve(materials))
            if (Buildable.Contains(id)) return id;
        return Obsidian;
    }

    /// <summary>Whether a match names at least one material the generator can actually stamp. Checked before
    /// authoring a destroyable's <c>materials</c>, because <see cref="BlockId"/>'s own fallback is silent: an
    /// unrecognised name still writes into the XML verbatim (<c>DestroyableGenerator</c>) while the stamper
    /// quietly builds obsidian, so the declared material matches nothing in its own region — the same
    /// zero-health failure as an unmatched kit tool, just one step upstream of it. Empty is buildable (the
    /// obsidian default).</summary>
    public static bool IsBuildable(string? materials) =>
        string.IsNullOrWhiteSpace(materials) || MaterialIds.Resolve(materials).Any(Buildable.Contains);

    private const int Obsidian = 49;
    private static readonly HashSet<int> Buildable = [.. All.SelectMany(MaterialIds.Resolve)];
}
