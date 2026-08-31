using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Domain;
using PgmStudio.Vocabulary;
using System.Text.Json;

namespace PgmStudio.Api.Services;

/// <summary>What replacing a stored document came to: a refusal, or the revision the map's copy is now at.
/// <para><b>Revision</b> — What the caller now holds, to guard its next write with.</para></summary>
public sealed record DocumentWritten(Refusal? Refusal, long? Revision = null);

/// <summary>
/// Replacing one of a map's stored documents, guarded by the revision the caller read.
///
/// <para><b>One operation, whichever document.</b> A plan and a sketch layout are stored the same way and
/// refuse the same two things — a body that is not JSON, and a write against a revision the store no longer
/// holds — so the two routes ask this rather than each spelling the sequence out. What differs between them
/// is what the document says about <em>itself</em>: a layout is refused for naming a board too large or a
/// house style the stamper cannot build, a plan is refused for neither. Those gates stay with the document
/// that has them and run before this is called, because a gate that moved here would have to be told which
/// caller it was running for.</para>
///
/// <para><b>The revision crosses as a value, in and out.</b> A caller states the one it read and is handed
/// the one it now holds; that the first arrives in an <c>If-Match</c> and the second leaves in an
/// <c>ETag</c> is the HTTP layer's business and nothing here needs to know it. The body is stored
/// <b>verbatim</b> — the bytes that arrived, not a re-serialization of what was parsed out of them — so a
/// field the reader has nowhere to keep is stored rather than dropped, and whether that is worth saying is
/// the caller's to decide.</para>
/// </summary>
public static class DocumentWrite
{
    public static async Task<DocumentWritten> StoreAsync(
        MapArtifactStore artifacts, long mapId, string kind, string what, byte[] body, long? expected,
        CancellationToken ct)
    {
        try { using var _ = JsonDocument.Parse(body); }
        catch (JsonException fault)
        {
            return new(Refusal.At(400, "invalid JSON",
                new Finding(RequestRules.Unreadable, fault.Message)));
        }

        var landed = expected is { } revision
            ? await artifacts.SaveIfUnchangedAsync(mapId, kind, body, revision, ct)
            : await artifacts.SaveAsync(mapId, kind, body, ct);
        if (landed is not null) return new(null, landed);

        var stored = await artifacts.RevisionAsync(mapId, kind, ct);
        return new(Refusal.At(409, "stale write",
            new Finding(RequestRules.Conflict,
                stored is { } now
                    ? $"this {what} has been replaced since it was read — the If-Match states {expected} and "
                      + $"it is at {now}; read it again and re-apply the change"
                    : $"this map holds no {what} to replace, so the If-Match matches nothing")));
    }
}
