using System.Text.Json;
using System.Text.Json.Nodes;
using FastEndpoints;
using PgmStudio.Api.Services;
using PgmStudio.Contracts;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Export;
using PgmStudio.Pgm.Sketch;
using PgmStudio.Minecraft.Painting;

namespace PgmStudio.Api.Endpoints;

/// <summary>A map's terrain-theme registry, and which entry of it the board defaults to.
///
/// <para>It lives here rather than in <c>Contracts</c> because it carries a <see cref="TerrainTheme"/>, and
/// the client project reaches <c>Contracts</c> without reaching <c>Minecraft</c>. A shape that spans the two
/// halves belongs where both are visible, which is the composition root — the same reason the placements
/// route answers a <c>DressingDoc</c> rather than a mirror of one.</para></summary>
/// <param name="Themes">Every theme the map paints with, by the id an author registered it under. A registry
/// entry the painter cannot read as a theme is left out, the same way the painter drops it.</param>
/// <param name="MapTheme">The registry id covering every cell no shape's own scope claims, or null where the
/// board states none and those cells take unthemed stone.</param>
public sealed record SketchThemesDto(
    IReadOnlyDictionary<string, TerrainTheme> Themes, string? MapTheme);

// ── the terrain themes, one registry entry at a time ────────────────────────────────
//
// The second part of the finish to get an address, on the reasoning the first one did: a theme is a place
// rather than a board-wide blob, an author changes one without restating the other twelve, and a body typed
// as TerrainTheme is what puts the buckets, the bands and every pattern in the published schema.

/// <summary>GET /api/map/{slug}/sketch/themes — the registry, and which of it is the map default.
///
/// <para>A theme that will not parse as one is left out, the same way the painter drops it: the registry is
/// a snapshot store rather than a validated model, and a reader asking what the board paints with wants what
/// the board will actually paint with.</para></summary>
public sealed class SketchThemeListEndpoint(MapRepository repo, MapArtifactStore artifacts)
    : EndpointWithoutRequest<SketchThemesDto>
{
    public override void Configure()
    {
        Get("/map/{slug}/sketch/themes"); AllowAnonymous();
        Description(b => b.Produces<SketchThemesDto>(200, "application/json").Refuses(404));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;
        var layoutJson = await SketchPartWrite.LayoutOf(artifacts, map.Id, ct) ?? "{}";
        var themes = TerrainThemeScope.ThemesOf(layoutJson).ToDictionary(row => row.Id, row => row.Theme);
        if (await artifacts.RevisionAsync(map.Id, ArtifactKind.SketchLayoutJson, ct) is { } revision)
            Revisions.Answer(HttpContext, revision);
        await Send.OkAsync(new SketchThemesDto(themes, SketchThemeWrite.MapThemeOf(layoutJson)), ct);
    }
}

/// <summary>GET /api/map/{slug}/sketch/themes/{themeId} — one theme, as the painter reads it. 404 where the
/// registry carries no such id, or carries one that will not parse as a theme.</summary>
public sealed class SketchThemeReadEndpoint(MapRepository repo, MapArtifactStore artifacts)
    : EndpointWithoutRequest<TerrainTheme>
{
    public override void Configure()
    {
        Get("/map/{slug}/sketch/themes/{themeId}"); AllowAnonymous();
        Description(b => b.Produces<TerrainTheme>(200, "application/json").Refuses(404));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;
        var id = Route<string>("themeId")!;
        var layoutJson = await SketchPartWrite.LayoutOf(artifacts, map.Id, ct) ?? "{}";
        var found = TerrainThemeScope.ThemesOf(layoutJson)
            .Where(row => string.Equals(row.Id, id, StringComparison.Ordinal))
            .Select(row => row.Theme).FirstOrDefault();
        if (found is null) { await Send.NotFoundAsync(ct); return; }
        await Send.OkAsync(found, ct);
    }
}

