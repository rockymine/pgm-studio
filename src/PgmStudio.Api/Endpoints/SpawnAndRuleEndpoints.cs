using FastEndpoints;
using PgmStudio.Data.Map;
using PgmStudio.Pgm.Editing;

namespace PgmStudio.Api.Endpoints;

// ── spawns ──────────────────────────────────────────────────────────────────────────

/// <summary>POST /api/map/{slug}/spawns — link a spawn to a region.</summary>
public sealed class SpawnCreateEndpoint(MapRepository repo, MapReader reader, MapWriter writer) : EndpointWithoutRequest
{
    public override void Configure() { Post("/map/{slug}/spawns"); AllowAnonymous(); }
    public override async Task HandleAsync(CancellationToken ct)
    {
        var p = await WriteSupport.ReadPayloadAsync(HttpContext, ct);
        var (s, b) = await WriteSupport.RunEditAsync(repo, reader, writer, Route<string>("slug")!, doc => SpawnEditor.AddSpawnLink(doc, p), ct);
        await Send.ResponseAsync(b!, s, ct);
    }
}

/// <summary>PATCH /api/map/{slug}/spawns/{regionId} — update a spawn link.</summary>
public sealed class SpawnUpdateEndpoint(MapRepository repo, MapReader reader, MapWriter writer) : EndpointWithoutRequest
{
    public override void Configure() { Patch("/map/{slug}/spawns/{regionId}"); AllowAnonymous(); }
    public override async Task HandleAsync(CancellationToken ct)
    {
        var rid = Route<string>("regionId")!;
        var p = await WriteSupport.ReadPayloadAsync(HttpContext, ct);
        var (s, b) = await WriteSupport.RunEditAsync(repo, reader, writer, Route<string>("slug")!, doc => SpawnEditor.UpdateSpawnLink(doc, rid, p), ct);
        await Send.ResponseAsync(b!, s, ct);
    }
}

/// <summary>DELETE /api/map/{slug}/spawns/{regionId} — remove a spawn link.</summary>
public sealed class SpawnDeleteEndpoint(MapRepository repo, MapReader reader, MapWriter writer) : EndpointWithoutRequest
{
    public override void Configure() { Delete("/map/{slug}/spawns/{regionId}"); AllowAnonymous(); }
    public override async Task HandleAsync(CancellationToken ct)
    {
        var rid = Route<string>("regionId")!;
        var (s, b) = await WriteSupport.RunEditAsync(repo, reader, writer, Route<string>("slug")!, doc => SpawnEditor.DeleteSpawnLink(doc, rid), ct);
        await Send.ResponseAsync(b!, s, ct);
    }
}

/// <summary>PATCH /api/map/{slug}/observer-spawn — set/replace the observer spawn.</summary>
public sealed class ObserverSpawnSetEndpoint(MapRepository repo, MapReader reader, MapWriter writer) : EndpointWithoutRequest
{
    public override void Configure() { Patch("/map/{slug}/observer-spawn"); AllowAnonymous(); }
    public override async Task HandleAsync(CancellationToken ct)
    {
        var p = await WriteSupport.ReadPayloadAsync(HttpContext, ct);
        var (s, b) = await WriteSupport.RunEditAsync(repo, reader, writer, Route<string>("slug")!, doc => SpawnEditor.SetObserverSpawn(doc, p), ct);
        await Send.ResponseAsync(b!, s, ct);
    }
}

/// <summary>DELETE /api/map/{slug}/observer-spawn — remove the observer spawn.</summary>
public sealed class ObserverSpawnDeleteEndpoint(MapRepository repo, MapReader reader, MapWriter writer) : EndpointWithoutRequest
{
    public override void Configure() { Delete("/map/{slug}/observer-spawn"); AllowAnonymous(); }
    public override async Task HandleAsync(CancellationToken ct)
    {
        var (s, b) = await WriteSupport.RunEditAsync(repo, reader, writer, Route<string>("slug")!, SpawnEditor.DeleteObserverSpawn, ct);
        await Send.ResponseAsync(b!, s, ct);
    }
}

// ── apply rules ─────────────────────────────────────────────────────────────────────

/// <summary>GET /api/map/{slug}/apply-rules — list rules (with backfilled ids).</summary>
public sealed class ApplyRulesListEndpoint(MapRepository repo, MapReader reader, MapWriter writer) : EndpointWithoutRequest
{
    public override void Configure() { Get("/map/{slug}/apply-rules"); AllowAnonymous(); }
    public override async Task HandleAsync(CancellationToken ct)
    {
        // read-only, but RunEditAsync persists the id backfill (harmless, positional) — acceptable.
        var (s, b) = await WriteSupport.RunEditAsync(repo, reader, writer, Route<string>("slug")!, ApplyRuleEditor.ListApplyRules, ct);
        await Send.ResponseAsync(b!, s, ct);
    }
}

/// <summary>POST /api/map/{slug}/apply-rules — create an apply-rule.</summary>
public sealed class ApplyRuleCreateEndpoint(MapRepository repo, MapReader reader, MapWriter writer) : EndpointWithoutRequest
{
    public override void Configure() { Post("/map/{slug}/apply-rules"); AllowAnonymous(); }
    public override async Task HandleAsync(CancellationToken ct)
    {
        var p = await WriteSupport.ReadPayloadAsync(HttpContext, ct);
        var (s, b) = await WriteSupport.RunEditAsync(repo, reader, writer, Route<string>("slug")!, doc => ApplyRuleEditor.CreateApplyRule(doc, p), ct);
        await Send.ResponseAsync(b!, s, ct);
    }
}

/// <summary>PATCH /api/map/{slug}/apply-rule/{ruleId} — replace an apply-rule's fields.</summary>
public sealed class ApplyRuleUpdateEndpoint(MapRepository repo, MapReader reader, MapWriter writer) : EndpointWithoutRequest
{
    public override void Configure() { Patch("/map/{slug}/apply-rule/{ruleId}"); AllowAnonymous(); }
    public override async Task HandleAsync(CancellationToken ct)
    {
        var rid = Route<string>("ruleId")!;
        var p = await WriteSupport.ReadPayloadAsync(HttpContext, ct);
        var (s, b) = await WriteSupport.RunEditAsync(repo, reader, writer, Route<string>("slug")!, doc => ApplyRuleEditor.UpdateApplyRule(doc, rid, p), ct);
        await Send.ResponseAsync(b!, s, ct);
    }
}

/// <summary>DELETE /api/map/{slug}/apply-rule/{ruleId} — remove an apply-rule.</summary>
public sealed class ApplyRuleDeleteEndpoint(MapRepository repo, MapReader reader, MapWriter writer) : EndpointWithoutRequest
{
    public override void Configure() { Delete("/map/{slug}/apply-rule/{ruleId}"); AllowAnonymous(); }
    public override async Task HandleAsync(CancellationToken ct)
    {
        var rid = Route<string>("ruleId")!;
        var (s, b) = await WriteSupport.RunEditAsync(repo, reader, writer, Route<string>("slug")!, doc => ApplyRuleEditor.DeleteApplyRule(doc, rid), ct);
        await Send.ResponseAsync(b!, s, ct);
    }
}
