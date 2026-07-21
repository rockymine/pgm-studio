#:project /home/user/pgm-studio/src/PgmStudio.Pgm/PgmStudio.Pgm.csproj
#:property JsonSerializerIsReflectionEnabledByDefault=true
// stalemate probe (prototype): the deeper per-wool siege factors beyond the WL9/WL10 distance terms —
// approach count (chokepoint routes into the wool's island), lane/entry width, proximity of a rotation
// hole (a middle/frontline void the attacker can loop through), and the defence deficit — composed into
// a per-wool STALEMATE flag: a single thin approach whose defender arrives no later than the attacker,
// with no rotation hole near enough to flank through. Runs every 2+-wool board in both gallery modes.
using PgmStudio.Geom;
using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Evaluate;
using PgmStudio.Pgm.Plan;

const int ThinEntryCells = 2;   // a lane/entry at most this wide (cells) is a chokepoint
const double DeficitMax = 10.0; // spawn − front at most this (blocks) = defender arrives no later

var flagged = new List<string>();
var boards = 0;

void Examine(string name, PlanModel plan)
{
    if (plan.Placements.Wools.Count < 2) return;
    var walk = new HashSet<(int, int)>();
    foreach (var p in plan.Pieces.Where(p => !PlanRoles.Annotations.Contains(p.Role)))
        for (var x = p.Rect[0]; x < p.Rect[0] + p.Rect[2]; x++)
            for (var z = p.Rect[1]; z < p.Rect[1] + p.Rect[3]; z++) walk.Add((x, z));
    foreach (var z0 in plan.Zones)
        for (var x = z0.Rect[0]; x < z0.Rect[0] + z0.Rect[2]; x++)
            for (var z = z0.Rect[1]; z < z0.Rect[1] + z0.Rect[3]; z++) walk.Add((x, z));

    (int, int)? Cell(string pieceId, double[] at)
    {
        var p = plan.Pieces.FirstOrDefault(q => q.Id == pieceId);
        if (p is null) return null;
        var c = (p.Rect[0] + Math.Clamp((int)at[0], 0, p.Rect[2] - 1), p.Rect[1] + Math.Clamp((int)at[1], 0, p.Rect[3] - 1));
        return walk.Contains(c) ? c : null;
    }

    var ctx = EvalContext.Build(plan);
    var board = ctx.Board;
    var front = board.BuildKindOf.Where(kv => kv.Value == "front-front").Select(kv => kv.Key).ToHashSet();
    var s = plan.Placements.Spawns.Select(sp => Cell(sp.Piece, sp.At)).FirstOrDefault(c => c is not null);
    if (s is null || front.Count == 0) return;

    // the walkable shore of every rotation-relevant hole: cells bordering a middle/frontline enclosed void —
    // reaching the shore means the attacker can start a loop around the hole
    var holeShore = new HashSet<(int, int)>();
    foreach (var v in board.Voids.Where(v => v.Class is "middle" or "frontline"))
        foreach (var c in v.Cells)
            foreach (var nb in Cells.N4(c))
                if (walk.Contains(nb)) holeShore.Add(nb);

    double? ToSet((int X, int Z) start, HashSet<(int, int)> targets)
    {
        if (targets.Count == 0) return null;
        if (targets.Contains(start)) return 0;
        var seen = new HashSet<(int, int)> { start };
        var q = new Queue<((int, int) C, int D)>(); q.Enqueue((start, 0));
        while (q.Count > 0)
        {
            var ((x, z), d) = q.Dequeue();
            foreach (var n in new[] { (x + 1, z), (x - 1, z), (x, z + 1), (x, z - 1) })
            {
                if (!walk.Contains(n) || !seen.Add(n)) continue;
                if (targets.Contains(n)) return (d + 1) * (double)plan.Globals.Cell;
                q.Enqueue((n, d + 1));
            }
        }
        return null;
    }

    boards++;
    var lines = new List<string>();
    var anyStalemate = false;
    for (var i = 0; i < plan.Placements.Wools.Count; i++)
    {
        var wp = plan.Placements.Wools[i];
        if (Cell(wp.Piece, wp.At) is not { } wc) return;
        if (Cells.PathLength(s.Value, wc, walk) is not { } steps) return;
        var spawnD = steps * (double)plan.Globals.Cell;
        if (ToSet(wc, front) is not { } frontD) return;

        // k=0 board reads, index-aligned with the wool order (the deriver emits them per wool, first orbit first)
        var approaches = i < board.Approaches.Count ? board.Approaches[i].Count : -1;
        var (shape, width) = i < board.WoolShapes.Count ? board.WoolShapes[i] : ("?", -1);
        var holeD = ToSet(wc, holeShore);

        // the flag deliberately ignores the approach count: the deriver counts the arms touching the wool's
        // room, so a donut's ring reads as two approaches even when both funnel through one upstream corridor —
        // reported as data, not trusted as route multiplicity. The discriminator that separates the known-bad
        // remote-donut board from the known-good Y-approach one is the rotation hole: the bad board has NO
        // reachable middle/frontline hole (no flank, ever), the good one has them 50–80 blocks out.
        var deficit = spawnD - frontD;
        var stalemate = width >= 0 && width <= ThinEntryCells && deficit <= DeficitMax && holeD is null;
        anyStalemate |= stalemate;
        lines.Add($"  w{i}: app {approaches} {shape}/{width}c spawn {spawnD:0} front {frontD:0} "
            + $"deficit {deficit:+0;-0;0} hole {(holeD is { } h ? $"{h:0}" : "none")}{(stalemate ? "  ← STALEMATE" : "")}");
    }
    if (anyStalemate) flagged.Add(name);
    Console.WriteLine($"{name}:");
    foreach (var l in lines) Console.WriteLine(l);
}

