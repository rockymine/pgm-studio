namespace PgmStudio.Pgm.Derive;

using PgmStudio.Pgm.Plan;

/// <summary>
/// The per-interface read: what happens along a piece edge, aggregated so a rule or a lint can quantify over
/// it instead of re-deriving its own geometry. Three populations, because a piece edge takes part in three
/// different relationships — a <see cref="Seam"/> where it meets another piece (length, height delta, a typed
/// wall), a <see cref="Frontage"/> where it faces the contested crossing (how much of the edge the abutting
/// build zones turn into frontline), and an <see cref="IslandGap"/> where its landmass confronts another
/// across a bridged void (the strait a build region spans). Everything here is aggregation over
/// <see cref="ContactGraph"/> and <see cref="BoardStructure"/> — the two derivations that already know the
/// geometry — never a third computation of it.
/// </summary>
public static class PieceInterfaces
{
    /// <summary>A piece↔piece land interface with everything a rule asks about it in one place: the border
    /// segment and its length (blocks), the surface step across it, the roles of both sides, and the wall
    /// mark if the author seated one on it.</summary>
    public sealed record Seam(
        string A, string B, string RoleA, string RoleB, ContactKind Kind,
        int X1, int Z1, int X2, int Z2, int Length, int SurfaceDelta,
        bool Wall, string? WallChestPiece);

    /// <summary>One side of one authored piece, measured against the fanned board: how many blocks of the
    /// side are exposed (facing no other piece) and how many of those the abutting build zones turn into
    /// frontline. <see cref="FrontlineShare"/> is the fraction of the exposed run that is frontline — the
    /// number that says whether a mid-facing edge actually presents a front or mostly walls the void.</summary>
    public sealed record Frontage(string Piece, string Side, int ExposedBlocks, int FrontlineBlocks)
    {
        public double FrontlineShare => ExposedBlocks == 0 ? 0 : (double)FrontlineBlocks / ExposedBlocks;
    }

    /// <summary>Two landmasses a build region bridges, with the walk across the strait between them in
    /// blocks (a cardinal step count over the empty cells the crossing spans) and a few piece
    /// ids naming each side so a finding can point at the board. <see cref="Direct"/> marks a crossing some
    /// shared region carries with no third landmass in it — the strait one bridge spans, as opposed to a
    /// chain hopping stepping stones, whose per-hop distances are the gap links' to judge.</summary>
    public sealed record IslandGap(
        IReadOnlyList<string> PiecesA, IReadOnlyList<string> PiecesB, string RoleA, string RoleB,
        int Blocks, bool Direct);

    /// <summary>A frontline run in block units: one island's contiguous void-facing face, its width (the
    /// longer extent of its bounding box) and where it stands.</summary>
    public sealed record FrontlineRun(int Team, int WidthBlocks, string Profile, int X1, int Z1, int X2, int Z2);

    /// <summary>Every land/narrow seam of the plan, merged from the contact classification (which knows the
    /// surface delta) and the interface segments (which know the border geometry and the wall marks).</summary>
    public static IReadOnlyList<Seam> Seams(ContactGraph d)
    {
        var deltas = new Dictionary<(string, string), int>();
        foreach (var c in d.Contacts) deltas[(c.A, c.B)] = c.SurfaceDelta;
        var roleOf = d.Pieces.ToDictionary(p => p.Id, p => p.Role);

        var seams = new List<Seam>();
        foreach (var seg in d.InterfaceSegments)
        {
            if (!ContactGraph.IsLandInterface(seg.Kind)) continue;
            seams.Add(new Seam(
                seg.A, seg.B, roleOf.GetValueOrDefault(seg.A, ""), roleOf.GetValueOrDefault(seg.B, ""),
                seg.Kind, seg.X1, seg.Z1, seg.X2, seg.Z2, seg.Length,
                deltas.GetValueOrDefault((seg.A, seg.B)), seg.Wall, seg.WallChestPiece));
        }
        return seams;
    }

    /// <summary>Per authored piece side: the exposed run and its frontline share. Reads the board's own
    /// cell-edge frontline classification (which knows contested from intra-team build) rather than
    /// re-deciding what a frontline is.</summary>
    public static IReadOnlyList<Frontage> Frontages(BoardStructure board)
    {
        var frontline = board.FrontEdges.ToHashSet();
        // exposed cell edges of the authored (k=0) image, keyed by piece and side
        var exposed = new Dictionary<(string Piece, string Side), (int Exposed, int Front)>();
        foreach (var (cell, (pieceId, k)) in board.Filled)
        {
            if (k != 0) continue;
            int x = cell.Item1, z = cell.Item2;
            foreach (var (nb, seg, side) in new ((int, int) Nb, (int, int, int, int) Seg, string Side)[]
                     {
                         ((x + 1, z), (x + 1, z, x + 1, z + 1), "+x"),
                         ((x - 1, z), (x, z, x, z + 1), "-x"),
                         ((x, z + 1), (x, z + 1, x + 1, z + 1), "+z"),
                         ((x, z - 1), (x, z, x + 1, z), "-z"),
                     })
            {
                if (board.Filled.ContainsKey(nb)) continue;
                var have = exposed.GetValueOrDefault((pieceId, side));
                exposed[(pieceId, side)] = (have.Exposed + 1, have.Front + (frontline.Contains(seg) ? 1 : 0));
            }
        }
        return exposed
            .Select(pair => new Frontage(pair.Key.Piece, pair.Key.Side,
                pair.Value.Exposed * board.Cell, pair.Value.Front * board.Cell))
            .OrderBy(f => f.Piece).ThenBy(f => f.Side)
            .ToList();
    }

