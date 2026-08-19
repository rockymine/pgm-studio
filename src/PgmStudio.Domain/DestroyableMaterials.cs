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

    /// <summary>The most blocks a goal built in <b>obsidian</b> may be (author). Obsidian is the slowest block
    /// in the game to break, which is what makes it right for the pillar every second corpus destroyable is —
    /// one to three blocks, a raid rather than a shift — and wrong for anything bigger: a 27-block cube of it
    /// is a grind nobody performs, and the corpus agrees, building its cubes in emerald, gold and ender stone
    /// (DT1, DT4). It is a property of the <em>material</em> rather than of the style, which is why the number
    /// lives here.</summary>
    public const int ObsidianLimit = 3;

    /// <summary>What a goal too big for obsidian is built in instead: ender stone, the corpus's own material
    /// for the large sculptural monument (DT4).</summary>
    public const string Bulk = "ender stone";

    /// <summary>
    /// The material a destroyable of <paramref name="style"/> is <b>actually</b> built in, and the sentence
    /// saying why that is not what it asked for — null where nothing was corrected.
    ///
    /// <para>Two faults, one correction, because they compound. A name outside <see cref="All"/> is written
    /// into the XML verbatim while <see cref="BlockId"/> quietly stamps obsidian, so the declared material
    /// matches nothing in its own region and the goal loads at zero health (OB3). And obsidian on a cube is
    /// buildable and still wrong, by <see cref="ObsidianLimit"/>. Correcting the first can produce the second,
    /// so one resolve answers both and the caller writes one complaint.</para>
    ///
    /// <para>A correction rather than a refusal: the world is built and the goal stands, in a material the
    /// author did not name — which is a thing the caller has to be <em>told</em>, and the reason this answers
    /// a sentence rather than a bool.</para>
    /// </summary>
    public static (string Materials, string? Correction) Resolve(
        DestroyableStyle style, string? materials, int columnHeight = 3)
    {
        var asked = string.IsNullOrWhiteSpace(materials) ? All[0] : materials;
        var unknown = !IsBuildable(materials);
        var built = unknown ? All[0] : asked;

        // Asked of the block that would actually be laid rather than of the names in the match: a compound
        // `gold block;obsidian` builds gold, and correcting it would be correcting something that never
        // happened.
        var blocks = ObjectiveFootprint.BlockCount(style, columnHeight);
        var overweight = blocks > ObsidianLimit && BlockId(built) == Obsidian;
        if (overweight) built = Bulk;

        return (built, (unknown, overweight) switch
        {
            (true, true) =>
                $"'{materials}' is not a material this studio builds and {DestroyableStyles.Slug(style)} is "
                + $"{blocks} blocks, past the {ObsidianLimit} obsidian is worth — built in {Bulk}",
            (true, false) =>
                $"'{materials}' is not a material this studio builds — built in {built}, which is what the "
                + "blocks would have been whatever the map.xml declared",
            (false, true) =>
                $"{DestroyableStyles.Slug(style)} is {blocks} blocks and obsidian is worth at most "
                + $"{ObsidianLimit} of them — built in {Bulk}",
            _ => null,
        });
    }

    private const int Obsidian = 49;
    private static readonly HashSet<int> Buildable = [.. All.SelectMany(MaterialIds.Resolve)];
}
