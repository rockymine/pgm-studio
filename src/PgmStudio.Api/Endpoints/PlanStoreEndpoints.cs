using System.Text.Json;
using FastEndpoints;
using PgmStudio.Contracts;
using PgmStudio.Data.Plan;
using PgmStudio.Data.Schema;
using PgmStudio.Pgm.Plan;

namespace PgmStudio.Api.Endpoints;

/// <summary>Shared mapping between the <c>plan</c> row and its wire DTOs.</summary>
internal static class PlanStoreMapping
{
    public static PlanSummary ToSummary(PlanRow r) =>
        new(r.Id, r.Name, r.Origin, r.ParentId, r.Seed, r.ComposerVersion, r.CreatedAt, r.UpdatedAt);

    public static PlanDetail ToDetail(PlanRow r) =>
        new(r.Id, r.Name, r.Origin, r.ParentId, r.Seed, r.ComposerVersion, r.CreatedAt, r.UpdatedAt, r.PlanJson);
}

/// <summary>GET /api/plans[?origin=generated|authored|imported] — the open-from-DB browser list, newest
/// touched first. Summaries only (no plan JSON); the detail endpoint carries the document.</summary>
public sealed class PlanListEndpoint(PlanStore store) : EndpointWithoutRequest<List<PlanSummary>>
{
    public override void Configure() { Get("/plans"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var origin = Query<string?>("origin", isRequired: false);
        var rows = await store.ListAsync(string.IsNullOrWhiteSpace(origin) ? null : origin, ct);
        await Send.OkAsync(rows.Select(PlanStoreMapping.ToSummary).ToList(), ct);
    }
}

/// <summary>GET /api/plans/{id} — one plan with its <c>*.plan.json</c> document, to load into the editor.</summary>
public sealed class PlanGetEndpoint(PlanStore store) : EndpointWithoutRequest<PlanDetail>
{
    public override void Configure() { Get("/plans/{id}"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var row = await store.GetByIdAsync(Route<long>("id"), ct);
        if (row is null) { await Send.NotFoundAsync(ct); return; }
        await Send.OkAsync(PlanStoreMapping.ToDetail(row), ct);
    }
}

/// <summary>POST /api/plans — save the plan open in the editor. Applies the fork-or-mutate doctrine in
/// <see cref="PlanStore.SaveFromEditorAsync"/> and returns the resulting row. A malformed plan body is
/// answered 400, never 500.</summary>
public sealed class PlanSaveEndpoint(PlanStore store) : Endpoint<PlanSaveRequest>
{
    public override void Configure() { Post("/plans"); AllowAnonymous(); }

    public override async Task HandleAsync(PlanSaveRequest req, CancellationToken ct)
    {
        PlanModel? plan;
        try { plan = string.IsNullOrWhiteSpace(req.PlanJson) ? null : PlanModel.Parse(req.PlanJson); }
        catch (JsonException) { plan = null; }
        if (plan is null) { await Send.ResponseAsync(new { error = "Malformed plan JSON" }, 400, ct); return; }

        var row = await store.SaveFromEditorAsync(req.PlanJson, req.SourceId, ct);
        await Send.OkAsync(PlanStoreMapping.ToDetail(row), ct);
    }
}

/// <summary>DELETE /api/plans/{id} — forget a plan (204). Forks of it survive: the self-FK sets their
/// <c>parent_id</c> null rather than cascading.</summary>
public sealed class PlanDeleteEndpoint(PlanStore store) : EndpointWithoutRequest
{
    public override void Configure() { Delete("/plans/{id}"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await store.DeleteAsync(Route<long>("id"), ct);
        await Send.NoContentAsync(ct);
    }
}
