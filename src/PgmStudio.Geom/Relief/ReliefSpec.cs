namespace PgmStudio.Geom.Relief;

/// <summary>
/// How a shape takes part in the relief of the group it belongs to. The unit a relief is solved over is the
/// <b>group</b> — the fused footprint of every shape on one landmass — because a relief solved per shape
/// leaves a seam wherever two of them meet and disagree about the height they share, and a plan-derived
/// sketch fuses equal-level pieces into exactly such a group.
///
/// <para>But the fusion is not always what an author wants, and the case that decides it is a built thing
/// standing on the ground: a city, a keep, a walled compound. Its floor is not terrain and it is themed as a
/// unit, so a field rolling through it would put a slope under a wall and land its pattern differently on
/// every stretch.</para>
///
/// <para>The distinction is not that one is smooth and the other is not: <b>both are boundaries</b>, and the
/// surrounding field differs under either, because a hole has an outline the relaxation must bend around.
/// What separates them is whether the shape's <b>height</b> reaches the ground around it. <see cref="Hold"/>
/// pins it, so the whole surrounding surface is solved knowing where it has to arrive and moves when that
/// height changes. <see cref="Exclude"/> pins nothing, so the land is the land that outline would have
/// produced whatever height the shape is finally stamped at — a town in a valley against a citadel on a
/// plinth.</para>
///
/// <para>Neither guarantees a flush meeting, and it is worth being plain about why: a flat pad laid across a
/// slope has to pay for its flatness at its edge, whichever way it is bound. <see cref="Hold"/> spreads that
/// payment as far as the relaxation can spread it; it does not remove it.</para>
/// </summary>
public enum Participation { Inherit, Hold, Exclude }

/// <summary>One shape of a group as the relief sees it: the ring it covers, how it takes part, and the
/// height it holds when it is holding one — <b>one for the whole ring, or one per vertex</b>, which is the
/// difference between a level pad and a shelf that falls along its length. <see cref="Bevel"/> is how far
/// inside the ring that height gives way to the ground around it, so a held shape can meet its neighbour on a
/// grade instead of on a step.</summary>
public sealed record ReliefShape(double[][] Ring, Participation Participation = Participation.Inherit,
                                 double[]? HeldHeights = null, double Bevel = 0)
{
    /// <summary>The shape as the mark it becomes — the one reading of its heights, so the solve and the
    /// excluded-shape stamp cannot interpolate the same ring differently.</summary>
    public AreaMark Held() => new(Ring, HeldHeights ?? [0], Bevel);
}

/// <summary>The relief of one footprint: a base level, the marks stated on it, the shapes pushed or pulled
/// out of it, and the finishing that turns a continuous surface into blocks.</summary>
public sealed record ReliefSpec
{
    public double Base { get; init; } = 4;

    /// <summary>Constraints. They resolve in order and the last wins a contested cell, which is what lets a
    /// bench drawn over a slope flatten it — and which bites: a rim written <em>after</em> a ridge cuts a
    /// doorway through both ends of the ridge where the two meet.</summary>
    public IReadOnlyList<Mark> Marks { get; init; } = [];

    /// <summary>Shapes of ground lifted or lowered after the field is solved — the sculpting half, where the
    /// landform's plan is whatever ring was drawn rather than whatever radius was typed.</summary>
    public IReadOnlyList<PushMark> Pushes { get; init; } = [];

    /// <summary>How far a mark's influence travels before the field falls back to <see cref="Base"/>, in
    /// blocks. Zero is unlimited reach — the marks decide the whole surface, which is what a room-sized shape
    /// wants. A finite reach makes each mark a local landform with plain ground between, which is what a team
    /// board wants.</summary>
    public double Reach { get; init; }

    /// <summary>Amplitude of the grain added after interpolation, in blocks — the wobble that stops a solved
    /// surface reading as machined. It never overrides a mark: a stated height is a statement, and a field
    /// that moved it would make every mark advisory.</summary>
    public double Grain { get; init; }

