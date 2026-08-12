using PgmStudio.Geom;
using PgmStudio.Geom.Relief;

namespace PgmStudio.Pgm.Sketch;

/// <summary>
/// Rasterizes a sketch layout (the <c>sketch_layout_json</c> blob) into the solid block cells of the
/// finished world (docs/contracts/sketch-tool-improvements.md §3). Pure: no DOM, no DB. <see cref="Rasterize"/>
/// yields the (x,z) footprint; <see cref="RasterizeColumns"/> also carries each cell's vertical span
/// <c>[YFloor, YTop]</c>, where <c>Floor</c> is the shape's elevation and <c>Height</c> its thickness:
/// <c>YTop = base_y + floor + height</c>. Height is a uniform <c>base_height</c>, or, for a polygon/lasso
/// whose <c>anchor_heights</c> line up with its vertices, a per-vertex thickness TIN-interpolated across
/// the footprint (<see cref="Triangulation"/>). Mirrors the JS geometry it must agree with (circle =
/// 64-gon, Bézier = 16 samples/edge); per-island mirror copies follow the saved island <c>shapeIds</c>.
/// </summary>
public static class SketchRasterizer
{
    private const int CirclePoints  = 64;   // matches JS geometry/shape.js CIRCLE_POINTS
    private const int BezierSamples  = 16;   // matches JS geometry/shape.js BEZIER_SAMPLES

    /// <summary>The finished world's solid (x,z) footprint (primary + opted-in island mirror copies).</summary>
    public static List<(int X, int Z)> Rasterize(string layoutJson)
        => RasterizeColumns(layoutJson).Select(c => (c.X, c.Z)).Distinct().ToList();

    /// <summary>As <see cref="Rasterize"/>, but each cell also carries its column span <c>[YFloor, YTop]</c>.
    /// Height never affects membership — the footprint is identical to <see cref="Rasterize"/>.</summary>
    public static List<(int X, int Z, int YFloor, int YTop)> RasterizeColumns(string layoutJson)
    {
        var state = SketchLayout.Parse(layoutJson);
        var cx = state?.Setup?.Center?.Cx ?? 0;
        var cz = state?.Setup?.Center?.Cz ?? 0;
        var axes = Symmetry.OrbitAxes(state?.Setup?.MirrorMode ?? "rot_180");

        // Stack every layer: each is rasterized in its own Y, then shifted by base_y. (x,z) may repeat
        // across layers — a column with a gap (e.g. ground + a sky bridge) keeps both segments.
        var output = new List<(int X, int Z, int YFloor, int YTop)>();
        foreach (var (layout, baseY) in ResolveLayers(state))
        {
            int by = (int)Math.Round(baseY);
            foreach (var kv in RasterizeLayout(layout, cx, cz, axes, state?.Relief, state?.Setup?.MirrorMode))
                output.Add((kv.Key.Item1, kv.Key.Item2, kv.Value.Floor + by, kv.Value.Top + by));
        }
        return output;
    }

    /// <summary>Every relief-bearing island's solved surface, keyed by island id, with each field's heights
    /// already shifted into world Y by its layer's <c>base_y</c>. This is the same solve the build runs, from
    /// the same entry point, so a preview drawn from it cannot show a surface the world will not have —
    /// which is the only reason a preview is worth drawing.
    ///
    /// <para>An island appears once. A layout that names the same island on two layers is malformed, and
    /// showing the lower of the two would be a quieter wrong answer than showing the first.</para></summary>
    /// <param name="warmStart">The surface each island's last solve settled on, asked for by island id and
    /// footprint. Resuming from it is a head start and never a different answer — the solver discards a resume
    /// that fails to settle — so a caller with nothing to offer simply omits this.</param>
    /// <param name="remember">Handed each island's solve as the solver produced it, for a caller keeping
    /// surfaces to resume from. It is the <b>unshifted</b> field, and the pairing is the point: what comes
    /// back from this method has its layer's <c>base_y</c> added, and feeding that back as a head start would
    /// seed the next solve a whole layer too high.</param>
    public static Dictionary<string, HeightField> ReliefFields(
        string layoutJson, Func<string, Footprint, double[]?>? warmStart = null,
        Action<string, HeightField>? remember = null)
    {
        var state = SketchLayout.Parse(layoutJson);
        if (state?.Relief is not { Count: > 0 } relief) return [];

        var cx = state.Setup?.Center?.Cx ?? 0;
        var cz = state.Setup?.Center?.Cz ?? 0;

        var fields = new Dictionary<string, HeightField>();
        foreach (var (layout, baseY) in ResolveLayers(state))
        {
            var shapes = layout?.Shapes ?? [];
            if (shapes.Count == 0) continue;

            var shift = (int)Math.Round(baseY);
            foreach (var (islandId, field) in SolveRelief(RasterGroup(shapes), shapes, layout?.Islands ?? [],
                                                          relief, state.Setup?.MirrorMode, cx, cz, warmStart))
            {
                if (!fields.ContainsKey(islandId)) remember?.Invoke(islandId, field);
                fields.TryAdd(islandId, shift == 0 ? field : new HeightField(field.Footprint,
                    [.. field.Continuous.Select(height => height + shift)],
                    [.. field.Blocks.Select(height => height + shift)]));
            }
        }
        return fields;
    }

