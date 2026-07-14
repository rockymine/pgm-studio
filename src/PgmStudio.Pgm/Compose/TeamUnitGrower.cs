using PgmStudio.Pgm.Plan;

namespace PgmStudio.Pgm.Compose;

/// <summary>A grown piece: a rect in cell coordinates (<see cref="Plan.PlanPiece.Rect"/> convention) and its
/// role. The grower authors only anonymous <c>piece</c>-role rects (the default); the role-bearing
/// <c>wool-room</c>/<c>spawn</c> rooms are carved from the terminal lanes afterwards
/// (<see cref="SpawnWoolRooms"/>).
///
/// <para><see cref="Slot"/> is the piece's <b>wool-approach slot role</b> (<see cref="ApproachSlots"/>) when it
/// came from <see cref="WoolBoxEmitter"/> — the shape-internal template position (<c>entry</c>/<c>bar</c>/
/// <c>leg</c>/<c>room</c>, qualified <c>entry-run</c>/<c>room-run</c>/<c>entry-bar</c>/<c>room-bar</c>), which
/// the shift/width/docking rules target. It is distinct from the map-level <see cref="Role"/> and is
/// <c>null</c> for any piece not emitted as part of an approach shape.</para></summary>
public sealed record GrownPiece(string Id, int[] Rect, string Role = PlanRoles.Piece, string? Slot = null);

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
/// sampled depth, and a frontline toward the axis (FR3/FR4) in one of three forms: none, a single chain, or
/// <b>twin chains</b> — two parallel narrow chains whose recessed gap the mid band later seals into a closure
/// hole, the rotation device (CT8). Every attachment shares a straight border of at least 2 cells (a full G2
/// corridor — no narrow seams are authored), no two pieces overlap, pieces of DIFFERENT orbit images keep at
/// least a 10-block clearance on some axis (team territories stay separate islands, bridgeable later at G5's
/// minimum hop — CT1), and no maximal collinear chain of land-joined pieces runs past 50 blocks (LN2).
///
/// Surplus land budget beyond what capped lanes can hold is spent structurally, never by stretching a lane:
/// a third wool lane on big teams, extra lane segments at right angles (turns), a wider hub, frontline
/// chains. Among hard-valid candidates the grower prefers those whose footprint-bbox land fill lands in the
/// corpus allotment band (0.32..0.60) — a soft criterion: an out-of-band unit is kept as a fallback, never
/// rejected. A candidate that fails any hard invariant is discarded and regrown from the same
/// <see cref="ComposeRng"/> — which is why every draw below happens in one fixed order — up to a bounded
/// number of attempts: the grower never emits a violating unit.
/// </summary>
public static class TeamUnitGrower
{
    private const int MaxAttempts = 500;
    private const int FillHuntAttempts = 40;
    private const double AreaTolerance = 0.20;
    private const double WoolSpawnMin = 20;    // WL2, blocks
    private const double WoolWoolMin = 45;     // WL7, blocks

    // corpus team-side allotment band: land area / footprint-bbox area (soft acceptance)
    private const double FillLo = 0.32;
    private const double FillHi = 0.60;

    /// <summary>LN2's hard cap: a lane runs at most this many blocks before a junction or dead end, and the
    /// cap binds the maximal collinear chain of land-joined same-cross-section pieces — a long lane cut into
    /// several collinear pieces is still one lane.</summary>
    public const int LaneChainMaxBlocks = 50;

    /// <summary>Minimum clearance (blocks, on at least one axis) between pieces of different orbit images
    /// and around every isolated piece: G5's smallest hop, so the void stays bridgeable without ever welding
    /// separate islands together (CT1).</summary>
    public const int ImageClearanceBlocks = 10;

    // WL1's corpus inset is ~5 blocks (~1 cell); half a cell is close enough to "not on the wall" while
    // leaving the smallest maps enough wool↔spawn separation to clear WL2 within the area budget.
    private const double MarkerInsetCells = 0.5;

    private enum FrontForm { None, Single, Wide, Twin }

