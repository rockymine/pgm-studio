using System.Globalization;
using PgmStudio.Pgm;
using PgmStudio.Domain;
using PgmStudio.Pgm.Detect;
using PgmStudio.Minecraft;
using PgmStudio.Minecraft.Render;
using PgmStudio.Analysis.Footprint;
using JP = System.Text.Json.Serialization.JsonPropertyNameAttribute;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Suggest;

// Numbers are dot-separated whatever the host's regional settings say — the same pin the API and the client
// hold. A harness that compared derivations under a comma-decimal locale would report differences that are
// the locale's, not the code's, and its numeric CLI arguments (tolerances) would parse a hundredfold out.
var invariant = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentCulture = invariant;
CultureInfo.DefaultThreadCurrentUICulture = invariant;
CultureInfo.CurrentCulture = invariant;
CultureInfo.CurrentUICulture = invariant;

// Corpus round-trip fidelity harness (C# port of tools/roundtrip_check.py).
//
//   check #1 — JSON idempotence (canonical):  ToDict(parse) == ToDict(FromDict(ToDict(parse)))
//              with the derived bounds_2d stripped from regions.
//   check #2 — XML semantic re-parse:          parse -> json -> MapXml -> to_xml -> re-parse,
//              compare named ids + counts.  (enabled once the XML writer lands)
//
// Usage:  dotnet run --project tools/PgmStudio.RoundTrip [root ...] [--verbose]

// Corpus roots from env (PGM_STUDIO_MAPS_ROOTS, semicolon/comma-separated); an explicit [root ...] arg overrides.
string[] defaultRoots = (Environment.GetEnvironmentVariable("PGM_STUDIO_MAPS_ROOTS") ?? "")
    .Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
var verbose = args.Contains("--verbose");

// --goldens [featureRoot] [--update]: run the four map-level derivations — region categories,
// buildability, traversability, wool availability — over the whole corpus and compare each map against the
// recorded digest, so a change that moves a verdict on a real map says which maps. A feature root (a
// directory of <slug>/*.parquet world-scan output) enables the three that read terrain; without one only the
// region categorizer runs. --update re-records instead of comparing.
var goldIdx = Array.IndexOf(args, "--goldens");
if (goldIdx >= 0)
{
    var featureRoot = goldIdx + 1 < args.Length && !args[goldIdx + 1].StartsWith("--") ? args[goldIdx + 1] : null;
    var goldRoots = args.Where(a => !a.StartsWith("--") && a != featureRoot).ToArray();
    return await RunGoldens(goldRoots.Length > 0 ? goldRoots : defaultRoots, featureRoot,
                            args.Contains("--update"), verbose);
}

// --readworld <regionDir>: decode all .mca and tally block ids of interest (validates AnvilRegion).
var rwIdx = Array.IndexOf(args, "--readworld");
if (rwIdx >= 0 && rwIdx + 1 < args.Length)
{
    var dir = args[rwIdx + 1];
    var counts = new Dictionary<int, long>();
    long total = 0;
    foreach (var mca in Directory.GetFiles(dir, "*.mca"))
        foreach (var chunk in PgmStudio.Minecraft.Anvil.AnvilRegion.ReadChunks(mca))
            foreach (var blk in PgmStudio.Minecraft.Anvil.AnvilRegion.Blocks(chunk))
            {
                total++;
                counts[blk.Id] = counts.GetValueOrDefault(blk.Id) + 1;
            }
    var names = new Dictionary<int, string> { [35] = "wool", [42] = "iron_block", [41] = "gold_block", [57] = "diamond_block", [54] = "chest", [146] = "trapped_chest", [52] = "mob_spawner" };
    Console.WriteLine($"readworld: {total} non-air blocks across {Directory.GetFiles(dir, "*.mca").Length} region file(s)");
    foreach (var (id, name) in names) Console.WriteLine($"  {name} (id {id}): {counts.GetValueOrDefault(id)}");
    return 0;
}

// --extract <regionDir> <oracleDir>: run every feature extractor over the .mca world and compare,
// row-for-row, against the Python parquet oracles (wools/resources/chests/spawners/layer_segments).
var exIdx = Array.IndexOf(args, "--extract");
if (exIdx >= 0 && exIdx + 2 < args.Length)
    return await RunExtractParity(args[exIdx + 1], args[exIdx + 2]);

// --scan-out <mapDir> <outRoot>: scan ONE map's world (mapDir/region) + parse mapDir/map.xml and write the
// full importer-ready output dir <outRoot>/<slug>/ (xml_data.json + feature parquets + layer.parquet +
// islands.json + map_config.json) WITHOUT a database. Run the heavy world scan on a fast host, then ingest
// the cheap files with `dotnet run --project src/PgmStudio.Import <outRoot>`.
var soIdx = Array.IndexOf(args, "--scan-out");
if (soIdx >= 0 && soIdx + 2 < args.Length)
    return await RunScanOut(args[soIdx + 1], args[soIdx + 2]);

// --scan-out-all <mapsRoot> <outRoot>: --scan-out for every map folder under mapsRoot (one that has region/).
var soaIdx = Array.IndexOf(args, "--scan-out-all");
if (soaIdx >= 0 && soaIdx + 2 < args.Length)
    return await RunScanOutAll(args[soaIdx + 1], args[soaIdx + 2]);

// --island-sketch <mapDir> <outJson>: scan a map and simplify each island outline (+ holes) into one
// editable SketchLayout (IslandSimplifier) — for previewing what gets stored as the island_sketch_json artifact.
var isIdx2 = Array.IndexOf(args, "--island-sketch");
if (isIdx2 >= 0 && isIdx2 + 2 < args.Length)
    return RunIslandSketch(args[isIdx2 + 1], args[isIdx2 + 2]);

// --island-stairaware <mapDir>: scan a map and compare the cleaned-base height-aware detection against the
// stair-aware detection (CleanColumns → DetectStairAware) — island counts + sizes side by side.
var saIdx = Array.IndexOf(args, "--island-stairaware");
if (saIdx >= 0 && saIdx + 1 < args.Length)
    return RunIslandStairAware(args[saIdx + 1]);

// --dump-cleanbase <mapDir> <outCsv>: dump the cleaned-base column profile (x,z,baseY,surfaces) for analysis.
var dcbIdx = Array.IndexOf(args, "--dump-cleanbase");
if (dcbIdx >= 0 && dcbIdx + 2 < args.Length)
{
    var rd = Path.Combine(args[dcbIdx + 1], "region");
    var ch = Directory.GetFiles(rd, "*.mca").SelectMany(PgmStudio.Minecraft.Anvil.AnvilRegion.ReadChunks).ToList();
    using var w = new StreamWriter(args[dcbIdx + 2]);
    w.WriteLine("x,z,baseY,blockId");
    foreach (var c in PgmStudio.Minecraft.Anvil.LayerExtractors.CleanBase(ch))
        w.WriteLine($"{c.WorldX},{c.WorldZ},{c.WorldY},{c.BlockId}");
    Console.WriteLine($"dumped clean-base columns → {args[dcbIdx + 2]}");
    return 0;
}

// --islands <regionDir> <oracleDir>: surface scan + island detection vs layer.parquet/islands.json.
var isIdx = Array.IndexOf(args, "--islands");
if (isIdx >= 0 && isIdx + 2 < args.Length)
    return await RunIslandParity(args[isIdx + 1], args[isIdx + 2]);

// --clean-base-render <regionDir> <outSvg>: ND2/A5 cleaned-base island detection (noise-excluded base +
// height-aware connectivity + floating-mass prune, with a y0/bedrock fallback) rendered as an SVG of the
// island outlines — the render-comparison pass for the cleaned base on real worlds.
var cbrIdx = Array.IndexOf(args, "--clean-base-render");
if (cbrIdx >= 0 && cbrIdx + 2 < args.Length)
    return RunCleanBaseRender(args[cbrIdx + 1], args[cbrIdx + 2]);

// --topdown <regionDir> <outPng> [--map <map.xml>] [--scale N] [--ymax Y] [--layer ground|structure|foliage|objectives]
// [--material] [--dressing <layout.json>]: the world's surface as a top-down PNG. The default reading sorts
// every column into RenderCategory and false-colours it for legibility (foliage/structure/ground/water/void,
// each a legend entry baked onto the image); --material switches back to the old per-block BlockPalette
// colouring, for checking a theme's actual paint rather than the map's shape. --layer isolates one category
// (or the map.xml overlay alone, for "objectives") instead of drawing the combined view. --map overlays what
// the XML declares (objectives, spawns, apply-rule boxes) so the geometry can be read against the terrain.
// --dressing switches --layer foliage from the leaf/log mass to each tree's own point and measured crown
// radius (docs/world-export/decoration.md §6) — it names the SketchLayout document the region was built from,
// which a bare region directory carries no other way to reach; a scanned or undocumented world has none, so
// the layer falls back to the mass reading it always had.
var topIdx = Array.IndexOf(args, "--topdown");
if (topIdx >= 0 && topIdx + 2 < args.Length)
{
    var mapIdx = Array.IndexOf(args, "--map");
    var scaleIdx = Array.IndexOf(args, "--scale");
    var yMaxIdx = Array.IndexOf(args, "--ymax");
    var layerIdx = Array.IndexOf(args, "--layer");
    var dressingIdx = Array.IndexOf(args, "--dressing");
    var overlayMap = mapIdx >= 0 && mapIdx + 1 < args.Length ? MapParser.Parse(args[mapIdx + 1]) : null;
    var layer = layerIdx >= 0 && layerIdx + 1 < args.Length ? args[layerIdx + 1].ToLowerInvariant() switch
    {
        "ground" => TopDownLayer.Ground,
        "structure" => TopDownLayer.Structure,
        "foliage" => TopDownLayer.Foliage,
        "objectives" => TopDownLayer.Objectives,
        "combined" => TopDownLayer.Combined,
        var other => throw new ArgumentException($"no top-down layer '{other}' — have: ground, structure, foliage, objectives, combined"),
    } : TopDownLayer.Combined;
    var treePoints = dressingIdx >= 0 && dressingIdx + 1 < args.Length
        ? PgmStudio.Export.DressingScope.TreeFootprints(File.ReadAllText(args[dressingIdx + 1]))
        : null;
    return TopDownRender.Run(
        args[topIdx + 1], args[topIdx + 2], overlayMap,
        scaleIdx >= 0 && scaleIdx + 1 < args.Length && int.TryParse(args[scaleIdx + 1], out var topScale) ? Math.Max(1, topScale) : 3,
        yMaxIdx >= 0 && yMaxIdx + 1 < args.Length && int.TryParse(args[yMaxIdx + 1], out var topYMax) ? topYMax : null,
        args.Contains("--material") ? TopDownColorMode.Material : TopDownColorMode.Category, layer, treePoints);
}

// --underground <regionDir> <outPng> [--scale N] [--band <yMin> <yMax>] [--ores]: the world below its own
// per-column roof — enclosed space shaded by depth, with rails/supports/cobweb/chests/spawners painted over
// it, so a cave system or a mineshaft reads as a shape instead of as a surface.
var underIdx = Array.IndexOf(args, "--underground");
if (underIdx >= 0 && underIdx + 2 < args.Length)
{
    var scaleAt = Array.IndexOf(args, "--scale");
    var bandAt = Array.IndexOf(args, "--band");
    int? bandMin = null, bandMax = null;
    if (bandAt >= 0 && bandAt + 2 < args.Length && int.TryParse(args[bandAt + 1], out var lowY) && int.TryParse(args[bandAt + 2], out var highY))
        (bandMin, bandMax) = (Math.Min(lowY, highY), Math.Max(lowY, highY));
    return PgmStudio.RoundTrip.UndergroundRender.Run(
        args[underIdx + 1], args[underIdx + 2],
        scaleAt >= 0 && scaleAt + 1 < args.Length && int.TryParse(args[scaleAt + 1], out var underScale) ? Math.Max(1, underScale) : 3,
        bandMin, bandMax, args.Contains("--ores"));
}

// --heightmap <regionDir> <outPng> [--scale N] [--contour N] [--grey] [--water]: terrain shape alone —
// ground height (vegetation, tree trunks, surface furniture and liquids skipped) as a hypsometric ramp under
// hillshade, with contour lines. Nothing is coloured by what the ground is made of.
var heightIdx = Array.IndexOf(args, "--heightmap");
if (heightIdx >= 0 && heightIdx + 2 < args.Length)
{
    var scaleWhere = Array.IndexOf(args, "--scale");
    var contourWhere = Array.IndexOf(args, "--contour");
    return HeightProfileRender.Run(
        args[heightIdx + 1], args[heightIdx + 2],
        scaleWhere >= 0 && scaleWhere + 1 < args.Length && int.TryParse(args[scaleWhere + 1], out var heightScale) ? Math.Max(1, heightScale) : 3,
        contourWhere >= 0 && contourWhere + 1 < args.Length && int.TryParse(args[contourWhere + 1], out var interval) ? interval : 0,
        args.Contains("--grey"), args.Contains("--water"));
}

// --column <regionDir> <x> <z> [x z ...]: one or more vertical columns, bedrock to sky, every solid block
// named — the textual section read-back. What column-probe scripts have been hand-written for outside this
// repo, because no renderer here answers "what stands at this exact point".
var columnIdx = Array.IndexOf(args, "--column");
if (columnIdx >= 0 && columnIdx + 3 < args.Length)
{
    var columns = new List<(int X, int Z)>();
    var cursor = columnIdx + 2;
    while (cursor + 1 < args.Length && int.TryParse(args[cursor], out var columnX) && int.TryParse(args[cursor + 1], out var columnZ))
    {
        columns.Add((columnX, columnZ));
        cursor += 2;
    }
    return ColumnReport.Run(args[columnIdx + 1], columns);
}

