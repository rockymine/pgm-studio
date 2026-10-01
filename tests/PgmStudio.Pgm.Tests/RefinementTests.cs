using System.Text.Json.Nodes;
using PgmStudio.Pgm.Plan;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Tests;

/// <summary>
/// <see cref="Refinement.Apply"/> — everything a board states that its plan cannot, applied onto the documents
/// the plan compiled to. What is asserted is that each statement lands where its route would put it, that a
/// statement reaching nothing is said rather than dropped in silence, and that a statement which does not say
/// what it means refuses the whole source.
/// </summary>
public sealed class RefinementTests
{
    private const string Layout = """
        {"setup":{"mirror_mode":"rot_180","center":{"cx":0,"cz":0}},
         "layers":[{"id":"ground","name":"Ground","base_y":0,"layout":{
           "shapes":[
             {"id":"moor-12","type":"polygon","operation":"add","base_height":12,
              "vertices":[[0,0],[40,0],[40,40],[0,40]]},
             {"id":"crag-20","type":"polygon","operation":"add","base_height":20,
              "vertices":[[40,0],[60,0],[60,20],[40,20]]},
             {"id":"spawn-red","type":"rectangle","operation":"add","role":"spawn","intentRef":"red",
              "min_x":2,"max_x":8,"min_z":2,"max_z":8,"base_height":12}],
           "groups":[{"id":"moor","name":"Moor","shapeIds":["moor-12","crag-20"]}]}}]}
        """;

    private const string Intent = """{"meta":{"name":"Weirgate","authors":[],"contributors":[]}}""";

    private static Refined Apply(string refinement) => Refinement.Apply(refinement, Layout, Intent);

    private static JsonObject Shape(Refined refined, string id) =>
        JsonNode.Parse(refined.LayoutJson)!["layers"]!.AsArray().OfType<JsonObject>()
            .SelectMany(layer => layer["layout"]!["shapes"]!.AsArray().OfType<JsonObject>())
            .Single(shape => shape["id"]!.GetValue<string>() == id);

    private static JsonArray Layers(Refined refined) => JsonNode.Parse(refined.LayoutJson)!["layers"]!.AsArray();

    [Test]
    public async Task A_theme_by_height_paints_the_compiled_ground_and_one_by_id_wins_even_on_a_room()
    {
        var refined = Apply("""
            {"themeByHeight":{"12":"heath","20":"scree"},"themeById":{"crag-20":"granite","spawn-red":"yard"}}
            """);

        await Assert.That(Shape(refined, "moor-12")["theme"]!.GetValue<string>()).IsEqualTo("heath");
        await Assert.That(Shape(refined, "crag-20")["theme"]!.GetValue<string>()).IsEqualTo("granite");
        await Assert.That(Shape(refined, "spawn-red")["theme"]!.GetValue<string>()).IsEqualTo("yard")
            .Because("a room piece is not terrain, so only its own id reaches it");
        await Assert.That(refined.Findings).IsEmpty();
    }

    [Test]
    public async Task A_statement_naming_a_shape_the_board_does_not_have_is_a_complaint_listing_the_ones_it_has()
    {
        var refined = Apply("""{"themeById":{"s3":"heath","moor-12":"heath"}}""");

        var finding = refined.Findings.Single();
        await Assert.That(finding.Rule).IsEqualTo(SourceRules.NamesNothing);
        await Assert.That(finding.Severity).IsEqualTo(Severity.Complaint);
        await Assert.That(finding.Message).Contains("moor-12");
        await Assert.That(Shape(refined, "moor-12")["theme"]!.GetValue<string>()).IsEqualTo("heath")
            .Because("the rest of the refinement is applied");
    }

    [Test]
    public async Task A_storey_goes_over_the_compiled_ground_or_under_it_and_one_stated_twice_refuses()
    {
        var refined = Apply("""
            {"addLayers":[
              {"id":"deck","base_y":30,"shapes":[],"groups":[]},
              {"id":"cellar","base_y":0,"below":true,"kind":"made","shapes":[],"groups":[]}]}
            """);
        var twice = Apply("""{"addLayers":[{"id":"ground","base_y":10,"shapes":[],"groups":[]}]}""");

        await Assert.That(Layers(refined).Select(layer => layer!["id"]!.GetValue<string>()))
            .IsEquivalentTo(["cellar", "ground", "deck"]);
        await Assert.That(Layers(refined)[0]!["kind"]!.GetValue<string>()).IsEqualTo("made");
        await Assert.That(twice.Findings.Single().Rule).IsEqualTo(SourceRules.LayerStatedTwice);
        await Assert.That(twice.Findings.Refuses).IsTrue();
        await Assert.That(twice.LayoutJson).IsEqualTo(Layout).Because("a refused source changes nothing");
    }

