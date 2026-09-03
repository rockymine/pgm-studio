using System.Text.Json.Nodes;
using FastEndpoints;
using PgmStudio.Api.Services;
using PgmStudio.Contracts;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Export;
using PgmStudio.Minecraft.Houses;
using PgmStudio.Minecraft.Painting;
using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Api.Endpoints;

// ── the room shells and the biome ───────────────────────────────────────────────────
//
// The last two parts of the finish with no address, and the last two SketchLayout fields the published schema
// described as a sentence and nothing else. Both are map-wide singles rather than registries, so they take
// the shape a single takes: read it, write it, take it away.

/// <summary>The shells a map's rooms are stamped in, <b>resolved</b> — which is what will actually be built.
/// A part that is absent answers its built-in shell; a part bound to open ground answers null.</summary>
/// <param name="Cage">The style every wool cage is stamped in, or null for open ground.</param>
/// <param name="Spawn">The style every spawn cube is stamped in, or null for open ground.</param>
public sealed record SketchRoomStylesDto(HouseStyle? Cage, HouseStyle? Spawn);

/// <summary>GET /api/map/{slug}/sketch/room-styles — both shells as the stampers will read them.
///
/// <para>Resolved rather than raw, because the three states a binding has do not survive being handed back
/// as a snapshot: <b>absent</b> falls back to the built-in shell, <b>null</b> asked for open ground, and a
/// stated style is itself. What a caller wants to know is which of those it is getting, and the resolved pair
/// says so — null for open ground, and a style for both of the others.</para></summary>
public sealed class SketchRoomStylesEndpoint(MapRepository repo, MapArtifactStore artifacts)
    : EndpointWithoutRequest<SketchRoomStylesDto>
{
    public override void Configure()
    {
        Get("/map/{slug}/sketch/room-styles"); AllowAnonymous();
        Description(b => b.Produces<SketchRoomStylesDto>(200, "application/json").Refuses(404));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;
        var layoutJson = await SketchPartWrite.LayoutOf(artifacts, map.Id, ct) ?? "{}";
        var (wool, spawn) = RoomStyleScope.StylesOf(layoutJson);
        if (await artifacts.RevisionAsync(map.Id, ArtifactKind.SketchLayoutJson, ct) is { } revision)
            Revisions.Answer(HttpContext, revision);
        await Send.OkAsync(new SketchRoomStylesDto(wool, spawn), ct);
    }
}

/// <summary>PUT /api/map/{slug}/sketch/room-styles/{part} — bind the shell one kind of room is stamped in.
/// <c>part</c> is <c>cage</c> or <c>spawn</c>.
///
/// <para><b>A body of literal <c>null</c> is a statement, not an omission</b>: it asks for open ground — a pad
/// rather than a building over it, which is what a spawn on a plateau the plan already shaped often wants to
/// be. Removing the binding entirely is <c>DELETE</c>, which restores the built-in shell. The three states the
/// stored layout can hold are all reachable, and they are different answers.</para></summary>
public sealed class SketchRoomStyleWriteEndpoint(MapRepository repo, MapArtifactStore artifacts)
    : EndpointWithoutRequest<PartWrittenDto>
{
    public override void Configure()
    {
        Put("/map/{slug}/sketch/room-styles/{part}"); AllowAnonymous();
        Description(b => b.Accepts<HouseStyle>("application/json")
                          .Produces<PartWrittenDto>(200, "application/json").Refuses(400, 404, 409));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;
        var part = Route<string>("part")!;
        if (!SketchFinishWrite.RoomParts.Contains(part, StringComparer.Ordinal))
        {
            await Refusals.UnreadableAsync(HttpContext, "unknown room part",
                $"a map binds a shell for {string.Join(" and ", SketchFinishWrite.RoomParts)}, and nothing else.",
                ct, field: "part");
            return;
        }

        var stated = SketchFinishWrite.StyleStated(await RawBody.ReadAsync(HttpContext, ct));
        if (!stated.Readable)
        {
            await Refusals.UnreadableAsync(HttpContext, "malformed room style",
                "the body is not a house style. Post `null` to ask for open ground, or DELETE the binding to "
                + "go back to the built-in shell.", ct, field: $"roomStyles.{part}");
            return;
        }

        var layoutJson = await SketchPartWrite.LayoutOf(artifacts, map.Id, ct);
        var edited = SketchFinishWrite.WithRoomStyle(layoutJson, part, stated.Node, unbind: false);
        var written = await SketchPartWrite.StoreAsync(HttpContext, artifacts, map.Id, edited, part, ct);
        if (await SketchPartWrite.RefusedAsync(HttpContext, written, ct)) return;
        await Send.OkAsync(new PartWrittenDto(written.Id), ct);
    }
}

