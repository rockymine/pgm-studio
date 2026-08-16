using FastEndpoints;
using PgmStudio.Contracts;
using PgmStudio.Pgm.Evaluate;

namespace PgmStudio.Api.Endpoints;

/// <summary>
/// <c>GET /api/rules/terms</c> — <b>every evaluator term with the band it is scoring against right now.</b>
///
/// <para><c>GET /api/rules</c> answers what a rule <em>means</em>; this answers the numbers. A soft term's
/// band lives in one of two places — stated on the term itself where it is an authored ruling, or in the
/// generated seed envelopes where it was learned — and both are read here through the same resolution
/// <c>SoftTerm.Measure</c> uses, so the band a caller sees is the band a plan is scored against, never a
/// transcription of it. A term with no band yet is listed as dormant (<c>bandSource: "none"</c>) rather than
/// omitted, because "this metric exists and does not score yet" is an answer.</para>
///
/// <para>Anonymous and map-independent, like the catalogue beside it.</para>
/// </summary>
public sealed class EvaluatorTermsEndpoint : EndpointWithoutRequest<List<TermDto>>
{
    public override void Configure() { Get("/rules/terms"); AllowAnonymous(); }

    private static readonly List<TermDto> Catalog =
    [
        .. LayoutEvaluator.AllTerms.Select(term => term is SoftTerm soft
            ? Soft(soft)
            : new TermDto(term.Id, term.RuleId, "hard", null, "none", null)),
    ];

    private static TermDto Soft(SoftTerm term)
    {
        var (band, source) = term.AuthoredBand is { } authored
            ? ((Band?)authored, "authored")
            : SeedEnvelopes.Default[term.Id] is { } learned
                ? ((Band?)learned, "envelope")
                : ((Band?)null, "none");
        return new TermDto(
            term.Id, term.RuleId, "soft",
            band is { } bounds ? [bounds.Lo, bounds.Hi] : null,
            source, term.LearnsFromTraced);
    }

    public override Task HandleAsync(CancellationToken ct) => Send.OkAsync(Catalog, ct);
}
