using System.Text;
using fNbt;
using static PgmStudio.Minecraft.Nbt;

namespace PgmStudio.Minecraft;

/// <summary>The pedestal block an author placed directly under the (air) monument block. Drawn from the
/// corpus: bedrock 33%, stained clay 16%, stained glass 14%, wool 11%, floating(=air) 9% — see
/// <c>docs/analysis/monument-patterns.md</c>.</summary>
public enum PedestalKind { Any, Bedrock, StainedClay, StainedGlass, Wool, Floating }

/// <summary>How the monument is labelled: a sign on the block below (34%), a sign above (16%), an
/// armour stand (3%), or no label (~47% → geometry-only fallback).</summary>
public enum LabelKind { Any, SignBelow, SignAbove, ArmorStand, None }

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

    private const int WallSignId = 68, SignPostId = 63, WoolId = 35;

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
    /// <paramref name="style"/>. Chunks outside the box (+2 margin) are skipped.</summary>
    public static List<MonumentSuggestion> Suggest(
        IEnumerable<AnvilRegion.Chunk> chunks, ScanBox box, MonumentStyle style)
    {
        var scan = box.Expand(2);   // a little slack so a sign/pedestal at the box edge still resolves
        var (blocks, tileList, entityList) = RegionScan.Read(chunks, scan.Contains, scan.IntersectsChunk);
        var signs = tileList
            .Where(t => Str(t.Te.Get("id")) == "Sign")
            .Select(t => new Sign(t.X, t.Y, t.Z, MonumentSliceExtractor.ReadSignText(t.Te)))
            .ToList();
        var stands = entityList
            .Where(e => Str(e.En.Get("id")) == "ArmorStand" && scan.Contains(e.Fx, (int)Math.Floor(e.Fy), e.Fz))
            .Select(e => new ArmorStand(e.Fx, e.Fy, e.Fz, HeadWool(e.En), Str(e.En.Get("CustomName"))))
            .ToList();

        // candidate cell -> best suggestion (cluster duplicates by exact cell; signs that agree boost it)
        var byCell = new Dictionary<(int, int, int), MonumentSuggestion>();
        void Offer(MonumentSuggestion s)
        {
            var key = (s.X, s.Y, s.Z);
            if (!byCell.TryGetValue(key, out var prev)) { byCell[key] = s; return; }
            // keep the stronger source; if two independent signs agree, nudge confidence up.
            var merged = s.Confidence >= prev.Confidence ? s : prev;
            var boost = (prev.Source == "sign" && s.Source == "sign") ? 0.05 : 0.0;
            byCell[key] = merged with { Confidence = Math.Min(0.99, merged.Confidence + boost) };
        }

        bool wantBelow = style.Label is LabelKind.Any or LabelKind.SignBelow;
        bool wantAbove = style.Label is LabelKind.Any or LabelKind.SignAbove;

        if (wantBelow || wantAbove)
            foreach (var sign in signs)
            {
                var text = sign.Text;
                if (!IsMonumentLabel(text)) continue;
                if (!blocks.TryGetValue((sign.X, sign.Y, sign.Z), out var sb) || sb.Id != WallSignId) continue;   // wall sign only
                if (!WallSignFacing.TryGetValue(sb.Data, out var f)) continue;
                var color = ColorFromText(text);
                // Two placements per level: the sign mounted *beside* the monument facing it (sign + facing),
                // or *in the monument's own column* (e.g. nutrient's "v Orange Wool v" capping the cell). Both
                // are validated against air + the declared pedestal/cap, so only the real one survives.
                if (wantBelow)
                {
                    TryEmit(sign.X + f.dx, sign.Y + 1, sign.Z + f.dz, color, "sign", sign, text);   // beside, at pedestal level
                    TryEmit(sign.X, sign.Y + 1, sign.Z, color, "sign", sign, text);                 // in-column, sign just under the monument
                }
                if (wantAbove)
                {
                    TryEmit(sign.X + f.dx, sign.Y - 1, sign.Z + f.dz, color, "sign", sign, text);   // beside, at cap level
                    TryEmit(sign.X, sign.Y - 1, sign.Z, color, "sign", sign, text);                 // in-column, sign caps the monument
                }
            }

        if (style.Label is LabelKind.Any or LabelKind.ArmorStand)
            foreach (var st in stands)
            {
                // Disambiguate by what the stand carries: a stand with wool *on its head* marks the
                // monument just *above* it (the wool sits where you place it — e.g. pigland); a name-only
                // marker stand sits *above* the monument and points down at it (e.g. dragons_hearth).
                var feet = (int)Math.Floor(st.FeetY);
                var headUp = st.HeadWool is not null;
                var (lo, hi, target) = headUp ? (feet + 1, feet + 3, feet + 2) : (feet - 3, feet - 1, feet - 2);
                int? best = null;
                for (var y = lo; y <= hi; y++)
                {
                    if (!box.Contains(st.Fx, y, st.Fz) || blocks.ContainsKey((st.Fx, y, st.Fz))) continue;   // must be air
                    blocks.TryGetValue((st.Fx, y - 1, st.Fz), out var bl);
                    if (!PedestalMatches(style.Pedestal, bl.Id)) continue;
                    if (best is null || Math.Abs(y - target) < Math.Abs(best.Value - target)) best = y;
                }
                if (best is { } yy)
                    TryEmit(st.Fx, yy, st.Fz, st.HeadWool ?? ColorFromText(st.CustomName ?? ""), "armorstand", null, st.CustomName);
            }

        // pure-geometry fallback when the author says there is no label — the pedestal *and cap* filter
        // is what makes label-free monuments (e.g. lupa/lupain: bedrock below + glass cap) findable.
        if (style.Label == LabelKind.None)
            foreach (var ((x, y, z), _) in blocks)
            {
                if (!box.Contains(x, y + 1, z)) continue;
                TryEmit(x, y + 1, z, null, "geometry", null, null);   // colour inferred from cap/pedestal
            }

        return byCell.Values.OrderByDescending(s => s.Confidence).ThenBy(s => (s.X, s.Y, s.Z)).ToList();

        // --- local: validate an air cell on the declared pedestal + under the declared cap, and offer it ---
        bool TryEmit(int x, int y, int z, string? color, string source, Sign? sign, string? evidence)
        {
            if (!box.Contains(x, y, z)) return false;
            if (blocks.ContainsKey((x, y, z))) return false;                      // monument cell must be air
            blocks.TryGetValue((x, y - 1, z), out var below);                     // (0,0) when below is air
            blocks.TryGetValue((x, y + 1, z), out var above);
            if (!PedestalMatches(style.Pedestal, below.Id)) return false;
            if (!CapMatches(style.Cap, above.Id)) return false;
            // A stained pedestal/cap (wool/clay/glass) is the *placed* colour and is authoritative over
            // parsed sign text (corpus: stain matches the true wool colour far more often than prose, which
            // uses approximate words like purple↔magenta). Stain wins; the label's colour is the fallback.
            color = ColorFromStain(below) ?? ColorFromStain(above) ?? color;
            Offer(new MonumentSuggestion(x, y, z, color,
                Confidence(source, color, style), source,
                below.Id, below.Data,
                sign?.X, sign?.Y, sign?.Z, evidence));
            return true;
        }
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