    /// <summary>The straits: for every pair of islands that a build region bridges (neither of them
    /// decorative), the walk between them across the empty cells. One BFS per island over the non-terrain
    /// cells, so the gap is the span a builder actually crosses, not a line through a third landmass.</summary>
    public static IReadOnlyList<IslandGap> IslandGaps(BoardStructure board)
    {
        // build regions — connected components of buildable cells not covered by terrain
        var open = board.Build.Where(c => !board.Filled.ContainsKey(c)).ToHashSet();
        var regionOf = new Dictionary<(int, int), int>();
        var regionCount = 0;
        foreach (var start in open)
        {
            if (regionOf.ContainsKey(start)) continue;
            var queue = new Queue<(int, int)>();
            queue.Enqueue(start); regionOf[start] = regionCount;
            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                foreach (var nb in Geom.Cells.N4(cur))
                    if (open.Contains(nb) && !regionOf.ContainsKey(nb)) { regionOf[nb] = regionCount; queue.Enqueue(nb); }
            }
            regionCount++;
        }

        // which islands each region touches
        var touching = new List<HashSet<int>>(Enumerable.Range(0, regionCount).Select(_ => new HashSet<int>()));
        foreach (var (cell, region) in regionOf)
            foreach (var nb in Geom.Cells.N4(cell))
                if (board.IslandOf.TryGetValue(nb, out var island)) touching[region].Add(island);

        // pairs, and whether some shared region carries the pair alone (no third non-decorative landmass in
        // it): that region is one bridge between the two, so its span is a strait rather than a chain
        var pairs = new Dictionary<(int, int), bool>();
        foreach (var islands in touching)
        {
            var landmasses = islands.Where(island => board.Roles[island] != "decorative").ToList();
            foreach (var a in landmasses)
                foreach (var b in landmasses)
                {
                    if (a >= b) continue;
                    var direct = landmasses.Count == 2;
                    pairs[(a, b)] = pairs.GetValueOrDefault((a, b)) || direct;
                }
        }
        if (pairs.Count == 0) return [];

        // one BFS per island with a pair, over every empty (non-terrain) cell in the board's extent
        var all = board.Filled.Keys.Concat(board.Build).ToList();
        int minX = all.Min(c => c.Item1) - 1, maxX = all.Max(c => c.Item1) + 1;
        int minZ = all.Min(c => c.Item2) - 1, maxZ = all.Max(c => c.Item2) + 1;
        bool Empty((int, int) c) => !board.Filled.ContainsKey(c)
            && c.Item1 >= minX && c.Item1 <= maxX && c.Item2 >= minZ && c.Item2 <= maxZ;

        // The fan copies every crossing once per orbit image, so gaps are keyed by the piece names of their
        // two sides and each keeps the shortest strait — the reading a finding about the authored board wants.
        var gaps = new Dictionary<(string, string), IslandGap>();
        foreach (var islandA in pairs.Keys.Select(p => p.Item1).Distinct())
        {
            var dist = new Dictionary<(int, int), int>();
            var queue = new Queue<(int, int)>();
            foreach (var cell in board.Islands[islandA])
                foreach (var nb in Geom.Cells.N4(cell))
                    if (Empty(nb) && dist.TryAdd(nb, 1)) queue.Enqueue(nb);
            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                foreach (var nb in Geom.Cells.N4(cur))
                    if (Empty(nb) && dist.TryAdd(nb, dist[cur] + 1)) queue.Enqueue(nb);
            }

            foreach (var ((a, b), direct) in pairs.Where(p => p.Key.Item1 == islandA))
            {
                var gapCells = int.MaxValue;
                foreach (var cell in board.Islands[b])
                    foreach (var nb in Geom.Cells.N4(cell))
                        if (dist.TryGetValue(nb, out var toHere)) gapCells = Math.Min(gapCells, toHere);
                if (gapCells == int.MaxValue) continue;   // no empty route between them at all

                var namesA = PieceNames(board, a);
                var namesB = PieceNames(board, b);
                var keyA = string.Join(",", namesA);
                var keyB = string.Join(",", namesB);
                var key = string.CompareOrdinal(keyA, keyB) <= 0 ? (keyA, keyB) : (keyB, keyA);
                var gap = new IslandGap(namesA, namesB, board.Roles[a], board.Roles[b], gapCells * board.Cell, direct);
                if (!gaps.TryGetValue(key, out var held)
                    || gap.Blocks < held.Blocks
                    || (gap.Blocks == held.Blocks && gap.Direct && !held.Direct))
                    gaps[key] = gap;
            }
        }
        return [.. gaps.Values];
    }

    /// <summary>The board's frontline runs, in blocks.</summary>
    public static IReadOnlyList<FrontlineRun> Runs(BoardStructure board) =>
        [.. board.FrontlineRuns.Select(run => new FrontlineRun(
            run.Team, run.Width * board.Cell, run.Profile,
            run.X1 * board.Cell, run.Z1 * board.Cell, run.X2 * board.Cell, run.Z2 * board.Cell))];

    // A short name for an island: its distinct authored piece ids, at most three, so a finding stays a sentence.
    private static IReadOnlyList<string> PieceNames(BoardStructure board, int island) =>
        [.. board.Islands[island]
            .Select(cell => board.Filled.GetValueOrDefault(cell).PieceId)
            .Where(id => id is not null)
            .Distinct().Order().Take(3)];
}
