using LinqToDB;
using LinqToDB.Async;
using PgmStudio.Data.Schema;
using PgmStudio.Vocabulary;

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

    /// <summary>Take a person off the whitelist, and revoke every token that acts as them. False where they
    /// were not on it.</summary>
    public async Task<bool> RemoveAsync(string uuid, CancellationToken ct = default)
    {
        await db.StudioTokens.Where(token => token.UserUuid == uuid).DeleteAsync(ct);
        return await db.StudioUsers.Where(user => user.Uuid == uuid).DeleteAsync(ct) > 0;
    }

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

    /// <summary>Put a person who signed themselves up on the whitelist, waiting for approval and already bound to
    /// the Discord account they signed in with. Answers the row as stored.</summary>
    public async Task<StudioUserRow> JoinAsync(
        string uuid, string name, string discordId, string? discordName, CancellationToken ct = default)
    {
        await db.InsertAsync(new StudioUserRow
        {
            Uuid = uuid, Name = name, Role = StudioRoles.Unapproved, CreatedAt = DateTime.UtcNow,
            DiscordId = discordId, DiscordName = discordName,
        }, token: ct);
        return (await GetAsync(uuid, ct))!;
    }

    /// <summary>Keep the Discord name the person signed in with, which an admin recognises them by.</summary>
    public Task NoteDiscordNameAsync(string uuid, string? discordName, CancellationToken ct = default) =>
        db.StudioUsers.Where(user => user.Uuid == uuid).Set(user => user.DiscordName, discordName).UpdateAsync(ct);

    /// <summary>
    /// Move a person on the whitelist from one Minecraft account to another, and everything that names them with
    /// it: their place on the whitelist, their tokens, the maps they own and are credited on, the library rows
    /// they made and the notes they wrote. Answers the ids of the maps whose credits moved, whose intents the
    /// caller projects the new name into. A change's record keeps the account it was written under.
    /// </summary>
    public async Task<IReadOnlyList<long>> ChangeAccountAsync(
        string from, string to, string name, CancellationToken ct = default)
    {
        await using var transaction = await db.BeginTransactionAsync(ct);
        await db.StudioUsers.Where(user => user.Uuid == from).Set(user => user.Uuid, to).Set(user => user.Name, name)
            .UpdateAsync(ct);
        await db.StudioTokens.Where(token => token.UserUuid == from).Set(token => token.UserUuid, to).UpdateAsync(ct);
        await db.Maps.Where(map => map.OwnerUuid == from).Set(map => map.OwnerUuid, to).UpdateAsync(ct);
        var credited = await db.Authors.Where(author => author.Uuid == from).Select(author => author.MapId)
            .Distinct().ToListAsync(ct);
        await db.Authors.Where(author => author.Uuid == from).Set(author => author.Uuid, to)
            .Set(author => author.Name, name).UpdateAsync(ct);
        await db.MapNoteMessages.Where(message => message.AuthorUuid == from).Set(message => message.AuthorUuid, to)
            .UpdateAsync(ct);
        await MoveOwnedAsync(db.Styles, from, to, ct);
        await MoveOwnedAsync(db.BiomePatterns, from, to, ct);
        await MoveOwnedAsync(db.Themes, from, to, ct);
        await MoveOwnedAsync(db.RoomStyles, from, to, ct);
        await MoveOwnedAsync(db.RoofStyles, from, to, ct);
        await MoveOwnedAsync(db.StoreyStyles, from, to, ct);
        await MoveOwnedAsync(db.PorchStyles, from, to, ct);
        await MoveOwnedAsync(db.TreeStyles, from, to, ct);
        await MoveOwnedAsync(db.BoulderStyles, from, to, ct);
        await transaction.CommitAsync(ct);
        return credited;
    }

    private static Task<int> MoveOwnedAsync<TRow>(ITable<TRow> table, string from, string to, CancellationToken ct)
        where TRow : class, ILibraryRow =>
        table.Where(row => row.OwnerUuid == from).Set(row => row.OwnerUuid, to).UpdateAsync(ct);

    /// <summary>Whether <paramref name="uuid"/> is credited as an author of the map — a contributor is
    /// credited too, and is not one.</summary>
    public Task<bool> IsAuthorOfAsync(long mapId, string uuid, CancellationToken ct = default) =>
        db.Authors.AnyAsync(author => author.MapId == mapId && author.Uuid == uuid && author.Role == "author", ct);
}
