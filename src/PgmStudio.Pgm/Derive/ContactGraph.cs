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

/// <summary>How two piece rects meet along a shared straight border (or fail to). Any positive shared border
/// connects (a walkable land interface): <see cref="Land"/> is at least a corridor wide, <see cref="Narrow"/>
/// is a narrower seam (still walkable, still connecting — the staircase/ledge idiom). <see cref="Corner"/> is a
/// bare point touch that never connects; <see cref="Overlap"/> is area overlap; <see cref="None"/> is disjoint.</summary>
public enum ContactKind { None, Land, Narrow, Corner, Overlap }

/// <summary>A classified pairwise contact between two pieces: the border length (blocks) for edge contacts,
/// and, for the connecting kinds (<see cref="ContactKind.Land"/>/<see cref="ContactKind.Narrow"/>/
/// <see cref="ContactKind.Overlap"/>), the surface delta between them.</summary>
public sealed record Contact(string A, string B, ContactKind Kind, int BorderLength, int SurfaceDelta);

/// <summary>Two pieces linked across a build zone's void, with the hop span (blocks) between their footprints.
/// <see cref="Zone"/> names the region zone the connecting span rides (a representative when the buildable
/// region spans several merged zones).</summary>
public sealed record GapLink(string A, string B, string Zone, int Hop);

/// <summary>A buildable region: a connected group of build zones treated as one continuous surface a builder
/// can traverse without landing on terrain. <see cref="ZoneIds"/> are the merged zones' ids; <see cref="Rects"/>
/// their block rects; <see cref="Holes"/> the union of their no-build cutouts. Two zones join a region when
/// they overlap or share a border segment of positive length (a bare corner touch does not merge — see
/// <see cref="ContactGraph.RegionsMerge"/>).</summary>
public sealed record BuildRegion(IReadOnlyList<string> ZoneIds, IReadOnlyList<BlockRect> Rects, IReadOnlyList<BlockRect> Holes);

/// <summary>An edge/point contact between two pieces resolved to a block-space segment: the shared border for a
/// land/narrow contact (a line), or the touch point for a corner (a degenerate segment). <see cref="Length"/>
/// is the border length in blocks (0 for a corner). <see cref="WoolRoom"/> flags a terrain↔wool-room land seam
/// (rendered red); <see cref="Wall"/> flags a land interface marked as a pre-built approach wall.</summary>
public sealed record InterfaceSegment(
    string A, string B, ContactKind Kind, int X1, int Z1, int X2, int Z2, int Length,
    bool WoolRoom = false, bool Wall = false);

/// <summary>A piece edge facing a build zone, as a block-space segment (the computed frontline geometry).</summary>
public sealed record FrontlineEdge(string Piece, string Zone, int X1, int Z1, int X2, int Z2);

/// <summary>
/// The structure a plan implies but never stores: pieces in block coordinates, the pairwise land/gap
/// connectivity, connected components (islands), the frontline (pieces facing a zone), and the fanned board
/// graph reachability uses. Pure — computed from a <see cref="PlanModel"/>, cached on the instance.
/// </summary>
public sealed class ContactGraph
{
    /// <summary>Minimum corridor width (blocks): a shared border at least this long is a full-width
    /// <see cref="ContactKind.Land"/> interface; a shorter positive border is a <see cref="ContactKind.Narrow"/>
    /// seam — still a walkable connection, just too thin to fight through (corridor quality, not connectivity,
    /// is judged later on the assembled footprint).</summary>
    public const int CorridorMin = 10;

    /// <summary>A contact whose kind forms a walkable land interface (any positive shared border): full-width
    /// land or a narrow seam. Corner (point) and disjoint contacts do not connect.</summary>
    public static bool IsLandInterface(ContactKind kind) => kind is ContactKind.Land or ContactKind.Narrow;

