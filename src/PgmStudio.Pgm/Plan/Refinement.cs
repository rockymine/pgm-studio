using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using PgmStudio.Domain;
using PgmStudio.Geom;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Sketch;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Plan;

/// <summary>
/// Everything a board states that its plan cannot: the paint on the compiled shapes, the storeys and shapes
/// drawn over the compiled ground, the outlines reshaped a point at a time and bent into coasts, the relief, the
/// theme registry, the biome, the room styles, the dressing, and the parts of the intent a plan has no words for.
/// A material it states more than once is named once, in <see cref="Materials"/>, and used by name.
/// It is applied onto the layout and intent a plan compiles to, or onto a drawn pair, by <see cref="Apply"/>;
/// each statement is the one a Sketch route makes, so a board stated here and one edited by hand end the same.
/// </summary>
public sealed record Refinement
{
    /// <summary>The materials the refinement states once, by name. A <c>{"use": name}</c> wherever a material is
    /// stated stands for a copy of it, with the fields stated beside it laid over.</summary>
    [JsonPropertyName("materials")] public Dictionary<string, JsonElement>? Materials { get; init; }

    /// <summary>The theme each compiled ground shape paints with, by the height it stands at.</summary>
    [JsonPropertyName("themeByHeight")] public Dictionary<string, string>? ThemeByHeight { get; init; }

    /// <summary>The theme a shape paints with, by its id, which wins over the height rule.</summary>
    [JsonPropertyName("themeById")] public Dictionary<string, string>? ThemeById { get; init; }

    /// <summary>Fields merged onto each compiled ground shape standing at a height.</summary>
    [JsonPropertyName("shapePropsByHeight")] public Dictionary<string, JsonElement>? ShapePropsByHeight { get; init; }

    /// <summary>Fields merged onto a shape, by its id — what <c>PATCH …/sketch/shapes/{shapeId}</c> takes.</summary>
    [JsonPropertyName("shapePropsById")] public Dictionary<string, JsonElement>? ShapePropsById { get; init; }

    /// <summary>Storeys added to the stack: over the compiled ground, or under it where one states
    /// <c>below</c>.</summary>
    [JsonPropertyName("addLayers")] public List<AddedLayer>? AddLayers { get; init; }

    /// <summary>Shapes drawn onto the board: each a shape, with the <c>layer</c> and <c>group</c> it joins
    /// beside its own fields — what <c>POST …/sketch/layers/{layerId}/shapes?group=</c> takes. A shape naming
    /// no layer joins the compiled ground, and one naming no group joins its layer's first.</summary>
    [JsonPropertyName("addShapes")] public List<JsonElement>? AddShapes { get; init; }

    /// <summary>Outlines reshaped one point at a time, in order, by shape id.</summary>
    [JsonPropertyName("editShapes")] public Dictionary<string, List<VertexEdit>>? EditShapes { get; init; }

    /// <summary>Outlines drawn as coasts, by shape id, after every point edit.</summary>
    [JsonPropertyName("bendShapes")] public Dictionary<string, ShapeBend>? BendShapes { get; init; }

    /// <summary>Outlines stated by their shape, by the id of what they outline: a shape's vertices, a relief
    /// area's or push's ring, a stroke's, fluid's or flora's points. Written once everything is drawn and before
    /// the point edits, so an outlined shape can still be edited and bent.</summary>
    [JsonPropertyName("outlines")] public Dictionary<string, Outline>? Outlines { get; init; }

    /// <summary>The relief, by group id; <c>*</c> stands for every group of the compiled ground.</summary>
    [JsonPropertyName("relief")] public Dictionary<string, SketchReliefJson>? Relief { get; init; }

    /// <summary>The theme registry, id → the theme.</summary>
    [JsonPropertyName("themes")] public Dictionary<string, JsonElement>? Themes { get; init; }

    /// <summary>The map's default theme. Absent takes the registry's first.</summary>
    [JsonPropertyName("mapTheme")] public string? MapTheme { get; init; }

    /// <summary>The layout's biome field, as <c>SketchLayout.biome</c> takes it.</summary>
    [JsonPropertyName("biome")] public JsonElement? Biome { get; init; }

    /// <summary>The two room styles the map binds.</summary>
    [JsonPropertyName("roomStyles")] public SketchRoomStyles? RoomStyles { get; init; }

    /// <summary>The dressing document: the recipes and every placed prop.</summary>
    [JsonPropertyName("dressing")] public JsonElement? Dressing { get; init; }

    /// <summary>The map's own date, as the intent's <c>meta.created</c>.</summary>
    [JsonPropertyName("created")] public string? Created { get; init; }

    /// <summary>Who the map credits: a bare name, or <c>{name, contribution}</c>.</summary>
    [JsonPropertyName("authors")] public List<JsonElement>? Authors { get; init; }

    /// <summary>The capture points, each stated once and fanned across the board's symmetry: an image is named
    /// for its point and numbered on, and a point already standing where an image would go is that image.</summary>
    [JsonPropertyName("controlPoints")] public List<ControlPointIntent>? ControlPoints { get; init; }

    /// <summary>The score the match ends at.</summary>
    [JsonPropertyName("scoreLimit")] public int? ScoreLimit { get; init; }

    /// <summary>The generators the board mints from, each stated once and fanned across the board's symmetry
    /// under its id numbered on, as the capture points are.</summary>
    [JsonPropertyName("spawners")] public List<SpawnerIntent>? Spawners { get; init; }

    /// <summary>The shops, whose keepers stand at every team's spawn.</summary>
    [JsonPropertyName("shops")] public List<ShopIntent>? Shops { get; init; }

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>What a body states as a refinement, or null where it will not read as one.</summary>
    public static Refinement? Stated(string json)
    {
        try { return JsonSerializer.Deserialize<Refinement>(json, Json); }
        catch (JsonException) { return null; }
    }

