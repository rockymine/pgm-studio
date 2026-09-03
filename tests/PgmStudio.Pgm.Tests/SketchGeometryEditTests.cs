using System.Text.Json.Nodes;
using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Pgm.Tests;

/// <summary>
/// The invariants a geometry edit holds: what it touches, what it leaves alone, and what it refuses.
///
/// <para>Every case starts from a layout carrying a field no model has, because the reason the editor splices
/// the tree rather than round-tripping the model is that a partial edit must not cost the caller the parts it
/// did not address.</para>
/// </summary>
public sealed class SketchGeometryEditTests
{
    private const string Board = """
        {"setup":{"mirror_mode":"rot_180"},
         "unknownToEveryReader":{"kept":true},
         "relief":{"i":{"base":8},"gone":{"base":3}},
         "layers":[
           {"id":"ground","base_y":0,"layout":{
             "shapes":[{"id":"s1","type":"rectangle","min_x":-4,"max_x":4,"min_z":-4,"max_z":4},
                       {"id":"s2","type":"circle","center_x":0,"center_z":0,"radius":3}],
             "groups":[{"id":"i","name":"I","mirrors":true,"shapeIds":["s1","s2"]}]}},
           {"id":"lid","base_y":12,"layout":{
             "shapes":[],"groups":[{"id":"gone","shapeIds":[]}]}}]}
        """;

    private static JsonObject Root(string json) => JsonNode.Parse(json)!.AsObject();
    private static JsonArray Shapes(string json, string layer) =>
        Root(json)["layers"]!.AsArray().OfType<JsonObject>()
            .First(entry => (string?)entry["id"] == layer)["layout"]!["shapes"]!.AsArray();
    private static JsonObject ShapeOf(string json, string id) =>
        Root(json)["layers"]!.AsArray().OfType<JsonObject>()
            .SelectMany(layer => layer["layout"]!["shapes"]!.AsArray().OfType<JsonObject>())
            .First(shape => (string?)shape["id"] == id);

    [Test]
    public async Task An_edit_leaves_every_field_no_reader_has_a_model_for()
    {
        var edited = SketchGeometryEdit.PatchShape(Board, "s1", new JsonObject { ["floor"] = 9 });
        await Assert.That((bool?)Root(edited.Layout!)["unknownToEveryReader"]!["kept"]).IsTrue();
    }

    [Test]
    public async Task A_patch_writes_the_stated_field_and_leaves_the_rest()
    {
        var edited = SketchGeometryEdit.PatchShape(Board, "s1", new JsonObject { ["floor"] = 9 });
        var shape = ShapeOf(edited.Layout!, "s1");
        await Assert.That((int?)shape["floor"]).IsEqualTo(9);
        await Assert.That((int?)shape["min_x"]).IsEqualTo(-4);
        await Assert.That((string?)shape["type"]).IsEqualTo("rectangle");
    }

    [Test]
    public async Task A_stated_null_takes_the_field_off()
    {
        var written = SketchGeometryEdit.PatchShape(Board, "s1", new JsonObject { ["relief_scope"] = "exclude" });
        var cleared = SketchGeometryEdit.PatchShape(written.Layout, "s1", new JsonObject { ["relief_scope"] = null });
        await Assert.That(ShapeOf(cleared.Layout!, "s1").ContainsKey("relief_scope")).IsFalse();
    }

    [Test]
    public async Task A_patch_keeps_the_id_it_is_addressed_by()
    {
        var edited = SketchGeometryEdit.PatchShape(Board, "s1", new JsonObject { ["id"] = "renamed" });
        await Assert.That((string?)ShapeOf(edited.Layout!, "s1")["id"]).IsEqualTo("s1");
    }

    [Test]
    [Arguments("role")]
    [Arguments("intentRef")]
    [Arguments("height_authored")]
    public async Task A_patch_refuses_the_three_fields_the_compiler_owns(string field)
    {
        var edited = SketchGeometryEdit.PatchShape(Board, "s1", new JsonObject { [field] = "wool" });
        await Assert.That(edited.Layout).IsNull();
        await Assert.That(edited.Refusal!.Field).IsEqualTo(field);
    }

    [Test]
    public async Task A_patch_of_an_id_that_names_nothing_is_missing_rather_than_refused()
    {
        var edited = SketchGeometryEdit.PatchShape(Board, "nobody", new JsonObject { ["floor"] = 9 });
        await Assert.That(edited.IsMissing).IsTrue();
    }

    [Test]
    public async Task An_added_shape_joins_the_group_it_names()
    {
        var edited = SketchGeometryEdit.AddShape(Board, "ground",
            new JsonObject { ["type"] = "rectangle" }, "i");
        var group = Root(edited.Layout!)["layers"]!.AsArray()[0]!["layout"]!["groups"]!.AsArray()[0]!;
        await Assert.That(group["shapeIds"]!.AsArray().Select(id => (string?)id))
            .IsEquivalentTo(new[] { "s1", "s2", edited.Id });
    }

    [Test]
    public async Task An_added_shape_naming_a_new_group_opens_it()
    {
        var edited = SketchGeometryEdit.AddShape(Board, "ground",
            new JsonObject { ["type"] = "rectangle" }, "east");
        var groups = Root(edited.Layout!)["layers"]!.AsArray()[0]!["layout"]!["groups"]!.AsArray();
        var opened = groups.OfType<JsonObject>().Single(group => (string?)group["id"] == "east");
        await Assert.That((bool?)opened["mirrors"]).IsTrue();
        await Assert.That(opened["shapeIds"]!.AsArray().Count).IsEqualTo(1);
    }

