using PgmStudio.Domain;
using PgmStudio.Geom;
using PgmStudio.Pgm.Derive;
using PgmStudio.Pgm.Plan;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Evaluate;

/// <summary>
/// The derived-once context every term reads: the plan, its <see cref="ContactGraph"/> (the rect-layer
/// adjacency), the structural <see cref="Finding"/>s (parse/overlap/reachability), the raster-layer
/// <see cref="BoardStructure"/> (islands, zone kinds, holes, lanes), and the authored metric bands. A term
/// never re-derives any of these — it reads them here. <see cref="Board"/> is computed lazily, so a hard-only
/// gate (which needs the board for no term) never pays for the raster derive on its resample loop.
/// </summary>
public sealed class EvalContext
{
    private readonly PlanModel? _plan;
    private readonly ContactGraph? _contacts;
    private readonly Findings? _findings;

    /// <summary>The plan the board was read off. A context read off another ground has none, and a term that
    /// asks for one is not a term that ground answers.</summary>
    public PlanModel Plan => _plan ?? throw NoPlan();
    public ContactGraph Contacts => _contacts ?? throw NoPlan();
    public Findings Findings => _findings ?? throw NoPlan();
    public SeedEnvelopes Envelopes { get; }

    /// <summary>How many images the board's symmetry has, which is how many teams share it.</summary>
    public int Order { get; }

    /// <summary>What the board is played for.</summary>
    public BoardGoals Goals { get; }

    private readonly Lazy<BoardStructure> _board;
    public BoardStructure Board => _board.Value;

    private readonly Lazy<PlanFlow.Result>? _flow;

    /// <summary>What the board asks of the two sides: each demand set's walk, its decisions, and the ground no
    /// journey reaches. Lazy for the same reason <see cref="Board"/> is — a hard-only gate never pays for a
    /// traversal it does not read.</summary>
    public PlanFlow.Result Flow => _flow?.Value ?? throw NoPlan();

    private EvalContext(
        PlanModel? plan, ContactGraph? contacts, Findings? findings, SeedEnvelopes envelopes, int order, BoardGoals goals,
        Lazy<BoardStructure> board)
    {
        _plan = plan;
        _contacts = contacts;
        _findings = findings;
        Envelopes = envelopes;
        Order = order;
        Goals = goals;
        _board = board;
        if (plan is not null) _flow = new Lazy<PlanFlow.Result>(() => PlanFlow.Read(plan));
    }

    /// <summary>Build the context for a plan, deriving the contact graph and running the structural validator
    /// once. Pass the authored <paramref name="envelopes"/> for soft scoring; omit for a hard-only gate.</summary>
    public static EvalContext Build(PlanModel plan, SeedEnvelopes? envelopes = null)
    {
        var contacts = ContactGraph.Build(plan);
        var findings = PlanValidator.Check(plan);
        var placed = plan.Placements;
        return new EvalContext(plan, contacts, findings, envelopes ?? SeedEnvelopes.Empty,
            Symmetry.Order(plan.Globals.Symmetry),
            new BoardGoals(placed.Wools.Count, placed.Destroyables.Count, placed.Cores.Count),
            new Lazy<BoardStructure>(() => BoardDeriver.Derive(plan)));
    }

    /// <summary>The context for a board read off a ground with no plan behind it: the terms that read only the
    /// board's structure answer it, and one that asks for the plan is refused.</summary>
    public static EvalContext OfBoard(BoardStructure board, int order, BoardGoals goals, SeedEnvelopes envelopes) =>
        new(null, null, null, envelopes, order, goals, new Lazy<BoardStructure>(() => board));

    private static InvalidOperationException NoPlan() =>
        new("this board was read off a ground with no plan behind it");
}

/// <summary>What a board is played for: how many wools, destroyables and cores it carries.</summary>
public readonly record struct BoardGoals(int Wools, int Destroyables, int Cores);

/// <summary>One rule, one term: reads derived measurables only (never a shape/family name — the enumeration
/// trap), cites exactly one <see cref="RuleId"/> from <see cref="LayoutRules"/>, and is pure (no RNG, no IO).</summary>
public interface ILayoutTerm
{
    /// <summary>Stable term id (e.g. <c>band-wool-clearance</c>) — the profile's enable/weight key.</summary>
    string Id { get; }

    /// <summary>The single layout rule this term scores (e.g. <see cref="LayoutRules.MidZoneNearWool"/>).</summary>
    string RuleId { get; }

    TermKind Kind { get; }

    /// <summary>Score this term against the derived context: a distance (0 inside band; always 0 for hard) and,
    /// when the term fires, a <see cref="Violation"/>.</summary>
    TermScore Measure(EvalContext ctx);
}
