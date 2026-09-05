using PgmStudio.Domain;
using PgmStudio.Geom;
using PgmStudio.Geom.Algorithms;
using PgmStudio.Geom.Relief;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Sketch;

// Rasterizes a sketch layout (the sketch_layout_json blob) into the solid block cells of the finished world
// (docs/tools/sketch.md). Pure: no DOM, no DB. Rasterize yields the (x,z) footprint; RasterizeColumns also
// carries each cell's vertical span [YFloor, YTop], where Floor is the shape's elevation and Height its
// thickness: YTop = base_y + floor + height. Height is a uniform base_height, or, for a polygon or lasso
// whose anchor_heights line up with its vertices, a per-vertex thickness TIN-interpolated across the
// footprint through Triangulation. Mirrors the JS geometry it must agree with (circle = 64-gon, Bézier = 16
// samples per edge); per-group mirror copies follow the saved group shapeIds.

/// <summary>Two shapes on one layer where the world holds only the upper: <paramref name="Lost"/> is the
/// shape whose ground is not in the built board, <paramref name="Kept"/> the one that replaced it.</summary>
public readonly record struct StackedShapes(string Layer, string Lost, string Kept);

/// <summary>A polyline whose offset band crosses itself, with a cell the crossing covers.</summary>
public readonly record struct LappedStroke(string Layer, string Shape, int X, int Z);

/// <summary>A tilted shape whose climb ends at a drop: which end (<c>high</c> or <c>low</c>), the cell of the
/// last tread the walk was taken from, the course it stands at and how far the ground beyond it falls.</summary>
public readonly record struct FlightEnd(string Layer, string Shape, string End, int X, int Z, int Top, int Drop);

/// <summary>Two layers driven into each other, the deepest they meet, and a column where they do.
/// <see cref="Courses"/> is that depth in blocks and <see cref="Cells"/> counts every column the two contest,
/// so a reader can tell a slab clipping a corner from two drawn through one another.</summary>
public readonly record struct OverlappingLayers(string Lower, string Upper, int Courses, int X, int Z, int Cells);

/// <summary>An add drawn over ground a subtract takes away, and what the algebra does with it.
/// <see cref="Survives"/> says which of the two silences this is: false where the add draws nothing at all,
/// true where it puts the ground back. <see cref="Cells"/> counts the columns the two contest and
/// <see cref="X"/>/<see cref="Z"/> name the northmost of them, so the pair can be flown to.</summary>
public readonly record struct AddOverSubtract(string Add, string AddLayer, string Subtract, string SubtractLayer,
                                              bool Survives, int Cells, int X, int Z);

/// <summary>A mass of standable ground nothing joins to the main one: how many places it holds, and the
/// lowest-then-northmost of them, so it can be flown to and looked at.</summary>
public readonly record struct DetachedMass(int Places, int X, int Z, int Y);

/// <summary>An override add whose stated top its group's relief will solve straight through: the shape, the
/// layer it is on, the group whose relief overrules it, and the top it asked for.</summary>
public readonly record struct ReliefOverTop(string Shape, string Layer, string Group, int Top);

/// <summary>Two shapes on one layer where one builds the ground and the other paints it: the taller
/// <see cref="Built"/> wins the column, the smaller <see cref="Painted"/> wins the theme, and the world holds
/// one shape's blocks in the other's material. <see cref="Cells"/> counts the columns they contest and
/// <see cref="X"/>/<see cref="Z"/> name the northmost.</summary>
public readonly record struct PaintedByAnother(string Layer, string Built, string Painted,
                                               string BuiltTheme, string PaintedTheme, int Cells, int X, int Z);

public static class SketchRasterizer
{
    /// <summary>How many blocks two layers may share before they are driven into each other rather than
    /// seamed. One: a layer's span is inclusive of its top, so an upper layer sitting at the lower one's top
    /// shares that course and nothing else.</summary>
    private const int SeamCourses = 1;

    /// <summary>The tallest step, up or down, that counts as the ground joining itself for
    /// <see cref="DetachedMasses"/>. Two, because that is the thinnest slab the rasterizer builds: a layer
    /// stacked on another raises the standing surface by two blocks at least, so a smaller bound would call
    /// every stacked board detached — measured, it shreds `opus5-undercroft` into 41 masses over 7,766
    /// places, and at two the same board is one.</summary>
    private const int JoinedRise = 2;

    private const int CirclePoints  = 64;   // matches JS geometry/shape.js CIRCLE_POINTS
    private const int BezierSamples  = 16;   // matches JS geometry/shape.js BEZIER_SAMPLES

    /// <summary>The finished world's solid (x,z) footprint (primary + opted-in group mirror copies).</summary>
    public static List<(int X, int Z)> Rasterize(string layoutJson)
        => RasterizeColumns(layoutJson).Select(c => (c.X, c.Z)).Distinct().ToList();

    /// <summary>As <see cref="Rasterize"/>, but each cell also carries its column span <c>[YFloor, YTop]</c>
    /// and the layer that drew it. Height never affects membership — the footprint is identical to
    /// <see cref="Rasterize"/>, and a cell on two layers answers once per layer.</summary>
    public static List<ColumnSegment> RasterizeColumns(string layoutJson)
        => RasterizeColumns(SketchLayout.Parse(layoutJson));

    /// <summary>As above, off a layout already read. What a gate holding the document takes, so a check and
    /// the build it describes rasterize the same board rather than parsing it twice.</summary>
    public static List<ColumnSegment> RasterizeColumns(SketchLayout? state)
    {
        var cx = state?.Setup?.Center?.Cx ?? 0;
        var cz = state?.Setup?.Center?.Cz ?? 0;
        var axes = Symmetry.OrbitAxes(state?.Setup?.MirrorMode ?? "rot_180");

        // Stack every layer: each is rasterized in its own Y, then shifted by base_y. (x,z) may repeat
        // across layers — a column with a gap (e.g. ground + a sky bridge) keeps both segments.
        var output = new List<ColumnSegment>();
        foreach (var layer in ResolveLayers(state))
        {
            int by = (int)Math.Round(layer.BaseY);
            foreach (var kv in RasterizeLayout(layer.Layout ?? new SketchShapes(), cx, cz, axes,
                                               state?.Relief, state?.Setup?.MirrorMode))
                output.Add(new ColumnSegment(kv.Key.Item1, kv.Key.Item2,
                                            kv.Value.Floor + by, kv.Value.Top + by, layer.Id!));
        }
        return Seat(state, output);
    }

    /// <summary>Every made thing that seats on the ground, moved down onto it, and the terrain over its seat
    /// taken out.
    ///
    /// <para>The seat is the <b>lowest</b> solid column under the columns the thing <b>rests on</b> — those
    /// whose own span starts at its lowest course — one course down, so a sculpture settles into a slope
    /// rather than perching on its high corner. Reading the whole shadow instead would seat a thing on
    /// whatever its overhang happens to pass over: a crane's jib reaching out across a harbour would find the
    /// seabed and take the crane down to it. This is the seat the dressing pass already takes for a placed
    /// prop (<c>docs/world-export/decoration.md</c> §8), which reads a prop's resting course for the same
    /// reason. Every footprint column is then cleared from the seated course up, which is what lets a made
    /// thing dig into a bank instead of having the bank stand through it, and the ground outside its
    /// footprint keeps its height.</para>
    ///
    /// <para><b>One seat per made thing, never one per layer.</b> A sculpture's column runs are split across
    /// as many layers as its busiest column is deep, and seating each of those on its own footprint would
    /// move them by different amounts and take the thing apart. Layers naming the same <c>prop</c> are seated
    /// together, over the union of what they cover; a layer naming none is its own thing.</para></summary>
    private static List<ColumnSegment> Seat(SketchLayout? state, List<ColumnSegment> segments)
    {
        var layers = ResolveLayers(state);
        var seated = layers.Where(layer => layer.SeatsOnGround).ToList();
        if (seated.Count == 0) return segments;

        var thingOf = seated.ToDictionary(layer => layer.Id!,
                                          layer => layer.PartOf is { Length: > 0 } thing ? thing : layer.Id!,
                                          StringComparer.Ordinal);

        // The ground every seat is measured against is what the thing is not: terrain, and any made thing
        // that states its own absolute height.
        var groundTop = new Dictionary<(int X, int Z), int>();
        foreach (var segment in segments)
            if (!thingOf.ContainsKey(segment.Layer))
                groundTop[segment.Cell] = Math.Max(groundTop.GetValueOrDefault(segment.Cell, int.MinValue),
                                                   segment.YTop);

        // Per made thing: the columns it covers, the lowest floor it states over them, and the floor each
        // column's own span starts at — the last is what separates the feet from the overhang.
        var footprint = new Dictionary<string, HashSet<(int X, int Z)>>(StringComparer.Ordinal);
        var lowest = new Dictionary<string, int>(StringComparer.Ordinal);
        var startsAt = new Dictionary<(string Thing, int X, int Z), int>();
        foreach (var segment in segments)
        {
            if (!thingOf.TryGetValue(segment.Layer, out var thing)) continue;
            if (!footprint.TryGetValue(thing, out var cells)) footprint[thing] = cells = [];
            cells.Add(segment.Cell);
            lowest[thing] = Math.Min(lowest.GetValueOrDefault(thing, int.MaxValue), segment.YFloor);
            var key = (thing, segment.Cell.X, segment.Cell.Z);
            startsAt[key] = Math.Min(startsAt.GetValueOrDefault(key, int.MaxValue), segment.YFloor);
        }

        var drop = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (thing, cells) in footprint)
        {
            var rests = cells.Where(cell => startsAt[(thing, cell.X, cell.Z)] == lowest[thing]).ToList();
            var under = rests.Where(groundTop.ContainsKey).Select(cell => groundTop[cell]).ToList();
            if (under.Count == 0) continue;                       // nothing to seat on — SK16 says so
            drop[thing] = under.Min() - 1 - lowest[thing];
        }
        if (drop.Count == 0) return segments;

