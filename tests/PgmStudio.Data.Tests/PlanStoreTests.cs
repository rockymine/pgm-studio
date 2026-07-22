using PgmStudio.Data.Plan;
using PgmStudio.Data.Schema;
using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Plan;

namespace PgmStudio.Data.Tests;

/// <summary>
/// The plan store's persistence doctrine (M0008): a fresh save inserts an authored row; editing an authored
/// row mutates it in place; editing a generated (or imported) row is forbidden, so the save <b>forks</b> a new
/// authored row whose <c>parent_id</c> points back. Content is canonicalized before hashing, so dedup is
/// stable under formatting noise; deleting a parent orphans its forks rather than cascading them away. Runs
/// against <c>pgm_studio_test</c> (override with <c>PGM_STUDIO_TEST_DB</c>); each test resets the schema.
/// </summary>
[NotInParallel]
public sealed class PlanStoreTests
{
    // A minimal plan whose only distinguishing content is its meta name — enough to vary the content hash.
    private static string PlanJson(string name) => new PlanModel { Meta = new PlanMeta { Name = name } }.ToJson();

    [Test]
    public async Task SaveFromEditor_with_no_source_inserts_an_authored_row()
    {
        await TestDb.ResetSchemaAsync();
        await using var db = TestDb.Connect();
        var store = new PlanStore(db);

        var row = await store.SaveFromEditorAsync(PlanJson("fresh"), sourceId: null);

        await Assert.That(row.Id).IsGreaterThan(0L);
        await Assert.That(row.Origin).IsEqualTo(PlanOrigin.Authored);
        await Assert.That(row.Name).IsEqualTo("fresh");
        await Assert.That(row.ParentId).IsNull();
    }

    [Test]
    public async Task SaveFromEditor_mutates_an_authored_source_in_place()
    {
        await TestDb.ResetSchemaAsync();
        await using var db = TestDb.Connect();
        var store = new PlanStore(db);

        var v1 = await store.SaveFromEditorAsync(PlanJson("v1"), sourceId: null);
        var v2 = await store.SaveFromEditorAsync(PlanJson("v2"), sourceId: v1.Id);

        await Assert.That(v2.Id).IsEqualTo(v1.Id);               // same row
        await Assert.That(v2.Name).IsEqualTo("v2");              // content updated
        await Assert.That(v2.Origin).IsEqualTo(PlanOrigin.Authored);
        await Assert.That((await store.ListAsync()).Count).IsEqualTo(1);
    }

    [Test]
    public async Task SaveFromEditor_forks_a_generated_source_into_a_new_authored_row()
    {
        await TestDb.ResetSchemaAsync();
        await using var db = TestDb.Connect();
        var store = new PlanStore(db);

        var gen = await store.SaveGeneratedAsync(
            PlanJson("gen"), ComposeDescriptor.For(new ComposeRequest(12, seed: 7)));
        var fork = await store.SaveFromEditorAsync(PlanJson("gen-edited"), sourceId: gen.Id);

        await Assert.That(fork.Id).IsNotEqualTo(gen.Id);         // a new row, not a mutation
        await Assert.That(fork.Origin).IsEqualTo(PlanOrigin.Authored);
        await Assert.That(fork.ParentId).IsEqualTo(gen.Id);      // fork provenance

        var genReloaded = await store.GetByIdAsync(gen.Id);
        await Assert.That(genReloaded!.Origin).IsEqualTo(PlanOrigin.Generated);
        await Assert.That(genReloaded.Name).IsEqualTo("gen");    // the generated corpus is untouched
    }

    [Test]
    public async Task SaveGenerated_stores_the_descriptor_and_dedups_identical_geometry()
    {
        await TestDb.ResetSchemaAsync();
        await using var db = TestDb.Connect();
        var store = new PlanStore(db);

        var descriptor = ComposeDescriptor.For(new ComposeRequest(16, 2, "rot_180", seed: 42));
        var g1 = await store.SaveGeneratedAsync(PlanJson("same"), descriptor);
        var g2 = await store.SaveGeneratedAsync(PlanJson("same"), descriptor);

        await Assert.That(g2.Id).IsEqualTo(g1.Id);               // identical content ⇒ the same row
        await Assert.That(g1.Seed).IsEqualTo(42UL);
        await Assert.That(g1.ComposerVersion).IsEqualTo(ComposerVersion.Current);
        await Assert.That(g1.RequestJson).IsNotNull();
        await Assert.That((await store.ListAsync(PlanOrigin.Generated)).Count).IsEqualTo(1);
    }

    [Test]
    public async Task SaveImported_dedups_and_the_hash_is_stable_under_formatting_noise()
    {
        await TestDb.ResetSchemaAsync();
        await using var db = TestDb.Connect();
        var store = new PlanStore(db);

        var canonical = PlanJson("imported");
        var noisy = "\n   " + canonical + "  \n";               // insignificant whitespace

        var i1 = await store.SaveImportedAsync(canonical);
        var i2 = await store.SaveImportedAsync(noisy);

        await Assert.That(i2.Id).IsEqualTo(i1.Id);               // canonicalized before hashing
        await Assert.That(i1.ContentHash.Length).IsEqualTo(64);
    }

    [Test]
    public async Task List_orders_newest_touched_first()
    {
        await TestDb.ResetSchemaAsync();
        await using var db = TestDb.Connect();
        var store = new PlanStore(db);

        var a = await store.SaveFromEditorAsync(PlanJson("a"), sourceId: null);
        var b = await store.SaveFromEditorAsync(PlanJson("b"), sourceId: null);

        var list = await store.ListAsync();
        await Assert.That(list[0].Id).IsEqualTo(b.Id);           // most recent first (id tie-breaks equal timestamps)
        await Assert.That(list[1].Id).IsEqualTo(a.Id);
    }

    [Test]
    public async Task Delete_orphans_forks_rather_than_cascading()
    {
        await TestDb.ResetSchemaAsync();
        await using var db = TestDb.Connect();
        var store = new PlanStore(db);

        var gen = await store.SaveGeneratedAsync(
            PlanJson("parent"), ComposeDescriptor.For(new ComposeRequest(12, seed: 1)));
        var fork = await store.SaveFromEditorAsync(PlanJson("fork"), sourceId: gen.Id);

        await store.DeleteAsync(gen.Id);

        await Assert.That(await store.GetByIdAsync(gen.Id)).IsNull();
        var forkReloaded = await store.GetByIdAsync(fork.Id);
        await Assert.That(forkReloaded).IsNotNull();             // the fork survives
        await Assert.That(forkReloaded!.ParentId).IsNull();      // its provenance is set null, not deleted
    }
}
