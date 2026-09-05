using PgmStudio.Geom.Algorithms;

namespace PgmStudio.Geom.Relief;

/// <summary>A solved height field over a footprint: the continuous surface and the block surface it snaps
/// to. Both are kept, because a drag resumes from the continuous one and the world is built from the
/// blocks.</summary>
public sealed class HeightField(Footprint footprint, double[] continuous, int[] blocks)
{
    public Footprint Footprint { get; } = footprint;
    public double[] Continuous { get; } = continuous;
    public int[] Blocks { get; } = blocks;

    public bool Has(int x, int z) => Footprint.Inside(x, z);
    public int At(int x, int z) => Has(x, z) ? Blocks[Footprint.Index(x, z)] : int.MinValue;

    public int Min => Footprint.Land().Select(cell => At(cell.X, cell.Z)).DefaultIfEmpty(0).Min();
    public int Max => Footprint.Land().Select(cell => At(cell.X, cell.Z)).DefaultIfEmpty(0).Max();

    /// <summary>A copy with the block surface replaced — how a composited shape or a carved bed is layered on
    /// without losing the field it was solved from.</summary>
    public HeightField WithBlocks(int[] blocks) => new(Footprint, Continuous, blocks);
}

/// <summary>
/// Solves a relief into a height field: the smoothest surface that satisfies the marks, then the pushes, the
/// grain, the symmetry fold and the block step, in that order.
///
/// <para>The fill is a screened-Poisson relaxation over the footprint, and it is chosen over weighting for a
/// property weighting cannot offer: <b>its extremes can only sit where a mark put one</b>. No bump appears
/// that nobody asked for, and no ridge sags between two marks that were both high — what the author states is
/// what the surface does, everywhere, and the fill is the least-eventful thing that satisfies it.</para>
/// </summary>
public static class ReliefSolver
{
    private static readonly (int X, int Z)[] Neighbours4 = [(1, 0), (-1, 0), (0, 1), (0, -1)];
    private static readonly (int X, int Z)[] Neighbours8 =
        [(1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1)];

    /// <summary>The sweep budget a solve gets when the caller states none — high enough that the relaxation
    /// settles on every footprint a map is built at, since it stops of its own accord once the field stops
    /// moving and an unused budget costs nothing.</summary>
    public const int DefaultSweeps = 1200;

