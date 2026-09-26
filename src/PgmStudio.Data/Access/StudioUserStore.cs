using LinqToDB;
using LinqToDB.Async;
using PgmStudio.Data.Schema;

namespace PgmStudio.Data.Access;

/// <summary>
/// The whitelist: who may write to the studio, and in which role. Kept by Minecraft uuid, the identity the
/// studio already credits authors under, so a person's role and their authorship name the same account.
/// </summary>
public sealed class StudioUserStore(PgmDb db)
{
    public Task<List<StudioUserRow>> ListAsync(CancellationToken ct = default) =>
        db.StudioUsers.OrderBy(user => user.Name).ToListAsync(ct);

    public Task<StudioUserRow?> GetAsync(string uuid, CancellationToken ct = default) =>
        db.StudioUsers.FirstOrDefaultAsync(user => user.Uuid == uuid, ct);

    /// <summary>Put a person on the whitelist in <paramref name="role"/>, or change the role of one already
    /// on it. Answers the row as stored.</summary>
    public async Task<StudioUserRow> PutAsync(string uuid, string name, string role, CancellationToken ct = default)
    {
        var updated = await db.StudioUsers.Where(user => user.Uuid == uuid)
            .Set(user => user.Name, name).Set(user => user.Role, role)
            .UpdateAsync(ct);
        if (updated == 0)
            await db.InsertAsync(new StudioUserRow
            {
                Uuid = uuid, Name = name, Role = role, CreatedAt = DateTime.UtcNow,
            }, token: ct);
        return (await GetAsync(uuid, ct))!;
    }

    /// <summary>Take a person off the whitelist. False where they were not on it.</summary>
    public async Task<bool> RemoveAsync(string uuid, CancellationToken ct = default) =>
        await db.StudioUsers.Where(user => user.Uuid == uuid).DeleteAsync(ct) > 0;

    /// <summary>Open an invitation for the person: store the hash of its code and when it lapses, replacing any
    /// invitation already open. False where the person is not on the whitelist.</summary>
    public async Task<bool> OpenInviteAsync(
        string uuid, string inviteHash, DateTime expiresAt, CancellationToken ct = default) =>
        await db.StudioUsers.Where(user => user.Uuid == uuid)
            .Set(user => user.InviteHash, inviteHash).Set(user => user.InviteExpiresAt, expiresAt)
            .UpdateAsync(ct) > 0;

    /// <summary>The person an open, unlapsed invitation is for, or null.</summary>
    public Task<StudioUserRow?> GetByInviteAsync(string inviteHash, DateTime now, CancellationToken ct = default) =>
        db.StudioUsers.FirstOrDefaultAsync(
            user => user.InviteHash == inviteHash && user.InviteExpiresAt > now, ct);

    public Task<StudioUserRow?> GetByDiscordAsync(string discordId, CancellationToken ct = default) =>
        db.StudioUsers.FirstOrDefaultAsync(user => user.DiscordId == discordId, ct);

    /// <summary>Bind a Discord account to the person and close their invitation. A Discord account signs in as
    /// one person, so a binding it held to anyone else is released first.</summary>
    public async Task BindDiscordAsync(string uuid, string discordId, CancellationToken ct = default)
    {
        await db.StudioUsers.Where(user => user.DiscordId == discordId && user.Uuid != uuid)
            .Set(user => user.DiscordId, (string?)null).UpdateAsync(ct);
        await db.StudioUsers.Where(user => user.Uuid == uuid)
            .Set(user => user.DiscordId, discordId)
            .Set(user => user.InviteHash, (string?)null).Set(user => user.InviteExpiresAt, (DateTime?)null)
            .UpdateAsync(ct);
    }

    /// <summary>Whether <paramref name="uuid"/> is credited as an author of the map — a contributor is
    /// credited too, and is not one.</summary>
    public Task<bool> IsAuthorOfAsync(long mapId, string uuid, CancellationToken ct = default) =>
        db.Authors.AnyAsync(author => author.MapId == mapId && author.Uuid == uuid && author.Role == "author", ct);
}
