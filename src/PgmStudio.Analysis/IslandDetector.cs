using System.Text.Json;
using System.Text.Json.Nodes;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Utilities;
using NetTopologySuite.Operation.Union;

namespace PgmStudio.Analysis;

/// <summary>
/// Detects "islands" — connected landmasses — from a layer scan's (x,z) footprint, with an exact
/// block-outline polygon per island. Port of <c>layout/islands.py</c> (8-connectivity BFS →
/// unit-square union → sort by block count). Geometry via NetTopologySuite (≡ Shapely/GEOS).
/// </summary>
public static class IslandDetector
{
    private static readonly GeometryFactory Gf = new();

    /// <summary>A detected island: 1-based id, block count, (min_x,min_z,max_x,max_z) extent, outline polygon.</summary>
    public sealed record Island(int Id, int BlockCount, (int MinX, int MinZ, int MaxX, int MaxZ) Bounds, Geometry Polygon);

    /// <summary>Detect islands from unique surface footprint coordinates.</summary>
    public static List<Island> Detect(IEnumerable<(int X, int Z)> coords, int minIslandSize = 10, int connectivity = 8)
    {
        var cells = new HashSet<(int, int)>(coords);
        if (cells.Count == 0) return [];

        var islands = new List<Island>();
        foreach (var comp in ConnectedComponents(cells, connectivity))
        {
            if (comp.Count < minIslandSize) continue;
            islands.Add(new Island(0, comp.Count, BoundsOf(comp), BlocksToPolygon(comp)));
        }

        // Stable sort by block count desc, then assign 1-based ids (matches Python's stable sort).
        var ordered = islands.OrderByDescending(i => i.BlockCount).ToList();
        for (var i = 0; i < ordered.Count; i++) ordered[i] = ordered[i] with { Id = i + 1 };
        return ordered;
    }

    /// <summary>
    /// Height-aware island detection (ND2 §6a / A5). Like <see cref="Detect"/>, but two adjacent base
    /// cells join into the same component only when their Y is <em>continuous</em>
    /// (|ΔY| ≤ <paramref name="heightTolerance"/>) — so a stark Y jump (the bottom-up base scan "shooting
    /// up" into a build floating over void) splits the floating mass off as its own component. Components
    /// whose median Y sits a clear <paramref name="heightOutlierMargin"/> <em>above</em> the terrain's
    /// dominant Y band are then pruned as floating decor (e.g. mame_i_shrunk_the_pvpers' eagles).
    /// </summary>
    public static List<Island> DetectHeightAware(
        IEnumerable<(int X, int Z, int Y)> cells,
        int minIslandSize = 10, int connectivity = 8, int heightTolerance = 3, int heightOutlierMargin = 12)
    {
        var yByCell = new Dictionary<(int, int), int>();
        foreach (var (x, z, y) in cells) yByCell[(x, z)] = y;   // one cell per column; last wins
        if (yByCell.Count == 0) return [];

        // Reference terrain height = median Y over all cells (terrain dominates the column count).
        var allYs = yByCell.Values.OrderBy(v => v).ToList();
        var terrainY = allYs[allYs.Count / 2];

        var islands = new List<Island>();
        foreach (var comp in HeightAwareComponents(yByCell, connectivity, heightTolerance))
        {
            if (comp.Count < minIslandSize) continue;
            var ys = comp.Select(c => yByCell[c]).OrderBy(v => v).ToList();
            if (ys[ys.Count / 2] > terrainY + heightOutlierMargin) continue;   // floating build over void
            islands.Add(new Island(0, comp.Count, BoundsOf(comp), BlocksToPolygon(comp)));
        }

        var ordered = islands.OrderByDescending(i => i.BlockCount).ToList();
        for (var i = 0; i < ordered.Count; i++) ordered[i] = ordered[i] with { Id = i + 1 };
        return ordered;
    }

    /// <summary>
    /// Cleaned-base island detection with a degenerate-read fallback (ND2 §6a / A5). Runs
    /// <see cref="DetectHeightAware"/> on the cleaned-base cells; if that reads degenerately (≤ 1 island —
    /// e.g. a base that bridges everything at one level even after the noise exclude), retries on the
    /// supplied fallback layers (typically y0 then bedrock) and keeps the first that separates into ≥ 2
    /// islands. Returns the base result when no fallback does better.
    /// </summary>
    public static List<Island> DetectCleaned(
        IEnumerable<(int X, int Z, int Y)> baseCells,
        IEnumerable<IEnumerable<(int X, int Z, int Y)>>? fallbackLayers = null,
        int minIslandSize = 10, int heightTolerance = 3, int heightOutlierMargin = 12)
    {
        var best = DetectHeightAware(baseCells, minIslandSize, 8, heightTolerance, heightOutlierMargin);
        if (best.Count >= 2 || fallbackLayers is null) return best;
        foreach (var layer in fallbackLayers)
        {
            var alt = DetectHeightAware(layer, minIslandSize, 8, heightTolerance, heightOutlierMargin);
            if (alt.Count > best.Count) best = alt;
            if (best.Count >= 2) break;
        }
        return best;
    }