    /// <summary>Solves a relief. Passing the previous solve's continuous surface as
    /// <paramref name="warmStart"/> resumes from it instead of from flat, which is what makes a drag
    /// affordable: moving one mark perturbs the field locally, so the relaxation has only that perturbation
    /// left to carry rather than the whole surface to build. A resume that fails to settle inside
    /// <paramref name="sweeps"/> is discarded and the cold path runs, so the answer does not depend on
    /// whether one was offered.</summary>
    public static HeightField Solve(Footprint footprint, ReliefSpec spec,
                                    double[]? warmStart = null, int sweeps = DefaultSweeps, bool cascade = true)
    {
        var pinned = new double[footprint.Cells];
        var isPinned = new bool[pinned.Length];
        var isRigid = new bool[pinned.Length];
        // Marks resolve in order and a full-weight pin replaces what an earlier one said, which is what lets a
        // bench be drawn over a slope and flatten it. A pin of less than full weight — a mark's outer shoulder
        // — grades that far toward its height from what is already there instead, so two bands that touch meet
        // on a ramp rather than on a step, and neither mark has to know the other exists.
        //
        // A shoulder over ground nobody has claimed states nothing at all and is left to the relaxation, which
        // is the only answer that keeps the ramp going: pinning it at the mark's own height would stop the
        // grade dead at whichever cell the earlier mark's band happened to end on.
        foreach (var mark in spec.Marks)
            foreach (var (cell, height, weight) in mark.Pins(footprint))
            {
                if (!footprint.Inside(cell.X, cell.Z)) continue;
                var index = footprint.Index(cell.X, cell.Z);
                if (weight < 1 && !isPinned[index]) continue;
                pinned[index] = weight >= 1
                    ? height
                    : pinned[index] + (height - pinned[index]) * Math.Clamp(weight, 0, 1);
                isPinned[index] = true; isRigid[index] = mark.Rigid;
            }

        var field = Diffuse(footprint, pinned, isPinned, spec, warmStart, sweeps, cascade);

        // Pushes go on before the grain and before the fold, so a lifted spur gets the same wobble and the
        // same symmetry guarantee as the ground it stands on. A rigid cell is the exception both of them
        // make: a push composes over ground, and a floor is not ground — a room lifted a course on one side
        // is a floor standing over a hole, since the world lays one floor course per room and fills the
        // bedrock under it column by column.
        if (spec.Pushes.Count > 0)
        {
            var lift = Sculpt(footprint, spec.Pushes);
            for (var index = 0; index < field.Length; index++)
                if (!isRigid[index]) field[index] += lift[index];
        }

        if (spec.Grain > 0)
            foreach (var (x, z) in footprint.Land())
            {
                var index = footprint.Index(x, z);
                if (isPinned[index]) continue;                       // grain never overrides a statement
                var (sx, sz) = Fold(x, z, spec);
                field[index] += (PatternNoise.Fbm(sx, sz, spec.Seed, spec.GrainScale, 3) - 0.5) * 2 * spec.Grain;
            }

        // Symmetry is imposed on the surface, not merely on the statements that made it. Mirrored marks give
        // a field that agrees with its own image to within the solver's tolerance, and a tolerance is exactly
        // what rounding turns into a whole block: two cells at 8.499 and 8.501 become 8 and 9, and one team
        // owns a step the other does not.
        if (spec.FoldMode is not null)
        {
            var symmetric = (double[])field.Clone();
            foreach (var (x, z) in footprint.Land())
            {
                var (sx, sz) = Fold(x, z, spec);
                if (footprint.Inside(sx, sz)) symmetric[footprint.Index(x, z)] = field[footprint.Index(sx, sz)];
            }
            field = symmetric;
        }

        var blocks = new int[field.Length];
        var step = Math.Max(1, spec.Step);
        var baseBlock = (int)Math.Round(spec.Base);
        foreach (var (x, z) in footprint.Land())
        {
            var index = footprint.Index(x, z);
            blocks[index] = (int)Math.Round((field[index] - spec.Base) / step) * step + baseBlock;
        }
        return new HeightField(footprint, field, blocks);
    }

    /// <summary>The coordinate a cell's grain is drawn from and the cell its solved height is copied from:
    /// itself, or — once a fold is declared — whichever of the cell and its mirror image lies in the canonical
    /// half. Both members of a mirrored pair then read the same lattice point and settle on the same block.
    ///
    /// <para>The reflection is of the cell's <b>centre</b>, not its corner. Taking the corner pairs each cell
    /// with its image's neighbour — a one-cell shear that looks like symmetry and measures as a full block of
    /// unfairness.</para></summary>
    private static (int X, int Z) Fold(int x, int z, ReliefSpec spec)
    {
        if (spec.FoldMode is null) return (x, z);
        double centreX = x + 0.5, centreZ = z + 0.5;
        int mirroredX = (int)Math.Floor(2 * spec.FoldCentreX - centreX);
        int mirroredZ = (int)Math.Floor(2 * spec.FoldCentreZ - centreZ);
        return spec.FoldMode switch
        {
            "mirror_x" => centreX <= spec.FoldCentreX ? (x, z) : (mirroredX, z),
            "mirror_z" => centreZ <= spec.FoldCentreZ ? (x, z) : (x, mirroredZ),
            "rot_90" => Quarter(centreX, centreZ, spec),
            // A half-turn has no single axis, so the canonical half is one side of a line through the centre,
            // broken along it by the other coordinate so the two images never both claim to be canonical.
            _ => centreZ < spec.FoldCentreZ || (centreZ == spec.FoldCentreZ && centreX <= spec.FoldCentreX)
                ? (x, z) : (mirroredX, mirroredZ),
        };
    }

