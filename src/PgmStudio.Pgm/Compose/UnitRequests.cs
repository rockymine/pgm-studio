using PgmStudio.Geom;
using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Compose;

// What the unit needs hung off its hub, sized before any position exists: one NeighbourRequest per
// neighbour, and the dock style each one implies.

/// <summary>A neighbour box to seat against the hub: the hub <see cref="Side"/> it docks, its box
/// <see cref="Kind"/>, its outward <see cref="Depth"/> (perpendicular to the hub edge) and along-edge
/// <see cref="Along"/> extent (cells), and its <see cref="Id"/>. Sizing is frame- and form-independent (it
/// reads the budget); only the seat position is not — so the whole set is fixed before the form is chosen.</summary>
internal sealed record NeighbourRequest(UnitSide Side, BoxKind Kind, int Depth, int Along, string Id, WoolFill? Wool = null);

/// <summary>
/// How a neighbour docks its host. The three styles are indexed by <b>how much is known about where the
/// shape's entries are</b>, which is what makes them three rather than an arbitrary list:
/// <list type="bullet">
/// <item><see cref="FullMouth"/> — nothing is known, so require the <em>whole</em> along-extent to sit
/// inside one free run; every entry then lands wherever they are. This is why the dual-entry staples
/// (<c>U</c>/<c>H</c>/<c>Clamp</c>) dock here — an overhang would strand their second entry off the
/// host.</item>
/// <item><see cref="Overhang"/> — the family has exactly one entry and the emitter can say where, so only
/// that interval must land and the body may hang past the run.</item>
/// <item><see cref="ContactPatch"/> — the frontline is a face, not a corridor, so it has no entry at all;
/// what must hold is that every stretch where it meets a run is at least a lane wide.</item>
/// </list>
/// <b>Derived, never sampled</b> — see <c>StyleOf</c>. The style falls out of the family roll that
/// already happened in <c>WoolRequest</c>; there is no "which dock style" draw anywhere.
/// </summary>
internal enum DockStyle { FullMouth, Overhang, ContactPatch }

public static class UnitRequests
{
    /// <summary>A wool family the <b>seat-and-shift</b> docks: a single-entry approach whose one narrow entry
    /// lands on a hub run while the body overhangs. The dual-entry staple/branch (<c>U</c>/<c>H</c>) is <b>not</b>
    /// one — both its entries must land on the host, so it docks its full mouth (the plain seat path), never an
    /// overhang (an overhang would strand the second entry off the hub — a pinch).</summary>
    internal static bool Overhangs(ShapeFamily family) => family is ShapeFamily.L or ShapeFamily.Donut;

    /// <summary>The narrowest face that can close every bay in a front edge's free <paramref name="runs"/>:
    /// from a lane onto the first run's far shoulder to a lane onto the last run's near one. Zero for an edge
    /// with one run or none, which imposes no floor at all.</summary>
    internal static int SealWidth(IReadOnlyList<(int Start, int Len)>? runs, int laneWidthCells)
    {
        if (runs is not { Count: > 1 }) return 0;
        var ordered = runs.OrderBy(r => r.Start).ToList();
        var from = ordered[0].Start + ordered[0].Len - laneWidthCells;
        var to = ordered[^1].Start + laneWidthCells;
        return Math.Max(0, to - from);
    }

    /// <summary>The dock style a request implies. A single-entry rich wool overhangs; the frontline takes the
    /// contact patch; everything else takes the shape-agnostic full mouth. Note this is the style a request
    /// <em>starts</em> at: an overhang that finds no clear placement is demoted to the compact <c>I</c> and
    /// re-dispatched as a <see cref="DockStyle.FullMouth"/>.</summary>
    internal static DockStyle StyleOf(NeighbourRequest request) =>
        request.Wool is { } wool && Overhangs(wool.Family) ? DockStyle.Overhang
        : request.Kind == BoxKind.Frontline ? DockStyle.ContactPatch
        : DockStyle.FullMouth;

