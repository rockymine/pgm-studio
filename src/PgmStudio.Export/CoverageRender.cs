using PgmStudio.Analysis.Playability;
using PgmStudio.Minecraft.Views;

namespace PgmStudio.Export;

/// <summary>The coverage read as a picture: one cell per column, coloured by its class — the stage image and
/// the endpoint's PNG both come off the measure's own grid, so the picture cannot disagree with the numbers
/// beside it.</summary>
public static class CoverageRender
{
    public static byte[] Png(GroundCoverage.Result coverage, int cell = 3)
        => new CellRaster(coverage.Width, coverage.Height, cell, (x, z) =>
        {
            var code = coverage.Cells[z * coverage.Width + x];
            return code == GroundCoverage.Void ? null : GroundCoverage.ClassColors[GroundCoverage.Classes[code]];
        }).Png();
}
