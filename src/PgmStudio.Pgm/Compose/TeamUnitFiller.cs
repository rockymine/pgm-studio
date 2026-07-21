using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Compose;

/// <summary>What filling an allocated partition produces: the <see cref="GrownUnit"/> the composer
/// assembles (pieces + spawn/wool placements) plus the frontline's <see cref="FrontlineFace"/> offers the mid
/// consumes (<c>mid = f(frontline)</c>), carried out for <see cref="MidCarver"/>.</summary>
public sealed record FilledUnit(GrownUnit Unit, IReadOnlyList<EdgeOffer> FrontlineFace);

/// <summary>
/// Fills an allocated team-unit partition <b>hub-first</b> — the fill half of the allocate-then-fill
/// pipeline. The hub emits first as the <b>constraint source</b> (<see cref="HubBoxEmitter"/>), and
/// each neighbour box <b>consumes the hub's <see cref="EdgeOffer"/></b> on the edge it docks: the offered
/// <see cref="EdgeOffer.WidthClass"/> <em>is</em> the neighbour's corridor width. The geometry is filled into
/// footprints the allocator positions, never grown outward.
///
/// <para>This first slice is the <b>offer-consumption seam</b>: the offer on a neighbour's own joint driving its
/// width. The full partition-topology orchestration — which neighbour docks which edge, assembling the
/// <see cref="GrownUnit"/> + placements, and the frontline's face dock — builds on it.</para>
/// </summary>
public static class TeamUnitFiller
{
    /// <summary>The corridor width the neighbour on <paramref name="joint"/> builds to, docking the hub's
    /// <paramref name="hubEdge"/> — the constraint-source contract: the hub sources the width, the neighbour
    /// builds to it. The grant is <b>per joint</b>, never per edge and not even per run: a third wool doubling
    /// onto the spawn's side shares the spawn's edge — on a solid hub, the very same run — at a different width,
    /// so an edge- or run-keyed lookup would hand one of the two the other's <c>cw</c>. A joint carrying no offer
    /// (a partition derived rather than allocated) falls back to what the run this dock lands on can support.
    /// Throws <see cref="ComposeException"/> when the hub published no offer at all on that edge (the allocator
    /// only docks a neighbour where the hub offered).</summary>
    public static int ConsumedCw(EmittedHub hub, BoxJoint joint, BoxEdge hubEdge)
    {
        if (joint.Offer is { } granted) return granted.WidthClass;

        var onEdge = hub.Offers.Where(o => o.Edge == hubEdge).ToList();
        if (onEdge.Count == 0)
            throw new ComposeException($"the hub publishes no offer on its {hubEdge} edge to consume.");
        int lo = joint.Interface.Start, hi = lo + joint.Interface.WidthCells;
        var run = onEdge.FirstOrDefault(o => o.Interval.Start < hi && lo < o.Interval.Start + o.Interval.LengthCells);
        return (run ?? onEdge[0]).WidthClass;
    }

    /// <summary>Fill a <b>spawn</b> box docking the hub's <paramref name="hubEdge"/> over
    /// <paramref name="joint"/>, at the width that joint was granted (the spawn's mouth is
    /// <paramref name="spawnMouth"/>, the edge facing the hub). <c>null</c> when the footprint is too small — a
    /// directed signal the allocator resizes against.</summary>
    public static EmittedSpawn? FillSpawn(
        EmittedHub hub, BoxJoint joint, BoxEdge hubEdge, Box spawnBox, BoxEdge spawnMouth, ShapeFamily family,
        bool flip, string roomId) =>
        SpawnBoxEmitter.Fill(spawnBox, spawnMouth, family, ConsumedCw(hub, joint, hubEdge), flip, roomId);

