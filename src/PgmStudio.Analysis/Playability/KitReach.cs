
namespace PgmStudio.Analysis.Playability;

using PgmStudio.Analysis.Region;
using PgmStudio.Geom;
using PgmStudio.Analysis.Scan;

using Dict = Dictionary<string, object?>;

/// <summary>
/// Budget-aware reachability: can a freshly-spawned player bridge from their spawn to each wool with only
/// the placeable blocks their spawn kit grants?
///
/// <para>It asks <see cref="Walk"/> under <see cref="WalkAim.Reach"/> — the aim that minimises blocks placed
/// rather than distance — over the board's own <see cref="WorldWalk.Ground"/>. That answer is the minimum
/// blocks needed to cross, and comparing it against the kit's placeable-block count is the per-life
/// crossing-feasibility signal. What it adds over the count is the rest of what the walk knows: how far round
/// the cheapest crossing goes, and what it falls down on the way.</para>
///
/// <para>The blocks count both halves of what a player builds: a rise steeper than a step is blocks placed
/// as much as a bridged gap is, so a wool up a scarp costs what climbing it costs. Water is not read here,
/// because swimming is slower and neither a block nor a wall — it moves no verdict this endpoint gives.</para>
///
/// <para>Each team walks <b>its own</b> ground, with whatever an <c>enter</c> rule bars it from subtracted.
/// A shared set answers that a wool behind an oversized protection is reachable for a kit that can never
/// legally get there, which is the same board the export gate refuses under <c>EX1</c>.</para>
/// </summary>
public static class KitReach
{
    /// <summary>What one wool costs this team to reach: the cheapest crossing, and whether the kit pays
    /// for it.</summary>
    /// <param name="Color">The wool to be captured.</param>
    /// <param name="Owner">The team that defends it, or empty where the document names none. A team's own
    /// wool is reported like any other and never held against it: this budget is what capturing a wool costs,
    /// and a defender has no capture to pay for.</param>
    /// <param name="X">Where it stands, east–west.</param>
    /// <param name="Z">Where it stands, north–south.</param>
    /// <param name="BlocksNeeded">How many blocks the cheapest path asks the player to place — bridging
    /// void, and climbing anything steeper than a step.</param>
    /// <param name="Blocks">How far round that cheapest crossing goes, in blocks walked.</param>
    /// <param name="Drops">How many falls over the free height it takes on the way.</param>
    /// <param name="Reachable">Whether any path exists at all, at any cost.</param>
    /// <param name="WithinBudget">Whether the kit grants enough blocks to pay for it.</param>
    /// <param name="Severity">What the verdict is worth — the same word a finding carries.</param>
    /// <param name="Message">The verdict in a sentence, with the numbers in it.</param>
    public sealed record WoolReach(string Color, string Owner, int X, int Z, int BlocksNeeded, int Blocks,
        int Drops, bool Reachable, bool WithinBudget, string Severity, string Message);
    /// <summary>What one team's spawn kit can reach.</summary>
    /// <param name="Team">The team, by id.</param>
    /// <param name="Kit">The kit it spawns with, by name.</param>
    /// <param name="Budget">How many placeable blocks that kit grants.</param>
    /// <param name="WaterBucket">Whether the kit carries a water bucket, which crosses a gap no block
    /// budget would.</param>
    /// <param name="Wools">One reading per wool the team must capture.</param>
    public sealed record TeamReach(string Team, string Kit, int Budget, bool WaterBucket, List<WoolReach> Wools);
    /// <summary>What every team's kit can reach. </summary>
    /// <param name="HaveLayers">Whether the map has scanned column data. False means nothing was measured, not
    /// that nothing is reachable.</param>
    /// <param name="Severity">The worst verdict over every team.</param>
    /// <param name="Message">That verdict in a sentence.</param>
    /// <param name="Teams">One reading per team.</param>
    public sealed record Result(bool HaveLayers, string Severity, string Message, List<TeamReach> Teams);