    /// <summary>The neighbour boxes to seat: the spawn (a straight I for now — cross = entry width, seats
    /// cleanly; the L's overhanging foot lands next), the wools, each on its planned side (the free sides
    /// first, a third doubling into the spawn's edge), and the frontline join on the front side (reach × a face spanning the hub front). Each takes its share out of
    /// <paramref name="budget"/> as it is sized, so what the unit leaves unspent is a number rather than an
    /// assumption. The spawn size is the one RNG draw here; the wool sizes read the budget (generic, no
    /// per-family solve), and the set is identical across a fallback re-seat, because the fallback is always
    /// toward a body offering more free surface than the one it replaces.
    ///
    /// <para><paramref name="frontRuns"/> is the hub body's own free runs on the front edge, read before this
    /// is called. It is what lets the face be sized against the edge a neighbour will actually meet instead of
    /// against the box: a body with a <b>bay</b> in its front hands the face a floor, and a face under it
    /// cannot be seated so as to close the bay however it is slid.</para></summary>
    internal static IReadOnlyList<NeighbourRequest> Sample(
        ComposeEnvelope env, ComposeRng rng, LandBudget budget, UnitPlan plan,
        int laneWidthCells, int hubU, int hubV, int frontReach,
        IReadOnlyList<(int Start, int Len)>? frontRuns = null)
    {
        var requests = new List<NeighbourRequest>();

        var iSizes = FillProfiles.SpawnSizes.Where(sz => sz.Family == ShapeFamily.I).ToList();
        var size = iSizes[rng.NextInt(0, iSizes.Count)];
        var (spW, spH) = SpawnBoxEmitter.Box(size.Family, laneWidthCells, size.RunCells, size.TurnCells);
        requests.Add(new NeighbourRequest(plan.Spawn, BoxKind.Spawn, spH, spW, "spawn"));
        budget.Spend(spW * (double)spH);

        // each wool grows into its own share of the whole budget rather than into whatever the hub happened to
        // leave — the shares are what the hub was sized against, so reading them back here is the same statement
        var woolShare = budget.Share(UnitTuning.WoolShare);
        for (var i = 0; i < plan.Wools.Count; i++)
        {
            var side = plan.Wools[i];
            var edgeLen = side is UnitSide.Front or UnitSide.Back ? hubV : hubU;
            var (fill, along, depth) = WoolRequest(rng, env.WoolCorridorCells, edgeLen, woolShare, env.Cell);
            requests.Add(new NeighbourRequest(side, BoxKind.Wool, depth, along, $"wool-{(char)('a' + i)}", fill));
            budget.Spend(along * (double)depth);
        }

        // the frontline join: it docks the hub's front edge with a face spanning it (corner clearance aside) and
        // reaches `frontReach` toward the axis; the filler picks its form (Bar / single / twin) and orientation
        // G123: the face is no longer pinned to the hub's full front width. A sampled width — seated anywhere
        // along the edge and free to overhang it — is the funnel: the mid meets only part of the hub front,
        // so the two onward routes around the front cost differently. The full face stays the common draw.
        var full = Math.Max(laneWidthCells, hubV - 2 * UnitTuning.CornerClearanceCells);
        // the floor a bay-fronted body imposes: a face closing a bay has to reach a lane onto the shoulder
        // each side of it, so the narrowest useful face spans from the near shoulder's lane to the far one's.
        // Below that no seat can close the bay, and the body's own bay stays an open notch (CT8's rotation
        // hole is a hole the frontline made). A solid front imposes nothing and the sample is the funnel's.
        // the parity law below rounds an ODD face down a cell, which would take it back under a floor it
        // has to clear — so a floor is rounded UP to even first, and a draw above an even floor stays
        // above it however the parity falls
        var flip = MidCarver.LateralFlip(env.Symmetry);
        var seal = Math.Min(full, SealWidth(frontRuns, laneWidthCells));
        if (flip && seal % 2 != 0) seal = Math.Min(full, seal + 1);
        var floor = Math.Max(Math.Min(UnitTuning.FaceMinCells, full), seal);
        var faceWidth = rng.NextBool(UnitTuning.FullFaceChance)
            ? full
            : rng.NextInt(floor, Math.Max(floor, full + UnitTuning.FaceOverhangMaxCells) + 1);
        // the face-parity law. Under a laterally-flipping symmetry the opposing image reflects v about the
        // axis point, so a face spanning [lo, hi) meets its own image only where [lo, hi) and [-hi, -lo)
        // overlap — which is exact when lo = -hi, i.e. when the span is EVEN and the face is centred. The hub
        // is already forced even for this reason; an odd face lands half a cell off the centre the seat aims
        // at and the band has to reach past it. Parity is all that is required — no lane multiple, and the
        // rule reads the same in blocks as in cells because the cell size is odd.
        if (flip && faceWidth % 2 != 0) faceWidth--;
        requests.Add(new NeighbourRequest(UnitSide.Front, BoxKind.Frontline, frontReach, faceWidth, "frontline"));
        budget.Spend(faceWidth * (double)frontReach);
        return requests;
    }

