namespace PgmStudio.Geom.Tests;

/// <summary>
/// A <see cref="BoxTurn"/> lays a shape back onto its own box: the box lands on its turned extent, a rectangle's
/// cells are the cells its corners bound, a direction is the difference of two turned points, and each turn is
/// undone by the turn it names as its opposite.
/// </summary>
public sealed class BoxTurnTests
{
    private const int Width = 7, Height = 4;

    private static readonly BoxTurn[] Turns = Enum.GetValues<BoxTurn>();

    [Test]
    public async Task The_box_lands_on_its_turned_extent()
    {
        foreach (var turn in Turns)
        {
            var (width, height) = turn.Extent(Width, Height);
            await Assert.That(turn.Rect(new CellRect(0, 0, Width, Height), Width, Height))
                .IsEqualTo(new CellRect(0, 0, width, height)).Because(turn.ToString());
        }
    }

    [Test]
    public async Task A_rectangle_keeps_its_cells_and_lands_inside_the_box()
    {
        var rect = new CellRect(1, 2, 3, 1);
        foreach (var turn in Turns)
        {
            var turned = turn.Rect(rect, Width, Height);
            var (width, height) = turn.Extent(Width, Height);
            await Assert.That(turned.Width * turned.Height).IsEqualTo(rect.Width * rect.Height).Because(turn.ToString());
            await Assert.That(turned.X >= 0 && turned.Z >= 0 && turned.MaxX <= width && turned.MaxZ <= height)
                .IsTrue().Because(turn.ToString());
        }
    }

    [Test]
    public async Task A_direction_is_the_difference_of_two_turned_points()
    {
        foreach (var turn in Turns)
            foreach (var (dx, dz) in new[] { (1, 0), (0, 1), (-1, 0), (0, -1) })
            {
                var (fromX, fromZ) = turn.Point(2, 1, Width, Height);
                var (toX, toZ) = turn.Point(2 + dx, 1 + dz, Width, Height);
                await Assert.That(turn.Direction(dx, dz)).IsEqualTo(((int)(toX - fromX), (int)(toZ - fromZ)))
                    .Because(turn.ToString());
            }
    }

    [Test]
    public async Task Each_turn_is_undone_by_its_opposite()
    {
        (BoxTurn Turn, BoxTurn Back)[] pairs =
        [
            (BoxTurn.None, BoxTurn.None), (BoxTurn.FlipX, BoxTurn.FlipX), (BoxTurn.FlipZ, BoxTurn.FlipZ),
            (BoxTurn.Transpose, BoxTurn.Transpose), (BoxTurn.Clockwise, BoxTurn.Anticlockwise),
            (BoxTurn.Anticlockwise, BoxTurn.Clockwise),
        ];
        var rect = new CellRect(1, 2, 3, 1);
        foreach (var (turn, back) in pairs)
        {
            var (width, height) = turn.Extent(Width, Height);
            await Assert.That(back.Rect(turn.Rect(rect, Width, Height), width, height)).IsEqualTo(rect)
                .Because(turn.ToString());
        }
    }
}
