using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Compose;

/// <summary>A <b>joint</b> of the partition graph: the shared edge interval where two boxes touch — the
/// <see cref="BoxInterface"/> (an edge, an offset, a width) read on <see cref="BoxA"/>'s frame, plus the box
/// on the other side. The graph edge the docking gate and the repair search reason over.</summary>
public sealed record BoxJoint(string BoxA, string BoxB, BoxInterface Interface);

/// <summary>
/// The <b>constraint graph</b> a partition is (G63): typed <see cref="Box"/>es (each an allocated
/// <see cref="Box.Rect"/> footprint + its <see cref="Box.LandTargetCells"/> land half of the two-currency
/// budget) and the <see cref="BoxJoint"/>s between them. This is what sampling produces once composition is
/// partition-first — boxes allocated, then filled — replacing the imperative sample-then-place shape record.
///
/// <para><b>Boxes may overlap</b>: the partition allocates budgets and constraints, not exclusive area (the
/// real invariant is piece-disjointness + image clearance, enforced downstream). A joint is only emitted where
/// two box footprints <em>abut</em> along a genuine edge interval; interpenetrating footprints simply carry no
/// joint. <see cref="Of"/> is the <b>derive-side mirror</b>: it reads the partition implied by a grown unit
/// (the emit side the allocator will produce), so an allocator's partition round-trips through it.</para>
/// </summary>
public sealed record BoxPartition(IReadOnlyList<Box> Boxes, IReadOnlyList<BoxJoint> Joints)
{
    /// <summary>The box with <paramref name="id"/>, or null.</summary>
    public Box? ById(string id) => Boxes.FirstOrDefault(b => b.Id == id);

    /// <summary>Every hard invariant of a well-formed partition: non-degenerate boxes, unique ids, the land
    /// currency never exceeds a box's footprint, and every joint references two distinct real boxes along an
    /// edge interval the two footprints actually share. Overlap between footprints is <b>not</b> a violation —
    /// only a joint asserting a contact that is not there is.</summary>
    public bool Valid()
    {
        if (Boxes.Any(b => b.Rect[2] <= 0 || b.Rect[3] <= 0)) return false;
        if (Boxes.Select(b => b.Id).Distinct().Count() != Boxes.Count) return false;
        if (Boxes.Any(b => b.LandTargetCells < 0 || b.LandTargetCells > b.Rect[2] * b.Rect[3])) return false;
        foreach (var j in Joints)
        {
            if (j.BoxA == j.BoxB) return false;
            var a = ById(j.BoxA);
            var b = ById(j.BoxB);
            if (a is null || b is null) return false;
            if (SharedEdge(a.Rect, b.Rect) is not { } shared || shared != j.Interface) return false;
        }
        return true;
    }

    /// <summary>The joints incident on the box with <paramref name="id"/>.</summary>
    public IReadOnlyList<BoxJoint> JointsOf(string id) =>
        Joints.Where(j => j.BoxA == id || j.BoxB == id).ToList();

    /// <summary>Read the partition implied by a grown <paramref name="unit"/> — the derive-side mirror of what a
    /// partition-first allocator emits. Labeled approach pieces group by their <see cref="BoxRef"/> into wool
    /// and spawn boxes; the structural pieces the grower authors directly (the hub, the frontline, the third
    /// wool lane) group by id into their plain boxes. Each box's footprint is its pieces' bounding envelope and
    /// its land the cells they cover; joints are the abutments between the footprints.</summary>
    public static BoxPartition Of(GrownUnit unit)
    {
        var order = new List<string>();
        var groups = new Dictionary<string, (BoxKind Kind, List<int[]> Rects)>();
        foreach (var p in unit.Pieces)
        {
            var (id, kind) = p.Box is { } box ? (box.Id, box.Kind) : Structural(p);
            if (!groups.TryGetValue(id, out var g)) { g = (kind, new List<int[]>()); groups[id] = g; order.Add(id); }
            g.Rects.Add(p.Rect);
        }

        var boxes = order.Select(id => new Box(id, groups[id].Kind, Bbox(groups[id].Rects), Land(groups[id].Rects))).ToList();
        var joints = new List<BoxJoint>();
        for (var i = 0; i < boxes.Count; i++)
            for (var j = i + 1; j < boxes.Count; j++)
                if (SharedEdge(boxes[i].Rect, boxes[j].Rect) is { } iface)
                    joints.Add(new BoxJoint(boxes[i].Id, boxes[j].Id, iface));
        return new BoxPartition(boxes, joints);
    }

    /// <summary>The plain box an unlabeled structural piece belongs to, by id: the hub, the frontline (its
    /// chains share one box), the third wool lane. Anything unexpected becomes its own single-piece box so the
    /// mirror stays total rather than throwing.</summary>
    private static (string Id, BoxKind Kind) Structural(GrownPiece p) =>
        p.Id == "hub" ? ("hub", BoxKind.Hub)
        : p.Id.StartsWith("frontline") ? ("frontline", BoxKind.Frontline)
        : p.Id.StartsWith("wool-lane") ? ("wool-c", BoxKind.Wool)
        : (p.Id, BoxKind.Mid);

    private static int[] Bbox(IReadOnlyList<int[]> rects)
    {
        var minX = rects.Min(r => r[0]);
        var minZ = rects.Min(r => r[1]);
        var maxX = rects.Max(r => r[0] + r[2]);
        var maxZ = rects.Max(r => r[1] + r[3]);
        return [minX, minZ, maxX - minX, maxZ - minZ];
    }

    private static int Land(IReadOnlyList<int[]> rects) => rects.Sum(r => r[2] * r[3]);

    /// <summary>The shared edge interval of two abutting rects (cell coordinates), on the first rect's frame,
    /// or null when they do not touch along an edge (a gap, a bare corner, or interpenetration). The interval's
    /// <see cref="BoxInterface.Start"/> is box-local to <paramref name="a"/>'s edge origin.</summary>
    public static BoxInterface? SharedEdge(int[] a, int[] b)
    {
        int ax0 = a[0], az0 = a[1], ax1 = a[0] + a[2], az1 = a[1] + a[3];
        int bx0 = b[0], bz0 = b[1], bx1 = b[0] + b[2], bz1 = b[1] + b[3];
        int zLo = Math.Max(az0, bz0), zHi = Math.Min(az1, bz1);         // vertical-edge overlap (Left/Right)
        int xLo = Math.Max(ax0, bx0), xHi = Math.Min(ax1, bx1);        // horizontal-edge overlap (Top/Bottom)
        if (ax1 == bx0 && zHi > zLo) return new BoxInterface(BoxEdge.Right, zLo - az0, zHi - zLo);
        if (ax0 == bx1 && zHi > zLo) return new BoxInterface(BoxEdge.Left, zLo - az0, zHi - zLo);
        if (az1 == bz0 && xHi > xLo) return new BoxInterface(BoxEdge.Bottom, xLo - ax0, xHi - xLo);
        if (az0 == bz1 && xHi > xLo) return new BoxInterface(BoxEdge.Top, xLo - ax0, xHi - xLo);
        return null;
    }
}
