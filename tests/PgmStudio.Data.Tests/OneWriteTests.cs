using LinqToDB;
using PgmStudio.Contracts;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;

namespace PgmStudio.Data.Tests;

/// <summary>
/// A write that replaces lands whole or not at all.
///
/// <para>The studio stores by replacing — a document, a map's features and an artifact are each written by
/// dropping what is there and inserting what was posted — so the window between the two halves is a state no
/// author ever authored, and no read can tell it apart from a map that really does have less in it. What is
/// asserted here is that the window does not exist.</para>
///
/// <para>The second test is the one the shape turns on. A connection carries a single transaction, so a
/// writer that opens one and calls another writer that opens one is a runtime fault, not merely a wasted
/// nesting — and a finished sketch does exactly that, writing its segment rows and its three artifacts
/// through two writers over one connection. The inner call joins the outer boundary instead, which means an
/// inner write is undone by an outer failure.</para>
/// </summary>
[NotInParallel]
public sealed class OneWriteTests
{
    private static readonly byte[] Before = "before"u8.ToArray();
    private static readonly byte[] After = "after"u8.ToArray();

    [Test]
    public async Task A_fault_part_way_through_leaves_what_was_there()
    {
        await TestDb.ResetSchemaAsync();
        await using var db = TestDb.Connect();
        var artifacts = new MapArtifactStore(db);
        var mapId = await InsertMapAsync(db);
        await artifacts.SaveAsync(mapId, ArtifactKind.PlanJson, Before);

        await Assert.That(async () => await db.InOneWriteAsync(async () =>
        {
            await artifacts.DeleteAsync(mapId, ArtifactKind.PlanJson);
            throw new InvalidOperationException("the write did not finish");
        })).Throws<InvalidOperationException>();

        await Assert.That(await artifacts.LoadAsync(mapId, ArtifactKind.PlanJson)).IsEquivalentTo(Before)
            .Because("the delete was half of a replacement that never completed");
    }

    /// <summary>A write inside a write is one write: the inner one's completion is not a commit, so the
    /// outer failure takes its rows with it.</summary>
    [Test]
    public async Task A_write_inside_a_write_is_undone_by_the_outer_failure()
    {
        await TestDb.ResetSchemaAsync();
        await using var db = TestDb.Connect();
        var artifacts = new MapArtifactStore(db);
        var mapId = await InsertMapAsync(db);

        await Assert.That(async () => await db.InOneWriteAsync(async () =>
        {
            // Opens its own write, which joins this one rather than failing on a second transaction.
            await artifacts.SaveAsync(mapId, ArtifactKind.PlanJson, After);
            throw new InvalidOperationException("the write did not finish");
        })).Throws<InvalidOperationException>();

        await Assert.That(await artifacts.LoadAsync(mapId, ArtifactKind.PlanJson)).IsNull();
    }

    /// <summary>And the ordinary path still commits. Asserted because a wrapper that rolled everything back
    /// would satisfy both tests above and store nothing at all.</summary>
    [Test]
    public async Task A_write_that_finishes_is_kept()
    {
        await TestDb.ResetSchemaAsync();
        await using var db = TestDb.Connect();
        var artifacts = new MapArtifactStore(db);
        var mapId = await InsertMapAsync(db);
        await artifacts.SaveAsync(mapId, ArtifactKind.PlanJson, Before);

        await db.InOneWriteAsync(() => artifacts.SaveAsync(mapId, ArtifactKind.PlanJson, After));

        await Assert.That(await artifacts.LoadAsync(mapId, ArtifactKind.PlanJson)).IsEquivalentTo(After);
    }

    private static Task<long> InsertMapAsync(PgmDb db) =>
        db.InsertWithInt64IdentityAsync(new MapRow
        {
            Slug = "one-write", Name = "one write", Gamemode = "ctw", Stage = MapStage.Plan,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        });
}
