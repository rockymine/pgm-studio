using PgmStudio.Geom;
using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Compose;

/// <summary>The parameter tuple that reproduced a box's terrain exactly — what the composer would have had to
/// draw to make it. <see cref="Label"/> names the tuple in the vocabulary's own terms.</summary>
public sealed record ProducibleAs(string Label, int Cw);

/// <summary>The closest the emitters got when nothing reproduced the box: the candidate whose terrain differs in
/// the fewest cells, and how it differs. <see cref="Extra"/> are cells the candidate emits that the box does not
/// have, <see cref="Missing"/> cells the box has that the candidate does not — the pair that says <em>where</em>
/// the authored geometry left the parameter space, without a bespoke per-family analyser.</summary>
public sealed record NearestMiss(
    string Label, int Cw, int DifferingCells,
    IReadOnlyList<int[]> Extra, IReadOnlyList<int[]> Missing);

/// <summary>One directed finding about a box's producibility. <see cref="Code"/> is a stable slug (the
/// machine-legible half); <see cref="Cites"/> is the rule id or the gap's task id when one applies
/// (<c>"G123"</c>); <see cref="Detail"/> is the human half, carrying measured numbers.</summary>
public sealed record ProducibilityFinding(string Code, string? Cites, string Detail);

/// <summary>
/// One box's producibility read: what the derivers see it as, whether any parameter tuple the production menus
/// admit reproduces it, and — when none does — the nearest candidate plus the directed findings.
///
/// <para><see cref="Identity"/> is a <b>hint, never a verdict</b>: the classifiers read topology, so a shape with
/// 1-cell walls still reads as the <c>G</c> it topologically is even though no <c>G</c> is emittable at that
/// width. Producibility is the separate question this record answers.</para>
/// </summary>
public sealed record BoxProducibility(
    string BoxId, string Kind, string Identity,
    ProducibleAs? Producible, NearestMiss? Nearest,
    IReadOnlyList<ProducibilityFinding> Findings)
{
    public bool IsProducible => Producible is not null;
}

/// <summary>
/// <b>Could the composer have produced this?</b> — the emit↔derive mirror turned into an authoring answer, and
/// the question the validator does not ask (a plan can score 0 and still be unbuildable by the machine).
///
/// <para>It answers by <b>search, never by inverse</b>. The parameter space is already declared as data — the
/// hub and frontline form menus, the wool production families, the spawn sizes, the wall/lane widths — and every
/// emitter is a pure function of an explicit tuple with no RNG inside (sampling lives up in
/// <see cref="TeamUnitAllocator"/>). So this enumerates the tuples those tables admit, calls the <b>real
/// emitters</b>, and compares masks. Nothing here re-derives a rule: add a hub form and the search picks it up
/// for free, which a hand-written parameter recovery could not.</para>
///
/// <para>The <b>why</b> comes from three sources, all of them existing: the emitters' own
/// <see cref="FillRejection"/> reasons; a measurement of the mask against the same constants the emitters read
/// (<see cref="Cells.MinRunWidthRaw"/> vs <see cref="FillProfiles.HubWallCells"/> /
/// <see cref="TeamUnitAllocator.WoolLaneCells"/>); and the <b>nearest miss</b>, which the enumeration produces
/// for free.</para>
///
/// <para>Terrain and room are compared <b>separately</b>: a box whose corridor the emitters reproduce but whose
/// terminal room they do not is a distinct, directed answer ("terrain replicable, room not"), because the room
/// carries load-bearing export semantics (the bedrock floor and entrance line) rather than being just more
/// terrain.</para>
///
/// <para>Pure and UI-free by design — the editor panel and any later agent surface are thin heads over this one
/// implementation.</para>
/// </summary>
public static class Producibility
{
    /// <summary>The lane widths a board may run at (<see cref="TeamUnitAllocator"/> picks 2 or 3 from the land
    /// budget). A standalone box read does not know its board's choice, so both are tried.</summary>
    private static readonly int[] LaneWidths = [2, 3];