// --section <regionDir> <outPng> --x <lo> <hi> --z <fixed> | --z <lo> <hi> --x <fixed> [--scale N] [--ymin Y]
// [--ymax Y] [--ticks N]: a vertical slice through the world along one axis-aligned line, drawn as an image —
// the picture half of the section read-back. A riser, a ramp's step heights, a building's storeys and a
// goal's clearance over the ground are none of them visible in a plan view; this is the one renderer that
// keeps Y. One of --x/--z takes two values (the range the cut runs along), the other takes one (where the
// line sits on the fixed axis).
var sectionIdx = Array.IndexOf(args, "--section");
if (sectionIdx >= 0 && sectionIdx + 2 < args.Length)
{
    var xAt = Array.IndexOf(args, "--x");
    var zAt = Array.IndexOf(args, "--z");

    bool RangeAt(int at, out int low, out int high)
    {
        low = high = 0;
        return at >= 0 && at + 2 < args.Length && int.TryParse(args[at + 1], out low) && int.TryParse(args[at + 2], out high);
    }
    bool FixedAt(int at, out int value)
    {
        value = 0;
        return at >= 0 && at + 1 < args.Length && int.TryParse(args[at + 1], out value);
    }

    SectionAxis sectionAxis;
    int sectionRangeMin, sectionRangeMax, sectionFixed;
    if (RangeAt(xAt, out var xLow, out var xHigh) && FixedAt(zAt, out var zFixed))
        (sectionAxis, sectionRangeMin, sectionRangeMax, sectionFixed) = (SectionAxis.AlongX, Math.Min(xLow, xHigh), Math.Max(xLow, xHigh), zFixed);
    else if (RangeAt(zAt, out var zLow, out var zHigh) && FixedAt(xAt, out var xFixed))
        (sectionAxis, sectionRangeMin, sectionRangeMax, sectionFixed) = (SectionAxis.AlongZ, Math.Min(zLow, zHigh), Math.Max(zLow, zHigh), xFixed);
    else
    {
        Console.Error.WriteLine("--section needs a ranged --x plus a fixed --z, or a ranged --z plus a fixed --x");
        return 1;
    }

    var sectionScaleAt = Array.IndexOf(args, "--scale");
    var yMinAt = Array.IndexOf(args, "--ymin");
    var yMaxAt = Array.IndexOf(args, "--ymax");
    var ticksAt = Array.IndexOf(args, "--ticks");
    return SectionRender.Run(
        args[sectionIdx + 1], args[sectionIdx + 2], sectionAxis, sectionRangeMin, sectionRangeMax, sectionFixed,
        sectionScaleAt >= 0 && sectionScaleAt + 1 < args.Length && int.TryParse(args[sectionScaleAt + 1], out var sectionScale) ? Math.Max(1, sectionScale) : 4,
        yMinAt >= 0 && yMinAt + 1 < args.Length && int.TryParse(args[yMinAt + 1], out var sectionYMin) ? sectionYMin : null,
        yMaxAt >= 0 && yMaxAt + 1 < args.Length && int.TryParse(args[yMaxAt + 1], out var sectionYMax) ? sectionYMax : null,
        ticksAt >= 0 && ticksAt + 1 < args.Length && int.TryParse(args[ticksAt + 1], out var sectionTicks) ? Math.Max(1, sectionTicks) : 8);
}

// --traversability-map <regionDir> <outPng> [--map <mapXmlPath>] [--scale N]: spawn/wool/monument/core
// connectivity over the navigable columns (ground + 2 blocks headroom, plus any void column the map's own
// buildable-region apply rule opens to bridging — requires --map to read that wiring), 4-connected
// components coloured so one dominant colour reading through every marker is a connected board. Distinct
// from --traversability (the Python-parity harness over parquet features) — this is the stage-image render.
var travMapIdx = Array.IndexOf(args, "--traversability-map");
if (travMapIdx >= 0 && travMapIdx + 2 < args.Length)
{
    var mapAt = Array.IndexOf(args, "--map");
    var scaleAt = Array.IndexOf(args, "--scale");
    var travMap = mapAt >= 0 && mapAt + 1 < args.Length ? MapParser.Parse(args[mapAt + 1]) : null;
    return TraversabilityRender.Run(
        args[travMapIdx + 1], args[travMapIdx + 2], travMap,
        scaleAt >= 0 && scaleAt + 1 < args.Length && int.TryParse(args[scaleAt + 1], out var travScale) ? Math.Max(1, travScale) : 3);
}

// --structures <regionDir> <outPng> [--scale N] [--min-area N] [--max-step N]: built structures found by the
// material on top of each column (not by elevation alone, which cannot tell a hut from a boulder), joined into
// components across a step of at most --max-step so a wall does not fuse into a painted plaza of the same
// material, and measured against the natural ground just outside their own footprint.
var structIdx = Array.IndexOf(args, "--structures");
if (structIdx >= 0 && structIdx + 2 < args.Length)
{
    var scaleSlot = Array.IndexOf(args, "--scale");
    var areaSlot = Array.IndexOf(args, "--min-area");
    var stepSlot = Array.IndexOf(args, "--max-step");
    return StructureFinder.Run(
        args[structIdx + 1], args[structIdx + 2],
        scaleSlot >= 0 && scaleSlot + 1 < args.Length && int.TryParse(args[scaleSlot + 1], out var structScale) ? Math.Max(1, structScale) : 3,
        areaSlot >= 0 && areaSlot + 1 < args.Length && int.TryParse(args[areaSlot + 1], out var minArea) ? Math.Max(1, minArea) : 12,
        stepSlot >= 0 && stepSlot + 1 < args.Length && int.TryParse(args[stepSlot + 1], out var maxStep) ? Math.Max(1, maxStep) : StructureFinder.DefaultMaximumStep);
}

// --mirror <regionDir> <outPng> [--mode <symmetry>] [--scale N] [--center <cx> <cz>]: each column against the
// column its own orbit lands on, with the ones that disagree marked. The one picture read off the blocks
// alone — every other structure render draws its extent from the provenance record, so a claim that is wrong
// about where its blocks are draws a building where none stands. A mirrored board is checked for exactly this
// and an eye cannot answer it. The mode is the symmetry the board was laid to (mirror_x/z/d1/d2, rot_180,
// rot_90); with none stated the report says so rather than guessing one.
var mirrorIdx = Array.IndexOf(args, "--mirror");
if (mirrorIdx >= 0 && mirrorIdx + 2 < args.Length)
{
    var modeAt = Array.IndexOf(args, "--mode");
    var mirrorScaleAt = Array.IndexOf(args, "--scale");
    var centreAt = Array.IndexOf(args, "--center");
    var centreX = 0.0;
    var centreZ = 0.0;
    if (centreAt >= 0 && centreAt + 2 < args.Length)
    {
        double.TryParse(args[centreAt + 1], CultureInfo.InvariantCulture, out centreX);
        double.TryParse(args[centreAt + 2], CultureInfo.InvariantCulture, out centreZ);
    }
    return MirrorReport.Run(
        args[mirrorIdx + 1], args[mirrorIdx + 2],
        mirrorScaleAt >= 0 && mirrorScaleAt + 1 < args.Length && int.TryParse(args[mirrorScaleAt + 1], out var mirrorScale) ? Math.Max(1, mirrorScale) : 3,
        modeAt >= 0 && modeAt + 1 < args.Length ? args[modeAt + 1] : null,
        centreX, centreZ);
}

