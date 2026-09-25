using PgmStudio.Geom;
using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Compose;

/// <summary>A defence wall the composer seats: the piece on the attack's side of it and the piece on the
/// wool's.</summary>
public sealed record ComposedWall(string Outer, string Inner);

/// <summary>
/// Seats the bedrock walls on every wool approach of a grown unit, where the author holds a defence: each on a
/// seam with no ground beyond either end so it is crossed rather than rounded (<c>PL17</c>), a lane mouth wide
/// (<c>ST8</c>), and at least <c>ST8</c>'s nearest standoff in front of the room. A plain approach takes one wall
/// across the route the attack takes, at <c>ST8</c>'s standoff — or, where the room lies deeper than that, the
/// nearest seam past it. An approach that goes round a hole takes two, one across each way round: across its two
/// legs as near the entry bar as each allows, or wherever else a pair closes it. A straight lane piece is cut across into two to make a seam; the room is never one
/// side of it (<c>PL13</c>). A wool no seam qualifies on is left unwalled.
/// </summary>
public static class WallPlacer
{
    /// <summary>The id suffix of the piece cut off a lane on the wool's side of its wall.</summary>
    public const string InnerSuffix = "-inner";

    /// <summary>The shortest straight lane, in cells on a <paramref name="cell"/>-block grid, that seats a wall
    /// between its hub end and its room: <c>ST8</c>'s nearest standoff behind the wall and one cell of lane in
    /// front of it, so the wall stands off the hub's side rather than at the junction.</summary>
    public static int LaneCells(int cell) => (PlanValidator.WallStandoffMinBlocks + cell - 1) / cell + 1;

    /// <summary>The unit with every seated wall's lane cut in two, and the walls.</summary>
    public static (GrownUnit Unit, IReadOnlyList<ComposedWall> Walls) Place(GrownUnit unit, int cell)
    {
        var pieces = unit.Pieces.ToList();
        var walls = new List<ComposedWall>();
        foreach (var wool in unit.Wools)
        {
            if (Seat(pieces, wool.Piece, cell) is not { } seated) continue;
            pieces = seated.Pieces;
            walls.AddRange(seated.Walls);
        }
        return (unit with { Pieces = pieces }, walls);
    }

    /// <summary>A seam between two rows or columns of cells: vertical at <c>x = At</c> between columns
    /// <c>At − 1</c> and <c>At</c>, or horizontal at <c>z = At</c>, spanning <c>[Lo, Hi)</c> along it.</summary>
    private readonly record struct Seam(bool Vertical, int At, int Lo, int Hi)
    {
        public IEnumerable<((int X, int Z) Before, (int X, int Z) After)> Faces()
        {
            for (var along = Lo; along < Hi; along++)
                yield return Vertical ? ((At - 1, along), (At, along)) : ((along, At - 1), (along, At));
        }

        /// <summary>The four cells beyond the seam's two ends, one on each face.</summary>
        public IEnumerable<(int X, int Z)> Ends() => Vertical
            ? [(At - 1, Lo - 1), (At, Lo - 1), (At - 1, Hi), (At, Hi)]
            : [(Lo - 1, At - 1), (Lo - 1, At), (Hi, At - 1), (Hi, At)];
    }

    /// <summary>A seam a wall may stand on: the lane piece it cuts, if any, and its standoff from the room in
    /// blocks.</summary>
    private readonly record struct Candidate(Seam Seam, GrownPiece? Cut, int Standoff);

