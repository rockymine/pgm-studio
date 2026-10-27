namespace PgmStudio.Geom;

/// <summary>The convex forms a <see cref="Solid"/> takes, and the one non-convex one.</summary>
public enum SolidForm
{
    /// <summary>A cuboid: <c>Width</c> across, <c>Height</c> up and <c>Depth</c> along.</summary>
    Box,

    /// <summary>A box cut by the plane falling from the full <c>Height</c> at its near (−depth) end to nothing at
    /// its far end.</summary>
    Wedge,

    /// <summary>An upright cylinder, elliptical where <c>Width</c> and <c>Depth</c> differ.</summary>
    Cylinder,

    /// <summary>An ellipsoid with the three semi-axes; a sphere where they agree.</summary>
    Sphere,

    /// <summary>A ring whose outer edge fills <c>Width</c> by <c>Depth</c> and whose tube has radius
    /// <c>Tube</c>, lying flat before it is tilted.</summary>
    Torus,
}

/// <summary>
/// A solid body in the block grid: a form placed over a plan point, turned about the vertical, tilted about its
/// own width axis and set on a floor. Pure geometry — what a column of blocks meets of it, as runs.
///
/// <para><b>The frame.</b> The body is built in its own frame: <c>u</c> across its width, <c>w</c> up its height,
/// <c>v</c> along its depth. A tilt of <c>t</c> degrees turns <c>w</c> and <c>v</c> about <c>u</c> so the
/// <c>+v</c> end rises; a turn of <c>a</c> degrees then swings the <c>u</c> axis to <c>(cos a, sin a)</c> on the
/// plan, clockwise as the canvas draws it. The frame's origin is the plan centre
/// (<see cref="CentreX"/>, <see cref="CentreZ"/>), and its height is set so the body's lowest point stands
/// <see cref="Sunk"/> blocks below <see cref="Floor"/>.</para>
///
/// <para><b>A block is in the body when its centre is.</b> A centre on the surface counts as inside, so every
/// symmetry of the body is a symmetry of its blocks. The forms but the torus are convex, so a vertical line
/// meets them in one run; a torus on its edge is met twice by a column through its hole, which is why
/// <see cref="Columns"/> answers runs and not a column.</para>
/// </summary>
public sealed record Solid(
    SolidForm Form, double CentreX, double CentreZ, double Width, double Height, double Depth,
    double Turn = 0, double Tilt = 0, double Floor = 0, double Sunk = 0, double Tube = 0)
{
    private const double Edge = 1e-9;

    /// <summary>The ceiling above which no run is searched, matching the world's own height.</summary>
    private const int SearchCeiling = 320;

    private double HalfWidth => Width / 2;
    private double HalfDepth => Form == SolidForm.Torus ? Width / 2 : Depth / 2;
    private double HalfHeight => Form == SolidForm.Torus ? TubeRadius : Height / 2;
    private double TubeRadius => Math.Min(Math.Max(Tube, 0), Width / 2);

    /// <summary>Whether the body has any volume.</summary>
    public bool IsEmpty => Width <= 0 || (Form == SolidForm.Torus ? Tube <= 0 : Depth <= 0 || Height <= 0);

    /// <summary>The vertical place of the frame's origin: the body's lowest point stands on
    /// <see cref="Floor"/> less <see cref="Sunk"/>.</summary>
    private double OriginY => Floor - Sunk + Reach(0, -1, 0);

    /// <summary>The lowest y the body reaches.</summary>
    public double Lowest => OriginY - Reach(0, -1, 0);

    /// <summary>The highest y the body reaches.</summary>
    public double Highest => OriginY + Reach(0, 1, 0);

    /// <summary>The plan box the body fits in: <c>(MinX, MinZ, MaxX, MaxZ)</c>.</summary>
    public (double MinX, double MinZ, double MaxX, double MaxZ) PlanBounds()
        => (CentreX - Reach(-1, 0, 0), CentreZ - Reach(0, 0, -1), CentreX + Reach(1, 0, 0), CentreZ + Reach(0, 0, 1));

    /// <summary>Every run of solid blocks, as the blocks <c>[Floor, Top)</c> at <c>(X, Z)</c>, column by
    /// column and lowest run first within a column. A run below the world's floor is cut at it, and one that
    /// ends there is dropped.</summary>
    public IEnumerable<(int X, int Z, int Floor, int Top)> Columns()
    {
        if (IsEmpty) yield break;
        var (minX, minZ, maxX, maxZ) = PlanBounds();
        int x0 = (int)Math.Floor(minX), x1 = (int)Math.Ceiling(maxX) - 1;
        int z0 = (int)Math.Floor(minZ), z1 = (int)Math.Ceiling(maxZ) - 1;
        int y0 = Math.Max(0, (int)Math.Floor(Lowest)), y1 = Math.Min(SearchCeiling, (int)Math.Ceiling(Highest) - 1);
        if (y1 < y0) yield break;

        var (cosTurn, sinTurn) = Trig(Turn);
        var (cosTilt, sinTilt) = Trig(Tilt);
        var originY = OriginY;
        for (var x = x0; x <= x1; x++)
        for (var z = z0; z <= z1; z++)
        {
            double dx = x + 0.5 - CentreX, dz = z + 0.5 - CentreZ;
            double across = dx * cosTurn + dz * sinTurn, along = -dx * sinTurn + dz * cosTurn;
            var start = -1;
            for (var y = y0; y <= y1 + 1; y++)
            {
                var inside = y <= y1 && Holds(across, along, y + 0.5 - originY, cosTilt, sinTilt);
                if (inside && start < 0) start = y;
                else if (!inside && start >= 0)
                {
                    yield return (x, z, start, y);
                    start = -1;
                }
            }
        }
    }

    // A world point in the turned frame (across, along) and the height over the origin, brought into the
    // body's own frame by undoing the tilt, then asked of the form.
    private bool Holds(double across, double along, double up, double cosTilt, double sinTilt)
    {
        var height = up * cosTilt - along * sinTilt;
        var depth = up * sinTilt + along * cosTilt;
        double a = HalfWidth, b = HalfHeight, c = HalfDepth;
        switch (Form)
        {
            case SolidForm.Box:
                return Math.Abs(across) <= a + Edge && Math.Abs(height) <= b + Edge && Math.Abs(depth) <= c + Edge;
            case SolidForm.Wedge:
            {
                if (Math.Abs(across) > a + Edge || Math.Abs(depth) > c + Edge || height < -b - Edge) return false;
                return height <= -b + Height * (c - depth) / Depth + Edge;
            }
            case SolidForm.Cylinder:
                return Math.Abs(height) <= b + Edge && Sq(across / a) + Sq(depth / c) <= 1 + Edge;
            case SolidForm.Sphere:
                return Sq(across / a) + Sq(height / b) + Sq(depth / c) <= 1 + Edge;
            default:
                return Sq(Math.Sqrt(across * across + depth * depth) - (a - TubeRadius)) + height * height
                       <= TubeRadius * TubeRadius + Edge;
        }
    }

    // How far the body reaches from the frame's origin in a world direction: the support function of the form
    // in the body's own frame, for the direction brought into it.
    private double Reach(double dirX, double dirY, double dirZ)
    {
        var (cosTurn, sinTurn) = Trig(Turn);
        var (cosTilt, sinTilt) = Trig(Tilt);
        double across = dirX * cosTurn + dirZ * sinTurn, along = -dirX * sinTurn + dirZ * cosTurn;
        double u = across, w = dirY * cosTilt - along * sinTilt, v = dirY * sinTilt + along * cosTilt;
        double a = HalfWidth, b = HalfHeight, c = HalfDepth;
        switch (Form)
        {
            case SolidForm.Box:
                return Math.Abs(u) * a + Math.Abs(w) * b + Math.Abs(v) * c;
            case SolidForm.Wedge:
            {
                var tallEnd = Math.Abs(u) * a + w * b - v * c;
                var base1 = Math.Abs(u) * a - w * b + Math.Abs(v) * c;
                return Math.Max(tallEnd, base1);
            }
            case SolidForm.Cylinder:
                return Math.Abs(w) * b + Math.Sqrt(Sq(u * a) + Sq(v * c));
            case SolidForm.Sphere:
                return Math.Sqrt(Sq(u * a) + Sq(w * b) + Sq(v * c));
            default:
                return (a - TubeRadius) * Math.Sqrt(u * u + v * v) + TubeRadius * Math.Sqrt(u * u + w * w + v * v);
        }
    }

    // Exact at the quarter turns, so a body turned 90 degrees swaps its width and depth with no rounding to
    // tip a block centre off an edge.
    private static (double Cos, double Sin) Trig(double degrees)
    {
        var turned = ((degrees % 360) + 360) % 360;
        return turned switch
        {
            0 => (1, 0),
            90 => (0, 1),
            180 => (-1, 0),
            270 => (0, -1),
            _ => (Math.Cos(turned * Math.PI / 180), Math.Sin(turned * Math.PI / 180)),
        };
    }

    private static double Sq(double value) => value * value;
}
