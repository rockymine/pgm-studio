using PgmStudio.Geom;
using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Compose;

/// <summary>How far a shape actually gets. A catalog that showed every emittable shape as "what the generator
/// makes" would assert a vocabulary the boards do not carry, so every entry is badged with the last stage that
/// admits it: <see cref="InMix"/> — a sampler draws it, so it reaches real boards; <see cref="Reachable"/> —
/// <see cref="BoxFiller"/> fills it and the profile lists it, but no sampler ever asks for one;
/// <see cref="EmitterOnly"/> — only a direct <see cref="ShapeEmitter"/> call builds it, because the fill menu
/// excludes the family or the knob is not plumbed through <see cref="WoolBoxEmitter.Fill"/>.</summary>
public enum CatalogTier { InMix, Reachable, EmitterOnly }

/// <summary>One catalog entry: a shape the studio can draw, with the parameters that produced it and the tier
/// that says how far it gets. <see cref="Plan"/> is a standalone <c>symmetry:none</c> plan ready for
/// <c>PlanBoardSvg</c>; <see cref="Knobs"/> are display tokens (<c>flipped</c>, <c>side-tuck</c>,
/// <c>attach 4</c>), not a re-encoding of the call. <see cref="Note"/> carries the reason a non-
/// <see cref="CatalogTier.InMix"/> entry stops where it does, so the badge is never bare.</summary>
public sealed record CatalogShape(
    string Id,
    string Kind,
    string Family,
    CatalogTier Tier,
    int BoxW,
    int BoxH,
    int LaneWidth,
    IReadOnlyList<string> Knobs,
    string? Note,
    PlanModel Plan);

/// <summary>
/// The vocabulary as data the studio can render — every approach family and compound form the pipeline can put in a
/// box, each one emitted once and badged with how far it actually gets (<see cref="CatalogTier"/>).
///
/// <para>The reachable set is <b>collected by running the sampler</b>, never by restating its laws:
/// <see cref="WoolEntries"/> sweeps <see cref="UnitRequests.WoolRequest"/> over seeds and a grid of the two
/// inputs it reads (the hub edge it docks, the budget share it is given) and keeps the distinct requests, the
/// same discipline <see cref="Producibility"/> uses for the leg and ring-wall samplers. Retuning a chance or a
/// cap in <see cref="UnitTuning"/> therefore moves the catalog with it — there is no second copy of the mix to
/// drift.</para>
///
/// <para>Approaches are emitted <b>mouth-up</b> and deduplicated by cell pattern: the composer docks all four
/// box edges, but the other three orientations are rigid motions of what is drawn here and would pad the grid
/// four-fold without adding a shape. Both handednesses are kept — a flip is an authored variant, not a
/// viewing angle.</para>
/// </summary>
public static class ShapeCatalog
{
    /// <summary>Seeds per sampler sweep. The wool draw is a short chain of chances over small integer ranges,
    /// so it saturates in the low thousands; this is well past that and costs one sweep, memoized.</summary>
    private const int SweepSeeds = 4_000;

    /// <summary>The piece-id prefix every wool card is emitted under. <c>PlanBoardSvg</c> colours a piece by
    /// its id prefix (<c>wool</c> amber, <c>hub</c> violet, <c>frontline</c> orange), so the prefix is what
    /// makes a card read as its box kind at a glance — the same coding a whole composed board uses.</summary>
    private const string WoolPrefix = "wool";

    /// <summary>The hub edge lengths and budget shares the sweep offers <see cref="UnitRequests.WoolRequest"/>.
    /// They are the only two inputs it reads, and both quantize hard (the share becomes a depth in a three-cell
    /// band, the edge length only gates the staple's full mouth), so a coarse grid saturates the outcomes.</summary>
    private static readonly int[] EdgeLengths = [4, 6, 8, 12, 16, 24, 40];

    private static readonly double[] BudgetShares = [10, 25, 50, 100, 200, 400];

    private static IReadOnlyList<CatalogShape>? cached;

    /// <summary>The whole catalog — wool approaches, then hub compounds, then frontline compounds. Built once and
    /// held: it is a pure function of the emitters and the tuning constants.</summary>
    public static IReadOnlyList<CatalogShape> All()
    {
        if (cached is not null) return cached;
        var shapes = new List<CatalogShape>();
        shapes.AddRange(WoolEntries());
        shapes.AddRange(CompoundEntries());
        return cached = shapes;
    }

