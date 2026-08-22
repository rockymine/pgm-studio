using PgmStudio.Domain;
using PgmStudio.Geom.Render;
using PgmStudio.Geom;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Palette;

namespace PgmStudio.Minecraft.Render;

/// <summary>Which colours a top-down reads by. <see cref="Category"/> — the default — sorts every column into
/// <see cref="RenderCategory"/> and paints the five-hue false-colour scheme; <see cref="Material"/> keeps the
/// old per-block <see cref="BlockPalette"/> reading, for the rarer question of exactly which material was
/// placed rather than what kind of thing is standing there. Legibility beats realism as the default
/// (<c>docs/tools/capabilities.md</c>'s renderer section); a caller checking a theme's actual paint reaches
/// for <see cref="Material"/> deliberately rather than getting it by default.</summary>
public enum TopDownColorMode { Category, Material }

/// <summary>Which slice of the map a top-down draws. <see cref="Combined"/> is the whole finished map, every
/// category at once — the view that already existed. The rest isolate one question at a time: a category
/// other than the one asked for reads as a flat, dim context tone rather than its own colour, so the requested
/// layer is the only thing that stands out. <see cref="Objectives"/> carries no block categories at all — its
/// terrain is uniformly the context tone, and only the <c>map.xml</c> overlay (goals, spawns, apply-rule
/// boxes) is drawn on top, because the question it answers is purely "where do the declared goals sit",
/// which the finished map's own colours only get in the way of.</summary>
public enum TopDownLayer { Combined, Ground, Structure, Foliage, Objectives }

/// <summary>
/// A world's surface as a top-down PNG: one pixel block per column. The default reading is a diagram, not a
/// photograph — every column is sorted into <see cref="RenderCategory"/> and painted that category's
/// false-coloured hue (<see cref="RenderCategories"/>), because a render meant to be reasoned from has to
/// separate foliage from surface from structure at a glance, and the game's own materials do not: stone,
/// stone brick, cobblestone, andesite and gravel are all some shade of grey, so painting each its own true
/// colour paints one grey field. <see cref="TopDownColorMode.Material"/> keeps the old per-block reading for
/// the caller checking a theme's actual paint rather than the map's shape.
///
/// <para><b>Relief still comes from the neighbour, not from the height.</b> Colouring a column by its
/// absolute height would repaint terrain in a ramp that has nothing to do with the blocks, and a flat plateau
/// would read as one dead field. Each column is lightened or darkened against the column one step north — the
/// same read the game's own map item uses — which marks every edge (a wall, a trench, a plateau's lip) while
/// a flat region keeps its category colour exactly; a gentle absolute-height term separates a valley floor
/// from a hilltop on top of that. Shading a category's own hue this way is not the thing the false-colour
/// scheme forbids — the rule is that <em>different</em> categories must separate by more than shade, not that
/// one category may carry no shade at all.</para>
///
/// <para><b>A layer is worth seeing alone.</b> The finished map draws every category and the declared goals
/// at once, which is a search rather than a look when the question at hand is just "where is the foliage" or
/// "where do the goals sit" — <see cref="TopDownLayer"/> isolates one question per image, each still keyed by
/// its own legend (<see cref="Legend"/>) baked onto the PNG a caller actually gets, so the picture never needs
/// a caption to be read correctly.</para>
///
/// <para>The optional overlay draws what <c>map.xml</c> declares on top of the terrain — objective
/// destroyables, spawns, and the boxes named by apply rules — because the question a render is usually asked
/// is whether the geometry sits where the XML says it does. Overlays take an already-parsed <see cref="MapXml"/>
/// rather than a file path: parsing the document is <c>map.xml</c> semantics, which stays in the caller —
/// a text file on disk for the <c>RoundTrip</c> harness, the string an export just composed for the API — so
/// this renderer needs nothing beyond <see cref="Domain"/> and its own world reads.</para>
/// </summary>
public static class TopDownRender
{
    /// <summary>How dark the deepest water gets — a shade of its own category hue rather than a switch to the
    /// bed's material, so every flooded column still reads as water at a glance and depth is legible as a
    /// second, subordinate reading on top of it.</summary>
    private const double DeepWaterKeep = 0.35;

    /// <summary>Depth in blocks at which water stops getting darker.</summary>
    private const int FullDepth = 12;

