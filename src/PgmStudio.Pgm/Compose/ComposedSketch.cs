using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Pgm.Compose;

/// <summary>
/// What a composed board is handed over as: a sketch layout and the intent it is played by, compiled from the
/// composer's pieces with every box of the team unit drawn as a shape of its own — the hub one, the front line
/// another, each wool approach and the spawn their own — named for the box and its surface (<c>hub-9</c>,
/// <c>wool-a-9</c>). Abutting shapes stay in the one mirrored group, so the unit moves as a whole and a box is
/// edited alone.
/// </summary>
public sealed record ComposedSketch(SketchLayout Layout, MapIntent Intent)
{
    public static ComposedSketch Of(ComposedStages stages)
    {
        var boxOf = stages.Unit.Pieces.ToDictionary(piece => piece.Id, piece => BoxLayout.KeyOf(piece).Id);
        var (layout, intent) = PlanCompiler.Compile(stages.Plan, id => boxOf.GetValueOrDefault(id));
        return new ComposedSketch(layout, intent);
    }
}