// --flora <regionDir> <outPng> --path <id[:data],...> [--scale N]: trees separated from structural timber by
// log connectivity and named by their canopy, plus the paved routes traced as connected surface components.
// The path palette is per map — the same block is a road on one world and bulk terrain on another.
var floraIdx = Array.IndexOf(args, "--flora");
if (floraIdx >= 0 && floraIdx + 2 < args.Length)
{
    var pathAt = Array.IndexOf(args, "--path");
    var pathSpec = new List<(int Id, int Data)>();
    if (pathAt >= 0 && pathAt + 1 < args.Length)
        foreach (var token in args[pathAt + 1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = token.Split(':');
            if (!int.TryParse(parts[0], out var blockId)) continue;
            pathSpec.Add((blockId, parts.Length > 1 && int.TryParse(parts[1], out var blockData) ? blockData : -1));
        }
    if (pathSpec.Count == 0) { Console.Error.WriteLine("--flora needs --path <id[:data],...>"); return 1; }

    // Bridge materials only count where they span water, so the same stone brick can pave a crossing here
    // and floor a building elsewhere without the two being confused.
    var bridgeAt = Array.IndexOf(args, "--bridge");
    var bridgeSpec = new List<(int Id, int Data)>();
    if (bridgeAt >= 0 && bridgeAt + 1 < args.Length)
        foreach (var token in args[bridgeAt + 1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = token.Split(':');
            if (!int.TryParse(parts[0], out var blockId)) continue;
            bridgeSpec.Add((blockId, parts.Length > 1 && int.TryParse(parts[1], out var blockData) ? blockData : -1));
        }

    var floraScale = Array.IndexOf(args, "--scale");
    return PgmStudio.RoundTrip.FloraRender.Run(args[floraIdx + 1], args[floraIdx + 2],
        floraScale >= 0 && floraScale + 1 < args.Length && int.TryParse(args[floraScale + 1], out var scaleValue) ? Math.Max(1, scaleValue) : 3,
        pathSpec, bridgeSpec);
}

// --buildings <regionDir> <outPng> --roof <id[:data],...> [--scale N] [--min-area N]: buildings found from
// their roofs, kept only where a solid run reaches the terrain — a roofed structure hanging in the air is
// not a building, whatever it is covered with. A region carrying a recorded WorldProvenance sidecar declines
// this guess entirely rather than run it: --structures and --topdown --layer structure already read that
// region's exact census, and a roof-material heuristic tuned for a corpus house has no reason to be trusted
// over it on a world the studio built.
var buildIdx = Array.IndexOf(args, "--buildings");
if (buildIdx >= 0 && buildIdx + 2 < args.Length)
{
    var roofAt = Array.IndexOf(args, "--roof");
    var roofSpec = new List<(int Id, int Data)>();
    if (roofAt >= 0 && roofAt + 1 < args.Length)
        foreach (var token in args[roofAt + 1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = token.Split(':');
            if (!int.TryParse(parts[0], out var blockId)) continue;
            roofSpec.Add((blockId, parts.Length > 1 && int.TryParse(parts[1], out var blockData) ? blockData : -1));
        }

    var rimAt = Array.IndexOf(args, "--rim");
    var rimSpec = new List<(int Id, int Data)>();
    if (rimAt >= 0 && rimAt + 1 < args.Length)
        foreach (var token in args[rimAt + 1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = token.Split(':');
            if (!int.TryParse(parts[0], out var blockId)) continue;
            rimSpec.Add((blockId, parts.Length > 1 && int.TryParse(parts[1], out var blockData) ? blockData : -1));
        }

    var buildScale = Array.IndexOf(args, "--scale");
    var buildArea = Array.IndexOf(args, "--min-area");
    return PgmStudio.RoundTrip.BuildingFinder.Run(args[buildIdx + 1], args[buildIdx + 2],
        buildScale >= 0 && buildScale + 1 < args.Length && int.TryParse(args[buildScale + 1], out var roofScale) ? Math.Max(1, roofScale) : 3,
        roofSpec, rimSpec,
        buildArea >= 0 && buildArea + 1 < args.Length && int.TryParse(args[buildArea + 1], out var roofArea) ? Math.Max(1, roofArea) : 9,
        Array.IndexOf(args, "--min-height") is var heightAt && heightAt >= 0 && heightAt + 1 < args.Length
            && int.TryParse(args[heightAt + 1], out var roofHeight) ? roofHeight : 3,
        Array.IndexOf(args, "--min-side") is var sideAt && sideAt >= 0 && sideAt + 1 < args.Length
            && int.TryParse(args[sideAt + 1], out var roofSide) ? roofSide : 6,
        Array.IndexOf(args, "--max-step") is var stepAt && stepAt >= 0 && stepAt + 1 < args.Length
            && int.TryParse(args[stepAt + 1], out var roofStep) ? roofStep : 4);
}

// --surface <regionDir> <outPng> [--scale N] [--top N]: what the ground is made of once decoration, water
// and structure are set aside — the material histogram, what each decoration grows on, what lies under the
// water, whether each material was scattered or laid in fields, and which full cubes on the board the
// terrain-paint vocabulary does not name at all (magenta on the image, counted rather than just shown).
var surfIdx = Array.IndexOf(args, "--surface");
if (surfIdx >= 0 && surfIdx + 2 < args.Length)
{
    var surfScale = Array.IndexOf(args, "--scale");
    var surfTop = Array.IndexOf(args, "--top");
    return SurfaceReport.Run(args[surfIdx + 1], args[surfIdx + 2],
        surfScale >= 0 && surfScale + 1 < args.Length && int.TryParse(args[surfScale + 1], out var sScale) ? Math.Max(1, sScale) : 3,
        surfTop >= 0 && surfTop + 1 < args.Length && int.TryParse(args[surfTop + 1], out var sTop) ? Math.Max(1, sTop) : 10);
}

// --resources <regionDir> <outPng> [--scale N]: what the map gives a player to take and what it charges —
// mineral blocks and ores as 6-connected deposits, each with the cover standing over its shallowest block,
// how open its faces are, and whether the two halves of the world were given the same.
var resIdx = Array.IndexOf(args, "--resources");
if (resIdx >= 0 && resIdx + 2 < args.Length)
{
    var resScale = Array.IndexOf(args, "--scale");
    return PgmStudio.RoundTrip.ResourceReport.Run(args[resIdx + 1], args[resIdx + 2],
        resScale >= 0 && resScale + 1 < args.Length && int.TryParse(args[resScale + 1], out var rScale) ? Math.Max(1, rScale) : 3);
}

// --island-study <regionDir> <outJson> [tolerance]: cleaned-base islands with their polygons (exterior +
// holes) both raw and Douglas-Peucker-simplified, emitted as JSON for studying real-map shapes.
var islandStudyIdx = Array.IndexOf(args, "--island-study");
if (islandStudyIdx >= 0 && islandStudyIdx + 2 < args.Length)
    return RunIslandStudy(args[islandStudyIdx + 1], args[islandStudyIdx + 2],
        islandStudyIdx + 3 < args.Length && double.TryParse(args[islandStudyIdx + 3], out var studyTol) ? studyTol : 2.0);

// --skeleton-study <regionDir> <map.xml> <outJson> [tolerance]: each island's simplified polygon plus its
// centerline graph (thinning → merge junction blobs → anchor-aware prune using the map.xml objectives as
// fixed nodes) for studying lane structure and where objectives sit.
var skelStudyIdx = Array.IndexOf(args, "--skeleton-study");
if (skelStudyIdx >= 0 && skelStudyIdx + 3 < args.Length)
    return RunSkeletonStudy(args[skelStudyIdx + 1], args[skelStudyIdx + 2], args[skelStudyIdx + 3],
        skelStudyIdx + 4 < args.Length && double.TryParse(args[skelStudyIdx + 4], out var skTol) ? skTol : 2.5);

// --monument-slices <regionDir> <xml_data.json> <outParquet>: sample the 3×3×5 block volume around
// every wool monument (MonumentSliceExtractor), write monument_slices.parquet, read it back and print
// a validation summary. The monument centres come from xml_data.json (wools[].monuments[].location).
var msIdx = Array.IndexOf(args, "--monument-slices");
if (msIdx >= 0 && msIdx + 3 < args.Length)
    return await RunMonumentSlices(args[msIdx + 1], args[msIdx + 2], args[msIdx + 3]);

// --suggest-monuments <regionDir> <xml_data.json> [--pedestal K] [--label K] [--margin M] [--auto-style]:
// run the authoring-flow MonumentSuggester inside a box derived from the ground-truth monument clusters
// (simulating the box the author draws) and score precision/recall against those monuments.
if (args.Contains("--suggest-monuments-corpus"))
    return RunSuggestMonumentsCorpus(args, defaultRoots, Environment.GetEnvironmentVariable("PGM_STUDIO_OUTPUT_ROOT") ?? "");
var sgIdx = Array.IndexOf(args, "--suggest-monuments");
if (sgIdx >= 0 && sgIdx + 2 < args.Length)
    return RunSuggestMonuments(args, args[sgIdx + 1], args[sgIdx + 2]);

// --authoring-fixture [slug ...] [--out <dir>]: write the *readable* region-authoring split for a
// map — primitives vs composed, each node trimmed to id/type/category/subtype/member_ids/wiring
// (geometry omitted as noise). A review artifact, not a parity check; needs only map.xml — no islands.json
// or pipeline run. Defaults to the region-authoring test maps, writing under tools/out/ — which is ignored,
// because a review artifact is read once and never diffed (`CLAUDE.md`, *Investigation stays local*).
var afIdx = Array.IndexOf(args, "--authoring-fixture");
if (afIdx >= 0)
    return RunAuthoringFixture(args, defaultRoots);

// --includes: the distinct <include> ids per map plus a corpus histogram. The bodies live in the server's
// includes directory and ship with neither the map nor the corpus, so this measures the size of what the
// studio cannot read rather than resolving any of it.
if (args.Contains("--includes"))
    return RunIncludes(defaultRoots, args, verbose);

// --island-erasure: island detection with the stained-glass guess vs with the map's stated phantom erasure.
if (args.Contains("--island-erasure"))
    return RunIslandErasure(defaultRoots, verbose);

// --resolve-includes <includesDir>: parse every map twice — as written, and with the fragments spliced —
// and report what resolving them changes.
var riIdx = Array.IndexOf(args, "--resolve-includes");
if (riIdx >= 0 && riIdx + 1 < args.Length)
    return RunResolveIncludes(defaultRoots, args[riIdx + 1], verbose);

// --water-lanes: run WaterLaneDetector over the corpus and report every lane by form.
if (args.Contains("--water-lanes"))
    return RunWaterLanes(defaultRoots, args, verbose);

// --dump <map.xml>: print canonical ToDict(parse) as indented JSON for diffing against Python.
var dumpIdx = Array.IndexOf(args, "--dump");
if (dumpIdx >= 0 && dumpIdx + 1 < args.Length)
{
    var tree = JsonTree.Canonical(Serializer.ToDict(MapParser.Parse(args[dumpIdx + 1])));
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(tree,
        new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    return 0;
}

var roots = args.Where(a => !a.StartsWith("--")).ToArray();
if (roots.Length == 0) roots = defaultRoots;

var xmls = roots
    .SelectMany(root => Directory.Exists(root)
        ? Directory.GetDirectories(root).Select(d => Path.Combine(d, "map.xml")).Where(File.Exists)
        : Array.Empty<string>())
    .OrderBy(p => p, StringComparer.Ordinal)
    .ToList();

int ok = 0, failed = 0;
var failures = new List<(string slug, string detail)>();

foreach (var xmlPath in xmls)
{
    var slug = Path.GetFileName(Path.GetDirectoryName(xmlPath))!;
    var (passed, detail) = CheckMap(xmlPath);
    if (passed) ok++;
    else { failed++; failures.Add((slug, detail)); }
}

Console.WriteLine($"round-trip (check #1 JSON idempotence + check #2 XML re-parse): {ok} ok, {failed} failed " +
                  $"({xmls.Count} maps across {roots.Length} root(s))");
if (failures.Count > 0)
{
    Console.WriteLine($"\n{failed} failure(s):");
    foreach (var (slug, detail) in verbose ? failures : failures.Take(20))
        Console.WriteLine($"  {slug}: {detail}");
    if (!verbose && failed > 20) Console.WriteLine($"  ... and {failed - 20} more (use --verbose)");
}
return failed == 0 ? 0 : 1;

// What dropping the stained-glass guess and honouring a map's stated phantom erasure does to island
// detection, measured on the real worlds. Each map is scanned twice — once as the blanket exclusion read it,
// once as the map states it — and the difference is the answer. A build-floor sheet that vanishes before play
// must not merge islands; a decorative glass floor that stays must not be deleted.
static int RunIslandErasure(string[] corpusRoots, bool verbose)
{
    var maps = CorpusMaps(corpusRoots)
        .Where(m => Directory.Exists(Path.Combine(Path.GetDirectoryName(m.XmlPath)!, "region")))
        .ToList();

    int changed = 0, withErasure = 0, scanned = 0, unreadable = 0;
    foreach (var (slug, xmlPath) in maps)
    {
        PhantomErasure erased;
        try { erased = PhantomErasure.From(MapParser.Parse(xmlPath)); }
        catch (UnsupportedMapException) { unreadable++; continue; }

        var regionDir = Path.Combine(Path.GetDirectoryName(xmlPath)!, "region");
        List<AnvilRegion.Chunk> chunks;
        try { chunks = Directory.GetFiles(regionDir, "*.mca").SelectMany(AnvilRegion.ReadChunks).ToList(); }
        catch (Exception ex) { Console.WriteLine($"  {slug,-30} unreadable world: {ex.GetType().Name}"); continue; }
        scanned++;
        if (!erased.IsEmpty) withErasure++;

        // The old reading: stained glass excluded in every map, phantoms unread.
        var guessExclude = new HashSet<int>(LayerExtractors.CleanBaseExclude) { 95 };
        var before = IslandDetector.DetectCleanedStairAware(
            LayerExtractors.CleanColumns(chunks, PhantomErasure.None, guessExclude)
                .Select(c => (c.WorldX, c.WorldZ, c.BaseY, c.Surfaces)).ToList(), []);
        var after = IslandDetector.DetectCleanedStairAware(
            LayerExtractors.CleanColumns(chunks, erased)
                .Select(c => (c.WorldX, c.WorldZ, c.BaseY, c.Surfaces)).ToList(), []);

        if (before.Count == after.Count && !verbose) continue;
        changed += before.Count == after.Count ? 0 : 1;
        Console.WriteLine($"  {slug,-30} islands {before.Count}->{after.Count}"
                          + (erased.IsEmpty ? "" : $"   (erases {erased.Boxes.Count} box(es))"));
    }

    Console.WriteLine();
    Console.WriteLine($"island count changed on {changed} of {scanned} scanned map(s); "
                      + $"{withErasure} state a pre-play erasure; {unreadable} outside the supported range");
    return 0;
}

// Every <include> id the corpus references, per map and as a histogram. The studio holds the id and nothing
// else — PGM resolves a fragment out of its own includes directory, which no map folder carries — so this
// measures the unread surface rather than closing it.
static int RunIncludes(string[] corpusRoots, string[] args, bool verbose)
{
    var maps = CorpusMaps(corpusRoots);
    var histogram = new Dictionary<string, int>(StringComparer.Ordinal);
    int withIncludes = 0, unreadable = 0;

    foreach (var (slug, xmlPath) in maps)
    {
        MapXml map;
        try { map = MapParser.Parse(xmlPath); }
        catch (UnsupportedMapException) { unreadable++; continue; }

        var ids = map.Includes.Distinct(StringComparer.Ordinal).OrderBy(i => i, StringComparer.Ordinal).ToList();
        if (ids.Count == 0) continue;
        withIncludes++;
        foreach (var id in map.Includes) histogram[id] = histogram.GetValueOrDefault(id) + 1;
        if (verbose) Console.WriteLine($"  {slug,-34} {string.Join(", ", ids)}");
    }

    Console.WriteLine();
    Console.WriteLine($"includes: {withIncludes} of {maps.Count} maps reference one "
                      + $"({(maps.Count == 0 ? 0 : 100.0 * withIncludes / maps.Count):F0}%); "
                      + $"{histogram.Count} distinct id(s); {unreadable} map(s) outside the supported range");
    foreach (var (id, count) in histogram.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal))
        Console.WriteLine($"  {count,5}  {id}");
    Console.WriteLine();
    Console.WriteLine("Every body above is unresolved: fragments live in the server's includes directory, so");
    Console.WriteLine("whatever rules they define are absent from the documents this studio analyses.");
    return 0;
}

// What resolving the shared fragments changes, per map and in total. Each map is parsed twice — as written,
// and with the library spliced in — because the difference IS the answer: everything the studio was reading
// past when it treated an <include> as absent.
static int RunResolveIncludes(string[] corpusRoots, string includesDir, bool verbose)
{
    var library = IncludeLibrary.Open(includesDir);
    if (library is null) { Console.Error.WriteLine($"no include library at '{includesDir}'"); return 2; }
    Console.WriteLine($"library: {library.AvailableIds.Count()} fragment(s) at {includesDir}\n");

    var maps = CorpusMaps(corpusRoots);
    int changed = 0, gainedGamemode = 0, unresolvable = 0, unreadable = 0;
    var missingIds = new SortedSet<string>(StringComparer.Ordinal);

    foreach (var (slug, xmlPath) in maps)
    {
        MapXml plain, resolvedMap;
        try
        {
            plain = MapParser.Parse(xmlPath);
            resolvedMap = MapParser.Parse(xmlPath, library);
        }
        catch (UnsupportedMapException) { unreadable++; continue; }

        var unresolved = plain.Includes.Except(resolvedMap.ResolvedIncludes, StringComparer.Ordinal).ToList();
        if (unresolved.Count > 0) { unresolvable++; foreach (var id in unresolved) missingIds.Add(id); }

        var deltas = new List<string>();
        void Delta(string label, int before, int after)
        {
            if (before != after) deltas.Add($"{label} {before}->{after}");
        }
        Delta("regions", plain.Regions.Count, resolvedMap.Regions.Count);
        Delta("filters", plain.Filters.Count, resolvedMap.Filters.Count);
        Delta("apply", plain.ApplyRules.Count, resolvedMap.ApplyRules.Count);
        Delta("kits", plain.Kits.Count, resolvedMap.Kits.Count);
        Delta("kill-rewards", plain.KillRewards.Count, resolvedMap.KillRewards.Count);
        Delta("modes", plain.Modes.Count, resolvedMap.Modes.Count);
        Delta("fills", plain.Fills.Count, resolvedMap.Fills.Count);

        var before2 = string.Join("+", plain.Gamemodes);
        var after2 = string.Join("+", resolvedMap.Gamemodes);
        if (before2 != after2)
        {
            deltas.Add($"gamemode [{(before2.Length == 0 ? "none" : before2)}]->[{(after2.Length == 0 ? "none" : after2)}]");
            gainedGamemode++;
        }

        if (deltas.Count == 0) continue;
        changed++;
        if (verbose || deltas.Any(d => d.StartsWith("gamemode")))
            Console.WriteLine($"  {slug,-34} {string.Join("  ", deltas)}");
    }

    Console.WriteLine();
    Console.WriteLine($"resolving changes {changed} of {maps.Count} maps ({unreadable} outside the supported range); "
                      + $"{gainedGamemode} gain a gamemode they were read as not having");
    if (missingIds.Count > 0)
        Console.WriteLine($"{unresolvable} map(s) reference {missingIds.Count} id(s) the library does not hold: "
                          + string.Join(", ", missingIds));
    return 0;
}

// Every water lane the corpus authors, by form. A lane is a route that opens mid-match, so the report is
// grouped by the wiring that opens it — the newest form is one include plus one region.
static int RunWaterLanes(string[] corpusRoots, string[] args, bool verbose)
{
    // Optional: run against maps with their fragments spliced in, which is what the server plays. The verdicts
    // must not change — the include form outranks the fill the fragment brings, and both name one region.
    var libIdx = Array.IndexOf(args, "--includes-dir");
    var library = libIdx >= 0 && libIdx + 1 < args.Length ? IncludeLibrary.Open(args[libIdx + 1]) : null;
    if (library is not null) Console.WriteLine("(reading maps with their includes resolved)\n");

    var maps = CorpusMaps(corpusRoots);
    var byForm = new Dictionary<WaterLaneForm, List<string>>();
    int lanesTotal = 0, mapsWithLanes = 0, unreadable = 0;

    foreach (var (slug, xmlPath) in maps)
    {
        MapXml map;
        try { map = MapParser.Parse(xmlPath, library); }
        catch (UnsupportedMapException) { unreadable++; continue; }

        var lanes = WaterLaneDetector.Detect(map);
        if (lanes.Count == 0) continue;
        mapsWithLanes++;
        lanesTotal += lanes.Count;

        Console.WriteLine($"  {slug}");
        foreach (var lane in lanes)
        {
            if (!byForm.TryGetValue(lane.Form, out var slugs)) byForm[lane.Form] = slugs = [];
            if (!slugs.Contains(slug)) slugs.Add(slug);

            var when = lane.Trigger.Length > 0 ? lane.Trigger : "(fragment)";
            Console.WriteLine($"      {lane.Form,-8} region={lane.RegionId,-24} opens={when,-12} "
                              + $"rects={lane.Footprint.Count} via {lane.Evidence}");
            if (verbose)
                foreach (var rect in lane.Footprint)
                    Console.WriteLine($"          [{rect.MinX},{rect.MinZ}] .. [{rect.MaxX},{rect.MaxZ}]");
        }
    }

    Console.WriteLine();
    Console.WriteLine($"water lanes: {lanesTotal} lane(s) across {mapsWithLanes} of {maps.Count} maps "
                      + $"({unreadable} outside the supported range)");
    foreach (var form in Enum.GetValues<WaterLaneForm>())
        Console.WriteLine($"  {form,-8} {byForm.GetValueOrDefault(form)?.Count ?? 0,3}  "
                          + string.Join(" ", byForm.GetValueOrDefault(form) ?? []));
    return 0;
}

// Every map.xml under the corpus roots, slug-keyed. Roots hold one directory per map.
static List<(string Slug, string XmlPath)> CorpusMaps(string[] corpusRoots) =>
    [.. corpusRoots
        .Where(Directory.Exists)
        .SelectMany(Directory.GetDirectories)
        .Select(d => (Slug: Path.GetFileName(d)!, XmlPath: Path.Combine(d, "map.xml")))
        .Where(m => File.Exists(m.XmlPath))
        .OrderBy(m => m.Slug, StringComparer.Ordinal)];

// ── --goldens: the corpus regression net ────────────────────────────────────────────────────────────────
// Run the four map-level derivations over every corpus map and compare each against the recorded digest.
// The record is meant to be re-recorded when a change is deliberate (--update); what it buys is that the
// change is looked at map by map first, which no synthetic fixture can show.
static async Task<int> RunGoldens(string[] corpusRoots, string? featureRoot, bool update, bool verbose)
{
    var maps = CorpusMaps(corpusRoots);
    if (maps.Count == 0)
    {
        Console.WriteLine("no corpus maps found — pass a root, or set PGM_STUDIO_MAPS_ROOTS");
        return 2;
    }

    var path = Path.Combine([".", .. CorpusGoldens.FilePath]);
    var previous = File.Exists(path) ? CorpusGoldens.Record.Parse(File.ReadAllText(path)) : null;
    var next = await CorpusGoldens.Compute(maps, featureRoot, verbose ? Console.WriteLine : null);

    int moved = 0, added = 0, measured = 0;
    var gone = previous is null ? [] : previous.Maps.Keys.Where(k => !next.Maps.ContainsKey(k)).ToList();
    foreach (var (slug, entry) in next.Maps)
    {
        var was = previous?.Maps.GetValueOrDefault(slug);
        foreach (var derivation in CorpusGoldens.Derivations)
        {
            var now = entry[derivation];
            if (now is null) continue;
            measured++;
            var before = was?[derivation];
            if (before == now) continue;
            if (before is null) { added++; continue; }   // newly measurable — nothing to have moved
            moved++;
            Console.WriteLine($"  {slug} {derivation}");
            Console.WriteLine($"      was  {before}");
            Console.WriteLine($"      now  {now}");
        }
    }
    // A map in the record that this run did not see is usually a narrower root set rather than a deletion,
    // so it is counted always and named only when asked.
    if (verbose) foreach (var slug in gone) Console.WriteLine($"  {slug}: in the record, not in this run");

    Console.WriteLine($"{next.Maps.Count} maps, {measured} derivations measured"
        + (featureRoot is null ? " (no feature root — regions only)" : "")
        + $": {moved} moved, {added} new, {gone.Count} gone");

    if (update)
    {
        File.WriteAllText(path, next.ToJson() + "\n");
        Console.WriteLine($"recorded {path}");
        return 0;
    }
    if (moved > 0)
        Console.WriteLine("a verdict moved on a real map — read the pairs above, then re-record with --update "
                          + "if the change was meant.");
    return moved == 0 ? 0 : 1;
}

static async Task<int> RunExtractParity(string regionDir, string oracleDir)
{
    var mcas = Directory.GetFiles(regionDir, "*.mca");
    IEnumerable<PgmStudio.Minecraft.Anvil.AnvilRegion.Chunk> Chunks() => mcas.SelectMany(PgmStudio.Minecraft.Anvil.AnvilRegion.ReadChunks);

    static string S(object? v) => v?.ToString() ?? "";
    static string N(object? v) => v is null ? "~" : Convert.ToInt64(v).ToString();
    static string B(object? v) => v is null ? "~" : (Convert.ToBoolean(v) ? "1" : "0");

    var fails = 0;
    void Check(string name, IEnumerable<string> mine, IEnumerable<string> oracle)
    {
        var m = new Dictionary<string, int>();
        foreach (var k in mine) m[k] = m.GetValueOrDefault(k) + 1;
        var o = new Dictionary<string, int>();
        foreach (var k in oracle) o[k] = o.GetValueOrDefault(k) + 1;
        int matched = 0, onlyMine = 0, onlyOracle = 0;
        foreach (var (k, c) in m) { matched += Math.Min(c, o.GetValueOrDefault(k)); if (c > o.GetValueOrDefault(k)) onlyMine += c - o.GetValueOrDefault(k); }
        foreach (var (k, c) in o) if (c > m.GetValueOrDefault(k)) onlyOracle += c - m.GetValueOrDefault(k);
        var ok = onlyMine == 0 && onlyOracle == 0;
        if (!ok) fails++;
        Console.WriteLine($"  {(ok ? "OK  " : "FAIL")} {name,-14} mine={m.Values.Sum(),-6} oracle={o.Values.Sum(),-6} matched={matched} onlyMine={onlyMine} onlyOracle={onlyOracle}");
        if (!ok)
            foreach (var k in m.Keys.Where(k => m[k] != o.GetValueOrDefault(k)).Take(4))
                Console.WriteLine($"        e.g. {k}  (mine×{m[k]} oracle×{o.GetValueOrDefault(k)})");
    }

    // wools
    var woolMine = PgmStudio.Minecraft.Anvil.FeatureExtractors.Wools(Chunks())
        .Select(w => $"{w.WorldX},{w.WorldZ},{w.WorldY},{w.Color}");
    var woolOra = (await TryRead(Path.Combine(oracleDir, "wools.parquet")))
        .Select(r => $"{N(r["world_x"])},{N(r["world_z"])},{N(r["world_y"])},{S(r["color"])}");
    Check("wools", woolMine, woolOra);

    // resources
    var resMine = PgmStudio.Minecraft.Anvil.FeatureExtractors.Resources(Chunks())
        .Select(r => $"{r.WorldX},{r.WorldZ},{r.WorldY},{r.ResourceType}");
    var resOra = (await TryRead(Path.Combine(oracleDir, "resources.parquet")))
        .Select(r => $"{N(r["world_x"])},{N(r["world_z"])},{N(r["world_y"])},{S(r["resource_type"])}");
    Check("resources", resMine, resOra);

    // chests
    var chestMine = PgmStudio.Minecraft.Anvil.FeatureExtractors.Chests(Chunks())
        .Select(c => $"{c.WorldX},{c.WorldZ},{c.WorldY},{c.ChestType},{c.Slot},{c.ItemId},{c.ItemDamage},{c.Count}");
    var chestOra = (await TryRead(Path.Combine(oracleDir, "chests.parquet")))
        .Select(r => $"{N(r["world_x"])},{N(r["world_z"])},{N(r["world_y"])},{S(r["chest_type"])},{N(r["slot"])},{S(r["item_id"])},{N(r["item_damage"])},{N(r["count"])}");
    Check("chests", chestMine, chestOra);

    // spawners
    var spMine = PgmStudio.Minecraft.Anvil.FeatureExtractors.Spawners(Chunks())
        .Select(s => $"{s.WorldX},{s.WorldZ},{s.WorldY}|{s.EntityId}|{(s.SpawnsWool ? "1" : "0")}|{s.SpawnItemId}|{Fmt(s.SpawnItemDamage)}|{Fmt(s.SpawnCount)}|{Fmt(s.SpawnRange)}|{Fmt(s.MinSpawnDelay)}|{Fmt(s.MaxSpawnDelay)}|{Fmt(s.RequiredPlayerRange)}|{Fmt(s.MaxNearbyEntities)}");
    var spOra = (await TryRead(Path.Combine(oracleDir, "spawners.parquet")))
        .Select(r => $"{N(r["world_x"])},{N(r["world_z"])},{N(r["world_y"])}|{S(r.GetValueOrDefault("entity_id"))}|{B(r["spawns_wool"])}|{S(r.GetValueOrDefault("spawn_item_id"))}|{N(r.GetValueOrDefault("spawn_item_damage"))}|{N(r.GetValueOrDefault("spawn_count"))}|{N(r.GetValueOrDefault("spawn_range"))}|{N(r.GetValueOrDefault("min_spawn_delay"))}|{N(r.GetValueOrDefault("max_spawn_delay"))}|{N(r.GetValueOrDefault("required_player_range"))}|{N(r.GetValueOrDefault("max_nearby_entities"))}");
    Check("spawners", spMine, spOra);

    // layer_segments
    var segMine = PgmStudio.Minecraft.Anvil.FeatureExtractors.Segments(Chunks())
        .Select(s => $"{s.WorldX},{s.WorldZ},{s.WorldYStart},{s.WorldYEnd}");
    var segOra = (await TryRead(Path.Combine(oracleDir, "layer_segments.parquet")))
        .Select(r => $"{N(r["world_x"])},{N(r["world_z"])},{N(r["world_y_start"])},{N(r["world_y_end"])}");
    Check("layer_segments", segMine, segOra);

    Console.WriteLine(fails == 0 ? "extract parity: ALL OK" : $"extract parity: {fails} mismatch(es)");
    return fails == 0 ? 0 : 1;

    static string Fmt(int? v) => v is null ? "~" : v.Value.ToString();
}

// ── --scan-out: write a map's importer-ready file set (no database), so the heavy world scan can run on a
// fast host and the cheap files be ingested later with `dotnet run --project src/PgmStudio.Import <outRoot>`.
static async Task<int> RunScanOut(string mapDir, string outRoot)
{
    if (string.IsNullOrWhiteSpace(mapDir) || !Directory.Exists(mapDir)) { Console.Error.WriteLine($"  scan-out: map directory not found: '{mapDir}'"); return 1; }
    mapDir = Path.GetFullPath(mapDir.TrimEnd(Path.DirectorySeparatorChar, '/'));
    var slug = Path.GetFileName(mapDir);
    var regionDir = Path.Combine(mapDir, "region");
    var mapXml = Path.Combine(mapDir, "map.xml");
    if (!Directory.Exists(regionDir)) { Console.Error.WriteLine($"  {slug}: SKIP — no region/ directory"); return 1; }

    // Parse + validate map.xml up front so an unsupported map (proto below the id-based floor, or a modern
    // world) throws here — before any output dir is created or the world is scanned, leaving no partial
    // output behind. The parsed map is reused for xml_data.json below.
    PgmStudio.Domain.MapXml? parsedXml = null;
    if (File.Exists(mapXml))
        parsedXml = PgmStudio.Pgm.MapParser.Parse(mapXml);
    else
        Console.Error.WriteLine($"  {slug}: WARNING no map.xml — xml_data.json not written; the importer will skip this dir");

    var outDir = Path.Combine(outRoot, slug);
    Directory.CreateDirectory(outDir);
    var sw = System.Diagnostics.Stopwatch.StartNew();

    // Materialise the world's chunks once — every extractor re-enumerates them (matches WorldFeatureWriter).
    var chunks = Directory.GetFiles(regionDir, "*.mca").SelectMany(PgmStudio.Minecraft.Anvil.AnvilRegion.ReadChunks).ToList();

    // feature rows → parquet (column names match the importer + the reference output)
    await WriteParquet(Path.Combine(outDir, "wools.parquet"), PgmStudio.Minecraft.Anvil.FeatureExtractors.Wools(chunks)
        .Select(w => new ScanWoolRow { WorldX = w.WorldX, WorldZ = w.WorldZ, WorldY = w.WorldY, Color = w.Color }).ToList());
    await WriteParquet(Path.Combine(outDir, "resources.parquet"), PgmStudio.Minecraft.Anvil.FeatureExtractors.Resources(chunks)
        .Select(r => new ScanResourceRow { WorldX = r.WorldX, WorldZ = r.WorldZ, WorldY = r.WorldY, ResourceType = r.ResourceType }).ToList());
    await WriteParquet(Path.Combine(outDir, "chests.parquet"), PgmStudio.Minecraft.Anvil.FeatureExtractors.Chests(chunks)
        .Select(c => new ScanChestRow { WorldX = c.WorldX, WorldZ = c.WorldZ, WorldY = c.WorldY, ChestType = c.ChestType, Slot = c.Slot, ItemId = c.ItemId, ItemDamage = c.ItemDamage, Count = c.Count }).ToList());
    await WriteParquet(Path.Combine(outDir, "spawners.parquet"), PgmStudio.Minecraft.Anvil.FeatureExtractors.Spawners(chunks)
        .Select(s => new ScanSpawnerRow { WorldX = s.WorldX, WorldZ = s.WorldZ, WorldY = s.WorldY, EntityId = s.EntityId, SpawnsWool = s.SpawnsWool, SpawnItemId = s.SpawnItemId, SpawnItemDamage = s.SpawnItemDamage, SpawnCount = s.SpawnCount, SpawnRange = s.SpawnRange, MinSpawnDelay = s.MinSpawnDelay, MaxSpawnDelay = s.MaxSpawnDelay, RequiredPlayerRange = s.RequiredPlayerRange, MaxNearbyEntities = s.MaxNearbyEntities }).ToList());
    await WriteParquet(Path.Combine(outDir, "layer_segments.parquet"), PgmStudio.Minecraft.Anvil.FeatureExtractors.Segments(chunks)
        .Select(s => new ScanSegmentRow { WorldX = s.WorldX, WorldZ = s.WorldZ, WorldYStart = s.WorldYStart, WorldYEnd = s.WorldYEnd }).ToList());

    // Surface layer → layer.parquet (the cached artifact + the bounding-box source)
    var surface = PgmStudio.Minecraft.Anvil.LayerExtractors.Surface(chunks).ToList();
    await WriteParquet(Path.Combine(outDir, "layer.parquet"), surface
        .Select(s => new ScanLayerRow { WorldX = s.WorldX, WorldZ = s.WorldZ, WorldY = s.WorldY, BlockId = s.BlockId, BlockData = s.BlockData }).ToList());

    // Stair-aware islands on the cleaned columns, with the lazy y0 → bedrock fallback (matches
    // WorldFeatureWriter) → islands.json
    static (int X, int Z, int Y) Cell(PgmStudio.Minecraft.Anvil.SurfaceBlock b) => (b.WorldX, b.WorldZ, b.WorldY);
    var columns = PgmStudio.Minecraft.Anvil.LayerExtractors.CleanColumns(chunks)
        .Select(c => (c.WorldX, c.WorldZ, c.BaseY, c.Surfaces)).ToList();
    var fallbacks = new[] { PgmStudio.Minecraft.Anvil.LayerExtractors.Y0(chunks).Select(Cell), PgmStudio.Minecraft.Anvil.LayerExtractors.Bedrock(chunks).Select(Cell) };
    var islands = PgmStudio.Analysis.Footprint.IslandDetector.DetectCleanedStairAware(columns, fallbacks);
    await File.WriteAllTextAsync(Path.Combine(outDir, "islands.json"), PgmStudio.Analysis.Footprint.IslandDetector.SerializeJson(islands));

    // Monument-candidate gather (F9 suggester) over the whole world → monument_candidates.parquet (the one
    // world-derived dataset the live scan-world writes that the reference file set never had).
    var worldBox = chunks.Count == 0
        ? new PgmStudio.Geom.BlockBox(0, 0, 0, 0, 0, 0)
        : new PgmStudio.Geom.BlockBox(chunks.Min(c => c.ChunkX) * 16, 0, chunks.Min(c => c.ChunkZ) * 16,
            chunks.Max(c => c.ChunkX) * 16 + 15, 255, chunks.Max(c => c.ChunkZ) * 16 + 15);
    var monuments = PgmStudio.Analysis.Suggest.MonumentSuggester.Gather(
        PgmStudio.Minecraft.Suggest.WorldReader.Read(chunks, worldBox), worldBox);
    await WriteParquet(Path.Combine(outDir, "monument_candidates.parquet"), monuments.Select(c => new ScanMonumentRow
    {
        CandX = c.X, CandY = c.Y, CandZ = c.Z, Source = c.Source,
        PedestalId = c.PedestalId, PedestalData = c.PedestalData, CapId = c.CapId, CapData = c.CapData,
        ColorHint = c.ColorHint, SignX = c.SignX, SignY = c.SignY, SignZ = c.SignZ, SignFacing = c.SignFacing,
        SignText = c.SignText, StandHeadColor = c.StandHeadColor, StandName = c.StandName, Evidence = c.Evidence,
    }).ToList());

    // map_config.json — the initial scan config (matches WorldFeatureWriter's SurfaceBbox + defaults)
    int minX = int.MaxValue, minZ = int.MaxValue, maxX = int.MinValue, maxZ = int.MinValue;
    foreach (var s in surface) { if (s.WorldX < minX) minX = s.WorldX; if (s.WorldX > maxX) maxX = s.WorldX; if (s.WorldZ < minZ) minZ = s.WorldZ; if (s.WorldZ > maxZ) maxZ = s.WorldZ; }
    var config = new System.Text.Json.Nodes.JsonObject
    {
        ["exclude_islands"] = new System.Text.Json.Nodes.JsonArray(),
        ["exclude_blocks"] = new System.Text.Json.Nodes.JsonArray(),
        ["scan_layer"] = "cleanbase",
        ["scan_layer_confirmed"] = false,
        ["bounding_box"] = surface.Count > 0
            ? new System.Text.Json.Nodes.JsonObject { ["min_x"] = minX, ["min_z"] = minZ, ["max_x"] = maxX, ["max_z"] = maxZ }
            : null,
    };
    await File.WriteAllTextAsync(Path.Combine(outDir, "map_config.json"), config.ToJsonString());

    // xml_data.json — the codec source the importer deserializes (from the map parsed + validated up front)
    if (parsedXml is not null)
        await File.WriteAllTextAsync(Path.Combine(outDir, "xml_data.json"),
            System.Text.Json.JsonSerializer.Serialize(PgmStudio.Pgm.Serializer.ToDict(parsedXml)));

    sw.Stop();
    Console.WriteLine($"  {slug,-28} chunks={chunks.Count,-5} islands={islands.Count,-3} mon={monuments.Count,-4} surface={surface.Count,-8} -> {outDir}  ({sw.ElapsedMilliseconds} ms)");
    return 0;
}

// ── --scan-out-all: --scan-out for every map folder (one with a region/ dir) under mapsRoot ───────────────
static async Task<int> RunScanOutAll(string mapsRoot, string outRoot)
{
    var dirs = Directory.GetDirectories(mapsRoot)
        .Where(d => Directory.Exists(Path.Combine(d, "region")))
        .OrderBy(d => d, StringComparer.Ordinal).ToList();
    Console.WriteLine($"Scanning {dirs.Count} map(s) from {mapsRoot} -> {outRoot}\n");
    var fail = 0;
    foreach (var d in dirs)
    {
        try { if (await RunScanOut(d, outRoot) != 0) fail++; }
        catch (Exception ex) { fail++; Console.Error.WriteLine($"  {Path.GetFileName(d)}: FAILED {ex.GetType().Name}: {ex.Message}"); }
    }
    Console.WriteLine($"\nscan-out: {dirs.Count - fail} ok, {fail} failed");
    return fail == 0 ? 0 : 1;
}

// ── --island-sketch: real island outlines → simplified exterior + holes, as one editable SketchLayout ────
static int RunIslandSketch(string mapDir, string outJson)
{
    var regionDir = Path.Combine(mapDir, "region");
    if (!Directory.Exists(regionDir)) { Console.Error.WriteLine($"  no region/ at {regionDir}"); return 1; }
    var chunks = Directory.GetFiles(regionDir, "*.mca").SelectMany(PgmStudio.Minecraft.Anvil.AnvilRegion.ReadChunks).ToList();
    static (int, int, int) ToCell(PgmStudio.Minecraft.Anvil.SurfaceBlock b) => (b.WorldX, b.WorldZ, b.WorldY);
    var columns = PgmStudio.Minecraft.Anvil.LayerExtractors.CleanColumns(chunks)
        .Select(c => (c.WorldX, c.WorldZ, c.BaseY, c.Surfaces)).ToList();
    var fallbacks = new[] { PgmStudio.Minecraft.Anvil.LayerExtractors.Y0(chunks).Select(ToCell), PgmStudio.Minecraft.Anvil.LayerExtractors.Bedrock(chunks).Select(ToCell) };
    var islands = PgmStudio.Analysis.Footprint.IslandDetector.DetectCleanedStairAware(columns, fallbacks);

    static List<double[]> Ring(NetTopologySuite.Geometries.LineString r) => r.Coordinates.Select(c => new[] { c.X, c.Y }).ToList();
    var shapes = new List<PgmStudio.Pgm.Sketch.SketchShape>();
    var islandGroups = new List<PgmStudio.Pgm.Sketch.SketchIsland>();
    double minX = double.MaxValue, minZ = double.MaxValue, maxX = double.MinValue, maxZ = double.MinValue;
    int imported = 0;

    Console.WriteLine($"{Path.GetFileName(Path.TrimEndingDirectorySeparator(mapDir))}: {islands.Count} islands");
    foreach (var isl in islands)
    {
        if (isl.Polygon is not NetTopologySuite.Geometries.Polygon poly) continue;
        var res = PgmStudio.Pgm.Sketch.IslandSimplifier.Simplify(Ring(poly.ExteriorRing), poly.InteriorRings.Select(Ring).ToList());
        if (res.Layout.Layout!.Shapes.Count == 0) continue;
        imported++;
        Console.WriteLine($"  island {isl.Id,-3} blocks={isl.BlockCount,-6} outline={res.ExteriorVertices,-3}v holes={res.Holes}");
        foreach (var s in res.Layout.Layout.Shapes)
        {
            s.Id = $"i{isl.Id}_{s.Id}";
            shapes.Add(s);
            foreach (var v in s.Vertices ?? []) { minX = Math.Min(minX, v[0]); maxX = Math.Max(maxX, v[0]); minZ = Math.Min(minZ, v[1]); maxZ = Math.Max(maxZ, v[1]); }
        }
        islandGroups.Add(new PgmStudio.Pgm.Sketch.SketchIsland { Id = $"i{isl.Id}", Name = $"Island {isl.Id}", Mirrors = false, ShapeIds = [.. res.Layout.Layout.Shapes.Select(s => s.Id)] });
    }

    var layout = new PgmStudio.Pgm.Sketch.SketchLayout
    {
        Setup = new PgmStudio.Pgm.Sketch.SketchSetup { MirrorMode = "none", Center = new PgmStudio.Pgm.Sketch.SketchCenter { Cx = shapes.Count > 0 ? (minX + maxX) / 2 : 0, Cz = shapes.Count > 0 ? (minZ + maxZ) / 2 : 0 } },
        Layout = new PgmStudio.Pgm.Sketch.SketchShapes { Shapes = shapes, Islands = islandGroups },
    };
    File.WriteAllText(outJson, layout.ToJson());
    Console.WriteLine($"island-sketch: {imported}/{islands.Count} islands simplified (outline + holes) → {outJson}");
    return 0;
}

// ── --island-stairaware: cleaned-base height-aware vs stair-aware island detection, side by side ──────────
static int RunIslandStairAware(string mapDir)
{
    var regionDir = Path.Combine(mapDir, "region");
    if (!Directory.Exists(regionDir)) { Console.Error.WriteLine($"  no region/ at {regionDir}"); return 1; }
    var chunks = Directory.GetFiles(regionDir, "*.mca").SelectMany(PgmStudio.Minecraft.Anvil.AnvilRegion.ReadChunks).ToList();

    var baseCells = PgmStudio.Minecraft.Anvil.LayerExtractors.CleanBase(chunks)
        .Select(b => (b.WorldX, b.WorldZ, b.WorldY)).ToList();
    var fallbacks = new[]
    {
        PgmStudio.Minecraft.Anvil.LayerExtractors.Y0(chunks).Select(b => (b.WorldX, b.WorldZ, b.WorldY)),
        PgmStudio.Minecraft.Anvil.LayerExtractors.Bedrock(chunks).Select(b => (b.WorldX, b.WorldZ, b.WorldY)),
    };
    var old = PgmStudio.Analysis.Footprint.IslandDetector.DetectCleaned(baseCells, fallbacks);

    var columns = PgmStudio.Minecraft.Anvil.LayerExtractors.CleanColumns(chunks)
        .Select(c => (c.WorldX, c.WorldZ, c.BaseY, c.Surfaces)).ToList();
    var neu = PgmStudio.Analysis.Footprint.IslandDetector.DetectStairAware(columns);

    static string Sizes(IReadOnlyList<PgmStudio.Analysis.Footprint.IslandDetector.Island> isls) =>
        string.Join(",", isls.Take(8).Select(i => i.BlockCount)) + (isls.Count > 8 ? ",…" : "");
    Console.WriteLine($"  {Path.GetFileName(Path.TrimEndingDirectorySeparator(mapDir)),-26} " +
                      $"height-aware: {old.Count,3} [{Sizes(old)}]   stair-aware: {neu.Count,3} [{Sizes(neu)}]");
    return 0;
}

// Write rows to a parquet file; an empty set writes NO file (so the importer's File.Exists check skips it).
static async Task WriteParquet<T>(string path, IReadOnlyList<T> rows) where T : class
{
    if (rows.Count == 0) { if (File.Exists(path)) File.Delete(path); return; }
    await using var os = File.Create(path);
    await Parquet.Serialization.ParquetSerializer.SerializeAsync(rows, os);
}

static async Task<int> RunMonumentSlices(string regionDir, string xmlDataPath, string outParquet)
{
    // Monument centres from xml_data.json: wools[].monuments[].location (the <block> coordinate).
    var slug = Path.GetFileName(Path.GetDirectoryName(Path.GetFullPath(regionDir).TrimEnd('/'))) ?? "map";
    using var jd = System.Text.Json.JsonDocument.Parse(File.ReadAllText(xmlDataPath));
    if (jd.RootElement.TryGetProperty("name", out var nm) && nm.ValueKind == System.Text.Json.JsonValueKind.String)
        slug = nm.GetString()!.ToLowerInvariant().Replace(' ', '_');

    static int Coord(System.Text.Json.JsonElement loc, string axis) => (int)Math.Floor(loc.GetProperty(axis).GetDouble());
    var monuments = new List<PgmStudio.Minecraft.Anvil.MonumentTarget>();
    if (jd.RootElement.TryGetProperty("wools", out var woolsEl))
        foreach (var wool in woolsEl.EnumerateArray())
        {
            var woolId = wool.TryGetProperty("id", out var wid) ? wid.GetString() ?? "" : "";
            var color = wool.TryGetProperty("color", out var c) ? c.GetString() ?? woolId : woolId;
            if (!wool.TryGetProperty("monuments", out var mons)) continue;   // some maps omit it
            foreach (var mon in mons.EnumerateArray())
            {
                if (!mon.TryGetProperty("location", out var loc)) continue;
                monuments.Add(new PgmStudio.Minecraft.Anvil.MonumentTarget(
                    slug, woolId, color,
                    mon.TryGetProperty("id", out var mid) ? mid.GetString() ?? "" : "",
                    mon.TryGetProperty("team", out var mt) ? mt.GetString() ?? "" : "",
                    Coord(loc, "x"), Coord(loc, "y"), Coord(loc, "z")));
            }
        }

    var mcas = Directory.GetFiles(regionDir, "*.mca");
    IEnumerable<PgmStudio.Minecraft.Anvil.AnvilRegion.Chunk> Chunks() => mcas.SelectMany(PgmStudio.Minecraft.Anvil.AnvilRegion.ReadChunks);

    Console.WriteLine($"{slug}: {monuments.Count} monument(s) over {mcas.Length} region file(s)");
    var cells = PgmStudio.Minecraft.Anvil.MonumentSliceExtractor.Extract(Chunks(), monuments);

    var rows = cells.Select(MonumentSliceRow.From).ToList();
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outParquet))!);
    await using (var os = File.Create(outParquet))
        if (rows.Count > 0) await Parquet.Serialization.ParquetSerializer.SerializeAsync(rows, os);
    Console.WriteLine($"wrote {rows.Count} cell(s) → {outParquet}");
    if (rows.Count == 0) { Console.WriteLine("  (no monuments — nothing to validate)"); return 0; }

    // Read back and validate against the extractor's invariants.
    var back = await FeatureData.ReadParquet(outParquet);
    var fails = 0;
    void Require(bool ok, string what) { if (!ok) { fails++; Console.WriteLine($"  FAIL {what}"); } else Console.WriteLine($"  OK   {what}"); }

    static int I(object? v) => Convert.ToInt32(v);
    static string S(object? v) => v?.ToString() ?? "";
    static bool B(object? v) => v is not null && Convert.ToBoolean(v);

    Require(back.Count == monuments.Count * PgmStudio.Minecraft.Anvil.MonumentSliceExtractor.CellsPerMonument,
        $"row count = {monuments.Count} monuments × {PgmStudio.Minecraft.Anvil.MonumentSliceExtractor.CellsPerMonument} = {monuments.Count * PgmStudio.Minecraft.Anvil.MonumentSliceExtractor.CellsPerMonument} (got {back.Count})");

    var byMon = back.GroupBy(r => S(r["monument_id"])).ToList();
    Require(byMon.Count == monuments.Count, $"{monuments.Count} distinct monuments present");
    Require(byMon.All(g => g.Count() == PgmStudio.Minecraft.Anvil.MonumentSliceExtractor.CellsPerMonument), "every monument has exactly 45 cells");

    var centers = back.Where(r => B(r["is_monument"])).ToList();
    Require(centers.Count == monuments.Count, $"one centre cell per monument ({centers.Count})");
    var centersAir = centers.Count(r => B(r["is_air"]));
    Require(centersAir == monuments.Count, $"all {monuments.Count} monument blocks are air (got {centersAir})");

    // Bedrock-below: the cell at (dx,dy,dz)=(0,-1,0) — authors' usual monument base.
    var below = back.Where(r => I(r["dx"]) == 0 && I(r["dy"]) == -1 && I(r["dz"]) == 0).ToList();
    var bedrockBelow = below.Count(r => I(r["block_id"]) == 7);
    Console.WriteLine($"  info bedrock directly below monument: {bedrockBelow}/{monuments.Count}");

    var signCells = back.Where(r => !string.IsNullOrWhiteSpace(S(r.GetValueOrDefault("sign_text")))).ToList();
    Console.WriteLine($"  info sign cells in slices: {signCells.Count}");
    var entityCells = back.Where(r => !string.IsNullOrWhiteSpace(S(r.GetValueOrDefault("entity_ids")))).ToList();
    Console.WriteLine($"  info entity cells in slices: {entityCells.Count}");
    foreach (var ec in entityCells)
    {
        Console.WriteLine($"   entity @ '{S(ec["monument_id"])}' (dx{I(ec["dx"]):+0;-0;0},dy{I(ec["dy"]):+0;-0;0},dz{I(ec["dz"]):+0;-0;0}): {S(ec["entity_ids"])}");
        try
        {
            using var ed = System.Text.Json.JsonDocument.Parse(S(ec["entity_nbt"]));
            foreach (var en in ed.RootElement.EnumerateArray())
            {
                var name = en.TryGetProperty("CustomName", out var cn) ? cn.GetString() : null;
                string head = "";
                if (en.TryGetProperty("Equipment", out var eq) && eq.ValueKind == System.Text.Json.JsonValueKind.Array && eq.GetArrayLength() >= 5)
                {
                    var h = eq[4];   // head slot
                    if (h.ValueKind == System.Text.Json.JsonValueKind.Object && h.TryGetProperty("id", out var hid))
                    {
                        var dmg = h.TryGetProperty("Damage", out var dd) ? dd.GetInt32() : 0;
                        var color = hid.GetString()?.EndsWith("wool") == true ? $" → {PgmStudio.Domain.BlockColors.BlockColor(dmg)}" : "";
                        head = $" head={hid.GetString()}:{dmg}{color}";
                    }
                }
                if (name is not null || head.Length > 0) Console.WriteLine($"      name=\"{name}\"{head}");
            }
        }
        catch { /* non-JSON entity payload */ }
    }

    // Show one full slice + its decoded signs so the result is eyeball-verifiable.
    var sample = byMon.First();
    Console.WriteLine($"\n  sample slice — monument '{sample.Key}' (wool={S(sample.First()["wool_color"])}, team={S(sample.First()["team"])}, " +
                      $"centre={I(sample.First()["center_x"])},{I(sample.First()["center_y"])},{I(sample.First()["center_z"])}):");
    foreach (var dy in new[] { 2, 1, 0, -1, -2 })
    {
        var line = new System.Text.StringBuilder($"   y{dy,+2}: ");
        foreach (var dz in new[] { -1, 0, 1 })
        {
            foreach (var dx in new[] { -1, 0, 1 })
            {
                var cell = sample.First(r => I(r["dx"]) == dx && I(r["dy"]) == dy && I(r["dz"]) == dz);
                var tag = B(cell["is_monument"]) ? "*" : "";
                line.Append($"{I(cell["block_id"]),3}:{I(cell["block_data"]),-2}{tag,-1} ");
            }
            line.Append(" | ");
        }
        Console.WriteLine(line.ToString());
    }
    foreach (var sc in signCells.Where(r => S(r["monument_id"]) == sample.Key))
        Console.WriteLine($"   sign @ (dx{I(sc["dx"]):+0;-0;0},dy{I(sc["dy"]):+0;-0;0},dz{I(sc["dz"]):+0;-0;0}): \"{S(sc["sign_text"]).Replace("\n", " | ")}\"");

    Console.WriteLine($"\nmonument-slices: {(fails == 0 ? "ALL OK" : $"{fails} check(s) failed")}");
    return fails == 0 ? 0 : 1;
}

static SuggestEval EvalSuggest(string regionDir, string xmlDataPath, bool autoStyle,
    PgmStudio.Analysis.Suggest.PedestalKind pedestal, PgmStudio.Analysis.Suggest.LabelKind label,
    PgmStudio.Analysis.Suggest.CapKind cap, int margin)
{
    using var jd = System.Text.Json.JsonDocument.Parse(File.ReadAllText(xmlDataPath));
    static int Coord(System.Text.Json.JsonElement loc, string a) => (int)Math.Floor(loc.GetProperty(a).GetDouble());
    var truth = new List<(int x, int y, int z, string id, string color)>();
    if (jd.RootElement.TryGetProperty("wools", out var woolsEl))
        foreach (var wool in woolsEl.EnumerateArray())
        {
            var color = wool.TryGetProperty("color", out var c) ? c.GetString() ?? "" : "";
            if (!wool.TryGetProperty("monuments", out var mons)) continue;   // some maps omit it
            foreach (var mon in mons.EnumerateArray())
            {
                if (!mon.TryGetProperty("location", out var loc)) continue;
                truth.Add((Coord(loc, "x"), Coord(loc, "y"), Coord(loc, "z"),
                    mon.TryGetProperty("id", out var mid) ? mid.GetString() ?? "" : "", color));
            }
        }

    var mcas = Directory.GetFiles(regionDir, "*.mca");
    var chunks = mcas.SelectMany(PgmStudio.Minecraft.Anvil.AnvilRegion.ReadChunks).ToList();   // decode the world once

    // cluster monuments (Chebyshev ≤ 16) → one box per cluster (the author boxes each monument group).
    var clusters = new List<List<(int x, int y, int z)>>();
    foreach (var (x, y, z, _, _) in truth)
    {
        var hit = clusters.FirstOrDefault(cl => cl.Any(q => Cheb(q, (x, y, z)) <= 16));
        if (hit is null) clusters.Add([(x, y, z)]); else hit.Add((x, y, z));
    }

    // auto-style: declare the modal pedestal *and cap* of each cluster's monuments — precompute the
    // block directly under and over every monument in a single chunk pass (the author would declare these).
    var adj = new Dictionary<(int, int, int), int>();
    if (autoStyle)
    {
        var want = truth.SelectMany(t => new[] { (t.x, t.y - 1, t.z), (t.x, t.y + 1, t.z) }).ToHashSet();
        foreach (var ch in chunks)
            foreach (var b in PgmStudio.Minecraft.Anvil.AnvilRegion.Blocks(ch))
                if (want.Contains((b.X, b.Y, b.Z))) adj[(b.X, b.Y, b.Z)] = b.Id;
    }
    // reuse the suggester's single id↔kind table, so auto-style can't drift from detection
    PgmStudio.Analysis.Suggest.PedestalKind PedestalBelow(int x, int y, int z) =>
        PgmStudio.Analysis.Suggest.MonumentSuggester.ClassifyPedestal(adj.GetValueOrDefault((x, y - 1, z), 0));
    PgmStudio.Analysis.Suggest.CapKind CapAbove(int x, int y, int z) =>
        PgmStudio.Analysis.Suggest.MonumentSuggester.ClassifyCap(adj.GetValueOrDefault((x, y + 1, z), 0));

    var suggestions = new Dictionary<(int, int, int), PgmStudio.Analysis.Suggest.MonumentSuggestion>();
    foreach (var cl in clusters)
    {
        var box = new PgmStudio.Geom.BlockBox(
            cl.Min(q => q.x) - margin, cl.Min(q => q.y) - margin, cl.Min(q => q.z) - margin,
            cl.Max(q => q.x) + margin, cl.Max(q => q.y) + margin, cl.Max(q => q.z) + margin);
        var ped = autoStyle
            ? cl.Select(q => PedestalBelow(q.x, q.y, q.z)).GroupBy(k => k).OrderByDescending(g => g.Count()).First().Key
            : pedestal;
        var cp = autoStyle
            ? cl.Select(q => CapAbove(q.x, q.y, q.z)).GroupBy(k => k).OrderByDescending(g => g.Count()).First().Key
            : cap;
        var read = PgmStudio.Minecraft.Suggest.WorldReader.Read(chunks, box.Expand(2));
        foreach (var s in PgmStudio.Analysis.Suggest.MonumentSuggester.Suggest(read, box, new PgmStudio.Analysis.Suggest.MonumentStyle(ped, label, cp)))
            if (!suggestions.TryGetValue((s.X, s.Y, s.Z), out var prev) || prev.Confidence < s.Confidence)
                suggestions[(s.X, s.Y, s.Z)] = s;
    }

    static int Cheb((int x, int y, int z) a, (int x, int y, int z) b) =>
        Math.Max(Math.Max(Math.Abs(a.x - b.x), Math.Abs(a.y - b.y)), Math.Abs(a.z - b.z));
    var sites = suggestions.Values.OrderByDescending(s => s.Confidence).ToList();
    var mt = new HashSet<int>(); var ms = new HashSet<int>();
    // Two passes (exact cell, then within 1) so adjacent monuments pair to their own cell, not a neighbour's.
    for (var tol = 0; tol <= 1; tol++)
        for (var i = 0; i < sites.Count; i++)
        {
            if (ms.Contains(i)) continue;
            for (var j = 0; j < truth.Count; j++)
            {
                if (mt.Contains(j)) continue;
                if (Cheb((sites[i].X, sites[i].Y, sites[i].Z), (truth[j].x, truth[j].y, truth[j].z)) <= tol)
                { ms.Add(i); mt.Add(j); break; }
            }
        }
    // colour-correct: count a matched monument if ANY suggestion at/adjacent to it carries the right
    // colour — independent of which site the greedy matcher assigned (so a wrong-colour higher-confidence
    // site at the same cell doesn't mask a correct-colour one).
    var colorOk = mt.Count(j => sites.Any(s =>
        Cheb((s.X, s.Y, s.Z), (truth[j].x, truth[j].y, truth[j].z)) <= 1 && s.Color == truth[j].color));
    return new SuggestEval(truth.Count, mt.Count, sites.Count - ms.Count, truth.Count - mt.Count, colorOk, clusters.Count, sites);
}

static int RunSuggestMonuments(string[] args, string regionDir, string xmlDataPath)
{
    string Flag(string name, string def) { var i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : def; }
    var margin = int.Parse(Flag("--margin", "8"));
    var autoStyle = args.Contains("--auto-style");
    var pedestal = Enum.Parse<PgmStudio.Analysis.Suggest.PedestalKind>(Flag("--pedestal", "Any"), true);
    var label = Enum.Parse<PgmStudio.Analysis.Suggest.LabelKind>(Flag("--label", "Any"), true);
    var cap = Enum.Parse<PgmStudio.Analysis.Suggest.CapKind>(Flag("--cap", "Any"), true);
    var slug = Path.GetFileName(Path.GetDirectoryName(Path.GetFullPath(regionDir).TrimEnd('/'))) ?? "map";

    var e = EvalSuggest(regionDir, xmlDataPath, autoStyle, pedestal, label, cap, margin);
    double prec = e.Tp + e.Fp > 0 ? (double)e.Tp / (e.Tp + e.Fp) : 0, rec = e.Tp + e.Fn > 0 ? (double)e.Tp / (e.Tp + e.Fn) : 0;
    Console.WriteLine($"{slug}: {e.Truth} monuments in {e.Clusters} cluster(s), box margin ±{margin}, style={(autoStyle ? "auto" : pedestal.ToString())}/{label}");
    Console.WriteLine($"  suggestions={e.Sites.Count}  TP={e.Tp} FP={e.Fp} FN={e.Fn}  precision={100 * prec:F1}%  recall={100 * rec:F1}%  colour-correct={e.ColorOk}/{e.Tp}");
    foreach (var s in e.Sites.Take(8))
        Console.WriteLine($"   ({s.X},{s.Y},{s.Z}) conf={s.Confidence:F2} {s.Source,-10} colour={s.Color ?? "?",-10} ped={s.PedestalId}:{s.PedestalData} \"{(s.Evidence ?? "").Replace("\n", " | ")}\"");
    return 0;
}

// --suggest-monuments-corpus [--auto-style] [--margin M] [--pedestal K] [--label K]: sweep every CTW map
// with a world + xml_data.json and report aggregate precision/recall for the authoring suggester.
static int RunSuggestMonumentsCorpus(string[] args, string[] corpusRoots, string outputRoot)
{
    string Flag(string name, string def) { var i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : def; }
    var margin = int.Parse(Flag("--margin", "8"));
    var autoStyle = args.Contains("--auto-style");
    var pedestal = Enum.Parse<PgmStudio.Analysis.Suggest.PedestalKind>(Flag("--pedestal", "Any"), true);
    var label = Enum.Parse<PgmStudio.Analysis.Suggest.LabelKind>(Flag("--label", "Any"), true);
    var cap = Enum.Parse<PgmStudio.Analysis.Suggest.CapKind>(Flag("--cap", "Any"), true);

    int maps = 0, truth = 0, tp = 0, fp = 0, fn = 0, colorOk = 0;
    foreach (var root in corpusRoots.Where(Directory.Exists))
        foreach (var dir in Directory.GetDirectories(root).OrderBy(d => d, StringComparer.Ordinal))
        {
            var slug = Path.GetFileName(dir)!;
            var region = Path.Combine(dir, "region");
            var xml = Path.Combine(outputRoot, slug, "xml_data.json");
            if (!Directory.Exists(region) || !Directory.GetFiles(region, "*.mca").Any() || !File.Exists(xml)) continue;
            try
            {
                var e = EvalSuggest(region, xml, autoStyle, pedestal, label, cap, margin);
                if (e.Truth == 0) continue;
                maps++; truth += e.Truth; tp += e.Tp; fp += e.Fp; fn += e.Fn; colorOk += e.ColorOk;
            }
            catch (Exception ex) { Console.WriteLine($"  !! {slug}: {ex.GetType().Name}"); }
            if (maps % 50 == 0 && maps > 0) Console.Error.WriteLine($"  ...{maps} maps");
        }
    double prec = tp + fp > 0 ? (double)tp / (tp + fp) : 0, rec = tp + fn > 0 ? (double)tp / (tp + fn) : 0;
    Console.WriteLine($"\ncorpus suggest: {maps} maps, {truth} monuments, style={(autoStyle ? "auto" : pedestal.ToString())}/{label}, margin ±{margin}");
    Console.WriteLine($"  TP={tp} FP={fp} FN={fn}  precision={100 * prec:F1}%  recall={100 * rec:F1}%  colour-correct={colorOk}/{tp} ({(tp > 0 ? 100.0 * colorOk / tp : 0):F1}%)");
    return 0;
}

static async Task<List<Dictionary<string, object?>>> TryRead(string path) =>
    File.Exists(path) ? await FeatureData.ReadParquet(path) : [];

static int RunAuthoringFixture(string[] args, string[] corpusRoots)
{
    var outIdx = Array.IndexOf(args, "--out");
    var outDir = outIdx >= 0 && outIdx + 1 < args.Length
        ? args[outIdx + 1]
        : Path.Combine(RepoRoot(), "tools", "out", "region-authoring");

    var slugs = new List<string>();
    for (int i = 0; i < args.Length; i++)
    {
        if (args[i].StartsWith("--")) { if (args[i] == "--out") i++; continue; }
        slugs.Add(args[i]);
    }
    if (slugs.Count == 0) slugs = ["annealing_iv", "outback_outback_edition"];

    Directory.CreateDirectory(outDir);
    int written = 0, missing = 0;
    foreach (var slug in slugs)
    {
        var mapXml = corpusRoots.Select(r => Path.Combine(r, slug, "map.xml")).FirstOrDefault(File.Exists);
        if (mapXml is null) { Console.WriteLine($"  SKIP {slug}: not in corpus"); missing++; continue; }

        var doc = Serializer.ToDict(MapParser.Parse(mapXml));
        var regions = doc.GetValueOrDefault("regions") as Dictionary<string, object?> ?? [];
        var applyRules = doc.GetValueOrDefault("apply_rules") as List<object?>;
        var cats = PgmStudio.Pgm.Authoring.RegionCategorizer.Categorize(doc);
        var facets = PgmStudio.Pgm.Authoring.RegionCategorizer.DeriveFacets(doc);

        var split = PgmStudio.Analysis.Region.RegionAuthoringEncoder.EncodeAuthoring(regions, cats, applyRules, null);
        var primitives = (split["primitives"] as List<object?> ?? []).OfType<Dictionary<string, object?>>().ToList();
        var composed = (split["composed"] as List<object?> ?? []).OfType<Dictionary<string, object?>>().ToList();

        var oracle = new Dictionary<string, object?>
        {
            ["map"] = slug,
            ["counts"] = new Dictionary<string, object?> { ["primitives"] = primitives.Count, ["composed"] = composed.Count },
            ["primitives"] = primitives.Select(n => (object?)TrimAuthoringNode(n, false, facets)).ToList(),
            ["composed"] = composed.Select(n => (object?)TrimAuthoringNode(n, true, facets)).ToList(),
        };

        var json = System.Text.Json.JsonSerializer.Serialize(oracle,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true }) + "\n";
        var path = Path.Combine(outDir, $"{slug}.json");
        File.WriteAllText(path, json);
        written++;
        Console.WriteLine($"  wrote {path}  (primitives={primitives.Count}, composed={composed.Count})");
    }
    Console.WriteLine($"authoring fixtures: {written} written, {missing} skipped");
    return written == 0 ? 1 : 0;
}