    public PlanModel Plan { get; }
    public int Cell { get; }
    public string Mode { get; }
    public IReadOnlyList<DerivedPiece> Pieces { get; }
    public IReadOnlyList<Contact> Contacts { get; }
    public IReadOnlyList<Contact> LandInterfaces { get; }
    /// <summary>The land interfaces marked as pre-built approach walls (a <c>walls</c> pair that resolves to a
    /// real land seam). A wall pair that shares no land interface is dropped here and flagged as an error.</summary>
    public IReadOnlyList<Contact> WallInterfaces { get; }
    public IReadOnlyList<GapLink> GapLinks { get; }
    /// <summary>Connected groups of build zones on the authored (team-0) board — each a continuous buildable
    /// surface (zones merged by overlap or shared edge). Gap links derive from these, not single zones, so a
    /// span may be carried across a chain of adjacent zones.</summary>
    public IReadOnlyList<BuildRegion> BuildRegions { get; }
    /// <summary>Piece ids that abut or overlap any build zone (the computed frontline).</summary>
    public IReadOnlySet<string> Frontline { get; }
    /// <summary>Edge/point contacts (land, narrow, corner) resolved to block-space segments — the overlay draws
    /// land and narrow as connectors (narrow slimmer) and corner as a non-connecting warning marker.</summary>
    public IReadOnlyList<InterfaceSegment> InterfaceSegments { get; }
    /// <summary>Per-piece-per-zone frontline edges as block-space segments (the piece sides facing a zone).</summary>
    public IReadOnlyList<FrontlineEdge> FrontlineEdges { get; }
    /// <summary>Connected components over land interfaces (full-width or narrow) and same-surface overlaps — each
    /// a set of piece ids, in first-appearance order.</summary>
    public IReadOnlyList<IReadOnlyList<string>> Components { get; }

    private readonly Dictionary<string, DerivedPiece> _byId;

    private ContactGraph(PlanModel plan)
    {
        Plan = plan;
        Cell = plan.Globals.Cell;
        Mode = plan.Globals.Symmetry;

        // Annotation pieces (buffers) mark empty space — they produce no terrain and take no part in
        // connectivity, so they never enter the derived structure. Filtering here is the single choke point
        // that keeps them out of Contacts/LandInterfaces/Components/Frontline/GapLinks/InterfaceSegments/
        // FrontlineEdges and everything downstream (the fanned graph and the compiler). d.Plan keeps them.
        Pieces = plan.Pieces.Where(p => PlanRoles.IsGenerating(p.Role)).Select(p => new DerivedPiece(
            p.Id, p.Role, ToBlock(p.Rect, Cell), p.Surface ?? plan.Globals.Surface, p.MirrorsOrDefault)).ToList();
        _byId = Pieces.ToDictionary(p => p.Id);

        Contacts = ClassifyContacts(Pieces);
        LandInterfaces = Contacts.Where(c => IsLandInterface(c.Kind)).ToList();
        var wallPairs = plan.Walls.Select(w => (w.A, w.B)).ToList();
        WallInterfaces = LandInterfaces.Where(c => wallPairs.Contains((c.A, c.B)) || wallPairs.Contains((c.B, c.A))).ToList();
        Frontline = ComputeFrontline(plan, Cell, Pieces);
        Components = ComputeComponents(Pieces, Contacts);
        BuildRegions = ComputeBuildRegions(plan, Cell);
        GapLinks = ComputeGapLinks(BuildRegions, Pieces, Components);
        InterfaceSegments = BuildInterfaceSegments(_byId, Contacts, wallPairs);
        FrontlineEdges = ComputeFrontlineEdges(plan, Cell, Pieces);
    }

    public static ContactGraph Build(PlanModel plan) => new(plan);

    /// <summary>The derived piece for an id, or null when no generating piece carries it (a missing id or an
    /// annotation piece such as a buffer, which never enters the derived structure).</summary>
    public DerivedPiece? Piece(string id) => _byId.TryGetValue(id, out var p) ? p : null;

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
        var kind = border >= CorridorMin ? ContactKind.Land : ContactKind.Narrow;   // any positive border connects
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

    // Gap links (the team-0 overlay + the G5 hop lint) derive from buildable REGIONS, not single zones: a
    // player can build through one zone and continue into an overlapping/adjacent one, so a span may be carried
    // across a chain of merged zones. Two pieces gap-link only across DIFFERENT land components (walkably
    // connected pieces need no void crossing) and only when a STRAIGHT nearest-edge span between them lies
    // inside the region's rectilinear area (union of the merged rects, minus holes) and crosses no third
    // piece's interior. The straight-span rule keeps hop distance well-defined and the overlay drawable; the
    // FannedGraph reachability split off of this (FannedGraph.Build) is looser — it connects any two pieces the
    // region touches without a straight-span test, since a player routes through the region interior freely.
    private static List<GapLink> ComputeGapLinks(
        IReadOnlyList<BuildRegion> regions, IReadOnlyList<DerivedPiece> pieces, IReadOnlyList<IReadOnlyList<string>> components)
    {
        var component = new Dictionary<string, int>();
        for (var c = 0; c < components.Count; c++)
            foreach (var id in components[c]) component[id] = c;

        var links = new List<GapLink>();
        foreach (var region in regions)
        {
            var touching = pieces.Where(p => region.Rects.Any(zr => TouchesOrOverlaps(p.Rect, zr))).ToList();
            for (var i = 0; i < touching.Count; i++)
                for (var j = i + 1; j < touching.Count; j++)
                {
                    var a = touching[i];
                    var b = touching[j];
                    if (component[a.Id] == component[b.Id]) continue;
                    var (sx1, sz1, sx2, sz2) = NearestSegment(a.Rect, b.Rect);
                    if (!SpanInsideRegion(sx1, sz1, sx2, sz2, region.Rects, region.Holes)) continue;
                    if (pieces.Any(p => p.Id != a.Id && p.Id != b.Id && SegmentCrossesInterior(sx1, sz1, sx2, sz2, p.Rect))) continue;
                    links.Add(new GapLink(a.Id, b.Id, RegionZoneFor(region, sx1, sz1, sx2, sz2), VoidSpan(a.Rect, b.Rect)));
                }
        }
        return links;
    }

