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

    /// <summary>Replace the map's artifact of this kind with these bytes, in one write — a fault between the
    /// delete and the insert would otherwise leave the map holding neither the old document nor the new.</summary>
    public Task SaveAsync(long mapId, string kind, byte[] data, CancellationToken ct = default)
        => db.InOneWriteAsync(async () =>
        {
            await DeleteAsync(mapId, kind, ct);
            await db.InsertAsync(new MapArtifactRow { MapId = mapId, Kind = kind, Data = data }, token: ct);
        }, ct);

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
