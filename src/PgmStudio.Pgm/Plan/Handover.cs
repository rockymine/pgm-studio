using System.Text.Json;
using System.Text.Json.Nodes;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Plan;

/// <summary>
/// A change to a map's documents written as the edits that would make its source state it: into the refinement
/// wherever the refinement has words for what changed, and as the change's own edit to the plan, the layout or the
/// intent where only the plan can state it. A source that takes these into its refinement builds the board the
/// change left.
/// </summary>
public static class Handover
{
    /// <summary>The intent's members a refinement states whole.</summary>
    private static readonly string[] PlayMembers = ["controlPoints", "scoreLimit", "spawners", "shops"];

    /// <summary>The layout's members a refinement states entry by entry, and the ones it states whole.</summary>
    private static readonly string[] Registries = ["themes", "relief", "roomStyles"];
    private static readonly string[] Wholes = ["mapTheme", "biome"];

    /// <summary>The fields of a shape that draw its outline.</summary>
    private static readonly string[] OutlineFields = ["vertices", "controls"];

    /// <summary>
    /// <paramref name="edits"/>, one change's edits to the map's documents, as edits to its source.
    /// <paramref name="refinement"/> is the refinement the map was last applied with, which says which shapes and
    /// storeys it draws and which outlines it reshapes; <paramref name="before"/> and <paramref name="after"/> are
    /// the layout and intent either side of the change, whose whole entries an edit inside one hands over.
    ///
    /// <para>A change that wrote the refinement was itself an apply: its layout and intent follow from its plan and
    /// refinement, so <paramref name="applied"/> hands over those two alone.</para>
    /// </summary>
    public static IReadOnlyList<DocumentEdit> Of(
        IReadOnlyList<DocumentEdit> edits, string? refinement,
        (string? Layout, string? Intent) before, (string? Layout, string? Intent) after, bool applied)
    {
        var reading = new Reading(Parse(refinement), Parse(before.Layout), Parse(after.Layout),
            Parse(before.Intent), Parse(after.Intent), edits);
        var handed = new List<DocumentEdit>();
        foreach (var edit in edits)
            switch (edit.Document)
            {
                case MapDocuments.Plan or MapDocuments.Refinement:
                    handed.Add(edit);
                    break;
                case MapDocuments.Layout when !applied:
                    handed.AddRange(reading.Layout(edit));
                    break;
                case MapDocuments.Intent when !applied:
                    handed.AddRange(reading.Intent(edit));
                    break;
            }

        return
        [
            .. handed.GroupBy(edit => (edit.Document, edit.Path, edit.Op))
                .Select(group => group.First() with
                {
                    Says = string.Join(", ", group.Select(edit => edit.Says).Distinct(StringComparer.Ordinal)),
                }),
        ];
    }