    /// <summary>Maps every cell a <em>themed</em> shape covers to that shape's id — the scope
    /// <c>TerrainThemeScope</c> resolves a cell's paint through.</summary>
    public static Dictionary<(int X, int Z), string> ShapeThemeOwners(string layoutJson)
        => ShapeScopeOwners(layoutJson, shape => shape.Theme);

    /// <summary>Maps every cell a scoped shape covers to that shape's id — the primary footprint plus each
    /// mirroring island's orbit copies (which keep the shape id), the smallest-area shape winning an overlap
    /// (the most specific scope). <paramref name="scopeOf"/> says which annotation makes a shape a scope, so
    /// paint and planting resolve through one traversal rather than two that could disagree about which shape
    /// owns a contested cell — and each caller keeps its own rule for what counts, since what makes a shape a
    /// paint scope and what makes it a planting one are not the same question. Only add shapes the predicate
    /// answers for are considered; subtracts and role-tagged (structural) shapes are skipped — they place no
    /// terrain of their own. Void cells that no surface stands on are harmless: a consumer only reads owners
    /// where a column is solid.</summary>
    public static Dictionary<(int X, int Z), string> ShapeScopeOwners(string layoutJson, Func<SketchShape, string?> scopeOf)
    {
        var state = SketchLayout.Parse(layoutJson);
        var cx = state?.Setup?.Center?.Cx ?? 0;
        var cz = state?.Setup?.Center?.Cz ?? 0;
        var axes = Symmetry.OrbitAxes(state?.Setup?.MirrorMode ?? "rot_180");

        var owner = new Dictionary<(int, int), string>();
        var areaOf = new Dictionary<(int, int), long>();

        void Claim(SketchShape s)
        {
            if (scopeOf(s) is null || s.Operation == "subtract" || s.Role is not null) return;
            var cells = RasterShape(s).Select(c => (c.X, c.Z)).ToList();
            long area = cells.Count;
            foreach (var cell in cells)
                if (!owner.ContainsKey(cell) || area < areaOf[cell]) { owner[cell] = s.Id; areaOf[cell] = area; }
        }

        foreach (var (layout, _) in ResolveLayers(state))
        {
            var shapes = layout?.Shapes ?? [];
            foreach (var s in shapes) Claim(s);                             // primary footprint

            var metas = layout?.Islands ?? [];
            if (metas.Count == 0)
            {
                foreach (var axis in axes) foreach (var s in shapes) Claim(MirrorShape(s, axis, cx, cz));
            }
            else
            {
                var byId = shapes.GroupBy(s => s.Id).ToDictionary(g => g.Key, g => g.First());
                foreach (var meta in metas.Where(m => m.Mirrors))
                    foreach (var id in meta.ShapeIds.Where(byId.ContainsKey))
                        foreach (var axis in axes) Claim(MirrorShape(byId[id], axis, cx, cz));
            }
        }
        return owner;
    }

    // Layers to rasterize: the S7 `layers` array, else the legacy single `layout` at base_y 0.
    private static List<(SketchShapes Layout, double BaseY)> ResolveLayers(SketchLayout? state)
    {
        if (state?.Layers is { Count: > 0 } layers)
            return layers.Select(l => (l.Layout ?? new SketchShapes(), l.BaseY)).ToList();
        if (state?.Layout is { } single) return [(single, 0.0)];
        return [];
    }

