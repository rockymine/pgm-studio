using PgmStudio.Data.Plan;
using PgmStudio.Data.Schema;
using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Plan;

namespace PgmStudio.Data.Tests;

/// <summary>
/// The verdict store's doctrine (M0015): one judgment per plan, refined in place — an upsert updates
/// direction, tags and note but never the evaluator snapshot taken at the first vote. The pinned flag
/// separates the two reasons a generated row persists: a vote stores it unpinned (labeled corpus, not the
/// tray), a pin promotes it, and deleting a voted plan demotes it to unpinned instead of losing the label.
/// Runs against <c>pgm_studio_test</c>; each test resets the schema.
/// </summary>
[NotInParallel]
public sealed class VerdictStoreTests
{
    private static string PlanJson(string name) => new PlanModel { Meta = new PlanMeta { Name = name } }.ToJson();

    private static Task<PlanRow> Generated(PlanStore store, string name, ulong seed, bool pinned = true) =>
        store.SaveGeneratedAsync(PlanJson(name), ComposeDescriptor.For(new ComposeRequest(12, 2, "rot_180", seed: seed)), pinned: pinned);

    [Test]
    public async Task Upsert_inserts_then_refines_in_place_keeping_the_snapshot()
    {
        await TestDb.ResetSchemaAsync();
        await using var db = TestDb.Connect();
        var store = new PlanStore(db);
        var verdicts = new VerdictStore(db);
        var plan = await Generated(store, "judged", 1);

        var first = await verdicts.UpsertAsync(plan.Id, "down", """["flat-front"]""", "meh", 3.5, """[{"termId":"x"}]""", "terms-1");
        var second = await verdicts.UpsertAsync(plan.Id, "up", """["great-hub"]""", null, 9.9, """[]""", "terms-2");

        await Assert.That(second.Id).IsEqualTo(first.Id);          // one judgment per plan
        await Assert.That(second.Verdict).IsEqualTo("up");
        await Assert.That(second.TagsJson).Contains("great-hub");
        await Assert.That(second.Note).IsNull();
        // the evaluator snapshot is what the evaluator said about the board, which has not changed
        await Assert.That(second.Score).IsEqualTo(3.5);
        await Assert.That(second.TermsJson).Contains("termId");
        await Assert.That(second.EvaluatorVersion).IsEqualTo("terms-1");
    }

    [Test]
    public async Task List_joins_the_plan_and_delete_retracts()
    {
        await TestDb.ResetSchemaAsync();
        await using var db = TestDb.Connect();
        var store = new PlanStore(db);
        var verdicts = new VerdictStore(db);
        var planA = await Generated(store, "a", 1);
        var planB = await Generated(store, "b", 2);
        await verdicts.UpsertAsync(planA.Id, "up", "[]", null, 0, "[]", "terms-1");
        await verdicts.UpsertAsync(planB.Id, "down", "[]", null, 0, "[]", "terms-1");

        var listed = await verdicts.ListAsync();
        await Assert.That(listed.Count).IsEqualTo(2);
        await Assert.That(listed.All(x => x.Plan.Id == x.Verdict.PlanId)).IsTrue();

        await Assert.That(await verdicts.DeleteAsync(planA.Id)).IsEqualTo(1);
        await Assert.That((await verdicts.ListAsync()).Count).IsEqualTo(1);
    }

    [Test]
    public async Task A_vote_stores_unpinned_and_a_pin_promotes_never_demotes()
    {
        await TestDb.ResetSchemaAsync();
        await using var db = TestDb.Connect();
        var store = new PlanStore(db);

        // vote first: the row persists outside the tray
        var voted = await Generated(store, "voted-first", 1, pinned: false);
        await Assert.That(voted.Pinned).IsFalse();
        await Assert.That((await store.ListAsync(PlanOrigin.Generated, pinned: true)).Count).IsEqualTo(0);

        // pinning the same board later promotes the existing row
        var promoted = await Generated(store, "voted-first", 1, pinned: true);
        await Assert.That(promoted.Id).IsEqualTo(voted.Id);
        await Assert.That(promoted.Pinned).IsTrue();

        // a later vote on a pinned board never demotes it
        var revisited = await Generated(store, "voted-first", 1, pinned: false);
        await Assert.That(revisited.Pinned).IsTrue();
        await Assert.That((await store.ListAsync(PlanOrigin.Generated, pinned: true)).Count).IsEqualTo(1);
    }

    [Test]
    public async Task Deleting_a_voted_plan_demotes_it_and_keeps_the_label()
    {
        await TestDb.ResetSchemaAsync();
        await using var db = TestDb.Connect();
        var store = new PlanStore(db);
        var verdicts = new VerdictStore(db);

        var plan = await Generated(store, "held-and-judged", 1, pinned: true);
        await verdicts.UpsertAsync(plan.Id, "up", "[]", null, 0, "[]", "terms-1");

        await store.DeleteAsync(plan.Id);                          // the tray's unpin
        var kept = await store.GetByIdAsync(plan.Id);
        await Assert.That(kept).IsNotNull().Because("a labeled row outlives the tray");
        await Assert.That(kept!.Pinned).IsFalse();
        await Assert.That((await verdicts.ListAsync()).Count).IsEqualTo(1);

        // retracting the verdict first makes the delete real
        await verdicts.DeleteAsync(plan.Id);
        await store.DeleteAsync(plan.Id);
        await Assert.That(await store.GetByIdAsync(plan.Id)).IsNull();
    }
}