static Dictionary<string, object?> TrimAuthoringNode(
    Dictionary<string, object?> n, bool composed,
    IReadOnlyDictionary<string, PgmStudio.Domain.RegionFacet> facets)
{
    var id = n.GetValueOrDefault("id") as string ?? "";
    var node = new Dictionary<string, object?>
    {
        ["id"] = id,
        ["type"] = n.GetValueOrDefault("type"),
        ["category"] = n.GetValueOrDefault("category"),
        ["subtype"] = facets.GetValueOrDefault(id)?.Subtype,
    };
    if (composed) node["member_ids"] = n.GetValueOrDefault("member_ids");
    node["wiring"] = (n.GetValueOrDefault("wiring") as List<object?> ?? [])
        .OfType<Dictionary<string, object?>>()
        .Select(w => (object?)new Dictionary<string, object?>
        {
            ["event"] = w.GetValueOrDefault("event"),
            ["value"] = w.GetValueOrDefault("value"),
        })
        .ToList();
    return node;
}

// Walk up from the running binary to the solution dir (the .slnx anchors the repo root).
static string RepoRoot()
{
    var dir = AppContext.BaseDirectory;
    while (dir is not null && !File.Exists(Path.Combine(dir, "PgmStudio.slnx")))
        dir = Path.GetDirectoryName(dir);
    return dir ?? Directory.GetCurrentDirectory();
}