    // One layer → its solid (x,z) cells with layer-local columns (primary + opted-in island mirror copies).
    private static Dictionary<(int, int), (int Top, int Floor)> RasterizeLayout(
        SketchShapes layout, double cx, double cz, IReadOnlyList<string> axes,
        Dictionary<string, SketchReliefJson>? relief = null, string? mirrorMode = null)
    {
        var shapes = layout?.Shapes ?? [];
        if (shapes.Count == 0) return [];

        var cells = RasterGroup(shapes);                 // primary
        var metas = layout?.Islands ?? [];

        // Interior elevation, per island, over the cells the set algebra actually left standing — so a relief
        // never re-adds ground a subtract took away. The solved surface replaces the column's top and leaves
        // its floor alone: a relief says where the ground is, not how thick the slab under it is.
        var solved = SolveRelief(cells, shapes, metas, relief, mirrorMode, cx, cz);
        foreach (var (islandId, field) in solved)
            foreach (var (x, z) in field.Footprint.Land())
                if (cells.TryGetValue((x, z), out var column))
                    cells[(x, z)] = (Math.Max(column.Floor + 1, field.At(x, z)), column.Floor);

        if (metas.Count == 0)
        {
            // No island metadata (hand-authored): mirror the whole primary footprint (height is invariant).
            var primary = new Dictionary<(int, int), (int Top, int Floor)>(cells);
            foreach (var axis in axes)
            {
                var mirrored = new Dictionary<(int, int), (int Top, int Floor)>();
                foreach (var (k, v) in primary) mirrored[MirrorCell(k, axis, cx, cz)] = v;
                Merge(cells, mirrored);
            }
        }
        else
        {
            var byId = shapes.GroupBy(s => s.Id).ToDictionary(g => g.Key, g => g.First());
            foreach (var meta in metas.Where(m => m.Mirrors))
            {
                var islandShapes = meta.ShapeIds.Where(byId.ContainsKey).Select(id => byId[id]).ToList();
                var field = meta.Id is { Length: > 0 } id ? solved.GetValueOrDefault(id) : null;
                foreach (var axis in axes)
                {
                    var mirrored = islandShapes.Select(s => MirrorShape(s, axis, cx, cz));
                    var copy = RasterGroup(mirrored);
                    // A mirrored copy of a relief-bearing island takes its heights from the island's own
                    // solved surface, read back through the same transform — exactly symmetric by
                    // construction, rather than symmetric to within a second solve's tolerance.
                    if (field is not null)
                        foreach (var cell in copy.Keys.ToList())
                        {
                            var source = MirrorCell(cell, axis, cx, cz);
                            if (!field.Has(source.Item1, source.Item2)) continue;
                            copy[cell] = (Math.Max(copy[cell].Floor + 1, field.At(source.Item1, source.Item2)),
                                          copy[cell].Floor);
                        }
                    Merge(cells, copy);
                }
            }
        }
        return cells;
    }

    /// <summary>Each relief-bearing island's solved surface, over the cells that island actually contributes
    /// to the standing footprint. An island with no relief is absent, which is the common case and costs
    /// nothing.</summary>
    private static Dictionary<string, HeightField> SolveRelief(
        Dictionary<(int, int), (int Top, int Floor)> cells, List<SketchShape> shapes, List<SketchIsland> metas,
        Dictionary<string, SketchReliefJson>? relief, string? mirrorMode, double cx, double cz,
        Func<string, Footprint, double[]?>? warmStart = null)
    {
        var solved = new Dictionary<string, HeightField>();
        if (relief is not { Count: > 0 }) return solved;

        var byId = shapes.GroupBy(s => s.Id).ToDictionary(g => g.Key, g => g.First());
        foreach (var meta in metas)
        {
            if (meta.Id is not { Length: > 0 } islandId) continue;
            if (!relief.TryGetValue(islandId, out var stated)) continue;

            // The island's own ground: the cells its add-shapes cover that survived the layer's set algebra.
            var owned = new List<(int X, int Z)>();
            foreach (var id in meta.ShapeIds.Where(byId.ContainsKey))
            {
                var shape = byId[id];
                if (shape.Role is not null || shape.Operation == "subtract") continue;
                foreach (var (x, z, _, _) in RasterShape(shape))
                    if (cells.ContainsKey((x, z))) owned.Add((x, z));
            }
            if (owned.Count == 0) continue;

            var footprint = Footprint.Over(owned.Distinct().ToList(), margin: 0);
            solved[islandId] = ReliefSolver.Solve(footprint, stated.ToSpec(mirrorMode, cx, cz),
                                                  warmStart?.Invoke(islandId, footprint));
        }
        return solved;
    }

