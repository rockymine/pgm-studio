using System.Text.Json.Nodes;
using PgmStudio.E2e.Tests.Harness;

namespace PgmStudio.E2e.Tests;

/// <summary>
/// The sketch tool's Theme phase — the paint pass, keyed on shapes and islands.
/// <list type="number">
/// <item>A layout carrying a theme registry, a map default and a per-shape override survives PUT → GET through
/// the real sketch endpoint, the storage the export resolves paint from.</item>
/// <item>The phase renders as one step — the island tree, the swatch strip at the foot of the canvas, the
/// inspector — and a theme taken in hand from the strip paints a shape with a click.</item>
/// <item>The phase edits nothing: its dock is select + move, and a drag moves no geometry.</item>
/// <item>The room shells bind from the inspector's board defaults, and what is stored is the snapshot.</item>
/// </list>
/// </summary>
[ClassDataSource<E2eSession>(Shared = SharedType.PerTestSession)]
public sealed class ThemingSpec(E2eSession session)
{
    [Test, NotInParallel(Order = 16)]
    public async Task TheThemePhasePaintsAndEditsNothing()
    {
        var api = session.Api;
        var slug = session.Seed.SketchSlug;
        var checks = new Checks("theming (sketch · Theme phase)");

        // ── 1. themes round-trip through the sketch layout ──
        checks.Section("a themed sketch layout survives PUT → GET");

        var layout = await api.Get($"/map/{slug}/sketch");
        var firstShapeId = layout?["layers"]?[0]?["layout"].Items("shapes").FirstOrDefault(shape => !shape.Truthy("role")).Text("id");
        var themed = layout!.DeepClone();
        static JsonObject Recipe(int surfaceId, int fillId) => new()
        {
            ["bedrock"] = new JsonObject { ["relative"] = false, ["value"] = 1 },
            ["surface"] = new JsonObject
            {
                ["material"] = new JsonObject { ["kind"] = "solid", ["id"] = surfaceId }, ["depth"] = 1, ["enabled"] = true,
            },
            ["fill"] = new JsonObject { ["kind"] = "solid", ["id"] = fillId },
        };
        // Two, because the brush is only testable against a theme no shape already carries.
        themed["themes"] = new JsonObject { ["forest"] = Recipe(2, 3), ["scar"] = Recipe(1, 1) };   // grass — a green the overlay shows
        themed["mapTheme"] = "forest";
        if (firstShapeId != null)
        {
            themed["layers"]![0]!["layout"].Items("shapes").First(shape => shape.Text("id") == firstShapeId)["theme"] = "forest";
        }
        await api.Put($"/map/{slug}/sketch", themed);

        var back = await api.Get($"/map/{slug}/sketch");
        checks.Add("theme registry persisted", back?["themes"]?["forest"] != null, back?["themes"].Keys() ?? "[]");
        checks.Add("map default persisted", back.Text("mapTheme") == "forest", $"mapTheme={back.Text("mapTheme")}");
        checks.Add("per-shape override persisted",
            firstShapeId == null
            || back?["layers"]?[0]?["layout"].Items("shapes").FirstOrDefault(shape => shape.Text("id") == firstShapeId).Text("theme") == "forest",
            firstShapeId != null ? $"shape {firstShapeId}" : "(no terrain shape to theme)");

        // A library row for §4 to bind, made before the page loads: the inspector reads the room library when
        // the Theme phase opens. Given a wall height no built-in shell has, so finding it in the layout says the
        // snapshot travelled. No courses: a part with none keeps the built-in finish.
        var style = await api.Post("/room-styles", new JsonObject
        {
            ["name"] = "e2e-tall", ["floorDepth"] = 1, ["wallHeight"] = 11, ["roofThickness"] = 1,
            ["roofForm"] = "flat", ["roofHole"] = true, ["door"] = "stained-glass-pane", ["doorHeight"] = 3,
            ["windows"] = new JsonObject { ["form"] = "none", ["block"] = 0, ["data"] = 0, ["sill"] = 0, ["width"] = 0, ["height"] = 0, ["spacing"] = 0 },
            ["storeyStack"] = new JsonArray(), ["courses"] = new JsonArray(),
        });
        var styleId = style.Number("id");
        checks.Add("a room style exists to bind", styleId > 0, $"id={styleId}");

        // ── 2. the Theme phase renders, and the strip is a brush ──
        checks.Section("the sketch Theme phase renders");

        await using var page = await session.NewPageAsync(1600, 1000);

        page.ClearFaults();
        var drove = false;
        try
        {
            await page.GotoAsync($"/maps/{slug}/sketch");
            await page.WaitForSelectorAsync("canvas", 20000);
            await StudioPage.Pause(1500);

            // Enter the Theme phase (the palette nav button). One step, so there is nothing to advance through.
            await page.ClickAsync("button[title=\"Theme\"]", 8000);
            await StudioPage.Pause(1200);
            await page.ScreenshotAsync("theme-phase.png");

            // The registry is chrome: one swatch per board theme, the map default badged, no apply button anywhere.
            var swatches = await page.Locator(".canvas-theme-swatch").AllInnerTextsAsync();
            checks.Add("the strip carries the board's themes", swatches.Length == 2, string.Join(" | ", swatches));
            checks.Add("the map default is badged on its swatch",
                await page.Locator(".canvas-theme-swatch", hasText: "forest").Locate(".canvas-theme-swatch-tag").CountAsync() == 1);
            // Nothing in the strip promises a chord the tool does not answer: the number keys are the phases'.
            checks.Add("no swatch claims a number key", await page.Locator(".canvas-theme-swatch-key").CountAsync() == 0);
            checks.Add("nothing is applied by a button", await page.Locator("button", hasText: "Apply").CountAsync() == 0);

            // With nothing selected the inspector describes the board rather than a selection.
            checks.Add("the inspector shows the board's defaults with nothing selected",
                await page.ShowsTextAsync("Board defaults") && await page.ShowsTextAsync("Default theme"));

            // The Blocks overlay is the phase's own, on without being asked for: the paint is what the phase acts on.
            checks.Add("Blocks is on when the phase opens",
                await page.Locator("button.canvas-chip", hasText: "Blocks").First()
                    .EvaluateAsync<bool>("el => el.classList.contains('canvas-chip--on')"));
            // The contour chip is not offered, because the painted ground already carries the height.
            checks.Add("the contour chip is not offered here",
                await page.Locator("button.canvas-chip", hasText: "Relief").CountAsync() == 0);

            // Take the unassigned theme in hand and paint a shape with a click on the board.
            await page.Locator(".canvas-theme-swatch", hasText: "scar").ClickAsync();
            await StudioPage.Pause(500);
            checks.Add("a swatch clicked is in hand",
                await page.Locator(".canvas-theme-swatch--on").CountAsync() == 1 && await page.ShowsTextAsync("In hand"));

            // Aim at a shape the layout carries rather than at a fraction of the viewport: where the board sits on
            // screen is the fit's business, and a click that misses proves nothing about the brush.
            var shapes = (await api.Get($"/map/{slug}/sketch")).TerrainShapes();
            var aim = await Aim.WorldAimerAsync(page);
            checks.Add("the canvas reports where a world block is", aim != null);
            async Task<int> ScarCount() =>
                (await api.Get($"/map/{slug}/sketch")).Items("layers").SelectMany(item => item["layout"].Items("shapes"))
                    .Count(shape => shape.Text("theme") == "scar");
            var scarred = 0;
            foreach (var point in shapes.SelectMany(shape => Aim.ShapePoints(shape)))
            {
                if (aim == null) break;
                var at = aim(point.X, point.Z);
                await page.Mouse.ClickAsync((decimal)at.X, (decimal)at.Y);
                await StudioPage.Pause(1400);   // past the autosave debounce
                scarred = await ScarCount();
                if (scarred >= 1) break;
            }
            await page.ScreenshotAsync("theme-painted.png");
            checks.Add("a click on the board painted a shape", scarred >= 1, $"{scarred} shape(s) → scar");
            drove = true;
        }
        catch (Exception failure)
        {
            page.Note($"theme phase drive: {failure.Message.Split('\n')[0]}");
        }
        checks.Add("Theme phase drove without error", drove);
        checks.Add("sketch tool is clean under theming", page.Faults.Count == 0, string.Join(" | ", page.Faults.Take(3)));

        // ── 3. the Theme phase is selection-only ──
        // Editing geometry belongs to the Draw phase. Theme assigns paint to shapes it does not own, so its canvas
        // offers exactly two things: pick something, and move the view. The dock is the visible half of that
        // contract and a drag on a selected island the load-bearing half.
        checks.Section("the Theme phase edits nothing");

        page.ClearFaults();
        var restricted = false;
        string?[] tools = [];
        try
        {
            // Still in the Theme phase. Put the brush down first: with one in hand a click paints rather than selects.
            await page.Keyboard.PressAsync("Escape");
            await StudioPage.Pause(300);
            tools = await page.Locator(".canvas-dock .canvas-dock-btn").EvaluateAllAsync<string?[]>(
                "els => els.map(el => el.getAttribute('aria-label'))");

            // A drag across the selected island must pan the view, not move the island.
            var before = (await api.Get($"/map/{slug}/sketch"))?.ToJsonString();
            var box = (await page.Locator("canvas").First().BoundingBoxAsync())!.Value;
            var centreX = box.X + box.Width / 2;
            var centreY = box.Y + box.Height / 2;
            await page.Mouse.MoveAsync((decimal)centreX, (decimal)centreY);
            await page.Mouse.DownAsync();
            await page.Mouse.MoveAsync((decimal)(centreX + 90), (decimal)(centreY + 60), new PuppeteerSharp.Input.MoveOptions { Steps = 12 });
            await page.Mouse.UpAsync();
            await StudioPage.Pause(1800);   // past the autosave debounce
            var after = (await api.Get($"/map/{slug}/sketch"))?.ToJsonString();
            checks.Add("dragging a selection moves no geometry", before == after,
                before == after ? "layout byte-identical" : "the layout changed under a Theme-phase drag");
            restricted = true;
        }
        catch (Exception failure)
        {
            page.Note($"select-only: {failure.Message.Split('\n')[0]}");
        }
        checks.Add("the dock offers select + move only", tools.Length == 2, tools.Length > 0 ? string.Join(" | ", tools) : "(no dock)");
        checks.Add("select-only checks ran", restricted, string.Join(" | ", page.Faults.Take(3)));

        // ── 4. the board defaults bind a room shell ──
        // A shell is a fallback in the sense the map default is: one for every wool room and one for every spawn
        // room, snapshotted into the layout rather than referenced, and bound from the inspector's board
        // defaults. The export reads the layout and nothing else, so the snapshot itself has to survive.
        checks.Section("the board defaults bind a room shell");

        page.ClearFaults();
        var bound = false;
        try
        {
            // The board defaults show only with nothing selected, so clear whatever the paint above left picked.
            await page.Keyboard.PressAsync("Escape");
            await page.Keyboard.PressAsync("Escape");
            await StudioPage.Pause(1000);
            await page.ScreenshotAsync("theme-rooms.png");
            checks.Add("the room shells render under the board defaults",
                await page.ShowsTextAsync("Wool rooms") && await page.ShowsTextAsync("Spawn rooms"));

            var picker = page.Locator(".field", hasText: "Wool rooms").Locate(".lib-bind select").First();
            await picker.SelectOptionAsync($"{styleId}");
            await StudioPage.Pause(2000);   // past the autosave debounce

            var document = await api.Get($"/map/{slug}/sketch");
            var wool = document?["roomStyles"]?["wool"];
            checks.Add("the wool binding survives PUT → GET", wool != null, document?["roomStyles"].Keys() ?? "[]");
            // A snapshot, not a reference: what came back is the style itself, carrying the wall this one was
            // given and no library id to go stale.
            checks.Add("what is stored is the style, not its id",
                wool?["wall"].Number("extent") == 11 && wool?["styleId"] == null && wool?["id"] == null,
                wool.Keys());
            // The other kind is untouched — the two bind independently.
            checks.Add("binding the wool room leaves the spawn on its built-in shell", document?["roomStyles"]?["spawn"] == null);
            await page.ScreenshotAsync("theme-rooms-bound.png");

            // …and the binding reads back. The finish records the snapshot and never the row, so the select says
            // which row is held by matching the document — the whole of it after a reload.
            await page.GotoAsync($"/maps/{slug}/sketch");
            await page.WaitForSelectorAsync("canvas", 20000);
            await StudioPage.Pause(1500);
            await page.ClickAsync("button[title=\"Theme\"]", 8000);
            await StudioPage.Pause(1500);
            var reloaded = page.Locator(".field", hasText: "Wool rooms").Locate(".lib-bind select").First();
            var held = await reloaded.InputValueAsync();
            checks.Add("the bound shell reads back as its own row after a reload", held == $"{styleId}",
                $"select={held} row={styleId}");
            bound = true;
        }
        catch (Exception failure)
        {
            page.Note($"room shells: {failure.Message.Split('\n')[0]}");
        }
        checks.Add("room shells drove without error", bound, string.Join(" | ", page.Faults.Take(3)));
        checks.Add("sketch tool is clean under the room shells", page.Faults.Count == 0, string.Join(" | ", page.Faults.Take(3)));

        checks.Finish();
    }
}
