using LinqToDB;
using PgmStudio.Data.Plan;
using PgmStudio.Data.Schema;
using PgmStudio.Pgm.Plan;

namespace PgmStudio.Data.Tests;

/// <summary>
/// The stored plan rows (M0008), kept read-only as history: listed newest touched first, optionally of one origin,
/// and read one by one. Runs against <c>pgm_studio_test</c> (override with <c>PGM_STUDIO_TEST_DB</c>); each test
/// resets the schema.
/// </summary>
[NotInParallel]
public sealed class PlanStoreTests
{
    private static PlanRow Row(string name, string origin, DateTime touched) => new()
    {
        Name = name, Origin = origin, PlanJson = new PlanModel { Meta = new PlanMeta { Name = name } }.ToJson(),
        ContentHash = new string('0', 64), CreatedAt = touched, UpdatedAt = touched,
    };

    [Test]
    public async Task List_orders_newest_touched_first_and_narrows_to_an_origin()
    {
        await TestDb.ResetSchemaAsync();
        await using var db = TestDb.Connect();
        var store = new PlanStore(db);
        var now = DateTime.UtcNow;

        var older = await db.InsertWithInt64IdentityAsync(Row("older", PlanOrigin.Generated, now.AddHours(-1)));
        var newer = await db.InsertWithInt64IdentityAsync(Row("newer", PlanOrigin.Authored, now));

        var list = await store.ListAsync();
        await Assert.That(list[0].Id).IsEqualTo(newer);
        await Assert.That(list[1].Id).IsEqualTo(older);
        await Assert.That((await store.ListAsync(PlanOrigin.Generated)).Single().Id).IsEqualTo(older);
        await Assert.That((await store.GetByIdAsync(newer))!.Name).IsEqualTo("newer");
    }
}
