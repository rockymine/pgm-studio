using LinqToDB;
using PgmStudio.Data.Schema;

namespace PgmStudio.Api.Services;

using Dict = Dictionary<string, object?>;

/// <summary>
/// Who a map is credited to. Stating authors is a <b>full replace</b> — the map's rows are wiped and rewritten
/// from what was given — which mirrors the contract, where the <c>&lt;authors&gt;</c> block is rewritten
/// whole rather than appended to.
///
/// <para>PGM takes a person as an <b>account or a pseudonym</b>: a <c>uuid</c> it resolves to a player, or the
/// element's own text. Both halves of the codec keep that rule, so an entry carrying either is an author and
/// only one carrying neither is skipped. A bare string is a pseudonym and nothing else, which is the form an
/// author writes when there is no account behind the name.</para>
/// </summary>
public static class MapAuthors
{
    /// <summary>Replace a map's authors with what the caller stated. Runs inside whatever transaction the
    /// caller opened.</summary>
    public static async Task ReplaceAsync(PgmDb db, long mapId, IEnumerable<object?> stated, CancellationToken ct)
    {
        await db.Authors.Where(row => row.MapId == mapId).DeleteAsync(ct);
        foreach (var entry in stated)
        {
            var (person, pseudonym) = entry is Dict dict ? (dict, "") : (null, (entry as string)?.Trim() ?? "");
            if (person is null && pseudonym.Length == 0) continue;
            var uuid = person is null ? "" : (person.GetValueOrDefault("uuid") as string)?.Trim() ?? "";
            var name = person is null ? pseudonym : (person.GetValueOrDefault("name") as string)?.Trim() ?? "";
            if (uuid.Length == 0 && name.Length == 0) continue;
            await db.InsertAsync(new AuthorRow
            {
                MapId = mapId,
                Uuid = uuid,
                Role = person?.GetValueOrDefault("role") as string == "contributor" ? "contributor" : "author",
                Contribution = person is null
                    ? null
                    : NullIfEmpty((person.GetValueOrDefault("contribution") as string)?.Trim()),
                Name = NullIfEmpty(name),
            }, token: ct);
        }
    }

    private static string? NullIfEmpty(string? text) => string.IsNullOrEmpty(text) ? null : text;
}
