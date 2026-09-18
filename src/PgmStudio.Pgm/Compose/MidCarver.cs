using PgmStudio.Geom;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Compose;

/// <summary>The u-axis arithmetic of a mid crossing, fixed before the team unit is allocated (its half-gap is
/// the allocator's axis margin): the void from one team's front to the other is <c>2 · HalfGapCells</c>.
/// <see cref="CrossingDesign.SplitBand"/> asks for the <b>split band</b> — two parallel crossings around a centre island
/// instead of one merged region — which the carve grants only where the face it is handed admits it. It is a
/// request, not a guarantee, and a face that admits a split is equally valid crossed by one band.</summary>
/// <summary>The row of stones a crossing carries. <see cref="SingleRank"/> stands one rank <b>astride</b> the
/// axis, so each stone's own image abuts it into one shared island; <see cref="DoubleRank"/> stands the rank
/// clear of the axis, so the image is a second rank facing it across the centre void and every team meets its
/// own first. <see cref="None"/> is a crossing with nothing standing in it.</summary>
public enum MidForm { None, SingleRank, DoubleRank }

/// <summary>How finely a crossing's row is cut. A <see cref="Broad"/> row is the band's own stone depth,
/// centred between the front's legs, so both of them converge on one piece of shared ground. A
/// <see cref="Fine"/> row is a corridor deep and <b>keys to the front's faces</b> where each can carry a
/// stone — one checkpoint off each leg, so a team steps straight forward off ground it already holds — and
/// centres at that same shallower depth where the front presents a single face. The fine row is the grid
/// form: more, smaller islands and a shorter crossing, at the cost that two fronts whose legs do not line up
/// send their teams past each other rather than into each other.</summary>
public enum MidGrain { Broad, Fine }

public sealed record CrossingDesign(int HalfGapCells, bool SplitBand, MidForm Form, MidGrain Grain);

/// <summary>A mid stepping stone: a piece standing astride the axis inside the band (MD1/MD4), fanned by
/// symmetry into one shared island. <see cref="MidCarver.Stones"/> lays the row.</summary>
public sealed record MidStone(string Id, CellRect Rect, int Surface);

/// <summary>The carved mid: the band zone rect (cells), the stones inside it, and two facts about the row
/// that the rects cannot be read back for. <see cref="MidResult.Grain"/> is what the crossing was <b>designed</b> at,
/// which is what its stones' depth follows — a keyed crossing is a corridor deep whether or not its row
/// found faces to key to. <see cref="KeyedToFront"/> is whether the row <b>landed</b> on them: a keyed design
/// whose front turns out to present one face lays the centred row at that same shallower depth, and a keyed
/// row whose faces happen to be equally wide is laid out exactly like a centred one.</summary>
public sealed record MidResult(
    CellRect BandRect, IReadOnlyList<MidStone> Stones, MidForm Form, MidGrain Grain, bool KeyedToFront);

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
///
/// <para>Inside the band it lays a row of <b>mid stones</b> (<see cref="Stones"/>) — the shared ground the
/// crossing is fought over, funded by <see cref="MidShare"/> out of what the units would have spent on
/// themselves. A split band carries none: the bay between its legs is already the island.</para>
///
/// <para>Deterministic — no draws.</para>
/// </summary>
public static class MidCarver
{
    /// <summary>The void a band opens from one team's front to the other's when it carries no stone, in blocks
    /// — half of it each side of the axis. A crossing with nothing in it is walked or bridged in one go, so it
    /// is a single distance rather than a sum of hops.</summary>
    public const int BandGapBlocks = 30;

    /// <summary>The share of <b>one</b> team's land budget the mid takes. A team's budget is half a whole map's
    /// land, so the mid's own total is twice this — it is ground both teams gave up land for. It comes out of
    /// what the units would otherwise have spent on themselves rather than on top of them, which is what keeps
    /// a board's total land the band's own.</summary>
    public const double MidShare = 0.10;

