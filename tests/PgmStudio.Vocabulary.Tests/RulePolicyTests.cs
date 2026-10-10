using PgmStudio.Vocabulary;

namespace PgmStudio.Vocabulary.Tests;

/// <summary>
/// The policy is process-wide, so every test here sets levels on ids no other test raises and clears them after.
/// The minimal mode, which relaxes every rule at once, is not set here for the same reason.
/// </summary>
[NotInParallel("rule-policy")]
public class RulePolicyTests
{
    [After(Test)]
    public void Clear()
    {
        RulePolicy.Configure(new Dictionary<string, string>());
        RulePolicy.Store(new Dictionary<string, string>());
    }

    [Test]
    public async Task A_rule_no_setting_names_keeps_what_its_code_says()
    {
        await Assert.That(RulePolicy.LevelOf("XQ1")).IsNull();
        await Assert.That(new Finding("XQ1", "a refusal").Severity).IsEqualTo(Severity.Refusal);
        await Assert.That(new Finding("XQ1", "a remark", Severity.Complaint).Severity).IsEqualTo(Severity.Complaint);
        await Assert.That(RulePolicy.Enforces("XQ1")).IsTrue();
    }

    [Test]
    public async Task A_hint_answers_a_refusal_as_a_complaint_and_turns_nothing_away()
    {
        RulePolicy.Configure(new Dictionary<string, string> { ["XQ2"] = RuleLevels.Hint });

        await Assert.That(new Finding("XQ2", "a refusal").Refuses).IsFalse();
        await Assert.That(RulePolicy.Enforces("XQ2")).IsFalse();
        await Assert.That(RulePolicy.LevelOf("XQ2")).IsEqualTo((RuleLevels.Hint, RuleLevelSources.Config));
    }

    [Test]
    public async Task A_refuse_answers_a_complaint_as_a_refusal()
    {
        RulePolicy.Configure(new Dictionary<string, string> { ["XQ3"] = RuleLevels.Refuse });

        await Assert.That(new Finding("XQ3", "a remark", Severity.Complaint).Refuses).IsTrue();
    }

    [Test]
    public async Task An_admins_level_wins_over_the_configuration()
    {
        RulePolicy.Configure(new Dictionary<string, string> { ["XQ4"] = RuleLevels.Hint });
        RulePolicy.Store(new Dictionary<string, string> { ["XQ4"] = RuleLevels.Refuse });

        await Assert.That(RulePolicy.LevelOf("XQ4")).IsEqualTo((RuleLevels.Refuse, RuleLevelSources.Studio));
        await Assert.That(new Finding("XQ4", "a refusal").Refuses).IsTrue();
    }

    [Test]
    public async Task A_decline_stays_a_decline_whatever_the_level()
    {
        RulePolicy.Configure(new Dictionary<string, string> { ["XQ5"] = RuleLevels.Hint, ["XQ6"] = RuleLevels.Refuse });

        await Assert.That(new Finding("XQ5", "dropped", Severity.Decline).Severity).IsEqualTo(Severity.Decline);
        await Assert.That(new Finding("XQ6", "dropped", Severity.Decline).Severity).IsEqualTo(Severity.Decline);
    }
}
