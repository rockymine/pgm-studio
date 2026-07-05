namespace PgmStudio.Pgm.Compose;

/// <summary>One authored stone row of a crossing: its near-edge offset from the axis and its depth, in
/// cells along the axis-normal. The row's orbit images complete the fanned crossing.</summary>
public readonly record struct StoneRow(int UMin, int Depth);

/// <summary>
/// The u-axis arithmetic of a mid crossing, sampled before the team unit grows (its half-gap is the grower's
/// axis margin). Hops follow G5/G7: the void from a team frontline to the first landing is 10..20 blocks;
/// with rows, the chain front → row(s) → row image(s) → enemy front keeps every hop in that band and the
/// hop sum in 30..60; without rows the single crossing is 20 blocks (or 30 for big teams — the corpus runs
/// 35-block single hops at 30 players/team). <see cref="TwinFrontlineAllowed"/> gates the twin-frontline
/// recess (CT8's hole mechanism): a twin front's second chain reaches the nearest stone diagonally, which
/// only stays a real ≤20-block hop when the front hop is short.
/// </summary>
public sealed record CrossingDesign(int HalfGapCells, IReadOnlyList<StoneRow> Rows, bool TwinFrontlineAllowed);

/// <summary>A mid stepping stone: an anonymous piece inside the band (MD1/MD4), fanned by symmetry.</summary>
public sealed record MidStone(string Id, int[] Rect, int Surface);

/// <summary>The carved mid: the band zone rect (cells) and the stones inside it.</summary>
public sealed record MidResult(int[] BandRect, IReadOnlyList<MidStone> Stones);

/// <summary>
/// Carves the mid in its CLEAN form (CT1): one authored band zone spanning the symmetry axis — its own orbit
/// images overlap it (it contains the centre), so the fanned zones merge into ONE build region, the clean
/// form's definition — plus 0..2 stone rows whose hop arithmetic the crossing design fixed up front. Stones
/// are grid-aligned with the team unit's front/hub edge lines (CT7), sit entirely inside the band (MD4), and
/// keep the full isolation clearance to everything (they connect by building only).
///
/// The band is <b>fit to the crossing it serves</b> (BZ9): laterally a sampled sub-interval between the
/// minimal connecting interval and the hull of the opposing frontline faces — never the board width — with
/// every face interface flush or full (BZ8's readable docks); in depth either <b>flush-docked</b> against
/// the frontline edges (zero overlap) or the sanctioned <b>plaza</b> form lapping one cell onto the
/// frontline pieces it connects (BZ7) — one sampled form per plan. The band touches nothing else: not the
/// hub, not the lanes, and never a wool-carrying piece, which it clears by two full cells (BZ6 — a
/// mid-bridgeable wool would erase the map's gameplay direction).
///
/// Draw order (after the grower's draws): (m1) stone width, (m2) stone column pick, (m3) per-row surface,
/// (m4) band form (flush/plaza), (m5) lateral extension fractions (left, right).
/// </summary>
public static class MidCarver
{

    /// <summary>Sample a crossing design. Draw order (the first draws of a compose attempt): (c1) row
    /// count, (c2) row-0: the deep-single-hop flag / row-1: stone depth, (c3) row-1: front hop, (c4) row-1:
    /// inner offset. The two-row design is fully determined (every hop at the 10-block minimum).</summary>
    public static CrossingDesign SampleCrossing(ComposeEnvelope env, ComposeRng rng)
    {
        var cell = env.Cell;
        var hopMinC = (10 + cell - 1) / cell;              // G5's smallest hop, in cells
        var hopMaxC = Math.Max(hopMinC, 20 / cell);

        // rot_90 units need the deeper hub-clearance margin, so the shallow stone-less crossings are
        // 2-team-only; big teams may take the deep two-row crossing
        var rows = env.Teams == 4
            ? (env.PlayersPerTeam >= 20 ? rng.NextInt(1, 3) : 1)
            : env.PlayersPerTeam >= 20 ? rng.NextInt(0, 3)
            : rng.NextInt(0, 2);

        if (rows == 0)
        {
            var deep = env.PlayersPerTeam >= 20 && rng.NextBool(0.5);   // the ≤35 single-hop sanction
            return new CrossingDesign(deep ? 30 / (2 * cell) : 20 / (2 * cell), [], TwinFrontlineAllowed: true);
        }
        if (rows == 1)
        {
            var d = rng.NextBool(0.5) ? 3 : 2;
            var h1 = rng.NextInt(hopMinC, hopMaxC + 1);
            var s = rng.NextInt(1, Math.Max(1, hopMaxC / 2) + 1);
            return new CrossingDesign(s + d + h1, [new StoneRow(s, d)], TwinFrontlineAllowed: h1 <= 3);
        }
        // two rows: every hop at the minimum — front → row1 → row2 → row2 image → row1 image → enemy front
        var s2 = hopMinC / 2;
        var s1 = s2 + 2 + hopMinC;
        return new CrossingDesign(s1 + 2 + hopMinC, [new StoneRow(s1, 2), new StoneRow(s2, 2)], TwinFrontlineAllowed: false);
    }