    // The zone id to label a gap link with: the region zone whose rect contains the span's midpoint (the span
    // rides that zone), falling back to the region's first zone.
    private static string RegionZoneFor(BuildRegion region, int x1, int z1, int x2, int z2)
    {
        double mx = (x1 + x2) / 2.0, mz = (z1 + z2) / 2.0;
        for (var i = 0; i < region.Rects.Count; i++)
        {
            var r = region.Rects[i];
            if (mx >= r.MinX && mx <= r.MaxX && mz >= r.MinZ && mz <= r.MaxZ) return region.ZoneIds[i];
        }
        return region.ZoneIds[0];
    }

    // The connecting span lies inside a buildable region when the (closed) region rects together cover the
    // whole straight segment and it enters no no-build hole. Generalizes single-rect containment to a
    // rectilinear region: each rect clips a sub-interval [t] off the segment; their union must cover [0,1]
    // (a convex single rect makes endpoint containment sufficient, but a span that crosses a seam between two
    // merged rects needs the interval cover).
    private static bool SpanInsideRegion(int x1, int z1, int x2, int z2, IReadOnlyList<BlockRect> rects, IReadOnlyList<BlockRect> holes)
    {
        var covered = new List<(double Lo, double Hi)>();
        foreach (var r in rects)
            if (ClipClosed(x1, z1, x2, z2, r, out var t0, out var t1)) covered.Add((t0, t1));
        if (!CoversUnit(covered)) return false;
        return !holes.Any(h => SegmentCrossesInterior(x1, z1, x2, z2, h));
    }

    // Liang–Barsky clip of a segment against a CLOSED rect (boundary counts as inside): the [t0,t1] sub-range
    // of the segment lying in the rect, or false if it misses. A segment riding an edge clips to its overlap.
    private static bool ClipClosed(double x1, double z1, double x2, double z2, BlockRect r, out double t0, out double t1)
    {
        t0 = 0.0; t1 = 1.0;
        double dx = x2 - x1, dz = z2 - z1;
        foreach (var (p, q) in new[] { (-dx, x1 - r.MinX), (dx, r.MaxX - x1), (-dz, z1 - r.MinZ), (dz, r.MaxZ - z1) })
        {
            if (Math.Abs(p) < 1e-12) { if (q < 0) return false; continue; }   // parallel to this edge, outside it
            var t = q / p;
            if (p < 0) { if (t > t1) return false; if (t > t0) t0 = t; }
            else       { if (t < t0) return false; if (t < t1) t1 = t; }
        }
        return t1 >= t0;
    }

    // True when a set of parameter sub-intervals together covers the whole [0,1] segment with no gap.
    private static bool CoversUnit(List<(double Lo, double Hi)> ivals)
    {
        if (ivals.Count == 0) return false;
        ivals.Sort((a, b) => a.Lo.CompareTo(b.Lo));
        const double eps = 1e-9;
        if (ivals[0].Lo > eps) return false;
        double reach = 0.0;
        foreach (var (lo, hi) in ivals)
        {
            if (lo > reach + eps) return false;   // an uncovered gap opens before this interval
            if (hi > reach) reach = hi;
        }
        return reach >= 1 - eps;
    }

