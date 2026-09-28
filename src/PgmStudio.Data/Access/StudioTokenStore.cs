using LinqToDB;
using LinqToDB.Async;
using PgmStudio.Data.Schema;

namespace PgmStudio.Data.Access;

/// <summary>
/// The tokens callers without a browser sign in with. A token acts as the person it was issued for and nothing
/// more: what that person may do is read off the whitelist on every request, so a token outlives neither their
/// place on it nor their role.
/// </summary>
public sealed class StudioTokenStore(PgmDb db)
{
    /// <summary>How stale a token's last use may be before a request records it again — one write a minute
    /// rather than one per request.</summary>
    public static readonly TimeSpan UseGrain = TimeSpan.FromMinutes(1);

    /// <summary>A person's tokens, newest first.</summary>
    public Task<List<StudioTokenRow>> ListAsync(string uuid, CancellationToken ct = default) =>
        db.StudioTokens.Where(token => token.UserUuid == uuid).OrderByDescending(token => token.Id).ToListAsync(ct);

    /// <summary>Keep a token for <paramref name="uuid"/> under its hash. Answers the row as stored.</summary>
    public async Task<StudioTokenRow> IssueAsync(string uuid, string hash, string label, CancellationToken ct = default)
    {
        var row = new StudioTokenRow { UserUuid = uuid, Hash = hash, Label = label, CreatedAt = DateTime.UtcNow };
        row.Id = await db.InsertWithInt64IdentityAsync(row, token: ct);
        return row;
    }

    /// <summary>The token stored under <paramref name="hash"/>, or null.</summary>
    public Task<StudioTokenRow?> GetByHashAsync(string hash, CancellationToken ct = default) =>
        db.StudioTokens.FirstOrDefaultAsync(token => token.Hash == hash, ct);

    /// <summary>Record that a token signed a request in, where its last use is older than
    /// <see cref="UseGrain"/>.</summary>
    public async Task TouchAsync(StudioTokenRow token, DateTime now, CancellationToken ct = default)
    {
        if (token.LastUsedAt is { } last && now - last < UseGrain) return;
        await db.StudioTokens.Where(row => row.Id == token.Id).Set(row => row.LastUsedAt, now).UpdateAsync(ct);
    }

    /// <summary>Revoke one of a person's tokens. False where they hold no token by that id.</summary>
    public async Task<bool> RevokeAsync(string uuid, long id, CancellationToken ct = default) =>
        await db.StudioTokens.Where(token => token.Id == id && token.UserUuid == uuid).DeleteAsync(ct) > 0;
}
