using PgmStudio.E2e.Tests.Harness;

namespace PgmStudio.E2e.Tests;

/// <summary>
/// Icons render on the real pages, offline, as inline svg with geometry in it.
///
/// A blank icon raises no error of its own, so the smoke sweep can only see its absence; this asserts
/// positively, on the icon-dense routes (the nav rail and a canvas tool's toolbars), that icons were drawn,
/// that every one carries geometry, that no unrendered placeholder is left behind, and that nothing was
/// fetched from a CDN to draw them.
/// </summary>
[ClassDataSource<E2eSession>(Shared = SharedType.PerTestSession)]
public sealed class IconsSpec(E2eSession session)
{
    [Test, NotInParallel(Order = 6)]
    public async Task IconsRenderOnTheRealPages()
    {
        var seed = session.Seed;
        (string Path, string Name)[] routes =
        [
            ("/maps", "maps dashboard"),
            ($"/maps/{seed.SketchSlug}/sketch", "sketch tool"),
            ($"/maps/{seed.MapSlug}/configure", "configure tool"),
        ];

        var checks = new Checks("icons");
        await using var page = await session.NewPageAsync();

        checks.Section("icons render on the real pages");
        foreach (var route in routes)
        {
            page.ClearFaults();
            await page.GotoAsync(route.Path);
            await page.WaitForSelectorAsync("body", 20000);
            await page.TryWaitForFunctionAsync("() => document.querySelector('svg.lucide') !== null", 20000);

            var state = await page.EvaluateAsync("""
                () => ({
                  rendered: document.querySelectorAll("svg.lucide").length,
                  placeholders: Array.from(document.querySelectorAll("i[data-lucide]")).map(e => e.getAttribute("data-lucide")),
                  empty: Array.from(document.querySelectorAll("svg.lucide")).filter(s => s.children.length === 0)
                    .map(s => [...s.classList].find(c => c.startsWith("lucide-")) ?? "(unnamed)"),
                })
                """);
            var rendered = state.GetProperty("rendered").GetInt32();
            var placeholders = state.GetProperty("placeholders").EnumerateArray().Select(name => name.GetString()).ToList();
            var empty = state.GetProperty("empty").EnumerateArray().Select(name => name.GetString()).ToList();
            var fromCdn = page.Faults.Where(fault => fault.Contains("cdn.", StringComparison.Ordinal)).ToList();

            checks.Add($"{route.Name}: icons rendered", rendered > 0, $"{rendered} svg.lucide");
            checks.Add($"{route.Name}: no placeholder left behind", placeholders.Count == 0,
                string.Join(", ", placeholders.Take(5)));
            checks.Add($"{route.Name}: every icon has geometry", empty.Count == 0,
                $"{empty.Count} empty{(empty.Count > 0 ? $": {string.Join(", ", empty.Distinct().Take(5))}" : "")}");
            checks.Add($"{route.Name}: no icon fetched from a CDN", fromCdn.Count == 0, string.Join(" | ", fromCdn));
        }

        checks.Finish();
    }
}
