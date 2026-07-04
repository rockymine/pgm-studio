using PgmStudio.Geom;
using PgmStudio.Pgm.Plan;

namespace PgmStudio.Pgm.Compose;

/// <summary>A grown piece: a plain <c>piece</c>-role rect in cell coordinates (<see cref="Plan.PlanPiece.Rect"/>
/// convention). The grammar authors no <c>wool-room</c>/<c>spawn</c>-role pieces — only anonymous ones; the
/// placements below carry all the meaning (PC1/PC3).</summary>
public sealed record GrownPiece(string Id, int[] Rect);

/// <summary>The grown unit's spawn: which piece it sits on, its piece-relative half-cell offset, and its
/// absolute facing (SP3: toward the enemy by default).</summary>
public sealed record GrownSpawn(string Piece, double[] At, string Facing);

/// <summary>A grown wool: which piece it sits on and its piece-relative half-cell offset.</summary>
public sealed record GrownWool(string Piece, double[] At);

/// <summary>The team-0 unit the grower produces: its pieces and objective placements, in plan cell
/// coordinates, ready for <see cref="Composer"/> to assemble into a <see cref="Plan.PlanModel"/>.</summary>
public sealed record GrownUnit(IReadOnlyList<GrownPiece> Pieces, GrownSpawn Spawn, IReadOnlyList<GrownWool> Wools);

/// <summary>
/// Grows one team's authored unit on the (u,v) grid a <see cref="Frame"/> maps to real cell coordinates: a
/// hub — widening toward a plaza on big maps (HB1/HB3) — a spawn lane that runs straight back or hooks into
/// an L (LN2/SP2/SP3), 1-3 wool lanes (WL6) hosted on the hub's sides or branching off the spawn lane at a
/// sampled depth, and 0-2 frontline pieces reaching toward the axis (FR3/FR4). Every attachment shares a
/// straight border of at least 2 cells (a full G2 corridor — no narrow seams are authored), no two pieces
/// overlap, pieces of DIFFERENT orbit images keep at least a 10-block clearance on some axis (team
/// territories stay separate islands, bridgeable later at G5's minimum hop — CT1), and no maximal collinear
/// chain of land-joined pieces runs past 50 blocks (LN2).
///
/// Surplus land budget beyond what capped lanes can hold is spent structurally, never by stretching a lane:
/// a third wool lane on big teams, extra lane segments at right angles (turns), a wider hub, frontline
/// pieces. A candidate that fails any hard invariant is discarded and regrown from the same
/// <see cref="ComposeRng"/> — which is why every draw below happens in one fixed order — up to a bounded
/// number of attempts before <see cref="ComposeException"/>: the grower never emits a violating unit.
/// </summary>
public static class TeamUnitGrower
{
    private const int MaxAttempts = 500;
    private const double AreaTolerance = 0.20;
    private const double WoolSpawnMin = 20;    // WL2, blocks
    private const double WoolWoolMin = 45;     // WL7, blocks

    /// <summary>LN2's hard cap: a lane runs at most this many blocks before a junction or dead end, and the
    /// cap binds the maximal collinear chain of land-joined same-cross-section pieces — a long lane cut into
    /// several collinear pieces is still one lane.</summary>
    public const int LaneChainMaxBlocks = 50;

    /// <summary>Minimum clearance (blocks, on at least one axis) between pieces of different orbit images:
    /// G5's smallest hop, so the void between team territories stays bridgeable without ever welding them
    /// into one landmass (CT1 — even a narrow shared border would connect them).</summary>
    public const int ImageClearanceBlocks = 10;

    // WL1's corpus inset is ~5 blocks (~1 cell); half a cell is close enough to "not on the wall" while
    // leaving the smallest maps enough wool↔spawn separation to clear WL2 within the area budget.
    private const double MarkerInsetCells = 0.5;

    public static GrownUnit Grow(ComposeEnvelope env, ComposeRng rng)
    {
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
            if (TryGrow(env, rng) is { } unit)
                return unit;
        throw new ComposeException(
            $"team-unit growth could not satisfy its invariants within {MaxAttempts} attempts " +
            $"(land/team {env.LandPerTeam:0}, teams {env.Teams}, symmetry '{env.Symmetry}')");
    }

