using PgmStudio.Geom.Relief;
namespace PgmStudio.Relief;

/// <summary>What a solved surface can be asked about, and the operations that act on it. Everything here
/// reads the block surface rather than the continuous one, because the block surface is what a player
/// stands on and every question below is a question about play.</summary>
internal static class Terrain
{
    private static readonly (int X, int Z)[] Neighbours4 = [(1, 0), (-1, 0), (0, 1), (0, -1)];

    /// <summary>The largest height difference between a cell and a land neighbour. The void is not a step —
    /// a player does not walk off the map — so only cells that exist are counted.</summary>
    public static int MaxStep(HeightField field, int x, int z)
    {
        var here = field.At(x, z);
        var worst = 0;
        foreach (var (dx, dz) in Neighbours4)
            if (field.Has(x + dx, z + dz)) worst = Math.Max(worst, Math.Abs(field.At(x + dx, z + dz) - here));
        return worst;
    }

    /// <summary>
    /// What a step costs a player, which is the only reading of a height difference that means anything. The
    /// tiers come from how the game moves rather than from a preference for flat ground: a one-block rise is
    /// a jump and costs nothing but the jump; a two-block rise cannot be jumped and is crossed by placing a
    /// block, which is slow and leaves the player standing still in the open; three and above is not crossed
    /// at all without building in earnest.
    ///
    /// <para>None of the three is a fault. A map that is <see cref="Walk"/> everywhere is a field, and the
    /// terrain of a good one is a mix — which is why the readback reports reachability at each tier instead
    /// of scoring a surface against the flattest one.</para>
    /// </summary>
    internal enum Passage { Walk = 1, Scramble = 2, Barrier = 3 }

    /// <summary>The largest step a tier admits.</summary>
    public static int Admits(Passage tier) => tier switch { Passage.Walk => 1, Passage.Scramble => 2, _ => 99 };

    /// <summary>The land reachable from a cell by a player willing to cross steps up to
    /// <paramref name="tier"/>. Running it at <see cref="Passage.Walk"/> and again at
    /// <see cref="Passage.Scramble"/> is what separates ground that is genuinely cut off from ground that is
    /// merely defended: the first says where a player can go, the second where a player who will spend a
    /// block can go, and the difference between them is the terrain's grip on the map's flow.</summary>
    public static List<HashSet<(int X, int Z)>> Reachable(HeightField field, Passage tier)
        => WalkableRegions(field, Admits(tier));