    // True when the segment passes through the open interior of a rect (Liang–Barsky clip against the rect
    // deflated by ε, so a span that only rides an edge or clips a corner does not count as crossing it).
    private static bool SegmentCrossesInterior(double x1, double z1, double x2, double z2, BlockRect r)
    {
        const double eps = 1e-6;
        double minX = r.MinX + eps, maxX = r.MaxX - eps, minZ = r.MinZ + eps, maxZ = r.MaxZ - eps;
        if (minX >= maxX || minZ >= maxZ) return false;                    // degenerate (zero-area) rect
        double dx = x2 - x1, dz = z2 - z1;
        double t0 = 0.0, t1 = 1.0;
        foreach (var (p, q) in new[] { (-dx, x1 - minX), (dx, maxX - x1), (-dz, z1 - minZ), (dz, maxZ - z1) })
        {
            if (Math.Abs(p) < 1e-12) { if (q < 0) return false; continue; } // parallel to this edge, outside it
            var t = q / p;
            if (p < 0) { if (t > t1) return false; if (t > t0) t0 = t; }
            else       { if (t < t0) return false; if (t < t1) t1 = t; }
        }
        return t1 - t0 > eps;                                              // a sub-span lies strictly inside
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

    // ── buildable regions (connected groups of build zones) ─────────────────────────────────────────────

    private static List<BuildRegion> ComputeBuildRegions(PlanModel plan, int cell)
    {
        var rects = plan.Zones.Select(z => ToBlock(z.Rect, cell)).ToList();
        var regions = new List<BuildRegion>();
        foreach (var group in MergeGroups(rects))
        {
            var ids = group.Select(i => plan.Zones[i].Id).ToList();
            var groupRects = group.Select(i => rects[i]).ToList();
            var holes = group.SelectMany(i => plan.Zones[i].Holes.Select(h => ToBlock(h, cell))).ToList();
            regions.Add(new BuildRegion(ids, groupRects, holes));
        }
        return regions;
    }

    /// <summary>Group rect indices into connected components where two rects join iff they
    /// <see cref="RegionsMerge"/> (overlap or share a border segment of positive length). Groups list in
    /// first-appearance order; used for both the authored gap-link regions and the fanned reachability board.</summary>
    public static List<List<int>> MergeGroups(IReadOnlyList<BlockRect> rects)
    {
        var parent = Enumerable.Range(0, rects.Count).ToArray();
        int Find(int x) { while (parent[x] != x) x = parent[x] = parent[parent[x]]; return x; }
        void Union(int a, int b) { parent[Find(a)] = Find(b); }

        for (var i = 0; i < rects.Count; i++)
            for (var j = i + 1; j < rects.Count; j++)
                if (RegionsMerge(rects[i], rects[j])) Union(i, j);

        var order = new List<int>();
        var groups = new Dictionary<int, List<int>>();
        for (var i = 0; i < rects.Count; i++)
        {
            var root = Find(i);
            if (!groups.TryGetValue(root, out var g)) { groups[root] = g = []; order.Add(root); }
            g.Add(i);
        }
        return order.Select(r => groups[r]).ToList();
    }

    /// <summary>Two build zones belong to the same buildable region when they overlap or share a border segment
    /// of positive length. A bare corner touch (they meet at a single point) does NOT merge: a lone diagonal
    /// block is not a continuous surface a builder can carry a bridge across, so corner-touching zones stay
    /// distinct regions — the same one-block-diagonal rule that makes a piece corner contact not a connection.</summary>
    public static bool RegionsMerge(BlockRect a, BlockRect b)
    {
        int ix = Math.Min(a.MaxX, b.MaxX) - Math.Max(a.MinX, b.MinX);
        int iz = Math.Min(a.MaxZ, b.MaxZ) - Math.Max(a.MinZ, b.MinZ);
        return ix >= 0 && iz >= 0 && !(ix == 0 && iz == 0);
    }

    // ── overlay geometry (block-space segments the editor draws) ────────────────────────────────────────

    private static List<InterfaceSegment> BuildInterfaceSegments(
        Dictionary<string, DerivedPiece> byId, IReadOnlyList<Contact> contacts, IReadOnlyList<(string A, string B)> wallPairs)
    {
        var list = new List<InterfaceSegment>();
        foreach (var c in contacts)
        {
            if (c.Kind is not (ContactKind.Land or ContactKind.Narrow or ContactKind.Corner)) continue;
            var (x1, z1, x2, z2) = BorderSegment(byId[c.A].Rect, byId[c.B].Rect);
            // Flags apply to land interfaces (full-width or narrow), never a bare corner: terrain↔wool-room
            // (exactly one side is a room) renders red; a marked wall pair renders as a heavy bar — a wall
            // across a narrow step is legal.
            bool woolRoom = IsLandInterface(c.Kind)
                && (byId[c.A].Role == PlanRoles.WoolRoom) != (byId[c.B].Role == PlanRoles.WoolRoom);
            bool wall = IsLandInterface(c.Kind)
                && (wallPairs.Contains((c.A, c.B)) || wallPairs.Contains((c.B, c.A)));
            list.Add(new InterfaceSegment(c.A, c.B, c.Kind, x1, z1, x2, z2, c.BorderLength, woolRoom, wall));
        }
        return list;
    }

    /// <summary>The shared-border segment of two edge/corner-touching rects: a vertical or horizontal line for a
    /// land/narrow contact, or the single touch point (a degenerate segment) for a corner.</summary>
    public static (int X1, int Z1, int X2, int Z2) BorderSegment(BlockRect a, BlockRect b)
    {
        int loX = Math.Max(a.MinX, b.MinX), hiX = Math.Min(a.MaxX, b.MaxX);
        int loZ = Math.Max(a.MinZ, b.MinZ), hiZ = Math.Min(a.MaxZ, b.MaxZ);
        if (hiX == loX && hiZ == loZ) return (loX, loZ, loX, loZ);   // corner touch point
        if (hiX == loX) return (loX, loZ, loX, hiZ);                 // vertical shared border (touch on X)
        return (loX, loZ, hiX, loZ);                                 // horizontal shared border (touch on Z)
    }

    /// <summary>The shortest connecting segment between two rects: the point of each nearest the other (a
    /// coordinate in the overlap on axes they share, the confronting edges on axes they don't).</summary>
    public static (int X1, int Z1, int X2, int Z2) NearestSegment(BlockRect a, BlockRect b)
    {
        var (ax, bx) = Nearest1D(a.MinX, a.MaxX, b.MinX, b.MaxX);
        var (az, bz) = Nearest1D(a.MinZ, a.MaxZ, b.MinZ, b.MaxZ);
        return (ax, az, bx, bz);
    }

    private static (int, int) Nearest1D(int aMin, int aMax, int bMin, int bMax)
    {
        if (aMax < bMin) return (aMax, bMin);
        if (bMax < aMin) return (aMin, bMax);
        int lo = Math.Max(aMin, bMin), hi = Math.Min(aMax, bMax), mid = (lo + hi) / 2;
        return (mid, mid);
    }

    private static List<FrontlineEdge> ComputeFrontlineEdges(PlanModel plan, int cell, IReadOnlyList<DerivedPiece> pieces)
    {
        var list = new List<FrontlineEdge>();
        foreach (var z in plan.Zones)
        {
            var zr = ToBlock(z.Rect, cell);
            foreach (var p in pieces)
            {
                if (!TouchesOrOverlaps(p.Rect, zr)) continue;
                foreach (var (x1, z1, x2, z2) in FacingEdges(p.Rect, zr))
                    list.Add(new FrontlineEdge(p.Id, z.Id, x1, z1, x2, z2));
            }
        }
        return list;
    }

    // The piece sides facing a zone: an edge faces the zone when its fixed coordinate lies within the zone's
    // span on that axis and its running range overlaps the zone (positive) on the other. An abutting piece
    // yields its one confronting side; a piece overlapping the zone yields every side inside it.
    private static IEnumerable<(int X1, int Z1, int X2, int Z2)> FacingEdges(BlockRect p, BlockRect z)
    {
        int zLo = Math.Max(p.MinZ, z.MinZ), zHi = Math.Min(p.MaxZ, z.MaxZ);
        if (zHi > zLo)
        {
            if (p.MinX >= z.MinX && p.MinX <= z.MaxX) yield return (p.MinX, zLo, p.MinX, zHi);
            if (p.MaxX >= z.MinX && p.MaxX <= z.MaxX) yield return (p.MaxX, zLo, p.MaxX, zHi);
        }
        int xLo = Math.Max(p.MinX, z.MinX), xHi = Math.Min(p.MaxX, z.MaxX);
        if (xHi > xLo)
        {
            if (p.MinZ >= z.MinZ && p.MinZ <= z.MaxZ) yield return (xLo, p.MinZ, xHi, p.MinZ);
            if (p.MaxZ >= z.MinZ && p.MaxZ <= z.MaxZ) yield return (xLo, p.MaxZ, xHi, p.MaxZ);
        }
    }

    // ── connected components (land + same-surface overlap) ──────────────────────────────────────────────

    private static List<IReadOnlyList<string>> ComputeComponents(
        IReadOnlyList<DerivedPiece> pieces, IReadOnlyList<Contact> contacts)
    {
        var parent = pieces.ToDictionary(p => p.Id, p => p.Id);
        string Find(string x) { while (parent[x] != x) x = parent[x] = parent[parent[x]]; return x; }
        void Union(string a, string b) { parent[Find(a)] = Find(b); }

        foreach (var c in contacts)
            if (IsLandInterface(c.Kind) || (c.Kind == ContactKind.Overlap && c.SurfaceDelta == 0))
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