    // ── approaches ────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>The wool approaches, in tier order. The <b>in-mix</b> set is whatever the sampler sweep turned
    /// up; the <b>reachable</b> set is every family the fill menu lists that the sweep never asked for, at its
    /// minimum box; the <b>emitter-only</b> set is the families the menu excludes plus the knobs
    /// <see cref="WoolBoxEmitter.Fill"/> does not forward.</summary>
    private static IEnumerable<CatalogShape> WoolEntries()
    {
        var lane = UnitTuning.WoolLaneWidthFloor;
        var seen = new HashSet<ulong>();            // dedupe by cell pattern across every tier
        var entries = new List<CatalogShape>();
        var sampled = new HashSet<CompoundForm>();
        var n = 0;

        foreach (var (fill, along, depth) in SampledRequests())
        {
            sampled.Add(fill.Family);
            foreach (var flip in new[] { false, true })
            {
                var box = new Box(WoolPrefix, BoxKind.Wool, new CellRect(0, 0, along, depth), along * depth);
                if (WoolBoxEmitter.Fill(box, BoxEdge.Top, fill.Family, lane, out _, flip, fill.Placement,
                        fill.WoolAtEnd, fill.AttachmentWidth) is not { } filled) continue;
                if (!seen.Add(PatternKey(filled))) continue;
                entries.Add(Approach($"wool-{++n}", fill.Family, CatalogTier.InMix, along, depth, lane,
                    Knobs(fill, flip), null, filled));
            }
        }

        // families the menu admits that no sampler asks for — drawn at their minimum box so the shape is legible
        foreach (var family in FillMenu.For(BoxKind.Wool).Where(f => !sampled.Contains(f)))
        {
            var (along, depth) = WoolBoxEmitter.MouthBox(family, lane);
            var box = new Box(WoolPrefix, BoxKind.Wool, new CellRect(0, 0, along, depth), along * depth);
            if (WoolBoxEmitter.Fill(box, BoxEdge.Top, family, lane, out _) is not { } filled) continue;
            if (!seen.Add(PatternKey(filled))) continue;
            entries.Add(Approach($"wool-{++n}", family, CatalogTier.Reachable, along, depth, lane, [],
                "On the fill menu and BoxFiller fills it, but no sampler draws one (G146).", filled));
        }

        // families the fill menu excludes — only a direct emitter call builds them
        foreach (var family in EmitterOnlyFamilies)
        {
            var (w, h) = ShapeEmitter.MinBox(family, lane);
            var emitted = ShapeEmitter.Emit(family, w, h, lane);
            if (!seen.Add(PatternKey(emitted))) continue;
            entries.Add(Approach($"wool-{++n}", family, CatalogTier.EmitterOnly, w, h, lane, [],
                "Excluded from the production menu — its bay's mouth is its docking edge, so a flush dock " +
                "seals it into a walled void (WL8). Elevation alternative: G81.", emitted));
        }

        // knobs the emitter takes that WoolBoxEmitter.Fill does not forward
        foreach (var (family, knob, note, emit) in UnplumbedKnobs(lane))
        {
            if (SmallestBox(family, lane, emit) is not var (w, h, emitted)) continue;
            if (!seen.Add(PatternKey(emitted))) continue;
            entries.Add(Approach($"wool-{++n}", family, CatalogTier.EmitterOnly, w, h, lane, [knob], note, emitted));
        }

        return entries;
    }

    /// <summary>The <b>smallest box the emitter accepts</b> for a knob, found by asking it rather than by
    /// choosing one. A knob's minimum is a property of its guards — the second donut stub has to clear the
    /// first, a scythe shift has to leave spine above the bar — and those live in <see cref="ShapeEmitter"/>,
    /// not here. Picking a display size instead would make the card assert a footprint the knob does not
    /// need, which is the misreading the whole page exists to avoid: the box is printed on the card, so a
    /// generous one reads as a requirement. Scans outward from <see cref="ShapeEmitter.MinBox"/> and takes
    /// the least-area success (ties to the squarer box, so a shape is legible rather than a sliver).</summary>
    private static (int W, int H, Compound Emitted)? SmallestBox(
        CompoundForm family, int lane, Func<int, int, Compound> emit)
    {
        const int probe = 16;                                  // far past any knob's reach past the base minimum
        var (baseW, baseH) = ShapeEmitter.MinBox(family, lane);
        (int W, int H, Compound Emitted)? best = null;
        for (var w = baseW; w <= baseW + probe; w++)
            for (var h = baseH; h <= baseH + probe; h++)
            {
                Compound emitted;
                try { emitted = emit(w, h); }
                catch (ArgumentException) { continue; }         // this box does not satisfy the knob's guard
                if (best is { } b && (b.W * b.H < w * h || (b.W * b.H == w * h && b.W + b.H <= w + h))) continue;
                best = (w, h, emitted);
            }
        return best;
    }