    // ── 4-step set algebra over a shape group, carrying each cell's column ─────────────────────────
    private static Dictionary<(int, int), (int Top, int Floor)> RasterGroup(IEnumerable<SketchShape> shapes)
    {
        Dictionary<(int, int), (int Top, int Floor)> add = [], oadd = [];
        HashSet<(int, int)> sub = [], osub = [];
        foreach (var s in shapes)
        {
            if (s.Role is not null) continue;   // structural annotation (S25) — not terrain, never rasterized
            if (s.Operation == "subtract")
            {
                var set = s.Override ? osub : sub;
                foreach (var c in RasterShape(s)) set.Add((c.X, c.Z));
            }
            else
            {
                var dict = s.Override ? oadd : add;
                foreach (var c in RasterShape(s)) MergeCell(dict, (c.X, c.Z), (c.Top, c.Floor));
            }
        }
        // ((adds − subs) ∪ override-adds) − override-subs; height = the tallest add on each cell.
        var result = new Dictionary<(int, int), (int Top, int Floor)>(add);
        foreach (var k in sub) result.Remove(k);
        foreach (var (k, v) in oadd) result[k] = v;        // override-add overwrites the column
        foreach (var k in osub) result.Remove(k);
        return result;
    }

    // Taller surface wins where add shapes overlap (carrying that surface's floor).
    private static void MergeCell(Dictionary<(int, int), (int Top, int Floor)> d, (int, int) k, (int Top, int Floor) v)
    {
        if (d.TryGetValue(k, out var ex)) { if (v.Top > ex.Top) d[k] = v; }
        else d[k] = v;
    }

    private static void Merge(Dictionary<(int, int), (int Top, int Floor)> dst, Dictionary<(int, int), (int Top, int Floor)> src)
    {
        foreach (var (k, v) in src) MergeCell(dst, k, v);
    }

    // ── single shape → cells with column (rasterize its ring by block-centre sampling) ────────────
    // Floor = where the shape's base sits (elevation), Height = thickness: a column spans
    // [floor, floor + height]. Invariants (enforced here so finish output is valid even for legacy
    // stored data): floor >= 0 and thickness >= 1 (a shape is never zero-height).
    private static IEnumerable<(int X, int Z, int Top, int Floor)> RasterShape(SketchShape s)
    {
        var ring = RingOf(s);
        if (ring.Count < 3) yield break;
        int floor = Math.Max(0, (int)Math.Round(s.Floor ?? 0));
        var height = HeightFn(s);
        foreach (var (x, z) in RasterRing(ring))
        {
            int thickness = Math.Max(1, (int)Math.Round(height(x + 0.5, z + 0.5)));
            yield return (x, z, floor + thickness, floor);
        }
    }

    // The thickness sampler for a shape: a per-vertex TIN (polygon/lasso with matching anchor_heights),
    // else the uniform base_height (default 1). The result is a thickness above the floor, not an absolute
    // top. The TIN is over the straight vertex polygon — points in a Bézier fringe fall back to the nearest
    // vertex inside Interpolate.
    private static Func<double, double, double> HeightFn(SketchShape s)
    {
        if ((s.Type == "polygon" || s.Type == "lasso") && s.Vertices is { Length: >= 3 } verts
            && s.AnchorHeights is { } ah && ah.Length == verts.Length)
        {
            var poly = verts.Select(v => new[] { v[0], v[1] }).ToList();
            var tris = Triangulation.EarClip(poly);
            return (x, z) => Triangulation.Interpolate(poly, ah, tris, x, z);
        }
        double bh = s.BaseHeight ?? 1;
        return (_, _) => bh;
    }

    private static List<double[]> RingOf(SketchShape s) => s.Type switch
    {
        "rectangle" => [[s.MinX ?? 0, s.MinZ ?? 0], [s.MaxX ?? 0, s.MinZ ?? 0], [s.MaxX ?? 0, s.MaxZ ?? 0], [s.MinX ?? 0, s.MaxZ ?? 0]],
        "circle"    => CircleRing(s.CenterX ?? 0, s.CenterZ ?? 0, s.Radius ?? 0),
        "polygon" or "lasso" => PolygonRing(s.Vertices, s.Controls),
        _ => [],
    };

    private static List<double[]> CircleRing(double cx, double cz, double r)
    {
        var pts = new List<double[]>(CirclePoints);
        for (var i = 0; i < CirclePoints; i++)
        {
            var a = 2 * Math.PI * i / CirclePoints;
            pts.Add([Math.Round(cx + r * Math.Cos(a)), Math.Round(cz + r * Math.Sin(a))]);
        }
        return pts;
    }

