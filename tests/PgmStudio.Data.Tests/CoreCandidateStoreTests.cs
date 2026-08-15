using PgmStudio.Data.Features;
using PgmStudio.Data.Schema;
using PgmStudio.Domain;
using PgmStudio.Minecraft;
using PgmStudio.Geom;

namespace PgmStudio.Data.Tests;

/// <summary>
/// The core-candidate store (M0014). It exists because the world a core was found in is deleted immediately
/// after import, so what is asserted here is that a suggestion survives that boundary intact — every measured
/// parameter, not just the position — and that a re-scan replaces rather than accumulates. Runs against
/// <c>pgm_studio_test</c>; each test resets the schema.
/// </summary>
[NotInParallel]
public sealed class CoreCandidateStoreTests
{
    private static CoreSuggestion Core(int x, int y, int z, int lava = 27, int shell = 1, int rise = 4, bool openTop = false)
        => new(new BlockBox(x, y, z, x + 4, y + 4, z + 4), lava, shell, rise, openTop);

    private static async Task<long> MapAsync(PgmDb db, string slug)
        => await new Map.MapRepository(db).InsertAsync(new MapRow { Slug = slug, Name = slug, Gamemode = "dtc" });

    [Test]
    public async Task A_suggestion_survives_the_round_trip_with_every_parameter()
    {
        await TestDb.ResetSchemaAsync();
        await using var db = TestDb.Connect();
        var mapId = await MapAsync(db, "core-store");

        var written = await CoreCandidateStore.WriteAsync(db, mapId, [Core(10, 20, 30, lava: 125, shell: 2, rise: 6, openTop: true)]);
        await Assert.That(written).IsEqualTo(1);

        var core = (await CoreCandidateStore.ReadAsync(db, mapId)).Single();
        await Assert.That(core.Casing).IsEqualTo(new BlockBox(10, 20, 30, 14, 24, 34));
        await Assert.That(core.LavaBlocks).IsEqualTo(125);
        await Assert.That(core.Shell).IsEqualTo(2);
        await Assert.That(core.Float).IsEqualTo(6);
        await Assert.That(core.OpenTop).IsTrue();
    }

    [Test]
    public async Task Re_gathering_replaces_rather_than_accumulates()
    {
        await TestDb.ResetSchemaAsync();
        await using var db = TestDb.Connect();
        var mapId = await MapAsync(db, "core-regather");

        await CoreCandidateStore.WriteAsync(db, mapId, [Core(0, 20, 0), Core(60, 20, 60)]);
        await CoreCandidateStore.WriteAsync(db, mapId, [Core(0, 20, 0)]);

        await Assert.That((await CoreCandidateStore.ReadAsync(db, mapId)).Count).IsEqualTo(1);
    }

    [Test]
    public async Task Candidates_are_scoped_to_their_map()
    {
        await TestDb.ResetSchemaAsync();
        await using var db = TestDb.Connect();
        var first = await MapAsync(db, "core-a");
        var second = await MapAsync(db, "core-b");

        await CoreCandidateStore.WriteAsync(db, first, [Core(0, 20, 0)]);
        await CoreCandidateStore.WriteAsync(db, second, [Core(100, 30, 100), Core(140, 30, 140)]);

        await Assert.That((await CoreCandidateStore.ReadAsync(db, first)).Count).IsEqualTo(1);
        await Assert.That((await CoreCandidateStore.ReadAsync(db, second)).Count).IsEqualTo(2);
    }

    [Test]
    public async Task Reads_come_back_in_a_stable_order()
    {
        // A suggestion list that reshuffles between requests is a list an author cannot confirm against.
        await TestDb.ResetSchemaAsync();
        await using var db = TestDb.Connect();
        var mapId = await MapAsync(db, "core-order");

        await CoreCandidateStore.WriteAsync(db, mapId, [Core(50, 40, 0), Core(0, 20, 0), Core(0, 20, 90)]);

        var ys = (await CoreCandidateStore.ReadAsync(db, mapId)).Select(core => core.Casing.MinY).ToList();
        await Assert.That(ys).IsEquivalentTo(new[] { 20, 20, 40 });
    }

    [Test]
    public async Task Deleting_the_map_takes_its_candidates()
    {
        await TestDb.ResetSchemaAsync();
        await using var db = TestDb.Connect();
        var repo = new Map.MapRepository(db);
        var mapId = await MapAsync(db, "core-cascade");
        await CoreCandidateStore.WriteAsync(db, mapId, [Core(0, 20, 0)]);

        await repo.DeleteMapAsync(mapId);

        await Assert.That((await CoreCandidateStore.ReadAsync(db, mapId))).IsEmpty();
    }

    [Test]
    public async Task A_map_with_no_cores_writes_nothing_and_reads_empty()
    {
        await TestDb.ResetSchemaAsync();
        await using var db = TestDb.Connect();
        var mapId = await MapAsync(db, "core-none");

        await Assert.That(await CoreCandidateStore.WriteAsync(db, mapId, [])).IsEqualTo(0);
        await Assert.That(await CoreCandidateStore.ReadAsync(db, mapId)).IsEmpty();
    }
}
