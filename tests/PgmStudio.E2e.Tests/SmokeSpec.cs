using PgmStudio.E2e.Tests.Harness;

namespace PgmStudio.E2e.Tests;

/// <summary>
/// The smoke sweep: every routable page loads, renders its shell, and raises nothing.
///
/// Deliberately the cheapest suite — no flows, no geometry, no numbers that can drift. It answers one question
/// per route, <i>is this page alive?</i>, which is the failure class a unit test cannot see: a WASM boot break
/// leaves every page on "Loading", a rename breaks a route, a phase host throws on mount.
/// </summary>
[ClassDataSource<E2eSession>(Shared = SharedType.PerTestSession)]
public sealed class SmokeSpec(E2eSession session)
{
    [Test, NotInParallel(Order = 13)]
    public async Task EveryRouteIsAlive()
    {
        var seed = session.Seed;
        // `Expect` names a selector that must exist once the page has settled — the page's "I rendered" proof.
        (string Path, string Name, string Expect)[] routes =
        [
            ("/", "landing", ".landing-choices a.card"),
            ("/maps", "maps dashboard", ".studio-shell, .workspace, main"),
            ("/maps?stage=sketch", "maps · sketch", "body"),
            ("/maps?stage=plan", "maps · plan", "body"),
            ("/maps?stage=configure", "maps · configure", "body"),
            ("/design", "design system", "body"),
            ("/generator", "generator", "body"),
            // the catalog proves itself by its cards: an empty grid is a dead enumerator, not a slow page
            ("/catalog", "shape catalog", ".lib-grid .lib-card"),
            // the library renders from the database, so an empty kind is still a live page — the chooser's
            // cards and a browse strip are the proof, since those are what a dead component would take down
            ("/library", "library chooser", ".lib-choices .card"),
            ("/library/styles", "style library", ".lib-page .lib-strip"),
            ("/library/themes", "theme library", ".lib-page .lib-strip"),
            ("/library/houses", "house library", ".lib-page .lib-strip"),
            // an editor page is the half a browse grid cannot prove: its outline is built from the draft
            ("/library/styles/new", "style editor", ".lib-outline-row"),
            ("/library/houses/new", "house editor", ".lib-outline-row"),
            ("/plan-editor", "plan editor (bare)", ".map-canvas-svg"),
            ($"/maps/{seed.PlanSlug}/plan", "plan tool", "body"),
            ($"/maps/{seed.SketchSlug}/sketch", "sketch tool", "body"),
            ($"/maps/{seed.MapSlug}/configure", "configure tool", "body"),
            ("/maps/new", "new map", "body"),
            ("/not-found", "not found", "body"),
            ("/definitely-not-a-route", "unknown route", "body"),
        ];

        var checks = new Checks("smoke");
        await using var page = await session.NewPageAsync();
        var tolerated = new HashSet<string>();

        checks.Section($"every route is alive ({Studio.Base})");
        foreach (var route in routes)
        {
            page.ClearFaults();
            var rendered = false;
            try
            {
                await page.GotoAsync(route.Path);
                // Blazor WASM boots after the network settles, so wait for the proof selector rather than a timeout.
                await page.WaitForSelectorAsync(route.Expect, 20000);
                rendered = true;
            }
            catch (Exception failure)
            {
                page.Note($"did not render \"{route.Expect}\": {failure.Message.Split('\n')[0]}");
            }
            tolerated.UnionWith(page.Allowed);

            // An icon the package does not have renders as an empty svg; it must not pass as a drawn one.
            var emptyIcons = await page.EvaluateAsync<int>(
                "() => [...document.querySelectorAll('svg.lucide')].filter(svg => svg.children.length === 0).length");

            checks.Add($"{route.Name} renders", rendered, rendered ? route.Path : $"{route.Path} — see faults");
            checks.Add($"{route.Name} is clean", page.Faults.Count == 0, string.Join(" | ", page.Faults.Take(3)));
            checks.Add($"{route.Name} draws no empty icon", emptyIcons == 0, $"{emptyIcons} empty svg.lucide");
        }

        // The app must not be showing Blazor's unhandled-error bar.
        page.ClearFaults();
        await page.GotoAsync("/maps");
        var errorUi = await page.EvaluateAsync<string>(
            "() => { const el = document.querySelector('#blazor-error-ui'); return el ? getComputedStyle(el).display : 'none'; }");
        checks.Add("no Blazor error bar", errorUi == "none", $"display: {errorUi}");

        if (tolerated.Count > 0)
        {
            Console.WriteLine("\ntolerated (see StudioPage.AllowedFaults):");
            foreach (var fault in tolerated) Console.WriteLine($"  · {fault}");
        }

        checks.Finish();
    }
}
