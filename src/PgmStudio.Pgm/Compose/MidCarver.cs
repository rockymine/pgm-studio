namespace PgmStudio.Pgm.Compose;

/// <summary>The u-axis arithmetic of a mid crossing, fixed before the team unit is allocated (its half-gap is
/// the allocator's axis margin): the void from one team's front to the other is <c>2 · HalfGapCells</c>.</summary>
public sealed record CrossingDesign(int HalfGapCells);

/// <summary>A mid stepping stone: an anonymous piece inside the band (MD1/MD4), fanned by symmetry. The
/// band-only mid carves none; richer crossings that place them layer back onto <see cref="MidCarver"/>.</summary>
public sealed record MidStone(string Id, int[] Rect, int Surface);

/// <summary>The carved mid: the band zone rect (cells) and the stones inside it.</summary>
public sealed record MidResult(int[] BandRect, IReadOnlyList<MidStone> Stones);

/// <summary>
/// Carves the mid in its CLEAN form (CT1): one authored band zone spanning the symmetry axis — its own orbit
/// images overlap it (it contains the centre), so the fanned zones merge into ONE build region, the clean
/// form's definition. The band is <b>fit to the crossing it serves</b> with no slack (BZ9): laterally it
/// spans EXACTLY the hull of the opposing frontline faces — one shape with the fronts it connects (a sampled
/// narrower fit underfits a twin/U front, and any post-hoc edge snap desymmetrizes the band so its flipped
/// fan image no longer coincides) — and in depth it is <b>flush-docked</b> against the frontline edges (BZ7:
/// a flat front edge takes the build zone straight against it, zero overlap). The band touches nothing else:
/// not the hub behind the front, not the lanes, and never a wool-carrying piece, which it clears by two full
/// cells across all orbit images (BZ6 — a mid-bridgeable wool would erase the map's gameplay direction).
/// Deterministic — no draws.
/// </summary>
public static class MidCarver
{
    /// <summary>The <b>band-only</b> crossing: a uniform 20-block gap (a 10-block half-gap per side), no stone
    /// rows, no centre island — the mid is one plain build band spanning the axis. Draw-free (uniform depth by
    /// choice), so it perturbs no RNG sequence. A frontline bay the band's flush dock seals (the staple/U
    /// front) still rings an enclosed hole — that hole is the terrain's, not the crossing's.</summary>
    public static CrossingDesign BandOnly(ComposeEnvelope env) => new(20 / (2 * env.Cell));

    /// <summary>Carve the band for a composed unit, or null when the band cannot keep its contact discipline
    /// (the caller retries the attempt).</summary>
    public static MidResult? TryCarve(ComposeEnvelope env, CrossingDesign design, GrownUnit unit)
    {
        var frame = Frame.For(env.Symmetry);
        var h = design.HalfGapCells;
        var flip = LateralFlip(env.Symmetry);

        var uvRects = unit.Pieces.Select(p => (p.Id, UV: frame.FromRect(p.Rect))).ToList();
        var minU = uvRects.Min(r => r.UV.UMin);
        var frontPieces = uvRects.Where(r => r.UV.UMin == minU).ToList();
        var frontIds = frontPieces.Select(f => f.Id).ToHashSet();

        // the opposing faces the band connects: the unit's frontline faces and their orbit counterparts
        // across the axis — the band spans exactly their hull (BZ9, no slack)
        var faces = frontPieces.Select(f => (Lo: f.UV.VMin, Hi: f.UV.VMin + f.UV.VSpan)).ToList();
        var allFaces = faces.Concat(faces.Select(f => flip ? (Lo: -f.Hi, Hi: -f.Lo) : f)).Distinct().ToList();
        var bandL = allFaces.Min(f => f.Lo);
        var bandR = allFaces.Max(f => f.Hi);

        var band = frame.ToRect(-h, 2 * h, bandL, bandR - bandL);
        if (!BandContactsOk(env, unit, band, frontIds)) return null;
        return new MidResult(band, []);
    }

    // The band's contact discipline: it borders the frontline pieces it connects (flush — never a lap), it
    // touches NOTHING else, and it keeps two full cells of clearance to every wool-carrying piece across all
    // orbit images (BZ6).
    private static bool BandContactsOk(
        ComposeEnvelope env, GrownUnit unit, int[] band, IReadOnlySet<string> frontIds)
    {
        foreach (var piece in unit.Pieces)
        {
            var ix = Math.Min(piece.Rect[0] + piece.Rect[2], band[0] + band[2]) - Math.Max(piece.Rect[0], band[0]);
            var iz = Math.Min(piece.Rect[1] + piece.Rect[3], band[1] + band[3]) - Math.Max(piece.Rect[1], band[1]);
            var overlaps = ix > 0 && iz > 0;
            var borders = !overlaps && ix >= 0 && iz >= 0 && !(ix == 0 && iz == 0);
            if (!overlaps && !borders) continue;
            if (overlaps) return false;                                  // flush means zero overlap, ever
            if (!frontIds.Contains(piece.Id)) return false;              // the band touches only its fronts
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

    /// <summary>Whether the symmetry's opposing image flips the cross axis (the rotations do; the mirrors
    /// preserve it — their images sit straight across).</summary>
    public static bool LateralFlip(string symmetry) => symmetry is "rot_180" or "rot_90";
}
