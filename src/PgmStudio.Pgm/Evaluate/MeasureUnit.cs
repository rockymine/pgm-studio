using System.Globalization;

namespace PgmStudio.Pgm.Evaluate;

/// <summary>What a soft term's value is counted in, which decides how a rule text and a finding message state
/// it: a distance or a width in blocks, a width in plan cells, a share as a percentage, a ratio as a number of
/// times, a count as a plain number.</summary>
public enum MeasureUnit { Blocks, Cells, Percent, Times, Count }

/// <summary>The one way a measured number and a band's edge are written, so a finding's message and the rule
/// it cites state the same number the same way.</summary>
public static class Measures
{
    /// <summary>A band edge as its rule states it: a share to the half percent, a ratio to two decimals, a
    /// count's upper edge down to a whole number, a length to the whole block or cell.</summary>
    public static string Bound(double value, MeasureUnit unit) => Number(Edge(value, unit), unit, EdgeFormat(unit));

    /// <summary>A band as its rule states it, the unit once at the end: "between 20 and 54 percent".</summary>
    public static string Between(double low, double high, MeasureUnit unit) =>
        $"between {Edge(low, unit).ToString(EdgeFormat(unit), CultureInfo.InvariantCulture)} and {Bound(high, unit)}";

    private static double Edge(double value, MeasureUnit unit) => unit switch
    {
        MeasureUnit.Percent => Math.Round(value * 200) / 2,
        MeasureUnit.Times => Math.Round(value, 2),
        MeasureUnit.Count => Math.Floor(value),
        _ => Math.Round(value),
    };

    private static string EdgeFormat(MeasureUnit unit) => unit switch
    {
        MeasureUnit.Percent => "0.#",
        MeasureUnit.Times => "0.##",
        _ => "0",
    };

    /// <summary>A measured value, one digit finer than a band edge so the two never read alike when the value is
    /// just past it: "28.6 blocks, less than 29 blocks".</summary>
    public static string Value(double value, MeasureUnit unit) => unit switch
    {
        MeasureUnit.Percent => Number(Math.Round(value * 100, 2), unit, "0.##"),
        MeasureUnit.Times => Number(Math.Round(value, 3), unit, "0.###"),
        _ => Number(Math.Round(value, 1), unit, "0.#"),
    };

    private static string Number(double value, MeasureUnit unit, string format)
    {
        var number = value.ToString(format, CultureInfo.InvariantCulture);
        var one = number == "1";
        return unit switch
        {
            MeasureUnit.Percent => $"{number} percent",
            MeasureUnit.Times => $"{number} times",
            MeasureUnit.Cells => one ? "1 cell" : $"{number} cells",
            MeasureUnit.Blocks => one ? "1 block" : $"{number} blocks",
            _ => number,
        };
    }
}
