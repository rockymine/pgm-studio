using PgmStudio.Domain;
using PgmStudio.Geom;
using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Shapes;
using PgmStudio.Vocabulary;

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
    IReadOnlyList<CellRect> Extra, IReadOnlyList<CellRect> Missing);

/// <summary>A producibility read answers in <see cref="Findings"/>, the same shape every gate answers in, and
/// its findings are the reasons the answer is no — so inside the read they are <b>refusals</b> and
/// <see cref="Findings.Refuses"/> is a question this file can ask of itself. They leave as
/// <b>complaints</b>: a box the emitters cannot reproduce is still a box an author drew, and the read says why
/// the composer's parameter space does not reach it rather than declining to build it. That downgrade is
/// <see cref="Findings.AsComplaints"/>, applied at the wire. The rule is the finding's own slug, since it is
/// the stable thing a caller keys on; the layout rule or gap it cites, where it cites one, is named in the
/// sentence beside the measured numbers.</summary>

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
    Findings Findings)
{
    public bool IsProducible => Producible is not null;
}

/// <summary>The whole plan's producibility: each box's own read plus the <b>unit-level</b> findings, which are
/// properties of how the boxes sit together (the parallel-fronts guard, the frontline's face demand, the
/// seat-separation law) rather than of any one box. Both halves are reported: a box can be unproducible on its
/// own geometry <em>and</em> the unit unproducible in how it is arranged, and an author wants to see both.</summary>
public sealed record PlanProducibility(
    IReadOnlyList<BoxProducibility> Boxes, Findings Unit)
{
    /// <summary>True when every box reproduces and no unit-level rule stands in the way. The second half asks
    /// <see cref="Findings.Refuses"/> rather than counting, so a unit finding added as a remark — one that
    /// observes something about the arrangement without putting it out of the composer's reach — does not
    /// silently turn a producible plan unproducible.</summary>
    public bool IsProducible => Boxes.All(b => b.IsProducible || b.Kind == PlanBoxKinds.Mid) && !Unit.Refuses;
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
/// <see cref="UnitTuning.WoolLaneCells"/>); and the <b>nearest miss</b>, which the enumeration produces
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

    /// <summary>How many seeds to draw a composer sampler with when enumerating what it can produce — the
    /// frontline's arm layouts, the hub's ring walls and leg layouts. The sampler is the only thing that knows its
    /// laws, so its <em>range</em> is collected by running it rather than by restating them here — which makes the
    /// seed count the coverage guarantee, and it has to clear the <b>largest</b> space any sampler has. The ring
    /// walls, the hub's legs and the single-leg frontline saturate within fifty draws; the frontline's two-leg
    /// layout does not, because its bay, end recess, offset and split are drawn independently — on a 20-cell spine
    /// it has ~390 outcomes and the rarest first appears around seed 5000. Under-drawing it makes the search miss
    /// layouts the composer really draws, i.e. a false unproducible. The sweeps are memoized per space
    /// (<see cref="Memo{T}"/>), so the count costs sampler calls once, not per box.</summary>
    private const int SamplerSweepSeeds = 50_000;

    /// <summary>The result of one sampler sweep, held per <b>space</b> — the dimensions the sampler reads, which
    /// is all its range depends on. A sweep is re-asked for every form, mouth, grouping and lane width a box is
    /// tried at, and those do not change the answer.</summary>
    private static readonly Dictionary<string, object> Sweeps = [];

    private static IReadOnlyList<T> Memo<T>(string key, Func<IReadOnlyList<T>> build)
    {
        lock (Sweeps)
        {
            if (Sweeps.TryGetValue(key, out var hit)) return (IReadOnlyList<T>)hit;
            var built = build();
            Sweeps[key] = built;
            return built;
        }
    }

    /// <summary>Read every box in <paramref name="plan"/>.</summary>
    public static IReadOnlyList<BoxProducibility> Read(PlanModel plan) =>
        plan.Boxes.Select(b => Read(plan, b)).ToList();

    /// <summary>Read the whole plan — every box plus the unit-level rules.</summary>
    public static PlanProducibility ReadPlan(PlanModel plan) => new(Read(plan), UnitFindings(plan));

    /// <summary>
    /// The rules that are properties of the <b>arrangement</b>, not of one box: the parallel-fronts guard, the
    /// frontline's pinned face demand, and the seat-separation law. Each is asked of the composer's own
    /// predicate where one exists (<see cref="Composer.FrontFacesSymmetric"/>,
    /// <see cref="SeatGeometry.TooClose"/>) rather than restated here.
    /// </summary>
    /// <summary>
    /// The growth frame an <b>authored</b> unit sits in. <see cref="Frame.For"/> fixes the sign per symmetry
    /// because a composed unit always grows on the +u side — but an authored plan may be drawn on either side of
    /// the axis, and reading <c>u</c> the wrong way round makes the min-u pieces the unit's <b>back</b>. Every
    /// front rule would then measure the spawn instead of the frontline. The sign is inferred from where the
    /// pieces actually are: a unit lying wholly on the far side gets the sign flipped so <c>u</c> still grows
    /// away from the axis.
    /// </summary>
    private static Frame AuthoredFrame(string symmetry, IReadOnlyList<CellRect> rects)
    {
        var frame = Frame.For(symmetry);
        if (rects.Count == 0) return frame;
        var uv = rects.Select(frame.FromRect).ToList();
        return uv.Max(r => r.UMin + r.USpan) <= 0 ? new Frame(frame.PrimaryAxis, -frame.Sign) : frame;
    }

    /// <summary>
    /// The widths of the contact patches a frontline's face makes with the hub's <b>front-row terrain</b> — the
    /// shoulders it rests on, in cells.
    ///
    /// <para>Read off the hub's member pieces rather than its box: a bay-fronted hub's box spans the bay, so a
    /// box-level overlap would report one wide contact where the face actually lands on two shoulders with a
    /// hole between them, which is the very distinction the spanning dock turns on.</para>
    /// </summary>
    private static IReadOnlyList<int> FrontPatches(PlanModel plan, PlanBox hub, PlanBox front, Frame frame)
    {
        var h = frame.FromRect(hub.Rect);
        var f = frame.FromRect(front.Rect);
        // the hub's front row: the member pieces whose own u-start is the box's, i.e. those facing the axis
        var filled = new SortedSet<int>();
        foreach (var piece in PlanBoxes.MembersOf(plan, hub))
        {
            var p = frame.FromRect(piece.Rect);
            if (p.UMin != h.UMin) continue;
            for (var v = p.VMin; v < p.VMin + p.VSpan; v++) filled.Add(v);
        }

        var patches = new List<int>();
        int run = 0;
        for (var v = f.VMin; v < f.VMin + f.VSpan; v++)
        {
            if (filled.Contains(v)) run++;
            else if (run > 0) { patches.Add(run); run = 0; }
        }
        if (run > 0) patches.Add(run);
        return patches;
    }

    private static Findings UnitFindings(PlanModel plan)
    {
        var findings = new List<Finding>();
        if (plan.Boxes.Count == 0) return findings;

        var symmetry = plan.Globals.Symmetry;
        var terrain = plan.Pieces.Where(p => PlanRoles.IsGenerating(p.Role)).Select(p => p.Rect).ToList();
        var frame = AuthoredFrame(symmetry, terrain);

        // the parallel-fronts guard: the mid band spans the hull of both images' front faces, so a front off the
        // axis costs the band slack rather than making it impossible. The gate bounds that slack (BZ9).
        if (terrain.Count > 0 && MidCarver.LateralFlip(symmetry)
            && Composer.FrontHullSlackCells(frame, terrain) is var slack && slack > Composer.FrontSlackCapCells)
            findings.Add(new Finding("front-hull-off-axis",
                $"The unit's front faces sit off the symmetry axis, so the mid band — which spans the hull of " +
                $"both images' faces — would reach {slack} cell(s) past the front it docks, over the " +
                $"{Composer.FrontSlackCapCells}-cell cap. Centre the front on the axis, or widen it so its " +
                "hull is symmetric; the legs within it need not be.", Cites: "BZ9"));

        // the frontline's face: a sampled width seated anywhere along the hub's front edge, free to overhang it,
        // but every contact patch it makes with the hub's front terrain must be at least a lane wide — the
        // spanning dock, so a face reaching across a bay is anchored on both shoulders rather than cantilevered.
        var hub = plan.Boxes.FirstOrDefault(b => b.Kind == PlanBoxKinds.Hub);
        var front = plan.Boxes.FirstOrDefault(b => b.Kind == PlanBoxKinds.Frontline);
        if (hub is not null && front is not null)
        {
            var f = frame.FromRect(front.Rect);
            var patches = FrontPatches(plan, hub, front, frame);
            var weakest = patches.Count == 0 ? 0 : patches.Min();
            if (weakest < UnitTuning.WoolLaneCells)
                findings.Add(new Finding("frontline-shoulder-too-narrow",
                    patches.Count == 0
                        ? $"The frontline's {f.VSpan}-cell face never meets the hub's front terrain, so its " +
                          "spine has nothing to dock through."
                        : $"The frontline's {f.VSpan}-cell face meets the hub's front terrain in " +
                          $"{patches.Count} patch(es) ({string.Join(", ", patches)} cell(s)); the narrowest is " +
                          $"{weakest}, under the {UnitTuning.WoolLaneCells}-cell lane. A face may be " +
                          "narrower than the edge or overhang it, and may reach across a bay — but every " +
                          "shoulder it lands on has to be a corridor's width, or the face is cantilevered " +
                          "over the hole.", Cites: "G2"));
        }

        // the seat-separation law: no spawn/wool seats within the separation gap of another. The gap is the map's
        // lane width (2 or 3), but FrontGuard.Resolve may fall back to the wool-lane 2 as its last tier before a
        // flush residue — so 2 is the true floor the allocator can seat at, and testing against it keeps this
        // from over-reporting on a wide board. NB the measurand is the box ENVELOPE (corner-inclusive), which
        // G124 questions: a donut's void margins can indict a placement whose emitted terrain keeps the gap.
        var seats = plan.Boxes.Where(b => b.Kind is PlanBoxKinds.Wool or PlanBoxKinds.Spawn).ToList();
        for (var i = 0; i < seats.Count; i++)
            for (var j = i + 1; j < seats.Count; j++)
                if (SeatGeometry.TooClose(seats[i].Rect, seats[j].Rect, UnitTuning.WoolLaneCells))
                    findings.Add(new Finding("seats-within-separation-gap",
                        $"Boxes '{seats[i].Id}' and '{seats[j].Id}' sit within the {UnitTuning.WoolLaneCells}-cell " +
                        "separation gap, which the allocator never seats through. Measured on the box " +
                        "envelopes (corner-inclusive) — the emitted terrain may keep more room than the " +
                        "envelopes suggest, which is the measurand question G124 parks.", Cites: "WL7"));

        return findings;
    }

    /// <summary>Read one box: group its members, derive its identity, search the declared parameter space, and
    /// report.</summary>
    public static BoxProducibility Read(PlanModel plan, PlanBox box)
    {
        var members = PlanBoxes.MembersOf(plan, box);
        if (members.Count == 0)
            return new BoxProducibility(box.Id, box.Kind, "empty", null, null,
                Findings.Of(new Finding("box-empty", "The box groups no pieces.")));

        var terrain = Mask(members.Where(p => p.Role == PlanRoles.Piece).Select(p => p.Rect));
        var roomPieces = members.Where(p => p.Role is PlanRoles.WoolRoom or PlanRoles.Spawn).ToList();
        var roomCells = Mask(roomPieces.Select(p => p.Rect));
        var all = new HashSet<(int, int)>(terrain);
        all.UnionWith(roomCells);

        var identity = Identify(all, roomCells);
        var findings = new List<Finding>();

        // The measurement, against the same constant the emitter reads. Independent of the search: a corridor
        // narrower than any lane the vocabulary has is worth saying even when a nearest miss also fires.
        var cwFloor = box.Kind == PlanBoxKinds.Wool ? UnitTuning.WoolLaneCells : FillProfiles.HubWallCells;
        var measured = Cells.MinRunWidthRaw(all, all);
        if (measured < cwFloor)
            findings.Add(new Finding("corridor-below-minimum",
                $"Narrowest cross-section is {measured} cell(s); the emitters build at {cwFloor} " +
                $"({(box.Kind == PlanBoxKinds.Wool ? "the wool lane" : "the hub/body wall")} width). " +
                "Every part of this box would have to be at least that wide.", Cites: "G2"));

        // enumerated lazily and kept as they come: an exact match ends the search, so the producible case — the
        // common one — never pays for the rest of the space. Only a real miss enumerates it all, to report against.
        var candidates = new List<Candidate>();
        foreach (var c in Candidates(box, all))
        {
            candidates.Add(c);
            if (c.Mask is not null && c.Mask.SetEquals(all))    // exact terrain+room match — the box is producible
                return new BoxProducibility(box.Id, box.Kind, identity,
                    new ProducibleAs(c.Label, c.Cw), null, findings);
        }
        if (candidates.Count == 0)
            findings.Add(new Finding("no-candidates",
                $"No production menu covers a '{box.Kind}' box, so there is nothing to compare against."));

        // the terrain/room split: the corridor reproduces but the terminal room does not. Only reachable past
        // the exact-match return above, so every candidate here already differs somewhere.
        if (roomPieces.Count > 0
            && candidates.FirstOrDefault(c => c.Mask is not null && c.Room is not null
                                              && TerrainOnly(c.Mask!, c.Room!.Value).SetEquals(terrain)) is { } roomMiss)
            findings.Add(new Finding("room-not-replicable",
                $"The corridor is reproducible ({roomMiss.Label}) but the terminal room is not: the emitters " +
                $"build a compact {ShapeEmitter.RoomDepthCells}-cell-deep room and this one differs. The room " +
                "is not just terrain — the export stamps its bedrock floor and entrance line from it.",
                Cites: "ST1"));

        var nearest = Nearest(candidates, all);
        if (nearest is not null)
        {
            findings.Add(new Finding("no-parameters-reproduce",
                $"No parameter tuple on the production menus reproduces this box. Closest is {nearest.Label} " +
                $"at cw {nearest.Cw}, differing in {nearest.DifferingCells} cell(s)."));
            if (ProportionGap(box.Kind, identity, nearest) is { } gap) findings.Add(gap);
        }
        else if (candidates.Count > 0)
            findings.AddRange(Rejections(candidates, box.Rect));

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
        string Label, int Cw, HashSet<(int, int)>? Mask, CellRect? Room, FillRejection? Rejection);

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
        var b = new Box(box.Id, BoxKind.Hub, box.Rect, box.Rect.Width * box.Rect.Height);
        var cw = FillProfiles.HubWallCells;
        foreach (var form in FillProfiles.HubForms)
            foreach (var walls in HubWallVectors(form, box, cw))
                foreach (var arms in HubArmLayouts(form, box, cw))
                    foreach (var flip in new[] { false, true })
                    {
                        var label = $"{Name(form)}{Widened(walls, cw)}{(arms is null ? "" : " legs " + Legs(arms))}{(flip ? " flipped" : "")}";
                        var hub = HubBoxEmitter.Fill(b, form, cw, flip, out var why, walls, arms);
                        yield return hub is null
                            ? new Candidate(label, cw, null, null, why)
                            : new Candidate(label, cw, Mask(hub.Pieces.Select(p => p.Rect)), null, null);
                    }
    }

    /// <summary>The leg layouts a branch hub can take in this box — collected by <b>running</b>
    /// <see cref="HubBoxEmitter.SampleArms"/> over many seeds, the same way the ring walls and the frontline's
    /// arms are, so the widths the search admits are exactly the widths the composer draws. The uniform default
    /// (<c>null</c>) leads, and a form without arms yields only that.</summary>
    private static IEnumerable<IReadOnlyList<(int Start, int Width)>?> HubArmLayouts(
        CompoundRead form, PlanBox box, int cw)
    {
        yield return null;
        if (form.Form != Compound.SpineArms) yield break;
        var spineLen = box.Rect.Width;
        foreach (var layout in Memo($"hub-legs {spineLen} {form.Arms} {cw}",
                     () => SweepLayouts(seed => HubBoxEmitter.SampleArms(seed, spineLen, form.Arms, cw))))
            yield return layout;
    }

    /// <summary>The distinct leg layouts a leg sampler yields over <see cref="SamplerSweepSeeds"/> draws, in the
    /// order it first produced them. The sizes it refuses (a <c>null</c> draw) are dropped.</summary>
    private static IReadOnlyList<IReadOnlyList<(int Start, int Width)>> SweepLayouts(
        Func<ComposeRng, IReadOnlyList<(int Start, int Width)>?> draw)
    {
        var seen = new HashSet<string>();
        var layouts = new List<IReadOnlyList<(int Start, int Width)>>();
        for (ulong seed = 1; seed <= SamplerSweepSeeds; seed++)
            if (draw(new ComposeRng(seed)) is { } layout && seen.Add(Legs(layout))) layouts.Add(layout);
        return layouts;
    }


    /// <summary>The ring-wall vectors a hub form can take in this box — collected by <b>running</b>
    /// <see cref="TeamUnitAllocator.ChooseHubWalls"/> over many seeds rather than restating its law (one side
    /// widened, within twice the narrowest, only where the hole has the slack). The even-walled ring
    /// (<c>null</c>) is always offered first, and a form without a ring yields only that.</summary>
    private static IEnumerable<RingWalls?> HubWallVectors(CompoundRead form, PlanBox box, int cw)
    {
        yield return null;
        int w = box.Rect.Width, h = box.Rect.Height;
        foreach (var walls in Memo($"hub-walls {form.Form} {form.Arms} {w} {h} {cw}", () =>
                 {
                     var seen = new HashSet<RingWalls>();
                     var vectors = new List<RingWalls>();
                     for (ulong seed = 1; seed <= SamplerSweepSeeds; seed++)
                         if (TeamUnitAllocator.ChooseHubWalls(form, w, h, cw, new ComposeRng(seed)) is { } v
                             && seen.Add(v)) vectors.Add(v);
                     return vectors;
                 }))
            yield return walls;
    }

    private static string Widened(RingWalls? walls, int cw) => walls is not { } v ? ""
        : $" walls {v.Top}/{v.Right}/{v.Bottom}/{v.Left}";

    private static IEnumerable<Candidate> FrontlineCandidates(PlanBox box)
    {
        var b = new Box(box.Id, BoxKind.Frontline, box.Rect, box.Rect.Width * box.Rect.Height);
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
        var spineLen = mouth is BoxEdge.Left or BoxEdge.Right ? box.Rect.Height : box.Rect.Width;
        foreach (var layout in Memo($"front-legs {spineLen} {form.Arms}",
                     () => SweepLayouts(seed => FrontlineBoxEmitter.SampleArms(seed, spineLen, form.Arms))))
            yield return layout;
    }

    private static IEnumerable<Candidate> ApproachCandidates(PlanBox box, BoxKind kind)
    {
        var b = new Box(box.Id, kind, box.Rect, box.Rect.Width * box.Rect.Height);
        // the wool lane is fixed at w2; a spawn reads the map's lane width, which a standalone box does not know
        var widths = kind == BoxKind.Wool ? new[] { UnitTuning.WoolLaneCells } : LaneWidths;
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
                for (var aw = cw; aw <= UnitTuning.DonutEntryMaxCells; aw++)
                    yield return (placement, atEnd, aw);
            }
    }

    /// <summary>
    /// The gap finding for a box whose <b>shape is in the vocabulary but whose proportions are not</b>: the
    /// nearest candidate is the same form the box reads as, so the emitters know this shape and only its
    /// dimensions are out of reach. Every emitter takes a single corridor width, so that is very often what one
    /// over-wide part runs into — and there are tasks that own it, which is more use to an author than a bare
    /// "no parameters reproduce".
    ///
    /// <para>A nearest miss of a <em>different</em> form means the shape itself is unreachable, and no single
    /// task owns that — so this returns <c>null</c> rather than guessing. The mapping below is documentation as
    /// data: it keys only off facts the search already produced (the box kind, the derived identity, the nearest
    /// form) and re-derives no geometry.</para>
    /// </summary>
    private static Finding? ProportionGap(string kind, string identity, NearestMiss nearest)
    {
        if (FormToken(identity) is not { Length: > 0 } read || read != FormToken(nearest.Label)) return null;
        var (cites, owner) = kind switch
        {
            PlanBoxKinds.Hub or PlanBoxKinds.Frontline =>
                ("G105", "per-piece body widths and the asymmetric ring"),
            _ => ("G82", "approach entry widening"),
        };
        return new Finding("proportions-outside-the-parameter-space",
            $"The shape is one the emitters build — the closest candidate is a {read} too — so only its " +
            $"proportions are out of reach. Every emitter takes a single corridor width, so a part wider or " +
            $"narrower than the rest cannot be asked for: {owner} is the gap ({cites}), and corridor width as a " +
            "per-part property rather than one board-wide constant is G129.", Cites: cites);
    }

    /// <summary>The leading form name of an identity or candidate label (<c>"Ring"</c>, <c>"SpineArms"</c>,
    /// <c>"Donut"</c>) — the token the two share when they name the same shape.</summary>
    private static string FormToken(string s)
    {
        var cut = s.IndexOfAny([' ', '(']);
        return cut < 0 ? s : s[..cut];
    }

    /// <summary>The findings for a box <b>no</b> form even emitted into — every candidate was refused, so there is
    /// no geometry to diff. A too-small footprint is the common case and collapses to <b>one</b> finding naming
    /// the smallest box any form on the menu fits: four near-identical "below the minimum" lines (one per mouth
    /// orientation) tell the author nothing the smallest one doesn't. Other refusal kinds report distinct
    /// reasons.</summary>
    private static IEnumerable<Finding> Rejections(IReadOnlyList<Candidate> candidates, CellRect rect)
    {
        var tooSmall = candidates
            .Select(c => c.Rejection).OfType<FillRejection.TooSmall>()
            .OrderBy(t => t.MinW * t.MinH).ThenBy(t => t.MinW).FirstOrDefault();
        if (tooSmall is not null)
            yield return new Finding("box-too-small",
                $"This box is {rect.Width}x{rect.Height} cells; the smallest footprint any form on the menu fits is " +
                $"{tooSmall.MinW}x{tooSmall.MinH}. Nothing can be emitted into it.");

        foreach (var detail in candidates
                     .Select(c => c.Rejection).Where(r => r is not null and not FillRejection.TooSmall)
                     .Select(r => Describe(r!)).Distinct().Take(3))
            yield return new Finding("every-form-refused", detail);
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
                extra.Select(p => new CellRect(p.Item1, p.Item2, 1, 1)).ToList(),
                missing.Select(p => new CellRect(p.Item1, p.Item2, 1, 1)).ToList());
        }
        return best;
    }

    private static readonly BoxEdge[] AllEdges = [BoxEdge.Top, BoxEdge.Bottom, BoxEdge.Left, BoxEdge.Right];

    private static HashSet<(int, int)> TerrainOnly(IReadOnlySet<(int, int)> mask, CellRect room)
    {
        var roomCells = Mask([room]);
        return mask.Where(c => !roomCells.Contains(c)).ToHashSet();
    }

    private static HashSet<(int, int)> Mask(IEnumerable<CellRect> rects)
    {
        var cells = new HashSet<(int, int)>();
        foreach (var r in rects)
            for (var x = r.X; x < r.X + r.Width; x++)
                for (var z = r.Z; z < r.Z + r.Height; z++) cells.Add((x, z));
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
