using System.Text.Json.Serialization;
using PgmStudio.Vocabulary;

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
///
/// <para><see cref="Category"/> and <see cref="Concerns"/> are the two machine-legible things beyond the id:
/// what to do about a finding citing this rule, and what the rule is about. Both are read off the
/// <c>[Rule]</c> attribute beside the constant, so they are declared once per rule rather than restated at
/// each site that raises one.</para>
/// </summary>
/// <param name="Fix">Null for a layout rule, and deliberately: those are claims about how a map is played,
/// which are the author's to state, so what is offered instead is <see cref="Evidence"/>.</param>
/// <param name="Evidence">How far a layout rule is backed — <c>corpus</c> (measured on the seeds),
/// <c>expert</c> (author-stated), <c>open</c> (awaiting the author) or <c>guess</c>. Null for a gate rule,
/// which is code rather than a claim.</param>
/// <param name="Category">What a caller does about a finding citing this rule — the closed set an agent
/// branches on without knowing the id. Absent for a layout rule, which has no declaration site to carry one,
/// and for a gate rule nothing raises: there is no caller to branch and nothing to do.</param>
/// <param name="Concerns">What the rule is about, one word or several. A rule concerns a combination —
/// <c>WX6</c> is a plan, a structure and an objective at once — which a one-token family prefix cannot say.
/// Absent for a layout rule.</param>
public sealed record RuleDto(
    string Rule, string Family, string Owner, string Means, string? Fix = null, string? Evidence = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] RuleCategory? Category = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<RuleConcern>? Concerns = null);
