using PgmStudio.Analysis.Playability;
using PgmStudio.Minecraft.Views;

namespace PgmStudio.Export;

/// <summary>The coverage read as a picture: one cell per column, coloured by its class — the stage image and
/// the endpoint's PNG both come off the measure's own grid, so the picture cannot disagree with the numbers
/// beside it.</summary>
public static class CoverageRender
{
    /// <summary>The one view this render draws — a coverage grid has nothing to choose between, so the route
    /// declares no <c>view</c> parameter and the name is only what the picture is.</summary>
    public static readonly string[] PngViews = ["coverage"];

    public static byte[] Png(GroundCoverage.Result coverage, int scale = 1)
        => new CellRaster(coverage.Width, coverage.Height, Cell, (x, z) =>
        {
            var code = coverage.Cells[z * coverage.Width + x];
            return code == GroundCoverage.Void ? null : GroundCoverage.ClassColors[GroundCoverage.Classes[code]];
        }).Scaled(scale).Png();

    /// <summary>Pixels a cell takes before the caller's scale.</summary>
    private const int Cell = 3;
}
