using PgmStudio.Pgm;
using JP = System.Text.Json.Serialization.JsonPropertyNameAttribute;

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

// --parity <outputRoot>: compare C# ToDict(parse(map.xml)) against the real Python xml_data.json
// (canonical, i.e. bounds_2d stripped — mirror reflection bounds are deferred to M5).
var parityIdx = Array.IndexOf(args, "--parity");
if (parityIdx >= 0 && parityIdx + 1 < args.Length)
    return RunParity(args[parityIdx + 1], defaultRoots, verbose);

// --categorize <pyfreshDir> <pyfacetsDir>: compare C# RegionCategorizer.DeriveFacets to the
// Python derive_region_facets oracle over every map (exact category + ordered roles).
var catIdx = Array.IndexOf(args, "--categorize");
if (catIdx >= 0 && catIdx + 2 < args.Length)
    return RunCategorizeParity(args[catIdx + 1], args[catIdx + 2], verbose);

// --buildability <pyfreshDir> <featureRoot> <pybuildDir>: compare C# Buildability.Compute to the
// Python oracle for maps with layer_segments.parquet (Y=0 columns drive the void verdict).
var bIdx = Array.IndexOf(args, "--buildability");
if (bIdx >= 0 && bIdx + 3 < args.Length)
    return await RunBuildabilityParity(args[bIdx + 1], args[bIdx + 2], args[bIdx + 3], verbose);

// --traversability <pyfreshDir> <featureRoot> <pytravDir>: compare C# Traversability.Check to Python.
var tIdx = Array.IndexOf(args, "--traversability");
if (tIdx >= 0 && tIdx + 3 < args.Length)
    return await RunTraversabilityParity(args[tIdx + 1], args[tIdx + 2], args[tIdx + 3], verbose);

// --wool <pyfreshDir> <featureRoot> <pywoolDir>: compare C# wool-availability + resource summaries.
var wIdx = Array.IndexOf(args, "--wool");
if (wIdx >= 0 && wIdx + 3 < args.Length)
    return await RunWoolParity(args[wIdx + 1], args[wIdx + 2], args[wIdx + 3], verbose);