    /// <summary>The canonical cell of a <b>quarter-turn</b> orbit: whichever of its four images lies in the
    /// quadrant running from the <c>+x</c> axis round to — but not including — the <c>+z</c> one.
    ///
    /// <para>A half-turn's two images are separated by a half-plane, which is a quadrant too coarse to tell
    /// four apart, so a cell reaches the canonical region by being <b>turned</b> rather than reflected. The
    /// turn is <see cref="Symmetry.RotatePoint"/>'s, the same one the board is fanned by, applied to the
    /// cell's centre and floored once at the end: turning an already-floored image would land it on a fifth
    /// position rather than on a member of its own orbit.</para></summary>
    private static (int X, int Z) Quarter(double centreX, double centreZ, ReliefSpec spec)
    {
        var (x, z) = (centreX, centreZ);
        for (var turn = 0; turn < 3 && !IsCanonicalQuarter(x - spec.FoldCentreX, z - spec.FoldCentreZ); turn++)
            (x, z) = Symmetry.RotatePoint(x, z, 90, spec.FoldCentreX, spec.FoldCentreZ);
        return ((int)Math.Floor(x), (int)Math.Floor(z));
    }

    /// <summary>Whether an offset from the fold centre lies in the canonical quadrant. The boundaries are
    /// taken one open and one closed so that the four images of a cell land in exactly one of them.</summary>
    private static bool IsCanonicalQuarter(double dx, double dz) => dx > 0 && dz >= 0;

    /// <summary>The block surface with every cell taking its mirror image's value from the canonical half —
    /// the same fold the continuous field gets, applied to a pass that ran after it.</summary>
    private static int[] FoldBlocks(Footprint footprint, int[] blocks, ReliefSpec spec)
    {
        var folded = (int[])blocks.Clone();
        foreach (var (x, z) in footprint.Land())
        {
            var (sx, sz) = Fold(x, z, spec);
            if (footprint.Inside(sx, sz)) folded[footprint.Index(x, z)] = blocks[footprint.Index(sx, sz)];
        }
        return folded;
    }

    private static double[] Diffuse(Footprint footprint, double[] pinned, bool[] isPinned, ReliefSpec spec,
                                    double[]? warmStart, int sweeps, bool cascade)
    {
        var anyPin = isPinned.Any(pin => pin);
        if (!anyPin)
        {
            var flat = new double[pinned.Length];
            Array.Fill(flat, spec.Base);
            return flat;
        }

        // The screening strength that gives a mark the requested reach: the field decays toward the base over
        // a characteristic length of one over the root of this, so reach is stated in blocks.
        var screen = spec.Reach > 0 ? 1.0 / (spec.Reach * spec.Reach) : 0.0;

        // Resuming from the surface already on screen. The relaxation stops when the field stops moving, so a
        // resumed run that settles has reached the SAME surface a cold one would — it simply had less left to
        // do, which is the whole saving. What it must not do is hand back a surface that never settled: an
        // edit big enough to need the long-range conversation the cascade exists for cannot get it here, since
        // information moves one cell per sweep on a single grid. So an unsettled resume is thrown away and the
        // cold path runs, and the preview stays the surface the export builds rather than an approximation of
        // it that happens to be quicker.
        if (warmStart is { } previous && previous.Length == pinned.Length)
        {
            var resumed = (double[])previous.Clone();
            for (var index = 0; index < pinned.Length; index++) if (isPinned[index]) resumed[index] = pinned[index];
            if (Lattice.Of(footprint, pinned, isPinned).Relax(resumed, screen, spec.Base, sweeps)) return resumed;
            // The fallback exists to be correct, so it is not held to the resume's budget. A caller offering a
            // previous surface may cap the attempt at a handful of sweeps precisely because it expects the
            // work to be small; inheriting that cap here would answer an unsettled surface in the one case the
            // fallback was written for.
            sweeps = Math.Max(sweeps, DefaultSweeps);
        }
        if (!cascade)
        {
            var lattice = Lattice.Of(footprint, pinned, isPinned);
            var settled = lattice.Seeded();
            lattice.Relax(settled, screen, spec.Base, sweeps);
            return settled;
        }
        return Cascade(footprint, pinned, isPinned, screen, spec.Base, sweeps);
    }

