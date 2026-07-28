using PgmStudio.Geom;
using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Compose;

/// <summary>What the unit needs hung off its hub, sized before any position exists: one <see cref="Demand"/>
/// per neighbour, and the dock style each one implies.</summary>
/// <summary>A neighbour box to seat against the hub: the hub <see cref="Side"/> it docks, its box
/// <see cref="Kind"/>, its outward <see cref="Depth"/> (perpendicular to the hub edge) and along-edge
/// <see cref="Along"/> extent (cells), and its <see cref="Id"/>. Sizing is frame- and form-independent (it
/// reads the budget); only the seat position is not — so the whole set is fixed before the form is chosen.</summary>
internal sealed record Demand(UnitSide Side, BoxKind Kind, int Depth, int Along, string Id, WoolFill? Wool = null);

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
/// <b>Derived, never sampled</b> — see <see cref="StyleOf"/>. The style falls out of the family roll that
/// already happened in <see cref="WoolDemand"/>; there is no "which dock style" draw anywhere.
/// </summary>
internal enum DockStyle { FullMouth, Overhang, ContactPatch }

public static class UnitDemands
{
    /// <summary>A wool family the <b>seat-and-shift</b> docks: a single-entry approach whose one narrow entry
    /// lands on a hub run while the body overhangs. The dual-entry staple/branch (<c>U</c>/<c>H</c>) is <b>not</b>
    /// one — both its entries must land on the host, so it docks its full mouth (the plain seat path), never an
    /// overhang (an overhang would strand the second entry off the hub — a pinch).</summary>
    internal static bool Overhangs(ShapeFamily family) => family is ShapeFamily.L or ShapeFamily.Donut;

    /// <summary>The dock style a demand implies. A single-entry rich wool overhangs; the frontline takes the
    /// contact patch; everything else takes the shape-agnostic full mouth. Note this is the style a demand
    /// <em>starts</em> at: an overhang that finds no clear placement is demoted to the compact <c>I</c> and
    /// re-dispatched as a <see cref="DockStyle.FullMouth"/>.</summary>
    internal static DockStyle StyleOf(Demand demand) =>
        demand.Wool is { } wool && Overhangs(wool.Family) ? DockStyle.Overhang
        : demand.Kind == BoxKind.Frontline ? DockStyle.ContactPatch
        : DockStyle.FullMouth;

