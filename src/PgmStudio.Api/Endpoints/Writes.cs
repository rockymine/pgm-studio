using PgmStudio.Data.Map;

namespace PgmStudio.Api.Endpoints;

using Dict = Dictionary<string, object?>;

/// <summary>
/// A write that replaces a whole document, guarded by the revision the caller read.
///
/// <para>Three things travel together and none of them is the endpoint's to remember: whether the request
/// stated an <c>If-Match</c>, whether the store still holds that revision, and what revision the caller now
/// holds. Left to each endpoint they would be three things to forget, and forgetting the last one is silent
/// — the write lands, no <c>ETag</c> comes back, and the caller's next write is unguarded without anything
/// saying so. So the whole sentence is one call, and an endpoint's duty is to stop when it answers
/// nothing.</para>
///
/// <para>The refusal and why it is <c>RQ5</c> at 409 rather than a bare 412 are
/// <see cref="Revisions"/>'s.</para>
/// </summary>
internal static class Writes
{
    /// <summary>Store the artifact, answering the revision it is now at — or null where the request guarded
    /// against a revision the store no longer holds, which is the caller's to refuse.</summary>
    public static async Task<long?> StoreAsync(
        HttpContext http, MapArtifactStore artifacts, long mapId, string kind, byte[] data, CancellationToken ct)
        => Answered(http, Revisions.Expected(http) is { } expected
            ? await artifacts.SaveIfUnchangedAsync(mapId, kind, data, expected, ct)
            : await artifacts.SaveAsync(mapId, kind, data, ct));

    /// <summary>The same for the map document itself — the entities an edit rewrites, rather than a stored
    /// blob. <paramref name="guarded"/> says whether the request's <c>If-Match</c> names this document; see
    /// <see cref="WriteSupport.RunEditAsync"/> for the one caller it is false for.</summary>
    public static async Task<long?> StoreDocAsync(
        HttpContext http, MapWriter writer, long mapId, Dict doc, bool guarded, CancellationToken ct)
        => Answered(http, guarded && Revisions.Expected(http) is { } expected
            ? await writer.SaveDocIfUnchangedAsync(mapId, doc, expected, ct)
            : await writer.SaveDocAsync(mapId, doc, ct));

    /// <summary>A write that landed says so in the one header a caller reads it from, and one that did not
    /// leaves the response alone for the refusal to fill.</summary>
    private static long? Answered(HttpContext http, long? revision)
    {
        if (revision is { } landed) Revisions.Answer(http, landed);
        return revision;
    }
}
