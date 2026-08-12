#:project ../../src/PgmStudio.Minecraft/PgmStudio.Minecraft.csproj
// Vertical probe: what actually stands in a named box, course by course.
using PgmStudio.Minecraft;

var regionAt = Array.IndexOf(args, "--region");
var regionDir = regionAt >= 0 ? args[regionAt + 1]
    : "tools/tree-corpus/tree-showcase/region";
var box = args.Where(a => !a.StartsWith("--")).ToArray();
if (regionAt >= 0) box = box.Where(a => a != regionDir).ToArray();
int minX = int.Parse(box[0]), maxX = int.Parse(box[1]);
int minZ = int.Parse(box[2]), maxZ = int.Parse(box[3]);
int maxY = box.Length > 4 ? int.Parse(box[4]) : 6;

var world = new Dictionary<(int X, int Y, int Z), (int Id, int Data)>();
foreach (var chunk in Directory.GetFiles(regionDir, "*.mca").SelectMany(AnvilRegion.ReadChunks))
{
    if (chunk.ChunkX * 16 > maxX + 2 || chunk.ChunkX * 16 + 15 < minX - 2) continue;
    if (chunk.ChunkZ * 16 > maxZ + 2 || chunk.ChunkZ * 16 + 15 < minZ - 2) continue;
    var ids = new ushort[256 * 256];
    var data = new byte[256 * 256];
    foreach (var section in AnvilRegion.Sections(chunk))
    {
        var yStart = section.SectionY * 16;
        if (yStart is < 0 or >= 256) continue;
        Array.Copy(section.Ids, 0, ids, yStart * 256, 4096);
        Array.Copy(section.Data, 0, data, yStart * 256, 4096);
    }
    for (var lz = 0; lz < 16; lz++)
        for (var lx = 0; lx < 16; lx++)
        {
            var col = (lz << 4) | lx;
            for (var y = 0; y < 256; y++)
                if (ids[(y << 8) | col] != 0)
                    world[(chunk.ChunkX * 16 + lx, y, chunk.ChunkZ * 16 + lz)] =
                        (ids[(y << 8) | col], data[(y << 8) | col]);
        }
}

for (var y = 0; y <= maxY; y++)
{
    var course = world.Where(e => e.Key.Y == y && e.Key.X >= minX && e.Key.X <= maxX
                                  && e.Key.Z >= minZ && e.Key.Z <= maxZ).ToList();
    Console.WriteLine($"\n── y={y}  ({course.Count} blocks: " + string.Join(", ", course
        .GroupBy(e => $"{BlockPalette.Name(e.Value.Id, -1)}:{e.Value.Data}")
        .OrderByDescending(g => g.Count()).Select(g => $"{g.Key} x{g.Count()}")) + ")");
    for (var z = minZ; z <= maxZ; z++)
    {
        var line = "";
        for (var x = minX; x <= maxX; x++)
            line += world.TryGetValue((x, y, z), out var block)
                ? block.Id switch
                {
                    17 or 162 => "L", 18 or 161 => "%", 35 => "W", 5 => "=", 126 => "-",
                    2 => ".", 3 => "d", _ => "?"
                }
                : " ";
        Console.WriteLine($"  z{z,5} |{line}|");
    }
}
