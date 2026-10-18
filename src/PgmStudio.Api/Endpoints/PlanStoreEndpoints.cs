using System.Text.Json;
using FastEndpoints;
using PgmStudio.Contracts;
using PgmStudio.Data.Compose;
using PgmStudio.Data.Plan;
using PgmStudio.Data.Schema;
using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Plan;

namespace PgmStudio.Api.Endpoints;

/// <summary>Shared mapping between the <c>plan</c> row and its wire DTOs.</summary>
internal static class PlanStoreMapping
{
    public static PlanSummary ToSummary(PlanRow r) =>
        new(r.Id, r.Name, r.Origin, r.ParentId, r.Seed, r.ComposerVersion, r.CreatedAt, r.UpdatedAt,
            DescriptorOf(r));

    /// <summary>The reproducible request behind a generated row, parsed from its stored descriptor (null for
    /// authored/imported, or if the stored JSON is unreadable — the list must never fail over one bad row).</summary>
    internal static ComposeRequestDto? DescriptorOf(PlanRow r)
    {
        if (r.RequestJson is null) return null;
        try
        {
            return ComposeDescriptor.Parse(r.RequestJson) is { } d ? ComposedBoardLibrary.DtoOf(d) : null;
        }
        catch (JsonException) { return null; }
    }

    public static PlanDetail ToDetail(PlanRow r) =>
        new(r.Id, r.Name, r.Origin, r.ParentId, r.Seed, r.ComposerVersion, r.CreatedAt, r.UpdatedAt, r.PlanJson);
}

/// <summary>GET /api/plans[?origin=generated|authored|imported] — the stored plan rows, kept read-only as history,
/// newest touched first. Summaries only (no plan JSON); the detail endpoint carries the document.</summary>
public sealed class PlanListEndpoint(PlanStore store) : EndpointWithoutRequest<List<PlanSummary>>
{
    public override void Configure()
    {
        Get("/plans");
        Description(b => b.Reads(new QueryWord("origin", "Only the plans of one origin. Absent lists them all.",
            [PlanOrigin.Generated, PlanOrigin.Authored, PlanOrigin.Imported])));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var origin = Query<string?>("origin", isRequired: false);
        var rows = await store.ListAsync(string.IsNullOrWhiteSpace(origin) ? null : origin, ct);
        await Send.OkAsync(rows.Select(PlanStoreMapping.ToSummary).ToList(), ct);
    }
}

/// <summary>GET /api/plans/{id} — one stored plan row with its <c>*.plan.json</c> document.</summary>
public sealed class PlanGetEndpoint(PlanStore store) : EndpointWithoutRequest<PlanDetail>
{
    public override void Configure() { Get("/plans/{id}"); Description(b => b.Refuses(404)); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var row = await store.GetByIdAsync(Route<long>("id"), ct);
        if (row is null) { await Refusals.NotFoundAsync(HttpContext, "stored plan", ct); return; }
        await Send.OkAsync(PlanStoreMapping.ToDetail(row), ct);
    }
}
