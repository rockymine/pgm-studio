using LinqToDB;
using LinqToDB.Async;
using PgmStudio.Data.Schema;

namespace PgmStudio.Data.Access;

/// <summary>The levels admins have set on rules, one row a rule.</summary>
public sealed class RuleLevelStore(PgmDb db)
{
    /// <summary>Every rule an admin has set, rule id to level.</summary>
    public async Task<Dictionary<string, string>> AllAsync(CancellationToken ct = default) =>
        (await db.RuleLevels.ToListAsync(ct)).ToDictionary(row => row.Rule, row => row.Level, StringComparer.Ordinal);

    /// <summary>Rule <paramref name="rule"/> now has <paramref name="level"/>, set by <paramref name="by"/>.</summary>
    public Task PutAsync(string rule, string level, string by, CancellationToken ct = default) =>
        db.InsertOrReplaceAsync(new RuleLevelRow { Rule = rule, Level = level, SetBy = by, SetAt = DateTime.UtcNow }, token: ct);

    /// <summary>Rule <paramref name="rule"/> goes back to what the configuration or its code says. Whether it had a
    /// level of its own.</summary>
    public async Task<bool> RemoveAsync(string rule, CancellationToken ct = default) =>
        await db.RuleLevels.Where(row => row.Rule == rule).DeleteAsync(ct) > 0;
}
