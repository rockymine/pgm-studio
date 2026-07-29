using FastEndpoints;
using PgmStudio.Contracts;
using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Render;

namespace PgmStudio.Api.Endpoints;

/// <summary>
/// GET /api/shapes/catalog — the vocabulary as cards. Unlike the browse feed this composes nothing and has no
/// cursor: <see cref="ShapeCatalog"/> is a bounded set derived from the emitters and the tuning constants, so
/// the whole thing is returned and the client filters in the page. Optional <c>kind</c> / <c>tier</c> /
/// <c>family</c> query filters narrow the returned cards; the counts always describe the <b>whole</b> catalog,
/// so a chip can say what it would show before it is picked.
/// </summary>
public sealed class ShapeCatalogEndpoint : EndpointWithoutRequest
{
    /// <summary>The card render scale — smaller than a board card because a shape is a handful of cells and the
    /// grid shows dozens at once.</summary>
    private const int CardScale = 7;

    public override void Configure() { Get("/shapes/catalog"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var kinds = Csv("kind");
        var tiers = Csv("tier");
        var families = Csv("family");

        var all = ShapeCatalog.All();
        var shapes = all
            .Where(s => kinds.Count == 0 || kinds.Contains(s.Kind))
            .Where(s => tiers.Count == 0 || tiers.Contains(TierToken(s.Tier)))
            .Where(s => families.Count == 0 || families.Contains(s.Family))
            .Select(s => new CatalogShapeDto(
                s.Id, s.Kind, s.Family, TierToken(s.Tier), s.BoxW, s.BoxH, s.CorridorCells,
                s.Knobs, s.Note, PlanBoardSvg.Render(s.Plan, CardScale)))
            .ToList();

        var page = new CatalogPage(
            shapes,
            all.Count,
            Tally(all, s => TierToken(s.Tier)),
            Tally(all, s => s.Family),
            Tally(all, s => s.Kind));
        await Send.ResponseAsync(page, cancellation: ct);
    }

    /// <summary>The tier's wire token — hyphenated lowercase, the same shape as every other filter token the
    /// client holds.</summary>
    private static string TierToken(CatalogTier tier) => tier switch
    {
        CatalogTier.InMix => "in-mix",
        CatalogTier.Reachable => "reachable",
        _ => "emitter-only",
    };

    private static Dictionary<string, int> Tally(
        IReadOnlyList<CatalogShape> shapes, Func<CatalogShape, string> key) =>
        shapes.GroupBy(key).ToDictionary(g => g.Key, g => g.Count());

    private List<string> Csv(string name) =>
        (Query<string?>(name, isRequired: false) ?? string.Empty)
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(x => x.ToLowerInvariant()).ToList();
}
