namespace PgmStudio.Vocabulary;

/// <summary>
/// Which rules stop the work. Under the full policy, the default, every rule does what its finding says.
/// Under the minimal policy only the rules it is given stop anything: every other refusal is answered as a
/// complaint, so the work goes ahead and the finding still rides along, and a dressing site that would have
/// turned a prop away seats it instead.
///
/// <para>Set once at startup from <c>Rules:Mode</c>; nothing changes it while a request runs.</para>
/// </summary>
public static class RulePolicy
{
    private static HashSet<string>? enforced;

    /// <summary>Whether only the given rules stop the work.</summary>
    public static bool Minimal => enforced is not null;

    /// <summary>Only <paramref name="rules"/> stop the work from here on.</summary>
    public static void UseMinimal(IEnumerable<string> rules) => enforced = [.. rules];

    /// <summary>Every rule stops the work again.</summary>
    public static void UseFull() => enforced = null;

    /// <summary>Whether a finding citing <paramref name="rule"/> stops the work or turns something away.</summary>
    public static bool Enforces(string rule) => enforced is null || enforced.Contains(rule);

    /// <summary>The severity a finding is answered with: a refusal under a rule the policy does not enforce
    /// is a complaint. A decline is left as it is, because it says something is not in the world, and only the
    /// site that dropped it can decide to keep it instead.</summary>
    public static Severity Apply(string rule, Severity severity) =>
        severity == Severity.Refusal && !Enforces(rule) ? Severity.Complaint : severity;
}
