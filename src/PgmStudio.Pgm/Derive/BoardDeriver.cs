using PgmStudio.Geom;
using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Derive;

/// <summary>Derives the raster-layer <see cref="BoardStructure"/> from a plan: fans every piece/zone to
/// the full board in cell space and computes structure from geometry + markers (islands, anchor roles,
/// stepping stones, build-zone kinds/widths/interfaces, wool lanes + approaches, frontline/intra/self edges,
/// the mid form, and enclosed voids). Pure over a <see cref="PlanModel"/>; the raster substrate routes
/// through <see cref="Cells"/>.</summary>
public static class BoardDeriver
{
    public static BoardStructure Derive(PlanModel plan)
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
                foreach (var nb in Cells.N4(cur))
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
                 touchesBuild = islands[i].Any(c => Cells.N4(c).Any(build.Contains) || build.Contains(c));
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
                foreach (var c in room) foreach (var nb in Cells.N4(c)) if (filled.ContainsKey(nb) && !room.Contains(nb)) arm.Add(nb);
                approaches.Add((isl, MarkerBlock(piece.Rect, w.At, k, axes).X, MarkerBlock(piece.Rect, w.At, k, axes).Z, Cells.Components(arm)));
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
            foreach (var nb in Cells.N4(c))
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
                foreach (var nb in Cells.N4(cur))
                    if (build.Contains(nb) && !filled.ContainsKey(nb) && !regionOf.ContainsKey(nb)) { regionOf[nb] = regionCount; q.Enqueue(nb); }
            }
            regionCount++;
        }
        var regionIslands = Enumerable.Range(0, regionCount).Select(_ => new HashSet<int>()).ToArray();
        foreach (var (cell, r) in regionOf)
            foreach (var nb in Cells.N4(cell)) if (filled.ContainsKey(nb)) regionIslands[r].Add(islandOf[nb]);

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

        // zone WIDTH (BZ3) — the corridor's narrowest cross-section: per cell take the shorter of its horizontal /
        // vertical run within the same region, then the MIN over the region (a choke narrows the whole zone). And
        // the INTERFACE / edge WIDTH (BZ8) — the contact run where the zone docks each island it connects. Together
        // they are BZ9 fit: an interface much narrower than the corridor is underfit; a corridor wider than every
        // interface is overfit. (Cells; ×5 = blocks — BZ3's 10-wide dominant is a 2-cell zone.)
        var regionCells = Enumerable.Range(0, regionCount).Select(_ => new List<(int, int)>()).ToArray();
        foreach (var (cell, r) in regionOf) regionCells[r].Add(cell);
        var zoneWidth = new int[regionCount];
        for (var r = 0; r < regionCount; r++)
        {
            int w = int.MaxValue;
            foreach (var c in regionCells[r])
            {
                int hr = 1, vr = 1;
                for (var x = c.Item1 - 1; regionOf.TryGetValue((x, c.Item2), out var rr) && rr == r; x--) hr++;
                for (var x = c.Item1 + 1; regionOf.TryGetValue((x, c.Item2), out var rr) && rr == r; x++) hr++;
                for (var z = c.Item2 - 1; regionOf.TryGetValue((c.Item1, z), out var rr) && rr == r; z--) vr++;
                for (var z = c.Item2 + 1; regionOf.TryGetValue((c.Item1, z), out var rr) && rr == r; z++) vr++;
                w = Math.Min(w, Math.Min(hr, vr));
            }
            zoneWidth[r] = regionCells[r].Count == 0 ? 0 : w;
        }
        var contact = new Dictionary<(int r, int i), int>();   // shared cell-edges between a zone and an island
        foreach (var (cell, r) in regionOf)
            foreach (var nb in Cells.N4(cell))
                if (filled.ContainsKey(nb)) { var key = (r, islandOf[nb]); contact[key] = contact.GetValueOrDefault(key) + 1; }
        var zoneIfaceMin = new int[regionCount];
        var zoneIfaceMax = new int[regionCount];
        for (var r = 0; r < regionCount; r++)
        {
            var widths = regionIslands[r].Select(i => contact.GetValueOrDefault((r, i))).Where(v => v > 0).ToList();
            zoneIfaceMin[r] = widths.Count == 0 ? 0 : widths.Min();
            zoneIfaceMax[r] = widths.Count == 0 ? 0 : widths.Max();
        }

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

        // per-wool LANE SHAPE — the corridor's topology, read independent of the objective-lane tiles above. A
        // width-adaptive flood traces the thin path from the room to the first junction (a plaza is a block WIDER
        // than the corridor, so a 3-wide lane still reads as a lane, not a junction); bends = reflex corners of
        // that path. I (straight) / L (one bend) / Z (two) / complex (more — the wool sits on a chunky island);
        // "none" = no corridor at all (the room docks a build zone directly — the isolated whole-island room).
        // shape is a TEAM-LOCAL property — read it on the k=0 unit terrain only, never the fanned board (a fanned
        // mirror image merges into the corridor near the centre and corrupts the trace).
        var filledK0 = new Dictionary<(int, int), (string PieceId, int K)>();
        foreach (var p in plan.Pieces)
        {
            if (p.Role is PlanRoles.Buffer or PlanRoles.Connector) continue;
            foreach (var c in FanCellsK(p.Rect, axes, 0)) filledK0[c] = (p.Id, 0);
        }
        var woolShapes = new List<(string Shape, int Width)>();
        foreach (var w in plan.Placements.Wools)
        {
            var pc = plan.Pieces.FirstOrDefault(p => p.Id == w.Piece);
            if (pc is null) continue;
            var (read, laneW) = ShapeClassifier.ClassifyOpen(filledK0.Keys.ToHashSet(), FanCellsK(pc.Rect, axes, 0).ToHashSet());
            woolShapes.Add((ShapeClassifier.LaneName(read), laneW));
        }

        // frontline edges + the intra-team interfaces kept as their OWN derived signal: they mark where the author
        // built a deliberate internal gap — a piece chopped off the main mass and bridged back across a slow-down
        // void (the CT5 isolation cut). A learnable pattern for the builder, not just an exclusion.
        var frontEdges = new List<(int X1, int Z1, int X2, int Z2)>();
        var frontEdgeIsland = new List<int>();   // owning island per frontline segment — grouped into runs below
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
                else if (voidDom) { frontEdges.Add(seg); frontEdgeIsland.Add(isl); }
            }
        }
        // frontline RUNS — group the segments into contiguous same-island faces (segments sharing an endpoint on
        // one island). Each run carries its owning team (orbit image), the face width (its longer extent in
        // cells), and the profile: STRAIGHT (one colinear face, e.g. isolated-spawn) or OFFSET (the face steps,
        // e.g. base-2island). A team's number of runs, face widths, and profiles are the frontline measurables.
        var frontlineRuns = GroupFrontlineRuns(frontEdges, frontEdgeIsland, islandTeam);

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
        while (oq.Count > 0) { var cur = oq.Dequeue(); foreach (var nb in Cells.N4(cur)) if (TrueVoid(nb) && outside.Add(nb)) oq.Enqueue(nb); }

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
                while (q.Count > 0) { var cur = q.Dequeue(); comp.Add(cur); foreach (var nb in Cells.N4(cur)) if (TrueVoid(nb) && !outside.Contains(nb) && seenVoid.Add(nb)) q.Enqueue(nb); }
                bool declared = comp.Any(declaredVoid.Contains);   // a buffer / zone-hole marks this pocket deliberate
                // report EVERY enclosed void, any size — the authored seeds are ground truth, and they carry
                // intended holes as small as 1x2 cells (mirror-tiny-map-cliff, rotate-wide-frontline). Never let a
                // size rule override the corpus.
                var terrTeams = new HashSet<int>(); var buildTeams = new HashSet<int>();
                bool touchesBuild = false, touchesFrontline = false;
                var borderRegions = new HashSet<int>();   // distinct build zones ringing the hole
                foreach (var c in comp)
                    foreach (var nb in Cells.N4(c))
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

        var zones = Enumerable.Range(0, regionCount)
            .Select(r => (Kind: zoneKind[r], Neutrals: zoneNeutral[r], Width: zoneWidth[r], IfaceMin: zoneIfaceMin[r], IfaceMax: zoneIfaceMax[r]))
            .ToList();
        return new BoardStructure(plan.Globals.Cell, filled, build, buildKindOf, zones, midForm, islands, islandOf, roles, steppingKind, approaches, woolShapes, frontEdges, intraEdges, selfEdges, laneCells, redstoneEdges, voids, frontlineRuns);
    }

    // group frontline segments into runs — a run is one island's contiguous void-facing face (segments joined by
    // a shared endpoint on the same island). Per run: the owning team (the island's orbit image), the face width
    // (the longer extent of its bounding box, in cells), and whether it is straight (all segments colinear on one
    // line) or offset (the face steps in and out).
    private static List<(int Team, int Width, string Profile)> GroupFrontlineRuns(
        List<(int X1, int Z1, int X2, int Z2)> edges, List<int> islandOfEdge, int[] islandTeam)
    {
        int n = edges.Count;
        var uf = Enumerable.Range(0, n).ToArray();
        int Find(int x) { while (uf[x] != x) { uf[x] = uf[uf[x]]; x = uf[x]; } return x; }
        var byPt = new Dictionary<((int, int) Pt, int Isl), List<int>>();
        for (var i = 0; i < n; i++)
            foreach (var p in new[] { (edges[i].X1, edges[i].Z1), (edges[i].X2, edges[i].Z2) })
                (byPt.TryGetValue((p, islandOfEdge[i]), out var l) ? l : byPt[(p, islandOfEdge[i])] = new()).Add(i);
        foreach (var l in byPt.Values) for (var j = 1; j < l.Count; j++) uf[Find(l[0])] = Find(l[j]);

        var comp = new Dictionary<int, List<int>>();
        for (var i = 0; i < n; i++) (comp.TryGetValue(Find(i), out var l) ? l : comp[Find(i)] = new()).Add(i);

        var runs = new List<(int Team, int Width, string Profile)>();
        foreach (var segs in comp.Values)
        {
            bool colinV = segs.All(i => edges[i].X1 == edges[i].X2) && segs.Select(i => edges[i].X1).Distinct().Count() == 1;
            bool colinH = segs.All(i => edges[i].Z1 == edges[i].Z2) && segs.Select(i => edges[i].Z1).Distinct().Count() == 1;
            var xs = segs.SelectMany(i => new[] { edges[i].X1, edges[i].X2 }).ToList();
            var zs = segs.SelectMany(i => new[] { edges[i].Z1, edges[i].Z2 }).ToList();
            int width = Math.Max(xs.Max() - xs.Min(), zs.Max() - zs.Min());
            runs.Add((islandTeam[islandOfEdge[segs[0]]], width, colinV || colinH ? "straight" : "offset"));
        }
        return runs;
    }

    // neighbour + the shared cell-edge segment (in CELL units) between c and that neighbour
    private static IEnumerable<((int, int) Nb, (int, int, int, int) Seg)> N4Seg((int, int) c)
    {
        int x = c.Item1, z = c.Item2;
        yield return ((x + 1, z), (x + 1, z, x + 1, z + 1));
        yield return ((x - 1, z), (x, z, x, z + 1));
        yield return ((x, z + 1), (x, z + 1, x + 1, z + 1));
        yield return ((x, z - 1), (x, z, x + 1, z));
    }

    // fan a cell rect to EVERY orbit image, yielding all cells
    private static IEnumerable<(int, int)> FanCells(int[] rect, string[] axes, int order)
    {
        for (var k = 0; k < order; k++) foreach (var c in FanCellsK(rect, axes, k)) yield return c;
    }

    // fan a cell rect to the k-th orbit image (identity at k=0), yielding its cells
    private static IEnumerable<(int, int)> FanCellsK(int[] rect, string[] axes, int k)
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
    public static (double X, double Z) MarkerBlock(int[] rect, double[] at, int k, string[] axes)
    {
        double cx = rect[0] + at[0], cz = rect[1] + at[1];   // in cells
        if (k > 0) { var (fx, fz) = Symmetry.Apply(cx, cz, axes[k - 1], 0, 0); cx = fx; cz = fz; }
        return (cx, cz);   // in cells; caller scales by cell
    }
}
