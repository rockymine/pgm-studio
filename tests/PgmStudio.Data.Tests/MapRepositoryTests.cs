using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;

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

    private static Task<long> Insert(MapRepository repo, string slug) => repo.InsertAsync(new MapRow
    {
        Slug = slug, Name = slug, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
    });
}
