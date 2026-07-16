using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Compose;

/// <summary>A grown piece: a rect in cell coordinates (<see cref="Plan.PlanPiece.Rect"/> convention), its
/// map-level role, and — when it came out of a box fill — its shape-internal <see cref="Slot"/>
/// (<see cref="ApproachSlots"/>) and its <see cref="Box"/> ownership. Slot + box are the full label
/// (<c>wool-a/entry</c>) the compose-side rules bind to; both are <c>null</c> on pieces the grower authors
/// directly (hub, spawn lane, frontline — until those become boxes too). Labels are compose-internal: every
/// compose move preserves them, and they drop only at <see cref="Composer"/> assembly (the written plan is
/// label-free by design).</summary>
public sealed record GrownPiece(string Id, int[] Rect, string Role = PlanRoles.Piece, string? Slot = null, BoxRef? Box = null);

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
/// an L (LN2/SP2/SP3), 1-3 <b>wool boxes</b> (WL6) docked on the hub's sides or the spawn lane's first run,
/// and a frontline toward the axis (FR3/FR4) in one of three forms: none, a single chain, or <b>twin
/// chains</b> — two parallel narrow chains whose recessed gap the mid band later seals into a closure hole,
/// the rotation device (CT8).
///
/// A wool arm is a typed box fill, not an inline lane: the arm's budget share sizes a <see cref="Box"/>
/// docking the host through a one-lane mouth, <see cref="FillMenu"/> gates which families the mouth admits,
/// and <see cref="WoolBoxEmitter.Fill"/> emits the family — every piece carrying its slot + box label, the
/// wool room emitted as a real role-bearing piece. Structural surplus is spent by the family escalating
/// (I → L → Z → scythe/clamp/U/H fit the budget share), never by stretching one lane.
///
/// Every attachment shares a straight border of at least 2 cells (a full G2 corridor — no narrow seams are
/// authored), no two pieces overlap, pieces of DIFFERENT orbit images keep at least a 10-block clearance on
/// some axis (team territories stay separate islands, bridgeable later at G5's minimum hop — CT1), and no
/// maximal collinear chain of land-joined pieces runs past 50 blocks (LN2). Among hard-valid candidates the
/// grower prefers those whose footprint-bbox land fill lands in the corpus allotment band (0.32..0.60) — a
/// soft criterion: an out-of-band unit is kept as a fallback, never rejected. A candidate that fails any
/// hard invariant is discarded and regrown from the same <see cref="ComposeRng"/> — which is why every draw
/// below happens in one fixed order — up to a bounded number of attempts: the grower never emits a
/// violating unit.
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
        int W, int ChainCap, int ArmDepthCap, int AxisMargin, int HubU, int HubV, int WoolInset,
        FrontForm Form, int[] FrontSegs, int FrontVOff, int TwinLen,
        int SpawnSegCount, bool SpawnL, int SpawnLDir, double SpawnSplitFrac, int SpawnURunCap,
        bool[] LaneSpawnHost, ShapeFamily[] LaneFamily, int[] LaneWidth, RoomPlacement[] LaneRoom,
        double[] LaneAttFracs, bool[] LaneFlip, bool SideFlip, int WoolCount);

    private static GrownUnit? TryGrow(ComposeEnvelope env, ComposeRng rng, CrossingDesign? design)
    {
        var frame = Frame.For(env.Symmetry);
        var w = env.LandPerTeam > 2500 ? 3 : 2;                       // lane width, cells (LN1: 10; 15 on big maps)
        var chainCap = Math.Max(3, LaneChainMaxBlocks / env.Cell);    // LN2 cap in cells
        // rot_90's wedge punishes sideways reach: cap the arm's outward depth shallower than the chain cap
        // (the separation check still rejects any candidate whose quarter-turn image comes too close)
        var armDepthCap = env.Teams == 4 ? Math.Min(chainCap, 8) : chainCap;
        double cellArea = env.Cell * (double)env.Cell;
        double budgetCells = env.LandPerTeam / cellArea;
        var clearCells = (ImageClearanceBlocks + env.Cell - 1) / env.Cell;

        // ── structure sampling — fixed draw order (part of the golden contract): (g1) wool-box count,
        // (g2) per side box host, (g3) per side family roll, (g4) per side attachment fraction, (g4b)
        // lone-box side flip, (g4c) per side emit flip (handedness), (g4d) per side room side-dock roll
        // (applies where the family supports it), (g5) arm asymmetry fraction, (g6) spawn
        // segment count, (g7) spawn dogleg style + direction, (g8) frontline form (+ single-chain segment
        // count and cross-offset fraction), (g9) hub depth + width, (g10) spawn split fraction, (g11)
        // single-chain split fraction. Draws whose feature is absent are skipped only on deterministic
        // conditions, so a given request always replays the same sequence. A family roll resolves later
        // against the deterministic fit-filtered menu (roll mod list size), so the roll itself is
        // share-independent. The twin form's geometry (chain width 2, recess 2, at the hub width floor 6)
        // is fully determined by the ≤6-cell hub cap and needs no draws. ──
        var frontlinePossible = env.LandPerTeam >= 800;
        var woolInset = frontlinePossible ? 1 : 0;

        var woolCount = env.PlayersPerTeam >= 16 ? (rng.NextBool(0.4) ? 3 : 2)
            : env.LandPerTeam < 600 ? 1
            : rng.NextInt(1, 3);
        var sideLanes = Math.Min(woolCount, 2);

        var laneSpawnHost = new bool[sideLanes];
        for (var i = 0; i < sideLanes; i++) laneSpawnHost[i] = rng.NextBool(0.35);

        var laneFamilyRolls = new int[sideLanes];
        for (var i = 0; i < sideLanes; i++) laneFamilyRolls[i] = rng.NextInt(0, 4096);

        var laneAttFracs = new double[sideLanes];
        for (var i = 0; i < sideLanes; i++) laneAttFracs[i] = rng.NextDouble();

        // a lone wool box picks its side (a two-box unit keeps the fixed left/right pair)
        var sideFlip = sideLanes == 1 && rng.NextBool(0.5);

        var laneFlip = new bool[sideLanes];
        for (var i = 0; i < sideLanes; i++) laneFlip[i] = rng.NextBool(0.5);

        var laneSideDockRoll = new bool[sideLanes];
        for (var i = 0; i < sideLanes; i++) laneSideDockRoll[i] = rng.NextBool(0.25);

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

        // hub (HB1/HB3): the depth floor clears the side-box inset; the width floor keeps a frontline
        // chain narrower than the hub (no merged spine chain), fits the twin recess (2+2+2), and fits the
        // third wool lane on the back edge; the cap grows toward the 30-block plaza on big budgets.
        var hubCap = env.LandPerTeam >= 3000 ? 6 : env.LandPerTeam >= 1500 ? 5 : frontlinePossible ? 4 : 3;
        // frontline-less boards deepen the hub by one cell so the wool box can dock a cell off the hub's
        // front corner — keeping the wool-carrying piece clear of the mid band (BZ6) without a frontline
        var hubUFloor = frontlinePossible ? w + 1 + woolInset : w + 2;
        int HubVFloor() => Math.Max(
            form switch { FrontForm.Twin => 6, FrontForm.Single or FrontForm.Wide => w + 1, _ => Math.Max(2, w) },
            woolCount == 3 ? w + 3 : 0);                   // back edge: spawn lane (w) + 1 gap + wool-c lane (2)
        var hubU = rng.NextInt(hubUFloor, Math.Max(hubUFloor, hubCap) + 1);
        var hubV = rng.NextInt(HubVFloor(), Math.Max(HubVFloor(), hubCap) + 1);

        var spawnSplitFrac = rng.NextDouble();
        var frontSplitFrac = form == FrontForm.Single && frontSegCount == 2 ? rng.NextDouble() : 0.5;

        // the hub-host mouth window (host edge cells a box's mouth row may touch): the interval between the
        // hub's front inset (BZ6 discipline — never the front corner) and a 1-cell back-corner clearance.
        // Its LENGTH is placement-independent, so the family fit below can use it before geometry assembly.
        var hubWindowLen = Math.Max(0, form == FrontForm.None ? hubU - 2 : hubU - woolInset - 1);

        // deterministic capacity screen (no draws): when even the roomiest family per arm cannot hold the
        // land budget, add structure — a frontline first; else give up on this attempt.
        double ArmCapacity() => FillMenu.ProductionFamilies
            .Where(f => FitsArm(f, w, hubWindowLen, armDepthCap))
            .Select(f => (double)ArmArea(f, w, ArmWidthCap(f, w, hubWindowLen, chainCap),
                ArmDepthCapOf(f, w, hubWindowLen, armDepthCap)))
            .DefaultIfEmpty(0)
            .Max();
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
            + sideLanes * ArmCapacity()
            + (woolCount == 3 ? chainCap * 2 : 0);
        if (Capacity() < 0.9 * budgetCells && frontlinePossible && form == FrontForm.None)
        {
            form = twinAllowed ? FrontForm.Twin : FrontForm.Wide;
            hubV = Math.Max(hubV, HubVFloor());
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
        // family/chain caps — the surplus that clamping sheds is what the structural additions absorb. ──
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

        // arm area shares: equal weighted shares skewed by the asymmetry draw
        var armShares = new int[sideLanes];
        for (var i = 0; i < sideLanes; i++)
            armShares[i] = (int)Math.Round(flexible * (woolUnit / totalUnits));
        if (sideLanes == 2)
        {
            var shift = (int)Math.Round((asymFrac - 0.5) * 0.8 * Math.Min(armShares[0], armShares[1]));
            armShares[0] += shift;
            armShares[1] -= shift;
        }

        // resolve each arm's family: the roll indexes the deterministic fit-filtered menu — a family whose
        // mouth fits the hub window, whose depth fits the wedge cap, and whose minimum area is not out of
        // all proportion to the arm's share. The fallback is the straight lane (always fits).
        var laneFamily = new ShapeFamily[sideLanes];
        var laneWidth = new int[sideLanes];
        var laneDepth = new int[sideLanes];
        var laneRoom = new RoomPlacement[sideLanes];
        for (var i = 0; i < sideLanes; i++)
        {
            var fit = FillMenu.FamiliesFor(w)
                .Where(f => FitsArm(f, w, hubWindowLen, armDepthCap))
                .Where(f => ArmArea(f, w, ShapeEmitter.MinBox(f, w).W, ShapeEmitter.MinBox(f, w).H) <= armShares[i] * 1.25 + 2)
                .ToList();
            var family = fit.Count == 0 ? ShapeFamily.I : fit[laneFamilyRolls[i] % fit.Count];
            // the side-dock roll applies where the family supports it and its (slightly larger) minimum
            // box still fits the caps — else the room stays inline
            var rp = laneSideDockRoll[i] && family is ShapeFamily.I or ShapeFamily.Z
                && ShapeEmitter.MinBox(family, w, RoomPlacement.SideTuck).H
                    <= ArmDepthCapOf(family, w, hubWindowLen, armDepthCap)
                ? RoomPlacement.SideTuck : RoomPlacement.Inline;
            var depthCap = ArmDepthCapOf(family, w, hubWindowLen, armDepthCap);
            var minW = ShapeEmitter.MinBox(family, w, rp).W;
            laneFamily[i] = family;
            laneRoom[i] = rp;
            laneDepth[i] = SolveDepth(family, w, minW, armShares[i], depthCap, rp);
            laneWidth[i] = ArmArea(family, w, minW, depthCap, rp) >= armShares[i]
                ? minW
                : SolveWidth(family, w, armShares[i], depthCap, ArmWidthCap(family, w, hubWindowLen, chainCap), rp);
        }

        var woolCLen = woolCount == 3
            ? Math.Clamp((int)Math.Round(flexible * (woolCUnit / totalUnits) / 2), 3, chainCap)
            : 0;

        var shape = new Shape(
            w, chainCap, armDepthCap, axisMargin, hubU, hubV, woolInset,
            form, frontSegs, frontVOff, twinLen,
            spawnSegCount, spawnLFeasible, woolCount == 3 ? -1 : spawnLDirDraw, spawnSplitFrac, spawnURunCap,
            laneSpawnHost, laneFamily, laneWidth, laneRoom, laneAttFracs, laneFlip, sideFlip, woolCount);

        // ── repair search (no draws): WL2/WL7, the area window, and the fill preference are coupled —
        // shrink the spawn lane (outer) and inflate the wool boxes toward their depth caps (inner) until
        // every hard invariant holds, preferring an in-band footprint fill and falling back to the first
        // valid. ──
        GrownUnit? fallback = null;
        var maxInflate = Enumerable.Range(0, sideLanes)
                .Sum(i => ArmDepthCapOf(laneFamily[i], w, hubWindowLen, armDepthCap) - laneDepth[i])
            + (woolCount == 3 ? chainCap - woolCLen : 0);
        for (var shrink = 0; shrink <= Math.Min(6, spawnLen - 1); shrink++)
        {
            var s = spawnLen - shrink;
            for (var inflate = 0; inflate <= maxInflate; inflate++)
            {
                var (depths, cLen) = ApplyInflation(
                    laneDepth, woolCLen, inflate, i => ArmDepthCapOf(laneFamily[i], w, hubWindowLen, armDepthCap),
                    chainCap, woolCount == 3);
                var built = Build(frame, shape, s, depths, cLen);
                if (built is null) continue;
                var (pieces, spawnPieceId, spawnAt, wools) = built.Value;

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

    // ── the arm-as-box helpers (deterministic, draw-free) ───────────────────────────────────────────────

    /// <summary>The emitted land area (cells) of a family filling a canonical box — monotone in the depth
    /// (every family spans it) and, for the bar-carrying families, in the width (their bar spans it): depth
    /// is the arm's repair knob, width its budget-absorption knob.</summary>
    private static int ArmArea(ShapeFamily family, int cw, int width, int depth, RoomPlacement rp = RoomPlacement.Inline)
    {
        var (minW, minH) = ShapeEmitter.MinBox(family, cw, rp);
        var s = ShapeEmitter.Emit(family, Math.Max(width, minW), Math.Max(depth, minH), cw, roomPlacement: rp);
        return s.Terrain.Sum(p => p.Rect[2] * p.Rect[3]) + s.Room[2] * s.Room[3];
    }

    /// <summary>How wide a family's canonical box may grow. Widening only pays where a bar spans the box
    /// (L's band, Z's crossing bar, U/H's crossbar — each capped by LN2's chain limit); U/H legs ride the
    /// box edges, so their mouth widens with the box and the host window caps them instead. The straight
    /// lane gains nothing from width (its lane is centred, the rest is unused envelope).</summary>
    private static int ArmWidthCap(ShapeFamily family, int cw, int hostWindowLen, int chainCap)
    {
        var (minW, _) = ShapeEmitter.MinBox(family, cw);
        return family switch
        {
            ShapeFamily.L or ShapeFamily.Z => chainCap,
            ShapeFamily.U or ShapeFamily.H => Math.Max(minW, Math.Min(hostWindowLen, chainCap)),
            _ => minW,
        };
    }

    /// <summary>The width of a family's mouth row — every box-local cell column its mouth-up emission
    /// occupies on the docking edge (entries, and for some families the room). All of it must land on the
    /// host edge, so this is what the host window gates.</summary>
    private static int MouthRowSpan(ShapeFamily family, int cw, int width, int depth, RoomPlacement rp = RoomPlacement.Inline)
    {
        var (minW, minH) = ShapeEmitter.MinBox(family, cw, rp);
        var canonW = Math.Max(width, minW);
        var canonH = Math.Max(depth, minH);
        var raw = ShapeEmitter.Emit(family, canonW, canonH, cw, roomPlacement: rp);
        var (s, _, _) = ShapeEmitter.OrientMouthTop(raw, family, false, canonW, canonH);
        var cells = s.Terrain.Select(p => p.Rect).Append(s.Room).Where(r => r[1] == 0).ToList();
        return cells.Count == 0 ? 0 : cells.Max(r => r[0] + r[2]) - cells.Min(r => r[0]);
    }

    /// <summary>The arm's depth cap for a family: the wedge/chain cap, except a transposing family (the
    /// clamp), whose canonical depth axis lies ALONG the host edge and is capped by the window instead.</summary>
    private static int ArmDepthCapOf(ShapeFamily family, int cw, int hostWindowLen, int armDepthCap) =>
        ShapeEmitter.MouthEdge(family) is BoxEdge.Left or BoxEdge.Right
            ? Math.Min(hostWindowLen, armDepthCap)
            : armDepthCap;

    private static bool FitsArm(ShapeFamily family, int cw, int hostWindowLen, int armDepthCap)
    {
        var (minW, minH) = ShapeEmitter.MinBox(family, cw);
        var cap = ArmDepthCapOf(family, cw, hostWindowLen, armDepthCap);
        return minH <= cap && MouthRowSpan(family, cw, minW, minH) <= hostWindowLen;
    }

    /// <summary>The smallest depth whose emitted area reaches the arm's share (clamped to the caps).</summary>
    private static int SolveDepth(ShapeFamily family, int cw, int width, int shareCells, int depthCap, RoomPlacement rp = RoomPlacement.Inline)
    {
        var (_, minH) = ShapeEmitter.MinBox(family, cw, rp);
        for (var h = minH; h < depthCap; h++)
            if (ArmArea(family, cw, width, h, rp) >= shareCells) return h;
        return Math.Max(minH, depthCap);
    }

    /// <summary>The smallest width whose emitted area at the capped depth reaches the arm's share (clamped
    /// to the family's width cap) — the second solve, run only when depth alone cannot absorb the share.</summary>
    private static int SolveWidth(ShapeFamily family, int cw, int shareCells, int depthCap, int widthCap, RoomPlacement rp = RoomPlacement.Inline)
    {
        var (minW, _) = ShapeEmitter.MinBox(family, cw, rp);
        for (var w2 = minW; w2 < widthCap; w2++)
            if (ArmArea(family, cw, w2, depthCap, rp) >= shareCells) return w2;
        return Math.Max(minW, widthCap);
    }

    // ── geometry assembly ───────────────────────────────────────────────────────────────────────────────

    private static (List<GrownPiece> Pieces, string SpawnPieceId, double[] SpawnAt, List<GrownWool> Wools)? Build(
        Frame frame, Shape sh, int spawnLen, int[] armDepths, int woolCLen)
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

        // wool boxes — docked on the hub's sides or the spawn lane's first run, mouth toward the host: the
        // arm's share picked the family, the depth knob sized the box, and the fill emits slot- and
        // box-labeled pieces with the wool room as a real role-bearing terminal (WL1/LN3)
        var wools = new List<GrownWool>();
        for (var i = 0; i < armDepths.Length; i++)
        {
            var side = (i == 0) == !sh.SideFlip ? -1 : 1;
            var letter = (char)('a' + i);

            var (winLo, winLen, vBase) = ResolveAttachment(
                sh, side, hubUMin, hubVMin, spawnU0, spawnS1, spawnLen, spawnL, i,
                MouthRowSpan(sh.LaneFamily[i], w, sh.LaneWidth[i], armDepths[i], sh.LaneRoom[i]));
            var uFloor = Math.Max(hubUMin + sh.WoolInset, sh.AxisMargin + 1);

            var arm = PlaceArm(
                frame, pieces, sh.LaneFamily[i], w, sh.LaneWidth[i], armDepths[i], sh.LaneFlip[i], sh.LaneRoom[i],
                sh.LaneAttFracs[i], side, winLo, winLen, uFloor, vBase, $"wool-{letter}", $"wool-room-{letter}");
            if (arm is null) return null;
            wools.Add(arm);
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

    /// <summary>Fill one wool box and place it against its host: the emission is normalized mouth-up in a
    /// side-agnostic (u, d) frame — d is outward distance from the host edge — then every rect maps through
    /// the side and the symmetry frame into real cell coordinates. Returns the wool, or null when the mouth
    /// cannot sit inside the host window (the attempt is discarded, never patched).</summary>
    private static GrownWool? PlaceArm(
        Frame frame, List<GrownPiece> pieces, ShapeFamily family, int cw, int width, int depth, bool flip,
        RoomPlacement rp, double attFrac, int side, int winLo, int winLen, int uFloor, int vBase,
        string boxId, string roomId)
    {
        var (minW, minH) = ShapeEmitter.MinBox(family, cw, rp);
        var canonW = Math.Max(width, minW);
        var canonH = Math.Max(depth, minH);
        var raw = ShapeEmitter.Emit(family, canonW, canonH, cw, flip, rp);
        var (s, _, _) = ShapeEmitter.OrientMouthTop(raw, family, flip, canonW, canonH);

        // the mouth row (d = 0) must sit fully on the host edge window, and the whole box body must stay
        // behind the front inset line (a flipped shape's body runs toward the axis from its entry — it may
        // overhang free space away from the mid, never toward it)
        var mouthRects = s.Terrain.Select(p => p.Rect).Append(s.Room).Where(r => r[1] == 0).ToList();
        var mouthLo = mouthRects.Min(r => r[0]);
        var mouthSpan = mouthRects.Max(r => r[0] + r[2]) - mouthLo;
        var placeLo = Math.Max(winLo, uFloor + mouthLo);
        var placeHi = winLo + winLen - mouthSpan;
        if (placeLo > placeHi) return null;
        var boxU0 = placeLo + (int)Math.Round(attFrac * (placeHi - placeLo)) - mouthLo;

        var box = new BoxRef(boxId, BoxKind.Wool);
        var n = 1;
        foreach (var (r, slot) in s.Terrain)
        {
            var vMin = side > 0 ? vBase + r[1] : vBase - r[1] - r[3];
            pieces.Add(new GrownPiece($"{boxId}-t{n++}", frame.ToRect(boxU0 + r[0], r[2], vMin, r[3]),
                PlanRoles.Piece, slot, box));
        }
        var room = s.Room;
        var roomVMin = side > 0 ? vBase + room[1] : vBase - room[1] - room[3];
        pieces.Add(new GrownPiece(roomId, frame.ToRect(boxU0 + room[0], room[2], roomVMin, room[3]),
            PlanRoles.WoolRoom, ApproachSlots.Room, box));

        var markerU = boxU0 + room[0] + s.At[0];
        var markerV = side > 0 ? vBase + room[1] + s.At[1] : vBase - room[1] - s.At[1];
        return new GrownWool(roomId, frame.LocalAt(boxU0 + room[0], room[2], roomVMin, room[3], markerU, markerV));
    }

    /// <summary>The host mouth window (u of its first cell, its length, and the host edge's v line) for a
    /// side box, on its sampled host. Hub host: the hub side between the front inset and a 1-cell
    /// back-corner clearance. Spawn host: along the spawn lane's first run, 1 cell behind the hub's back
    /// corner, clear of any continuation piece at the run's end — and clear of an L-hook's own band when the
    /// hook turns to this side. Only a 2-piece spawn lane may host (a wool box on the spawn MARKER's own
    /// piece would leave the wool reachable only through it — SP1); degrades to the hub host when the
    /// window cannot take the mouth.</summary>
    private static (int Lo, int Len, int VBase) ResolveAttachment(
        Shape sh, int side, int hubUMin, int hubVMin, int spawnU0, int spawnS1, int spawnLen, bool spawnL,
        int lane, int mouthSpan)
    {
        var w = sh.W;
        // never on the hub's front corner: the box must stay clear of the mid band's docking line (BZ6)
        var hubLo = Math.Max(hubUMin + sh.WoolInset, sh.AxisMargin + 1);
        var hubLen = Math.Max(0, spawnU0 - 1 - hubLo);
        var hubVBase = side < 0 ? hubVMin : hubVMin + sh.HubV;

        var twoPieceSpawn = spawnL || (sh.SpawnSegCount == 2 && spawnLen >= 2);
        var wantSpawn = sh.LaneSpawnHost[lane]
            && twoPieceSpawn                               // the spawn marker must not sit on the host piece (SP1)
            && !(sh.WoolCount == 3 && side > 0);           // the third wool owns the spawn's right band
        if (!wantSpawn) return (hubLo, hubLen, hubVBase);

        var lo = spawnU0 + 1;
        var hi = spawnU0 + spawnS1 - w - 1;                // clear of the continuation at the run's end
        if (spawnL && sh.SpawnLDir == side)
            hi = Math.Min(hi, spawnU0 + spawnS1 - 2 * w - 1);   // stay clear of the hook's own band
        var len = hi + w - lo;
        if (len < mouthSpan) return (hubLo, hubLen, hubVBase);

        var vBase = side < 0 ? hubVMin : hubVMin + w;
        return (lo, len, vBase);
    }

    // ── deterministic sizing helpers ────────────────────────────────────────────────────────────────────

    /// <summary>Split a total into two parts by a pre-drawn fraction, each at least <paramref name="min"/>.</summary>
    private static int[] SplitTwo(int total, double frac, int min)
    {
        var first = Math.Clamp((int)Math.Round(total * frac), min, total - min);
        return [first, total - first];
    }

    /// <summary>Apply <paramref name="steps"/> one-cell depth inflations round-robin across every wool box
    /// (then the third wool lane), skipping arms already at their cap.</summary>
    private static (int[] Depths, int WoolCLen) ApplyInflation(
        int[] baseDepths, int baseWoolC, int steps, Func<int, int> capOf, int chainCap, bool hasWoolC)
    {
        var depths = (int[])baseDepths.Clone();
        var c = baseWoolC;
        var knobCount = depths.Length + (hasWoolC ? 1 : 0);
        var idx = 0;
        var guard = 0;
        while (steps > 0 && guard++ < 10_000)
        {
            var anyRoom = depths.Where((d, i) => d < capOf(i)).Any() || (hasWoolC && c < chainCap);
            if (!anyRoom) break;
            if (idx < depths.Length)
            {
                if (depths[idx] < capOf(idx)) { depths[idx]++; steps--; }
            }
            else if (hasWoolC && c < chainCap) { c++; steps--; }
            idx = (idx + 1) % knobCount;
        }
        return (depths, c);
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
    /// directly so this is exactly the same test the plan validator/editor apply. Known over-rejection:
    /// a Corner pair whose diagonal is bridged by a third piece is a ¾-solid inside corner of one connected
    /// mass — harmless (the editor's PC-C lint suppresses it), but this pairwise read cannot see the third
    /// piece; the real cell-level failure is the diagonal pinch (two tiles point-to-point, void/build on
    /// both opposite diagonals). This is what gates the donut family out of
    /// <see cref="FillMenu.ProductionFamilies"/>.</summary>
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
