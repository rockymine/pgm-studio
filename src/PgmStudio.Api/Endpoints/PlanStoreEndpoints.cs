using System.Text.Json;
using FastEndpoints;
using PgmStudio.Contracts;
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
            DescriptorOf(r), IsStale(r));

    /// <summary>Whether a generated row was made by an older composer than the one running. The row's stored
    /// geometry is unaffected — it is loaded, never recomputed — but its descriptor has stopped reproducing it,
    /// so re-composing that request now yields a different board. A row with no recorded version predates the
    /// stamp and is treated as stale for the same reason: nothing says the current composer would make it.</summary>
    internal static bool IsStale(PlanRow r) =>
        r.Origin == PlanOrigin.Generated && r.ComposerVersion != ComposerVersion.Current;

    /// <summary>The reproducible request behind a generated row, parsed from its stored descriptor (null for
    /// authored/imported, or if the stored JSON is unreadable — the list must never fail over one bad row).</summary>
    internal static ComposeRequestDto? DescriptorOf(PlanRow r)
    {
        if (r.RequestJson is null) return null;
        try
        {
            return ComposeDescriptor.Parse(r.RequestJson) is { } d
                ? new ComposeRequestDto(d.PlayersPerTeam, d.Teams, d.Symmetry, d.Cell, d.Seed, d.ComposerVersion, d.Schema)
                : null;
        }
        catch (JsonException) { return null; }
    }

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
        if (row is null) { await Refusals.NotFoundAsync(HttpContext, "stored plan", ct); return; }
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
        var plan = PlanModel.Stated(req.PlanJson);
        if (plan is null)
        {
            await Refusals.UnreadableAsync(HttpContext, "malformed plan JSON",
                "the body is not a plan document: it is empty, or it is not JSON the plan reader accepts", ct);
            return;
        }
        // The row keeps the posted text, so a field the reader has nowhere to keep is stored with it and
        // read by nothing that later opens the plan.
        Complaints.Unread(HttpContext, req.PlanJson, plan);

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
