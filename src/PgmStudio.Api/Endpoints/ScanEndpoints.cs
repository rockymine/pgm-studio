using FastEndpoints;
using LinqToDB;
using LinqToDB.Async;
using PgmStudio.Analysis.Scan;
using PgmStudio.Data.Features;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Minecraft;

namespace PgmStudio.Api.Endpoints;

using Dict = Dictionary<string, object?>;
using PgmStudio.Contracts;
using PgmStudio.Minecraft.Palette;

/// <summary>Shared surface-parquet → pixels / block-types projection (B4 + B9).</summary>
internal static class BlockPixels
{
    /// <summary>Parallel xs/zs/colors arrays + bounds for a column set (caller ensures non-empty).</summary>
    public static BlockPixelsDto Pixels(IReadOnlyList<SurfaceCell> cells)
    {
        var xs = new int[cells.Count];
        var zs = new int[cells.Count];
        var colors = new string[cells.Count];
        int minX = int.MaxValue, minZ = int.MaxValue, maxX = int.MinValue, maxZ = int.MinValue;
        for (var i = 0; i < cells.Count; i++)
        {
            var c = cells[i];
            xs[i] = c.X; zs[i] = c.Z;
            // BlockPalette.Hex is an array read returning a shared string, so a per-call cache would cost
            // more than it saves.
            colors[i] = BlockPalette.Hex(c.BlockId, c.BlockData);
            if (c.X < minX) minX = c.X; if (c.X > maxX) maxX = c.X;
            if (c.Z < minZ) minZ = c.Z; if (c.Z > maxZ) maxZ = c.Z;
        }
        return new BlockPixelsDto(minX, minZ, maxX, maxZ, Xs: xs, Zs: zs, Colors: colors);
    }

    /// <summary>The payload for no cells at all — a degenerate zero-span box, so a client can decode it with
    /// the same code path and simply blit nothing rather than branch on a null.</summary>
    public static BlockPixelsDto EmptyPixels() => new(0, 0, -1, -1, Xs: [], Zs: [], Colors: []);

    /// <summary>
    /// A painted footprint in whichever of the two block-pixel encodings is smaller for it — the client decodes
    /// both, so the choice is free and self-tuning.
    /// <para><b>Runs</b> (<c>palette</c> + <c>runs</c>) walk the bounding box row by row as
    /// <c>[paletteIndex, length, …]</c> with <c>-1</c> for a cell outside the footprint. This is what terrain
    /// actually looks like — measured over the sketch corpus a board emits <b>0.05 to 0.27 runs per painted
    /// cell</b>, because even a voronoi paints in patches and the void between islands is one long run — so it
    /// beats a per-cell list several times over, and a plain dense mask by more (real footprints fill only
    /// 17–40% of their own bounding box, so most of a mask would be void).</para>
    /// <para><b>Cells</b> (<c>xs</c>/<c>zs</c>/<c>color_idx</c>) is the fallback, for a footprint so scattered
    /// that its runs cost more than its cells, or one whose bounding box is too large to raster at all.</para>
    /// </summary>
    public static BlockPixelsDto PalettePixels(IReadOnlyList<SurfaceCell> cells)
    {
        var asRuns = RunPixels(cells);
        return asRuns is not null && asRuns.Runs!.Count < 3 * cells.Count ? asRuns : CellPixels(cells);
    }

    // The largest bounding box worth rastering to find runs. A footprint is normally compact, but nothing stops
    // two shapes being drawn a world apart, and that box must not become the allocation.
    private const long MaxRunRasterCells = 4_000_000;

