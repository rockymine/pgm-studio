using LinqToDB;
using LinqToDB.Async;
using LinqToDB.Data;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Import;
using PgmStudio.Migrations;
using PgmStudio.Pgm;

// Importer: processed map output dirs → MariaDB.
//   dotnet run --project src/PgmStudio.Import [outputRoot]
// Connection string from PGM_STUDIO_DB, else the local dev database.

var connectionString = Environment.GetEnvironmentVariable("PGM_STUDIO_DB");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("Set the PGM_STUDIO_DB environment variable (the database connection string).");
    return 1;
}
var outputRoot = args.FirstOrDefault(a => !a.StartsWith("--")) ?? Environment.GetEnvironmentVariable("PGM_STUDIO_OUTPUT_ROOT");

Console.WriteLine($"Ensuring schema on {Mask(connectionString)} …");
SchemaMigrator.MigrateUp(connectionString);

// --migrate-only: apply pending migrations and exit (no import) — e.g. to add a new table to a live DB.
if (args.Contains("--migrate-only")) { Console.WriteLine("schema is up to date."); return 0; }

await using var db = new PgmDb(PgmDataOptions.ForConnectionString(connectionString));
var importer = new MapImporter(db);

// --refresh-xml: re-derive each existing map's XML entities (regions/filters/teams/wools/…) from the
// current map.xml under the corpus roots, via the editor write path (SaveDocAsync) — preserves the
// world-derived feature rows + artifacts (no world re-scan). Fixes stale regions (D1) cheaply.
if (args.Contains("--refresh-xml"))
{
    string[] corpusRoots = (Environment.GetEnvironmentVariable("PGM_STUDIO_MAPS_ROOTS") ?? "")
        .Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    if (corpusRoots.Length == 0) { Console.Error.WriteLine("--refresh-xml needs PGM_STUDIO_MAPS_ROOTS (semicolon/comma-separated corpus roots)."); return 1; }
    var writer = new MapWriter(db);
    var maps = await db.Maps.OrderBy(m => m.Slug).ToListAsync();
    Console.WriteLine($"Refreshing XML for {maps.Count} map(s) from current map.xml\n");
    int refreshed = 0, skipped = 0, errored = 0, changed = 0;
    foreach (var map in maps)
    {
        var xml = corpusRoots.Select(r => Path.Combine(r, map.Slug, "map.xml")).FirstOrDefault(File.Exists);
        if (xml is null) { skipped++; continue; }
        try
        {
            var before = await db.Regions.CountAsync(r => r.MapId == map.Id);
            var doc = Serializer.ToDict(MapParser.Parse(xml));
            await writer.SaveDocAsync(map.Id, doc);
            var after = await db.Regions.CountAsync(r => r.MapId == map.Id);
            refreshed++;
            if (before != after) { changed++; Console.WriteLine($"  {map.Slug,-24} regions {before} → {after}"); }
        }
        catch (Exception ex) { errored++; Console.WriteLine($"  {map.Slug,-24} FAILED: {ex.GetType().Name}: {ex.Message}"); }
    }
    Console.WriteLine($"\nrefreshed {refreshed} ({changed} with region-count changes), skipped {skipped} (no map.xml), failed {errored}");
    return 0;
}

// --monuments-only: ingest just the F9 monument-candidate gather (monument_candidates.parquet) for maps
// already in the DB — for a re-scan that only added monuments, without a full re-import.
if (args.Contains("--monuments-only"))
{
    if (string.IsNullOrWhiteSpace(outputRoot)) { Console.Error.WriteLine("--monuments-only needs the output root (arg or PGM_STUDIO_OUTPUT_ROOT)."); return 1; }
    var mdirs = Directory.GetDirectories(outputRoot)
        .Where(d => File.Exists(Path.Combine(d, "monument_candidates.parquet")))
        .OrderBy(d => d, StringComparer.Ordinal).ToList();
    Console.WriteLine($"Ingesting monument candidates for {mdirs.Count} map(s) with a monument file …");
    int mok = 0, mskip = 0, mtot = 0;
    foreach (var dir in mdirs)
    {
        var slug = Path.GetFileName(dir)!;
        var map = await db.Maps.FirstOrDefaultAsync(mm => mm.Slug == slug);
        if (map is null) { mskip++; continue; }
        mtot += await importer.ImportMonumentCandidatesAsync(map.Id, dir);
        mok++;
    }
    Console.WriteLine($"monument candidates: {mtot} rows across {mok} map(s); {mskip} dir(s) not in DB (full-import them first)");
    return 0;
}

