#:project ../../src/PgmStudio.Minecraft/PgmStudio.Minecraft.csproj
// world-marker-readback: read an exported world back and report the build-region marker (ST5) actually in it.
// Decodes every region file with the Anvil reader and lists the redstone-wire blocks at y=1 as straight runs,
// which is the export-side check that the marker survived the whole chain rather than only the stamper's unit.
// Usage: dotnet run tools/deriver/world-marker-readback.cs <worldFolder>
using PgmStudio.Minecraft;

if (args.Length == 0)
{
    Console.WriteLine("usage: dotnet run tools/deriver/world-marker-readback.cs <worldFolder>");
    return;
}

var regionDir = Path.Combine(args[0], "region");
var wire = new HashSet<(int X, int Z)>();
var levels = new SortedDictionary<int, int>();

foreach (var mca in Directory.EnumerateFiles(regionDir, "*.mca").Order())
foreach (var chunk in AnvilRegion.ReadChunks(mca))
foreach (var block in AnvilRegion.Blocks(chunk))
{
    if (block.Id != Blocks.RedstoneWire && block.Id != Blocks.RedstoneTorch) continue;
    levels[block.Y] = levels.GetValueOrDefault(block.Y) + 1;
    if (block.Id == Blocks.RedstoneWire && block.Y == BuildMarkerStamper.MarkerY) wire.Add((block.X, block.Z));
}

Console.WriteLine($"redstone by height: {string.Join(", ", levels.Select(l => $"y={l.Key} ×{l.Value}"))}");
Console.WriteLine($"marker wire at y={BuildMarkerStamper.MarkerY}: {wire.Count} blocks");

foreach (var x in wire.Select(w => w.X).Distinct().Order())
    Report("column", $"x={x}", wire.Where(w => w.X == x).Select(w => w.Z), "z");
foreach (var z in wire.Select(w => w.Z).Distinct().Order())
    Report("row   ", $"z={z}", wire.Where(w => w.Z == z).Select(w => w.X), "x");

// Maximal straight runs along one line, so a ring reads as its four sides rather than as a block count.
static void Report(string kind, string at, IEnumerable<int> along, string axis)
{
    var sorted = along.Order().ToList();
    for (var start = 0; start < sorted.Count;)
    {
        var end = start;
        while (end + 1 < sorted.Count && sorted[end + 1] == sorted[end] + 1) end++;
        if (end > start)
            Console.WriteLine($"  {kind} {at,-8} {end - start + 1,3} blocks, {axis} {sorted[start]}..{sorted[end]}");
        start = end + 1;
    }
}
