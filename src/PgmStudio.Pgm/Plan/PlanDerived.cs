using PgmStudio.Geom;

namespace PgmStudio.Pgm.Plan;

/// <summary>An axis-aligned block rectangle (min inclusive, max exclusive in extent terms).</summary>
public readonly record struct BlockRect(int MinX, int MinZ, int MaxX, int MaxZ)
{
    public int Width => MaxX - MinX;
    public int Depth => MaxZ - MinZ;
    public double CenterX => (MinX + MaxX) / 2.0;
    public double CenterZ => (MinZ + MaxZ) / 2.0;
}

/// <summary>A piece resolved to block coordinates, with its plateau surface and mirror flag.</summary>
public readonly record struct DerivedPiece(string Id, string Role, BlockRect Rect, int Surface, bool Mirrors);

/// <summary>How two piece rects meet along a shared straight border (or fail to).</summary>
public enum ContactKind { None, Land, Sliver, Corner, Overlap }

/// <summary>A classified pairwise contact between two pieces: the border length (blocks) for edge contacts,
/// and, for <see cref="ContactKind.Land"/>/<see cref="ContactKind.Overlap"/>, the surface delta between them.</summary>
public sealed record Contact(string A, string B, ContactKind Kind, int BorderLength, int SurfaceDelta);

/// <summary>Two pieces linked across a build zone's void, with the hop span (blocks) between their footprints.</summary>
public sealed record GapLink(string A, string B, string Zone, int Hop);

/// <summary>
/// The structure a plan implies but never stores: pieces in block coordinates, the pairwise land/gap
/// connectivity, connected components (islands), the frontline (pieces facing a zone), and the fanned board
/// graph reachability uses. Pure — computed from a <see cref="PlanModel"/>, cached on the instance.
/// </summary>
public sealed class PlanDerived
{
    /// <summary>Minimum corridor width (blocks): a shared border shorter than this is a sliver, not a
    /// connection.</summary>
    public const int CorridorMin = 10;

    public PlanModel Plan { get; }
    public int Cell { get; }
    public string Mode { get; }
    public IReadOnlyList<DerivedPiece> Pieces { get; }
    public IReadOnlyList<Contact> Contacts { get; }
    public IReadOnlyList<Contact> LandInterfaces { get; }
    public IReadOnlyList<GapLink> GapLinks { get; }
    /// <summary>Piece ids that abut or overlap any build zone (the computed frontline).</summary>
    public IReadOnlySet<string> Frontline { get; }
    /// <summary>Connected components over land interfaces and same-surface overlaps — each a set of piece ids,
    /// in first-appearance order.</summary>
    public IReadOnlyList<IReadOnlyList<string>> Components { get; }

    private readonly Dictionary<string, DerivedPiece> _byId;

    private PlanDerived(PlanModel plan)
    {
        Plan = plan;
        Cell = plan.Globals.Cell;
        Mode = plan.Globals.Symmetry;

        Pieces = plan.Pieces.Select(p => new DerivedPiece(
            p.Id, p.Role, ToBlock(p.Rect, Cell), p.Surface ?? plan.Globals.Surface, p.MirrorsOrDefault)).ToList();
        _byId = Pieces.ToDictionary(p => p.Id);

        Contacts = ClassifyContacts(Pieces);
        LandInterfaces = Contacts.Where(c => c.Kind == ContactKind.Land).ToList();
        Frontline = ComputeFrontline(plan, Cell, Pieces);
        GapLinks = ComputeGapLinks(plan, Cell, Pieces);
        Components = ComputeComponents(Pieces, Contacts);
    }

    public static PlanDerived Build(PlanModel plan) => new(plan);

    public DerivedPiece? Piece(string id) => _byId.GetValueOrDefault(id);

    /// <summary>Convert a <c>[x, z, w, h]</c> cell rect to block coordinates.</summary>
    public static BlockRect ToBlock(int[] rect, int cell) =>
        new(rect[0] * cell, rect[1] * cell, (rect[0] + rect[2]) * cell, (rect[1] + rect[3]) * cell);

    // ── contact classification ──────────────────────────────────────────────────────────────────────────

    private static List<Contact> ClassifyContacts(IReadOnlyList<DerivedPiece> pieces)
    {
        var list = new List<Contact>();
        for (var i = 0; i < pieces.Count; i++)
            for (var j = i + 1; j < pieces.Count; j++)
            {
                var c = Classify(pieces[i], pieces[j]);
                if (c.Kind != ContactKind.None) list.Add(c);
            }
        return list;
    }

    /// <summary>Classify how two pieces meet (see <see cref="ContactKind"/>).</summary>
    public static Contact Classify(DerivedPiece a, DerivedPiece b)
    {
        var (ra, rb) = (a.Rect, b.Rect);
        int ix = Math.Min(ra.MaxX, rb.MaxX) - Math.Max(ra.MinX, rb.MinX);   // x overlap (blocks)
        int iz = Math.Min(ra.MaxZ, rb.MaxZ) - Math.Max(ra.MinZ, rb.MinZ);   // z overlap
        int delta = b.Surface - a.Surface;

        if (ix > 0 && iz > 0)       return new Contact(a.Id, b.Id, ContactKind.Overlap, 0, delta);
        if (ix < 0 || iz < 0)       return new Contact(a.Id, b.Id, ContactKind.None, 0, 0);   // disjoint (a gap)
        if (ix == 0 && iz == 0)     return new Contact(a.Id, b.Id, ContactKind.Corner, 0, delta);
        int border = ix == 0 ? iz : ix;                                    // touch along one axis
        var kind = border >= CorridorMin ? ContactKind.Land : ContactKind.Sliver;
        return new Contact(a.Id, b.Id, kind, border, delta);
    }

