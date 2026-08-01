using FastEndpoints;
using PgmStudio.Api.Services;

namespace PgmStudio.Api.Endpoints;

/// <summary>POST /api/terrain/theme-preview — body is a serialized terrain-paint theme (a <c>TerrainTheme</c>
/// JSON); returns a top-down SVG swatch per themeable bucket (<c>rim</c>/<c>wall</c>/<c>surface</c>/<c>fill</c>)
/// so the Theme rail's create step can preview the theme's materials — a noise field, a voronoi, a wall-run —
/// with the real palette (docs/world-export/terrain-painting.md TP10).</summary>
public sealed class ThemePreviewEndpoint : EndpointWithoutRequest
{
    public override void Configure() { Post("/terrain/theme-preview"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        using var reader = new StreamReader(HttpContext.Request.Body);
        var json = await reader.ReadToEndAsync(ct);
        try { await Send.OkAsync(TerrainPreview.ThemeSwatches(json), ct); }
        catch { await Send.ResponseAsync(new { error = "invalid theme JSON" }, 400, ct); }
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
        using var reader = new StreamReader(HttpContext.Request.Body);
        var json = await reader.ReadToEndAsync(ct);
        try { await Send.OkAsync(new { svg = TerrainPreview.MapSvg(json) }, ct); }
        catch { await Send.ResponseAsync(new { error = "could not render plan" }, 400, ct); }
    }
}