    public int GrainScale { get; init; } = 9;
    public uint Seed { get; init; } = 1;

    /// <summary>The block quantum the finished surface snaps to. One follows the field cell by cell; two is
    /// the step unit the hand-built corpus uses and reads as deliberate terracing — and it is the one knob
    /// that can break a map, because every riser becomes a two-block wall.</summary>
    public int Step { get; init; } = 1;

    /// <summary>The symmetry the grain is sampled through and the solved field folded across, when the relief
    /// covers a whole mirrored map. Mirroring the marks alone is not enough: the relaxation agrees with its
    /// own image only to within its tolerance, and a tolerance is what rounding turns into a whole block.</summary>
    public string? FoldMode { get; init; }

    public double FoldCentreX { get; init; }
    public double FoldCentreZ { get; init; }
}

/// <summary>A group as the relief is asked to solve it: its shapes, and the relief stated over them.</summary>
public sealed record ReliefGroup(IReadOnlyList<ReliefShape> Shapes, ReliefSpec Spec)
{
    /// <summary>The fused footprint every shape contributes to, minus the shapes that take themselves out of
    /// the solve. An excluded shape is a hole, so the relaxation bends around it exactly as it bends around
    /// the void — the same no-flow boundary doing the same job at a smaller scale.</summary>
    public Footprint Footprint()
    {
        var footprint = Relief.Footprint.Of(Shapes.Select(shape => (shape.Ring, false)));
        foreach (var shape in Shapes.Where(shape => shape.Participation == Participation.Exclude))
            footprint.Remove(Relief.Footprint.Rasterize(shape.Ring, footprint.MinX, footprint.MinZ,
                footprint.MinX + footprint.Width, footprint.MinZ + footprint.Depth));
        return footprint;
    }

    /// <summary>The marks the author stated, plus one <see cref="AreaMark"/> per held shape. They go last so
    /// a held shape wins its cells outright: a city floor is a statement about the map, not a suggestion the
    /// terrain averages against.</summary>
    public IReadOnlyList<Mark> Marks()
    {
        var marks = new List<Mark>(Spec.Marks);
        foreach (var shape in Shapes.Where(shape => shape.Participation == Participation.Hold))
            marks.Add(shape.Held());
        return marks;
    }

    /// <summary>The solved surface over the group's own ground, excluded shapes left out of it.</summary>
    public HeightField Solve() => ReliefSolver.Solve(Footprint(), Spec with { Marks = Marks() });

    /// <summary>The <em>built</em> surface: the solve with every excluded shape stamped back at its own
    /// height. They are different objects on purpose — keeping them apart is what lets an excluded compound
    /// be moved or re-themed without re-solving the ground around it.</summary>
    public HeightField Build()
    {
        var solved = Solve();
        var whole = Relief.Footprint.Of(Shapes.Select(shape => (shape.Ring, false)));
        var blocks = new int[whole.Cells];
        var continuous = new double[blocks.Length];
        foreach (var (x, z) in whole.Land())
        {
            var index = whole.Index(x, z);
            blocks[index] = solved.Has(x, z) ? solved.At(x, z) : (int)Math.Round(Spec.Base);
            continuous[index] = blocks[index];
        }
        foreach (var shape in Shapes.Where(shape => shape.Participation == Participation.Exclude))
        {
            // The same surface the solve would have pinned, read through the mark rather than off the scalar
            // it used to be: an excluded shape that tilts is stamped tilted.
            var held = shape.Held();
            foreach (var (x, z) in whole.Land())
                if (Polygon.PointInRing(x + 0.5, z + 0.5, shape.Ring))
                {
                    var index = whole.Index(x, z);
                    var height = held.HeightAt(x + 0.5, z + 0.5);
                    blocks[index] = (int)Math.Round(height);
                    continuous[index] = height;
                }
        }
        return new HeightField(whole, continuous, blocks);
    }
}
