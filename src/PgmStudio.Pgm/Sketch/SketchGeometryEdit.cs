using System.Globalization;
using System.Text.Json.Nodes;
using PgmStudio.Domain;
using PgmStudio.Geom;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Sketch;

/// <summary>
/// The edits a layout's geometry takes one layer, one group or one shape at a time — the drawing half of what
/// <c>DressingEdit</c> does for the props.
///
/// <para>Pure, and stated over <see cref="JsonNode"/> rather than over <see cref="SketchLayout"/>, for the
/// reason the theme and relief writers are: a layout read into the model and written back out loses whatever
/// the reader has no field for, and a partial edit is the worst place to lose it — the caller touched one
/// shape and paid for every property the model does not carry. Splicing the tree touches what was addressed
/// and copies the rest through byte for byte.</para>
///
/// <para>Every edit answers <see cref="GeometryEdit.Missing"/> where the id names nothing, which the routes
/// turn into a 404, and a <see cref="Finding"/> where the edit itself is refused. The two are different
/// answers: a stale id is the ordinary case for a client that had the document a moment ago, and a refused
/// edit is one the caller has to change to make.</para>
/// </summary>
public static class SketchGeometryEdit
{
    /// <summary>The three fields a shape patch may not write. <c>role</c> and <c>intentRef</c> are the
    /// identity a plan recompile matches a shape by, so moving either silently rebinds the piece to a
    /// different intent or to none; <c>height_authored</c> is the mark that says a floor was corrected by
    /// hand, and setting it by hand is claiming a correction that was never made. All three are written by
    /// the compiler and read by it, and a caller has no way to state a coherent value for any of them.</summary>
    public static readonly string[] Structural = ["role", "intentRef", "height_authored"];

    /// <summary>The layer at <paramref name="layerId"/> with <paramref name="stated"/> merged onto it, or a
    /// new layer at the end of the stack where the id names none.
    ///
    /// <para>Stating <c>layout</c> replaces the layer's shapes and groups outright; leaving it out keeps
    /// them, because a layer's shapes have routes of their own and a caller renaming a layer is not asking
    /// to rub its drawing out. <c>id</c> is the address and is taken from the route whatever the body
    /// says.</para></summary>
    public static GeometryEdit PutLayer(string? layoutJson, string layerId, JsonObject stated)
    {
        var root = Root(layoutJson);
        var layers = Layers(root);
        var layer = LayerAt(layers, layerId);
        if (layer is null) layers.Add(layer = new JsonObject { ["id"] = layerId, ["layout"] = Empty() });

        foreach (var (key, value) in stated.ToList())
        {
            if (key is "id") continue;
            layer[key] = value?.DeepClone();
        }
        layer["id"] = layerId;
        layer["layout"] ??= Empty();
        return new(root.ToJsonString(), layerId);
    }

    /// <summary>The layout without that layer, and without the relief of every group no remaining layer
    /// carries — a relief keyed to a group that is gone builds nothing and reads as terrain the board still
    /// has.</summary>
    public static GeometryEdit RemoveLayer(string? layoutJson, string layerId)
    {
        var root = Root(layoutJson);
        var layers = Layers(root);
        var at = IndexOfLayer(layers, layerId);
        if (at < 0) return GeometryEdit.Missing;

        layers.RemoveAt(at);
        PruneRelief(root, layers);
        return new(root.ToJsonString(), layerId);
    }

    /// <summary>One group of one layer, stated whole: its name, whether it is fanned onto the symmetry orbit,
    /// and the shapes it lists. Creates it where the layer carries none under that id.</summary>
    public static GeometryEdit PutGroup(string? layoutJson, string layerId, string groupId, JsonObject stated)
    {
        var root = Root(layoutJson);
        var layers = Layers(root);
        if (LayerAt(layers, layerId) is not { } layer) return GeometryEdit.Missing;

        var groups = Groups(layer);
        var group = GroupAt(groups, groupId);
        if (group is null) groups.Add(group = new JsonObject { ["id"] = groupId });

        foreach (var (key, value) in stated.ToList())
        {
            if (key is "id") continue;
            group[key] = value?.DeepClone();
        }
        group["id"] = groupId;
        group["shapeIds"] ??= new JsonArray();
        return new(root.ToJsonString(), groupId);
    }

