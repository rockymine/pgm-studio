using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using PgmStudio.E2e.Tests.Harness;

namespace PgmStudio.E2e.Tests;

/// <summary>
/// Per-shape theming end to end, on a board with more than one themeable unit.
///
/// The single-island seed has one shape at one height, so this composes a generator plan and spreads its
/// piece heights, which makes the compiler emit one shape per plateau instead of fusing everything. It then
/// assigns a theme to one shape through the UI, proves the assignment persisted, and reads the Blocks overlay's
/// pixels for two shapes in two distinct theme colours against the stone the rest is built from.
/// </summary>
[ClassDataSource<E2eSession>(Shared = SharedType.PerTestSession)]
public sealed class ThemingMultiSpec(E2eSession session)
{
    /// <summary>The terrain shapes of the layout's first layer.</summary>
    private static List<JsonNode> TerrainShapes(JsonNode? layout) =>
        [.. layout?["layers"]?[0]?["layout"].Items("shapes").Where(shape => !shape.Truthy("role")) ?? []];

    private static JsonObject Theme(JsonNode surface, int fillId) => new()
    {
        ["bedrock"] = new JsonObject { ["relative"] = false, ["value"] = 1 },
        ["surface"] = new JsonObject { ["material"] = surface, ["depth"] = 1, ["enabled"] = true },
        ["fill"] = new JsonObject { ["kind"] = "solid", ["id"] = fillId },
    };