/// <summary>DELETE /api/map/{slug}/sketch/room-styles/{part} — unbind a shell, which puts that kind of room
/// back to its built-in one. Not the same as binding null, which asks for open ground.</summary>
public sealed class SketchRoomStyleDeleteEndpoint(MapRepository repo, MapArtifactStore artifacts)
    : EndpointWithoutRequest<PartWrittenDto>
{
    public override void Configure()
    {
        Delete("/map/{slug}/sketch/room-styles/{part}"); AllowAnonymous();
        Description(b => b.Produces<PartWrittenDto>(200, "application/json").Refuses(400, 404, 409));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;
        var part = Route<string>("part")!;
        var layoutJson = await SketchPartWrite.LayoutOf(artifacts, map.Id, ct);
        if (!SketchFinishWrite.Binds(layoutJson, part)) { await Send.NotFoundAsync(ct); return; }

        var written = await SketchPartWrite.StoreAsync(HttpContext, artifacts, map.Id,
            SketchFinishWrite.WithRoomStyle(layoutJson, part, null, unbind: true), part, ct);
        if (await SketchPartWrite.RefusedAsync(HttpContext, written, ct)) return;
        await Send.OkAsync(new PartWrittenDto(written.Id), ct);
    }
}

/// <summary>GET /api/map/{slug}/sketch/biome — the field every column answers to. 404 where the board states
/// none, which is plains everywhere and what every board that never opened the question exports as.</summary>
public sealed class SketchBiomeReadEndpoint(MapRepository repo, MapArtifactStore artifacts)
    : EndpointWithoutRequest<BiomeField>
{
    public override void Configure()
    {
        Get("/map/{slug}/sketch/biome"); AllowAnonymous();
        Description(b => b.Produces<BiomeField>(200, "application/json").Refuses(400, 404));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;
        var layoutJson = await SketchPartWrite.LayoutOf(artifacts, map.Id, ct) ?? "{}";
        var stated = SketchLayout.Stated(layoutJson)?.Biome;
        if (stated is null) { await Send.NotFoundAsync(ct); return; }

        var field = SketchFinishWrite.BiomeStated(stated.Value.GetRawText());
        if (field is null)
        {
            await Refusals.UnreadableAsync(HttpContext, "unreadable biome",
                "the stored field does not read as a biome: it states no `kind`, or one that is not "
                + "`solid`, `cell` or `noise`.", ct, field: "biome");
            return;
        }
        await Send.OkAsync(field, ct);
    }
}

/// <summary>PUT /api/map/{slug}/sketch/biome — which biome each column of the exported world carries. Map-wide
/// and answered per chunk, because a biome's tint is blended across a radius and a region drawn to a finer
/// edge never reaches its own colour there.</summary>
public sealed class SketchBiomeWriteEndpoint(MapRepository repo, MapArtifactStore artifacts)
    : EndpointWithoutRequest<PartWrittenDto>
{
    public override void Configure()
    {
        Put("/map/{slug}/sketch/biome"); AllowAnonymous();
        Description(b => b.Accepts<BiomeField>("application/json")
                          .Produces<PartWrittenDto>(200, "application/json").Refuses(400, 404, 409));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;
        var body = await RawBody.ReadAsync(HttpContext, ct);
        if (SketchFinishWrite.BiomeStated(body) is null)
        {
            await Refusals.UnreadableAsync(HttpContext, "malformed biome",
                "the body is not a biome field: it states `kind` as `solid`, `cell` or `noise`.",
                ct, field: "biome");
            return;
        }

        var layoutJson = await SketchPartWrite.LayoutOf(artifacts, map.Id, ct);
        var written = await SketchPartWrite.StoreAsync(HttpContext, artifacts, map.Id,
            SketchFinishWrite.WithBiome(layoutJson, JsonNode.Parse(body)), "biome", ct);
        if (await SketchPartWrite.RefusedAsync(HttpContext, written, ct)) return;
        await Send.OkAsync(new PartWrittenDto(written.Id), ct);
    }
}

/// <summary>DELETE /api/map/{slug}/sketch/biome — take the field off the board, which is plains
/// everywhere.</summary>
public sealed class SketchBiomeDeleteEndpoint(MapRepository repo, MapArtifactStore artifacts)
    : EndpointWithoutRequest<PartWrittenDto>
{
    public override void Configure()
    {
        Delete("/map/{slug}/sketch/biome"); AllowAnonymous();
        Description(b => b.Produces<PartWrittenDto>(200, "application/json").Refuses(400, 404, 409));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;
        var layoutJson = await SketchPartWrite.LayoutOf(artifacts, map.Id, ct);
        if (!SketchFinishWrite.HasBiome(layoutJson)) { await Send.NotFoundAsync(ct); return; }

        var written = await SketchPartWrite.StoreAsync(HttpContext, artifacts, map.Id,
            SketchFinishWrite.WithBiome(layoutJson, null), "biome", ct);
        if (await SketchPartWrite.RefusedAsync(HttpContext, written, ct)) return;
        await Send.OkAsync(new PartWrittenDto(written.Id), ct);
    }
}
