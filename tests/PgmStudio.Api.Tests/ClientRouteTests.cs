using System.Text.Json;
using System.Text.RegularExpressions;

namespace PgmStudio.Api.Tests;

/// <summary>
/// Every route the Blazor client names is a route the API serves.
///
/// <para><b>The route string is what stays hand-written.</b> The response shapes come from
/// <c>Contracts</c> and are typed at the call site, so a renamed DTO field is a compile error; a mistyped
/// path is not, and reaches the browser as a 404 that reads like a missing map. This is the gate over that
/// one gap, and it is the reason the studio has no generated client: what a generated one would buy over
/// the typed reads already in place is exactly this check, at the price of a build-time package and a second
/// copy of the whole surface committed to the tree.</para>
///
/// <para>Only a string naming a <b>whole</b> route is checked. Three Edit phases compose theirs from a
/// prefix stated once and a tail passed at the call site, and neither half is a route on its own — that is
/// <c>C47</c>, and until it lands those routes are the ones nothing here can read.</para>
/// </summary>
[NotInParallel("api-db")]
public sealed class ClientRouteTests
{
    [Test]
    public async Task Every_route_the_client_calls_is_one_the_api_serves()
    {
        var served = await ServedAsync();
        var unknown = ClientRoutes()
            .Where(route => !served.Contains(route.Path))
            .Select(route => $"{route.Where}: {route.Raw}")
            .ToList();

        await Assert.That(unknown).IsEmpty()
            .Because($"{unknown.Count} route(s) the client calls are not in the schema:"
                     + $"{Environment.NewLine}  {string.Join($"{Environment.NewLine}  ", unknown)}");
    }

    /// <summary>A guard on the harness: a reader that matched nothing would pass the test above by having
    /// nothing to check.</summary>
    [Test]
    public async Task The_client_names_its_routes_as_whole_strings()
    {
        await Assert.That(ClientRoutes().Select(route => route.Path).Distinct().Count()).IsGreaterThan(70);
    }

    /// <summary>The one string in the client that names half a route. Three Edit phases each declare
    /// <c>Post</c>, <c>Patch</c> and <c>Delete</c> over this prefix and take the tail from the call site, so
    /// neither half is a route and the twenty-one routes they reach are the ones nothing here can read. It is
    /// spelled identically in all three, which is what lets it be named once — and
    /// <see cref="The_only_half_route_is_the_one_C47_names"/> fails when <c>C47</c> puts those routes in one
    /// place, so the exception cannot outlive its reason.</summary>
    private const string Composed = "api/map/{Slug}/{path}";

    [Test]
    public async Task The_only_half_route_is_the_one_C47_names()
    {
        var files = Directory.EnumerateFiles(ClientRoot(), "*.*", SearchOption.AllDirectories)
            .Where(file => file.EndsWith(".cs") || file.EndsWith(".razor"))
            .Where(file => !file.Contains("/obj/") && !file.Contains("/bin/"))
            .Where(file => File.ReadAllText(file).Contains(Composed))
            .Select(Path.GetFileName)
            .Order()
            .ToList();

        await Assert.That(files).IsEquivalentTo(
            (string[])["BuildRegionsPhase.razor.cs", "ObjectivePhase.razor.cs", "TeamsPhase.razor.cs"])
            .Because("the composed-route exception is named for three Edit phases and nothing else; a fourth "
                     + "file spreading it, or C47 landing and removing it, changes what this test excludes");
    }

    private readonly record struct Call(string Path, string Raw, string Where);

    /// <summary>Every <c>/api</c> path the schema publishes, with each route parameter reduced to a hole: the
    /// client writes the value it interpolates where the route writes the parameter's name, and neither is
    /// the point.</summary>
    private static async Task<HashSet<string>> ServedAsync()
    {
        using var client = ApiTestFactory.Shared.CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/api/openapi/v1.json"));
        return
        [
            .. document.RootElement.GetProperty("paths").EnumerateObject().Select(path => Hollow(path.Name))
        ];
    }

    /// <summary>
    /// Every string in the client that names a route, as the path it resolves to.
    ///
    /// <para>A literal and an interpolated string are read the same way, because <c>$</c> sits outside the
    /// quotes and what it holds is a value standing where a route parameter stands. A query string is the
    /// caller's and is cut; the client writes the path with no leading slash about as often as with one, and
    /// the API serves the same route either way.</para>
    /// </summary>
    private static List<Call> ClientRoutes()
    {
        var calls = new List<Call>();
        foreach (var path in Directory.EnumerateFiles(ClientRoot(), "*.*", SearchOption.AllDirectories)
                     .Where(file => file.EndsWith(".cs") || file.EndsWith(".razor"))
                     .Where(file => !file.Contains("/obj/") && !file.Contains("/bin/"))
                     .OrderBy(file => file))
        {
            var lines = File.ReadAllLines(path);
            for (var i = 0; i < lines.Length; i++)
                foreach (Match match in Regex.Matches(lines[i], @"""(/?api/[^""]*)"""))
                {
                    var raw = match.Groups[1].Value;
                    if (raw == Composed) continue;
                    var route = raw.StartsWith('/') ? raw : '/' + raw;
                    var query = route.IndexOf('?');
                    if (query >= 0) route = route[..query];
                    calls.Add(new Call(Hollow(route), raw, $"{Path.GetFileName(path)}:{i + 1}"));
                }
        }
        return calls;
    }

    private static string Hollow(string route) =>
        Regex.Replace(route.ToLowerInvariant().TrimEnd('/'), @"\{[^}]*\}", "{}");

    private static string ClientRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "PgmStudio.Client")))
            dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new DirectoryNotFoundException(
            "no src/PgmStudio.Client above the test output — the repository layout moved"),
            "src", "PgmStudio.Client");
    }
}