    /// <summary>The sampled shape of one growth attempt — everything random, resolved before the
    /// deterministic repair search runs.</summary>
    private sealed record Shape(
        int W, int ChainCap, int AxisMargin, int HubU, int HubV, int WoolInset,
        int[] FrontSegs, int FrontVOff,
        int SpawnSegCount, bool SpawnL, int SpawnLDir, double SpawnSplitFrac, int SpawnURunCap,
        bool[] LaneSpawnHost, double[] LaneAttFracs, int WoolCount);

    private static GrownUnit? TryGrow(ComposeEnvelope env, ComposeRng rng)
    {
        var frame = Frame.For(env.Symmetry);
        var w = env.LandPerTeam > 2500 ? 3 : 2;                       // lane width, cells (LN1: 10; 15 on big maps)
        var chainCap = Math.Max(3, LaneChainMaxBlocks / env.Cell);    // LN2 cap in cells
        // rot_90's wedge punishes sideways reach: cap the side (v-run) segment shallower than the chain cap
        var seg1Cap = env.Teams == 4 ? Math.Min(chainCap, 6) : chainCap;
        double cellArea = env.Cell * (double)env.Cell;
        double budgetCells = env.LandPerTeam / cellArea;

        // ── structure sampling — fixed draw order (part of the golden contract): (1) wool-lane count,
        // (2) per side lane host, (3) per side lane segment count, (4) per side lane attachment fraction,
        // (5) arm asymmetry fraction, (6) spawn segment count, (7) spawn dogleg style + direction, (8)
        // frontline count, (9) frontline cross-offset fraction, (10) hub depth + width, (11) spawn split
        // fraction, (12) frontline split fraction. Draws whose feature is absent are skipped only on
        // deterministic conditions, so a given request always replays the same sequence. ──
        var frontlinePossible = env.LandPerTeam >= 800;
        var woolInset = frontlinePossible ? 1 : 0;

        var woolCount = env.PlayersPerTeam >= 16 ? (rng.NextBool(0.4) ? 3 : 2)
            : env.LandPerTeam < 600 ? 1
            : rng.NextInt(1, 3);
        var sideLanes = Math.Min(woolCount, 2);

        var laneSpawnHost = new bool[sideLanes];
        for (var i = 0; i < sideLanes; i++) laneSpawnHost[i] = rng.NextBool(0.35);

        var laneSegCounts = new int[sideLanes];
        for (var i = 0; i < sideLanes; i++)
            laneSegCounts[i] = env.LandPerTeam < 900 ? (rng.NextBool(0.5) ? 2 : 1) : rng.NextInt(1, 4);

        var laneAttFracs = new double[sideLanes];
        for (var i = 0; i < sideLanes; i++) laneAttFracs[i] = rng.NextDouble();

        var asymFrac = sideLanes == 2 ? rng.NextDouble() : 0.5;

        var spawnSegCount = rng.NextBool(0.5) ? 2 : 1;
        var spawnLStyle = rng.NextBool(0.5);
        var spawnLDirDraw = rng.NextBool(0.5) ? -1 : 1;

        var frontlineCount = frontlinePossible ? rng.NextInt(0, 3) : 0;
        var frontVFrac = frontlinePossible ? rng.NextDouble() : 0.0;

        // hub (HB1/HB3): the depth floor clears the side-lane inset; the width floor keeps a frontline chain
        // narrower than the hub (no merged spine chain) and fits the third wool lane on the back edge; the
        // cap grows toward the 30-block plaza on big budgets.
        var hubCap = env.LandPerTeam >= 3000 ? 6 : env.LandPerTeam >= 1500 ? 5 : frontlinePossible ? 4 : 3;
        var hubUFloor = w + 1 + woolInset;
        int HubVFloor() => Math.Max(
            frontlineCount > 0 ? w + 1 : Math.Max(2, w),
            woolCount == 3 ? w + 3 : 0);                   // back edge: spawn lane (w) + 1 gap + wool-c lane (2)
        var hubU = rng.NextInt(hubUFloor, Math.Max(hubUFloor, hubCap) + 1);
        var hubV = rng.NextInt(HubVFloor(), Math.Max(HubVFloor(), hubCap) + 1);

        var spawnSplitFrac = rng.NextDouble();
        var frontSplitFrac = frontlinePossible ? rng.NextDouble() : 0.0;

        // deterministic capacity repair (no draws): when the sampled shape cannot hold the land budget even
        // with every run at its cap, add structure — more lane segments first, then a full frontline chain.
        int LaneCapCells(int n) => (seg1Cap + (n - 1) * chainCap) * w;
        double Capacity() =>
            hubU * hubV
            + chainCap * w                                 // spawn (its straight-run cap; an L holds more)
            + (frontlineCount > 0 ? chainCap * w : 0)
            + Enumerable.Range(0, sideLanes).Sum(i => LaneCapCells(laneSegCounts[i]))
            + (woolCount == 3 ? chainCap * 2 : 0);
        while (Capacity() < 0.9 * budgetCells)
        {
            var bumped = false;
            for (var i = 0; i < sideLanes && !bumped; i++)
                if (laneSegCounts[i] < 3) { laneSegCounts[i]++; bumped = true; }
            if (!bumped && frontlinePossible && frontlineCount == 0)
            {
                frontlineCount = 2;
                hubV = Math.Max(hubV, HubVFloor());
                bumped = true;
            }
            if (!bumped) break;
        }
        if (Capacity() < (1 - AreaTolerance) * budgetCells) return null;

        // rot_90 needs the whole central band deep enough that a piece and its quarter-turn image keep the
        // image clearance: margin ≥ the hub's half-width + the clearance (in cells).
        var clearCells = (ImageClearanceBlocks + env.Cell - 1) / env.Cell;
        var axisMargin = env.Teams == 4
            ? Math.Max(Envelope.AxisMarginCells + 2, hubV - hubV / 2 + clearCells)
            : Envelope.AxisMarginCells;

        // ── deterministic length solve: distribute the non-hub budget by fixed weights, clamped to the
        // chain caps — the surplus that clamping sheds is what the structural additions above absorb. ──
        double flexible = budgetCells - hubU * hubV;
        if (flexible <= 0) return null;
        const double spawnUnit = 2.0, woolUnit = 1.8, woolCUnit = 1.2, frontlineUnit = 0.5;
        double totalUnits = spawnUnit + sideLanes * woolUnit
            + (woolCount == 3 ? woolCUnit : 0) + frontlineCount * frontlineUnit;

        // when the hub is exactly lane-width and no frontline breaks the spine, hub + spawn's straight run
        // merge into one collinear chain — the run must leave the hub room under the cap
        var spawnURunCap = hubV == w && frontlineCount == 0 ? Math.Max(1, chainCap - hubU) : chainCap;
        var spawnLFeasible = spawnLStyle && spawnSegCount == 2;
        var spawnLenCap = spawnLFeasible ? spawnURunCap + chainCap : spawnURunCap;
        var spawnLen = Math.Clamp((int)Math.Round(flexible * (spawnUnit / totalUnits) / w), 1, spawnLenCap);

        var frontTotal = frontlineCount == 0 ? 0 : Math.Clamp(
            (int)Math.Round(flexible * (frontlineCount * frontlineUnit / totalUnits) / w),
            2 * frontlineCount, chainCap);
        int[] frontSegs = frontlineCount switch
        {
            0 => [],
            1 => [frontTotal],
            _ => SplitTwo(frontTotal, frontSplitFrac, 2),
        };
        var frontVOff = (int)Math.Round(frontVFrac * (hubV - w));

        // arm totals: equal weighted shares skewed by the asymmetry draw, then clamped to the lane's caps
        var laneTotals = new int[sideLanes];
        for (var i = 0; i < sideLanes; i++)
            laneTotals[i] = (int)Math.Round(flexible * (woolUnit / totalUnits) / w);
        if (sideLanes == 2)
        {
            var shift = (int)Math.Round((asymFrac - 0.5) * 0.8 * Math.Min(laneTotals[0], laneTotals[1]));
            laneTotals[0] += shift;
            laneTotals[1] -= shift;
        }
        var laneSegLens = new int[sideLanes][];
        for (var i = 0; i < sideLanes; i++)
        {
            laneTotals[i] = Math.Clamp(laneTotals[i], 3 * laneSegCounts[i], LaneCapCells(laneSegCounts[i]) / w);
            laneSegLens[i] = DistributeLane(laneTotals[i], laneSegCounts[i], seg1Cap, chainCap);
        }

        var woolCLen = woolCount == 3
            ? Math.Clamp((int)Math.Round(flexible * (woolCUnit / totalUnits) / 2), 3, chainCap)
            : 0;

        var shape = new Shape(
            w, chainCap, axisMargin, hubU, hubV, woolInset, frontSegs, frontVOff,
            spawnSegCount, spawnLFeasible, woolCount == 3 ? -1 : spawnLDirDraw, spawnSplitFrac, spawnURunCap,
            laneSpawnHost, laneAttFracs, woolCount);

        // ── repair search (no draws): WL2/WL7 and the area window are coupled — shrink the spawn lane
        // (outer) and inflate the wool lanes toward their caps (inner) until every invariant holds. ──
        var maxInflate = Enumerable.Range(0, sideLanes)
                .Sum(i => laneSegLens[i].Select((l, j) => (j == 0 ? seg1Cap : chainCap) - l).Sum())
            + (woolCount == 3 ? chainCap - woolCLen : 0);
        for (var shrink = 0; shrink <= Math.Min(6, spawnLen - 1); shrink++)
        {
            var s = spawnLen - shrink;
            for (var inflate = 0; inflate <= maxInflate; inflate++)
            {
                var (lens, cLen) = ApplyInflation(laneSegLens, woolCLen, inflate, seg1Cap, chainCap, woolCount == 3);
                var (pieces, spawnPieceId, spawnAt, wools) = Build(frame, shape, s, lens, cLen);

                var total = TotalArea(env, pieces);
                if (total > env.LandPerTeam * (1 + AreaTolerance)) break;   // inflation only grows — next shrink

                if (!ValidateGeometry(env, pieces)) continue;
                if (!ValidateContacts(env, pieces)) continue;
                if (MaxChainBlocks(env.Cell, pieces.Select(p => p.Rect).ToList()) > LaneChainMaxBlocks) continue;
                if (total < env.LandPerTeam * (1 - AreaTolerance)) continue;
                var rectById = pieces.ToDictionary(p => p.Id, p => p.Rect);
                if (!ValidateMarkers(env, rectById, spawnPieceId, spawnAt, wools)) continue;

                return new GrownUnit(pieces, new GrownSpawn(spawnPieceId, spawnAt, frame.TowardAxis), wools);
            }
        }
        return null;
    }

