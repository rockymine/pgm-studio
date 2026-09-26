using PgmStudio.E2e.Tests.Harness;

namespace PgmStudio.E2e.Tests;

/// <summary>
/// The Draw phase's dock says what the next shape will do.
///
/// The operation — build or carve — is the word that leads the draw group in the canvas dock, and the group
/// wears the colour of the mode, so the three shape buttons beside it state it too. Checked here: one control,
/// carrying one state, colouring the tools it decides for, that goes quiet when the tool in hand does not draw.
/// </summary>
[ClassDataSource<E2eSession>(Shared = SharedType.PerTestSession)]
public sealed class DrawToolsSpec(E2eSession session)
{
    [Test, NotInParallel(Order = 4)]
    public async Task TheDockNamesTheNextShapesOperation()
    {
        var checks = new Checks("draw tools");
        await using var page = await session.NewPageAsync();

        await page.GotoAsync($"/maps/{session.Seed.SketchSlug}/sketch");
        await page.WaitForSelectorAsync(".canvas-dock", 20000);

        var mode = page.Locator(".canvas-dock-mode");
        var drawGroup = page.Locator(".canvas-dock-group").Has(".canvas-dock-mode");
        async Task<string> Label() => (await mode.First().TextContentAsync()).Trim();
        async Task<string> Accent() => await drawGroup.First().GetAttributeAsync("style") ?? "";
        async Task<bool> Idle() =>
            (await drawGroup.First().GetAttributeAsync("class") ?? "").Contains("canvas-dock-group--idle", StringComparison.Ordinal);

        // ── the operation is one control, not two ──
        checks.Section("the operation is one control naming one state");

        var modes = await mode.CountAsync();
        checks.Add("there is exactly one operation control", modes == 1, $"{modes}");
        var tools = await drawGroup.Locate(".canvas-dock-btn").CountAsync();
        checks.Add("it leads a group of its own, holding the three tools it decides for", tools == 3, $"{tools}");
        checks.Add("and getting around the canvas is a different box",
            await page.Locator(".canvas-dock-group:not(:has(.canvas-dock-mode)) button[aria-label=\"Move\"]").CountAsync() == 1);

        // ── idle until a tool that draws is in hand ──
        checks.Section("it goes quiet when nothing it decides is about to happen");

        // The sketch opens on move, which does not draw.
        checks.Add("on move the group is dimmed", await Idle());

        await page.ClickAsync("button[aria-label=\"Measure\"]");
        await StudioPage.Pause(120);
        checks.Add("measure does not draw either, so it stays dimmed", await Idle());

        // ── armed, it names its mode and flips ──
        checks.Section("armed, it names its mode, colours its tools, and one click flips it");

        await page.ClickAsync("button[aria-label=\"Rectangle\"]");
        await StudioPage.Pause(120);
        checks.Add("a draw tool wakes it", !await Idle());
        checks.Add("and a sketch starts building",
            await Label() == "Build" && (await Accent()).Contains("canvas-add-fill", StringComparison.Ordinal),
            $"{await Label()} · {await Accent()}");

        await mode.ClickAsync();
        await StudioPage.Pause(120);
        checks.Add("one click carves instead",
            await Label() == "Carve" && (await Accent()).Contains("canvas-sub-fill", StringComparison.Ordinal),
            $"{await Label()} · {await Accent()}");

        await mode.ClickAsync();
        await StudioPage.Pause(120);
        checks.Add("and clicking again goes back", await Label() == "Build", await Label());

        // The operation survives a tool change — it is a property of the next shape, not of the tool in hand.
        await mode.ClickAsync();
        await page.ClickAsync("button[aria-label=\"Lasso\"]");
        await StudioPage.Pause(120);
        checks.Add("it is remembered across a tool change", await Label() == "Carve" && !await Idle(), await Label());

        checks.Add("the page raised no fault", page.Faults.Count == 0, string.Join(" | ", page.Faults));

        checks.Finish();
    }
}