// corpus mode
foreach (var (label, players) in new[] { ("small", 6), ("mid", 8), ("big", 12), ("huge", 20), ("giant", 30) })
    for (var seed = 0; seed < 16; seed++)
    {
        try { Examine($"corpus {label} s{seed}", Composer.ComposeBoxStages(new ComposeRequest(players, seed: (ulong)seed)).Plan); }
        catch (ComposeException) { }
    }

// preset mode
foreach (var (label, players, land) in new[]
    { ("small", 6, 700.0), ("mid", 8, 1600.0), ("big", 12, 2800.0), ("huge", 20, 3800.0), ("giant", 30, 6000.0) })
{
    var env = new ComposeEnvelope("mirror_z", Teams: 2, players, Cell: 5, Surface: 9, Headroom: 11,
        BoardWidthBlocks: 300, BoardLengthBlocks: 300, land, UnitMinX: 0, UnitMinZ: 0, UnitMaxX: 60, UnitMaxZ: 60);
    var crossing = MidCarver.BandOnly(env);
    for (var seed = 0; seed < 16; seed++)
    {
        if (TeamUnitAllocator.Allocate(env, new ComposeRng((ulong)seed), crossing) is not { } a) continue;
        if (TeamUnitFiller.Fill(a.Partition, a.SpawnFacing, new ComposeRng((ulong)seed)) is not { } filled) continue;
        if (MidCarver.TryCarve(env, new ComposeRng((ulong)seed), crossing, filled.Unit, flushOnly: true) is not { } mid) continue;
        var plan = new PlanModel
        {
            Meta = new PlanMeta { Name = $"{label} s{seed}" },
            Globals = new PlanGlobals { Cell = 5, Symmetry = "mirror_z", MaxPlayers = players, Surface = 9, Headroom = 11 },
        };
        foreach (var piece in filled.Unit.Pieces)
            plan.Pieces.Add(new PlanPiece { Id = piece.Id, Role = piece.Role, Rect = piece.Rect });
        plan.Zones.Add(new PlanZone { Id = "mid-band", Rect = mid.BandRect });
        plan.Placements.Spawns.Add(new SpawnPlacement
        { Piece = filled.Unit.Spawn.Piece, At = filled.Unit.Spawn.At, Facing = filled.Unit.Spawn.Facing });
        foreach (var wool in filled.Unit.Wools)
            plan.Placements.Wools.Add(new WoolPlacement { Piece = wool.Piece, At = wool.At });
        Examine($"preset {label} s{seed}", plan);
    }
}

Console.WriteLine($"== {boards} boards examined; STALEMATE flagged on {flagged.Count}: {string.Join(", ", flagged)}");