// ±Infinity → finite sentinel, NaN → null, so JsonDocument parses both sides identically.
static async Task<int> RunIslandParity(string regionDir, string oracleDir)
{
    var mcas = Directory.GetFiles(regionDir, "*.mca");
    IEnumerable<PgmStudio.Minecraft.Anvil.AnvilRegion.Chunk> Chunks() => mcas.SelectMany(PgmStudio.Minecraft.Anvil.AnvilRegion.ReadChunks);

    // Surface layer: compare row count to layer.parquet (default ScanConfig: surface, no exclude/cap).
    var surface = PgmStudio.Minecraft.Anvil.LayerExtractors.Surface(Chunks()).ToList();
    var layerOra = await TryRead(Path.Combine(oracleDir, "layer.parquet"));
    var surfOk = surface.Count == layerOra.Count;
    Console.WriteLine($"  {(surfOk ? "OK  " : "FAIL")} surface        mine={surface.Count} oracle={layerOra.Count}");

    // Islands: compare count + the multiset of (block_count, bounds) + total polygon area.
    var mineIslands = PgmStudio.Analysis.Footprint.IslandDetector.Detect(surface.Select(s => (s.WorldX, s.WorldZ)));
    var oraPath = Path.Combine(oracleDir, "islands.json");
    var oraIslands = File.Exists(oraPath)
        ? System.Text.Json.JsonDocument.Parse(File.ReadAllText(oraPath)).RootElement
        : default;

    var oraCount = oraIslands.ValueKind == System.Text.Json.JsonValueKind.Array ? oraIslands.GetArrayLength() : 0;
    static string Key(int bc, int a, int b, int c, int d) => $"{bc}|{a},{b},{c},{d}";
    var mineKeys = mineIslands.Select(i => Key(i.BlockCount, i.Bounds.MinX, i.Bounds.MinZ, i.Bounds.MaxX, i.Bounds.MaxZ)).OrderBy(s => s).ToList();
    var oraKeys = new List<string>();
    if (oraCount > 0)
        foreach (var e in oraIslands.EnumerateArray())
        {
            var b = e.GetProperty("bounds");
            oraKeys.Add(Key(e.GetProperty("block_count").GetInt32(), b[0].GetInt32(), b[1].GetInt32(), b[2].GetInt32(), b[3].GetInt32()));
        }
    oraKeys.Sort();

    var islOk = mineKeys.SequenceEqual(oraKeys);
    Console.WriteLine($"  {(islOk ? "OK  " : "FAIL")} islands        mine={mineIslands.Count} oracle={oraCount}");
    if (!islOk)
        foreach (var k in mineKeys.Except(oraKeys).Concat(oraKeys.Except(mineKeys)).Take(6))
            Console.WriteLine($"        differs: {k}");

    var ok = surfOk && islOk;
    Console.WriteLine(ok ? "island parity: OK" : "island parity: MISMATCH");
    return ok ? 0 : 1;
}