    /// <summary>
    /// Solves coarse first, then refines. A relaxation moves information one cell per sweep, so the sweeps a
    /// field needs grow with how far across it the marks have to talk — which is why a solve comfortable on a
    /// room is not on a map. Halving the grid halves that distance and quarters the cells, so the long-range
    /// conversation happens on a quarter of the work and the finest level has only detail left.
    ///
    /// <para>A coarse cell is land if any of the four under it is, and pinned if any of them is, so a scarp
    /// two blocks wide cannot vanish at the top of the cascade and reappear as a shock at the bottom.</para>
    /// </summary>
    private static double[] Cascade(Footprint footprint, double[] pinned, bool[] isPinned,
                                    double screen, double baseHeight, int sweeps)
    {
        var levels = new List<Lattice> { Lattice.Of(footprint, pinned, isPinned) };
        while (Math.Max(levels[^1].Width, levels[^1].Depth) > 24 && levels.Count < 5)
            levels.Add(levels[^1].Coarsen());

        double[]? carried = null;
        for (var level = levels.Count - 1; level >= 0; level--)
        {
            var lattice = levels[level];
            var field = carried is null ? lattice.Seeded() : lattice.Adopt(carried, levels[level + 1]);
            // The screening length is stated in blocks, so it is re-expressed per level: a coarse cell is
            // twice as wide, and a reach not rescaled would pull the field to base twice as fast at each step
            // up the cascade.
            var levelScreen = screen * Math.Pow(4, level);
            // A coarse level is cheap and is where the long-range work happens, so it settles; the finest has
            // only local detail left and needs a fraction of the sweeps.
            lattice.Relax(field, levelScreen, baseHeight, level == 0 ? Math.Max(60, sweeps / 6) : sweeps);
            carried = field;
        }
        return carried!;
    }

    /// <summary>
    /// How much each cell is lifted by the pushes. Distance is measured <b>from the ring</b> by a chamfer
    /// sweep over the footprint, not from a centre, which is why a push keeps its shape: a long thin outline
    /// stays long and thin as it fades. The sweep only steps onto land, so a push on one arm of a shape does
    /// not lift the arm across the notch from it.
    /// </summary>
    private static double[] Sculpt(Footprint footprint, IReadOnlyList<PushMark> pushes)
    {
        var lift = new double[footprint.Cells];
        foreach (var push in pushes)
        {
            if (push.Ring.Length < 3) continue;

            var outward = new double[lift.Length];
            Array.Fill(outward, double.PositiveInfinity);
            var seeded = false;
            foreach (var (x, z) in footprint.Land())
                if (Polygon.PointInRing(x + 0.5, z + 0.5, push.Ring))
                { outward[footprint.Index(x, z)] = 0; seeded = true; }
            if (!seeded) continue;
            Chamfer(footprint, outward);

            // The same sweep run the other way: seeded on everything the ring does not cover, so an interior
            // cell holds its distance in from the outline. Its maximum is the deepest point of the shape, and
            // the set of local maxima is the medial axis — the point a round push crowns toward and the line
            // a long one does, neither of them authored.
            double[]? inward = null;
            var deepest = 0.0;
            if (push.Crown != 0)
            {
                inward = new double[lift.Length];
                Array.Fill(inward, double.PositiveInfinity);
                foreach (var (x, z) in footprint.Land())
                    if (!Polygon.PointInRing(x + 0.5, z + 0.5, push.Ring)) inward[footprint.Index(x, z)] = 0;
                Chamfer(footprint, inward);
                foreach (var (x, z) in footprint.Land())
                {
                    var value = inward[footprint.Index(x, z)];
                    if (!double.IsPositiveInfinity(value)) deepest = Math.Max(deepest, value);
                }
            }

            // A closed copy, so the arc the per-vertex amounts read runs the whole way round and meets itself
            // rather than stopping at the last drawn vertex.
            var closed = push.Ring.Append(push.Ring[0]).ToArray();
            var falloff = Math.Max(0.5, push.Falloff);

            foreach (var (x, z) in footprint.Land())
            {
                var index = footprint.Index(x, z);
                var away = outward[index];
                if (double.IsPositiveInfinity(away)) continue;
                // Only the distance is wobbled, never the amount, so the push still reaches its full height
                // inside its own ring.
                if (push.Roughness > 0)
                    away = Math.Max(0, away + (PatternNoise.Fbm(x, z, push.Seed, 9, 3) - 0.5) * 2 * push.Roughness);
                if (away > falloff) continue;

                var amount = push.Amounts is null
                    ? push.Amount
                    : push.AmountAt(Polyline.Nearest(x + 0.5, z + 0.5, closed).Along);
                if (inward is not null && deepest > 0)
                {
                    var depth = inward[index];
                    if (!double.IsPositiveInfinity(depth) && depth > 0)
                        amount += push.Crown * PushMark.Ease(1 - depth / deepest);
                }
                lift[index] += amount * PushMark.Ease(away / falloff);
            }
        }
        return lift;
    }

