using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using LinqToDB;
using LinqToDB.Async;
using PgmStudio.Data.Schema;

namespace PgmStudio.Data.Map;

/// <summary>Who a write to a map's documents is and what they said about it: the account, the token's label
/// where a token wrote it, the origin a caller states — the repository, commit and folder its documents were
/// built from, as JSON — a note, and the earlier changes the write drops.</summary>
public sealed record ChangeStamp(
    string? WriterUuid = null, string? WriterName = null, string? TokenLabel = null,
    string? OriginJson = null, string? Note = null, IReadOnlyList<long>? Discarded = null)
{
    /// <summary>A write nobody signed: the importer, or a request with no account behind it.</summary>
    public static readonly ChangeStamp Unknown = new();
}

/// <summary>One change to a map's documents: its number, when it landed, who wrote it and what they said, the
/// kinds of document it wrote, and the earlier changes it dropped.</summary>
public sealed record MapChange(
    long Number, DateTime At, string? WriterUuid, string? WriterName, string? TokenLabel, string? OriginJson,
    string? Note, IReadOnlyList<string> Kinds, IReadOnlyList<long> Discarded);

/// <summary>
/// Every write to a map's documents, kept. A change is numbered per slug and the number never repeats — not
/// across a reload, which replaces the map's row, and not across a delete, after which a map recreated under
/// the slug numbers on — so the revision a kept document answers as its <c>ETag</c> names one board.
///
/// <para>The documents are kept once each under the SHA-256 of their bytes, gzipped: a change that rewrote a
/// document without changing it costs a row, not a copy. <see cref="MapArtifactStore"/> writes the log as it
/// writes a kept document (<see cref="ArtifactKind.Kept"/>); this reads it and forgets it.</para>
/// </summary>
public sealed class MapChangeLog(PgmDb db)
{
    /// <summary>The longest note a change keeps, in characters: a sentence or a few, never a report.</summary>
    public const int NoteLength = 1000;

    /// <summary>A new change on <paramref name="slug"/>, stamped, under the slug's next number.</summary>
    public async Task<(long Id, long Number)> OpenAsync(string slug, ChangeStamp stamp, CancellationToken ct = default)
    {
        await db.MapChangeSequences.InsertOrUpdateAsync(
            () => new MapChangeSequenceRow { MapSlug = slug, LastNumber = 1 },
            row => new MapChangeSequenceRow { LastNumber = row.LastNumber + 1 },
            ct);
        var number = await db.MapChangeSequences.Where(row => row.MapSlug == slug)
            .Select(row => row.LastNumber).FirstAsync(ct);
        var id = await db.InsertWithInt64IdentityAsync(new MapChangeRow
        {
            MapSlug = slug, Number = number, CreatedAt = DateTime.UtcNow,
            WriterUuid = stamp.WriterUuid, WriterName = stamp.WriterName, TokenLabel = stamp.TokenLabel,
            OriginJson = stamp.OriginJson, Note = stamp.Note,
            DiscardedJson = stamp.Discarded is { Count: > 0 } discarded ? JsonSerializer.Serialize(discarded) : null,
        }, token: ct);
        return (id, number);
    }

    /// <summary>Whether the change is stored — false for one whose write was rolled back.</summary>
    public Task<bool> ExistsAsync(long changeId, CancellationToken ct = default)
        => db.MapChanges.AnyAsync(change => change.Id == changeId, ct);

    /// <summary>Record that the change wrote <paramref name="data"/> as its document of this kind. A change
    /// writing one kind twice keeps the second.</summary>
    public async Task KeepAsync(long changeId, string kind, byte[] data, CancellationToken ct = default)
    {
        var hash = Convert.ToHexStringLower(SHA256.HashData(data));
        if (!await db.DocumentBlobs.AnyAsync(blob => blob.Hash == hash, ct))
            await db.InsertOrReplaceAsync(
                new DocumentBlobRow { Hash = hash, Data = Gzip(data), Length = data.Length }, token: ct);
        await db.InsertOrReplaceAsync(
            new MapChangeDocumentRow { ChangeId = changeId, Kind = kind, BlobHash = hash }, token: ct);
    }

    /// <summary>The number of the slug's latest change, or 0 where it has none.</summary>
    public async Task<long> LatestAsync(string slug, CancellationToken ct = default) =>
        await db.MapChanges.Where(change => change.MapSlug == slug)
            .Select(change => (long?)change.Number).MaxAsync(ct) ?? 0;