    /// <summary>
    /// The layout and intent with <paramref name="refinementJson"/> applied, in the order a hand would work: the
    /// paint and fields on the compiled shapes, the storeys, the shapes drawn onto them, the relief, the theme
    /// registry, the biome, the room styles and the dressing, then the outlines reshaped point by point and bent.
    /// Each statement is copied through as it was written, so a field the typed reader has no room for still
    /// reaches the layout. Before any of it, every <c>use</c> is replaced by the material it names.
    ///
    /// <para>A statement naming a shape or a layer the board does not have, or an edit the board refuses, is a
    /// complaint and the rest is applied: the board is what was stated less that one statement, and the finding
    /// says which. A storey stated twice and an edit stating no single index are refusals, because neither says
    /// what was meant.</para>
    /// </summary>
    public static Refined Apply(string refinementJson, string layoutJson, string intentJson)
    {
        var findings = new List<Finding>();
        if (JsonNode.Parse(refinementJson) is not JsonObject refinement)
            return new(layoutJson, intentJson, new Findings(findings));
        var layout = JsonNode.Parse(layoutJson) as JsonObject ?? [];
        var intent = JsonNode.Parse(intentJson) as JsonObject ?? [];
        Name(refinement, findings);
        if (findings.Any(finding => finding.Refuses)) return new(layoutJson, intentJson, new Findings(findings));

        var layers = layout["layers"] as JsonArray ?? (JsonArray)(layout["layers"] = new JsonArray());
        var ground = layers.OfType<JsonObject>().FirstOrDefault();
        Paint(refinement, layers, ground, findings);
        if (findings.Any(finding => finding.Refuses)) return new(layoutJson, intentJson, new Findings(findings));
        Stack(refinement, layers, findings);
        if (findings.Any(finding => finding.Refuses)) return new(layoutJson, intentJson, new Findings(findings));

        var groundId = ground?["id"]?.GetValue<string>();
        var drawn = Draw(refinement, layout.ToJsonString(), groundId, findings);
        var finished = JsonNode.Parse(drawn)!.AsObject();
        Finish(refinement, finished, groundId);
        Outlined(refinement, finished, findings);
        if (findings.Any(finding => finding.Refuses)) return new(layoutJson, intentJson, new Findings(findings));
        var edited = Edit(refinement, finished.ToJsonString(), findings);
        if (findings.Any(finding => finding.Refuses)) return new(layoutJson, intentJson, new Findings(findings));

        var (mode, centreX, centreZ) = SketchGeometryEdit.SymmetryOf(layout);
        Play(refinement, intent,
             Symmetry.Order(mode) > 1 ? new SymmetryIntent { Mode = mode, CenterX = centreX, CenterZ = centreZ } : null);
        return new(edited, intent.ToJsonString(), new Findings(findings));
    }

    // The materials named once: every `use` in a statement that paints or builds is replaced by a copy of the
    // material the registry states under that name, with the fields stated beside it laid over.
    private static readonly string[] Using =
        ["themes", "roomStyles", "dressing", "biome", "addShapes", "addLayers", "shapePropsById", "shapePropsByHeight"];

    private static void Name(JsonObject refinement, List<Finding> findings)
    {
        var registry = refinement["materials"] as JsonObject ?? [];
        foreach (var member in Using)
            if (refinement[member] is { } node && Used(node, member, registry, findings) is var named && named != node)
                refinement[member] = named;
    }

    private static JsonNode Used(JsonNode node, string path, JsonObject registry, List<Finding> findings)
    {
        if (node is JsonObject stated && stated[StatedName.UseKey] is JsonValue name && name.TryGetValue<string>(out var used))
        {
            if (registry[used] is not { } material)
            {
                findings.Add(new Finding(SourceRules.UsesNoMaterial,
                    $"`{path}` uses material '{used}', which "
                    + (registry.Count > 0
                        ? $"is not one of {string.Join(", ", registry.Select(pair => pair.Key))}"
                        : "`materials` does not have"),
                    Field: $"refinement.{path}.use"));
                return node;
            }
            var beside = new JsonObject();
            foreach (var (field, value) in stated)
                if (field != StatedName.UseKey) beside[field] = value?.DeepClone();
            return LaidOver(material, beside)!;
        }
        switch (node)
        {
            case JsonObject members:
                foreach (var key in members.Select(pair => pair.Key).ToList())
                    if (members[key] is { } child && Used(child, $"{path}.{key}", registry, findings) is var named
                        && named != child)
                        members[key] = named;
                break;
            case JsonArray items:
                for (var at = 0; at < items.Count; at++)
                    if (items[at] is { } child && Used(child, $"{path}[{at}]", registry, findings) is var named
                        && named != child)
                        items[at] = named;
                break;
        }
        return node;
    }

    /// <summary>The fields stated beside a name laid over the copy it stands for: an object merges member by member
    /// and anything else replaces what the copy held. An object that is itself a name — it states <c>use</c> or
    /// <c>library</c> — replaces too, since it stands for a whole thing of its own.</summary>
    public static JsonNode? LaidOver(JsonNode? copy, JsonNode? stated)
    {
        if (copy is JsonObject into && stated is JsonObject over
            && !over.ContainsKey(StatedName.UseKey) && !over.ContainsKey(StatedName.LibraryKey))
        {
            var merged = into.DeepClone().AsObject();
            foreach (var (key, value) in over) merged[key] = LaidOver(merged[key], value);
            return merged;
        }
        return stated?.DeepClone();
    }