    [Test]
    public async Task A_drawn_shape_joins_the_compiled_grounds_first_group_unless_it_names_another()
    {
        var refined = Apply("""
            {"addShapes":[
              {"id":"tor","type":"circle","operation":"add","center_x":20,"center_z":20,"radius":4},
              {"id":"quay","type":"rectangle","operation":"add","min_x":0,"max_x":4,"min_z":0,"max_z":4,
               "group":"harbour"},
              {"id":"lost","type":"circle","operation":"add","center_x":0,"center_z":0,"radius":2,"layer":"attic"}]}
            """);

        var groups = Layers(refined)[0]!["layout"]!["groups"]!.AsArray();
        var moor = groups.Single(group => group!["id"]!.GetValue<string>() == "moor")!["shapeIds"]!.AsArray();
        var harbour = groups.Single(group => group!["id"]!.GetValue<string>() == "harbour")!["shapeIds"]!.AsArray();
        await Assert.That(moor.Select(id => id!.GetValue<string>())).Contains("tor");
        await Assert.That(harbour.Select(id => id!.GetValue<string>())).IsEquivalentTo(["quay"]);
        await Assert.That(Shape(refined, "tor").ContainsKey("layer")).IsFalse();
        var lost = refined.Findings.Single();
        await Assert.That(lost.Rule).IsEqualTo(SourceRules.NamesNothing);
        await Assert.That(lost.Message).Contains("attic");
    }

    [Test]
    public async Task A_relief_for_every_group_reaches_the_compiled_grounds_groups_under_a_storey_added_below()
    {
        var refined = Apply("""
            {"addLayers":[{"id":"cellar","base_y":0,"below":true,"shapes":[],"groups":[{"id":"vault","shapeIds":[]}]}],
             "relief":{"*":{"base":6},"vault":{"base":2}}}
            """);

        var relief = JsonNode.Parse(refined.LayoutJson)!["relief"]!.AsObject();
        await Assert.That(relief.Select(pair => pair.Key)).IsEquivalentTo(["moor", "vault"]);
        await Assert.That(relief["moor"]!["base"]!.GetValue<double>()).IsEqualTo(6d);
        await Assert.That(relief["vault"]!["base"]!.GetValue<double>()).IsEqualTo(2d);
    }

    [Test]
    public async Task A_theme_registry_takes_its_first_theme_as_the_default_and_keeps_every_field_it_was_given()
    {
        var refined = Apply("""{"themes":{"heath":{"surface":{"material":{"kind":"solid","id":2}},"note":"kept"},"scree":{}}}""");

        var layout = JsonNode.Parse(refined.LayoutJson)!;
        await Assert.That(layout["mapTheme"]!.GetValue<string>()).IsEqualTo("heath");
        await Assert.That(layout["themes"]!["heath"]!["note"]!.GetValue<string>()).IsEqualTo("kept");
    }

    [Test]
    public async Task Point_edits_apply_in_order_and_a_bend_comes_after_them()
    {
        var refined = Apply("""
            {"editShapes":{"moor-12":[{"after":0},{"index":3,"x":44,"z":44},{"remove":1}]},
             "bendShapes":{"crag-20":{"wander":1.5,"step":5,"seed":3}}}
            """);

        var moor = Shape(refined, "moor-12")["vertices"]!.AsArray().Select(point => point!.ToJsonString()).ToList();
        await Assert.That(moor).IsEquivalentTo(["[0,0]", "[40,0]", "[44,44]", "[0,40]"])
            .Because("the insert lands at 1, the move is stated against the ring as it then is, and the remove "
                     + "drops the point the insert added");
        await Assert.That(Shape(refined, "crag-20")["vertices"]!.AsArray().Count).IsGreaterThan(4);
        await Assert.That(Shape(refined, "crag-20").ContainsKey("controls")).IsTrue();
    }

