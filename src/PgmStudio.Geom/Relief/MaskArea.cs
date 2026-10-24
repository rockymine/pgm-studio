namespace PgmStudio.Geom.Relief;

/// <summary>The outlines a modifier's mask is drawn as.</summary>
public enum MaskShape { Ellipse, Polygon, Board, Line }

/// <summary>Where a point stands against a <see cref="MaskShape.Line"/>: how many blocks it lies from the line,
/// the arc length from the line's first point to the nearest point on it, and which hand it lies on walking
/// along the line (+1 right, -1 left, x east and z south).</summary>
public readonly record struct LineFix(double Distance, double Along, int Side);

/// <summary>
/// One outline of a modifier's mask, measured as <see cref="E"/>: 0 at its middle or deepest point, 1 on its rim
/// and over 1 outside it. An ellipse measures in its own radii; a polygon measures its distance to the outline
/// in units of its deepest interior point, so a thin sliver and a broad pond both run from 0 to 1. A line measures
/// its distance to the polyline in units of half its width, so <see cref="E"/> is 1 on the edge of the strip it
/// draws.
/// </summary>
public sealed class MaskArea
{
    private const double SameTolerance = 1e-6;

    private readonly double _centreX, _centreZ, _radiusX, _radiusZ;
    private readonly double _alongXx, _alongXz, _alongZx, _alongZz;
    private readonly (double X, double Z)[] _points = [];
    private readonly double[] _arc = [];
    private readonly bool _flipped;
    private string? _axis;
    private string[] _folding = [];
    private double _aboutX, _aboutZ;

    public MaskShape Shape { get; }

    /// <summary>The outline as the author drew it: this one, or the area this one is an image of.</summary>
    public MaskArea Drawn { get; private set; }

    /// <summary>How many blocks one unit of <see cref="E"/> spans, for an operation that measures in blocks: the
    /// longer radius of an ellipse, the deepest interior distance of a polygon (at least one), half the width of a
    /// line, one for a board.</summary>
    public double Scale { get; }

    private MaskArea(MaskShape shape, double scale) { Shape = shape; Scale = scale; Drawn = this; }

    private MaskArea(double centreX, double centreZ, double radiusX, double radiusZ,
                     (double X, double Z) alongX, (double X, double Z) alongZ)
    {
        Shape = MaskShape.Ellipse;
        Drawn = this;
        (_centreX, _centreZ, _radiusX, _radiusZ) = (centreX, centreZ, radiusX, radiusZ);
        (_alongXx, _alongXz, _alongZx, _alongZz) = (alongX.X, alongX.Z, alongZ.X, alongZ.Z);
        Scale = Math.Max(radiusX, radiusZ);
    }

    private MaskArea((double X, double Z)[] points, double scale)
    {
        Shape = MaskShape.Polygon;
        Drawn = this;
        _points = points;
        Scale = scale;
    }