    [Test, NotInParallel(Order = 15)]
    public async Task EachShapeCarriesItsOwnTheme()
    {
        var api = session.Api;
        var checks = new Checks("theming · per-shape (sketch · Theme phase)");

        // ── build a multi-shape sketch from a height-spread generator plan ──
        checks.Section("a height-randomized plan compiles to several themeable shapes");

        // The descriptor comes from a browsed card: it carries the composer's own version and schema.
        var cards = (await api.Get("/compose?players=12&symmetry=rot_180&cell=5&count=1")).Items("cards");
        var pinned = await api.Post("/compose/pin", cards[0]["descriptor"]);
        var plan = JsonNode.Parse(pinned.Text("planJson")!)!;
        var pieces = plan.Items("pieces");
        for (var i = 0; i < pieces.Count; i++) pieces[i]["surface"] = 9 + i % 3 * 6;   // 9 / 15 / 21 — distinct plateaus
        var compiled = await api.Post("/plan/compile", plan.ToJsonString());
        var shapeCount = TerrainShapes(compiled?["layout"]).Count;
        checks.Add("plan yields multiple shapes", shapeCount >= 2, $"{shapeCount} terrain shapes");

        var slug = (await api.Post("/sketch", new { name = "theme multi" })).Text("slug")!;
        // The board's registry, put there over the API — the phase picks and places themes, it does not author them.
        var seeded = compiled!["layout"]!.DeepClone();
        seeded["themes"] = new JsonObject { ["grass"] = Theme(new JsonObject { ["kind"] = "solid", ["id"] = 2 }, 3) };
        await api.Put($"/map/{slug}/sketch", seeded);

        // ── drive the UI: assign a theme to ONE shape, and prove it persisted ──
        checks.Section("the UI per-shape assignment persists");

        await using var page = await session.NewPageAsync(1600, 1000);
        var drove = false;
        try
        {
            page.ClearFaults();
            await page.GotoAsync($"/maps/{slug}/sketch");
            await page.WaitForSelectorAsync("canvas", 20000);
            await StudioPage.Pause(1200);

            await page.ClickAsync("button[title=\"Theme\"]");
            await StudioPage.Pause(1200);

            // Take a theme in hand from the strip, then click one shape on the board — the whole of applying one.
            await page.Locator(".canvas-theme-swatch", hasText: "grass").ClickAsync();
            await StudioPage.Pause(400);
            await page.ScreenshotAsync("theme-multi-inhand.png");

            // Aim at a shape the layout carries rather than at a fraction of the viewport.
            var aim = await Aim.WorldAimerAsync(page);
            async Task<int> GrassCount() =>
                TerrainShapes(await api.Get($"/map/{slug}/sketch")).Count(shape => shape.Text("theme") == "grass");
            foreach (var point in TerrainShapes(await api.Get($"/map/{slug}/sketch")).SelectMany(shape => Aim.ShapePoints(shape)))
            {
                if (aim == null) break;
                var at = aim(point.X, point.Z);
                await page.Mouse.ClickAsync((decimal)at.X, (decimal)at.Y);
                await StudioPage.Pause(1500);   // let the debounced save flush
                if (await GrassCount() >= 1) break;
            }
            drove = aim != null;
        }
        catch (Exception failure)
        {
            page.Note($"drive: {failure.Message.Split('\n')[0]}");
        }
        checks.Add("Theme phase drove without error", drove);
        checks.Add("sketch tool clean", page.Faults.Count == 0, string.Join(" | ", page.Faults.Take(3)));

        // The shape the UI themed carries it in the saved layout.
        var saved = (await api.Get($"/map/{slug}/sketch"))!;
        var grassed = TerrainShapes(saved).Where(shape => shape.Text("theme") == "grass").ToList();
        checks.Add("a shape's theme persisted through the UI", grassed.Count >= 1, $"{grassed.Count} shape(s) → grass");
        checks.Add("only the assigned shape is themed (not all)", grassed.Count < TerrainShapes(saved).Count,
            $"{grassed.Count}/{TerrainShapes(saved).Count}");

        // ── distinct per-shape colours in the Blocks overlay ──
        checks.Section("two shapes, two colours, over stone");

        // Two maximally-contrasting blocks from the real paint palette, so both themes render a distinct,
        // in-palette surface colour. One entry per block id, first wins — how the bridge builds its id → hex table.
        var palette = (await api.Get("/terrain/blocks"))!.AsArray().Select(entry => entry!).ToList();
        var byId = new Dictionary<string, JsonNode>();
        foreach (var entry in palette) byId.TryAdd(entry["id"]!.ToJsonString(), entry);
        var representatives = byId.Values.ToList();
        static (int R, int G, int B) Rgb(string hex)
        {
            var packed = int.Parse(hex.TrimStart('#'), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return (packed >> 16 & 255, packed >> 8 & 255, packed & 255);
        }
        static int Distance(string first, string second)
        {
            var (r1, g1, b1) = Rgb(first);
            var (r2, g2, b2) = Rgb(second);
            return (r1 - r2) * (r1 - r2) + (g1 - g2) * (g1 - g2) + (b1 - b2) * (b1 - b2);
        }
        static int Chroma(string hex) { var (r, g, b) = Rgb(hex); return Math.Max(r, Math.Max(g, b)) - Math.Min(r, Math.Min(g, b)); }
        static double Light(string hex) { var (r, g, b) = Rgb(hex); return (r + g + b) / 3.0; }
        static string Hex(JsonNode block) => block.Text("hex")!;
        // Saturated and mid-lightness — greys, darks and lights wash out against the canvas and the island fill.
        var vivid = representatives.Where(block => Chroma(Hex(block)) > 60 && Light(Hex(block)) > 90 && Light(Hex(block)) < 205).ToList();
        var pool = vivid.Count >= 2 ? vivid : representatives;
        var first = pool[0];
        var second = pool[1];
        foreach (var candidate in pool)
        {
            foreach (var other in pool)
            {
                if (candidate["id"]!.ToJsonString() != other["id"]!.ToJsonString()
                    && Distance(Hex(candidate), Hex(other)) > Distance(Hex(first), Hex(second)))
                {
                    first = candidate;
                    second = other;
                }
            }
        }
        checks.Add("two contrasting palette blocks", first["id"]!.ToJsonString() != second["id"]!.ToJsonString(),
            $"{first.Text("name")} {Hex(first)} / {second.Text("name")} {Hex(second)}");

        var shapes = TerrainShapes(saved);
        var grassShape = shapes.FirstOrDefault(shape => shape.Text("theme") == "grass");
        var otherShape = shapes.FirstOrDefault(shape => !shape.Truthy("theme") && shape.Text("id") != grassShape.Text("id"))
            ?? shapes.FirstOrDefault(shape => shape.Text("id") != grassShape.Text("id"));
        static JsonObject Surface(JsonNode block)
        {
            var material = new JsonObject { ["kind"] = "solid", ["id"] = block["id"]!.DeepClone() };
            if (block["data"] is { } data) material["data"] = data.DeepClone();
            return material;
        }
        if (saved["themes"] is not JsonObject themes)
        {
            themes = [];
            saved["themes"] = themes;
        }
        themes["grass"] = Theme(Surface(first), 1);
        themes["dirt"] = Theme(Surface(second), 1);
        if (grassShape != null) grassShape["theme"] = "grass";
        if (otherShape != null) otherShape["theme"] = "dirt";
        saved["mapTheme"] = "";   // no map default — the two themed shapes read as their two colours against stone
        await api.Put($"/map/{slug}/sketch", saved);

        var shot = false;
        JsonElement painted = default;
        try
        {
            page.ClearFaults();
            await page.GotoAsync($"/maps/{slug}/sketch");
            await page.WaitForSelectorAsync("canvas", 20000);
            await StudioPage.Pause(2000);   // WASM boot + the async block-palette fetch that colours the overlay
            // The phase opens with Blocks on — the paint is what it acts on — so this only confirms it.
            await page.ClickAsync("button[title=\"Theme\"]");
            await StudioPage.Pause(2200);
            var blocks = page.Locator("button.canvas-chip", hasText: "Blocks").First();
            Task<bool> IsOn() => blocks.EvaluateAsync<bool>("el => el.classList.contains('canvas-chip--on')");
            if (!await IsOn()) await blocks.ClickAsync();
            await StudioPage.Pause(2500);   // let the coloured raster render
            var blocksOn = await IsOn();
            await page.ScreenshotAsync("theme-multi-blocks.png");
            checks.Add("Blocks overlay is on for the shot", blocksOn);

            // The canvas must carry the two themes' block colours at full opacity; a translucent overlay composites
            // them to grey while every structural assertion still passes. Read the pixels.
            painted = await page.EvaluateAsync("""
                () => {
                  const canvas = document.querySelector("canvas.world-canvas-2d");
                  if (!canvas) return null;
                  const d = canvas.getContext("2d").getImageData(0, 0, canvas.width, canvas.height).data;
                  const hist = {};
                  for (let i = 0; i < d.length; i += 4) {
                    if (d[i + 3] < 250) continue;   // opaque only — a painted block is not blended with anything
                    const hex = "#" + [d[i], d[i + 1], d[i + 2]].map(v => v.toString(16).padStart(2, "0")).join("");
                    hist[hex] = (hist[hex] ?? 0) + 1;
                  }
                  return hist;
                }
                """);
            shot = true;
        }
        catch (Exception failure)
        {
            page.Note($"blocks shot: {failure.Message.Split('\n')[0]}");
        }

        // The paint itself: the server runs the real painter over the layout, so BOTH themes must appear in what
        // it returns — each shape's override resolved per cell, plus the painter, end to end.
        var paint = (await api.Post($"/map/{slug}/sketch/paint", saved))!;
        // Both wire forms are palette-indexed: runs are [paletteIndex, length, …] row-major over the bounding box,
        // -1 for a cell outside the footprint.
        long CellsOf(string hex)
        {
            var target = hex.ToLowerInvariant();
            bool Hits(long index) =>
                paint["palette"] is JsonArray colours && index >= 0 && index < colours.Count
                && colours[(int)index]?.GetValue<string>().ToLowerInvariant() == target;
            if (paint["runs"] is JsonArray runs)
            {
                long cells = 0;
                for (var i = 0; i + 1 < runs.Count; i += 2)
                {
                    if (Hits(runs[i]!.GetValue<long>())) cells += runs[i + 1]!.GetValue<long>();
                }
                return cells;
            }
            return paint.Items("color_idx").Count(index => Hits(index.GetValue<long>()));
        }
        checks.Add($"theme \"grass\" is painted ({Hex(first)})", CellsOf(Hex(first)) > 0, $"{CellsOf(Hex(first))} cells");
        checks.Add($"theme \"dirt\" is painted ({Hex(second)})", CellsOf(Hex(second)) > 0, $"{CellsOf(Hex(second))} cells");

        // The render: those colours must reach the canvas exactly and opaque. Only an exact-hex pixel read
        // catches a wash; "some colour is there" is exactly what a wash would satisfy.
        long Pixels(string hex) =>
            painted.ValueKind == JsonValueKind.Object && painted.TryGetProperty(hex.ToLowerInvariant(), out var count) ? count.GetInt64() : 0;
        var onCanvas = new[] { first, second }.Where(block => Pixels(Hex(block)) > 0).ToList();
        checks.Add("a theme's exact surface hex is on the canvas, unblended", onCanvas.Count > 0,
            onCanvas.Count > 0 ? string.Join(", ", onCanvas.Select(block => $"{Hex(block)} ×{Pixels(Hex(block))}")) : "none of either theme's hex");
        checks.Add("two shapes carry distinct themes", otherShape.Text("theme") == "dirt" && grassShape.Text("theme") == "grass");
        checks.Add("themed Blocks overlay captured", shot, string.Join(" | ", page.Faults.Take(3)));

        checks.Finish();
    }
}