    /// <summary>The families the emitter builds that the wool menu (<see cref="FillMenu.For"/>) leaves off, read
    /// off the two lists rather than hard-coded, so admitting one removes it from here automatically.</summary>
    private static IEnumerable<CompoundForm> EmitterOnlyFamilies =>
        ShapeEmitter.Families.Where(f => !FillMenu.For(BoxKind.Wool).Contains(f));

    /// <summary>The five knobs <see cref="ShapeEmitter.Emit"/> exposes and <see cref="WoolBoxEmitter.Fill"/>
    /// drops. Each is declared as a <b>function of the box</b>, not at a chosen size, so
    /// <see cref="SmallestBox"/> can ask the emitter what the knob actually costs.</summary>
    private static IEnumerable<(CompoundForm Family, string Knob, string Note, Func<int, int, Compound> Emit)>
        UnplumbedKnobs(int lane)
    {
        const string donutNote = "The emitter builds it, but the Fill path " +
                                 "the composer uses does not forward it (G145).";
        const string scytheNote = "A scythe endpoint shift — unreachable twice over: the knob is not " +
                                  "forwarded (G145) and the family is off the menu (G146).";
        yield return (CompoundForm.Donut, "two attachments", donutNote,
            (w, h) => ShapeEmitter.Emit(CompoundForm.Donut, w, h, lane, attachments: 2));
        yield return (CompoundForm.Donut, "moved attachment", donutNote,
            (w, h) => ShapeEmitter.Emit(CompoundForm.Donut, w, h, lane, attachmentOffset: 4));
        yield return (CompoundForm.Donut, "wool extended", donutNote,
            (w, h) => ShapeEmitter.Emit(CompoundForm.Donut, w, h, lane, woolExtend: true));
        yield return (CompoundForm.Scythe, "entry shifted", scytheNote,
            (w, h) => ShapeEmitter.Emit(CompoundForm.Scythe, w, h, lane, entryShift: 3));
        yield return (CompoundForm.Scythe, "wool shifted", scytheNote,
            (w, h) => ShapeEmitter.Emit(CompoundForm.Scythe, w, h, lane, woolShift: 3));
    }

    /// <summary>The distinct wool requests the sampler can make — collected by <b>running</b>
    /// <see cref="UnitRequests.WoolRequest"/> over seeds and the grid of inputs it reads, plus the compact
    /// demotion the seat step applies when a request finds no placement. The sampler owns its laws; this only
    /// records what it produced.</summary>
    private static IEnumerable<(WoolFill Fill, int Along, int Depth)> SampledRequests()
    {
        var distinct = new HashSet<(CompoundForm, RoomPlacement, bool, int, int, int)>();
        var requests = new List<(WoolFill, int, int)>();

        void Keep(WoolFill fill, int along, int depth)
        {
            if (distinct.Add((fill.Family, fill.Placement, fill.WoolAtEnd, fill.AttachmentWidth, along, depth)))
                requests.Add((fill, along, depth));
        }

        foreach (var edgeLen in EdgeLengths)
            foreach (var share in BudgetShares)
                for (ulong seed = 1; seed <= SweepSeeds; seed++)
                {
                    var (fill, along, depth) = UnitRequests.WoolRequest(new ComposeRng(seed), UnitTuning.WoolLaneWidthFloor, edgeLen, share, ComposeRequest.DefaultCell);
                    Keep(fill, along, depth);
                    // the seat step's fallback: any request that finds no placement is re-dispatched compact
                    var compact = UnitRequests.Compact(
                        new NeighbourRequest(UnitSide.Back, BoxKind.Wool, depth, along, "w", fill),
                        UnitTuning.WoolLaneWidthFloor, ComposeRequest.DefaultCell);
                    if (compact.Wool is { } laneWidth) Keep(laneWidth, compact.Along, compact.Depth);
                }

        return requests;
    }

