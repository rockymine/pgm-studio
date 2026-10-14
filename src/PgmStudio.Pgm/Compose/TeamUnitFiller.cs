using PgmStudio.Pgm.Shapes;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Compose;

/// <summary>
/// Fills an allocated team <see cref="BoxLayout"/> <b>hub-first</b> — the fill half of the allocate-then-fill
/// pipeline. The hub emits first as the <b>constraint source</b> (<see cref="HubBoxEmitter"/>), and
/// each neighbour box <b>consumes the hub's <see cref="EdgeOffer"/></b> on the edge it docks: the offered
/// <see cref="EdgeOffer.Width"/> <em>is</em> the neighbour's lane width. The geometry is filled into
/// footprints the allocator positions, never grown outward.
///
/// <para>Every box is filled with a <see cref="Compound"/> and laid onto the board with
/// <see cref="UnitPieces.Lay(Compound, Box)"/>, so each piece carries the box it fills.</para>
/// </summary>
public static class TeamUnitFiller
{
    /// <summary>The lane width the neighbour on <paramref name="joint"/> builds to, docking the hub's
    /// <paramref name="hubEdge"/> — the constraint-source contract: the hub sources the width, the neighbour
    /// builds to it. The grant is <b>per joint</b>, never per edge and not even per run: a third wool doubling
    /// onto the spawn's side shares the spawn's edge — on a solid hub, the very same run — at a different width,
    /// so an edge- or run-keyed lookup would hand one of the two the other's <c>laneWidth</c>. A joint carrying no offer
    /// (a partition derived rather than allocated) falls back to what the run this dock lands on can support.
    /// Throws <see cref="ComposeException"/> when the hub published no offer at all on that edge (the allocator
    /// only docks a neighbour where the hub offered).</summary>
    public static int GrantedLaneWidth(Box hubBox, Compound hub, BoxJoint joint, BoxEdge hubEdge)
    {
        if (joint.Width is { } granted) return granted;

        var onEdge = HubBoxEmitter.Offers(hubBox, hub).Where(o => o.Edge == hubEdge).ToList();
        if (onEdge.Count == 0)
            throw new ComposeException($"the hub publishes no offer on its {hubEdge} edge to consume.");
        var run = onEdge.FirstOrDefault(o => o.Interval.Overlaps(joint.Shared));
        return (run ?? onEdge[0]).Width;
    }