    /// <summary>How many seeds to draw <see cref="FrontlineBoxEmitter.SampleArms"/> with when enumerating the
    /// arm layouts it can produce. The layout space is tiny and the sampler is the only thing that knows its
    /// laws, so its <em>range</em> is collected by running it rather than by restating them here.</summary>
    private const int ArmLayoutSeeds = 400;

    /// <summary>Read every box in <paramref name="plan"/>.</summary>
    public static IReadOnlyList<BoxProducibility> Read(PlanModel plan) =>
        plan.Boxes.Select(b => Read(plan, b)).ToList();

    /// <summary>Read one box: group its members, derive its identity, search the declared parameter space, and
    /// report.</summary>
    public static BoxProducibility Read(PlanModel plan, PlanBox box)
    {
        var members = PlanBoxes.MembersOf(plan, box);
        if (members.Count == 0)
            return new BoxProducibility(box.Id, box.Kind, "empty", null, null,
                [new ProducibilityFinding("box-empty", null, "The box groups no pieces.")]);

        var terrain = Mask(members.Where(p => p.Role == PlanRoles.Piece).Select(p => p.Rect));
        var roomPieces = members.Where(p => p.Role is PlanRoles.WoolRoom or PlanRoles.Spawn).ToList();
        var roomCells = Mask(roomPieces.Select(p => p.Rect));
        var all = new HashSet<(int, int)>(terrain);
        all.UnionWith(roomCells);

        var identity = Identify(all, roomCells);
        var findings = new List<ProducibilityFinding>();

        // The measurement, against the same constant the emitter reads. Independent of the search: a corridor
        // narrower than any lane the vocabulary has is worth saying even when a nearest miss also fires.
        var cwFloor = box.Kind == PlanBoxKinds.Wool ? TeamUnitAllocator.WoolLaneCells : FillProfiles.HubWallCells;
        var measured = Cells.MinRunWidthRaw(all, all);
        if (measured < cwFloor)
            findings.Add(new ProducibilityFinding("corridor-below-minimum", "G2",
                $"Narrowest cross-section is {measured} cell(s); the emitters build at {cwFloor} " +
                $"({(box.Kind == PlanBoxKinds.Wool ? "the wool lane" : "the hub/body wall")} width). " +
                "Every part of this box would have to be at least that wide."));

        var candidates = Candidates(box, all).ToList();
        if (candidates.Count == 0)
            findings.Add(new ProducibilityFinding("no-candidates", null,
                $"No production menu covers a '{box.Kind}' box, so there is nothing to compare against."));

        // exact terrain+room match — the box is producible
        foreach (var c in candidates.Where(c => c.Mask is not null))
            if (c.Mask!.SetEquals(all))
                return new BoxProducibility(box.Id, box.Kind, identity,
                    new ProducibleAs(c.Label, c.Cw), null, findings);

        // the terrain/room split: the corridor reproduces but the terminal room does not. Only reachable past
        // the exact-match return above, so every candidate here already differs somewhere.
        if (roomPieces.Count > 0
            && candidates.FirstOrDefault(c => c.Mask is not null && c.Room is not null
                                              && TerrainOnly(c.Mask!, c.Room!).SetEquals(terrain)) is { } roomMiss)
            findings.Add(new ProducibilityFinding("room-not-replicable", "ST1",
                $"The corridor is reproducible ({roomMiss.Label}) but the terminal room is not: the emitters " +
                $"build a compact {ShapeEmitter.RoomDepthCells}-cell-deep room and this one differs. The room " +
                "is not just terrain — the export stamps its bedrock floor and entrance line from it."));

        var nearest = Nearest(candidates, all);
        if (nearest is not null)
            findings.Add(new ProducibilityFinding("no-parameters-reproduce", null,
                $"No parameter tuple on the production menus reproduces this box. Closest is {nearest.Label} " +
                $"at cw {nearest.Cw}, differing in {nearest.DifferingCells} cell(s)."));
        else if (candidates.Count > 0)
            findings.AddRange(Refusals(candidates, box.Rect));

        return new BoxProducibility(box.Id, box.Kind, identity, null, nearest, findings);
    }