    private static IReadOnlyList<string> Knobs(WoolFill fill, bool flip)
    {
        var knobs = new List<string>();
        if (flip) knobs.Add("flipped");
        if (fill.Placement == RoomPlacement.SideTuck) knobs.Add("side-tuck");
        if (fill.WoolAtEnd) knobs.Add(fill.Family == CompoundForm.Clamp ? "corner wool" : "wool at end");
        if (fill.AttachmentWidth > 0) knobs.Add($"attach {fill.AttachmentWidth}");
        return knobs;
    }

    private static CatalogShape Approach(
        string id, CompoundForm family, CatalogTier tier, int w, int h, int laneWidth,
        IReadOnlyList<string> knobs, string? note, Compound compound) =>
        new(id, "wool", family.ToString().ToLowerInvariant(), tier, w, h, laneWidth, knobs, note,
            UnitPieces.AsPlan(compound, id, WoolPrefix));

    // ── compounds ────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>The room-free compounds: the hub's form menu and the frontline's, each at a box big enough for
    /// the widest form so the grid reads as one family of shapes rather than a size ladder. Both menus are
    /// sampled by the allocator, so every entry is in the mix; the ring-wall and arm-layout axes are a separate
    /// slice (they multiply each form rather than adding one).</summary>
    private static IEnumerable<CatalogShape> CompoundEntries()
    {
        var laneWidth = FillProfiles.HubLaneWidthFloor;
        var n = 0;
        foreach (var form in FillMenu.For(BoxKind.Hub))
        {
            var box = new Box("hub", BoxKind.Hub, new CellRect(0, 0, 16, 12), 16 * 12);
            if (HubBoxEmitter.Fill(box, form, laneWidth) is not { } hub) continue;
            yield return new CatalogShape($"hub-{++n}", "hub", Token(form), CatalogTier.InMix,
                16, 12, laneWidth, [], null, UnitPieces.AsPlan(hub, $"hub-{n}", box.Id));
        }
        n = 0;
        foreach (var form in FillMenu.For(BoxKind.Frontline))
        {
            var box = new Box("frontline", BoxKind.Frontline, new CellRect(0, 0, 16, 10), 16 * 10);
            if (FrontlineBoxEmitter.Fill(box, form, laneWidth) is not { } front) continue;
            yield return new CatalogShape($"front-{++n}", "frontline", Token(form), CatalogTier.InMix,
                16, 10, laneWidth, [], null, UnitPieces.AsPlan(front, $"front-{n}", box.Id));
        }
    }

    /// <summary>A compound form's display token — the same lowercase vocabulary the browse filter chips use.</summary>
    private static string Token(CompoundForm form) => form.Word();

    // ── identity ──────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>A shape's cell pattern as one hash: the row runs of its terrain and room, translated to the
    /// pattern's own origin. Two emissions that occupy the same cells are the same catalog card however they
    /// were parameterized.</summary>
    private static ulong PatternKey(Compound compound)
    {
        var parts = compound.Pieces.Select(p => (Rect: p.Rect, Room: p.Part == CompoundParts.Room)).ToList();
        int minX = parts.Min(p => p.Rect.X), minZ = parts.Min(p => p.Rect.Z);
        var maxZ = parts.Max(p => p.Rect.Z + p.Rect.Height);
        var hash = 14695981039346656037UL;
        void Mix(long v) { hash ^= (ulong)v; hash *= 1099511628211UL; }
        for (var z = minZ; z < maxZ; z++)
        {
            var runs = parts
                .Where(p => z >= p.Rect.Z && z < p.Rect.Z + p.Rect.Height)
                .Select(p => (X0: p.Rect.X - minX, X1: p.Rect.X + p.Rect.Width - minX, p.Room))
                .OrderBy(r => r.X0).ToList();
            if (runs.Count == 0) continue;
            Mix(z - minZ);
            foreach (var (x0, x1, room) in runs) { Mix(x0); Mix(x1); Mix(room ? 1 : 0); }
        }
        return hash;
    }
}
