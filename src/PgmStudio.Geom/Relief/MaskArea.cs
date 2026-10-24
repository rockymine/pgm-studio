namespace PgmStudio.Geom.Relief;

/// <summary>The outlines a modifier's mask is drawn as.</summary>
public enum MaskShape { Ellipse, Polygon, Board }

/// <summary>
/// One outline of a modifier's mask, measured as <see cref="E"/>: 0 at its middle or deepest point, 1 on its rim
/// and over 1 outside it. An ellipse measures in its own radii; a polygon measures its distance to the outline
/// in units of its deepest interior point, so a thin sliver and a broad pond both run from 0 to 1.
/// </summary>
public sealed class MaskArea
{
    private const double SameTolerance = 1e-6;

    private readonly double _centreX, _centreZ, _radiusX, _radiusZ;
    private readonly double _alongXx, _alongXz, _alongZx, _alongZz;
    private readonly (double X, double Z)[] _points = [];

    public MaskShape Shape { get; }

    /// <summary>How many blocks one unit of <see cref="E"/> spans, for an operation that measures in blocks: the
    /// longer radius of an ellipse, the deepest interior distance of a polygon (at least one), one for a board.</summary>
    public double Scale { get; }

    private MaskArea(MaskShape shape, double scale) { Shape = shape; Scale = scale; }

    private MaskArea(double centreX, double centreZ, double radiusX, double radiusZ,
                     (double X, double Z) alongX, (double X, double Z) alongZ)
    {
        Shape = MaskShape.Ellipse;
        (_centreX, _centreZ, _radiusX, _radiusZ) = (centreX, centreZ, radiusX, radiusZ);
        (_alongXx, _alongXz, _alongZx, _alongZz) = (alongX.X, alongX.Z, alongZ.X, alongZ.Z);
        Scale = Math.Max(radiusX, radiusZ);
    }

    private MaskArea((double X, double Z)[] points, double scale)
    {
        Shape = MaskShape.Polygon;
        _points = points;
        Scale = scale;
    }

    /// <summary>All the ground there is, at full strength everywhere.</summary>
    public static MaskArea Board { get; } = new(MaskShape.Board, 1);

    /// <summary>An ellipse about a centre, its x radius turned <paramref name="angleDegrees"/> from east toward
    /// south.</summary>
    public static MaskArea Ellipse(double centreX, double centreZ, double radiusX, double radiusZ, double angleDegrees = 0)
    {
        if (!(radiusX > 0) || !(radiusZ > 0))
            throw new ArgumentOutOfRangeException(nameof(radiusX), "An ellipse needs positive radii.");
        var turn = angleDegrees * Math.PI / 180;
        double cos = Math.Cos(turn), sin = Math.Sin(turn);
        return new MaskArea(centreX, centreZ, radiusX, radiusZ, (cos, sin), (-sin, cos));
    }

    /// <summary>A closed outline of at least three points. Its scale is the deepest interior distance among the
    /// centres of the cells its bounds cover.</summary>
    public static MaskArea Polygon(IEnumerable<(double X, double Z)> points)
    {
        var outline = points.ToArray();
        if (outline.Length < 3) throw new ArgumentException("A polygon needs three points.", nameof(points));
        double deepest = 1;
        double minX = outline.Min(p => p.X), maxX = outline.Max(p => p.X);
        double minZ = outline.Min(p => p.Z), maxZ = outline.Max(p => p.Z);
        for (var x = Math.Floor(minX) + 0.5; x <= maxX; x += 1)
            for (var z = Math.Floor(minZ) + 0.5; z <= maxZ; z += 1)
            {
                var (inside, edge) = Measure(outline, x, z);
                if (inside && edge > deepest) deepest = edge;
            }
        return new MaskArea(outline, deepest);
    }

    /// <summary>How far out the point lies: 0 at the middle, 1 on the rim, over 1 outside.</summary>
    public double E(double x, double z)
    {
        switch (Shape)
        {
            case MaskShape.Ellipse:
                var (dx, dz) = (x - _centreX, z - _centreZ);
                return double.Hypot((dx * _alongXx + dz * _alongXz) / _radiusX,
                                    (dx * _alongZx + dz * _alongZz) / _radiusZ);
            case MaskShape.Polygon:
                var (inside, edge) = Measure(_points, x, z);
                return inside ? Math.Max(0, 1 - edge / Scale) : 1 + edge / Scale;
            default:
                return 0;
        }
    }

