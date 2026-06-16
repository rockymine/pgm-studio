using System.Text;
using fNbt;
using static PgmStudio.Minecraft.Nbt;

namespace PgmStudio.Minecraft;

/// <summary>The pedestal block an author placed directly under the (air) monument block. Drawn from the
/// corpus: bedrock 33%, stained clay 16%, stained glass 14%, wool 11%, floating(=air) 9% — see
/// <c>docs/analysis/monument-patterns.md</c>.</summary>
public enum PedestalKind { Any, Bedrock, StainedClay, StainedGlass, Wool, Floating }

/// <summary>How the monument is labelled: a sign on the block below (34%), a sign above (16%), an
/// armour stand (3%), an item frame holding a wool item (a_new_day / golden_drought_iii), or no label
/// (~47% → geometry-only fallback).</summary>
public enum LabelKind { Any, SignBelow, SignAbove, ArmorStand, ItemFrame, None }

/// <summary>The block capping the monument (directly above the air cell): stained glass 19%, open/air
/// 13%, barrier 11%, slab 11%, bedrock 9%, stained clay 6%, wool 6% (Q3). The decisive signal for
/// *unlabelled* monuments like <c>lupa</c>/<c>lupain</c> (bedrock below + glass cap, no sign).</summary>
public enum CapKind { Any, Open, StainedGlass, StainedClay, Bedrock, Slab, Barrier, Wool, Sign }

/// <summary>The author's declared monument style (the "which monument style?" menu): the block below
/// (pedestal), how it's labelled, and the block above (cap). Declaring these lifts detection precision
/// — and a colour-encoding cap (stained glass/clay) also yields the wool colour for label-free maps.</summary>
public readonly record struct MonumentStyle(
    PedestalKind Pedestal = PedestalKind.Any, LabelKind Label = LabelKind.Any, CapKind Cap = CapKind.Any);

/// <summary>The world region the author boxed (inclusive, world coords). Bounds both the scan and the
/// candidate sign anchors — cheap and the dominant false-positive filter (off-site team/wool-room signs
/// fall outside it).</summary>
public readonly record struct ScanBox(int MinX, int MinY, int MinZ, int MaxX, int MaxY, int MaxZ)
{
    public bool Contains(int x, int y, int z) => x >= MinX && x <= MaxX && y >= MinY && y <= MaxY && z >= MinZ && z <= MaxZ;
    public ScanBox Expand(int m) => new(MinX - m, MinY - m, MinZ - m, MaxX + m, MaxY + m, MaxZ + m);
    public bool IntersectsChunk(int chunkX, int chunkZ)
    {
        int x0 = chunkX * 16, z0 = chunkZ * 16;
        return x0 + 15 >= MinX && x0 <= MaxX && z0 + 15 >= MinZ && z0 <= MaxZ;
    }
}

/// <summary>A suggested monument: the (air) placement block, an inferred wool colour, a confidence and
/// the evidence it was derived from. Returned highest-confidence first for the authoring UI to confirm.</summary>
public sealed record MonumentSuggestion(
    int X, int Y, int Z,
    string? Color,
    double Confidence,
    string Source,                       // "sign" | "armorstand" | "geometry"
    int PedestalId, int PedestalData,
    int? SignX, int? SignY, int? SignZ,
    string? Evidence);

/// <summary>
/// A gathered monument candidate — the <em>style-agnostic</em> output of the detector's ingest pass
/// (<see cref="MonumentSuggester.Gather"/>), persisted in the <c>monument_candidate</c> table so the
/// authoring tier can score suggestions without re-reading the world. One row per gathered anchor
/// emission (the cell-merge happens at <see cref="MonumentSuggester.Score"/> time). Carries exactly what
/// <c>Score</c> needs to reproduce the declared-style filter / confidence / colour, and nothing it can
/// recompute. See <c>docs/contracts/monument-candidate-store.md</c>.
/// </summary>
public sealed record MonumentCandidate(
    int X, int Y, int Z,
    string Source,                       // "sign" | "armorstand" | "geometry"
    int PedestalId, int PedestalData,    // block directly below the candidate (air) cell
    int CapId, int CapData,              // block directly above it
    string? ColorHint,                   // colour parsed from label text / stand head / name (stain still wins at Score)
    int? SignX, int? SignY, int? SignZ, int? SignFacing, string? SignText,
    string? StandHeadColor, string? StandName,
    string? Evidence);