// --readworld <regionDir>: decode all .mca and tally block ids of interest (validates AnvilRegion).
var rwIdx = Array.IndexOf(args, "--readworld");
if (rwIdx >= 0 && rwIdx + 1 < args.Length)
{
    var dir = args[rwIdx + 1];
    var counts = new Dictionary<int, long>();
    long total = 0;
    foreach (var mca in Directory.GetFiles(dir, "*.mca"))
        foreach (var chunk in PgmStudio.Minecraft.AnvilRegion.ReadChunks(mca))
            foreach (var blk in PgmStudio.Minecraft.AnvilRegion.Blocks(chunk))
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

// --island-study <regionDir> <outJson> [tolerance]: cleaned-base islands with their polygons (exterior +
// holes) both raw and Douglas-Peucker-simplified, emitted as JSON for studying real-map shapes.
var islandStudyIdx = Array.IndexOf(args, "--island-study");
if (islandStudyIdx >= 0 && islandStudyIdx + 2 < args.Length)
    return RunIslandStudy(args[islandStudyIdx + 1], args[islandStudyIdx + 2],
        islandStudyIdx + 3 < args.Length && double.TryParse(args[islandStudyIdx + 3], out var studyTol) ? studyTol : 2.0);

// --gen-preview <archetype> <seed> <outJson>: run the sketch generator and rasterize it (no DB), writing
// the island polygons + objective hints for a local render of a generated layout.
var gpIdx = Array.IndexOf(args, "--gen-preview");
if (gpIdx >= 0 && gpIdx + 3 < args.Length)
    return RunGenPreview(args[gpIdx + 1], int.TryParse(args[gpIdx + 2], out var gseed) ? gseed : 1, args[gpIdx + 3]);

// --gen-sketch <archetype> <seed> <outJson>: dump the RAW sketch layout (the polygon shapes + mirror setup,
// before rasterization) so the base sketch model can be rendered as outlines rather than the rasterized cells.
var gsIdx = Array.IndexOf(args, "--gen-sketch");
if (gsIdx >= 0 && gsIdx + 3 < args.Length)
{
    var arch = Enum.Parse<PgmStudio.Pgm.Sketch.LaneArchetype>(args[gsIdx + 1], ignoreCase: true);
    var opts = new PgmStudio.Pgm.Sketch.LaneLayoutOptions { Archetype = arch, Seed = int.TryParse(args[gsIdx + 2], out var sseed) ? sseed : 1 };
    var res = PgmStudio.Pgm.Sketch.LaneSketchGenerator.Build(opts);
    File.WriteAllText(args[gsIdx + 3], res.Layout.ToJson());
    Console.WriteLine($"gen-sketch {arch} seed={opts.Seed}: {res.Layout.Layout!.Shapes.Count} shapes, {res.Layout.Layout.Islands.Count} islands, mirror={res.Layout.Setup!.MirrorMode}");
    return 0;
}

// --gen-catalog <outJson>: dump the non-mirrored style catalogue (every hub style + lane behaviour) as a raw
// sketch layout, for rendering an overview of the generator's shape primitives.
var gcatIdx = Array.IndexOf(args, "--gen-catalog");
if (gcatIdx >= 0 && gcatIdx + 1 < args.Length)
{
    var layout = PgmStudio.Pgm.Sketch.OrganicLane.StyleCatalog();
    File.WriteAllText(args[gcatIdx + 1], layout.ToJson());
    Console.WriteLine($"gen-catalog: {layout.Layout!.Shapes.Count} shapes → {args[gcatIdx + 1]}");
    return 0;
}

// --gen-map-preview <archetype> <seed> <outJson>: run the full map generator (LaneMapGenerator) and emit the
// island polygons together with the intent — spawns+protection, wools+rooms+monuments, build bridges — so
// playability (reachability around spawn protection) can be validated without a database.
var gmpIdx = Array.IndexOf(args, "--gen-map-preview");
if (gmpIdx >= 0 && gmpIdx + 3 < args.Length)
    return RunGenMapPreview(args[gmpIdx + 1], int.TryParse(args[gmpIdx + 2], out var mseed) ? mseed : 1, args[gmpIdx + 3]);

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

// --authoring <oracleRoot>: RegionAuthoringEncoder vs Python authoring_oracle.json over the corpus.
var auIdx = Array.IndexOf(args, "--authoring");
if (auIdx >= 0 && auIdx + 1 < args.Length)
    return RunAuthoringParity(args[auIdx + 1], defaultRoots, verbose);

// --authoring-fixture [slug ...] [--out <dir>]: write the *readable* region-authoring split for a
// map — primitives vs composed, each node trimmed to id/type/category/subtype/member_ids/wiring
// (geometry omitted as noise). A review artifact (mirror of the reference
// tools/gen_region_authoring_oracle.py), not a parity check; needs only map.xml — no islands.json or
// pipeline run. Defaults to the region-authoring test maps; output dir defaults to
// tools/region-authoring-fixtures/.
var afIdx = Array.IndexOf(args, "--authoring-fixture");
if (afIdx >= 0)
    return RunAuthoringFixture(args, defaultRoots);

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

static int RunCategorizeParity(string pyfreshDir, string pyfacetsDir, bool verbose)
{
    var dirs = Directory.GetDirectories(pyfreshDir)
        .Where(d => File.Exists(Path.Combine(d, "xml_data.json")))
        .OrderBy(d => d, StringComparer.Ordinal).ToList();

    int ok = 0, failed = 0;
    var failures = new List<(string, string)>();
    foreach (var dir in dirs)
    {
        var slug = Path.GetFileName(dir)!;
        var oraclePath = Path.Combine(pyfacetsDir, slug + ".json");
        if (!File.Exists(oraclePath)) continue;
        try
        {
            var doc = (Dictionary<string, object?>)JsonTree.FromJsonLenient(File.ReadAllText(Path.Combine(dir, "xml_data.json")))!;
            var mine = PgmStudio.Pgm.Authoring.RegionCategorizer.DeriveFacets(doc);
            var theirs = (Dictionary<string, object?>)JsonTree.FromJson(File.ReadAllText(oraclePath))!;

            var diffs = new List<string>();
            foreach (var (rid, tObj) in theirs)
            {
                var t = (Dictionary<string, object?>)tObj!;
                var tCat = t.GetValueOrDefault("category") as string ?? "";
                var tRoles = ((List<object?>)t.GetValueOrDefault("roles")!).Select(x => (string)x!).ToList();
                if (!mine.TryGetValue(rid, out var m)) { diffs.Add($"{rid}: missing in C#"); continue; }
                // C# folds the objective trio into one `wool` category + subtype; map it back to the
                // Python fine category for comparison so the 350-map guard survives the taxonomy change.
                var eff = m.Category == "wool"
                    ? m.Subtype switch { "monument" => "monument", "spawner" => "wool_spawner", _ => "wool_room" }
                    : m.Category;
                if (eff != tCat) diffs.Add($"{rid}: cat {eff}({m.Category}/{m.Subtype})≠{tCat}");
                if (!m.Roles.SequenceEqual(tRoles)) diffs.Add($"{rid}: roles [{string.Join(",", m.Roles)}]≠[{string.Join(",", tRoles)}]");
            }
            if (mine.Count != theirs.Count) diffs.Add($"count {mine.Count}≠{theirs.Count}");
            if (diffs.Count == 0) ok++;
            else { failed++; failures.Add((slug, string.Join("; ", diffs.Take(4)))); }
        }
        catch (Exception ex) { failed++; failures.Add((slug, $"{ex.GetType().Name}: {ex.Message}")); }
    }
    Console.WriteLine($"region categorizer parity vs Python: {ok} ok, {failed} failed ({dirs.Count} maps)");
    foreach (var (slug, detail) in verbose ? failures : failures.Take(20))
        Console.WriteLine($"  {slug}: {detail}");
    return failed == 0 ? 0 : 1;
}

static async Task<int> RunBuildabilityParity(string pyfreshDir, string featureRoot, string pybuildDir, bool verbose)
{
    var oracles = Directory.GetFiles(pybuildDir, "*.json").OrderBy(x => x, StringComparer.Ordinal).ToList();
    int ok = 0, failed = 0;
    var failures = new List<(string, string)>();
    foreach (var oraclePath in oracles)
    {
        var slug = Path.GetFileNameWithoutExtension(oraclePath);
        var xmlData = Path.Combine(pyfreshDir, slug, "xml_data.json");
        var segPath = Path.Combine(featureRoot, slug, "layer_segments.parquet");
        if (!File.Exists(xmlData) || !File.Exists(segPath)) continue;
        try
        {
            var doc = (Dictionary<string, object?>)JsonTree.FromJsonLenient(File.ReadAllText(xmlData))!;
            var y0 = new PgmStudio.Analysis.Layer.SegmentIndex(await ReadSegments(segPath)).Y0Columns();
            var res = PgmStudio.Analysis.Playability.Buildability.Compute(doc, y0);
            var oracle = (Dictionary<string, object?>)JsonTree.FromJson(File.ReadAllText(oraclePath))!;

            var diffs = new List<string>();
            var ob = ((List<object?>)oracle["bbox"]!).Select(Convert.ToInt32).ToList();
            if (res.MinX != ob[0] || res.MinZ != ob[1] || res.MaxX != ob[2] || res.MaxZ != ob[3])
                diffs.Add($"bbox [{res.MinX},{res.MinZ},{res.MaxX},{res.MaxZ}]≠[{string.Join(",", ob)}]");
            var oc = (Dictionary<string, object?>)oracle["counts"]!;
            foreach (var c in PgmStudio.Analysis.Playability.Buildability.Classes)
                if (res.Counts[c] != Convert.ToInt32(oc[c])) diffs.Add($"{c} {res.Counts[c]}≠{oc[c]}");
            var orows = ((List<object?>)oracle["rows"]!).Select(x => (string)x!).ToList();
            var myRows = Enumerable.Range(0, res.Height)
                .Select(iz => string.Concat(Enumerable.Range(0, res.Width).Select(ix => (char)('0' + res.Verdict[iz * res.Width + ix])))).ToList();
            if (myRows.Count != orows.Count) diffs.Add($"rowcount {myRows.Count}≠{orows.Count}");
            else
            {
                var cellDiffs = Enumerable.Range(0, orows.Count).Sum(i => orows[i].Where((ch, j) => j < myRows[i].Length && myRows[i][j] != ch).Count());
                if (cellDiffs > 0) diffs.Add($"{cellDiffs} cell diff(s) of {res.Width * res.Height}");
            }
            if (diffs.Count == 0) ok++; else { failed++; failures.Add((slug, string.Join("; ", diffs.Take(4)))); }
        }
        catch (Exception ex) { failed++; failures.Add((slug, $"{ex.GetType().Name}: {ex.Message}")); }
    }
    Console.WriteLine($"buildability parity vs Python: {ok} ok, {failed} failed ({oracles.Count} maps)");
    foreach (var (slug, d) in verbose ? failures : failures.Take(20)) Console.WriteLine($"  {slug}: {d}");
    return failed == 0 ? 0 : 1;
}

static async Task<int> RunTraversabilityParity(string pyfreshDir, string featureRoot, string pytravDir, bool verbose)
{
    var oracles = Directory.GetFiles(pytravDir, "*.json").OrderBy(x => x, StringComparer.Ordinal).ToList();
    int ok = 0, failed = 0;
    var failures = new List<(string, string)>();
    foreach (var oraclePath in oracles)
    {
        var slug = Path.GetFileNameWithoutExtension(oraclePath);
        var xmlData = Path.Combine(pyfreshDir, slug, "xml_data.json");
        var segPath = Path.Combine(featureRoot, slug, "layer_segments.parquet");
        if (!File.Exists(xmlData) || !File.Exists(segPath)) continue;
        try
        {
            var doc = (Dictionary<string, object?>)JsonTree.FromJsonLenient(File.ReadAllText(xmlData))!;
            var si = new PgmStudio.Analysis.Layer.SegmentIndex(await ReadSegments(segPath));
            var res = PgmStudio.Analysis.Playability.Traversability.Check(doc, si.SurfaceColumns(), si.Y0Columns());
            var oracle = (Dictionary<string, object?>)JsonTree.FromJson(File.ReadAllText(oraclePath))!;

            var diffs = new List<string>();
            if (res.Connected != (bool)oracle["connected"]!) diffs.Add($"connected {res.Connected}≠{oracle["connected"]}");
            if (res.ComponentCount != Convert.ToInt32(oracle["component_count"])) diffs.Add($"component_count {res.ComponentCount}≠{oracle["component_count"]}");
            if (res.HaveLayers != (bool)oracle["have_layers"]!) diffs.Add($"have_layers {res.HaveLayers}≠{oracle["have_layers"]}");
            var op = ((List<object?>)oracle["points"]!).Cast<Dictionary<string, object?>>().ToList();
            if (op.Count != res.Points.Count) diffs.Add($"points {res.Points.Count}≠{op.Count}");
            else for (var i = 0; i < op.Count; i++)
                {
                    var mp = res.Points[i];
                    if (mp.Kind != op[i]["kind"] as string || mp.Name != op[i]["name"] as string
                        || mp.X != Convert.ToInt32(op[i]["x"]) || mp.Z != Convert.ToInt32(op[i]["z"])
                        || mp.Component != Convert.ToInt32(op[i]["component"]))
                        diffs.Add($"point[{i}] {mp.Kind}/{mp.Name}@({mp.X},{mp.Z})c{mp.Component} ≠ {op[i]["kind"]}/{op[i]["name"]}@({op[i]["x"]},{op[i]["z"]})c{op[i]["component"]}");
                }
            if (diffs.Count == 0) ok++; else { failed++; failures.Add((slug, string.Join("; ", diffs.Take(3)))); }
        }
        catch (Exception ex) { failed++; failures.Add((slug, $"{ex.GetType().Name}: {ex.Message}")); }
    }
    Console.WriteLine($"traversability parity vs Python: {ok} ok, {failed} failed ({oracles.Count} maps)");
    foreach (var (slug, d) in verbose ? failures : failures.Take(20)) Console.WriteLine($"  {slug}: {d}");
    return failed == 0 ? 0 : 1;
}

static async Task<int> RunWoolParity(string pyfreshDir, string featureRoot, string pywoolDir, bool verbose)
{
    var oracles = Directory.GetFiles(pywoolDir, "*.json").OrderBy(x => x, StringComparer.Ordinal).ToList();
    int ok = 0, failed = 0;
    var failures = new List<(string, string)>();
    foreach (var oraclePath in oracles)
    {
        var slug = Path.GetFileNameWithoutExtension(oraclePath);
        var xmlData = Path.Combine(pyfreshDir, slug, "xml_data.json");
        var dir = Path.Combine(featureRoot, slug);
        if (!File.Exists(xmlData)) continue;
        try
        {
            var doc = (Dictionary<string, object?>)JsonTree.FromJsonLenient(File.ReadAllText(xmlData))!;
            var sources = await LoadWoolSources(dir);
            sources.AddRange(PgmStudio.Analysis.Playability.WoolSources.PgmSpawnerSources(doc));
            var avail = PgmStudio.Analysis.Playability.WoolSources.CheckAvailability(doc, sources);
            var resBlocks = await LoadResourceBlocks(dir);
            var res = PgmStudio.Analysis.Playability.ResourceSources.Summarize(resBlocks, null, PgmStudio.Analysis.Playability.ResourceSources.RenewableRegions(doc));

            var oracle = (Dictionary<string, object?>)JsonTree.FromJson(File.ReadAllText(oraclePath))!;
            var diffs = new List<string>();

            var oa = ((List<object?>)oracle["availability"]!).Cast<Dictionary<string, object?>>().ToList();
            if (oa.Count != avail.Count) diffs.Add($"availability {avail.Count}≠{oa.Count}");
            else for (var i = 0; i < oa.Count; i++)
                {
                    var m = avail[i]; var o = oa[i];
                    if (m.Color != o["color"] as string || m.Obtainable != (bool)o["obtainable"]! || m.Repeatable != (bool)o["repeatable"]!
                        || m.OneTime != (bool)o["one_time"]! || m.Severity != o["severity"] as string
                        || !m.SourceTypes.SequenceEqual(((List<object?>)o["source_types"]!).Select(x => (string)x!))
                        || m.Message != o["message"] as string)
                        diffs.Add($"avail[{i}] {m.Color}/{m.Severity}/[{string.Join(",", m.SourceTypes)}] ≠ {o["color"]}/{o["severity"]}/[{string.Join(",", ((List<object?>)o["source_types"]!))}]");
                }

            var orr = ((List<object?>)oracle["resources"]!).Cast<Dictionary<string, object?>>().ToList();
            if (orr.Count != res.Count) diffs.Add($"resources {res.Count}≠{orr.Count}");
            else for (var i = 0; i < orr.Count; i++)
                {
                    var m = res[i]; var o = orr[i];
                    if (m.Type != o["type"] as string || m.Total != Convert.ToInt32(o["total"]) || m.Renewable != Convert.ToInt32(o["renewable"]) || m.AllRenewable != (bool)o["all_renewable"]!)
                        diffs.Add($"res[{i}] {m.Type} t{m.Total}/r{m.Renewable} ≠ {o["type"]} t{o["total"]}/r{o["renewable"]}");
                }

            if (diffs.Count == 0) ok++; else { failed++; failures.Add((slug, string.Join("; ", diffs.Take(3)))); }
        }
        catch (Exception ex) { failed++; failures.Add((slug, $"{ex.GetType().Name}: {ex.Message}")); }
    }
    Console.WriteLine($"wool/resource parity vs Python: {ok} ok, {failed} failed ({oracles.Count} maps)");
    foreach (var (slug, d) in verbose ? failures : failures.Take(20)) Console.WriteLine($"  {slug}: {d}");
    return failed == 0 ? 0 : 1;
}

static async Task<List<PgmStudio.Analysis.Playability.WoolSources.Source>> LoadWoolSources(string dir)
{
    var sources = new List<PgmStudio.Analysis.Playability.WoolSources.Source>();
    var wp = Path.Combine(dir, "wools.parquet");
    if (File.Exists(wp))
        foreach (var r in await ReadParquet(wp))
            sources.Add(new("block", PgmStudio.Domain.WoolColors.Normalize(r["color"]?.ToString() ?? ""),
                Convert.ToInt32(r["world_x"]), Convert.ToInt32(r["world_y"]), Convert.ToInt32(r["world_z"]), 1));
    var cp = Path.Combine(dir, "chests.parquet");
    if (File.Exists(cp))
        foreach (var r in await ReadParquet(cp))
        {
            if (!(r["item_id"]?.ToString() ?? "").Contains("wool", StringComparison.OrdinalIgnoreCase)) continue;
            if (!PgmStudio.Domain.WoolColors.WoolDamageToColor.TryGetValue(Convert.ToInt32(r["item_damage"]), out var color)) continue;
            sources.Add(new("chest", color, Convert.ToInt32(r["world_x"]), Convert.ToInt32(r["world_y"]), Convert.ToInt32(r["world_z"]), Convert.ToInt32(r["count"])));
        }
    var sp = Path.Combine(dir, "spawners.parquet");
    if (File.Exists(sp))
        foreach (var r in await ReadParquet(sp))
        {
            if (r.GetValueOrDefault("spawns_wool") is not true) continue;
            if (r.GetValueOrDefault("spawn_item_damage") is null || !PgmStudio.Domain.WoolColors.WoolDamageToColor.TryGetValue(Convert.ToInt32(r["spawn_item_damage"]), out var color)) continue;
            var count = r.GetValueOrDefault("spawn_count") is { } sc ? Convert.ToInt32(sc) : 1;
            sources.Add(new("spawner", color, Convert.ToInt32(r["world_x"]), Convert.ToInt32(r["world_y"]), Convert.ToInt32(r["world_z"]), count == 0 ? 1 : count));
        }
    return sources;
}

static async Task<List<PgmStudio.Analysis.Playability.ResourceSources.Block>> LoadResourceBlocks(string dir)
{
    var path = Path.Combine(dir, "resources.parquet");
    if (!File.Exists(path)) return [];
    return (await ReadParquet(path)).Select(r => new PgmStudio.Analysis.Playability.ResourceSources.Block(
        r["resource_type"]?.ToString() ?? "", Convert.ToInt32(r["world_x"]), Convert.ToInt32(r["world_y"]), Convert.ToInt32(r["world_z"]))).ToList();
}

static async Task<List<Dictionary<string, object?>>> ReadParquet(string path)
{
    await using var stream = File.OpenRead(path);
    var result = await Parquet.Serialization.ParquetSerializer.DeserializeUntypedAsync(stream);
    return result.Data.Select(d => d.ToDictionary(kv => kv.Key, kv => (object?)kv.Value)).ToList();
}

static async Task<int> RunExtractParity(string regionDir, string oracleDir)
{
    var mcas = Directory.GetFiles(regionDir, "*.mca");
    IEnumerable<PgmStudio.Minecraft.AnvilRegion.Chunk> Chunks() => mcas.SelectMany(PgmStudio.Minecraft.AnvilRegion.ReadChunks);

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
    var woolMine = PgmStudio.Minecraft.FeatureExtractors.Wools(Chunks())
        .Select(w => $"{w.WorldX},{w.WorldZ},{w.WorldY},{w.Color}");
    var woolOra = (await TryRead(Path.Combine(oracleDir, "wools.parquet")))
        .Select(r => $"{N(r["world_x"])},{N(r["world_z"])},{N(r["world_y"])},{S(r["color"])}");
    Check("wools", woolMine, woolOra);

    // resources
    var resMine = PgmStudio.Minecraft.FeatureExtractors.Resources(Chunks())
        .Select(r => $"{r.WorldX},{r.WorldZ},{r.WorldY},{r.ResourceType}");
    var resOra = (await TryRead(Path.Combine(oracleDir, "resources.parquet")))
        .Select(r => $"{N(r["world_x"])},{N(r["world_z"])},{N(r["world_y"])},{S(r["resource_type"])}");
    Check("resources", resMine, resOra);

    // chests
    var chestMine = PgmStudio.Minecraft.FeatureExtractors.Chests(Chunks())
        .Select(c => $"{c.WorldX},{c.WorldZ},{c.WorldY},{c.ChestType},{c.Slot},{c.ItemId},{c.ItemDamage},{c.Count}");
    var chestOra = (await TryRead(Path.Combine(oracleDir, "chests.parquet")))
        .Select(r => $"{N(r["world_x"])},{N(r["world_z"])},{N(r["world_y"])},{S(r["chest_type"])},{N(r["slot"])},{S(r["item_id"])},{N(r["item_damage"])},{N(r["count"])}");
    Check("chests", chestMine, chestOra);

    // spawners
    var spMine = PgmStudio.Minecraft.FeatureExtractors.Spawners(Chunks())
        .Select(s => $"{s.WorldX},{s.WorldZ},{s.WorldY}|{s.EntityId}|{(s.SpawnsWool ? "1" : "0")}|{s.SpawnItemId}|{Fmt(s.SpawnItemDamage)}|{Fmt(s.SpawnCount)}|{Fmt(s.SpawnRange)}|{Fmt(s.MinSpawnDelay)}|{Fmt(s.MaxSpawnDelay)}|{Fmt(s.RequiredPlayerRange)}|{Fmt(s.MaxNearbyEntities)}");
    var spOra = (await TryRead(Path.Combine(oracleDir, "spawners.parquet")))
        .Select(r => $"{N(r["world_x"])},{N(r["world_z"])},{N(r["world_y"])}|{S(r.GetValueOrDefault("entity_id"))}|{B(r["spawns_wool"])}|{S(r.GetValueOrDefault("spawn_item_id"))}|{N(r.GetValueOrDefault("spawn_item_damage"))}|{N(r.GetValueOrDefault("spawn_count"))}|{N(r.GetValueOrDefault("spawn_range"))}|{N(r.GetValueOrDefault("min_spawn_delay"))}|{N(r.GetValueOrDefault("max_spawn_delay"))}|{N(r.GetValueOrDefault("required_player_range"))}|{N(r.GetValueOrDefault("max_nearby_entities"))}");
    Check("spawners", spMine, spOra);

    // layer_segments
    var segMine = PgmStudio.Minecraft.FeatureExtractors.Segments(Chunks())
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

    var outDir = Path.Combine(outRoot, slug);
    Directory.CreateDirectory(outDir);
    var sw = System.Diagnostics.Stopwatch.StartNew();

    // Materialise the world's chunks once — every extractor re-enumerates them (matches WorldFeatureWriter).
    var chunks = Directory.GetFiles(regionDir, "*.mca").SelectMany(PgmStudio.Minecraft.AnvilRegion.ReadChunks).ToList();

    // feature rows → parquet (column names match the importer + the reference output)
    await WriteParquet(Path.Combine(outDir, "wools.parquet"), PgmStudio.Minecraft.FeatureExtractors.Wools(chunks)
        .Select(w => new ScanWoolRow { WorldX = w.WorldX, WorldZ = w.WorldZ, WorldY = w.WorldY, Color = w.Color }).ToList());
    await WriteParquet(Path.Combine(outDir, "resources.parquet"), PgmStudio.Minecraft.FeatureExtractors.Resources(chunks)
        .Select(r => new ScanResourceRow { WorldX = r.WorldX, WorldZ = r.WorldZ, WorldY = r.WorldY, ResourceType = r.ResourceType }).ToList());
    await WriteParquet(Path.Combine(outDir, "chests.parquet"), PgmStudio.Minecraft.FeatureExtractors.Chests(chunks)
        .Select(c => new ScanChestRow { WorldX = c.WorldX, WorldZ = c.WorldZ, WorldY = c.WorldY, ChestType = c.ChestType, Slot = c.Slot, ItemId = c.ItemId, ItemDamage = c.ItemDamage, Count = c.Count }).ToList());
    await WriteParquet(Path.Combine(outDir, "spawners.parquet"), PgmStudio.Minecraft.FeatureExtractors.Spawners(chunks)
        .Select(s => new ScanSpawnerRow { WorldX = s.WorldX, WorldZ = s.WorldZ, WorldY = s.WorldY, EntityId = s.EntityId, SpawnsWool = s.SpawnsWool, SpawnItemId = s.SpawnItemId, SpawnItemDamage = s.SpawnItemDamage, SpawnCount = s.SpawnCount, SpawnRange = s.SpawnRange, MinSpawnDelay = s.MinSpawnDelay, MaxSpawnDelay = s.MaxSpawnDelay, RequiredPlayerRange = s.RequiredPlayerRange, MaxNearbyEntities = s.MaxNearbyEntities }).ToList());
    await WriteParquet(Path.Combine(outDir, "layer_segments.parquet"), PgmStudio.Minecraft.FeatureExtractors.Segments(chunks)
        .Select(s => new ScanSegmentRow { WorldX = s.WorldX, WorldZ = s.WorldZ, WorldYStart = s.WorldYStart, WorldYEnd = s.WorldYEnd }).ToList());

    // Surface layer → layer.parquet (the cached artifact + the bounding-box source)
    var surface = PgmStudio.Minecraft.LayerExtractors.Surface(chunks).ToList();
    await WriteParquet(Path.Combine(outDir, "layer.parquet"), surface
        .Select(s => new ScanLayerRow { WorldX = s.WorldX, WorldZ = s.WorldZ, WorldY = s.WorldY, BlockId = s.BlockId, BlockData = s.BlockData }).ToList());

    // Stair-aware islands on the cleaned columns, with the lazy y0 → bedrock fallback (matches
    // WorldFeatureWriter) → islands.json
    static (int X, int Z, int Y) Cell(PgmStudio.Minecraft.SurfaceBlock b) => (b.WorldX, b.WorldZ, b.WorldY);
    var columns = PgmStudio.Minecraft.LayerExtractors.CleanColumns(chunks)
        .Select(c => (c.WorldX, c.WorldZ, c.BaseY, c.Surfaces)).ToList();
    var fallbacks = new[] { PgmStudio.Minecraft.LayerExtractors.Y0(chunks).Select(Cell), PgmStudio.Minecraft.LayerExtractors.Bedrock(chunks).Select(Cell) };
    var islands = PgmStudio.Analysis.Footprint.IslandDetector.DetectCleanedStairAware(columns, fallbacks);
    await File.WriteAllTextAsync(Path.Combine(outDir, "islands.json"), PgmStudio.Analysis.Footprint.IslandDetector.SerializeJson(islands));

    // Monument-candidate gather (F9 suggester) over the whole world → monument_candidates.parquet (the one
    // world-derived dataset the live scan-world writes that the reference file set never had).
    var worldBox = chunks.Count == 0
        ? new PgmStudio.Minecraft.ScanBox(0, 0, 0, 0, 0, 0)
        : new PgmStudio.Minecraft.ScanBox(chunks.Min(c => c.ChunkX) * 16, 0, chunks.Min(c => c.ChunkZ) * 16,
            chunks.Max(c => c.ChunkX) * 16 + 15, 255, chunks.Max(c => c.ChunkZ) * 16 + 15);
    var monuments = PgmStudio.Minecraft.MonumentSuggester.Gather(chunks, worldBox);
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

    // xml_data.json — the codec source the importer deserializes (parse map.xml with the studio's own parser)
    if (File.Exists(mapXml))
    {
        var doc = PgmStudio.Pgm.Serializer.ToDict(PgmStudio.Pgm.MapParser.Parse(mapXml));
        await File.WriteAllTextAsync(Path.Combine(outDir, "xml_data.json"), System.Text.Json.JsonSerializer.Serialize(doc));
    }
    else
        Console.Error.WriteLine($"  {slug}: WARNING no map.xml — xml_data.json not written; the importer will skip this dir");

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
    var chunks = Directory.GetFiles(regionDir, "*.mca").SelectMany(PgmStudio.Minecraft.AnvilRegion.ReadChunks).ToList();
    static (int, int, int) ToCell(PgmStudio.Minecraft.SurfaceBlock b) => (b.WorldX, b.WorldZ, b.WorldY);
    var columns = PgmStudio.Minecraft.LayerExtractors.CleanColumns(chunks)
        .Select(c => (c.WorldX, c.WorldZ, c.BaseY, c.Surfaces)).ToList();
    var fallbacks = new[] { PgmStudio.Minecraft.LayerExtractors.Y0(chunks).Select(ToCell), PgmStudio.Minecraft.LayerExtractors.Bedrock(chunks).Select(ToCell) };
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
    var chunks = Directory.GetFiles(regionDir, "*.mca").SelectMany(PgmStudio.Minecraft.AnvilRegion.ReadChunks).ToList();

    var baseCells = PgmStudio.Minecraft.LayerExtractors.CleanBase(chunks)
        .Select(b => (b.WorldX, b.WorldZ, b.WorldY)).ToList();
    var fallbacks = new[]
    {
        PgmStudio.Minecraft.LayerExtractors.Y0(chunks).Select(b => (b.WorldX, b.WorldZ, b.WorldY)),
        PgmStudio.Minecraft.LayerExtractors.Bedrock(chunks).Select(b => (b.WorldX, b.WorldZ, b.WorldY)),
    };
    var old = PgmStudio.Analysis.Footprint.IslandDetector.DetectCleaned(baseCells, fallbacks);

    var columns = PgmStudio.Minecraft.LayerExtractors.CleanColumns(chunks)
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
    var monuments = new List<PgmStudio.Minecraft.MonumentTarget>();
    if (jd.RootElement.TryGetProperty("wools", out var woolsEl))
        foreach (var wool in woolsEl.EnumerateArray())
        {
            var woolId = wool.TryGetProperty("id", out var wid) ? wid.GetString() ?? "" : "";
            var color = wool.TryGetProperty("color", out var c) ? c.GetString() ?? woolId : woolId;
            if (!wool.TryGetProperty("monuments", out var mons)) continue;   // some maps omit it
            foreach (var mon in mons.EnumerateArray())
            {
                if (!mon.TryGetProperty("location", out var loc)) continue;
                monuments.Add(new PgmStudio.Minecraft.MonumentTarget(
                    slug, woolId, color,
                    mon.TryGetProperty("id", out var mid) ? mid.GetString() ?? "" : "",
                    mon.TryGetProperty("team", out var mt) ? mt.GetString() ?? "" : "",
                    Coord(loc, "x"), Coord(loc, "y"), Coord(loc, "z")));
            }
        }

    var mcas = Directory.GetFiles(regionDir, "*.mca");
    IEnumerable<PgmStudio.Minecraft.AnvilRegion.Chunk> Chunks() => mcas.SelectMany(PgmStudio.Minecraft.AnvilRegion.ReadChunks);

    Console.WriteLine($"{slug}: {monuments.Count} monument(s) over {mcas.Length} region file(s)");
    var cells = PgmStudio.Minecraft.MonumentSliceExtractor.Extract(Chunks(), monuments);

    var rows = cells.Select(MonumentSliceRow.From).ToList();
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outParquet))!);
    await using (var os = File.Create(outParquet))
        if (rows.Count > 0) await Parquet.Serialization.ParquetSerializer.SerializeAsync(rows, os);
    Console.WriteLine($"wrote {rows.Count} cell(s) → {outParquet}");
    if (rows.Count == 0) { Console.WriteLine("  (no monuments — nothing to validate)"); return 0; }

    // Read back and validate against the extractor's invariants.
    var back = await ReadParquet(outParquet);
    var fails = 0;
    void Require(bool ok, string what) { if (!ok) { fails++; Console.WriteLine($"  FAIL {what}"); } else Console.WriteLine($"  OK   {what}"); }

    static int I(object? v) => Convert.ToInt32(v);
    static string S(object? v) => v?.ToString() ?? "";
    static bool B(object? v) => v is not null && Convert.ToBoolean(v);

    Require(back.Count == monuments.Count * PgmStudio.Minecraft.MonumentSliceExtractor.CellsPerMonument,
        $"row count = {monuments.Count} monuments × {PgmStudio.Minecraft.MonumentSliceExtractor.CellsPerMonument} = {monuments.Count * PgmStudio.Minecraft.MonumentSliceExtractor.CellsPerMonument} (got {back.Count})");

    var byMon = back.GroupBy(r => S(r["monument_id"])).ToList();
    Require(byMon.Count == monuments.Count, $"{monuments.Count} distinct monuments present");
    Require(byMon.All(g => g.Count() == PgmStudio.Minecraft.MonumentSliceExtractor.CellsPerMonument), "every monument has exactly 45 cells");

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
                        var color = hid.GetString()?.EndsWith("wool") == true ? $" → {PgmStudio.Domain.WoolColors.WoolColor(dmg)}" : "";
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
    PgmStudio.Minecraft.PedestalKind pedestal, PgmStudio.Minecraft.LabelKind label,
    PgmStudio.Minecraft.CapKind cap, int margin)
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
    var chunks = mcas.SelectMany(PgmStudio.Minecraft.AnvilRegion.ReadChunks).ToList();   // decode the world once

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
            foreach (var b in PgmStudio.Minecraft.AnvilRegion.Blocks(ch))
                if (want.Contains((b.X, b.Y, b.Z))) adj[(b.X, b.Y, b.Z)] = b.Id;
    }
    // reuse the suggester's single id↔kind table, so auto-style can't drift from detection
    PgmStudio.Minecraft.PedestalKind PedestalBelow(int x, int y, int z) =>
        PgmStudio.Minecraft.MonumentSuggester.ClassifyPedestal(adj.GetValueOrDefault((x, y - 1, z), 0));
    PgmStudio.Minecraft.CapKind CapAbove(int x, int y, int z) =>
        PgmStudio.Minecraft.MonumentSuggester.ClassifyCap(adj.GetValueOrDefault((x, y + 1, z), 0));

    var suggestions = new Dictionary<(int, int, int), PgmStudio.Minecraft.MonumentSuggestion>();
    foreach (var cl in clusters)
    {
        var box = new PgmStudio.Minecraft.ScanBox(
            cl.Min(q => q.x) - margin, cl.Min(q => q.y) - margin, cl.Min(q => q.z) - margin,
            cl.Max(q => q.x) + margin, cl.Max(q => q.y) + margin, cl.Max(q => q.z) + margin);
        var ped = autoStyle
            ? cl.Select(q => PedestalBelow(q.x, q.y, q.z)).GroupBy(k => k).OrderByDescending(g => g.Count()).First().Key
            : pedestal;
        var cp = autoStyle
            ? cl.Select(q => CapAbove(q.x, q.y, q.z)).GroupBy(k => k).OrderByDescending(g => g.Count()).First().Key
            : cap;
        foreach (var s in PgmStudio.Minecraft.MonumentSuggester.Suggest(chunks, box, new PgmStudio.Minecraft.MonumentStyle(ped, label, cp)))
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
    var pedestal = Enum.Parse<PgmStudio.Minecraft.PedestalKind>(Flag("--pedestal", "Any"), true);
    var label = Enum.Parse<PgmStudio.Minecraft.LabelKind>(Flag("--label", "Any"), true);
    var cap = Enum.Parse<PgmStudio.Minecraft.CapKind>(Flag("--cap", "Any"), true);
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
    var pedestal = Enum.Parse<PgmStudio.Minecraft.PedestalKind>(Flag("--pedestal", "Any"), true);
    var label = Enum.Parse<PgmStudio.Minecraft.LabelKind>(Flag("--label", "Any"), true);
    var cap = Enum.Parse<PgmStudio.Minecraft.CapKind>(Flag("--cap", "Any"), true);

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
    File.Exists(path) ? await ReadParquet(path) : [];

static int RunAuthoringParity(string oracleRoot, string[] corpusRoots, bool verbose)
{
    var dirs = Directory.GetDirectories(oracleRoot)
        .Where(d => File.Exists(Path.Combine(d, "authoring_oracle.json")))
        .OrderBy(d => d, StringComparer.Ordinal).ToList();

    int ok = 0, failed = 0;
    foreach (var dir in dirs)
    {
        var slug = Path.GetFileName(dir)!;
        var mapXml = corpusRoots.Select(r => Path.Combine(r, slug, "map.xml")).FirstOrDefault(File.Exists);
        if (mapXml is null) continue;

        var doc = Serializer.ToDict(MapParser.Parse(mapXml));
        var regions = doc.GetValueOrDefault("regions") as Dictionary<string, object?> ?? [];
        var applyRules = doc.GetValueOrDefault("apply_rules") as List<object?>;
        var cats = PgmStudio.Pgm.Authoring.RegionCategorizer.Categorize(doc);
        var bbox = ReadIslandsBbox(Path.Combine(dir, "islands.json"));

        var mine = PgmStudio.Analysis.Region.RegionAuthoringEncoder.EncodeAuthoring(regions, cats, applyRules, bbox);
        // PGM oo/-oo coords/bounds (e.g. an all-XZ cuboid) are ±Infinity on both sides; normalise
        // ±Infinity → a finite sentinel and NaN → null so JsonDocument can read both identically.
        var mineStr = System.Text.Json.JsonSerializer.Serialize(mine,
            new System.Text.Json.JsonSerializerOptions { NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals });
        var mineJson = System.Text.Json.JsonDocument.Parse(NormInf(mineStr)).RootElement;
        var oracle = System.Text.Json.JsonDocument.Parse(NormInf(File.ReadAllText(Path.Combine(dir, "authoring_oracle.json")))).RootElement;

        var mineSig = AuthoringSigs(mineJson);
        var oraSig = AuthoringSigs(oracle);
        var diffs = new List<string>();
        foreach (var (id, sig) in oraSig)
            if (!mineSig.TryGetValue(id, out var ms)) diffs.Add($"missing node '{id}'");
            else if (ms != sig) diffs.Add($"'{id}': mine[{ms}] != oracle[{sig}]");
        foreach (var id in mineSig.Keys.Except(oraSig.Keys)) diffs.Add($"extra node '{id}'");

        if (diffs.Count == 0) { ok++; Console.WriteLine($"  OK   {slug,-16} prim+comp={mineSig.Count}"); }
        else { failed++; Console.WriteLine($"  FAIL {slug}"); foreach (var d in (verbose ? diffs : diffs.Take(4))) Console.WriteLine($"        {d}"); }
    }
    Console.WriteLine($"authoring parity: {ok} ok, {failed} failed");
    return failed == 0 ? 0 : 1;
}

// Region-authoring split as a readable JSON artifact, one file per map. Mirrors the reference
// tools/gen_region_authoring_oracle.py: {map, counts, primitives, composed}, each node trimmed to the
// fields that define the *authoring view* — id/type/category/subtype, composed member_ids, and the
// apply-rule wiring (event→value). Subtype isn't on the authoring node itself; it's pulled from the
// categoriser facets (RegionFacet.Subtype) per region id.
static int RunAuthoringFixture(string[] args, string[] corpusRoots)
{
    var outIdx = Array.IndexOf(args, "--out");
    var outDir = outIdx >= 0 && outIdx + 1 < args.Length
        ? args[outIdx + 1]
        : Path.Combine(RepoRoot(), "tools", "region-authoring-fixtures");

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
static string NormInf(string json)
{
    json = System.Text.RegularExpressions.Regex.Replace(json, @"(?<![\w""])-Infinity(?![\w""])", "-1e308");
    json = System.Text.RegularExpressions.Regex.Replace(json, @"(?<![\w""])Infinity(?![\w""])", "1e308");
    return System.Text.RegularExpressions.Regex.Replace(json, @"(?<![\w""])NaN(?![\w""])", "null");
}

static (double, double, double, double)? ReadIslandsBbox(string path)
{
    if (!File.Exists(path)) return null;
    var arr = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path)).RootElement;
    if (arr.ValueKind != System.Text.Json.JsonValueKind.Array || arr.GetArrayLength() == 0) return null;
    double minX = double.MaxValue, minZ = double.MaxValue, maxX = double.MinValue, maxZ = double.MinValue;
    foreach (var e in arr.EnumerateArray())
    {
        var b = e.GetProperty("bounds");
        minX = Math.Min(minX, b[0].GetDouble()); minZ = Math.Min(minZ, b[1].GetDouble());
        maxX = Math.Max(maxX, b[2].GetDouble()); maxZ = Math.Max(maxZ, b[3].GetDouble());
    }
    return (minX, minZ, maxX, maxZ);
}