// --islands-only: replace each map's islands_json artifact from the re-scanned islands.json files (e.g. a
// stair-aware re-detect) and refresh the derived island_sketch_json — without the full re-import, which drops
// the map row and FK-cascades away its human authoring artifacts (intent / decomposition / review / sketch).
// Only islands_json changes between re-scans of the same world, so this is the surgical update. Skips dirs
// not yet in the DB (full-import them first).
if (args.Contains("--islands-only"))
{
    if (string.IsNullOrWhiteSpace(outputRoot)) { Console.Error.WriteLine("--islands-only needs the output root (arg or PGM_STUDIO_OUTPUT_ROOT)."); return 1; }
    var idirs = Directory.GetDirectories(outputRoot)
        .Where(d => File.Exists(Path.Combine(d, "islands.json")))
        .OrderBy(d => d, StringComparer.Ordinal).ToList();
    Console.WriteLine($"Updating islands_json (+ derived island_sketch_json) for {idirs.Count} map dir(s) …");
    int iok = 0, iskip = 0, isketch = 0;
    foreach (var dir in idirs)
    {
        var slug = Path.GetFileName(dir)!;
        var map = await db.Maps.FirstOrDefaultAsync(mm => mm.Slug == slug);
        if (map is null) { iskip++; continue; }

        var data = await File.ReadAllBytesAsync(Path.Combine(dir, "islands.json"));
        await db.Artifacts.Where(a => a.MapId == map.Id && a.Kind == ArtifactKind.IslandsJson).DeleteAsync();
        await db.InsertAsync(new MapArtifactRow { MapId = map.Id, Kind = ArtifactKind.IslandsJson, Data = data });

        // refresh the derived sketch so island-sketch readers see the new outlines (drop a now-stale one)
        await db.Artifacts.Where(a => a.MapId == map.Id && a.Kind == ArtifactKind.IslandSketchJson).DeleteAsync();
        if (IslandSketchArtifact.FromIslandsJson(data) is { } sketch)
        {
            await db.InsertAsync(new MapArtifactRow { MapId = map.Id, Kind = ArtifactKind.IslandSketchJson, Data = sketch });
            isketch++;
        }
        iok++;
    }
    Console.WriteLine($"islands-only: updated {iok} map(s) ({isketch} with a sketch); {iskip} dir(s) not in DB");
    return 0;
}

// --store-island-sketch: store each map's Douglas-Peucker simplified island outlines (+ holes) in the sketch
// layout format, under the island_sketch_json artifact. Derived from the stored islands_json — no re-scan.
if (args.Contains("--store-island-sketch"))
{
    var maps2 = await db.Maps.OrderBy(m => m.Slug).ToListAsync();
    int sok = 0, sskip = 0;
    foreach (var map in maps2)
    {
        var art = await db.Artifacts.FirstOrDefaultAsync(a => a.MapId == map.Id && a.Kind == ArtifactKind.IslandsJson);
        if (art?.Data is null) { sskip++; continue; }
        var bytes = IslandSketchArtifact.FromIslandsJson(art.Data);
        if (bytes is null) { sskip++; continue; }
        await db.Artifacts.Where(a => a.MapId == map.Id && a.Kind == ArtifactKind.IslandSketchJson).DeleteAsync();
        await db.InsertAsync(new MapArtifactRow { MapId = map.Id, Kind = ArtifactKind.IslandSketchJson, Data = bytes });
        sok++;
    }
    Console.WriteLine($"island-sketch: stored for {sok} map(s); {sskip} skipped (no islands)");
    return 0;
}

// Known-malformed maps, excluded by design (do NOT relax the schema to accommodate them).
var exclusions = new Dictionary<string, string>
{
    ["kytriak_te"] = "two empty-id teams (violates per-map unique team id)",
};

if (string.IsNullOrWhiteSpace(outputRoot))
{
    Console.Error.WriteLine("No output root: pass it as an argument or set PGM_STUDIO_OUTPUT_ROOT.");
    return 1;
}