/// <summary>
/// The "which monument style? + box" intelligent extractor (authoring-flow backend). Given the world,
/// the box the author drew around the monument area, and the style they declared, it suggests monument
/// block positions. Grounded in the 345-map / 1723-monument corpus study (<c>docs/analysis/
/// monument-patterns.md</c>): it inverts the learned wall-sign facing→monument geometry, classifies sign
/// <em>text</em> as a label (not just keyword-matches), requires the declared pedestal under an air cell,
/// and falls back to armour-stand and pure-geometry anchors.
///
/// Reads the Anvil world (like <see cref="MonumentSliceExtractor"/>): <c>layer_segment.parquet</c> can't
/// drive this — it stores only per-column solid-run extents, no block materials, signs or entities. Its
/// role here is downstream (snap/validate a suggestion onto buildable ground), not detection.
/// </summary>
public static class MonumentSuggester
{
    // Wall-sign (id 68) facing → unit vector the readable face points (= toward the monument horizontally).
    // Derived from the corpus modal offsets (data 2→(0,1,1), 3→(0,1,-1), 4→(1,1,0), 5→(-1,1,0)).
    private static readonly IReadOnlyDictionary<int, (int dx, int dz)> WallSignFacing = new Dictionary<int, (int, int)>
    {
        [2] = (0, 1), [3] = (0, -1), [4] = (1, 0), [5] = (-1, 0),
    };

    private const int WallSignId = 68, SignPostId = 63, WoolId = 35, StainedClayId = 159, BarrierId = 166;

    // Pedestal neighbour offsets for the geometry terrain-reject rules (§4.1).
    private static readonly (int dx, int dz)[] Faces = [(1, 0), (-1, 0), (0, 1), (0, -1)];
    private static readonly (int dx, int dz)[] Neighbours8 =
        [(1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1)];

    // Item-frame Facing byte (the front-facing dir) → horizontal offset to the block it's mounted on:
    // support = (TileX,TileY,TileZ) + this. Front faces away from the wall, so the wall is the opposite side.
    private static readonly IReadOnlyDictionary<int, (int dx, int dz)> FrameSupport = new Dictionary<int, (int, int)>
    {
        [0] = (0, -1), [1] = (1, 0), [2] = (0, 1), [3] = (-1, 0),
    };

    /// <summary>Block id directly below the monument → its <see cref="PedestalKind"/> (the single
    /// id↔kind table; <c>PedestalMatches</c> and the authoring/auto-style tooling both go through it).</summary>
    public static PedestalKind ClassifyPedestal(int belowId) => belowId switch
    {
        7 => PedestalKind.Bedrock, 159 => PedestalKind.StainedClay, 95 => PedestalKind.StainedGlass,
        35 => PedestalKind.Wool, 0 => PedestalKind.Floating, _ => PedestalKind.Any,
    };

    /// <summary>Block id directly above the monument → its <see cref="CapKind"/>.</summary>
    public static CapKind ClassifyCap(int aboveId) => aboveId switch
    {
        95 => CapKind.StainedGlass, 159 => CapKind.StainedClay, 7 => CapKind.Bedrock, 166 => CapKind.Barrier,
        35 => CapKind.Wool, 44 or 126 => CapKind.Slab, 63 or 68 => CapKind.Sign, 0 => CapKind.Open, _ => CapKind.Any,
    };

    /// <summary>Suggest monument blocks inside <paramref name="box"/> for the declared
    /// <paramref name="style"/> — the live path, kept as <see cref="Score"/> over <see cref="Gather"/>
    /// (the same composition the candidate store serves from the DB; this guards the factoring against the
    /// corpus parity harness). Chunks outside the box (+2 margin) are skipped.</summary>
    public static List<MonumentSuggestion> Suggest(
        IEnumerable<AnvilRegion.Chunk> chunks, ScanBox box, MonumentStyle style) =>
        Score(Gather(chunks, box.Expand(2)), box, style);

