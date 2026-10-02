using LinqToDB;
using LinqToDB.Async;
using PgmStudio.Data.Schema;

namespace PgmStudio.Data.Theme;

/// <summary>
/// The seed key a library row carries (<see cref="StyleRow.SeedKey"/> and its eight siblings): set where the seed
/// takes a row as the folder entry it holds, cleared where the seed hands a row whose entry has left to the author
/// still using it. No other write touches it, so an edit never moves a row in or out of the folder's hands.
/// </summary>
public sealed class SeedKeyStore(PgmDb db)
{
    /// <summary>Row <paramref name="id"/> of <typeparamref name="TRow"/>'s table now carries <paramref name="key"/>.</summary>
    public Task<int> SetAsync<TRow>(long id, string? key, CancellationToken ct = default) where TRow : class
        => db.GetTable<TRow>()
            .Where(row => Sql.Property<long>(row, "Id") == id)
            .Set(row => Sql.Property<string?>(row, "SeedKey"), key)
            .UpdateAsync(ct);
}
