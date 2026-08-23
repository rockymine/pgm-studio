using PgmStudio.Analysis.Playability;
using PgmStudio.Geom.Render;
using PgmStudio.Minecraft.Views;

namespace PgmStudio.Export;

/// <summary>The coverage read as a picture: one cell per column, coloured by its class, with each waypoint
/// drawn in its own kind's colour and a key naming every one of them — the stage image and the endpoint's PNG
/// both come off the measure's own grid, so the picture cannot disagree with the numbers beside it.</summary>
public static class CoverageRender
{
    /// <summary>The one view this render draws — a coverage grid has nothing to choose between, so the route
    /// declares no <c>view</c> parameter and the name is only what the picture is.</summary>
    public static readonly string[] PngViews = ["coverage"];

    /// <summary>Pixels a cell takes before the caller's scale.</summary>
    private const int Cell = 3;

    /// <summary>How far a marker's cross reaches from its centre, in cells. A waypoint drawn as its own 3×3
    /// cell block vanishes in a board-sized picture scaled to fit a page, and a reader cannot check a claim
    /// about a place they cannot find.</summary>
    private const int MarkerReach = 3;

    public static byte[] Png(GroundCoverage.Result coverage, int scale = 1)
    {
        var raster = new CellRaster(coverage.Width, coverage.Height, Cell, (x, z) =>
        {
            var code = coverage.Cells[z * coverage.Width + x];
            return code == GroundCoverage.Void ? null : GroundCoverage.ClassColors[GroundCoverage.Classes[code]];
        }).Scaled(scale);

        var cellPixels = Cell * Math.Max(1, scale);
        int width = coverage.Width * cellPixels, height = coverage.Height * cellPixels;
        var pixels = raster.Pixels();
        foreach (var marker in coverage.Markers) DrawMarker(pixels, width, height, coverage, marker, cellPixels);

        var kinds = coverage.Markers.Select(marker => marker.Kind).Distinct().OrderBy(kind => kind).ToList();
        List<Legend.Entry> entries =
        [
            .. GroundCoverage.Classes.Skip(1).Where(name => name != "waypoint")
                .Select(name => new Legend.Entry(name.ToUpperInvariant(), Packed(GroundCoverage.ClassColors[name]))),
            new("VOID", Packed(CellRaster.Background)),
            .. kinds.Select(kind => new Legend.Entry(kind.ToUpperInvariant(), ObjectiveColors.Of(kind))),
        ];
        var withLegend = Legend.AppendBelow(pixels, width, height, entries, out var legendHeight,
            scaleLabel: $"SCALE: 1 BLOCK = {cellPixels} PX - {coverage.Width} X {coverage.Height} BLOCKS");
        return PngWriter.Encode(width, legendHeight, withLegend);
    }

    /// <summary>A marker as a cross centred on its cell, in its kind's colour — a cross rather than a blob
    /// because it stays findable when the picture is scaled down and still shows exactly which cell it names
    /// at the centre.</summary>
    private static void DrawMarker(byte[] pixels, int width, int height, GroundCoverage.Result coverage,
        GroundCoverage.Marker marker, int cellPixels)
    {
        var rgb = ObjectiveColors.Of(marker.Kind);
        int column = marker.X - coverage.MinX, row = marker.Z - coverage.MinZ;
        if (column < 0 || column >= coverage.Width || row < 0 || row >= coverage.Height) return;

        for (var offset = -MarkerReach; offset <= MarkerReach; offset++)
        {
            Cellwise(pixels, width, height, column + offset, row, cellPixels, rgb);
            Cellwise(pixels, width, height, column, row + offset, cellPixels, rgb);
        }
    }

    private static void Cellwise(byte[] pixels, int width, int height, int column, int row, int cellPixels,
        int rgb)
    {
        if (column < 0 || row < 0 || (column + 1) * cellPixels > width || (row + 1) * cellPixels > height) return;
        Raster.FillRect(pixels, width, height, column * cellPixels, row * cellPixels, cellPixels, cellPixels, rgb);
    }

    private static int Packed(string hex) =>
        int.Parse(hex.TrimStart('#'), System.Globalization.NumberStyles.HexNumber);
}