    // ── geometry assembly ───────────────────────────────────────────────────────────────────────────────

    private static (List<GrownPiece> Pieces, string SpawnPieceId, double[] SpawnAt, List<GrownWool> Wools) Build(
        Frame frame, Shape sh, int spawnLen, int[][] laneSegLens, int woolCLen)
    {
        var w = sh.W;
        var hubUMin = sh.AxisMargin + sh.FrontSegs.Sum();
        var hubVMin = -(sh.HubV / 2);
        var spawnU0 = hubUMin + sh.HubU;

        var pieces = new List<GrownPiece>();
        void Place(string id, int uMin, int uSpan, int vMin, int vSpan) =>
            pieces.Add(new GrownPiece(id, frame.ToRect(uMin, uSpan, vMin, vSpan)));

        Place("hub", hubUMin, sh.HubU, hubVMin, sh.HubV);

        // frontline — a chain toward the axis (u-) from the hub's front edge, at a sampled cross offset
        var curFrontU = hubUMin;
        for (var i = 0; i < sh.FrontSegs.Length; i++)
        {
            curFrontU -= sh.FrontSegs[i];
            Place(i == 0 ? "frontline" : $"frontline-{i + 1}", curFrontU, sh.FrontSegs[i], hubVMin + sh.FrontVOff, w);
        }

        // spawn lane — straight back (1-2 collinear segments) or an L hook (a straight run, then a sideways
        // turn); the marker sits near the far end of the last segment (SP2), facing the axis (SP3)
        var spawnL = sh.SpawnL && spawnLen >= w + 2;
        string spawnPieceId;
        double[] spawnAt;
        int spawnS1;
        if (spawnL)
        {
            var s1Min = Math.Max(w, spawnLen - sh.ChainCap);
            var s1Max = Math.Min(sh.SpawnURunCap, spawnLen - 2);
            var s1 = Math.Clamp((int)Math.Round(spawnLen * sh.SpawnSplitFrac), s1Min, Math.Max(s1Min, s1Max));
            var s2 = spawnLen - s1;
            spawnS1 = s1;
            Place("spawn-lane", spawnU0, s1, hubVMin, w);
            var uEnd = spawnU0 + s1;
            var vMin2 = sh.SpawnLDir < 0 ? hubVMin - s2 : hubVMin + w;
            Place("spawn-lane-2", uEnd - w, w, vMin2, s2);
            spawnPieceId = "spawn-lane-2";
            var markerV = sh.SpawnLDir < 0 ? MarkerInsetCells : s2 - MarkerInsetCells;
            spawnAt = frame.LocalAt(0, w, 0, s2, w / 2.0, markerV);
        }
        else
        {
            var segs = sh.SpawnSegCount == 2 && spawnLen >= 2
                ? SplitTwo(spawnLen, sh.SpawnSplitFrac, 1)
                : [spawnLen];
            spawnS1 = segs[0];
            spawnPieceId = "spawn-lane";
            var cur = spawnU0;
            for (var i = 0; i < segs.Length; i++)
            {
                spawnPieceId = i == 0 ? "spawn-lane" : $"spawn-lane-{i + 1}";
                Place(spawnPieceId, cur, segs[i], hubVMin, w);
                cur += segs[i];
            }
            var last = segs[^1];
            spawnAt = frame.LocalAt(0, last, 0, w, last - MarkerInsetCells, w / 2.0);
        }

        // wool side lanes — hosted on the hub's sides or branching off the spawn lane's first run at a
        // sampled depth, dead-ending in the wool plateau (WL1/LN3): segment 1 runs outward in v, segment 2
        // turns backward (u+) hugging the lane's FAR v edge, segment 3 turns outward in v again — long
        // routes come from turns, not stretched runs (LN2)
        var wools = new List<GrownWool>();
        for (var i = 0; i < laneSegLens.Length; i++)
        {
            var side = i == 0 ? -1 : 1;
            var letter = (char)('a' + i);
            var lens = laneSegLens[i];
            var l1 = lens[0];

            // resolve the attachment: the spawn-lane host degrades to the hub when the first run is too
            // short for a full-width attachment with corner clearance, or when the third wool lane owns the
            // spawn's right band
            var (attLo, attHi, vBase) = ResolveAttachment(sh, side, hubUMin, hubVMin, spawnU0, spawnS1, spawnLen, spawnL, i);
            var att = attLo + (int)Math.Round(sh.LaneAttFracs[i] * (attHi - attLo));

            var seg1VMin = side < 0 ? vBase - l1 : vBase;
            Place($"wool-lane-{letter}", att, w, seg1VMin, l1);

            string markerPiece;
            double markerU, markerV;
            int mUMin, mUSpan, mVMin, mVSpan;
            if (lens.Length == 1)
            {
                markerPiece = $"wool-lane-{letter}";
                mUMin = att; mUSpan = w; mVMin = seg1VMin; mVSpan = l1;
                markerU = mUMin + w / 2.0;
                markerV = side < 0 ? mVMin + MarkerInsetCells : mVMin + mVSpan - MarkerInsetCells;
            }
            else
            {
                var l2 = lens[1];
                var w2 = Math.Min(w, l1 - 1);                              // stays clear of the host's edge
                var seg2UMin = att + w;
                var seg2VMin = side < 0 ? seg1VMin : seg1VMin + l1 - w2;   // hug the lane's FAR v edge
                Place($"wool-lane-{letter}-2", seg2UMin, l2, seg2VMin, w2);

                if (lens.Length == 2)
                {
                    markerPiece = $"wool-lane-{letter}-2";
                    mUMin = seg2UMin; mUSpan = l2; mVMin = seg2VMin; mVSpan = w2;
                    markerU = mUMin + mUSpan - MarkerInsetCells;
                    markerV = mVMin + w2 / 2.0;
                }
                else
                {
                    var l3 = lens[2];
                    var w3 = Math.Min(w, l2 - 1);                 // stays clear of segment 1's far edge
                    var seg3UMin = seg2UMin + l2 - w3;            // hug segment 2's far u end
                    var seg3VMin = side < 0 ? seg2VMin - l3 : seg2VMin + w2;
                    Place($"wool-lane-{letter}-3", seg3UMin, w3, seg3VMin, l3);

                    markerPiece = $"wool-lane-{letter}-3";
                    mUMin = seg3UMin; mUSpan = w3; mVMin = seg3VMin; mVSpan = l3;
                    markerU = mUMin + w3 / 2.0;
                    markerV = side < 0 ? mVMin + MarkerInsetCells : mVMin + mVSpan - MarkerInsetCells;
                }
            }
            wools.Add(new GrownWool(markerPiece, frame.LocalAt(mUMin, mUSpan, mVMin, mVSpan, markerU, markerV)));
        }

        // third wool (WL6) — a narrow (2-cell) dead-end lane straight back from the hub's back edge, beside
        // the spawn lane with a 1-cell gap; the hub width floor guarantees both fit inside the edge
        if (woolCLen > 0)
        {
            var cVMin = hubVMin + w + 1;
            Place("wool-lane-c", spawnU0, woolCLen, cVMin, 2);
            wools.Add(new GrownWool("wool-lane-c", frame.LocalAt(
                spawnU0, woolCLen, cVMin, 2, spawnU0 + woolCLen - MarkerInsetCells, cVMin + 1)));
        }

        return (pieces, spawnPieceId, spawnAt, wools);
    }