    /// <summary>
    /// Gather pass (ingest, reads the world) — find every anchor (monument-label wall signs, wool-head /
    /// named armour stands, and distinctive-geometry cells) and project each to a candidate air cell,
    /// capturing the surrounding evidence. <strong>Style-agnostic</strong>: it accepts any pedestal/cap and
    /// runs every anchor type — the declared <c>MonumentStyle</c> isn't known at ingest, so all filtering
    /// (pedestal/cap/label) and the confidence/colour are deferred to <see cref="Score"/>. The geometry
    /// pass is bounded to distinctive pedestals/caps (§4.1) so the whole-world row count stays ≈ the real
    /// monument count. <paramref name="world"/> is the gather region (the whole world at ingest; the
    /// box+2 on the live path).
    /// </summary>
    public static List<MonumentCandidate> Gather(IEnumerable<AnvilRegion.Chunk> chunks, ScanBox world)
    {
        var (blocks, tileList, entityList) = RegionScan.Read(chunks, world.Contains, world.IntersectsChunk);
        var signs = tileList
            .Where(t => Str(t.Te.Get("id")) == "Sign")
            .Select(t => new Sign(t.X, t.Y, t.Z, MonumentSliceExtractor.ReadSignText(t.Te)))
            .ToList();
        var stands = entityList
            .Where(e => Str(e.En.Get("id")) == "ArmorStand" && world.Contains(e.Fx, (int)Math.Floor(e.Fy), e.Fz))
            .Select(e => new ArmorStand(e.Fx, e.Fy, e.Fz, HeadWool(e.En), Str(e.En.Get("CustomName"))))
            .ToList();
        // Item frames holding a WOOL item — each resolved to the support block it's mounted on + the colour.
        var frames = entityList
            .Select(e => FrameWool(e.En))
            .Where(fr => fr is not null)
            .Select(fr => fr!.Value)
            .ToList();

        var candidates = new List<MonumentCandidate>();

        // An air cell + the ids/data directly below/above it (the evidence Score re-filters), or null when
        // the cell isn't air / lies outside the gathered region.
        ((int Id, int Data) below, (int Id, int Data) above)? Cell(int x, int y, int z)
        {
            if (!world.Contains(x, y, z) || blocks.ContainsKey((x, y, z))) return null;
            blocks.TryGetValue((x, y - 1, z), out var below);
            blocks.TryGetValue((x, y + 1, z), out var above);
            return (below, above);
        }

        // Signs — both below- and above-of-monument placements, beside the sign and in its own column.
        // The declared-Label gating + the pedestal/cap filter move to Score (only the real placement
        // survives there); here we just record every air placement with its evidence.
        foreach (var sign in signs)
        {
            var text = sign.Text;
            if (!IsMonumentLabel(text)) continue;
            if (!blocks.TryGetValue((sign.X, sign.Y, sign.Z), out var sb) || sb.Id != WallSignId) continue;   // wall sign only
            if (!WallSignFacing.TryGetValue(sb.Data, out var f)) continue;
            var color = ColorFromText(text);
            // Beside placements — the wall sign faces the monument (sign + facing); always tried.
            var placements = new List<(int x, int y, int z)>
            {
                (sign.X + f.dx, sign.Y + 1, sign.Z + f.dz),   // beside, monument above (sign mounted below it)
                (sign.X + f.dx, sign.Y - 1, sign.Z + f.dz),   // beside, monument below (sign caps it)
            };
            // In-column placements — the sign sits in the monument's OWN column (e.g. nutrient's "v WOOL v"
            // capping the cell). Only when the sign's column isn't empty: a real in-column monument has a
            // pedestal within the sign's ±2 (corpus: 16/16), whereas wool signs that merely *ring* a monument
            // from open air (pigland) float (0/16 have a solid ±2) and would only emit noise.
            if (Enumerable.Range(sign.Y - 2, 5).Any(k => k != sign.Y && blocks.ContainsKey((sign.X, k, sign.Z))))
            {
                placements.Add((sign.X, sign.Y + 1, sign.Z));
                placements.Add((sign.X, sign.Y - 1, sign.Z));
            }
            foreach (var (x, y, z) in placements)
                if (Cell(x, y, z) is (var below, var above))
                    candidates.Add(new MonumentCandidate(x, y, z, "sign", below.Id, below.Data, above.Id, above.Data,
                        color, sign.X, sign.Y, sign.Z, sb.Data, text, null, null, text));
        }

        // Armour stands — one candidate per stand at the best air cell. Style-agnostic: pick the air cell
        // nearest the expected monument over *any* solid pedestal; Score re-applies the declared pedestal.
        foreach (var st in stands)
        {
            var feet = (int)Math.Floor(st.FeetY);
            var headUp = st.HeadWool is not null;
            var (lo, hi, target) = headUp ? (feet + 1, feet + 3, feet + 2) : (feet - 3, feet - 1, feet - 2);
            int? best = null;
            for (var y = lo; y <= hi; y++)
            {
                if (!world.Contains(st.Fx, y, st.Fz) || blocks.ContainsKey((st.Fx, y, st.Fz))) continue;   // air
                blocks.TryGetValue((st.Fx, y - 1, st.Fz), out var bl);
                if (!PedestalMatches(PedestalKind.Any, bl.Id)) continue;
                if (best is null || Math.Abs(y - target) < Math.Abs(best.Value - target)) best = y;
            }
            if (best is { } yy && Cell(st.Fx, yy, st.Fz) is (var below, var above))
                candidates.Add(new MonumentCandidate(st.Fx, yy, st.Fz, "armorstand", below.Id, below.Data, above.Id, above.Data,
                    st.HeadWool ?? ColorFromText(st.CustomName ?? ""), null, null, null, null, null,
                    st.HeadWool, st.CustomName, st.CustomName));
        }

        // Item frames holding wool — a 4th anchor (a_new_day mounts the frame on the pedestal → monument
        // ABOVE; golden_drought_iii mounts it on the cap → monument BELOW, the "sign+frame in one block"
        // case). Try the cell on each side of the support and keep it only when it's a real monument POCKET:
        // air over a solid pedestal that is either capped (solid above) or itself grounded ≥3 deep. That
        // structural test is what separates real frame-monuments from DECORATIVE wool frames — palette walls
        // (molcein 40: frame on a solid wall, cell isn't air), floating samplers (mist 22), and novelty
        // builds (mame's black-wool "frog eyes" on a floating platform): corpus 20/20 real cells, 0 FP.
        var frameAnchor = false;
        foreach (var (sx, sy, sz, color) in frames)
            foreach (var dy in (ReadOnlySpan<int>)[1, -1])
            {
                int cy = sy + dy;
                if (Cell(sx, cy, sz) is not (var below, var above)) continue;
                var pocket = below.Id != 0 && (above.Id != 0
                    || (blocks.ContainsKey((sx, cy - 2, sz)) && blocks.ContainsKey((sx, cy - 3, sz))));
                if (!pocket) continue;
                frameAnchor = true;
                candidates.Add(new MonumentCandidate(sx, cy, sz, "itemframe", below.Id, below.Data, above.Id, above.Data,
                    color, null, null, null, null, null, null, null, "item frame wool: " + color));
            }

        // Geometry — the LAST resort, only ever scored for Label=None. Skip it entirely when the map has a
        // genuine monument anchor (a label sign / a wool-head or monument-label-named stand / a wool item
        // frame): the author would never declare "no label" there, so the rows would be pure noise. (corpus:
        // this alone takes thunder's candidates 2193→24, pigland 258→68, and a_new_day's frame map 186→4.)
        // The stand half checks IsMonumentLabel, not just "has a name" — else a rules/info stand (lupa's
        // "Enemy Rushers may enter…") falsely anchors the map and suppresses geometry for its real
        // bedrock+glass monuments (A6).
        var anchored = frameAnchor
            || signs.Any(s => IsMonumentLabel(s.Text)
                && blocks.TryGetValue((s.X, s.Y, s.Z), out var sb) && sb.Id == WallSignId)
            || stands.Any(s => s.HeadWool is not null || IsMonumentLabel(s.CustomName));
        if (!anchored)
            foreach (var ((x, y, z), _) in blocks)
            {
                if (Cell(x, y + 1, z) is not (var below, var above)) continue;
                // UNSIGNED-MONUMENT ALLOWLIST: a distinctive pedestal (bedrock/clay/glass/wool) under a
                // COLOUR-or-MARKER cap — glass/wool/clay encode the colour, barrier marks it. These are the
                // 14 ped×cap combos real label-free monuments actually use (corpus: 38% of monuments; lupain
                // = bedrock+glass). Tighter than "any distinctive cap": slab/sign/bedrock caps are
                // terrain-ambiguous (34% of unlabelled reals but low precision) and dropped; single-signal
                // (only one distinctive) was 0.27%-precision spray, also out.
                var pedSpecific = ClassifyPedestal(below.Id) is not (PedestalKind.Any or PedestalKind.Floating);
                var capCurated = ClassifyCap(above.Id)
                    is CapKind.StainedGlass or CapKind.StainedClay or CapKind.Wool or CapKind.Barrier;
                if (!pedSpecific || !capCurated) continue;
                // ACCESSIBLE: the cell needs ≥1 air horizontal neighbour, else it's a sealed pocket in terrain,
                // not a placeable monument (corpus: 99.7% of these monuments have an open side). Cell-level
                // accessibility — supersedes the old buried-pedestal + open-sky reject.
                if (Faces.All(f => blocks.ContainsKey((x + f.dx, y + 1, z + f.dz)))) continue;
                // A clay FLOOR with a cap (≥3 same-clay neighbours) is terrain, not an isolated clay pedestal.
                if (below.Id == StainedClayId && SameMassNeighbours(x, y, z, StainedClayId) >= 3) continue;
                candidates.Add(new MonumentCandidate(x, y + 1, z, "geometry", below.Id, below.Data, above.Id, above.Data,
                    null, null, null, null, null, null, null, null, null));
            }

        // One row per CELL, keeping the strongest anchor (armorstand > sign > geometry): several wall signs
        // ringing one monument all hint at the same cell, and a monument is often marked by BOTH a stand and
        // a sign there — Score collapses by cell anyway (the stand always scores ≥ the sign), so storing the
        // duplicates just bloats the table (pigland's 64 sign rows → 40 → 8 cells, corpus parity unchanged).
        // Drop cells whose pedestal Score can never accept — pure dead storage:
        //  • a SIGN (wall 68 / post 63) is never a pedestal: PedestalMatches rejects it under *every* style
        //    (a code-level guarantee, not just corpus). These are the in-column "monument-above" emissions
        //    that land directly on top of the sign → thunder 24 → 12 (exactly its 12 real monuments).
        //  • a BARRIER (166) is never a *real* pedestal (0/593 corpus — it's only ever a cap, 78×), so an air
        //    cell above one is a deliberately-blocked, unreachable spot (pigland's barrier-cap phantom) → 8 → 4.
        return candidates
            .Where(c => c.PedestalId != WallSignId && c.PedestalId != SignPostId && c.PedestalId != BarrierId)
            .GroupBy(c => (c.X, c.Y, c.Z))
            .Select(g => g.OrderBy(c => c.Source switch { "armorstand" => 0, "itemframe" => 1, "sign" => 2, _ => 3 }).First())
            .ToList();

        // ── geometry clay-mass terrain-reject helper (§4.1) ─────────────────────────────────────
        // Same-material neighbours of the pedestal among its 8 (a clay block in a clay mass has many).
        int SameMassNeighbours(int x, int y, int z, int id) =>
            Neighbours8.Count(n => blocks.TryGetValue((x + n.dx, y, z + n.dz), out var b) && b.Id == id);
    }

