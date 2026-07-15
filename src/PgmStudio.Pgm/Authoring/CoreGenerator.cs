namespace PgmStudio.Pgm.Authoring;

using Dict = Dictionary<string, object?>;

/// <summary>
/// Core (DTC) slice of the declarative generator: a cuboid region around the casing plus the
/// <c>&lt;core&gt;</c> that scopes it. The destroyable's slice with three differences.
/// <para>The name is optional, because PGM auto-names a core per team; emitting an empty one would replace a
/// sensible default with nothing. And no <c>material</c> is emitted at all: PGM defaults to obsidian, which
/// is effectively universal in the corpus (DC1). The owning attribute stays <c>owner</c> here — the XML
/// spells it <c>team</c> (OB1), but that translation is <see cref="XmlWriter"/>'s, at the boundary.</para>
/// <para><c>leak</c> is emitted only when it differs from PGM's own default, since the pair that matters is
/// <c>float</c>+<c>leak</c> (DC2) and <c>float</c> is already expressed in the region's Y. The region is the
/// stamper's box (OB8); an unresolved box emits nothing. Idempotent clear-then-build.</para>
/// </summary>
public static class CoreGenerator
{
    private const string Key = "cores";

    public static void Apply(Dict doc, MapIntent intent)
    {
        var list = ObjectiveRegion.List(doc, Key);
        ObjectiveRegion.Clear(doc, list);
        if (intent.Cores is null) return;

        foreach (var c in intent.Cores)
        {
            if (c.Box is not { } box) continue;

            // A core is usually nameless (PGM names it), so the id falls back to the owner rather than to a
            // bare "core" that every team would then collide on.
            var baseId = c.Name.Length > 0 ? IntentNaming.Slug(c.Name) : $"{IntentNaming.Slug(c.Owner)}-core";
            var id = ObjectiveRegion.UniqueId(list, baseId, "core");
            var regionId = ObjectiveRegion.Emit(doc, id, box);

            var entry = new Dict
            {
                ["id"] = id,
                // `owner` is the doc-tree's name for it everywhere; only the XML spells it `team`, and
                // XmlWriter does that translation at the boundary (OB1). Writing `team` here instead would
                // read back as an unowned core.
                ["owner"] = c.Owner,
                ["region"] = regionId,
            };
            if (c.Name.Length > 0) entry["name"] = c.Name;
            if (c.Leak != Domain.ObjectiveDefaults.CoreLeak) entry["leak"] = (long)c.Leak;
            list.Add(entry);
        }
    }
}
