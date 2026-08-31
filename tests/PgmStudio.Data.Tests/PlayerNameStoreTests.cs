using LinqToDB;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;

namespace PgmStudio.Data.Tests;

/// <summary>
/// The players a lookup has already resolved (M0028). The studio asks for the same few people constantly —
/// every intent write resolves every author it names — and a resolved pair barely changes, so it is kept.
/// Runs against <c>pgm_studio_test</c>; each test resets the schema.
/// </summary>
[NotInParallel]
public sealed class PlayerNameStoreTests
{
    private const string Uuid = "069a79f4-44e9-4726-a5be-fca90e38aaf5";

    [Test]
    public async Task A_kept_player_is_found_by_either_half()
    {
        await TestDb.ResetSchemaAsync();
        await using var db = TestDb.Connect();
        var store = new PlayerNameStore(db);

        await store.KeepAsync(Uuid, "Notch");

        await Assert.That(await store.LookAsync("Notch")).IsEqualTo((Uuid, "Notch"));
        await Assert.That(await store.LookAsync(Uuid)).IsEqualTo((Uuid, "Notch"));
        await Assert.That(await store.LookAsync("nobody")).IsNull();
    }

    /// <summary>The uuid is the identity, so a player who renames updates their own row. Keying on the name
    /// would leave the old one behind and answer two different people to the same question.</summary>
    [Test]
    public async Task A_rename_updates_the_row_rather_than_growing_a_second()
    {
        await TestDb.ResetSchemaAsync();
        await using var db = TestDb.Connect();
        var store = new PlayerNameStore(db);

        await store.KeepAsync(Uuid, "Notch");
        await store.KeepAsync(Uuid, "jeb_");

        await Assert.That(await store.LookAsync(Uuid)).IsEqualTo((Uuid, "jeb_"));
        await Assert.That(db.MinecraftPlayers.Count()).IsEqualTo(1);
    }

    /// <summary>A kept answer stands for <see cref="PlayerNameStore.Freshness"/> and no longer, so a name that
    /// has changed since is asked again rather than served stale for ever.</summary>
    [Test]
    public async Task A_stale_row_is_not_an_answer()
    {
        await TestDb.ResetSchemaAsync();
        await using var db = TestDb.Connect();
        var store = new PlayerNameStore(db);

        await db.InsertAsync(new MinecraftPlayerRow
        {
            Uuid = Uuid,
            Name = "Notch",
            FetchedAt = DateTime.UtcNow - PlayerNameStore.Freshness - TimeSpan.FromDays(1),
        });

        await Assert.That(await store.LookAsync("Notch")).IsNull();
    }

    [Test]
    public async Task Keeping_half_an_answer_keeps_nothing()
    {
        await TestDb.ResetSchemaAsync();
        await using var db = TestDb.Connect();
        var store = new PlayerNameStore(db);

        await store.KeepAsync("", "Notch");
        await store.KeepAsync(Uuid, "");

        await Assert.That(db.MinecraftPlayers.Count()).IsEqualTo(0);
    }
}