    /// <summary>Carve the band and stones for a grown unit, or null when no stone column fits the unit's
    /// lines or the sized band cannot keep its contact discipline (the caller retries the attempt).</summary>
    public static MidResult? TryCarve(ComposeEnvelope env, ComposeRng rng, CrossingDesign design, GrownUnit unit)
    {
        var frame = Frame.For(env.Symmetry);
        var h = design.HalfGapCells;
        var flip = LateralFlip(env.Symmetry);

        var uvRects = unit.Pieces.Select(p => (p.Id, UV: frame.FromRect(p.Rect))).ToList();
        var minU = uvRects.Min(r => r.UV.UMin);
        var frontPieces = uvRects.Where(r => r.UV.UMin == minU).ToList();
        var frontIds = frontPieces.Select(f => f.Id).ToHashSet();
        var hub = uvRects.First(r => r.Id == "hub");

        // the opposing faces the band connects: the unit's frontline faces and their orbit counterparts
        // across the axis — the band's lateral extent lives inside their hull (BZ9)
        var faces = frontPieces.Select(f => (Lo: f.UV.VMin, Hi: f.UV.VMin + f.UV.VSpan)).ToList();
        var allFaces = faces.Concat(faces.Select(f => flip ? (Lo: -f.Hi, Hi: -f.Lo) : f)).Distinct().ToList();
        var hullL = allFaces.Min(f => f.Lo);
        var hullR = allFaces.Max(f => f.Hi);

        var stones = new List<MidStone>();
        int stoneL = 0, stoneR = 0;
        if (design.Rows.Count > 0)
        {
            var sw = rng.NextBool(0.5) ? 3 : 2;                                   // (m1) MD1: 2x2 / 2x3 / 3x2
            var lines = frontPieces.SelectMany(f => new[] { f.UV.VMin, f.UV.VMin + f.UV.VSpan })
                .Concat([hub.UV.VMin, hub.UV.VMin + hub.UV.VSpan])
                .Distinct().OrderBy(v => v).ToList();
            var fronts = frontPieces.Select(f => (f.UV.VMin, f.UV.VMin + f.UV.VSpan)).ToList();
            var innerU = design.Rows.Min(r => r.UMin);
            var candidates = CandidateColumns(lines, fronts, sw, flip, innerU, env.Cell)
                .Where(v => v >= hullL && v + sw <= hullR)                        // MD4/BZ9: inside the band's extent
                .ToList();
            if (candidates.Count == 0) return null;
            var vMin = rng.Pick(candidates);                                      // (m2) CT7 column

            for (var i = 0; i < design.Rows.Count; i++)
            {
                var surface = rng.NextBool(0.5) ? env.Surface + 4 : env.Surface;  // (m3) level or raised (EL1)
                stones.Add(new MidStone($"stone-{(char)('a' + i)}",
                    frame.ToRect(design.Rows[i].UMin, design.Rows[i].Depth, vMin, sw), surface));
            }
            stoneL = vMin;
            stoneR = vMin + sw;
        }

        // ── band lateral (BZ9): sampled between the minimal connecting interval and the face hull. The
        // minimal left edge is the largest value that still overlaps every face by a full G2 interface,
        // covers the stones, and (under a flipping symmetry) reaches the axis line so the fanned images
        // merge into one region; symmetrically on the right. ──
        var minBandL = allFaces.Min(f => f.Hi) - 2;
        if (flip) minBandL = Math.Min(minBandL, 0);
        if (stones.Count > 0) minBandL = Math.Min(minBandL, stoneL);
        minBandL = Math.Max(minBandL, hullL);
        var minBandR = allFaces.Max(f => f.Lo) + 2;
        if (flip) minBandR = Math.Max(minBandR, 0);
        if (stones.Count > 0) minBandR = Math.Max(minBandR, stoneR);
        minBandR = Math.Min(minBandR, hullR);

        var plaza = rng.NextBool(0.5);                                            // (m4) BZ7 form
        var tL = rng.NextDouble();                                                // (m5) lateral extensions
        var tR = rng.NextDouble();
        var bandL = hullL + (int)Math.Round(tL * (minBandL - hullL));
        var bandR = hullR - (int)Math.Round(tR * (hullR - minBandR));

        // BZ8: every face interface stays readable — flush with a face edge or covering it fully; an edge
        // that would cut an interior sub-interval snaps outward to the nearer face border (two passes, since
        // a snap can expose a second face)
        for (var pass = 0; pass < 2; pass++)
            foreach (var f in allFaces)
            {
                var iLo = Math.Max(bandL, f.Lo);
                var iHi = Math.Min(bandR, f.Hi);
                if (iHi - iLo <= 0 || (iLo == f.Lo || iHi == f.Hi)) continue;
                if (iLo - f.Lo <= f.Hi - iHi) bandL = f.Lo;
                else bandR = f.Hi;
            }

        // depth (BZ7): flush-docked against the frontline edges, or the plaza form lapping one cell onto them
        var lap = plaza ? 1 : 0;
        var band = frame.ToRect(-(h + lap), 2 * (h + lap), bandL, bandR - bandL);

        if (!BandContactsOk(env, unit, band, frontIds, stones)) return null;
        if (!ComposeGeometry.SeparationOk(env,
                unit.Pieces.Select(p => p.Rect).ToList(),
                stones.Select(s => s.Rect).ToList()))
            return null;

        return new MidResult(band, stones);
    }

