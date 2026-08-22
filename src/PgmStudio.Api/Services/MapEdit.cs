using PgmStudio.Data.Map;
using PgmStudio.Domain;
using PgmStudio.Pgm.Editing;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Services;

using Dict = Dictionary<string, object?>;

/// <summary>What one edit to a map's document came to: a refusal, or what the editor produced and the
/// revision the map is now at.</summary>
/// <param name="Result">The editor's own answer — the team it added, the orbit it computed — which the route
/// hands back unchanged.</param>
/// <param name="Revision">What the caller now holds, to guard its next write with. Present only where the
/// write landed.</param>
public sealed record EditApplied(Refusal? Refusal, object? Result = null, long? Revision = null);

/// <summary>
/// One edit to a map's document: load it, apply the change, write it back.
///
/// <para><b>Every edit route is this operation.</b> Thirty-six of them differ only in the editor they hand
/// over to, so what a route contributes is the lambda and the id it reads out of its path — and the loading,
/// the revision guard, the persistence and the four ways an edit can be refused are asked once, here.</para>
///
/// <para><b>The revision crosses as a value.</b> A caller states the one it read and is handed the one it now
/// holds; that the first arrives in an <c>If-Match</c> and the second leaves in an <c>ETag</c> is the HTTP
/// layer's business. <c>null</c> is an unguarded write, and it is what one caller genuinely wants: an intent
/// write states the <em>intent's</em> revision, and the projection that follows rewrites the map, whose
/// revision is a different number — guarding it with the same header would refuse every guarded intent
/// write.</para>
/// </summary>
public static class MapEdit
{
    public static async Task<EditApplied> RunAsync(
        MapRepository repo, MapReader reader, MapWriter writer, string slug,
        Func<Dict, Dict> edit, long? expected, CancellationToken ct)
    {
        var map = await repo.GetBySlugAsync(slug, ct);
        if (map is null)
            return new(Refusal.At(404, "no such map",
                new Finding(RequestRules.NoSuchSubject, $"no map is stored under '{slug}'")));

        var doc = await reader.ReadDocAsync(map, ct);
        try
        {
            var result = edit(doc);
            var landed = expected is { } revision
                ? await writer.SaveDocIfUnchangedAsync(map.Id, doc, revision, ct)
                : await writer.SaveDocAsync(map.Id, doc, ct);
            if (landed is null)
                return new(Stale(expected, await writer.RevisionAsync(map.Id, ct)));
            return new(null, result, landed);
        }
        catch (EditException fault) { return new(new Refusal(fault.Status, fault.Error, [fault.Finding])); }
    }

    /// <summary>A write against a revision the map no longer has: what was expected, what is stored, and what
    /// to do about it — read it again and re-apply, because the studio has no way to merge two whole-document
    /// writes and guessing at one would lose whichever half it guessed against.</summary>
    private static Refusal Stale(long? expected, long? stored) =>
        Refusal.At(409, "stale write",
            new Finding(RequestRules.Conflict,
                stored is { } now
                    ? $"this map has been replaced since it was read — the If-Match states {expected} and it "
                      + $"is at {now}; read it again and re-apply the change"
                    : "this map holds nothing to replace, so the If-Match matches nothing"));
}
