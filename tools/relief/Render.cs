namespace PgmStudio.Relief;

/// <summary>The figure renderers. A relief is judged in two views and nothing else does the job: a
/// topographic read from above, which says where the high ground is and how steeply a route climbs, and a
/// block view from an angle, which says whether the result looks like terrain a player would walk on.</summary>
internal static class Render
{
    public const int Background = 0x080F1A;
    public const int Void = 0x0E121C;
    public const int Ink = 0xE8EEF9;
    public const int InkDim = 0x9FB0CC;
    public const int Axis = 0xA78BFA;
    public const int Accent = 0x38BDF8;
    public const int Warn = 0xFBBF24;
    public const int Bad = 0xF87171;
    public const int Good = 0x34D399;

    /// <summary>Hypsometric stops, low to high — the convention a topographic map reads by.</summary>
    private static readonly (double At, int Rgb)[] Ramp =
    [
        (0.00, 0x27455E), (0.12, 0x336B57), (0.30, 0x5C9450), (0.48, 0x9DB258),
        (0.66, 0xC8AE64), (0.82, 0xC08A63), (0.93, 0xAE7A72), (1.00, 0xF0EAE4),
    ];

    public static int Hypsometric(double position)
    {
        var at = Math.Clamp(position, 0, 1);
        for (var stop = 1; stop < Ramp.Length; stop++)
            if (at <= Ramp[stop].At)
                return Canvas.Lerp(Ramp[stop - 1].Rgb, Ramp[stop].Rgb,
                    (at - Ramp[stop - 1].At) / (Ramp[stop].At - Ramp[stop - 1].At));
        return Ramp[^1].Rgb;
    }

    /// <summary>The topographic view: elevation colour, relief shading taken off a smoothed copy so a
    /// one-block lip does not read as a cliff, and contour lines, which are the only one of the three that
    /// says how much a slope climbs rather than that it exists.</summary>
    public static Canvas TopDown(HeightField field, int scale, int contourInterval = 1,
                                 int? floor = null, int? ceiling = null, bool contours = true)
    {
        var footprint = field.Footprint;
        var lowest = floor ?? field.Min;
        var highest = ceiling ?? field.Max;
        var span = Math.Max(1, highest - lowest);
        var smoothed = Smooth(field);

        var canvas = new Canvas(footprint.Width, footprint.Depth, Void);
        for (var z = footprint.MinZ; z < footprint.MinZ + footprint.Depth; z++)
            for (var x = footprint.MinX; x < footprint.MinX + footprint.Width; x++)
            {
                if (!field.Has(x, z)) continue;
                var height = field.At(x, z);
                var tint = Hypsometric((height - lowest) / (double)span);
                var lit = Canvas.Scale(tint, Hillshade(smoothed, footprint, x, z));
                if (contours && CrossesBand(field, x, z, contourInterval))
                    lit = Canvas.Scale(lit, height % (contourInterval * 4) == 0 ? 0.45 : 0.68);
                canvas.Set(x - footprint.MinX, z - footprint.MinZ, lit);
            }
        return canvas.Upscale(scale);
    }

    /// <summary>The marks drawn over a rendered field — what the author actually said, so a figure can be
    /// read as a statement and its consequence at once.</summary>
    public static void DrawMarks(Canvas canvas, Footprint footprint, IEnumerable<Mark> marks, int scale)
    {
        double Sx(double x) => (x - footprint.MinX) * scale;
        double Sz(double z) => (z - footprint.MinZ) * scale;

        foreach (var mark in marks)
            switch (mark)
            {
                case PointMark point:
                    canvas.Ring(Sx(point.X), Sz(point.Z), Math.Max(3.0, point.Radius * scale), 1.6, Ink);
                    canvas.Disc(Sx(point.X), Sz(point.Z), 2.2, Ink);
                    break;
                case LineMark line:
                    for (var i = 0; i + 1 < line.Points.Length; i++)
                        canvas.Line((int)Sx(line.Points[i][0]), (int)Sz(line.Points[i][1]),
                                    (int)Sx(line.Points[i + 1][0]), (int)Sz(line.Points[i + 1][1]), Ink, 0.9);
                    foreach (var point in line.Points) canvas.Disc(Sx(point[0]), Sz(point[1]), 2.2, Ink);
                    break;
                case AreaMark area:
                    for (var i = 0; i < area.Ring.Length; i++)
                    {
                        var next = area.Ring[(i + 1) % area.Ring.Length];
                        canvas.Line((int)Sx(area.Ring[i][0]), (int)Sz(area.Ring[i][1]),
                                    (int)Sx(next[0]), (int)Sz(next[1]), Ink, 0.85);
                    }
                    break;
            }
    }

