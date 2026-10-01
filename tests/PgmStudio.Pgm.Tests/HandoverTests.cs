using System.Text.Json.Nodes;
using PgmStudio.Pgm.Plan;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Tests;

/// <summary>
/// <see cref="Handover.Of"/> — a change made to a board by hand, written as the edits that would make its source
/// state it. What is asserted is where each edit lands: in the refinement wherever the refinement has words for
/// what changed, stated whole where an edit inside an entry would lose the rest of it, and left as the document's
/// own edit where only the plan can state it.
/// </summary>
public sealed class HandoverTests
{
    private const string Layout = """
        {"setup":{"mirror_mode":"rot_180","center":{"cx":0,"cz":0}},
         "layers":[
           {"id":"ground","name":"Ground","base_y":0,"layout":{
             "shapes":[
               {"id":"moor-12","type":"polygon","operation":"add","base_height":12,"vertices":[[0,0],[40,0],[40,40],[0,40]]},
               {"id":"crag-20","type":"polygon","operation":"add","base_height":20,"vertices":[[40,0],[60,0],[60,20],[40,20]]},
               {"id":"tor","type":"circle","operation":"add","center_x":20,"center_z":20,"radius":4}],
             "groups":[{"id":"moor","name":"Moor","shapeIds":["moor-12","crag-20","tor"]}]}},
           {"id":"deck","name":"Deck","base_y":30,"layout":{
             "shapes":[{"id":"plank","type":"rectangle","operation":"add","min_x":0,"max_x":4,"min_z":0,"max_z":4}],
             "groups":[{"id":"boards","name":"Boards","shapeIds":["plank"]}]}}],
         "relief":{"moor":{"base":6,"reach":10}},
         "themes":{"heath":{"surface":{"material":{"kind":"solid","id":2}}}},
         "dressing":{"props":[{"kind":"tree","id":"fir-1","x":10,"z":4,"seed":3}]}}
        """;

    private const string Intent = """
        {"meta":{"name":"Weirgate","created":"2026-09-30"},
         "controlPoints":[{"name":"Mill","capture":{"min_x":0,"min_z":0,"max_x":4,"max_z":4}}]}
        """;

    private const string Refinement = """
        {"bendShapes":{"crag-20":{"wander":1.5,"step":5,"seed":3}},
         "addShapes":[{"id":"tor","type":"circle","operation":"add","center_x":20,"center_z":20,"radius":4}],
         "addLayers":[{"id":"deck","base_y":30,"shapes":[],"groups":[]}]}
        """;

    private static string Edited(string json, Action<JsonObject> edit)
    {
        var root = JsonNode.Parse(json)!.AsObject();
        edit(root);
        return root.ToJsonString();
    }

    private static JsonArray Shapes(JsonObject root, int layer = 0) =>
        root["layers"]![layer]!["layout"]!["shapes"]!.AsArray();

    private static JsonObject Shape(JsonObject root, string id) =>
        Shapes(root).OfType<JsonObject>().Single(shape => shape["id"]!.GetValue<string>() == id);

    /// <summary>What a hand edit of the layout hands over.</summary>
    private static IReadOnlyList<DocumentEdit> HandedOver(string after) =>
        Handover.Of(DocumentDiff.Between(MapDocuments.Layout, Layout, after), Refinement,
            (Layout, Intent), (after, Intent), applied: false);

    private static DocumentEdit At(IReadOnlyList<DocumentEdit> edits, string path) =>
        edits.Single(edit => edit.Path == path);

    [Test]
    public async Task A_theme_painted_by_hand_on_a_compiled_shape_is_stated_by_its_id()
    {
        var handed = HandedOver(Edited(Layout, root => Shape(root, "moor-12")["theme"] = "scree"));

        var edit = handed.Single();
        await Assert.That((edit.Document, edit.Path, edit.Op))
            .IsEqualTo((MapDocuments.Refinement, "themeById.moor-12", DocumentEdit.Set));
        await Assert.That(edit.Value.GetString()).IsEqualTo("scree");
    }

    [Test]
    public async Task An_outline_redrawn_by_hand_is_stated_whole_and_the_bend_that_would_redraw_it_goes()
    {
        var handed = HandedOver(Edited(Layout, root =>
            Shape(root, "crag-20")["vertices"] = JsonNode.Parse("[[40,0],[62,0],[60,20],[40,20]]")));

        var outline = At(handed, "shapePropsById.crag-20.vertices");
        await Assert.That(outline.Document).IsEqualTo(MapDocuments.Refinement);
        await Assert.That(outline.Value.GetRawText()).IsEqualTo("[[40,0],[62,0],[60,20],[40,20]]");
        var bend = At(handed, "bendShapes.crag-20");
        await Assert.That(bend.Op).IsEqualTo(DocumentEdit.Remove)
            .Because("the refinement would bend the outline drawn by hand a second time");
        await Assert.That(handed.Count).IsEqualTo(2);
    }

    [Test]
    public async Task A_prop_moved_by_hand_is_the_same_move_in_the_refinements_dressing()
    {
        var handed = HandedOver(Edited(Layout, root =>
        {
            var fir = root["dressing"]!["props"]![0]!;
            fir["x"] = 14;
            fir["z"] = 1;
        }));

        var edit = handed.Single();
        await Assert.That((edit.Document, edit.Path, edit.Op))
            .IsEqualTo((MapDocuments.Refinement, "dressing.props[fir-1]", DocumentEdit.Move));
        await Assert.That(edit.Value.GetProperty("x").GetDouble()).IsEqualTo(14d);
    }