    /// <summary>The run encoding, or null when the footprint's bounding box is too large to raster.</summary>
    private static BlockPixelsDto? RunPixels(IReadOnlyList<SurfaceCell> cells)
    {
        int minX = int.MaxValue, minZ = int.MaxValue, maxX = int.MinValue, maxZ = int.MinValue;
        foreach (var c in cells)
        {
            if (c.X < minX) minX = c.X; if (c.X > maxX) maxX = c.X;
            if (c.Z < minZ) minZ = c.Z; if (c.Z > maxZ) maxZ = c.Z;
        }
        long width = (long)maxX - minX + 1, height = (long)maxZ - minZ + 1;
        if (width * height > MaxRunRasterCells) return null;

        var colorCache = new Dictionary<(int, int), int>();
        var palette = new List<string>();
        var slot = new int[width * height];
        Array.Fill(slot, -1);
        foreach (var c in cells)
        {
            var key = (c.BlockId, c.BlockData);
            if (!colorCache.TryGetValue(key, out var index))
            {
                colorCache[key] = index = palette.Count;
                palette.Add(BlockPalette.Hex(c.BlockId, c.BlockData));
            }
            slot[(c.Z - minZ) * width + (c.X - minX)] = index;
        }

        var runs = new List<int>();
        var value = slot[0];
        var length = 0;
        foreach (var s in slot)
        {
            if (s == value) { length++; continue; }
            runs.Add(value); runs.Add(length);
            value = s; length = 1;
        }
        runs.Add(value); runs.Add(length);

        return new BlockPixelsDto(minX, minZ, maxX, maxZ, Palette: palette, Runs: runs);
    }

    /// <summary>One entry per painted cell: parallel <c>xs</c>/<c>zs</c> plus a <c>color_idx</c> into the shared
    /// palette. Terrain is built from a handful of blocks, so repeating an 8-character hex per cell was most of
    /// the response before the palette went in — 657 KB on a 200×200 board, 440 KB of it the same few strings.
    /// </summary>
    private static BlockPixelsDto CellPixels(IReadOnlyList<SurfaceCell> cells)
    {
        var colorCache = new Dictionary<(int, int), int>();
        var palette = new List<string>();
        var xs = new int[cells.Count];
        var zs = new int[cells.Count];
        var idx = new int[cells.Count];
        int minX = int.MaxValue, minZ = int.MaxValue, maxX = int.MinValue, maxZ = int.MinValue;
        for (var i = 0; i < cells.Count; i++)
        {
            var c = cells[i];
            xs[i] = c.X; zs[i] = c.Z;
            var key = (c.BlockId, c.BlockData);
            if (!colorCache.TryGetValue(key, out var slot))
            {
                colorCache[key] = slot = palette.Count;
                palette.Add(BlockPalette.Hex(c.BlockId, c.BlockData));
            }
            idx[i] = slot;
            if (c.X < minX) minX = c.X; if (c.X > maxX) maxX = c.X;
            if (c.Z < minZ) minZ = c.Z; if (c.Z > maxZ) maxZ = c.Z;
        }
        return new BlockPixelsDto(minX, minZ, maxX, maxZ, Xs: xs, Zs: zs, Palette: palette, ColorIdx: idx);
    }

}

/// <summary>
/// GET /api/map/{slug}/top-surface — per-column surface colour overlay (B4). Reads the cached
/// <c>layer.parquet</c> artifact, maps each column's (block_id, block_data) to a hex colour, and
/// returns parallel xs/zs/colors arrays + the bounds. Mirrors the reference <c>layer_top_surface</c>;
/// unblocks the "Blocks" canvas overlay (C6).
/// </summary>
public sealed class TopSurfaceEndpoint(MapRepository repo, MapArtifactStore artifacts) : EndpointWithoutRequest<BlockPixelsDto>
{
    public override void Configure() { Get("/map/{slug}/top-surface"); AllowAnonymous(); Description(b => b.Refuses(404)); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;

        var layer = await artifacts.LoadAsync(map.Id, ArtifactKind.SurfaceParquet, ct);
        if (layer is null) { await Refusals.NotFoundAsync(HttpContext, "surface layer", ct); return; }

        var cells = await SurfaceScan.ReadAsync(layer);
        if (cells.Count == 0) { await Refusals.NotFoundAsync(HttpContext, "surface layer", ct); return; }
        await Send.OkAsync(BlockPixels.Pixels(cells), ct);
    }
}