    // The band's contact discipline: it may lap the frontline pieces it connects by at most one cell
    // (plaza) or merely border them (flush), it fully encases its stones, it touches NOTHING else, and it
    // keeps two full cells of clearance to every wool-carrying piece across all orbit images (BZ6).
    private static bool BandContactsOk(
        ComposeEnvelope env, GrownUnit unit, int[] band, IReadOnlySet<string> frontIds, IReadOnlyList<MidStone> stones)
    {
        foreach (var piece in unit.Pieces)
        {
            var ix = Math.Min(piece.Rect[0] + piece.Rect[2], band[0] + band[2]) - Math.Max(piece.Rect[0], band[0]);
            var iz = Math.Min(piece.Rect[1] + piece.Rect[3], band[1] + band[3]) - Math.Max(piece.Rect[1], band[1]);
            var overlaps = ix > 0 && iz > 0;
            var borders = !overlaps && ix >= 0 && iz >= 0 && !(ix == 0 && iz == 0);
            if (!overlaps && !borders) continue;
            if (!frontIds.Contains(piece.Id)) return false;              // the band touches only its fronts
            if (overlaps && Math.Min(ix, iz) > 1) return false;          // a lap deeper than one cell is overfit
        }

        // BZ6, across images: any hole the mid opens toward a wool would bridge straight to the point
        var order = Geom.Symmetry.Order(env.Symmetry);
        var axes = Geom.Symmetry.OrbitAxes(env.Symmetry);
        var woolPieces = unit.Wools.Select(w => w.Piece).ToHashSet();
        var bandImages = Enumerable.Range(0, order)
            .Select(k => ComposeGeometry.FanImage(band[0], band[1], band[0] + band[2], band[1] + band[3], axes, k))
            .ToList();
        foreach (var piece in unit.Pieces.Where(p => woolPieces.Contains(p.Id)))
            for (var k = 0; k < order; k++)
            {
                var (px1, pz1, px2, pz2) = ComposeGeometry.FanImage(
                    piece.Rect[0], piece.Rect[1], piece.Rect[0] + piece.Rect[2], piece.Rect[1] + piece.Rect[3], axes, k);
                foreach (var b in bandImages)
                {
                    var ix = Math.Min(px2, b.X2) - Math.Max(px1, b.X1);
                    var iz = Math.Min(pz2, b.Z2) - Math.Max(pz1, b.Z1);
                    if (ix > -2 + 1e-9 && iz > -2 + 1e-9) return false;
                }
            }
        return true;
    }

    /// <summary>Candidate stone columns (vMin values), CT7-aligned: each candidate's edges snap to one of
    /// the team unit's front/hub edge <paramref name="lines"/>. A candidate must reach every frontline
    /// interval (overlap it, or sit within 2 cells — the diagonal to a twin front's far chain must stay a
    /// real hop) and, under a laterally-flipping symmetry, stay close enough to the axis line that the
    /// stone→own-image hop stays within 20 blocks despite the flip.</summary>
    public static IReadOnlyList<int> CandidateColumns(
        IReadOnlyList<int> lines, IReadOnlyList<(int V1, int V2)> frontIntervals,
        int stoneW, bool lateralFlip, int innerUMin, int cell)
    {
        var result = new List<int>();
        foreach (var line in lines)
            foreach (var vMin in new[] { line, line - stoneW })
            {
                var vMax = vMin + stoneW;

                var ok = true;
                foreach (var (f1, f2) in frontIntervals)
                {
                    var overlap = Math.Min(vMax, f2) - Math.Max(vMin, f1);
                    var maxGap = frontIntervals.Count > 1 ? 2 : 0;      // twin fronts may be reached diagonally
                    if (overlap < 1 && -overlap > maxGap) { ok = false; break; }
                }
                if (!ok) continue;

                if (lateralFlip)
                {
                    // the opposing image mirrors v: the stone→image hop is diagonal unless the column covers
                    // the axis line — cap the real diagonal at 20 blocks
                    var vGap = Math.Max(0, 2 * Math.Max(vMin, -vMax));
                    double hop = 2.0 * innerUMin * cell;
                    if (Math.Sqrt(hop * hop + (double)vGap * cell * vGap * cell) > 20 + 1e-9) continue;
                }

                if (!result.Contains(vMin)) result.Add(vMin);
            }
        return result;
    }

    /// <summary>Whether the symmetry's opposing image flips the cross axis (the rotations do; the mirrors
    /// preserve it — their images sit straight across).</summary>
    public static bool LateralFlip(string symmetry) => symmetry is "rot_180" or "rot_90";
}
