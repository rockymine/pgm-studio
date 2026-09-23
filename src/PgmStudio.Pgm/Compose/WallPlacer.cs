using PgmStudio.Geom;
using PgmStudio.Pgm.Plan;

namespace PgmStudio.Pgm.Compose;

/// <summary>A defence wall the composer seats: the piece on the attack's side of it and the piece on the
/// wool's.</summary>
public sealed record ComposedWall(string Outer, string Inner);

/// <summary>
/// Seats one bedrock wall on every wool approach of a grown unit, where the author holds a defence: across
/// the route the attack takes into the approach, on a seam with no ground beyond either end so it is crossed
/// rather than rounded (<c>PL17</c>), a lane mouth wide (<c>ST8</c>), and in front of the room by <c>ST8</c>'s
/// standoff so the checkpoint is near the wool it keeps — or, on an approach whose room lies deeper than that,
/// the nearest seam past it. One wall per wool: a defence holds one line per objective. A straight lane piece is
/// cut across into two to make the seam; the room is never one side of it (<c>PL13</c>). A wool no seam
/// qualifies on is left unwalled.
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
            walls.Add(seated.Wall);
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

    private static (List<GrownPiece> Pieces, ComposedWall Wall)? Seat(List<GrownPiece> pieces, string roomId, int cell)
    {
        if (pieces.FirstOrDefault(p => p.Id == roomId) is not { Box: { } box } room) return null;
        var owner = new Dictionary<(int X, int Z), GrownPiece>();
        foreach (var piece in pieces)
            foreach (var c in Cells(piece.Rect)) owner[c] = piece;
        var front = pieces.Where(p => p.Box?.Kind == BoxKind.Frontline).SelectMany(p => Cells(p.Rect)).ToList();
        if (front.Count == 0) return null;

        var fromRoom = Distances(Cells(room.Rect), owner);
        var fromFront = Distances(front, owner);
        var shortest = owner.Keys.Where(c => fromRoom.ContainsKey(c) && fromFront.ContainsKey(c))
            .Select(c => fromRoom[c] + fromFront[c]).DefaultIfEmpty(-1).Min();
        if (shortest < 0) return null;
        bool OnRoute((int X, int Z) c) =>
            fromRoom.TryGetValue(c, out var r) && fromFront.TryGetValue(c, out var f) && r + f == shortest;

        var approach = pieces.Where(p => p.Box?.Id == box.Id && p.Role != PlanRoles.WoolRoom).ToList();
        var best = ((Seam Seam, GrownPiece? Cut, int Standoff)?)null;
        foreach (var (seam, cut) in Candidates(approach, owner))
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
            if (!faces.Any(f => OnRoute(f.Before) && OnRoute(f.After))) continue;   // the attack does not cross it
            var inner = faces.Min(f => Math.Min(fromRoom[f.Before], fromRoom[f.After]));
            var standoff = inner * cell;                                           // blocks from the room's edge
            if (standoff < PlanValidator.WallStandoffMinBlocks) continue;         // against the room's door
            if (best is not { } held || Rank(standoff) < Rank(held.Standoff)
                || (Rank(standoff) == Rank(held.Standoff) && standoff < held.Standoff))
                best = (seam, cut, standoff);
        }
        if (best is not { } chosen) return null;

        var (before, after) = chosen.Seam.Faces().First();
        var beforeIsInner = fromRoom[before] < fromRoom[after];
        if (chosen.Cut is not { } lane)
        {
            var (outerCell, innerCell) = beforeIsInner ? (after, before) : (before, after);
            return (pieces, new ComposedWall(owner[outerCell].Id, owner[innerCell].Id));
        }

        var (low, high) = Split(lane.Rect, chosen.Seam);
        var innerId = lane.Id + InnerSuffix;
        var outerRect = beforeIsInner ? high : low;
        var innerRect = beforeIsInner ? low : high;
        var next = pieces.Where(p => p.Id != lane.Id).ToList();
        next.Add(lane with { Rect = outerRect });
        next.Add(lane with { Id = innerId, Rect = innerRect });
        return (next, new ComposedWall(lane.Id, innerId));
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
    private static IEnumerable<(Seam Seam, GrownPiece? Cut)> Candidates(
        IReadOnlyList<GrownPiece> approach, IReadOnlyDictionary<(int X, int Z), GrownPiece> owner)
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
            foreach (var n in new[] { (c.X + 1, c.Z), (c.X - 1, c.Z), (c.X, c.Z + 1), (c.X, c.Z - 1) })
                if (land.ContainsKey(n) && !distance.ContainsKey(n)) { distance[n] = distance[c] + 1; queue.Enqueue(n); }
        }
        return distance;
    }
}
