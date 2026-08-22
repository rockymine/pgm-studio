using FastEndpoints;
using PgmStudio.Contracts;
using PgmStudio.Data.Map;
using PgmStudio.Pgm.Editing;

namespace PgmStudio.Api.Endpoints;

// ── spawns ──────────────────────────────────────────────────────────────────────────

/// <summary>POST /api/map/{slug}/spawns — link a spawn to a region.</summary>
public sealed class SpawnCreateEndpoint(MapRepository repo, MapReader reader, MapWriter writer) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Post("/map/{slug}/spawns");
        AllowAnonymous();
        Description(b => b.Accepts<SpawnCreateRequest>("application/json").Produces<AppliedDto>(200, "application/json").Refuses(404, 409, 422));
    }
    public override async Task HandleAsync(CancellationToken ct)
    {
        var p = await WriteSupport.ReadPayloadAsync(HttpContext, ct);
        var (s, b) = await WriteSupport.RunEditAsync(HttpContext, repo, reader, writer, Route<string>("slug")!, doc => SpawnEditor.AddSpawnLink(doc, p), ct);
        await Send.ResponseAsync(b!, s, ct);
    }
}

/// <summary>PATCH /api/map/{slug}/spawns/{regionId} — update a spawn link.</summary>
public sealed class SpawnUpdateEndpoint(MapRepository repo, MapReader reader, MapWriter writer) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Patch("/map/{slug}/spawns/{regionId}");
        AllowAnonymous();
        Description(b => b.Accepts<SpawnUpdateRequest>("application/json").Produces<AppliedDto>(200, "application/json").Refuses(404, 409, 422));
    }
    public override async Task HandleAsync(CancellationToken ct)
    {
        var rid = Route<string>("regionId")!;
        var p = await WriteSupport.ReadPayloadAsync(HttpContext, ct);
        var (s, b) = await WriteSupport.RunEditAsync(HttpContext, repo, reader, writer, Route<string>("slug")!, doc => SpawnEditor.UpdateSpawnLink(doc, rid, p), ct);
        await Send.ResponseAsync(b!, s, ct);
    }
}

/// <summary>DELETE /api/map/{slug}/spawns/{regionId} — remove a spawn link.</summary>
public sealed class SpawnDeleteEndpoint(MapRepository repo, MapReader reader, MapWriter writer) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Delete("/map/{slug}/spawns/{regionId}");
        AllowAnonymous();
        Description(b => b.Produces<AppliedDto>(200, "application/json").Refuses(404, 409, 422));
    }
    public override async Task HandleAsync(CancellationToken ct)
    {
        var rid = Route<string>("regionId")!;
        var (s, b) = await WriteSupport.RunEditAsync(HttpContext, repo, reader, writer, Route<string>("slug")!, doc => SpawnEditor.DeleteSpawnLink(doc, rid), ct);
        await Send.ResponseAsync(b!, s, ct);
    }
}

/// <summary>PATCH /api/map/{slug}/observer-spawn — set/replace the observer spawn.</summary>
public sealed class ObserverSpawnSetEndpoint(MapRepository repo, MapReader reader, MapWriter writer) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Patch("/map/{slug}/observer-spawn");
        AllowAnonymous();
        Description(b => b.Accepts<ObserverSpawnRequest>("application/json").Produces<AppliedDto>(200, "application/json").Refuses(404, 409, 422));
    }
    public override async Task HandleAsync(CancellationToken ct)
    {
        var p = await WriteSupport.ReadPayloadAsync(HttpContext, ct);
        var (s, b) = await WriteSupport.RunEditAsync(HttpContext, repo, reader, writer, Route<string>("slug")!, doc => SpawnEditor.SetObserverSpawn(doc, p), ct);
        await Send.ResponseAsync(b!, s, ct);
    }
}

/// <summary>DELETE /api/map/{slug}/observer-spawn — remove the observer spawn.</summary>
public sealed class ObserverSpawnDeleteEndpoint(MapRepository repo, MapReader reader, MapWriter writer) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Delete("/map/{slug}/observer-spawn");
        AllowAnonymous();
        Description(b => b.Produces<AppliedDto>(200, "application/json").Refuses(404, 409, 422));
    }
    public override async Task HandleAsync(CancellationToken ct)
    {
        var (s, b) = await WriteSupport.RunEditAsync(HttpContext, repo, reader, writer, Route<string>("slug")!, SpawnEditor.DeleteObserverSpawn, ct);
        await Send.ResponseAsync(b!, s, ct);
    }
}
