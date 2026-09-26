using System.Text.Json;
using PgmStudio.E2e.Tests.Harness;

namespace PgmStudio.E2e.Tests;

/// <summary>
/// The painted world surfaces: each authoring canvas's world layers really draw, and a zoom really re-draws them.
///
/// A blank canvas raises no error — it is exactly as clean as a working one, and a painted surface leaves no
/// elements behind to inspect — so this asserts positively, on pixels, for all three drawing surfaces. The zoom
/// check is why the surfaces are painted at all: a painter re-draws the geometry at the new scale, so the pixels
/// must differ after a wheel burst; identical pixels would mean the surface was scaled rather than redrawn.
/// </summary>
[ClassDataSource<E2eSession>(Shared = SharedType.PerTestSession)]
public sealed class PaintSpec(E2eSession session)
{
    /// <summary>Painted pixels, distinct colours, and an order-sensitive signature of the surface's content.</summary>
    private const string Survey = """
        () => {
          const canvas = document.querySelector("canvas.world-canvas-2d");
          if (!canvas) return null;
          const { width, height } = canvas;
          const data = canvas.getContext("2d").getImageData(0, 0, width, height).data;
          const colors = new Set();
          let painted = 0, signature = 0;
          for (let i = 0; i < data.length; i += 4) {
            if (data[i + 3] === 0) continue;
            painted++;
            signature = (signature + data[i] * 3 + data[i + 1] * 5 + data[i + 2] * 7 + i) % 1_000_000_007;
            if (colors.size < 64) colors.add(`${data[i]},${data[i + 1]},${data[i + 2]}`);
          }
          const svg = document.querySelector("svg.map-canvas-svg");
          return {
            painted, signature, colors: colors.size,
            bufferW: width, bufferH: height,
            cssW: canvas.clientWidth, cssH: canvas.clientHeight,
            dpr: window.devicePixelRatio || 1,
            svgLayers: svg ? [...svg.querySelectorAll("[data-layer]")].map(g => g.getAttribute("data-layer")) : [],
          };
        }
        """;

    [Test, NotInParallel(Order = 8)]
    public async Task EveryWorldSurfacePaintsAndRepaintsOnZoom()
    {
        var seed = session.Seed;
        var checks = new Checks("paint");
        await using var page = await session.NewPageAsync();

        // `Floor` is the share of the viewport the world must cover, set per surface because a plan board fills
        // more of its frame than a world map fills its bounding box. `Enter` reaches a surface its route does not
        // land on — Configure opens on Identity, so the world canvas is reached through its nav rail.
        (string Name, string Path, double Floor, string[] Screen, string? Enter)[] surfaces =
        [
            ("plan", $"/maps/{seed.PlanSlug}/plan", 5, ["overlay", "scale"], null),
            ("sketch", $"/maps/{seed.SketchSlug}/sketch", 5, ["groupChrome", "handles", "center", "scale"], null),
            ("world", $"/maps/{seed.MapSlug}/configure", 1, ["overlay"], "button.nav-btn[title=\"World\"]"),
        ];

        foreach (var surface in surfaces)
        {
            await page.GotoAsync(surface.Path);
            if (surface.Enter != null)
            {
                try { await page.ClickAsync(surface.Enter); }
                catch (TimeoutException) { /* the canvas wait below reports it */ }
            }
            var mounted = await page.TryWaitForSelectorAsync("canvas.world-canvas-2d", 20000);
            checks.Add($"{surface.Name}: the world surface exists", mounted, mounted ? "found" : "no canvas.world-canvas-2d");
            if (!mounted) continue;
            await StudioPage.Pause(1200);   // the first paint lands after the document arrives over interop

            var before = await page.EvaluateAsync(Survey);
            var painted = before.GetProperty("painted").GetDouble();
            var bufferW = before.GetProperty("bufferW").GetDouble();
            var bufferH = before.GetProperty("bufferH").GetDouble();

            // A blank or nearly-blank surface is the failure this exists to catch, so the bar is well clear of it.
            var coverage = painted / (bufferW * bufferH) * 100;
            checks.Add($"{surface.Name}: the surface is painted, not blank", coverage > surface.Floor,
                $"{coverage:F1}% of pixels (floor {surface.Floor}%)");
            var colors = before.GetProperty("colors").GetInt32();
            checks.Add($"{surface.Name}: it draws more than one thing", colors > 3, $"{colors} distinct colours");

            // The buffer must be the CSS box times the pixel ratio — the whole of why painted text and hairlines
            // are sharp. Headless runs at 1, so this catches a buffer that was never sized at all.
            var cssW = before.GetProperty("cssW").GetDouble();
            var cssH = before.GetProperty("cssH").GetDouble();
            var dpr = before.GetProperty("dpr").GetDouble();
            checks.Add($"{surface.Name}: the backing store matches the box × DPR",
                bufferW == Math.Round(cssW * Math.Min(dpr, 2), MidpointRounding.AwayFromZero)
                && bufferH == Math.Round(cssH * Math.Min(dpr, 2), MidpointRounding.AwayFromZero),
                $"{bufferW}x{bufferH} buffer for {cssW}x{cssH} @ {dpr}");

            // World layers painted, screen-space chrome still in the svg where DOM semantics are useful.
            var svgLayers = before.GetProperty("svgLayers").EnumerateArray().Select(layer => layer.GetString()).ToList();
            var missing = surface.Screen.Where(name => !svgLayers.Contains(name)).ToList();
            checks.Add($"{surface.Name}: screen chrome stays in the svg", missing.Count == 0,
                missing.Count > 0 ? $"missing {string.Join(", ", missing)}" : string.Join(", ", svgLayers));

            var box = (await page.Locator("svg.map-canvas-svg").First().BoundingBoxAsync())!.Value;
            await page.Mouse.MoveAsync((decimal)(box.X + box.Width / 2), (decimal)(box.Y + box.Height / 2));
            for (var i = 0; i < 6; i++) await page.Mouse.WheelAsync(0, -120);
            await StudioPage.Pause(600);
            var after = await page.EvaluateAsync(Survey);

            var redrawn = after.ValueKind == JsonValueKind.Object
                && after.GetProperty("signature").GetDouble() != before.GetProperty("signature").GetDouble();
            checks.Add($"{surface.Name}: a zoom re-draws the surface", redrawn,
                $"{painted} px → {(after.ValueKind == JsonValueKind.Object ? after.GetProperty("painted").GetDouble().ToString() : "undefined")} px");
        }

        // A Minecraft username resolves through a third-party host the container cannot reach, so an author row
        // on any page answers 404 — an artifact of the arrangement rather than anything about painting.
        var faults = page.Faults.Where(fault => !fault.Contains("/api/minecraft/", StringComparison.Ordinal)).ToList();
        checks.Add("the pages raised nothing", faults.Count == 0, faults.Count > 0 ? string.Join(" · ", faults) : "clean");

        checks.Finish();
    }
}