    /// <summary>What the derivers read the box as — the approach family for a roomed box, the body compound for a
    /// terminal-free one. A hint that narrows a search, not a verdict (see <see cref="BoxProducibility"/>).</summary>
    private static string Identify(IReadOnlySet<(int, int)> all, IReadOnlySet<(int, int)> roomCells)
    {
        if (all.Count == 0) return "empty";
        if (roomCells.Count > 0)
        {
            var read = ShapeClassifier.Classify(all, roomCells);
            return $"{read.Family} (w {read.Width})";
        }
        var body = ShapeClassifier.ClassifyBody(all);
        return body.Arms > 0 ? $"{body.Form}({body.Arms} arms)" : body.Form.ToString();
    }

    /// <summary>One enumerated tuple: how it reads, the terrain+room mask it emits (<c>null</c> when the emitter
    /// refused), the room rect it stamps, and the refusal reason when there is one.</summary>
    private sealed record Candidate(
        string Label, int Cw, HashSet<(int, int)>? Mask, int[]? Room, FillRejection? Rejection);

    /// <summary>Every tuple the declared production menus admit for this box kind, emitted into the box's own
    /// footprint by the real emitters. The menus are read as data — nothing here restates them.</summary>
    private static IEnumerable<Candidate> Candidates(PlanBox box, IReadOnlySet<(int, int)> target) =>
        box.Kind switch
        {
            PlanBoxKinds.Hub => HubCandidates(box),
            PlanBoxKinds.Frontline => FrontlineCandidates(box),
            PlanBoxKinds.Wool => ApproachCandidates(box, BoxKind.Wool),
            PlanBoxKinds.Spawn => ApproachCandidates(box, BoxKind.Spawn),
            _ => [],
        };

    private static IEnumerable<Candidate> HubCandidates(PlanBox box)
    {
        var b = new Box(box.Id, BoxKind.Hub, box.Rect, box.Rect[2] * box.Rect[3]);
        var cw = FillProfiles.HubWallCells;
        foreach (var form in FillProfiles.HubForms)
            foreach (var flip in new[] { false, true })
            {
                var label = $"{Name(form)}{(flip ? " flipped" : "")}";
                var hub = HubBoxEmitter.Fill(b, form, cw, flip, out var why);
                yield return hub is null
                    ? new Candidate(label, cw, null, null, why)
                    : new Candidate(label, cw, Mask(hub.Pieces.Select(p => p.Rect)), null, null);
            }
    }

    private static IEnumerable<Candidate> FrontlineCandidates(PlanBox box)
    {
        var b = new Box(box.Id, BoxKind.Frontline, box.Rect, box.Rect[2] * box.Rect[3]);
        foreach (var cw in LaneWidths)
            foreach (var form in FillProfiles.FrontlineForms)
                foreach (var mouth in AllEdges)
                    foreach (var grouping in new[] { OfferGrouping.Joint, OfferGrouping.Several })
                        foreach (var layout in ArmLayouts(form, box, mouth))
                        {
                            var label = $"{Name(form)} spine {mouth}{(layout is null ? "" : " legs " + Legs(layout))}";
                            var f = FrontlineBoxEmitter.Fill(b, form, cw, grouping, out var why, mouth, armLayout: layout);
                            yield return f is null
                                ? new Candidate(label, cw, null, null, why)
                                : new Candidate(label, cw, Mask(f.Pieces.Select(p => p.Rect)), null, null);
                        }
    }