    /// <summary>Fill a <b>wool</b> box docking the hub's <paramref name="hubEdge"/> over
    /// <paramref name="joint"/>, at the width that joint was granted — routed through the profile-gated
    /// <see cref="BoxFiller"/> so the dock is gated legal and the family is checked against the wool width menu
    /// (the granted width chooses the menu row).</summary>
    public static FillResult FillWool(
        EmittedHub hub, BoxJoint joint, BoxEdge hubEdge, Box woolBox, BoxEdge woolMouth, ShapeFamily family,
        bool flip, string roomId) =>
        BoxFiller.Fill(woolBox, woolMouth, ConsumedCw(hub, joint, hubEdge), family, flip, roomId);

    /// <summary>
    /// Fill an allocated <paramref name="partition"/> into a team unit, hub-first (the allocate↔fill contract).
    /// The <b>allocator (C.2) provides</b>: the positioned boxes (plan-cell <see cref="Box.Rect"/>s — one hub,
    /// one spawn, 1–3 wools, 0–1 frontline), the joints between them with the hub's <b>width plan carried
    /// per joint as their <see cref="BoxJoint.Offer"/>s</b>, and the spawn's <paramref name="spawnFacing"/> (the one
    /// board-frame value — every other output is plan-cell or piece-relative). This <b>filler</b> emits the hub
    /// first at the form the allocator chose (<see cref="Box.Form"/>), then for each hub joint fills the
    /// neighbour box — the spawn/wool consuming <b>its own joint's</b> granted width as its <c>cw</c>, docking the
    /// edge facing the hub — and assembles the pieces + placements.
    /// <c>null</c> on any directed fill failure (a form or family that does not fit), which the composer
    /// resamples. The frontline (a join, not a placement — its face offer feeds the mid) joins next.
    /// </summary>
    public static FilledUnit? Fill(BoxPartition partition, string spawnFacing, ComposeRng rng)
    {
        var hubBox = partition.Boxes.FirstOrDefault(b => b.Kind == BoxKind.Hub);
        if (hubBox is null) return null;
        var hubJoints = partition.JointsOf(hubBox.Id);

        // the hub emits first, at the form the allocator chose (Box.Form; the solid rectangle when unset) — the
        // same body the allocator seated its neighbours against, so every dock lands on real terrain (§1.13's
        // offerable surface), never an empty bounding-box stretch. It publishes each run at what that run can
        // support; what each neighbour was granted rides on its own joint.
        var form = hubBox.Form ?? new CompoundRead(Compound.Rectangle);
        if (HubBoxEmitter.Fill(hubBox, form, FillProfiles.HubWallCells, hubBox.FlipV) is not { } hub)
            return null;

        var pieces = new List<GrownPiece>(hub.Pieces);
        GrownSpawn? spawn = null;
        var wools = new List<GrownWool>();
        var faceOffers = new List<EdgeOffer>();

        foreach (var j in hubJoints)
        {
            var neighbour = partition.ById(Other(j, hubBox.Id));
            if (neighbour is null) continue;
            var hubEdge = HubEdge(j, hubBox.Id);
            var mouth = Opposite(hubEdge);
            var roomId = $"{neighbour.Id}-room";

            switch (neighbour.Kind)
            {
                case BoxKind.Spawn:
                    var fitS = SpawnBoxEmitter.Families
                        .Where(f => FillSpawn(hub, j, hubEdge, neighbour, mouth, f, flip: false, roomId) is not null).ToList();
                    if (fitS.Count == 0) return null;
                    var es = FillSpawn(hub, j, hubEdge, neighbour, mouth, fitS[rng.NextInt(0, fitS.Count)], flip: false, roomId)!;
                    pieces.AddRange(es.Pieces);
                    spawn = new GrownSpawn(roomId, es.MarkerAt, spawnFacing);
                    break;

                case BoxKind.Wool:
                    // fill the shape the allocator chose and seated (Box.Wool): the compact inline / side-tuck I, or
                    // a richer family docked by the seat-and-shift. The allocator positioned the box for this exact
                    // family's entry, so the filler re-emits it, not a re-picked family.
                    var cwW = ConsumedCw(hub, j, hubEdge);
                    var wf = neighbour.Wool ?? new WoolFill(ShapeFamily.I, RoomPlacement.Inline, false);
                    if (BoxFiller.Fill(neighbour, mouth, cwW, wf.Family, wf.Flip, roomId, wf.Placement,
                            wf.WoolAtEnd, wf.AttachmentWidth) is not FillResult.Ok okW)
                        return null;
                    pieces.AddRange(okW.Approach.Terrain);
                    pieces.Add(okW.Approach.WoolRoom);
                    wools.Add(new GrownWool(roomId, okW.Approach.At));
                    break;

                case BoxKind.Frontline:
                    // a join, not a placement: the frontline's spine docks the hub with the mouth facing it (at
                    // the hub's front-edge offer width), and its face — opposite the spine, toward the axis —
                    // offers to the mid (mid = f(frontline)). No room, no marker.
                    var cwF = ConsumedCw(hub, j, hubEdge);
                    var fitF = FillProfiles.FrontlineForms
                        .Where(f => FrontlineBoxEmitter.Fill(neighbour, f, cwF, OfferGrouping.Several, mouth) is not null).ToList();
                    if (fitF.Count == 0) return null;
                    // the frontline form answers the hub form: a branch hub takes the wide Bar across its front
                    // (it overlaps the short leg), but a square or holed hub prefers a staple/strand — a solid Bar
                    // flush against an already-square hub reads flat (a square on a square).
                    var hubForm = hubBox.Form?.Form ?? Compound.Rectangle;
                    var bar = fitF.FirstOrDefault(f => f.Form == Compound.Rectangle);
                    var strands = fitF.Where(f => f.Form == Compound.SpineArms).ToList();
                    var frontForm = hubForm == Compound.SpineArms && bar is not null ? bar
                        : strands.Count > 0 ? strands[rng.NextInt(0, strands.Count)]
                        : fitF[rng.NextInt(0, fitF.Count)];
                    var grouping = rng.NextBool(0.5) ? OfferGrouping.Joint : OfferGrouping.Several;
                    // the branch forms take a sampled leg layout (SampleArms — varied leg widths/placements
                    // under the leg laws: ≥2 wide, factor-2 pairs, 2–4 bays, capped end recesses) with the
                    // canonical fat L / symmetric twin as the fallback when the spine cannot host one
                    var spineLenF = mouth is BoxEdge.Left or BoxEdge.Right ? neighbour.Rect[3] : neighbour.Rect[2];
                    var layoutF = frontForm.Form == Compound.SpineArms
                        ? FrontlineBoxEmitter.SampleArms(rng, spineLenF, frontForm.Arms) : null;
                    var ef = (layoutF is null ? null
                            : FrontlineBoxEmitter.Fill(neighbour, frontForm, cwF, grouping, mouth, armLayout: layoutF))
                        ?? FrontlineBoxEmitter.Fill(neighbour, frontForm, cwF, grouping, mouth)!;
                    pieces.AddRange(ef.Pieces);
                    faceOffers.AddRange(ef.FaceOffers);
                    break;
            }
        }

        if (spawn is null) return null;   // a team unit is anchored by its spawn
        return new FilledUnit(new GrownUnit(pieces, spawn, wools), faceOffers);
    }

    /// <summary>The box edge opposite <paramref name="e"/> — a neighbour docks the hub with the edge facing it.</summary>
    private static BoxEdge Opposite(BoxEdge e) => e switch
    {
        BoxEdge.Top => BoxEdge.Bottom,
        BoxEdge.Bottom => BoxEdge.Top,
        BoxEdge.Left => BoxEdge.Right,
        _ => BoxEdge.Left,
    };

    /// <summary>The hub's edge a <paramref name="j"/>oint touches — the interface edge when the hub is
    /// <see cref="BoxJoint.BoxA"/>, else its opposite (the interface is read on <c>BoxA</c>'s frame).</summary>
    private static BoxEdge HubEdge(BoxJoint j, string hubId) =>
        j.BoxA == hubId ? j.Interface.Edge : Opposite(j.Interface.Edge);

    private static string Other(BoxJoint j, string id) => j.BoxA == id ? j.BoxB : j.BoxA;
}
