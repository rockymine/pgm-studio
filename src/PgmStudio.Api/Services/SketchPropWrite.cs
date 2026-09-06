using System.Text.Json;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using PgmStudio.Api.Endpoints;
using PgmStudio.Contracts;
using PgmStudio.Data.Map;
using PgmStudio.Minecraft.Dressing;

namespace PgmStudio.Api.Services;

/// <summary>
/// The body every write to one thing on a map's dressing shares: read the map, read its dressing, apply the
/// edit, gate the layout the edit produced and store it. A route differs only in which edit it runs, so that
/// is all each of them hands over — a placement's or a biome patch's, and the body each is read from is that
/// route's own to read and refuse.
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

    /// <summary>The biome patch a request body states, or null once the refusal is on the wire. A patch is an
    /// outline and a field, and both have to read: an outline of fewer than three points encloses no column,
    /// and a field states one of the three kinds a map's own does.</summary>
    public static async Task<BiomePatch?> BiomeBodyAsync(HttpContext http, CancellationToken ct)
    {
        var patch = SketchDressingWrite.StatedBiome(await RawBody.ReadAsync(http, ct));
        if (patch is { Points.Count: >= 3 }) return patch;
        await Refusals.UnreadableAsync(http, "malformed biome patch",
            "the body is not one drawn patch: it states an outline of at least three `[x, z]` points and a "
            + "`field` whose `kind` is `solid`, `cell` or `noise`.", ct, field: "points");
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
