namespace PgmStudio.Contracts;

/// <summary>
/// One evaluator term, as <c>GET /api/rules/terms</c> answers it — the numeric half the rule catalogue's
/// prose cannot carry. <see cref="Term"/> is the metric id a violation names and a profile keys on;
/// <see cref="Rule"/> is the <c>rules.md</c> id it scores, which is what to ask <c>GET /api/rules</c> about.
///
/// <para><see cref="Band"/> is the band the term is scoring against <em>right now</em>, read from the same
/// place the scorer reads it, so the number served can never disagree with the number enforced.
/// <see cref="BandSource"/> says where it came from: <c>authored</c> (a ruling stated on the term itself),
/// <c>envelope</c> (learned from the teaching seeds), or <c>none</c> (a dormant soft term, or a hard term —
/// hard terms have no band, they refuse).</para>
/// </summary>
/// <param name="Band"><c>[lo, hi]</c>, or null where <see cref="BandSource"/> is <c>none</c>.</param>
/// <param name="LearnsFromTraced">Whether the envelope generator may widen this term's band with the traced
/// real-map corpus. Null for a hard term, which has no band to widen.</param>
/// <param name="Term">The metric id a violation names and a profile keys on.</param>
/// <param name="Rule">The <c>rules.md</c> id it scores — what to ask <c>GET /api/rules</c> about.</param>
/// <param name="Kind"><c>hard</c> where the term refuses a board outright, <c>soft</c> where it scores one
/// against a band.</param>
/// <param name="BandSource">Where <paramref name="Band"/> came from: <c>authored</c>, <c>envelope</c>, or
/// <c>none</c>.</param>
public sealed record TermDto(
    string Term, string Rule, string Kind, double[]? Band, string BandSource, bool? LearnsFromTraced);