    [Test]
    public async Task A_bend_named_to_one_edge_cuts_into_that_edge_and_no_other()
    {
        var refined = Apply("""
            {"bendShapes":{"crag-20":{"wander":1.5,"step":5,"seed":3,"side":"in","edges":[1]}}}
            """);

        var crag = Shape(refined, "crag-20")["vertices"]!.AsArray()
            .Select(point => (X: point![0]!.GetValue<double>(), Z: point[1]!.GetValue<double>())).ToList();
        await Assert.That(refined.Findings.Count).IsEqualTo(0);
        await Assert.That(crag.Count).IsGreaterThan(4);
        await Assert.That(crag[..2]).IsEquivalentTo([(40.0, 0.0), (60.0, 0.0)]);
        await Assert.That(crag[^2..]).IsEquivalentTo([(60.0, 20.0), (40.0, 20.0)]);
        foreach (var (x, z) in crag[2..^2])
            await Assert.That(x <= 60 && z is > 0 and < 20).IsTrue()
                .Because($"({x}, {z}) is cut into edge 1, from (60, 0) to (60, 20), and pulled inward");
    }

    [Test]
    public async Task A_bend_naming_an_edge_the_outline_does_not_have_is_a_complaint_and_the_rest_lands()
    {
        var refined = Apply("""
            {"themeById":{"moor-12":"heath"},"bendShapes":{"crag-20":{"wander":1.5,"step":5,"seed":3,"edges":[4]}}}
            """);

        var finding = refined.Findings.Single();
        await Assert.That(finding.Severity).IsEqualTo(Severity.Complaint);
        await Assert.That(finding.Field).IsEqualTo("bendShapes.crag-20");
        await Assert.That(finding.Message).Contains("edge 4");
        await Assert.That(Shape(refined, "crag-20")["vertices"]!.AsArray().Count).IsEqualTo(4);
        await Assert.That(Shape(refined, "moor-12")["theme"]!.GetValue<string>()).IsEqualTo("heath");
    }

    [Test]
    public async Task An_outline_is_written_as_the_points_of_every_shape_mark_push_and_prop_carrying_its_id()
    {
        var refined = Apply("""
            {"relief":{"moor":{"marks":[{"id":"knoll","kind":"area","h":14}],"pushes":[{"id":"lift","amount":3}]}},
             "dressing":{"props":[{"kind":"fluid","id":"mere","shape":"pool","seed":1}]},
             "outlines":{"crag-20":{"at":[50,10],"radius":8,"radiusZ":6,"points":20,"wobble":0.1},
                         "knoll":{"at":[20,20],"radius":5,"points":16},
                         "lift":{"at":[20,20],"radius":9,"points":18,"lobes":4,"wobble":0.15},
                         "mere":{"at":[10,30],"radius":4,"radiusZ":3,"points":12}}}
            """);

        await Assert.That(refined.Findings.Count).IsEqualTo(0);
        var crag = Shape(refined, "crag-20");
        await Assert.That(crag["type"]!.GetValue<string>()).IsEqualTo("polygon");
        await Assert.That(crag["vertices"]!.AsArray().Count).IsEqualTo(20);
        var layout = JsonNode.Parse(refined.LayoutJson)!;
        await Assert.That(layout["relief"]!["moor"]!["marks"]![0]!["ring"]!.AsArray().Count).IsEqualTo(16);
        await Assert.That(layout["relief"]!["moor"]!["pushes"]![0]!["ring"]!.AsArray().Count).IsEqualTo(18);
        await Assert.That(layout["dressing"]!["props"]![0]!["points"]!.AsArray().Count).IsEqualTo(12);
    }

    [Test]
    public async Task An_outline_reaching_no_outline_of_its_own_or_nothing_at_all_is_a_complaint()
    {
        var refined = Apply("""
            {"outlines":{"spawn-red":{"at":[5,5],"radius":3},"nowhere":{"at":[0,0],"radius":3}}}
            """);

        await Assert.That(refined.Findings.Count).IsEqualTo(2);
        await Assert.That(refined.Findings.Refuses).IsFalse();
        await Assert.That(refined.Findings.Single(finding => finding.Field == "outlines.spawn-red").Message).Contains("spawn");
        await Assert.That(refined.Findings.Single(finding => finding.Field == "outlines.nowhere").Rule)
            .IsEqualTo(SourceRules.NamesNothing);
        await Assert.That(Shape(refined, "spawn-red")["type"]!.GetValue<string>()).IsEqualTo("rectangle");
    }

    [Test]
    public async Task An_outline_whose_troughs_reach_its_centre_refuses_the_source()
    {
        var refined = Apply("""{"outlines":{"crag-20":{"at":[50,10],"radius":8,"wobble":1}}}""");

        await Assert.That(refined.Findings.Single().Rule).IsEqualTo(SourceRules.OutlineDrawsNoRing);
        await Assert.That(refined.Findings.Refuses).IsTrue();
    }