    /// <summary>The void between a mid stone and the ground it is reached from, in blocks — one hop, inside
    /// G5's 10–20. A symmetric row may take one cell more than this between its stones (see
    /// <see cref="Stones"/>).</summary>
    public const int HopBlocks = 12;

    /// <summary>The lateral void a stone keeps from the band's own ends, in cells, so the row reads as islands
    /// in a crossing rather than as a bar across it.</summary>
    public const int StoneInsetCells = 1;

    /// <summary>MD6: two lateral columns of mid stones are the norm and three the hard maximum.</summary>
    public const int StoneMaxCount = 3;

    /// <summary>How often a crossing that can afford two ranks draws them rather than one. The single rank
    /// astride the axis stays the common form — it is the one both teams arrive at together — and the facing
    /// pair is the variation.</summary>
    public const double DoubleRankChance = 0.4;

    /// <summary>The id every mid stone carries in front of its index, so a reader of a composed plan can tell
    /// the crossing's ground from the team unit's.</summary>
    public const string StoneIdPrefix = "mid-stone-";

    /// <summary>Whether a plan piece id names one of the crossing's own stones. A reader that means <em>the
    /// authored unit</em> asks this and leaves them out: a stone is ordinary generating terrain to everything
    /// that walks a board, and the one piece that sits across the axis, so a front-row read that counts it
    /// measures the crossing instead of the unit.</summary>
    public static bool IsStone(string? pieceId) =>
        pieceId is not null && pieceId.StartsWith(StoneIdPrefix, StringComparison.Ordinal);

    /// <summary>How deep a mid stone stands, in blocks. A centred row takes the band's own figure; a
    /// <b>keyed</b> row takes the band's <b>corridor</b> instead, because a stone keyed to a front's leg is
    /// only as wide as that leg and has to stay wider than it is deep — measured over the seed range the
    /// narrowest leg on a two-faced front runs 20 blocks at milli and 24 at centi against stone depths of 24,
    /// so keying at the band's depth would refuse half the boards that could carry it. The shallower stone is
    /// what makes the crossing shorter as well as the row wider.</summary>
    private static int StoneDeepBlocks(string band, MidGrain grain) =>
        grain == MidGrain.Fine ? UnitTuning.CorridorBlocks(SizeBands.Canonical(band))
        : SizeBands.Canonical(band) switch
        {
            SizeBands.Micro or SizeBands.Milli or SizeBands.Centi => 24,
            _ => 16,
        };

    /// <summary>A mid stone's depth on this board, in cells — <b>even</b>, because a stone stands astride the
    /// axis and spends half its depth each side, which is what makes its own fanned image abut it into one
    /// shared island instead of landing as a second one across the gap (CT11).</summary>
    public static int StoneDeepCells(ComposeEnvelope env, MidGrain grain = MidGrain.Broad) => 2 * Math.Max(1,
        (int)Math.Round(StoneDeepBlocks(env.Band, grain) / (2.0 * env.Cell), MidpointRounding.AwayFromZero));

    /// <summary>One hop on a grid of <paramref name="cell"/>-block cells.</summary>
    public static int HopCells(int cell) =>
        Math.Max(1, (int)Math.Round(HopBlocks / (double)cell, MidpointRounding.AwayFromZero));

    /// <summary>Half the gap a <b>stoneless</b> band opens, on a grid of <paramref name="cell"/>-block cells:
    /// <see cref="BandGapBlocks"/> laid on the grid, rounded to a whole cell and floored at
    /// <see cref="Envelope.AxisMarginCells"/>, so the gap a board actually opens is the stated blocks at
    /// whatever scale it is composed at.</summary>
    public static int EmptyHalfGapCells(int cell) => Math.Max(
        Envelope.AxisMarginCells,
        (int)Math.Round(BandGapBlocks / 2.0 / cell, MidpointRounding.AwayFromZero));

    /// <summary>How far a <b>double rank</b> stands off the axis, in cells: one hop, so the void between the
    /// two facing ranks is two of them — the board's longest single jump, and the one that crosses the centre
    /// line. A rank clear of the axis has its own image for the opposite rank, so a board carrying one is a
    /// stone each rather than a stone shared.</summary>
    public static int RankOffsetCells(int cell) => HopCells(cell);