/// <summary>
/// GET /api/map/{slug}/segments?axis=x|z — side-view depth profile (B5). Projects the map's vertical
/// solid segments onto a 2D (primary × y) grid via <see cref="SideView"/>; feeds the Build-Regions
/// side-view canvas (C7). Mirrors the reference <c>get_segments</c>.
/// </summary>
public sealed class SegmentsEndpoint(MapRepository repo, PgmDb db) : EndpointWithoutRequest<SegmentsDto>
{
    public override void Configure() { Get("/map/{slug}/segments"); AllowAnonymous(); Description(b => b.Refuses(404)); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var axis = HttpContext.Request.Query["axis"].ToString();
        if (string.IsNullOrEmpty(axis)) axis = "nz";
        if (axis is not ("x" or "z" or "nz" or "pz" or "nx" or "px"))
        {
            await Refusals.UnreadableAsync(HttpContext, "invalid axis",
                "axis must be one of nz/pz/nx/px (or the legacy x/z)", ct, field: "axis");
            return;
        }

        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;

        // Optional world-window filter (xmin/xmax/zmin/zmax) → a localised slice for the mini side-view
        // (a point's column + neighbours, or a rectangle's footprint). Absent params = the whole map.
        int? Q(string k) => int.TryParse(HttpContext.Request.Query[k], out var v) ? v : null;
        int? xmin = Q("xmin"), xmax = Q("xmax"), zmin = Q("zmin"), zmax = Q("zmax");
        var q = db.Segments.Where(s => s.MapId == map.Id);
        if (xmin is int a) q = q.Where(s => s.WorldX >= a);
        if (xmax is int b) q = q.Where(s => s.WorldX <= b);
        if (zmin is int c) q = q.Where(s => s.WorldZ >= c);
        if (zmax is int d) q = q.Where(s => s.WorldZ <= d);

        var rows = await q.ToListAsync(ct);
        var result = SideView.Build(rows.Select(r => (r.WorldX, r.WorldZ, r.WorldYStart, r.WorldYEnd)), axis);
        if (result is null) { await Refusals.NotFoundAsync(HttpContext, "segment data", ct); return; }

        await Send.OkAsync(new SegmentsDto(
            result.Axis, result.PrimaryMin, result.PrimaryCount, result.YMin, result.YCount, result.Depth), ct);
    }
}

/// <summary>
/// GET /api/map/{slug}/column-floor?x=&amp;z=&amp;y= — the terrain floor Y at a single column from the
/// vertical segments: the top of the highest solid segment at or below the reference <c>y</c> (the floor a
/// thing at <c>y</c> rests on), falling back to the segment top nearest <c>y</c>. Returns <c>{y:null}</c>
/// when the column has no segment data. Used to seed a wool spawn's Y onto solid ground.
/// </summary>
public sealed class ColumnFloorEndpoint(MapRepository repo, PgmDb db) : EndpointWithoutRequest<ColumnFloorDto>
{
    public override void Configure() { Get("/map/{slug}/column-floor"); AllowAnonymous(); Description(b => b.Refuses(404)); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;
        if (!int.TryParse(HttpContext.Request.Query["x"], out var x) || !int.TryParse(HttpContext.Request.Query["z"], out var z))
        {
            await Refusals.UnreadableAsync(HttpContext, "column not named",
                "a column is asked for by its x and z, and one of them is missing", ct);
            return;
        }
        var refY = int.TryParse(HttpContext.Request.Query["y"], out var ry) ? ry : int.MaxValue;

        var tops = await db.Segments
            .Where(s => s.MapId == map.Id && s.WorldX == x && s.WorldZ == z)
            .Select(s => s.WorldYEnd).ToListAsync(ct);
        if (tops.Count == 0) { await Send.OkAsync(new ColumnFloorDto(null), ct); return; }

        var below = tops.Where(t => t <= refY).ToList();
        int floor = below.Count > 0 ? below.Max() : tops.OrderBy(t => Math.Abs(t - refY)).First();
        await Send.OkAsync(new ColumnFloorDto(floor), ct);
    }
}