    [Test]
    public async Task An_edit_naming_no_single_index_refuses_the_source()
    {
        var refined = Apply("""{"editShapes":{"moor-12":[{"x":1,"z":1}]}}""");

        await Assert.That(refined.Findings.Single().Rule).IsEqualTo(SourceRules.EditStatesNoIndex);
        await Assert.That(refined.Findings.Refuses).IsTrue();
    }

    [Test]
    public async Task An_edit_the_board_refuses_is_a_complaint_and_the_rest_still_lands()
    {
        var refined = Apply("""
            {"editShapes":{"spawn-red":[{"remove":0}],"moor-12":[{"index":1,"x":42,"z":0}]}}
            """);

        await Assert.That(refined.Findings.Single().Severity).IsEqualTo(Severity.Complaint);
        await Assert.That(refined.Findings.Single().Field).IsEqualTo("editShapes.spawn-red[0]");
        await Assert.That(Shape(refined, "moor-12")["vertices"]![1]!.ToJsonString()).IsEqualTo("[42,0]");
    }

    [Test]
    public async Task What_the_intent_cannot_be_compiled_to_is_written_onto_it()
    {
        var refined = Apply("""
            {"created":"2026-09-30","authors":["Opus 5",{"name":"rockymine","role":"author"}],
             "scoreLimit":250,"controlPoints":[{"name":"Mill","capture":{"min_x":0,"min_z":0,"max_x":4,"max_z":4}}]}
            """);

        var intent = JsonNode.Parse(refined.IntentJson)!;
        await Assert.That(intent["meta"]!["created"]!.GetValue<string>()).IsEqualTo("2026-09-30");
        await Assert.That(intent["meta"]!["name"]!.GetValue<string>()).IsEqualTo("Weirgate");
        await Assert.That(intent["meta"]!["authors"]![0]!["name"]!.GetValue<string>()).IsEqualTo("Opus 5");
        await Assert.That(intent["meta"]!["authors"]![1]!["role"]!.GetValue<string>()).IsEqualTo("author");
        await Assert.That(intent["scoreLimit"]!.GetValue<int>()).IsEqualTo(250);
        await Assert.That(intent["controlPoints"]!.AsArray().Count).IsEqualTo(1);
    }

    [Test]
    public async Task A_material_named_once_is_copied_wherever_it_is_used_with_what_is_stated_beside_it()
    {
        var refined = Apply("""
            {"materials":{"strata":{"kind":"layered","bands":[{"id":24,"thickness":2}]}},
             "themes":{"heath":{"surface":{"material":{"use":"strata"},"depth":1},"wall":{"use":"strata","seed":9}}}}
            """);

        var layout = JsonNode.Parse(refined.LayoutJson)!;
        var heath = layout["themes"]!["heath"]!;
        await Assert.That(heath["surface"]!["material"]!["kind"]!.GetValue<string>()).IsEqualTo("layered");
        await Assert.That(heath["wall"]!["kind"]!.GetValue<string>()).IsEqualTo("layered");
        await Assert.That(heath["wall"]!["seed"]!.GetValue<int>()).IsEqualTo(9)
            .Because("a field stated beside a name is laid over the copy");
        await Assert.That(heath["surface"]!["material"]!.AsObject().ContainsKey("seed")).IsFalse()
            .Because("each use is its own copy");
        await Assert.That(layout.AsObject().ContainsKey("materials")).IsFalse();
        await Assert.That(refined.Findings).IsEmpty();
    }

    [Test]
    public async Task A_material_used_by_a_name_the_registry_does_not_state_refuses_the_source()
    {
        var refined = Apply("""{"materials":{"strata":{"kind":"solid","id":1}},"themes":{"heath":{"wall":{"use":"sand"}}}}""");

        var finding = refined.Findings.Single();
        await Assert.That(finding.Rule).IsEqualTo(SourceRules.UsesNoMaterial);
        await Assert.That(finding.Refuses).IsTrue();
        await Assert.That(finding.Message).Contains("'strata'");
        await Assert.That(refined.LayoutJson).IsEqualTo(Layout).Because("a refused source changes nothing");
    }

    [Test]
    public async Task An_empty_refinement_leaves_both_documents_as_they_were()
    {
        var refined = Apply("{}");

        await Assert.That(JsonNode.DeepEquals(JsonNode.Parse(refined.LayoutJson), JsonNode.Parse(Layout))).IsTrue();
        await Assert.That(JsonNode.DeepEquals(JsonNode.Parse(refined.IntentJson), JsonNode.Parse(Intent))).IsTrue();
        await Assert.That(refined.Findings).IsEmpty();
    }
}