    // The paint and fields on the shapes already drawn: by height on the compiled ground, by id anywhere. A room
    // piece is not terrain, so only its own id reaches it.
    private static void Paint(JsonObject refinement, JsonArray layers, JsonObject? ground, List<Finding> findings)
    {
        var byHeight = Strings(refinement["themeByHeight"]);
        var propsByHeight = refinement["shapePropsByHeight"] as JsonObject;
        var byId = Strings(refinement["themeById"]);
        var propsById = refinement["shapePropsById"] as JsonObject;

        if (ground?["layout"]?["shapes"] is JsonArray compiled)
            foreach (var shape in compiled.OfType<JsonObject>())
            {
                if (shape["role"] is not null) continue;
                if (Height(shape) is not { } height) continue;
                if (byHeight.TryGetValue(height, out var theme)) shape["theme"] = theme;
                if (propsByHeight?[height] is JsonObject fields) Merge(shape, fields);
            }

        var shapes = layers.OfType<JsonObject>()
            .SelectMany(layer => (layer["layout"]?["shapes"] as JsonArray)?.OfType<JsonObject>() ?? [])
            .GroupBy(shape => shape["id"]?.GetValue<string>() ?? "")
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        foreach (var (id, theme) in byId)
            if (shapes.TryGetValue(id, out var shape)) shape["theme"] = theme;
            else findings.Add(NamesNothing("themeById", id, shapes.Keys));
        foreach (var (id, fields) in propsById ?? [])
            if (!shapes.TryGetValue(id, out var shape)) findings.Add(NamesNothing("shapePropsById", id, shapes.Keys));
            else if (fields is JsonObject stated) Merge(shape, stated);
    }

    // The storeys: each over the compiled ground, or under it where it states `below`, so the stack is written
    // bottom-up as the painter walks it.
    private static void Stack(JsonObject refinement, JsonArray layers, List<Finding> findings)
    {
        if (refinement["addLayers"] is not JsonArray added) return;
        var taken = layers.OfType<JsonObject>().Select(layer => layer["id"]?.GetValue<string>())
            .OfType<string>().ToHashSet(StringComparer.Ordinal);
        foreach (var (entry, index) in added.OfType<JsonObject>().Select((entry, index) => (entry, index)))
        {
            var id = entry["id"]?.GetValue<string>() ?? "";
            if (id.Length == 0 || !taken.Add(id))
            {
                findings.Add(new Finding(SourceRules.LayerStatedTwice,
                    id.Length == 0
                        ? $"`addLayers[{index}]` has no `id`"
                        : $"`addLayers[{index}]` adds layer '{id}', which the layout already has",
                    Field: $"addLayers[{index}].id", Subjects: id.Length == 0 ? null : [id]));
                continue;
            }
            var layer = new JsonObject
            {
                ["id"] = id,
                ["name"] = entry["name"]?.DeepClone() ?? id,
                ["base_y"] = entry["base_y"]?.DeepClone() ?? 0,
            };
            foreach (var key in (string[])["kind", "part_of", "seat"])
                if (entry[key] is { } value) layer[key] = value.DeepClone();
            layer["layout"] = new JsonObject
            {
                ["shapes"] = entry["shapes"]?.DeepClone() ?? new JsonArray(),
                ["groups"] = entry["groups"]?.DeepClone() ?? new JsonArray(),
            };
            if (entry["below"] is JsonValue below && below.TryGetValue<bool>(out var under) && under) layers.Insert(0, layer);
            else layers.Add(layer);
        }
    }

    // The shapes drawn onto the board, each onto the layer and group it names: the compiled ground and its first
    // group where it names neither, and a new group where it names one the layer does not have.
    private static string Draw(JsonObject refinement, string layoutJson, string? groundId, List<Finding> findings)
    {
        if (refinement["addShapes"] is not JsonArray added) return layoutJson;
        foreach (var (entry, index) in added.OfType<JsonObject>().Select((entry, index) => (entry, index)))
        {
            var shape = entry.DeepClone().AsObject();
            var layerId = shape[ShapeJoin.LayerKey]?.GetValue<string>() ?? groundId ?? SketchLayer.GroundId;
            var groupId = shape[ShapeJoin.GroupKey]?.GetValue<string>();
            shape.Remove(ShapeJoin.LayerKey);
            shape.Remove(ShapeJoin.GroupKey);
            groupId ??= FirstGroup(layoutJson, layerId);

            var edit = SketchGeometryEdit.AddShape(layoutJson, layerId, shape, groupId);
            if (edit.Layout is { } drawn) { layoutJson = drawn; continue; }
            findings.Add(edit.Refusal?.AsComplaint() ?? new Finding(SourceRules.NamesNothing,
                $"`addShapes[{index}]` names layer '{layerId}', which the layout does not have",
                Severity.Complaint, Field: $"addShapes[{index}].layer",
                Subjects: shape["id"]?.GetValue<string>() is { } id ? [id] : null));
        }
        return layoutJson;
    }

    // What the layout carries beside its drawing: the relief, the theme registry and its default, the biome, the
    // room styles and the dressing — each replacing what the layout held, as the route that writes it does.
    private static void Finish(JsonObject refinement, JsonObject layout, string? groundId)
    {
        if (refinement["relief"] is JsonObject relief)
        {
            var stated = new JsonObject();
            if (relief["*"] is { } every)
                foreach (var group in GroupsOf(layout, groundId)) stated[group] = every.DeepClone();
            foreach (var (group, value) in relief)
                if (group != "*") stated[group] = value?.DeepClone();
            layout["relief"] = stated;
        }
        if (refinement["themes"] is JsonObject themes)
        {
            layout["themes"] = themes.DeepClone();
            layout["mapTheme"] = refinement["mapTheme"]?.DeepClone() ?? themes.FirstOrDefault().Key;
        }
        else if (refinement["mapTheme"] is { } mapTheme) layout["mapTheme"] = mapTheme.DeepClone();
        foreach (var key in (string[])["biome", "roomStyles", "dressing"])
            if (refinement.ContainsKey(key)) layout[key] = refinement[key]?.DeepClone();
    }