    /// <summary>The arm layouts a branch frontline form can take in this box — collected by <b>running</b>
    /// <see cref="FrontlineBoxEmitter.SampleArms"/> over many seeds rather than restating its laws (every leg ≥ 2,
    /// widths within factor 2, bay 2–4, recesses ≤ a third of the spine). The canonical layout (<c>null</c>) is
    /// always offered first.</summary>
    private static IEnumerable<IReadOnlyList<(int Start, int Width)>?> ArmLayouts(
        CompoundRead form, PlanBox box, BoxEdge mouth)
    {
        yield return null;                                   // the canonical fat L / symmetric twin
        if (form.Form != Compound.SpineArms) yield break;
        var spineLen = mouth is BoxEdge.Left or BoxEdge.Right ? box.Rect[3] : box.Rect[2];
        var seen = new HashSet<string>();
        for (ulong seed = 1; seed <= ArmLayoutSeeds; seed++)
        {
            var layout = FrontlineBoxEmitter.SampleArms(new ComposeRng(seed), spineLen, form.Arms);
            if (layout is null) continue;
            if (seen.Add(Legs(layout))) yield return layout;
        }
    }

    private static IEnumerable<Candidate> ApproachCandidates(PlanBox box, BoxKind kind)
    {
        var b = new Box(box.Id, kind, box.Rect, box.Rect[2] * box.Rect[3]);
        // the wool lane is fixed at w2; a spawn reads the map's lane width, which a standalone box does not know
        var widths = kind == BoxKind.Wool ? new[] { TeamUnitAllocator.WoolLaneCells } : LaneWidths;
        foreach (var cw in widths)
            foreach (var family in FillProfiles.Families(kind, cw))
                foreach (var mouth in AllEdges)
                    foreach (var flip in new[] { false, true })
                        foreach (var (placement, atEnd, attachW) in ApproachKnobs(kind, family, cw))
                        {
                            var label = $"{family}{(flip ? " flipped" : "")} mouth {mouth}" +
                                        $"{(placement == RoomPlacement.Inline ? "" : " side-tuck")}" +
                                        $"{(atEnd ? " wool-at-end" : "")}" +
                                        $"{(attachW > 0 ? $" attach {attachW}" : "")}";
                            if (kind == BoxKind.Spawn)
                            {
                                var s = SpawnBoxEmitter.Fill(b, mouth, family, cw, flip, $"{box.Id}-room", out var sWhy);
                                yield return s is null
                                    ? new Candidate(label, cw, null, null, sWhy)
                                    : new Candidate(label, cw, Mask(s.Pieces.Select(p => p.Rect)), s.Room.Rect, null);
                                continue;
                            }
                            var res = BoxFiller.Fill(b, mouth, cw, family, flip, $"{box.Id}-room", placement, atEnd, attachW);
                            if (res is not FillResult.Ok ok)
                            {
                                yield return new Candidate(label, cw, null, null, res.Rejection);
                                continue;
                            }
                            var mask = Mask(ok.Approach.Terrain.Select(p => p.Rect));
                            mask.UnionWith(Mask([ok.Approach.WoolRoom.Rect]));
                            yield return new Candidate(label, cw, mask, ok.Approach.WoolRoom.Rect, null);
                        }
    }

    /// <summary>The per-family emit knobs, from the declared ranges: the room placement, the wool-at-an-end
    /// variant, and the donut's sampled hub-entry width (<c>cw..DonutEntryMaxCells</c>). The donut's hole size is
    /// not a knob here — the allocator spends it as box <em>growth</em>, so a given footprint implies it.</summary>
    private static IEnumerable<(RoomPlacement Placement, bool AtEnd, int AttachW)> ApproachKnobs(
        BoxKind kind, ShapeFamily family, int cw)
    {
        if (kind == BoxKind.Spawn) { yield return (RoomPlacement.Inline, false, 0); yield break; }
        foreach (var placement in new[] { RoomPlacement.Inline, RoomPlacement.SideTuck })
            foreach (var atEnd in new[] { false, true })
            {
                if (family != ShapeFamily.Donut) { yield return (placement, atEnd, 0); continue; }
                yield return (placement, atEnd, 0);          // the min-only one-corridor entry
                for (var aw = cw; aw <= TeamUnitAllocator.DonutEntryMaxCells; aw++)
                    yield return (placement, atEnd, aw);
            }
    }

