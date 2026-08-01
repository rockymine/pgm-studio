#:project ../../src/PgmStudio.Pgm/PgmStudio.Pgm.csproj
#:project ../../src/PgmStudio.Minecraft/PgmStudio.Minecraft.csproj
#:property JsonSerializerIsReflectionEnabledByDefault=true
// build-marker-check: render the build-region marker (ST5) a plan would export, as ASCII.
// Compiles the plan for real (PlanCompiler → terrain columns + build areas), runs the export-time marker
// geometry over it, and draws one grid per build zone: '#' terrain, '+' build region, 'r' marker redstone.
// The per-zone line lengths are printed too, which is what the teaching seed's expectations are stated in.
// `--svg <path>` additionally writes the whole board as one picture, zones labelled with their line lengths.
// Usage: dotnet run tools/deriver/build-marker-check.cs <plan.json> [<plan.json>…] [--svg <out.svg>]
using PgmStudio.Minecraft;
using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Sketch;

var svgFlag = Array.IndexOf(args, "--svg");
var svgPath = svgFlag >= 0 && svgFlag + 1 < args.Length ? args[svgFlag + 1] : null;
var plans = svgFlag >= 0 ? args.Where((_, i) => i != svgFlag && i != svgFlag + 1).ToArray() : args;

if (plans.Length == 0)
{
    Console.WriteLine("usage: dotnet run tools/deriver/build-marker-check.cs <plan.json> [<plan.json>…] [--svg <out.svg>]");
    return;
}

foreach (var path in plans)
{
    if (PlanModel.Parse(File.ReadAllText(path)) is not { } plan)
    {
        Console.WriteLine($"{Path.GetFileName(path)}: PARSE FAILED");
        continue;
    }

    var (layout, intent) = PlanCompiler.Compile(plan);
    var surfaceTop = SketchTerrainBuilder.SurfaceTops(SketchRasterizer.RasterizeColumns(layout.ToJson()));
    var areas = intent.Build!.Areas
        .Select(r => ((int)r.MinX, (int)r.MinZ, (int)r.MaxX, (int)r.MaxZ)).ToList();
    var holes = intent.Build!.Holes
        .Select(r => ((int)r.MinX, (int)r.MinZ, (int)r.MaxX, (int)r.MaxZ)).ToList();
    var marker = BuildMarkerStamper.MarkerCells(areas, holes, surfaceTop).ToHashSet();

    Console.WriteLine($"── {plan.Meta?.Name ?? "(unnamed)"} ({Path.GetFileName(path)})  cell={plan.Globals.Cell}  " +
                      $"{marker.Count} marker block(s) at y={BuildMarkerStamper.MarkerY} ──");

    foreach (var zone in plan.Zones)
    {
        var cell = plan.Globals.Cell;
        var (minX, minZ) = (zone.Rect.X * cell, zone.Rect.Z * cell);
        var (maxX, maxZ) = (minX + zone.Rect.Width * cell - 1, minZ + zone.Rect.Height * cell - 1);
        const int pad = 4;

        Console.WriteLine();
        Console.WriteLine($"  {zone.Id}: build {maxX - minX + 1}×{maxZ - minZ + 1} blocks at " +
                          $"x {minX}..{maxX}, z {minZ}..{maxZ}");
        foreach (var run in Runs(marker, minX - pad, minZ - pad, maxX + pad, maxZ + pad))
            Console.WriteLine($"    {run}");

        for (var z = minZ - pad; z <= maxZ + pad; z++)
        {
            var row = new System.Text.StringBuilder("    ");
            for (var x = minX - pad; x <= maxX + pad; x++)
                row.Append(marker.Contains((x, z)) ? 'r'
                    : surfaceTop.ContainsKey((x, z)) ? '#'
                    : x >= minX && x <= maxX && z >= minZ && z <= maxZ ? '+' : '.');
            Console.WriteLine(row);
        }
    }
    Console.WriteLine();

    if (svgPath is not null)
    {
        var file = plans.Length > 1
            ? Path.Combine(Path.GetDirectoryName(svgPath) ?? ".",
                $"{Path.GetFileNameWithoutExtension(svgPath)}-{Path.GetFileNameWithoutExtension(path)}.svg")
            : svgPath;
        File.WriteAllText(file, Svg(plan, surfaceTop, areas, marker));
        Console.WriteLine($"  wrote {file}");
    }
}

