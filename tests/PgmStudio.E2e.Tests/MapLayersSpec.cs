using System.Text.Json.Nodes;
using PgmStudio.E2e.Tests.Harness;

namespace PgmStudio.E2e.Tests;

/// <summary>
/// A map's authoring layers are all visible, and all one click away.
///
/// A map is one row that can hold a plan, a sketch and a world at once. Two claims: the lists that name a layer
/// list every map holding it, whatever the map has since become; and a row offers each layer directly, without
/// moving the map — the stage pointer is progress, not a cursor.
/// </summary>
[ClassDataSource<E2eSession>(Shared = SharedType.PerTestSession)]
public sealed class MapLayersSpec(E2eSession session)
{
    [Test, NotInParallel(Order = 7)]
    public async Task EveryLayerIsListedAndOneClickAway()
    {
        var api = session.Api;
        var seed = session.Seed;
        var checks = new Checks("map layers");

        // A composed plan carried all the way to world geometry: it holds all three layers and stands at configure.
        var slug = seed.MapSlug;
        static JsonNode? Find(JsonNode? list, string slug) =>
            list?.AsArray().FirstOrDefault(map => map.Text("slug") == slug);

        checks.Section("a layer list shows every map holding that layer");

        var plans = await api.Get("/maps?stage=plan");
        var sketches = await api.Get("/maps?stage=sketch");
        var configuring = await api.Get("/maps?stage=configure");

        checks.Add("the built map is in Plans, though its stage moved on", Find(plans, slug) != null,
            $"stage={Find(configuring, slug).Text("stage") ?? "?"} — listed by the plan it holds, not by where it stands");
        checks.Add("…and in Sketches", Find(sketches, slug) != null);
        checks.Add("…and in Configuring, where it stands", Find(configuring, slug) != null);

        var entry = Find(configuring, slug);
        static string Layers(JsonNode? map) =>
            $"plan={Flag(map, "hasPlan")} sketch={Flag(map, "hasSketch")} world={Flag(map, "hasSurface")}";
        checks.Add("its row carries all three layers",
            entry.Truthy("hasPlan") && entry.Truthy("hasSketch") && entry.Truthy("hasSurface"), Layers(entry));

        // A plan-only map has exactly one layer — the flags are the map's own history, not a constant.
        var planOnly = Find(plans, seed.PlanSlug);
        checks.Add("a plan that was never built carries only its plan",
            planOnly != null && planOnly.Truthy("hasPlan") && !planOnly.Truthy("hasSketch") && !planOnly.Truthy("hasSurface"),
            Layers(planOnly));

        // ── the row offers each layer, and reaching one does not move the map ──
        checks.Section("every layer is one click, and looking changes nothing");

        await using var page = await session.NewPageAsync(1600, 1000);
        var drove = false;
        try
        {
            page.ClearFaults();
            await page.GotoAsync("/maps?stage=configure");
            await page.WaitForSelectorAsync(".panel-list .list-row", 15000);

            var row = page.Locator(".list-row-pair").Has("*", slug, exact: true).First();
            var layers = row.Locate(".map-layer");
            var hrefs = await layers.EvaluateAllAsync<string?[]>("els => els.map(e => e.getAttribute('href'))");
            string[] expected = [$"maps/{slug}/plan", $"maps/{slug}/sketch", $"maps/{slug}/configure"];
            checks.Add("the row links plan, sketch and configure", hrefs.SequenceEqual(expected), string.Join(" · ", hrefs));

            var now = await layers.EvaluateAllAsync<string[]>(
                "els => els.filter(e => e.classList.contains('map-layer--now')).map(e => e.textContent.trim())");
            checks.Add("the layer the map stands at is marked", now.Length == 1 && now[0] == "Configure",
                now.Length > 0 ? string.Join(",", now) : "(none marked)");

            // One backward hop with a hard-coded preference order is not a control this page offers.
            var reopen = await page.Locator("button", hasText: "Reopen").CountAsync();
            checks.Add("no reopen control remains", reopen == 0, $"{reopen} found");

            // Straight to the plan from the Configuring list — one click, no intermediate list. The bridge polls
            // the derived reads once it holds a document, so a successful /plan/inspect means the whole feed has
            // settled and the fault check below reads a finished page. Armed before the click, because it can
            // answer before the wait.
            var loaded = page.WaitForOkResponseAsync("/api/plan/inspect");
            await row.Locate(".map-layer", hasText: "Plan").ClickAsync();
            await page.WaitForUrlEndingAsync($"/maps/{slug}/plan", 15000);
            await page.WaitForSelectorAsync(".map-canvas-svg", 20000);
            await loaded;
            checks.Add("the plan editor opens on the built map", page.Url.EndsWith($"/maps/{slug}/plan", StringComparison.Ordinal), page.Url);
            checks.Add("opening it raised no faults", page.Faults.Count == 0, string.Join(" | ", page.Faults.Take(3)));

            // Looking at the plan of a configured map leaves it configured, so nothing downstream is rebuilt or
            // re-listed by a visit.
            var after = Find(await api.Get("/maps?stage=configure"), slug);
            checks.Add("the map is still where it was", after != null && after.Text("stage") == "configure",
                after != null ? $"stage={after.Text("stage")}" : "no longer listed under Configuring");
            drove = true;
        }
        catch (Exception failure)
        {
            page.Note($"layers: {failure.Message.Split('\n')[0]}");
        }
        checks.Add("the layer checks ran", drove, string.Join(" | ", page.Faults.Take(3)));

        // ── the build says which of the two things it is about to do ──
        checks.Section("a rebuild asks; a first build does not");

        var asked = false;
        try
        {
            // Standing in the plan of the already-built map: the same button now means "replace the board someone
            // has been working on", so it states the trade instead of just doing it.
            await page.Locator("button", hasText: "Compile").ClickAsync();
            await page.WaitForSelectorAsync(".plan-compile-json", 20000);
            // The json pane shows whatever the compile answered; the draft button leaving its disabled state is
            // what says the compile succeeded, and waiting for it fails with the compile's own findings.
            await page.WaitForSelectorAsync(".plan-compile-draft button:not([disabled])", 20000);

            var label = (await page.Locator(".plan-compile-draft button").First().TextContentAsync()).Trim();
            checks.Add("the button names a rebuild, not a build", label.Contains("Rebuild this map", StringComparison.Ordinal), label);

            await page.Locator("button", hasText: "Rebuild this map").ClickAsync();
            await page.WaitForSelectorAsync(".plan-rebuild-warn", 5000);
            var warn = await page.TextContentAsync(".plan-rebuild-warn");
            checks.Add("it names what is replaced",
                warn.Contains("Replaces", StringComparison.Ordinal) && warn.Contains("terrain", StringComparison.Ordinal),
                "board + structure");
            checks.Add("…and what is kept",
                warn.Contains("Keeps", StringComparison.Ordinal) && warn.Contains("themes", StringComparison.Ordinal)
                && warn.Contains("authors", StringComparison.Ordinal),
                "themes, room shells, dressing, authors");

            // Cancelling is a real exit: the map is untouched and the button is back.
            await page.Locator("button", hasText: "Cancel").ClickAsync();
            await page.WaitForSelectorAsync(".plan-rebuild-warn", 5000, hidden: true);
            var untouched = Find(await api.Get("/maps?stage=configure"), slug);
            checks.Add("cancelling rebuilds nothing", untouched != null && untouched.Text("stage") == "configure",
                "the map is where it was");

            // A plan that was never built has nothing to lose, so it is not interrupted.
            var planLoaded = page.WaitForOkResponseAsync("/api/plan/inspect");
            await page.GotoAsync($"/maps/{seed.PlanSlug}/plan");
            await page.WaitForSelectorAsync(".map-canvas-svg", 15000);
            await planLoaded;
            await page.Locator("button", hasText: "Compile").ClickAsync();
            await page.WaitForSelectorAsync(".plan-compile-json", 20000);
            var first = (await page.Locator(".plan-compile-draft button").First().TextContentAsync()).Trim();
            checks.Add("a first build is offered plainly", first.Contains("Build the map", StringComparison.Ordinal), first);
            asked = true;
        }
        catch (Exception failure)
        {
            page.Note($"confirm: {failure.Message.Split('\n')[0]}");
        }
        checks.Add("the confirmation checks ran", asked, string.Join(" | ", page.Faults.Take(3)));

        checks.Finish();
    }

    private static string Flag(JsonNode? map, string key) => map?[key]?.ToJsonString() ?? "undefined";
}
