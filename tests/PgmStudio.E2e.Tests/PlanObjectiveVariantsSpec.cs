using System.Text.RegularExpressions;
using PgmStudio.E2e.Tests.Harness;

namespace PgmStudio.E2e.Tests;

/// <summary>
/// Varying an objective in the plan tool.
///
/// A core and a destroyable are placed as bare markers and take the generator's defaults; the knobs that vary
/// them — the casing's shape and its float/leak pair, a destroyable's design and material — live in the panel
/// that opens when one is selected. Checked: the panel offers them, a change reaches the document, and it comes
/// back on the next selection. The dig-depth sentence proves the round trip rather than the render: it is
/// derived from leak minus float, so it changes only if the new leak was written into the plan and read back.
/// </summary>
[ClassDataSource<E2eSession>(Shared = SharedType.PerTestSession)]
public sealed partial class PlanObjectiveVariantsSpec(E2eSession session)
{
    [Test, NotInParallel(Order = 11)]
    public async Task AnObjectiveIsVariedFromItsPanel()
    {
        var checks = new Checks("plan · objective variants");
        await using var page = await session.NewPageAsync();

        // Arms a tool from one of the dock's flyout families.
        async Task Pick(string family, string option)
        {
            await page.ClickAsync($".canvas-dock-chevron[aria-label=\"{family}\"]");
            await page.Locator(".canvas-dock-flyout-row", hasText: option).ClickAsync();
            await StudioPage.Pause(120);
        }

        // A field's label carries its hint in the same element ("Float (air under the casing)"), so the match is
        // by substring rather than exact text.
        Locator Control(string label, string tag) => page.Locator(".field").Has(".field-label", label).Locate(tag).First();
        Locator Field(string label) => Control(label, "input");
        // A two-state knob is a pair of chips rather than an input, so what it reads is which of them is active.
        async Task<string> ActiveChip(string label) => (await Control(label, ".filter-chip--active").TextContentAsync()).Trim();
        Task Chip(string label, string option) =>
            page.Locator(".field").Has(".field-label", label).Locate(".filter-chip", hasText: option).ClickAsync();
        Task<string> Body() => page.TextContentAsync(".workspace");
        static string Obsidian(string text) => ObsidianReadout().Match(text) is { Success: true } match ? match.Value : "(absent)";

        // A blank plan opens at the origin with rot_180 — an order-2 symmetry, which is what offers the objective
        // tools at all.
        await page.GotoAsync("/plan-editor");
        await page.WaitForSelectorAsync(".canvas-dock", 20000);
        await page.WaitForSelectorAsync(".map-canvas-svg", 20000);

        var canvas = (await page.Locator(".map-canvas-svg").First().BoundingBoxAsync())!.Value;
        var midX = canvas.X + canvas.Width / 2;
        var midZ = canvas.Y + canvas.Height / 2;

        // ── a piece to stand on, then a core on it ──
        checks.Section("a core placed on a piece opens its casing panel");

        await Pick("Pieces", "Piece");
        await page.Mouse.MoveAsync((decimal)(midX - 120), (decimal)(midZ - 80));
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync((decimal)(midX + 120), (decimal)(midZ + 80), new PuppeteerSharp.Input.MoveOptions { Steps = 8 });
        await page.Mouse.UpAsync();
        await StudioPage.Pause(250);

        await Pick("Markers", "Core");
        await page.Mouse.ClickAsync((decimal)midX, (decimal)midZ);
        await StudioPage.Pause(300);

        var opened = await Body();
        // A core is chosen, not designed: the author states the interior and the obsidian follows, so the three
        // knobs are the lava's footprint, its height, and whether the casing is capped.
        checks.Add("the panel names the three knobs",
            opened.Contains("Lava footprint", StringComparison.Ordinal) && opened.Contains("Lava height", StringComparison.Ordinal)
            && opened.Contains("Casing", StringComparison.Ordinal));
        checks.Add("and the float/leak pair", opened.Contains("Float", StringComparison.Ordinal) && opened.Contains("Leak", StringComparison.Ordinal));
        // Defaults are served, not hardcoded on the client — 3×3 lava, 3 courses, float 6, leak 5.
        var lava = await Control("Lava footprint", "select").InputValueAsync();
        var height = await Control("Lava height", "select").InputValueAsync();
        checks.Add("it opens on the generator's defaults, not on blanks", lava == "3" && height == "3", $"{lava} / {height}");
        // The readout states the structure being built rather than the interior alone.
        checks.Add("the obsidian the two imply is read back",
            opened.Contains("5×5×5 obsidian, 3×3×3 lava inside", StringComparison.Ordinal), Obsidian(opened));
        // Which state the casing is in is the active chip; both chip labels are in the panel's text either way.
        var capped = await ActiveChip("Casing");
        checks.Add("the lava starts capped", capped == "capped", capped);
        checks.Add("and leak 5 under float 6 means no digging", opened.Contains("no digging", StringComparison.Ordinal));

        // ── a change reaches the document and comes back ──
        checks.Section("a varied core states what it varies");

        await Field("Leak").FillAsync("9");
        await Field("Leak").BlurAsync();
        await StudioPage.Pause(300);
        // leak 9 over float 6: the sentence can only say 4 if the plan document carries the new leak. The lava
        // free-falls to the first air cell over the terrain and PGM tests its centre, so the core leaks one
        // course lower than the number reads.
        checks.Add("the dig depth follows leak through the document", (await Body()).Contains("digging 4 blocks", StringComparison.Ordinal));

        await Chip("Casing", "open");
        await StudioPage.Pause(300);
        var uncapped = await Body();
        var open = await ActiveChip("Casing");
        checks.Add("the lava can be left flush with the rim", open == "open", open);
        // An open top gives up the cap course, so the derived obsidian loses exactly one from its height.
        checks.Add("and the casing loses its cap course, not its walls",
            uncapped.Contains("5×5×4 obsidian", StringComparison.Ordinal), Obsidian(uncapped));

        await Control("Lava footprint", "select").SelectOptionAsync("5");
        await StudioPage.Pause(300);
        var widened = await Body();
        checks.Add("the casing keeps its new footprint", await Control("Lava footprint", "select").InputValueAsync() == "5");
        checks.Add("and the obsidian follows the interior", widened.Contains("7×7×4 obsidian", StringComparison.Ordinal), Obsidian(widened));

        // ── the destroyable's designs ──
        checks.Section("a destroyable offers the designs the stamper can build");

        await Pick("Markers", "Destroyable");
        await page.Mouse.ClickAsync((decimal)(midX + 40), (decimal)(midZ + 40));
        await StudioPage.Pause(300);

        var designs = await page.Locator(".field").Has(".field-label", "Design").Locate("option").AllTextContentsAsync();
        checks.Add("all six designs are offered", designs.Length == 6, string.Join(" · ", designs));
        checks.Add("including the pillars and the cubes",
            designs.Contains("pillar-3") && designs.Contains("cube-4") && designs.Contains("column-plus"), string.Join(" · ", designs));

        var materials = await page.Locator(".field").Has(".field-label", "Material").Locate("option").AllTextContentsAsync();
        checks.Add("and only materials the stamper builds", materials.Length == 4, string.Join(" · ", materials));

        await Control("Design", "select").SelectOptionAsync("cube-4");
        await StudioPage.Pause(300);
        var chosen = await Control("Design", "select").InputValueAsync();
        checks.Add("a chosen design survives the round trip", chosen == "cube-4", chosen);

        checks.Add("varying an objective raised nothing", page.Faults.Count == 0, string.Join(" | ", page.Faults));

        checks.Finish();
    }

    [GeneratedRegex(@"\d+×\d+×\d+ obsidian[^.]*")]
    private static partial Regex ObsidianReadout();
}
