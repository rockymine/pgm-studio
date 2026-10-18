using PgmStudio.Geom;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Pgm.Compose;

/// <summary>One box of a composed board, at one of its orbit images, in blocks.</summary>
/// <param name="Id">The box's id in the team unit — <c>hub</c>, <c>spawn</c>, <c>wool-a</c>, <c>frontline</c>.</param>
/// <param name="Kind">What the box is.</param>
/// <param name="Image">Which orbit image it is, 0 being the unit as composed; the intent's team at the same index
/// is the team it belongs to.</param>
/// <param name="Rect">Its footprint at that image.</param>
public sealed record ComposedBox(string Id, BoxKind Kind, int Image, BlockRect Rect);

/// <summary>
/// What a composed board is handed over as: a sketch layout and the intent it is played by, compiled from the
/// composer's pieces with every box of the team unit drawn as a shape of its own — the hub one, the front line
/// another, each wool approach and the spawn their own — named for the box and its surface (<c>hub-9</c>,
/// <c>wool-a-9</c>). Abutting shapes stay in the one mirrored group, so the unit moves as a whole and a box is
/// edited alone. The boxes themselves ride beside it, fanned, for whatever annotates the board with them.
/// </summary>
public sealed record ComposedSketch(SketchLayout Layout, MapIntent Intent, IReadOnlyList<ComposedBox> Boxes)
{
    public static ComposedSketch Of(ComposedStages stages)
    {
        var boxOf = stages.Unit.Pieces.ToDictionary(piece => piece.Id, piece => BoxLayout.KeyOf(piece).Id);
        var (layout, intent) = PlanCompiler.Compile(stages.Plan, id => boxOf.GetValueOrDefault(id));

        var cell = stages.Plan.Globals.Cell;
        var mode = stages.Plan.Globals.Symmetry;
        var boxes = new List<ComposedBox>();
        for (var image = 0; image < Symmetry.Order(mode); image++)
            foreach (var box in BoxLayout.Of(stages.Unit).Boxes)
            {
                var rect = box.Rect;
                var (minX, minZ, maxX, maxZ) = Symmetry.Rect(rect.X * cell, rect.Z * cell,
                    (rect.X + rect.Width) * cell, (rect.Z + rect.Height) * cell, mode, 0, 0, image);
                boxes.Add(new ComposedBox(box.Id, box.Kind, image, new BlockRect(
                    (int)Math.Round(minX), (int)Math.Round(minZ), (int)Math.Round(maxX), (int)Math.Round(maxZ))));
            }
        return new ComposedSketch(layout, intent, boxes);
    }
}
