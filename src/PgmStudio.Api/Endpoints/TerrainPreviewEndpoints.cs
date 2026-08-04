using System.Text.Json;
using FastEndpoints;
using PgmStudio.Api.Services;
using PgmStudio.Contracts;
using PgmStudio.Minecraft;
using PgmStudio.Minecraft.Dressing;

namespace PgmStudio.Api.Endpoints;

/// <summary>GET /api/terrain/blocks — the blocks a terrain-paint material may be built from
/// (<see cref="TerrainPalette"/>), each with its id/data pair, display name, picker group and swatch colour.
/// The block picker reads it, so the colour a picker shows is the colour the export places
/// (docs/world-export/terrain-painting.md §3).</summary>
public sealed class TerrainBlocksEndpoint : EndpointWithoutRequest<List<PaintBlockDto>>
{
    public override void Configure() { Get("/terrain/blocks"); AllowAnonymous(); }

    public override Task HandleAsync(CancellationToken ct)
        => Send.OkAsync(TerrainPalette.Paintable
            .Select(b => new PaintBlockDto(b.Id, b.Data, b.Name, b.Group, b.Hex)).ToList(), ct);
}

/// <summary>The preview endpoints take a raw JSON document as their body rather than a wrapper DTO — the body
/// <em>is</em> the material / theme / plan the painter deserializes — so they read it as text.</summary>
internal static class RawBody
{
    public static async Task<string> ReadAsync(HttpContext http, CancellationToken ct)
    {
        using var reader = new StreamReader(http.Request.Body);
        return await reader.ReadToEndAsync(ct);
    }
}

/// <summary>POST /api/terrain/material-preview — body is one serialized <c>TerrainMaterial</c>; returns both
/// views of it (top-down and cut open). What a style editor re-renders on every edit — see
/// <see cref="StylePreview"/> for why one material needs two views.</summary>
public sealed class MaterialPreviewEndpoint : EndpointWithoutRequest
{
    public override void Configure() { Post("/terrain/material-preview"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var json = await RawBody.ReadAsync(HttpContext, ct);
        try { await Send.OkAsync(StylePreview.Views(json), ct); }
        catch (JsonException) { await Send.ResponseAsync(new { error = "invalid material JSON" }, 400, ct); }
    }
}

/// <summary>POST /api/terrain/theme-preview — body is a serialized terrain-paint theme (a <c>TerrainTheme</c>
/// JSON); returns the sample plateau painted with it and cut open, plus a top-down swatch per themeable bucket,
/// so a theme editor previews the whole finish and each brush as it is edited (TP10).</summary>
public sealed class ThemePreviewEndpoint : EndpointWithoutRequest
{
    public override void Configure() { Post("/terrain/theme-preview"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var json = await RawBody.ReadAsync(HttpContext, ct);
        try { await Send.OkAsync(StylePreview.ThemeViews(json), ct); }
        catch (JsonException) { await Send.ResponseAsync(new { error = "invalid theme JSON" }, 400, ct); }
    }
}

/// <summary>GET /api/terrain/species — the tree species a dressing may draw from, so the editor's picker
/// cannot name one the grower does not know.</summary>
public sealed class TreeSpeciesEndpoint : EndpointWithoutRequest<List<TreeSpeciesDto>>
{
    public override void Configure() { Get("/terrain/species"); AllowAnonymous(); }

    public override Task HandleAsync(CancellationToken ct)
        => Send.OkAsync(DressingPalette.Species.Select(s => new TreeSpeciesDto(s.Name)).ToList(), ct);
}

/// <summary>POST /api/terrain/dressing-preview — a dressing recipe and the terrain finish it grows on; returns
/// a sample patch actually grown by the pass, from above and cut open. The theme is part of the request
/// because what the paint leaves on top is what decides whether flora grows at all.</summary>
public sealed class DressingPreviewEndpoint : Endpoint<DressingPreviewRequest, DressingPreviewDto>
{
    public override void Configure() { Post("/terrain/dressing-preview"); AllowAnonymous(); }

    public override async Task HandleAsync(DressingPreviewRequest req, CancellationToken ct)
    {
        DressingRecipe recipe;
        TerrainTheme theme;
        try
        {
            recipe = DressingJson.Deserialize(req.DressingJson);
            theme = string.IsNullOrWhiteSpace(req.ThemeJson) ? TerrainTheme.Default : TerrainThemeJson.Deserialize(req.ThemeJson);
        }
        catch (JsonException)
        {
            AddError("The dressing or theme JSON could not be read.");
            await Send.ErrorsAsync(400, ct);
            return;
        }
        await Send.OkAsync(DressingPreview.Views(recipe, theme), ct);
    }
}

/// <summary>POST /api/terrain/theme-map-preview — body is a plan JSON; compiles it, paints the terrain through
/// the scoped theme resolver, and returns a top-down SVG of the map's top blocks so the Theme rail's apply step
/// can show the themes on the actual map (TP10). 400 when the plan can't be rendered.</summary>
public sealed class ThemeMapPreviewEndpoint : EndpointWithoutRequest
{
    public override void Configure() { Post("/terrain/theme-map-preview"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var json = await RawBody.ReadAsync(HttpContext, ct);
        try
        {
            var paint = TerrainPreview.MapSvg(json);
            await Send.OkAsync(new { svg = paint.Svg, minX = paint.MinX, minZ = paint.MinZ, spanX = paint.SpanX, spanZ = paint.SpanZ }, ct);
        }
        catch { await Send.ResponseAsync(new { error = "could not render plan" }, 400, ct); }
    }
}
