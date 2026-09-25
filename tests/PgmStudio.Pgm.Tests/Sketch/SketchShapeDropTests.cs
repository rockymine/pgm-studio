using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Pgm.Tests.Sketch;

/// <summary>
/// What a rebuild from the plan does not keep of the shapes a stored layout holds. Geometry is the plan's, so
/// the invariant is exact: every stored shape is either produced again by the compile, stands for an intent
/// entity, or is named as dropped — none is lost without a name.
/// </summary>
public sealed class SketchShapeDropTests
{
    private static string Layout(params string[] shapes) => $$"""
    { "layers": [{ "id": "ground", "base_y": 0, "layout": {
        "shapes": [ {{string.Join(",", shapes)}} ], "groups": [] } }] }
    """;

    private static string Shape(string id, string? intentRef = null) =>
        $$"""{ "id": "{{id}}", "type": "rectangle", "operation": "add", "min_x": 0, "min_z": 0, "max_x": 4, "max_z": 4{{(intentRef is null ? "" : $", \"role\": \"spawn\", \"intentRef\": \"{intentRef}\"")}} }""";

    [Test]
    public async Task Every_stored_shape_is_recompiled_projected_or_named_as_dropped()
    {
        var stored = Layout(Shape("plan-a"), Shape("plan-b"), Shape("circle-1"), Shape("red-room", "red"),
                            Shape("sketch-2"));
        var compiled = Layout(Shape("plan-a"), Shape("plan-c"));

        await Assert.That(SketchLayout.DroppedShapes(compiled, stored))
            .IsEquivalentTo(["plan-b", "circle-1", "sketch-2"]);
    }

    [Test]
    public async Task A_map_with_nothing_stored_drops_nothing()
    {
        await Assert.That(SketchLayout.DroppedShapes(Layout(Shape("plan-a")), null)).IsEmpty();
    }
}
