namespace PgmStudio.Pgm.Plan;

/// <summary>
/// The whole symmetric board as a reachability graph: every piece fanned to each orbit image is a node, edges
/// are land interfaces (walkable adjacency) and gap links (a shared build zone spans the void). Reachability
/// checks — capturing spawn → wool, and frontline → wool avoiding spawn pieces — run over it. Built from a
/// <see cref="ContactGraph"/>; pure.
/// </summary>
public sealed class FannedGraph
{
    public readonly record struct Node(int Team, string PieceId, BlockRect Rect)
    {
        public (int, string) Key => (Team, PieceId);
    }

    public IReadOnlyList<Node> Nodes { get; }
    public IReadOnlySet<(int, string)> Frontline { get; }
    private readonly Dictionary<(int, string), List<(int, string)>> _adj;

    private FannedGraph(List<Node> nodes, Dictionary<(int, string), List<(int, string)>> adj, HashSet<(int, string)> frontline)
    {
        Nodes = nodes;
        _adj = adj;
        Frontline = frontline;
    }

    public static FannedGraph Build(ContactGraph d)
    {
        // fan every piece to each orbit image; drop image duplicates a self-symmetric piece produces
        var nodes = new List<Node>();
        var seen = new HashSet<(int, string)>();
        foreach (var p in d.Pieces)
            for (var k = 0; k < d.Order; k++)
            {
                var key = (k, p.Id);
                if (seen.Add(key)) nodes.Add(new Node(k, p.Id, d.FanRect(p.Rect, k)));
            }

        var zones = d.Plan.Zones
            .SelectMany(z => Enumerable.Range(0, d.Order).Select(k => d.FanRect(ContactGraph.ToBlock(z.Rect, d.Cell), k)))
            .ToList();

        var adj = nodes.ToDictionary(n => n.Key, _ => new List<(int, string)>());
        void Link(Node a, Node b) { adj[a.Key].Add(b.Key); adj[b.Key].Add(a.Key); }

        for (var i = 0; i < nodes.Count; i++)
            for (var j = i + 1; j < nodes.Count; j++)
                if (LandAdjacent(nodes[i].Rect, nodes[j].Rect)) Link(nodes[i], nodes[j]);

        // Gap links over buildable REGIONS, not single zones. A player builds through one zone and continues
        // into an overlapping/adjacent one without landing on terrain, so the fanned zones first merge into
        // regions (overlap or shared edge), then every pair of nodes the region touches is linked. This is the
        // chained-bridging fix: a piece touching one end of a region reaches a piece at the other end even
        // when no single zone spans both. Unlike the team-0 overlay gap links, reachability requires no
        // straight span — routing through the region interior is free.
        foreach (var group in ContactGraph.MergeGroups(zones))
        {
            var regionRects = group.Select(i => zones[i]).ToList();
            var touch = nodes.Where(n => regionRects.Any(zr => Touches(n.Rect, zr))).ToList();
            for (var i = 0; i < touch.Count; i++)
                for (var j = i + 1; j < touch.Count; j++)
                    Link(touch[i], touch[j]);
        }

        var frontline = nodes.Where(n => zones.Any(zr => Touches(n.Rect, zr))).Select(n => n.Key).ToHashSet();
        return new FannedGraph(nodes, adj, frontline);
    }

    public bool Reachable(IEnumerable<(int, string)> from, (int, string) target) =>
        Bfs(from, target, avoid: null);

    public bool ReachableAvoiding(IEnumerable<(int, string)> from, (int, string) target, IReadOnlySet<(int, string)> avoid) =>
        Bfs(from, target, avoid);

    private bool Bfs(IEnumerable<(int, string)> from, (int, string) target, IReadOnlySet<(int, string)>? avoid)
    {
        var q = new Queue<(int, string)>();
        var seen = new HashSet<(int, string)>();
        foreach (var s in from)
            if ((avoid is null || !avoid.Contains(s)) && seen.Add(s))
            {
                if (s == target) return true;
                q.Enqueue(s);
            }
        while (q.Count > 0)
        {
            var cur = q.Dequeue();
            foreach (var nb in _adj[cur])
            {
                if (avoid is not null && avoid.Contains(nb) && nb != target) continue;
                if (!seen.Add(nb)) continue;
                if (nb == target) return true;
                q.Enqueue(nb);
            }
        }
        return false;
    }

    // Reachability adjacency on bare fanned rects: any positive edge border connects — Narrow seams included,
    // matching ContactGraph.Components. The old full-corridor-width floor here misread a lane docking across
    // the SEAM of two host pieces (a 10-block mouth split 5+5 over two slot pieces whose joint edge is one
    // straight interface) as disconnected — the "wool unreachable" misfire; the walkable surface has no such
    // per-piece-pair cut. NOTE the one remaining divergence from the rect-layer authority (review 6.5): any
    // area overlap connects here regardless of surface delta, whereas Components unions an overlap only when
    // SurfaceDelta == 0 — settling that needs per-node surface, not carried here yet.
    private static bool LandAdjacent(BlockRect a, BlockRect b)
    {
        int ix = Math.Min(a.MaxX, b.MaxX) - Math.Max(a.MinX, b.MinX);
        int iz = Math.Min(a.MaxZ, b.MaxZ) - Math.Max(a.MinZ, b.MinZ);
        if (ix > 0 && iz > 0) return true;                                  // area overlap → same landmass
        if (ix < 0 || iz < 0) return false;                                 // disjoint
        int border = ix == 0 ? iz : ix;
        return border > 0;
    }

    private static bool Touches(BlockRect a, BlockRect b)
    {
        int ix = Math.Min(a.MaxX, b.MaxX) - Math.Max(a.MinX, b.MinX);
        int iz = Math.Min(a.MaxZ, b.MaxZ) - Math.Max(a.MinZ, b.MinZ);
        return ix >= 0 && iz >= 0 && !(ix == 0 && iz == 0);
    }
}