    /// <summary>The findings for a box <b>no</b> form even emitted into — every candidate was refused, so there is
    /// no geometry to diff. A too-small footprint is the common case and collapses to <b>one</b> finding naming
    /// the smallest box any form on the menu fits: four near-identical "below the minimum" lines (one per mouth
    /// orientation) tell the author nothing the smallest one doesn't. Other refusal kinds report distinct
    /// reasons.</summary>
    private static IEnumerable<ProducibilityFinding> Refusals(IReadOnlyList<Candidate> candidates, int[] rect)
    {
        var tooSmall = candidates
            .Select(c => c.Rejection).OfType<FillRejection.TooSmall>()
            .OrderBy(t => t.MinW * t.MinH).ThenBy(t => t.MinW).FirstOrDefault();
        if (tooSmall is not null)
            yield return new ProducibilityFinding("box-too-small", null,
                $"This box is {rect[2]}x{rect[3]} cells; the smallest footprint any form on the menu fits is " +
                $"{tooSmall.MinW}x{tooSmall.MinH}. Nothing can be emitted into it.");

        foreach (var detail in candidates
                     .Select(c => c.Rejection).Where(r => r is not null and not FillRejection.TooSmall)
                     .Select(r => Describe(r!)).Distinct().Take(3))
            yield return new ProducibilityFinding("every-form-refused", null, detail);
    }

    /// <summary>The candidate whose emitted terrain differs from <paramref name="target"/> in the fewest cells,
    /// or <c>null</c> when every candidate was refused outright (nothing to compare).</summary>
    private static NearestMiss? Nearest(IReadOnlyList<Candidate> candidates, IReadOnlySet<(int, int)> target)
    {
        NearestMiss? best = null;
        foreach (var c in candidates)
        {
            if (c.Mask is null) continue;
            var extra = c.Mask.Except(target).ToList();
            var missing = target.Except(c.Mask).ToList();
            var diff = extra.Count + missing.Count;
            if (best is not null && diff >= best.DifferingCells) continue;
            best = new NearestMiss(c.Label, c.Cw, diff,
                extra.Select(p => new[] { p.Item1, p.Item2, 1, 1 }).ToList(),
                missing.Select(p => new[] { p.Item1, p.Item2, 1, 1 }).ToList());
        }
        return best;
    }

    private static readonly BoxEdge[] AllEdges = [BoxEdge.Top, BoxEdge.Bottom, BoxEdge.Left, BoxEdge.Right];

    private static HashSet<(int, int)> TerrainOnly(IReadOnlySet<(int, int)> mask, int[] room)
    {
        var roomCells = Mask([room]);
        return mask.Where(c => !roomCells.Contains(c)).ToHashSet();
    }

    private static HashSet<(int, int)> Mask(IEnumerable<int[]> rects)
    {
        var cells = new HashSet<(int, int)>();
        foreach (var r in rects)
            for (var x = r[0]; x < r[0] + r[2]; x++)
                for (var z = r[1]; z < r[1] + r[3]; z++) cells.Add((x, z));
        return cells;
    }

    private static string Name(CompoundRead form) =>
        form.Arms > 0 ? $"{form.Form}({form.Arms})" : form.Form.ToString();

    private static string Legs(IReadOnlyList<(int Start, int Width)> layout) =>
        string.Join("+", layout.Select(a => $"{a.Start}:{a.Width}"));

    private static string Describe(FillRejection r) => r switch
    {
        FillRejection.TooSmall t => $"The footprint is below the minimum box ({t.MinW}x{t.MinH} cells).",
        FillRejection.FormDoesNotFit f => f.Detail,
        FillRejection.NotOnMenu n => $"Off the production menu (on offer: {string.Join(", ", n.Menu)}).",
        FillRejection.IllegalDock d => $"The docking gate refuses the {d.Mouth} edge ({d.Reason}).",
        FillRejection.UnsupportedKnobs u => u.Detail,
        _ => "Refused.",
    };
}
