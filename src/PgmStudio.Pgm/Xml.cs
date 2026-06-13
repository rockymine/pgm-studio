using System.Globalization;
using System.Xml.Linq;
using PgmStudio.Domain;

namespace PgmStudio.Pgm;

/// <summary>XML attribute/text/coordinate helpers mirroring the Python ElementTree usage.</summary>
internal static class Xml
{
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