    /// <summary>The feasible attachment interval (u of the arm's near edge) and outward base line (v) for a
    /// side lane, on its sampled host. Hub host: anywhere along the hub side between the front inset and a
    /// 1-cell back-corner clearance. Spawn host: along the spawn lane's first run, 1 cell behind the hub's
    /// back corner, clear of any continuation piece at the run's end — and clear of an L-hook's own band when
    /// the hook turns to this side. Degrades to the hub host when no interval survives.</summary>
    private static (int Lo, int Hi, int VBase) ResolveAttachment(
        Shape sh, int side, int hubUMin, int hubVMin, int spawnU0, int spawnS1, int spawnLen, bool spawnL, int lane)
    {
        var w = sh.W;
        var hubLo = hubUMin + sh.WoolInset;
        var hubHi = Math.Max(hubLo, spawnU0 - w - 1);
        var hubVBase = side < 0 ? hubVMin : hubVMin + sh.HubV;

        var wantSpawn = sh.LaneSpawnHost[lane]
            && !(sh.WoolCount == 3 && side > 0);           // the third wool owns the spawn's right band
        if (!wantSpawn) return (hubLo, hubHi, hubVBase);

        var continues = spawnL || (sh.SpawnSegCount == 2 && spawnLen >= 2 && !spawnL);
        var lo = spawnU0 + 1;
        var hi = spawnU0 + spawnS1 - w - (continues ? 1 : 0);
        if (spawnL && sh.SpawnLDir == side)
            hi = Math.Min(hi, spawnU0 + spawnS1 - 2 * w - 1);   // stay clear of the hook's own band
        if (hi < lo) return (hubLo, hubHi, hubVBase);

        var vBase = side < 0 ? hubVMin : hubVMin + w;
        return (lo, hi, vBase);
    }