        // The course each thing's footprint is cleared from, so the bank it digs into stops there.
        var cutTo = new Dictionary<(int X, int Z), int>();
        foreach (var (thing, cells) in footprint)
        {
            if (!drop.TryGetValue(thing, out var moved)) continue;
            var floor = lowest[thing] + moved;
            foreach (var cell in cells)
                cutTo[cell] = Math.Min(cutTo.GetValueOrDefault(cell, int.MaxValue), floor);
        }

        var settled = new List<ColumnSegment>(segments.Count);
        foreach (var segment in segments)
        {
            if (thingOf.TryGetValue(segment.Layer, out var thing))
            {
                var moved = drop.GetValueOrDefault(thing, 0);
                settled.Add(segment with { YFloor = segment.YFloor + moved, YTop = segment.YTop + moved });
                continue;
            }
            if (!cutTo.TryGetValue(segment.Cell, out var top) || segment.YTop <= top)
            {
                settled.Add(segment);
                continue;
            }
            if (segment.YFloor < top) settled.Add(segment with { YTop = top });
        }
        return settled;
    }

    /// <summary>The columns covered by every shape that says it is not ground to dress
    /// (<see cref="SketchShape.KeepClear"/>), over every layer and through the symmetry fan — what the
    /// dressing pass takes as a keep-out so a road does not repaint a town wall's top course and a channel
    /// does not cut one down to its water line.
    ///
    /// <para>The marked shapes are rasterized on their own, so what comes back is their footprint rather
    /// than what survives the set algebra of the whole layer: a shape that says keep off means the ground it
    /// covers, whatever is later drawn over it. Group membership still decides the fan, so a marked shape on
    /// a mirrored group keeps its images clear too.</para></summary>
    public static HashSet<(int X, int Z)> KeepClearCells(SketchLayout? state)
    {
        var cx = state?.Setup?.Center?.Cx ?? 0;
        var cz = state?.Setup?.Center?.Cz ?? 0;
        var axes = Symmetry.OrbitAxes(state?.Setup?.MirrorMode ?? "rot_180");

        var kept = new HashSet<(int X, int Z)>();
        foreach (var layer in ResolveLayers(state))
        {
            if (layer.Layout is not { } layout) continue;
            var marked = layout.Shapes.Where(shape => shape.KeepClear).ToList();
            if (marked.Count == 0) continue;
            var only = new SketchShapes { Shapes = marked, Groups = layout.Groups };
            foreach (var cell in RasterizeLayout(only, cx, cz, axes, state?.Relief, state?.Setup?.MirrorMode).Keys)
                kept.Add(cell);
        }
        return kept;
    }

    /// <summary>Every relief-bearing group's solved surface, keyed by group id, with each field's heights already
    /// shifted into world Y by its layer's <c>base_y</c>. This is the same solve the build runs, from the same
    /// entry point, so a preview drawn from it cannot show a surface the world will not have — which is the only
    /// reason a preview is worth drawing. <para>A group appears once. A layout that names the same group on two
    /// layers is malformed, and showing the lower of the two would be a quieter wrong answer than showing the
    /// first.</para>
    /// <para><b>warmStart</b> — The surface each group's last solve settled on, asked for by group id and
    /// footprint. Resuming from it is a head start and never a different answer — the solver discards a resume
    /// that fails to settle — so a caller with nothing to offer simply omits this.</para>
    /// <para><b>remember</b> — Handed each group's solve as the solver produced it, for a caller keeping surfaces
    /// to resume from. It is the <b>unshifted</b> field, and the pairing is the point: what comes back from this
    /// method has its layer's <c>base_y</c> added, and feeding that back as a head start would seed the next
    /// solve a whole layer too high.</para></summary>
    public static Dictionary<string, HeightField> ReliefFields(
        string layoutJson, Func<string, Footprint, double[]?>? warmStart = null,
        Action<string, HeightField>? remember = null)
    {
        var state = SketchLayout.Parse(layoutJson);
        if (state?.Relief is not { Count: > 0 } relief) return [];

        var cx = state.Setup?.Center?.Cx ?? 0;
        var cz = state.Setup?.Center?.Cz ?? 0;

        var fields = new Dictionary<string, HeightField>();
        foreach (var layer in ResolveLayers(state))
        {
            var shapes = layer.Shapes;
            if (shapes.Count == 0) continue;

            var shift = (int)Math.Round(layer.BaseY);
            foreach (var (groupId, field) in SolveRelief(RasterGroup(shapes), shapes, layer.Groups,
                                                          relief, state.Setup?.MirrorMode, cx, cz, warmStart))
            {
                if (!fields.ContainsKey(groupId)) remember?.Invoke(groupId, field);
                fields.TryAdd(groupId, shift == 0 ? field : new HeightField(field.Footprint,
                    [.. field.Continuous.Select(height => height + shift)],
                    [.. field.Blocks.Select(height => height + shift)]));
            }
        }
        return fields;
    }

    /// <summary>Maps every cell an add shape covers to the id of the group that shape belongs to, later
    /// layers over earlier ones — what a gate needs to speak about the relief a cell's ground is solved
    /// under, since a relief is keyed on its group. A cell no grouped shape covers is absent.</summary>
    public static Dictionary<(int X, int Z), string> GroupOwners(string layoutJson)
    {
        var state = SketchLayout.Parse(layoutJson);
        var groupOfShape = new Dictionary<(string Layer, string Shape), string>();
        foreach (var layer in SketchLayout.Stack(state))
            foreach (var group in layer.Groups)
                foreach (var shapeId in group.ShapeIds)
                    if (group.Id is { } groupId) groupOfShape[(layer.Id!, shapeId)] = groupId;

        var owners = new Dictionary<(int X, int Z), string>();
        foreach (var ((layer, x, z), shapeId) in ShapeScopeOwners(layoutJson, _ => true))
            if (groupOfShape.TryGetValue((layer, shapeId), out var groupId)) owners[(x, z)] = groupId;
        return owners;
    }

    /// <summary>Maps every cell a <em>painted</em> shape covers, on the layer that covers it, to that shape's
    /// id — the scope <c>TerrainThemeScope</c> resolves a cell's paint through. A shape is painted when it
    /// states a theme or a material: the two are one question — what covers this cell — answered at two
    /// grains, so they resolve an overlap through one traversal and by one rule.</summary>
    public static Dictionary<(string Layer, int X, int Z), string> ShapeThemeOwners(string layoutJson)
        => ShapeScopeOwners(layoutJson, shape => shape.Theme is not null || shape.Material is not null);

    /// <summary>Maps every cell a scoped shape covers, keyed by the layer it covers it on, to that shape's
    /// id — the primary footprint plus each
    /// mirroring group's orbit copies (which keep the shape id), the smallest-area shape winning an overlap
    /// (the most specific scope). <paramref name="isScope"/> says which annotation makes a shape a scope, so
    /// paint and planting resolve through one traversal rather than two that could disagree about which shape
    /// owns a contested cell — and each caller keeps its own rule for what counts, since what makes a shape a
    /// paint scope and what makes it a planting one are not the same question. Only add shapes the predicate
    /// answers for are considered; subtracts and role-tagged (structural) shapes are skipped — they place no
    /// terrain of their own. Void cells that no surface stands on are harmless: a consumer only reads owners
    /// where a column is solid.</summary>
    public static Dictionary<(string Layer, int X, int Z), string> ShapeScopeOwners(
        string layoutJson, Func<SketchShape, bool> isScope)
    {
        var state = SketchLayout.Parse(layoutJson);
        var cx = state?.Setup?.Center?.Cx ?? 0;
        var cz = state?.Setup?.Center?.Cz ?? 0;
        var axes = Symmetry.OrbitAxes(state?.Setup?.MirrorMode ?? "rot_180");

        var owner = new Dictionary<(string, int, int), string>();
        var areaOf = new Dictionary<(string, int, int), long>();
        var layerId = "";

        void Claim(SketchShape s)
        {
            if (!isScope(s) || s.Operation == "subtract" || s.Role is not null) return;
            var cells = RasterShape(s).Select(c => (layerId, c.X, c.Z)).ToList();
            long area = cells.Count;
            foreach (var cell in cells)
                if (!owner.ContainsKey(cell) || area < areaOf[cell]) { owner[cell] = s.Id; areaOf[cell] = area; }
        }

        foreach (var layer in ResolveLayers(state))
        {
            // A cell contested on one layer goes to the smallest-area shape covering it; a cell covered on
            // two layers is not contested at all, because each layer shows its own surface.
            layerId = layer.Id!;
            var shapes = layer.Shapes;
            foreach (var s in shapes) Claim(s);                             // primary footprint

            var metas = layer.Groups;
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

    // Layers to rasterize, in draw order — read through the document's one stack reader.
    private static IReadOnlyList<SketchLayer> ResolveLayers(SketchLayout? state) => SketchLayout.Stack(state);

    // One layer → its solid (x,z) cells with layer-local columns (primary + opted-in group mirror copies).
    private static Dictionary<(int, int), (int Top, int Floor)> RasterizeLayout(
        SketchShapes layout, double cx, double cz, IReadOnlyList<string> axes,
        Dictionary<string, SketchReliefJson>? relief = null, string? mirrorMode = null)
    {
        var shapes = layout?.Shapes ?? [];
        if (shapes.Count == 0) return [];

        var cells = RasterGroup(shapes, out var claimed);        // primary
        var metas = layout?.Groups ?? [];

        // Interior elevation, per group, over the cells the set algebra actually left standing — so a relief
        // never re-adds ground a subtract took away. The solved surface replaces the column's top and leaves
        // its floor alone: a relief says where the ground is, not how thick the slab under it is.
        var solved = SolveRelief(cells, shapes, metas, relief, mirrorMode, cx, cz);
        foreach (var (groupId, field) in solved)
            foreach (var (x, z) in field.Footprint.Land())
                if (cells.TryGetValue((x, z), out var column))
                    cells[(x, z)] = (Math.Max(column.Floor + 1, field.At(x, z)), column.Floor);

        // Erected shapes go on AFTER the relief, which is the whole of what makes them erected: a shape that
        // declares how its top is decided is one meant to stand OUT of the field rather than be part of it,
        // and it can only stand out of ground that already exists.
        Erect(cells, shapes, claimed);

        if (metas.Count == 0)
        {
            // No group metadata (hand-authored): mirror the whole primary footprint (height is invariant).
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
                var groupShapes = meta.ShapeIds.Where(byId.ContainsKey).Select(id => byId[id]).ToList();
                var field = meta.Id is { Length: > 0 } id ? solved.GetValueOrDefault(id) : null;
                foreach (var axis in axes)
                {
                    var mirrored = groupShapes.Select(s => MirrorShape(s, axis, cx, cz)).ToList();
                    var copy = RasterGroup(mirrored, out var mirroredClaims);
                    // A mirrored copy of a relief-bearing group takes its heights from the group's own
                    // solved surface, read back through the transform that undoes the one that placed it —
                    // exactly symmetric by construction, rather than symmetric to within a second solve's
                    // tolerance. The inverse is what a quarter-turn needs: a rot_90 image read back through
                    // rot_90 lands on the rot_180 image's ground, which the field does not cover, so the
                    // copy keeps its shapes' flat base heights and one pair of teams plays a solved surface
                    // while the other pair plays a table.
                    var back = Symmetry.Inverse(axis);
                    if (field is not null)
                        foreach (var cell in copy.Keys.ToList())
                        {
                            var source = MirrorCell(cell, back, cx, cz);
                            if (!field.Has(source.Item1, source.Item2)) continue;
                            copy[cell] = (Math.Max(copy[cell].Floor + 1, field.At(source.Item1, source.Item2)),
                                          copy[cell].Floor);
                        }
                    // The image gets the same pass over it the primary got, and in the same order. An erected
                    // shape is settled against the ground under it, so reading its height back through the
                    // mirror would give it the relief's answer instead of its own — one team a mesa and the
                    // other a hillside.
                    Erect(copy, mirrored, mirroredClaims);
                    Merge(cells, copy, mirroredClaims, claimed);
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
    private static void Erect(Dictionary<(int, int), (int Top, int Floor)> cells, List<SketchShape> shapes,
                              HashSet<(int, int)>? claimed = null)
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

            // Ties go one way, always. The surface is sampled at the cell's centre, so a ramp at one
            // course a cell — the most natural gradient there is — lands EVERY sample exactly on a half,
            // and .NET's default rounding is to-even: the courses come out as a beat of noughts and twos
            // rather than as a stair of ones, and a two-block rise costs a placed block to climb. Rounding
            // ties away from zero moves only the samples that were on a tie, and moves them all alike.
            int Stated(int x, int z) =>
                datum + rise * Math.Max(1, (int)Math.Floor(surface(x + 0.5, z + 0.5)));

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
                // A shape that says how its top is decided has claimed the column as surely as an override
                // add has: the settled top is not the shape's stated one, so a merge that re-reads the
                // stated top — a group's own reflection, most of all — must not win it back.
                claimed?.Add(cell);
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

    /// <summary>The height a room should be level at, read off a surface solved without its pin: the median of
    /// the ground **immediately outside its doors**, or of the ground under the room where it states none.
    /// Null where neither is on the group, which leaves the stated height as the only answer there is.
    ///
    /// <para>The door and not the whole footprint, because a room is a level rectangle that can never slope
    /// while the ground it sits in can. A room whose approach runs downhill across it has no single height
    /// that suits every side: seating on the middle of the footprint splits the difference and leaves a step
    /// at the door as well as at the back, where seating on the door leaves the way in and out flush and puts
    /// the whole of the difference behind the room, which is the side nobody walks.</para>
    ///
    /// <para>The median rather than the mean or an extreme: a door spans several cells and one of them may sit
    /// on a boulder-sized wrinkle in the grain, which a mean would carry into the room's floor and a min or max
    /// would take as the answer.</para></summary>
    private static int? SeatOf(SketchShape shape, List<(int X, int Z)> covered, HeightField field,
                               Footprint footprint)
    {
        var doors = (shape.Doors ?? []).Select(RoomEdges.OfWord).Where(edge => edge is not null)
                                       .Select(edge => edge!.Value).Distinct().ToList();
        // A room stating no door is entered from wherever the ground reaches it, so every side is read.
        // The ground under the footprint is the wrong answer here and is only the last resort: it splits the
        // difference and leaves a step at the door as well as at the back, which is the whole reason the
        // height is read outside the room in the first place.
        if (doors.Count == 0) doors = [.. Enum.GetValues<RoomEdge>()];
        var inside = new HashSet<(int X, int Z)>(covered);

        var outside = new List<int>();
        foreach (var edge in doors)
        {
            var (stepX, stepZ) = edge.Outward();
            foreach (var (x, z) in covered)
            {
                var (ax, az) = (x + stepX, z + stepZ);
                if (inside.Contains((ax, az)) || !footprint.Inside(ax, az)) continue;
                outside.Add(field.At(ax, az));
            }
        }
        if (outside.Count > 0) { outside.Sort(); return outside[outside.Count / 2]; }

        // Nothing outside it at all — a room filling its own group. Its own ground is all there is to read.
        var under = covered.Where(cell => footprint.Inside(cell.X, cell.Z))
                           .Select(cell => field.At(cell.X, cell.Z)).ToList();
        if (under.Count == 0) return null;
        under.Sort();
        return under[under.Count / 2];
    }

    /// <summary>Each relief-bearing group's solved surface, over the cells that group actually contributes
    /// to the standing footprint. A group with no relief is absent, which is the common case and costs
    /// nothing.</summary>
    private static Dictionary<string, HeightField> SolveRelief(
        Dictionary<(int, int), (int Top, int Floor)> cells, List<SketchShape> shapes, List<SketchGroup> metas,
        Dictionary<string, SketchReliefJson>? relief, string? mirrorMode, double cx, double cz,
        Func<string, Footprint, double[]?>? warmStart = null)
    {
        var solved = new Dictionary<string, HeightField>();
        if (relief is not { Count: > 0 }) return solved;

        var byId = shapes.GroupBy(s => s.Id).ToDictionary(g => g.Key, g => g.First());
        foreach (var meta in metas)
        {
            if (meta.Id is not { Length: > 0 } groupId) continue;
            if (!relief.TryGetValue(groupId, out var stated)) continue;

            // The group's own ground: the cells its add-shapes cover that survived the layer's set algebra,
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

            // A structural annotation (a spawn or wool room, S25) is never listed in a group's own
            // ShapeIds — that list is read elsewhere as the group's terrain rings — so a room binds by
            // footprint instead: if the room it marks overlaps ground this group already owns, a stated
            // relief_scope applies to it exactly as it would an ordinary shape's. RasterGroup still skips
            // the annotation itself, so it never draws terrain of its own; this only lets it pin or hole
            // the terrain that was already there.
            var groundSoFar = new HashSet<(int X, int Z)>(owned);
            var seated = new List<(SketchShape Shape, List<(int X, int Z)> Covered)>();
            foreach (var shape in shapes)
            {
                if (shape.Role is null || shape.ReliefScope is not ("hold" or "exclude")) continue;
                var covered = RasterShape(shape).Select(cell => (cell.X, cell.Z))
                                                .Where(cells.ContainsKey).ToList();
                if (covered.Count == 0 || !covered.Any(groundSoFar.Contains)) continue;
                if (shape.ReliefScope == "exclude") { excluded.UnionWith(covered); continue; }
                owned.AddRange(covered);
                var ring = RingOf(shape);
                if (ring.Count < 3) continue;
                // A room whose height the author has corrected states it; one that has not is still carrying
                // the plan's flat number and is seated on the terrain below, once there is terrain to read.
                // Either way the mark is rigid: what it pins is a floor, and the sculpting passes may not
                // tilt a floor.
                if (shape.HeightAuthored == true)
                    held.Add(new AreaMark([.. ring], StatedTop(shape, ring)) { Rigid = true });
                else seated.Add((shape, covered));
            }

            var ground = owned.Distinct().Where(cell => !excluded.Contains(cell)).ToList();
            if (ground.Count == 0) continue;

            // Held shapes go last so one wins its cells outright: a compound's floor is a statement about the
            // map, not a suggestion the terrain averages against.
            var spec = stated.ToSpec(mirrorMode, cx, cz);
            if (held.Count > 0) spec = spec with { Marks = [.. spec.Marks, .. held] };

            var footprint = Footprint.Over(ground, margin: 0);
            var field = ReliefSolver.Solve(footprint, spec, warmStart?.Invoke(groupId, footprint));

            // A room that has not been corrected takes its height from the surface just solved for it, and the
            // group is solved again holding it there. A plan-space piece states its height before any terrain
            // exists, so the number it carries is about a flat board; leaving it alone puts a spawn door
            // against a wall the relief built around it, and a player walks out into rock.
            if (seated.Count > 0)
            {
                foreach (var (shape, covered) in seated)
                {
                    var ring = RingOf(shape);
                    held.Add(SeatOf(shape, covered, field, footprint) is { } seat
                        ? new AreaMark([.. ring], seat) { Rigid = true }
                        : new AreaMark([.. ring], StatedTop(shape, ring)) { Rigid = true });
                }
                var reseated = stated.ToSpec(mirrorMode, cx, cz);
                spec = reseated with { Marks = [.. reseated.Marks, .. held] };
                field = ReliefSolver.Solve(footprint, spec, field.Continuous);
            }

            solved[groupId] = field;
        }
        return solved;
    }

    /// <summary>Every override add whose stated top its group's relief discards. An override add says the
    /// column is its own, floor and all; a relief solves a surface over every column of its group and
    /// replaces the top of each. Only an <b>erected</b> shape stands out of that field — one naming a
    /// <c>height_mode</c> — and only a <c>relief_scope</c> keeps a shape's ground out of the solve, so an
    /// override add carrying neither builds to whatever the field says and not to what it stated.
    ///
    /// <para>Judged per group, since a relief is keyed on one: a shape listed in a group the document
    /// carries no relief for is not in this. Plain adds are not either — a relief shaping ordinary terrain is
    /// what a relief is for. It is the <em>override</em> that is the statement being overruled.</para>
    ///
    /// <para>And a top has to have been <b>stated</b> to be discarded: an override add carrying neither
    /// <c>base_height</c>, <c>floor</c> nor <c>anchor_heights</c> is a footprint holding a theme, and the
    /// relief shaping the ground under it is what such a shape is drawn for.</para></summary>
    public static List<ReliefOverTop> ReliefOverridesStatedTop(SketchLayout? state)
    {
        var found = new List<ReliefOverTop>();
        if (state?.Relief is not { Count: > 0 } relief) return found;

        foreach (var layer in ResolveLayers(state))
        {
            var groupOf = new Dictionary<string, string>();
            foreach (var group in layer.Groups)
                if (group.Id is { Length: > 0 } id && relief.ContainsKey(id))
                    foreach (var shapeId in group.ShapeIds)
                        groupOf[shapeId] = id;
            if (groupOf.Count == 0) continue;

            foreach (var shape in layer.Shapes)
            {
                if (!shape.Override || shape.Operation == "subtract" || shape.Role is not null) continue;
                if (IsErected(shape) || shape.ReliefScope is "hold" or "exclude") continue;
                if (shape.BaseHeight is null && shape.Floor is null && shape.AnchorHeights is null) continue;
                if (!groupOf.TryGetValue(shape.Id, out var groupId)) continue;
                var floor = Math.Max(0, (int)Math.Round(shape.Floor ?? 0));
                found.Add(new ReliefOverTop(shape.Id, layer.Id ?? "", groupId,
                                            floor + Math.Max(1, (int)Math.Round(shape.BaseHeight ?? 1))));
            }
        }
        return found;
    }

    /// <summary>Every pair of override adds on one layer where one shape's blocks come out in another's
    /// material. Two override adds over a column is not a fault in itself — the taller wins it, which is what
    /// "the tallest add is the height" means — but a theme is scoped by <b>area</b> rather than by height
    /// (<see cref="ShapeThemeOwners"/>), so where the smaller of the two is also the shorter, the world holds
    /// the taller shape's ground painted in the smaller one's theme. A mound's ring crossing a wall leaves the
    /// wall standing to its own courses and finished in grass over dirt.
    ///
    /// <para>Only pairs that differ in <em>both</em> theme and stated top are in it: two shapes at one height
    /// are a theme scoped to a patch, which is what scoping is for, and two sharing a theme cannot disagree
    /// about paint. One entry per pair.</para>
    ///
    /// <para><b>The images count.</b> A shape in a mirroring group stands on the board once for every axis
    /// of the orbit, and what a patch contests is as often another patch's <em>reflection</em> as the patch
    /// itself — a dais laid clear of a court on the half it is drawn on lands in the middle of it on the
    /// other. The image carries its shape's theme and top, so it is judged as that shape and reported under
    /// its id.</para></summary>
    public static List<PaintedByAnother> PaintedByAnotherShape(SketchLayout? state)
    {
        var found = new List<PaintedByAnother>();
        var axes = Symmetry.OrbitAxes(state?.Setup?.MirrorMode ?? "rot_180");
        double centerX = state?.Setup?.Center?.Cx ?? 0, centerZ = state?.Setup?.Center?.Cz ?? 0;

        foreach (var layer in ResolveLayers(state))
        {
            var fanned = new HashSet<string>(layer.Groups.Where(group => group.Mirrors)
                                                          .SelectMany(group => group.ShapeIds),
                                             StringComparer.Ordinal);
            var adds = layer.Shapes
                .Where(shape => shape.Override && shape.Operation != "subtract"
                             && shape.Role is null && shape.Theme is { Length: > 0 })
                .Select(shape => (shape.Id, shape.Theme!,
                                  Top: Math.Max(0, (int)Math.Round(shape.Floor ?? 0))
                                     + Math.Max(1, (int)Math.Round(shape.BaseHeight ?? 1)),
                                  Cells: Placed(shape, fanned.Contains(shape.Id))))
                .Where(entry => entry.Cells.Count > 0)
                .ToList();

            HashSet<(int X, int Z)> Placed(SketchShape shape, bool mirrors)
            {
                var cells = RasterShape(shape).Select(cell => (cell.X, cell.Z)).ToHashSet();
                if (!mirrors) return cells;
                foreach (var axis in axes)
                    foreach (var (x, z) in cells.ToList())
                    {
                        var (imageX, imageZ) = MirrorCell((x, z), axis, centerX, centerZ);
                        cells.Add((imageX, imageZ));
                    }
                return cells;
            }

            for (var i = 0; i < adds.Count; i++)
            for (var j = i + 1; j < adds.Count; j++)
            {
                var (tall, low) = adds[i].Top >= adds[j].Top ? (adds[i], adds[j]) : (adds[j], adds[i]);
                if (tall.Top == low.Top || tall.Item2 == low.Item2) continue;
                // The smaller shape wins the paint. Where that is the taller one, the two agree.
                if (low.Cells.Count >= tall.Cells.Count) continue;
                var shared = tall.Cells.Where(low.Cells.Contains).ToList();
                if (shared.Count == 0) continue;
                var (x, z) = shared.OrderBy(cell => cell.Z).ThenBy(cell => cell.X).First();
                found.Add(new PaintedByAnother(layer.Id ?? "", tall.Id, low.Id, tall.Item2, low.Item2,
                                               shared.Count, x, z));
            }
        }
        return found;
    }

    /// <summary>Every polyline whose band laps itself. The band is offset either side of the centreline into
    /// ONE ring and a ring is filled even-odd, so a winding that crosses its own neighbour cancels there and
    /// the lap builds as void — which is why a spiral has to be drawn as several strokes rather than one.
    /// Read off the ring the rasterizer itself offsets, so a band that clears itself says nothing.</summary>
    public static List<LappedStroke> StrokesLappingThemselves(SketchLayout? state)
    {
        var found = new List<LappedStroke>();
        foreach (var layer in ResolveLayers(state))
            foreach (var shape in layer.Shapes)
            {
                if (shape.Type != ShapeKinds.Polyline || shape.Role is not null) continue;
                var ring = RingOf(shape);
                if (ring.Count < 6) continue;
                if (SelfCrossing(ring) is not { } at) continue;
                found.Add(new LappedStroke(layer.Id ?? "", shape.Id, (int)Math.Floor(at[0]), (int)Math.Floor(at[1])));
            }
        return found;
    }

    /// <summary>Where a closed ring first crosses itself, or null. Neighbouring edges share an endpoint and
    /// are skipped; everything else is tested as a pair of segments.</summary>
    private static double[]? SelfCrossing(List<double[]> ring)
    {
        var n = ring.Count;
        for (var i = 0; i < n; i++)
        {
            var (a, b) = (ring[i], ring[(i + 1) % n]);
            for (var j = i + 2; j < n; j++)
            {
                if (i == 0 && j == n - 1) continue;                 // the closing edge touches the first
                var (c, d) = (ring[j], ring[(j + 1) % n]);
                if (Crosses(a, b, c, d) is { } hit) return hit;
            }
        }
        return null;
    }

    private static double[]? Crosses(double[] a, double[] b, double[] c, double[] d)
    {
        double rx = b[0] - a[0], rz = b[1] - a[1], sx = d[0] - c[0], sz = d[1] - c[1];
        var denominator = rx * sz - rz * sx;
        if (Math.Abs(denominator) < 1e-12) return null;             // parallel, or both degenerate
        var t = ((c[0] - a[0]) * sz - (c[1] - a[1]) * sx) / denominator;
        var u = ((c[0] - a[0]) * rz - (c[1] - a[1]) * rx) / denominator;
        // Strictly inside both segments: a ring's own vertices touch by construction and are not a crossing.
        if (t <= 1e-9 || t >= 1 - 1e-9 || u <= 1e-9 || u >= 1 - 1e-9) return null;
        return [a[0] + t * rx, a[1] + t * rz];
    }

    /// <summary>How far the walk off a lip looks before giving up. It only ever needs to step over cells of
    /// the flight itself — what it tests is the FIRST cell that is not one, because a cell a player falls
    /// into is not made good by ground on the far side of it.</summary>
    private const int ArrivalReach = 6;

    /// <summary>Every tilted shape whose climb ends at a drop, at either end.
    ///
    /// <para>Nothing in the document says a shape is a flight — a stair, a ramp and a bank are all one polygon
    /// with a height per vertex — so what is read here is the tilt itself: a shape whose built cells are not
    /// all at one course has a high end and a low end, and the line between their two centroids is the
    /// direction it runs. From every cell of an end, the walk steps that way up to
    /// <see cref="ArrivalReach"/> cells and asks for ground within one course of the tread. An end where NO
    /// cell finds any is a flight that arrives nowhere: level with the ground beside it for one cell, and then
    /// the drop it just climbed.</para>
    ///
    /// <para>Read against the layer's own merged tops rather than the shape's raster, because what a flight
    /// arrives on is whatever the layer holds there — the plateau it is cut into, the landing drawn beside it,
    /// or nothing.</para></summary>
    public static List<FlightEnd> FlightsEndingAtADrop(SketchLayout? state)
    {
        var found = new List<FlightEnd>();
        // Read across the whole stack, not one layer. What a flight arrives on is whatever the board holds
        // there at its own course — a rampart walk on the layer above the stair that climbs to it, a gallery
        // floor under the deck it rises through — and a per-layer reading calls every one of those a drop.
        var tops = TopsOfEveryLayer(state);
        if (tops.Count == 0) return found;
        foreach (var layer in ResolveLayers(state))
        {
            // A shape's raster is in its layer's own frame and the stack above is in the world's, so the
            // flight is lifted onto the board before the two are compared.
            var shift = (int)Math.Round(layer.BaseY);
            foreach (var shape in layer.Shapes)
            {
                if (shape.Role is not null || shape.Operation == "subtract" || IsErected(shape)) continue;
                if (shape.AnchorHeights is not { Length: > 1 } anchors) continue;
                if (anchors.Max() - anchors.Min() < 2) continue;         // not a climb worth the name

                var cells = RasterShape(shape)
                    .Select(c => (c.X, c.Z, Top: c.Top + shift, c.Floor)).ToList();
                if (cells.Count < 4) continue;
                var (low, high) = (cells.Min(c => c.Top), cells.Max(c => c.Top));
                if (high - low < 2) continue;

                var mine = cells.ToDictionary(c => (c.X, c.Z), c => c.Top);
                // A stroke says where it runs, so it is not inferred. Its ends are the two ends of its own
                // centreline and the direction at each is that line's tangent — which a band's cells cannot
                // give: a ribbon is level across its width and, on a coil, almost level along it too, so the
                // steepest neighbour is noise and the walk leaves the ramp sideways.
                var ends = EndsOfStroke(shape);
                foreach (var (end, rising) in new[] { ("high", true), ("low", false) })
                {
                    if (ends is { } stroke)
                    {
                        var (at, along) = rising ? stroke.High : stroke.Low;
                        var lipCells = cells
                            .Where(c => Math.Abs(c.X + 0.5 - at.X) <= 2 && Math.Abs(c.Z + 0.5 - at.Z) <= 2)
                            .Select(c => (Tread: c, Step: along)).ToList();
                        if (lipCells.Count == 0) continue;
                        if (Arrival(lipCells, mine, tops) is { } strokeMiss)
                            found.Add(new FlightEnd(layer.Id ?? "", shape.Id, end,
                                                    strokeMiss.X, strokeMiss.Z, strokeMiss.Top, strokeMiss.Drop));
                        continue;
                    }
                    // The lip of an end: the treads at its extreme course that have no more flight in front
                    // of them. A tread with another of its own shape ahead has not ended anywhere, and
                    // walking from one lands on the flight rather than on what the flight arrives at.
                    //
                    // Where "ahead" points is read PER TREAD, from the shape's own surface: the direction a
                    // player was walking as they reached that cell is the steepest climb into it from a
                    // neighbour of the same shape. A curved ribbon has no single bearing — the line between
                    // its two ends runs across the chord rather than along the ramp — so a global direction
                    // walks a spiral off its own side and calls the ground beside it a drop.
                    var edge = cells.Where(c => rising ? c.Top >= high - 1 : c.Top <= low + 1);
                    var lip = new List<((int X, int Z, int Top, int Floor) Tread, (int X, int Z) Step)>();
                    foreach (var tread in edge)
                        if (Travel(tread, mine, rising) is { } step
                            && !mine.ContainsKey((tread.X + step.X, tread.Z + step.Z)))
                            lip.Add((tread, step));
                    if (lip.Count == 0) continue;
                    if (Arrival(lip, mine, tops) is { } miss)
                        found.Add(new FlightEnd(layer.Id ?? "", shape.Id, end,
                                                miss.X, miss.Z, miss.Top, miss.Drop));
                }
            }
        }
        return found;
    }

    /// <summary>The lip tread whose walk found the deepest drop, or null where any of them arrives on ground
    /// within one course. One answer per end: a lip with a way off it somewhere is a flight that arrives, and
    /// only a lip with none at all is the finding — a flight is not obliged to be steppable off along its
    /// whole width, only to have somewhere a player who walked up it can stand.</summary>
    private static (int X, int Z, int Top, int Drop)? Arrival(
        List<((int X, int Z, int Top, int Floor) Tread, (int X, int Z) Step)> lip,
        Dictionary<(int, int), int> own, Dictionary<(int, int), List<int>> tops)
    {
        (int X, int Z, int Top, int Drop)? worst = null;
        foreach (var (tread, step) in lip)
        {
            var reached = false;
            var fall = 0;
            for (var k = 1; k <= ArrivalReach; k++)
            {
                var cell = (tread.X + step.X * k, tread.Z + step.Z * k);
                if (own.ContainsKey(cell)) continue;                      // a cell of the flight itself
                var here = tops.GetValueOrDefault(cell) ?? [0];
                reached = here.Any(there => Math.Abs(there - tread.Top) <= 1);
                if (!reached) fall = tread.Top - here.Max();
                break;                                                    // the first cell off is the answer
            }
            if (reached) return null;                                     // this end has somewhere to go
            if (worst is null || fall > worst.Value.Drop) worst = (tread.X, tread.Z, tread.Top, fall);
        }
        return worst;
    }

    /// <summary>Where a stroke's two ends are and which way it runs at each — the centreline's own endpoints
    /// and tangents, taken off the same spline the band was offset from, so the walk follows the ramp rather
    /// than the raster. Null for anything that is not a graded polyline.</summary>
    private static (((double X, double Z) At, (int X, int Z) Along) High,
                    ((double X, double Z) At, (int X, int Z) Along) Low)? EndsOfStroke(SketchShape s)
    {
        if (s.Type != ShapeKinds.Polyline || s.Vertices is not { Length: >= 2 } drawn) return null;
        if (s.AnchorHeights is not { } anchors || anchors.Length != drawn.Length) return null;
        var line = Centerline.Of([.. drawn.Select(v => new[] { v[0], v[1] })]);
        if (line.Count < 2) return null;

        var last = line.Count - 1;
        var startsHigh = anchors[0] > anchors[^1];
        // Outward at each end: away from the point beside it, so the walk continues off the stroke.
        var atStart = (line[0][0], line[0][1]);
        var atEnd   = (line[last][0], line[last][1]);
        var outStart = Compass(line[0][0] - line[1][0], line[0][1] - line[1][1]);
        var outEnd   = Compass(line[last][0] - line[last - 1][0], line[last][1] - line[last - 1][1]);
        if (outStart is not { } fromStart || outEnd is not { } fromEnd) return null;
        return startsHigh ? ((atStart, fromStart), (atEnd, fromEnd))
                          : ((atEnd, fromEnd), (atStart, fromStart));
    }

    /// <summary>A vector as one of the eight steps a cell walk can take.</summary>
    private static (int X, int Z)? Compass(double dx, double dz)
    {
        var length = Math.Sqrt(dx * dx + dz * dz);
        if (length < 1e-6) return null;
        var step = (Math.Abs(dx) / length < 0.383 ? 0 : Math.Sign(dx),
                    Math.Abs(dz) / length < 0.383 ? 0 : Math.Sign(dz));
        return step == (0, 0) ? null : step;
    }

    /// <summary>Which way a player was walking as they reached this tread, read off the shape's own surface:
    /// of the eight neighbours belonging to the same shape, the one the climb into the tread is steepest
    /// from. Continuing that way is walking on. Null where no neighbour of the shape is lower (for a high
    /// end) or higher (for a low one), which is a tread at neither end of anything.</summary>
    private static (int X, int Z)? Travel((int X, int Z, int Top, int Floor) tread,
                                          Dictionary<(int, int), int> own, bool rising)
    {
        (int X, int Z)? best = null;
        var steepest = 0.0;
        for (var dx = -1; dx <= 1; dx++)
        for (var dz = -1; dz <= 1; dz++)
        {
            if (dx == 0 && dz == 0) continue;
            if (!own.TryGetValue((tread.X - dx, tread.Z - dz), out var behind)) continue;
            // Per cell walked, not per neighbour: a diagonal step covers √2 cells for the same climb, so a
            // flight running down an axis reads as running down that axis rather than cornering off it. That
            // matters, because what a flight has to arrive on is what is in front of a player walking up it
            // — ground reachable by turning at the lip is the plateau it was cut into, not a landing.
            var climb = (rising ? tread.Top - behind : behind - tread.Top)
                        / (dx != 0 && dz != 0 ? Math.Sqrt(2) : 1);
            if (climb <= steepest) continue;
            (steepest, best) = (climb, (dx, dz));
        }
        return best;
    }

    /// <summary>Every course the board stands at, cell by cell — one entry per layer that holds the cell,
    /// each already shifted into world Y. A stacked column is several somewheres rather than one, so a walk
    /// asking whether it can step off here has to be able to see all of them.</summary>
    private static Dictionary<(int, int), List<int>> TopsOfEveryLayer(SketchLayout? state)
    {
        var tops = new Dictionary<(int, int), List<int>>();
        foreach (var layer in ResolveLayers(state))
        {
            var shift = (int)Math.Round(layer.BaseY);
            var here = new Dictionary<(int, int), int>();
            foreach (var shape in layer.Shapes)
            {
                if (shape.Role is not null || shape.Operation == "subtract") continue;
                foreach (var (x, z, top, _) in RasterShape(shape))
                    if (!here.TryGetValue((x, z), out var held) || top > held) here[(x, z)] = top;
            }
            foreach (var (cell, top) in here)
            {
                if (!tops.TryGetValue(cell, out var stack)) tops[cell] = stack = [];
                stack.Add(top + shift);
            }
        }
        return tops;
    }

    /// <summary>A shape whose ground the world does not hold, and the one that took its place: two adds on
    /// one layer covering a cell with spans that do not touch, where the taller replaces the shorter outright.
    /// One entry per pair, whatever the number of cells they contest.
    ///
    /// <para>A layer holds one span per column, so this is the shape of a stack drawn where a stack cannot
    /// go. Overlapping spans are not this — two adds at one floor with different thicknesses are ordinary
    /// ground, and the taller winning is what "height is the tallest add" means.</para></summary>
    public static List<StackedShapes> StackedInOneLayer(SketchLayout? state)
    {
        var found = new List<StackedShapes>();
        foreach (var layer in ResolveLayers(state))
        {
            // Only an add places ground of its own; a subtract takes it away and a role-tagged shape is an
            // annotation. An erected shape settles its top against the ground after the relief, so the span
            // it states here is not the span it builds and a pair holding one cannot be judged.
            var adds = layer.Shapes
                .Where(shape => shape.Role is null && shape.Operation != "subtract" && !IsErected(shape))
                .ToList();
            // An override add cannot be the shape a pair loses. It is laid after the ordinary pass and
            // overwrites the column it lands on, and where that column reaches its floor the result runs
            // from the ground's own floor to the override's top — so a wall traced along a lip keeps the
            // ground under it rather than replacing it. Reading one as the lower half of a stack reports a
            // slab lost that is still in the world.
            var kept = new HashSet<string>(adds.Where(shape => shape.Override).Select(shape => shape.Id),
                                           StringComparer.Ordinal);
            if (adds.Count < 2) continue;

            var byCell = new Dictionary<(int, int), List<(string Id, int Floor, int Top)>>();
            foreach (var shape in adds)
                foreach (var (x, z, top, floor) in RasterShape(shape))
                {
                    if (!byCell.TryGetValue((x, z), out var here)) byCell[(x, z)] = here = [];
                    here.Add((shape.Id, floor, top));
                }

            var seen = new HashSet<(string, string)>();
            foreach (var spans in byCell.Values)
            {
                if (spans.Count < 2) continue;
                for (var i = 0; i < spans.Count; i++)
                for (var j = 0; j < spans.Count; j++)
                {
                    if (i == j) continue;
                    // A span starting at or above another's top is a second deck: the taller replaces the
                    // shorter outright, floor included, so the lower one's ground is not in the world.
                    if (spans[j].Floor < spans[i].Top) continue;
                    if (kept.Contains(spans[i].Id)) continue;
                    if (seen.Add((spans[i].Id, spans[j].Id)))
                        found.Add(new StackedShapes(layer.Id!, spans[i].Id, spans[j].Id));
                }
            }
        }
        return found;
    }

    /// <summary>
    /// Every add that covers ground a subtract takes away, whatever layer either is on. A subtract is how a
    /// board states its <b>negative space</b> — the void a plan's buffer pieces compile to — and an add over
    /// one is one of two silences: on the same layer a plain add draws nothing there, because the algebra is
    /// <c>((adds − subs) ∪ override-adds) − override-subs</c> and a subtract beats every plain add whatever
    /// order they are written in; an override add, or any add on another layer, puts the ground back instead.
    ///
    /// <para><b>Order exempts a hole, and only within one layer.</b> A body and the hole cut out of it are
    /// drawn in that order — an exterior ring then its interior rings, a compiled footprint then the buffers
    /// that state its negative space — so a subtract following an add <em>on that add's own layer</em> is its
    /// hole and says nothing. The algebra is order-independent and a layer's shape list is not: what the
    /// order carries there is which shape the void belongs to. Across layers it carries nothing of the kind —
    /// a layer's place in the stack is a height, and a slab written first is written <c>below</c> — so an add
    /// on another layer is a fill wherever it lands.</para>
    ///
    /// <para><b>A lid is not a fill.</b> A layer holds one span per column, so an override add resting
    /// <em>above</em> the subtract's own floor moves that single span up and records nothing beneath it — the
    /// void the subtract states is still void, with a deck over it. Only an override add standing at or below
    /// the subtract's floor puts the negative space back as ground. Same layer only: a floor is measured from
    /// its own layer's <c>base_y</c>, so two layers' floors are not one number.</para>
    ///
    /// <para>One entry per contesting pair, carrying which of the two happened. Role-tagged shapes are
    /// annotations and are not in it.</para>
    /// </summary>
    public static List<AddOverSubtract> AddsOverSubtracts(SketchLayout? state)
    {
        var subtracts = new List<(string Id, string Layer, bool Override, int Index, int Floor, HashSet<(int X, int Z)> Cells)>();
        var adds = new List<(string Id, string Layer, bool Override, int Index, int Floor, HashSet<(int X, int Z)> Cells)>();
        var index = 0;
        foreach (var layer in ResolveLayers(state))
            foreach (var shape in layer.Shapes)
            {
                index++;
                if (shape.Role is not null) continue;
                var cells = RasterShape(shape).Select(column => (column.X, column.Z)).ToHashSet();
                if (cells.Count == 0) continue;
                (shape.Operation == "subtract" ? subtracts : adds)
                    .Add((shape.Id, layer.Id!, shape.Override, index,
                          Math.Max(0, (int)Math.Round(shape.Floor ?? 0)), cells));
            }

        var found = new List<AddOverSubtract>();
        foreach (var subtract in subtracts)
            foreach (var add in adds)
            {
                if (add.Layer == subtract.Layer && add.Index < subtract.Index) continue;
                // A lid: the span moves up and leaves the void under it, so the subtract still holds there.
                if (add.Layer == subtract.Layer && add.Override && add.Floor > subtract.Floor) continue;
                var shared = add.Cells.Where(subtract.Cells.Contains).ToList();
                if (shared.Count == 0) continue;
                // A subtract only reaches the layer it is on, so an add anywhere else is ground of its own
                // over the hole. On one layer the override flags decide it.
                var survives = add.Layer != subtract.Layer || (add.Override && !subtract.Override);
                var first = shared.MinBy(cell => (cell.Z, cell.X));
                found.Add(new AddOverSubtract(add.Id, add.Layer, subtract.Id, subtract.Layer,
                                              survives, shared.Count, first.X, first.Z));
            }
        return found;
    }

    /// <summary>Where two layers are driven into each other. A layer is a slab and the stack is what puts air
    /// between two of them, so a pair whose spans meet builds as one solid mass and the gap the layers were
    /// drawn to have is not in the world. One entry per pair of layers, carrying how deep they meet, the
    /// first column they contest and how many they contest in all.
    ///
    /// <para><b>A single shared course is the seam, not a fault.</b> A layer spans
    /// <c>[base_y, base_y + height]</c> <em>inclusive</em>, so setting the upper layer's <c>base_y</c> to the
    /// lower's top — "the deck starts where the walls end", the obvious gesture — shares exactly one block.
    /// Complaining about that would be complaining about the coordinate system rather than about the board:
    /// <c>opus5-mineshaft</c> is built that way, its walls meeting the deck over 5,752 of 6,400 columns.
    /// Anything deeper is a slab driven through another.</para></summary>
    public static List<OverlappingLayers> OverlappingLayerSpans(SketchLayout? state)
    {
        // A made thing is not a deck. It stands on the ground and sinks into whatever it stands on, so the
        // courses it shares with the terrain are the seat rather than a lost gap, and a pair holding one is
        // not two layers driven into each other.
        var made = MadeLayers(state);
        var byCell = new Dictionary<(int X, int Z), List<ColumnSegment>>();
        foreach (var segment in RasterizeColumns(state))
        {
            if (made.Contains(segment.Layer)) continue;
            if (!byCell.TryGetValue(segment.Cell, out var here)) byCell[segment.Cell] = here = [];
            here.Add(segment);
        }

        var found = new Dictionary<(string, string), (int Courses, int X, int Z, int Cells)>();
        foreach (var (cell, spans) in byCell.OrderBy(entry => entry.Key.X).ThenBy(entry => entry.Key.Z))
        {
            if (spans.Count < 2) continue;
            var ordered = spans.OrderBy(span => span.YFloor).ToList();
            for (var i = 0; i < ordered.Count; i++)
            for (var j = i + 1; j < ordered.Count; j++)
            {
                if (ordered[i].Layer == ordered[j].Layer) continue;          // SK9's ground, not this one
                var shared = Math.Min(ordered[i].YTop, ordered[j].YTop) - ordered[j].YFloor + 1;
                if (shared <= SeamCourses) continue;                         // clear of each other, or the seam
                var pair = (ordered[i].Layer, ordered[j].Layer);
                found[pair] = found.TryGetValue(pair, out var known)
                    ? (Math.Max(known.Courses, shared), known.X, known.Z, known.Cells + 1)
                    : (shared, cell.X, cell.Z, 1);
            }
        }
        return [.. found.Select(entry =>
            new OverlappingLayers(entry.Key.Item1, entry.Key.Item2, entry.Value.Courses,
                                  entry.Value.X, entry.Value.Z, entry.Value.Cells))];
    }

    /// <summary>Every mass of standable ground that <b>stands over other ground</b> and that nothing joins to it.
    /// A raised mass with no way onto it, in other words — which is the one shape of this that is a fault rather
    /// than a choice. <para><b>A mass beside another is a landmass, not a fault.</b> Two landmasses across a void
    /// are how a board is normally drawn — the build zone bridges them at the intent tier, which a sketch does
    /// not state — so a mass sharing no column with any other says nothing. What is reported is a mass some of
    /// whose columns also carry ground in another mass: something floating above another thing, with nothing
    /// between them. Measured, the discriminator is the whole difference between a useful finding and noise:
    /// without it `thunderstorm`, a one-layer board of ordinary landmasses, reports eight.</para>
    /// <para>Ground under a roof says nothing either: that is a room, and a room with no door is the author's to
    /// have. Only a mass with open sky over some of it is reported.</para>
    /// <para>Joined means walked, not reached: the flood is bounded to <see cref="JoinedRise"/>, so what counts
    /// is a way the author <b>drew</b>. Unbounded it would find one onto every exposed deck, because a player
    /// carrying blocks can pillar up to any of them and the walk prices that climb rather than refusing it —
    /// which is the right answer to "can anyone get there" and the wrong one to "is there a way up". The bound
    /// cuts both ways: a cliff a player can only drop off does not join its two sides.</para>
    /// <para><b>floor</b> — Masses smaller than this are a ledge or a rasterizer sliver, not a
    /// place.</para></summary>
    public static List<DetachedMass> DetachedMasses(SketchLayout? state, int floor = 16)
    {
        // The walk is over terrain alone: a dome on columns, a raised arm and an antenna are all standable
        // ground under open sky that nothing reaches, all true, and none of them a way onto a deck somebody
        // forgot to draw.
        var made = MadeLayers(state);
        var ground = WalkGround.OfSpans(
            RasterizeColumns(state).Where(segment => !made.Contains(segment.Layer))
                                   .Select(segment => (segment.X, segment.Z, segment.YFloor, segment.YTop)));
        if (ground.Ground.Count == 0) return [];

        var components = Walk.Components(ground, JoinedRise);
        var sizes = components.GroupBy(entry => entry.Value)
                              .ToDictionary(group => group.Key, group => group.Count());
        var main = sizes.OrderByDescending(entry => entry.Value).ThenBy(entry => entry.Key).First().Key;

        // Which masses each column carries: a mass standing over another shares a column with it.
        var massesAt = new Dictionary<(int X, int Z), HashSet<int>>();
        foreach (var (place, id) in components)
        {
            if (!massesAt.TryGetValue(place.Cell, out var here)) massesAt[place.Cell] = here = [];
            here.Add(id);
        }

        var found = new List<DetachedMass>();
        foreach (var (id, size) in sizes.OrderBy(entry => entry.Key))
        {
            if (id == main || size < floor) continue;
            var places = components.Where(entry => entry.Value == id).Select(entry => entry.Key).ToList();
            if (places.All(place => ground.ClearAbove(place) != int.MaxValue)) continue;
            if (!places.Any(place => massesAt[place.Cell].Count > 1)) continue;   // beside, not above
            var seat = places.OrderBy(place => place.Y).ThenBy(place => place.Z).ThenBy(place => place.X).First();
            found.Add(new DetachedMass(size, seat.X, seat.Z, seat.Y));
        }
        return [.. found.OrderByDescending(mass => mass.Places)];
    }

    /// <summary>Every made thing that asked to be seated and found no ground under any of its columns, with
    /// how many columns it covers — what <c>SK16</c> reports. A thing partly over ground seats on what there
    /// is, which is the same reading a slope gets.</summary>
    public static List<(string Thing, int Cells)> SeatedOnNothing(SketchLayout? state)
    {
        var layers = ResolveLayers(state);
        var seated = layers.Where(layer => layer.SeatsOnGround).ToList();
        if (seated.Count == 0) return [];

        var thingOf = seated.ToDictionary(layer => layer.Id!,
                                          layer => layer.PartOf is { Length: > 0 } thing ? thing : layer.Id!,
                                          StringComparer.Ordinal);
        var segments = RasterizeColumns(state);
        var ground = segments.Where(segment => !thingOf.ContainsKey(segment.Layer))
                             .Select(segment => segment.Cell).ToHashSet();

        var cells = new Dictionary<string, HashSet<(int X, int Z)>>(StringComparer.Ordinal);
        foreach (var segment in segments)
            if (thingOf.TryGetValue(segment.Layer, out var thing))
            {
                if (!cells.TryGetValue(thing, out var here)) cells[thing] = here = [];
                here.Add(segment.Cell);
            }

        return [.. cells.Where(entry => !entry.Value.Any(ground.Contains))
                        .Select(entry => (entry.Key, entry.Value.Count))
                        .OrderByDescending(entry => entry.Count).ThenBy(entry => entry.Key, StringComparer.Ordinal)];
    }

    /// <summary>The ids of the layers holding a made thing rather than terrain — what the stacking rules skip.
    /// A layer that names no kind is ground, which is every board drawn before the word existed.</summary>
    public static HashSet<string> MadeLayers(SketchLayout? state) =>
        [.. ResolveLayers(state).Where(layer => layer.IsMade).Select(layer => layer.Id!)];

    // ── 4-step set algebra over a shape group, carrying each cell's column ─────────────────────────
    private static Dictionary<(int, int), (int Top, int Floor)> RasterGroup(IEnumerable<SketchShape> shapes)
        => RasterGroup(shapes, out _);

    // The cells an override add claimed come back beside the columns, because "the column is its own, floor
    // and all" has to survive being merged with another mass: between two masses the taller column wins,
    // which is right for two masses meeting and wrong for a claim meeting ordinary ground.
    private static Dictionary<(int, int), (int Top, int Floor)> RasterGroup(
        IEnumerable<SketchShape> shapes, out HashSet<(int, int)> claimed)
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
        // An override add overwrites the column — and where it stands in ground, takes the ground under its
        // floor with it. A wall traced along a lip with its floor a few courses under the bed is the case: the
        // column is the wall from its own floor up and the ground below that, not a wall over a shaft to
        // bedrock. Only a column whose ordinary span reaches the override's floor is ground it stands in; a
        // deck drawn above the ground's top keeps the air beneath it, because that gap was drawn.
        foreach (var (k, v) in oadd)
            result[k] = result.TryGetValue(k, out var ground) && ground.Floor < v.Floor && ground.Top >= v.Floor
                ? (v.Top, ground.Floor)
                : v;
        foreach (var k in osub) result.Remove(k);
        claimed = [.. oadd.Keys.Where(result.ContainsKey)];
        return result;
    }

    // Taller surface wins where add shapes overlap (carrying that surface's floor), and at one height the
    // DEEPER column wins. Two masses meeting at the same top is the ordinary case — a deck drawn over the
    // ground it crosses, a pad laid on the plateau it sits on — and a strict `>` leaves it to whichever shape
    // the merge happened to see first, which is not the same shape on the two halves of a mirrored board: the
    // primary half compares an authored shape against another authored shape, the mirrored half compares an
    // image against what is already there. Lindenkreuz's viaduct and the island it lands on both top out at
    // y20, and the seam came out solid to bedrock on one face and a four-course deck over void on the other,
    // cell by cell. Reading the floor makes the merge commutative, so the answer is the board's and not the
    // list's.
    private static void MergeCell(Dictionary<(int, int), (int Top, int Floor)> d, (int, int) k, (int Top, int Floor) v)
    {
        if (d.TryGetValue(k, out var ex))
        {
            if (v.Top > ex.Top || (v.Top == ex.Top && v.Floor < ex.Floor)) d[k] = v;
        }
        else d[k] = v;
    }

    private static void Merge(Dictionary<(int, int), (int Top, int Floor)> dst, Dictionary<(int, int), (int Top, int Floor)> src)
    {
        foreach (var (k, v) in src) MergeCell(dst, k, v);
    }

    /// <summary>Merges a group's mirror image back onto the layer, with the columns each side's override
    /// adds claimed. An override add overwrites the column it lands on, and a group centred on the mirror
    /// has its own image lying over it: without this a flight cut into a whole-board group is refilled by
    /// the reflection of the ground around it, and the flight's own image is buried by the ground it was cut
    /// out of. Where neither side claimed the column the taller wins, which is what it means between two
    /// masses meeting.</summary>
    private static void Merge(Dictionary<(int, int), (int Top, int Floor)> dst,
                              Dictionary<(int, int), (int Top, int Floor)> src,
                              HashSet<(int, int)> srcClaimed, HashSet<(int, int)> dstClaimed)
    {
        foreach (var (k, v) in src)
        {
            if (srcClaimed.Contains(k) == dstClaimed.Contains(k)) { MergeCell(dst, k, v); continue; }
            if (srcClaimed.Contains(k)) dst[k] = v;
        }
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
            // Away from zero, not to even. A cell's thickness is the surface read at its centre, and a
            // quad rising exactly one course a cell reads every centre on a half — so the default
            // round-half-to-even sends them alternately down and up and the flight comes out with a
            // two-block rise in every other tread, which no player walks. Gradient 1 is the only whole
            // ramp that ties on every cell, and it is the one an author states to mean forty-five degrees.
            int thickness = Math.Max(1, (int)Math.Round(height(x + 0.5, z + 0.5), MidpointRounding.AwayFromZero));
            yield return (x, z, floor + thickness, floor);
        }
    }

    // The thickness sampler for a shape: a per-vertex reading where anchor_heights lines up with the vertices,
    // else the uniform base_height (default 1). The result is a thickness above the floor, not an absolute
    // top.
    //
    // A closed ring and an open line read the same statement differently, because their vertices mean
    // different things. A polygon or lasso encloses its own footprint, so the heights interpolate over a TIN
    // of it — points in a Bézier fringe fall back to the nearest vertex inside Interpolate. A polyline's
    // vertices are its centreline and enclose nothing, so every cell of the band around it is somewhere
    // ALONG the line and the heights interpolate over the arc instead.
    private static Func<double, double, double> HeightFn(SketchShape s)
    {
        if ((s.Type == ShapeKinds.Polygon || s.Type == ShapeKinds.Lasso) && s.Vertices is { Length: >= 3 } verts
            && s.AnchorHeights is { } ah && ah.Length == verts.Length)
        {
            var poly = verts.Select(v => new[] { v[0], v[1] }).ToList();
            var tris = Triangulation.EarClip(poly);
            return (x, z) => Triangulation.Interpolate(poly, ah, tris, x, z);
        }

        if (s.Type == ShapeKinds.Polyline && s.Vertices is { Length: >= 2 } line
            && s.AnchorHeights is { } graded && graded.Length == line.Length)
        {
            // The same curve the band was offset from, so the arc a cell is read at is the arc it stands on.
            var centerline = Centerline.Of([.. line.Select(v => new[] { v[0], v[1] })]);
            if (ArcProfile.Of(centerline, graded) is { } profile)
                return (x, z) => profile.At(Geom.Algorithms.Polyline.Nearest(centerline, x, z).Arc);
        }

        double bh = s.BaseHeight ?? 1;
        return (_, _) => bh;
    }

    private static List<double[]> RingOf(SketchShape s) => s.Type switch
    {
        ShapeKinds.Rectangle => [[s.MinX ?? 0, s.MinZ ?? 0], [s.MaxX ?? 0, s.MinZ ?? 0], [s.MaxX ?? 0, s.MaxZ ?? 0], [s.MinX ?? 0, s.MaxZ ?? 0]],
        ShapeKinds.Circle    => CircleRing(s.CenterX ?? 0, s.CenterZ ?? 0, s.Radius ?? 0),
        ShapeKinds.Polygon or ShapeKinds.Lasso => PolygonRing(s.Vertices, s.Controls),
        // A path arrives as a centerline and a half-width; the band around it is the ring, so nothing below
        // this point learns a shape that is not a ring.
        ShapeKinds.Polyline => StrokeOutline.Ring(s.Vertices ?? [], s.Radius ?? 0, ParseStrokeEdge(s.StrokeEdge), s.StrokeSeed ?? 0),
        _ => [],
    };

    // How a shape's ground takes part in its group's relief. An erected shape does not get a say: it already
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
        // Ties away from zero, as an erected shape's own courses take them: a held mark and the shape it
        // is read off must not differ by a course because one rounded a half up and the other down.
        return floor + Math.Max(1, (int)Math.Round(HeightFn(s)(centreX, centreZ),
                                                  MidpointRounding.AwayFromZero));
    }

    private static StrokeEdge ParseStrokeEdge(string? edge) => edge switch
    {
        "rough"   => Geom.Algorithms.StrokeEdge.Rough,
        "tapered" => Geom.Algorithms.StrokeEdge.Tapered,
        _         => Geom.Algorithms.StrokeEdge.Solid,
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

    /// <summary>The cells a ring covers, by the same point-in-ring test every shape is rasterized with — so a
    /// caller asking about a ring it has not drawn yet is asking about the cells it would actually get.</summary>
    public static IEnumerable<(int X, int Z)> CellsOfRing(IReadOnlyList<double[]> ring)
        => RasterRing([.. ring]);

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

    /// <summary>One shape's image under <paramref name="axis"/>. What a shape <b>says</b> travels with the
    /// image beside its geometry: an image whose paint is dropped is ground the map default finishes, and both
    /// grains of paint — the <c>theme</c> and the <c>material</c> — have to survive, since a scope resolver
    /// asks whether a shape states either.</summary>
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
                Material = s.Material,
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
            Material = s.Material,
        };
    }

    // The one canonical concrete-axis transform — every orbit axis stays consistent with the generator + JS.
    private static (double, double) MirrorPoint(double x, double z, string axis, double cx, double cz)
        => Symmetry.Apply(x, z, axis, cx, cz);
}