    /// <summary>The neighbour boxes to seat: the spawn (a straight I for now — cross = entry width, seats
    /// cleanly; the L's overhanging foot lands next), the budget-share-sized wools, each on its planned side
    /// (the free sides first, a third doubling into the spawn's edge), and — when the plan carries one — the
    /// frontline join on the front side (reach × a face spanning the hub front). The spawn size is the one RNG
    /// draw here; the wool sizes read the budget (generic, no per-family solve), so the whole set is fixed before
    /// the form is chosen and is identical across a fallback re-seat.</summary>
    internal static IReadOnlyList<Demand> Demands(
        ComposeEnvelope env, ComposeRng rng, UnitPlan plan, int laneWidthCells, int hubU, int hubV, int frontReach)
    {
        var demands = new List<Demand>();

        var iSizes = FillProfiles.SpawnSizes.Where(sz => sz.Family == ShapeFamily.I).ToList();
        var size = iSizes[rng.NextInt(0, iSizes.Count)];
        var (spW, spH) = SpawnBoxEmitter.Box(size.Family, laneWidthCells, size.RunCells, size.TurnCells);
        demands.Add(new Demand(plan.Spawn, BoxKind.Spawn, spH, spW, "spawn"));

        // the flexible budget left after the hub, split into a rough share per wool (the spawn takes one too)
        var budgetCells = env.LandPerTeam / (env.Cell * (double)env.Cell);
        var flexible = Math.Max(0.0, budgetCells - hubU * hubV);
        var woolShare = flexible / (plan.Wools.Count + 1.0);
        for (var i = 0; i < plan.Wools.Count; i++)
        {
            var side = plan.Wools[i];
            var edgeLen = side is UnitSide.Front or UnitSide.Back ? hubV : hubU;
            var (fill, along, depth) = WoolDemand(rng, edgeLen, woolShare);
            demands.Add(new Demand(side, BoxKind.Wool, depth, along, $"wool-{(char)('a' + i)}", fill));
        }

        // the frontline join: it docks the hub's front edge with a face spanning it (corner clearance aside) and
        // reaches `frontReach` toward the axis; the filler picks its form (Bar / single / twin) and orientation
        if (plan.Frontline is { } front)
        {
            // G123: the face is no longer pinned to the hub's full front width. A sampled width — seated anywhere
            // along the edge and free to overhang it — is the funnel: the mid meets only part of the hub front,
            // so the two onward routes around the front cost differently. The full face stays the common draw.
            var full = Math.Max(laneWidthCells, hubV - 2 * UnitTuning.CornerClearanceCells);
            var faceWidth = rng.NextBool(UnitTuning.FullFaceChance)
                ? full
                : rng.NextInt(Math.Min(UnitTuning.FaceMinCells, full), full + UnitTuning.FaceOverhangMaxCells + 1);
            // the face-parity law. Under a laterally-flipping symmetry the opposing image reflects v about the
            // axis point, so a face spanning [lo, hi) meets its own image only where [lo, hi) and [-hi, -lo)
            // overlap — which is exact when lo = -hi, i.e. when the span is EVEN and the face is centred. The hub
            // is already forced even for this reason; an odd face lands half a cell off the centre the seat aims
            // at and the band has to reach past it. Parity is all that is required — no lane multiple, and the
            // rule reads the same in blocks as in cells because the cell size is odd.
            if (MidCarver.LateralFlip(env.Symmetry) && faceWidth % 2 != 0) faceWidth--;
            demands.Add(new Demand(front, BoxKind.Frontline, frontReach, faceWidth, "frontline"));
        }
        return demands;
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
    /// The wool lane is always <see cref="UnitTuning.WoolLaneCells"/> (§4), never the map's <c>w</c>.</summary>
    internal static (WoolFill Fill, int Along, int Depth) WoolDemand(ComposeRng rng, int edgeLen, double woolShare)
    {
        var woolLaneCells = UnitTuning.WoolLaneCells;
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
                attachW = rng.NextInt(woolLaneCells, UnitTuning.DonutEntryMaxCells + 1);
                var holeAlong = rng.NextInt(1, UnitTuning.DonutHoleAlongMaxCells + 1);
                var holeDeep = rng.NextInt(woolLaneCells, UnitTuning.DonutHoleDeepMaxCells + 1);
                depth += holeDeep - woolLaneCells;
                along = Math.Max(along, Math.Max(2 * woolLaneCells + holeAlong, attachW + woolLaneCells));
            }
            if (!Overhangs(family) && along > edgeLen)
                (family, woolAtEnd, (along, depth)) = (ShapeFamily.L, false, WoolBoxEmitter.MouthBox(ShapeFamily.L, woolLaneCells));
            return (new WoolFill(family, RoomPlacement.Inline, false, woolAtEnd, attachW), along, depth);
        }

        // the budget's rough lane: the share spread over a narrow along-extent, the rest becoming depth
        var rd = ShapeEmitter.RoomDepthCells;
        var maxDepth = UnitTuning.WoolLengthRatio * Math.Max(woolLaneCells, rd) - 1;
        var narrowAlong = Math.Clamp((int)Math.Round(Math.Sqrt(woolShare)), woolLaneCells, Math.Min(UnitTuning.WoolAlongCapLanes * woolLaneCells, edgeLen));
        var budgetDepth = (int)Math.Round(woolShare / narrowAlong);

        // NB the short-circuit is load-bearing: a lane that would run long side-tucks WITHOUT consuming a draw
        if (budgetDepth > maxDepth || rng.NextBool(UnitTuning.SideRoomChance))
        {
            var tuck = new WoolFill(ShapeFamily.I, RoomPlacement.SideTuck, false);
            var (along, depth) = WoolBoxEmitter.MouthBox(tuck.Family, woolLaneCells, tuck.Placement);
            return (tuck, along, depth);
        }

        return (new WoolFill(ShapeFamily.I, RoomPlacement.Inline, false),
            woolLaneCells, Math.Clamp(budgetDepth, rd + 1, maxDepth));
    }

    /// <summary>Demote a wool demand to the <b>compact inline <c>I</c></b> — the always-seatable shape: a
    /// one-lane mouth at the hub's offered width, its depth capped under the wool length rule. Both seat failures
    /// land here (an overhang with no clear placement, a full mouth no run holds) rather than failing the unit.</summary>
    internal static Demand Compact(Demand demand, int grantedWidthCells) =>
        demand with
        {
            Along = grantedWidthCells,
            Depth = Math.Min(demand.Depth, UnitTuning.WoolLengthRatio * ShapeEmitter.RoomDepthCells - 1),
            Wool = new WoolFill(ShapeFamily.I, RoomPlacement.Inline, false),
        };
}
