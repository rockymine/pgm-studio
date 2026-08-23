using PgmStudio.Geom;

namespace PgmStudio.Pgm.Sketch;

/// <summary>
/// Whether a ring stands on ground, asked of the <b>rasterised</b> footprint the build actually produces.
///
/// <para>A shape is seated by eye or by a model of the coast rebuilt outside the studio, and a rebuilt model
/// disagrees with the rasterised one — a ring faceted to sixty-four points is not the cells that ring covers.
/// The disagreement is only ever a cell or two, and a cell or two is the whole failure: a lift with no ground
/// under it reads no terrain, falls back to the shape's own floor and stands a stub of cobble in open void,
/// which nothing declines because a shape is terrain and terrain over void is a spur.</para>
///
/// <para><b>Three answers, and the third is the one that needs saying.</b> A cell is <c>land</c> where the
/// footprint has it and <c>void</c> where it does not. A <c>hole</c> is a void cell the footprint encloses —
/// a hub's slots, a U-shaped wool room's notch — and those are made by <b>arrangement</b>, so no region marks
/// one and nothing objects to a shape dropped on top of it. It is exactly the gap the layout was composed to
/// have, filled in silently. <see cref="Cells.EnclosedVoid"/> is what names them, which is the same
/// derivation <c>CT8</c> counts enclosed voids with, so a probe and a rule cannot disagree about what a hole
/// is.</para>
/// </summary>
public static class FootprintProbe
{
    /// <summary>The body <c>POST /map/{slug}/sketch/probe-footprint</c> takes: the live layout, and the ring
    /// to ask about. It sits beside <see cref="SketchLayout"/> rather than among the wire DTOs because it
    /// carries one, the way every other route that takes a live layout declares it.</summary>
    /// <param name="Layout">The layout whose rasterised footprint the ring is measured against.</param>
    /// <param name="Ring">The ring, as <c>[x, z]</c> pairs. Three or more; fewer covers no cell.</param>
    public sealed record Request(SketchLayout Layout, double[][] Ring);

    /// <summary>How many offending cells are named. A ring that misses the coast by two cells is the case this
    /// exists for; one that misses by four hundred needs a count and a corner, not a list.</summary>
    public const int NamedCells = 24;

    /// <summary>What a ring stands on. <see cref="Land"/> + <see cref="Void"/> + <see cref="Hole"/> is every
    /// cell of the ring, since a hole is a void cell counted apart rather than as well.</summary>
    /// <param name="Cells">Cells the ring covers.</param>
    /// <param name="Land">Of those, cells the footprint has.</param>
    /// <param name="Void">Cells outside the footprint altogether — past the coast.</param>
    /// <param name="Hole">Cells the footprint encloses but does not fill.</param>
    /// <param name="VoidCells">Where the void cells are, up to <see cref="NamedCells"/>.</param>
    /// <param name="HoleCells">Where the hole cells are, up to <see cref="NamedCells"/>.</param>
    public sealed record Result(
        int Cells, int Land, int Void, int Hole,
        IReadOnlyList<(int X, int Z)> VoidCells,
        IReadOnlyList<(int X, int Z)> HoleCells);

    /// <summary>Probe a ring against a layout's own rasterised footprint.</summary>
    public static Result Of(string layoutJson, IReadOnlyList<double[]> ring)
        => Of(new HashSet<(int X, int Z)>(SketchRasterizer.Rasterize(layoutJson)), ring);

    /// <summary>Probe a ring against a footprint already in hand.</summary>
    public static Result Of(IReadOnlySet<(int X, int Z)> footprint, IReadOnlyList<double[]> ring)
    {
        var covered = SketchRasterizer.CellsOfRing(ring).ToList();
        if (covered.Count == 0 || footprint.Count == 0)
            return new Result(covered.Count, 0, covered.Count, 0, [.. covered.Take(NamedCells)], []);

        var holes = Cells.EnclosedVoid(footprint);
        var voids = new List<(int X, int Z)>();
        var enclosed = new List<(int X, int Z)>();
        var land = 0;
        foreach (var cell in covered)
        {
            if (footprint.Contains(cell)) land++;
            else if (holes.Contains(cell)) enclosed.Add(cell);
            else voids.Add(cell);
        }
        return new Result(covered.Count, land, voids.Count, enclosed.Count,
            [.. voids.Take(NamedCells)], [.. enclosed.Take(NamedCells)]);
    }
}
