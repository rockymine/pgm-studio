using System.Text.Json;
using FastEndpoints;
using LinqToDB;
using PgmStudio.Api.Services;
using PgmStudio.Contracts;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Domain;
using PgmStudio.Pgm;
using PgmStudio.Pgm.Editing;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Endpoints;

using Dict = Dictionary<string, object?>;

/// <summary>Shared helpers for document-editing write endpoints.</summary>
internal static class WriteSupport
{
    /// <summary>Read the JSON request body into the doc-tree dict (only the provided keys → partial edits).</summary>
    public static async Task<Dict> ReadPayloadAsync(HttpContext ctx, CancellationToken ct)
    {
        var body = await RawBody.ReadAsync(ctx, ct);
        return string.IsNullOrWhiteSpace(body) ? new Dict() : JsonTree.FromJson(body) as Dict ?? new Dict();
    }

    /// <summary>
    /// A bound request as the same doc-tree dict, so an editor reads one shape whichever way its route got
    /// there. The record is written back out through the wire's own serializer, which is what makes the two
    /// paths agree about spelling: <c>RegionId</c> reaches the editor as <c>region_id</c> because the
    /// property says so, and <c>MaxPlayers</c> as <c>max_players</c> for the same reason.
    ///
    /// <para><b>Only a create binds.</b> An absent field and a null one are one thing here — every optional
    /// on a create record is serialized, so the dict carries a key for each — which is right where the editor
    /// reads with a default and wrong where it asks <c>ContainsKey</c> to tell "leave this alone" from "clear
    /// it". That is every update, and every update stays hand-read.</para>
    /// </summary>
    public static Dict Stated<TRequest>(TRequest request) =>
        JsonTree.FromJson(JsonSerializer.Serialize(request, Wire)) as Dict ?? new Dict();

    /// <summary>The wire's own spelling — camelCase, and each property's own name where it states one.
    /// </summary>
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web);
}

/// <summary>PATCH /api/map/{slug}/metadata — update name/version/objective/max_build_height
/// (scalar columns) plus authors/contributors (the <c>author</c> table). Authors are id'd by uuid;
/// the resolved username is cached in <c>author.name</c> for display.
/// <para>The <c>gamemode</c> column is not writable here: it holds the author's original
/// <c>&lt;gamemode&gt;</c> label, which is round-tripped as written, while the gamemode itself is derived
/// from the map's objective modules and so cannot be set by hand.</para></summary>
public sealed class MetadataEndpoint(MapRepository repo, PgmDb db) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Patch("/map/{slug}/metadata");
        AllowAnonymous();
        Description(b => b.Accepts<MapMetadataRequest>("application/json").Produces<OkDto>(200, "application/json").Refuses(404));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;
        var p = await WriteSupport.ReadPayloadAsync(HttpContext, ct);

        await using var tx = await db.BeginTransactionAsync(ct);

        var u = db.Maps.Where(x => x.Id == map.Id).AsUpdatable();
        if (p.ContainsKey("name")) u = u.Set(x => x.Name, p["name"] as string ?? "");
        if (p.ContainsKey("version")) u = u.Set(x => x.Version, NullIfEmpty(p["version"] as string));
        if (p.ContainsKey("objective")) u = u.Set(x => x.Objective, NullIfEmpty(p["objective"] as string));
        if (p.ContainsKey("max_build_height")) u = u.Set(x => x.MaxBuildHeight, p["max_build_height"] is { } v ? Convert.ToDouble(v) : null);
        u = u.Set(x => x.UpdatedAt, DateTime.UtcNow);
        await u.UpdateAsync(ct);

        // Authors are a full replace, and the rule for what counts as a person is MapAuthors' — the load
        // that reconstitutes a map from its documents states them the same way.
        if (p.TryGetValue("authors", out var authorsRaw) && authorsRaw is List<object?> authors)
            await MapAuthors.ReplaceAsync(db, map.Id, authors, ct);

        await tx.CommitAsync(ct);
        await Send.OkAsync(new Dict { ["ok"] = true }, ct);
    }

    private static string? NullIfEmpty(string? s) => string.IsNullOrEmpty(s) ? null : s;
}

/// <summary>POST /api/map/{slug}/teams — add a team.</summary>
public sealed class TeamCreateEndpoint(MapRepository repo, MapReader reader, MapWriter writer)
    : Endpoint<TeamCreateRequest>
{
    public override void Configure()
    {
        Post("/map/{slug}/teams");
        AllowAnonymous();
        Description(b => b.Produces<TeamWrittenDto>(200, "application/json").Refuses(404, 409, 422));
    }

    public override async Task HandleAsync(TeamCreateRequest req, CancellationToken ct)
    {
        var payload = WriteSupport.Stated(req);
        var applied = await MapEdit.RunAsync(repo, reader, writer, Route<string>("slug")!, doc => TeamEditor.AddTeam(doc, payload), Revisions.Expected(HttpContext), ct);
        await Send.ResponseAsync(applied.Body(HttpContext), applied.Status(), ct);
    }
}

/// <summary>PATCH /api/map/{slug}/teams/{teamId} — update a team.</summary>
public sealed class TeamUpdateEndpoint(MapRepository repo, MapReader reader, MapWriter writer) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Patch("/map/{slug}/teams/{teamId}");
        AllowAnonymous();
        Description(b => b.Accepts<TeamUpdateRequest>("application/json").Produces<TeamWrittenDto>(200, "application/json").Refuses(404, 409, 422));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var teamId = Route<string>("teamId")!;
        var payload = await WriteSupport.ReadPayloadAsync(HttpContext, ct);
        var applied = await MapEdit.RunAsync(repo, reader, writer, Route<string>("slug")!, doc => TeamEditor.UpdateTeam(doc, teamId, payload), Revisions.Expected(HttpContext), ct);
        await Send.ResponseAsync(applied.Body(HttpContext), applied.Status(), ct);
    }
}

/// <summary>DELETE /api/map/{slug}/teams/{teamId} — remove a team and its spawns.</summary>
public sealed class TeamDeleteEndpoint(MapRepository repo, MapReader reader, MapWriter writer) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Delete("/map/{slug}/teams/{teamId}");
        AllowAnonymous();
        Description(b => b.Produces<AppliedDto>(200, "application/json").Refuses(404, 409, 422));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var teamId = Route<string>("teamId")!;
        var applied = await MapEdit.RunAsync(repo, reader, writer, Route<string>("slug")!, doc => TeamEditor.DeleteTeam(doc, teamId), Revisions.Expected(HttpContext), ct);
        await Send.ResponseAsync(applied.Body(HttpContext), applied.Status(), ct);
    }
}
