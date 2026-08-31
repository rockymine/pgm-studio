using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Vocabulary;

namespace PgmStudio.Data.Tests;

/// <summary>Repository helpers beyond the entity-graph round-trip — slug uniquification for URL imports
/// (a download URL carries the world's own name, so independent imports collide on the base slug).</summary>
[NotInParallel]
public sealed class MapRepositoryTests
{
    [Test]
    public async Task UniqueSlugAsync_suffixes_past_taken_slugs()
    {
        await TestDb.ResetSchemaAsync();
        await using var db = TestDb.Connect();
        var repo = new MapRepository(db);

        // a free base slug comes back unchanged
        await Assert.That(await repo.UniqueSlugAsync("rockymine")).IsEqualTo("rockymine");

        await Insert(repo, "rockymine");
        await Assert.That(await repo.UniqueSlugAsync("rockymine")).IsEqualTo("rockymine-2");

        await Insert(repo, "rockymine-2");
        await Assert.That(await repo.UniqueSlugAsync("rockymine")).IsEqualTo("rockymine-3");

        // the "taken" check is case-insensitive, so a differently-cased base still gets suffixed
        await Assert.That(await repo.UniqueSlugAsync("RockyMine")).IsEqualTo("RockyMine-3");
    }

    /// <summary>The Edit stage lists by slug and every other stage by recency. An edit row's
    /// <c>updated_at</c> records when the ingest pipeline last wrote it rather than when its author last
    /// worked on it — a re-processing pass stamps whole batches within a second of each other — so recency
    /// there orders the list by something that carries no authoring signal and renders as runs of
    /// alphabetical batches (<c>B34</c>).</summary>
    [Test]
    public async Task The_edit_stage_lists_by_slug_and_the_others_by_recency()
    {
        await TestDb.ResetSchemaAsync();
        await using var db = TestDb.Connect();
        var repo = new MapRepository(db);

        // Three rows per stage, inserted so that slug order and recency order disagree.
        var batch = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        foreach (var stage in new[] { MapStage.Edit, MapStage.Sketch })
        {
            await Staged(repo, $"{stage}-charlie", stage, batch.AddMinutes(3));
            await Staged(repo, $"{stage}-alpha", stage, batch.AddMinutes(2));
            await Staged(repo, $"{stage}-bravo", stage, batch.AddMinutes(1));
        }

        var edit = (await repo.ListByStageAsync(MapStage.Edit)).Select(row => row.Slug).ToList();
        await Assert.That(string.Join(" ", edit)).IsEqualTo("edit-alpha edit-bravo edit-charlie");

        var sketch = (await repo.ListByStageAsync(MapStage.Sketch)).Select(row => row.Slug).ToList();
        await Assert.That(string.Join(" ", sketch)).IsEqualTo("sketch-charlie sketch-alpha sketch-bravo");
    }

    private static Task<long> Insert(MapRepository repo, string slug) => repo.InsertAsync(new MapRow
    {
        Slug = slug, Name = slug, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
    });

    private static Task<long> Staged(MapRepository repo, string slug, string stage, DateTime updated) =>
        repo.InsertAsync(new MapRow
        {
            Slug = slug, Name = slug, Stage = stage, CreatedAt = updated, UpdatedAt = updated,
        });
}
