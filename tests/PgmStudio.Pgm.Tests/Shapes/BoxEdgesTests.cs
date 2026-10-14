using PgmStudio.Geom;
using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Tests.Shapes;

/// <summary>
/// An edge turns with the box it bounds: the turn an edge names takes a shape's top edge onto it, and a vacancy's
/// mouth lands on the edge its own rectangle now touches.
/// </summary>
public sealed class BoxEdgesTests
{
    [Test]
    [Arguments(BoxEdge.Top)] [Arguments(BoxEdge.Bottom)] [Arguments(BoxEdge.Left)] [Arguments(BoxEdge.Right)]
    public async Task The_turn_onto_an_edge_takes_the_top_edge_there(BoxEdge edge)
        => await Assert.That(BoxEdge.Top.Turned(edge.TopOnto())).IsEqualTo(edge);

    [Test]
    public async Task A_turned_vacancy_opens_on_the_edge_its_rectangle_touches()
    {
        // A bay along the top of a 7 × 4 box, open upward.
        var body = new ShapeBody([], [new ShapeVacancy("bay", new CellRect(2, 0, 3, 1), BoxEdge.Top, [])]);
        foreach (var turn in Enum.GetValues<BoxTurn>())
        {
            var (width, height) = turn.Extent(7, 4);
            var bay = body.Turned(turn, 7, 4).Vacancies.Single();
            var touches = bay.Mouth switch
            {
                BoxEdge.Top => bay.Rect.Z == 0,
                BoxEdge.Bottom => bay.Rect.MaxZ == height,
                BoxEdge.Left => bay.Rect.X == 0,
                _ => bay.Rect.MaxX == width,
            };
            await Assert.That(touches).IsTrue().Because(turn.ToString());
        }
    }
}
