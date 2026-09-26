using System.Text.Json;
using System.Text.Json.Nodes;
using PgmStudio.E2e.Tests.Harness;

namespace PgmStudio.E2e.Tests;

/// <summary>
/// The control row and the icon scale — two things a stylesheet states and nothing else checks.
///
/// A row mixing a select, a text input, a coord-field and an icon button gets different intrinsic heights for
/// one type size, so the library rows state the height once as <c>--control-height</c> and the square controls
/// read their width off it. Both halves break quietly, so this measures the rendered boxes. Comparing a row's
/// controls against the token alone would be a tautology; the check that gives it meaning is the unconstrained
/// select, which renders at its own intrinsic height and parts company with the token if the type scale moves.
///
/// The icon half is the same shape of problem: every rendered glyph must land on the five-step scale, on the
/// surfaces that only exist once a rail is open as well as on the routes' resting state.
/// </summary>
[ClassDataSource<E2eSession>(Shared = SharedType.PerTestSession)]
public sealed class ControlsSpec(E2eSession session)
{
    private const string Rows = ".material-editor-head, .block-picker-row, .lib-bind";

    [Test, NotInParallel(Order = 3)]
    public async Task ControlsAndGlyphsKeepTheirScale()
    {
        var api = session.Api;
        var checks = new Checks("controls");
        await using var page = await session.NewPageAsync();

        // A layer stack over a voronoi: nested entries, a band list, and a block picker inside each — every row
        // shape the material editor has, in one style.
        var solid = new JsonObject { ["kind"] = "solid", ["id"] = 1, ["data"] = 0 };
        var voronoi = new JsonObject
        {
            ["kind"] = "voronoi", ["cellSize"] = 9, ["rise"] = 0, ["seed"] = 3,
            ["bands"] = new JsonArray(
                new JsonObject { ["depth"] = 1, ["material"] = solid },
                new JsonObject { ["depth"] = 2, ["material"] = new JsonObject { ["kind"] = "solid", ["id"] = 2, ["data"] = 0 } }),
        };
        var layered = new JsonObject
        {
            ["kind"] = "layered",
            ["layers"] = new JsonArray(new JsonObject { ["thickness"] = 1, ["material"] = voronoi }),
        };
        var style = (await api.Post("/styles", new JsonObject
        {
            ["name"] = "E2E control row", ["kind"] = "layered", ["params"] = layered.ToJsonString(),
        }))!;
        await api.Post("/room-styles", new JsonObject
        {
            ["name"] = "E2E control room", ["floorDepth"] = 1, ["wallHeight"] = 5, ["roofThickness"] = 1,
            ["roofForm"] = "flat", ["roofHole"] = false, ["door"] = "oak_door", ["doorHeight"] = 3,
            ["windows"] = new JsonObject { ["form"] = "none", ["block"] = 0, ["data"] = 0, ["sill"] = 0, ["width"] = 0, ["height"] = 0, ["spacing"] = 0 },
            ["storeyStack"] = new JsonArray(),
            ["courses"] = new JsonArray(new JsonObject { ["part"] = "wall", ["ordinal"] = 0, ["styleId"] = style["id"]!.DeepClone(), ["height"] = 2 }),
        });

        // Every control in every row on the page, against the token the stylesheet declares.
        async Task MeasureRows(string label)
        {
            var state = await page.EvaluateAsync("""
                ROWS => {
                  const declared = parseFloat(getComputedStyle(document.documentElement).getPropertyValue("--control-height"));
                  const SQUARE = ".action-btn--icon, .block-chip, .lib-bind-swatch";
                  const name = el => `${el.tagName.toLowerCase()}.${(el.className || "").toString().split(" ")[0]}`;
                  const wrong = [], notSquare = [];
                  let controls = 0, squares = 0;
                  for (const row of document.querySelectorAll(ROWS)) {
                    for (const el of row.children) {
                      // The help mark is a round marker beside a control, not a control — it keeps its own size.
                      if (el.classList.contains("help-mark")) continue;
                      const box = el.getBoundingClientRect();
                      controls++;
                      if (Math.abs(box.height - declared) > 0.5) wrong.push(`${name(el)} h=${box.height}`);
                      if (el.matches(SQUARE)) {
                        squares++;
                        if (Math.abs(box.width - box.height) > 0.5) notSquare.push(`${name(el)} ${box.width}×${box.height}`);
                      }
                    }
                  }
                  return { declared, controls, squares, wrong, notSquare };
                }
                """, Rows);
            var controls = state.GetProperty("controls").GetInt32();
            var squares = state.GetProperty("squares").GetInt32();
            var declared = state.GetProperty("declared").GetDouble();
            var wrong = Strings(state.GetProperty("wrong"));
            var notSquare = Strings(state.GetProperty("notSquare"));

            // Vacuity guard: a renamed selector would otherwise turn this green by measuring nothing.
            checks.Add($"{label}: rows were found to measure", controls > 0, $"{controls} controls");
            checks.Add($"{label}: every control is one --control-height", wrong.Count == 0,
                wrong.Count > 0 ? string.Join(" | ", wrong) : $"{controls} × {declared}px");
            checks.Add($"{label}: square controls were found", squares > 0, $"{squares} square");
            checks.Add($"{label}: every square control is square", notSquare.Count == 0,
                notSquare.Count > 0 ? string.Join(" | ", notSquare) : $"{squares} checked");
        }

        // The one measurement the token did not produce: a select no row constrains renders at its own height.
        async Task MeasureUnconstrainedSelect(string label)
        {
            var state = await page.EvaluateAsync("""
                ROWS => {
                  const declared = parseFloat(getComputedStyle(document.documentElement).getPropertyValue("--control-height"));
                  const free = [...document.querySelectorAll("select.field-input")].find(el => !el.parentElement?.matches(ROWS));
                  return { declared, height: free ? free.getBoundingClientRect().height : null };
                }
                """, Rows);
            var declared = state.GetProperty("declared").GetDouble();
            double? height = state.GetProperty("height") is { ValueKind: JsonValueKind.Number } measured ? measured.GetDouble() : null;
            checks.Add($"{label}: an unconstrained select was found", height != null,
                height == null ? "nothing outside a stated-height row" : $"{height}px");
            checks.Add($"{label}: --control-height is what a select actually wants",
                height != null && Math.Abs(height.Value - declared) <= 1,
                $"token {declared}px vs unconstrained select {(height?.ToString() ?? "null")}px");
        }

        // Every rendered glyph on screen must land on a step of the icon scale.
        async Task MeasureGlyphs(string label)
        {
            await page.TryWaitForFunctionAsync("() => document.querySelector('svg.lucide') !== null", 20000);
            var sizes = await page.EvaluateAsync("""
                () => {
                  const root = getComputedStyle(document.documentElement);
                  const scale = ["xs", "sm", "md", "lg", "xl"].map(step => parseFloat(root.getPropertyValue(`--icon-${step}`)));
                  const off = [];
                  let seen = 0;
                  for (const svg of document.querySelectorAll("svg.lucide")) {
                    const box = svg.getBoundingClientRect();
                    if (box.width === 0) continue;   // an icon in a collapsed/hidden panel measures nothing
                    seen++;
                    if (!scale.some(step => Math.abs(box.width - step) < 0.5)) {
                      const name = [...svg.classList].find(c => c.startsWith("lucide-")) ?? svg.getAttribute("data-lucide");
                      off.push(`${name} ${box.width}px`);
                    }
                  }
                  return { scale, seen, off: [...new Set(off)] };
                }
                """);
            var seen = sizes.GetProperty("seen").GetInt32();
            var off = Strings(sizes.GetProperty("off"));
            var scale = sizes.GetProperty("scale").EnumerateArray().Select(step => step.ToString());
            checks.Add($"{label}: glyphs rendered", seen > 0, $"{seen} glyphs");
            checks.Add($"{label}: every glyph is a scale step", off.Count == 0,
                off.Count > 0 ? string.Join(", ", off) : $"on {string.Join(" / ", scale)}px");
        }

        async Task OpenStyleEditor()
        {
            page.ClearFaults();
            await page.GotoAsync("/library/styles");
            await page.WaitForSelectorAsync(".lib-card", 20000);
            await page.Locator(".lib-card", hasText: "E2E control row").Locate(".lib-card-fig").ClickAsync();
            await page.WaitForSelectorAsync(".material-editor-head", 20000);
        }

        checks.Section("a control row is one row of one height");

        await OpenStyleEditor();
        await MeasureRows("style editor");

        page.ClearFaults();
        await page.GotoAsync("/library/houses");
        await page.WaitForSelectorAsync(".lib-card", 20000);
        await page.Locator(".lib-card", hasText: "E2E control room").Locate(".lib-card-fig").ClickAsync();
        await page.WaitForSelectorAsync(".lib-outline-row", 20000);
        await page.Locator(".lib-outline-row", hasText: "Walls").ClickAsync();
        await page.WaitForSelectorAsync(".lib-bind", 20000);
        await MeasureRows("house editor");
        await MeasureUnconstrainedSelect("house editor");

        // ── the icon scale ──
        // The house editor is still open, so this pass covers the outline and the inspector — surfaces the
        // resting routes below never render.
        checks.Section("every glyph lands on the icon scale");
        await MeasureGlyphs("house editor (open)");

        foreach (var route in new[] { "/", "/library/styles", "/maps", "/catalog" })
        {
            page.ClearFaults();
            await page.GotoAsync(route);
            await MeasureGlyphs(route);
        }

        // The style editor, for the same reason — and it carries the material editor's own glyphs.
        await OpenStyleEditor();
        await MeasureGlyphs("style editor (open)");

        checks.Finish();
    }

    private static List<string> Strings(JsonElement array) =>
        [.. array.EnumerateArray().Select(item => item.GetString() ?? "")];
}
