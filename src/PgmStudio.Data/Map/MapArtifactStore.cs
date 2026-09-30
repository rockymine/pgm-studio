using System.Text.Json;
using LinqToDB;
using LinqToDB.Async;
using PgmStudio.Data.Schema;

namespace PgmStudio.Data.Map;

/// <summary>
/// The one place the <c>map_artifact</c> table is read and written. Every artifact — the scanned layer,
/// the detected islands, the scan configuration, the authoring intent, the sketch and plan blobs, the
/// editor's region drafts — is one row keyed by <c>(map_id, kind)</c>, so one store keyed on
/// <see cref="ArtifactKind"/> answers for all of them and no caller writes the query itself.
/// <para>A kind holds at most one row per map: a save <b>replaces</b> (delete then insert), and a caller
/// that means "there is no longer one" calls <see cref="DeleteAsync"/> rather than saving an empty blob.
/// JSON is read and written with web defaults (camelCase properties, case-insensitive reads, verbatim
/// dictionary keys), which is what the browser sends and what every stored document already carries.</para>
/// </summary>
public sealed class MapArtifactStore(PgmDb db)
{
    /// <summary>The serializer every JSON artifact is stored under — web defaults, so a blob written by
    /// the Blazor client and one written here are the same document.</summary>
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly MapChangeLog log = new(db);

    /// <summary>The change each map's kept documents are being written under by this store's caller, so the
    /// documents one request writes land as one change.</summary>
    private readonly Dictionary<long, (long Id, long Number)> opened = [];

    /// <summary>Who the writes through this store are, recorded on every change they open. The API stamps
    /// each request with its caller; a store nobody stamped writes <see cref="ChangeStamp.Unknown"/>.</summary>
    public ChangeStamp Stamp { get; set; } = ChangeStamp.Unknown;

    /// <summary>The artifact's raw bytes, or null when the map holds none of that kind.</summary>
    public async Task<byte[]?> LoadAsync(long mapId, string kind, CancellationToken ct = default)
        => (await RowAsync(mapId, kind, ct))?.Data;

    /// <summary>The artifact deserialized, or null when the map holds none of that kind.</summary>
    public async Task<T?> LoadJsonAsync<T>(long mapId, string kind, CancellationToken ct = default)
        => await LoadAsync(mapId, kind, ct) is { } data ? JsonSerializer.Deserialize<T>(data, Json) : default;

    /// <summary>The artifact deserialized, or a fresh empty document when the map holds none of that kind —
    /// the shape for artifacts whose absence means "nothing stated yet" rather than "not applicable".</summary>
    public async Task<T> LoadJsonOrEmptyAsync<T>(long mapId, string kind, CancellationToken ct = default)
        where T : new()
        => await LoadJsonAsync<T>(mapId, kind, ct) ?? new T();

    /// <summary>Whether the map holds this artifact. Several answers hang off presence alone — a map is
    /// intent-authored iff it has an intent, sketch-origin iff it has a sketch layout, scanned iff it has a
    /// layer — so this asks the question without loading the blob.</summary>
    public Task<bool> HasAsync(long mapId, string kind, CancellationToken ct = default)
        => db.Artifacts.AnyAsync(a => a.MapId == mapId && a.Kind == kind, ct);

    /// <summary>The revision the map's artifact of this kind is at, or null when it holds none. What a read
    /// answers as its <c>ETag</c> and what a later write states it was written against.</summary>
    public async Task<long?> RevisionAsync(long mapId, string kind, CancellationToken ct = default)
        => (await db.Artifacts.Where(a => a.MapId == mapId && a.Kind == kind)
                              .Select(a => (long?)a.Revision).FirstOrDefaultAsync(ct));

    /// <summary>Replace the map's artifact of this kind with these bytes and answer the revision it is now
    /// at, in one write — a fault between the delete and the insert would otherwise leave the map holding
    /// neither the old document nor the new. A kept document (<see cref="ArtifactKind.Kept"/>) is written as a
    /// change the map keeps, and its revision is that change's number.</summary>
    public async Task<long> SaveAsync(long mapId, string kind, byte[] data, CancellationToken ct = default)
    {
        if (ArtifactKind.Kept.Contains(kind)) return (await SaveKeptAsync(mapId, kind, data, null, ct))!.Value;

        var revision = 1L;
        await db.InOneWriteAsync(async () =>
        {
            revision = (await RevisionAsync(mapId, kind, ct) ?? 0) + 1;
            await DeleteAsync(mapId, kind, ct);
            await db.InsertAsync(
                new MapArtifactRow { MapId = mapId, Kind = kind, Data = data, Revision = revision }, token: ct);
        }, ct);
        return revision;
    }