    /// <summary>
    /// Score pass (authoring, pure — no world access) — filter the gathered candidates to the author's
    /// <paramref name="box"/> and declared <paramref name="style"/>, resolve each one's colour + confidence,
    /// cluster duplicates by cell (agreeing signs boost), and rank highest-confidence first. Reproduces the
    /// old <c>Suggest</c> exactly: <c>Suggest == Score(Gather(box+2), box, style)</c>.
    /// </summary>
    public static List<MonumentSuggestion> Score(
        IEnumerable<MonumentCandidate> candidates, ScanBox? box, MonumentStyle style)
    {
        var byCell = new Dictionary<(int, int, int), MonumentSuggestion>();
        // Cell-merge: keep the strongest candidate per cell (a cell can still get both a sign and a stand;
        // Gather already deduped same-source emissions, so there's no agreeing-sign boost to apply).
        void Offer(MonumentSuggestion s)
        {
            var key = (s.X, s.Y, s.Z);
            if (!byCell.TryGetValue(key, out var prev) || s.Confidence >= prev.Confidence)
                byCell[key] = s;
        }

        bool wantBelow = style.Label is LabelKind.Any or LabelKind.SignBelow;
        bool wantAbove = style.Label is LabelKind.Any or LabelKind.SignAbove;
        bool wantStand = style.Label is LabelKind.Any or LabelKind.ArmorStand;
        bool wantFrame = style.Label is LabelKind.Any or LabelKind.ItemFrame;
        bool wantGeom = style.Label is LabelKind.None;

        foreach (var c in candidates)
        {
            if (box is { } b && !b.Contains(c.X, c.Y, c.Z)) continue;

            // Declared-Label gating (old Suggest's "which anchors to run"), recovered from the source +
            // the candidate's Y vs its sign (cand above the sign = "sign below the monument", and vice versa).
            var include = c.Source switch
            {
                "sign" => (wantBelow && c.SignY is { } sy && c.Y > sy) || (wantAbove && c.SignY is { } sa && c.Y < sa),
                "armorstand" => wantStand,
                "itemframe" => wantFrame,
                "geometry" => wantGeom,
                _ => false,
            };
            if (!include) continue;

            if (!PedestalMatches(style.Pedestal, c.PedestalId)) continue;
            if (!CapMatches(style.Cap, c.CapId)) continue;

            // A stained pedestal/cap is the placed colour and wins over the parsed label/name hint.
            var color = ColorFromStain((c.PedestalId, c.PedestalData)) ?? ColorFromStain((c.CapId, c.CapData)) ?? c.ColorHint;
            Offer(new MonumentSuggestion(c.X, c.Y, c.Z, color, Confidence(c.Source, color, style), c.Source,
                c.PedestalId, c.PedestalData, c.SignX, c.SignY, c.SignZ, c.Evidence));
        }

        return byCell.Values.OrderByDescending(s => s.Confidence).ThenBy(s => (s.X, s.Y, s.Z)).ToList();
    }