    /// <summary>The layer without that group. Its shapes stay on the layer and are drawn where they were
    /// drawn; what goes with the group is the orbit fan and the relief keyed under its id.</summary>
    public static GeometryEdit RemoveGroup(string? layoutJson, string layerId, string groupId)
    {
        var root = Root(layoutJson);
        var layers = Layers(root);
        if (LayerAt(layers, layerId) is not { } layer) return GeometryEdit.Missing;

        var groups = Groups(layer);
        var at = IndexOfGroup(groups, groupId);
        if (at < 0) return GeometryEdit.Missing;

        groups.RemoveAt(at);
        PruneRelief(root, layers);
        return new(root.ToJsonString(), groupId);
    }

    /// <summary>One shape drawn on a layer, listed in the group <paramref name="groupId"/> names.
    ///
    /// <para>The group is where the shape's ground is decided: the orbit fan is read off each mirroring
    /// group's list and so is the relief, so a shape in no group is built once, where it was drawn, on flat
    /// ground. A layer that carries groups therefore takes no shape that names none, and a name the layer
    /// does not carry yet opens a group — the id is the caller's own vocabulary, and the alternative is a
    /// mandatory write before every first shape.</para>
    ///
    /// <para>The shape keeps the id it states where that id is free across the whole document, and is minted
    /// <c>{type}-{n}</c> otherwise: ids are the address every other route uses, and two shapes answering to
    /// one leave a patch with no single subject.</para></summary>
    public static GeometryEdit AddShape(string? layoutJson, string layerId, JsonObject shape, string? groupId)
    {
        var root = Root(layoutJson);
        var layers = Layers(root);
        if (LayerAt(layers, layerId) is not { } layer) return GeometryEdit.Missing;

        var groups = Groups(layer);
        if (groupId is not { Length: > 0 })
        {
            if (groups.Count > 0)
                return GeometryEdit.Refused(new Finding(RequestRules.Unreadable,
                    $"the shape added to layer '{layerId}' names no `group`, and the layer has {groups.Count} "
                    + $"group{(groups.Count == 1 ? "" : "s")}",
                    Field: "group"));
        }

        var id = FreeId(layers, shape);
        shape = shape.DeepClone().AsObject();
        shape["id"] = id;
        Shapes(layer).Add(shape);

        if (groupId is { Length: > 0 })
        {
            var group = GroupAt(groups, groupId);
            if (group is null) groups.Add(group = new JsonObject { ["id"] = groupId, ["mirrors"] = true });
            if (group["shapeIds"] is not JsonArray listed) group["shapeIds"] = listed = [];
            listed.Add(id);
        }
        return new(root.ToJsonString(), id);
    }

    /// <summary>The shape at <paramref name="shapeId"/> with <paramref name="stated"/> merged onto it —
    /// a stated field replaces what the shape carried and a stated null takes the field off, so the one call
    /// both writes and clears. The three <see cref="Structural"/> fields are refused, and <c>id</c> is the
    /// address and is kept whatever the body says.</summary>
    public static GeometryEdit PatchShape(string? layoutJson, string shapeId, JsonObject stated)
    {
        if (Structural.Where(stated.ContainsKey).ToList() is { Count: > 0 } refused)
            return GeometryEdit.Refused(new Finding(RequestRules.Unreadable,
                $"the patch of shape '{shapeId}' states "
                + (refused.Count == 1
                    ? $"`{refused[0]}`"
                    : string.Join(", ", refused.Take(refused.Count - 1).Select(field => $"`{field}`"))
                      + $" and `{refused[^1]}`")
                + ", which a patch may not write",
                Field: refused[0], Subjects: [shapeId]));

        var root = Root(layoutJson);
        var layers = Layers(root);
        if (ShapeAt(layers, shapeId) is not { } shape) return GeometryEdit.Missing;

        foreach (var (key, value) in stated.ToList())
        {
            if (key is "id") continue;
            if (value is null) shape.Remove(key);
            else shape[key] = value.DeepClone();
        }
        shape["id"] = shapeId;
        return new(root.ToJsonString(), shapeId);
    }

