using System.Text.Json;
using System.Text.Json.Nodes;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Tests;

/// <summary>
/// <see cref="DocumentDiff.Between"/> — the edits that take one version of a document to another. What is
/// asserted is what a reader leans on: a thing in a list is named by its id wherever it sits, so inserting one
/// shape does not read as every later shape changing; what an edit replaced travels with it; and every path
/// an edit names resolves in the document it describes.
/// </summary>
public sealed class DocumentDiffTests
{
    private const string Layout = """
        {"setup":{"mirror_mode":"rot_180","center":{"cx":0,"cz":0}},
         "layers":[{"id":"ground","base_y":0,"layout":{
           "shapes":[
             {"id":"s1","type":"rectangle","operation":"add","min_x":-20,"max_x":20,"min_z":-20,"max_z":20,"floor":8},
             {"id":"p1","type":"polygon","operation":"add","vertices":[[0,0],[10,0],[10,10],[0,10]]},
             {"id":"s2","type":"circle","operation":"add","center_x":30,"center_z":0,"radius":6}],
           "groups":[{"id":"g1","name":"Isle","shapeIds":["s1","p1","s2"]}]}}],
         "themes":{"meadow":{"surface":"grass"}},
         "relief":{"g1":{"base":4,"marks":[{"id":"m1","kind":"point","at":[0,0],"h":9}]}},
         "dressing":{"props":[{"kind":"tree","id":"t1","x":10,"z":4,"seed":3}]}}
        """;

    private static string Edited(Action<JsonObject> edit)
    {
        var root = JsonNode.Parse(Layout)!.AsObject();
        edit(root);
        return root.ToJsonString();
    }

    private static JsonArray Shapes(JsonObject root) => root["layers"]![0]!["layout"]!["shapes"]!.AsArray();

    private static IReadOnlyList<DocumentEdit> Diff(string after) =>
        DocumentDiff.Between(MapDocuments.Layout, Layout, after);

    [Test]
    public async Task The_same_document_has_no_edits_however_it_is_spelled()
    {
        var respelled = JsonNode.Parse(Layout)!.ToJsonString(new JsonSerializerOptions { WriteIndented = true })
            .Replace("\"floor\": 8", "\"floor\": 8.0");

        await Assert.That(Diff(Layout)).IsEmpty();
        await Assert.That(Diff(respelled)).IsEmpty()
            .Because("whitespace and a number's spelling are not a change to the document");
    }

    [Test]
    public async Task A_shape_inserted_ahead_of_the_others_is_one_add_and_nothing_else()
    {
        var after = Edited(root => Shapes(root).Insert(0, JsonNode.Parse(
            """{"id":"s9","type":"circle","operation":"subtract","center_x":0,"center_z":0,"radius":3}""")));

        var edit = (await Only(Diff(after), "layers[ground].layout.shapes"));
        await Assert.That(edit.Op).IsEqualTo(DocumentEdit.Add);
        await Assert.That(edit.Value.GetProperty("id").GetString()).IsEqualTo("s9");
        await Assert.That(edit.Before).IsNull();
        await Assert.That(edit.Says).Contains("s9");
    }

    [Test]
    public async Task A_removed_shape_is_named_by_its_id_and_carries_what_it_was()
    {
        var after = Edited(root => Shapes(root).RemoveAt(2));

        var removed = Diff(after).Single(edit => edit.Op == DocumentEdit.Remove);
        await Assert.That(removed.Path).IsEqualTo("layers[ground].layout.shapes[s2]");
        await Assert.That(removed.Value.ValueKind).IsEqualTo(JsonValueKind.Null);
        await Assert.That(removed.Before!.Value.GetProperty("radius").GetDouble()).IsEqualTo(6d);
    }

    [Test]
    public async Task A_placed_thing_that_moved_is_one_move_saying_how_far()
    {
        var after = Edited(root =>
        {
            var tree = root["dressing"]!["props"]![0]!.AsObject();
            tree["x"] = 13;
            tree["z"] = 8;
        });

        var move = await Only(Diff(after), "dressing.props[t1]");
        await Assert.That(move.Op).IsEqualTo(DocumentEdit.Move);
        await Assert.That(move.Value.GetProperty("x").GetInt32()).IsEqualTo(13);
        await Assert.That(move.Before!.Value.GetProperty("z").GetInt32()).IsEqualTo(4);
        await Assert.That(move.Says).Contains("5 blocks").Because("(10, 4) to (13, 8) is five blocks");
    }

    [Test]
    public async Task An_outline_edit_is_one_set_saying_which_vertices_moved_and_which_are_new()
    {
        var after = Edited(root => Shapes(root)[1]!["vertices"] = JsonNode.Parse("[[0,0],[12,0],[10,10],[5,12],[0,10]]"));

        var outline = await Only(Diff(after), "layers[ground].layout.shapes[p1].vertices");
        await Assert.That(outline.Op).IsEqualTo(DocumentEdit.Set);
        await Assert.That(outline.Before!.Value.GetArrayLength()).IsEqualTo(4);
        await Assert.That(outline.Value.GetArrayLength()).IsEqualTo(5);
        await Assert.That(outline.Says).Contains("1 of 4 vertices moved");
        await Assert.That(outline.Says).Contains("1 inserted");
    }

