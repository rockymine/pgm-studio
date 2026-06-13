using FastEndpoints;
using LinqToDB;
using PgmStudio.Data;
using PgmStudio.Data.Repositories;
using PgmStudio.Pgm;
using PgmStudio.Pgm.Editing;

namespace PgmStudio.Api.Endpoints;

using Dict = Dictionary<string, object?>;

/// <summary>Shared helpers for document-editing write endpoints.</summary>
internal static class WriteSupport
{
    /// <summary>Read the JSON request body into the doc-tree dict (only the provided keys → partial edits).</summary>
    public static async Task<Dict> ReadPayloadAsync(HttpContext ctx, CancellationToken ct)
    {
        using var reader = new StreamReader(ctx.Request.Body);
        var body = await reader.ReadToEndAsync(ct);
        return string.IsNullOrWhiteSpace(body) ? new Dict() : JsonTree.FromJson(body) as Dict ?? new Dict();
    }

    /// <summary>Load the map doc, apply an edit, and persist via MapWriter. Returns (status, body).</summary>
    public static async Task<(int status, object? body)> RunEditAsync(
        MapRepository repo, MapReader reader, MapWriter writer, string slug, Func<Dict, Dict> edit, CancellationToken ct)
    {
        var map = await repo.GetBySlugAsync(slug, ct);
        if (map is null) return (404, new Dict { ["error"] = "map not found" });
        var doc = await reader.ReadDocAsync(map, ct);
        try
        {
            var result = edit(doc);
            await writer.SaveDocAsync(map.Id, doc, ct);
            return (200, result);
        }
        catch (EditException ex) { return (ex.Status, new Dict { ["error"] = ex.Message }); }
    }
}

/// <summary>PATCH /api/map/{slug}/metadata — update name/version/objective/gamemode/max_build_height.</summary>
public sealed class MetadataEndpoint(MapRepository repo, PgmDb db) : EndpointWithoutRequest
{
    public override void Configure() { Patch("/map/{slug}/metadata"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var map = await repo.GetBySlugAsync(Route<string>("slug")!, ct);
        if (map is null) { await Send.NotFoundAsync(ct); return; }
        var p = await WriteSupport.ReadPayloadAsync(HttpContext, ct);

        var u = db.Maps.Where(x => x.Id == map.Id).AsUpdatable();
        if (p.ContainsKey("name")) u = u.Set(x => x.Name, p["name"] as string ?? "");
        if (p.ContainsKey("version")) u = u.Set(x => x.Version, NullIfEmpty(p["version"] as string));
        if (p.ContainsKey("objective")) u = u.Set(x => x.Objective, NullIfEmpty(p["objective"] as string));
        if (p.ContainsKey("gamemode")) u = u.Set(x => x.Gamemode, NullIfEmpty(p["gamemode"] as string));
        if (p.ContainsKey("max_build_height")) u = u.Set(x => x.MaxBuildHeight, p["max_build_height"] is { } v ? Convert.ToDouble(v) : null);
        u = u.Set(x => x.UpdatedAt, DateTime.UtcNow);
        await u.UpdateAsync(ct);
        await Send.OkAsync(new Dict { ["ok"] = true }, ct);
    }

    private static string? NullIfEmpty(string? s) => string.IsNullOrEmpty(s) ? null : s;
}

/// <summary>POST /api/map/{slug}/teams — add a team.</summary>
public sealed class TeamCreateEndpoint(MapRepository repo, MapReader reader, MapWriter writer) : EndpointWithoutRequest
{
    public override void Configure() { Post("/map/{slug}/teams"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var payload = await WriteSupport.ReadPayloadAsync(HttpContext, ct);
        var (status, body) = await WriteSupport.RunEditAsync(repo, reader, writer, Route<string>("slug")!, doc => TeamEditor.AddTeam(doc, payload), ct);
        await Send.ResponseAsync(body!, status, ct);
    }
}

/// <summary>PATCH /api/map/{slug}/teams/{teamId} — update a team.</summary>
public sealed class TeamUpdateEndpoint(MapRepository repo, MapReader reader, MapWriter writer) : EndpointWithoutRequest
{
    public override void Configure() { Patch("/map/{slug}/teams/{teamId}"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var teamId = Route<string>("teamId")!;
        var payload = await WriteSupport.ReadPayloadAsync(HttpContext, ct);
        var (status, body) = await WriteSupport.RunEditAsync(repo, reader, writer, Route<string>("slug")!, doc => TeamEditor.UpdateTeam(doc, teamId, payload), ct);
        await Send.ResponseAsync(body!, status, ct);
    }
}

/// <summary>DELETE /api/map/{slug}/teams/{teamId} — remove a team and its spawns.</summary>
public sealed class TeamDeleteEndpoint(MapRepository repo, MapReader reader, MapWriter writer) : EndpointWithoutRequest
{
    public override void Configure() { Delete("/map/{slug}/teams/{teamId}"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var teamId = Route<string>("teamId")!;
        var (status, body) = await WriteSupport.RunEditAsync(repo, reader, writer, Route<string>("slug")!, doc => TeamEditor.DeleteTeam(doc, teamId), ct);
        await Send.ResponseAsync(body!, status, ct);
    }
}
