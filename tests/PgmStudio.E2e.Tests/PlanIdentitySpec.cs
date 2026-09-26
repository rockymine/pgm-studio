using System.Text.Json.Nodes;
using PgmStudio.E2e.Tests.Harness;

namespace PgmStudio.E2e.Tests;

/// <summary>
/// The plan editor shows the plan it names, and a built map keeps the plan it was built from.
///
/// Both halves are about a document arriving from somewhere it should not: the editor holds only what its
/// route loaded (never a copy the browser kept of another map), and a map built from the plan tool holds its
/// plan beside the layout it compiled into. Driven through the real UI, because what the browser holds at
/// mount is invisible to an API-level test.
/// </summary>
[ClassDataSource<E2eSession>(Shared = SharedType.PerTestSession)]
public sealed class PlanIdentitySpec(E2eSession session)
{
    [Test, NotInParallel(Order = 10)]
    public async Task TheEditorHoldsOnlyThePlanItsRouteNames()
    {
        var api = session.Api;
        var checks = new Checks("plan identity (G152)");
        await using var page = await session.NewPageAsync(1600, 1000);

        static int PieceCount(JsonNode? plan) => plan.Items("pieces").Count;

        // Two fresh plan maps, both blank on the server. `drawn` is then given a real plan so the browser has
        // something to remember; `blank` is left as the `{}` a New plan starts as.
        var drawn = (await api.Post("/plan", new { name = "e2e identity drawn" })).Text("slug")!;
        var blank = (await api.Post("/plan", new { name = "e2e identity blank" })).Text("slug")!;
        await api.Put($"/map/{drawn}/plan", session.Seed.PlanJson);

        checks.Section("a blank plan starts blank on the server");
        var drawnPieces = PieceCount(await api.Get($"/map/{drawn}/plan"));
        checks.Add("the drawn map has a plan", drawnPieces > 0, $"{drawnPieces} pieces");
        checks.Add("the blank map has none", PieceCount(await api.Get($"/map/{blank}/plan")) == 0, "{}");

        // ── the editor holds only what its route loaded ──
        checks.Section("opening one plan does not carry it into the next");

        async Task OpenPlan(string slug)
        {
            page.ClearFaults();
            await page.GotoAsync($"/maps/{slug}/plan");
            await page.WaitForSelectorAsync(".map-canvas-svg", 15000);
            await StudioPage.Pause(1200);   // the artifact load runs after first render
        }

        var drove = false;
        try
        {
            // The drawn plan first, so anything the editor caches has been cached by the time the blank one opens.
            await OpenPlan(drawn);
            checks.Add("the drawn plan opens without faults", page.Faults.Count == 0, string.Join(" | ", page.Faults.Take(3)));

            // No document key, whatever else the browser keeps. The keys that remain are UI preferences (overlay
            // chips, height-map fill, surface stepper).
            var keys = await page.EvaluateAsync<string[]>("() => Object.keys(localStorage).filter(k => k.startsWith('pgm-plan'))");
            checks.Add("no plan document is cached in the browser", !keys.Contains("pgm-plan-editor"),
                keys.Length > 0 ? string.Join(", ", keys) : "(nothing stored)");

            // Now the blank one. Saving immediately writes back exactly what the editor was holding.
            await OpenPlan(blank);
            await page.Locator("button", hasText: "Save").ClickAsync();
            await StudioPage.Pause(1500);

            var after = PieceCount(await api.Get($"/map/{blank}/plan"));
            checks.Add("the blank plan is still blank after opening a drawn one first", after == 0,
                $"{after} pieces — {(after > 0 ? "the previous plan followed the browser" : "empty")}");
            var stillDrawn = PieceCount(await api.Get($"/map/{drawn}/plan"));
            checks.Add("and the drawn plan is untouched", stillDrawn > 0, $"{stillDrawn} pieces");
            drove = true;
        }
        catch (Exception failure)
        {
            page.Note($"identity: {failure.Message.Split('\n')[0]}");
        }
        checks.Add("the identity checks ran", drove, string.Join(" | ", page.Faults.Take(3)));

        // ── a built map carries the plan it was built from ──
        // The seed's plan is a real composed board, so compiling it succeeds; building it from a map-backed route
        // puts layout, intent AND plan on the one row, which is what lets the overview offer both tools.
        checks.Section("building a draft records the plan on the map");

        var built = false;
        try
        {
            var target = (await api.Post("/plan", new { name = "e2e identity built" })).Text("slug")!;
            await api.Put($"/map/{target}/plan", session.Seed.PlanJson);
            await OpenPlan(target);

            await page.Locator("button", hasText: "Compile").ClickAsync();
            await page.WaitForSelectorAsync(".plan-compile-json", 20000);
            await page.Locator("button", hasText: "Build the map").ClickAsync();   // a first build — no rebuild confirmation
            // Scoped to the drawer: the Score panel uses the same class for "No rules fired".
            await page.WaitForSelectorAsync(".plan-compile-draft .plan-lint-ok", 90000);

            var planPieces = PieceCount(await api.Get($"/map/{target}/plan"));
            checks.Add("the built map still holds its plan", planPieces > 0, $"{planPieces} pieces");
            var layout = await api.Get($"/map/{target}/sketch");
            checks.Add("…beside the layout it compiled into", (layout?["layers"] ?? layout?["layout"]) != null,
                "both blobs on the one row — what lets the overview link either tool");
            built = true;
        }
        catch (Exception failure)
        {
            page.Note($"build: {failure.Message.Split('\n')[0]}");
        }
        checks.Add("the build checks ran", built, string.Join(" | ", page.Faults.Take(3)));

        checks.Finish();
    }
}