    private static List<double[]> PolygonRing(double[][]? verts, Dictionary<string, SketchControl>? controls)
    {
        if (verts is null || verts.Length < 3) return [];
        if (controls is null || controls.Count == 0) return [.. verts.Select(v => new[] { v[0], v[1] })];

        var ring = new List<double[]>();
        var n = verts.Length;
        for (var i = 0; i < n; i++)
        {
            var j = (i + 1) % n;
            var p0 = verts[i];
            var p3 = verts[j];
            var cpOut = controls.GetValueOrDefault(i.ToString())?.Out;
            var cpIn  = controls.GetValueOrDefault(j.ToString())?.In;
            if (cpOut is not null || cpIn is not null) ring.AddRange(SampleBezier(p0, cpOut ?? p0, cpIn ?? p3, p3));
            else ring.Add([p0[0], p0[1]]);
        }
        return ring;
    }

    private static IEnumerable<double[]> SampleBezier(double[] p0, double[] c1, double[] c2, double[] p3)
    {
        for (var k = 0; k < BezierSamples; k++)
        {
            double t = (double)k / BezierSamples, u = 1 - t;
            yield return [
                u*u*u*p0[0] + 3*u*u*t*c1[0] + 3*u*t*t*c2[0] + t*t*t*p3[0],
                u*u*u*p0[1] + 3*u*u*t*c1[1] + 3*u*t*t*c2[1] + t*t*t*p3[1],
            ];
        }
    }

    private static IEnumerable<(int, int)> RasterRing(List<double[]> ring)
    {
        if (ring.Count < 3) yield break;
        double minX = double.MaxValue, minZ = double.MaxValue, maxX = double.MinValue, maxZ = double.MinValue;
        foreach (var p in ring)
        {
            minX = Math.Min(minX, p[0]); maxX = Math.Max(maxX, p[0]);
            minZ = Math.Min(minZ, p[1]); maxZ = Math.Max(maxZ, p[1]);
        }
        for (var x = (int)Math.Floor(minX); x < (int)Math.Ceiling(maxX); x++)
            for (var z = (int)Math.Floor(minZ); z < (int)Math.Ceiling(maxZ); z++)
                if (Polygon.PointInRing(x + 0.5, z + 0.5, ring)) yield return (x, z);
    }

    // ── symmetry ──────────────────────────────────────────────────────────────────────────────────
    // Mirror a block cell's centre, then floor back to a cell (used only for the no-metadata fallback).
    private static (int, int) MirrorCell((int X, int Z) c, string axis, double cx, double cz)
    {
        var (x, z) = MirrorPoint(c.X + 0.5, c.Z + 0.5, axis, cx, cz);
        return ((int)Math.Floor(x), (int)Math.Floor(z));
    }

    private static SketchShape MirrorShape(SketchShape s, string axis, double cx, double cz)
    {
        // Polygon/lasso: transform vertices + Bézier handles in place, keeping anchor_heights index-aligned
        // (height is invariant) so a per-vertex surface mirrors correctly.
        if ((s.Type == "polygon" || s.Type == "lasso") && s.Vertices is { } verts)
        {
            var nv = verts.Select(v => { var (x, z) = MirrorPoint(v[0], v[1], axis, cx, cz); return new[] { x, z }; }).ToArray();
            Dictionary<string, SketchControl>? nc = null;
            if (s.Controls is { } ctrls)
            {
                nc = [];
                foreach (var (k, c) in ctrls)
                {
                    var nco = new SketchControl();
                    if (c.In  is { } i) { var (x, z) = MirrorPoint(i[0], i[1], axis, cx, cz); nco.In  = [x, z]; }
                    if (c.Out is { } o) { var (x, z) = MirrorPoint(o[0], o[1], axis, cx, cz); nco.Out = [x, z]; }
                    nc[k] = nco;
                }
            }
            return new SketchShape
            {
                Id = s.Id, Type = s.Type, Operation = s.Operation, Override = s.Override,
                Vertices = nv, Controls = nc, AnchorHeights = s.AnchorHeights, BaseHeight = s.BaseHeight, Floor = s.Floor,
                Theme = s.Theme,
            };
        }
        // Rectangle/circle: flatten the transformed footprint to a polygon (uniform height carried).
        var ring = RingOf(s).Select(p => { var (x, z) = MirrorPoint(p[0], p[1], axis, cx, cz); return new[] { x, z }; }).ToArray();
        return new SketchShape
        {
            Id = s.Id, Type = "polygon", Operation = s.Operation, Override = s.Override,
            Vertices = ring, BaseHeight = s.BaseHeight, Floor = s.Floor, Theme = s.Theme,
        };
    }

    // The one canonical concrete-axis transform — every orbit axis stays consistent with the generator + JS.
    private static (double, double) MirrorPoint(double x, double z, string axis, double cx, double cz)
        => Symmetry.Apply(x, z, axis, cx, cz);
}