    // ── frontline + gap links (over build zones) ────────────────────────────────────────────────────────

    private static HashSet<string> ComputeFrontline(PlanModel plan, int cell, IReadOnlyList<DerivedPiece> pieces)
    {
        var front = new HashSet<string>();
        foreach (var z in plan.Zones)
        {
            var zr = ToBlock(z.Rect, cell);
            foreach (var p in pieces)
                if (TouchesOrOverlaps(p.Rect, zr)) front.Add(p.Id);
        }
        return front;
    }

    private static List<GapLink> ComputeGapLinks(PlanModel plan, int cell, IReadOnlyList<DerivedPiece> pieces)
    {
        var links = new List<GapLink>();
        foreach (var z in plan.Zones)
        {
            var zr = ToBlock(z.Rect, cell);
            var touching = pieces.Where(p => TouchesOrOverlaps(p.Rect, zr)).ToList();
            for (var i = 0; i < touching.Count; i++)
                for (var j = i + 1; j < touching.Count; j++)
                {
                    var hop = VoidSpan(touching[i].Rect, touching[j].Rect);
                    links.Add(new GapLink(touching[i].Id, touching[j].Id, z.Id, hop));
                }
        }
        return links;
    }

    // A piece touches a zone if their rects share a border or interior (contact within the zone's extent).
    private static bool TouchesOrOverlaps(BlockRect a, BlockRect b)
    {
        int ix = Math.Min(a.MaxX, b.MaxX) - Math.Max(a.MinX, b.MinX);
        int iz = Math.Min(a.MaxZ, b.MaxZ) - Math.Max(a.MinZ, b.MinZ);
        return ix >= 0 && iz >= 0 && !(ix == 0 && iz == 0);   // edge or area contact, not a bare corner
    }

    // The void span between two disjoint (or abutting) piece rects: the axis gap where they overlap on the
    // other, else the diagonal corner gap.
    private static int VoidSpan(BlockRect a, BlockRect b)
    {
        int gx = Math.Max(0, Math.Max(a.MinX - b.MaxX, b.MinX - a.MaxX));
        int gz = Math.Max(0, Math.Max(a.MinZ - b.MaxZ, b.MinZ - a.MaxZ));
        if (gx == 0) return gz;
        if (gz == 0) return gx;
        return (int)Math.Round(Math.Sqrt((double)gx * gx + (double)gz * gz));
    }

    // ── connected components (land + same-surface overlap) ──────────────────────────────────────────────

    private static List<IReadOnlyList<string>> ComputeComponents(
        IReadOnlyList<DerivedPiece> pieces, IReadOnlyList<Contact> contacts)
    {
        var parent = pieces.ToDictionary(p => p.Id, p => p.Id);
        string Find(string x) { while (parent[x] != x) x = parent[x] = parent[parent[x]]; return x; }
        void Union(string a, string b) { parent[Find(a)] = Find(b); }

        foreach (var c in contacts)
            if (c.Kind == ContactKind.Land || (c.Kind == ContactKind.Overlap && c.SurfaceDelta == 0))
                Union(c.A, c.B);

        var order = new List<string>();
        var groups = new Dictionary<string, List<string>>();
        foreach (var p in pieces)
        {
            var root = Find(p.Id);
            if (!groups.TryGetValue(root, out var g)) { groups[root] = g = []; order.Add(root); }
            g.Add(p.Id);
        }
        return order.Select(r => (IReadOnlyList<string>)groups[r]).ToList();
    }

    // ── fanning (orbit images) ──────────────────────────────────────────────────────────────────────────

    /// <summary>The orbit order of the plan's symmetry mode (team count).</summary>
    public int Order => Symmetry.Order(Mode);

    /// <summary>The k-th orbit image of a point about the centre: image 0 is the identity, images 1..n−1 apply
    /// the mode's concrete orbit axes in turn (so rot_180/mirror get a real identity that
    /// <see cref="Symmetry.Point"/>'s k-agnostic form does not).</summary>
    public (double X, double Z) FanPoint(double x, double z, int k)
    {
        if (k == 0) return (x, z);
        var axes = Symmetry.OrbitAxes(Mode);
        return k - 1 < axes.Length ? Symmetry.Apply(x, z, axes[k - 1], 0, 0) : (x, z);
    }

    /// <summary>The k-th orbit image of a block rect (four corners fanned and re-bounded), rounded to ints.</summary>
    public BlockRect FanRect(BlockRect r, int k)
    {
        (double x, double z)[] c =
        [
            FanPoint(r.MinX, r.MinZ, k), FanPoint(r.MinX, r.MaxZ, k),
            FanPoint(r.MaxX, r.MinZ, k), FanPoint(r.MaxX, r.MaxZ, k),
        ];
        return new BlockRect(
            (int)Math.Round(c.Min(p => p.x)), (int)Math.Round(c.Min(p => p.z)),
            (int)Math.Round(c.Max(p => p.x)), (int)Math.Round(c.Max(p => p.z)));
    }
}