static int RunCleanBaseRender(string regionDir, string outSvg)
{
    var chunks = Directory.GetFiles(regionDir, "*.mca")
        .SelectMany(PgmStudio.Minecraft.Anvil.AnvilRegion.ReadChunks).ToList();
    static (int, int, int) ToCell(PgmStudio.Minecraft.Anvil.SurfaceBlock b) => (b.WorldX, b.WorldZ, b.WorldY);

    var baseCells = PgmStudio.Minecraft.Anvil.LayerExtractors.CleanBase(chunks).Select(ToCell).ToList();
    // Deferred — only extracted/scanned if the cleaned base reads degenerately (the fallback path).
    var fallbacks = new[]
    {
        PgmStudio.Minecraft.Anvil.LayerExtractors.Y0(chunks).Select(ToCell),
        PgmStudio.Minecraft.Anvil.LayerExtractors.Bedrock(chunks).Select(ToCell),
    };
    var islands = PgmStudio.Analysis.Footprint.IslandDetector.DetectCleaned(baseCells, fallbacks);

    var name = Path.GetFileName(Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(regionDir)) ?? regionDir);
    Console.WriteLine($"clean-base-render {name}: {baseCells.Count} cleaned-base cells → {islands.Count} islands " +
        $"[{string.Join(",", islands.Take(12).Select(i => i.BlockCount))}]");

    var polys = islands
        .Select(i => i.Polygon as NetTopologySuite.Geometries.Polygon)
        .Where(p => p is not null).Select(p => p!).ToList();
    if (polys.Count == 0) { Console.WriteLine("  no islands to render"); return 1; }

    var exterior = polys.SelectMany(p => p.ExteriorRing.Coordinates).ToList();
    double minX = exterior.Min(c => c.X), maxX = exterior.Max(c => c.X);
    double minZ = exterior.Min(c => c.Y), maxZ = exterior.Max(c => c.Y);
    const double size = 800, pad = 24;
    var scale = Math.Min((size - 2 * pad) / Math.Max(1, maxX - minX), (size - 2 * pad) / Math.Max(1, maxZ - minZ));
    double SX(double x) => pad + (x - minX) * scale;
    double SZ(double z) => pad + (z - minZ) * scale;

    // Each island → one SVG path: exterior ring + interior rings (holes), cut out via fill-rule=evenodd.
    static string RingPath(NetTopologySuite.Geometries.LineString ring, Func<double, double> sx, Func<double, double> sz)
    {
        var cs = ring.Coordinates;
        var d = new System.Text.StringBuilder($"M{sx(cs[0].X):0.#},{sz(cs[0].Y):0.#}");
        for (var k = 1; k < cs.Length; k++) d.Append($"L{sx(cs[k].X):0.#},{sz(cs[k].Y):0.#}");
        return d.Append('Z').ToString();
    }

    var sb = new System.Text.StringBuilder();
    sb.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{size}\" height=\"{size}\" viewBox=\"0 0 {size} {size}\">\n");
    sb.Append($"  <rect width=\"{size}\" height=\"{size}\" fill=\"#1b1e24\"/>\n");
    sb.Append($"  <text x=\"{pad}\" y=\"{size - 10}\" fill=\"#7b828d\" font-family=\"monospace\" font-size=\"13\">{name} · cleaned base · {islands.Count} islands</text>\n");
    var holeTotal = 0;
    foreach (var p in polys)
    {
        var d = RingPath(p.ExteriorRing, SX, SZ);
        foreach (var hole in p.InteriorRings) { d += RingPath(hole, SX, SZ); holeTotal++; }
        sb.Append($"  <path d=\"{d}\" fill-rule=\"evenodd\" fill=\"#9aa0a8\" fill-opacity=\"0.85\" stroke=\"#cfd5de\" stroke-width=\"1\"/>\n");
    }
    sb.Append("</svg>\n");
    if (holeTotal > 0) Console.WriteLine($"  ({holeTotal} hole ring(s) cut out)");
    File.WriteAllText(outSvg, sb.ToString());
    Console.WriteLine($"  wrote {outSvg}");
    return 0;
}