    private static bool CapMatches(CapKind kind, int aboveId) =>
        kind == CapKind.Any || ClassifyCap(aboveId) == kind;

    private static bool PedestalMatches(PedestalKind kind, int belowId) => kind == PedestalKind.Any
        ? belowId != 0 && belowId != SignPostId && belowId != WallSignId   // any solid, non-sign
        : ClassifyPedestal(belowId) == kind;                               // Bedrock/Clay/Glass/Wool/Floating

    private static double Confidence(string source, string? color, MonumentStyle style)
    {
        var hasColor = !string.IsNullOrEmpty(color);
        return source switch
        {
            "armorstand" => hasColor ? 0.90 : 0.75,
            "itemframe" => 0.88,   // always carries the framed wool colour, and the structural pocket test is strict
            "geometry" => GeometryConfidence(style),
            _ /* sign */ => style.Pedestal == PedestalKind.Any ? (hasColor ? 0.78 : 0.68)
                                                               : (hasColor ? 0.90 : 0.80),
        };
    }

    // A label-free monument is only trustworthy when its geometry is *specific*: a distinctive pedestal
    // and/or cap (e.g. bedrock + glass). Bare "air on any solid" stays low.
    private static double GeometryConfidence(MonumentStyle s)
    {
        var capSpecific = s.Cap is not (CapKind.Any or CapKind.Open);
        var pedSpecific = s.Pedestal is not (PedestalKind.Any or PedestalKind.Floating);
        return (capSpecific && pedSpecific) ? 0.60 : (capSpecific || pedSpecific) ? 0.40 : 0.25;
    }