    /// <summary><b>declared</b> is goals the document cannot carry — see <see cref="NavPoints.Of"/>.</summary>
    public static Result Check(Dict data, SegmentIndex? segments, int margin = 16,
        IReadOnlyList<NavPoint>? declared = null)
    {
        var shared = WorldWalk.Ground(data, segments, margin);
        var bounds = ((double)shared.Bounds.X, (double)shared.Bounds.Z,
                      (double)shared.Bounds.MaxX, (double)shared.Bounds.MaxZ);
        var haveLayers = shared.Ground.Count > 0;

        var kitBudgets = KitBudgets(data);
        var regions = MapDoc.AsDict(data.GetValueOrDefault("regions"));
        var wools = NavPoints.Of(data, bounds, declared).Where(point => point.Kind == "wool").ToList();

        var teams = new List<TeamReach>();
        foreach (var sp in MapDoc.AsList(data.GetValueOrDefault("spawns")).OfType<Dict>())
        {
            if (MapDoc.Truthy(sp.GetValueOrDefault("observer"))) continue;
            var team = sp.GetValueOrDefault("team") as string ?? "";
            var kitId = sp.GetValueOrDefault("kit") as string ?? "";
            var (budget, water) = kitBudgets.GetValueOrDefault(kitId, (0, false));

            // This team's own ground, not everyone's: a route through a region an enter rule bars it from is
            // a route it cannot take, and pricing one is the whole reason the verdict and the export gate
            // could disagree about the same board.
            var ground = WorldWalk.For(shared, data, team.Length == 0 ? null : team);

            // The spawn's own storey, not its cell's lowest: a spawn on a deck over a gallery walks the deck,
            // and pricing its kit from the gallery floor is a budget for a route the team never takes.
            var box = NavPoints.Region(sp.GetValueOrDefault("region"), regions);
            var start = NavPoints.Centre(box, regions, bounds) is { } seat
                ? Cells.SnapToWalkable(seat, ground.Footprint, SnapRadius)
                : null;
            var storey = NavPoints.Height(box);
            var from = start is { } cell
                ? storey is { } height ? ground.Nearest(cell, height) : ground.Stand(cell)
                : null;
            var field = from is { } origin ? Walk.Field(origin, ground, WalkAim.Reach) : [];

            var perWool = new List<WoolReach>();
            foreach (var wool in wools)
            {
                var (color, wx, wz) = (wool.Name, wool.X, wool.Z);
                // The wool is snapped onto the ground everyone shares and then looked up in this team's own
                // field. Snapping on the team's ground instead would slide a barred wool sideways until it
                // found a cell the team may stand on, and report the walk to that cell as the walk to the
                // wool.
                var target = Cells.SnapToWalkable((wx, wz), shared.Footprint, SnapRadius);
                var cost = target is { } at
                    && (wool.Y is { } woolY ? shared.Nearest(at, woolY) : shared.Stand(at)) is { } to
                    && field.TryGetValue(to, out var reached)
                    ? reached : (WalkCost?)null;
                var reachable = cost is not null;
                var need = cost?.Blocks ?? -1;
                var within = reachable && need <= budget;
                var (sev, msg) = (reachable, within) switch
                {
                    (false, _) => ("error", "no path from spawn — the ground between is void, no-build, or "
                                          + "barred to this team by an enter rule"),
                    (_, true) => ("ok", $"{need} block(s) to place over {cost!.Value.Distance} walked "
                                      + $"— kit gives {budget}"),
                    _ => ("warning", $"needs {need} blocks to place but kit gives only {budget}"),
                };
                perWool.Add(new WoolReach(color, wool.Owner, wx, wz, need, cost?.Distance ?? -1,
                    cost?.Drops ?? 0, reachable, within, sev, msg));
            }
            teams.Add(new TeamReach(team, kitId, budget, water, perWool));
        }

        // A team is judged on the wools it must capture. Its own is reported and never counted against it:
        // the budget is what a capture costs, and a defender makes none.
        var allWools = teams.SelectMany(t => t.Wools.Where(w => w.Owner != t.Team)).ToList();
        var worst = allWools.Any(w => !w.Reachable) ? "error"
            : allWools.Any(w => !w.WithinBudget) ? "warning" : "ok";
        var message = worst switch
        {
            "error" => "some wools are unreachable from spawn — the gap can't be bridged (void / no-build)",
            "warning" => "some wools need more bridging blocks than the spawn kit grants",
            _ => "every wool is reachable within the spawn kit's block budget",
        };
        return new Result(haveLayers, worst, message, teams);
    }

    /// <summary>How far a marker may sit off walkable ground and still be walked to. A wool's stated location
    /// is a block in a room, not a cell of terrain, so it lands inside a wall as often as on a floor.</summary>
    private const int SnapRadius = 16;

    // ── kit budget: count placeable blocks (and note a water bucket as a bridging aid) ──────────
    private static Dictionary<string, (int budget, bool water)> KitBudgets(Dict data)
    {
        var map = new Dictionary<string, (int, bool)>();
        foreach (var kit in MapDoc.AsList(data.GetValueOrDefault("kits")).OfType<Dict>())
        {
            var id = kit.GetValueOrDefault("id") as string ?? "";
            var blocks = 0;
            var water = false;
            foreach (var item in MapDoc.AsList(kit.GetValueOrDefault("items")).OfType<Dict>())
            {
                var mat = MapDoc.Normalize(item.GetValueOrDefault("material") as string ?? "");
                var amount = MapDoc.Num(item.GetValueOrDefault("amount")) is { } a ? (int)a : 1;
                if (KitBlocks.IsPlaceable(mat)) blocks += amount;
                else if (mat is "water bucket" or "water") water = true;
            }
            map[id] = (blocks, water);
        }
        return map;
    }

}