    [Test]
    public async Task A_relief_edited_inside_a_group_hands_over_the_whole_group()
    {
        var handed = HandedOver(Edited(Layout, root =>
        {
            root["relief"]!["moor"]!["base"] = 8;
            root["relief"]!["moor"]!["reach"] = 12;
        }));

        var edit = handed.Single();
        await Assert.That((edit.Path, edit.Op)).IsEqualTo(("relief.moor", DocumentEdit.Set))
            .Because("a refinement stating `relief.moor.base` alone would lose the group's other fields");
        await Assert.That(edit.Value.GetProperty("reach").GetDouble()).IsEqualTo(12d);
        await Assert.That(edit.Before!.Value.GetProperty("base").GetDouble()).IsEqualTo(6d);
    }

    [Test]
    public async Task A_shape_drawn_by_hand_is_an_addShapes_entry_carrying_its_layer_and_group()
    {
        var handed = HandedOver(Edited(Layout, root =>
        {
            Shapes(root).Add(JsonNode.Parse("""{"id":"cairn","type":"circle","operation":"add","center_x":5,"center_z":5,"radius":2}"""));
            root["layers"]![0]!["layout"]!["groups"]![0]!["shapeIds"]!.AsArray().Add("cairn");
        }));

        var edit = handed.Single();
        await Assert.That((edit.Document, edit.Path, edit.Op))
            .IsEqualTo((MapDocuments.Refinement, "addShapes", DocumentEdit.Add))
            .Because("the group's list only names the shape the entry already joins to it");
        await Assert.That(edit.Value.GetProperty("layer").GetString()).IsEqualTo("ground");
        await Assert.That(edit.Value.GetProperty("group").GetString()).IsEqualTo("moor");
    }

    [Test]
    public async Task A_compiled_shape_taken_away_is_the_plans_to_state_and_is_handed_over_as_it_is()
    {
        var handed = HandedOver(Edited(Layout, root =>
        {
            Shapes(root).Remove(Shape(root, "moor-12"));
            var listed = root["layers"]![0]!["layout"]!["groups"]![0]!["shapeIds"]!.AsArray();
            listed.Remove(listed.First(id => id!.GetValue<string>() == "moor-12"));
        }));

        var edit = handed.Single();
        await Assert.That((edit.Document, edit.Path, edit.Op))
            .IsEqualTo((MapDocuments.Layout, "layers[ground].layout.shapes[moor-12]", DocumentEdit.Remove));
        await Assert.That(edit.Before).IsNotNull();
    }

    [Test]
    public async Task A_shape_or_storey_the_refinement_draws_is_edited_where_the_refinement_states_it()
    {
        var handed = HandedOver(Edited(Layout, root =>
        {
            Shape(root, "tor")["radius"] = 6;
            root["layers"]![1]!["base_y"] = 32;
            Shapes(root, 1)[0]!["max_x"] = 6;
        }));

        await Assert.That(handed.Select(edit => (edit.Document, edit.Path)))
            .IsEquivalentTo([
                (MapDocuments.Refinement, "addShapes[tor].radius"),
                (MapDocuments.Refinement, "addLayers[deck].base_y"),
                (MapDocuments.Refinement, "addLayers[deck].shapes[plank].max_x"),
            ]);
    }

    [Test]
    public async Task A_capture_point_or_a_date_changed_by_hand_hands_over_the_intents_member_whole()
    {
        var after = Edited(Intent, root =>
        {
            root["controlPoints"]![0]!["name"] = "Weir";
            root["meta"]!["created"] = "2026-10-01";
        });
        var handed = Handover.Of(DocumentDiff.Between(MapDocuments.Intent, Intent, after), Refinement,
            (Layout, Intent), (Layout, after), applied: false);

        await Assert.That(handed.Select(edit => (edit.Document, edit.Path, edit.Op)))
            .IsEquivalentTo([
                (MapDocuments.Refinement, "controlPoints", DocumentEdit.Set),
                (MapDocuments.Refinement, "created", DocumentEdit.Set),
            ]);
        await Assert.That(At(handed, "controlPoints").Value[0].GetProperty("name").GetString()).IsEqualTo("Weir");
    }

    [Test]
    public async Task An_apply_hands_over_its_plan_and_refinement_and_nothing_they_compile_to()
    {
        var layoutAfter = Edited(Layout, root => Shape(root, "moor-12")["theme"] = "scree");
        var refinementAfter = Edited(Refinement, root => root["themeById"] = new JsonObject { ["moor-12"] = "scree" });
        var edits = new[]
        {
            DocumentDiff.Between(MapDocuments.Refinement, Refinement, refinementAfter),
            DocumentDiff.Between(MapDocuments.Layout, Layout, layoutAfter),
        }.SelectMany(list => list).ToList();

        var handed = Handover.Of(edits, refinementAfter, (Layout, Intent), (layoutAfter, Intent), applied: true);

        await Assert.That(handed.Select(edit => (edit.Document, edit.Path)))
            .IsEquivalentTo([(MapDocuments.Refinement, "themeById")]);
    }
}
