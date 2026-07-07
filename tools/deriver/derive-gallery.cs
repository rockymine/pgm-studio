#:project ../../src/PgmStudio.Pgm/PgmStudio.Pgm.csproj
#:property JsonSerializerIsReflectionEnabledByDefault=true
// build-cache bust: deriver v1
// The layout DERIVER, first cut (docs/contracts/layout-evaluator.md §5). Reads the authored seed corpus
// (tools/seeds/*.plan.json), fans each to the full board in CELL space, and computes structure from geometry +
// markers: islands + anchor role (team/objective/neutral/decorative), stepping stones (neutral/team), wool
// lanes (the terrain stacked from a wool room's redstone interface), residual (whatever terrain remains), per-
// wool approach count (arms at the room), the frontline EDGE (team land facing a build zone), the intra-team /
// self bridges, and enclosed voids classified by position (encased/gap/frontline/middle) + declared/undeclared.
// Renders one annotated card per seed to a self-contained gallery so the author can eyeball whether the
// deriver's reading matches theirs — its disagreements are the cutoff test set (§5.4). The undeclared voids
// are the buffer worklist. Writes tools/deriver/out/derive-gallery.html.
using System.Globalization;
using System.Text;
using PgmStudio.Geom;
using PgmStudio.Pgm.Plan;

const string BgCanvas = "#080f1a";
const string AxisCol = "#a78bfa";
const string CWoolRoom = "#3fae74";   // AUTHORED wool-room piece (editor green) — intent, not derived
const string CSpawnRole = "#8f7bd6";  // AUTHORED spawn piece (editor purple) — intent, not derived
const string CResidual = "#5b6b7a";   // DERIVED residual — terrain not claimed by any specific label (dark slate)
const string CStoneNeutral = "#78716c"; // DERIVED neutral stepping stone — a contested island (warm stone gray)
const string CStoneTeam = "#d946ef";  // DERIVED team stepping stone — captive, on a spawn<->wool route (fuchsia)
const string CBuild = "#3b82f6";      // build zone (fallback accent)
// build-zone kinds — typed by what the zone links
const string CZFrontFront = "#3b82f6";   // front<->front — the crossing / direct team link (blue)
const string CZFrontNeut = "#14b8a6";    // front<->neutral — a team's bridge toward the mid (teal)
const string CZNeutNeut = "#a855f7";     // neutral<->neutral — mid-internal link between neutral islands (violet)
const string CZIntra = "#f472b6";        // intra — a team's own isolation cut (pink, matches the bridge edge)
const string CZSelf = "#22d3ee";         // self — a one-island notch (cyan, matches the self edge)
const string CFront = "#f59e0b";      // frontline edge (amber)
const string CIntra = "#f472b6";      // intra-team spawn<->wool interface / isolation-cut bridge (pink)
const string CSelf = "#22d3ee";       // self-bridge — a notch within ONE island (cyan, dotted)
// enclosed-void (hole) classes, interior → contested spectrum
const string CHoleEncased = "#818cf8";   // one team's terrain, no build — a bubble deep inside a landmass (indigo)
const string CHoleGap = "#f472b6";       // one team, intra/self build — a team's isolation-cut gap (pink)
const string CHoleFront = "#fbbf24";     // one team touching frontline build — the team's exposed edge (amber)
const string CHoleMiddle = "#ef4444";    // >=2 teams / pure build — the contested crossing arena (red)
const string CWoolLane = "#f97316";   // wool lane — the terrain the wool room stacks into (orange, a solid label)
const string CRedstone = "#ff2d2d";   // the wool-room interface (redstone line the generator stamps)
const string MkWool = "#e6e6e6";
const string MkSpawn = "#e0b13c";
const string MkStroke = "#222222";
var RoleInk = new Dictionary<string, string> { ["team"] = "#93c5fd", ["objective"] = "#6ee7b7", ["neutral"] = "#fcd34d", ["decorative"] = "#94a3b8" };

var files = Directory.EnumerateFiles(Path.Combine("tools", "seeds"), "*.plan.json").OrderBy(p => p, StringComparer.Ordinal).ToList();
int cards = 0;
var sb = new StringBuilder();
var failures = new List<string>();

foreach (var path in files)
{
    var name = Path.GetFileName(path)[..^".plan.json".Length];
    try
    {
        var plan = PlanModel.Parse(File.ReadAllText(path))!;
        var d = Derive(plan);
        sb.Append(Card(name, plan, d));
        cards++;
        var roleCounts = string.Join(",", d.Roles.GroupBy(r => r).OrderBy(g => g.Key).Select(g => $"{g.Count()}{g.Key[..1]}"));
        var apps = string.Join("/", d.Approaches.Select(a => a.Count).OrderByDescending(x => x));
        var voidSizes = string.Join(",", d.Voids.Where(v => !v.Declared).Select(v => v.Cells.Count).OrderByDescending(x => x));
        var holes = string.Join(",", d.Voids.GroupBy(v => v.Class).OrderBy(g => g.Key).Select(g => $"{g.Count()}{g.Key[..1]}"));
        int nStone = d.SteppingKind.Count(k => k == "neutral"), tStone = d.SteppingKind.Count(k => k == "team");
        var zoneStr = string.Join(",", d.Zones.GroupBy(z => z.Kind).OrderBy(g => g.Key).Select(g => $"{g.Count()}{g.Key}"));
        var pw = string.Join("/", d.Voids.Where(v => v.Class == "middle" && v.CrossRoutes > 0).Select(v => v.CrossRoutes).OrderByDescending(x => x));
        Console.WriteLine($"  {name,-42} mid={d.MidForm,-11} zones=[{zoneStr}]  stones={nStone}n/{tStone}t  woolLane={d.LaneCells.Count}  approaches={apps}  holes=[{holes}]{(pw.Length > 0 ? $" parallelWays={pw}" : "")}");
    }
    catch (Exception ex) { failures.Add($"{name}: {ex.GetType().Name}: {ex.Message}"); }
}

var html = Page(sb.ToString());
var outPath = Path.Combine("tools", "deriver", "out", "derive-gallery.html");
Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
File.WriteAllText(outPath, html);
Console.WriteLine($"wrote {outPath}");
Console.WriteLine($"cards: {cards}  failures: {failures.Count}");
foreach (var f in failures) Console.WriteLine($"  FAIL {f}");

// ── the deriver ───────────────────────────────────────────────────────────────────────────────────────────

