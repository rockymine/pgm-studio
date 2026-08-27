using System.Text.Json;
using System.Text.RegularExpressions;
using PgmStudio.Client.Components;

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
/// <para>Only a string naming a <b>whole</b> route is checked, and every route the client calls is written
/// as one: the Edit tool's writes are named operations on <c>MapEdits</c>, each carrying its own literal, so
/// there is no half-route for this to be blind to.</para>
///
/// <para>One route is written per <b>kind</b> rather than per path — the six libraries differ only in a stem,
/// and <c>TerrainLibraryClient</c> hangs one verb off <see cref="LibraryKind.Route"/> rather than repeating
/// itself six times. A template naming that stem is expanded against every kind, so consolidating the client
/// costs the check nothing: all six paths are still asserted, one per library.</para>
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
        await Assert.That(ClientRoutes().Select(route => route.Path).Distinct().Count()).IsGreaterThan(80);
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
                    var route = raw.StartsWith('/') ? raw : '/' + raw;
                    var query = route.IndexOf('?');
                    if (query >= 0) route = route[..query];
                    var where = $"{Path.GetFileName(path)}:{i + 1}";
                    foreach (var one in PerKind(route))
                        calls.Add(new Call(Hollow(one), raw, where));
                }
        }
        return calls;
    }

    /// <summary>A route written against a library kind's stem, once per kind that serves it; anything else,
    /// once as itself. Two verbs are not every kind's — a style previews as a bare material rather than as a
    /// row, and only a theme and a house compose to a document — so the descriptor says which, and expanding
    /// over the rest would assert routes the API is right not to serve.</summary>
    private static IEnumerable<string> PerKind(string route)
    {
        if (!route.Contains(KindStem)) return [route];
        var kinds = route.EndsWith("/preview") ? LibraryKinds.All.Where(kind => kind.DraftPreview)
            : route.EndsWith("/json") ? LibraryKinds.All.Where(kind => kind.Composed)
            : LibraryKinds.All;
        return kinds.Select(kind => route.Replace(KindStem, kind.Route));
    }

    private const string KindStem = "{kind.Route}";

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