    private static (List<GrownPiece> Pieces, IReadOnlyList<ComposedWall> Walls)? Seat(
        List<GrownPiece> pieces, string roomId, int cell)
    {
        if (pieces.FirstOrDefault(p => p.Id == roomId) is not { Box: { } box } room) return null;
        var owner = Owners(pieces);
        var front = pieces.Where(p => p.Box?.Kind == BoxKind.Frontline).SelectMany(p => Cells(p.Rect)).ToList();
        if (front.Count == 0) return null;

        var fromRoom = Distances(Cells(room.Rect), owner);
        var fromFront = Distances(front, owner);
        var shortest = owner.Keys.Where(c => fromRoom.ContainsKey(c) && fromFront.ContainsKey(c))
            .Select(c => fromRoom[c] + fromFront[c]).DefaultIfEmpty(-1).Min();
        if (shortest < 0) return null;
        bool OnRoute((int X, int Z) c) =>
            fromRoom.TryGetValue(c, out var r) && fromFront.TryGetValue(c, out var f) && r + f == shortest;

        var region = pieces.Where(p => p.Box?.Id == box.Id).ToList();
        var approach = region.Where(p => p.Role != PlanRoles.WoolRoom).ToList();
        var candidates = Qualifying(approach, owner, fromRoom, cell).ToList();

        if (Encloses(region))
        {
            // across the ring's two legs where a pair stands there, else any pair that closes the way round
            var pairs = Pairs(candidates, region, owner, room).ToList();
            var legs = pairs.Where(p => p.First.Cut?.Slot == ApproachSlots.Leg && p.Second.Cut?.Slot == ApproachSlots.Leg);
            foreach (var (first, second) in legs.Concat(pairs))
                if (Apply(pieces, [first, second], region, room, fromRoom) is { } walled) return walled;
        }

        var best = (Candidate?)null;
        foreach (var candidate in candidates)
        {
            if (!candidate.Seam.Faces().Any(f => OnRoute(f.Before) && OnRoute(f.After))) continue;   // the attack does not cross it
            if (best is not { } held || Rank(candidate.Standoff) < Rank(held.Standoff)
                || (Rank(candidate.Standoff) == Rank(held.Standoff) && candidate.Standoff < held.Standoff))
                best = candidate;
        }
        return best is { } chosen ? Apply(pieces, [chosen], region, room, fromRoom) : null;
    }

    /// <summary>Every seam of the approach a wall may stand on, whether or not the attack crosses it: a mouth
    /// wide, void past both ends, one piece on each side and neither of them the room, and far enough in front
    /// of the room.</summary>
    private static IEnumerable<Candidate> Qualifying(
        IReadOnlyList<GrownPiece> approach, IReadOnlyDictionary<(int X, int Z), GrownPiece> owner,
        IReadOnlyDictionary<(int X, int Z), int> fromRoom, int cell)
    {
        foreach (var (seam, cut) in Candidates(approach))
        {
            var mouth = (seam.Hi - seam.Lo) * cell;
            if (mouth < PlanValidator.WallMouthMinBlocks || mouth > PlanValidator.WallMouthMaxBlocks) continue;
            if (seam.Ends().Any(owner.ContainsKey)) continue;                      // ground past an end: rounded
            var faces = seam.Faces().ToList();
            if (faces.Any(f => !owner.ContainsKey(f.Before) || !owner.ContainsKey(f.After))) continue;
            if (faces.Any(f => owner[f.Before].Role == PlanRoles.WoolRoom || owner[f.After].Role == PlanRoles.WoolRoom)) continue;
            // one piece each side, so the wall the plan names spans the whole seam
            if (faces.Select(f => owner[f.Before].Id).Distinct().Count() > 1
                || faces.Select(f => owner[f.After].Id).Distinct().Count() > 1) continue;
            if (faces.Any(f => !fromRoom.ContainsKey(f.Before) || !fromRoom.ContainsKey(f.After))) continue;
            var standoff = faces.Min(f => Math.Min(fromRoom[f.Before], fromRoom[f.After])) * cell;
            if (standoff < PlanValidator.WallStandoffMinBlocks) continue;         // against the room's door
            yield return new Candidate(seam, cut, standoff);
        }
    }

    /// <summary>Whether a box's pieces close round void: a cell inside their bounds the outside cannot reach
    /// without crossing them.</summary>
    private static bool Encloses(IReadOnlyList<GrownPiece> region)
    {
        var land = region.SelectMany(p => Cells(p.Rect)).ToHashSet();
        int minX = land.Min(c => c.X) - 1, maxX = land.Max(c => c.X) + 1;
        int minZ = land.Min(c => c.Z) - 1, maxZ = land.Max(c => c.Z) + 1;
        var outside = new HashSet<(int X, int Z)> { (minX, minZ) };
        var queue = new Queue<(int X, int Z)>(outside);
        while (queue.Count > 0)
            foreach (var n in Neighbours(queue.Dequeue()))
                if (n.X >= minX && n.X <= maxX && n.Z >= minZ && n.Z <= maxZ && !land.Contains(n) && outside.Add(n))
                    queue.Enqueue(n);
        return (maxX - minX + 1) * (maxZ - minZ + 1) > land.Count + outside.Count;
    }