Derived Derive(PlanModel plan)
{
    int order = Symmetry.Order(plan.Globals.Symmetry);
    string[] axes = Symmetry.OrbitAxes(plan.Globals.Symmetry);
    var roleOf = plan.Pieces.ToDictionary(p => p.Id, p => p.Role);        // authored piece role (wool-room/spawn carry intent)

    var filled = new Dictionary<(int, int), (string PieceId, int K)>();   // generating-piece cells → hosting piece image
    var build = new HashSet<(int, int)>();                                // buildable (zone) cells
    var declaredVoid = new HashSet<(int, int)>();                         // buffer pieces + zone holes = declared empties

    foreach (var p in plan.Pieces)
        foreach (var c in FanCells(p.Rect, axes, order))
        {
            if (p.Role == PlanRoles.Buffer) declaredVoid.Add(c);
            else if (p.Role == PlanRoles.Connector) { /* annotation: no terrain */ }
            else filled[c] = (p.Id, 0);   // K unused post-fan; kept for clarity
        }
    // re-tag with image K so marker hosting resolves per image
    filled.Clear();
    foreach (var p in plan.Pieces)
    {
        if (p.Role is PlanRoles.Buffer or PlanRoles.Connector) continue;
        for (var k = 0; k < order; k++)
            foreach (var c in FanCellsK(p.Rect, axes, k)) filled[c] = (p.Id, k);
    }
    foreach (var z in plan.Zones)
    {
        foreach (var c in FanCells(z.Rect, axes, order)) build.Add(c);
        foreach (var h in z.Holes) foreach (var c in FanCells(h, axes, order)) declaredVoid.Add(c);
    }

    // islands — 4-connected components of filled cells
    var islandOf = new Dictionary<(int, int), int>();
    var islands = new List<HashSet<(int, int)>>();
    foreach (var start in filled.Keys)
    {
        if (islandOf.ContainsKey(start)) continue;
        var comp = new HashSet<(int, int)>();
        var q = new Queue<(int, int)>();
        q.Enqueue(start); islandOf[start] = islands.Count;
        while (q.Count > 0)
        {
            var cur = q.Dequeue(); comp.Add(cur);
            foreach (var nb in N4(cur))
                if (filled.ContainsKey(nb) && !islandOf.ContainsKey(nb)) { islandOf[nb] = islands.Count; q.Enqueue(nb); }
        }
        islands.Add(comp);
    }

    // anchor role per island — team (spawn) / objective (wool, no spawn) / neutral (in build) / decorative
    var spawnKeys = new HashSet<(string, int)>();
    var woolKeys = new HashSet<(string, int)>();
    for (var k = 0; k < order; k++)
    {
        foreach (var s in plan.Placements.Spawns) spawnKeys.Add((s.Piece, k));
        foreach (var w in plan.Placements.Wools) woolKeys.Add((w.Piece, k));
    }
    var roles = new string[islands.Count];
    for (var i = 0; i < islands.Count; i++)
    {
        // the authored wool-room / spawn ROLE is the strongest intent signal — use it alongside the markers
        bool hasSpawn = islands[i].Any(c => spawnKeys.Contains(filled[c]) || roleOf[filled[c].PieceId] == PlanRoles.Spawn),
             hasWool = islands[i].Any(c => woolKeys.Contains(filled[c]) || roleOf[filled[c].PieceId] == PlanRoles.WoolRoom),
             touchesBuild = islands[i].Any(c => N4(c).Any(build.Contains) || build.Contains(c));
        roles[i] = hasSpawn ? "team" : hasWool ? "objective" : touchesBuild ? "neutral" : "decorative";
    }

    // per-wool approach count — arms (connected filled clusters adjacent to the room) touching the wool's piece
    var approaches = new List<(int Island, double Bx, double Bz, int Count)>();
    for (var k = 0; k < order; k++)
        foreach (var w in plan.Placements.Wools)
        {
            var piece = plan.Pieces.FirstOrDefault(p => p.Id == w.Piece);
            if (piece is null) continue;
            var room = FanCellsK(piece.Rect, axes, k).Where(filled.ContainsKey).ToHashSet();
            if (room.Count == 0) continue;
            int isl = islandOf[room.First()];
            var arm = new HashSet<(int, int)>();
            foreach (var c in room) foreach (var nb in N4(c)) if (filled.ContainsKey(nb) && !room.Contains(nb)) arm.Add(nb);
            approaches.Add((isl, MarkerBlock(piece.Rect, w.At, k, axes).X, MarkerBlock(piece.Rect, w.At, k, axes).Z, ComponentCount(arm)));
        }

    // frontline edges — a void-facing OUTSIDE edge (buildable AND empty neighbour; never an interior seam). An
    // island has a frontline only if its border is VOID-DOMINANT (more void-border than build-border) — it is
    // exposed territory whose build-facing edges are the frontline. A build-dominant island is embedded in the
    // crossing (mostly surrounded by build, e.g. a mid stone sitting in the band) and a pure-void island is
    // floating — both are STEPPING STONES with no frontline. This drops the base-2island 2x2 stones (6 build /
    // 2 void) while keeping the isolated-wool island (4 build / 22 void) and the team territory.
    var islBuild = new int[islands.Count];
    var islVoid = new int[islands.Count];
    foreach (var c in filled.Keys)
    {
        int isl = islandOf[c];
        foreach (var nb in N4(c))
        {
            if (filled.ContainsKey(nb)) continue;
            if (build.Contains(nb)) islBuild[isl]++;
            else islVoid[isl]++;
        }
    }

    // intra-team bridges — a build region (connected empty buildable cells) that is part of a team's own
    // internal spawn<->wool route across a deliberate gap. Direct case: a region touching only that team's
    // spawn island and wool island. Chain case: spawn <-> stepping stone <-> wool, where the stepping stone
    // is CAPTIVE to the team (every region touching it stays single-team — no enemy can reach it). A neutral
    // island that any second team can also reach (a contested middle island / tower) is NOT captive, so a
    // region bridging out to it stays a frontline, not an intra interface. The edges these regions present
    // are intra-team interfaces — the deliberate internal gap where a piece was chopped off and bridged back
    // to slow attackers (the isolation cut), a learnable pattern for the builder.
    var regionOf = new Dictionary<(int, int), int>();
    var regionCount = 0;
    foreach (var b in build)
    {
        if (filled.ContainsKey(b) || regionOf.ContainsKey(b)) continue;   // buildVoid cells only
        var q = new Queue<(int, int)>(); q.Enqueue(b); regionOf[b] = regionCount;
        while (q.Count > 0)
        {
            var cur = q.Dequeue();
            foreach (var nb in N4(cur))
                if (build.Contains(nb) && !filled.ContainsKey(nb) && !regionOf.ContainsKey(nb)) { regionOf[nb] = regionCount; q.Enqueue(nb); }
        }
        regionCount++;
    }
    var regionIslands = Enumerable.Range(0, regionCount).Select(_ => new HashSet<int>()).ToArray();
    foreach (var (cell, r) in regionOf)
        foreach (var nb in N4(cell)) if (filled.ContainsKey(nb)) regionIslands[r].Add(islandOf[nb]);

    var islandTeam = new int[islands.Count];       // orbit image k == team index
    var hasSpawnI = new bool[islands.Count];
    var hasWoolI = new bool[islands.Count];
    for (var i = 0; i < islands.Count; i++)
    {
        islandTeam[i] = filled[islands[i].First()].K;
        hasSpawnI[i] = islands[i].Any(c => spawnKeys.Contains(filled[c]) || roleOf[filled[c].PieceId] == PlanRoles.Spawn);
        hasWoolI[i] = islands[i].Any(c => woolKeys.Contains(filled[c]) || roleOf[filled[c].PieceId] == PlanRoles.WoolRoom);
    }

    // a region is single-team if every island it touches belongs to one team
    var singleTeam = new bool[regionCount];
    for (var r = 0; r < regionCount; r++)
        singleTeam[r] = regionIslands[r].Count > 0 && regionIslands[r].Select(i => islandTeam[i]).Distinct().Count() == 1;

    // an island is CAPTIVE if every build region touching it is single-team — no enemy team can reach it.
    // A spawn/wool anchor is always a valid route endpoint even when it also faces a shared (multi-team)
    // region (a spawn fronting the mid band). Only a captive island may be a pass-through stepping stone.
    var islandRegions = Enumerable.Range(0, islands.Count).Select(_ => new List<int>()).ToArray();
    for (var r = 0; r < regionCount; r++) foreach (var i in regionIslands[r]) islandRegions[i].Add(r);
    var captive = new bool[islands.Count];
    for (var i = 0; i < islands.Count; i++) captive[i] = islandRegions[i].All(r => singleTeam[r]);
    var nodeIn = new bool[islands.Count];            // islands eligible to lie on an internal route
    for (var i = 0; i < islands.Count; i++) nodeIn[i] = captive[i] || hasSpawnI[i] || hasWoolI[i];

    // union-find over the route-eligible islands, joined by single-team regions — each component is one
    // team's internal reachability. A region is an intra bridge iff it stays inside this subgraph (touches
    // >=2 eligible islands) and its component holds both a spawn and a wool (it is ON a spawn<->wool route).
    var uf = Enumerable.Range(0, islands.Count).ToArray();
    int Find(int x) { while (uf[x] != x) { uf[x] = uf[uf[x]]; x = uf[x]; } return x; }
    void Union(int a, int b) { uf[Find(a)] = Find(b); }
    for (var r = 0; r < regionCount; r++)
    {
        if (!singleTeam[r]) continue;
        var elig = regionIslands[r].Where(i => nodeIn[i]).ToList();
        for (var t = 1; t < elig.Count; t++) Union(elig[0], elig[t]);
    }
    var compSpawn = new HashSet<int>();
    var compWool = new HashSet<int>();
    for (var i = 0; i < islands.Count; i++)
    {
        if (!nodeIn[i]) continue;
        if (hasSpawnI[i]) compSpawn.Add(Find(i));
        if (hasWoolI[i]) compWool.Add(Find(i));
    }
    var intraTeam = new bool[regionCount];
    var selfBridge = new bool[regionCount];
    for (var r = 0; r < regionCount; r++)
    {
        if (!singleTeam[r] || regionIslands[r].Count == 0) continue;
        // it must stay INSIDE the team's own reachable subgraph — every island it touches is route-eligible.
        // A region that also touches a non-eligible island bridges OUT to contested territory (a tower a second
        // team reaches) and stays a frontline. What remains is either a two-piece bridge or a SELF-BRIDGE: a
        // notch touching only the team's own island, its two walls the same landmass (mirror-big-board's spawn).
        if (!regionIslands[r].All(i => nodeIn[i])) continue;
        int root = Find(regionIslands[r].First());
        if (!(compSpawn.Contains(root) && compWool.Contains(root))) continue;   // must be on the team's spawn<->wool route
        intraTeam[r] = true;
        if (regionIslands[r].Count == 1) selfBridge[r] = true;   // a notch within one island, not a two-piece gap
    }

    // stepping stones — an anchorless island (no spawn, no wool). Split by reachability: a TEAM stepping stone
    // is CAPTIVE (every region touching it is single-team) and sits on that team's own spawn<->wool route (its
    // component holds both a spawn and a wool) — a movement stone the attackers can't flank; a NEUTRAL stepping
    // stone is a contested middle island a second team can also reach. These are islands, not branch/residual.
    var steppingKind = new string[islands.Count];
    for (var i = 0; i < islands.Count; i++)
    {
        if (hasSpawnI[i] || hasWoolI[i]) { steppingKind[i] = ""; continue; }   // anchored — not a stepping stone
        int root = Find(i);
        steppingKind[i] = captive[i] && compSpawn.Contains(root) && compWool.Contains(root) ? "team" : "neutral";
    }

    // build-ZONE kinds — a build region typed by WHAT it links (read straight off the island incidence). A
    // team-owned island (anchored spawn/wool, or a captive team stone) contributes a team "frontline" endpoint;
    // a neutral stepping stone contributes a neutral endpoint. self / intra are the same-team internal cuts
    // (already found); the rest split by how many teams and whether a neutral sits at an endpoint:
    //   front-front     — >=2 teams: the crossing / direct team link (may carry neutral stones sitting between).
    //   front-neutral   — one team + a neutral: a team's bridge toward the mid.
    //   neutral-neutral — only neutrals: a mid-internal link between neutral islands (often crosses the axis).
    bool TeamOwned(int i) => hasSpawnI[i] || hasWoolI[i] || steppingKind[i] == "team";
    var zoneKind = new string[regionCount];
    var zoneNeutral = new int[regionCount];   // neutral stones sitting inside the zone (CT5 fragmentation, per zone)
    for (var r = 0; r < regionCount; r++)
    {
        zoneNeutral[r] = regionIslands[r].Count(i => steppingKind[i] == "neutral");
        var teams = regionIslands[r].Where(TeamOwned).Select(i => islandTeam[i]).Distinct().Count();
        bool hasNeutral = zoneNeutral[r] > 0;
        zoneKind[r] = selfBridge[r] ? "self" : intraTeam[r] ? "intra"
            : teams >= 2 ? "front-front"
            : teams == 1 ? (hasNeutral ? "front-neutral" : "front-solo")
            : hasNeutral ? "neutral-neutral" : "empty";
    }
    var buildKindOf = new Dictionary<(int, int), string>();
    foreach (var (cell, r) in regionOf) buildKindOf[cell] = zoneKind[r];

    // CT mid-form (derived, not authored) — from the zone grammar: any neutral↔neutral zone means the mid is
    // fractured into interlinked islands → HASH; else >=2 separate crossings → PARALLEL; a single crossing →
    // CHANNELLED. (layout-rules.md CT, read off the closure.)
    int nnZones = zoneKind.Count(k => k == "neutral-neutral");
    int ffZones = zoneKind.Count(k => k == "front-front");
    string midForm = nnZones > 0 ? "hash" : ffZones >= 2 ? "parallel" : ffZones == 1 ? "channelled" : "—";

    // wool lanes — the wool room's approach, read off the terrain shape. The room INTERFACES with terrain along
    // an edge (where the generator stamps the objective's redstone line); STACK that interface straight outward,
    // a fixed-width band, cell by cell, until void, build, or a T — a crossbar (terrain reaching beyond the band
    // on BOTH sides; a one-sided jut is just a side branch and does not stop it). A two-sided room stacks both
    // ways. If the forward stack immediately dead-ends into void (the room is docked against the SIDE of a lane,
    // an L not an I), stack instead along that lane's own axis (perpendicular) until void/build/T — so the whole
    // I the room docks against becomes the lane. ONLY wools stack a lane — never spawns.
    var laneCells = new HashSet<(int, int)>();
    var redstoneEdges = new List<(int X1, int Z1, int X2, int Z2)>();
    bool LaneTerr((int, int) c, HashSet<(int, int)> room) => filled.ContainsKey(c) && !room.Contains(c);
    // stack a band (a line of cells perpendicular to dir) outward; return the terrain cells added, the stop
    // reason, and how many rows advanced. Stops at void/build or a crossbar (terrain beyond both band ends).
    (List<(int, int)> Cells, string Reason, int Len) StackBand(List<(int, int)> band, (int dx, int dz) dir, HashSet<(int, int)> room)
    {
        var added = new List<(int, int)>();
        (int cx, int cz) cross = dir.dz != 0 ? (1, 0) : (0, 1);
        string reason = "void"; int len = 0;
        for (var step = 0; ; step++)
        {
            var row = band.Select(c => (c.Item1 + dir.dx * step, c.Item2 + dir.dz * step)).ToList();
            var terr = row.Where(c => LaneTerr(c, room)).ToList();
            if (terr.Count == 0) { reason = row.Any(build.Contains) ? "build" : "void"; break; }
            double Key((int, int) c) => c.Item1 * cross.cx + c.Item2 * cross.cz;
            var lo = row.OrderBy(Key).First(); var hi = row.OrderBy(Key).Last();
            var bLo = (lo.Item1 - cross.cx, lo.Item2 - cross.cz); var bHi = (hi.Item1 + cross.cx, hi.Item2 + cross.cz);
            if (LaneTerr(bLo, room) && LaneTerr(bHi, room)) { reason = "crossbar"; break; }   // a T-bar crosses → stop
            added.AddRange(terr); len++;
        }
        return (added, reason, len);
    }
    foreach (var w in plan.Placements.Wools)
    {
        var pc = plan.Pieces.FirstOrDefault(p => p.Id == w.Piece);
        if (pc is null) continue;
        for (var k = 0; k < order; k++)
        {
            var room = FanCellsK(pc.Rect, axes, k).ToHashSet();
            if (room.Count == 0) continue;
            int rminx = room.Min(c => c.Item1), rmaxx = room.Max(c => c.Item1);
            int rminz = room.Min(c => c.Item2), rmaxz = room.Max(c => c.Item2);
            var lane = new HashSet<(int, int)>();
            foreach (var (dx, dz) in new[] { (0, 1), (0, -1), (1, 0), (-1, 0) })
            {
                List<(int, int)> band; (int, int) edge;
                if (dz != 0)
                {
                    int z0 = dz > 0 ? rmaxz + 1 : rminz - 1, zEdge = dz > 0 ? rmaxz + 1 : rminz;
                    band = Enumerable.Range(rminx, rmaxx - rminx + 1).Select(x => (x, z0)).ToList();
                    edge = (0, zEdge);
                }
                else
                {
                    int x0 = dx > 0 ? rmaxx + 1 : rminx - 1, xEdge = dx > 0 ? rmaxx + 1 : rminx;
                    band = Enumerable.Range(rminz, rmaxz - rminz + 1).Select(z => (x0, z)).ToList();
                    edge = (xEdge, 0);
                }
                if (!band.Any(c => LaneTerr(c, room))) continue;   // this side faces no terrain
                foreach (var c in band.Where(c => LaneTerr(c, room)))
                    redstoneEdges.Add(dz != 0 ? (c.Item1, edge.Item2, c.Item1 + 1, edge.Item2)
                                              : (edge.Item1, c.Item2, edge.Item1, c.Item2 + 1));
                var (fwd, reason, len) = StackBand(band, (dx, dz), room);
                foreach (var c in fwd) lane.Add(c);
                // side-dock (L): the forward stack dead-ended into void after ~a bar's thickness — the room is
                // docked against the side of a lane running perpendicular. Stack that lane along its own axis.
                if (reason == "void" && len <= 2 && fwd.Count > 0)
                {
                    (int cx, int cz) cross = dz != 0 ? (1, 0) : (0, 1);
                    double Key((int, int) c) => c.Item1 * cross.cx + c.Item2 * cross.cz;
                    var loFace = fwd.Where(c => Key(c) == fwd.Min(Key)).ToList();
                    var hiFace = fwd.Where(c => Key(c) == fwd.Max(Key)).ToList();
                    foreach (var c in StackBand(loFace, (-cross.cx, -cross.cz), room).Cells) lane.Add(c);
                    foreach (var c in StackBand(hiFace, (cross.cx, cross.cz), room).Cells) lane.Add(c);
                }
            }
            laneCells.UnionWith(lane);
        }
    }

    // frontline edges + the intra-team interfaces kept as their OWN derived signal: they mark where the author
    // built a deliberate internal gap — a piece chopped off the main mass and bridged back across a slow-down
    // void (the CT5 isolation cut). A learnable pattern for the builder, not just an exclusion.
    var frontEdges = new List<(int X1, int Z1, int X2, int Z2)>();
    var intraEdges = new List<(int X1, int Z1, int X2, int Z2)>();
    var selfEdges = new List<(int X1, int Z1, int X2, int Z2)>();
    foreach (var c in filled.Keys)
    {
        int isl = islandOf[c];
        bool voidDom = islVoid[isl] > islBuild[isl];   // exposed territory (else a stepping stone)
        foreach (var (nb, seg) in N4Seg(c))
        {
            if (!(build.Contains(nb) && !filled.ContainsKey(nb))) continue;
            // an intra-team bridge is marked wherever it appears — a build-dominant stepping stone that only
            // connects a team's own pieces is part of that team's internal lane, not a frontline. Frontline
            // edges are still gated to void-dominant islands (exposed territory facing a shared void). A
            // self-bridge (region touching only this island) is split off as its own signal: a notch, not a gap.
            if (regionOf.TryGetValue(nb, out var r) && intraTeam[r]) (selfBridge[r] ? selfEdges : intraEdges).Add(seg);
            else if (voidDom) frontEdges.Add(seg);
        }
    }

    // enclosed voids — a hole is TRUE void (empty terrain, non-buildable) that the border can't reach without
    // crossing terrain OR a build region: both terrain and build are walls for this flood. That is what lets a
    // rotation pocket ("rotary device") near the frontline — walled by twin frontlines on some sides and the
    // mid build band on the others — register as enclosed, instead of leaking to the border through the band.
    // Declared when the pocket overlaps a buffer / zone-hole; else an undeclared void (the buffer worklist).
    var all = filled.Keys.Concat(build).Concat(declaredVoid).ToList();
    int minX = all.Min(c => c.Item1) - 1, maxX = all.Max(c => c.Item1) + 1;
    int minZ = all.Min(c => c.Item2) - 1, maxZ = all.Max(c => c.Item2) + 1;
    bool TrueVoid((int, int) c) => !filled.ContainsKey(c) && !build.Contains(c)
        && c.Item1 >= minX && c.Item1 <= maxX && c.Item2 >= minZ && c.Item2 <= maxZ;
    var outside = new HashSet<(int, int)>();
    var oq = new Queue<(int, int)>();
    for (var x = minX; x <= maxX; x++) foreach (var c in new[] { (x, minZ), (x, maxZ) }) if (TrueVoid(c) && outside.Add(c)) oq.Enqueue(c);
    for (var z = minZ; z <= maxZ; z++) foreach (var c in new[] { (minX, z), (maxX, z) }) if (TrueVoid(c) && outside.Add(c)) oq.Enqueue(c);
    while (oq.Count > 0) { var cur = oq.Dequeue(); foreach (var nb in N4(cur)) if (TrueVoid(nb) && outside.Add(nb)) oq.Enqueue(nb); }

    // a hole is classified by WHAT its boundary touches (never by size — the corpus is ground truth). The
    // discriminators are all already derived: how many teams the boundary reaches (terrain ownership first),
    // and whether the build it touches is a frontline (the contested crossing) or intra/self (a team's own
    // isolation-cut gap). This places a hole on the interior→contested spectrum:
    //   encased   — one team's terrain, no build boundary: a bubble deep inside a team's landmass.
    //   gap       — one team, build boundary all intra/self: a hole in the team's own isolation-cut gap.
    //   frontline — one team's terrain but touching frontline build: the team's exposed edge on the crossing.
    //   middle    — reaches >=2 teams, or floats in pure build: the contested crossing / arena.
    var voids = new List<(HashSet<(int, int)> Cells, bool Declared, string Class, int CrossRoutes)>();
    var seenVoid = new HashSet<(int, int)>();
    for (var x = minX; x <= maxX; x++)
        for (var z = minZ; z <= maxZ; z++)
        {
            var s = (x, z);
            if (!TrueVoid(s) || outside.Contains(s) || seenVoid.Contains(s)) continue;
            var comp = new HashSet<(int, int)>(); var q = new Queue<(int, int)>(); q.Enqueue(s); seenVoid.Add(s);
            while (q.Count > 0) { var cur = q.Dequeue(); comp.Add(cur); foreach (var nb in N4(cur)) if (TrueVoid(nb) && !outside.Contains(nb) && seenVoid.Add(nb)) q.Enqueue(nb); }
            bool declared = comp.Any(declaredVoid.Contains);   // a buffer / zone-hole marks this pocket deliberate
            // report EVERY enclosed void, any size — the authored seeds are ground truth, and they carry
            // intended holes as small as 1x2 cells (mirror-tiny-map-cliff, rotate-wide-frontline). Never let a
            // size rule override the corpus.
            var terrTeams = new HashSet<int>(); var buildTeams = new HashSet<int>();
            bool touchesBuild = false, touchesFrontline = false;
            var borderRegions = new HashSet<int>();   // distinct build zones ringing the hole
            foreach (var c in comp)
                foreach (var nb in N4(c))
                {
                    if (comp.Contains(nb)) continue;
                    if (filled.ContainsKey(nb))
                    {
                        // only ANCHORED terrain (spawn/wool) confers team ownership — a neutral stepping stone
                        // has no real team (its orbit-image label is arbitrary, and a centre island shared by
                        // both images carries a single fixed value), which would otherwise break mirror symmetry.
                        int isl = islandOf[nb];
                        if (hasSpawnI[isl] || hasWoolI[isl]) terrTeams.Add(islandTeam[isl]);
                    }
                    else if (build.Contains(nb) && regionOf.TryGetValue(nb, out var r))
                    {
                        touchesBuild = true;
                        borderRegions.Add(r);
                        if (!intraTeam[r]) touchesFrontline = true;   // a frontline (non-intra) build region
                        foreach (var i in regionIslands[r]) buildTeams.Add(islandTeam[i]);
                    }
                }
            bool contested = terrTeams.Count >= 2
                || (terrTeams.Count == 0 && (touchesFrontline || buildTeams.Count >= 2));
            string cls = contested ? "middle"
                : !touchesBuild ? "encased"
                : touchesFrontline ? "frontline"
                : "gap";
            // routes around the hole — distinct CROSSING zones (front-front / neutral-neutral) ringing it. On a
            // middle hole this is the "parallel ways": big-board's central hole has two front-front crossings
            // flanking it (2 parallel routes), four-team-towers' centre is ringed by four neutral-neutral links.
            int crossRoutes = borderRegions.Count(r => zoneKind[r] is "front-front" or "neutral-neutral");
            voids.Add((comp, declared, cls, crossRoutes));
        }

    var zones = Enumerable.Range(0, regionCount).Select(r => (Kind: zoneKind[r], Neutrals: zoneNeutral[r])).ToList();
    return new Derived(plan.Globals.Cell, filled, build, buildKindOf, zones, midForm, islands, islandOf, roles, steppingKind, approaches, frontEdges, intraEdges, selfEdges, laneCells, redstoneEdges, voids);
}