    // The outlines stated by their shape, each written as the points of everything carrying its id. A relief
    // stated for every group is copied into each, so one id can be a mark in several and the outline is all of
    // them; an outline that draws no ring refuses the source, and one reaching nothing that reads a ring is said.
    private static void Outlined(JsonObject refinement, JsonObject layout, List<Finding> findings)
    {
        if (refinement["outlines"] is not JsonObject outlines) return;
        var shapes = (layout["layers"] as JsonArray ?? []).OfType<JsonObject>()
            .SelectMany(layer => layer["layout"]?["shapes"] as JsonArray ?? []).OfType<JsonObject>().ToList();
        var relief = (layout["relief"] as JsonObject ?? []).Select(group => group.Value).OfType<JsonObject>().ToList();
        var marks = relief.SelectMany(group => group["marks"] as JsonArray ?? []).OfType<JsonObject>().ToList();
        var pushes = relief.SelectMany(group => group["pushes"] as JsonArray ?? []).OfType<JsonObject>().ToList();
        var props = (layout["dressing"]?["props"] as JsonArray ?? []).OfType<JsonObject>().ToList();

        foreach (var (id, stated) in outlines)
        {
            var field = $"outlines.{id}";
            Outline? outline;
            try { outline = stated?.Deserialize<Outline>(Json); }
            catch (JsonException) { outline = null; }
            string? why = "states no outline";
            if ((outline is null ? null : outline.Drawn(out why)) is not { } ring)
            {
                findings.Add(new Finding(SourceRules.OutlineDrawsNoRing, $"outline '{id}' {why}",
                    Field: field, Subjects: [id]));
                return;
            }

            var reached = 0;
            var unread = new List<string>();
            foreach (var shape in shapes.Where(shape => Text(shape["id"]) == id))
            {
                if (Text(shape["role"]) is { Length: > 0 } role) { unread.Add($"the plan's own {role} rectangle"); continue; }
                shape["type"] = ShapeKinds.Polygon;
                foreach (var bound in (string[])["min_x", "min_z", "max_x", "max_z", "center_x", "center_z", "radius", "controls"])
                    shape.Remove(bound);
                shape["vertices"] = Points(ring);
                reached++;
            }
            foreach (var mark in marks.Where(mark => Text(mark["id"]) == id))
            {
                if (Text(mark["kind"]) is var kind && kind != MarkKinds.Area) { unread.Add($"a {kind ?? MarkKinds.Point} mark"); continue; }
                mark["ring"] = Points(ring);
                reached++;
            }
            foreach (var push in pushes.Where(push => Text(push["id"]) == id))
            {
                push["ring"] = Points(ring);
                reached++;
            }
            foreach (var prop in props.Where(prop => Text(prop["id"]) == id))
            {
                if (Text(prop["kind"]) is var kind && kind is not (PropKinds.Stroke or PropKinds.Fluid or PropKinds.Flora))
                { unread.Add($"a {kind} prop"); continue; }
                prop["points"] = Points(ring);
                reached++;
            }

            if (unread.Count > 0)
                findings.Add(new Finding(SourceRules.NamesNothing,
                    $"outline '{id}' is stated for {string.Join(" and ", unread)}, none of which takes an outline",
                    Severity.Complaint, Field: field, Subjects: [id]));
            else if (reached == 0)
                findings.Add(NamesNothing("outlines", id,
                    shapes.Concat(marks).Concat(pushes).Concat(props).Select(thing => Text(thing["id"])).OfType<string>().Distinct()));
        }

        static JsonArray Points(double[][] ring) => new([.. ring.Select(point =>
            (JsonNode)new JsonArray(JsonValue.Create(point[0]), JsonValue.Create(point[1])))]);
    }

