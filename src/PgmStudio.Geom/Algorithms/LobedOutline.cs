namespace PgmStudio.Geom.Algorithms;

/// <summary>
/// An ellipse pulled in and out as it goes round: <c>lobes</c> bulges spaced evenly about its centre, each
/// reaching <c>wobble</c> of the radius past the ellipse and drawing in as far between them, the whole turned
/// by <c>turn</c> degrees. The outline a hummock, a pond or a patch of different ground is drawn with, stated
/// by a handful of numbers where its points would be dozens.
/// </summary>
public static class LobedOutline
{
    /// <summary>The outline's <paramref name="points"/> points, evenly round from the ellipse's +x side, each
    /// rounded to a tenth of a block. A wobble of nought is the plain ellipse, and equal radii a circle.</summary>
    /// <param name="centerX">The centre on the x axis.</param>
    /// <param name="centerZ">The centre on the z axis.</param>
    /// <param name="radiusX">The radius along x before the turn.</param>
    /// <param name="radiusZ">The radius along z before the turn.</param>
    /// <param name="points">How many points the outline is drawn with.</param>
    /// <param name="lobes">How many bulges it carries.</param>
    /// <param name="wobble">How far a bulge reaches, as a fraction of the radius it swells.</param>
    /// <param name="phase">Where round the outline the first bulge stands, in radians.</param>
    /// <param name="turnDegrees">How far the whole outline is turned about its centre.</param>
    public static double[][] Of(double centerX, double centerZ, double radiusX, double radiusZ, int points,
                                int lobes, double wobble, double phase, double turnDegrees)
    {
        var turn = turnDegrees * Math.PI / 180;
        var ring = new double[points][];
        for (var index = 0; index < points; index++)
        {
            var angle = 2 * Math.PI * index / points;
            var swell = 1 + wobble * Math.Cos(lobes * angle + phase);
            double x = radiusX * swell * Math.Cos(angle), z = radiusZ * swell * Math.Sin(angle);
            ring[index] = [Math.Round(centerX + x * Math.Cos(turn) - z * Math.Sin(turn), 1),
                           Math.Round(centerZ + x * Math.Sin(turn) + z * Math.Cos(turn), 1)];
        }
        return ring;
    }
}
