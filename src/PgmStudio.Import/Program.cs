using LinqToDB;
using LinqToDB.Async;
using LinqToDB.Data;
using PgmStudio.Data;
using PgmStudio.Data.Repositories;
using PgmStudio.Import;
using PgmStudio.Migrations;
using PgmStudio.Pgm;

// Importer: processed map output dirs → MariaDB.
//   dotnet run --project src/PgmStudio.Import [outputRoot]
// Connection string from PGM_STUDIO_DB, else the local dev database.

var connectionString = Environment.GetEnvironmentVariable("PGM_STUDIO_DB")
    ?? "Server=localhost;Database=pgm_studio;User ID=pgm;Password=pgm_dev_pw;";
var outputRoot = args.FirstOrDefault(a => !a.StartsWith("--")) ?? "/media/sf_repos/pgm-map-studio-output";

Console.WriteLine($"Ensuring schema on {Mask(connectionString)} …");
SchemaMigrator.MigrateUp(connectionString);

await using var db = new PgmDb(PgmDataOptions.ForConnectionString(connectionString));
var importer = new MapImporter(db);

// Known-malformed maps, excluded by design (do NOT relax the schema to accommodate them).
var exclusions = new Dictionary<string, string>
{
    ["kytriak_te"] = "two empty-id teams (violates per-map unique team id)",
};

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
                          $"spawns={c.Spawns,-3} blocks(w/r/c/s/seg)={c.WoolBlocks}/{c.ResourceBlocks}/{c.ChestItems}/{c.SpawnerBlocks}/{c.LayerSegments} artifacts={c.Artifacts}");
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