    /// <summary>The shape at <paramref name="shapeId"/> redrawn as a coast: its outline resampled along the
    /// long edges, each inserted point pulled off its edge, and Bézier handles fitted over the result.
    ///
    /// <para>Only the points <em>between</em> the outline's own vertices move, so a corner stays where the
    /// plan put it. Which way they move is <paramref name="side"/>'s. A shape carrying a <c>role</c> is
    /// refused: a room's rectangle is what a stamper seats a building on, not a coast. So is one with no
    /// <c>vertices</c> — a rectangle or a circle states its outline as bounds and has none to resample — and
    /// one whose drawn ring would fold over itself.</para>
    ///
    /// <para><paramref name="held"/> answers how many inserted points had no room on the side asked for and
    /// stayed where they were cut, which is <c>SK21</c>.</para></summary>
    public static GeometryEdit BendShape(
        string? layoutJson, string shapeId, double wander, double step, uint seed, double tension,
        BendSide side, out int held, IReadOnlyList<int>? edges = null, bool fan = true)
    {
        held = 0;
        var root = Root(layoutJson);
        var layers = Layers(root);
        if (ShapeAt(layers, shapeId) is not { } shape) return GeometryEdit.Missing;

        if (Text(shape["role"]) is { Length: > 0 } role)
            return GeometryEdit.Refused(new Finding(RequestRules.Unreadable,
                $"shape '{shapeId}' has the `role` '{role}', which a bend does not take",
                Field: "role", Subjects: [shapeId]));

        if (shape["vertices"] is not JsonArray stated || stated.Count < 3)
            return GeometryEdit.Refused(new Finding(RequestRules.Unreadable,
                $"shape '{shapeId}' states fewer than 3 points in `vertices`",
                Field: "vertices", Subjects: [shapeId]));

        var ring = stated.Select(point => new[] { Number(point?[0]), Number(point?[1]) }).ToList();
        if (edges?.Where(edge => edge < 0 || edge >= ring.Count).Select(edge => (int?)edge).FirstOrDefault()
            is { } outside)
            return GeometryEdit.Refused(new Finding(RequestRules.Unreadable,
                $"the bend of shape '{shapeId}' names edge {outside}, not between 0 and {ring.Count - 1}",
                Field: "edges", Subjects: [shapeId]));
        var named = edges?.ToHashSet();
        Func<double, double, (double X, double Z)>? sampleAt = null;
        var (mode, centreX, centreZ) = SymmetryOf(root);
        if (fan && Symmetry.SelfImage(ring, mode, centreX, centreZ) is { } images)
        {
            sampleAt = (x, z) => Symmetry.Canonical(x, z, mode, centreX, centreZ);
            foreach (var lands in images)
                foreach (var edge in edges ?? []) named!.Add(Symmetry.ImageEdge(lands, edge));
        }
        if (RingBend.Draw(ring, wander, step, seed, tension, side: side, edges: named, sampleAt: sampleAt) is not { } coast)
            return GeometryEdit.Refused(new Finding(RequestRules.Unreadable,
                $"the bend of shape '{shapeId}' with a `wander` of {wander} blocks and a `step` of {step} blocks "
                + "folds its outline across itself",
                Field: "wander", Subjects: [shapeId]));

        held = coast.Held;
        Reheight(shape, ring, coast.Ring);
        shape["vertices"] = new JsonArray([.. coast.Ring.Select(point =>
            (JsonNode)new JsonArray(JsonValue.Create(point[0]), JsonValue.Create(point[1])))]);
        shape["controls"] = new JsonObject(coast.Controls.Select(handle =>
            KeyValuePair.Create(handle.Key.ToString(CultureInfo.InvariantCulture), (JsonNode?)new JsonObject
            {
                ["in"] = new JsonArray(JsonValue.Create(handle.Value.In[0]), JsonValue.Create(handle.Value.In[1])),
                ["out"] = new JsonArray(JsonValue.Create(handle.Value.Out[0]), JsonValue.Create(handle.Value.Out[1])),
            })));
        return new(root.ToJsonString(), shapeId);
    }