    /// <summary>Whether the crossing's share can pay for two ranks at all: each rank is fanned, so the pair
    /// spends twice one rank's land, and the narrowest stone the aspect rule admits is as wide as it is deep.
    /// Below this the draw is not offered and the single rank is the only form — a band that asked for a pair
    /// it cannot afford would come out with nothing standing in it.</summary>
    public static bool AffordsTwoRanks(ComposeEnvelope env) =>
        env.MidLandCells >= 2 * StoneDeepCells(env) * (double)StoneDeepCells(env);

    /// <summary>How often a crossing asks for the <b>fine</b> row, which it must decide before the unit is
    /// grown because the row's depth sets the gap. An even draw, because the two are different boards rather
    /// than a form and its exception: a broad row puts one meeting ground between a front's legs for two
    /// teams to converge on, and a fine row puts a checkpoint off each leg and lets two fronts that do not
    /// line up run past each other. A front that turns out to present one face lays the centred row at the
    /// same depth — still the finer, shallower crossing, with nothing to key to.</summary>
    public const double FineRowChance = 0.5;

    /// <summary>Whether a board's symmetry admits a fine row's keying at this form. Under a laterally flipping image a
    /// stone keyed to one of the unit's own front faces has its image where the <b>enemy's</b> face is, and on
    /// a row astride the axis those two overlap — an interior clash rather than CT11's abutment. A double rank
    /// stands clear of the axis, so its image is the opposite rank and no two stones share a depth; under a
    /// mirror the cross coordinate is kept and every keyed stone is its own image, so either row serves.</summary>
    public static bool AdmitsFineRow(ComposeEnvelope env, MidForm form) =>
        form != MidForm.None && (!LateralFlip(env.Symmetry) || form == MidForm.DoubleRank);

    /// <summary>
    /// The crossing this board is allocated against: how far from the axis the unit's front sits, which row it
    /// will carry and whether the board asks for a split band. A <b>single rank</b> stands astride the axis and
    /// opens exactly one hop either side of it, so its half-gap is the hop plus half the stone; a <b>double
    /// rank</b> stands one hop off the axis, so its half-gap is that offset plus a whole stone plus the hop to
    /// the front. A split band carries none — the bay between its legs is the island — and takes
    /// <see cref="EmptyHalfGapCells"/>. Order-4 boards take the stoneless gap too: a quarter-turn mid is four
    /// fanned images of one wedge rather than two halves meeting, and CT10's archetypes are not this row.
    ///
    /// <para>Draw-free, so it perturbs no RNG sequence — <paramref name="doubleRank"/> is drawn by the caller
    /// in the board's own fixed order.</para>
    /// </summary>
    public static CrossingDesign Crossing(ComposeEnvelope env, bool splitBand, bool doubleRank, bool fine)
    {
        if (splitBand || Geom.Symmetry.Order(env.Symmetry) != 2)
            return new(EmptyHalfGapCells(env.Cell), splitBand, MidForm.None, MidGrain.Broad);
        var form = doubleRank && AffordsTwoRanks(env) ? MidForm.DoubleRank : MidForm.SingleRank;
        var grain = fine && AdmitsFineRow(env, form) ? MidGrain.Fine : MidGrain.Broad;
        var deep = StoneDeepCells(env, grain);
        return form == MidForm.DoubleRank
            ? new(RankOffsetCells(env.Cell) + deep + HopCells(env.Cell), false, form, grain)
            : new(HopCells(env.Cell) + deep / 2, false, form, grain);
    }