// id → robust signature: type, category, member_ids, wiring(event|value), bounds(2dp), polygon vert-count + bbox.
static Dictionary<string, string> AuthoringSigs(System.Text.Json.JsonElement root)
{
    var sigs = new Dictionary<string, string>();
    foreach (var listName in new[] { "primitives", "composed" })
    {
        if (!root.TryGetProperty(listName, out var list)) continue;
        foreach (var n in list.EnumerateArray())
        {
            var id = n.GetProperty("id").GetString() ?? "";
            sigs[id] = NodeSig(n, listName);
        }
    }
    return sigs;
}

static string NodeSig(System.Text.Json.JsonElement n, string list)
{
    string S(string k) => n.TryGetProperty(k, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.String ? v.GetString()! : "";
    var members = n.TryGetProperty("member_ids", out var m) && m.ValueKind == System.Text.Json.JsonValueKind.Array
        ? string.Join(",", m.EnumerateArray().Select(x => x.GetString()).OrderBy(x => x)) : "";
    var wiring = n.TryGetProperty("wiring", out var w) && w.ValueKind == System.Text.Json.JsonValueKind.Array
        ? string.Join(",", w.EnumerateArray().Select(x => $"{x.GetProperty("event").GetString()}={x.GetProperty("value").GetString()}").OrderBy(x => x)) : "";
    var bounds = BoundsSig(n);
    var poly = "nopoly";
    if (n.TryGetProperty("polygon_2d", out var p) && p.ValueKind == System.Text.Json.JsonValueKind.Object)
    {
        var ext = p.GetProperty("exterior");
        double mnx = double.MaxValue, mnz = double.MaxValue, mxx = double.MinValue, mxz = double.MinValue;
        foreach (var pt in ext.EnumerateArray())
        {
            mnx = Math.Min(mnx, D(pt[0])); mnz = Math.Min(mnz, D(pt[1]));
            mxx = Math.Max(mxx, D(pt[0])); mxz = Math.Max(mxz, D(pt[1]));
        }
        poly = $"P{ext.GetArrayLength()}:{R(mnx)},{R(mnz)},{R(mxx)},{R(mxz)}";
    }
    return $"{list}|{S("type")}|{S("category")}|m[{members}]|w[{wiring}]|{bounds}|{poly}";
}

static string BoundsSig(System.Text.Json.JsonElement n)
{
    if (!n.TryGetProperty("bounds", out var b) || b.ValueKind != System.Text.Json.JsonValueKind.Object) return "nb";
    return $"{R(D(b.GetProperty("min_x")))},{R(D(b.GetProperty("min_z")))},{R(D(b.GetProperty("max_x")))},{R(D(b.GetProperty("max_z")))}";
}

// Robust number read: a PGM oo/-oo coordinate can surface as a string; map it to ±inf sentinel.
static double D(System.Text.Json.JsonElement e) => e.ValueKind switch
{
    System.Text.Json.JsonValueKind.Number => e.GetDouble(),
    System.Text.Json.JsonValueKind.String => e.GetString() is { } s
        ? (s is "oo" ? 1e308 : s is "-oo" ? -1e308 : double.TryParse(s, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : double.NaN)
        : double.NaN,
    _ => double.NaN,
};

static string R(double v) => Math.Round(v, 1).ToString(System.Globalization.CultureInfo.InvariantCulture);

static async Task<int> RunIslandParity(string regionDir, string oracleDir)
{
    var mcas = Directory.GetFiles(regionDir, "*.mca");
    IEnumerable<PgmStudio.Minecraft.AnvilRegion.Chunk> Chunks() => mcas.SelectMany(PgmStudio.Minecraft.AnvilRegion.ReadChunks);

    // Surface layer: compare row count to layer.parquet (default ScanConfig: surface, no exclude/cap).
    var surface = PgmStudio.Minecraft.LayerExtractors.Surface(Chunks()).ToList();
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
        .SelectMany(PgmStudio.Minecraft.AnvilRegion.ReadChunks).ToList();
    static (int, int, int) ToCell(PgmStudio.Minecraft.SurfaceBlock b) => (b.WorldX, b.WorldZ, b.WorldY);

    var baseCells = PgmStudio.Minecraft.LayerExtractors.CleanBase(chunks).Select(ToCell).ToList();
    // Deferred — only extracted/scanned if the cleaned base reads degenerately (the fallback path).
    var fallbacks = new[]
    {
        PgmStudio.Minecraft.LayerExtractors.Y0(chunks).Select(ToCell),
        PgmStudio.Minecraft.LayerExtractors.Bedrock(chunks).Select(ToCell),
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
        .SelectMany(PgmStudio.Minecraft.AnvilRegion.ReadChunks).ToList();
    static (int, int, int) ToCell(PgmStudio.Minecraft.SurfaceBlock b) => (b.WorldX, b.WorldZ, b.WorldY);
    var baseCells = PgmStudio.Minecraft.LayerExtractors.CleanBase(chunks).Select(ToCell).ToList();
    var fallbacks = new[]
    {
        PgmStudio.Minecraft.LayerExtractors.Y0(chunks).Select(ToCell),
        PgmStudio.Minecraft.LayerExtractors.Bedrock(chunks).Select(ToCell),
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

static int RunGenPreview(string archetype, int seed, string outJson)
{
    var arch = Enum.Parse<PgmStudio.Pgm.Sketch.LaneArchetype>(archetype, ignoreCase: true);
    var opts = new PgmStudio.Pgm.Sketch.LaneLayoutOptions { Archetype = arch, Seed = seed };
    var result = PgmStudio.Pgm.Sketch.LaneSketchGenerator.Build(opts);
    var cells = PgmStudio.Pgm.Sketch.SketchRasterizer.Rasterize(result.Layout.ToJson());
    if (cells.Count == 0) { Console.Error.WriteLine("no cells produced"); return 1; }
    var islands = PgmStudio.Analysis.Footprint.IslandDetector.Detect(cells, minIslandSize: 1);
    var polys = islands.Select(i =>
    {
        var p = i.Polygon as NetTopologySuite.Geometries.Polygon;
        return new
        {
            id = i.Id,
            blockCount = i.BlockCount,
            exterior = p!.ExteriorRing.Coordinates.Select(c => new[] { c.X, c.Y }).ToList(),
            holes = p.InteriorRings.Select(r => r.Coordinates.Select(c => new[] { c.X, c.Y }).ToList()).ToList(),
        };
    }).ToList();
    File.WriteAllText(outJson, System.Text.Json.JsonSerializer.Serialize(new
    {
        archetype, seed,
        bbox = new { minX = cells.Min(c => c.X), minZ = cells.Min(c => c.Z), maxX = cells.Max(c => c.X), maxZ = cells.Max(c => c.Z) },
        objectives = result.Objectives.Select(o => new { kind = o.Kind, team = o.Team, x = o.X, z = o.Z }).ToList(),
        islands = polys,
    }));
    Console.WriteLine($"gen-preview {archetype} seed={seed}: {cells.Count} cells, {islands.Count} islands [{string.Join(",", islands.Take(8).Select(i => i.BlockCount))}]");
    return 0;
}

static int RunGenMapPreview(string archetype, int seed, string outJson)
{
    var arch = Enum.Parse<PgmStudio.Pgm.Sketch.LaneArchetype>(archetype, ignoreCase: true);
    var (layout, intent) = PgmStudio.Pgm.Sketch.LaneMapGenerator.Generate(
        new PgmStudio.Pgm.Sketch.LaneLayoutOptions { Archetype = arch, Seed = seed }, "preview");
    var cells = PgmStudio.Pgm.Sketch.SketchRasterizer.Rasterize(layout.ToJson());
    if (cells.Count == 0) { Console.Error.WriteLine("no cells"); return 1; }
    var islands = PgmStudio.Analysis.Footprint.IslandDetector.Detect(cells, minIslandSize: 1);
    object? Rect(PgmStudio.Pgm.Authoring.Rect? r) => r is { } v ? new { minX = v.MinX, minZ = v.MinZ, maxX = v.MaxX, maxZ = v.MaxZ } : null;
    File.WriteAllText(outJson, System.Text.Json.JsonSerializer.Serialize(new
    {
        archetype, seed,
        bbox = new { minX = cells.Min(c => c.X), minZ = cells.Min(c => c.Z), maxX = cells.Max(c => c.X), maxZ = cells.Max(c => c.Z) },
        islands = islands.Select(i =>
        {
            var p = (NetTopologySuite.Geometries.Polygon)i.Polygon;
            return new
            {
                exterior = p.ExteriorRing.Coordinates.Select(c => new[] { c.X, c.Y }).ToList(),
                holes = p.InteriorRings.Select(r => r.Coordinates.Select(c => new[] { c.X, c.Y }).ToList()).ToList(),
            };
        }).ToList(),
        spawns = intent.Spawns.Select(s => new { team = s.Team, x = s.Point.X, z = s.Point.Z, protection = Rect(s.Protection) }).ToList(),
        wools = intent.Wools!.Select(w => new
        {
            owner = w.Owner, x = w.Spawn.X, z = w.Spawn.Z, room = Rect(w.Room),
            monuments = w.Monuments.Select(mm => new { team = mm.Team, x = mm.Location.X, z = mm.Location.Z }).ToList(),
        }).ToList(),
        build = new { areas = intent.Build!.Areas.Select(r => new { minX = r.MinX, minZ = r.MinZ, maxX = r.MaxX, maxZ = r.MaxZ }).ToList() },
    }));
    Console.WriteLine($"gen-map-preview {archetype} seed={seed}: {islands.Count} islands, {intent.Spawns.Count} spawns, {intent.Wools!.Count} wools, {intent.Build!.Areas.Count} bridges");
    return 0;
}

static int RunSkeletonStudy(string regionDir, string mapXml, string outJson, double tolerance)
{
    var chunks = Directory.GetFiles(regionDir, "*.mca")
        .SelectMany(PgmStudio.Minecraft.AnvilRegion.ReadChunks).ToList();
    static (int, int, int) ToCell(PgmStudio.Minecraft.SurfaceBlock b) => (b.WorldX, b.WorldZ, b.WorldY);
    var baseCells = PgmStudio.Minecraft.LayerExtractors.CleanBase(chunks).Select(ToCell).ToList();
    var fallbacks = new[]
    {
        PgmStudio.Minecraft.LayerExtractors.Y0(chunks).Select(ToCell),
        PgmStudio.Minecraft.LayerExtractors.Bedrock(chunks).Select(ToCell),
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

static async Task<List<(int, int, int, int)>> ReadSegments(string path)
{
    await using var stream = File.OpenRead(path);
    var result = await Parquet.Serialization.ParquetSerializer.DeserializeUntypedAsync(stream);
    return result.Data.Select(r => (Convert.ToInt32(r["world_x"]), Convert.ToInt32(r["world_z"]),
        Convert.ToInt32(r["world_y_start"]), Convert.ToInt32(r["world_y_end"]))).ToList();
}

static int RunParity(string outputRoot, string[] corpusRoots, bool verbose)
{
    var dirs = Directory.GetDirectories(outputRoot)
        .Where(d => File.Exists(Path.Combine(d, "xml_data.json")))
        .OrderBy(d => d, StringComparer.Ordinal).ToList();

    int ok = 0, failed = 0, skipped = 0;
    var failures = new List<(string, string)>();
    foreach (var dir in dirs)
    {
        var slug = Path.GetFileName(dir)!;
        var mapXml = corpusRoots.Select(r => Path.Combine(r, slug, "map.xml")).FirstOrDefault(File.Exists);
        if (mapXml is null) { skipped++; continue; }
        try
        {
            var mine = JsonTree.Canonical(Serializer.ToDict(MapParser.Parse(mapXml)));
            // Python json.dumps emits bare NaN/Infinity (non-standard) for some derived bounds;
            // System.Text.Json can't read them. Null them out — bounds_2d is stripped anyway.
            var raw = System.Text.RegularExpressions.Regex.Replace(
                File.ReadAllText(Path.Combine(dir, "xml_data.json")),
                @"(?<![\w""])(-?Infinity|NaN)(?![\w""])", "null");
            var theirs = (Dictionary<string, object?>)JsonTree.FromJson(raw)!;
            theirs = JsonTree.Canonical(theirs);
            if (JsonTree.DeepEquals(mine, theirs)) ok++;
            else { failed++; failures.Add((slug, $"drift in: [{string.Join(", ", JsonTree.DiffKeys(mine, theirs))}]")); }
        }
        catch (Exception ex) { failed++; failures.Add((slug, $"{ex.GetType().Name}: {ex.Message}")); }
    }
    Console.WriteLine($"parity vs Python xml_data.json (canonical): {ok} ok, {failed} failed, {skipped} skipped (no map.xml)");
    foreach (var (slug, detail) in verbose ? failures : failures.Take(20))
        Console.WriteLine($"  {slug}: {detail}");
    return failed == 0 ? 0 : 1;
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
    List<PgmStudio.Minecraft.MonumentSuggestion> Sites);

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

    public static MonumentSliceRow From(PgmStudio.Minecraft.MonumentSliceCell c) => new()
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
