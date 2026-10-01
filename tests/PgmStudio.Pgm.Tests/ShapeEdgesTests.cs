using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Pgm.Tests;

/// <summary>
/// <see cref="ShapeEdges"/> — a shape's outline as the edges a point edit names. What has to hold is that each
/// edge is numbered by the vertex it leaves, and that what it says lies across an edge is what the layer covers
/// there: a neighbour along a seam, the image the board's symmetry draws, and void along a coast.
/// </summary>
public sealed class ShapeEdgesTests
{
    private static SketchLayout Board(string mode) => SketchLayout.Stated($$$"""
        {"setup":{"mirror_mode":"{{{mode}}}","center":{"cx":0,"cz":0}},
         "layers":[{"id":"ground","name":"Ground","base_y":0,"layout":{
           "shapes":[
             {"id":"moor-12","type":"polygon","operation":"add","base_height":12,
              "vertices":[[0,0],[40,0],[40,40],[0,40]]},
             {"id":"crag-20","type":"polygon","operation":"add","base_height":20,
              "vertices":[[40,0],[60,0],[60,20],[40,20]]},
             {"id":"spawn-red","type":"rectangle","operation":"add","role":"spawn",
              "min_x":2,"max_x":8,"min_z":2,"max_z":8,"base_height":12}],
           "groups":[{"id":"moor","name":"Moor","shapeIds":["moor-12","crag-20"]}]}}]}
        """)!;

    private static Dictionary<int, string> Rows(string text) =>
        text.Split('\n').Where(line => line.Length > 4 && int.TryParse(line[..4], out _))
            .ToDictionary(line => int.Parse(line[..4]), line => line);

    [Test]
    public async Task An_edge_is_named_by_the_vertex_it_leaves_and_says_what_stands_across_it()
    {
        var text = ShapeEdges.Text(Board("mirror_x"), "moor-12")!;
        var rows = Rows(text);

        await Assert.That(text).StartsWith("EDGES  moor-12  layer ground, group moor, drawn again by mirror_x about (0, 0)");
        await Assert.That(rows.Count).IsEqualTo(4);
        await Assert.That(rows[1]).Contains("(40, 0)").And.Contains("(40, 40)").And.Contains("40.0");
        await Assert.That(rows[1]).EndsWith("crag-20 0–0.5 · void 0.5–1")
            .Because("crag-20 stands against the first half of edge 1 and nothing against the rest");
        await Assert.That(rows[0]).EndsWith("void");
        await Assert.That(rows[3]).EndsWith("moor-12 (image)")
            .Because("edge 3 lies on the mirror, so what stands across it is the shape's own image");
    }

    [Test]
    public async Task A_shape_stating_its_bounds_says_it_has_no_outline_to_edit()
    {
        var text = ShapeEdges.Text(Board("rot_180"), "spawn-red")!;

        await Assert.That(text).Contains("a rectangle states no outline of its own");
        await Assert.That(Rows(text).Count).IsEqualTo(0);
    }

    [Test]
    public async Task An_id_no_shape_carries_has_no_edges()
    {
        await Assert.That(ShapeEdges.Text(Board("rot_180"), "nowhere")).IsNull();
    }
}
