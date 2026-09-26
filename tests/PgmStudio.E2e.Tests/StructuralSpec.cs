using PgmStudio.E2e.Tests.Harness;

namespace PgmStudio.E2e.Tests;

/// <summary>
/// The plan's spawn and wool pieces surface as locked, labelled rectangles in the sketch, on a real composed
/// board — single height, so its same-plane pieces fuse into one island polygon, exactly where a spawn or wool
/// would otherwise vanish.
/// <list type="number">
/// <item>Compiling the plan yields role-tagged spawn/wool rectangles alongside the fused terrain, carrying the
/// link and colour the sketch renders from.</item>
/// <item>The plan tool and the sketch tool are captured, so the surfaced pieces can be seen over the fused
/// terrain.</item>
/// </list>
/// </summary>
[ClassDataSource<E2eSession>(Shared = SharedType.PerTestSession)]
public sealed class StructuralSpec(E2eSession session)
{
    [Test, NotInParallel(Order = 14)]
    public async Task StructuralPiecesSurfaceInTheSketch()
    {
        var seed = session.Seed;
        var checks = new Checks("structural (S25)");

        // ── 1. the compiled layout carries the surfaced pieces ──
        checks.Section("compiled layout surfaces the plan's structural pieces");

        var compiled = await session.Api.Post("/plan/compile", seed.PlanJson);
        // The layout is a stack of layers and a layer holds its own drawing, so the shapes are read per layer.
        var shapes = compiled?["layout"].Items("layers").SelectMany(layer => layer["layout"].Items("shapes")).ToList() ?? [];
        var spawns = shapes.Where(shape => shape.Text("role") == "spawn").ToList();
        var wools = shapes.Where(shape => shape.Text("role") == "woolRoom").ToList();
        var terrain = shapes.Where(shape => !shape.Truthy("role")).ToList();
        var structural = spawns.Concat(wools).ToList();

        checks.Add("spawn pieces surfaced", spawns.Count >= 2, $"{spawns.Count} spawn rectangles");
        checks.Add("wool rooms surfaced", wools.Count >= 2, $"{wools.Count} wool rectangles");
        checks.Add("structural shapes are rectangles",
            structural.All(shape => shape.Text("type") == "rectangle" && shape.AsObject().ContainsKey("min_x")),
            "each carries a rectangle footprint");
        checks.Add("each is linked back to its intent entity",
            structural.All(shape => shape.Truthy("intentRef")) && spawns.All(shape => shape.Truthy("color")),
            "intentRef + colour present");
        // The very case the feature exists for: the single-height board fused its terrain, yet the pieces survive.
        checks.Add("terrain fused below the surfaced pieces",
            terrain.Count > 0 && terrain.Count < structural.Count,
            $"{terrain.Count} terrain polygon(s) vs {structural.Count} structural pieces");

        // ── 2. screenshot the plan tool and the sketch tool ──
        checks.Section("plan → sketch, captured");

        await using var page = await session.NewPageAsync(1600, 1000);

        async Task Capture(string path, string file, string waitFor)
        {
            page.ClearFaults();
            var captured = false;
            try
            {
                await page.GotoAsync(path);
                await page.WaitForSelectorAsync(waitFor, 20000);
                await StudioPage.Pause(2500);   // WASM boot + canvas paint + fit-to-bbox settle
                await page.ScreenshotAsync(file);
                captured = true;
            }
            catch (Exception failure)
            {
                page.Note($"capture {path}: {failure.Message.Split('\n')[0]}");
            }
            checks.Add($"{file} captured", captured, captured ? path : "see faults");
            checks.Add($"{path} is clean", page.Faults.Count == 0, string.Join(" | ", page.Faults.Take(3)));
        }

        // The plan tool renders the generated plan; the sketch draft carries the same compiled layout, so the
        // surfaced spawn/wool boxes appear over the fused island.
        await Capture($"/maps/{seed.PlanSlug}/plan", "s25-plan.png", "canvas, .map-canvas-svg");
        await Capture($"/maps/{seed.SketchSlug}/sketch", "s25-sketch.png", "canvas");

        checks.Finish();
    }
}
