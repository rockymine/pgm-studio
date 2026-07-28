using PgmStudio.Geom;

namespace PgmStudio.Pgm.Compose;

/// <summary>The u-axis arithmetic of a mid crossing, fixed before the team unit is allocated (its half-gap is
/// the allocator's axis margin): the void from one team's front to the other is <c>2 · HalfGapCells</c>.
/// <see cref="SplitBand"/> asks for the <b>split band</b> — two parallel crossings around a centre island
/// instead of one merged region — which the carve grants only where the face it is handed admits it. It is a
/// request, not a guarantee, and a face that admits a split is equally valid crossed by one band.</summary>
public sealed record CrossingDesign(int HalfGapCells, bool SplitBand = false);

/// <summary>A mid stepping stone: an anonymous piece inside the band (MD1/MD4), fanned by symmetry. The
/// band-only mid carves none; richer crossings that place them layer back onto <see cref="MidCarver"/>.</summary>
public sealed record MidStone(string Id, CellRect Rect, int Surface);

/// <summary>The carved mid: the band zone rect (cells) and the stones inside it.</summary>
public sealed record MidResult(CellRect BandRect, IReadOnlyList<MidStone> Stones);

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

    /// <summary>Of boards whose symmetry could carry one, how often the crossing <b>asks</b> for a split band.
    /// Most faces cannot host one, so the realised rate is far lower — this is the appetite, not the outcome.</summary>
    public const double SplitBandChance = 0.35;

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

        // ...unless the face offers a split and the crossing asked for one, in which case the band spans a single
        // leg and the symmetry supplies its partner — two parallel crossings with the bay between them left as an
        // island. The mid reads the face here; it never asks the frontline to be shaped a particular way.
        if (design.SplitBand && SplitRun(frontPieces, flip) is { } run) (bandL, bandR) = run;

        var band = frame.ToRect(-h, 2 * h, bandL, bandR - bandL);
        if (!BandContactsOk(env, unit, band, frontIds)) return null;
        return new MidResult(band, []);
    }

    // The band's contact discipline: it borders the frontline pieces it connects (flush — never a lap), it
    // touches NOTHING else, and it keeps two full cells of clearance to every wool-carrying piece across all
    // orbit images (BZ6).
    private static bool BandContactsOk(
        ComposeEnvelope env, GrownUnit unit, CellRect band, IReadOnlySet<string> frontIds)
    {
        foreach (var piece in unit.Pieces)
        {
            var ix = Math.Min(piece.Rect.X + piece.Rect.Width, band.X + band.Width) - Math.Max(piece.Rect.X, band.X);
            var iz = Math.Min(piece.Rect.Z + piece.Rect.Height, band.Z + band.Height) - Math.Max(piece.Rect.Z, band.Z);
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
            .Select(k => ComposeGeometry.FanImage(band.X, band.Z, band.X + band.Width, band.Z + band.Height, axes, k))
            .ToList();
        foreach (var piece in unit.Pieces.Where(p => woolPieces.Contains(p.Id)))
            for (var k = 0; k < order; k++)
            {
                var (px1, pz1, px2, pz2) = ComposeGeometry.FanImage(
                    piece.Rect.X, piece.Rect.Z, piece.Rect.X + piece.Rect.Width, piece.Rect.Z + piece.Rect.Height, axes, k);
                foreach (var b in bandImages)
                {
                    var ix = Math.Min(px2, b.X2) - Math.Max(px1, b.X1);
                    var iz = Math.Min(pz2, b.Z2) - Math.Max(pz1, b.Z1);
                    if (ix > -2 + 1e-9 && iz > -2 + 1e-9) return false;
                }
            }
        return true;
    }

    /// <summary>
    /// The lateral run one band of a <b>split</b> spans, or <c>null</c> when this face cannot host one.
    ///
    /// <para>A split works by carrying a band narrower than the face and letting the symmetry produce its
    /// partner, so it needs three things of the face and asks nothing of it: the image must land <b>across</b>
    /// the cross axis (only the rotations flip it — under a mirror the partner would fall back on top of the
    /// original, leaving the far leg uncrossed); the face must <b>coincide with its own image</b>, so that the
    /// leg one band lands on has terrain facing it on both sides; and it must be <b>two runs with the axis in
    /// the gap</b> between them, which is the island the two bands leave. Legs may differ in width and a bay may
    /// be any size — such a face simply does not offer a split, and is crossed by one band as before.</para>
    /// </summary>
    private static (int Lo, int Hi)? SplitRun(
        IReadOnlyList<(string Id, (int UMin, int USpan, int VMin, int VSpan) UV)> frontPieces, bool flip)
    {
        if (!flip) return null;
        var occupied = new HashSet<int>();
        foreach (var f in frontPieces)
            for (var v = f.UV.VMin; v < f.UV.VMin + f.UV.VSpan; v++) occupied.Add(v);
        // cell v reflects onto cell −1−v, the axis lying on the boundary between cells −1 and 0
        if (!occupied.SetEquals(occupied.Select(v => -1 - v))) return null;
        if (occupied.Contains(0) || occupied.Contains(-1)) return null;   // no gap over the axis to island

        var runs = new List<(int Lo, int Hi)>();
        foreach (var v in occupied.OrderBy(v => v))
            if (runs.Count > 0 && runs[^1].Hi == v) runs[^1] = (runs[^1].Lo, v + 1);
            else runs.Add((v, v + 1));
        return runs.Count == 2 ? runs[0] : null;
    }

    /// <summary>Whether the symmetry's opposing image flips the cross axis (the rotations do; the mirrors
    /// preserve it — their images sit straight across).</summary>
    public static bool LateralFlip(string symmetry) => symmetry is "rot_180" or "rot_90";
}
