namespace PgmStudio.Pgm.Sketch;

/// <summary>
/// One plot of a grid layout: the outline to draw, the paint it wears, and nothing about where it goes — the
/// grid decides that.
///
/// <para><see cref="Kind"/> is the sketch's own shape vocabulary and no more of it: <c>rectangle</c>,
/// <c>circle</c> and <c>polygon</c>. Which fields a kind reads follows from the kind — a rectangle reads
/// <see cref="Width"/> and <see cref="Depth"/>, a circle reads <see cref="Radius"/>, a polygon reads
/// <see cref="Vertices"/> as offsets from the plot's own centre, so one outline can be laid at every plot
/// without restating its coordinates.</para>
/// </summary>
/// <param name="Kind"><c>rectangle</c> · <c>circle</c> · <c>polygon</c>.</param>
/// <param name="Theme">The id of a theme in the layout's own <c>themes</c> registry, or null for the map's.</param>
/// <param name="Name">What this plot is showing, carried onto the island so a report can name it.</param>
/// <param name="Vertices">Polygon outline as <c>[dx, dz]</c> offsets from the plot centre.</param>
public sealed record GridPlot(
    string Kind,
    string? Theme = null,
    string? Name = null,
    double Width = 34,
    double Depth = 26,
    double Radius = 14,
    double[][]? Vertices = null);

/// <summary>
/// A row of unrelated islands, laid on a grid — the one board shape the studio could not state.
///
/// <para><b>It is a layout emitter and deliberately not a plan one.</b> A plan piece is a rectangle on a cell
/// grid, and a plot here is a disc, an octagon, a cross or a wedge; compiling one through a plan would either
/// lose the outline or grow the plan a shape vocabulary it has no gameplay use for. So this emits a
/// <see cref="SketchLayout"/> directly, beside what a compile produces rather than through it, and everything
/// downstream — the rasterizer, the painter, the dressing pass, the export — reads it as the ordinary layout
/// it is.</para>
///
/// <para><b>Every island stands alone.</b> <c>mirrors: false</c> on all of them and <c>mirror_mode: none</c> on
/// the setup, because a grid of plots is a catalogue and not a board: each plot shows exactly one thing, and
/// fanning one across an orbit would show it twice and something else not at all. A map that wants symmetry
/// wants a compose or a plan, which is what those are for.</para>
///
/// <para>The plots run left to right and then down, so plot <c>i</c> sits at column <c>i % columns</c> — the
/// order a caller listed them in is the order they are walked in, which is what makes a catalogue readable
/// from the ground.</para>
/// </summary>
public static class IslandGrid
{
    /// <summary>The grid's own geometry: how wide the rows are, how far apart the plots sit, and how tall a
    /// plot stands. <see cref="Pitch"/> is centre to centre, so a plot wider than the pitch overlaps its
    /// neighbour and the two read as one landmass — which is refused rather than drawn.</summary>
    /// <param name="Floor">The lowest course a plot's column is filled from, just above bedrock.</param>
    /// <param name="Top">The walking surface's course, so every plot carries a wall of that height above void.</param>
    public sealed record Geometry(int Columns = 5, int Pitch = 56, int Floor = 1, int Top = 26);

    /// <summary>Where plot <paramref name="index"/> sits, centred on the whole grid so a catalogue of any
    /// length is built around the origin rather than running away from it in one direction.</summary>
    public static (int X, int Z) Origin(int index, int count, Geometry grid)
    {
        var columns = Math.Max(1, grid.Columns);
        // Centred on the columns actually occupied rather than on the row width, so a catalogue of three in a
        // five-wide grid stands over the origin instead of two pitches to the left of it.
        var used = Math.Min(count, columns);
        var rows = (count + columns - 1) / columns;
        return ((index % columns) * grid.Pitch - (used - 1) * grid.Pitch / 2,
                (index / columns) * grid.Pitch - (rows - 1) * grid.Pitch / 2);
    }