    // ── deterministic sizing helpers ────────────────────────────────────────────────────────────────────

    /// <summary>Split a total into two parts by a pre-drawn fraction, each at least <paramref name="min"/>.</summary>
    private static int[] SplitTwo(int total, double frac, int min)
    {
        var first = Math.Clamp((int)Math.Round(total * frac), min, total - min);
        return [first, total - first];
    }

    /// <summary>Distribute a lane's total length over its segments: every segment starts at 3 cells (room
    /// for the next turn's attachment and the marker inset), the remainder spreads round-robin up to each
    /// segment's cap (the side run may carry a tighter cap than the chain cap).</summary>
    private static int[] DistributeLane(int total, int segCount, int seg1Cap, int chainCap)
    {
        var lens = new int[segCount];
        Array.Fill(lens, 3);
        int CapOf(int j) => j == 0 ? seg1Cap : chainCap;
        var rest = total - 3 * segCount;
        var idx = 0;
        while (rest > 0)
        {
            if (!Enumerable.Range(0, segCount).Any(j => lens[j] < CapOf(j))) break;
            if (lens[idx] < CapOf(idx)) { lens[idx]++; rest--; }
            idx = (idx + 1) % segCount;
        }
        return lens;
    }

    /// <summary>Apply <paramref name="steps"/> one-cell inflations round-robin across every wool-lane
    /// segment (then the third wool lane), skipping segments already at their cap.</summary>
    private static (int[][] LaneSegLens, int WoolCLen) ApplyInflation(
        int[][] baseLens, int baseWoolC, int steps, int seg1Cap, int chainCap, bool hasWoolC)
    {
        var lens = baseLens.Select(l => (int[])l.Clone()).ToArray();
        var c = baseWoolC;
        var knobs = new List<(int Lane, int Seg)>();
        for (var i = 0; i < lens.Length; i++)
            for (var j = 0; j < lens[i].Length; j++)
                knobs.Add((i, j));
        var knobCount = knobs.Count + (hasWoolC ? 1 : 0);
        int CapOf(int seg) => seg == 0 ? seg1Cap : chainCap;

        var idx = 0;
        var guard = 0;
        while (steps > 0 && guard++ < 10_000)
        {
            var anyRoom = lens.Any(l => l.Where((x, j) => x < CapOf(j)).Any()) || (hasWoolC && c < chainCap);
            if (!anyRoom) break;
            if (idx < knobs.Count)
            {
                var (li, sj) = knobs[idx];
                if (lens[li][sj] < CapOf(sj)) { lens[li][sj]++; steps--; }
            }
            else if (hasWoolC && c < chainCap) { c++; steps--; }
            idx = (idx + 1) % knobCount;
        }
        return (lens, c);
    }