    /// <summary>Sweeps a seeded distance field out across the footprint, forward then backward until it
    /// settles. Only land is stepped onto, so every distance this produces is one a player could walk.</summary>
    private static void Chamfer(Footprint footprint, double[] distance)
    {
        for (var sweep = 0; sweep < 6; sweep++)
        {
            var moved = false;
            for (var pass = 0; pass < 2; pass++)
                for (var i = 0; i < distance.Length; i++)
                {
                    var index = pass == 0 ? i : distance.Length - 1 - i;
                    if (!footprint.InsideAt(index)) continue;
                    var (x, z) = footprint.CellAt(index);
                    var best = distance[index];
                    foreach (var (dx, dz) in Neighbours8)
                    {
                        if (!footprint.Inside(x + dx, z + dz)) continue;
                        var step = dx != 0 && dz != 0 ? 1.41421356 : 1.0;
                        var candidate = distance[footprint.Index(x + dx, z + dz)] + step;
                        if (candidate < best) best = candidate;
                    }
                    if (best < distance[index]) { distance[index] = best; moved = true; }
                }
            if (!moved) break;
        }
    }

    /// <summary>One grid the relaxation runs on, held purely as topology: which cells are land, which are
    /// pinned and to what. No world coordinates, because a coarse level of the cascade has none that mean
    /// anything — and the relaxation never needed them, only the neighbours.</summary>
    private sealed class Lattice(int width, int depth, bool[] inside, double[] pinned, bool[] isPinned)
    {
        public readonly int Width = width, Depth = depth;
        private readonly bool[] _inside = inside;
        private readonly double[] _pinned = pinned;
        private readonly bool[] _isPinned = isPinned;

        public static Lattice Of(Footprint footprint, double[] pinned, bool[] isPinned)
        {
            var inside = new bool[footprint.Cells];
            for (var index = 0; index < inside.Length; index++) inside[index] = footprint.InsideAt(index);
            return new Lattice(footprint.Width, footprint.Depth, inside, pinned, isPinned);
        }

