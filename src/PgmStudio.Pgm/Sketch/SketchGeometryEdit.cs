using System.Text.Json.Nodes;
using PgmStudio.Domain;
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
                    $"layer '{layerId}' groups its shapes and this one names no group, so it would be built "
                    + "once where it was drawn, off the symmetry orbit and on flat ground. Name one of "
                    + $"[{string.Join(", ", GroupIds(groups))}] in `group`, or a new id to open one",
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
                $"a shape patch cannot write {string.Join(" or ", refused.Select(field => $"`{field}`"))} — "
                + "`role` and `intentRef` are the identity a plan recompile matches a shape by, and "
                + "`height_authored` is the mark that a floor was corrected by hand. All three are the "
                + "compiler's to write. State the geometry and leave them off",
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
