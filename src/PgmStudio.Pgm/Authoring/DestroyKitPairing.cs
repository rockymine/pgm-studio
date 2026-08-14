using PgmStudio.Domain;

namespace PgmStudio.Pgm.Authoring;

/// <summary>
/// The derivation behind the spawn kit's pickaxe: what pickaxe material a kit should carry to match every
/// destroyable and core a map defends.
/// <para>An iron pickaxe <b>breaks</b> obsidian — vanilla breaks any block with any tool, given enough time —
/// what it does not do is <b>drop</b> it, and a destroy goal only requires the block gone. So a mismatched
/// pickaxe is not a legality question; it is a raid-length one, and the corpus answer to it is a substitution
/// rather than a gap: a destroy kit's pickaxe is upgraded to match its goal's material
/// (docs/pgm/destroyables-and-cores.md DT1) — obsidian pairs with diamond, anything softer with iron — so a
/// generated kit follows the same rule rather than leaving every obsidian goal to drop nothing. A core's
/// casing carries no authored material — the generator never emits one, relying on PGM's own obsidian
/// default — so a map with a core always upgrades to diamond outright.</para>
/// </summary>
public static class DestroyKitPairing
{
    /// <summary>The corpus default pickaxe tier for a map with no destroy goal at all.</summary>
    private const string DefaultTier = "iron";

    /// <summary>The pickaxe material (e.g. <c>"diamond pickaxe"</c>) the standard spawn kit should carry: the
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
}
