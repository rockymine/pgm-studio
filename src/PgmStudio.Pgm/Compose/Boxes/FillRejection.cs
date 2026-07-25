using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Compose;

/// <summary>
/// <b>Why</b> a box fill was refused — the one directed-reason vocabulary every box kind reports through.
///
/// <para>The box kinds cannot share a single result <em>type</em>: an approach fill carries an
/// <see cref="EmittedApproach"/> keyed by <see cref="ShapeFamily"/> while a body fill carries an
/// <see cref="EmittedHub"/>/<see cref="EmittedFrontline"/> keyed by <see cref="CompoundRead"/> (the same split
/// <see cref="FillProfiles.Families"/> vs <see cref="FillProfiles.HubForms"/> already makes deliberately). They
/// can and do share the <em>reason</em>, which is what a caller reasoning about producibility actually reads —
/// so a refusal is legible without knowing which kind produced it.</para>
///
/// <para>What was attempted is not carried here: the caller passed the form/family in, so repeating it in the
/// rejection would be a second copy free to disagree. A rejection says only what the caller could not have
/// known.</para>
/// </summary>
public abstract record FillRejection
{
    /// <summary>The footprint is below the family's minimum box (<see cref="ShapeEmitter.MinBox"/>), which is
    /// known exactly — so a caller can resize directedly rather than search.</summary>
    public sealed record TooSmall(int MinW, int MinH) : FillRejection;

    /// <summary>The body emitter refused the form at this footprint and wall width, reporting
    /// <see cref="Detail"/> (its own dimension guard). Distinct from <see cref="TooSmall"/> because a body
    /// emitter states no minimum box — inventing one here would be a rule this layer does not own.</summary>
    public sealed record FormDoesNotFit(string Detail) : FillRejection;

    /// <summary>The requested form/family is outside the box kind's profile; <see cref="Menu"/> names what was
    /// on offer.</summary>
    public sealed record NotOnMenu(IReadOnlyList<string> Menu) : FillRejection;

    /// <summary>The docking gate (<see cref="DockingGate"/>) refused the mouth the fill would dock through.</summary>
    public sealed record IllegalDock(DockRejection Reason, BoxEdge Mouth) : FillRejection;

    /// <summary>The knob combination is not one the emitter supports (a side-tuck room on an <c>L</c>, say) —
    /// <see cref="Detail"/> is the emitter's own message. A legal-but-unsupported request, not a size problem.</summary>
    public sealed record UnsupportedKnobs(string Detail) : FillRejection;
}