    /// <summary>
    /// Replace the artifact only if it is still at <paramref name="expected"/>, answering the revision it is
    /// now at — or null where it is not, which is a write against a document somebody else has already
    /// replaced.
    ///
    /// <para>One statement, because the answer has to be true at the moment it is acted on. Reading the
    /// revision and then writing is two, and two callers can both read the same one and both write; the
    /// revision is in the <c>where</c> so the database decides, and the second writer's update matches no
    /// row.</para>
    ///
    /// <para>A map holding no artifact of this kind also matches nothing, which is the right answer to a
    /// caller claiming to have read one.</para>
    /// </summary>
    public async Task<long?> SaveIfUnchangedAsync(
        long mapId, string kind, byte[] data, long expected, CancellationToken ct = default)
    {
        if (ArtifactKind.Kept.Contains(kind)) return await SaveKeptAsync(mapId, kind, data, expected, ct);

        var written = await db.Artifacts
            .Where(a => a.MapId == mapId && a.Kind == kind && a.Revision == expected)
            .Set(a => a.Data, data)
            .Set(a => a.Revision, a => a.Revision + 1)
            .UpdateAsync(ct);
        return written == 0 ? null : expected + 1;
    }

    /// <summary>Write a kept document under the change this caller has open on the map, or a new one, and
    /// answer the change's number — or null where <paramref name="expected"/> names a revision the document is
    /// no longer at. A stale write opens no change: the revision is compared before one is opened, and again
    /// in the update's <c>where</c> for a writer that got there in between, whose change is rolled back with
    /// it.</summary>
    private async Task<long?> SaveKeptAsync(
        long mapId, string kind, byte[] data, long? expected, CancellationToken ct)
    {
        if (expected is { } stated && await RevisionAsync(mapId, kind, ct) != stated) return null;
        try
        {
            return await db.InOneWriteAsync(async () =>
            {
                var change = await ChangeOfAsync(mapId, ct);
                if (expected is { } read)
                {
                    var written = await db.Artifacts
                        .Where(a => a.MapId == mapId && a.Kind == kind && a.Revision == read)
                        .Set(a => a.Data, data)
                        .Set(a => a.Revision, change.Number)
                        .UpdateAsync(ct);
                    if (written == 0) throw new OvertakenWrite();
                }
                else
                {
                    await DeleteAsync(mapId, kind, ct);
                    await db.InsertAsync(
                        new MapArtifactRow { MapId = mapId, Kind = kind, Data = data, Revision = change.Number },
                        token: ct);
                }
                await log.KeepAsync(change.Id, kind, data, ct);
                return (long?)change.Number;
            }, ct);
        }
        catch (OvertakenWrite)
        {
            opened.Remove(mapId);
            return null;
        }
    }

    /// <summary>The change this caller's writes to the map land under: the one already open where it is still
    /// stored, else a new one on the map's slug.</summary>
    private async Task<(long Id, long Number)> ChangeOfAsync(long mapId, CancellationToken ct)
    {
        if (opened.TryGetValue(mapId, out var open) && await log.ExistsAsync(open.Id, ct)) return open;
        var slug = await db.Maps.Where(m => m.Id == mapId).Select(m => m.Slug).FirstAsync(ct);
        open = await log.OpenAsync(slug, Stamp, ct);
        opened[mapId] = open;
        return open;
    }

    /// <summary>A guarded write another writer overtook between the revision check and the update.</summary>
    private sealed class OvertakenWrite : Exception;

    /// <summary>Replace the map's artifact of this kind with this document, serialized.</summary>
    public Task SaveJsonAsync<T>(long mapId, string kind, T value, CancellationToken ct = default)
        => SaveAsync(mapId, kind, JsonSerializer.SerializeToUtf8Bytes(value, Json), ct);

    /// <summary>Drop the map's artifact of this kind, if it has one.</summary>
    public Task DeleteAsync(long mapId, string kind, CancellationToken ct = default)
        => db.Artifacts.Where(a => a.MapId == mapId && a.Kind == kind).DeleteAsync(ct);

    /// <summary>Which kinds the map holds — the "which layers does this map have" question.</summary>
    public Task<List<string>> KindsAsync(long mapId, CancellationToken ct = default)
        => db.Artifacts.Where(a => a.MapId == mapId).Select(a => a.Kind).Distinct().ToListAsync(ct);

    /// <summary>The maps holding each of these kinds, one set per kind (kinds nobody holds map to an empty
    /// set). One query for the whole list, because the map list asks about three kinds at once.</summary>
    public async Task<Dictionary<string, HashSet<long>>> HoldersAsync(
        IReadOnlyList<string> kinds, CancellationToken ct = default)
    {
        var rows = await db.Artifacts.Where(a => kinds.Contains(a.Kind))
            .Select(a => new { a.MapId, a.Kind }).Distinct().ToListAsync(ct);
        var byKind = rows.GroupBy(a => a.Kind).ToDictionary(g => g.Key, g => g.Select(a => a.MapId).ToHashSet());
        foreach (var kind in kinds) byKind.TryAdd(kind, []);
        return byKind;
    }

    /// <summary>How many maps hold this kind.</summary>
    public Task<int> HolderCountAsync(string kind, CancellationToken ct = default)
        => db.Artifacts.Where(a => a.Kind == kind).Select(a => a.MapId).Distinct().CountAsync(ct);

    private Task<MapArtifactRow?> RowAsync(long mapId, string kind, CancellationToken ct)
        => db.Artifacts.FirstOrDefaultAsync(a => a.MapId == mapId && a.Kind == kind, ct);
}
