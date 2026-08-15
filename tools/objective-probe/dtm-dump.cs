#:project ../../src/PgmStudio.Minecraft/PgmStudio.Minecraft.csproj
#:project ../../src/PgmStudio.Pgm/PgmStudio.Pgm.csproj

// A full dump of every declared destroyable and its NEIGHBOURHOOD, so the shape of the thing can be reasoned
// about from data rather than from prose.
//
// For each declared destroyable: resolve its region, take the blocks matching its material as the structure,
// then read the world 10 blocks outward on every horizontal axis, 10 up, and ALL THE WAY DOWN TO y=0. The
// downward reach is the point — "destroyables float" is a claim about what is under them, and a fixed-height
// window cannot see the difference between resting on a pedestal and hovering over the void.
//
// Nothing is dropped for being large. A 21,057-block structure is a real destroyable and its row says so.
//
// Emits one TSV row per structure to stdout; summary to stderr.

using PgmStudio.Domain;
using PgmStudio.Minecraft;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Pgm;

var roots = Directory.GetDirectories("/media/sf_repos/CommunityMaps")
    .Concat(Directory.Exists("/workspace/overcastcommunity/publicmaps")
        ? Directory.GetDirectories("/workspace/overcastcommunity/publicmaps") : [])
    .ToList();
var faces = new (int X, int Y, int Z)[] { (1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1) };
const int Reach = 10;

Console.WriteLine(string.Join('\t',
    "slug", "material", "size", "w", "h", "d", "fill_pct",
    "air_face_pct", "solid_face_pct", "distinct_touching",
    "support_pct",        // % of footprint columns whose block directly below is solid
    "drop_min", "drop_med", "drop_max",   // air blocks below each footprint column before the next solid (or y=0)
    "void_pct",           // % of footprint columns with nothing at all beneath, down to y=0
    "min_y", "surface_y", // structure floor, and the terrain height nearby
    "same_mat_within_10", "clear_pct", "top_open", "region_slack"));

int structures = 0, maps = 0;