    private static List<List<(int X, int Z)>> HeightAwareComponents(
        Dictionary<(int, int), int> yByCell, int connectivity, int heightTolerance)
    {
        (int, int)[] deltas = connectivity == 8
            ? [(-1, -1), (-1, 0), (-1, 1), (0, -1), (0, 1), (1, -1), (1, 0), (1, 1)]
            : [(-1, 0), (1, 0), (0, -1), (0, 1)];

        var remaining = new HashSet<(int, int)>(yByCell.Keys);
        var seeds = yByCell.Keys.OrderBy(c => c.Item1).ThenBy(c => c.Item2);
        var components = new List<List<(int X, int Z)>>();

        foreach (var seed in seeds)
        {
            if (!remaining.Remove(seed)) continue;
            var comp = new List<(int, int)>();
            var queue = new Queue<(int, int)>();
            queue.Enqueue(seed);
            while (queue.Count > 0)
            {
                var (x, z) = queue.Dequeue();
                comp.Add((x, z));
                var cy = yByCell[(x, z)];
                foreach (var (dx, dz) in deltas)
                {
                    var nb = (x + dx, z + dz);
                    // Join only across continuous terrain — a stark Y step breaks the link.
                    if (remaining.Contains(nb) && Math.Abs(yByCell[nb] - cy) <= heightTolerance)
                    {
                        remaining.Remove(nb);
                        queue.Enqueue(nb);
                    }
                }
            }
            components.Add(comp);
        }
        return components;
    }

    /// <summary>Serialise islands to the <c>islands.json</c> format (GeoJSON polygons, matching Shapely's <c>mapping()</c>).</summary>
    public static string SerializeJson(IReadOnlyList<Island> islands)
    {
        var arr = new JsonArray();
        foreach (var isl in islands)
        {
            arr.Add(new JsonObject
            {
                ["id"] = isl.Id,
                ["block_count"] = isl.BlockCount,
                ["bounds"] = new JsonArray(isl.Bounds.MinX, isl.Bounds.MinZ, isl.Bounds.MaxX, isl.Bounds.MaxZ),
                ["polygon"] = PolygonToGeoJson(isl.Polygon),
            });
        }
        return arr.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static List<List<(int X, int Z)>> ConnectedComponents(HashSet<(int, int)> cells, int connectivity)
    {
        (int, int)[] deltas = connectivity == 8
            ? [(-1, -1), (-1, 0), (-1, 1), (0, -1), (0, 1), (1, -1), (1, 0), (1, 1)]
            : [(-1, 0), (1, 0), (0, -1), (0, 1)];

        var remaining = new HashSet<(int, int)>(cells);
        // Deterministic seed order so island ids are reproducible (Python's set order is not).
        var seeds = cells.OrderBy(c => c.Item1).ThenBy(c => c.Item2);
        var components = new List<List<(int X, int Z)>>();

        foreach (var seed in seeds)
        {
            if (!remaining.Remove(seed)) continue;
            var comp = new List<(int, int)>();
            var queue = new Queue<(int, int)>();
            queue.Enqueue(seed);
            while (queue.Count > 0)
            {
                var (x, z) = queue.Dequeue();
                comp.Add((x, z));
                foreach (var (dx, dz) in deltas)
                {
                    var nb = (x + dx, z + dz);
                    if (remaining.Remove(nb)) queue.Enqueue(nb);
                }
            }
            components.Add(comp);
        }
        return components;
    }

    private static (int, int, int, int) BoundsOf(List<(int X, int Z)> comp)
    {
        int minX = int.MaxValue, minZ = int.MaxValue, maxX = int.MinValue, maxZ = int.MinValue;
        foreach (var (x, z) in comp)
        {
            if (x < minX) minX = x;
            if (z < minZ) minZ = z;
            if (x > maxX) maxX = x;
            if (z > maxZ) maxZ = z;
        }
        return (minX, minZ, maxX + 1, maxZ + 1);
    }

    private static Geometry BlocksToPolygon(List<(int X, int Z)> comp)
    {
        var squares = comp.Select(c =>
            (Geometry)Gf.ToGeometry(new Envelope(c.X, c.X + 1, c.Z, c.Z + 1))).ToList();
        var poly = UnaryUnionOp.Union((IEnumerable<Geometry>)squares);
        if (!poly.IsValid) poly = GeometryFixer.Fix(poly);
        // Diagonal-only touches can split into a MultiPolygon — keep the largest part (matches Python).
        if (poly is MultiPolygon mp && mp.NumGeometries > 0)
        {
            Geometry largest = mp.GetGeometryN(0);
            for (var i = 1; i < mp.NumGeometries; i++)
                if (mp.GetGeometryN(i).Area > largest.Area) largest = mp.GetGeometryN(i);
            poly = largest;
        }
        return poly;
    }

    private static JsonObject PolygonToGeoJson(Geometry geom)
    {
        var rings = new JsonArray();
        if (geom is Polygon p)
        {
            rings.Add(RingToJson(p.ExteriorRing));
            foreach (var hole in p.InteriorRings) rings.Add(RingToJson(hole));
        }
        return new JsonObject { ["type"] = "Polygon", ["coordinates"] = rings };
    }

    private static JsonArray RingToJson(LineString ring)
    {
        // The footprint is built in the X/Y plane (boxes are Envelope(x, x+1, z, z+1)), so the
        // map's z ordinate lives in Coordinate.Y — emit GeoJSON (x, z) pairs accordingly.
        var pts = new JsonArray();
        foreach (var c in ring.Coordinates)
            pts.Add(new JsonArray(c.X, c.Y));
        return pts;
    }
}