// ── geometry helpers ────────────────────────────────────────────────────────────────────────────────────────

IEnumerable<(int, int)> N4((int, int) c) { yield return (c.Item1 + 1, c.Item2); yield return (c.Item1 - 1, c.Item2); yield return (c.Item1, c.Item2 + 1); yield return (c.Item1, c.Item2 - 1); }

// neighbour + the shared cell-edge segment (in CELL units) between c and that neighbour
IEnumerable<((int, int) Nb, (int, int, int, int) Seg)> N4Seg((int, int) c)
{
    int x = c.Item1, z = c.Item2;
    yield return ((x + 1, z), (x + 1, z, x + 1, z + 1));
    yield return ((x - 1, z), (x, z, x, z + 1));
    yield return ((x, z + 1), (x, z + 1, x + 1, z + 1));
    yield return ((x, z - 1), (x, z, x + 1, z));
}

int ComponentCount(HashSet<(int, int)> cells)
{
    var seen = new HashSet<(int, int)>(); int n = 0;
    foreach (var s in cells)
    {
        if (!seen.Add(s)) continue; n++;
        var q = new Queue<(int, int)>(); q.Enqueue(s);
        while (q.Count > 0) { var cur = q.Dequeue(); foreach (var nb in N4(cur)) if (cells.Contains(nb) && seen.Add(nb)) q.Enqueue(nb); }
    }
    return n;
}

