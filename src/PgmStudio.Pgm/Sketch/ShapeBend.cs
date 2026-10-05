using PgmStudio.Domain;
using PgmStudio.Geom;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Sketch;

/// <summary>What a bend is asked for: how far a point may be pulled off its edge, which way, how often to cut
/// along an edge, and which coast.</summary>
/// <param name="Wander">How far, in blocks, an inserted point may be pulled off its edge. The whole of what
/// makes an outline organic, and the one number a bend is refused over.</param>
/// <param name="Step">How often to cut along an edge, in blocks. An edge with room for fewer than two cuts
/// is left straight, so a neck and a short face come out as the plan drew them.</param>
/// <param name="Seed">Which coast. The same seed draws the same one, so a spec re-driven is re-driven.</param>
/// <param name="Tension">How long the Bézier handles are, as a fraction of their own edge. Absent is 0.22.</param>
/// <param name="Side">Which way the cut points move — <c>out</c> of the ring, <c>in</c> to it, or
/// <c>both</c>, wandering across the line the plan drew. Absent is <c>out</c>, the slight bloat that reads as
/// land; <c>in</c> is what a board whose shapes abut on a measured strait asks for.</param>
/// <param name="Edges">The edges to draw as coast, each by the vertex it leaves, counted on the outline as
/// every point edit leaves it. Absent bends every edge long enough to cut; named, every other edge stays
/// exactly as it was drawn — a seam, a frontline — which is the coast a board states with <c>side: in</c>.</param>
/// <param name="Fan">False draws the coast as the outline's own coordinates give it. Absent, an outline the
/// board's symmetry carries onto itself — a shape on the axis — is drawn as a coast that is its own image too,
/// and an edge named is drawn at each of its images.</param>
public sealed record ShapeBend(
    double Wander, double Step, uint Seed, double? Tension = null, BendSide? Side = null, IReadOnlyList<int>? Edges = null,
    bool? Fan = null)
{
    /// <summary>The handle length a coast reads well at, as a fraction of its own edge — measured over the
    /// seven boards in the corpus that bend one.</summary>
    public const double DefaultTension = 0.22;

    /// <summary>The layout with the shape at <paramref name="shapeId"/> bent as this states, or the finding
    /// that refuses it: a bend states a wander and a step, both greater than nought.
    /// <paramref name="held"/> answers how many cut points had no room on the side asked for.</summary>
    public GeometryEdit ApplyTo(string? layoutJson, string shapeId, out int held)
    {
        held = 0;
        if (Wander <= 0 || Step <= 0)
            return GeometryEdit.Refused(new Finding(RequestRules.Unreadable,
                $"the bend of shape '{shapeId}' has a `wander` of {Wander} blocks and a `step` of {Step} blocks, "
                + "not both more than 0", Field: "wander", Subjects: [shapeId]));
        return SketchGeometryEdit.BendShape(
            layoutJson, shapeId, Wander, Step, Seed, Tension ?? DefaultTension, Side ?? BendSide.Out, out held, Edges,
            Fan ?? true);
    }
}
