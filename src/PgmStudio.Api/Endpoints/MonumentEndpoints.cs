using FastEndpoints;
using LinqToDB;
using LinqToDB.Async;
using PgmStudio.Contracts;
using PgmStudio.Data.Features;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Domain;
using PgmStudio.Geom;
using PgmStudio.Minecraft;
using PgmStudio.Pgm;

namespace PgmStudio.Api.Endpoints;

using PgmStudio.Minecraft.Suggest;

/// <summary>
/// GET /api/map/{slug}/monument-suggestions?box=x0,y0,z0,x1,y1,z1[&amp;style=pedestal,label,cap] — score the
/// pre-gathered <c>monument_candidate</c> rows inside the author's box for the declared style (F9). No
/// world access: loads the candidates and runs <c>MonumentSuggester.Score</c>. <c>box</c> is required (the
/// author marks the monument area); <c>style</c> defaults to <c>Any,Any,Any</c>.
/// </summary>
public sealed class MonumentSuggestionsEndpoint(MapRepository repo, PgmDb db)
    : EndpointWithoutRequest<List<MonumentSuggestionDto>>
{
    public override void Configure() { Get("/map/{slug}/monument-suggestions"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var map = await repo.GetBySlugAsync(Route<string>("slug")!, ct);
        if (map is null) { await Refusals.NotFoundAsync(HttpContext, "map", ct); return; }

        if (!BlockBox.TryParse(HttpContext.Request.Query["box"].ToString(), out var box))
        {
            await Refusals.UnreadableAsync(HttpContext, "no box given",
                "the volume to search is required, as box=x0,y0,z0,x1,y1,z1", ct, field: "box");
            return;
        }
        var style = ParseStyle(HttpContext.Request.Query["style"].ToString());

        var candidates = await MonumentCandidateStore.ReadAsync(db, map.Id, ct);
        var suggestions = MonumentSuggester.Score(candidates, box, style);

        await Send.OkAsync(suggestions.Select(s => new MonumentSuggestionDto(
            s.X, s.Y, s.Z, s.Color, s.Confidence, s.Source, s.PedestalId, s.PedestalData,
            s.SignX is null ? null : new SignPositionDto(s.SignX.Value, s.SignY!.Value, s.SignZ!.Value),
            s.Evidence)).ToList(), ct);
    }

    private static MonumentStyle ParseStyle(string s)
    {
        var p = s.Split(',', StringSplitOptions.TrimEntries);
        var ped = p.Length > 0 && Enum.TryParse<PedestalKind>(p[0], true, out var pk) ? pk : PedestalKind.Any;
        var lab = p.Length > 1 && Enum.TryParse<LabelKind>(p[1], true, out var lk) ? lk : LabelKind.Any;
        var cap = p.Length > 2 && Enum.TryParse<CapKind>(p[2], true, out var ck) ? ck : CapKind.Any;
        return new MonumentStyle(ped, lab, cap);
    }
}