    /// <summary>The land the crossing's stones hold on the <b>fanned</b> board, in cells. An authored stone
    /// astride the axis and its own image are one island, so the count is taken over the fan: counting what
    /// was authored would report half the ground on a board whose row is a mirrored pair, and all of it on one
    /// whose stone is centred.</summary>
    public static int StoneLandCells(ComposeEnvelope env, IReadOnlyList<MidStone> stones)
    {
        var order = Geom.Symmetry.Order(env.Symmetry);
        var axes = Geom.Symmetry.OrbitAxes(env.Symmetry);
        var cells = new HashSet<(int X, int Z)>();
        foreach (var stone in stones)
            for (var image = 0; image < order; image++)
            {
                var (x1, z1, x2, z2) = ComposeGeometry.FanImage(
                    stone.Rect.X, stone.Rect.Z,
                    stone.Rect.X + stone.Rect.Width, stone.Rect.Z + stone.Rect.Height, axes, image);
                for (var x = (int)x1; x < (int)x2; x++)
                    for (var z = (int)z1; z < (int)z2; z++)
                        cells.Add((x, z));
            }
        return cells.Count;
    }

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
        // the faces a keyed row lines up with are this unit's own, merged where two pieces abut, since two
        // touching pieces present one face to the mid rather than two
        var merged = new List<(int Lo, int Hi)>();
        foreach (var face in faces.OrderBy(f => f.Lo))
            if (merged.Count > 0 && face.Lo <= merged[^1].Hi)
                merged[^1] = (merged[^1].Lo, Math.Max(merged[^1].Hi, face.Hi));
            else merged.Add(face);
        var stones = Stones(env, band, design.Form, design.Grain, merged);
        var landed = design.Grain == MidGrain.Fine && Keyed(env, frame, merged,
                         StoneDeepCells(env, design.Grain), design.Form == MidForm.DoubleRank).Count > 0;
        return new MidResult(
            band, stones, stones.Count == 0 ? MidForm.None : design.Form, design.Grain, landed);
    }

    /// <summary>
    /// The stones a band carries: one lateral row of islands inside it, standing at the depth
    /// <paramref name="form"/> names.
    ///
    /// <para>Each stone is <b>wider than it is deep</b> and stands clear of the band's own ends, so it reads as
    /// an island in a crossing rather than a bar across it. Where the row stands decides what its fanned image
    /// is. <b>Astride the axis</b> (<see cref="MidForm.SingleRank"/>) the image abuts it into <b>one shared
    /// landmass</b> both teams reach at once (CT11), and under a laterally flipping symmetry the row's outer
    /// stones are each other's images, so only the centre stone and the ones beyond it are authored. <b>Clear
    /// of the axis</b> (<see cref="MidForm.DoubleRank"/>) no stone is its own image and none is another's, so
    /// every one is authored and the fan supplies a whole second rank facing it across the centre void — a
    /// stone each, met before the enemy's.</para>
    ///
    /// <para>The count is the widest the hull affords at that aspect, capped at <see cref="StoneMaxCount"/>
    /// (MD6), and a pair of ranks pays twice for each because both are ground. The gap between stones takes one
    /// cell more than <see cref="HopCells"/> where a symmetric row needs it: a row spanning an odd number of
    /// cells cannot sit symmetric about the axis's own cell boundary. Deterministic — no draws. A hull too
    /// narrow to hold one stone at the aspect rule carries none, and the band is then wider than it needed to
    /// be rather than refused.</para>
    /// </summary>
    public static IReadOnlyList<MidStone> Stones(
        ComposeEnvelope env, CellRect band, MidForm form, MidGrain grain = MidGrain.Broad,
        IReadOnlyList<(int Lo, int Hi)>? faces = null)
    {
        if (form == MidForm.None) return [];
        var frame = Frame.For(env.Symmetry);
        var deep = StoneDeepCells(env, grain);
        var pair = form == MidForm.DoubleRank;
        if (grain == MidGrain.Fine && faces is { Count: >= 2 }
            && Keyed(env, frame, faces, deep, pair) is { Count: > 0 } keyed) return keyed;
        var uMin = pair ? RankOffsetCells(env.Cell) : -deep / 2;
        var uv = frame.FromRect(band);
        var lo = uv.VMin + StoneInsetCells;
        var hi = uv.VMin + uv.VSpan - StoneInsetCells;
        var flip = LateralFlip(env.Symmetry);

        for (var count = StoneMaxCount; count >= 1; count--)
            foreach (var hop in new[] { HopCells(env.Cell), HopCells(env.Cell) + 1 })
            {
                var byHull = (hi - lo - (count - 1) * hop) / count;
                var byLand = (int)(env.MidLandCells / ((pair ? 2 : 1) * count * (double)deep));
                var wide = Math.Min(byHull, byLand);
                if (count == 1 && !pair && wide % 2 != 0) wide--;  // a lone shared stone's span carries the parity
                if (wide < deep) continue;                     // wider than deep, or it is a line
                var span = count * wide + (count - 1) * hop;
                // a row astride the axis under a cross-flipping image has to sit symmetric about the axis's
                // own cell boundary, which takes an even span and centres the row on zero. A rank clear of the
                // axis is under no such rule — its image is the opposite rank, which lands where it lands — so
                // an odd span there only offsets the two ranks a cell from each other. A mirror keeps the
                // cross coordinate either way, and its rows centre in the band.
                if (!pair && flip && span % 2 != 0) continue;
                var rowLeft = flip ? -span / 2 : lo + (hi - lo - span) / 2;
                if (rowLeft < lo || rowLeft + span > hi) continue;

                var stones = new List<MidStone>();
                for (var index = 0; index < count; index++)
                {
                    var vMin = rowLeft + index * (wide + hop);
                    // in a row astride the axis the outer stones are each other's images; a rank clear of it
                    // has the opposite rank for its image, so every stone in it is authored
                    if (!pair && flip && 2 * vMin < -wide) continue;
                    stones.Add(new MidStone($"{StoneIdPrefix}{stones.Count}",
                                            frame.ToRect(uMin, deep, vMin, wide), env.Surface));
                }
                return stones;
            }
        return [];
    }

    /// <summary>The keyed row: one stone on each face of the unit's own front row, so a team steps straight
    /// forward off ground it already holds instead of converging on a shared island between its legs. The
    /// widest faces are taken, at most <see cref="StoneMaxCount"/> of them, and the row forms only when every
    /// one of them carries a stone wider than it is deep and the whole row is inside the crossing's share —
    /// a row keyed to a leg too narrow for it would be the line down the middle the aspect rule exists to
    /// refuse, and half a keyed row is neither form. Empty means the caller lays the centred row instead.</summary>
    private static List<MidStone> Keyed(ComposeEnvelope env, Frame frame,
                                        IReadOnlyList<(int Lo, int Hi)> faces, int deep, bool pair)
    {
        var chosen = faces.OrderByDescending(face => face.Hi - face.Lo).Take(StoneMaxCount)
                          .OrderBy(face => face.Lo).ToList();
        if (chosen.Count < 2) return [];
        // the faces have to clear each other by a hop, because the stones on them inherit that gap: two legs
        // closer together than one carry a row a player crosses without leaving the ground it stands on
        for (var index = 1; index < chosen.Count; index++)
            if (chosen[index].Lo - chosen[index - 1].Hi < HopCells(env.Cell)) return [];

        // what one stone may be at its widest, so the row fits the crossing's share. A stone spans its whole
        // face where that is affordable and sits centred on it where it is not: the face is what a stone is
        // keyed to, and keying is about where the stone stands rather than how far it reaches.
        var cap = (int)(env.MidLandCells / (chosen.Count * (double)deep * (pair ? 2 : 1)));
        var widths = chosen.Select(face => Math.Min(face.Hi - face.Lo, cap)).ToList();
        if (widths.Any(wide => wide < deep)) return [];

        var uMin = pair ? RankOffsetCells(env.Cell) : -deep / 2;
        return [.. chosen.Select((face, index) => new MidStone(
            $"{StoneIdPrefix}{index}",
            frame.ToRect(uMin, deep, face.Lo + (face.Hi - face.Lo - widths[index]) / 2, widths[index]),
            env.Surface))];
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