    /// <summary>A column whose category is not the one <see cref="TopDownLayer"/> asked for — present, but
    /// deliberately not the void colour, so "nothing recorded here" and "something recorded, just not this
    /// layer's question" stay two different findings. The isolated foliage layer's <b>point</b> mode reuses it
    /// as the backdrop every tree circle is drawn over, for the same reason: it is terrain, just not the thing
    /// being asked about.</summary>
    private const int ContextRgb = 0x2A2C33;

    /// <summary>A tree's own anchor, drawn over its crown circle so the trunk stays a single countable dot even
    /// where two crowns overlap. White reads against every category hue here, foliage's violet included.</summary>
    private const int TreeTrunkRgb = 0xF5F5F0;

    /// <summary>How strongly one crown circle tints the backdrop — soft enough that overlapping crowns build up
    /// visibly denser rather than flattening into one solid mass, which is the reading this mode exists to
    /// replace.</summary>
    private const double TreeCrownWeight = 0.55;

    private sealed record Column(int SurfaceY, RenderCategory Category, int BlockId, int BlockData, int WaterDepth);

    public sealed record Result(byte[] Pixels, int BlocksWide, int BlocksHigh, int ColumnCount,
        int LowestY, int HighestY, int OverlayCount, int TreePointCount = 0);