    public static GrownUnit Grow(ComposeEnvelope env, ComposeRng rng) =>
        TryGrowUnit(env, rng, null, MaxAttempts) ?? throw new ComposeException(
            $"team-unit growth could not satisfy its invariants within {MaxAttempts} attempts " +
            $"(land/team {env.LandPerTeam:0}, teams {env.Teams}, symmetry '{env.Symmetry}')");

    /// <summary>Grow a unit under an optional crossing design (whose half-gap becomes the axis margin),
    /// hunting for a footprint fill inside the corpus band: an in-band unit returns immediately; the first
    /// hard-valid out-of-band unit is kept as a fallback and returned once the hunt budget is spent.</summary>
    public static GrownUnit? TryGrowUnit(ComposeEnvelope env, ComposeRng rng, CrossingDesign? design, int maxAttempts)
    {
        GrownUnit? fallback = null;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var unit = TryGrow(env, rng, design);
            if (unit is null) continue;
            if (FootprintFillInBand(unit.Pieces)) return unit;
            fallback ??= unit;
            if (attempt + 1 >= FillHuntAttempts) break;
        }
        return fallback;
    }

    /// <summary>Land area over footprint-bbox area, on the unit's cell rects (the team-side allotment fill).</summary>
    public static double FootprintFill(IReadOnlyList<GrownPiece> pieces)
    {
        var area = pieces.Sum(p => (double)p.Rect[2] * p.Rect[3]);
        var minX = pieces.Min(p => p.Rect[0]);
        var minZ = pieces.Min(p => p.Rect[1]);
        var maxX = pieces.Max(p => p.Rect[0] + p.Rect[2]);
        var maxZ = pieces.Max(p => p.Rect[1] + p.Rect[3]);
        return area / ((double)(maxX - minX) * (maxZ - minZ));
    }

    private static bool FootprintFillInBand(IReadOnlyList<GrownPiece> pieces)
    {
        var fill = FootprintFill(pieces);
        return fill >= FillLo && fill <= FillHi;
    }

    /// <summary>The sampled shape of one growth attempt — everything random, resolved before the
    /// deterministic repair search runs.</summary>
    private sealed record Shape(
        int W, int ChainCap, int AxisMargin, int HubU, int HubV, int WoolInset,
        FrontForm Form, int[] FrontSegs, int FrontVOff, int TwinLen,
        int SpawnSegCount, bool SpawnL, int SpawnLDir, double SpawnSplitFrac, int SpawnURunCap,
        bool[] LaneSpawnHost, double[] LaneAttFracs, bool SideFlip, bool[] WoolEndBack, int WoolCount);

    private static GrownUnit? TryGrow(ComposeEnvelope env, ComposeRng rng, CrossingDesign? design)
    {
        var frame = Frame.For(env.Symmetry);
        var w = env.LandPerTeam > 2500 ? 3 : 2;                       // lane width, cells (LN1: 10; 15 on big maps)
        var chainCap = Math.Max(3, LaneChainMaxBlocks / env.Cell);    // LN2 cap in cells
        // rot_90's wedge punishes sideways reach: cap the side (v-run) segment shallower than the chain cap
        var seg1Cap = env.Teams == 4 ? Math.Min(chainCap, 6) : chainCap;
        double cellArea = env.Cell * (double)env.Cell;
        double budgetCells = env.LandPerTeam / cellArea;
        var clearCells = (ImageClearanceBlocks + env.Cell - 1) / env.Cell;

        // ── structure sampling — fixed draw order (part of the golden contract): (g1) wool-lane count,
        // (g2) per side lane host, (g3) per side lane segment count, (g4) per side lane attachment fraction,
        // (g4b) lone-lane side flip, (g4c) per side lane wool-end back offset, (g5) arm asymmetry fraction,
        // (g6) spawn segment count, (g7) spawn dogleg style + direction, (g8) frontline form (+ single-chain
        // segment count and cross-offset fraction), (g9) hub depth + width, (g10) spawn split fraction,
        // (g11) single-chain split fraction. Draws whose feature is absent are skipped only on deterministic
        // conditions, so a given request always replays the same sequence. The twin form's geometry (chain
        // width 2, recess 2, at the hub width floor 6) is fully determined by the ≤6-cell hub cap and needs
        // no draws. ──
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

        // a lone wool lane picks its side (a two-lane unit keeps the fixed left/right pair)
        var sideFlip = sideLanes == 1 && rng.NextBool(0.5);

        // a straight lane's wool may press against the lane's back edge instead of its centreline — the
        // wool end offsets away from the mid rather than mirroring the lane's parallel run
        var woolEndBack = new bool[sideLanes];
        for (var i = 0; i < sideLanes; i++) woolEndBack[i] = rng.NextBool(0.5);

        var asymFrac = sideLanes == 2 ? rng.NextDouble() : 0.5;

        var spawnSegCount = rng.NextBool(0.5) ? 2 : 1;
        var spawnLStyle = rng.NextBool(0.5);
        var spawnLDirDraw = rng.NextBool(0.5) ? -1 : 1;

        // frontline form: WIDE — a solid face the band docks to fully (FR6) — is the default: it makes room
        // for the MD6 stone grid and pairs with any crossing. Twin (its recess is CT8's hole mechanism) only
        // fits a stone-less/short crossing, so it is the hole-forming exception (degrades to wide when the
        // crossing forbids it); a lone single chain and no frontline are the narrow exceptions.
        var twinAllowed = design?.TwinFrontlineAllowed ?? false;
        var form = FrontForm.None;
        var frontSegCount = 0;
        var frontVFrac = 0.0;
        if (frontlinePossible)
        {
            var roll = rng.NextInt(0, 10);
            form = roll < 1 ? FrontForm.None : roll < 3 ? FrontForm.Single : roll < 6 ? FrontForm.Twin : FrontForm.Wide;
            if (form == FrontForm.Twin && !twinAllowed) form = FrontForm.Wide;
            if (form == FrontForm.Single)
            {
                frontSegCount = rng.NextInt(1, 3);
                frontVFrac = rng.NextDouble();
            }
        }

        // hub (HB1/HB3): the depth floor clears the side-lane inset; the width floor keeps a frontline
        // chain narrower than the hub (no merged spine chain), fits the twin recess (2+2+2), and fits the
        // third wool lane on the back edge; the cap grows toward the 30-block plaza on big budgets.
        var hubCap = env.LandPerTeam >= 3000 ? 6 : env.LandPerTeam >= 1500 ? 5 : frontlinePossible ? 4 : 3;
        // frontline-less boards deepen the hub by one cell so the wool arm can attach a cell off the hub's
        // front corner — keeping the wool-carrying piece clear of the mid band (BZ6) without a frontline
        var hubUFloor = frontlinePossible ? w + 1 + woolInset : w + 2;
        int HubVFloor() => Math.Max(
            form switch { FrontForm.Twin => 6, FrontForm.Single or FrontForm.Wide => w + 1, _ => Math.Max(2, w) },
            woolCount == 3 ? w + 3 : 0);                   // back edge: spawn lane (w) + 1 gap + wool-c lane (2)
        var hubU = rng.NextInt(hubUFloor, Math.Max(hubUFloor, hubCap) + 1);
        var hubV = rng.NextInt(HubVFloor(), Math.Max(HubVFloor(), hubCap) + 1);

        var spawnSplitFrac = rng.NextDouble();
        var frontSplitFrac = form == FrontForm.Single && frontSegCount == 2 ? rng.NextDouble() : 0.5;

        // deterministic capacity repair (no draws): when the sampled shape cannot hold the land budget even
        // with every run at its cap, add structure — more lane segments first, then a frontline.
        int LaneCapCells(int n) => (seg1Cap + (n - 1) * chainCap) * w;
        double FrontCapCells() => form switch
        {
            FrontForm.Twin => 2.0 * chainCap * 2,
            FrontForm.Wide => Math.Min(6, hubV + 2) * 4.0,       // FR6 wide face: width x depth cap
            FrontForm.Single => chainCap * w,
            _ => 0.0,
        };
        double Capacity() =>
            hubU * hubV
            + chainCap * w                                 // spawn (its straight-run cap; an L holds more)
            + FrontCapCells()
            + Enumerable.Range(0, sideLanes).Sum(i => LaneCapCells(laneSegCounts[i]))
            + (woolCount == 3 ? chainCap * 2 : 0);
        while (Capacity() < 0.9 * budgetCells)
        {
            var bumped = false;
            for (var i = 0; i < sideLanes && !bumped; i++)
                if (laneSegCounts[i] < 3) { laneSegCounts[i]++; bumped = true; }
            if (!bumped && frontlinePossible && form == FrontForm.None)
            {
                form = twinAllowed ? FrontForm.Twin : FrontForm.Wide;
                hubV = Math.Max(hubV, HubVFloor());
                bumped = true;
            }
            if (!bumped) break;
        }
        if (Capacity() < (1 - AreaTolerance) * budgetCells) return null;

        // the axis margin: the crossing design's half-gap when composing; rot_90 additionally needs the
        // central band deep enough that a piece and its quarter-turn image keep the image clearance
        var hubClear = hubV - hubV / 2 + clearCells;
        int axisMargin;
        if (design is { } dz)
        {
            axisMargin = dz.HalfGapCells;
            if (env.Teams == 4 && hubClear > axisMargin) return null;
        }
        else
            axisMargin = env.Teams == 4 ? Math.Max(Envelope.AxisMarginCells + 2, hubClear) : Envelope.AxisMarginCells;

        // ── deterministic length solve: distribute the non-hub budget by fixed weights, clamped to the
        // chain caps — the surplus that clamping sheds is what the structural additions above absorb. ──
        double flexible = budgetCells - hubU * hubV;
        if (flexible <= 0) return null;
        const double spawnUnit = 2.0, woolUnit = 1.8, woolCUnit = 1.2;
        var frontUnit = form switch
        {
            FrontForm.Twin => 1.0, FrontForm.Wide => 1.2, FrontForm.Single => 0.5 * frontSegCount, _ => 0.0,
        };
        double totalUnits = spawnUnit + sideLanes * woolUnit + (woolCount == 3 ? woolCUnit : 0) + frontUnit;

        // when the hub is exactly lane-width and no frontline breaks the spine, hub + spawn's straight run
        // merge into one collinear chain — the run must leave the hub room under the cap
        var spawnURunCap = hubV == w && form == FrontForm.None ? Math.Max(2, chainCap - hubU) : chainCap;
        var spawnLFeasible = spawnLStyle && spawnSegCount == 2;
        var spawnLenCap = spawnLFeasible ? spawnURunCap + chainCap : spawnURunCap;
        // every spawn segment keeps a ≥2×2 footprint (a 1-cell-deep marker piece reads too small)
        var spawnLen = Math.Clamp((int)Math.Round(flexible * (spawnUnit / totalUnits) / w), 2, spawnLenCap);

        int[] frontSegs = [];
        var twinLen = 0;
        if (form == FrontForm.Single)
        {
            var frontTotal = Math.Clamp(
                (int)Math.Round(flexible * (frontUnit / totalUnits) / w), 2 * frontSegCount, chainCap);
            frontSegs = frontSegCount == 1 ? [frontTotal] : SplitTwo(frontTotal, frontSplitFrac, 2);
        }
        else if (form == FrontForm.Twin)
            twinLen = Math.Clamp((int)Math.Round(flexible * (frontUnit / totalUnits) / 4), 2, chainCap);
        else if (form == FrontForm.Wide)
            twinLen = Math.Clamp((int)Math.Round(flexible * (frontUnit / totalUnits) / Math.Min(6, hubV + 2)), 2, 4);
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
            // a straight lane may run as short as 2 cells (the tiniest boards); turning lanes keep 3 per
            // segment so every turn keeps a full attachment and the marker its inset
            laneTotals[i] = Math.Clamp(laneTotals[i],
                laneSegCounts[i] == 1 ? 2 : 3 * laneSegCounts[i], LaneCapCells(laneSegCounts[i]) / w);
            laneSegLens[i] = DistributeLane(laneTotals[i], laneSegCounts[i], seg1Cap, chainCap);
        }

        var woolCLen = woolCount == 3
            ? Math.Clamp((int)Math.Round(flexible * (woolCUnit / totalUnits) / 2), 3, chainCap)
            : 0;

        var shape = new Shape(
            w, chainCap, axisMargin, hubU, hubV, woolInset,
            form, frontSegs, frontVOff, twinLen,
            spawnSegCount, spawnLFeasible, woolCount == 3 ? -1 : spawnLDirDraw, spawnSplitFrac, spawnURunCap,
            laneSpawnHost, laneAttFracs, sideFlip, woolEndBack, woolCount);

        // ── repair search (no draws): WL2/WL7, the area window, and the fill preference are coupled —
        // shrink the spawn lane (outer) and inflate the wool lanes toward their caps (inner) until every
        // hard invariant holds, preferring an in-band footprint fill and falling back to the first valid. ──
        GrownUnit? fallback = null;
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

                if (!ComposeGeometry.SeparationOk(env, pieces.Select(p => p.Rect).ToList(), [])) continue;
                if (!ValidateContacts(env, pieces)) continue;
                if (MaxChainBlocks(env.Cell, pieces.Select(p => p.Rect).ToList()) > LaneChainMaxBlocks) continue;
                if (total < env.LandPerTeam * (1 - AreaTolerance)) continue;
                var rectById = pieces.ToDictionary(p => p.Id, p => p.Rect);
                if (!ValidateMarkers(env, rectById, spawnPieceId, spawnAt, wools)) continue;

                var unit = new GrownUnit(pieces, new GrownSpawn(spawnPieceId, spawnAt, frame.TowardAxis), wools);
                if (FootprintFillInBand(pieces)) return unit;
                fallback ??= unit;
            }
        }
        return fallback;
    }

    // ── geometry assembly ───────────────────────────────────────────────────────────────────────────────

    private static (List<GrownPiece> Pieces, string SpawnPieceId, double[] SpawnAt, List<GrownWool> Wools) Build(
        Frame frame, Shape sh, int spawnLen, int[][] laneSegLens, int woolCLen)
    {
        var w = sh.W;
        var frontReach = sh.Form switch
        {
            FrontForm.Twin or FrontForm.Wide => sh.TwinLen,
            FrontForm.Single => sh.FrontSegs.Sum(),
            _ => 0,
        };
        var hubUMin = sh.AxisMargin + frontReach;
        var hubVMin = -(sh.HubV / 2);
        var spawnU0 = hubUMin + sh.HubU;

        var pieces = new List<GrownPiece>();
        void Place(string id, int uMin, int uSpan, int vMin, int vSpan) =>
            pieces.Add(new GrownPiece(id, frame.ToRect(uMin, uSpan, vMin, vSpan)));

        Place("hub", hubUMin, sh.HubU, hubVMin, sh.HubV);

        // frontline — toward the axis (u-) from the hub's front edge. Single: one chain at a sampled cross
        // offset. Twin: two parallel 2-cell chains with a 2-cell recess between them — the mid band seals
        // the recess into a closure hole (CT8's rotation device).
        if (sh.Form == FrontForm.Single)
        {
            var curFrontU = hubUMin;
            for (var i = 0; i < sh.FrontSegs.Length; i++)
            {
                curFrontU -= sh.FrontSegs[i];
                Place(i == 0 ? "frontline" : $"frontline-{i + 1}", curFrontU, sh.FrontSegs[i], hubVMin + sh.FrontVOff, w);
            }
        }
        else if (sh.Form == FrontForm.Twin)
        {
            Place("frontline", sh.AxisMargin, sh.TwinLen, hubVMin, 2);
            Place("frontline-2", sh.AxisMargin, sh.TwinLen, hubVMin + 4, 2);
        }
        else if (sh.Form == FrontForm.Wide)
        {
            // FR6: a solid wide frontline face the mid band docks to fully — centred on the hub front (it may
            // overhang a narrow hub by a cell each side), reaching from the hub front toward the axis. Its
            // width leaves room for the MD6 two-column stone grid.
            var wideW = Math.Min(6, sh.HubV + 2);
            Place("frontline", sh.AxisMargin, sh.TwinLen, hubVMin + (sh.HubV - wideW) / 2, wideW);
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
            // both straight segments keep the ≥2×2 footprint, so a two-piece run needs ≥4 cells
            var segs = sh.SpawnSegCount == 2 && spawnLen >= 4
                ? SplitTwo(spawnLen, sh.SpawnSplitFrac, 2)
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
            var side = (i == 0) == !sh.SideFlip ? -1 : 1;
            var letter = (char)('a' + i);
            var lens = laneSegLens[i];
            var l1 = lens[0];

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
                // the sampled back offset presses the wool against the lane's rear edge — away from the mid
                markerU = sh.WoolEndBack[i] ? mUMin + w - MarkerInsetCells : mUMin + w / 2.0;
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
    /// the hook turns to this side. Only a 2-piece spawn lane may host (a wool arm on the spawn MARKER's own
    /// piece would leave the wool reachable only through it — SP1); degrades to the hub host when no interval
    /// survives.</summary>
    private static (int Lo, int Hi, int VBase) ResolveAttachment(
        Shape sh, int side, int hubUMin, int hubVMin, int spawnU0, int spawnS1, int spawnLen, bool spawnL, int lane)
    {
        var w = sh.W;
        // never on the hub's front corner: the arm must stay clear of the mid band's docking line (BZ6)
        var hubLo = Math.Max(hubUMin + sh.WoolInset, sh.AxisMargin + 1);
        var hubHi = Math.Max(hubLo, spawnU0 - w - 1);
        var hubVBase = side < 0 ? hubVMin : hubVMin + sh.HubV;

        var twoPieceSpawn = spawnL || (sh.SpawnSegCount == 2 && spawnLen >= 2);
        var wantSpawn = sh.LaneSpawnHost[lane]
            && twoPieceSpawn                               // the spawn marker must not sit on the host piece (SP1)
            && !(sh.WoolCount == 3 && side > 0);           // the third wool owns the spawn's right band
        if (!wantSpawn) return (hubLo, hubHi, hubVBase);

        var lo = spawnU0 + 1;
        var hi = spawnU0 + spawnS1 - w - 1;                // clear of the continuation at the run's end
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

    /// <summary>Distribute a lane's total length over its segments: a turning lane's segments start at
    /// 3 cells (room for the next turn's attachment and the marker inset), a straight lane may run at 2;
    /// the remainder spreads round-robin up to each segment's cap (the side run may carry a tighter cap
    /// than the chain cap).</summary>
    private static int[] DistributeLane(int total, int segCount, int seg1Cap, int chainCap)
    {
        var floor = segCount == 1 ? 2 : 3;
        var lens = new int[segCount];
        Array.Fill(lens, floor);
        int CapOf(int j) => j == 0 ? seg1Cap : chainCap;
        var rest = total - floor * segCount;
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

    private static double TotalArea(ComposeEnvelope env, IReadOnlyList<GrownPiece> pieces) =>
        pieces.Sum(p => (double)p.Rect[2] * p.Rect[3] * env.Cell * env.Cell);

    /// <summary>Every pairwise contact among the authored pieces (pre-fan) is a full land interface or no
    /// contact at all — no narrow seams and no bare corners are authored (unlike a hand-authored plan, the
    /// grower has no author judgment to fall back on for either). Reuses <see cref="ContactGraph.Classify"/>
    /// directly so this is exactly the same test the plan validator/editor apply.</summary>
    private static bool ValidateContacts(ComposeEnvelope env, IReadOnlyList<GrownPiece> pieces)
    {
        var derived = pieces
            .Select(p => new DerivedPiece(p.Id, PlanRoles.Piece, ContactGraph.ToBlock(p.Rect, env.Cell), env.Surface, true))
            .ToList();
        for (var i = 0; i < derived.Count; i++)
            for (var j = i + 1; j < derived.Count; j++)
                if (ContactGraph.Classify(derived[i], derived[j]).Kind is not (ContactKind.Land or ContactKind.None))
                    return false;
        return true;
    }

    /// <summary>Wool↔spawn ≥ 20 blocks (WL2), wool↔wool ≥ 45 blocks (WL7), measured marker to marker.</summary>
    internal static bool ValidateMarkers(
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