    /// <summary>The land a player can reach on foot from a cell: a flood fill that crosses a boundary only
    /// where the step is within reach. More than one component at a tier means the relief has separated the
    /// map for a player at that tier — deliberate as often as not, which is why it is reported rather than
    /// judged.</summary>
    public static List<HashSet<(int X, int Z)>> WalkableRegions(HeightField field, int walkableStep = 1)
    {
        var seen = new HashSet<(int X, int Z)>();
        var regions = new List<HashSet<(int X, int Z)>>();
        foreach (var start in field.Footprint.Land())
        {
            if (!seen.Add(start)) continue;
            var region = new HashSet<(int X, int Z)> { start };
            var queue = new Queue<(int X, int Z)>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var (x, z) = queue.Dequeue();
                foreach (var (dx, dz) in Neighbours4)
                {
                    var next = (x + dx, z + dz);
                    if (!field.Has(next.Item1, next.Item2)) continue;
                    if (Math.Abs(field.At(next.Item1, next.Item2) - field.At(x, z)) > walkableStep) continue;
                    if (!seen.Add(next)) continue;
                    region.Add(next); queue.Enqueue(next);
                }
            }
            regions.Add(region);
        }
        return regions.OrderByDescending(region => region.Count).ToList();
    }

    /// <summary>A one-way drop and its measurements: the cells on the high side of a run of steep boundary,
    /// how wide the run is and how far it falls. The qualification is the corpus rule — a real cliff cuts a
    /// wide face and falls far; a narrow steep seam is the edge of a staircase, not a cliff.</summary>
    internal sealed record Scarp(List<(int X, int Z)> Cells, int Width, int Drop)
    {
        public bool IsCliff => Width >= 10 && Drop >= 6;
    }

    public static List<Scarp> Scarps(HeightField field, int minimumDrop = 4)
    {
        var steep = new Dictionary<(int X, int Z), int>();
        foreach (var (x, z) in field.Footprint.Land())
        {
            var drop = 0;
            foreach (var (dx, dz) in Neighbours4)
                if (field.Has(x + dx, z + dz)) drop = Math.Max(drop, field.At(x, z) - field.At(x + dx, z + dz));
            if (drop >= minimumDrop) steep[(x, z)] = drop;
        }

        var seen = new HashSet<(int X, int Z)>();
        var scarps = new List<Scarp>();
        foreach (var start in steep.Keys)
        {
            if (!seen.Add(start)) continue;
            var run = new List<(int X, int Z)> { start };
            var queue = new Queue<(int X, int Z)>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var (x, z) = queue.Dequeue();
                for (var dx = -1; dx <= 1; dx++)
                    for (var dz = -1; dz <= 1; dz++)
                    {
                        var next = (x + dx, z + dz);
                        if (!steep.ContainsKey(next) || !seen.Add(next)) continue;
                        run.Add(next); queue.Enqueue(next);
                    }
            }
            // A face's width is its longer plan extent — the run is one cell deep on the high lip, so the
            // extent along the face is what "how wide is this cliff" means.
            var width = Math.Max(run.Max(c => c.X) - run.Min(c => c.X) + 1, run.Max(c => c.Z) - run.Min(c => c.Z) + 1);
            scarps.Add(new Scarp(run, width, run.Max(cell => steep[cell])));
        }
        return scarps.OrderByDescending(scarp => scarp.Width).ToList();
    }

    /// <summary>Where a barrier running north–south can actually be got past, and at what cost: each row is
    /// walked west to east across the corridor and admitted only if every step in it is within the tier, and
    /// the admitted rows are grouped into the runs a player would recognise as fords. It is the measurement
    /// terrain-as-flow-control is authored against — the point of a cliffed bank is not that it is steep but
    /// that it leaves exactly three ways through, at places the author chose.</summary>
    /// <param name="eastbound">Which way the crossing is attempted. A drop is not a barrier — a player walks
    /// off a ledge and takes the fall — so only the climbs count against the tier, and a face that stops a
    /// crossing one way lets it through the other. That is what a cliff <em>is</em>, and measuring both
    /// directions is the only way to see it: a bank that reads as a wall from the river is a shortcut from
    /// the plateau.</param>
    public static List<(int From, int To)> Fords(HeightField field, Func<(int X, int Z), bool> inCorridor,
                                                 Passage tier, bool eastbound = true, int fallLimit = 20)
    {
        var limit = Admits(tier);
        var open = new List<int>();
        var footprint = field.Footprint;

        for (var z = footprint.MinZ; z < footprint.MinZ + footprint.Depth; z++)
        {
            var band = Enumerable.Range(footprint.MinX, footprint.Width)
                                 .Where(x => footprint.Inside(x, z) && inCorridor((x, z))).ToList();
            if (band.Count == 0) continue;
            int from = band.Min() - 2, to = band.Max() + 2;
            var passable = true;
            for (var x = from; x < to && passable; x++)
            {
                if (!field.Has(x, z) || !field.Has(x + 1, z)) { passable = false; break; }
                var rise = field.At(x + 1, z) - field.At(x, z);
                if (!eastbound) rise = -rise;
                if (rise > limit || rise < -fallLimit) passable = false;
            }
            if (passable) open.Add(z);
        }

        var fords = new List<(int, int)>();
        for (var i = 0; i < open.Count;)
        {
            var start = i;
            while (i + 1 < open.Count && open[i + 1] == open[i] + 1) i++;
            fords.Add((open[start], open[i]));
            i++;
        }
        return fords;
    }

    /// <summary>What a barrier actually costs, which is not always that it cannot be crossed. A face too
    /// steep to climb rarely cuts a map in two — there is usually a way round the end of it — so the honest
    /// measure is how far round: the least-climb route between two places against the straight-line distance
    /// between them. A detour of three is a barrier doing its job whether or not anything is unreachable, and
    /// it is the number an author is really choosing when they pick a grade.</summary>
    public static (int Steps, int Direct, double Detour) CrossingCost(HeightField field, (int X, int Z) from,
                                                                     (int X, int Z) to, Passage tier)
    {
        var route = Route(field, from, to, Admits(tier));
        var direct = Math.Abs(to.X - from.X) + Math.Abs(to.Z - from.Z);
        if (route.Count == 0) return (0, direct, double.PositiveInfinity);
        return (route.Count, direct, (double)route.Count / Math.Max(1, direct));
    }

    /// <summary>Copies the canonical half of a surface onto its mirror image. Every pass that runs after the
    /// solve — a carve, a graded road, a stair cut — decides things by walking the map, and a walk has a
    /// direction the symmetry does not preserve, so each one has to be folded again or it undoes the
    /// fairness the solve established.</summary>
    public static HeightField Fold(HeightField field, string mode, double centreX, double centreZ)
    {
        var blocks = (int[])field.Blocks.Clone();
        foreach (var (x, z) in field.Footprint.Land())
        {
            var canonical = mode == "rot_180"
                ? z + 0.5 < centreZ || (z + 0.5 == centreZ && x + 0.5 <= centreX)
                : x + 0.5 <= centreX;
            if (canonical) continue;
            var (mx, mz) = Mirror(x + 0.5, z + 0.5, mode, centreX, centreZ);
            int ix = (int)Math.Floor(mx), iz = (int)Math.Floor(mz);
            if (field.Has(ix, iz)) blocks[field.Footprint.Index(x, z)] = field.At(ix, iz);
        }
        return field.WithBlocks(blocks);
    }

    /// <summary>The worst disagreement between a field and its own mirror image — the fairness measure. Zero
    /// is the only passing answer on a competitive map, and it is a number rather than a look because a
    /// two-block advantage on one side is invisible in a picture and decisive in a match.</summary>
    public static (int WorstDelta, int Cells) SymmetryError(HeightField field, string mode, double centreX, double centreZ)
    {
        int worst = 0, counted = 0;
        foreach (var (x, z) in field.Footprint.Land())
        {
            var (mx, mz) = Mirror(x + 0.5, z + 0.5, mode, centreX, centreZ);
            int ix = (int)Math.Floor(mx), iz = (int)Math.Floor(mz);
            if (!field.Has(ix, iz)) continue;
            counted++;
            worst = Math.Max(worst, Math.Abs(field.At(x, z) - field.At(ix, iz)));
        }
        return (worst, counted);
    }

    public static (double X, double Z) Mirror(double x, double z, string mode, double centreX, double centreZ)
        => mode switch
        {
            "rot_180" => (2 * centreX - x, 2 * centreZ - z),
            "mirror_x" => (2 * centreX - x, z),
            "mirror_z" => (x, 2 * centreZ - z),
            _ => (x, z),
        };

    // ── operations on a solved field ──────────────────────────────────────────────────────────────

    /// <summary>How a shape drawn over a relief decides its own top. The distinction is the whole point of
    /// drawing one: a <see cref="Level"/> shape states an absolute height and so cuts through whatever it
    /// crosses; a <see cref="Raise"/> shape stands a fixed amount proud of the ground under it and so keeps
    /// its prominence wherever it is dragged; a <see cref="Sink"/> shape does the same downward.</summary>
    internal enum Erection { Level, Raise, Sink }

    /// <summary>Composites a shape onto a solved field. The result is a new block surface — the field's own
    /// solve is untouched, so the shape can be moved or removed without re-stating the relief.</summary>
    public static HeightField Erect(HeightField field, double[][] ring, Erection mode, int amount)
    {
        var blocks = (int[])field.Blocks.Clone();
        var covered = new List<(int X, int Z)>();
        foreach (var (x, z) in field.Footprint.Land())
            if (PgmStudio.Geom.Polygon.PointInRing(x + 0.5, z + 0.5, ring)) covered.Add((x, z));
        if (covered.Count == 0) return field.WithBlocks(blocks);

        // A raised or sunk shape takes one height for the whole footprint — the ground it stands on read at
        // its median — so a box on a slope is a box and not a warped lid.
        var ground = covered.Select(cell => field.At(cell.X, cell.Z)).Order().ToList();
        var median = ground[ground.Count / 2];
        var top = mode switch
        {
            Erection.Level => amount,
            Erection.Raise => median + amount,
            _ => median - amount,
        };
        foreach (var (x, z) in covered) blocks[field.Footprint.Index(x, z)] = top;
        return field.WithBlocks(blocks);
    }

    /// <summary>The least-climb route between two cells: a shortest path whose cost counts climbing far more
    /// than distance, and which refuses a step no player could take. It is what turns a dragged line into a
    /// road that switches back across a slope instead of running straight up it.</summary>
    public static List<(int X, int Z)> Route(HeightField field, (int X, int Z) from, (int X, int Z) to,
                                             int walkableStep = 1, double climbCost = 12)
    {
        var best = new Dictionary<(int X, int Z), double> { [from] = 0 };
        var cameFrom = new Dictionary<(int X, int Z), (int X, int Z)>();
        var frontier = new PriorityQueue<(int X, int Z), double>();
        frontier.Enqueue(from, 0);
        var settled = new HashSet<(int X, int Z)>();

        while (frontier.TryDequeue(out var cell, out _))
        {
            if (!settled.Add(cell)) continue;
            if (cell == to) break;
            foreach (var (dx, dz) in Neighbours4)
            {
                var next = (cell.X + dx, cell.Z + dz);
                if (!field.Has(next.Item1, next.Item2)) continue;
                var rise = Math.Abs(field.At(next.Item1, next.Item2) - field.At(cell.X, cell.Z));
                if (rise > walkableStep) continue;
                var cost = best[cell] + 1 + climbCost * rise;
                if (best.TryGetValue(next, out var known) && known <= cost) continue;
                best[next] = cost; cameFrom[next] = cell;
                frontier.Enqueue(next, cost);
            }
        }
        if (!cameFrom.ContainsKey(to) && from != to) return [];
        var route = new List<(int X, int Z)> { to };
        while (route[^1] != from) route.Add(cameFrom[route[^1]]);
        route.Reverse();
        return route;
    }

    /// <summary>Raises every closed basin to the level of its lowest outlet, so from any cell there is a path
    /// that only ever goes down to the edge of the land. A solved surface is full of one-block pits — the
    /// grain puts them there — and each one stops a stream dead at the first cell. Filling is a flood inward
    /// from the boundary, always through the lowest cell reached so far: the level a cell is raised to is the
    /// highest sill on the cheapest way out, which is exactly the water line a basin would hold.
    ///
    /// <para>The filled surface is the one <em>water is routed on</em>, not the one that is built — a pit an
    /// author dug is theirs to keep — so it is returned separately.</para></summary>
    public static HeightField FillDepressions(HeightField field)
    {
        var filled = new int[field.Blocks.Length];
        Array.Fill(filled, int.MaxValue);
        var frontier = new PriorityQueue<(int X, int Z), int>();
        foreach (var (x, z) in field.Footprint.Land())
        {
            var onEdge = Neighbours4.Any(step => !field.Has(x + step.X, z + step.Z));
            if (!onEdge) continue;
            var index = field.Footprint.Index(x, z);
            filled[index] = field.At(x, z);
            frontier.Enqueue((x, z), filled[index]);
        }

        var settled = new HashSet<(int X, int Z)>();
        while (frontier.TryDequeue(out var cell, out var level))
        {
            if (!settled.Add(cell)) continue;
            foreach (var (dx, dz) in Neighbours4)
            {
                var next = (cell.X + dx, cell.Z + dz);
                if (!field.Has(next.Item1, next.Item2) || settled.Contains(next)) continue;
                var index = field.Footprint.Index(next.Item1, next.Item2);
                var raised = Math.Max(field.At(next.Item1, next.Item2), level);
                if (raised >= filled[index]) continue;
                filled[index] = raised;
                frontier.Enqueue(next, raised);
            }
        }
        for (var index = 0; index < filled.Length; index++)
            if (filled[index] == int.MaxValue) filled[index] = field.Blocks[index];
        return field.WithBlocks(filled);
    }

    /// <summary>Cuts a walkable way up every riser the quantizer left: wherever a step is too tall to climb,
    /// a stair is carved through it at the narrowest place, so a terraced surface stays one connected piece
    /// of ground. It is the repair that makes a coarse step usable rather than merely handsome — without it
    /// a two-block terracing walls the map into a dozen islands.</summary>
    public static HeightField Stair(HeightField field, int walkableStep = 1)
    {
        var blocks = (int[])field.Blocks.Clone();
        var working = field.WithBlocks(blocks);
        for (var attempt = 0; attempt < 64; attempt++)
        {
            var regions = WalkableRegions(working, walkableStep);
            if (regions.Count <= 1) break;

            // The cheapest join: the pair of cells across a region boundary with the smallest rise, cut by
            // laying one intermediate tread between them.
            var main = regions[0];
            (int X, int Z)? bestLow = null, bestHigh = null;
            var bestRise = int.MaxValue;
            foreach (var cell in main)
                foreach (var (dx, dz) in Neighbours4)
                {
                    var next = (cell.X + dx, cell.Z + dz);
                    if (!working.Has(next.Item1, next.Item2) || main.Contains(next)) continue;
                    var rise = Math.Abs(working.At(next.Item1, next.Item2) - working.At(cell.X, cell.Z));
                    if (rise >= bestRise) continue;
                    bestRise = rise; bestLow = cell; bestHigh = next;
                }
            if (bestLow is null || bestHigh is null) break;

            // The tread is written onto the higher cell and widened a little across the face, so the way up
            // is a stair a player can actually stand on rather than a one-block notch.
            var low = working.At(bestLow.Value.X, bestLow.Value.Z);
            var high = working.At(bestHigh.Value.X, bestHigh.Value.Z);
            var tread = low + Math.Sign(high - low) * walkableStep;
            for (var spread = -1; spread <= 1; spread++)
            {
                var alongX = bestHigh.Value.X == bestLow.Value.X;
                var cell = alongX ? (bestHigh.Value.X + spread, bestHigh.Value.Z) : (bestHigh.Value.X, bestHigh.Value.Z + spread);
                if (!working.Has(cell.Item1, cell.Item2)) continue;
                var index = working.Footprint.Index(cell.Item1, cell.Item2);
                if (Math.Abs(blocks[index] - high) <= walkableStep) blocks[index] = tread;
            }
            working = field.WithBlocks(blocks);
        }
        return working;
    }

    /// <summary>The route water takes: from a source, always to the lowest neighbour, stopping when nothing
    /// nearby is lower. Where it stops is a basin, which is where a pond belongs.</summary>
    public static List<(int X, int Z)> Descend(HeightField field, (int X, int Z) source, int maximumLength = 4000)
    {
        var route = new List<(int X, int Z)> { source };
        var seen = new HashSet<(int X, int Z)> { source };
        while (route.Count < maximumLength)
        {
            var (x, z) = route[^1];
            (int X, int Z)? lowest = null;
            var lowestHeight = field.At(x, z);
            for (var dx = -1; dx <= 1; dx++)
                for (var dz = -1; dz <= 1; dz++)
                {
                    if (dx == 0 && dz == 0) continue;
                    var next = (x + dx, z + dz);
                    if (!field.Has(next.Item1, next.Item2) || seen.Contains(next)) continue;
                    if (field.At(next.Item1, next.Item2) < lowestHeight)
                    { lowestHeight = field.At(next.Item1, next.Item2); lowest = next; }
                }
            if (lowest is null) break;
            route.Add(lowest.Value); seen.Add(lowest.Value);
        }
        return route;
    }

    /// <summary>The whole way water goes: the shortest run from a source to the lowest edge of the land that
    /// never once climbs. Reading it off the depression-filled surface is what makes it always exist — on the
    /// raw surface the first grain-made pit ends the stream, which is what a plain steepest descent does.</summary>
    public static List<(int X, int Z)> Flow(HeightField raw, HeightField filled, (int X, int Z) source)
    {
        var best = new Dictionary<(int X, int Z), double> { [source] = 0 };
        var cameFrom = new Dictionary<(int X, int Z), (int X, int Z)>();
        var frontier = new PriorityQueue<(int X, int Z), double>();
        frontier.Enqueue(source, 0);
        var settled = new HashSet<(int X, int Z)>();
        var outlets = new List<(int X, int Z)>();

        while (frontier.TryDequeue(out var cell, out _))
        {
            if (!settled.Add(cell)) continue;
            // Water stops in two places and no others: at the edge of the land, and in a basin. A basin
            // announces itself as a cell the fill had to raise — the surface there is below its own way out.
            var atEdge = Neighbours4.Any(step => !raw.Has(cell.X + step.X, cell.Z + step.Z));
            var inBasin = filled.At(cell.X, cell.Z) > raw.At(cell.X, cell.Z);
            if (atEdge || inBasin) outlets.Add(cell);
            foreach (var (dx, dz) in Neighbours4)
            {
                var next = (cell.X + dx, cell.Z + dz);
                if (!filled.Has(next.Item1, next.Item2)) continue;
                if (filled.At(next.Item1, next.Item2) > filled.At(cell.X, cell.Z)) continue;   // water never climbs
                // Crossing a level stretch is allowed but paid for, so the run drops where it can rather
                // than wandering across the flat a filled basin leaves behind.
                var cost = best[cell] + (filled.At(next.Item1, next.Item2) == filled.At(cell.X, cell.Z) ? 2.5 : 1.0);
                if (best.TryGetValue(next, out var known) && known <= cost) continue;
                best[next] = cost; cameFrom[next] = cell;
                frontier.Enqueue(next, cost);
            }
        }
        if (outlets.Count == 0) return [source];
        var mouth = outlets.OrderBy(cell => raw.At(cell.X, cell.Z)).ThenBy(cell => best[cell]).First();
        var route = new List<(int X, int Z)> { mouth };
        while (route[^1] != source) route.Add(cameFrom[route[^1]]);
        route.Reverse();
        return route;
    }

    /// <summary>Cuts and fills a corridor around a route so its surface never steps more than the given
    /// amount along its length — the difference between a drawn road and a walkable one. The corridor's
    /// shoulders are blended back into the field over a couple of blocks so the graded strip does not sit in
    /// the landscape as a trench.</summary>
    public static HeightField Grade(HeightField field, List<(int X, int Z)> route, double radius = 2.5, int maxStep = 1)
    {
        if (route.Count == 0) return field;
        var blocks = (int[])field.Blocks.Clone();

        // The route's own profile is smoothed and then limited, so the graded line follows the land's trend
        // rather than every one-block wobble it happens to cross.
        var profile = route.Select(cell => (double)field.At(cell.X, cell.Z)).ToArray();
        for (var pass = 0; pass < 12; pass++)
            for (var i = 1; i + 1 < profile.Length; i++) profile[i] = (profile[i - 1] + profile[i] + profile[i + 1]) / 3;
        var graded = profile.Select(value => (int)Math.Round(value)).ToArray();
        for (var i = 1; i < graded.Length; i++)
            graded[i] = Math.Clamp(graded[i], graded[i - 1] - maxStep, graded[i - 1] + maxStep);
        for (var i = graded.Length - 2; i >= 0; i--)
            graded[i] = Math.Clamp(graded[i], graded[i + 1] - maxStep, graded[i + 1] + maxStep);

        var points = route.Select(cell => new[] { (double)cell.X + 0.5, cell.Z + 0.5 }).ToArray();
        foreach (var (x, z) in field.Footprint.Land())
        {
            var (distance, along, _, _) = Polyline.Nearest(x + 0.5, z + 0.5, points);
            if (distance > radius + 2.5) continue;
            var at = Math.Clamp((int)Math.Round(along * (graded.Length - 1)), 0, graded.Length - 1);
            var index = field.Footprint.Index(x, z);
            if (distance <= radius) { blocks[index] = graded[at]; continue; }
            var blend = 1 - (distance - radius) / 2.5;
            blocks[index] = (int)Math.Round(blocks[index] * (1 - blend) + graded[at] * blend);
        }
        return field.WithBlocks(blocks);
    }

    /// <summary>A channel cut into the field: a shallow U dug along a route, its floor forced to fall the
    /// whole way so water never has to climb, and the surface it fills to held flat between the steps —
    /// so a run down a hillside reads as a chain of pools rather than one impossible tilted sheet.</summary>
    internal sealed record Channel(HeightField Bed, Dictionary<(int X, int Z), int> Water);

    /// <param name="level">A water line to hold for the whole run instead of letting it fall. A channel that
    /// falls cannot be symmetric — a half-turn reverses the direction of flow, so the same water would have
    /// to run both ways at once — and on a map's mirror axis that is exactly what is being asked of it. Held
    /// to one level it becomes a canal, whose bed is the same shape read from either end, and a canal is what
    /// a river down the middle of a competitive map has always had to be.</param>
    public static Channel Carve(HeightField field, List<(int X, int Z)> route, double radius = 3, int depth = 2,
                                int? level = null)
    {
        var blocks = (int[])field.Blocks.Clone();
        var water = new Dictionary<(int X, int Z), int>();
        if (route.Count == 0) return new Channel(field.WithBlocks(blocks), water);

        int[] bed, surface;
        if (level is { } line)
        {
            bed = route.Select(_ => line - depth).ToArray();
            surface = route.Select(_ => line).ToArray();
        }
        else
        {
            // The bed's own profile: the land under the route, dropped by the channel depth, then forced
            // non-increasing downstream. That single constraint is what makes water behave.
            bed = route.Select(cell => field.At(cell.X, cell.Z) - depth).ToArray();
            for (var i = 1; i < bed.Length; i++) bed[i] = Math.Min(bed[i], bed[i - 1]);

            // The water line steps down only where the bed does, and holds level in between: each level
            // stretch is one pool, filled to its own bed plus the depth it was cut to.
            surface = new int[bed.Length];
            for (var i = 0; i < bed.Length; i++) surface[i] = bed[i] + depth;
            for (var i = 1; i < surface.Length; i++) surface[i] = Math.Min(surface[i], surface[i - 1]);
            for (var i = surface.Length - 2; i >= 0; i--)
                if (bed[i] == bed[i + 1]) surface[i] = Math.Min(surface[i], surface[i + 1]);
        }

        var points = route.Select(cell => new[] { (double)cell.X + 0.5, cell.Z + 0.5 }).ToArray();
        foreach (var (x, z) in field.Footprint.Land())
        {
            var (distance, along, _, _) = Polyline.Nearest(x + 0.5, z + 0.5, points);
            if (distance > radius + 2) continue;
            var at = Math.Clamp((int)Math.Round(along * (bed.Length - 1)), 0, bed.Length - 1);
            var index = field.Footprint.Index(x, z);

            if (distance <= radius)
            {
                // Deepest on the centerline, rising to the rim at the band's edge — a bowl, not a trench.
                var lift = (int)Math.Round(depth * (distance / radius) * (distance / radius));
                var floor = Math.Min(blocks[index], bed[at] + lift);
                blocks[index] = floor;
                if (floor < surface[at]) water[(x, z)] = surface[at];
            }
            else
            {
                // The bank: anything standing above the water line beside the channel is cut back to it, so
                // the channel stays open rather than running through a slot.
                var blend = 1 - (distance - radius) / 2.0;
                var shore = (int)Math.Round(blocks[index] * (1 - blend) + (surface[at] + 1) * blend);
                blocks[index] = Math.Min(blocks[index], Math.Max(shore, surface[at]));
            }
        }
        return new Channel(field.WithBlocks(blocks), water);
    }

    // ── reporting ─────────────────────────────────────────────────────────────────────────────────

    /// <summary>The readback: everything a caller that cannot see the picture needs in order to judge the
    /// surface and decide what to change. It is the half of the design that makes a relief drivable by
    /// something other than a hand.</summary>
    public static string Report(HeightField field, string name, string? mirrorMode = null,
                                double centreX = 0, double centreZ = 0)
    {
        var cells = field.Footprint.Land().ToList();
        var lines = new List<string> { $"{name}: {cells.Count} cells, y {field.Min}..{field.Max} (relief {field.Max - field.Min})" };

        var steps = cells.GroupBy(cell => MaxStep(field, cell.X, cell.Z))
                         .ToDictionary(group => group.Key, group => group.Count());
        lines.Add($"  steps: walk (0-1) {steps.Where(e => e.Key <= 1).Sum(e => e.Value) * 100.0 / cells.Count:0.0}%, " +
                  $"scramble (2) {steps.GetValueOrDefault(2)}, " +
                  $"barrier (3+) {steps.Where(e => e.Key >= 3).Sum(e => e.Value)}");

        // Regions are counted at two scales because the raw count is dominated by one-cell shelves on top of
        // cliffs and in the grain — real, and not what "the map is in pieces" means. A region holding a
        // hundredth of the ground is a place; the rest are ledges.
        string Places(List<HashSet<(int X, int Z)>> regions)
        {
            var places = regions.Count(region => region.Count * 100 >= cells.Count);
            return $"{places} place(s) of 1% or more, largest {regions[0].Count * 100.0 / cells.Count:0.0}% " +
                   $"({regions.Count - places} ledge(s) besides)";
        }
        lines.Add($"  on foot: {Places(Reachable(field, Passage.Walk))}");
        lines.Add($"  placing a block: {Places(Reachable(field, Passage.Scramble))}");

        var scarps = Scarps(field);
        var cliffs = scarps.Count(scarp => scarp.IsCliff);
        if (scarps.Count > 0)
            lines.Add($"  scarps: {scarps.Count} steep faces, {cliffs} qualify as cliffs " +
                      $"(widest {scarps[0].Width} wide, drop {scarps[0].Drop})");

        if (mirrorMode is not null)
        {
            var (worst, counted) = SymmetryError(field, mirrorMode, centreX, centreZ);
            lines.Add($"  symmetry: worst mirrored difference {worst} block(s) over {counted} paired cells");
        }
        return string.Join("\n", lines);
    }
}