    /// <summary>The pairs of seams that between them close the way from the approach's entry to the room while
    /// neither does alone — one across each way round a hole — the pair standing farthest from the room first.</summary>
    private static IEnumerable<(Candidate First, Candidate Second)> Pairs(
        IReadOnlyList<Candidate> candidates, IReadOnlyList<GrownPiece> region,
        IReadOnlyDictionary<(int X, int Z), GrownPiece> owner, GrownPiece room)
    {
        var land = region.SelectMany(p => Cells(p.Rect)).ToHashSet();
        var entries = land.Where(c => Neighbours(c).Any(n => owner.ContainsKey(n) && !land.Contains(n))).ToList();
        var roomCells = Cells(room.Rect).ToList();
        bool Closes(params Seam[] seams) => !roomCells.Any(Reach(entries, land, seams).Contains);

        var open = candidates.Where(c => !Closes(c.Seam)).ToList();
        var pairs = new List<(Candidate First, Candidate Second)>();
        for (var i = 0; i < open.Count; i++)
            for (var j = i + 1; j < open.Count; j++)
            {
                if (open[i].Cut is { } cutA && open[j].Cut is { } cutB && cutA.Id == cutB.Id) continue;
                if (Closes(open[i].Seam, open[j].Seam)) pairs.Add((open[i], open[j]));
            }
        return pairs
            .OrderByDescending(p => Math.Min(p.First.Standoff, p.Second.Standoff))
            .ThenByDescending(p => p.First.Standoff + p.Second.Standoff);
    }

    /// <summary>The walls on <paramref name="chosen"/>, each lane seam cut first: a wall's inner piece is the
    /// side the room reaches with every chosen seam closed, or the nearer the room where the room reaches both.
    /// Null when a cut leaves a seam without one piece on each side.</summary>
    private static (List<GrownPiece> Pieces, IReadOnlyList<ComposedWall> Walls)? Apply(
        List<GrownPiece> pieces, IReadOnlyList<Candidate> chosen, IReadOnlyList<GrownPiece> region, GrownPiece room,
        IReadOnlyDictionary<(int X, int Z), int> fromRoom)
    {
        var land = region.SelectMany(p => Cells(p.Rect)).ToHashSet();
        var reached = Reach(Cells(room.Rect), land, chosen.Select(c => c.Seam).ToArray());
        bool BeforeIsInner(Seam seam)
        {
            var (before, after) = seam.Faces().First();
            return reached.Contains(before) != reached.Contains(after)
                ? reached.Contains(before)
                : fromRoom[before] < fromRoom[after];
        }
        var next = pieces;
        foreach (var candidate in chosen.Where(c => c.Cut is not null))
        {
            var lane = next.First(p => p.Id == candidate.Cut!.Id);
            if (next.Any(p => p.Id == lane.Id + InnerSuffix)) return null;
            var (low, high) = Split(lane.Rect, candidate.Seam);
            var lowIsInner = BeforeIsInner(candidate.Seam);
            next = next.Where(p => p.Id != lane.Id).ToList();
            next.Add(lane with { Rect = lowIsInner ? high : low });
            next.Add(lane with { Id = lane.Id + InnerSuffix, Rect = lowIsInner ? low : high });
        }

        var owner = Owners(next);
        var walls = new List<ComposedWall>();
        foreach (var candidate in chosen)
        {
            var faces = candidate.Seam.Faces().ToList();
            var before = faces.Select(f => owner[f.Before].Id).Distinct().ToList();
            var after = faces.Select(f => owner[f.After].Id).Distinct().ToList();
            if (before.Count > 1 || after.Count > 1) return null;
            walls.Add(BeforeIsInner(candidate.Seam)
                ? new ComposedWall(after[0], before[0])
                : new ComposedWall(before[0], after[0]));
        }
        return (next, walls);
    }

    /// <summary>How far a <paramref name="standoff"/> sits from <c>ST8</c>'s seat: from the middle of its window
    /// inside it, and past its far edge by more than the whole window beyond it — so a seam in the window always
    /// wins, and a two-legged approach whose room is deeper than the window still takes the nearest seam its
    /// attack crosses, the entrance the defence holds.</summary>
    private static int Rank(int standoff)
    {
        const int lo = PlanValidator.WallStandoffMinBlocks, hi = PlanValidator.WallStandoffMaxBlocks;
        return standoff <= hi ? Math.Abs(standoff - (lo + hi) / 2) : standoff - hi + (hi - lo);
    }

