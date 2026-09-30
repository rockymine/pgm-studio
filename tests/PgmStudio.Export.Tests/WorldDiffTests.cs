using System.Text.Json.Nodes;
using PgmStudio.Minecraft.Painting;
using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Export.Tests;

/// <summary>
/// <see cref="WorldDiff.Between"/> — the columns two builds of a board disagree on, sorted by what changed in
/// them: the ground's height, the block its surface is made of, or something standing on it. What is asserted is
/// that each kind of edit lands in its own class and only on the columns it touched.
/// </summary>
public sealed class WorldDiffTests
{
    private const string Plate = """
        {"setup":{"mirror_mode":"none","center":{"cx":0,"cz":0}},
         "layers":[{"id":"ground","base_y":0,"layout":{
           "shapes":[{"id":"a","type":"rectangle","operation":"add","min_x":-20,"min_z":-20,"max_x":20,"max_z":20,
                      "floor":0,"base_height":8}],
           "groups":[]}}]}
        """;

    private static BuiltWorld Build(string layout) => WorldBuilder.Build(layout, new MapIntent());

    private static string Edited(Action<JsonObject> edit)
    {
        var root = JsonNode.Parse(Plate)!.AsObject();
        edit(root);
        return root.ToJsonString();
    }

    [Test]
    public async Task The_same_documents_build_the_same_world()
    {
        var diff = WorldDiff.Between(Build(Plate), Build(Plate));

        await Assert.That(diff.Count).IsEqualTo(0);
    }

    [Test]
    public async Task A_widened_shape_changes_the_ground_of_the_strip_it_added_and_nothing_else()
    {
        var wider = Edited(root => root["layers"]![0]!["layout"]!["shapes"]![0]!["max_x"] = 24);

        var diff = WorldDiff.Between(Build(Plate), Build(wider));

        await Assert.That(diff.Ground.Count).IsEqualTo(4 * 40);
        await Assert.That(diff.Surface.Count + diff.Structure.Count).IsEqualTo(0);
        await Assert.That(diff.Ground.All(cell => cell.X is >= 20 and < 24 && cell.Z is >= -20 and < 20)).IsTrue();
    }

    [Test]
    public async Task A_new_theme_changes_the_surface_block_and_not_the_ground()
    {
        var quartz = new SolidMaterial(155);
        var theme = TerrainTheme.Default with
        {
            Surface = new TopBand(quartz, Depth: 1), Rim = new TopBand(quartz, Depth: 1), Fill = quartz, Wall = quartz,
        };
        var themed = Edited(root =>
        {
            root["themes"] = new JsonObject { ["crest"] = JsonNode.Parse(TerrainThemeJson.Serialize(theme)) };
            root["mapTheme"] = "crest";
        });

        var diff = WorldDiff.Between(Build(Plate), Build(themed));

        await Assert.That(diff.Surface.Count).IsEqualTo(40 * 40);
        await Assert.That(diff.Ground.Count).IsEqualTo(0);
    }

    [Test]
    public async Task A_made_thing_stood_on_the_ground_changes_structure_over_its_own_footprint()
    {
        var statue = Edited(root => root["layers"]!.AsArray().Add(JsonNode.Parse("""
            {"id":"statue","base_y":8,"kind":"made","layout":{
              "shapes":[{"id":"s","type":"rectangle","operation":"add","min_x":0,"min_z":0,"max_x":4,"max_z":4,
                         "floor":0,"base_height":3}],"groups":[]}}
            """)));

        var diff = WorldDiff.Between(Build(Plate), Build(statue));

        await Assert.That(diff.Structure.Count).IsEqualTo(16);
        await Assert.That(diff.Ground.Count + diff.Surface.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Changed_columns_read_as_runs_largest_first_with_the_box_to_find_each_in()
    {
        var edited = Edited(root =>
        {
            var shapes = root["layers"]![0]!["layout"]!["shapes"]!.AsArray();
            shapes[0]!["max_x"] = 24;
            shapes.Add(JsonNode.Parse("""
                {"id":"b","type":"rectangle","operation":"add","min_x":-30,"min_z":0,"max_x":-28,"max_z":2,
                 "floor":0,"base_height":8}
                """));
        });

        var runs = WorldDiff.Between(Build(Plate), Build(edited)).Runs(WorldDiff.Changes.Ground);

        await Assert.That(runs.Select(run => run.Cells)).IsEquivalentTo([160, 4]);
        var strip = runs[0];
        await Assert.That((strip.Box.X, strip.Box.Z, strip.Box.Width, strip.Box.Height)).IsEqualTo((20, -20, 4, 40));
    }

    [Test]
    public async Task The_picture_draws_every_board_that_changed()
    {
        var wider = Edited(root => root["layers"]![0]!["layout"]!["shapes"]![0]!["max_x"] = 24);
        var before = Build(Plate);
        var after = Build(wider);

        var png = WorldDiff.Between(before, after).Png(before, after, scale: 2);

        await Assert.That(png!.Take(8).ToArray()).IsEquivalentTo(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
    }
}
