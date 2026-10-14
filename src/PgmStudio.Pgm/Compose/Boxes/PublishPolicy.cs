using PgmStudio.Pgm.Shapes;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Compose;

/// <summary>Whether a negative space may be offered onward for later fills.</summary>
public enum PublishVerdict { Veto, Allow }

/// <summary>
/// The publish policy (docs/generator/audit.md §4.1): which of a shape's negative spaces are
/// offered onward, as ordered verdicts over the edge-taxonomy facts — <b>space veto → guard → part allow →
/// default deny</b>. Publishing is an <b>offer, never a fill</b>: a published vacancy enters the pipeline for a
/// later step to claim once the base is built (a third wool seated in a free-standing U's bay or a ring's
/// hole) — it may legitimately stay empty.
///
/// <para><b>Room-capped shapes</b> (an approach — the classification carries a room): <b>holes and bays
/// veto</b> — the bay walled by the room's own path (the scythe's <c>room-run</c>, the clamp's <c>room</c>)
/// grants a second approach (the WL8 motif), and the U/H entry-walled bay is vetoed alike; an enclosed hole is
/// the shape's own device. <b>Notches allow</b> — including the notch walled by a <c>room-run</c> (the Z's
/// second notch): the clearance margin, not a veto, is what keeps pieces off the room there. <b>Room-free
/// compounds allow everything</b> — a bare U's bay, a Z's notches, a ring's hole (whose size the composer floors
/// at <see cref="UnitTuning.HubHoleCells"/>). The publishable region of an allowed space is its <b>front, unguarded parts</b> (the
/// mouth-touching covering layer); a hole, having no mouth, offers all its parts. <c>Open</c>-kind spaces are
/// plain outside — nothing to publish.</para>
/// </summary>
public static class PublishPolicy
{
    /// <summary>The space-level verdict: room-capped shapes veto their bays and holes; everything else is
    /// allowed through to the part filter.</summary>
    public static PublishVerdict Space(NegativeSpace space, bool roomCapped) =>
        roomCapped && space.Kind is NegativeSpaceKinds.Hole or NegativeSpaceKinds.Bay
            ? PublishVerdict.Veto
            : PublishVerdict.Allow;

    /// <summary>The publishable region of <paramref name="space"/>: empty when vetoed (or plain outside), else
    /// its front, unguarded parts — a hole (no mouth, no front) offers all its unguarded parts.</summary>
    public static IReadOnlyList<NegativeSpacePart> PublishableParts(NegativeSpace space, bool roomCapped)
    {
        if (space.Kind == NegativeSpaceKinds.Open) return [];
        if (Space(space, roomCapped) == PublishVerdict.Veto) return [];
        return space.Parts
            .Where(p => !p.Guarded && (p.Front || space.Kind == NegativeSpaceKinds.Hole))
            .ToList();
    }

    /// <summary>Every space of <paramref name="read"/> that offers anything, with its publishable parts — the
    /// room-capped context inferred from the edges (any room-owned run).</summary>
    public static IReadOnlyList<(NegativeSpace Space, IReadOnlyList<NegativeSpacePart> Parts)> Publishable(
        EdgeClassification read)
    {
        var capped = read.Edges.Any(e => e.OnRoom);
        return read.Spaces
            .Select(s => (Space: s, Parts: PublishableParts(s, capped)))
            .Where(t => t.Parts.Count > 0)
            .ToList();
    }
}