    private static string? Text(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    // The outlines reshaped a point at a time, in order, and then bent: a bend resamples whatever ring it is given,
    // so every point edit comes first.
    private static string Edit(JsonObject refinement, string layoutJson, List<Finding> findings)
    {
        var board = SketchGeometryEdit.SymmetryOf(JsonNode.Parse(layoutJson) as JsonObject ?? []);
        if (refinement["editShapes"] is JsonObject edits)
            foreach (var (shapeId, ops) in edits)
                foreach (var (op, index) in (ops as JsonArray ?? []).OfType<JsonObject>().Select((op, index) => (op, index)))
                {
                    var field = $"editShapes.{shapeId}[{index}]";
                    var named = ((string[])["after", "index", "remove", "pulls"]).Where(op.ContainsKey).ToList();
                    if (named.Count != 1)
                    {
                        findings.Add(new Finding(SourceRules.EditStatesNoIndex,
                            $"point edit `{field}` names {named.Count} of `after`, `index`, `remove` and `pulls`, "
                            + $"{(named.Count == 0 ? "less" : "more")} than 1",
                            Field: field, Subjects: [shapeId]));
                        return layoutJson;
                    }
                    var fans = op["fan"] is not JsonValue fan || !fan.TryGetValue<bool>(out var stated) || stated;
                    if (named[0] == "pulls")
                    {
                        layoutJson = Applied(Pulled(op["pulls"], shapeId) is { } pulls
                                ? SketchGeometryEdit.PullShape(layoutJson, shapeId, pulls, fans)
                                : GeometryEdit.Refused(new Finding(RequestRules.Unreadable,
                                    $"point edit `{field}` states `pulls` that is not edges, each holding pairs of "
                                    + "fraction and blocks",
                                    Field: field, Subjects: [shapeId])),
                            layoutJson, field, shapeId, findings);
                        continue;
                    }
                    var at = op[named[0]]!.GetValue<int>();
                    double? x = op["x"]?.GetValue<double>(), z = op["z"]?.GetValue<double>();
                    if (fans && SketchGeometryEdit.RingOf(layoutJson, shapeId) is { } ring
                        && Symmetry.SelfImage(ring, board.Mode, board.CentreX, board.CentreZ) is { } images
                        && (named[0] != "index" || x is not null && z is not null))
                    {
                        layoutJson = AtEveryImage(named[0], at, x, z, ring, images, board, layoutJson, field, shapeId, findings);
                        continue;
                    }
                    var edit = named[0] switch
                    {
                        "remove" => SketchGeometryEdit.RemoveVertex(layoutJson, shapeId, at),
                        "index" when x is { } moveX && z is { } moveZ =>
                            SketchGeometryEdit.MoveVertex(layoutJson, shapeId, at, moveX, moveZ),
                        "index" => GeometryEdit.Refused(new Finding(RequestRules.Unreadable,
                            $"point edit `{field}` moves point {at} and states no `x` and `z`", Field: field,
                            Subjects: [shapeId])),
                        _ => SketchGeometryEdit.InsertVertex(layoutJson, shapeId, at, x, z, out _),
                    };
                    layoutJson = Applied(edit, layoutJson, field, shapeId, findings);
                }

        if (refinement["bendShapes"] is JsonObject bends)
            foreach (var (shapeId, stated) in bends)
            {
                var bend = stated?.Deserialize<ShapeBend>(SketchLayout.Json) ?? new ShapeBend(0, 0, 0);
                var edit = bend.ApplyTo(layoutJson, shapeId, out var held);
                layoutJson = Applied(edit, layoutJson, $"bendShapes.{shapeId}", shapeId, findings);
                if (held > 0)
                    findings.Add(new Finding(SketchRules.BendHeldBack,
                        $"bend of shape '{shapeId}' has {held} point{(held == 1 ? "" : "s")} with no room on the "
                        + "side it asks for", Severity.Complaint, Subjects: [shapeId]));
            }
        return layoutJson;
    }

    // The pulls an edit states, edge by edge, or null where they are not edges holding [fraction, blocks] pairs.
    private static Dictionary<int, IReadOnlyList<RingPull.Pull>>? Pulled(JsonNode? stated, string shapeId)
    {
        if (stated is not JsonObject edges) return null;
        var pulls = new Dictionary<int, IReadOnlyList<RingPull.Pull>>();
        foreach (var (key, along) in edges)
        {
            if (!int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out var edge) || along is not JsonArray pairs)
                return null;
            var points = new List<RingPull.Pull>();
            foreach (var pair in pairs)
            {
                if (pair is not JsonArray { Count: 2 } numbers
                    || numbers[0] is not JsonValue at || !at.TryGetValue<double>(out var fraction)
                    || numbers[1] is not JsonValue by || !by.TryGetValue<double>(out var blocks))
                    return null;
                points.Add(new RingPull.Pull(fraction, blocks));
            }
            pulls[edge] = points;
        }
        return pulls;
    }

    // One point edit to an outline the board's symmetry carries onto itself, made at every image of the point
    // it names. The images are worked out on the ring before any of them lands, then made from the highest
    // index down — and along one edge from the far end in — so no edit moves the index another one names. A
    // point the symmetry holds in place moves alone, and the finding says the outline is no longer its image.
    private static string AtEveryImage(
        string kind, int at, double? x, double? z, List<double[]> ring, int[][] images,
        (string Mode, double CentreX, double CentreZ) board, string layoutJson, string field, string shapeId,
        List<Finding> findings)
    {
        if (at < 0 || at >= ring.Count)
            return Applied(kind switch
            {
                "remove" => SketchGeometryEdit.RemoveVertex(layoutJson, shapeId, at),
                "index" => SketchGeometryEdit.MoveVertex(layoutJson, shapeId, at, x!.Value, z!.Value),
                _ => SketchGeometryEdit.InsertVertex(layoutJson, shapeId, at, x, z, out _),
            }, layoutJson, field, shapeId, findings);

        (double X, double Z) Image((double X, double Z) point, int k) =>
            Symmetry.Point(point.X, point.Z, board.Mode, board.CentreX, board.CentreZ, k);
        bool Same((double X, double Z) one, (double X, double Z) other) =>
            Math.Abs(one.X - other.X) < 0.01 && Math.Abs(one.Z - other.Z) < 0.01;

        switch (kind)
        {
            case "remove":
                foreach (var vertex in images.Select(lands => lands[at]).Append(at).Distinct().OrderDescending())
                    layoutJson = Applied(SketchGeometryEdit.RemoveVertex(layoutJson, shapeId, vertex),
                                         layoutJson, field, shapeId, findings);
                return layoutJson;

            case "index":
                var moved = (X: x!.Value, Z: z!.Value);
                var moves = new List<(int Vertex, (double X, double Z) To)> { (at, moved) };
                for (var k = 1; k <= images.Length; k++)
                {
                    var (vertex, to) = (images[k - 1][at], Image(moved, k));
                    if (vertex == at && !Same(to, moved))
                    {
                        findings.Add(new Finding(SourceRules.EditOffItsAxis,
                            $"point edit `{field}` moves fixed point {at} of shape '{shapeId}' to a place where it is "
                            + "not a fixed point", Severity.Complaint, Field: field, Subjects: [shapeId]));
                        continue;
                    }
                    if (!moves.Any(move => move.Vertex == vertex)) moves.Add((vertex, to));
                }
                foreach (var (vertex, to) in moves)
                    layoutJson = Applied(SketchGeometryEdit.MoveVertex(layoutJson, shapeId, vertex, to.X, to.Z),
                                         layoutJson, field, shapeId, findings);
                return layoutJson;

            default:
                var (from, next) = (ring[at], ring[(at + 1) % ring.Count]);
                var point = (X: x ?? (from[0] + next[0]) / 2, Z: z ?? (from[1] + next[1]) / 2);
                var inserts = new List<(int Edge, (double X, double Z) Point)> { (at, point) };
                for (var k = 1; k <= images.Length; k++)
                {
                    var (edge, landed) = (Symmetry.ImageEdge(images[k - 1], at), Image(point, k));
                    if (!inserts.Any(insert => insert.Edge == edge && Same(insert.Point, landed)))
                        inserts.Add((edge, landed));
                }
                foreach (var (edge, to) in inserts
                             .OrderByDescending(insert => insert.Edge)
                             .ThenByDescending(insert => Math.Abs(insert.Point.X - ring[insert.Edge][0])
                                                         + Math.Abs(insert.Point.Z - ring[insert.Edge][1])))
                    layoutJson = Applied(SketchGeometryEdit.InsertVertex(layoutJson, shapeId, edge, to.X, to.Z, out _),
                                         layoutJson, field, shapeId, findings);
                return layoutJson;
        }
    }

