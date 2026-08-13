using PgmStudio.Domain;
using PgmStudio.Geom.Render;

namespace PgmStudio.Minecraft.Render;

/// <summary>
/// A world's surface as a top-down PNG: one pixel block per column, coloured by the same
/// <see cref="BlockPalette"/> table the studio's layer view and terrain picker read, so the image and the
/// editor agree on what a block looks like.
///
/// <para><b>Relief comes from the neighbour, not from the height.</b> Colouring a column by its absolute
/// height would repaint terrain in a ramp that has nothing to do with the blocks, and a flat plateau would
/// read as one dead field. Each column is instead lightened or darkened against the column one step north,
/// which is what the game's own map item does: the shading then marks every edge — a wall, a trench, the lip
/// of a plateau — while a flat region keeps its material colour exactly. A gentle absolute-height term is
/// added on top, small enough that it separates a valley floor from a hilltop without tinting either.</para>
///
/// <para><b>Water is drawn as depth, not as a surface.</b> The extractor answers the topmost non-air block,
/// which over an ocean is the water surface and hides the bed entirely. A column whose top is water is
/// therefore traced down to the first non-liquid block and drawn as that block's colour dimmed by how much
/// water stands over it, so a shallow channel and a deep pit read differently and the shoreline shows.</para>
///
/// <para>The optional overlay draws what <c>map.xml</c> declares on top of the terrain — objective
/// destroyables, spawns, and the boxes named by apply rules — because the question a render is usually asked
/// is whether the geometry sits where the XML says it does. Overlays take an already-parsed <see cref="MapXml"/>
/// rather than a file path: parsing the document is <c>map.xml</c> semantics, which stays in the caller
/// (a text file on disk for the <c>RoundTrip</c> harness, the string a build just produced for
/// <c>tools/mapgen</c>), so this renderer needs nothing beyond <see cref="Domain"/> and its own world reads.</para>
/// </summary>
public static class TopDownRender
{
    /// <summary>How dark the deepest water gets over its bed.</summary>
    private const double DeepWaterKeep = 0.35;

    /// <summary>Depth in blocks at which water stops getting darker.</summary>
    private const int FullDepth = 12;

    private sealed record Column(int SurfaceY, int PackedRgb);

    public sealed record Result(byte[] Pixels, int BlocksWide, int BlocksHigh, int ColumnCount,
        int LowestY, int HighestY, int OverlayCount);

