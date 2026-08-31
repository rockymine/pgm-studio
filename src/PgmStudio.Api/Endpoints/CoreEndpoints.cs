using FastEndpoints;
using PgmStudio.Contracts;
using PgmStudio.Data.Features;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Domain;

namespace PgmStudio.Api.Endpoints;

using Dict = Dictionary<string, object?>;
using PgmStudio.Geom;

/// <summary>
/// GET /api/map/{slug}/core-suggestions[?box=x0,y0,z0,x1,y1,z1] — the cores the ingest scan proposed for this
/// map, read straight out of <c>core_candidate</c>. No world access and no scoring pass: a core's signature
/// is unambiguous enough that the gather already knew the casing, so the stored row <i>is</i> the suggestion
/// (docs/world-scan/objective-suggestion.md §2). <c>box</c> is optional and narrows the list to casings
/// intersecting it, which is how the configure step scopes a detect to the area the author drew.
///
/// <para>The response also carries the generator's casing <b>defaults</b>. A core placed by hand has no
/// suggestion to read its size, shell, float and leak from, and the client cannot reach
/// <see cref="ObjectiveDefaults"/> — so the one source of truth for those numbers is served rather than
/// duplicated on the far side of the wire, where it would drift out of step with the stamper silently.</para>
/// </summary>
public sealed class CoreSuggestionsEndpoint(MapRepository repo, PgmDb db)
    : EndpointWithoutRequest<CoreSuggestionsDto>
{
    public override void Configure() { Get("/map/{slug}/core-suggestions"); AllowAnonymous(); Description(b => b.Refuses(404)); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;

        // The filter is optional and an unreadable one is not the same as an absent one: skipping the filter
        // on a box that cannot be read answers every casing the map has under a 200, which reads as "this
        // volume holds them all". Stated and unreadable is a refusal; absent is no filter.
        var stated = HttpContext.Request.Query["box"].ToString();
        if (stated.Length > 0 && !BlockBox.TryParse(stated, out _))
        {
            await Refusals.UnreadableAsync(HttpContext, "box unreadable",
                "the volume to filter by is stated as box=x0,y0,z0,x1,y1,z1; leave it off to read them all",
                ct, field: "box");
            return;
        }

        var cores = await CoreCandidateStore.ReadAsync(db, map.Id, ct);
        if (BlockBox.TryParse(stated, out var box))
            cores = [.. cores.Where(core => core.Casing.Intersects(box))];

        await Send.OkAsync(new CoreSuggestionsDto(
            new CoreDefaultsDto(ObjectiveDefaults.CoreLava, ObjectiveDefaults.CoreLavaHeight,
                ObjectiveDefaults.CoreFloat, ObjectiveDefaults.CoreLeak),
            [.. cores.Select(core => new CoreSuggestionDto(
                new CoreBoxDto(core.Casing.MinX, core.Casing.MinY, core.Casing.MinZ,
                    core.Casing.MaxX, core.Casing.MaxY, core.Casing.MaxZ),
                // The casing as the author edits it: width/depth and height come off the box, so a confirmed
                // suggestion states the structure that is actually there rather than the generator's default.
                Math.Max(core.Casing.Width, core.Casing.Depth), core.Casing.Height,
                core.Shell, core.Float, core.OpenTop, core.LavaBlocks))]), ct);
    }

}
