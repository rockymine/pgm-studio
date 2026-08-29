using PgmStudio.Geom;
using PgmStudio.Pgm.Evaluate.Terms;
using PgmStudio.Pgm.Plan;

namespace PgmStudio.Pgm.Derive;

/// <summary>
/// What a destroy goal stands from, in blocks — from the two spawns that contest it, and from every other
/// goal on the board. Every leg is the traversal over the fanned closure (pieces ∪ build zones) that the
/// distance rules are stated in, never the straight line.
///
/// <para><b>One field per goal answers all of it.</b> A goal is defended by the team it belongs to, attacked
/// from the other spawn, and stands some way from the goals beside it and the ones across the axis; those
/// are four questions about one walk out of the goal, so the walk is taken once and read four times.
/// <see cref="Read"/> is the spawn reading and <see cref="Pairs"/> the goal-to-goal one, and both are laid
/// over the same closure, so a board cannot answer them in two different geometries.</para>
///
/// <para>This is a <b>measurement surface</b>: it reports and never judges. The judging lives in the
/// evaluator terms — <see cref="Evaluate.Terms.GoalSpawnRatio"/> (GO1),
/// <see cref="Evaluate.Terms.OwnGoalDistance"/> (GO2), <see cref="Evaluate.Terms.OpposingGoalDistance"/>
/// (GO3) and <see cref="Evaluate.Terms.GoalSpawnDistance"/> (GO4) — each of which reads its numbers from
/// here, so the score and the inspect response can never disagree about what a board's walks are.</para>
/// </summary>
public static class GoalDistances
{
    /// <summary>One goal's walks, in blocks: from its own team's nearest spawn, from the enemy's, and the
    /// enemy÷own ratio. A null walk means no route exists over the closure — the traversability gate's
    /// territory, reported here as the absence it is rather than invented.</summary>
    public sealed record GoalWalk(
        string Id, string Kind, double? OwnSpawnBlocks, double? EnemySpawnBlocks, double? Ratio);

    /// <summary>One walk between two destroy goals, in blocks. <paramref name="Opposing"/> separates the
    /// two rules stated over it: false is a team's own pair (<c>GO2</c>), true is a goal against a goal the
    /// other team defends (<c>GO3</c>), which on an order-2 board is the orbit image of an authored one.
    /// A goal's own image is a pair like any other, and the one every board has.</summary>
    /// <param name="Blocks">Null where no route joins them over the closure.</param>
    public sealed record GoalPair(string From, string To, bool Opposing, double? Blocks);

    /// <summary>Every destroyable and core's walks, or empty where the symmetry is not order 2 (the only
    /// order those goals compile at) or the plan has no destroy goal or no spawn.</summary>
    public static List<GoalWalk> Read(PlanModel plan)
    {
        if (Closure.Over(plan) is not { } board) return [];

        var walks = new List<GoalWalk>(board.Goals.Count);
        foreach (var goal in board.Goals)
        {
            // The authored image of the goal belongs to the authored team (goals fan team-outer), so its own
            // spawns are image 0 and the enemy's image 1. Both images give the same pair by symmetry, so one
            // is measured and reported once per authored goal.
            if (board.Reach(goal) is not { } reach)
            {
                walks.Add(new GoalWalk(goal.Id, goal.Kind, null, null, null));
                continue;
            }
            var own = board.Nearest(reach, board.OwnSpawns);
            var enemy = board.EnemySpawns.Count > 0 ? board.Nearest(reach, board.EnemySpawns) : null;
            var ratio = own is > 0 && enemy is { } far ? far / own : null;
            walks.Add(new GoalWalk(goal.Id, goal.Kind, own, enemy, ratio));
        }
        return walks;
    }

    /// <summary>Every pair of destroy goals with the walk between them, over the same closure
    /// <see cref="Read"/> walks. Empty on a board <see cref="Read"/> answers nothing for.
    ///
    /// <para>Each unordered pair appears once. A team's own pairs are the authored goals against each other;
    /// the opposing pairs are each authored goal against the <em>image</em> of an authored one, its own
    /// included — a monument and its mirror are the pair every symmetric board carries, and the one the
    /// contest is usually measured by. By symmetry the walk from A to B's image equals the walk from B to
    /// A's, so the pair is stated once.</para></summary>
    public static List<GoalPair> Pairs(PlanModel plan)
    {
        if (Closure.Over(plan) is not { } board) return [];

        var pairs = new List<GoalPair>();
        for (var from = 0; from < board.Goals.Count; from++)
        {
            // A goal with no route contributes the pairs it is in with no distance, rather than dropping
            // them: the pair exists on the board either way and its walk is the thing that is unknown.
            var goal = board.Goals[from];
            var reach = board.Reach(goal);
            for (var to = from + 1; to < board.Goals.Count; to++)
                pairs.Add(new GoalPair(goal.Id, board.Goals[to].Id, false,
                                       board.Blocks(reach, board.Goals[to].Cell)));
            for (var to = from; to < board.Goals.Count; to++)
                pairs.Add(new GoalPair(goal.Id, board.Goals[to].Id, true,
                                       board.Blocks(reach, board.Goals[to].Image)));
        }
        return pairs;
    }