    private MaskArea((double X, double Z)[] points, double halfWidth, bool flipped)
    {
        Shape = MaskShape.Line;
        Drawn = this;
        _points = points;
        _flipped = flipped;
        Scale = halfWidth;
        _arc = new double[points.Length];
        for (var i = 1; i < points.Length; i++)
            _arc[i] = _arc[i - 1] + double.Hypot(points[i].X - points[i - 1].X, points[i].Z - points[i - 1].Z);
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

    /// <summary>An open polyline of at least two points, <paramref name="width"/> blocks wide.</summary>
    public static MaskArea Line(IEnumerable<(double X, double Z)> points, double width)
    {
        var path = points.ToArray();
        if (path.Length < 2) throw new ArgumentException("A line needs two points.", nameof(points));
        if (!(width > 0)) throw new ArgumentOutOfRangeException(nameof(width), "A line needs a positive width.");
        return new MaskArea(path, width / 2, false);
    }

    /// <summary>The points of a line or a polygon, in the order they were drawn; empty for any other outline.</summary>
    public IReadOnlyList<(double X, double Z)> Points => Shape is MaskShape.Line or MaskShape.Polygon ? _points : [];

    /// <summary>A line's length in blocks, 0 for any other outline.</summary>
    public double Length => Shape == MaskShape.Line ? _arc[^1] : 0;

    /// <summary>The middle of the outline: an ellipse's centre, a polygon's centroid, the midpoint of a line's
    /// length; the origin for a board.</summary>
    public (double X, double Z) Centre
    {
        get
        {
            switch (Shape)
            {
                case MaskShape.Ellipse:
                    return (_centreX, _centreZ);
                case MaskShape.Polygon:
                    double twiceArea = 0, sumX = 0, sumZ = 0;
                    for (int i = 0, j = _points.Length - 1; i < _points.Length; j = i++)
                    {
                        var cross = _points[j].X * _points[i].Z - _points[i].X * _points[j].Z;
                        twiceArea += cross;
                        sumX += (_points[j].X + _points[i].X) * cross;
                        sumZ += (_points[j].Z + _points[i].Z) * cross;
                    }
                    return Math.Abs(twiceArea) < 1e-9
                        ? (_points.Average(p => p.X), _points.Average(p => p.Z))
                        : (sumX / (3 * twiceArea), sumZ / (3 * twiceArea));
                case MaskShape.Line:
                    return PointAt(_arc[^1] / 2);
                default:
                    return (0, 0);
            }
        }
    }

    /// <summary>The point on a line at an arc length from its first point, clamped to its ends.</summary>
    private (double X, double Z) PointAt(double arc)
    {
        var segment = 1;
        while (segment < _points.Length - 1 && _arc[segment] < arc) segment++;
        var span = _arc[segment] - _arc[segment - 1];
        var blend = span > 0 ? Math.Clamp((arc - _arc[segment - 1]) / span, 0, 1) : 0;
        return (_points[segment - 1].X + (_points[segment].X - _points[segment - 1].X) * blend,
                _points[segment - 1].Z + (_points[segment].Z - _points[segment - 1].Z) * blend);
    }

    /// <summary>Where a point stands against a line. Only a line answers.</summary>
    public LineFix Fix(double x, double z)
    {
        if (Shape != MaskShape.Line) throw new InvalidOperationException("Only a line has a side and a run.");
        var best = double.PositiveInfinity;
        var along = 0.0;
        var right = 1;
        for (var i = 1; i < _points.Length; i++)
        {
            var (ax, az) = _points[i - 1];
            var (bx, bz) = _points[i];
            double dx = bx - ax, dz = bz - az, lengthSquared = dx * dx + dz * dz;
            var at = lengthSquared > 0 ? Math.Clamp(((x - ax) * dx + (z - az) * dz) / lengthSquared, 0, 1) : 0;
            var distance = double.Hypot(x - (ax + at * dx), z - (az + at * dz));
            if (distance >= best) continue;
            best = distance;
            along = _arc[i - 1] + at * (_arc[i] - _arc[i - 1]);
            right = dx * (z - az) - dz * (x - ax) >= 0 ? 1 : -1;
        }
        return new LineFix(best, along, _flipped ? -right : right);
    }

    /// <summary>A line as a Catmull-Rom curve through its points, sampled about every
    /// <paramref name="step"/> blocks; any other outline is returned as it is.</summary>
    public MaskArea Smoothed(double step)
    {
        if (Shape != MaskShape.Line || _points.Length < 3) return this;
        var padded = new List<(double X, double Z)> { _points[0] };
        padded.AddRange(_points);
        padded.Add(_points[^1]);
        var curve = new List<(double X, double Z)>();
        for (var i = 1; i < padded.Count - 2; i++)
        {
            var (p0, p1, p2, p3) = (padded[i - 1], padded[i], padded[i + 1], padded[i + 2]);
            var samples = Math.Max(2, (int)(double.Hypot(p2.X - p1.X, p2.Z - p1.Z) / step));
            for (var k = 0; k < samples; k++)
            {
                double t = (double)k / samples, t2 = t * t, t3 = t2 * t;
                double Spline(double a, double b, double c, double d) =>
                    0.5 * (2 * b + (-a + c) * t + (2 * a - 5 * b + 4 * c - d) * t2 + (-a + 3 * b - 3 * c + d) * t3);
                curve.Add((Spline(p0.X, p1.X, p2.X, p3.X), Spline(p0.Z, p1.Z, p2.Z, p3.Z)));
            }
        }
        curve.Add(_points[^1]);
        var smoothed = new MaskArea([.. curve], Scale, _flipped);
        return _axis is null ? smoothed : smoothed.ImageOf(Drawn, _axis, _aboutX, _aboutZ);
    }

    /// <summary>How far out the point lies: 0 at the middle, 1 on the rim, over 1 outside.</summary>
    public double E(double x, double z)
    {
        switch (Shape)
        {
            case MaskShape.Line:
                return Fix(x, z).Distance / Scale;
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
    public MaskArea Image(string axis, double centreX, double centreZ) =>
        Shape == MaskShape.Board ? this : Imaged(axis, centreX, centreZ).ImageOf(this, axis, centreX, centreZ);

    private MaskArea Imaged(string axis, double centreX, double centreZ)
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
            case MaskShape.Line:
                return new MaskArea([.. _points.Select(p => Symmetry.Apply(p.X, p.Z, axis, centreX, centreZ))], Scale,
                                    _flipped ^ (Symmetry.Normal(axis) is not null));
            default:
                return this;
        }
    }

    private MaskArea ImageOf(MaskArea drawn, string axis, double centreX, double centreZ)
    {
        (Drawn, _axis, _aboutX, _aboutZ) = (drawn, axis, centreX, centreZ);
        return this;
    }

    /// <summary>The point of the drawn outline's frame (<see cref="Drawn"/>) that a point of this one is the image
    /// of, and the axis that carries it back here (null where the two frames are one). A drawn outline answers the
    /// point itself; a board folded by a symmetry answers the member of the point's orbit that stands for all of
    /// it. An operation that scatters or reads noise reads it there, so every image comes out as the same shape.</summary>
    public (double X, double Z, string? Axis) ToDrawn(double x, double z)
    {
        if (_axis is not null)
        {
            var (drawnX, drawnZ) = Symmetry.Apply(x, z, Symmetry.Inverse(_axis), _aboutX, _aboutZ);
            return (drawnX, drawnZ, _axis);
        }
        var best = (X: x, Z: z, Axis: (string?)null);
        foreach (var axis in _folding)
        {
            var (imageX, imageZ) = Symmetry.Apply(x, z, axis, _aboutX, _aboutZ);
            if (imageZ < best.Z - SameTolerance || (Math.Abs(imageZ - best.Z) <= SameTolerance && imageX < best.X - SameTolerance))
                best = (imageX, imageZ, Symmetry.Inverse(axis));
        }
        return best;
    }

    /// <summary>Where a point of the drawn outline's frame stands here, carried by the axis
    /// <see cref="ToDrawn"/> answered.</summary>
    public (double X, double Z) FromDrawn(double x, double z, string? axis) =>
        axis is null ? (x, z) : Symmetry.Apply(x, z, axis, _aboutX, _aboutZ);

    /// <summary>The board folded by a symmetry's axes about a centre: it measures as the board does, and reads
    /// its noise at one member of each orbit (<see cref="ToDrawn"/>).</summary>
    public static MaskArea FoldedBoard(IReadOnlyList<string> axes, double centreX, double centreZ) =>
        new(MaskShape.Board, 1) { _folding = [.. axes], _aboutX = centreX, _aboutZ = centreZ };

    /// <summary>The bounds of the outline in blocks: an ellipse's box of its longer radius, a polygon's or a
    /// line's points widened by its scale for a line; null for a board.</summary>
    public (double MinX, double MinZ, double MaxX, double MaxZ)? Bounds => Shape switch
    {
        MaskShape.Ellipse => (_centreX - Scale, _centreZ - Scale, _centreX + Scale, _centreZ + Scale),
        MaskShape.Polygon => (_points.Min(p => p.X), _points.Min(p => p.Z), _points.Max(p => p.X), _points.Max(p => p.Z)),
        MaskShape.Line => (_points.Min(p => p.X) - Scale, _points.Min(p => p.Z) - Scale,
                           _points.Max(p => p.X) + Scale, _points.Max(p => p.Z) + Scale),
        _ => null,
    };

    /// <summary>The areas and each of their images under every axis, in order, without any outline twice: an
    /// area standing on the symmetry, or an image the author also drew, would otherwise count double wherever
    /// areas add or melt.</summary>
    public static List<MaskArea> Fanned(IEnumerable<MaskArea> areas, IReadOnlyList<string> axes, double centreX, double centreZ)
    {
        var fanned = new List<MaskArea>();
        foreach (var area in areas)
        {
            if (area.Shape == MaskShape.Board)
            {
                if (!fanned.Any(area.SameOutline)) fanned.Add(axes.Count > 0 ? FoldedBoard(axes, centreX, centreZ) : area);
                continue;
            }
            if (!fanned.Any(area.SameOutline)) fanned.Add(area);
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
            case MaskShape.Line:
                if (_points.Length != other._points.Length || Math.Abs(Scale - other.Scale) > SameTolerance) return false;
                var reversed = _flipped != other._flipped;
                return _points.Select((p, i) => (p, q: other._points[reversed ? _points.Length - 1 - i : i]))
                              .All(pair => Math.Abs(pair.p.X - pair.q.X) <= SameTolerance
                                           && Math.Abs(pair.p.Z - pair.q.Z) <= SameTolerance);
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
