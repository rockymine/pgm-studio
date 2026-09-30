using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using PgmStudio.Domain;
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

    /// <summary>Who the map credits: a bare name, or <c>{uuid, name, role, contribution}</c>.</summary>
    [JsonPropertyName("authors")] public List<JsonElement>? Authors { get; init; }

    /// <summary>The capture points, every one stated, already fanned.</summary>
    [JsonPropertyName("controlPoints")] public List<ControlPointIntent>? ControlPoints { get; init; }

    /// <summary>The score the match ends at.</summary>
    [JsonPropertyName("scoreLimit")] public int? ScoreLimit { get; init; }

    /// <summary>The generators the board mints from.</summary>
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
        var edited = Edit(refinement, finished.ToJsonString(), findings);
        if (findings.Any(finding => finding.Refuses)) return new(layoutJson, intentJson, new Findings(findings));

        Play(refinement, intent);
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
        if (node is JsonObject stated && stated["use"] is JsonValue name && name.TryGetValue<string>(out var used))
        {
            if (registry[used] is not { } material)
            {
                findings.Add(new Finding(SourceRules.UsesNoMaterial,
                    $"{path} uses the material '{used}', which `materials` does not state"
                    + (registry.Count > 0 ? $" — it states {string.Join(", ", registry.Select(pair => $"'{pair.Key}'"))}" : ""),
                    Field: $"refinement.{path}.use"));
                return node;
            }
            var beside = new JsonObject();
            foreach (var (field, value) in stated)
                if (field != "use") beside[field] = value?.DeepClone();
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
        if (copy is JsonObject into && stated is JsonObject over && !over.ContainsKey("use") && !over.ContainsKey("library"))
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
                        ? $"addLayers[{index}] states no `id`, and a storey is found by its id"
                        : $"addLayers states layer '{id}', which the board already has — a stack holding two layers "
                          + "under one id has no single one to draw a shape onto",
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
            var layerId = shape["layer"]?.GetValue<string>() ?? groundId ?? SketchLayer.GroundId;
            var groupId = shape["group"]?.GetValue<string>();
            shape.Remove("layer");
            shape.Remove("group");
            groupId ??= FirstGroup(layoutJson, layerId);

            var edit = SketchGeometryEdit.AddShape(layoutJson, layerId, shape, groupId);
            if (edit.Layout is { } drawn) { layoutJson = drawn; continue; }
            findings.Add(edit.Refusal?.AsComplaint() ?? new Finding(SourceRules.NamesNothing,
                $"addShapes[{index}] names layer '{layerId}', which the board does not have, so the shape is not "
                + "drawn", Severity.Complaint, Field: $"addShapes[{index}].layer",
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

    // The outlines reshaped a point at a time, in order, and then bent: a bend resamples whatever ring it is given,
    // so every point edit comes first.
    private static string Edit(JsonObject refinement, string layoutJson, List<Finding> findings)
    {
        if (refinement["editShapes"] is JsonObject edits)
            foreach (var (shapeId, ops) in edits)
                foreach (var (op, index) in (ops as JsonArray ?? []).OfType<JsonObject>().Select((op, index) => (op, index)))
                {
                    var field = $"editShapes.{shapeId}[{index}]";
                    var named = ((string[])["after", "index", "remove"]).Where(op.ContainsKey).ToList();
                    if (named.Count != 1)
                    {
                        findings.Add(new Finding(SourceRules.EditStatesNoIndex,
                            $"{field} names {(named.Count == 0 ? "no index" : string.Join(" and ", named))}; an edit "
                            + "states exactly one of `after` (insert), `index` (move) or `remove` (drop)",
                            Field: field, Subjects: [shapeId]));
                        return layoutJson;
                    }
                    var at = op[named[0]]!.GetValue<int>();
                    double? x = op["x"]?.GetValue<double>(), z = op["z"]?.GetValue<double>();
                    var edit = named[0] switch
                    {
                        "remove" => SketchGeometryEdit.RemoveVertex(layoutJson, shapeId, at),
                        "index" when x is { } moveX && z is { } moveZ =>
                            SketchGeometryEdit.MoveVertex(layoutJson, shapeId, at, moveX, moveZ),
                        "index" => GeometryEdit.Refused(new Finding(RequestRules.Unreadable,
                            $"{field} moves vertex {at} and states no `x` and `z` to move it to", Field: field,
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
                        $"{held} of the points cut into '{shapeId}' had no room on the side asked for and stayed on "
                        + "the edge they were cut from", Severity.Complaint, Subjects: [shapeId]));
            }
        return layoutJson;
    }

    // What the intent carries that a plan cannot state: the map's date, its credits, its capture points and the
    // score they end at, its generators and its shops.
    private static void Play(JsonObject refinement, JsonObject intent)
    {
        if (refinement["created"] is { } created)
            (intent["meta"] as JsonObject ?? (JsonObject)(intent["meta"] = new JsonObject()))["created"] = created.DeepClone();
        if (refinement["authors"] is JsonArray authors)
            (intent["meta"] as JsonObject ?? (JsonObject)(intent["meta"] = new JsonObject()))["authors"] = new JsonArray(
                [.. authors.Select(person => person is JsonValue name ? new JsonObject { ["name"] = name.DeepClone() } : person?.DeepClone())]);
        foreach (var key in (string[])["controlPoints", "scoreLimit", "spawners", "shops"])
            if (refinement.ContainsKey(key)) intent[key] = refinement[key]?.DeepClone();
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
            $"{key} names '{id}', which the board does not have"
            + (have.Count > 0 ? $" — it has {string.Join(", ", have.Take(24))}{(have.Count > 24 ? ", …" : "")}" : ""),
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

/// <summary>One point edit to an outline: exactly one of <see cref="After"/> (insert a point on that edge, at its
/// midpoint where no <c>x</c>/<c>z</c> is stated), <see cref="Index"/> (move that point to <c>x</c>/<c>z</c>) or
/// <see cref="Remove"/> (drop that point). Every other point stays exactly where it was drawn.</summary>
/// <param name="After">Insert a point on the edge after this one.</param>
/// <param name="Index">Move this point to <c>x</c>/<c>z</c>.</param>
/// <param name="Remove">Drop this point.</param>
/// <param name="X">Where the point goes on the x axis, in blocks.</param>
/// <param name="Z">Where the point goes on the z axis, in blocks.</param>
public sealed record VertexEdit(
    [property: JsonPropertyName("after")] int? After = null,
    [property: JsonPropertyName("index")] int? Index = null,
    [property: JsonPropertyName("remove")] int? Remove = null,
    [property: JsonPropertyName("x")] double? X = null,
    [property: JsonPropertyName("z")] double? Z = null);

/// <summary>A refinement applied: the layout and intent it produced, and what it had to say about them.</summary>
public sealed record Refined(string LayoutJson, string IntentJson, Findings Findings);

/// <summary>The rules a map's source fires as it is applied.</summary>
public static class SourceRules
{
    /// <summary>A source is applied over a change it has not seen: a map made from a refinement was changed —
    /// by hand in a tool, by another writer's source — after the change the source states it was built against.
    /// Applying it would replace that change without a word, so the source is refused 409 and the change is
    /// handed over: one finding per edit it made, naming the change in <c>subjects</c> and stating the edit as
    /// the source would state it — into the refinement where the refinement has words for it, as the plan's,
    /// the layout's or the intent's own edit where it does not.</summary>
    /// <remarks>Take the edits into the source and state the change as <c>after</c>, or name the changes to drop
    /// in <c>?discard=</c>, which the change the source lands as records. A map with no refinement is never
    /// refused this way.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Request)]
    public const string UnseenChange = "SR1";

    /// <summary><b>A complaint.</b> A refinement statement names a shape or a layer the board does not have — a
    /// theme or fields by a shape id, a point edit or a bend, a shape drawn onto a layer — so it reaches nothing
    /// and the board stores without it. A compiled shape answers to its component's first piece and the surface
    /// it stands at, so a piece renamed or moved to another height renames what the statement anchors to.</summary>
    /// <remarks>Re-key the statement to one of the ids the finding lists, which are the shapes the board has.</remarks>
    [Rule(RuleCategory.Unknown, RuleConcern.Request, RuleConcern.Terrain)]
    public const string NamesNothing = "SR2";

    /// <summary>A refinement adds a storey under an id the board already has, or under none. A stack holding two
    /// layers under one id has no single one for a shape to be drawn onto.</summary>
    /// <remarks>Give the storey an id no layer of the board carries.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Request, RuleConcern.Terrain)]
    public const string LayerStatedTwice = "SR3";

    /// <summary>A point edit states none, or more than one, of <c>after</c>, <c>index</c> and <c>remove</c>, so it
    /// does not say which point it is about.</summary>
    /// <remarks>State exactly one: <c>after</c> inserts a point on that edge, <c>index</c> moves that point to
    /// <c>x</c>/<c>z</c>, and <c>remove</c> drops it.</remarks>
    [Rule(RuleCategory.Malformed, RuleConcern.Request)]
    public const string EditStatesNoIndex = "SR4";

    /// <summary>A refinement uses a material by a name its <c>materials</c> registry does not state, so there is
    /// nothing to copy where the name stands.</summary>
    /// <remarks>State the material under that name in <c>materials</c>, or use one of the names the finding
    /// lists.</remarks>
    [Rule(RuleCategory.Unknown, RuleConcern.Request)]
    public const string UsesNoMaterial = "SR5";

    /// <summary>A refinement names a library row — a material, a theme, a room style, a prop style or a biome — by
    /// a name no row of that kind carries, or one several rows carry, so there is no single row to copy.</summary>
    /// <remarks>Name the row by a name the finding lists, or by its id where several rows share the name.</remarks>
    [Rule(RuleCategory.Unknown, RuleConcern.Request)]
    public const string NamesNoLibraryRow = "SR6";
}