    private sealed class Reading(
        JsonObject refinement, JsonObject layoutBefore, JsonObject layoutAfter, JsonObject intentBefore,
        JsonObject intentAfter, IReadOnlyList<DocumentEdit> edits)
    {
        private readonly HashSet<string> drawn = Ids(refinement["addShapes"]);
        private readonly HashSet<string> storeys = Ids(refinement["addLayers"]);
        private readonly JsonObject reshapings = new()
        {
            ["editShapes"] = refinement["editShapes"]?.DeepClone(), ["bendShapes"] = refinement["bendShapes"]?.DeepClone(),
        };

        public IEnumerable<DocumentEdit> Layout(DocumentEdit edit)
        {
            var head = Head(edit.Path);
            if (head == "layers") return Layers(edit);
            if (head == "dressing") return [edit with { Document = MapDocuments.Refinement }];
            if (Registries.Contains(head))
            {
                var entry = Entry(edit.Path);
                return entry is null
                    ? [Whole(head, layoutBefore[head], layoutAfter[head], edit.Says)]
                    : [Whole($"{head}.{entry}", layoutBefore[head]?[entry], layoutAfter[head]?[entry], edit.Says)];
            }
            if (Wholes.Contains(head)) return [Whole(head, layoutBefore[head], layoutAfter[head], edit.Says)];
            return [edit];
        }

        public IEnumerable<DocumentEdit> Intent(DocumentEdit edit)
        {
            var head = Head(edit.Path);
            if (PlayMembers.Contains(head)) return [Whole(head, intentBefore[head], intentAfter[head], edit.Says)];
            if (head == "meta" && Entry(edit.Path) is "created" or "authors")
            {
                var member = Entry(edit.Path)!;
                return [Whole(member, intentBefore["meta"]?[member], intentAfter["meta"]?[member], edit.Says)];
            }
            return [edit];
        }

        // A storey the refinement adds is restated under `addLayers`, and anything else on the layers is the
        // compiled ground: a shape drawn onto it is an `addShapes` entry, a field of a compiled shape is stated by
        // its id, and a compiled shape taken away is the plan's to state.
        private IEnumerable<DocumentEdit> Layers(DocumentEdit edit)
        {
            if (edit.Path == "layers")
                return edit.Op == DocumentEdit.Add && edit.Value.ValueKind == JsonValueKind.Object
                    ? [Storey(edit)]
                    : [edit];

            var layer = Key(edit.Path, "layers")!;
            var layerPath = $"layers[{layer}]";
            var rest = edit.Path[layerPath.Length..];
            if (storeys.Contains(layer))
            {
                var inside = rest.StartsWith(".layout", StringComparison.Ordinal) ? rest[".layout".Length..] : rest;
                return [edit with { Document = MapDocuments.Refinement, Path = $"addLayers[{layer}]{inside}" }];
            }
            if (rest.Length == 0) return [edit];

            var shapesPath = $"{layerPath}.layout.shapes";
            if (edit.Path == shapesPath)
                return edit.Op == DocumentEdit.Add && edit.Value.ValueKind == JsonValueKind.Object
                    ? [Drawn(edit, layer)]
                    : [edit];
            if (edit.Path.StartsWith(shapesPath + "[", StringComparison.Ordinal))
                return Shape(edit, shapesPath);
            if (edit.Path.StartsWith($"{layerPath}.layout.groups", StringComparison.Ordinal) && Explained(edit, layer))
                return [];
            return [edit];
        }

        private IEnumerable<DocumentEdit> Shape(DocumentEdit edit, string shapesPath)
        {
            var shape = Bracketed(edit.Path, shapesPath.Length)!;
            var shapePath = $"{shapesPath}[{shape}]";
            var field = edit.Path[shapePath.Length..];
            if (drawn.Contains(shape))
                return [edit with { Document = MapDocuments.Refinement, Path = $"addShapes[{shape}]{field}" }];
            if (field.Length == 0) return [edit];

            var name = Head(field.TrimStart('.'));
            if (name == "theme" && edit.Op == DocumentEdit.Set)
                return [edit with { Document = MapDocuments.Refinement, Path = $"themeById.{shape}" }];
            var stated = edit.Op == DocumentEdit.Remove ? edit with { Op = DocumentEdit.Set } : edit;
            var handed = new List<DocumentEdit>
            {
                stated with { Document = MapDocuments.Refinement, Path = $"shapePropsById.{shape}{field}" },
            };
            // An outline drawn by hand is the outline: the point edits and the bend the refinement makes to that
            // shape would be made again on top of it.
            if (OutlineFields.Contains(name))
                foreach (var reshaping in (string[])["editShapes", "bendShapes"])
                    if (reshapings[reshaping] is JsonObject by && by.ContainsKey(shape))
                        handed.Add(new DocumentEdit(MapDocuments.Refinement, $"{reshaping}.{shape}", DocumentEdit.Remove,
                            Element(null), $"{shape} is drawn by hand, so its {reshaping} statement goes",
                            Element(by[shape])));
            return handed;
        }

        private DocumentEdit Drawn(DocumentEdit edit, string layer)
        {
            var shape = JsonNode.Parse(edit.Value.GetRawText())!.AsObject();
            var id = shape["id"]?.GetValue<string>();
            shape["layer"] = layer;
            if (GroupOf(layer, id) is { } group) shape["group"] = group;
            return new DocumentEdit(MapDocuments.Refinement, "addShapes", DocumentEdit.Add, Element(shape), edit.Says);
        }

        private DocumentEdit Storey(DocumentEdit edit)
        {
            var layer = JsonNode.Parse(edit.Value.GetRawText())!.AsObject();
            var id = layer["id"]?.GetValue<string>();
            var added = new JsonObject { ["id"] = id, ["name"] = layer["name"]?.DeepClone(), ["base_y"] = layer["base_y"]?.DeepClone() };
            foreach (var key in (string[])["kind", "part_of", "seat"])
                if (layer[key] is { } value) added[key] = value.DeepClone();
            added["shapes"] = layer["layout"]?["shapes"]?.DeepClone() ?? new JsonArray();
            added["groups"] = layer["layout"]?["groups"]?.DeepClone() ?? new JsonArray();
            if (Below(id)) added["below"] = true;
            return new DocumentEdit(MapDocuments.Refinement, "addLayers", DocumentEdit.Add, Element(added), edit.Says);
        }

        // Whether a storey added in this change stands before the ground a refinement draws onto: the first layer
        // that is neither one of the refinement's storeys nor the one added.
        private bool Below(string? id)
        {
            var order = (layoutAfter["layers"] as JsonArray ?? []).OfType<JsonObject>()
                .Select(layer => layer["id"]?.GetValue<string>()).ToList();
            var ground = order.FindIndex(layer => layer is not null && layer != id && !storeys.Contains(layer));
            var at = order.IndexOf(id);
            return ground >= 0 && at >= 0 && at < ground;
        }

        private string? GroupOf(string layer, string? shape) =>
            (layoutAfter["layers"] as JsonArray ?? []).OfType<JsonObject>()
                .FirstOrDefault(entry => entry["id"]?.GetValue<string>() == layer)?["layout"]?["groups"] is JsonArray groups
                ? groups.OfType<JsonObject>().FirstOrDefault(group =>
                      (group["shapeIds"] as JsonArray ?? []).Any(listed => listed?.GetValue<string>() == shape))?["id"]
                  ?.GetValue<string>()
                : null;

        // A group edit that only lists the shapes this change drew or took away on the layer, which the handed
        // over shapes already carry.
        private bool Explained(DocumentEdit edit, string layer)
        {
            var shapes = $"layers[{layer}].layout.shapes";
            var moved = edits
                .Where(other => other.Document == MapDocuments.Layout)
                .Select(other => other.Path == shapes && other.Op == DocumentEdit.Add
                    ? other.Value.TryGetProperty("id", out var id) ? id.GetString() : null
                    : other.Op == DocumentEdit.Remove && other.Path.StartsWith(shapes + "[", StringComparison.Ordinal)
                      && other.Path.EndsWith(']')
                        ? Bracketed(other.Path, shapes.Length)
                        : null)
                .OfType<string>().ToHashSet(StringComparer.Ordinal);

            IEnumerable<string> Listed(JsonElement? value) =>
                value is { ValueKind: JsonValueKind.Array } list
                    ? list.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString()!)
                    : value is { ValueKind: JsonValueKind.Object } group && group.TryGetProperty("shapeIds", out var ids)
                        ? Listed(ids)
                        : [];

            var changed = Listed(edit.Value).ToHashSet(StringComparer.Ordinal);
            changed.SymmetricExceptWith(Listed(edit.Before));
            return edit.Path.EndsWith("shapeIds", StringComparison.Ordinal) || edit.Op == DocumentEdit.Add
                ? changed.Count > 0 && changed.IsSubsetOf(moved)
                : false;
        }
    }

    // An entry stated whole: its value as the change left it, or its removal where the change took it away.
    private static DocumentEdit Whole(string path, JsonNode? was, JsonNode? now, string says) =>
        now is null
            ? new DocumentEdit(MapDocuments.Refinement, path, DocumentEdit.Remove, Element(null), says, Element(was))
            : new DocumentEdit(MapDocuments.Refinement, path, DocumentEdit.Set, Element(now), says,
                was is null ? null : Element(was));

    private static string Head(string path)
    {
        var end = path.IndexOfAny(['.', '[']);
        return end < 0 ? path : path[..end];
    }

    // The member after the head: `relief.team.base` → `team`.
    private static string? Entry(string path)
    {
        var dot = path.IndexOf('.');
        if (dot < 0) return null;
        var rest = path[(dot + 1)..];
        var end = rest.IndexOfAny(['.', '[']);
        return end < 0 ? rest : rest[..end];
    }

    // The key in brackets after `member`: `layers[ground].layout` → `ground`.
    private static string? Key(string path, string member) =>
        path.StartsWith(member + "[", StringComparison.Ordinal) ? Bracketed(path, member.Length) : null;

    // The key in the brackets opening at `open`.
    private static string? Bracketed(string path, int open)
    {
        if (open >= path.Length || path[open] != '[') return null;
        var close = path.IndexOf(']', open);
        return close < 0 ? null : path[(open + 1)..close];
    }

    private static HashSet<string> Ids(JsonNode? list) =>
        (list as JsonArray ?? []).OfType<JsonObject>().Select(entry => entry["id"]?.GetValue<string>())
            .OfType<string>().ToHashSet(StringComparer.Ordinal);

    private static JsonObject Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonNode.Parse(json) as JsonObject ?? []; }
        catch (JsonException) { return []; }
    }

    private static JsonElement Element(JsonNode? node) => JsonSerializer.SerializeToElement(node);
}
