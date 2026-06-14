using PgmStudio.Pgm;

// Corpus round-trip fidelity harness (C# port of tools/roundtrip_check.py).
//
//   check #1 — JSON idempotence (canonical):  ToDict(parse) == ToDict(FromDict(ToDict(parse)))
//              with the derived bounds_2d stripped from regions.
//   check #2 — XML semantic re-parse:          parse -> json -> MapXml -> to_xml -> re-parse,
//              compare named ids + counts.  (enabled once the XML writer lands)
//
// Usage:  dotnet run --project tools/PgmStudio.RoundTrip [root ...] [--verbose]

string[] defaultRoots = ["/media/sf_repos/CommunityMaps/ctw", "/media/sf_repos/PublicMaps/ctw"];
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

// --islands <regionDir> <oracleDir>: surface scan + island detection vs layer.parquet/islands.json.
var isIdx = Array.IndexOf(args, "--islands");
if (isIdx >= 0 && isIdx + 2 < args.Length)
    return await RunIslandParity(args[isIdx + 1], args[isIdx + 2]);

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

// --colors <oracle.json>: compare BlockColors.Hex/Name against a Python colors.py oracle dump
// ({"bid,bdat": {"hex": "#rrggbb", "name": "..."}}) over every known (id,data) pair.
var colIdx = Array.IndexOf(args, "--colors");
if (colIdx >= 0 && colIdx + 1 < args.Length)
{
    using var jd = System.Text.Json.JsonDocument.Parse(File.ReadAllText(args[colIdx + 1]));
    int cok = 0, cfail = 0;
    foreach (var entry in jd.RootElement.EnumerateObject())
    {
        var parts = entry.Name.Split(',');
        int bid = int.Parse(parts[0]), bdat = int.Parse(parts[1]);
        var wantHex = entry.Value.GetProperty("hex").GetString();
        var wantName = entry.Value.GetProperty("name").GetString();
        var gotHex = PgmStudio.Minecraft.BlockColors.Hex(bid, bdat);
        var gotName = PgmStudio.Minecraft.BlockColors.Name(bid, bdat);
        if (gotHex == wantHex && gotName == wantName) { cok++; continue; }
        cfail++;
        Console.WriteLine($"  ({bid},{bdat}): hex {gotHex} vs {wantHex} | name '{gotName}' vs '{wantName}'");
    }
    Console.WriteLine($"colors parity: {cok} ok, {cfail} failed ({cok + cfail} known pairs)");
    return cfail == 0 ? 0 : 1;
}

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
            var mine = PgmStudio.Analysis.RegionCategorizer.DeriveFacets(doc);
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
            var y0 = new PgmStudio.Analysis.SegmentIndex(await ReadSegments(segPath)).Y0Columns();
            var res = PgmStudio.Analysis.Buildability.Compute(doc, y0);
            var oracle = (Dictionary<string, object?>)JsonTree.FromJson(File.ReadAllText(oraclePath))!;

            var diffs = new List<string>();
            var ob = ((List<object?>)oracle["bbox"]!).Select(Convert.ToInt32).ToList();
            if (res.MinX != ob[0] || res.MinZ != ob[1] || res.MaxX != ob[2] || res.MaxZ != ob[3])
                diffs.Add($"bbox [{res.MinX},{res.MinZ},{res.MaxX},{res.MaxZ}]≠[{string.Join(",", ob)}]");
            var oc = (Dictionary<string, object?>)oracle["counts"]!;
            foreach (var c in PgmStudio.Analysis.Buildability.Classes)
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
            var si = new PgmStudio.Analysis.SegmentIndex(await ReadSegments(segPath));
            var res = PgmStudio.Analysis.Traversability.Check(doc, si.SurfaceColumns(), si.Y0Columns());
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
            sources.AddRange(PgmStudio.Analysis.WoolSources.PgmSpawnerSources(doc));
            var avail = PgmStudio.Analysis.WoolSources.CheckAvailability(doc, sources);
            var resBlocks = await LoadResourceBlocks(dir);
            var res = PgmStudio.Analysis.ResourceSources.Summarize(resBlocks, null, PgmStudio.Analysis.ResourceSources.RenewableRegions(doc));

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

static async Task<List<PgmStudio.Analysis.WoolSources.Source>> LoadWoolSources(string dir)
{
    var sources = new List<PgmStudio.Analysis.WoolSources.Source>();
    var wp = Path.Combine(dir, "wools.parquet");
    if (File.Exists(wp))
        foreach (var r in await ReadParquet(wp))
            sources.Add(new("block", PgmStudio.Analysis.WoolColors.Normalize(r["color"]?.ToString() ?? ""),
                Convert.ToInt32(r["world_x"]), Convert.ToInt32(r["world_y"]), Convert.ToInt32(r["world_z"]), 1));
    var cp = Path.Combine(dir, "chests.parquet");
    if (File.Exists(cp))
        foreach (var r in await ReadParquet(cp))
        {
            if (!(r["item_id"]?.ToString() ?? "").Contains("wool", StringComparison.OrdinalIgnoreCase)) continue;
            if (!PgmStudio.Analysis.WoolColors.WoolDamageToColor.TryGetValue(Convert.ToInt32(r["item_damage"]), out var color)) continue;
            sources.Add(new("chest", color, Convert.ToInt32(r["world_x"]), Convert.ToInt32(r["world_y"]), Convert.ToInt32(r["world_z"]), Convert.ToInt32(r["count"])));
        }
    var sp = Path.Combine(dir, "spawners.parquet");
    if (File.Exists(sp))
        foreach (var r in await ReadParquet(sp))
        {
            if (r.GetValueOrDefault("spawns_wool") is not true) continue;
            if (r.GetValueOrDefault("spawn_item_damage") is null || !PgmStudio.Analysis.WoolColors.WoolDamageToColor.TryGetValue(Convert.ToInt32(r["spawn_item_damage"]), out var color)) continue;
            var count = r.GetValueOrDefault("spawn_count") is { } sc ? Convert.ToInt32(sc) : 1;
            sources.Add(new("spawner", color, Convert.ToInt32(r["world_x"]), Convert.ToInt32(r["world_y"]), Convert.ToInt32(r["world_z"]), count == 0 ? 1 : count));
        }
    return sources;
}

static async Task<List<PgmStudio.Analysis.ResourceSources.Block>> LoadResourceBlocks(string dir)
{
    var path = Path.Combine(dir, "resources.parquet");
    if (!File.Exists(path)) return [];
    return (await ReadParquet(path)).Select(r => new PgmStudio.Analysis.ResourceSources.Block(
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
        var cats = PgmStudio.Analysis.RegionCategorizer.Categorize(doc);
        var bbox = ReadIslandsBbox(Path.Combine(dir, "islands.json"));

        var mine = PgmStudio.Analysis.RegionAuthoringEncoder.EncodeAuthoring(regions, cats, applyRules, bbox);
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
        var cats = PgmStudio.Analysis.RegionCategorizer.Categorize(doc);
        var facets = PgmStudio.Analysis.RegionCategorizer.DeriveFacets(doc);

        var split = PgmStudio.Analysis.RegionAuthoringEncoder.EncodeAuthoring(regions, cats, applyRules, null);
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
    IReadOnlyDictionary<string, PgmStudio.Analysis.RegionFacet> facets)
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
    var mineIslands = PgmStudio.Analysis.IslandDetector.Detect(surface.Select(s => (s.WorldX, s.WorldZ)));
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
