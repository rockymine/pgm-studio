#:project ../../src/PgmStudio.Minecraft/PgmStudio.Minecraft.csproj

// A stable digest of MonumentSuggester.Gather over real corpus worlds — the before/after equality check for
// a refactor that touches the detector's box type. Gather needs only the world, so it runs without the scan
// outputs the precision harness requires, and every field of every candidate feeds the hash: a changed
// anchor, offset, colour hint or ordering moves the digest.

using System.Security.Cryptography;
using System.Text;
using PgmStudio.Domain;
using PgmStudio.Geom;
using PgmStudio.Minecraft;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Suggest;

var maps = Directory.GetDirectories("/media/sf_repos/CommunityMaps")
    .SelectMany(Directory.GetDirectories)
    .OrderBy(dir => dir, StringComparer.Ordinal)
    .Where(dir => Directory.Exists(Path.Combine(dir, "region")))
    .Take(40)
    .ToList();

var digest = SHA256.Create();
var total = 0;

foreach (var mapDir in maps)
{
    List<AnvilRegion.Chunk> chunks;
    try { chunks = Directory.GetFiles(Path.Combine(mapDir, "region"), "*.mca").SelectMany(AnvilRegion.ReadChunks).ToList(); }
    catch { continue; }
    if (chunks.Count == 0) continue;

    var world = new BlockBox(
        chunks.Min(chunk => chunk.ChunkX) * 16, 0, chunks.Min(chunk => chunk.ChunkZ) * 16,
        chunks.Max(chunk => chunk.ChunkX) * 16 + 15, 255, chunks.Max(chunk => chunk.ChunkZ) * 16 + 15);

    var candidates = MonumentSuggester.Gather(chunks, world);
    total += candidates.Count;
    Console.Error.WriteLine($"{Path.GetFileName(mapDir),-28} {candidates.Count}");

    foreach (var candidate in candidates)
    {
        var line = string.Join('|', Path.GetFileName(mapDir), candidate.X, candidate.Y, candidate.Z,
            candidate.Source, candidate.PedestalId, candidate.PedestalData, candidate.CapId, candidate.CapData,
            candidate.ColorHint, candidate.SignX, candidate.SignY, candidate.SignZ, candidate.SignFacing,
            candidate.SignText, candidate.StandHeadColor, candidate.StandName, candidate.Evidence);
        var bytes = Encoding.UTF8.GetBytes(line + "\n");
        digest.TransformBlock(bytes, 0, bytes.Length, null, 0);
    }
}

digest.TransformFinalBlock([], 0, 0);
Console.WriteLine($"maps {maps.Count}, candidates {total}, digest {Convert.ToHexString(digest.Hash!)}");