    /// <summary>
    /// Fill an allocated <paramref name="layout"/> into a team unit, hub-first (the allocate↔fill contract).
    /// The <b>allocator (C.2) provides</b>: the positioned boxes (plan-cell <see cref="Box.Rect"/>s — one hub,
    /// one spawn, 1–3 wools, 0–1 frontline), the joints between them with the <b>lane width each consumer
    /// was granted, carried per joint as their <see cref="BoxJoint.Width"/>s</b>. This <b>filler</b> emits the hub
    /// first at the form the allocator chose (<see cref="Box.Form"/>), then for each hub joint fills the
    /// neighbour box — the spawn/wool consuming <b>its own joint's</b> granted width as its <c>laneWidth</c>, docking the
    /// edge facing the hub — and lays every compound as the unit's pieces, with the spawn and wool markers.
    /// <c>null</c> on any directed fill failure (a form or family that does not fit), which the composer
    /// resamples.
    /// </summary>
    public static TeamUnit? Fill(BoxLayout layout, ComposeRng rng)
    {
        var hubBox = layout.Boxes.FirstOrDefault(b => b.Kind == BoxKind.Hub);
        if (hubBox is null) return null;
        var hubJoints = layout.JointsOf(hubBox.Id);

        // the hub emits first, at the form the allocator chose (Box.Form; the solid rectangle when unset) — the
        // same compound the allocator seated its neighbours against, so every dock lands on real terrain (§4's
        // offerable surface), never an empty bounding-box stretch. It publishes each run at what that run can
        // support; what each neighbour was granted rides on its own joint.
        var form = hubBox.Form ?? CompoundForm.I;
        if (HubBoxEmitter.Fill(hubBox, form, hubBox.HubLaneWidth, hubBox.FlipV, hubBox.HubWalls,
                hubBox.HubArms) is not { } hub)
            return null;

        var pieces = UnitPieces.Lay(hub, hubBox).Pieces;
        Marker? spawn = null;
        string? spawnFacing = null;
        var wools = new List<Marker>();

        foreach (var j in hubJoints)
        {
            var neighbour = layout.ById(Other(j, hubBox.Id));
            if (neighbour is null) continue;
            var hubEdge = HubEdge(j, hubBox.Id);
            var mouth = Opposite(hubEdge);

            switch (neighbour.Kind)
            {
                case BoxKind.Spawn:
                    var laneWidth = GrantedLaneWidth(hubBox, hub, j, hubEdge);
                    var fitS = FillMenu.For(BoxKind.Spawn)
                        .Where(f => SpawnBoxEmitter.Fill(neighbour, mouth, f, laneWidth, flip: false) is not null).ToList();
                    if (fitS.Count == 0) return null;
                    var spawnFill = SpawnBoxEmitter.Fill(neighbour, mouth, fitS[rng.NextInt(0, fitS.Count)], laneWidth, flip: false)!;
                    (var spawnPieces, spawn) = UnitPieces.Lay(spawnFill, neighbour);
                    pieces.AddRange(spawnPieces);
                    spawnFacing = TowardHub(hubEdge);
                    break;

                case BoxKind.Wool:
                    // fill the shape the allocator chose and seated (Box.Wool): the compact inline / side-tuck I, or
                    // a richer family docked by the seat-and-shift. The allocator positioned the box for this exact
                    // family's entry, so the filler re-emits it, not a re-picked family.
                    laneWidth = GrantedLaneWidth(hubBox, hub, j, hubEdge);
                    var wf = neighbour.Wool ?? new WoolFill(CompoundForm.I, RoomPlacement.Inline, false);
                    if (BoxFiller.Fill(neighbour, mouth, laneWidth, wf.Family, out _, wf.Flip, wf.Placement,
                            wf.WoolAtEnd, wf.AttachmentWidth, wf.RingWalls) is not { } woolFill)
                        return null;
                    var (woolPieces, woolMarker) = UnitPieces.Lay(woolFill, neighbour);
                    pieces.AddRange(woolPieces);
                    wools.Add(woolMarker!);
                    break;

                case BoxKind.Frontline:
                    // a join, not a placement: the frontline's spine docks the hub with the mouth facing it (at
                    // the hub's front-edge offer width), and its face — opposite the spine, toward the axis —
                    // is what the mid docks. No room, no marker.
                    laneWidth = GrantedLaneWidth(hubBox, hub, j, hubEdge);
                    var fitF = FillMenu.For(BoxKind.Frontline)
                        .Where(f => FrontlineBoxEmitter.Fill(neighbour, f, laneWidth, mouth) is not null).ToList();
                    if (fitF.Count == 0) return null;
                    // the frontline form answers the hub form: a branch hub takes the wide I across its front
                    // (it overlaps the short leg), but a square or holed hub prefers an L or U — a solid I
                    // flush against an already-square hub reads flat (a square on a square).
                    var hubForm = hubBox.Form ?? CompoundForm.I;
                    var strands = fitF.Where(f => f.Arms() > 0).ToList();
                    var frontForm = hubForm.Arms() > 0 && fitF.Contains(CompoundForm.I) ? CompoundForm.I
                        : strands.Count > 0 ? strands[rng.NextInt(0, strands.Count)]
                        : fitF[rng.NextInt(0, fitF.Count)];
                    _ = rng.NextBool(0.5);   // the face offer's grouping, drawn and unread
                    // the branch forms take a sampled leg layout (SampleArms — varied leg widths/placements
                    // under the leg laws: ≥2 wide, factor-2 pairs, 2–4 bays, capped end recesses) with the
                    // canonical fat L / symmetric twin as the fallback when the spine cannot host one
                    var spineLenF = mouth is BoxEdge.Left or BoxEdge.Right ? neighbour.Rect.Height : neighbour.Rect.Width;
                    var layoutF = frontForm.Arms() > 0
                        ? FrontlineBoxEmitter.SampleArms(rng, spineLenF, frontForm.Arms()) : null;
                    var frontFill = (layoutF is null ? null
                            : FrontlineBoxEmitter.Fill(neighbour, frontForm, laneWidth, mouth, layoutF))
                        ?? FrontlineBoxEmitter.Fill(neighbour, frontForm, laneWidth, mouth)!;
                    pieces.AddRange(UnitPieces.Lay(frontFill, neighbour).Pieces);
                    break;
            }
        }

        if (spawn is null || spawnFacing is null) return null;   // a team unit is anchored by its spawn
        return new TeamUnit(pieces, spawn, spawnFacing, wools);
    }

    /// <summary>The box edge opposite <paramref name="e"/> — a neighbour docks the hub with the edge facing it.</summary>
    private static BoxEdge Opposite(BoxEdge e) => e switch
    {
        BoxEdge.Top => BoxEdge.Bottom,
        BoxEdge.Bottom => BoxEdge.Top,
        BoxEdge.Left => BoxEdge.Right,
        _ => BoxEdge.Left,
    };

    /// <summary>The hub's edge a joint touches — the interface edge when the hub is
    /// <see cref="BoxJoint.BoxA"/>, else its opposite (the interface is read on <c>BoxA</c>'s frame).</summary>
    /// <summary>The board direction a spawn docked on the hub's <paramref name="hubEdge"/> faces: into the hub.
    /// A spawn behind the hub faces the axis through it, and one beside it faces across it — never out over the
    /// void past the hub's side (<c>SP9</c>).</summary>
    internal static string TowardHub(BoxEdge hubEdge) => hubEdge switch
    {
        BoxEdge.Left => SpawnFacings.Right,
        BoxEdge.Right => SpawnFacings.Left,
        BoxEdge.Top => SpawnFacings.Back,
        _ => SpawnFacings.Front,
    };

    private static BoxEdge HubEdge(BoxJoint j, string hubId) =>
        j.BoxA == hubId ? j.Shared.Edge : Opposite(j.Shared.Edge);

    private static string Other(BoxJoint j, string id) => j.BoxA == id ? j.BoxB : j.BoxA;
}