static int RunIslandStudy(string regionDir, string outJson, double tolerance)
{
    var chunks = Directory.GetFiles(regionDir, "*.mca")
        .SelectMany(PgmStudio.Minecraft.Anvil.AnvilRegion.ReadChunks).ToList();
    static (int, int, int) ToCell(PgmStudio.Minecraft.Anvil.SurfaceBlock b) => (b.WorldX, b.WorldZ, b.WorldY);
    var baseCells = PgmStudio.Minecraft.Anvil.LayerExtractors.CleanBase(chunks).Select(ToCell).ToList();
    var fallbacks = new[]
    {
        PgmStudio.Minecraft.Anvil.LayerExtractors.Y0(chunks).Select(ToCell),
        PgmStudio.Minecraft.Anvil.LayerExtractors.Bedrock(chunks).Select(ToCell),
    };
    var islands = PgmStudio.Analysis.Footprint.IslandDetector.DetectCleaned(baseCells, fallbacks);

    static List<double[]> Ring(NetTopologySuite.Geometries.LineString r) =>
        r.Coordinates.Select(c => new[] { c.X, c.Y }).ToList();

    var outIslands = new List<object>();
    foreach (var isl in islands)
    {
        if (isl.Polygon is not NetTopologySuite.Geometries.Polygon poly) continue;
        var ext = Ring(poly.ExteriorRing);
        var holes = poly.InteriorRings.Select(Ring).ToList();
        var simp = PgmStudio.Geom.PolygonSimplify.Simplify(ext, holes, tolerance, minHoleArea: 8);
        outIslands.Add(new
        {
            id = isl.Id,
            blockCount = isl.BlockCount,
            bounds = new[] { isl.Bounds.MinX, isl.Bounds.MinZ, isl.Bounds.MaxX, isl.Bounds.MaxZ },
            rawVerts = ext.Count + holes.Sum(h => h.Count),
            simpVerts = simp.VertexCount,
            rawExterior = ext, rawHoles = holes,
            exterior = simp.Exterior, holes = simp.Holes,
        });
    }

    var allX = islands.SelectMany(i => new[] { i.Bounds.MinX, i.Bounds.MaxX });
    var allZ = islands.SelectMany(i => new[] { i.Bounds.MinZ, i.Bounds.MaxZ });
    var doc = new
    {
        name = Path.GetFileName(Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(regionDir)) ?? regionDir),
        tolerance,
        bbox = new { minX = allX.Min(), minZ = allZ.Min(), maxX = allX.Max(), maxZ = allZ.Max() },
        islandCount = islands.Count,
        islands = outIslands,
    };
    File.WriteAllText(outJson, System.Text.Json.JsonSerializer.Serialize(doc));
    Console.WriteLine($"island-study {doc.name}: {islands.Count} islands, tol={tolerance}");
    foreach (var isl in islands)
        Console.WriteLine($"  island {isl.Id}: {isl.BlockCount} blocks, bounds {isl.Bounds}");
    var raw = outIslands.Sum(o => (int)o.GetType().GetProperty("rawVerts")!.GetValue(o)!);
    var simp2 = outIslands.Sum(o => (int)o.GetType().GetProperty("simpVerts")!.GetValue(o)!);
    Console.WriteLine($"  vertices: {raw} raw → {simp2} simplified ({(raw > 0 ? 100 * simp2 / raw : 0)}%)");
    Console.WriteLine($"  wrote {outJson}");
    return 0;
}