    /// <summary>The block view: every column drawn as a cube from a fixed angle, back to front. It is the
    /// only figure that answers the question the author is really asking — whether a solved surface reads as
    /// ground, or as a machined ramp.</summary>
    public static Canvas Isometric(HeightField field, int tileWidth = 4, int tileDepth = 2, int blockRise = 3,
                                   int? floor = null, int? ceiling = null, Func<(int X, int Z), int?>? tintOf = null)
    {
        var footprint = field.Footprint;
        var lowest = floor ?? field.Min;
        var highest = ceiling ?? field.Max;
        var span = Math.Max(1, highest - lowest);

        var cells = footprint.Cells().ToList();
        if (cells.Count == 0) return new Canvas(1, 1, Background);

        double ScreenX(int x, int z) => (x - footprint.MinX - (z - footprint.MinZ)) * tileWidth;
        double ScreenY(int x, int z, int height) => (x - footprint.MinX + (z - footprint.MinZ)) * tileDepth - (height - lowest) * blockRise;

        double minScreenX = double.MaxValue, maxScreenX = double.MinValue, minScreenY = double.MaxValue, maxScreenY = double.MinValue;
        foreach (var (x, z) in cells)
        {
            var height = field.At(x, z);
            foreach (var (cx, cz) in new[] { (x, z), (x + 1, z), (x, z + 1), (x + 1, z + 1) })
            {
                minScreenX = Math.Min(minScreenX, ScreenX(cx, cz)); maxScreenX = Math.Max(maxScreenX, ScreenX(cx, cz));
                minScreenY = Math.Min(minScreenY, ScreenY(cx, cz, height)); maxScreenY = Math.Max(maxScreenY, ScreenY(cx, cz, lowest));
            }
        }
        const int Pad = 8;
        var canvas = new Canvas((int)(maxScreenX - minScreenX) + 2 * Pad, (int)(maxScreenY - minScreenY) + 2 * Pad, Background);
        double offsetX = Pad - minScreenX, offsetY = Pad - minScreenY;

        foreach (var (x, z) in cells.OrderBy(cell => cell.X + cell.Z).ThenBy(cell => cell.X))
        {
            var height = field.At(x, z);
            var tint = tintOf?.Invoke((x, z)) ?? Hypsometric((height - lowest) / (double)span);

            (double, double) Point(int cx, int cz, int h) => (ScreenX(cx, cz) + offsetX, ScreenY(cx, cz, h) + offsetY);

            // The two visible walls run down to whichever neighbour they face, so an interior cell draws no
            // side at all and a plateau edge draws its full riser.
            var eastNeighbour = field.Has(x + 1, z) ? field.At(x + 1, z) : lowest - 2;
            var southNeighbour = field.Has(x, z + 1) ? field.At(x, z + 1) : lowest - 2;

            if (height > eastNeighbour)
            {
                var wall = Canvas.Scale(tint, 0.62);
                canvas.FillPolygon([Point(x + 1, z, height), Point(x + 1, z + 1, height),
                                    Point(x + 1, z + 1, eastNeighbour), Point(x + 1, z, eastNeighbour)], wall);
            }
            if (height > southNeighbour)
            {
                var wall = Canvas.Scale(tint, 0.45);
                canvas.FillPolygon([Point(x, z + 1, height), Point(x + 1, z + 1, height),
                                    Point(x + 1, z + 1, southNeighbour), Point(x, z + 1, southNeighbour)], wall);
            }
            canvas.FillPolygon([Point(x, z, height), Point(x + 1, z, height),
                                Point(x + 1, z + 1, height), Point(x, z + 1, height)], tint);
        }
        return canvas;
    }

    /// <summary>A cut through the field along one row — the block staircase seen edge on, which is how an
    /// author checks that a climb is a climb and not a wall.</summary>
    public static Canvas Section(HeightField field, int z, int scale = 4, int? floor = null, int? ceiling = null)
    {
        var footprint = field.Footprint;
        var lowest = floor ?? field.Min;
        var highest = ceiling ?? field.Max;
        var rows = highest - lowest + 3;
        var canvas = new Canvas(footprint.Width, rows, Background);

        for (var x = footprint.MinX; x < footprint.MinX + footprint.Width; x++)
        {
            if (!field.Has(x, z)) continue;
            var height = field.At(x, z);
            for (var y = lowest - 1; y <= height; y++)
            {
                var tint = y == height
                    ? Hypsometric((height - lowest) / (double)Math.Max(1, highest - lowest))
                    : Canvas.Scale(Hypsometric((height - lowest) / (double)Math.Max(1, highest - lowest)), 0.42);
                canvas.Set(x - footprint.MinX, rows - 2 - (y - lowest), tint);
            }
        }
        return canvas.Upscale(scale);
    }