// The whole board as one picture: terrain, build regions, and the marker blocks, each zone labelled with the
// straight runs its outline came out as.
static string Svg(
    PlanModel plan, IReadOnlyDictionary<(int X, int Z), int> surfaceTop,
    List<(int MinX, int MinZ, int MaxX, int MaxZ)> areas, HashSet<(int X, int Z)> marker)
{
    const int scale = 9;             // pixels per block
    const int pad = 5;               // blocks of empty board kept around the content
    const int header = 60;           // room for the title and the legend
    var cell = plan.Globals.Cell;

    var xs = surfaceTop.Keys.Select(k => k.X).Concat(marker.Select(m => m.X)).ToList();
    var zs = surfaceTop.Keys.Select(k => k.Z).Concat(marker.Select(m => m.Z)).ToList();
    var (minX, maxX) = (xs.Min() - pad, xs.Max() + pad);
    var (minZ, maxZ) = (zs.Min() - pad, zs.Max() + pad);
    int Px(int x) => (x - minX) * scale;
    int Pz(int z) => (z - minZ) * scale + header;
    var (width, height) = ((maxX - minX + 1) * scale, (maxZ - minZ + 1) * scale + header + 10);

    var svg = new System.Text.StringBuilder();
    svg.Append($"<svg xmlns='http://www.w3.org/2000/svg' width='{width}' height='{height}' " +
               $"viewBox='0 0 {width} {height}' font-family='ui-sans-serif, system-ui, sans-serif'>");
    svg.Append($"<rect width='{width}' height='{height}' fill='#f7f8fa'/>");
    svg.Append($"<text x='10' y='24' font-size='15' font-weight='600' fill='#1c2530'>" +
               $"{Escape(plan.Meta?.Name ?? "plan")} — build-region outline (ST5), cell {cell}, " +
               $"{marker.Count} marker blocks at y=1</text>");
    var legend = new (string Fill, string Label)[]
        { ("#8e99a6", "terrain"), ("#cfe2f6", "build region"), ("#cf2d24", "marker — redstone at y=1") };
    var legendX = 10;
    foreach (var (fill, label) in legend)
    {
        svg.Append($"<rect x='{legendX}' y='{35}' width='11' height='11' rx='2' fill='{fill}' " +
                   "stroke='#7d8894' stroke-width='0.5'/>");
        svg.Append($"<text x='{legendX + 17}' y='{45}' font-size='11.5' fill='#5c6672'>{label}</text>");
        legendX += 30 + label.Length * 6;
    }

    foreach (var (areaMinX, areaMinZ, areaMaxX, areaMaxZ) in areas)
        svg.Append($"<rect x='{Px(areaMinX)}' y='{Pz(areaMinZ)}' width='{(areaMaxX - areaMinX) * scale}' " +
                   $"height='{(areaMaxZ - areaMinZ) * scale}' fill='#cfe2f6' stroke='#6b9dd0' stroke-width='1'/>");

    foreach (var (x, z) in surfaceTop.Keys.OrderBy(k => k.Z).ThenBy(k => k.X))
        svg.Append($"<rect x='{Px(x)}' y='{Pz(z)}' width='{scale}' height='{scale}' fill='#8e99a6'/>");

    foreach (var (x, z) in marker)
        svg.Append($"<rect x='{Px(x) + 1}' y='{Pz(z) + 1}' width='{scale - 2}' height='{scale - 2}' " +
                   "rx='2' fill='#cf2d24'/>");

    // Zone ids ride inside their own rect — adjacent zones would collide anywhere else on the board.
    foreach (var zone in plan.Zones)
    {
        var (zoneMinX, zoneMinZ) = (zone.Rect.X * cell, zone.Rect.Z * cell);
        var (zoneMaxX, zoneMaxZ) = (zoneMinX + zone.Rect.Width * cell, zoneMinZ + zone.Rect.Height * cell);
        svg.Append($"<text x='{(Px(zoneMinX) + Px(zoneMaxX)) / 2}' y='{(Pz(zoneMinZ) + Pz(zoneMaxZ)) / 2 + 4}' " +
                   $"font-size='10' fill='#41556b' text-anchor='middle'>{Escape(zone.Id)}</text>");
    }

    svg.Append("</svg>");
    return svg.ToString();
}

static string Escape(string text) => text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

// Maximal straight runs of marker blocks in the window, so the drawn shape can be read as lengths.
static List<string> Runs(HashSet<(int X, int Z)> marker, int minX, int minZ, int maxX, int maxZ)
{
    var lines = new List<string>();
    for (var x = minX; x <= maxX; x++)
    {
        var run = 0;
        for (var z = minZ; z <= maxZ + 1; z++)
        {
            if (z <= maxZ && marker.Contains((x, z))) { run++; continue; }
            if (run > 1) lines.Add($"column x={x,-4} {run,2} blocks, z {z - run}..{z - 1}");
            run = 0;
        }
    }
    for (var z = minZ; z <= maxZ; z++)
    {
        var run = 0;
        for (var x = minX; x <= maxX + 1; x++)
        {
            if (x <= maxX && marker.Contains((x, z))) { run++; continue; }
            if (run > 1) lines.Add($"row    z={z,-4} {run,2} blocks, x {x - run}..{x - 1}");
            run = 0;
        }
    }
    return lines;
}