    // ── hard-invariant validation ───────────────────────────────────────────────────────────────────────

    /// <summary>The longest maximal collinear chain over a set of cell rects, in blocks: rects with the same
    /// cross-axis interval that abut along the run axis merge into one chain (a lane cut into collinear
    /// pieces is still one lane); a jog, a width change, or a corner touch breaks the chain. LN2 caps the
    /// result at <see cref="LaneChainMaxBlocks"/>.</summary>
    public static int MaxChainBlocks(int cell, IReadOnlyList<int[]> rects)
    {
        var xRuns = LongestRun(rects, r => (r[1], r[3]), r => r[0], r => r[2]);
        var zRuns = LongestRun(rects, r => (r[0], r[2]), r => r[1], r => r[3]);
        return Math.Max(xRuns, zRuns) * cell;
    }

    private static int LongestRun(
        IReadOnlyList<int[]> rects,
        Func<int[], (int, int)> cross, Func<int[], int> runMin, Func<int[], int> runSpan)
    {
        var best = 0;
        foreach (var group in rects.GroupBy(cross))
        {
            var runs = group.OrderBy(runMin).ToList();
            int start = runMin(runs[0]), end = start + runSpan(runs[0]);
            foreach (var r in runs.Skip(1))
            {
                if (runMin(r) == end) end = runMin(r) + runSpan(r);   // abutting — same chain
                else
                {
                    best = Math.Max(best, end - start);
                    start = runMin(r);
                    end = start + runSpan(r);
                }
            }
            best = Math.Max(best, end - start);
        }
        return best;
    }