foreach (var mapDir in roots.SelectMany(Directory.GetDirectories).OrderBy(d => d, StringComparer.Ordinal))
{
    var xmlPath = Path.Combine(mapDir, "map.xml");
    var regionDir = Path.Combine(mapDir, "region");
    if (!File.Exists(xmlPath) || !Directory.Exists(regionDir)) continue;

    MapXml map;
    try { map = MapParser.Parse(xmlPath); }
    catch (UnsupportedMapException) { continue; }
    var objectives = map.Destroyables.Where(d => d.IsObjective).ToList();
    if (objectives.Count == 0) continue;

    List<AnvilRegion.Chunk> chunks;
    try { chunks = Directory.GetFiles(regionDir, "*.mca").SelectMany(AnvilRegion.ReadChunks).ToList(); }
    catch { continue; }
    if (chunks.Count == 0) continue;

    var world = new Dictionary<(int X, int Y, int Z), int>();
    foreach (var chunk in chunks)
        foreach (var block in AnvilRegion.Blocks(chunk))
            world[(block.X, block.Y, block.Z)] = block.Id;
    maps++;
    var slug = Path.GetFileName(mapDir);

    foreach (var destroyable in objectives)
    {
        var ids = MaterialIds.Resolve(destroyable.Materials);
        if (ids.Count == 0) continue;
        var boxes = RegionBoxes.Of(map.Regions, destroyable.RegionId);
        if (boxes.Count == 0) continue;

        var cells = new HashSet<(int X, int Y, int Z)>();
        long regionVolume = 0;
        foreach (var box in boxes)
        {
            regionVolume += (long)box.Width * box.Height * box.Depth;
            for (var x = box.MinX; x <= box.MaxX; x++)
                for (var y = box.MinY; y <= box.MaxY; y++)
                    for (var z = box.MinZ; z <= box.MaxZ; z++)
                        if (ids.Contains(world.GetValueOrDefault((x, y, z)))) cells.Add((x, y, z));
        }
        if (cells.Count == 0) continue;
        structures++;

        var materialName = ids.Count == 1
            ? ids.Single() switch { 49 => "obsidian", 133 => "emerald", 41 => "gold", 121 => "endstone", var other => other.ToString() }
            : "mixed";

        int minX = cells.Min(c => c.X), maxX = cells.Max(c => c.X);
        int minY = cells.Min(c => c.Y), maxY = cells.Max(c => c.Y);
        int minZ = cells.Min(c => c.Z), maxZ = cells.Max(c => c.Z);
        int width = maxX - minX + 1, height = maxY - minY + 1, depth = maxZ - minZ + 1;
        var fill = (int)Math.Round(100.0 * cells.Count / ((long)width * height * depth));

        // face contacts + what materials the structure actually touches
        int airFaces = 0, solidFaces = 0;
        var touching = new HashSet<int>();
        foreach (var cell in cells)
            foreach (var face in faces)
            {
                var next = (cell.X + face.X, cell.Y + face.Y, cell.Z + face.Z);
                if (cells.Contains(next)) continue;
                var id = world.GetValueOrDefault(next);
                if (id == 0) airFaces++;
                else { solidFaces++; touching.Add(id); }
            }
        var totalFaces = airFaces + solidFaces;

        // WHAT IS UNDERNEATH — the whole point. Per footprint column, walk down to y=0.
        var columns = cells.GroupBy(cell => (cell.X, cell.Z)).ToList();
        int supported = 0, voidColumns = 0;
        var drops = new List<int>();
        foreach (var column in columns)
        {
            var lowest = column.Min(cell => cell.Y);
            var air = 0;
            var hitSolid = false;
            for (var y = lowest - 1; y >= 0; y--)
            {
                if (world.ContainsKey((column.Key.X, y, column.Key.Z))) { hitSolid = true; break; }
                air++;
            }
            if (air == 0) supported++;
            if (!hitSolid) voidColumns++;
            drops.Add(air);
        }
        drops.Sort();

        // the terrain height near the structure, from the neighbourhood ring rather than under it
        var surfaceHeights = new List<int>();
        for (var x = minX - Reach; x <= maxX + Reach; x++)
            for (var z = minZ - Reach; z <= maxZ + Reach; z++)
            {
                if (x >= minX && x <= maxX && z >= minZ && z <= maxZ) continue;
                for (var y = maxY + Reach; y >= 0; y--)
                    if (world.ContainsKey((x, y, z))) { surfaceHeights.Add(y); break; }
            }
        surfaceHeights.Sort();
        var surfaceY = surfaceHeights.Count == 0 ? -1 : surfaceHeights[surfaceHeights.Count / 2];

        // is the same material used again nearby, and how clear is the neighbourhood?
        int sameNearby = 0, neighbourhood = 0, clear = 0;
        for (var x = minX - Reach; x <= maxX + Reach; x++)
            for (var y = Math.Max(0, minY - Reach); y <= maxY + Reach; y++)
                for (var z = minZ - Reach; z <= maxZ + Reach; z++)
                {
                    if (cells.Contains((x, y, z))) continue;
                    neighbourhood++;
                    var id = world.GetValueOrDefault((x, y, z));
                    if (id == 0) clear++;
                    else if (ids.Contains(id)) sameNearby++;
                }

        var topOpen = cells.Where(cell => cell.Y == maxY)
                           .All(cell => !world.ContainsKey((cell.X, cell.Y + 1, cell.Z)));
        var slack = (int)Math.Round(100.0 * cells.Count / Math.Max(1, regionVolume));

        Console.WriteLine(string.Join('\t',
            slug, materialName, cells.Count, width, height, depth, fill,
            totalFaces == 0 ? 0 : 100 * airFaces / totalFaces,
            totalFaces == 0 ? 0 : 100 * solidFaces / totalFaces,
            touching.Count,
            columns.Count == 0 ? 0 : 100 * supported / columns.Count,
            drops.Count == 0 ? 0 : drops[0], drops.Count == 0 ? 0 : drops[drops.Count / 2], drops.Count == 0 ? 0 : drops[^1],
            columns.Count == 0 ? 0 : 100 * voidColumns / columns.Count,
            minY, surfaceY,
            sameNearby,
            neighbourhood == 0 ? 0 : 100 * clear / neighbourhood,
            topOpen ? 1 : 0, slack));
    }
}

Console.Error.WriteLine($"maps {maps}, structures dumped {structures}");
