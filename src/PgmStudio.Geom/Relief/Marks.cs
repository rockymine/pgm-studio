namespace PgmStudio.Geom.Relief;

/// <summary>
/// What an author states about height: a placed mark holding a patch of the footprint at a chosen elevation.
/// Every kind reduces to the same thing for the solver — a set of cells pinned to a value — which is what
/// lets one field carry a summit, a ridgeline, a flat bench, a rim and a scarp at once.
///
/// <para>A mark is <b>clipped</b> to the footprint rather than confined to it, so one placed past an edge
/// contributes only its overlap. That is not a tolerance: it is how a hill at the corner of a map is
/// authored, rising into the corner and stopping there rather than cresting inside the board and falling
/// down a back slope nobody will stand on.</para>
/// </summary>
public abstract record Mark
{
    /// <summary>The cells this mark pins and the height it pins each to.</summary>
    public abstract IEnumerable<((int X, int Z) Cell, double Height)> Pins(Footprint footprint);

    /// <summary>Whether the sculpting passes may move what this mark pinned. A mark states a height about
    /// the <em>ground</em>, and a push composes over ground — that is the whole difference between the two
    /// halves of the vocabulary. A built floor is not ground: a spawn or a wool room is a level rectangle
    /// that can never slope, and one course of it lifted is a floor standing over a hole. So a mark placed
    /// for a floor is rigid, and the lift steps over it exactly as the grain already does.</summary>
    public bool Rigid { get; init; }
}

/// <summary>A summit, a hollow or a spot height: one position, one height, and the radius over which it is
/// held. A radius of zero pins a single cell and reads as a spike; from about two up it reads as a summit,
/// which is the whole reason the radius exists.</summary>
public sealed record PointMark(double X, double Z, double Height, double Radius = 2) : Mark
{
    public override IEnumerable<((int X, int Z) Cell, double Height)> Pins(Footprint footprint)
    {
        var radius = Math.Max(0.5, Radius);
        for (var x = (int)(X - radius) - 1; x <= X + radius + 1; x++)
            for (var z = (int)(Z - radius) - 1; z <= Z + radius + 1; z++)
            {
                double dx = x + 0.5 - X, dz = z + 0.5 - Z;
                if (dx * dx + dz * dz <= radius * radius && footprint.Inside(x, z))
                    yield return ((x, z), Height);
            }
    }
}

/// <summary>A ridgeline, a valley floor or any drawn height line: a polyline held at a height, varying along
/// its length when more than one height is given. <see cref="Radius"/> is how far either side of the
/// centerline is held — the same quantity a <see cref="PointMark"/> states, reaching from a line instead of
/// from a point — so one stroke is a knife edge or a broad shoulder and the band it writes is twice it.</summary>
public sealed record LineMark(double[][] Points, double[] Heights, double Radius = 2) : Mark
{
    public override IEnumerable<((int X, int Z) Cell, double Height)> Pins(Footprint footprint)
    {
        if (Points.Length < 2) yield break;
        foreach (var (x, z) in footprint.Land())
        {
            var (distance, along, _, _) = Polyline.Nearest(x + 0.5, z + 0.5, Points);
            if (distance > Radius) continue;
            yield return ((x, z), HeightAt(along));
        }
    }

    /// <summary>The stated height at a fractional position along the line — one height holds the whole run,
    /// several interpolate between the vertices so a ridge can fall as it goes.</summary>
    public double HeightAt(double along)
    {
        if (Heights.Length == 0) return 0;
        if (Heights.Length == 1) return Heights[0];
        var scaled = Math.Clamp(along, 0, 1) * (Heights.Length - 1);
        var lower = Math.Min(Heights.Length - 2, (int)scaled);
        return Heights[lower] + (Heights[lower + 1] - Heights[lower]) * (scaled - lower);
    }
}

/// <summary>A bench, a mesa top or a sunken floor: every cell inside a ring held at one height, so the field
/// arrives at a genuinely flat surface rather than the rounded top an interpolation would give.</summary>
public sealed record AreaMark(double[][] Ring, double Height) : Mark
{
    public override IEnumerable<((int X, int Z) Cell, double Height)> Pins(Footprint footprint)
    {
        if (Ring.Length < 3) yield break;
        foreach (var (x, z) in footprint.Land())
            if (Polygon.PointInRing(x + 0.5, z + 0.5, Ring)) yield return ((x, z), Height);
    }
}

