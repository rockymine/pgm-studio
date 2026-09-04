using PgmStudio.Domain;

namespace PgmStudio.Pgm.Authoring;

using Dict = Dictionary<string, object?>;

/// <summary>
/// The <c>&lt;modes&gt;</c> slice of the declarative generator: when every opted-in objective stops being what
/// it is made of.
///
/// <para><b>A destroy map without them does not end.</b> A monument or a core is obsidian because obsidian is
/// what reads as a goal — opaque, slow, unmistakable — and that same slowness is what lets a defending team
/// hold one indefinitely. PGM's answer is a ladder: at a stated time every opted-in objective's blocks are
/// replaced with a softer material, so the raid that could not finish at minute ten finishes at minute twenty.
/// Measured over the 314 DTM/DTC maps of the two corpora, 173 declare a ladder and 171 of those have an
/// objective opted in; the ladder written here when a map states none is their modal answer.</para>
///
/// <para><b>Declaring the modes is only half of it.</b> A <c>&lt;destroyable&gt;</c> or a <c>&lt;core&gt;</c>
/// is affected by <em>no</em> mode unless it says so — PGM defaults the set to empty
/// (<c>DestroyableModule</c>, <c>CoreModule</c>) — so the objective generators write
/// <c>mode-changes="true"</c> beside every goal they emit. A map with a ladder nothing opts into is a map
/// whose ladder does nothing, which is <c>OB26</c>.</para>
/// </summary>
internal static class ModesGenerator
{
    private const string Key = "modes";

    public static void Apply(Dict doc, MapIntent intent)
    {
        var modes = ObjectiveModes.For(intent);
        if (modes.Count == 0) { doc.Remove(Key); return; }

        var list = new List<object?>(modes.Count);
        foreach (var mode in modes)
        {
            var entry = new Dict
            {
                // The id is what `/mode start` names and what a `modes="…"` set would reference. PGM
                // generates one when a document states none; naming it here means the same ladder reads the
                // same way on every board the studio writes.
                ["id"] = Id(mode, list),
                ["after"] = mode.After,
                ["material"] = mode.Material,
            };
            if (mode.Name.Length > 0) entry["name"] = mode.Name;
            list.Add(entry);
        }
        doc[Key] = list;
    }

    /// <summary>A mode's id: its material as a slug, and a number where two rungs share one. The same shape
    /// PGM derives for an unstated id, so a document that names them reads as one that did not.</summary>
    private static string Id(ModeIntent mode, List<object?> siblings)
    {
        var baseId = $"mode-{IntentNaming.Slug(mode.Material)}";
        if (baseId == "mode-") baseId = "mode";
        var taken = siblings.OfType<Dict>().Select(d => d.GetValueOrDefault("id") as string).ToHashSet();
        if (!taken.Contains(baseId)) return baseId;
        for (var i = 2; ; i++)
            if (!taken.Contains($"{baseId}-{i}")) return $"{baseId}-{i}";
    }
}
