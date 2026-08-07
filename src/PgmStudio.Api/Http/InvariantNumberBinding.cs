using System.Globalization;
using FastEndpoints;
using Microsoft.Extensions.Primitives;

namespace PgmStudio.Api.Http;

/// <summary>
/// Makes the model binder read every fractional number off the wire as dot-separated, whatever the host's
/// regional settings are.
///
/// <para>JSON bodies are already safe — System.Text.Json is invariant by definition. Query, route, form,
/// header and claim values are not: the default binder converts them through a <c>TypeConverter</c>, which
/// reads <see cref="CultureInfo.CurrentCulture"/>. On a comma-decimal host that makes the '.' a
/// <em>group</em> separator, so <c>?leader=0.55</c> binds as fifty-five — a hundredfold error that is silent,
/// because it produces a perfectly valid number. The same request on a dot-decimal host binds correctly, so
/// the bug is invisible to everyone who does not run under the affected locale.</para>
///
/// <para>A process-wide culture pin (<see cref="CultureInfo.DefaultThreadCurrentCulture"/>, set at startup)
/// covers this too, but only for as long as nothing else moves it — it is ambient state any library may
/// write. These parsers make the wire boundary itself invariant, which is where the guarantee belongs.
/// Integers are registered alongside the fractional types: a group separator changes how those parse too.</para>
/// </summary>
public static class InvariantNumberBinding
{
    /// <summary>Register an invariant parser for every numeric type the binder may be asked for. Nullable
    /// forms are registered as well — the binder looks the parser up by the property's own type, so
    /// <c>double?</c> does not find <c>double</c>'s.</summary>
    public static void UseInvariantNumbers(this BindingOptions binding)
    {
        Register<double>(binding, static text => (double.TryParse(text, Fractional, Invariant, out var v), v));
        Register<float>(binding, static text => (float.TryParse(text, Fractional, Invariant, out var v), v));
        Register<decimal>(binding, static text => (decimal.TryParse(text, Fractional, Invariant, out var v), v));
        Register<int>(binding, static text => (int.TryParse(text, Whole, Invariant, out var v), v));
        Register<long>(binding, static text => (long.TryParse(text, Whole, Invariant, out var v), v));
        Register<short>(binding, static text => (short.TryParse(text, Whole, Invariant, out var v), v));
    }

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    private const NumberStyles Fractional = NumberStyles.Float;
    private const NumberStyles Whole = NumberStyles.Integer;

    /// <summary>One type and its nullable twin. An absent or blank value parses as null for the nullable
    /// form rather than failing, so an omitted optional parameter stays omitted.</summary>
    private static void Register<T>(BindingOptions binding, Func<string, (bool Ok, T Value)> parse)
        where T : struct
    {
        binding.ValueParserFor<T>(input => Parse(input, parse) is { } value ? new(true, value) : new(false, null));
        binding.ValueParserFor<T?>(input => string.IsNullOrWhiteSpace(input.ToString())
            ? new(true, null)
            : Parse(input, parse) is { } value ? new(true, value) : new(false, null));
    }

    private static object? Parse<T>(StringValues input, Func<string, (bool Ok, T Value)> parse) where T : struct
    {
        var (ok, value) = parse(input.ToString().Trim());
        return ok ? value : null;
    }
}
