using LinqToDB;
using LinqToDB.Async;
using PgmStudio.Data.Schema;

namespace PgmStudio.Api.Services;

using Dict = Dictionary<string, object?>;

/// <summary>
/// A map's identity: what it is called, what version it states, what it is played for, how high it may be
/// built, and who wrote it.
///
/// <para><b>Only what the request named.</b> Every field is optional and an absent one is left alone, which
/// is what lets a rename be one call rather than a read-modify-write of the whole row — so the payload is
/// asked whether it <em>holds</em> each key rather than what its value is.</para>
///
/// <para><b>The authors are a replace and the rest are a patch</b>, and the two happen in one transaction so
/// a map is never briefly credited to nobody. Who counts as a person is <see cref="MapAuthors"/>'s rule, the
/// same one the load from documents states them by.</para>
///
/// <para>The <c>gamemode</c> column is not writable here: it holds the author's original
/// <c>&lt;gamemode&gt;</c> label, round-tripped as written, while the gamemode itself is derived from the
/// map's objective modules and so cannot be set by hand.</para>
/// </summary>
public static class MapMetadata
{
    public static async Task ApplyAsync(PgmDb db, long mapId, Dict stated, CancellationToken ct)
    {
        await using var transaction = await db.BeginTransactionAsync(ct);

        var update = db.Maps.Where(map => map.Id == mapId).AsUpdatable();
        if (stated.ContainsKey("name"))
            update = update.Set(map => map.Name, stated["name"] as string ?? "");
        if (stated.ContainsKey("version"))
            update = update.Set(map => map.Version, NullIfEmpty(stated["version"] as string));
        if (stated.ContainsKey("objective"))
            update = update.Set(map => map.Objective, NullIfEmpty(stated["objective"] as string));
        if (stated.ContainsKey("max_build_height"))
            update = update.Set(map => map.MaxBuildHeight,
                stated["max_build_height"] is { } height ? Convert.ToDouble(height) : null);
        update = update.Set(map => map.UpdatedAt, DateTime.UtcNow);
        await update.UpdateAsync(ct);

        if (stated.TryGetValue("authors", out var raw) && raw is List<object?> authors)
            await MapAuthors.ReplaceAsync(db, mapId, authors, ct);

        await transaction.CommitAsync(ct);
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