/// <summary>The footprint's own outer rings held at one height — the statement that the land meets the void
/// at a known level. It is optional, and that matters: without one, marks alone decide the whole surface, so
/// a shape carrying a single high mark rises to that height everywhere and runs off its own edge, which is
/// usually what a group's interior wants and never what a lake wants.</summary>
public sealed record RimMark(double Height, int Depth = 1) : Mark
{
    public override IEnumerable<((int X, int Z) Cell, double Height)> Pins(Footprint footprint)
    {
        foreach (var (x, z) in footprint.Land())
            if (RingDistance(footprint, x, z) <= Math.Max(1, Depth)) yield return ((x, z), Height);
    }

    // A boundary cell is one step from the void, so the outermost ring answers 1 — the depth counts rings
    // inward from there rather than from zero, which nothing outside the footprint could occupy.
    private static int RingDistance(Footprint footprint, int x, int z)
    {
        for (var radius = 0; radius < 12; radius++)
            for (var dx = -radius; dx <= radius; dx++)
                for (var dz = -radius; dz <= radius; dz++)
                    if (Math.Max(Math.Abs(dx), Math.Abs(dz)) == radius && !footprint.Inside(x + dx, z + dz))
                        return radius;
        return int.MaxValue;
    }
}

/// <summary>
/// A face too steep to walk up: the mark that makes terrain decide where players go. Every other mark states
/// a height; this one states a <b>drop</b> — a height each side of a drawn line and the width of the face
/// between them — so what the author picks is the grade, and the grade decides whether the line can be
/// crossed on foot, crossed by placing a block, or not crossed at all.
///
/// <para>It pins two bands and leaves the face free, and the relaxation runs a near-linear ramp between two
/// pinned levels: a ten-block drop over ten blocks of run is a hillside, over five it is a wall a player can
/// spend a block on, and over two it is not crossed on foot in either direction — though it is still walked
/// <em>down</em>, because a drop is not a barrier. One number spells all three.</para>
///
/// <para>The high side is the left of the drawn direction, and the band <b>stops where the line stops</b>.
/// Measuring perpendicular distance alone wraps a half-disc around each end, which for a scarp closes the gap
/// beside it — the gap being the entire reason the line was drawn to end there.</para>
/// </summary>
public sealed record ScarpMark(double[][] Points, double High, double Low,
                               double FaceWidth = 2, double BandWidth = 5) : Mark
{
    public override IEnumerable<((int X, int Z) Cell, double Height)> Pins(Footprint footprint)
    {
        if (Points.Length < 2) yield break;
        var half = Math.Max(0.5, FaceWidth) / 2;
        var band = Math.Max(1, BandWidth);
        foreach (var (x, z) in footprint.Land())
        {
            var (distance, _, side, atEnd) = Polyline.Nearest(x + 0.5, z + 0.5, Points);
            if (atEnd) continue;
            if (distance < half || distance > half + band) continue;
            yield return ((x, z), side > 0 ? High : Low);
        }
    }

    /// <summary>The rise per block of run the face works out to — what the mark is actually choosing.</summary>
    public double Grade => Math.Abs(High - Low) / Math.Max(1, FaceWidth);
}

