using FastEndpoints;
using PgmStudio.Api.Services;
using PgmStudio.Contracts;
using PgmStudio.Data.Map;
using PgmStudio.Domain;
using PgmStudio.Minecraft.Render;

namespace PgmStudio.Api.Endpoints;

/// <summary>GET /api/map/{slug}/report — everything a drive reads back about a stored map, off one build
/// (<see cref="MapReport"/>): the three numbers, every reading with the route that answers it alone, and the
/// pictures by the routes that draw them. <c>?pictures=true</c> draws the pictures too; <c>?format=text</c>
/// answers the whole report as one document. 404 where the map holds no sketch layout.</summary>
[Queued]
internal sealed class MapReportEndpoint(MapRepository repo, MapReport report) : EndpointWithoutRequest<MapReportDto>
{
    public override void Configure()
    {
        Get("/map/{slug}/report");
        Summary(s => s.Summary = WorldReadCatalog.Sentence("report"));
        Description(b => b.AlsoText().Refuses(404).Reads(
            new QueryWord("pictures", "Draw every picture the report names, as base64 PNG beside its route. Absent "
                + "names them by route alone, each drawn on asking for it. Ignored by `?format=text`.",
                Value: QueryValue.Flag)));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;

        var asText = TextAnswer.Wanted(HttpContext);
        var pictures = !asText && Query<bool?>("pictures", isRequired: false) == true;
        if (await report.OfAsync(map, pictures, ct) is not { } answer)
        {
            await Refusals.WriteAsync(HttpContext, 404, "no world to read",
                [new Vocabulary.Finding(PgmStudio.Pgm.Sketch.SketchRules.NothingStored,
                    "this map has no stored sketch layout, so there is no world for the studio to build and "
                    + "report on")], ct);
            return;
        }

        if (asText)
        {
            await TextAnswer.WriteAsync(HttpContext, MapReport.Text(answer), ct);
            return;
        }
        await Send.OkAsync(answer, ct);
    }
}