    /// <summary>Fanned-board separation: no two pieces overlap, and pieces of DIFFERENT orbit images keep at
    /// least <see cref="ImageClearanceBlocks"/> of clearance on some axis — a shared border, however narrow,
    /// would weld two team territories into one island (CT1), and anything under G5's minimum hop leaves the
    /// mid stage no room to bridge. Image 0 is the identity and images 1..order-1 apply the mode's concrete
    /// orbit axes in turn, matching <see cref="Plan.PlanDerived.FanRect"/>.</summary>
    private static bool ValidateGeometry(ComposeEnvelope env, IReadOnlyList<GrownPiece> pieces)
    {
        var order = Symmetry.Order(env.Symmetry);
        var axes = Symmetry.OrbitAxes(env.Symmetry);
        var images = new List<(int K, double X1, double Z1, double X2, double Z2)>();
        foreach (var p in pieces)
        {
            double x1 = p.Rect[0] * env.Cell, z1 = p.Rect[1] * env.Cell;
            double x2 = (p.Rect[0] + p.Rect[2]) * env.Cell, z2 = (p.Rect[1] + p.Rect[3]) * env.Cell;
            for (var k = 0; k < order; k++)
            {
                var (ix1, iz1, ix2, iz2) = FanImage(x1, z1, x2, z2, axes, k);
                images.Add((k, ix1, iz1, ix2, iz2));
            }
        }
        for (var i = 0; i < images.Count; i++)
            for (var j = i + 1; j < images.Count; j++)
            {
                var (a, b) = (images[i], images[j]);
                var ix = Math.Min(a.X2, b.X2) - Math.Max(a.X1, b.X1);
                var iz = Math.Min(a.Z2, b.Z2) - Math.Max(a.Z1, b.Z1);
                if (a.K == b.K)
                {
                    if (ix > 1e-6 && iz > 1e-6) return false;                       // overlap within one image
                }
                else if (ix > -ImageClearanceBlocks + 1e-6 && iz > -ImageClearanceBlocks + 1e-6)
                    return false;                                                   // images too close
            }
        return true;
    }

