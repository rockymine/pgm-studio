namespace PgmStudio.Geom.Relief;

/// <summary>
/// One cell a mark states a height for, and <b>how firmly</b>. A pin of full weight is a statement: this ground
/// is at this height, and whatever an earlier mark said about the cell is replaced. A pin of less than full
/// weight is an <em>approach</em> — the cell grades this far toward the height from whatever is already pinned
/// there — which is what a mark's outer shoulder is, and what keeps two marks whose bands touch from meeting
/// on a wall.
///
/// <para>Weight is the mark's own reading of how far into its shoulder the cell sits: 1 at the edge of the flat
/// it guarantees, falling to 0 at the limit of its reach. So the ground runs from the mark's height at its
/// tread out to the neighbour's at its rim, and neither mark has to know the other exists. Over ground nobody
/// has claimed a shoulder states nothing at all and the cell is left to the relaxation, which is the only
/// answer that keeps the grade going: planting it at the mark's own height would stop the ramp dead at
/// whichever cell the earlier mark's band happened to end on.</para>
///
/// <para><b>Order still decides.</b> A mark can only grade into what was written before it, so the softening is
/// the later mark's, the same way the winning of a contested cell always was.</para>
/// </summary>
public readonly record struct Pin((int X, int Z) Cell, double Height, double Weight = 1);

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
    public abstract IEnumerable<Pin> Pins(Footprint footprint);

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
    public override IEnumerable<Pin> Pins(Footprint footprint)
    {
        var radius = Math.Max(0.5, Radius);
        for (var x = (int)(X - radius) - 1; x <= X + radius + 1; x++)
            for (var z = (int)(Z - radius) - 1; z <= Z + radius + 1; z++)
            {
                double dx = x + 0.5 - X, dz = z + 0.5 - Z;
                if (dx * dx + dz * dz <= radius * radius && footprint.Inside(x, z))
                    yield return new Pin((x, z), Height);
            }
    }
}