    [Test]
    public async Task A_member_stated_for_the_first_time_is_set_and_one_dropped_is_removed()
    {
        var after = Edited(root =>
        {
            var themes = root["themes"]!.AsObject();
            themes.Remove("meadow");
            themes["dunes"] = JsonNode.Parse("""{"surface":"sand"}""");
        });

        var edits = Diff(after);
        var dropped = edits.Single(edit => edit.Path == "themes.meadow");
        var stated = edits.Single(edit => edit.Path == "themes.dunes");
        await Assert.That(dropped.Op).IsEqualTo(DocumentEdit.Remove);
        await Assert.That(dropped.Before!.Value.GetProperty("surface").GetString()).IsEqualTo("grass");
        await Assert.That(stated.Op).IsEqualTo(DocumentEdit.Set);
        await Assert.That(stated.Before).IsNull().Because("the member held nothing before");
    }

    [Test]
    public async Task A_changed_value_carries_both_values()
    {
        var after = Edited(root => root["relief"]!["g1"]!["marks"]![0]!["h"] = 12);

        var edit = await Only(Diff(after), "relief.g1.marks[m1].h");
        await Assert.That(edit.Op).IsEqualTo(DocumentEdit.Set);
        await Assert.That(edit.Before!.Value.GetDouble()).IsEqualTo(9d);
        await Assert.That(edit.Value.GetDouble()).IsEqualTo(12d);
        await Assert.That(edit.Says).IsEqualTo("h 9 → 12");
    }

    [Test]
    public async Task A_list_whose_things_changed_order_is_one_set_of_the_whole_list()
    {
        var after = Edited(root =>
        {
            var shapes = Shapes(root);
            var first = shapes[0]!;
            shapes.RemoveAt(0);
            shapes.Add(first);
        });

        var edit = await Only(Diff(after), "layers[ground].layout.shapes");
        await Assert.That(edit.Op).IsEqualTo(DocumentEdit.Set)
            .Because("a shape's place in the list is its draw order, so a reorder is a change to the list");
    }

    [Test]
    public async Task A_list_without_ids_is_walked_by_index_while_its_length_holds()
    {
        const string intent = """{"spawns":[{"team":"red","yaw":0},{"team":"blue","yaw":180}]}""";

        var edits = DocumentDiff.Between(MapDocuments.Intent, intent, intent.Replace("\"yaw\":180", "\"yaw\":90"));

        await Assert.That(edits.Single().Path).IsEqualTo("spawns[1].yaw");
    }

    [Test]
    public async Task A_document_stated_for_the_first_time_is_a_set_of_each_member()
    {
        var edits = DocumentDiff.Between(MapDocuments.Plan, null, """{"plan":2,"pieces":[{"id":"a"}]}""");

        await Assert.That(edits.Select(edit => edit.Path)).IsEquivalentTo(["plan", "pieces"]);
        await Assert.That(edits.All(edit => edit.Op == DocumentEdit.Set && edit.Before is null)).IsTrue();
    }

    /// <summary>Every edit names a path that resolves: in the document after the change for everything but a
    /// removal, and in the document before it for a removal — the property that lets a reader apply one or
    /// highlight what it names.</summary>
    [Test]
    public async Task Every_path_an_edit_names_resolves_in_the_document_it_describes()
    {
        var after = Edited(root =>
        {
            Shapes(root).RemoveAt(2);
            Shapes(root).Insert(0, JsonNode.Parse("""{"id":"s9","type":"circle","center_x":0,"center_z":0,"radius":3}"""));
            Shapes(root)[1]!["floor"] = 10;
            root["dressing"]!["props"]![0]!["x"] = 20;
            root["relief"]!["g1"]!["base"] = 6;
            root["themes"]!["dunes"] = JsonNode.Parse("""{"surface":"sand"}""");
            root["layers"]![0]!["layout"]!["groups"]![0]!["shapeIds"] = JsonNode.Parse("""["s9","s1","p1"]""");
        });

        var edits = Diff(after);
        await Assert.That(edits.Count).IsGreaterThanOrEqualTo(6);
        foreach (var edit in edits)
        {
            var document = JsonNode.Parse(edit.Op == DocumentEdit.Remove ? Layout : after);
            await Assert.That(Resolve(document, edit.Path)).IsNotNull().Because($"{edit.Op} {edit.Path}");
        }
    }

    private static async Task<DocumentEdit> Only(IReadOnlyList<DocumentEdit> edits, string path)
    {
        await Assert.That(edits.Select(edit => edit.Path)).IsEquivalentTo([path]);
        return edits[0];
    }

    /// <summary>The node a path names: members by name, an element by its id in brackets or by its index.</summary>
    private static JsonNode? Resolve(JsonNode? node, string path)
    {
        foreach (var step in path.Split('.'))
        {
            var member = step.Contains('[') ? step[..step.IndexOf('[')] : step;
            if (member.Length > 0) node = node?[member];
            var rest = step[member.Length..];
            while (rest.StartsWith('['))
            {
                var key = rest[1..rest.IndexOf(']')];
                rest = rest[(key.Length + 2)..];
                node = int.TryParse(key, out var index) && node is JsonArray byIndex && index < byIndex.Count
                    ? byIndex[index]
                    : node?.AsArray().FirstOrDefault(element => element?["id"]?.GetValue<string>() == key);
            }
        }
        return node;
    }
}