static int RunSkeletonStudy(string regionDir, string mapXml, string outJson, double tolerance)
{
    var chunks = Directory.GetFiles(regionDir, "*.mca")
        .SelectMany(PgmStudio.Minecraft.Anvil.AnvilRegion.ReadChunks).ToList();
    static (int, int, int) ToCell(PgmStudio.Minecraft.Anvil.SurfaceBlock b) => (b.WorldX, b.WorldZ, b.WorldY);
    var baseCells = PgmStudio.Minecraft.Anvil.LayerExtractors.CleanBase(chunks).Select(ToCell).ToList();
    var fallbacks = new[]
    {
        PgmStudio.Minecraft.Anvil.LayerExtractors.Y0(chunks).Select(ToCell),
        PgmStudio.Minecraft.Anvil.LayerExtractors.Bedrock(chunks).Select(ToCell),
    };
    var islands = PgmStudio.Analysis.Footprint.IslandDetector.DetectCleaned(baseCells, fallbacks);

    var allX = islands.SelectMany(i => new[] { i.Bounds.MinX, i.Bounds.MaxX }).ToList();
    var allZ = islands.SelectMany(i => new[] { i.Bounds.MinZ, i.Bounds.MaxZ }).ToList();
    int bMinX = allX.Min() - 16, bMinZ = allZ.Min() - 16, bMaxX = allX.Max() + 16, bMaxZ = allZ.Max() + 16;

    // map.xml → doc, categories, build regions, objectives (the fixed anchor points)
    var doc = PgmStudio.Pgm.Serializer.ToDict(PgmStudio.Pgm.MapParser.Parse(mapXml));
    var regionRegistry = doc.GetValueOrDefault("regions") as Dictionary<string, object?> ?? [];
    var cats = PgmStudio.Pgm.Authoring.RegionCategorizer.Categorize(doc);
    var bounds = ((double)bMinX, (double)bMinZ, (double)bMaxX, (double)bMaxZ);

    // build-region cells — the island↔bridge contact that pruning must preserve. Rasterize only the OUTERMOST
    // build regions: the categorizer marks every nested sub-component "build" too, so a complement's base/hole
    // rectangles are also flagged — rasterizing those on their own would re-fill a hole the parent complement
    // (or union) correctly excludes. A region that is a child of another build region is covered by its parent.
    var buildIds = cats.Where(kv => kv.Value == "build").Select(kv => kv.Key).ToHashSet();
    var nestedInBuild = new HashSet<string>();
    foreach (var id in buildIds)
        if (regionRegistry.GetValueOrDefault(id) is Dictionary<string, object?> r && r.GetValueOrDefault("children") is IEnumerable<object?> ch)
            foreach (var c in ch) if (c is string cs) nestedInBuild.Add(cs);

    var buildCells = new HashSet<(int, int)>();
    foreach (var id in buildIds)
        if (!nestedInBuild.Contains(id) && regionRegistry.GetValueOrDefault(id) is Dictionary<string, object?> reg
            && PgmStudio.Analysis.Region.RegionGeometry2d.ToGeometry(reg, bounds, regionRegistry) is { IsEmpty: false } geom)
        {
            var env = geom.EnvelopeInternal;
            for (var x = (int)Math.Floor(env.MinX); x <= (int)Math.Ceiling(env.MaxX); x++)
                for (var z = (int)Math.Floor(env.MinY); z <= (int)Math.Ceiling(env.MaxY); z++)
                    if (geom.Intersects(new NetTopologySuite.Geometries.Point(x + 0.5, z + 0.5))) buildCells.Add((x, z));
        }

    var objectives = ReadObjectives(doc, regionRegistry, bounds);
    var anchorCells = new HashSet<(int, int)>(buildCells);
    foreach (var (_, ox, oz) in objectives) anchorCells.Add(((int)Math.Floor(ox), (int)Math.Floor(oz)));

    static List<double[]> Ring(NetTopologySuite.Geometries.LineString r) =>
        r.Coordinates.Select(c => new[] { c.X, c.Y }).ToList();

    var outIslands = new List<object>();
    foreach (var isl in islands)
    {
        if (isl.Polygon is not NetTopologySuite.Geometries.Polygon poly) continue;
        var ext = Ring(poly.ExteriorRing);
        var holes = poly.InteriorRings.Select(Ring).ToList();
        var simp = PgmStudio.Geom.PolygonSimplify.Simplify(ext, holes, tolerance, minHoleArea: 8);
        outIslands.Add(new
        {
            id = isl.Id,
            blockCount = isl.BlockCount,
            bounds = new[] { isl.Bounds.MinX, isl.Bounds.MinZ, isl.Bounds.MaxX, isl.Bounds.MaxZ },
            exterior = simp.Exterior,
            holes = simp.Holes,
            rawExterior = ext,
            rawHoles = holes,
        });
    }

    var name = Path.GetFileName(Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(regionDir)) ?? regionDir);
    File.WriteAllText(outJson, System.Text.Json.JsonSerializer.Serialize(new
    {
        name,
        bbox = new { minX = allX.Min(), minZ = allZ.Min(), maxX = allX.Max(), maxZ = allZ.Max() },
        islandCount = islands.Count,
        objectives = objectives.Select(o => new { kind = o.Kind, x = o.X, z = o.Z }).ToList(),
        buildCells = buildCells.Select(c => new[] { c.Item1, c.Item2 }).ToList(),
        islands = outIslands,
    }));
    Console.WriteLine($"skeleton-study {name}: {islands.Count} islands, {objectives.Count} objectives, {buildCells.Count} build cells");
    Console.WriteLine($"  wrote {outJson}");
    return 0;
}

// Objective positions from the parsed doc: wool locations, monument blocks, and spawn-region centres.
static List<(string Kind, double X, double Z)> ReadObjectives(
    Dictionary<string, object?> doc, Dictionary<string, object?> regions, (double, double, double, double) bounds)
{
    static double? Num(object? v) => v switch { double d => d, long l => l, int i => i, _ => null };
    static Dictionary<string, object?> AsDict(object? o) => o as Dictionary<string, object?> ?? [];
    static List<object?> AsList(object? o) => o as List<object?> ?? [];

    var outp = new List<(string, double, double)>();
    foreach (var w in AsList(doc.GetValueOrDefault("wools")).OfType<Dictionary<string, object?>>())
    {
        var loc = AsDict(w.GetValueOrDefault("location"));
        if (Num(loc.GetValueOrDefault("x")) is { } wx && Num(loc.GetValueOrDefault("z")) is { } wz)
            outp.Add(("wool", wx, wz));
        foreach (var m in AsList(w.GetValueOrDefault("monuments")).OfType<Dictionary<string, object?>>())
        {
            var ml = AsDict(m.GetValueOrDefault("location"));
            if (Num(ml.GetValueOrDefault("x")) is { } mx && Num(ml.GetValueOrDefault("z")) is { } mz)
                outp.Add(("monument", mx, mz));
        }
    }
    foreach (var sp in AsList(doc.GetValueOrDefault("spawns")).OfType<Dictionary<string, object?>>())
    {
        var r = sp.GetValueOrDefault("region");
        var region = r is string s ? regions.GetValueOrDefault(s) as Dictionary<string, object?> : r as Dictionary<string, object?>;
        if (region is not null
            && PgmStudio.Analysis.Region.RegionGeometry2d.ToGeometry(region, bounds, regions) is { IsEmpty: false } geom)
        {
            var c = geom.Centroid;
            outp.Add(("spawn", c.X, c.Y));
        }
    }
    return outp;
}

static (bool ok, string detail) CheckMap(string xmlPath)
{
    PgmStudio.Domain.MapXml m1;
    Dictionary<string, object?> d1;
    try
    {
        m1 = MapParser.Parse(xmlPath);
        d1 = Serializer.ToDict(m1);
    }
    catch (Exception ex) { return (false, $"parse/serialize raised: {ex.GetType().Name}: {ex.Message}"); }

    // check #1 — JSON idempotence (canonical)
    try
    {
        var d2 = Serializer.ToDict(Deserializer.FromDict(d1));
        var c1 = JsonTree.Canonical(d1);
        var c2 = JsonTree.Canonical(d2);
        if (!JsonTree.DeepEquals(c1, c2))
            return (false, $"json not idempotent; drift in: [{string.Join(", ", JsonTree.DiffKeys(c1, c2))}]");
    }
    catch (Exception ex) { return (false, $"json round-trip raised: {ex.GetType().Name}: {ex.Message}"); }

    // check #2 — XML write + re-parse, compare order-independent semantic invariants
    try
    {
        var xml2 = XmlWriter.ToXml(Deserializer.FromDict(d1));
        var m3 = MapParser.ParseXmlString(xml2);
        var s1 = Semantic(m1);
        var s3 = Semantic(m3);
        if (!JsonTree.DeepEquals(s1, s3))
            return (false, $"xml re-parse semantic drift in: [{string.Join(", ", JsonTree.DiffKeys(s1, s3))}]");
    }
    catch (Exception ex) { return (false, $"xml write/re-parse raised: {ex.GetType().Name}: {ex.Message}"); }

    return (true, "");
}

static Dictionary<string, object?> Semantic(PgmStudio.Domain.MapXml m) => new()
{
    ["teams"] = m.Teams.Select(t => t.Id).OrderBy(x => x, StringComparer.Ordinal).ToList<object?>(),
    ["wools"] = m.Wools.Select(w => $"{w.Team}/{w.Color}").OrderBy(x => x, StringComparer.Ordinal).ToList<object?>(),
    ["regions"] = m.Regions.Keys.Where(k => !k.Contains("__")).OrderBy(x => x, StringComparer.Ordinal).ToList<object?>(),
    ["filters"] = m.Filters.Keys.Where(k => !k.Contains("__")).OrderBy(x => x, StringComparer.Ordinal).ToList<object?>(),
    ["applies"] = (long)m.ApplyRules.Count,
    ["spawns"] = (long)m.Spawns.Count,
};

readonly record struct SuggestEval(
    int Truth, int Tp, int Fp, int Fn, int ColorOk, int Clusters,
    List<PgmStudio.Analysis.Suggest.MonumentSuggestion> Sites);

// Parquet shape for monument_slices.parquet (snake_case columns, one row per cell).
// ── --scan-out parquet rows (column names match the importer + the reference pipeline output) ────────────
sealed class ScanWoolRow
{
    [JP("world_x")] public int WorldX { get; set; }
    [JP("world_z")] public int WorldZ { get; set; }
    [JP("world_y")] public int WorldY { get; set; }
    [JP("color")] public string Color { get; set; } = "";
}

sealed class ScanResourceRow
{
    [JP("world_x")] public int WorldX { get; set; }
    [JP("world_z")] public int WorldZ { get; set; }
    [JP("world_y")] public int WorldY { get; set; }
    [JP("resource_type")] public string ResourceType { get; set; } = "";
}

sealed class ScanChestRow
{
    [JP("world_x")] public int WorldX { get; set; }
    [JP("world_z")] public int WorldZ { get; set; }
    [JP("world_y")] public int WorldY { get; set; }
    [JP("chest_type")] public string ChestType { get; set; } = "";
    [JP("slot")] public int Slot { get; set; }
    [JP("item_id")] public string ItemId { get; set; } = "";
    [JP("item_damage")] public int ItemDamage { get; set; }
    [JP("count")] public int Count { get; set; }
}

sealed class ScanSpawnerRow
{
    [JP("world_x")] public int WorldX { get; set; }
    [JP("world_z")] public int WorldZ { get; set; }
    [JP("world_y")] public int WorldY { get; set; }
    [JP("entity_id")] public string? EntityId { get; set; }
    [JP("spawns_wool")] public bool SpawnsWool { get; set; }
    [JP("spawn_item_id")] public string? SpawnItemId { get; set; }
    [JP("spawn_item_damage")] public int? SpawnItemDamage { get; set; }
    [JP("spawn_count")] public int? SpawnCount { get; set; }
    [JP("spawn_range")] public int? SpawnRange { get; set; }
    [JP("min_spawn_delay")] public int? MinSpawnDelay { get; set; }
    [JP("max_spawn_delay")] public int? MaxSpawnDelay { get; set; }
    [JP("required_player_range")] public int? RequiredPlayerRange { get; set; }
    [JP("max_nearby_entities")] public int? MaxNearbyEntities { get; set; }
}

sealed class ScanSegmentRow
{
    [JP("world_x")] public int WorldX { get; set; }
    [JP("world_z")] public int WorldZ { get; set; }
    [JP("world_y_start")] public int WorldYStart { get; set; }
    [JP("world_y_end")] public int WorldYEnd { get; set; }
}

sealed class ScanLayerRow
{
    [JP("world_x")] public int WorldX { get; set; }
    [JP("world_z")] public int WorldZ { get; set; }
    [JP("world_y")] public int WorldY { get; set; }
    [JP("block_id")] public int BlockId { get; set; }
    [JP("block_data")] public int BlockData { get; set; }
}

sealed class ScanMonumentRow
{
    [JP("cand_x")] public int CandX { get; set; }
    [JP("cand_y")] public int CandY { get; set; }
    [JP("cand_z")] public int CandZ { get; set; }
    [JP("source")] public string Source { get; set; } = "";
    [JP("pedestal_id")] public int PedestalId { get; set; }
    [JP("pedestal_data")] public int PedestalData { get; set; }
    [JP("cap_id")] public int CapId { get; set; }
    [JP("cap_data")] public int CapData { get; set; }
    [JP("color_hint")] public string? ColorHint { get; set; }
    [JP("sign_x")] public int? SignX { get; set; }
    [JP("sign_y")] public int? SignY { get; set; }
    [JP("sign_z")] public int? SignZ { get; set; }
    [JP("sign_facing")] public int? SignFacing { get; set; }
    [JP("sign_text")] public string? SignText { get; set; }
    [JP("stand_head_color")] public string? StandHeadColor { get; set; }
    [JP("stand_name")] public string? StandName { get; set; }
    [JP("evidence")] public string? Evidence { get; set; }
}

sealed class MonumentSliceRow
{
    [System.Text.Json.Serialization.JsonPropertyName("map")] public string Map { get; set; } = "";
    [System.Text.Json.Serialization.JsonPropertyName("wool_id")] public string WoolId { get; set; } = "";
    [System.Text.Json.Serialization.JsonPropertyName("wool_color")] public string WoolColor { get; set; } = "";
    [System.Text.Json.Serialization.JsonPropertyName("monument_id")] public string MonumentId { get; set; } = "";
    [System.Text.Json.Serialization.JsonPropertyName("team")] public string Team { get; set; } = "";
    [System.Text.Json.Serialization.JsonPropertyName("center_x")] public int CenterX { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("center_y")] public int CenterY { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("center_z")] public int CenterZ { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("dx")] public int Dx { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("dy")] public int Dy { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("dz")] public int Dz { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("world_x")] public int WorldX { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("world_y")] public int WorldY { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("world_z")] public int WorldZ { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("block_id")] public int BlockId { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("block_data")] public int BlockData { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("block_name")] public string BlockName { get; set; } = "";
    [System.Text.Json.Serialization.JsonPropertyName("is_monument")] public bool IsMonument { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("is_air")] public bool IsAir { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("tile_entity_id")] public string? TileEntityId { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("sign_text")] public string? SignText { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("tile_nbt")] public string? TileNbt { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("entity_ids")] public string? EntityIds { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("entity_nbt")] public string? EntityNbt { get; set; }

    public static MonumentSliceRow From(PgmStudio.Minecraft.Anvil.MonumentSliceCell c) => new()
    {
        Map = c.MapSlug, WoolId = c.WoolId, WoolColor = c.WoolColor, MonumentId = c.MonumentId, Team = c.Team,
        CenterX = c.CenterX, CenterY = c.CenterY, CenterZ = c.CenterZ,
        Dx = c.Dx, Dy = c.Dy, Dz = c.Dz, WorldX = c.WorldX, WorldY = c.WorldY, WorldZ = c.WorldZ,
        BlockId = c.BlockId, BlockData = c.BlockData, BlockName = c.BlockName,
        IsMonument = c.IsMonument, IsAir = c.IsAir,
        TileEntityId = c.TileEntityId, SignText = c.SignText, TileNbt = c.TileNbtJson,
        EntityIds = c.EntityIds, EntityNbt = c.EntityNbtJson,
    };
}
