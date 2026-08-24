using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Pgm.Tests.Sketch;

/// <summary>
/// <see cref="SketchLayout.Stack"/> — a layout is composed of layers, and the ground is one of them.
///
/// <para>The document holds <c>layers[]</c> and nothing beside it, so a flat board is a stack of one. What
/// these pin is that there is no second place shapes can live: a document naming them anywhere else draws
/// nothing rather than drawing them, which is what stops a gate quantifying over shapes the rasterizer will
/// never build.</para>
/// </summary>
public sealed class SketchLayoutStackTests
{
    private const string Square =
        @"{""shapes"":[{""id"":""a"",""type"":""rectangle"",""operation"":""add""," +
        @"""min_x"":0,""min_z"":0,""max_x"":3,""max_z"":3,""base_height"":4}],""islands"":[]}";

    private const string Setup = @"""setup"":{""mirror_mode"":""none"",""center"":{""cx"":0,""cz"":0}}";

    /// <summary>A document naming its shapes under the given top-level key.</summary>
    private static string Under(string key) => "{" + Setup + @",""" + key + @""":" + Square + "}";

    /// <summary>A document holding one layer, spelled by its body.</summary>
    private static string Stacked(string layer) => "{" + Setup + @",""layers"":[" + layer + "]}";

    private const string GroundLayer = @"{""id"":""ground"",""base_y"":0,""layout"":" + Square + "}";

    [Test]
    public async Task A_flat_board_is_a_stack_of_one()
    {
        var stack = SketchLayout.Stack(SketchLayout.Parse(Stacked(GroundLayer)));
        await Assert.That(stack.Count).IsEqualTo(1);
        await Assert.That(stack[0].Id).IsEqualTo(SketchLayer.GroundId);
        await Assert.That(stack[0].Shapes.Count).IsEqualTo(1);
    }

    /// <summary>The old document kept the ground shapes outside the stack, so this key drew a board. It is
    /// not read any more, and a document still using it draws nothing — a reader silently accepting it would
    /// be the second shape this collapse exists to remove.</summary>
    [Test]
    public async Task Shapes_outside_the_stack_are_not_drawn()
    {
        await Assert.That(SketchLayout.Stack(SketchLayout.Parse(Under("layout")))).IsEmpty();
        await Assert.That(SketchRasterizer.Rasterize(Under("layout"))).IsEmpty();
        await Assert.That(SketchRasterizer.Rasterize(Stacked(GroundLayer)).Count).IsEqualTo(9);    // 3×3
    }

    /// <summary>A layer stating no shapes answers an empty list rather than a null, so every walk over a
    /// stack is the same walk whether or not a layer was left blank.</summary>
    [Test]
    public async Task An_empty_layer_answers_empty_rather_than_null()
    {
        var stack = SketchLayout.Stack(SketchLayout.Parse(Stacked(@"{""id"":""bare"",""base_y"":8}")));
        await Assert.That(stack[0].Shapes).IsEmpty();
        await Assert.That(stack[0].Islands).IsEmpty();
    }

    /// <summary>What the plan compiles to is a stack of one, so the compiler and a hand-drawn flat board
    /// state the same document rather than two shapes a reader has to tell apart.</summary>
    [Test]
    public async Task A_compiled_plan_emits_one_ground_layer()
    {
        var plan = PlanModel.Parse(
            @"{""plan"":1,""globals"":{""symmetry"":""rot_180""}," +
            @"""pieces"":[{""id"":""lane"",""kind"":""lane"",""rect"":[0,0,8,8]}]," +
            @"""placements"":{""spawns"":[{""piece"":""lane"",""at"":[1,5],""facing"":""front""}]}}")!;

        var stack = SketchLayout.Stack(PlanCompiler.Compile(plan).Layout);
        await Assert.That(stack.Count).IsEqualTo(1);
        await Assert.That(stack[0].Id).IsEqualTo(SketchLayer.GroundId);
        await Assert.That(stack[0].BaseY).IsEqualTo(0);
    }
}
