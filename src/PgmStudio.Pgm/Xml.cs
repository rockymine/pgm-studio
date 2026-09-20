using System.Globalization;
using System.Xml.Linq;
using PgmStudio.Domain;

namespace PgmStudio.Pgm;

/// <summary>
/// A group leaf together with every attribute visible on it. Attributes cascade from the enclosing
/// groups; the nearest declaration wins, so the leaf's own always does.
/// </summary>
internal readonly record struct InheritedElement(XElement Element, IReadOnlyDictionary<string, string> Attributes)
{
    public string Get(string name, string def = "") => Attributes.GetValueOrDefault(name) ?? def;
    public string? GetOrNull(string name) => Attributes.GetValueOrDefault(name);
    public bool Has(string name) => Attributes.ContainsKey(name);

    public bool Bool(string name, bool def = false)
    {
        var raw = Get(name, def ? "true" : "false").Trim().ToLowerInvariant();
        return raw is "true" or "1" or "yes" or "on";
    }

    /// <summary>The attribute as a boolean, or <c>null</c> when it is absent. Distinct from
    /// <see cref="Bool"/> wherever PGM's own default is not <c>false</c> — an unwritten attribute is then a
    /// different value from a written <c>false</c>, and materialising one as the other changes the map.</summary>
    public bool? BoolOrNull(string name) => GetOrNull(name) is null ? null : Bool(name);

    /// <summary>The attribute as a number, or <c>null</c> when it is absent or does not parse. Accepts
    /// PGM's <c>oo</c>/<c>-oo</c> spelling of infinity, which several of the capture rates take.</summary>
    public double? DoubleOrNull(string name)
    {
        var raw = GetOrNull(name)?.Trim();
        if (raw is null) return null;
        if (raw is "oo") return double.PositiveInfinity;
        if (raw is "-oo") return double.NegativeInfinity;
        return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;
    }
}

/// <summary>XML attribute, text and coordinate helpers over XElement.</summary>
internal static class Xml
{
    /// <summary>
    /// Flatten an objective group (<c>&lt;wools&gt;</c>, <c>&lt;destroyables&gt;</c>, <c>&lt;cores&gt;</c>,
    /// <c>&lt;modes&gt;</c>) to its leaves, cascading group attributes down to each one. Groups nest
    /// arbitrarily deep and every attribute inherits — not just the obvious one — so a leaf can take its
    /// colour, materials or location from an ancestor and declare nothing of its own. The nearest
    /// declaration wins.
    /// </summary>
    public static List<InheritedElement> Flatten(XElement root, string groupTag, string leafTag)
    {
        var leaves = new List<InheritedElement>();
        foreach (var group in root.Elements(groupTag)) leaves.AddRange(FlattenUnder(group, groupTag, leafTag));
        return leaves;
    }

    /// <summary>
    /// Flatten from an element that is itself outside the group — <c>&lt;king&gt;</c> over
    /// <c>&lt;hills&gt;</c>/<c>&lt;hill&gt;</c> — so that the outer element's attributes seed the cascade
    /// and reach every leaf. A leaf sitting directly under it is a leaf, which is how
    /// <c>&lt;king&gt;&lt;hill/&gt;&lt;/king&gt;</c> parses the same as the nested spelling.
    /// </summary>
    public static List<InheritedElement> FlattenUnder(XElement outer, string groupTag, string leafTag)
    {
        var leaves = new List<InheritedElement>();
        Walk(outer, AttributesOf(outer, null));
        return leaves;

        void Walk(XElement group, Dictionary<string, string> inherited)
        {
            foreach (var child in group.Elements())
            {
                var tag = child.Name.LocalName;
                if (tag == groupTag) Walk(child, AttributesOf(child, inherited));
                else if (tag == leafTag) leaves.Add(new InheritedElement(child, AttributesOf(child, inherited)));
            }
        }
    }

    private static Dictionary<string, string> AttributesOf(XElement e, Dictionary<string, string>? inherited)
    {
        var attrs = inherited is null ? new Dictionary<string, string>() : new Dictionary<string, string>(inherited);
        foreach (var a in e.Attributes()) attrs[a.Name.LocalName] = a.Value;
        return attrs;
    }

    /// <summary>Attribute value or <paramref name="def"/> (mirrors elem.get(name, def)).</summary>
    public static string Get(XElement e, string name, string def = "")
        => e.Attribute(name)?.Value ?? def;

    /// <summary>Attribute value or null (mirrors elem.get(name) with no default).</summary>
    public static string? GetOrNull(XElement e, string name) => e.Attribute(name)?.Value;

    /// <summary>Own text content (mirrors elem.text for a leaf element).</summary>
    public static string Text(XElement e) => e.Nodes().OfType<XText>().Aggregate("", (a, t) => a + t.Value);

    public static int IntAttr(XElement e, string name, int def)
        => int.TryParse(Get(e, name, def.ToString(CultureInfo.InvariantCulture)),
                        NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : def;

    public static bool BoolAttr(XElement e, string name, bool def = false)
    {
        var raw = Get(e, name, def ? "true" : "false").Trim().ToLowerInvariant();
        return raw is "true" or "1" or "yes";
    }

    /// <summary>Parse "x,y,z" → 3 components (each may be null for a template variable).</summary>
    public static double?[] Coords3(string s)
    {
        var parts = s.Split(',');
        if (parts.Length >= 3)
            return [Coord.Parse(parts[0]), Coord.Parse(parts[1]), Coord.Parse(parts[2])];
        return [0.0, 0.0, 0.0];
    }

    /// <summary>Parse "x,z" → 2 components (each may be null).</summary>
    public static double?[] Coords2(string s)
    {
        var parts = s.Split(',');
        if (parts.Length >= 2)
            return [Coord.Parse(parts[0]), Coord.Parse(parts[1])];
        return [0.0, 0.0];
    }

    /// <summary>None → 0.0 coercion used by block/cylinder/circle/sphere/half parsing.</summary>
    public static double Or0(double? v) => v ?? 0.0;

    /// <summary>The vector a <c>&lt;block&gt;</c> states, from either form PGM accepts: the <c>location</c>
    /// attribute first, then the element's own text. PGM's <c>RegionParser.parseBlock</c> reads them in that
    /// order — the attribute is a back-compat spelling it still takes — and the corpus uses both, so a reader
    /// that takes only the text resolves every attribute-spelled monument to the origin.</summary>
    public static string BlockVector(XElement e) =>
        GetOrNull(e, "location") is { Length: > 0 } attr ? attr : Text(e);
}
