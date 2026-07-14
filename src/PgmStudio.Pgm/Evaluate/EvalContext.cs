using PgmStudio.Pgm.Derive;
using PgmStudio.Pgm.Plan;

namespace PgmStudio.Pgm.Evaluate;

/// <summary>
/// The derived-once context every term reads: the plan, its <see cref="ContactGraph"/> (the rect-layer
/// adjacency), the structural <see cref="PlanFinding"/>s (parse/overlap/reachability), the raster-layer
/// <see cref="BoardStructure"/> (islands, zone kinds, holes, lanes), and the authored metric bands. A term
/// never re-derives any of these — it reads them here. <see cref="Board"/> is computed lazily, so a hard-only
/// gate (which no ported term needs the board for) never pays for the raster derive on its resample loop.
/// </summary>
public sealed class EvalContext
{
    public PlanModel Plan { get; }
    public ContactGraph Contacts { get; }
    public IReadOnlyList<PlanFinding> Findings { get; }
    public SeedEnvelopes Envelopes { get; }

    private readonly Lazy<BoardStructure> _board;
    public BoardStructure Board => _board.Value;

    private EvalContext(PlanModel plan, ContactGraph contacts, IReadOnlyList<PlanFinding> findings, SeedEnvelopes envelopes)
    {
        Plan = plan;
        Contacts = contacts;
        Findings = findings;
        Envelopes = envelopes;
        _board = new Lazy<BoardStructure>(() => BoardDeriver.Derive(plan));
    }

    /// <summary>Build the context for a plan, deriving the contact graph and running the structural validator
    /// once. Pass the authored <paramref name="envelopes"/> for soft scoring; omit for a hard-only gate.</summary>
    public static EvalContext Build(PlanModel plan, SeedEnvelopes? envelopes = null)
    {
        var contacts = ContactGraph.Build(plan);
        var findings = PlanValidator.Validate(plan);
        return new EvalContext(plan, contacts, findings, envelopes ?? SeedEnvelopes.Empty);
    }
}

/// <summary>One rule, one term: reads derived measurables only (never a shape/family name — the enumeration
/// trap), cites exactly one <see cref="RuleId"/> from <c>layout-rules.md</c>, and is pure (no RNG, no IO).</summary>
public interface ILayoutTerm
{
    /// <summary>Stable term id (e.g. <c>band-wool-clearance</c>) — the profile's enable/weight key.</summary>
    string Id { get; }

    /// <summary>The single <c>layout-rules.md</c> id this term scores (e.g. <c>BZ6</c>).</summary>
    string RuleId { get; }

    TermKind Kind { get; }

    /// <summary>Score this term against the derived context: a distance (0 inside band; always 0 for hard) and,
    /// when the term fires, a <see cref="Violation"/>.</summary>
    TermScore Measure(EvalContext ctx);
}
