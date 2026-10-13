using FastEndpoints;
using PgmStudio.Contracts;
using PgmStudio.Data.Features;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Domain;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Endpoints;

/// <summary>
/// GET /api/map/{slug}/flag-suggestions — the banners the ingest scan found on this map, read straight out of
/// <c>flag_candidate</c>, each a place a flag could start (<c>docs/world-scan/objective-suggestion.md</c> §6).
/// The response also carries what a King of the Flag flag is when the author states nothing, so the one
/// definition — <see cref="ObjectiveDefaults"/> — is served rather than copied into the client.
/// </summary>
public sealed class FlagSuggestionsEndpoint(MapRepository repo, PgmDb db)
    : EndpointWithoutRequest<FlagSuggestionsDto>
{
    public override void Configure()
    {
        Get("/map/{slug}/flag-suggestions");
        Description(b => b.Refuses(404));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;

        var banners = await FlagCandidateStore.ReadAsync(db, map.Id, ct);
        await Send.OkAsync(new FlagSuggestionsDto(
            new FlagDefaultsDto(ObjectiveDefaults.FlagPointsRate, FlagCarriers.Lowered,
                ObjectiveDefaults.FlagCarrierHearts, ObjectiveDefaults.FlagScoreLimit),
            [.. banners.Select(banner => new FlagSuggestionDto(
                banner.WorldX, banner.WorldY, banner.WorldZ, banner.Wall, Yaw(banner.Wall, banner.Facing),
                banner.Color, banner.Patterns))]), ct);
    }

    /// <summary>A banner's facing as a yaw, south 0 and west 90: a standing banner turns in sixteenths of a
    /// circle, and a wall banner faces away from the wall it hangs on (2 north, 3 south, 4 west, 5 east).</summary>
    internal static double Yaw(bool wall, int facing) => wall
        ? facing switch { 2 => 180, 4 => 90, 5 => 270, _ => 0 }
        : facing % 16 * 22.5;
}