    /// <summary>The ground every leg is walked over, with the cells the legs run between already on it: the
    /// fanned closure of pieces and build zones, each goal seated as it stands and as its orbit image
    /// stands, and the two spawn sets. Built once and read by both surfaces, which is what keeps the spawn
    /// walks and the goal-to-goal walks the same geometry rather than two that agree by coincidence.</summary>
    private sealed record Closure(WalkGround Ground, List<Goal> Goals,
                                  List<(int X, int Z)> OwnSpawns, List<(int X, int Z)> EnemySpawns)
    {
        /// <summary>The field out of one goal, or null where the goal has no cell to stand on at all.</summary>
        public Dictionary<WalkPlace, WalkCost>? Reach(Goal goal)
            => goal.Cell is { } cell && Ground.Stand(cell) is { } seat ? Walk.Field(seat, Ground) : null;

        /// <summary>The walk to one cell, in blocks, out of a field already walked — null where the cell is
        /// absent or unreached, which is the absence it is rather than a number invented for it.</summary>
        public double? Blocks(Dictionary<WalkPlace, WalkCost>? reach, (int X, int Z)? target)
            => reach is not null && target is { } cell && Ground.Stand(cell) is { } place
               && reach.TryGetValue(place, out var cost) ? cost.Distance : null;

        /// <summary>The nearest of a set, in blocks, out of a field already walked — null where none of them
        /// is reached.</summary>
        public double? Nearest(Dictionary<WalkPlace, WalkCost> reach, List<(int X, int Z)> targets)
        {
            double? best = null;
            foreach (var target in targets)
                if (Ground.Stand(target) is { } place && reach.TryGetValue(place, out var cost)
                    && cost.Distance < (best ?? double.MaxValue))
                    best = cost.Distance;
            return best;
        }

        /// <summary>The closure a plan's goals are measured over, or null where there is nothing to measure:
        /// a symmetry that is not order 2 (the only order these goals compile at), no destroy goal, or no
        /// spawn to walk from.</summary>
        public static Closure? Over(PlanModel plan)
        {
            var mode = plan.Globals.Symmetry;
            if (Symmetry.Order(mode) != 2) return null;

            var placed = new List<(string Id, string Kind, string Piece, double[] At)>();
            foreach (var destroyable in plan.Placements.Destroyables)
                placed.Add((destroyable.Id, "destroyable", destroyable.Piece, destroyable.At));
            foreach (var core in plan.Placements.Cores)
                placed.Add((core.Id, "core", core.Piece, core.At));
            if (placed.Count == 0 || plan.Placements.Spawns.Count == 0) return null;

            // The fanned closure: both orbit images of every generating piece and every build zone. Distances
            // here cross the team boundary (the enemy's spawn to this team's goal), so the un-fanned surface
            // the intra-team terms walk would cut the route at the axis.
            var walkable = new HashSet<(int, int)>();
            foreach (var piece in plan.Pieces)
                if (!PlanRoles.IsAnnotation(piece.Role))
                    AddFanned(walkable, piece.Rect, mode);
            foreach (var zone in plan.BuildZones)
                AddFanned(walkable, zone.Rect, mode);

            // Spawn cells per orbit image: image 0 is the authored team, image 1 its opponent.
            List<(int X, int Z)> own = [], enemy = [];
            foreach (var spawn in plan.Placements.Spawns)
            {
                if (SurfaceNav.MarkerCell(plan, spawn.Piece, spawn.At, walkable) is not { } authored) continue;
                own.Add(authored);
                if (Snap(ImageCell(authored, mode), walkable) is { } opposed) enemy.Add(opposed);
            }
            if (own.Count == 0) return null;

            var goals = new List<Goal>(placed.Count);
            foreach (var (id, kind, piece, at) in placed)
            {
                var cell = SurfaceNav.MarkerCell(plan, piece, at, walkable);
                var image = cell is { } seated ? Snap(ImageCell(seated, mode), walkable) : null;
                goals.Add(new Goal(id, kind, cell, image));
            }
            return new Closure(WalkGround.Over(walkable, plan.Globals.Cell), goals, own, enemy);
        }
    }

    /// <summary>One goal as the closure holds it: where it stands, and where the other team's copy of it
    /// stands. Either is null where the closure has no walkable cell for it.</summary>
    private sealed record Goal(string Id, string Kind, (int X, int Z)? Cell, (int X, int Z)? Image);

    // A cell under the second orbit image: carry its centre through the point fan and floor back to a cell,
    // which is exact for every order-2 mode (a rect corner fan would need the same rounding either way).
    private static (int, int) ImageCell((int X, int Z) cell, string? mode)
    {
        var (imageX, imageZ) = Symmetry.Point(cell.X + 0.5, cell.Z + 0.5, mode, 0, 0, 1);
        return ((int)Math.Floor(imageX), (int)Math.Floor(imageZ));
    }

    private static (int, int)? Snap((int, int) cell, IReadOnlySet<(int, int)> within) =>
        Cells.SnapToWalkable(cell, within, radius: 2);

    private static void AddFanned(HashSet<(int, int)> set, CellRect rect, string? mode)
    {
        for (var imageIndex = 0; imageIndex < 2; imageIndex++)
            for (var x = rect.X; x < rect.X + rect.Width; x++)
                for (var z = rect.Z; z < rect.Z + rect.Height; z++)
                {
                    if (imageIndex == 0) { set.Add((x, z)); continue; }
                    set.Add(ImageCell((x, z), mode));
                }
    }
}