    /// <summary>Every seam an approach offers: each cut across one of its pieces, and each of its four sides.</summary>
    private static IEnumerable<(Seam Seam, GrownPiece? Cut)> Candidates(IReadOnlyList<GrownPiece> approach)
    {
        foreach (var piece in approach)
        {
            var r = piece.Rect;
            for (var x = r.X + 1; x < r.X + r.Width; x++) yield return (new Seam(true, x, r.Z, r.Z + r.Height), piece);
            for (var z = r.Z + 1; z < r.Z + r.Height; z++) yield return (new Seam(false, z, r.X, r.X + r.Width), piece);
            yield return (new Seam(true, r.X, r.Z, r.Z + r.Height), null);
            yield return (new Seam(true, r.X + r.Width, r.Z, r.Z + r.Height), null);
            yield return (new Seam(false, r.Z, r.X, r.X + r.Width), null);
            yield return (new Seam(false, r.Z + r.Height, r.X, r.X + r.Width), null);
        }
    }

    private static (CellRect Low, CellRect High) Split(CellRect r, Seam seam) => seam.Vertical
        ? (new CellRect(r.X, r.Z, seam.At - r.X, r.Height), new CellRect(seam.At, r.Z, r.X + r.Width - seam.At, r.Height))
        : (new CellRect(r.X, r.Z, r.Width, seam.At - r.Z), new CellRect(r.X, seam.At, r.Width, r.Z + r.Height - seam.At));

    private static IEnumerable<(int X, int Z)> Cells(CellRect r)
    {
        for (var x = r.X; x < r.X + r.Width; x++)
            for (var z = r.Z; z < r.Z + r.Height; z++)
                yield return (x, z);
    }

    private static Dictionary<(int X, int Z), GrownPiece> Owners(IEnumerable<GrownPiece> pieces)
    {
        var owner = new Dictionary<(int X, int Z), GrownPiece>();
        foreach (var piece in pieces)
            foreach (var c in Cells(piece.Rect)) owner[c] = piece;
        return owner;
    }

    private static (int X, int Z)[] Neighbours((int X, int Z) c) =>
        [(c.X + 1, c.Z), (c.X - 1, c.Z), (c.X, c.Z + 1), (c.X, c.Z - 1)];

    /// <summary>The four-connected step count from <paramref name="sources"/> to every land cell.</summary>
    private static Dictionary<(int X, int Z), int> Distances(
        IEnumerable<(int X, int Z)> sources, IReadOnlyDictionary<(int X, int Z), GrownPiece> land)
    {
        var distance = new Dictionary<(int X, int Z), int>();
        var queue = new Queue<(int X, int Z)>();
        foreach (var s in sources) { distance[s] = 0; queue.Enqueue(s); }
        while (queue.Count > 0)
        {
            var c = queue.Dequeue();
            foreach (var n in Neighbours(c))
                if (land.ContainsKey(n) && !distance.ContainsKey(n)) { distance[n] = distance[c] + 1; queue.Enqueue(n); }
        }
        return distance;
    }

    /// <summary>The <paramref name="land"/> cells four-connected to <paramref name="sources"/> without stepping
    /// across any of the <paramref name="closed"/> seams.</summary>
    private static HashSet<(int X, int Z)> Reach(
        IEnumerable<(int X, int Z)> sources, IReadOnlySet<(int X, int Z)> land, IReadOnlyList<Seam> closed)
    {
        var shut = closed.SelectMany(s => s.Faces())
            .SelectMany(f => new[] { (f.Before, f.After), (f.After, f.Before) }).ToHashSet();
        var reached = new HashSet<(int X, int Z)>();
        var queue = new Queue<(int X, int Z)>();
        foreach (var s in sources) if (land.Contains(s) && reached.Add(s)) queue.Enqueue(s);
        while (queue.Count > 0)
        {
            var c = queue.Dequeue();
            foreach (var n in Neighbours(c))
                if (land.Contains(n) && !shut.Contains((c, n)) && reached.Add(n)) queue.Enqueue(n);
        }
        return reached;
    }
}