    /// <summary>Choose one wool's <b>shape and footprint</b> — the whole per-wool decision in one place. Three
    /// outcomes, in the order they are sampled:
    /// <list type="bullet">
    /// <item><b>rich</b> — a donut or a full-mouth staple (<c>U</c>/<c>H</c>/clamp), else a bent <c>L</c>; sized at
    /// the family's mouth box. A staple whose mouth the hub edge (<paramref name="edgeLen"/>) cannot hold demotes
    /// to the <c>L</c>, which the seat-and-shift docks at any width.</item>
    /// <item><b>side-tuck</b> — a compact side-room <c>I</c>, taken when the budget lane would run long (the wool
    /// length rule) or simply by chance.</item>
    /// <item><b>back-room lane</b> — a short inline <c>I</c>, its depth the budget share capped under the same
    /// length rule.</item>
    /// </list>
    /// The wool lane is the band's own <paramref name="woolLaneCells"/> (§4), one rung under the map's
    /// <c>w</c>. A back-room lane runs at least <see cref="WallPlacer.LaneCells"/> before its room, so it seats a
    /// defence wall.</summary>
    internal static (WoolFill Fill, int Along, int Depth) WoolRequest(
        ComposeRng rng, int woolLaneCells, int edgeLen, double woolShare, int cell)
    {
        if (rng.NextBool(UnitTuning.BentWoolChance))
        {
            var family = rng.NextBool(UnitTuning.DonutChance) ? ShapeFamily.Donut
                : rng.NextBool(UnitTuning.StapleChance) ? rng.Pick(new[] { ShapeFamily.U, ShapeFamily.H, ShapeFamily.Clamp })
                : ShapeFamily.L;
            // clamp: adjacent vs centered; donut: the wool at the ring's corner vs on a trailing room
            var woolAtEnd = family switch
            {
                ShapeFamily.Clamp => rng.NextBool(UnitTuning.ClampAdjacentChance),
                ShapeFamily.Donut => rng.NextBool(UnitTuning.DonutCornerWoolChance),
                _ => false,
            };
            var (along, depth) = WoolBoxEmitter.MouthBox(family, woolLaneCells, woolAtEnd: woolAtEnd);
            // the donut's growth knobs: the hub-entry width (the min-only one-corridor entry read as a real
            // chokepoint) and the enclosed hole up to the along × deep caps — the box grows and the emitter's
            // ring absorbs it. The min box stays the floor, so a crowded hub falls back exactly as before.
            var attachW = 0;
            if (family == ShapeFamily.Donut)
            {
                attachW = rng.NextInt(woolLaneCells, UnitTuning.DonutEntryMaxCells(woolLaneCells) + 1);
                // the hole is one a player rounds rather than jumps, so neither extent starts under a hub hole's
                var holeFloor = UnitTuning.HubHoleCells(cell);
                var holeAlong = rng.NextInt(holeFloor, Math.Max(holeFloor, UnitTuning.DonutHoleAlongMaxCells) + 1);
                var deepFloor = Math.Max(woolLaneCells, holeFloor);
                var holeDeep = rng.NextInt(deepFloor, Math.Max(deepFloor, UnitTuning.DonutHoleDeepMaxCells(woolLaneCells)) + 1);
                depth += holeDeep - woolLaneCells;
                along = Math.Max(along, Math.Max(2 * woolLaneCells + holeAlong, attachW + woolLaneCells));
            }
            // the family's minimum box is a floor, not a size: grow it into the wool's share of the budget, which
            // the emitter absorbs as a longer run, a wider ring or deeper legs. Without this a rich wool costs its
            // minimum whatever band it is built at, and the budget buys nothing on three wools in five.
            (along, depth) = GrowToShare(along, depth, woolShare,
                                         Overhangs(family) ? MaxAlongCells(woolLaneCells) : edgeLen,
                                         MaxDepthCells(woolLaneCells));
            if (!Overhangs(family) && along > edgeLen)
                (family, woolAtEnd, (along, depth)) = (ShapeFamily.L, false, WoolBoxEmitter.MouthBox(ShapeFamily.L, woolLaneCells));
            return (new WoolFill(family, RoomPlacement.Inline, false, woolAtEnd, attachW), along, depth);
        }

        // the budget's rough lane: the share spread over a narrow along-extent, the rest becoming depth
        var rd = ShapeEmitter.RoomDepthCells;
        var maxDepth = MaxDepthCells(woolLaneCells);
        var narrowAlong = Math.Clamp((int)Math.Round(Math.Sqrt(woolShare)), woolLaneCells, Math.Min(MaxAlongCells(woolLaneCells), edgeLen));
        var budgetDepth = (int)Math.Round(woolShare / narrowAlong);

        // NB the short-circuit is load-bearing: a lane that would run long side-tucks WITHOUT consuming a draw
        if (budgetDepth > maxDepth || rng.NextBool(UnitTuning.SideRoomChance))
        {
            var tuck = new WoolFill(ShapeFamily.I, RoomPlacement.SideTuck, false);
            var (along, depth) = WoolBoxEmitter.MouthBox(tuck.Family, woolLaneCells, tuck.Placement);
            return (tuck, along, depth);
        }

        return (new WoolFill(ShapeFamily.I, RoomPlacement.Inline, false),
            woolLaneCells, Math.Clamp(budgetDepth, Math.Min(rd + WallPlacer.LaneCells(cell), maxDepth), maxDepth));
    }

