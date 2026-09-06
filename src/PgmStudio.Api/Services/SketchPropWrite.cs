using System.Text.Json;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using PgmStudio.Api.Endpoints;
using PgmStudio.Contracts;
using PgmStudio.Data.Map;
using PgmStudio.Minecraft.Dressing;

namespace PgmStudio.Api.Services;

/// <summary>
/// The body every write to one placement shares: read the map, read its dressing, apply the edit, gate the
/// layout the edit produced and store it. A route differs only in which edit it runs, so that is all each of
/// them hands over, and the body it is read from is that route's own to read and refuse.
/// </summary>
internal static class SketchPropWrite
{
    public static async Task<PropWriteOutcome> RunAsync(
        MapRepository repo, MapArtifactStore artifacts, HttpContext http,
        CancellationToken ct, Func<DressingDoc, DressingEditResult> edit)
    {
        if (await repo.OfRouteAsync(http, ct) is not { } map) return PropWriteOutcome.Answered;

        var layoutJson = await SketchPartWrite.LayoutOf(artifacts, map.Id, ct);
        DressingDoc doc;
        try { doc = SketchDressingWrite.Read(layoutJson); }
        catch (Exception fault) when (fault is JsonException or DressingParseException)
        {
            await Refusals.UnreadableAsync(http, "unreadable dressing", fault, ct);
            return PropWriteOutcome.Answered;
        }

        var result = edit(doc);
        if (!result.Applied) return PropWriteOutcome.Missing;

        var written = await SketchPartWrite.StoreAsync(
            http, artifacts, map.Id, SketchDressingWrite.With(layoutJson, result.Doc!), result.Id, ct);

        return await SketchPartWrite.RefusedAsync(http, written, ct)
            ? PropWriteOutcome.Answered
            : PropWriteOutcome.Wrote(written.Id);
    }

    /// <summary>The placement a request body states, or null once the refusal for a body that states none is
    /// on the wire.</summary>
    public static async Task<PlacedProp?> PropBodyAsync(HttpContext http, CancellationToken ct)
    {
        var prop = SketchDressingWrite.Stated(await RawBody.ReadAsync(http, ct));
        if (prop is not null) return prop;
        await Refusals.UnreadableAsync(http, "malformed prop",
            "the body is not one placement: it states no `kind`, or a kind the dressing reader does "
            + $"not know. The kinds are {string.Join(", ", PlacedProp.Kinds)}.", ct, field: "kind");
        return null;
    }
}

/// <summary>What the shared body did, so the endpoint that owns the route sends its own response — the shape
/// <c>MapEdit</c> already answers in. <see cref="IsAnswered"/> means a refusal is already on the wire and the
/// endpoint has nothing left to say.</summary>
internal readonly record struct PropWriteOutcome(bool IsAnswered, bool IsMissing, string Id)
{
    public static PropWriteOutcome Answered => new(true, false, "");
    public static PropWriteOutcome Missing => new(false, true, "");
    public static PropWriteOutcome Wrote(string id) => new(false, false, id);
}