/// <summary>A ridgeline, a valley floor or any drawn height line: a polyline held at a height, varying along
/// its length when more than one height is given. <see cref="Radius"/> is how far either side of the
/// centerline is held — the same quantity a <see cref="PointMark"/> states, reaching from a line instead of
/// from a point — so one stroke is a knife edge or a broad shoulder and the band it writes is twice it.
///
/// <para><b>Tread</b> is how much of that band is <em>flat</em>, and it is what a line drawn as a road needs.
/// A line that passes close to itself — a serpentine, a spiral haul road, a switchback — pins every cell to
/// whichever pass is nearest, so two cells either side of the midline between two passes take heights a whole
/// winding apart and the ground between them is a wall, however far apart the passes are drawn. Stating a
/// tread narrower than the radius pins that much flat and <b>lofts</b> the rest: a cell out past the tread
/// with a second pass of the same line in reach takes a straight ramp between the two treads' edges, so a
/// serpentine comes out as flat road and graded batter rather than road and cliff. Unset, the tread is the
/// whole radius and every cell in the band is flat, which is what a ridgeline wants.</para>
///
/// <para><b>Batter</b> is how steeply that ramp falls, in degrees from level. Unset, it takes the whole run
/// the drawing leaves it — two passes <c>pitch</c> apart falling <c>drop</c> between them grade over
/// <c>pitch − 2·tread</c>, and nothing is flat between them. Stated, it may only be <em>steeper</em> than
/// that: the fall runs at the stated angle from the upper tread's edge and then holds at the lower pass's
/// height, which is a bench under a bank rather than one continuous slope. A batter gentler than the run
/// requires cannot be honoured — the ramp would not have arrived by the time it met the next tread, and what
/// is left over is a step — so it is raised to what the gap needs and the drawing stays the thing that
/// decides.</para>
/// </summary>
public sealed record LineMark(
    double[][] Points, double[] Heights, double Radius = 2, double Tread = double.NaN, double Batter = 0)
    : Mark
{
    /// <summary>The flat half-width, which is the whole radius unless a narrower one is stated.</summary>
    private double FlatTo => double.IsNaN(Tread) ? Radius : Math.Clamp(Tread, 0, Radius);

    /// <summary>The stated batter as a fall per block of run, or zero for none. Clamped short of vertical,
    /// since a mark states ground and ground that overhangs is not a height field.</summary>
    private double Fall => Batter <= 0 ? 0 : Math.Tan(Math.Clamp(Batter, 1, 89) * Math.PI / 180.0);

    public override IEnumerable<Pin> Pins(Footprint footprint)
    {
        if (Points.Length < 2) yield break;
        var flat = FlatTo;
        var fall = Fall;
        var shoulder = Radius - flat;
        // A second pass is one at least this far away measured ALONG the line, which is what tells a
        // neighbouring winding from the far side of a bend in the pass the cell is already on.
        var apart = Math.Max(1, 2 * Radius);

        foreach (var (x, z) in footprint.Land())
        {
            var (distance, along, _, _) = Polyline.Nearest(x + 0.5, z + 0.5, Points);
            if (distance > Radius) continue;

            var height = HeightAt(along);
            if (distance <= flat) { yield return new Pin((x, z), height); continue; }

            // How firmly the shoulder states its height: full at the tread's edge, nothing at the reach's, so
            // where the band runs over ground another mark has already pinned the two grade into one another
            // instead of meeting on a step. Nothing pinned there and the weight is spent on nothing — the
            // shoulder lands at its own height, which is what the whole band always did.
            var weight = shoulder <= 0 ? 1 : Math.Clamp((Radius - distance) / shoulder, 0, 1);

            // Out past the tread, and the band is still the mark's: what the tread changes is only what
            // happens where the line comes back past itself. With no second pass in reach the shoulder is
            // held at the line's own height, exactly as the whole band always was — a mark that stopped
            // claiming its band would hand those cells back to whichever earlier mark had pinned them, which
            // is a wall from somewhere else rather than a shoulder.
            // What puts a cell BETWEEN two passes rather than outside them both: the two nearest points lie
            // on opposite sides of it, so the vectors to them oppose. Which hand of each pass the cell falls
            // on cannot answer this — two limbs of a switchback travel opposite ways, so a cell between them
            // is on the same hand of both, while a cell outside is on opposite hands.
            //
            // The loft then runs only where the two bands actually meet, `d1 + d2 <= 2r`. Between two passes
            // that sum is the pitch, the same for every cell in the gap, so a gap is wholly lofted or wholly
            // not — where a bound on the second pass alone would cut through the middle of one and leave a
            // step at whichever cell fell the wrong side of it.
            var second = Polyline.NearestPass(x + 0.5, z + 0.5, Points, along, apart);
            if (second is not { } other || distance + other.Distance > 2 * Radius
                || !Polyline.Between(x + 0.5, z + 0.5, Points, along, other.Along))
            { yield return new Pin((x, z), height, weight); continue; }

            var otherHeight = HeightAt(other.Along);
            var here = Math.Max(0, distance - flat);
            var there = Math.Max(0, other.Distance - flat);
            var gap = here + there;
            if (gap <= 0) { yield return new Pin((x, z), height, weight); continue; }

            // Read in the gap's own frame rather than from whichever pass is nearer, so every cell between
            // the two treads answers one function of one distance and the ramp is continuous across the
            // midline. The needed fall is what the run demands; a stated batter may only exceed it.
            var high = Math.Max(height, otherHeight);
            var low = Math.Min(height, otherHeight);
            var fromHigh = height >= otherHeight ? here : there;
            // A cell between two passes of this line is the line's own business and is stated outright: the
            // ramp here is already the answer, and softening it into whatever lay under the band would let a
            // mark drawn earlier reach up between two windings of a road.
            var slope = Math.Max(fall, (high - low) / gap);
            yield return new Pin((x, z), Math.Max(high - slope * fromHigh, low));
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
    public override IEnumerable<Pin> Pins(Footprint footprint)
    {
        if (Ring.Length < 3) yield break;
        foreach (var (x, z) in footprint.Land())
            if (Polygon.PointInRing(x + 0.5, z + 0.5, Ring)) yield return new Pin((x, z), Height);
    }
}

/// <summary>The footprint's own outer rings held at one height — the statement that the land meets the void
/// at a known level. It is optional, and that matters: without one, marks alone decide the whole surface, so
/// a shape carrying a single high mark rises to that height everywhere and runs off its own edge, which is
/// usually what a group's interior wants and never what a lake wants.</summary>
public sealed record RimMark(double Height, int Depth = 1) : Mark
{
    public override IEnumerable<Pin> Pins(Footprint footprint)
    {
        foreach (var (x, z) in footprint.Land())
            if (RingDistance(footprint, x, z) <= Math.Max(1, Depth)) yield return new Pin((x, z), Height);
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
/// <para>The high side is the +z hand of the drawn direction — south of a line traced with x increasing, north
/// of one traced against it — and the band <b>stops where the line stops</b>.
/// Measuring perpendicular distance alone wraps a half-disc around each end, which for a scarp closes the gap
/// beside it — the gap being the entire reason the line was drawn to end there.</para>
/// </summary>
public sealed record ScarpMark(double[][] Points, double High, double Low,
                               double FaceWidth = 2, double BandWidth = 5) : Mark
{
    public override IEnumerable<Pin> Pins(Footprint footprint)
    {
        if (Points.Length < 2) yield break;
        var half = Math.Max(0.5, FaceWidth) / 2;
        var band = Math.Max(1, BandWidth);
        foreach (var (x, z) in footprint.Land())
        {
            var (distance, _, side, atEnd) = Polyline.Nearest(x + 0.5, z + 0.5, Points);
            if (atEnd) continue;
            if (distance < half || distance > half + band) continue;
            yield return new Pin((x, z), side > 0 ? High : Low);
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

    /// <summary>The nearest point of the line on a <b>separated stretch</b> of it — the second pass, where a
    /// line comes back past itself. <paramref name="notNear"/> is the fractional position to keep away from
    /// and <paramref name="apart"/> how far, measured along the line in the same units its points are in, so
    /// what counts as a different pass is a distance travelled rather than a distance across. That is the
    /// distinction a bend defeats: the far side of a tight corner is close in plan and close along the line,
    /// while the next winding of a spiral is close in plan and a whole turn away.
    ///
    /// <para>Null where the line never comes back — a straight ridge has one pass and nothing to ramp
    /// toward.</para></summary>
    public static (double Distance, double Along, int Side)? NearestPass(
        double x, double z, double[][] points, double notNear, double apart)
    {
        double best = double.MaxValue, bestAlong = 0, arc = 0, total = 0;
        var bestSide = 1;
        var found = false;

        for (var i = 0; i + 1 < points.Length; i++)
            total += Math.Sqrt(Square(points[i + 1][0] - points[i][0]) + Square(points[i + 1][1] - points[i][1]));
        if (total <= 0) return null;

        for (var i = 0; i + 1 < points.Length; i++)
        {
            double ax = points[i][0], az = points[i][1], bx = points[i + 1][0], bz = points[i + 1][1];
            double dx = bx - ax, dz = bz - az, length2 = dx * dx + dz * dz;
            var t = length2 <= 0 ? 0 : Math.Clamp(((x - ax) * dx + (z - az) * dz) / length2, 0, 1);
            var along = (arc + t * Math.Sqrt(length2)) / total;
            arc += Math.Sqrt(length2);
            if (Math.Abs(along - notNear) * total < apart) continue;

            double px = ax + t * dx, pz = az + t * dz;
            var distance = Math.Sqrt(Square(x - px) + Square(z - pz));
            if (distance >= best) continue;
            best = distance;
            bestAlong = along;
            bestSide = dx * (z - az) - dz * (x - ax) >= 0 ? 1 : -1;
            found = true;
        }
        return found ? (best, bestAlong, bestSide) : null;
    }

    /// <summary>Whether the cell lies <b>between</b> two points of the line rather than outside them both:
    /// the vectors from it to the two points oppose. Direction-free, which the hand each pass presents is
    /// not — two limbs of a switchback travel opposite ways and show a cell between them the same hand.
    /// </summary>
    public static bool Between(double x, double z, double[][] points, double along, double otherAlong)
    {
        var (ax, az) = PointAt(points, along);
        var (bx, bz) = PointAt(points, otherAlong);
        return (ax - x) * (bx - x) + (az - z) * (bz - z) < 0;
    }

    /// <summary>The point at a fractional position along the line — the inverse of the <c>Along</c> every
    /// read here answers with.</summary>
    public static (double X, double Z) PointAt(double[][] points, double along)
    {
        double total = 0;
        for (var i = 0; i + 1 < points.Length; i++)
            total += Math.Sqrt(Square(points[i + 1][0] - points[i][0]) + Square(points[i + 1][1] - points[i][1]));
        var wanted = Math.Clamp(along, 0, 1) * total;

        double arc = 0;
        for (var i = 0; i + 1 < points.Length; i++)
        {
            double ax = points[i][0], az = points[i][1];
            var length = Math.Sqrt(Square(points[i + 1][0] - ax) + Square(points[i + 1][1] - az));
            if (arc + length >= wanted || i == points.Length - 2)
            {
                var t = length <= 0 ? 0 : Math.Clamp((wanted - arc) / length, 0, 1);
                return (ax + t * (points[i + 1][0] - ax), az + t * (points[i + 1][1] - az));
            }
            arc += length;
        }
        return (points[0][0], points[0][1]);
    }

    private static double Square(double value) => value * value;
}
