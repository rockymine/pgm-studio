using LinqToDB;
using LinqToDB.Async;
using PgmStudio.Data.Schema;

namespace PgmStudio.Data.Map;

/// <summary>
/// The players a Mojang lookup has already resolved. A resolved <c>(uuid, name)</c> pair is a fact that
/// barely changes, and the studio asks for the same handful of them constantly — every intent write resolves
/// every author it names — so the answer is kept and re-read.
///
/// <para>Kept by uuid, because that is the identity: a player who renames updates their own row rather than
/// growing a second one. A lookup arrives as either half, so both are answered.</para>
/// </summary>
public sealed class PlayerNameStore(PgmDb db)
{
    /// <summary>How long a kept answer stands before Mojang is asked again. A name change is rare and the
    /// uuid — the half the <c>map.xml</c> stores — never changes at all, so a stale row is at worst a
    /// display name a month behind and never a wrong identity.</summary>
    public static readonly TimeSpan Freshness = TimeSpan.FromDays(30);

    /// <summary>The kept answer for a name or a uuid, or null where there is none or it has gone stale.
    /// Matching is case-insensitive on both: Mojang is, and a typed name rarely carries its own casing.</summary>
    public async Task<(string Uuid, string Name)?> LookAsync(string nameOrUuid, CancellationToken ct = default)
    {
        var wanted = nameOrUuid.Trim();
        if (wanted.Length == 0) return null;

        var row = await db.MinecraftPlayers
            .Where(player => player.Uuid == wanted || player.Name == wanted)
            .FirstOrDefaultAsync(ct);

        if (row is null || DateTime.UtcNow - row.FetchedAt > Freshness) return null;
        return (row.Uuid, row.Name);
    }

    /// <summary>Keep what a lookup answered, replacing whatever was held for that uuid.</summary>
    public async Task KeepAsync(string uuid, string name, CancellationToken ct = default)
    {
        if (uuid.Length == 0 || name.Length == 0) return;
        var now = DateTime.UtcNow;
        var updated = await db.MinecraftPlayers.Where(player => player.Uuid == uuid)
            .Set(player => player.Name, name).Set(player => player.FetchedAt, now)
            .UpdateAsync(ct);
        if (updated == 0)
            await db.InsertAsync(new MinecraftPlayerRow { Uuid = uuid, Name = name, FetchedAt = now }, token: ct);
    }
}
