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

    /// <summary>Whether <paramref name="uuid"/> is credited as an author of the map — a contributor is
    /// credited too, and is not one.</summary>
    public Task<bool> IsAuthorOfAsync(long mapId, string uuid, CancellationToken ct = default) =>
        db.Authors.AnyAsync(author => author.MapId == mapId && author.Uuid == uuid && author.Role == "author", ct);
}
