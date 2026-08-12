using PgmStudio.Geom;
using PgmStudio.Geom.Algorithms;
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

        // Erected shapes go on AFTER the relief, which is the whole of what makes them erected: a shape that
        // declares how its top is decided is one meant to stand OUT of the field rather than be part of it,
        // and it can only stand out of ground that already exists.
        Erect(cells, shapes);

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
                    var mirrored = islandShapes.Select(s => MirrorShape(s, axis, cx, cz)).ToList();
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
                    // The image gets the same pass over it the primary got, and in the same order. An erected
                    // shape is settled against the ground under it, so reading its height back through the
                    // mirror would give it the relief's answer instead of its own — one team a mesa and the
                    // other a hillside.
                    Erect(copy, mirrored);
                    Merge(cells, copy);
                }
            }
        }
        return cells;
    }

    /// <summary>The three words a shape can use to say how its top is decided. Anything else — including a
    /// word a later build knows and this one does not — is ordinary ground, which places terrain an author
    /// drew rather than terrain nobody asked for.</summary>
    private static bool IsErected(SketchShape shape)
        => shape.Role is null && shape.Operation != "subtract"
           && shape.HeightMode?.ToLowerInvariant() is "level" or "raise" or "sink";

    /// <summary>
    /// Applies the shapes that declare how their top is decided, over ground the relief has already made.
    ///
    /// <para>A shape without the word is ordinary ground: it draws a landmass, and the relief inside that
    /// landmass is what the ground does. The three words are for the other thing a shape can be — something
    /// standing in the terrain rather than being it.</para>
    ///
    /// <para><b>level</b> cuts a flat top at an absolute height, so it reads as a mesa and its faces are
    /// cliffs. <b>raise</b> and <b>sink</b> are relative, and are read at the <b>median</b> of the ground the
    /// shape covers rather than per cell: a monolith standing a fixed amount above every cell under it would
    /// be a blanket following the hillside, where what the word means is one flat-topped thing standing proud
    /// — which is also what keeps its prominence when it is dragged somewhere else on the map.</para>
    /// </summary>
    private static void Erect(Dictionary<(int, int), (int Top, int Floor)> cells, List<SketchShape> shapes)
    {
        foreach (var shape in shapes)
        {
            if (!IsErected(shape)) continue;
            var mode = shape.HeightMode!.ToLowerInvariant();

            var covered = RasterShape(shape).Select(cell => (cell.X, cell.Z))
                                            .Where(cells.ContainsKey).ToList();
            if (covered.Count == 0) continue;

            // The shape's OWN surface, whatever it is — a flat plate, a plane tilted through two vertices, or
            // a triangulation of per-vertex anchors. The word says what that surface is measured FROM; it does
            // not say the surface is flat, and reading one number here is what silently levelled a tilted
            // polygon the moment it was erected.
            var surface = HeightFn(shape);
            var floor = Math.Max(0, (int)Math.Round(shape.Floor ?? 0));

            // What a relative mode measures from: the middle of the ground the shape covers, read BEFORE any
            // of it is moved. Per-cell would make a monolith a blanket following the hillside; the median is
            // what makes it one thing standing proud, and what keeps its prominence when it is dragged.
            var ground = covered.Select(cell => cells[cell].Top).OrderBy(height => height).ToList();
            var datum = mode == "level" ? floor : ground[ground.Count / 2];
            var rise = mode == "sink" ? -1 : 1;

            int Stated(int x, int z) =>
                datum + rise * Math.Max(1, (int)Math.Round(surface(x + 0.5, z + 0.5)));

            // The skirt: how far in from its own outline the shape eases back into the ground it meets, so it
            // sits IN the terrain rather than on it. Zero is a sheer face, which is right for a plinth and
            // wrong for a landform — an unskirted mesa drops seventeen blocks in one cell.
            var skirt = Math.Max(0, shape.Skirt ?? 0);
            var depth = skirt > 0 ? InwardDepth(covered, cells) : null;

            foreach (var cell in covered)
            {
                var column = cells[cell];
                var top = Stated(cell.X, cell.Z);
                if (depth is not null && depth.TryGetValue(cell, out var reach) && reach.Depth <= skirt)
                {
                    // Linear from the ground just outside at the outline to the shape's own surface a skirt
                    // in. Linear rather than eased on purpose: the result is a talus slope of one constant
                    // grade, which is a thing the readback can measure and a player can read.
                    var t = reach.Depth / (double)(skirt + 1);
                    top = (int)Math.Round(reach.Ground + (top - reach.Ground) * t);
                }
                cells[cell] = (Math.Max(column.Floor + 1, top), column.Floor);
            }
        }
    }

    /// <summary>How deep inside a shape each of its cells sits, and the ground height just outside the outline
    /// nearest it. A multi-source sweep out from the boundary, so a cell knows both how far in it is and what
    /// the terrain was doing where it meets it — which is what lets a skirt follow a hillside rather than ease
    /// toward one average.</summary>
    private static Dictionary<(int X, int Z), (int Depth, int Ground)> InwardDepth(
        List<(int X, int Z)> covered, Dictionary<(int, int), (int Top, int Floor)> cells)
    {
        var inside = new HashSet<(int, int)>(covered);
        var reach = new Dictionary<(int X, int Z), (int Depth, int Ground)>();
        var queue = new Queue<(int X, int Z)>();

        foreach (var cell in covered)
            foreach (var (dx, dz) in Neighbours4)
            {
                var outside = (cell.X + dx, cell.Z + dz);
                if (inside.Contains(outside)) continue;
                // A boundary cell eases from the ground it meets, or — over the void — from its own floor,
                // since there is no terrain out there to embed into.
                var ground = cells.TryGetValue(outside, out var column) ? column.Top : cells[cell].Floor;
                if (!reach.TryGetValue(cell, out var known) || ground < known.Ground)
                    reach[cell] = (1, ground);
            }

        foreach (var cell in reach.Keys) queue.Enqueue(cell);
        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            var here = reach[cell];
            foreach (var (dx, dz) in Neighbours4)
            {
                var next = (cell.X + dx, cell.Z + dz);
                if (!inside.Contains(next)) continue;
                if (reach.ContainsKey(next)) continue;
                reach[next] = (here.Depth + 1, here.Ground);
                queue.Enqueue(next);
            }
        }
        return reach;
    }

    private static readonly (int X, int Z)[] Neighbours4 = [(1, 0), (-1, 0), (0, 1), (0, -1)];

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

            // The island's own ground: the cells its add-shapes cover that survived the layer's set algebra,
            // minus the shapes that take themselves out of the solve. An excluded shape is a hole, so the
            // relaxation bends around it exactly as it bends around the void.
            var owned = new List<(int X, int Z)>();
            var excluded = new HashSet<(int X, int Z)>();
            var held = new List<Mark>();
            foreach (var id in meta.ShapeIds.Where(byId.ContainsKey))
            {
                var shape = byId[id];
                if (shape.Role is not null || shape.Operation == "subtract") continue;
                var covered = RasterShape(shape).Select(cell => (cell.X, cell.Z))
                                                .Where(cells.ContainsKey).ToList();
                switch (ScopeOf(shape))
                {
                    case Participation.Exclude:
                        excluded.UnionWith(covered);
                        break;
                    case Participation.Hold:
                        owned.AddRange(covered);
                        var ring = RingOf(shape);
                        if (ring.Count >= 3) held.Add(new AreaMark([.. ring], StatedTop(shape, ring)));
                        break;
                    default:
                        owned.AddRange(covered);
                        break;
                }
            }
            var ground = owned.Distinct().Where(cell => !excluded.Contains(cell)).ToList();
            if (ground.Count == 0) continue;

            // Held shapes go last so one wins its cells outright: a compound's floor is a statement about the
            // map, not a suggestion the terrain averages against.
            var spec = stated.ToSpec(mirrorMode, cx, cz);
            if (held.Count > 0) spec = spec with { Marks = [.. spec.Marks, .. held] };

            var footprint = Footprint.Over(ground, margin: 0);
            solved[islandId] = ReliefSolver.Solve(footprint, spec, warmStart?.Invoke(islandId, footprint));
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
        // An erected shape adds its footprint but does NOT decide its own height here — that is settled after
        // the relief, against the ground it stands on. Contributing its full thickness now would make it part
        // of that ground, so a raise would read its own plate and stand proud of itself.
        if (IsErected(s))
        {
            foreach (var (x, z) in RasterRing(ring)) yield return (x, z, floor + 1, floor);
            yield break;
        }
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
        // A path arrives as a centerline and a half-width; the band around it is the ring, so nothing below
        // this point learns a shape that is not a ring.
        "path" => PathBand.Ring(s.Vertices ?? [], s.Radius ?? 0, ParsePathEdge(s.PathEdge), s.PathSeed ?? 0),
        _ => [],
    };

    // How a shape's ground takes part in its island's relief. An erected shape does not get a say: it already
    // stands out of the field, and raise/sink read the ground under their own footprint to know where to stand.
    private static Participation ScopeOf(SketchShape s) => IsErected(s) ? Participation.Inherit : s.ReliefScope switch
    {
        "hold"    => Participation.Hold,
        "exclude" => Participation.Exclude,
        _         => Participation.Inherit,
    };

    // The top a held shape pins the field to, read the same way RasterShape reads it so the shape lands where
    // it drew itself. One level, sampled at the ring's centre: holding is what a floor does, and a floor that
    // followed a per-vertex tilt would be the slope it was declared to replace. An EXCLUDED shape keeps its
    // own column instead, tilt and all, because it never enters the solve at all.
    private static int StatedTop(SketchShape s, List<double[]> ring)
    {
        var floor = Math.Max(0, (int)Math.Round(s.Floor ?? 0));
        var centreX = ring.Average(point => point[0]);
        var centreZ = ring.Average(point => point[1]);
        return floor + Math.Max(1, (int)Math.Round(HeightFn(s)(centreX, centreZ)));
    }

    private static PathEdge ParsePathEdge(string? edge) => edge switch
    {
        "rough"   => Geom.Algorithms.PathEdge.Rough,
        "tapered" => Geom.Algorithms.PathEdge.Tapered,
        _         => Geom.Algorithms.PathEdge.Solid,
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
                HeightMode = s.HeightMode, Skirt = s.Skirt, ReliefScope = s.ReliefScope, Theme = s.Theme,
            };
        }
        // Rectangle/circle/path: flatten the transformed footprint to a polygon (uniform height carried). A
        // path mirrors as its band rather than as its centerline: a reflection reverses handedness, so
        // re-deriving the band on the far side would swap which edge a rough or tapered width was drawn on.
        var ring = RingOf(s).Select(p => { var (x, z) = MirrorPoint(p[0], p[1], axis, cx, cz); return new[] { x, z }; }).ToArray();
        return new SketchShape
        {
            Id = s.Id, Type = "polygon", Operation = s.Operation, Override = s.Override,
            Vertices = ring, BaseHeight = s.BaseHeight, Floor = s.Floor,
            HeightMode = s.HeightMode, Skirt = s.Skirt, ReliefScope = s.ReliefScope, Theme = s.Theme,
        };
    }

    // The one canonical concrete-axis transform — every orbit axis stays consistent with the generator + JS.
    private static (double, double) MirrorPoint(double x, double z, string axis, double cx, double cz)
        => Symmetry.Apply(x, z, axis, cx, cz);
}
