using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Compose;

/// <summary>Whether a negative space may be offered onward for later fills.</summary>
public enum PublishVerdict { Veto, Allow }

/// <summary>
/// The publish policy (docs/map-generation-constraint-taxonomy.md §4.1): which of a shape's negative spaces are
/// offered onward, as ordered verdicts over the edge-taxonomy facts — <b>space veto → guard → part allow →
/// default deny</b>. Publishing is an <b>offer, never a fill</b>: a published vacancy enters the pipeline for a
/// later step to claim once the base is built (a third wool seated in a free-standing U's bay or a ring's
/// hole) — it may legitimately stay empty.
///
/// <para><b>Terminal-capped shapes</b> (an approach — the classification carries a terminal): <b>holes and bays
/// veto</b> — the bay walled by the terminal's own path (the scythe's <c>room-run</c>, the clamp's <c>room</c>)
/// grants a second approach (the WL8 motif), and the U/H entry-walled bay is vetoed alike; an enclosed hole is
/// the shape's own device. <b>Notches allow</b> — including the notch walled by a <c>room-run</c> (the Z's
/// second notch): the clearance margin, not a veto, is what keeps pieces off the room there. <b>Terminal-free
/// bodies allow everything</b> — a bare U's bay, a Z's notches, a ring's hole (the hole's size condition is a
/// pending gate). The publishable region of an allowed space is its <b>front, unguarded parts</b> (the
/// mouth-touching covering layer); a hole, having no mouth, offers all its parts. <c>Open</c>-kind spaces are
/// plain outside — nothing to publish.</para>
/// </summary>
public static class PublishPolicy
{
    /// <summary>The space-level verdict: terminal-capped shapes veto their bays and holes; everything else is
    /// allowed through to the part filter.</summary>
    public static PublishVerdict Space(NegativeSpace space, bool terminalCapped) =>
        terminalCapped && space.Kind is NegativeSpaceKind.Hole or NegativeSpaceKind.Bay
            ? PublishVerdict.Veto
            : PublishVerdict.Allow;

    /// <summary>The publishable region of <paramref name="space"/>: empty when vetoed (or plain outside), else
    /// its front, unguarded parts — a hole (no mouth, no front) offers all its unguarded parts.</summary>
    public static IReadOnlyList<NegativeSpacePart> PublishableParts(NegativeSpace space, bool terminalCapped)
    {
        if (space.Kind == NegativeSpaceKind.Open) return [];
        if (Space(space, terminalCapped) == PublishVerdict.Veto) return [];
        return space.Parts
            .Where(p => !p.Guarded && (p.Front || space.Kind == NegativeSpaceKind.Hole))
            .ToList();
    }

    /// <summary>Every space of <paramref name="read"/> that offers anything, with its publishable parts — the
    /// terminal-capped context inferred from the edges (any terminal-owned run).</summary>
    public static IReadOnlyList<(NegativeSpace Space, IReadOnlyList<NegativeSpacePart> Parts)> Publishable(
        EdgeClassification read)
    {
        var capped = read.Edges.Any(e => e.Terminal);
        return read.Spaces
            .Select(s => (Space: s, Parts: PublishableParts(s, capped)))
            .Where(t => t.Parts.Count > 0)
            .ToList();
    }
}
