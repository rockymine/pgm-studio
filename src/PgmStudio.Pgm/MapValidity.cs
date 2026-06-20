namespace PgmStudio.Pgm;

using Dict = Dictionary<string, object?>;

/// <summary>
/// Structural validity of a parsed map document against the invariants PGM enforces while loading
/// (proto 1.5.0). The headline rule: every wool needs a monument. PGM reads it with
/// <c>parseRequiredRegionProperty(woolEl, "monument")</c>, which throws
/// <c>InvalidXMLException("Missing required region 'monument'")</c> — so a monument-less wool is an
/// unloadable map, not merely an unplayable one. This document parser is deliberately lenient there (a
/// missing monument resolves to <c>0,0,0</c>), so this check is what stops an invalid <c>map.xml</c> from
/// being exported. A wool's monument is the block where a capturing team places the wool to score.
/// </summary>
public static class MapValidity
{
    /// <summary>One validity finding. <see cref="Severity"/> is <c>"error"</c> (blocks export) or
    /// <c>"warning"</c>; <see cref="Subject"/> names the offending entity (e.g. the wool colour).</summary>
    public sealed record Issue(string Kind, string Subject, string Severity, string Message);

    public sealed record Result(bool Valid, IReadOnlyList<Issue> Issues)
    {
        public IEnumerable<Issue> Errors => Issues.Where(i => i.Severity == "error");
    }

    public static Result Check(Dict doc)
    {
        var issues = new List<Issue>();
        foreach (var wool in Wools(doc))
        {
            var color = wool.GetValueOrDefault("color") as string
                ?? wool.GetValueOrDefault("id") as string ?? "wool";
            if (!HasMonument(wool))
                issues.Add(new Issue("wool_monument", color, "error",
                    $"Wool '{color}' has no monument — PGM requires a monument per wool (the block where a capturing team places the wool)."));
        }
        return new Result(issues.All(i => i.Severity != "error"), issues);
    }

    // Capturable iff at least one monument resolves to a block — an inline location or a
    // monument_region reference (the two forms PGM's required <monument> accepts).
    private static bool HasMonument(Dict wool) =>
        (wool.GetValueOrDefault("monuments") as List<object?> ?? [])
        .OfType<Dict>()
        .Any(m => m.GetValueOrDefault("location") is Dict || m.GetValueOrDefault("monument_region") is string { Length: > 0 });

    private static IEnumerable<Dict> Wools(Dict doc) =>
        (doc.GetValueOrDefault("wools") as List<object?> ?? []).OfType<Dict>();
}