    /// <summary>The layout the plots compose to: one shape and one island per plot, at the grid's own origins.
    /// Themes and everything else a layout carries are the caller's to add — this states the ground.</summary>
    public static SketchLayout Lay(IReadOnlyList<GridPlot> plots, Geometry grid)
    {
        if (plots.Count == 0) throw new ArgumentException("a grid needs at least one plot");
        RefuseOverlap(plots, grid);

        var shapes = new List<SketchShape>();
        var islands = new List<SketchGroup>();
        for (var index = 0; index < plots.Count; index++)
        {
            var plot = plots[index];
            var (originX, originZ) = Origin(index, plots.Count, grid);
            var id = $"plot-{index}";
            shapes.Add(Outline(id, plot, originX, originZ, grid));
            islands.Add(new SketchGroup
            {
                Id = $"island-{index}",
                Name = plot.Name,
                Mirrors = false,
                ShapeIds = [id],
            });
        }

        return new SketchLayout
        {
            Setup = new SketchSetup { MirrorMode = "none", Center = new SketchCenter { Cx = 0, Cz = 0 } },
            Layers = [SketchLayer.Ground(shapes, islands)],
        };
    }

    /// <summary>One plot's outline, centred on the origin the grid gave it. Floor and top come from the grid
    /// rather than the plot, because a catalogue whose plots stood at different heights would be reading a
    /// difference the plots do not mean.</summary>
    private static SketchShape Outline(string id, GridPlot plot, int originX, int originZ, Geometry grid)
    {
        var shape = new SketchShape
        {
            Id = id, Operation = "add", Floor = grid.Floor, BaseHeight = grid.Top - 1, Theme = plot.Theme,
        };

        switch (plot.Kind.ToLowerInvariant())
        {
            case "rectangle":
                shape.Type = "rectangle";
                (shape.MinX, shape.MaxX) = (originX - plot.Width / 2, originX + plot.Width / 2);
                (shape.MinZ, shape.MaxZ) = (originZ - plot.Depth / 2, originZ + plot.Depth / 2);
                break;

            // A circle is a first-class shape rather than a polygon approximating one, which is why a round
            // plot is drawable at all.
            case "circle":
                shape.Type = "circle";
                (shape.CenterX, shape.CenterZ, shape.Radius) = (originX, originZ, plot.Radius);
                break;

            case "polygon":
                if (plot.Vertices is not { Length: >= 3 })
                    throw new ArgumentException($"plot '{id}' is a polygon with fewer than three vertices");
                shape.Type = "polygon";
                shape.Vertices = [.. plot.Vertices.Select(v => new[] { originX + v[0], originZ + v[1] })];
                break;

            default:
                throw new ArgumentException(
                    $"no plot kind '{plot.Kind}' — have: rectangle, circle, polygon");
        }
        return shape;
    }

    /// <summary>Refused rather than drawn: two plots whose outlines reach into one another rasterize as one
    /// landmass, so the catalogue silently shows fewer things than it lists. The check is the plot's own
    /// half-extent against half the pitch, which is the widest a plot may be and still leave ground between
    /// itself and its neighbour.</summary>
    private static void RefuseOverlap(IReadOnlyList<GridPlot> plots, Geometry grid)
    {
        var room = grid.Pitch / 2.0;
        for (var index = 0; index < plots.Count; index++)
        {
            var plot = plots[index];
            var (reachX, reachZ) = plot.Kind.ToLowerInvariant() switch
            {
                "rectangle" => (plot.Width / 2, plot.Depth / 2),
                "circle" => (plot.Radius, plot.Radius),
                "polygon" when plot.Vertices is { Length: > 0 } vertices
                    => (vertices.Max(v => Math.Abs(v[0])), vertices.Max(v => Math.Abs(v[1]))),
                _ => (0d, 0d),
            };
            if (reachX > room || reachZ > room)
                throw new ArgumentException(
                    $"plot {index} reaches {Math.Max(reachX, reachZ):0.#} blocks from its centre, past the "
                  + $"{room:0.#} the {grid.Pitch}-block pitch leaves it — the plots would join into one island");
        }
    }
}