    // ---- refined sign-text classifier (port of docs/analysis/sign_text_analysis.py) ----

    // Matched against the *normalised* text (lowercased, § codes + punctuation collapsed to spaces), so
    // each phrase must be written in that form — e.g. "can t build", not "can't build".
    private static readonly string[] NegPhrases =
    {
        "team only", "only reds", "only blues", "beyond this", "behind you", "behind this", "back to",
        "entrance", "kill", "break wool", "break the", "get wool", "return the", "return your", "victory monument",
        "can t build", "cannot build", "no build", "do not", "this way", "exit", "welcome", "spawn", "shop", "buy",
        "sell", "woolroom", "wool room", "this line", "blocked", "this point", "pick up", "stand",
        "portal", "teleport", "defend", "danger", "redstone", "button", "helicopter", "use before",
    };

    // colour word → canonical wool slug (PGM names; "light gray" == silver).
    private static readonly (string word, string slug)[] ColorWords =
    {
        ("light blue", "light_blue"), ("light gray", "silver"), ("light grey", "silver"),
        ("white", "white"), ("orange", "orange"), ("magenta", "magenta"), ("yellow", "yellow"),
        ("lime", "lime"), ("pink", "pink"), ("silver", "silver"), ("gray", "gray"), ("grey", "gray"),
        ("cyan", "cyan"), ("purple", "purple"), ("blue", "blue"), ("brown", "brown"),
        ("green", "green"), ("red", "red"), ("black", "black"),
    };

