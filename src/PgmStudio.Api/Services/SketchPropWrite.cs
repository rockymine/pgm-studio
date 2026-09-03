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
/// layout the edit produced and store it. The three routes differ only in which edit they run and whether
/// they take a body, so that is all each of them states.
/// </summary>
internal static class SketchPropWrite
{
    public static async Task<PropWriteOutcome> RunAsync(
        MapRepository repo, MapArtifactStore artifacts, HttpContext http,
        CancellationToken ct, Func<DressingDoc, PlacedProp?, DressingEditResult> edit, bool needsBody)
    {
        if (await repo.OfRouteAsync(http, ct) is not { } map) return PropWriteOutcome.Answered;

        PlacedProp? prop = null;
        if (needsBody)
        {
            var body = await RawBody.ReadAsync(http, ct);
            prop = SketchDressingWrite.Stated(body);
            if (prop is null)
            {
                await Refusals.UnreadableAsync(http, "malformed prop",
                    "the body is not one placement: it states no `kind`, or a kind the dressing reader does "
                    + $"not know. The kinds are {string.Join(", ", PlacedProp.Kinds)}.", ct, field: "kind");
                return PropWriteOutcome.Answered;
            }
        }

        var layoutJson = await SketchPropListEndpoint.LayoutOf(artifacts, map.Id, ct);
        DressingDoc doc;
        try { doc = SketchDressingWrite.Read(layoutJson); }
        catch (Exception fault) when (fault is JsonException or DressingParseException)
        {
            await Refusals.UnreadableAsync(http, "unreadable dressing", fault, ct);
            return PropWriteOutcome.Answered;
        }

        var result = edit(doc, prop);
        if (!result.Applied) return PropWriteOutcome.Missing;

        var written = await SketchDressingWrite.StoreAsync(
            artifacts, map.Id, SketchDressingWrite.With(layoutJson, result.Doc!), result.Id,
            Revisions.Expected(http), ct);

        if (written.Refusal is { } refusal)
        {
            await Refusals.WriteAsync(http, refusal, ct);
            return PropWriteOutcome.Answered;
        }
        if (written.Findings.Count > 0)
        {
            await Refusals.StopAsync(http, 400, "invalid style or theme", written.Findings, ct);
            return PropWriteOutcome.Answered;
        }

        Revisions.Answer(http, written.Revision!.Value);
        return PropWriteOutcome.Wrote(written.Id);
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