    /// <summary>Reads a built region directory from disk and renders it — the <c>RoundTrip</c> CLI's entry
    /// point.</summary>
    public static int Run(string regionDir, string outPng, MapXml? map, int scale, int? yMax)
    {
        if (!Directory.Exists(regionDir)) { Console.Error.WriteLine($"no region dir: {regionDir}"); return 1; }
        var chunks = Directory.GetFiles(regionDir, "*.mca").SelectMany(AnvilRegion.ReadChunks).ToList();
        if (chunks.Count == 0) { Console.Error.WriteLine($"no chunks in {regionDir}"); return 1; }
        return Emit(chunks, outPng, map, scale, yMax,
            Path.GetFileName(Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(regionDir)) ?? regionDir));
    }

    /// <summary>Renders a world still held in memory — no round trip through a region file. This is what lets
    /// a generator emit its own top-down the moment it finishes building, over the exact world it just made
    /// rather than one re-read off disk.</summary>
    public static int Run(VoxelWorld world, string outPng, MapXml? map, int scale, int? yMax, string name)
    {
        var chunks = AnvilRegion.FromWorld(world).ToList();
        if (chunks.Count == 0) { Console.Error.WriteLine("world has no chunks"); return 1; }
        return Emit(chunks, outPng, map, scale, yMax, name);
    }

    private static int Emit(List<AnvilRegion.Chunk> chunks, string outPng, MapXml? map, int scale, int? yMax, string name)
    {
        var result = Render(chunks, map, yMax);
        if (result is null) { Console.Error.WriteLine("no non-air columns"); return 1; }

        var scaled = Raster.Upscale(result.Pixels, result.BlocksWide, result.BlocksHigh, scale);
        PngWriter.Write(outPng, result.BlocksWide * scale, result.BlocksHigh * scale, scaled);

        Console.WriteLine($"topdown {name}: {result.ColumnCount} columns over {result.BlocksWide}x{result.BlocksHigh} blocks, " +
            $"surface y {result.LowestY}..{result.HighestY}");
        if (result.OverlayCount > 0) Console.WriteLine($"  overlay: {result.OverlayCount} box(es)");
        Console.WriteLine($"  wrote {outPng} ({result.BlocksWide * scale}x{result.BlocksHigh * scale} px, {scale} px/block)");
        return 0;
    }

    /// <summary>The pure render: columns in, an RGB pixel buffer (one pixel per block, unscaled) out. No file
    /// or console I/O, so a caller that only wants the bytes — an HTTP endpoint, a batch of stage images — pays
    /// nothing beyond the pixels themselves.</summary>
    public static Result? Render(IEnumerable<AnvilRegion.Chunk> chunks, MapXml? map, int? yMax)
    {
        var columns = ReadColumns(chunks.ToList(), yMax);
        if (columns.Count == 0) return null;

        int minX = columns.Keys.Min(cell => cell.X), maxX = columns.Keys.Max(cell => cell.X);
        int minZ = columns.Keys.Min(cell => cell.Z), maxZ = columns.Keys.Max(cell => cell.Z);
        int blocksWide = maxX - minX + 1, blocksHigh = maxZ - minZ + 1;

        var heights = columns.Values.Select(column => column.SurfaceY).ToList();
        int lowest = heights.Min(), highest = heights.Max();

        var pixels = Paint(columns, minX, minZ, blocksWide, blocksHigh, lowest, highest);

        var overlays = map is null ? [] : Overlays(map);
        foreach (var overlay in overlays) DrawBox(pixels, blocksWide, blocksHigh, minX, minZ, overlay);

        return new Result(pixels, blocksWide, blocksHigh, columns.Count, lowest, highest, overlays.Count);
    }

    /// <summary>Surface colour + height per column, with water resolved to its bed.</summary>
    private static Dictionary<(int X, int Z), Column> ReadColumns(List<AnvilRegion.Chunk> chunks, int? yMax)
    {
        var surface = LayerExtractors.Surface(chunks, maxBuildHeight: yMax).ToList();
        var byCell = new Dictionary<(int X, int Z), Column>(surface.Count);
        foreach (var block in surface)
            byCell[(block.WorldX, block.WorldZ)] = new Column(block.WorldY, BlockPalette.PackedRgb(block.BlockId, block.BlockData));

        // Only the water columns need the second, deeper read, so the bed lookup is built for those alone.
        var waterCells = surface
            .Where(block => block.BlockId is Blocks.Water or Blocks.StationaryWater)
            .Select(block => ((block.WorldX, block.WorldZ), block.WorldY))
            .ToDictionary(entry => entry.Item1, entry => entry.WorldY);
        if (waterCells.Count == 0) return byCell;

        foreach (var (cell, bed, depth) in Beds(chunks, waterCells))
            byCell[cell] = new Column(bed.Y, Raster.Scale(BlockPalette.PackedRgb(bed.BlockId, bed.BlockData),
                1 - (1 - DeepWaterKeep) * Math.Min(1.0, depth / (double)FullDepth)));
        return byCell;
    }

    /// <summary>For each flooded column, the first non-liquid block under the water surface and how many
    /// blocks of water stand over it. A column that is liquid all the way down yields nothing and keeps the
    /// water's own colour.</summary>
    private static IEnumerable<((int X, int Z) Cell, (int Y, int BlockId, int BlockData) Bed, int Depth)> Beds(
        List<AnvilRegion.Chunk> chunks, Dictionary<(int X, int Z), int> waterTops)
    {
        foreach (var chunk in chunks)
        {
            var found = new Dictionary<(int X, int Z), (int Y, int BlockId, int BlockData)>();
            foreach (var block in AnvilRegion.Blocks(chunk))
            {
                var cell = (block.X, block.Z);
                if (!waterTops.TryGetValue(cell, out var top) || block.Y > top) continue;
                if (block.Id is 0 or Blocks.Water or Blocks.StationaryWater) continue;
                // Blocks arrive in no guaranteed vertical order, so keep the highest candidate seen.
                if (found.TryGetValue(cell, out var best) && best.Y >= block.Y) continue;
                found[cell] = (block.Y, block.Id, block.Data);
            }
            foreach (var (cell, bed) in found)
                yield return (cell, bed, waterTops[cell] - bed.Y);
        }
    }

    /// <summary>Terrain colours with north-facing relief and a light absolute-height term.</summary>
    private static byte[] Paint(Dictionary<(int X, int Z), Column> columns, int minX, int minZ,
                                int blocksWide, int blocksHigh, int lowest, int highest)
    {
        var pixels = new byte[blocksWide * blocksHigh * 3];
        var span = Math.Max(1, highest - lowest);

        for (var row = 0; row < blocksHigh; row++)
            for (var col = 0; col < blocksWide; col++)
            {
                if (!columns.TryGetValue((minX + col, minZ + row), out var column))
                {
                    // Void: a flat near-black, so a hole in the world is obviously not terrain.
                    Raster.Set(pixels, blocksWide, col, row, 0x0E0E0E);
                    continue;
                }

                var keep = 1.0;
                if (columns.TryGetValue((minX + col, minZ + row - 1), out var north))
                {
                    var step = column.SurfaceY - north.SurfaceY;
                    // A tall step is an edge worth more contrast than a one-block lip, but the extra
                    // saturates quickly or a cliff face washes out to white.
                    keep = 1.0 + Math.Sign(step) * (0.16 + 0.05 * Math.Min(3, Math.Abs(step) - 1));
                }
                keep *= 0.88 + 0.24 * ((column.SurfaceY - lowest) / (double)span);

                Raster.Set(pixels, blocksWide, col, row, Raster.Scale(column.PackedRgb, keep));
            }
        return pixels;
    }

    public sealed record Overlay(BlockBox Box, int PackedRgb, bool Filled, string Label);

    /// <summary>What the map declares, as boxes to outline: objective destroyables and cores filled in their
    /// owner's colour, spawns as a small filled marker, and every apply-rule region outlined. A region whose
    /// shape does not reduce to boxes contributes nothing, which is <see cref="RegionBoxes"/>'s contract.</summary>
    public static List<Overlay> Overlays(MapXml map)
    {
        var overlays = new List<Overlay>();

        var teamColors = map.Teams.ToDictionary(team => team.Id, team => TeamRgb(team.Color));
        int ColorOf(string teamId) => teamColors.TryGetValue(teamId, out var rgb) ? rgb : 0xFFFFFF;

        foreach (var destroyable in map.Destroyables.Where(d => d.IsObjective))
            foreach (var box in RegionBoxes.Of(map.Regions, destroyable.RegionId))
                overlays.Add(new Overlay(box, ColorOf(destroyable.Owner), true, $"destroyable {destroyable.Name}"));

        foreach (var core in map.Cores)
            foreach (var box in RegionBoxes.Of(map.Regions, core.RegionId))
                overlays.Add(new Overlay(box, ColorOf(core.Owner), true, "core"));

        foreach (var spawn in map.Spawns.Where(spawn => spawn.Region is not null))
            foreach (var box in RegionBoxes.Of(map.Regions, spawn.Region!))
                overlays.Add(new Overlay(Grow(box, 1), ColorOf(spawn.Team), true, $"spawn {spawn.Team}"));

        // A wool room reduces to boxes like any other region; a wool with no declared room still marks the
        // block its own goal is read at, grown by one so a single point is visible at typical render scales.
        foreach (var wool in map.Wools)
        {
            if (wool.WoolRoomRegion is { Length: > 0 } roomRegion)
                foreach (var box in RegionBoxes.Of(map.Regions, roomRegion))
                    overlays.Add(new Overlay(box, WoolRgb(wool.Color), true, $"wool {wool.Color}"));
            else
                overlays.Add(new Overlay(Grow(PointBox(wool.Location), 1), WoolRgb(wool.Color), true, $"wool {wool.Color}"));
        }

        foreach (var rule in map.ApplyRules)
            foreach (var box in RegionBoxes.Of(map.Regions, rule.RegionId))
                overlays.Add(new Overlay(box, 0xF0F0F0, false, $"apply {rule.RegionId}"));

        return overlays;
    }

    private static BlockBox Grow(BlockBox box, int by) =>
        new(box.MinX - by, box.MinY, box.MinZ - by, box.MaxX + by, box.MaxY, box.MaxZ + by);

    private static BlockBox PointBox(Vec3 point)
    {
        int x = (int)Math.Floor(point.X), y = (int)Math.Floor(point.Y), z = (int)Math.Floor(point.Z);
        return new BlockBox(x, y, z, x, y, z);
    }

    /// <summary>Common wool dye names → a pixel colour. Unknown names fall back to amber rather than to a
    /// guess.</summary>
    private static int WoolRgb(string? color) => (color ?? "").ToLowerInvariant() switch
    {
        "red" => 0xef4444, "blue" => 0x3b82f6, "green" or "lime" => 0x22c55e,
        "yellow" => 0xeab308, "orange" => 0xf97316, "purple" or "magenta" => 0xa855f7,
        "cyan" or "aqua" => 0x06b6d4, "pink" => 0xec4899, "white" => 0xf8fafc,
        "black" => 0x334155, "brown" => 0x92400e,
        _ => 0xfbbf24,
    };

    /// <summary>A PGM team colour name as a pixel colour. Unknown names fall back to white rather than to a
    /// guess, so a mis-typed colour is visible as one.</summary>
    private static int TeamRgb(string color) => color.Trim().ToLowerInvariant() switch
    {
        "dark red" => 0xAA0000,
        "red" => 0xFF5555,
        "blue" => 0x5555FF,
        "dark blue" => 0x0000AA,
        "green" => 0x55FF55,
        "dark green" => 0x00AA00,
        "yellow" => 0xFFFF55,
        "gold" => 0xFFAA00,
        "aqua" => 0x55FFFF,
        "dark aqua" => 0x00AAAA,
        "purple" or "light purple" => 0xFF55FF,
        "dark purple" => 0xAA00AA,
        "gray" or "grey" => 0xAAAAAA,
        "dark gray" or "dark grey" => 0x555555,
        "black" => 0x000000,
        "white" => 0xFFFFFF,
        _ => 0xFFFFFF,
    };

    /// <summary>An overlay box onto the pixel buffer — a filled tint keeps some of the terrain under it so
    /// the marker reads as a highlight rather than as a hole, an outline touches only its border cells.</summary>
    private static void DrawBox(byte[] pixels, int blocksWide, int blocksHigh, int minX, int minZ, Overlay overlay)
    {
        int left = overlay.Box.MinX - minX, right = overlay.Box.MaxX - minX;
        int top = overlay.Box.MinZ - minZ, bottom = overlay.Box.MaxZ - minZ;

        for (var row = Math.Max(0, top); row <= Math.Min(blocksHigh - 1, bottom); row++)
            for (var col = Math.Max(0, left); col <= Math.Min(blocksWide - 1, right); col++)
            {
                var onBorder = row == top || row == bottom || col == left || col == right;
                if (!overlay.Filled && !onBorder) continue;
                Raster.Over(pixels, blocksWide, col, row, overlay.PackedRgb, overlay.Filled ? (onBorder ? 0.85 : 0.55) : 0.9);
            }
    }
}