    /// <summary>This area as it stands under one concrete symmetry axis about a centre
    /// (<see cref="Symmetry.Apply"/>), measuring the same distances from the image's own middle. A board is its
    /// own image.</summary>
    public MaskArea Image(string axis, double centreX, double centreZ)
    {
        switch (Shape)
        {
            case MaskShape.Ellipse:
                var middle = Symmetry.Apply(_centreX, _centreZ, axis, centreX, centreZ);
                var alongX = Symmetry.Apply(_centreX + _alongXx, _centreZ + _alongXz, axis, centreX, centreZ);
                var alongZ = Symmetry.Apply(_centreX + _alongZx, _centreZ + _alongZz, axis, centreX, centreZ);
                return new MaskArea(middle.X, middle.Z, _radiusX, _radiusZ,
                                    (alongX.X - middle.X, alongX.Z - middle.Z),
                                    (alongZ.X - middle.X, alongZ.Z - middle.Z));
            case MaskShape.Polygon:
                return new MaskArea([.. _points.Select(p => Symmetry.Apply(p.X, p.Z, axis, centreX, centreZ))], Scale);
            default:
                return this;
        }
    }

    /// <summary>The areas and each of their images under every axis, in order, without any outline twice: an
    /// area standing on the symmetry, or an image the author also drew, would otherwise count double wherever
    /// areas add or melt.</summary>
    public static List<MaskArea> Fanned(IEnumerable<MaskArea> areas, IReadOnlyList<string> axes, double centreX, double centreZ)
    {
        var fanned = new List<MaskArea>();
        foreach (var area in areas)
        {
            if (!fanned.Any(area.SameOutline)) fanned.Add(area);
            if (area.Shape == MaskShape.Board) continue;
            foreach (var axis in axes)
            {
                var image = area.Image(axis, centreX, centreZ);
                if (!fanned.Any(image.SameOutline)) fanned.Add(image);
            }
        }
        return fanned;
    }

    /// <summary>Whether two areas measure every point the same way. An ellipse is its centre and the quadratic
    /// form of its radii, which a half turn, a swap of the two radii or any turn of a circle leaves alone.</summary>
    private bool SameOutline(MaskArea other)
    {
        if (Shape != other.Shape) return false;
        switch (Shape)
        {
            case MaskShape.Ellipse:
                if (Math.Abs(_centreX - other._centreX) > SameTolerance || Math.Abs(_centreZ - other._centreZ) > SameTolerance)
                    return false;
                var (form, otherForm) = (Form(), other.Form());
                var shortest = Math.Min(Math.Min(_radiusX, _radiusZ), Math.Min(other._radiusX, other._radiusZ));
                var tolerance = 1e-9 / (shortest * shortest);
                return Math.Abs(form.Xx - otherForm.Xx) <= tolerance
                       && Math.Abs(form.Xz - otherForm.Xz) <= tolerance
                       && Math.Abs(form.Zz - otherForm.Zz) <= tolerance;
            case MaskShape.Polygon:
                return _points.Length == other._points.Length
                       && _points.All(p => other._points.Any(q => Math.Abs(p.X - q.X) <= SameTolerance
                                                                  && Math.Abs(p.Z - q.Z) <= SameTolerance));
            default:
                return true;
        }
    }

    private (double Xx, double Xz, double Zz) Form()
    {
        double inverseX = 1 / (_radiusX * _radiusX), inverseZ = 1 / (_radiusZ * _radiusZ);
        return (_alongXx * _alongXx * inverseX + _alongZx * _alongZx * inverseZ,
                _alongXx * _alongXz * inverseX + _alongZx * _alongZz * inverseZ,
                _alongXz * _alongXz * inverseX + _alongZz * _alongZz * inverseZ);
    }

    private static (bool Inside, double Edge) Measure((double X, double Z)[] outline, double x, double z)
    {
        var inside = false;
        var edge = double.PositiveInfinity;
        for (int i = 0, j = outline.Length - 1; i < outline.Length; j = i++)
        {
            var (ax, az) = outline[j];
            var (bx, bz) = outline[i];
            edge = Math.Min(edge, SegmentDistance(x, z, ax, az, bx, bz));
            if ((bz > z) != (az > z) && x < (ax - bx) * (z - bz) / (az - bz) + bx) inside = !inside;
        }
        return (inside, edge);
    }

    private static double SegmentDistance(double px, double pz, double ax, double az, double bx, double bz)
    {
        double dx = bx - ax, dz = bz - az, lengthSquared = dx * dx + dz * dz;
        var along = lengthSquared > 0 ? Math.Clamp(((px - ax) * dx + (pz - az) * dz) / lengthSquared, 0, 1) : 0;
        return double.Hypot(px - (ax + along * dx), pz - (az + along * dz));
    }
}
