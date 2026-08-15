namespace PgmStudio.Contracts;

/// <summary>
/// One rule, as <c>GET /api/rules</c> answers it. <see cref="Rule"/> is the id a finding carries and a client
/// keys on; <see cref="Family"/> groups it by what it is about rather than by which gate asks it, so the same
/// objective rule stays one rule when the compile gate and the export gate both ask it; <see cref="Owner"/> is
/// where it is stated, which is the file to read next.
///
/// <para><see cref="Means"/> is what the rule refuses and <see cref="Fix"/> what to do about it. Both are the
/// rule's own words — a gate rule's come out of the docstring beside its <c>const</c>, a layout rule's out of
/// <c>docs/generator/rules.md</c> — so neither can drift from the rule it describes.</para>
/// </summary>
/// <param name="Fix">Null for a layout rule, and deliberately: those are claims about how a map is played,
/// which are the author's to state, so what is offered instead is <see cref="Evidence"/>.</param>
/// <param name="Evidence">How far a layout rule is backed — <c>corpus</c> (measured on the seeds),
/// <c>expert</c> (author-stated), <c>open</c> (awaiting the author) or <c>guess</c>. Null for a gate rule,
/// which is code rather than a claim.</param>
public sealed record RuleDto(
    string Rule, string Family, string Owner, string Means, string? Fix = null, string? Evidence = null);
