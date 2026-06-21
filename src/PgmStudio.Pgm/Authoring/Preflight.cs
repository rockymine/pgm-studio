namespace PgmStudio.Pgm.Authoring;

using Dict = Dictionary<string, object?>;

/// <summary>
/// The Review-phase pre-flight checks that prove a generated map is <i>correct</i>, not merely present
/// (new-map-authoring.md §9). Two checks live here because they need only the codec + categorizer:
/// <list type="bullet">
/// <item><b>Round-trip</b> — the document survives the export codec (<c>FromDict → XmlWriter → re-parse</c>)
/// with no field lost; a drift here is a generator/codec bug, and the same path is what <c>GET /xml</c>
/// runs, so a failure means export would throw.</item>
/// <item><b>Mirror consistency</b> — <see cref="RegionCategorizer.DeriveFacets"/> recovers the intent's
/// classification (spawn protection reads back as <c>spawn/protection</c>, the wool room as
/// <c>wool/room</c>, the build union as <c>build</c>, monuments as <c>wool/monument</c>). Generator and
/// categorizer are inverses — the strongest test that generation produced correct structure.</item>
/// </list>
/// The other two checks (buildability placements, traversability connectivity) need world feature data
/// and are composed by the endpoint from the analysis layer.
/// </summary>
public static class Preflight
{
    /// <summary>One pre-flight finding. <see cref="Status"/> is <c>"pass"</c>, <c>"fail"</c> or
    /// <c>"skip"</c> (the check could not run — e.g. no scan layers); <see cref="Detail"/> is the line
    /// surfaced in the validate log + the check row.</summary>
    public sealed record Check(string Key, string Label, string Status, string Detail);

    /// <summary>Push the document through the export codec — serialize to <c>map.xml</c>, re-parse it, and
    /// confirm the codec is idempotent on that serialized form (the corpus round-trip guard, check #1: no
    /// field is lost or reshaped across <c>dict ⇄ MapXml ⇄ XML</c>). The serialize + re-parse legs prove the
    /// generated document is a real, loadable PGM map; any codec exception or non-idempotent drift fails.
    /// (The pre-serialization generator doc itself isn't the comparison target — it carries derived,
    /// non-XML fields like <c>region_categories</c> that never round-trip.)</summary>
    public static Check RoundTrip(Dict doc)
    {
        try
        {
            var xml = XmlWriter.ToXml(Deserializer.FromDict(doc));
            var parsed = Serializer.ToDict(MapParser.ParseXmlString(xml));
            var reSerialized = Serializer.ToDict(Deserializer.FromDict(parsed));
            // Compare canonically (derived per-region bounds_2d stripped); FromDict above needs the full
            // dict, so the strip happens only at the comparison.
            var a = JsonTree.Canonical(parsed);
            var b = JsonTree.Canonical(reSerialized);
            if (JsonTree.DeepEquals(a, b))
                return new("round-trip", "Round-trip", "pass", "codec parity — no field lost in XML ↔ dict");
            var diff = JsonTree.DiffKeys(a, b);
            return new("round-trip", "Round-trip", "fail",
                $"fields drifted through the export codec: {string.Join(", ", diff.Take(6))}");
        }
        catch (Exception ex)
        {
            return new("round-trip", "Round-trip", "fail", $"export codec rejected the generated map: {ex.Message}");
        }
    }

    /// <summary>Re-derive region facets from the generated document and confirm the categorizer recovers
    /// every classification the intent declared. Classifications the intent doesn't use (e.g. a roomless
    /// wool) are not demanded.</summary>
    public static Check Mirror(Dict doc, MapIntent intent)
    {
        var facets = RegionCategorizer.DeriveFacets(doc).Values.ToList();
        bool HasFacet(string category, string? subtype) =>
            facets.Any(f => f.Category == category && (subtype is null || f.Subtype == subtype));

        var recovered = new List<string>();
        var missing = new List<string>();
        // (intent declares it) ⇒ (the generated structure must read back). Skip what the intent doesn't author.
        void Expect(bool want, string label, Func<bool> ok) { if (want) (ok() ? recovered : missing).Add(label); }

        Expect(intent.Spawns.Any(s => s.Protection is not null), "spawn/protection", () => HasFacet("spawn", "protection"));
        Expect((intent.Wools ?? []).Any(w => w.Room is not null), "wool/room",        () => HasFacet("wool", "room"));
        Expect(intent.Build is { Areas.Count: > 0 },              "build",            () => HasFacet("build", null));
        // Monuments are inline <monument location> on each wool (not regions, so no facet) — they read back
        // structurally: every declared wool must resolve a monument (PGM's load-time requirement).
        Expect((intent.Wools ?? []).Any(w => w.Monuments.Count > 0), "wool/monument", () => MapValidity.Check(doc).Valid);

        if (missing.Count > 0)
            return new("mirror", "Mirror check", "fail",
                $"generated structure not recovered: {string.Join(" · ", missing)}");
        var detail = recovered.Count > 0
            ? string.Join(" ", recovered.Select(r => $"{r} ✓"))
            : "no classified structure to recover yet";
        return new("mirror", "Mirror check", "pass", detail);
    }
}
