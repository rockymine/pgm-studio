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
}

/// <summary>XML attribute/text/coordinate helpers mirroring the Python ElementTree usage.</summary>
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
        foreach (var group in root.Elements(groupTag))
            Walk(group, AttributesOf(group, null));
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
}