// fan a cell rect to EVERY orbit image, yielding all cells
IEnumerable<(int, int)> FanCells(int[] rect, string[] axes, int order)
{
    for (var k = 0; k < order; k++) foreach (var c in FanCellsK(rect, axes, k)) yield return c;
}

// fan a cell rect to the k-th orbit image (identity at k=0), yielding its cells
IEnumerable<(int, int)> FanCellsK(int[] rect, string[] axes, int k)
{
    int x = rect[0], z = rect[1], w = rect[2], h = rect[3];
    int x1, z1, x2, z2;
    if (k == 0) { x1 = x; z1 = z; x2 = x + w; z2 = z + h; }
    else
    {
        var axis = axes[k - 1];
        var pts = new[] { (x, z), (x + w, z), (x, z + h), (x + w, z + h) }
            .Select(p => Symmetry.Apply(p.Item1, p.Item2, axis, 0, 0)).ToList();
        x1 = (int)Math.Round(pts.Min(p => p.X)); z1 = (int)Math.Round(pts.Min(p => p.Z));
        x2 = (int)Math.Round(pts.Max(p => p.X)); z2 = (int)Math.Round(pts.Max(p => p.Z));
    }
    for (var cx = x1; cx < x2; cx++) for (var cz = z1; cz < z2; cz++) yield return (cx, cz);
}

