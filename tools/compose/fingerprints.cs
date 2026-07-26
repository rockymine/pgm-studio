#:project ../../src/PgmStudio.Pgm/PgmStudio.Pgm.csproj
#:property JsonSerializerIsReflectionEnabledByDefault=true
// Record what the current composer makes of a fixed set of requests, so the test suite can tell a geometry
// change apart from a version that failed to move with it. Run after deliberately changing the pipeline AND
// bumping ComposerVersion.Current; the file it writes is meant to be regenerated, not defended.
using PgmStudio.Pgm.Compose;

var path = Path.Combine([".", .. ComposerFingerprint.FilePath]);
var previous = File.Exists(path) ? ComposerFingerprintFile.Parse(File.ReadAllText(path)) : null;

var next = ComposerFingerprint.Compute();
File.WriteAllText(path, next.ToJson() + "\n");

Console.WriteLine($"composer version: {next.ComposerVersion}"
    + (previous is null ? "  (no previous record)"
       : previous.ComposerVersion == next.ComposerVersion ? "  (unchanged)"
       : $"  (was {previous.ComposerVersion})"));

int moved = 0, added = 0;
foreach (var (key, digest) in next.Boards)
{
    var was = previous?.Boards.GetValueOrDefault(key);
    if (was == digest) continue;
    if (was is null) { added++; continue; }              // a request new to the set has nothing to have moved
    moved++;
    Console.WriteLine($"  {key,-22} {was} -> {digest}");
}
Console.WriteLine($"{next.Boards.Count} boards, {moved} moved, {added} new");

if (moved > 0 && previous is not null && previous.ComposerVersion == next.ComposerVersion)
    Console.WriteLine("WARNING: geometry moved but the composer version did not — bump "
        + "ComposerVersion.Current before recording, or the record claims a reproducibility it has not got.");