    [Test]
    public async Task A_shape_naming_no_group_on_a_grouped_layer_is_refused()
    {
        var edited = SketchGeometryEdit.AddShape(Board, "ground",
            new JsonObject { ["type"] = "rectangle" }, null);
        await Assert.That(edited.Layout).IsNull();
        await Assert.That(edited.Refusal!.Field).IsEqualTo("group");
        await Assert.That(edited.Refusal!.Message).Contains("[i]");
    }

    [Test]
    public async Task A_shape_naming_no_group_on_an_ungrouped_layer_is_taken()
    {
        var bare = SketchGeometryEdit.PutLayer(Board, "bare", new JsonObject { ["base_y"] = 30 });
        var edited = SketchGeometryEdit.AddShape(bare.Layout, "bare",
            new JsonObject { ["type"] = "rectangle" }, null);
        await Assert.That(Shapes(edited.Layout!, "bare").Count).IsEqualTo(1);
    }

    [Test]
    public async Task A_free_id_is_kept_and_a_taken_one_is_minted_from_the_type()
    {
        var kept = SketchGeometryEdit.AddShape(Board, "ground",
            new JsonObject { ["id"] = "east-pier", ["type"] = "rectangle" }, "i");
        await Assert.That(kept.Id).IsEqualTo("east-pier");

        var minted = SketchGeometryEdit.AddShape(Board, "ground",
            new JsonObject { ["id"] = "s1", ["type"] = "rectangle" }, "i");
        await Assert.That(minted.Id).IsEqualTo("rectangle-1");
    }

    [Test]
    public async Task A_removed_shape_leaves_no_group_listing_it()
    {
        var edited = SketchGeometryEdit.RemoveShape(Board, "s1");
        var group = Root(edited.Layout!)["layers"]!.AsArray()[0]!["layout"]!["groups"]!.AsArray()[0]!;
        await Assert.That(group["shapeIds"]!.AsArray().Select(id => (string?)id))
            .IsEquivalentTo(new[] { "s2" });
        await Assert.That(Shapes(edited.Layout!, "ground").Count).IsEqualTo(1);
    }

    [Test]
    public async Task Putting_a_layer_without_a_layout_keeps_the_shapes_it_carried()
    {
        var edited = SketchGeometryEdit.PutLayer(Board, "ground", new JsonObject { ["base_y"] = 4 });
        await Assert.That(Shapes(edited.Layout!, "ground").Count).IsEqualTo(2);
        await Assert.That((int?)Root(edited.Layout!)["layers"]!.AsArray()[0]!["base_y"]).IsEqualTo(4);
    }

    [Test]
    public async Task Putting_a_layer_with_a_layout_replaces_the_shapes_it_carried()
    {
        var edited = SketchGeometryEdit.PutLayer(Board, "ground", new JsonObject
        {
            ["layout"] = new JsonObject { ["shapes"] = new JsonArray(), ["groups"] = new JsonArray() },
        });
        await Assert.That(Shapes(edited.Layout!, "ground").Count).IsEqualTo(0);
    }

    [Test]
    public async Task Putting_an_id_the_stack_does_not_carry_adds_a_layer_at_the_end()
    {
        var edited = SketchGeometryEdit.PutLayer(Board, "roof", new JsonObject { ["base_y"] = 24 });
        var layers = Root(edited.Layout!)["layers"]!.AsArray();
        await Assert.That(layers.Count).IsEqualTo(3);
        await Assert.That((string?)layers[2]!["id"]).IsEqualTo("roof");
    }

    [Test]
    public async Task Removing_a_layer_drops_the_relief_of_every_group_that_lived_only_there()
    {
        var edited = SketchGeometryEdit.RemoveLayer(Board, "lid");
        var relief = Root(edited.Layout!)["relief"]!.AsObject();
        await Assert.That(relief.ContainsKey("i")).IsTrue();
        await Assert.That(relief.ContainsKey("gone")).IsFalse();
    }

    [Test]
    public async Task Removing_a_group_leaves_its_shapes_on_the_layer()
    {
        var edited = SketchGeometryEdit.RemoveGroup(Board, "ground", "i");
        await Assert.That(Shapes(edited.Layout!, "ground").Count).IsEqualTo(2);
        await Assert.That(Root(edited.Layout!)["layers"]!.AsArray()[0]!["layout"]!["groups"]!.AsArray().Count)
            .IsEqualTo(0);
    }

    [Test]
    public async Task A_layer_that_named_itself_is_addressed_by_its_id_and_one_that_did_not_by_its_position()
    {
        const string unnamed = """{"layers":[{"base_y":0,"layout":{"shapes":[],"groups":[]}}]}""";
        var edited = SketchGeometryEdit.PutLayer(unnamed, "layer0", new JsonObject { ["base_y"] = 6 });
        var layers = Root(edited.Layout!)["layers"]!.AsArray();
        await Assert.That(layers.Count).IsEqualTo(1);
        await Assert.That((int?)layers[0]!["base_y"]).IsEqualTo(6);
    }
}
