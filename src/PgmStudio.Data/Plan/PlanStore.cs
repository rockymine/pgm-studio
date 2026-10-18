using LinqToDB;
using LinqToDB.Async;
using PgmStudio.Data.Schema;

namespace PgmStudio.Data.Plan;

/// <summary>
/// The stored plan rows (the M0008 <c>plan</c> table): the plans the generator's candidates were kept as, and the
/// ones drawn or imported without a map behind them, kept read-only as history. Nothing writes a row.
/// </summary>
public sealed class PlanStore(PgmDb db)
{
    public Task<PlanRow?> GetByIdAsync(long id, CancellationToken ct = default)
        => db.Plans.FirstOrDefaultAsync(p => p.Id == id, ct);

    /// <summary>Plans, newest-touched first, optionally one origin.</summary>
    public Task<List<PlanRow>> ListAsync(string? origin = null, CancellationToken ct = default)
        => (origin is null ? db.Plans : db.Plans.Where(p => p.Origin == origin))
            .OrderByDescending(p => p.UpdatedAt).ThenByDescending(p => p.Id).ToListAsync(ct);

}