    private static (double X1, double Z1, double X2, double Z2) FanImage(
        double x1, double z1, double x2, double z2, string[] axes, int k)
    {
        if (k == 0) return (x1, z1, x2, z2);
        (double x, double z)[] corners = [(x1, z1), (x1, z2), (x2, z1), (x2, z2)];
        var axis = axes[k - 1];
        var pts = corners.Select(c => Symmetry.Apply(c.x, c.z, axis, 0, 0)).ToList();
        return (pts.Min(p => p.X), pts.Min(p => p.Z), pts.Max(p => p.X), pts.Max(p => p.Z));
    }

    private static double TotalArea(ComposeEnvelope env, IReadOnlyList<GrownPiece> pieces) =>
        pieces.Sum(p => (double)p.Rect[2] * p.Rect[3] * env.Cell * env.Cell);

    /// <summary>Every pairwise contact among the authored pieces (pre-fan) is a full land interface or no
    /// contact at all — no narrow seams and no bare corners are authored (unlike a hand-authored plan, the
    /// grower has no author judgment to fall back on for either). Reuses <see cref="PlanDerived.Classify"/>
    /// directly so this is exactly the same test the plan validator/editor apply.</summary>
    private static bool ValidateContacts(ComposeEnvelope env, IReadOnlyList<GrownPiece> pieces)
    {
        var derived = pieces
            .Select(p => new DerivedPiece(p.Id, PlanRoles.Piece, PlanDerived.ToBlock(p.Rect, env.Cell), env.Surface, true))
            .ToList();
        for (var i = 0; i < derived.Count; i++)
            for (var j = i + 1; j < derived.Count; j++)
                if (PlanDerived.Classify(derived[i], derived[j]).Kind is not (ContactKind.Land or ContactKind.None))
                    return false;
        return true;
    }

    /// <summary>Wool↔spawn ≥ 20 blocks (WL2), wool↔wool ≥ 45 blocks (WL7), measured marker to marker.</summary>
    private static bool ValidateMarkers(
        ComposeEnvelope env, IReadOnlyDictionary<string, int[]> rectById,
        string spawnPieceId, double[] spawnAt, IReadOnlyList<GrownWool> wools)
    {
        (double X, double Z) Resolve(string pieceId, double[] at)
        {
            var r = rectById[pieceId];
            return ((r[0] + at[0]) * env.Cell, (r[1] + at[1]) * env.Cell);
        }
        static double Dist((double X, double Z) a, (double X, double Z) b) =>
            Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z));

        var spawn = Resolve(spawnPieceId, spawnAt);
        var woolPts = wools.Select(w => Resolve(w.Piece, w.At)).ToList();

        foreach (var w in woolPts)
            if (Dist(w, spawn) < WoolSpawnMin) return false;
        for (var i = 0; i < woolPts.Count; i++)
            for (var j = i + 1; j < woolPts.Count; j++)
                if (Dist(woolPts[i], woolPts[j]) < WoolWoolMin) return false;
        return true;
    }
}
