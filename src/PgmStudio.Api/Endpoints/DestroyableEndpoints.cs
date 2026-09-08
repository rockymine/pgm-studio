using FastEndpoints;
using PgmStudio.Contracts;
using PgmStudio.Data.Features;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Domain;
using PgmStudio.Geom;

namespace PgmStudio.Api.Endpoints;

/// <summary>
/// GET /api/map/{slug}/destroyable-suggestions[?box=x0,y0,z0,x1,y1,z1] — the destroyables the ingest scan
/// proposed for this map, read straight out of <c>destroyable_candidate</c>. <c>box</c> is optional and
/// narrows the list to masses intersecting it, which is how the configure step scopes a detect to the area
/// the author drew.
///
/// <para>Each proposal carries the two readings that produced it rather than a score, because a destroyable
/// is not self-evident the way a core is: a sealed lava container is a core, while a mass of ender stone is a
/// goal only relative to what surrounds it. About one proposal in two is real
/// (docs/world-scan/objective-suggestion.md §3), so this is a list to confirm from and the readings are what
/// a person confirms against — how alone the mass is, and how far it stands above the ground around it.</para>
///
/// <para>The response also carries the generator's <b>defaults</b> and the two vocabularies a picker offers.
/// A destroyable placed by hand has no suggestion to read its style, material and float from, and the client
/// cannot reach <see cref="ObjectiveDefaults"/> or <see cref="DestroyableMaterials"/> — so the one source of
/// truth is served rather than duplicated on the far side of the wire, where it would drift out of step with
/// the stamper silently.</para>
/// </summary>
public sealed class DestroyableSuggestionsEndpoint(MapRepository repo, PgmDb db)
    : EndpointWithoutRequest<DestroyableSuggestionsDto>
{
    public override void Configure()
    {
        Get("/map/{slug}/destroyable-suggestions"); AllowAnonymous(); Description(b => b.Refuses(404));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;

        // Stated and unreadable is a refusal; absent is no filter. Skipping an unreadable filter would answer
        // every mass the map has under a 200, which reads as "this volume holds them all".
        var stated = HttpContext.Request.Query["box"].ToString();
        if (stated.Length > 0 && !BlockBox.TryParse(stated, out _))
        {
            await Refusals.UnreadableAsync(HttpContext, "box unreadable",
                "the volume to filter by is stated as box=x0,y0,z0,x1,y1,z1; leave it off to read them all",
                ct, field: "box");
            return;
        }

        var destroyables = await DestroyableCandidateStore.ReadAsync(db, map.Id, ct);
        if (BlockBox.TryParse(stated, out var box))
            destroyables = [.. destroyables.Where(d => d.Structure.Intersects(box))];

        await Send.OkAsync(new DestroyableSuggestionsDto(
            new DestroyableDefaultsDto(
                DestroyableStyles.Slug(ObjectiveDefaults.Style),
                ObjectiveDefaults.Materials,
                ObjectiveDefaults.DestroyableFloat,
                DestroyableStyles.All,
                DestroyableMaterials.All),
            [.. destroyables.Select(d => new DestroyableSuggestionDto(
                new SuggestedBoxDto(d.Structure.MinX, d.Structure.MinY, d.Structure.MinZ,
                    d.Structure.MaxX, d.Structure.MaxY, d.Structure.MaxZ),
                d.Materials, d.Blocks, d.SameNearby, d.Elevation))]), ct);
    }
}
