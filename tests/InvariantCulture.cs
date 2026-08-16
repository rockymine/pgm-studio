using System.Globalization;
using System.Runtime.CompilerServices;

namespace PgmStudio.Tests;

/// <summary>
/// Every test project runs the same dot-separated numbers the product does.
///
/// <para><c>Program.cs</c> pins <see cref="CultureInfo.InvariantCulture"/> on the way up, because the studio
/// parses and formats numbers on three boundaries that must agree byte for byte — query and route values,
/// <c>map.xml</c> attributes and SVG geometry — and each defaults to the ambient culture. A test host makes no
/// such decision, so without this the suite exercises the code under a culture the product never runs under:
/// green on a dot-decimal machine and red on a comma-decimal one, for reasons that have nothing to do with
/// the code under test. That is not hypothetical — <c>RuleBandDriftTests</c> formats a rule's band and compares
/// it against the prose that states it, and read `[3,0, 4,0]` against `[3.0, 4.0]` on a German-locale box.</para>
///
/// <para>A module initializer rather than a per-suite hook, so it is in force before anything is discovered,
/// and compiled into every test project by <c>tests/Directory.Build.props</c> so no project can be the one
/// that forgot.</para>
/// </summary>
internal static class InvariantCulture
{
    [ModuleInitializer]
    internal static void Pin()
    {
        var invariant = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentCulture = invariant;
        CultureInfo.DefaultThreadCurrentUICulture = invariant;
        CultureInfo.CurrentCulture = invariant;
        CultureInfo.CurrentUICulture = invariant;
    }
}