    /// <summary>For each slug the person named here wrote to themselves, in a browser and not through a token,
    /// when they last did: by account where <paramref name="uuid"/> is given, else by
    /// <paramref name="name"/> among the writes no account signed, which is how the local admin of an open
    /// studio writes.</summary>
    public async Task<IReadOnlyDictionary<string, DateTime>> LastWrittenByAsync(
        string? uuid, string? name, CancellationToken ct = default)
    {
        if (uuid is null && name is null) return new Dictionary<string, DateTime>();
        var own = db.MapChanges.Where(change => change.TokenLabel == null);
        own = uuid is not null
            ? own.Where(change => change.WriterUuid == uuid)
            : own.Where(change => change.WriterUuid == null && change.WriterName == name);
        var latest = await own.GroupBy(change => change.MapSlug)
            .Select(group => new { Slug = group.Key, At = group.Max(change => change.CreatedAt) })
            .ToListAsync(ct);
        return latest.ToDictionary(row => row.Slug, row => DateTime.SpecifyKind(row.At, DateTimeKind.Utc));
    }

    /// <summary>The slug's changes, oldest first.</summary>
    public async Task<IReadOnlyList<MapChange>> ListAsync(string slug, CancellationToken ct = default)
    {
        var changes = await db.MapChanges.Where(change => change.MapSlug == slug)
            .OrderBy(change => change.Number).ToListAsync(ct);
        var ids = changes.Select(change => change.Id).ToList();
        var kinds = (await db.MapChangeDocuments.Where(document => ids.Contains(document.ChangeId)).ToListAsync(ct))
            .ToLookup(document => document.ChangeId, document => document.Kind);
        return
        [
            .. changes.Select(change => new MapChange(
                change.Number, change.CreatedAt, change.WriterUuid, change.WriterName, change.TokenLabel,
                change.OriginJson, change.Note, [.. kinds[change.Id].Order(StringComparer.Ordinal)],
                change.DiscardedJson is { } discarded ? JsonSerializer.Deserialize<long[]>(discarded) ?? [] : [])),
        ];
    }

    /// <summary>The slug's kept documents as they stood at change <paramref name="number"/>: for each kind, what
    /// the latest change at or before it wrote. Empty where the slug has no change that early.</summary>
    public async Task<IReadOnlyDictionary<string, byte[]>> DocumentsAtAsync(
        string slug, long number, CancellationToken ct = default)
    {
        var written = await (
            from change in db.MapChanges
            join document in db.MapChangeDocuments on change.Id equals document.ChangeId
            where change.MapSlug == slug && change.Number <= number
            select new { document.Kind, change.Number, document.BlobHash }).ToListAsync(ct);
        var latest = written.GroupBy(row => row.Kind)
            .ToDictionary(group => group.Key, group => group.MaxBy(row => row.Number)!.BlobHash);
        var hashes = latest.Values.Distinct().ToList();
        var blobs = await db.DocumentBlobs.Where(blob => hashes.Contains(blob.Hash))
            .ToDictionaryAsync(blob => blob.Hash, blob => Gunzip(blob.Data), ct);
        return latest.ToDictionary(pair => pair.Key, pair => blobs[pair.Value]);
    }

    /// <summary>Forget the slug's history, and every kept document no change still names. The slug's last
    /// number stays, so a map made again under it does not answer a revision the forgotten one did.</summary>
    public async Task ForgetAsync(string slug, CancellationToken ct = default)
    {
        await db.MapChanges.Where(change => change.MapSlug == slug).DeleteAsync(ct);
        await db.DocumentBlobs
            .Where(blob => !db.MapChangeDocuments.Any(document => document.BlobHash == blob.Hash))
            .DeleteAsync(ct);
    }

    private static byte[] Gzip(byte[] data)
    {
        using var packed = new MemoryStream();
        using (var gzip = new GZipStream(packed, CompressionLevel.Optimal, leaveOpen: true)) gzip.Write(data);
        return packed.ToArray();
    }

    private static byte[] Gunzip(byte[] data)
    {
        using var gzip = new GZipStream(new MemoryStream(data), CompressionMode.Decompress);
        using var unpacked = new MemoryStream();
        gzip.CopyTo(unpacked);
        return unpacked.ToArray();
    }
}
