using PgmStudio.Domain;

namespace PgmStudio.Pgm.Authoring;

/// <summary>
/// The one derivation behind the spawn kit's pickaxe and the guard that checks it held: what pickaxe
/// material a kit needs to break every destroyable and core a map defends, and — reading a generated map's
/// actual kits rather than re-trusting the derivation — which of those goals nothing in the kit can break.
/// <para>A destroyable's material defaults to obsidian and the corpus norm is a destroy kit whose pickaxe is
/// upgraded to match (docs/pgm/destroyables-and-cores.md DT1, tools/mapgen/review.md MG18): an iron pickaxe does not mine obsidian
/// at all, so a mismatch is not a rough edge, it is an unwinnable map. A core's casing carries no authored
/// material — the generator never emits one, relying on PGM's own obsidian default — so a map with a core
/// always needs a diamond pickaxe outright.</para>
/// </summary>
public static class DestroyKitPairing
{
    /// <summary>OB18 — the rule id carried on every unwinnable-goal finding, so a caller can act on the id
    /// rather than parse the sentence (docs/pgm/destroyables-and-cores.md).</summary>
    public const string Rule = "OB18";

    /// <summary>The corpus default pickaxe tier for a map with no destroy goal at all.</summary>
    private const string DefaultTier = "iron";

    /// <summary>The pickaxe material (e.g. <c>"iron pickaxe"</c>) the standard spawn kit should carry: the
    /// corpus default, upgraded to whatever the map's own destroyables/cores need.</summary>
    public static string RequiredPickaxe(MapIntent intent)
    {
        var tier = DefaultTier;
        foreach (var destroyable in intent.Destroyables ?? [])
            if (MiningTiers.Required(destroyable.Materials) is { } needed)
                tier = MiningTiers.Higher(tier, needed);
        if (intent.Cores is { Count: > 0 }) tier = MiningTiers.Higher(tier, "diamond");
        return $"{tier} pickaxe";
    }

    /// <summary>The destroyables/cores, named, that none of <paramref name="kitPickaxeMaterials"/> can break —
    /// empty when the map is winnable. Takes the kit materials actually written to the map rather than
    /// re-deriving them, so a caller learns whether the pairing really held rather than assuming it did.</summary>
    public static List<string> Unwinnable(MapIntent intent, IEnumerable<string> kitPickaxeMaterials)
    {
        string? haveTier = null;
        foreach (var material in kitPickaxeMaterials)
            if (MiningTiers.TierOfPickaxe(material) is { } tier)
                haveTier = haveTier is null ? tier : MiningTiers.Higher(haveTier, tier);

        var findings = new List<string>();
        bool CannotBreak(string? needed) =>
            needed is not null && (haveTier is null || MiningTiers.Rank(haveTier) < MiningTiers.Rank(needed));

        foreach (var destroyable in intent.Destroyables ?? [])
            if (CannotBreak(MiningTiers.Required(destroyable.Materials)))
                findings.Add(destroyable.Name.Length > 0 ? destroyable.Name : destroyable.Owner);

        if (intent.Cores is { Count: > 0 } cores && CannotBreak("diamond"))
            findings.AddRange(cores.Select(core => core.Name.Length > 0 ? core.Name : core.Owner));

        return findings;
    }

    /// <summary>Every pickaxe material named by every kit in an exported map document — the alphabet
    /// <see cref="Unwinnable"/> checks a destroy goal's material against, read back from the kits actually
    /// written rather than assumed. Shared by every caller that has already composed the doc's kits
    /// (<c>tools/mapgen</c>, the studio's own export gate), so the reading is asked once.</summary>
    public static List<string> KitPickaxeMaterials(Dictionary<string, object?> doc)
    {
        var materials = new List<string>();
        if (doc.GetValueOrDefault("kits") is List<object?> kits)
            foreach (var kit in kits.OfType<Dictionary<string, object?>>())
                if (kit.GetValueOrDefault("items") is List<object?> items)
                    foreach (var item in items.OfType<Dictionary<string, object?>>())
                        if (item.GetValueOrDefault("material") is string material)
                            materials.Add(material);
        return materials;
    }
}
