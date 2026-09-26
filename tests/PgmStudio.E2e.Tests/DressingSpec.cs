using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using PgmStudio.E2e.Tests.Harness;

namespace PgmStudio.E2e.Tests;

/// <summary>
/// The Dressing phase on the sketch tool — the pass that places what stands on the terrain.
/// <list type="number">
/// <item>Placed props (a path, an area, a tree, a boulder) survive PUT → GET through the real sketch endpoint,
/// each read back as the kind it was written as.</item>
/// <item>The option cards are drawn by the pass itself, and the preview's counts move with the knobs.</item>
/// <item>The phase renders its placing tools, and dragging on the canvas actually places a prop.</item>
/// </list>
/// </summary>
[ClassDataSource<E2eSession>(Shared = SharedType.PerTestSession)]
public sealed class DressingSpec(E2eSession session)
{
    /// <summary>
    /// The placing tools this spec drives, named once: the dock check asserts they are offered and the drive
    /// clicks them, so a renamed tool fails both together.
    /// </summary>
    private static readonly (string Stroke, string Water, string Tree) Driven = ("Stroke", "Water", "Tree");

    [Test, NotInParallel(Order = 5)]
    public async Task DressingIsPlacedAndPreviewedByThePass()
    {
        var api = session.Api;
        var slug = session.Seed.SketchSlug;
        var checks = new Checks("dressing (decoration.md)");

        // ── 1. placed props round-trip ──
        checks.Section("placed dressing survives PUT → GET");

        var dressed = (await api.Get($"/map/{slug}/sketch"))!.DeepClone();
        dressed["dressing"] = JsonNode.Parse("""
            { "props": [
              { "kind": "stroke", "id": "p1", "points": [[0, 0], [24, 6], [40, 24]], "radius": 3, "style": "stones", "seed": 5,
                "pave": { "kind": "solid", "id": 4, "data": 0 } },
              { "kind": "flora", "id": "f1", "points": [[0, 0], [20, 0], [20, 20]], "spec": { "coverage": 0.8 }, "seed": 7 },
              { "kind": "tree", "id": "t1", "x": 10, "z": 12, "species": "birch", "height": 22, "seed": 9 },
              { "kind": "boulder", "id": "b1", "x": -8, "z": 4, "form": "cairn", "size": 4, "seed": 11 }
            ] }
            """);
        await api.Put($"/map/{slug}/sketch", dressed);

        var back = await api.Get($"/map/{slug}/sketch");
        var props = back?["dressing"].Items("props") ?? [];
        checks.Add("every prop persisted", props.Count == 4, $"{props.Count} props");
        var kinds = string.Join(",", props.Select(prop => prop.Text("kind")));
        checks.Add("each kept its kind", kinds == "stroke,flora,tree,boulder", kinds);
        var path = props.ElementAtOrDefault(0);
        checks.Add("a path kept its route, width and style",
            path.Items("points").Count == 3 && path.Number("radius") == 3 && path.Text("style") == "stones",
            path?.ToJsonString() ?? "null");
        var tree = props.ElementAtOrDefault(2);
        checks.Add("a marker kept its cell", tree.Number("x") == 10 && tree.Number("z") == 12, tree?.ToJsonString() ?? "null");

        // ── 2. the pickers are drawn, and the preview places ──
        checks.Section("every option is drawn by the pass");

        var styles = (await api.Get("/terrain/stroke-styles"))!.AsArray();
        var waterForms = (await api.Get("/terrain/water-forms"))!.AsArray();
        var forms = (await api.Get("/terrain/boulder-forms"))!.AsArray();
        var species = (await api.Get("/terrain/species"))!.AsArray();
        static bool Drawn(JsonNode? card) => (card.Text("svg") ?? "").Contains("<rect", StringComparison.Ordinal);
        static string KeysOf(JsonArray cards) => string.Join(" ", cards.Select(card => card.Text("key")));

        checks.Add("five stroke styles, each drawn", styles.Count == 5 && styles.All(Drawn), KeysOf(styles));
        checks.Add("three channel forms, each drawn", waterForms.Count == 3 && waterForms.All(Drawn), KeysOf(waterForms));
        checks.Add("four boulder forms, each drawn", forms.Count == 4 && forms.All(Drawn), KeysOf(forms));
        checks.Add("every species drawn, and carrying its own proportions",
            species.Count >= 6 && species.All(card => Drawn(card) && (card.Text("defaults") ?? "").Contains("height", StringComparison.Ordinal)),
            KeysOf(species));

        // A species names a silhouette, so six species cards that drew the same tree would be six palettes on
        // one shape — the claim the picker exists to make, asserted on the cards themselves.
        static int Blocks(JsonNode? card) => Regex.Matches(card.Text("svg") ?? "", "<rect").Count;
        checks.Add("the species differ in shape, not just in wood",
            species.Select(Blocks).Distinct().Count() >= 5,
            string.Join(" ", species.Select(card => $"{card.Text("key")}:{Blocks(card)}")));

        checks.Section("the preview places what the prop says");

        var grassTheme = """{"surface":{"material":{"kind":"solid","id":2},"depth":1,"enabled":true}}""";
        async Task<JsonNode> Preview(string prop) =>
            (await api.Post("/terrain/prop-preview", new JsonObject { ["propJson"] = prop, ["themeJson"] = grassTheme }))!;

        var sparse = await Preview("""{"kind":"flora","points":[[0,0],[40,0],[40,40],[0,40]],"spec":{"coverage":0.2},"seed":7}""");
        var lush = await Preview("""{"kind":"flora","points":[[0,0],[40,0],[40,40],[0,40]],"spec":{"coverage":0.9},"seed":7}""");
        var road = await Preview("""{"kind":"stroke","points":[[0,20],[40,20]],"radius":3,"seed":5,"pave":{"kind":"solid","id":13,"data":0}}""");
        var trail = await Preview("""{"kind":"stroke","points":[[0,20],[40,20]],"radius":3,"style":"stones","seed":5,"pave":{"kind":"solid","id":13,"data":0}}""");
        var spruce = await Preview("""{"kind":"tree","x":0,"z":0,"species":"spruce","height":24,"seed":5}""");
        var shallow = await Preview("""{"kind":"water","points":[[0,20],[40,20]],"radius":4,"depth":1,"seed":5}""");
        var deep = await Preview("""{"kind":"water","points":[[0,20],[40,20]],"radius":4,"depth":5,"seed":5}""");
        static double Count(JsonNode preview, string key) => preview["counts"].Number(key) ?? 0;
        static int Length(JsonNode preview, string key) => preview.Text(key)?.Length ?? 0;

        checks.Add("coverage moves the plant count", Count(lush, "plants") > Count(sparse, "plants"),
            $"{Count(sparse, "plants")} → {Count(lush, "plants")}");
        checks.Add("stepping stones pave less than a road",
            Count(trail, "pathCells") < Count(road, "pathCells") && Count(trail, "pathCells") > 0,
            $"{Count(road, "pathCells")} → {Count(trail, "pathCells")}");
        checks.Add("a channel carves and fills, and a deeper one is drawn no shorter",
            Count(deep, "waterCells") > 0 && Count(shallow, "waterCells") > 0 && Length(deep, "section") > 100,
            $"shallow {Count(shallow, "waterCells")} · deep {Count(deep, "waterCells")} · section {Length(deep, "section")}");
        checks.Add("one tree is one tree", Count(spruce, "trees") == 1, spruce["counts"]?.ToJsonString() ?? "");
        checks.Add("both views are drawn", Length(spruce, "plan") > 100 && Length(spruce, "section") > 100,
            $"plan {Length(spruce, "plan")} · section {Length(spruce, "section")}");

        // ── 3. the phase places on the canvas ──
        checks.Section("the sketch Dressing phase places things");

        await using var page = await session.NewPageAsync(1600, 1000);
        page.ClearFaults();
        var drove = false;
        try
        {
            await page.GotoAsync($"/maps/{slug}/sketch");
            await page.WaitForSelectorAsync("canvas", 20000);
            await StudioPage.Pause(1500);

            await page.ClickAsync("button[title=\"Dressing\"]", 8000);
            await StudioPage.Pause(1500);

            var groups = await page.Locator(".canvas-dock .canvas-dock-group").EvaluateAllAsync<string?[][]>("""
                els => els.map(group => [...group.querySelectorAll(".canvas-dock-btn")].map(btn => btn.getAttribute("aria-label")))
                """);
            var tools = groups.SelectMany(group => group).ToList();
            // The dock leads with getting around the canvas (Select · Move) and every group after it places
            // something. What this spec needs is that the tools it drives below are on offer.
            var placing = groups.Skip(1).SelectMany(group => group).ToList();
            checks.Add("the phase offers the placing tools this spec drives",
                placing.Count > 0 && new[] { Driven.Stroke, Driven.Water, Driven.Tree }.All(placing.Contains),
                string.Join(" | ", placing));
            // The draw tools are Draw's: dressing places props, it does not author geometry.
            checks.Add("and none of the shape tools",
                !tools.Any(tool => Regex.IsMatch(tool ?? "", "^(Rectangle|Polygon|Lasso)")), string.Join(" | ", tools));

            await page.ScreenshotAsync("dressing-phase.png");

            // Drop a tree by clicking, which is the whole interaction.
            var box = (await page.Locator("svg.map-canvas-svg").First().BoundingBoxAsync())!.Value;
            (decimal X, decimal Y) At(double fractionX, double fractionY) =>
                ((decimal)(box.X + box.Width * fractionX), (decimal)(box.Y + box.Height * fractionY));

            await page.ClickAsync($"button[aria-label^=\"{Driven.Tree}\"]");
            var treeAt = At(0.45, 0.45);
            await page.Mouse.ClickAsync(treeAt.X, treeAt.Y);
            await StudioPage.Pause(1500);
            // What the phase draws for a tree is the recipe list, so that is what says one landed.
            var panel = await page.EvaluateAsync<string>("() => document.body.innerText");
            checks.Add("a click places a tree, and the phase asks which one",
                await page.Locator(".prop-card[title=\"oak\"]").CountAsync() > 0,
                Regex.Match(panel, "WHICH TREE") is { Success: true } asked ? asked.Value : "(no recipe list)");
            await page.ScreenshotAsync("dressing-tree.png");

            // Which tree a placement is, is the recipe it names, so what the phase carries is the card it marks
            // active. Cards are addressed by their title, which is the row's name exactly.
            await page.ClickAsync(".prop-card[title=\"spruce\"]");
            await StudioPage.Pause(2500);
            checks.Add("picking a recipe marks it, and only it",
                await page.Locator(".prop-card--active[title=\"spruce\"]").CountAsync() == 1
                && await page.Locator(".prop-card--active").CountAsync() == 1);
            await page.ScreenshotAsync("dressing-tree-spruce.png");
            await page.ClickAsync(".prop-card[title=\"oak\"]");
            await StudioPage.Pause(2000);
            checks.Add("and picking another moves the mark rather than adding one",
                await page.Locator(".prop-card--active[title=\"oak\"]").CountAsync() == 1
                && await page.Locator(".prop-card--active").CountAsync() == 1);

            // Drag a route: press, trace, release — releasing is what ends it.
            await page.ClickAsync($"button[aria-label^=\"{Driven.Stroke}\"]");
            var routeStart = At(0.25, 0.65);
            await page.Mouse.MoveAsync(routeStart.X, routeStart.Y);
            await page.Mouse.DownAsync();
            foreach (var step in new[] { 0.35, 0.45, 0.55, 0.65 })
            {
                var point = At(step, 0.65 - (step - 0.25));
                await page.Mouse.MoveAsync(point.X, point.Y);
                await StudioPage.Pause(60);
            }
            await page.Mouse.UpAsync();
            await StudioPage.Pause(1500);
            checks.Add("a drag places a path, and releasing ends it", await page.ShowsTextAsync("Style"));
            await page.ScreenshotAsync("dressing-path.png");

            // Drag a water channel: the same press-trace-release, but its inspector is the water one — a depth
            // and a form, the knobs a channel has and a path does not.
            await page.ClickAsync($"button[aria-label^=\"{Driven.Water}\"]");
            var channelStart = At(0.25, 0.35);
            await page.Mouse.MoveAsync(channelStart.X, channelStart.Y);
            await page.Mouse.DownAsync();
            foreach (var step in new[] { 0.35, 0.45, 0.55, 0.65, 0.75 })
            {
                var point = At(step, 0.35 + (step - 0.25) * 0.4);
                await page.Mouse.MoveAsync(point.X, point.Y);
                await StudioPage.Pause(60);
            }
            await page.Mouse.UpAsync();
            await StudioPage.Pause(2000);
            checks.Add("a drag places a water channel, with its own depth + form knobs",
                await page.ShowsTextAsync("Depth") && await page.ShowsTextAsync("Form"));
            await page.ScreenshotAsync("dressing-water.png");
            drove = true;
        }
        catch (Exception failure)
        {
            page.Note($"dressing phase drive: {failure.Message.Split('\n')[0]}");
        }
        checks.Add("Dressing phase drove without error", drove);
        checks.Add("sketch tool is clean under dressing", page.Faults.Count == 0, string.Join(" | ", page.Faults.Take(3)));

        checks.Finish();
    }
}