    /// <summary>True if the sign text reads like a monument label (a colour + "wool"/"place…here", and
    /// none of the barrier/navigation/source phrasings). Decoration is stripped before the length gate so
    /// arrow-padded labels still pass.</summary>
    public static bool IsMonumentLabel(string? signText)
    {
        if (string.IsNullOrWhiteSpace(signText)) return false;
        var hasColor = ColorFromText(signText) is not null;
        // Strip § codes and ascii decoration (arrows, dashes, =) to spaces *before* classifying, so an
        // arrow-padded label ("---> red wool --->") reads as "red wool" and isn't tripped by decoration.
        var t = System.Text.RegularExpressions.Regex.Replace(signText.ToLowerInvariant(), "§.", " ");
        t = new string(t.Select(c => char.IsLetterOrDigit(c) || c == ' ' ? c : ' ').ToArray());
        t = System.Text.RegularExpressions.Regex.Replace(t, @"\s+", " ").Trim();
        foreach (var n in NegPhrases) if (t.Contains(n)) return false;
        if (t.Contains("place") && t.Contains("here") && hasColor) return true;
        if (t.Contains("monument") && hasColor && t.Length <= 40) return true;
        if (t.Contains("wool") && hasColor && t.Length <= 24) return true;
        return false;
    }

    /// <summary>Canonical wool colour mentioned in free text, or null. Longest match wins (so
    /// "light blue" beats "blue").</summary>
    public static string? ColorFromText(string? text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        var t = text.ToLowerInvariant();
        foreach (var (word, slug) in ColorWords) if (t.Contains(word)) return slug;
        return null;
    }

    private static string? ColorFromStain((int Id, int Data) b) =>
        b.Id is 35 or 95 or 159 && b.Data is >= 0 and < 16 ? WoolData.WoolColor(b.Data) : null;

    /// <summary>An item frame entity holding a wool item → the (support block it's mounted on, wool colour),
    /// or null when it isn't a wool-bearing item frame. The monument sits directly above/below the support.</summary>
    private static (int X, int Y, int Z, string Color)? FrameWool(NbtCompound frame)
    {
        if (Str(frame.Get("id")) is not ("ItemFrame" or "minecraft:item_frame")) return null;
        if (frame.Get("Item") is not NbtCompound item) return null;
        if (Str(item.Get("id")) is not { } iid || !iid.ToLowerInvariant().EndsWith("wool")) return null;
        if (Int(frame.Get("TileX")) is not { } tx || Int(frame.Get("TileY")) is not { } ty
            || Int(frame.Get("TileZ")) is not { } tz || Int(frame.Get("Facing")) is not { } fac
            || !FrameSupport.TryGetValue(fac, out var s)) return null;
        return (tx + s.dx, ty, tz + s.dz, WoolData.WoolColor(Int(item.Get("Damage")) ?? 0));
    }

    private static string? HeadWool(NbtCompound stand)
    {
        // 1.8 Equipment[4] = head; 1.9+ ArmorItems[3] = head.
        foreach (var (list, headIdx) in new[] { ("Equipment", 4), ("ArmorItems", 3) })
            if (stand.Get<NbtList>(list) is { } eq && eq.Count > headIdx && eq[headIdx] is NbtCompound item)
            {
                var id = Str(item.Get("id"));
                if (id is not null && id.ToLowerInvariant().EndsWith("wool"))
                    return WoolData.WoolColor(Int(item.Get("Damage")) ?? 0);
            }
        return null;
    }

    private readonly record struct Sign(int X, int Y, int Z, string Text);
    private readonly record struct ArmorStand(int Fx, double FeetY, int Fz, string? HeadWool, string? CustomName);
}