    // What the intent carries that a plan cannot state: the map's date, its credits, its capture points and the
    // score they end at, its generators and its shops. The capture points and the generators are stated once
    // each and fanned across the board's symmetry, the way the intent's own orbit fill fans them.
    private static void Play(JsonObject refinement, JsonObject intent, SymmetryIntent? board)
    {
        if (refinement["created"] is { } created)
            (intent["meta"] as JsonObject ?? (JsonObject)(intent["meta"] = new JsonObject()))["created"] = created.DeepClone();
        if (refinement["authors"] is JsonArray authors)
            (intent["meta"] as JsonObject ?? (JsonObject)(intent["meta"] = new JsonObject()))["authors"] = new JsonArray(
                [.. authors.Select(person => person is JsonValue name ? new JsonObject { ["name"] = name.DeepClone() } : person?.DeepClone())]);
        foreach (var key in (string[])["controlPoints", "scoreLimit", "spawners", "shops"])
            if (refinement.ContainsKey(key)) intent[key] = refinement[key]?.DeepClone();
        if (board is null) return;
        if (refinement["controlPoints"] is JsonArray points)
            intent["controlPoints"] = JsonSerializer.SerializeToNode(
                SymmetryExpander.FillControlPoints(points.Deserialize<List<ControlPointIntent>>(Json), board), Json);
        if (refinement["spawners"] is JsonArray spawners)
            intent["spawners"] = JsonSerializer.SerializeToNode(
                SymmetryExpander.FillSpawners(spawners.Deserialize<List<SpawnerIntent>>(Json), board), Json);
    }

    private static string Applied(GeometryEdit edit, string layoutJson, string field, string shapeId, List<Finding> findings)
    {
        if (edit.Layout is { } done) return done;
        findings.Add(edit.Refusal is { } refused
            ? refused.AsComplaint() with { Field = field }
            : NamesNothing(field[..field.IndexOf('.')], shapeId, ShapeIds(layoutJson)));
        return layoutJson;
    }

    private static IEnumerable<string> ShapeIds(string layoutJson) =>
        SketchLayout.Stack(SketchLayout.Stated(layoutJson)).SelectMany(layer => layer.Shapes).Select(shape => shape.Id);

    private static Finding NamesNothing(string key, string id, IEnumerable<string> drawn)
    {
        var have = drawn.Order(StringComparer.Ordinal).ToList();
        return new Finding(SourceRules.NamesNothing,
            $"`{key}` names '{id}', which "
            + (have.Count > 0
                ? $"is not one of {string.Join(", ", have.Take(24))}"
                  + (have.Count > 24 ? $" and {have.Count - 24} others" : "")
                : "the layout does not have"),
            Severity.Complaint, Field: $"{key}.{id}", Subjects: [id]);
    }

    private static Dictionary<string, string> Strings(JsonNode? node) =>
        (node as JsonObject)?.Where(pair => pair.Value is JsonValue)
            .ToDictionary(pair => pair.Key, pair => pair.Value!.GetValue<string>(), StringComparer.Ordinal) ?? [];

    private static string? Height(JsonObject shape) =>
        shape["base_height"] is JsonValue value && value.TryGetValue<double>(out var height)
            ? ((int)height).ToString(CultureInfo.InvariantCulture) : null;

    private static void Merge(JsonObject shape, JsonObject fields)
    {
        foreach (var (key, value) in fields)
        {
            if (key is "id") continue;
            if (value is null) shape.Remove(key);
            else shape[key] = value.DeepClone();
        }
    }

    private static string? FirstGroup(string layoutJson, string layerId) =>
        (JsonNode.Parse(layoutJson)?["layers"] as JsonArray)?.OfType<JsonObject>()
            .FirstOrDefault(layer => layer["id"]?.GetValue<string>() == layerId)?["layout"]?["groups"] is JsonArray groups
            ? groups.OfType<JsonObject>().Select(group => group["id"]?.GetValue<string>()).FirstOrDefault(id => id is { Length: > 0 })
            : null;

    private static IEnumerable<string> GroupsOf(JsonObject layout, string? layerId) =>
        (layout["layers"] as JsonArray)?.OfType<JsonObject>()
            .FirstOrDefault(layer => layer["id"]?.GetValue<string>() == layerId)?["layout"]?["groups"] is JsonArray groups
            ? groups.OfType<JsonObject>().Select(group => group["id"]?.GetValue<string>()).OfType<string>()
            : [];
}