/// <summary> A shape of ground lifted or lowered, rather than a height stated. Every <see cref="Mark"/> is a
/// <em>constraint</em> — the ground here <b>is</b> twelve — and constraints are what a solver needs but not what
/// a hand wants: stated as a position and a radius they can only make a round hill, and the roundness is not a
/// style, it is the shape of the only footprint that could be typed. <para>A push takes a drawn ring and raises
/// the ground inside it, falling away outside over a stated distance, so the landform's plan is whatever was
/// drawn. It applies to the solved surface rather than into it, which is what makes pushes <b>compose</b>: two
/// over the same ground add, where two constraints would have to argue.</para>
/// <para>The falloff is distance from the ring <b>across the land</b>, never from a centre — a radial falloff
/// rounds a long thin push off within a few blocks of its own outline and cannot keep the hollow inside a
/// crescent's curve at all.</para>
/// <para><b>Amounts</b> — A lift per ring vertex instead of one for the whole outline, interpolated around the
/// ring and wrapped so a closed loop has no seam. This is what makes a drawn ridge a ridge rather than a plateau
/// with a shaped edge. Null uses <paramref name="Amount"/> the whole way round.</para>
/// <para><b>Crown</b> — How much higher the middle of the push stands than its edge. Zero is a flat top; positive
/// domes it; negative dishes it into a hollow whose rim is the drawn outline. <para>What "the middle" means is
/// not authored, because the shape already knows: it is the deepest point of the outline measured inward, which
/// is the medial axis. For a round push that is a <b>point</b> and the result is a dome; for a long one it is a
/// <b>line</b> and the result is a ridge whose crest follows the shape's own spine.</para></para>
/// <para><b>Roughness</b> — How far the falloff distance wanders against a noise field, so the skirt is not a
/// clean offset of the outline — the difference between a hill and an extruded logo.</para></summary>
public sealed record PushMark(double[][] Ring, double Amount, double Falloff = 10, double Roughness = 0,
                              uint Seed = 1, double[]? Amounts = null, double Crown = 0)
{
    /// <summary>How the lift decays from the ring outward. Smoothstep flattens at both ends, so the push
    /// leaves the surrounding land level and meets its own edge without a crease.</summary>
    public static double Ease(double t) => t <= 0 ? 1 : t >= 1 ? 0 : 1 - t * t * (3 - 2 * t);

    /// <summary>The lift stated at a fractional position around the ring, wrapped at the seam.</summary>
    public double AmountAt(double around)
    {
        if (Amounts is not { Length: > 1 } stated) return Amount;
        var scaled = Math.Clamp(around, 0, 1) * stated.Length;
        var lower = (int)scaled % stated.Length;
        var upper = (lower + 1) % stated.Length;
        return stated[lower] + (stated[upper] - stated[lower]) * (scaled - (int)scaled);
    }
}

/// <summary>Distance from a point to a drawn line, and the three facts a mark needs alongside it.</summary>
public static class Polyline
{
    /// <summary>The distance to the nearest point of the line, that point's fractional position along it, and
    /// two facts a band cannot do without: which <b>side</b> of the line the point falls on (+1 to the left of
    /// the direction of travel), and whether the nearest point is one of the line's two <b>ends</b> rather
    /// than a point along it.</summary>
    public static (double Distance, double Along, int Side, bool AtEnd) Nearest(double x, double z, double[][] points)
    {
        double best = double.MaxValue, bestAlong = 0, arc = 0, total = 0;
        var bestSide = 1;
        var atEnd = false;

        for (var i = 0; i + 1 < points.Length; i++)
            total += Math.Sqrt(Square(points[i + 1][0] - points[i][0]) + Square(points[i + 1][1] - points[i][1]));
        if (total <= 0) total = 1;

        for (var i = 0; i + 1 < points.Length; i++)
        {
            double ax = points[i][0], az = points[i][1], bx = points[i + 1][0], bz = points[i + 1][1];
            double dx = bx - ax, dz = bz - az, length2 = dx * dx + dz * dz;
            var t = length2 <= 0 ? 0 : Math.Clamp(((x - ax) * dx + (z - az) * dz) / length2, 0, 1);
            double px = ax + t * dx, pz = az + t * dz;
            var distance = Math.Sqrt(Square(x - px) + Square(z - pz));
            if (distance < best)
            {
                best = distance;
                bestAlong = (arc + t * Math.Sqrt(length2)) / total;
                bestSide = dx * (z - az) - dz * (x - ax) >= 0 ? 1 : -1;
                atEnd = (i == 0 && t <= 0) || (i == points.Length - 2 && t >= 1);
            }
            arc += Math.Sqrt(length2);
        }
        return (best, bestAlong, bestSide, atEnd);
    }

    private static double Square(double value) => value * value;
}
