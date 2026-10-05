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
/// rule's own words, read out of the docstring beside its <c>const</c>, so neither can drift from the rule it
/// describes.</para>
///
/// <para><see cref="Category"/> and <see cref="Concerns"/> are the two machine-legible things beyond the id:
/// what to do about a finding citing this rule, and what the rule is about. Both are read off the
/// <c>[Rule]</c> attribute beside the constant, so they are declared once per rule rather than restated at
/// each site that raises one.</para>
/// </summary>
/// <param name="Fix">What to do about a finding citing the rule.</param>
/// <param name="Category">What a caller does about a finding citing this rule — the closed set an agent
/// branches on without knowing the id. Absent for a rule nothing raises: there is no caller to branch and
/// nothing to do.</param>
/// <param name="Concerns">What the rule is about, one word or several. A rule concerns a combination —
/// <c>WX6</c> is a plan, a structure and an objective at once — which a one-token family prefix cannot
/// say.</param>
/// <param name="Rule">The stable id a finding carries — <c>PL9</c>, <c>HS1</c>, <c>WL2</c>.</param>
/// <param name="Family">Its letters, which group rules by what they are about rather than by which gate
/// asks.</param>
/// <param name="Owner">Where the rule is stated, which is the file to read next: its declaring
/// constant.</param>
/// <param name="Means">What the rule refuses, in one sentence.</param>
public sealed record RuleDto(
    string Rule, string Family, string Owner, string Means, string? Fix = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] RuleCategory? Category = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<RuleConcern>? Concerns = null);