/// <summary>A storey a refinement adds: its id and name, the height its ground starts at, what it holds and how it
/// meets the ground, whether it goes under the compiled ground, and its shapes and groups.</summary>
/// <param name="Id">What the rest of the document names it by.</param>
/// <param name="Name">What it is called on screen. Absent is its id.</param>
/// <param name="BaseY">The height its ground starts at, in blocks.</param>
/// <param name="Below">Whether it goes under the compiled ground rather than over it.</param>
/// <param name="Kind"><c>made</c> for a made thing rather than terrain.</param>
/// <param name="PartOf">The made thing it is a slice of.</param>
/// <param name="Seat"><c>ground</c> to settle it onto the terrain under it.</param>
/// <param name="Shapes">Its shapes.</param>
/// <param name="Groups">The groups they group into.</param>
public sealed record AddedLayer(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string? Name = null,
    [property: JsonPropertyName("base_y")] double BaseY = 0,
    [property: JsonPropertyName("below")] bool? Below = null,
    [property: JsonPropertyName("kind")] string? Kind = null,
    [property: JsonPropertyName("part_of")] string? PartOf = null,
    [property: JsonPropertyName("seat")] string? Seat = null,
    [property: JsonPropertyName("shapes")] List<SketchShape>? Shapes = null,
    [property: JsonPropertyName("groups")] List<SketchGroup>? Groups = null);

/// <summary>A name standing where a refinement states a material, a theme, a room style, a prop style or a
/// biome, instead of the thing itself: <c>{"use": name}</c> for a material the refinement's own
/// <c>materials</c> states, <c>{"library": name or id}</c> for a row of the studio's library. The fields stated
/// beside it are laid over the copy, member by member. Besides the registry entries the schema names it at, it
/// may stand for a material wherever one is stated, for a recipe in <c>dressing.styles</c> and for a room
/// style in <c>roomStyles</c>.</summary>
/// <param name="Use">A material the refinement's own <c>materials</c> states, by its name there.</param>
/// <param name="Library">A row of the studio's library, by its name or its id. A name two rows share is
/// refused, and the id names one.</param>
/// <param name="Row">The row the name resolved to when the source was applied, written by the studio into the
/// refinement the map keeps and read to say whether the row has moved on since.</param>
/// <param name="Hash">A hash of what that apply copied, written beside <paramref name="Row"/>.</param>
public sealed record StatedName(
    [property: JsonPropertyName(StatedName.UseKey)] string? Use = null,
    [property: JsonPropertyName(StatedName.LibraryKey)] JsonElement? Library = null,
    [property: JsonPropertyName(StatedName.RowKey)] long? Row = null,
    [property: JsonPropertyName(StatedName.HashKey)] string? Hash = null)
{
    public const string UseKey = "use", LibraryKey = "library", RowKey = "row", HashKey = "hash";
}

/// <summary>Where a shape drawn by <see cref="Refinement.AddShapes"/> joins the board, stated beside the shape's
/// own fields.</summary>
/// <param name="Layer">The layer it is drawn onto. Absent is the compiled ground.</param>
/// <param name="Group">The group it joins, which is a new group where the layer has none by that id. Absent is
/// the layer's first.</param>
public sealed record ShapeJoin(
    [property: JsonPropertyName(ShapeJoin.LayerKey)] string? Layer = null,
    [property: JsonPropertyName(ShapeJoin.GroupKey)] string? Group = null)
{
    public const string LayerKey = "layer", GroupKey = "group";
}

/// <summary>One point edit to an outline: exactly one of <see cref="After"/> (insert a point on that edge, at its
/// midpoint where no <c>x</c>/<c>z</c> is stated), <see cref="Index"/> (move that point to <c>x</c>/<c>z</c>),
/// <see cref="Remove"/> (drop that point) or <see cref="Pulls"/> (insert points along named edges, each pulled
/// across its edge). Every other point stays exactly where it was drawn.</summary>
/// <param name="After">Insert a point on the edge after this one.</param>
/// <param name="Index">Move this point to <c>x</c>/<c>z</c>.</param>
/// <param name="Remove">Drop this point.</param>
/// <param name="Pulls">Points stated where they stand, by edge: each edge named by the vertex it leaves on the
/// outline as this edit finds it, holding <c>[fraction, blocks]</c> pairs — a point that fraction of the way along
/// the edge, above 0 and below 1, moved that many blocks across it, into the ring where positive and out of it
/// where negative (<see cref="RingPull"/>). Every edge's points land together, so no pull renumbers another.</param>
/// <param name="X">Where the point goes on the x axis, in blocks.</param>
/// <param name="Z">Where the point goes on the z axis, in blocks.</param>
/// <param name="Fan">False makes this edit alone. Absent, an edit to an outline the board's symmetry carries
/// onto itself — a shape on the axis — is made at every image of the point it names, so the outline stays its
/// own image.</param>
public sealed record VertexEdit(
    [property: JsonPropertyName("after")] int? After = null,
    [property: JsonPropertyName("index")] int? Index = null,
    [property: JsonPropertyName("remove")] int? Remove = null,
    [property: JsonPropertyName("pulls")] IReadOnlyDictionary<int, IReadOnlyList<double[]>>? Pulls = null,
    [property: JsonPropertyName("x")] double? X = null,
    [property: JsonPropertyName("z")] double? Z = null,
    [property: JsonPropertyName("fan")] bool? Fan = null);

