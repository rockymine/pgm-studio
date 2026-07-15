namespace PgmStudio.Pgm.Authoring;

using Dict = Dictionary<string, object?>;

/// <summary>
/// Destroyable (DTM) slice of the declarative generator. Per destroyable it emits a cuboid region around the
/// structure and the <c>&lt;destroyable&gt;</c> that scopes its goal to the region's <c>materials</c> blocks.
/// <para>The region is the structure's own <see cref="DestroyableIntent.Box"/> — the box the stamper built the
/// blocks from — so the two cannot disagree (OB8). A destroyable whose box is unresolved is skipped rather
/// than given a guessed region: a region that misses its structure is a zero-health goal PGM accepts with
/// nothing louder than a warning, which is worse than no goal at all.</para>
/// <para>Generated regions are the exact structure bounds. Hand-authored maps draw a loose box around the
/// structure (OB12); that slack is a human artifact, not something to reproduce. Idempotent
/// clear-then-build.</para>
/// </summary>
public static class DestroyableGenerator
{
    private const string Key = "destroyables";

    public static void Apply(Dict doc, MapIntent intent)
    {
        var list = ObjectiveRegion.List(doc, Key);
        ObjectiveRegion.Clear(doc, list);
        if (intent.Destroyables is null) return;

        foreach (var b in intent.Destroyables)
        {
            if (b.Box is not { } box) continue;
            var id = ObjectiveRegion.UniqueId(list, IntentNaming.Slug(b.Name), "destroyable");
            var regionId = ObjectiveRegion.Emit(doc, id, box);
            list.Add(new Dict
            {
                ["id"] = id,
                ["name"] = b.Name,
                ["owner"] = b.Owner,
                ["region"] = regionId,
                ["materials"] = b.Materials,
            });
        }
    }
}