    /// <summary>The deepest a wool box may run outward from the hub, in cells — the wool length rule, over
    /// whichever is larger of its corridor and its room depth.</summary>
    private static int MaxDepthCells(int woolLaneCells) =>
        UnitTuning.WoolLengthRatio * Math.Max(woolLaneCells, ShapeEmitter.RoomDepthCells) - 1;

    /// <summary>The widest a wool box may run along the hub edge, in cells — the along-extent a share is spread
    /// over before it turns into depth.</summary>
    private static int MaxAlongCells(int woolLaneCells) => UnitTuning.WoolAlongCapLanes * woolLaneCells;

    /// <summary>Grow an <paramref name="along"/> × <paramref name="depth"/> box until it holds
    /// <paramref name="share"/> cells or meets a cap, adding to whichever side is shorter so the box spreads
    /// rather than stretching into a corridor. Both caps bind: a box that cannot reach its share leaves the
    /// difference unspent, which the spend gate reads.</summary>
    private static (int Along, int Depth) GrowToShare(
        int along, int depth, double share, int alongCap, int depthCap)
    {
        while (along * (double)depth < share)
        {
            if (along <= depth && along < alongCap) along++;
            else if (depth < depthCap) depth++;
            else if (along < alongCap) along++;
            else break;
        }
        return (along, depth);
    }

    /// <summary>Demote a wool request to the <b>compact inline <c>I</c></b> — the always-seatable shape: a
    /// one-lane mouth at the hub's offered width, its depth capped under the wool length rule and never short of
    /// the lane a wall seats in (<see cref="WallPlacer.LaneCells"/>). Both seat failures land here (an overhang
    /// with no clear placement, a full mouth no run holds) rather than failing the unit.</summary>
    internal static NeighbourRequest Compact(NeighbourRequest request, int grantedWidthCells, int cell)
    {
        var walled = ShapeEmitter.RoomDepthCells + WallPlacer.LaneCells(cell);
        var cap = Math.Max(walled, UnitTuning.WoolLengthRatio * ShapeEmitter.RoomDepthCells - 1);
        return request with
        {
            Along = grantedWidthCells,
            Depth = Math.Clamp(request.Depth, walled, cap),
            Wool = new WoolFill(ShapeFamily.I, RoomPlacement.Inline, false),
        };
    }
}