    /// <summary>The shape at <paramref name="shapeId"/> with points pulled across its edges
    /// (<see cref="RingPull"/>): each edge named by its index on the outline as it stands, each point a fraction
    /// of the way along its edge — above 0 and below 1 — moved its number of blocks into the ring, or out of it
    /// where the number is negative. An outline the board's symmetry carries onto itself takes every pull at each
    /// image of its edge too, a fraction along a reflected edge counted from its other end, unless
    /// <paramref name="fan"/> is false. Refused, as a point edit is, on a shape with no outline of its own, at an
    /// edge the outline does not have, and where the pulled outline would fold across itself.</summary>
    public static GeometryEdit PullShape(
        string? layoutJson, string shapeId, IReadOnlyDictionary<int, IReadOnlyList<RingPull.Pull>> pulls, bool fan = true)
    {
        var root = Root(layoutJson);
        if (Outline(Layers(root), shapeId, out var shape, out var vertices) is { } refused) return refused;
        foreach (var (edge, along) in pulls)
        {
            if (Range(shapeId, edge, vertices.Count) is { } outOfRange) return outOfRange;
            foreach (var pull in along)
                if (pull.At is not (> 0 and < 1) || !double.IsFinite(pull.In))
                    return GeometryEdit.Refused(new Finding(RequestRules.Unreadable,
                        $"edge {edge} of shape '{shapeId}' has a pull at "
                        + $"{pull.At.ToString(CultureInfo.InvariantCulture)} by "
                        + $"{pull.In.ToString(CultureInfo.InvariantCulture)}, not a position between 0 and 1 "
                        + "moved by a number of blocks",
                        Field: "pulls", Subjects: [shapeId]));
        }

        var ring = Ring(vertices);
        var placed = pulls.ToDictionary(edge => edge.Key, edge => edge.Value.ToList());
        var (mode, centreX, centreZ) = SymmetryOf(root);
        if (fan && Symmetry.SelfImage(ring, mode, centreX, centreZ) is { } images)
            foreach (var lands in images)
                foreach (var (edge, along) in pulls)
                {
                    var image = Symmetry.ImageEdge(lands, edge);
                    var reversed = lands[(edge + 1) % ring.Count] != (lands[edge] + 1) % ring.Count;
                    if (!placed.TryGetValue(image, out var there)) placed[image] = there = [];
                    foreach (var pull in along)
                    {
                        var landed = reversed ? pull with { At = 1 - pull.At } : pull;
                        if (!there.Any(other => Math.Abs(other.At - landed.At) < 1e-9 && Math.Abs(other.In - landed.In) < 1e-9))
                            there.Add(landed);
                    }
                }

        if (RingPull.Draw(ring, placed.ToDictionary(edge => edge.Key, edge => (IReadOnlyList<RingPull.Pull>)edge.Value))
            is not { } drawn)
            return Folded(shapeId, "points are pulled");

        // Each vertex keeps its handles at its new index, less those of a vertex whose edge took a point.
        var moved = new Dictionary<int, int>();
        var landedAt = 0;
        for (var vertex = 0; vertex < ring.Count; vertex++)
        {
            moved[vertex] = landedAt;
            landedAt += 1 + (placed.TryGetValue(vertex, out var inserted) ? inserted.Count : 0);
        }
        var stale = placed.Where(edge => edge.Value.Count > 0)
            .SelectMany(edge => (int[])[edge.Key, (edge.Key + 1) % ring.Count]).ToHashSet();
        vertices.Clear();
        foreach (var point in drawn) vertices.Add(Point(point[0], point[1]));
        Reheight(shape, ring, drawn);
        if (shape["controls"] is JsonObject controls)
        {
            var kept = new JsonObject();
            foreach (var (key, value) in controls.ToList())
                if (int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out var vertex)
                    && moved.TryGetValue(vertex, out var to) && !stale.Contains(vertex))
                    kept[to.ToString(CultureInfo.InvariantCulture)] = value?.DeepClone();
            if (kept.Count == 0) shape.Remove("controls");
            else shape["controls"] = kept;
        }
        return new(root.ToJsonString(), shapeId);
    }

    /// <summary>The shape at <paramref name="shapeId"/> with the one vertex at <paramref name="index"/>
    /// moved to <c>(x, z)</c>. Every other vertex stays exactly where it was drawn, which is the whole point
    /// of the call: a board's shapes abut, and an edit that drags a ring's other points opens ground between
    /// two that were flush.</summary>
    public static GeometryEdit MoveVertex(string? layoutJson, string shapeId, int index, double x, double z)
    {
        var root = Root(layoutJson);
        if (Outline(Layers(root), shapeId, out var shape, out var vertices) is { } refused) return refused;
        if (Range(shapeId, index, vertices.Count) is { } outOfRange) return outOfRange;

        var ring = Ring(vertices);
        ring[index] = [x, z];
        if (Polygon.SelfIntersects(ring)) return Folded(shapeId, "a point is moved");

        vertices[index] = Point(x, z);
        Recontrol(shape, index, vertices.Count, shift: null);
        return new(root.ToJsonString(), shapeId);
    }

    /// <summary>The shape at <paramref name="shapeId"/> with one vertex added after <paramref name="after"/>,
    /// at <c>(x, z)</c> where the caller states a point and at the midpoint of that edge where it does not —
    /// the anchor a hand reaches for when it wants a new corner half way along a wall. The new vertex's index
    /// rides back in <paramref name="index"/>.</summary>
    public static GeometryEdit InsertVertex(
        string? layoutJson, string shapeId, int after, double? x, double? z, out int index)
    {
        index = -1;
        var root = Root(layoutJson);
        if (Outline(Layers(root), shapeId, out var shape, out var vertices) is { } refused) return refused;
        if (Range(shapeId, after, vertices.Count) is { } outOfRange) return outOfRange;

        var ring = Ring(vertices);
        var next = ring[(after + 1) % ring.Count];
        var at = index = after + 1;
        double px = x ?? (ring[after][0] + next[0]) / 2, pz = z ?? (ring[after][1] + next[1]) / 2;

        var before = Ring(vertices);
        ring.Insert(at, [px, pz]);
        if (Polygon.SelfIntersects(ring)) return Folded(shapeId, "a point is added");

        vertices.Insert(at, Point(px, pz));
        Reheight(shape, before, ring);
        Recontrol(shape, at, vertices.Count, shift: from => from >= at ? from + 1 : from);
        return new(root.ToJsonString(), shapeId);
    }

    /// <summary>The shape at <paramref name="shapeId"/> without the vertex at <paramref name="index"/>.
    /// Refused where the outline is down to its last three, since two points draw no ground.</summary>
    public static GeometryEdit RemoveVertex(string? layoutJson, string shapeId, int index)
    {
        var root = Root(layoutJson);
        if (Outline(Layers(root), shapeId, out var shape, out var vertices) is { } refused) return refused;
        if (Range(shapeId, index, vertices.Count) is { } outOfRange) return outOfRange;

        if (vertices.Count <= 3)
            return GeometryEdit.Refused(new Finding(RequestRules.Unreadable,
                $"shape '{shapeId}' has {vertices.Count} points, and taking one away leaves fewer than 3",
                Field: "index", Subjects: [shapeId]));

        var before = Ring(vertices);
        var ring = Ring(vertices);
        ring.RemoveAt(index);
        if (Polygon.SelfIntersects(ring)) return Folded(shapeId, "a point is removed");

        vertices.RemoveAt(index);
        Reheight(shape, before, ring);
        Recontrol(shape, index % vertices.Count, vertices.Count, shift: from =>
            from == index ? null : from > index ? from - 1 : from);
        return new(root.ToJsonString(), shapeId);
    }

    /// <summary>The vertex list of a shape that can take a per-vertex edit, or the finding that refuses it: a
    /// <c>role</c> shape is the plan's own rectangle and is the compiler's to draw, and a rectangle or a
    /// circle states its bounds rather than an outline.</summary>
    private static GeometryEdit? Outline(
        JsonArray layers, string shapeId, out JsonObject shape, out JsonArray vertices)
    {
        shape = null!;
        vertices = null!;
        if (ShapeAt(layers, shapeId) is not { } found) return GeometryEdit.Missing;
        shape = found;

        if (Text(found["role"]) is { Length: > 0 } role)
            return GeometryEdit.Refused(new Finding(RequestRules.Unreadable,
                $"shape '{shapeId}' has the `role` '{role}', which a point edit does not take",
                Field: "role", Subjects: [shapeId]));

        if (found["vertices"] is not JsonArray stated || stated.Count < 3)
            return GeometryEdit.Refused(new Finding(RequestRules.Unreadable,
                $"shape '{shapeId}' states fewer than 3 points in `vertices`",
                Field: "vertices", Subjects: [shapeId]));

        vertices = stated;
        return null;
    }

    private static GeometryEdit? Range(string shapeId, int index, int count) =>
        index >= 0 && index < count ? null
            : GeometryEdit.Refused(new Finding(RequestRules.Unreadable,
                $"the edit of shape '{shapeId}' names index {index}, not between 0 and {count - 1}",
                Field: "index", Subjects: [shapeId]));

    private static GeometryEdit Folded(string shapeId, string cause) =>
        GeometryEdit.Refused(new Finding(RequestRules.Unreadable,
            $"the outline of shape '{shapeId}' folds across itself after {cause}",
            Field: "index", Subjects: [shapeId]));

    private static List<double[]> Ring(JsonArray vertices) =>
        [.. vertices.Select(point => new[] { Number(point?[0]), Number(point?[1]) })];

    private static JsonNode Point(double x, double z) =>
        new JsonArray(JsonValue.Create(x), JsonValue.Create(z));

    /// <summary>
    /// The shape's stated heights carried from the outline <paramref name="before"/> an edit onto the one
    /// <paramref name="after"/> it: a point that was a vertex keeps its height, and a point the edit added takes
    /// the height the old outline has where it stands, along its nearest edge. So an added point leaves the
    /// surface where it was, and a removed point takes its own height with it. Heights that did not line up with
    /// the outline are left as they stand, for <c>SK22</c> to name.
    /// </summary>
    private static void Reheight(JsonObject shape, IReadOnlyList<double[]> before, IReadOnlyList<double[]> after)
    {
        if (shape["anchor_heights"] is not JsonArray stated || stated.Count != before.Count) return;
        var heights = stated.Select(Number).ToArray();
        var closed = Text(shape["type"]) != "polyline";
        var unclaimed = Enumerable.Range(0, before.Count).ToList();
        var carried = new JsonArray();
        foreach (var point in after)
        {
            var same = unclaimed.FindIndex(at => before[at][0] == point[0] && before[at][1] == point[1]);
            double height;
            if (same >= 0)
            {
                height = heights[unclaimed[same]];
                unclaimed.RemoveAt(same);
            }
            else height = Math.Max(1, Math.Round(HeightAlong(before, heights, closed, point[0], point[1]),
                MidpointRounding.AwayFromZero));
            carried.Add(JsonValue.Create(height));
        }
        shape["anchor_heights"] = carried;
    }

    /// <summary>The height an outline with per-vertex <paramref name="heights"/> has at the point of its nearest
    /// edge to <c>(x, z)</c>, interpolated between that edge's two ends.</summary>
    private static double HeightAlong(IReadOnlyList<double[]> ring, double[] heights, bool closed, double x, double z)
    {
        var nearest = double.MaxValue;
        var height = heights[0];
        var edges = closed ? ring.Count : ring.Count - 1;
        for (var edge = 0; edge < edges; edge++)
        {
            int start = edge, end = (edge + 1) % ring.Count;
            double runX = ring[end][0] - ring[start][0], runZ = ring[end][1] - ring[start][1];
            var length = runX * runX + runZ * runZ;
            var along = length == 0 ? 0
                : Math.Clamp(((x - ring[start][0]) * runX + (z - ring[start][1]) * runZ) / length, 0, 1);
            double offX = ring[start][0] + along * runX - x, offZ = ring[start][1] + along * runZ - z;
            var distance = offX * offX + offZ * offZ;
            if (distance >= nearest) continue;
            nearest = distance;
            height = heights[start] + along * (heights[end] - heights[start]);
        }
        return height;
    }

    /// <summary>The shape's handle map re-keyed by <paramref name="shift"/> — an index it answers null for
    /// loses its handles — and then cleared at <paramref name="touched"/> and that index's two neighbours in
    /// a ring of <paramref name="count"/>. A handle is an absolute point fitted to a vertex and its two
    /// edges, so a vertex that has moved, arrived or gone leaves all three of them stale.</summary>
    private static void Recontrol(JsonObject shape, int touched, int count, Func<int, int?>? shift)
    {
        if (shape["controls"] is not JsonObject controls || count < 1) return;

        var next = new JsonObject();
        foreach (var (key, value) in controls.ToList())
        {
            if (!int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out var at)) continue;
            if ((shift is null ? at : shift(at)) is { } to)
                next[to.ToString(CultureInfo.InvariantCulture)] = value?.DeepClone();
        }
        foreach (var stale in new[] { (touched - 1 + count) % count, touched % count, (touched + 1) % count })
            next.Remove(stale.ToString(CultureInfo.InvariantCulture));

        if (next.Count == 0) shape.Remove("controls");
        else shape["controls"] = next;
    }

    private static double Number(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<double>(out var at) ? at : 0;

    /// <summary>The layout without that shape, and without it in any group that listed it — a list naming a
    /// shape the layout does not carry is what <c>SK3</c> reports, and rubbing a shape out is no reason to
    /// leave one behind.</summary>
    public static GeometryEdit RemoveShape(string? layoutJson, string shapeId)
    {
        var root = Root(layoutJson);
        var layers = Layers(root);
        var found = false;

        foreach (var layer in layers.OfType<JsonObject>())
        {
            var shapes = Shapes(layer);
            var at = IndexOfShape(shapes, shapeId);
            if (at >= 0) { shapes.RemoveAt(at); found = true; }

            foreach (var group in Groups(layer).OfType<JsonObject>())
                if (group["shapeIds"] is JsonArray listed)
                    for (var i = listed.Count - 1; i >= 0; i--)
                        if (Text(listed[i]) == shapeId) listed.RemoveAt(i);
        }
        return found ? new(root.ToJsonString(), shapeId) : GeometryEdit.Missing;
    }

    // ── reading the tree ───────────────────────────────────────────────────────────

    private static JsonObject Root(string? layoutJson) =>
        string.IsNullOrWhiteSpace(layoutJson) ? [] : JsonNode.Parse(layoutJson) as JsonObject ?? [];

    /// <summary>The symmetry a layout fans its mirroring groups by: its setup's mode and centre, and the
    /// setup's own default mode where it states none.</summary>
    internal static (string Mode, double CentreX, double CentreZ) SymmetryOf(JsonObject root)
    {
        var setup = root["setup"] as JsonObject;
        var mode = Text(setup?["mirror_mode"]) ?? new SketchSetup().MirrorMode;
        return (mode, Number(setup?["center"]?["cx"]), Number(setup?["center"]?["cz"]));
    }

    /// <summary>The outline of the shape at <paramref name="shapeId"/> as it stands, or null where the layout
    /// carries no such polygon.</summary>
    internal static List<double[]>? RingOf(string? layoutJson, string shapeId) =>
        ShapeAt(Layers(Root(layoutJson)), shapeId)?["vertices"] is JsonArray stated && stated.Count >= 3
            ? [.. stated.Select(point => new[] { Number(point?[0]), Number(point?[1]) })]
            : null;

    private static JsonArray Layers(JsonObject root)
    {
        if (root["layers"] is not JsonArray layers) root["layers"] = layers = [];
        return layers;
    }

    private static JsonObject Empty() => new() { ["shapes"] = new JsonArray(), ["groups"] = new JsonArray() };

    private static JsonArray Shapes(JsonObject layer)
    {
        if (layer["layout"] is not JsonObject layout) layer["layout"] = layout = Empty();
        if (layout["shapes"] is not JsonArray shapes) layout["shapes"] = shapes = [];
        return shapes;
    }

    private static JsonArray Groups(JsonObject layer)
    {
        if (layer["layout"] is not JsonObject layout) layer["layout"] = layout = Empty();
        if (layout["groups"] is not JsonArray groups) layout["groups"] = groups = [];
        return groups;
    }

    private static string? Text(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static bool Is(JsonNode? node, string key, string id) =>
        node is JsonObject entry && Text(entry[key]) == id;

    private static int IndexOfLayer(JsonArray layers, string id)
    {
        for (var i = 0; i < layers.Count; i++)
            if (Is(layers[i], "id", id) || (Text((layers[i] as JsonObject)?["id"]) is null && $"layer{i}" == id))
                return i;
        return -1;
    }

    private static JsonObject? LayerAt(JsonArray layers, string id) =>
        IndexOfLayer(layers, id) is var at && at >= 0 ? layers[at] as JsonObject : null;

    private static int IndexOfGroup(JsonArray groups, string id)
    {
        for (var i = 0; i < groups.Count; i++) if (Is(groups[i], "id", id)) return i;
        return -1;
    }

    private static JsonObject? GroupAt(JsonArray groups, string id) =>
        IndexOfGroup(groups, id) is var at && at >= 0 ? groups[at] as JsonObject : null;

    private static int IndexOfShape(JsonArray shapes, string id)
    {
        for (var i = 0; i < shapes.Count; i++) if (Is(shapes[i], "id", id)) return i;
        return -1;
    }

    private static JsonObject? ShapeAt(JsonArray layers, string id)
    {
        foreach (var layer in layers.OfType<JsonObject>())
        {
            var shapes = Shapes(layer);
            if (IndexOfShape(shapes, id) is var at && at >= 0) return shapes[at] as JsonObject;
        }
        return null;
    }

    private static IEnumerable<string> GroupIds(JsonArray groups) =>
        groups.Select(group => Text((group as JsonObject)?["id"])).OfType<string>();

    /// <summary>The id the shape keeps, or the lowest free <c>{type}-{n}</c> where the one it states is
    /// taken or absent. Named for the type so a document read by hand says what each id draws.</summary>
    private static string FreeId(JsonArray layers, JsonObject shape)
    {
        var taken = layers.OfType<JsonObject>()
                          .SelectMany(layer => Shapes(layer).OfType<JsonObject>())
                          .Select(drawn => Text(drawn["id"]))
                          .OfType<string>()
                          .ToHashSet(StringComparer.Ordinal);

        if (Text(shape["id"]) is { Length: > 0 } stated && !taken.Contains(stated)) return stated;

        var type = Text(shape["type"]) is { Length: > 0 } named ? named : "shape";
        var next = 1;
        while (taken.Contains($"{type}-{next}")) next++;
        return $"{type}-{next}";
    }

    /// <summary>Drops every relief entry keyed to a group no layer carries any more.</summary>
    private static void PruneRelief(JsonObject root, JsonArray layers)
    {
        if (root["relief"] is not JsonObject relief) return;
        var live = layers.OfType<JsonObject>().SelectMany(layer => GroupIds(Groups(layer)))
                         .ToHashSet(StringComparer.Ordinal);
        foreach (var key in relief.Select(entry => entry.Key).Where(key => !live.Contains(key)).ToList())
            relief.Remove(key);
    }
}

/// <summary>The outcome of one geometry edit: the layout it produced, the id of the part that was edited, and
/// the finding that refused it. <see cref="Layout"/> is null exactly when nothing was edited — with a
/// <see cref="Refusal"/> where the caller has something to fix, and without one where the id named
/// nothing.</summary>
public readonly record struct GeometryEdit(string? Layout, string Id, Finding? Refusal = null)
{
    /// <summary>The id named no layer, group or shape.</summary>
    public static GeometryEdit Missing => new(null, "");

    /// <summary>The edit was refused, and the finding says what to change.</summary>
    public static GeometryEdit Refused(Finding finding) => new(null, "", finding);

    /// <summary>Whether the id named nothing, as against having been refused.</summary>
    public bool IsMissing => Layout is null && Refusal is null;
}
