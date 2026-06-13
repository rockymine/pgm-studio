namespace PgmStudio.Domain;

/// <summary>
/// A PGM coordinate value, as produced by the Python <c>parse_coord</c>:
/// <list type="bullet">
///   <item><c>null</c> — a template variable <c>${…}</c> (absent value)</item>
///   <item><c>double.PositiveInfinity</c>/<c>NegativeInfinity</c> — the literals <c>"oo"</c>/<c>"-oo"</c></item>
///   <item>a finite <see cref="double"/> — a normal numeric literal</item>
/// </list>
/// In the JSON tree a coordinate is therefore one of: <c>null</c>, the string <c>"oo"</c>/<c>"-oo"</c>,
/// or a number. <see cref="Coord"/> centralises that mapping (mirrors serializer <c>_coord</c> /
/// deserializer <c>_coord</c>).
/// </summary>
public static class Coord
{
    /// <summary>Parse a single PGM coordinate component (mirrors regions.parse_coord).</summary>
    public static double? Parse(string value)
    {
        value = value.Trim();
        if (value.Contains('$')) return null;          // ${var} or bare $ → None
        var lower = value.ToLowerInvariant();
        if (lower == "oo") return double.PositiveInfinity;
        if (lower == "-oo") return double.NegativeInfinity;
        if (double.TryParse(value, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out var d))
            return d;
        // Malformed literal → 0.0 (flag-and-continue, matching the Python warning path).
        return 0.0;
    }

    /// <summary>Encode a parsed coordinate into its JSON-tree value (number, "oo"/"-oo", or null).</summary>
    public static object? Encode(double? v)
    {
        if (v is null) return null;
        if (double.IsPositiveInfinity(v.Value)) return "oo";
        if (double.IsNegativeInfinity(v.Value)) return "-oo";
        return v.Value;
    }

    /// <summary>Decode a JSON-tree coordinate value back into a double (mirrors deserializer._coord).</summary>
    public static double Decode(object? v) => v switch
    {
        "oo" => double.PositiveInfinity,
        "-oo" => double.NegativeInfinity,
        null => throw new InvalidOperationException("null coordinate where a value was required"),
        _ => System.Convert.ToDouble(v, System.Globalization.CultureInfo.InvariantCulture),
    };
}