var dirs = Directory.GetDirectories(outputRoot)
    .Where(d => File.Exists(Path.Combine(d, "xml_data.json")))
    .Where(d => !exclusions.ContainsKey(Path.GetFileName(d)!))
    .OrderBy(d => d, StringComparer.Ordinal).ToList();
if (exclusions.Count > 0)
    Console.WriteLine("excluded (malformed): " + string.Join(", ", exclusions.Select(e => $"{e.Key} ({e.Value})")));

Console.WriteLine($"Importing {dirs.Count} map(s) from {outputRoot}\n");
int ok = 0, failed = 0;
foreach (var dir in dirs)
{
    var slug = Path.GetFileName(dir)!;
    try
    {
        var c = await importer.ImportDirAsync(slug, dir);
        ok++;
        Console.WriteLine($"  {slug,-22} regions={c.Regions,-4} filters={c.Filters,-4} wools={c.Wools}/{c.Monuments,-5} " +
                          $"spawns={c.Spawns,-3} blocks(w/r/c/s/seg)={c.WoolBlocks}/{c.ResourceBlocks}/{c.ChestItems}/{c.SpawnerBlocks}/{c.LayerSegments} mon_cand={c.MonumentCandidates,-4} artifacts={c.Artifacts}");
    }
    catch (Exception ex) { failed++; Console.WriteLine($"  {slug,-22} FAILED: {ex.GetType().Name}: {ex.Message}"); }
}

// Verification: DB feature-row counts must equal the source parquet row counts.
Console.WriteLine("\nVerifying DB row counts vs source parquet …");
var mismatches = 0;
foreach (var dir in dirs)
{
    var slug = Path.GetFileName(dir)!;
    var map = await db.Maps.FirstOrDefaultAsync(m => m.Slug == slug);
    if (map is null) continue;
    foreach (var (file, dbCount) in new (string, Func<long, Task<int>>)[]
             {
                 ("wools.parquet", id => db.WoolBlocks.CountAsync(x => x.MapId == id)),
                 ("resources.parquet", id => db.ResourceBlocks.CountAsync(x => x.MapId == id)),
                 ("chests.parquet", id => db.ChestItems.CountAsync(x => x.MapId == id)),
                 ("layer_segments.parquet", id => db.LayerSegments.CountAsync(x => x.MapId == id)),
             })
    {
        var path = Path.Combine(dir, file);
        var srcCount = File.Exists(path) ? (await ParquetIo.ReadRowsAsync(path)).Count : 0;
        var got = await dbCount(map.Id);
        if (got != srcCount) { mismatches++; Console.WriteLine($"  MISMATCH {slug}/{file}: db={got} src={srcCount}"); }
    }
}

// Doc round-trip: MapReader(DB) → ToDict must equal the imported xml_data.json (canonical).
Console.WriteLine("Verifying DB round-trip (MapReader → doc == source xml_data.json) …");
var reader = new MapReader(db);
int rtOk = 0, rtBad = 0;
foreach (var dir in dirs)
{
    var slug = Path.GetFileName(dir)!;
    var dbDoc0 = await reader.ReadDocAsync(slug);
    if (dbDoc0 is null) { rtBad++; continue; }
    var dbDoc = JsonTree.Canonical(dbDoc0);
    var srcDoc = JsonTree.Canonical((Dictionary<string, object?>)JsonTree.FromJson(
        System.Text.RegularExpressions.Regex.Replace(File.ReadAllText(Path.Combine(dir, "xml_data.json")),
            @"(?<![\w""])(-?Infinity|NaN)(?![\w""])", "null"))!);
    if (JsonTree.DeepEquals(dbDoc, srcDoc)) rtOk++;
    else { rtBad++; Console.WriteLine($"  ROUND-TRIP DRIFT {slug}: [{string.Join(", ", JsonTree.DiffKeys(dbDoc, srcDoc))}]"); }
}
Console.WriteLine($"  round-trip: {rtOk} ok, {rtBad} drift");

Console.WriteLine($"\nimport: {ok} ok, {failed} failed; row-count verification: {(mismatches == 0 ? "all match" : $"{mismatches} mismatch(es)")}; round-trip: {rtBad} drift");
return failed == 0 && mismatches == 0 && rtBad == 0 ? 0 : 1;

static string Mask(string cs) => System.Text.RegularExpressions.Regex.Replace(cs, "(?i)password=[^;]*", "password=***");