/// <summary>An outline stated by its shape rather than its points: an ellipse about <see cref="At"/>, pulled in
/// and out by <see cref="Lobes"/> bulges reaching <see cref="Wobble"/> past it, and turned by <see cref="Turn"/>
/// (<see cref="Geom.Algorithms.LobedOutline"/>).</summary>
/// <param name="At">The centre, as an <c>[x, z]</c> pair.</param>
/// <param name="Radius">The radius along x before the turn, in blocks.</param>
/// <param name="RadiusZ">The radius along z before the turn. Absent is <paramref name="Radius"/>, a circle.</param>
/// <param name="Points">How many points it is drawn with.</param>
/// <param name="Lobes">How many bulges it carries.</param>
/// <param name="Wobble">How far a bulge reaches past the ellipse, as a fraction of the radius it swells, and as
/// far in between them: 0 is the plain ellipse, and under 1 so no trough reaches the centre.</param>
/// <param name="Phase">Where round the outline the first bulge stands, in radians.</param>
/// <param name="Turn">How far the whole outline is turned about its centre, in degrees.</param>
public sealed record Outline(
    [property: JsonPropertyName("at")] double[] At,
    [property: JsonPropertyName("radius")] double Radius,
    [property: JsonPropertyName("radiusZ")] double? RadiusZ = null,
    [property: JsonPropertyName("points")] int Points = 28,
    [property: JsonPropertyName("lobes")] int Lobes = 3,
    [property: JsonPropertyName("wobble")] double Wobble = 0,
    [property: JsonPropertyName("phase")] double Phase = 0,
    [property: JsonPropertyName("turn")] double Turn = 0)
{
    /// <summary>The points this outline is drawn as, or null with <paramref name="why"/> saying what stops it
    /// drawing a ring: no centre, a radius of nought, fewer than three points, or a wobble outside
    /// <c>[0, 1)</c>. Inside those every point stands off the centre at its own angle, so the ring never crosses
    /// itself.</summary>
    public double[][]? Drawn(out string? why)
    {
        why = At is not { Length: 2 } ? "has no `at`"
            : Radius <= 0 ? $"has a `radius` of {Radius}, not more than 0"
            : RadiusZ is <= 0 ? $"has a `radiusZ` of {RadiusZ}, not more than 0"
            : Points < 3 ? $"has {Points} points, less than 3"
            : Wobble is < 0 or >= 1 ? $"has a `wobble` of {Wobble}, not at least 0 and below 1"
            : null;
        return why is null
            ? Geom.Algorithms.LobedOutline.Of(At[0], At[1], Radius, RadiusZ ?? Radius, Points, Lobes, Wobble, Phase, Turn)
            : null;
    }
}

/// <summary>A refinement applied: the layout and intent it produced, and what it had to say about them.</summary>
public sealed record Refined(string LayoutJson, string IntentJson, Findings Findings);

/// <summary>The rules a map's source fires as it is applied.</summary>
public static class SourceRules
{
    /// <summary>A source for a map made from a refinement was built against a change, and the map has a later
    /// change that edited something.</summary>
    /// <remarks>Either add the edits the finding names to the <c>refinement</c>, then set the <c>after</c> of the
    /// source to the latest change number, or send the request again with <c>discard</c> set to the changes to
    /// drop.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Request)]
    public const string UnseenChange = "SR1";

    /// <summary>A refinement names a shape or a layer that the map does not have, or states an outline for a thing
    /// that has none.</summary>
    /// <remarks>Either change the id in the entry to the id of a shape or layer the finding names, or delete the
    /// entry from the <c>refinement</c>.</remarks>
    [Rule(RuleCategory.Unknown, RuleConcern.Request, RuleConcern.Terrain)]
    public const string NamesNothing = "SR2";

    /// <summary>A refinement adds a layer that has no id, or whose id another layer of the map already
    /// has.</summary>
    /// <remarks>Either set the <c>id</c> of the layer in <c>addLayers</c> to one no layer of the map has, or delete
    /// the layer from <c>addLayers</c>.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Request, RuleConcern.Terrain)]
    public const string LayerStatedTwice = "SR3";

    /// <summary>A point edit names more than one of after, index, remove and pulls, or none of them.</summary>
    /// <remarks>Change the edit in <c>editShapes</c> to one that sets exactly one of <c>after</c>, <c>index</c>,
    /// <c>remove</c> and <c>pulls</c>.</remarks>
    [Rule(RuleCategory.Malformed, RuleConcern.Request)]
    public const string EditStatesNoIndex = "SR4";

    /// <summary>A refinement names a material that its list of materials does not have.</summary>
    /// <remarks>Either add the material the <c>use</c> names to <c>materials</c>, or change the <c>use</c> to one
    /// of the names <c>materials</c> has.</remarks>
    [Rule(RuleCategory.Unknown, RuleConcern.Request)]
    public const string UsesNoMaterial = "SR5";

    /// <summary>A refinement names a library entry that the library does not have.</summary>
    /// <remarks>Change the <c>library</c> of the entry to the name or the id of a library entry of the same kind
    /// the finding names.</remarks>
    [Rule(RuleCategory.Unknown, RuleConcern.Request)]
    public const string NamesNoLibraryRow = "SR6";

    /// <summary>An outline in a refinement has no centre, a radius of 0 or less, fewer than 3 points, or a wobble
    /// below 0 or of 1 or more.</summary>
    /// <remarks>Set the <c>at</c> of the outline in <c>outlines</c> to [x, z], then set its <c>radius</c> to more
    /// than 0, its <c>points</c> to at least 3 and its <c>wobble</c> to at least 0 and below 1.</remarks>
    [Rule(RuleCategory.Malformed, RuleConcern.Request, RuleConcern.Terrain)]
    public const string OutlineDrawsNoRing = "SR7";

    /// <summary>A point edit moves a fixed point to a place where it is not a fixed point.</summary>
    /// <remarks>Either change the <c>x</c> and <c>z</c> of the edit in <c>editShapes</c> to a fixed point, or set
    /// the <c>fan</c> of the edit to <c>false</c>.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Request, RuleConcern.Terrain)]
    public const string EditOffItsAxis = "SR8";
}
