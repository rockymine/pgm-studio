namespace PgmStudio.Pgm;

using Dict = Dictionary<string, object?>;

/// <summary>
/// Structural comparison of the JSON object trees produced by <see cref="Serializer.ToDict"/>,
/// matching Python dict/list equality semantics: objects compared order-independently, arrays
/// order-sensitively, numbers by value (so <c>0</c> == <c>0.0</c>). Plus <see cref="Canonical"/>,
/// which drops the derived <c>bounds_2d</c> from regions (the harness's idempotence comparison).
/// </summary>
public static class JsonTree
{
    public static bool DeepEquals(object? a, object? b)
    {
        if (a is null || b is null) return a is null && b is null;
        if (a is Dict da && b is Dict db)
        {
            if (da.Count != db.Count) return false;
            foreach (var (k, va) in da)
                if (!db.TryGetValue(k, out var vb) || !DeepEquals(va, vb)) return false;
            return true;
        }
        if (a is List<object?> la && b is List<object?> lb)
        {
            if (la.Count != lb.Count) return false;
            for (var i = 0; i < la.Count; i++)
                if (!DeepEquals(la[i], lb[i])) return false;
            return true;
        }
        if (a is bool ba && b is bool bb) return ba == bb;
        if (IsNumber(a) && IsNumber(b)) return ToD(a) == ToD(b);
        if (a is string sa && b is string sb) return sa == sb;
        return Equals(a, b);
    }

    /// <summary>Top-level copy with each region's derived <c>bounds_2d</c> removed.</summary>
    public static Dict Canonical(Dict top)
    {
        var outv = new Dict(top);
        if (top.GetValueOrDefault("regions") is Dict regions)
        {
            var stripped = new Dict();
            foreach (var (rid, rv) in regions)
            {
                var copy = new Dict((Dict)rv!);
                copy.Remove("bounds_2d");
                stripped[rid] = copy;
            }
            outv["regions"] = stripped;
        }
        return outv;
    }

    /// <summary>Keys whose top-level values differ (for failure diagnostics).</summary>
    public static List<string> DiffKeys(Dict a, Dict b)
        => a.Keys.Where(k => !DeepEquals(a[k], b.GetValueOrDefault(k))).ToList();

    private static bool IsNumber(object? v) => v is int or long or double or float or decimal;
    private static double ToD(object? v) => Convert.ToDouble(v, System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Parse a JSON string into the same object tree shape (Dict/List/string/long/double/bool/null).</summary>
    public static object? FromJson(string json)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        return FromElement(doc.RootElement);
    }

    /// <summary>
    /// Like <see cref="FromJson"/> but tolerant of Python's non-standard bare <c>NaN</c>/<c>Infinity</c>
    /// value tokens (emitted for some derived bounds) — they are nulled out. The lookarounds avoid
    /// touching those words inside quoted strings.
    /// </summary>
    public static object? FromJsonLenient(string json)
        => FromJson(System.Text.RegularExpressions.Regex.Replace(json, @"(?<![\w""])(-?Infinity|NaN)(?![\w""])", "null"));

    private static object? FromElement(System.Text.Json.JsonElement e)
    {
        switch (e.ValueKind)
        {
            case System.Text.Json.JsonValueKind.Object:
                var d = new Dict();
                foreach (var p in e.EnumerateObject()) d[p.Name] = FromElement(p.Value);
                return d;
            case System.Text.Json.JsonValueKind.Array:
                return e.EnumerateArray().Select(FromElement).ToList();
            case System.Text.Json.JsonValueKind.String:
                return e.GetString();
            case System.Text.Json.JsonValueKind.Number:
                return e.TryGetInt64(out var l) ? l : e.GetDouble();
            case System.Text.Json.JsonValueKind.True: return true;
            case System.Text.Json.JsonValueKind.False: return false;
            default: return null;
        }
    }
}