        /// <summary>The next level up: each cell covers a two-by-two block of this one, land if any of them is
        /// and pinned if any of them is, at the mean of the pins it swallowed.</summary>
        public Lattice Coarsen()
        {
            int width = (Width + 1) / 2, depth = (Depth + 1) / 2;
            var inside = new bool[width * depth];
            var pinned = new double[inside.Length];
            var isPinned = new bool[inside.Length];
            var counted = new int[inside.Length];

            for (var z = 0; z < Depth; z++)
                for (var x = 0; x < Width; x++)
                {
                    var source = z * Width + x;
                    if (!_inside[source]) continue;
                    var target = z / 2 * width + x / 2;
                    inside[target] = true;
                    if (!_isPinned[source]) continue;
                    isPinned[target] = true;
                    pinned[target] += _pinned[source];
                    counted[target]++;
                }
            for (var index = 0; index < pinned.Length; index++)
                if (counted[index] > 0) pinned[index] /= counted[index];
            return new Lattice(width, depth, inside, pinned, isPinned);
        }

        /// <summary>A starting surface with nothing carried in: every free cell at the mean of the pins.</summary>
        public double[] Seeded()
        {
            var field = new double[_inside.Length];
            double total = 0; var counted = 0;
            for (var index = 0; index < _inside.Length; index++)
                if (_isPinned[index]) { total += _pinned[index]; counted++; }
            Array.Fill(field, counted > 0 ? total / counted : 0);
            for (var index = 0; index < _inside.Length; index++)
                if (_isPinned[index]) field[index] = _pinned[index];
            return field;
        }

        /// <summary>This level's starting surface, read off the level above by taking each cell's parent.</summary>
        public double[] Adopt(double[] coarse, Lattice above)
        {
            var field = new double[_inside.Length];
            for (var z = 0; z < Depth; z++)
                for (var x = 0; x < Width; x++)
                {
                    var parent = Math.Min(above.Depth - 1, z / 2) * above.Width + Math.Min(above.Width - 1, x / 2);
                    field[z * Width + x] = coarse[parent];
                }
            for (var index = 0; index < _inside.Length; index++)
                if (_isPinned[index]) field[index] = _pinned[index];
            return field;
        }

        /// <summary>Relaxes until the field stops moving, or until the sweep budget runs out. Returns whether
        /// it <b>settled</b> — which is the difference between a surface and an unfinished one, and the only
        /// thing that makes resuming from a previous solve safe: a resumed run that reaches the same tolerance
        /// has reached the same surface, where one merely cut short has not.</summary>
        public bool Relax(double[] field, double screen, double baseHeight, int sweeps)
        {
            const double OverRelaxation = 1.85;
            var settled = false;
            for (var sweep = 0; sweep < sweeps; sweep++)
            {
                var movement = 0.0;
                // Red-black ordering, so the sweep is order-independent and converges evenly from both sides.
                for (var parity = 0; parity < 2; parity++)
                    for (var z = 0; z < Depth; z++)
                        for (var x = 0; x < Width; x++)
                        {
                            if (((x + z) & 1) != parity) continue;
                            var index = z * Width + x;
                            if (!_inside[index] || _isPinned[index]) continue;

                            double sum = 0; var count = 0;
                            if (x > 0 && _inside[index - 1]) { sum += field[index - 1]; count++; }
                            if (x + 1 < Width && _inside[index + 1]) { sum += field[index + 1]; count++; }
                            if (z > 0 && _inside[index - Width]) { sum += field[index - Width]; count++; }
                            if (z + 1 < Depth && _inside[index + Width]) { sum += field[index + Width]; count++; }
                            if (count == 0) continue;

                            var target = (sum + screen * baseHeight) / (count + screen);
                            var moved = OverRelaxation * (target - field[index]);
                            field[index] += moved;
                            movement = Math.Max(movement, Math.Abs(moved));
                        }
                if (movement < Settled) { settled = true; break; }
            }
            for (var index = 0; index < field.Length; index++)
                if (_isPinned[index]) field[index] = _pinned[index];
            return settled;
        }

        /// <summary>How little a sweep has to move the field before it counts as finished. Four orders below
        /// the block the surface is rounded to, so a field that has settled by this measure cannot round to a
        /// different block than one relaxed further.</summary>
        private const double Settled = 1e-4;
    }
}