// a marker's block coordinate at image k (piece origin + half-cell offset, fanned)
(double X, double Z) MarkerBlock(int[] rect, double[] at, int k, string[] axes)
{
    double cx = rect[0] + at[0], cz = rect[1] + at[1];   // in cells
    if (k > 0) { var (fx, fz) = Symmetry.Apply(cx, cz, axes[k - 1], 0, 0); cx = fx; cz = fz; }
    return (cx, cz);   // in cells; caller scales by cell
}

// ── render ────────────────────────────────────────────────────────────────────────────────────────────────

string Card(string name, PlanModel plan, Derived d)
{
    int cell = d.Cell;
    var allCells = d.Filled.Keys.Concat(d.Build).ToList();
    foreach (var v in d.Voids) allCells.AddRange(v.Cells);
    double minX = allCells.Min(c => c.Item1) * cell, minZ = allCells.Min(c => c.Item2) * cell;
    double maxX = (allCells.Max(c => c.Item1) + 1) * cell, maxZ = (allCells.Max(c => c.Item2) + 1) * cell;
    double mgn = cell * 1.2; minX -= mgn; minZ -= mgn; maxX += mgn; maxZ += mgn;
    double bw = maxX - minX, bh = maxZ - minZ;
    const double TW = 320; double s = TW / bw, vbw = bw * s, vbh = bh * s;
    double PX(double bx) => (bx - minX) * s;
    double PY(double bz) => (bz - minZ) * s;

    var svg = new StringBuilder();
    svg.Append($"<svg viewBox=\"0 0 {N(vbw)} {N(vbh)}\" xmlns=\"http://www.w3.org/2000/svg\" class=\"map\" role=\"img\">");
    svg.Append($"<rect width=\"{N(vbw)}\" height=\"{N(vbh)}\" fill=\"{BgCanvas}\"/>");

    void CellRect(int cx, int cz, string fill, double fo, string stroke, double sw) =>
        svg.Append($"<rect x=\"{N(PX(cx * cell))}\" y=\"{N(PY(cz * cell))}\" width=\"{N(cell * s)}\" height=\"{N(cell * s)}\" fill=\"{fill}\" fill-opacity=\"{N(fo)}\" stroke=\"{stroke}\" stroke-width=\"{N(sw)}\"/>");

    // build zones (under terrain) — tinted by ZONE KIND (what the zone links)
    string ZoneCol(string k) => k switch {
        "front-front" => CZFrontFront, "front-neutral" => CZFrontNeut, "neutral-neutral" => CZNeutNeut,
        "intra" => CZIntra, "self" => CZSelf, _ => CBuild };
    foreach (var c in d.Build)
    {
        var zc = d.BuildKindOf.TryGetValue(c, out var k) ? ZoneCol(k) : CBuild;
        CellRect(c.Item1, c.Item2, zc, 0.18, zc, 0.5);
    }
    // terrain cells — every tile gets exactly ONE label, by priority: AUTHORED wool-room (green) / spawn
    // (purple) keep their editor colour; a STEPPING-STONE island is coloured whole (neutral stone-gray / team
    // fuchsia); a WOOL-LANE tile is the wool room's approach (orange, a solid label, not an overlay); everything
    // that remains is RESIDUAL (dark slate) — no erosion split, residual is simply the unclaimed terrain.
    var roleOf = plan.Pieces.ToDictionary(p => p.Id, p => p.Role);
    foreach (var c in d.Filled.Keys)
    {
        var role = roleOf[d.Filled[c].PieceId];
        var kind = d.SteppingKind[d.IslandOf[c]];
        string fill = role == PlanRoles.WoolRoom ? CWoolRoom : role == PlanRoles.Spawn ? CSpawnRole
            : kind == "team" ? CStoneTeam : kind == "neutral" ? CStoneNeutral
            : d.LaneCells.Contains(c) ? CWoolLane : CResidual;
        CellRect(c.Item1, c.Item2, fill, 0.75, BgCanvas, 0.5);
    }
    // enclosed voids — coloured by position class; undeclared (the buffer worklist) pops, declared is muted
    foreach (var (vc, isDecl, cls, _) in d.Voids)
    {
        string hc = cls == "encased" ? CHoleEncased : cls == "gap" ? CHoleGap : cls == "frontline" ? CHoleFront : CHoleMiddle;
        foreach (var c in vc) CellRect(c.Item1, c.Item2, hc, isDecl ? 0.14 : 0.30, hc, isDecl ? 0.6 : 1.1);
    }
    // axis
    svg.Append($"<line x1=\"{N(PX(0))}\" y1=\"{N(PY(minZ))}\" x2=\"{N(PX(0))}\" y2=\"{N(PY(maxZ))}\" stroke=\"{AxisCol}\" stroke-opacity=\"0.4\" stroke-width=\"1\"/>");
    svg.Append($"<line x1=\"{N(PX(minX))}\" y1=\"{N(PY(0))}\" x2=\"{N(PX(maxX))}\" y2=\"{N(PY(0))}\" stroke=\"{AxisCol}\" stroke-opacity=\"0.4\" stroke-width=\"1\"/>");
    // intra-team spawn<->wool interfaces (pink, dashed) — the deliberate internal gap / isolation-cut bridge
    foreach (var (x1, z1, x2, z2) in d.IntraEdges)
        svg.Append($"<line x1=\"{N(PX(x1 * cell))}\" y1=\"{N(PY(z1 * cell))}\" x2=\"{N(PX(x2 * cell))}\" y2=\"{N(PY(z2 * cell))}\" stroke=\"{CIntra}\" stroke-width=\"2.4\" stroke-linecap=\"round\" stroke-dasharray=\"3 2\"/>");
    // self-bridge notches (cyan, dotted) — a build pocket carved into ONE island, its two walls the same landmass
    foreach (var (x1, z1, x2, z2) in d.SelfEdges)
        svg.Append($"<line x1=\"{N(PX(x1 * cell))}\" y1=\"{N(PY(z1 * cell))}\" x2=\"{N(PX(x2 * cell))}\" y2=\"{N(PY(z2 * cell))}\" stroke=\"{CSelf}\" stroke-width=\"2.4\" stroke-linecap=\"round\" stroke-dasharray=\"1 2.6\"/>");
    // redstone interface line — the wool-room edge the stack projects from (where the generator stamps redstone)
    foreach (var (x1, z1, x2, z2) in d.RedstoneEdges)
        svg.Append($"<line x1=\"{N(PX(x1 * cell))}\" y1=\"{N(PY(z1 * cell))}\" x2=\"{N(PX(x2 * cell))}\" y2=\"{N(PY(z2 * cell))}\" stroke=\"{CRedstone}\" stroke-width=\"3\" stroke-linecap=\"round\"/>");
    // frontline edges (amber, thick, solid)
    foreach (var (x1, z1, x2, z2) in d.FrontEdges)
        svg.Append($"<line x1=\"{N(PX(x1 * cell))}\" y1=\"{N(PY(z1 * cell))}\" x2=\"{N(PX(x2 * cell))}\" y2=\"{N(PY(z2 * cell))}\" stroke=\"{CFront}\" stroke-width=\"2.4\" stroke-linecap=\"round\"/>");
    // markers + per-wool approach count
    for (var k = 0; k < Symmetry.Order(plan.Globals.Symmetry); k++)
    {
        foreach (var w in plan.Placements.Wools)
        {
            var pc = plan.Pieces.FirstOrDefault(p => p.Id == w.Piece); if (pc is null) continue;
            var (bx, bz) = MarkerBlock(pc.Rect, w.At, k, Symmetry.OrbitAxes(plan.Globals.Symmetry));
            double sq = cell * 0.5 * s;
            svg.Append($"<rect x=\"{N(PX(bx * cell) - sq / 2)}\" y=\"{N(PY(bz * cell) - sq / 2)}\" width=\"{N(sq)}\" height=\"{N(sq)}\" fill=\"{MkWool}\" stroke=\"{MkStroke}\" stroke-width=\"1\"/>");
        }
        foreach (var sp in plan.Placements.Spawns)
        {
            var pc = plan.Pieces.FirstOrDefault(p => p.Id == sp.Piece); if (pc is null) continue;
            var (bx, bz) = MarkerBlock(pc.Rect, sp.At, k, Symmetry.OrbitAxes(plan.Globals.Symmetry));
            svg.Append($"<circle cx=\"{N(PX(bx * cell))}\" cy=\"{N(PY(bz * cell))}\" r=\"{N(cell * 0.3 * s)}\" fill=\"{MkSpawn}\" stroke=\"{MkStroke}\" stroke-width=\"1\"/>");
        }
    }
    // approach-count badge beside each wool
    svg.Append($"<g font-family=\"ui-monospace, Menlo, monospace\" font-weight=\"700\" font-size=\"10\" text-anchor=\"middle\" paint-order=\"stroke\" stroke=\"{BgCanvas}\" stroke-width=\"2.4\">");
    foreach (var a in d.Approaches)
        svg.Append($"<text x=\"{N(PX(a.Bx * cell))}\" y=\"{N(PY(a.Bz * cell) - cell * 0.5 * s)}\" fill=\"{(a.Count >= 2 ? "#6ee7b7" : "#fca5a5")}\">{a.Count}×</text>");
    svg.Append("</g></svg>");

    // stats
    var byRole = d.Roles.GroupBy(r => r).ToDictionary(g => g.Key, g => g.Count());
    int undecl = d.Voids.Count(v => !v.Declared), decl = d.Voids.Count(v => v.Declared);
    int neutralStones = d.SteppingKind.Count(k => k == "neutral"), teamStones = d.SteppingKind.Count(k => k == "team");
    // anchor roles only (the anchorless islands are reported as stepping stones, not a "neutral" anchor role)
    string roleStr = string.Join(" ", new[] { "team", "objective" }.Where(byRole.ContainsKey).Select(r => $"{byRole[r]} {r}"));
    var appCounts = d.Approaches.Select(a => a.Count).ToList();
    string appStr = appCounts.Count == 0 ? "—" : string.Join("/", appCounts.OrderByDescending(x => x));
    string stat(string v, string l) => $"<span class=\"stat\"><span class=\"stat-v\">{v}</span> {l}</span>";
    string stoneStr = teamStones > 0 ? $"{neutralStones} neutral / {teamStones} team" : $"{neutralStones}";
    var holeOrder = new[] { "encased", "gap", "frontline", "middle" };
    var holeByCls = d.Voids.GroupBy(v => v.Class).ToDictionary(g => g.Key, g => g.Count());
    string holeStr = d.Voids.Count == 0 ? "0"
        : string.Join(" ", holeOrder.Where(holeByCls.ContainsKey).Select(c => $"{holeByCls[c]} {c}"));
    var pw = d.Voids.Where(v => v.Class == "middle" && v.CrossRoutes > 1).Select(v => v.CrossRoutes).OrderByDescending(x => x).ToList();
    var zoneOrder = new[] { "front-front", "front-neutral", "neutral-neutral", "intra", "self" };
    var zoneBy = d.Zones.GroupBy(z => z.Kind).ToDictionary(g => g.Key, g => g.Count());
    int ffNeut = d.Zones.Where(z => z.Kind == "front-front").Sum(z => z.Neutrals);
    string zoneStr = string.Join(" ", zoneOrder.Where(zoneBy.ContainsKey).Select(k =>
        $"{zoneBy[k]} {k}" + (k == "front-front" && ffNeut > 0 ? $" (+{ffNeut} stones)" : "")));
    var stats = string.Join("<span class=\"dot\">·</span>",
        stat(d.Islands.Count.ToString(), "islands"), stat(roleStr, ""),
        stat(stoneStr, "stepping stones"),
        stat(appStr, "approaches"),
        stat(d.LaneCells.Count.ToString(), "wool-lane tiles"),
        stat(zoneStr, "zones"),
        stat(d.FrontEdges.Count.ToString(), "frontline")
            + (d.IntraEdges.Count > 0 ? $"<span class=\"dot\">·</span>{stat(d.IntraEdges.Count.ToString(), "intra-team")}" : "")
            + (d.SelfEdges.Count > 0 ? $"<span class=\"dot\">·</span>{stat(d.SelfEdges.Count.ToString(), "self-bridge")}" : ""),
        stat(holeStr, "holes") + (pw.Count > 0 ? $"<span class=\"dot\">·</span>{stat(string.Join("/", pw), "parallel ways")}" : "")
            + (decl > 0 ? $"<span class=\"dot\">·</span>{stat($"{undecl} undeclared", "")}" : ""));

    return $"""
          <article class="card">
            <div class="card-head"><span class="card-id">{Esc(name)}</span><span class="card-sub"><span class="midform midform--{d.MidForm}">{Esc(d.MidForm)}</span> · {Esc(plan.Globals.Symmetry)}</span></div>
            <div class="svg-wrap">{svg}</div>
            <div class="card-stats">{stats}</div>
          </article>

    """;
}

static string N(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
static string Esc(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

string Page(string cardsHtml)
{
    const string css = """
    :root{ --bg-base:#0f172a; --bg-panel:#1e293b; --bg-canvas:#080f1a; --border:#334155; --text-muted:#8397b0;
      --text-secondary:#94a3b8; --text-primary:#cbd5e1; --text-bright:#e2e8f0; --text-strong:#fff; --accent-light:#60a5fa; --warn:#f59e0b;
      --mono:ui-monospace, SFMono-Regular, Menlo, monospace; --sans:-apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif; }
    @media (prefers-color-scheme: light){ :root{ --bg-base:#f1f5f9; --bg-panel:#fff; --border:#cbd5e1; --text-muted:#586780;
      --text-secondary:#475569; --text-primary:#1e293b; --text-bright:#0f172a; --text-strong:#0f172a; --accent-light:#2563eb; } }
    :root[data-theme="dark"]{ --bg-base:#0f172a; --bg-panel:#1e293b; --border:#334155; --text-muted:#8397b0; --text-secondary:#94a3b8; --text-primary:#cbd5e1; --text-bright:#e2e8f0; --text-strong:#fff; --accent-light:#60a5fa; }
    :root[data-theme="light"]{ --bg-base:#f1f5f9; --bg-panel:#fff; --border:#cbd5e1; --text-muted:#586780; --text-secondary:#475569; --text-primary:#1e293b; --text-bright:#0f172a; --text-strong:#0f172a; --accent-light:#2563eb; }
    *{ box-sizing:border-box; } html,body{ margin:0; padding:0; }
    body{ background:var(--bg-base); color:var(--text-primary); font-family:var(--sans); font-size:14px; line-height:1.5; overflow-x:hidden; -webkit-font-smoothing:antialiased; }
    .wrap{ max-width:1320px; margin:0 auto; padding:30px 24px 64px; }
    header.top{ border-bottom:1px solid var(--border); padding-bottom:20px; }
    .eyebrow{ font-family:var(--mono); font-size:11px; letter-spacing:.14em; text-transform:uppercase; color:var(--accent-light); margin:0 0 6px; }
    h1{ font-size:24px; line-height:1.2; margin:0 0 10px; color:var(--text-strong); font-weight:660; text-wrap:balance; }
    .lede{ margin:0; max-width:80ch; color:var(--text-secondary); font-size:13.5px; }
    .lede code,.lede b{ font-family:var(--mono); font-size:12px; } .lede b{ color:var(--text-bright); }
    .lede code{ color:var(--text-bright); background:var(--bg-panel); padding:1px 5px; border-radius:3px; }
    .legend{ display:flex; flex-wrap:wrap; gap:7px 16px; margin-top:16px; padding:12px 14px; background:var(--bg-panel); border:1px solid var(--border); border-radius:6px; align-items:center; }
    .legend-lbl{ font-family:var(--mono); font-size:10px; letter-spacing:.1em; text-transform:uppercase; color:var(--text-muted); }
    .lg{ display:inline-flex; align-items:center; gap:6px; font-size:12px; color:var(--text-secondary); font-family:var(--mono); }
    .sw{ width:13px; height:13px; border-radius:2px; flex:none; } .sw--edge{ width:15px; height:0; border-top:3px solid; }
    .sep{ width:1px; align-self:stretch; background:var(--border); }
    .grid{ display:grid; grid-template-columns:repeat(auto-fill, minmax(340px, 1fr)); gap:18px; align-items:start; margin-top:24px; }
    .card{ background:var(--bg-panel); border:1px solid var(--border); border-radius:10px; padding:12px 12px 10px; display:flex; flex-direction:column; gap:9px; }
    .card-head{ display:flex; align-items:baseline; justify-content:space-between; gap:8px; }
    .card-id{ font-family:var(--mono); font-size:12.5px; color:var(--text-bright); font-weight:600; }
    .card-sub{ font-family:var(--mono); font-size:11px; color:var(--text-muted); }
    .midform{ font-weight:700; text-transform:uppercase; letter-spacing:.06em; padding:1px 5px; border-radius:3px; color:#0f172a; }
    .midform--channelled{ background:#7dd3fc; } .midform--parallel{ background:#fca5a5; } .midform--hash{ background:#c4b5fd; }
    .svg-wrap{ background:var(--bg-canvas); border:1px solid var(--border); border-radius:6px; overflow:hidden; line-height:0; }
    .svg-wrap svg.map{ display:block; width:100%; height:auto; }
    .card-stats{ display:flex; flex-wrap:wrap; align-items:center; gap:3px 6px; font-family:var(--mono); font-size:11px; color:var(--text-muted); border-top:1px solid var(--border); padding-top:8px; }
    .stat-v{ color:var(--text-bright); font-weight:600; } .dot{ color:var(--border); }
    footer{ margin-top:40px; padding-top:16px; border-top:1px solid var(--border); font-family:var(--mono); font-size:11px; color:var(--text-muted); max-width:90ch; }
    """;

    string legend = $"""
        <div class="legend">
          <span class="legend-lbl">Authored</span>
          <span class="lg"><span class="sw" style="background:{CWoolRoom}"></span>wool-room piece</span>
          <span class="lg"><span class="sw" style="background:{CSpawnRole}"></span>spawn piece</span>
          <span class="sep"></span>
          <span class="legend-lbl">Derived</span>
          <span class="lg"><span class="sw" style="background:{CWoolLane}"></span>wool lane</span>
          <span class="lg"><span class="sw" style="background:{CStoneNeutral}"></span>neutral stepping stone</span>
          <span class="lg"><span class="sw" style="background:{CStoneTeam}"></span>team stepping stone</span>
          <span class="lg"><span class="sw" style="background:{CResidual}"></span>residual (what remains)</span>
          <span class="lg"><span class="sw sw--edge" style="border-color:{CFront}"></span>frontline edge</span>
          <span class="lg"><span class="sw sw--edge" style="border-top-style:dashed;border-color:{CIntra}"></span>intra-team bridge</span>
          <span class="lg"><span class="sw sw--edge" style="border-top-style:dotted;border-color:{CSelf}"></span>self-bridge notch</span>
          <span class="lg"><span class="sw sw--edge" style="border-color:{CRedstone};border-top-width:3px"></span>redstone interface</span>
          <span class="sep"></span>
          <span class="legend-lbl">Holes</span>
          <span class="lg"><span class="sw" style="background:{CHoleEncased}55;border:1.3px solid {CHoleEncased}"></span>encased (in one team)</span>
          <span class="lg"><span class="sw" style="background:{CHoleGap}55;border:1.3px solid {CHoleGap}"></span>gap (isolation cut)</span>
          <span class="lg"><span class="sw" style="background:{CHoleFront}55;border:1.3px solid {CHoleFront}"></span>frontline pocket</span>
          <span class="lg"><span class="sw" style="background:{CHoleMiddle}55;border:1.3px solid {CHoleMiddle}"></span>middle (contested)</span>
          <span class="sep"></span>
          <span class="legend-lbl">Markers</span>
          <span class="lg"><span class="sw" style="background:{MkWool}"></span>wool (n× = approaches)</span>
          <span class="lg"><span class="sw" style="background:{MkSpawn};border-radius:50%"></span>spawn</span>
          <span class="sep"></span>
          <span class="legend-lbl">Build zones</span>
          <span class="lg"><span class="sw" style="background:{CZFrontFront}55;border:1.2px solid {CZFrontFront}"></span>front↔front (crossing)</span>
          <span class="lg"><span class="sw" style="background:{CZFrontNeut}55;border:1.2px solid {CZFrontNeut}"></span>front↔neutral (bridge to mid)</span>
          <span class="lg"><span class="sw" style="background:{CZNeutNeut}55;border:1.2px solid {CZNeutNeut}"></span>neutral↔neutral (mid-internal)</span>
          <span class="lg"><span class="sw" style="background:{CZIntra}55;border:1.2px solid {CZIntra}"></span>intra / self (isolation cut)</span>
        </div>
    """;

    string body = $"""
    <div class="wrap">
      <header class="top">
        <p class="eyebrow">Layout evaluator · deriver v1 · seed corpus</p>
        <h1>What the deriver reads in the authored seeds</h1>
        <p class="lede">Each card is a seed from <code>tools/seeds/</code>, fanned to the full board, with the
        <strong>derived</strong> structure drawn on — nothing here is authored beyond geometry + the wool/spawn
        markers. Review target: does the deriver's reading match yours? Every terrain tile gets exactly one label:
        a <strong style="color:{CStoneNeutral}">stone-gray</strong> or <strong style="color:{CStoneTeam}">fuchsia</strong>
        island is a <b>stepping stone</b> (whole island — <b>team</b> fuchsia if captive on one team's spawn↔wool
        route, else <b>neutral</b> gray, a contested centre island); the <strong style="color:{CWoolLane}">orange</strong>
        tiles are the <b>wool lane</b> — the terrain stacked out from the wool room's
        <strong style="color:{CRedstone}">redstone interface</strong> (bright red line) until void, build, or a
        <b>T</b> (a crossbar reaching beyond the band on both sides; a one-sided jut is just a side branch), a
        two-sided room stacking both ways and a side-docked room stacking along the docked lane's axis (a lane may
        run to a frontline; spawns never stack a lane); and everything that remains is
        <strong style="color:{CResidual}">residual</strong> — unclaimed terrain (no erosion split; residual is
        simply what is left). The <b>n×</b> beside each wool is its approach count (green ≥2 = multi-access, red =
        lone dead-end). The
        <b>holes</b> (enclosed voids) are coloured by <b>what their boundary touches</b>, interior→contested:
        <strong style="color:{CHoleEncased}">encased</strong> (deep in one team), <strong style="color:{CHoleGap}">gap</strong>
        (a team's isolation-cut), <strong style="color:{CHoleFront}">frontline pocket</strong> (a team's exposed edge),
        <strong style="color:{CHoleMiddle}">middle</strong> (contested crossing) — and any still <b>undeclared</b>
        is the buffer worklist. The <b>build zones</b> are tinted by what they link:
        <strong style="color:{CZFrontFront}">front↔front</strong> (the crossing, may carry stepping stones between it),
        <strong style="color:{CZFrontNeut}">front↔neutral</strong> (a team's bridge to the mid),
        <strong style="color:{CZNeutNeut}">neutral↔neutral</strong> (a mid-internal link, often across the axis), and
        <strong style="color:{CZIntra}">intra/self</strong> (a team's isolation cut) — from which the
        <b>CT mid-form</b> in the card header (<b>channelled</b> / <b>parallel</b> / <b>hash</b>) is derived, and a
        middle hole's <b>parallel ways</b> = the crossings ringing it (big-board's two front↔front lanes). A
        <strong style="color:{CIntra}">pink dashed edge</strong> is an <b>intra-team bridge</b> — a build region on
        a team's own internal spawn↔wool route (direct, or a chain through a <b>captive</b> stepping stone only that
        team can reach): it marks a deliberate internal gap where a piece was chopped off and bridged back to slow
        attackers (the isolation cut). A <strong style="color:{CSelf}">cyan dotted edge</strong> is a
        <b>self-bridge notch</b> — a build pocket carved into a <em>single</em> island (its two walls the same
        landmass); it is internal like the bridge but shapes one piece rather than gapping two, so it reads as its
        own signal. Captivity also separates a <b>team</b> stepping stone (on the spawn↔wool path) from a
        <b>neutral</b> one (a contested centre island).
        Disagreements are the cutoff test set. Per <code>docs/contracts/layout-evaluator.md</code> §5.</p>
        {legend}
      </header>

      <div class="grid">
    {cardsHtml}  </div>

      <footer>Deriver v1 — first cut for visual review, not the final algorithm. Every terrain tile gets ONE
      label by priority: authored <b>wool-room</b> / <b>spawn</b> pieces keep their editor colour (intent), then
      stepping-stone islands, then wool-lane tiles, then <b>residual</b> = whatever remains (no erosion split —
      residual is simply unclaimed terrain). approach count = arms at the room; frontline = OUTSIDE edge facing a
      build void, but only on a VOID-DOMINANT island (more void-border than build-border — exposed territory); a
      build-dominant island (embedded in the crossing) or a pure-void one is a stepping stone with no frontline
      (corpus-wide, void-dominant == holds a spawn/wool), labelled as a whole island (team = fuchsia if captive on
      a spawn&lt;-&gt;wool route, else neutral = stone-gray); an edge facing an intra-team spawn&lt;-&gt;wool bridge
      (a build region on a team's own internal route — direct, or through a captive stepping stone only that team
      can reach) is re-tagged intra, not frontline, and a build pocket carved into a SINGLE island is split off as
      a self-bridge notch (cyan dotted); wool lane (orange) = terrain stacked from the wool room's redstone
      interface (red line) out to void/build/T (a crossbar reaching beyond the band on both sides stops it; a
      one-sided jut is a side branch), both ways for two-sided rooms, and along the docked lane's own axis for a
      side-dock — wools only, never spawns, and a lane may reach a frontline; holes = true void walled by terrain
      OR build (the terrain+build encasing catches the frontline rotary devices), EVERY one reported at any size
      (the seeds are ground truth), classified by what the boundary touches — encased (one team, no build), gap
      (one team + intra/self build, an isolation cut), frontline pocket (one team + frontline build), middle (>=2
      teams / pure build); still-undeclared holes are the buffer worklist. Build zones are typed by what they link
      (front↔front / front↔neutral / neutral↔neutral / intra / self), read off the island incidence — from which
      the CT mid-form (channelled / parallel / hash) and a middle hole's parallel-ways are derived. Static SVG,
      self-contained, cell = 5 blocks.</footer>
    </div>
    """;

    return $"""
    <!doctype html>
    <html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
    <title>Deriver v1 — seed corpus</title><style>{css}</style></head>
    <body>{body}</body></html>
    """;
}

// derived-structure bundle for one seed
record Derived(
    int Cell,
    Dictionary<(int, int), (string PieceId, int K)> Filled,
    HashSet<(int, int)> Build,
    Dictionary<(int, int), string> BuildKindOf,
    List<(string Kind, int Neutrals)> Zones,
    string MidForm,
    List<HashSet<(int, int)>> Islands,
    Dictionary<(int, int), int> IslandOf,
    string[] Roles,
    string[] SteppingKind,
    List<(int Island, double Bx, double Bz, int Count)> Approaches,
    List<(int X1, int Z1, int X2, int Z2)> FrontEdges,
    List<(int X1, int Z1, int X2, int Z2)> IntraEdges,
    List<(int X1, int Z1, int X2, int Z2)> SelfEdges,
    HashSet<(int, int)> LaneCells,
    List<(int X1, int Z1, int X2, int Z2)> RedstoneEdges,
    List<(HashSet<(int, int)> Cells, bool Declared, string Class, int CrossRoutes)> Voids);