    /// <summary>How the surface steps between neighbours, painted as the one thing a competitive map cannot
    /// get wrong: a flat, a walkable rise, a jump, and a drop no player crosses on foot.</summary>
    public static Canvas StepMap(HeightField field, int scale)
    {
        var footprint = field.Footprint;
        var canvas = new Canvas(footprint.Width, footprint.Depth, Void);
        foreach (var (x, z) in footprint.Cells())
        {
            var step = Terrain.MaxStep(field, x, z);
            var colour = step switch
            {
                0 => 0x223146,
                1 => 0x2F6B4F,
                2 or 3 => 0xB08428,
                _ => 0xA6383A,
            };
            canvas.Set(x - footprint.MinX, z - footprint.MinZ, colour);
        }
        return canvas.Upscale(scale);
    }

    private static double[] Smooth(HeightField field)
    {
        var footprint = field.Footprint;
        var smoothed = new double[footprint.Width * footprint.Depth];
        foreach (var (x, z) in footprint.Cells())
        {
            double total = 0; var counted = 0;
            for (var dz = -1; dz <= 1; dz++)
                for (var dx = -1; dx <= 1; dx++)
                {
                    if (!field.Has(x + dx, z + dz)) continue;
                    total += field.At(x + dx, z + dz); counted++;
                }
            smoothed[footprint.Index(x, z)] = counted == 0 ? 0 : total / counted;
        }
        return smoothed;
    }

    private static double Hillshade(double[] smoothed, Footprint footprint, int x, int z)
    {
        double At(int dx, int dz)
        {
            int nx = x + dx, nz = z + dz;
            return footprint.Inside(nx, nz) ? smoothed[footprint.Index(nx, nz)] : smoothed[footprint.Index(x, z)];
        }
        var dzdx = (At(1, -1) + 2 * At(1, 0) + At(1, 1) - (At(-1, -1) + 2 * At(-1, 0) + At(-1, 1))) / 8.0;
        var dzdz = (At(-1, 1) + 2 * At(0, 1) + At(1, 1) - (At(-1, -1) + 2 * At(0, -1) + At(1, -1))) / 8.0;
        var slope = Math.Atan(Math.Sqrt(dzdx * dzdx + dzdz * dzdz) * 2.2);
        var aspect = Math.Atan2(dzdz, -dzdx);
        const double Zenith = (90 - 45) * Math.PI / 180, Azimuth = (360 - 315 + 90) * Math.PI / 180;
        var shade = Math.Cos(Zenith) * Math.Cos(slope) + Math.Sin(Zenith) * Math.Sin(slope) * Math.Cos(Azimuth - aspect);
        return Math.Clamp(0.58 + 0.72 * shade, 0.45, 1.42);
    }

    private static bool CrossesBand(HeightField field, int x, int z, int interval)
    {
        var band = (int)Math.Floor(field.At(x, z) / (double)interval);
        foreach (var (dx, dz) in new[] { (1, 0), (0, 1) })
            if (field.Has(x + dx, z + dz) && (int)Math.Floor(field.At(x + dx, z + dz) / (double)interval) != band)
                return true;
        return false;
    }

    // ── figure assembly ───────────────────────────────────────────────────────────────────────────

    /// <summary>Lays panels out in a row with a caption strip above each, on the studio's canvas colour.</summary>
    public static Canvas Row(IEnumerable<(string Caption, Canvas Panel)> panels, int gap = 14, int captionHeight = 13)
    {
        var items = panels.ToList();
        var height = items.Max(item => item.Panel.Height) + captionHeight + 6;
        var width = items.Sum(item => item.Panel.Width) + gap * (items.Count - 1) + 12;
        var canvas = new Canvas(width, height + 6, Background);
        var x = 6;
        foreach (var (caption, panel) in items)
        {
            Text.Draw(canvas, caption, x, 4, InkDim);
            canvas.Blit(panel, x, captionHeight + 6);
            x += panel.Width + gap;
        }
        return canvas;
    }

    public static Canvas Stack(IEnumerable<Canvas> panels, int gap = 10)
    {
        var items = panels.ToList();
        var width = items.Max(panel => panel.Width);
        var height = items.Sum(panel => panel.Height) + gap * (items.Count - 1);
        var canvas = new Canvas(width, height, Background);
        var y = 0;
        foreach (var panel in items) { canvas.Blit(panel, 0, y); y += panel.Height + gap; }
        return canvas;
    }
}
