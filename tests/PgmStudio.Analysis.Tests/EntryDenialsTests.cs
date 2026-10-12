using PgmStudio.Analysis.Playability;

namespace PgmStudio.Analysis.Tests;

using Dict = Dictionary<string, object?>;

/// <summary>
/// Who an <c>enter</c> filter lets in (<see cref="EntryDenials.Allows"/>): a team id named as a filter answers by
/// that team, the way PGM reads it, and the same way a <c>&lt;team&gt;</c> filter does.
/// </summary>
public sealed class EntryDenialsTests
{
    private static readonly HashSet<string> Teams = ["red-team", "blue-team"];

    private static readonly Dict Filters = new()
    {
        ["only-red"] = new Dict { ["id"] = "only-red", ["type"] = "team", ["team"] = "red-team" },
        ["not-red"] = new Dict { ["id"] = "not-red", ["type"] = "not", ["child"] = "red-team" },
    };

    [Test]
    public async Task A_team_id_admits_its_own_team_only()
    {
        await Assert.That(EntryDenials.Allows("red-team", Filters, Teams, "red-team")).IsTrue();
        await Assert.That(EntryDenials.Allows("red-team", Filters, Teams, "blue-team")).IsFalse();
    }

    [Test]
    public async Task A_team_id_reads_the_same_as_a_team_filter()
    {
        foreach (var team in Teams)
            await Assert.That(EntryDenials.Allows("red-team", Filters, Teams, team))
                .IsEqualTo(EntryDenials.Allows("only-red", Filters, Teams, team));
    }

    [Test]
    public async Task A_not_over_a_team_id_keeps_that_team_out()
    {
        await Assert.That(EntryDenials.Allows("not-red", Filters, Teams, "red-team")).IsFalse();
        await Assert.That(EntryDenials.Allows("not-red", Filters, Teams, "blue-team")).IsTrue();
    }

    [Test]
    public async Task An_id_that_is_neither_filter_nor_team_admits_everyone()
    {
        await Assert.That(EntryDenials.Allows("somewhere", Filters, Teams, "red-team")).IsTrue();
    }
}