    /// <summary>Reads a built region directory from disk and renders it — the <c>RoundTrip</c> CLI's entry
    /// point. Picks up <see cref="WorldProvenanceFile"/>'s sidecar automatically when the region carries one;
    /// a region with none (a scanned world, or one built before this recording existed) falls back to the
    /// material estimate, which is <see cref="TopDownColorMode.Material"/>'s reading regardless.
    ///
    /// <para><paramref name="treePoints"/> is the isolated foliage layer's point-and-radius reading
    /// (<c>DressingScope.TreeFootprints</c>, docs/world-export/decoration.md §6) — every authored tree's anchor
    /// and measured crown radius. A caller reading a bare region directory has no dressing document to read it
    /// from, so this is null by construction here and the layer falls back to painting the leaf/log mass, the
    /// reading every layer already had. A caller that <em>does</em> hold the document (a studio export in
    /// memory) passes it through the <c>VoxelWorld</c> overload instead.</para></summary>
    public static int Run(string regionDir, string outPng, MapXml? map, int scale, int? yMax,
        TopDownColorMode colorMode = TopDownColorMode.Category, TopDownLayer layer = TopDownLayer.Combined,
        IReadOnlyList<(int X, int Z, double Radius)>? treePoints = null)
    {
        if (!Directory.Exists(regionDir)) { Console.Error.WriteLine($"no region dir: {regionDir}"); return 1; }
        var chunks = Directory.GetFiles(regionDir, "*.mca").SelectMany(AnvilRegion.ReadChunks).ToList();
        if (chunks.Count == 0) { Console.Error.WriteLine($"no chunks in {regionDir}"); return 1; }
        return Emit(chunks, outPng, map, scale, yMax, colorMode, layer, WorldProvenanceFile.TryRead(regionDir),
            Path.GetFileName(Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(regionDir)) ?? regionDir),
            treePoints);
    }

    /// <summary>Renders a world still held in memory — no round trip through a region file. This is what lets
    /// a generator emit its own top-down the moment it finishes building, over the exact world it just made
    /// rather than one re-read off disk. <paramref name="provenance"/> is the record the build itself kept
    /// (<see cref="BuiltWorld.Provenance"/> via <c>PgmStudio.Export</c>); null reads by the material estimate,
    /// exactly as a caller with no world build behind it — a corpus scan — already does. <paramref
    /// name="treePoints"/> is the isolated foliage layer's point-and-radius reading, from the same build's
    /// dressing document (<c>DressingScope.TreeFootprints</c>); null falls back to the leaf/log mass, exactly
    /// as the disk-reading overload's caller with no document does.</summary>
    public static int Run(VoxelWorld world, string outPng, MapXml? map, int scale, int? yMax, string name,
        TopDownColorMode colorMode = TopDownColorMode.Category, TopDownLayer layer = TopDownLayer.Combined,
        WorldProvenance? provenance = null, IReadOnlyList<(int X, int Z, double Radius)>? treePoints = null)
    {
        var chunks = AnvilRegion.FromWorld(world).ToList();
        if (chunks.Count == 0) { Console.Error.WriteLine("world has no chunks"); return 1; }
        return Emit(chunks, outPng, map, scale, yMax, colorMode, layer, provenance, name, treePoints);
    }

    private static int Emit(List<AnvilRegion.Chunk> chunks, string outPng, MapXml? map, int scale, int? yMax,
        TopDownColorMode colorMode, TopDownLayer layer, WorldProvenance? provenance, string name,
        IReadOnlyList<(int X, int Z, double Radius)>? treePoints = null)
    {
        var result = Render(chunks, map, yMax, colorMode, layer, provenance, treePoints);
        if (result is null) { Console.Error.WriteLine("no non-air columns"); return 1; }

        // Whether the Ground/Structure split is a recorded fact or a material guess is the one thing a legend
        // swatch cannot say on its own (B133), so it is baked into the picture itself rather than left to a
        // caption an image reader never sees — the same reason the legend exists at all.
        var provenanceState = colorMode != TopDownColorMode.Category ? null
            : provenance is not null ? "STRUCTURE READING: RECORDED PROVENANCE"
            : "STRUCTURE READING: MATERIAL ESTIMATE (NO RECORDED PROVENANCE)";

        var scaled = Raster.Upscale(result.Pixels, result.BlocksWide, result.BlocksHigh, scale);
        var withLegend = Legend.AppendBelow(scaled, result.BlocksWide * scale, result.BlocksHigh * scale,
            LegendEntries(colorMode, layer, result.TreePointCount > 0), out var legendHeight,
            scaleLabel: $"SCALE: 1 BLOCK = {scale} PX - {result.BlocksWide} X {result.BlocksHigh} BLOCKS"
                       + (provenanceState is null ? "" : $"  -  {provenanceState}"));
        PngWriter.Write(outPng, result.BlocksWide * scale, legendHeight, withLegend);

        Console.WriteLine($"topdown {name} ({layer}/{colorMode}{(provenanceState is null ? "" : $", {provenanceState}")}): " +
            $"{result.ColumnCount} columns over {result.BlocksWide}x{result.BlocksHigh} blocks, " +
            $"surface y {result.LowestY}..{result.HighestY}");
        if (result.OverlayCount > 0) Console.WriteLine($"  overlay: {result.OverlayCount} box(es)");
        if (layer == TopDownLayer.Foliage)
            Console.WriteLine(result.TreePointCount > 0
                ? $"  {result.TreePointCount} tree(s), drawn as point + measured crown radius"
                : $"  no dressing document given — drawn as the leaf/log mass (no tree points to plot)");
        Console.WriteLine($"  wrote {outPng} ({result.BlocksWide * scale}x{legendHeight} px, {scale} px/block)");
        return 0;
    }

    /// <summary>The legend a caller's picture actually carries — one entry per colour <see cref="Paint"/> can
    /// place under the given mode/layer, so a swatch on the image always has a name beside it.</summary>
    private static List<Legend.Entry> LegendEntries(TopDownColorMode colorMode, TopDownLayer layer, bool treePoints = false)
    {
        if (colorMode == TopDownColorMode.Material)
            return [new Legend.Entry("REAL MATERIAL COLOUR", ContextRgb), new Legend.Entry("VOID", RenderCategories.VoidRgb)];

        if (layer == TopDownLayer.Combined)
            return
            [
                new Legend.Entry("GROUND", RenderCategories.GroundRgb),
                new Legend.Entry("STRUCTURE", RenderCategories.StructureRgb),
                new Legend.Entry("FOLIAGE", RenderCategories.FoliageRgb),
                new Legend.Entry("WATER", RenderCategories.WaterRgb),
                new Legend.Entry("VOID", RenderCategories.VoidRgb),
            ];

        if (layer == TopDownLayer.Objectives)
            return
            [
                new Legend.Entry("TERRAIN (CONTEXT)", ContextRgb),
                new Legend.Entry("GOAL/SPAWN - OWN TEAM COLOUR", 0xF5F5F0),
                new Legend.Entry("VOID", RenderCategories.VoidRgb),
            ];

        // The isolated foliage layer's point mode is a different picture from the other two isolated layers —
        // a tree's crown circle and its trunk dot rather than a highlighted material — so it carries its own
        // pair of swatches instead of the one-category-plus-context shape the rest share.
        if (layer == TopDownLayer.Foliage && treePoints)
            return
            [
                new Legend.Entry("TREE CROWN (MEASURED RADIUS)", RenderCategories.HighlightOf(RenderCategory.Foliage)),
                new Legend.Entry("TREE TRUNK (ONE PER PROP)", TreeTrunkRgb),
                new Legend.Entry("OTHER (CONTEXT)", ContextRgb),
                new Legend.Entry("VOID", RenderCategories.VoidRgb),
            ];

        var wanted = layer switch
        {
            TopDownLayer.Ground => RenderCategory.Ground,
            TopDownLayer.Structure => RenderCategory.Structure,
            TopDownLayer.Foliage => RenderCategory.Foliage,
            _ => RenderCategory.Ground,
        };
        return
        [
            new Legend.Entry(RenderCategories.NameOf(wanted).ToUpperInvariant(), RenderCategories.HighlightOf(wanted)),
            new Legend.Entry("OTHER (CONTEXT)", ContextRgb),
            new Legend.Entry("VOID", RenderCategories.VoidRgb),
        ];
    }

    /// <summary>The pure render: columns in, an RGB pixel buffer (one pixel per block, unscaled) out. No file
    /// or console I/O, no legend baked in — what a caller that only wants the categorised pixels (a test, an
    /// HTTP endpoint composing its own page) pays for. <see cref="Emit"/> is what appends the key.
    ///
    /// <para><paramref name="treePoints"/> switches the isolated foliage layer from painting the leaf/log mass
    /// to plotting each tree's own anchor and measured crown radius (docs/world-export/decoration.md §6) — a
    /// mode, not a second renderer: every other layer, and the combined view, are unaffected by it, because the
    /// mass is still the honest reading of what cover a player actually has. Ignored on every layer but
    /// <see cref="TopDownLayer.Foliage"/>.</para></summary>
    public static Result? Render(IEnumerable<AnvilRegion.Chunk> chunks, MapXml? map, int? yMax,
        TopDownColorMode colorMode = TopDownColorMode.Category, TopDownLayer layer = TopDownLayer.Combined,
        WorldProvenance? provenance = null, IReadOnlyList<(int X, int Z, double Radius)>? treePoints = null)
    {
        var columns = ReadColumns(chunks.ToList(), yMax, provenance);
        if (columns.Count == 0) return null;

        int minX = columns.Keys.Min(cell => cell.X), maxX = columns.Keys.Max(cell => cell.X);
        int minZ = columns.Keys.Min(cell => cell.Z), maxZ = columns.Keys.Max(cell => cell.Z);
        int blocksWide = maxX - minX + 1, blocksHigh = maxZ - minZ + 1;

        var heights = columns.Values.Select(column => column.SurfaceY).ToList();
        int lowest = heights.Min(), highest = heights.Max();

        var pointMode = layer == TopDownLayer.Foliage && treePoints is { Count: > 0 };
        var pixels = Paint(columns, minX, minZ, blocksWide, blocksHigh, lowest, highest, colorMode, layer, pointMode);
        if (pointMode) DrawTreePoints(pixels, blocksWide, blocksHigh, minX, minZ, treePoints!);

        var overlays = map is null ? [] : Overlays(map);
        foreach (var overlay in overlays) DrawBox(pixels, blocksWide, blocksHigh, minX, minZ, overlay);

        return new Result(pixels, blocksWide, blocksHigh, columns.Count, lowest, highest, overlays.Count,
            pointMode ? treePoints!.Count : 0);
    }

    /// <summary>Plots every tree as its own circle — filled to its measured crown radius, softly enough that
    /// overlapping crowns read as denser cover rather than one fused blob — with a solid one-block trunk dot on
    /// top so a cluster stays countable even where the circles merge. Clipped to the frame rather than to the
    /// map's own bounds: a crown seated near the edge is allowed to draw the part of itself that is still in
    /// frame.</summary>
    private static void DrawTreePoints(byte[] pixels, int blocksWide, int blocksHigh, int minX, int minZ,
        IReadOnlyList<(int X, int Z, double Radius)> treePoints)
    {
        var crownRgb = RenderCategories.HighlightOf(RenderCategory.Foliage);
        foreach (var (treeX, treeZ, radius) in treePoints)
        {
            var reach = (int)Math.Ceiling(Math.Max(0, radius));
            var squared = radius * radius;
            for (var dz = -reach; dz <= reach; dz++)
            for (var dx = -reach; dx <= reach; dx++)
            {
                if (dx * dx + dz * dz > squared) continue;
                var col = treeX + dx - minX;
                var row = treeZ + dz - minZ;
                if (col < 0 || col >= blocksWide || row < 0 || row >= blocksHigh) continue;
                Raster.Over(pixels, blocksWide, col, row, crownRgb, TreeCrownWeight);
            }
        }

        // Trunks drawn after every crown, so a tree standing under a neighbour's overhang still shows its own
        // point rather than disappearing into the shared tint.
        foreach (var (treeX, treeZ, _) in treePoints)
        {
            var col = treeX - minX;
            var row = treeZ - minZ;
            if (col < 0 || col >= blocksWide || row < 0 || row >= blocksHigh) continue;
            Raster.Set(pixels, blocksWide, col, row, TreeTrunkRgb);
        }
    }

    /// <summary>Surface category + height per column, with water resolved to its bed for the depth reading
    /// (the category stays <see cref="RenderCategory.Water"/> regardless of what the bed is made of).
    /// <paramref name="provenance"/> null reads every column by the material estimate, exactly the pre-B133
    /// behaviour; non-null lets a recorded Ground claim override a built-looking material's own reading.</summary>
    private static Dictionary<(int X, int Z), Column> ReadColumns(
        List<AnvilRegion.Chunk> chunks, int? yMax, WorldProvenance? provenance)
    {
        var surface = LayerExtractors.Surface(chunks, maxBuildHeight: yMax).ToList();
        var byCell = new Dictionary<(int X, int Z), Column>(surface.Count);
        foreach (var block in surface)
            byCell[(block.WorldX, block.WorldZ)] = new Column(block.WorldY,
                RenderCategories.Of(block.BlockId, provenance?.LayerAt(block.WorldX, block.WorldZ)),
                block.BlockId, block.BlockData, 0);

        // Only the water columns need the second, deeper read, so the bed lookup is built for those alone.
        var waterCells = surface
            .Where(block => block.BlockId is Blocks.Water or Blocks.StationaryWater)
            .Select(block => ((block.WorldX, block.WorldZ), block.WorldY))
            .ToDictionary(entry => entry.Item1, entry => entry.WorldY);
        if (waterCells.Count == 0) return byCell;

        foreach (var (cell, bed, depth) in Beds(chunks, waterCells))
            byCell[cell] = new Column(bed.Y, RenderCategory.Water, bed.BlockId, bed.BlockData, depth);
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

    /// <summary>Category (or material) colours with north-facing relief and a light absolute-height term.
    /// <paramref name="pointMode"/> is the isolated foliage layer's point-and-radius reading (<see cref="
    /// DrawTreePoints"/> paints over the result after this returns): every column becomes the flat context
    /// backdrop regardless of its own category, because the leaf/log mass is exactly the reading this mode
    /// replaces — painting it underneath the circles would just be the old picture with dots added to it.</summary>
    private static byte[] Paint(Dictionary<(int X, int Z), Column> columns, int minX, int minZ,
                                int blocksWide, int blocksHigh, int lowest, int highest,
                                TopDownColorMode colorMode, TopDownLayer layer, bool pointMode = false)
    {
        var pixels = new byte[blocksWide * blocksHigh * 3];
        var span = Math.Max(1, highest - lowest);

        for (var row = 0; row < blocksHigh; row++)
            for (var col = 0; col < blocksWide; col++)
            {
                if (!columns.TryGetValue((minX + col, minZ + row), out var column))
                {
                    // Void: a flat near-black, so a hole in the world is obviously not terrain.
                    Raster.Set(pixels, blocksWide, col, row, RenderCategories.VoidRgb);
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

                var baseColor = pointMode ? ContextRgb : BaseColor(column, colorMode, layer);
                Raster.Set(pixels, blocksWide, col, row, Raster.Scale(baseColor, keep));
            }
        return pixels;
    }

    private static int BaseColor(Column column, TopDownColorMode colorMode, TopDownLayer layer)
    {
        if (colorMode == TopDownColorMode.Material) return BlockPalette.PackedRgb(column.BlockId, column.BlockData);

        var categoryRgb = column.Category == RenderCategory.Water
            ? Raster.Scale(RenderCategories.WaterRgb, 1 - (1 - DeepWaterKeep) * Math.Min(1.0, column.WaterDepth / (double)FullDepth))
            : RenderCategories.ColourOf(column.Category);

        return layer switch
        {
            TopDownLayer.Combined => categoryRgb,
            TopDownLayer.Objectives => ContextRgb,
            TopDownLayer.Ground => column.Category == RenderCategory.Ground ? RenderCategories.HighlightOf(RenderCategory.Ground) : ContextRgb,
            TopDownLayer.Structure => column.Category == RenderCategory.Structure ? RenderCategories.HighlightOf(RenderCategory.Structure) : ContextRgb,
            TopDownLayer.Foliage => column.Category == RenderCategory.Foliage ? RenderCategories.HighlightOf(RenderCategory.Foliage) : ContextRgb,
            _ => categoryRgb,
        };
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
