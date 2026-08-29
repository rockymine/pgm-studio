using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Pgm.Tests.Sketch;

/// <summary>
/// The one board shape the studio could not state: a catalogue of unrelated islands. What is worth asserting is
/// the part a caller cannot check by looking — that the plots are laid where the grid says and, above all, that
/// two plots which would rasterize into one landmass are refused rather than drawn, since a catalogue silently
/// showing fewer things than it lists is the failure the whole emitter exists to avoid.
/// </summary>
public sealed class IslandGridTests
{
    private static readonly IslandGrid.Geometry Grid = new(Columns: 5, Pitch: 56, Floor: 1, Top: 26);

    [Test]
    public async Task The_plots_run_left_to_right_and_then_down()
    {
        // The order stated is the order walked, which is what makes a catalogue readable from the ground.
        var first = IslandGrid.Origin(0, 10, Grid);
        var alongTheRow = IslandGrid.Origin(1, 10, Grid);
        var nextRow = IslandGrid.Origin(5, 10, Grid);

        await Assert.That(alongTheRow.X - first.X).IsEqualTo(56);
        await Assert.That(alongTheRow.Z).IsEqualTo(first.Z);
        await Assert.That(nextRow.X).IsEqualTo(first.X);
        await Assert.That(nextRow.Z - first.Z).IsEqualTo(56);
    }

    [Test]
    public async Task A_catalogue_of_any_length_is_built_around_the_origin()
    {
        // Not run away from it in one direction: a grid centred on nothing is a map whose observer point and
        // whose spawn have to be computed from its length before either can be written.
        foreach (var count in (int[])[1, 4, 5, 6, 25, 37])
        {
            var origins = Enumerable.Range(0, count).Select(i => IslandGrid.Origin(i, count, Grid)).ToList();
            var middleX = (origins.Min(o => o.X) + origins.Max(o => o.X)) / 2.0;
            var middleZ = (origins.Min(o => o.Z) + origins.Max(o => o.Z)) / 2.0;
            await Assert.That((count, middleX, middleZ)).IsEqualTo((count, 0d, 0d));
        }
    }

    [Test]
    public async Task Every_plot_is_its_own_island_and_none_of_them_mirror()
    {
        var layout = IslandGrid.Lay(
            [.. Enumerable.Range(0, 7).Select(i => new GridPlot("rectangle", Name: $"plot {i}"))], Grid);

        await Assert.That(SketchLayout.Stack(layout)[0].Shapes.Count).IsEqualTo(7);
        await Assert.That(SketchLayout.Stack(layout)[0].Groups.Count).IsEqualTo(7);
        await Assert.That(SketchLayout.Stack(layout)[0].Groups.All(island => !island.Mirrors)).IsTrue();
        await Assert.That(SketchLayout.Stack(layout)[0].Groups.All(island => island.ShapeIds.Count == 1)).IsTrue();
        await Assert.That(layout.Setup!.MirrorMode).IsEqualTo("none");
        // The name travels onto the island, which is what lets a report say what a plot was showing.
        await Assert.That(SketchLayout.Stack(layout)[0].Groups[3].Name).IsEqualTo("plot 3");
    }

    [Test]
    public async Task A_polygons_vertices_are_offsets_so_one_outline_lays_at_every_plot()
    {
        double[][] triangle = [[-4, -4], [4, -4], [0, 4]];
        var layout = IslandGrid.Lay(
            [new GridPlot("polygon", Vertices: triangle), new GridPlot("polygon", Vertices: triangle)], Grid);

        var (firstX, firstZ) = IslandGrid.Origin(0, 2, Grid);
        var (secondX, _) = IslandGrid.Origin(1, 2, Grid);
        await Assert.That(SketchLayout.Stack(layout)[0].Shapes[0].Vertices![0]).IsEquivalentTo(new[] { firstX - 4d, firstZ - 4d });
        await Assert.That(SketchLayout.Stack(layout)[0].Shapes[1].Vertices![0]).IsEquivalentTo(new[] { secondX - 4d, firstZ - 4d });
    }

    [Test]
    public async Task A_plot_wider_than_the_pitch_leaves_room_for_is_refused()
    {
        // Two plots reaching into one another rasterize as one landmass, so the catalogue shows fewer things
        // than it lists — and nothing downstream would say so. Every kind measures its own reach.
        var wide = Assert.Throws<ArgumentException>(
            () => IslandGrid.Lay([new GridPlot("rectangle", Width: 120)], Grid));
        await Assert.That(wide!.Message).Contains("join into one island");

        Assert.Throws<ArgumentException>(() => IslandGrid.Lay([new GridPlot("circle", Radius: 40)], Grid));
        Assert.Throws<ArgumentException>(
            () => IslandGrid.Lay([new GridPlot("polygon", Vertices: [[-40, 0], [40, 0], [0, 40]])], Grid));

        // and the widest plot that still leaves ground between neighbours is drawn
        await Assert.That(SketchLayout.Stack(IslandGrid.Lay([new GridPlot("circle", Radius: 28)], Grid))[0].Shapes.Count)
            .IsEqualTo(1);
    }

    [Test]
    public async Task A_kind_the_sketch_cannot_draw_and_a_polygon_without_an_outline_are_refused()
    {
        var kind = Assert.Throws<ArgumentException>(() => IslandGrid.Lay([new GridPlot("hexagon")], Grid));
        await Assert.That(kind!.Message).Contains("rectangle, circle, polygon");

        Assert.Throws<ArgumentException>(
            () => IslandGrid.Lay([new GridPlot("polygon", Vertices: [[0, 0], [1, 1]])], Grid));
        Assert.Throws<ArgumentException>(() => IslandGrid.Lay([], Grid));
    }

    [Test]
    public async Task Every_plot_stands_at_the_grids_own_floor_and_top()
    {
        // A catalogue whose plots stood at different heights would be reading a difference the plots do not
        // mean, so the height is the grid's and never the plot's.
        var layout = IslandGrid.Lay(
            [new GridPlot("rectangle"), new GridPlot("circle"), new GridPlot("polygon",
                Vertices: [[-4, -4], [4, -4], [0, 4]])], Grid);

        await Assert.That(SketchLayout.Stack(layout)[0].Shapes.All(shape => shape.Floor == 1)).IsTrue();
        await Assert.That(SketchLayout.Stack(layout)[0].Shapes.All(shape => shape.BaseHeight == 25)).IsTrue();
    }
}