/// <summary>PUT /api/map/{slug}/sketch/themes/{themeId} — register one theme under an id, replacing whatever
/// that id carried. A registry entry is addressed by the name an author gave it, so this is the one write in
/// the sketch that creates and replaces through the same verb.</summary>
public sealed class SketchThemeWriteEndpoint(MapRepository repo, MapArtifactStore artifacts)
    : EndpointWithoutRequest<PartWrittenDto>
{
    public override void Configure()
    {
        Put("/map/{slug}/sketch/themes/{themeId}"); AllowAnonymous();
        Description(b => b.Accepts<TerrainTheme>("application/json")
                          .Produces<PartWrittenDto>(200, "application/json").Refuses(400, 404, 409));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;
        var id = Route<string>("themeId")!;

        var body = await RawBody.ReadAsync(HttpContext, ct);
        if (SketchThemeWrite.Stated(body) is null)
        {
            await Refusals.UnreadableAsync(HttpContext, "malformed theme",
                "the body is not a terrain theme: a theme states `rim` and `surface` as bands "
                + "(`{\"material\": …, \"depth\": N}`) and `wall` and `fill` as materials directly.",
                ct, field: "themes");
            return;
        }

        var layoutJson = await SketchPartWrite.LayoutOf(artifacts, map.Id, ct);
        var edited = SketchThemeWrite.With(layoutJson, id, JsonNode.Parse(body));
        var written = await SketchPartWrite.StoreAsync(HttpContext, artifacts, map.Id, edited, id, ct);
        if (await SketchPartWrite.RefusedAsync(HttpContext, written, ct)) return;
        await Send.OkAsync(new PartWrittenDto(written.Id), ct);
    }
}

/// <summary>DELETE /api/map/{slug}/sketch/themes/{themeId} — take a theme out of the registry.
///
/// <para>It does not refuse over what still names the id. A shape painting with a theme the registry has
/// stopped carrying takes the map default, and the map default naming one takes unthemed stone — both are
/// already <c>SK3</c> complaints on the stored document, and they ride back on this write like any other.
/// Refusing would make a registry entry undeletable until every shape naming it had been found by hand.</para></summary>
public sealed class SketchThemeDeleteEndpoint(MapRepository repo, MapArtifactStore artifacts)
    : EndpointWithoutRequest<PartWrittenDto>
{
    public override void Configure()
    {
        Delete("/map/{slug}/sketch/themes/{themeId}"); AllowAnonymous();
        Description(b => b.Produces<PartWrittenDto>(200, "application/json").Refuses(400, 404, 409));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;
        var id = Route<string>("themeId")!;
        var layoutJson = await SketchPartWrite.LayoutOf(artifacts, map.Id, ct);
        if (!SketchThemeWrite.Carries(layoutJson, id)) { await Send.NotFoundAsync(ct); return; }
        var written = await SketchPartWrite.StoreAsync(HttpContext, artifacts, map.Id,
            SketchThemeWrite.With(layoutJson, id, null), id, ct);
        if (await SketchPartWrite.RefusedAsync(HttpContext, written, ct)) return;
        await Send.OkAsync(new PartWrittenDto(written.Id), ct);
    }
}

/// <summary>PUT /api/map/{slug}/sketch/map-theme — which registered theme covers every cell no shape's own
/// scope claims. The body is <c>{"theme": "meadow"}</c>; a null or absent theme clears it, which paints
/// unthemed stone. Naming a theme the registry does not carry is stored and complained about (<c>SK3</c>)
/// rather than refused, the same as every other dangling name in a working document.</summary>
public sealed class SketchMapThemeEndpoint(MapRepository repo, MapArtifactStore artifacts)
    : EndpointWithoutRequest<PartWrittenDto>
{
    public override void Configure()
    {
        Put("/map/{slug}/sketch/map-theme"); AllowAnonymous();
        Description(b => b.Accepts<MapThemeRequest>("application/json")
                          .Produces<PartWrittenDto>(200, "application/json").Refuses(400, 404, 409));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;
        var body = await RawBody.ReadAsync(HttpContext, ct);
        MapThemeRequest? stated;
        try { stated = JsonSerializer.Deserialize<MapThemeRequest>(body, SketchLayout.Json); }
        catch (JsonException)
        {
            await Refusals.UnreadableAsync(HttpContext, "malformed request",
                "the body states the map default as {\"theme\": \"<id>\"}, or null to clear it.",
                ct, field: "theme");
            return;
        }

        var layoutJson = await SketchPartWrite.LayoutOf(artifacts, map.Id, ct);
        var written = await SketchPartWrite.StoreAsync(HttpContext, artifacts, map.Id,
            SketchThemeWrite.WithMapTheme(layoutJson, stated?.Theme), stated?.Theme ?? "", ct);
        if (await SketchPartWrite.RefusedAsync(HttpContext, written, ct)) return;
        await Send.OkAsync(new PartWrittenDto(written.Id), ct);
    }
}
