#:project ../src/PgmStudio.Api/PgmStudio.Api.csproj
// A file-based app turns reflection-based JSON off by default, and the theme materials are a polymorphic
// tagged union serialized that way — without this every material comes back as "reflection is disabled".
#:property JsonSerializerIsReflectionEnabledByDefault=true
// seed-library: put the seed folder (src/PgmStudio.Minecraft/Library) into a database — the patterns, themes,
// houses and the parts they are built of, the boulders and the copied trees — without starting the studio.
//
//   dotnet run tools/seed-library.cs [connection string]
//
// Falls back to PGM_STUDIO_DB, then to the local dev database. Safe on an empty database and on a full one:
// a row already holding a seeded entry is updated in place, so a second run changes nothing. Nothing is ever
// deleted — an entry retired from the folder stays in the library as a row the author owns.
//
// It finishes by composing each seeded room style back out of the library and reporting any field that came
// back different, which is the only honest way to say whether a house survived being stored.
using PgmStudio.Api.Services;
using PgmStudio.Data;
using PgmStudio.Data.Schema;
using PgmStudio.Data.Theme;
using PgmStudio.Migrations;

var connection = args.FirstOrDefault(arg => !arg.StartsWith("--"))
    ?? Environment.GetEnvironmentVariable("PGM_STUDIO_DB")
    ?? "Server=localhost;Database=pgm_studio;User ID=pgm;Password=pgm_dev_pw;";

// The library's tables have to exist before anything can be put in them, and a database that is already
// current costs one query to find out.
var state = SchemaMigrator.GetSchemaState(connection);
if (state.Pending.Count > 0)
{
    Console.WriteLine($"applying {state.Pending.Count} pending migration(s) …");
    SchemaMigrator.MigrateUp(connection);
}

await using var db = new PgmDb(PgmDataOptions.ForConnectionString(connection));
var styles = new ThemeStore(db);
var rooms = new RoomStyleStore(db);
var parts = new HousePartStore(db);
var props = new PropStyleStore(db);

var seed = new LibrarySeed(styles, rooms, parts, props);
var tally = await seed.SeedAsync();

Console.WriteLine($"\npatterns   {tally.PatternsAdded} added, {tally.PatternsUpdated} updated");
Console.WriteLine($"parts      {tally.PartsAdded} added, {tally.PartsUpdated} updated");
Console.WriteLine($"houses     {tally.HousesAdded} added, {tally.HousesUpdated} updated");
Console.WriteLine($"themes     {tally.ThemesAdded} added, {tally.ThemesUpdated} updated");
Console.WriteLine($"recipes    {tally.RecipesAdded} added, {tally.RecipesUpdated} updated");

// ── did they survive? ─────────────────────────────────────────────────────────────────────────────────
var report = await seed.VerifyAsync();
Console.WriteLine("\nread back out of the library:\n");

var whole = 0;
foreach (var (house, lost) in report)
{
    if (lost.Count == 0) { Console.WriteLine($"  {house,-22} stored whole"); whole++; continue; }
    Console.WriteLine($"  {house,-22} lost: {string.Join(", ", lost)}");
}

Console.WriteLine();
if (whole == report.Count)
{
    Console.WriteLine($"all {whole} houses round-trip through the library.");
    return 0;
}

Console.WriteLine($"{whole} of {report.Count} houses round-trip; the rest name knobs the room-style row has");
Console.WriteLine("no column for. Those houses are stored as the building the library can describe, which is");
Console.WriteLine("not the building the preset is — see the fields listed above.");
return 0;
