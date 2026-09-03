using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using PgmStudio.Api.Endpoints;
using PgmStudio.Data.Map;
using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Api.Services;

/// <summary>
/// The body every write to one layer, group or shape shares: read the map, read its layout, run the edit,
/// gate what the edit produced and store it. The routes differ only in which edit they run and whether they
/// take a body, so that is all each of them states.
/// </summary>
internal static class SketchGeometryWrite
{
    public static async Task<GeometryWriteOutcome> RunAsync(
        MapRepository repo, MapArtifactStore artifacts, HttpContext http, CancellationToken ct,
        Func<string?, JsonObject, GeometryEdit> edit, bool needsBody)
    {
        if (await repo.OfRouteAsync(http, ct) is not { } map) return GeometryWriteOutcome.Answered;

        var stated = new JsonObject();
        if (needsBody && await StatedAsync(http, ct) is var body)
        {
            if (body is null) return GeometryWriteOutcome.Answered;
            stated = body;
        }

        var layoutJson = await SketchPartWrite.LayoutOf(artifacts, map.Id, ct);
        var result = edit(layoutJson, stated);
        if (result.Refusal is { } refusal)
        {
            await Refusals.WriteAsync(http, 400, "the edit cannot be made", [refusal], ct);
            return GeometryWriteOutcome.Answered;
        }
        if (result.IsMissing) return GeometryWriteOutcome.Missing;

        var written = await SketchPartWrite.StoreAsync(
            http, artifacts, map.Id, result.Layout!, result.Id, ct);

        return await SketchPartWrite.RefusedAsync(http, written, ct)
            ? GeometryWriteOutcome.Answered
            : GeometryWriteOutcome.Wrote(written.Id);
    }

    /// <summary>The body as a JSON object, or null once the refusal for a body that is not one is on the
    /// wire. Every geometry write states an object — a layer, a group, a shape — so this is the one place
    /// that is checked.</summary>
    private static async Task<JsonObject?> StatedAsync(HttpContext http, CancellationToken ct)
    {
        var body = await RawBody.ReadAsync(http, ct);
        JsonNode? node = null;
        try { node = string.IsNullOrWhiteSpace(body) ? null : JsonNode.Parse(body); }
        catch (System.Text.Json.JsonException) { }

        if (node is JsonObject stated) return stated;
        await Refusals.UnreadableAsync(http, "malformed body",
            "the body is not a JSON object stating the fields to write.", ct);
        return null;
    }

    /// <summary>The layout a read route answers off, parsed, or null where the map carries none.</summary>
    public static async Task<SketchLayout?> ReadAsync(MapArtifactStore artifacts, long mapId, CancellationToken ct) =>
        SketchLayout.Stated(await SketchPartWrite.LayoutOf(artifacts, mapId, ct) ?? "{}");
}

/// <summary>What the shared body did, so the endpoint that owns the route sends its own response.
/// <see cref="IsAnswered"/> means a refusal is already on the wire and the endpoint has nothing left to
/// say.</summary>
internal readonly record struct GeometryWriteOutcome(bool IsAnswered, bool IsMissing, string Id)
{
    public static GeometryWriteOutcome Answered => new(true, false, "");
    public static GeometryWriteOutcome Missing => new(false, true, "");
    public static GeometryWriteOutcome Wrote(string id) => new(false, false, id);
}
