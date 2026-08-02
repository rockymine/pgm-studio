using System.Text.Json;
using FastEndpoints;
using PgmStudio.Api.Services;
using PgmStudio.Contracts;
using PgmStudio.Data.Schema;
using PgmStudio.Data.Theme;

namespace PgmStudio.Api.Endpoints;

/// <summary>Row ↔ wire-DTO mapping for the theme/style library (B44).</summary>
internal static class ThemeLibraryMapping
{
    public static StyleDto ToDto(StyleRow r) => new(r.Id, r.Name, r.Kind, r.Params);
    public static ThemeSummary ToSummary(ThemeRow r) => new(r.Id, r.Name);
    public static ThemeDetail ToDetail(ThemeRow r, IReadOnlyList<ThemeBucketRow> buckets) =>
        new(r.Id, r.Name, r.BedrockRelative, r.BedrockValue, r.Closed, r.WallOnTerrainFaces,
            buckets.Select(b => new ThemeBucketDto(b.Bucket, b.StyleId, b.Depth, b.Enabled)).ToList());
}

// ── styles ────────────────────────────────────────────────────────────────────

/// <summary>GET /api/styles[?kind=voronoi|noise|…] — the style library, newest first, optionally one kind
/// (the "show every voronoi" browse).</summary>
public sealed class StyleListEndpoint(ThemeStore store) : EndpointWithoutRequest<List<StyleDto>>
{
    public override void Configure() { Get("/styles"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var kind = Query<string?>("kind", isRequired: false);
        var rows = await store.ListStylesAsync(string.IsNullOrWhiteSpace(kind) ? null : kind, ct);
        await Send.OkAsync(rows.Select(ThemeLibraryMapping.ToDto).ToList(), ct);
    }
}

/// <summary>GET /api/styles/{id} — one style.</summary>
public sealed class StyleGetEndpoint(ThemeStore store) : EndpointWithoutRequest<StyleDto>
{
    public override void Configure() { Get("/styles/{id}"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var row = await store.GetStyleAsync(Route<long>("id"), ct);
        if (row is null) { await Send.NotFoundAsync(ct); return; }
        await Send.OkAsync(ThemeLibraryMapping.ToDto(row), ct);
    }
}

/// <summary>POST /api/styles — save a new reusable style.</summary>
public sealed class StyleCreateEndpoint(ThemeStore store) : Endpoint<StyleSaveRequest, StyleDto>
{
    public override void Configure() { Post("/styles"); AllowAnonymous(); }

    public override async Task HandleAsync(StyleSaveRequest req, CancellationToken ct)
    {
        var id = await store.CreateStyleAsync(new StyleRow { Name = req.Name, Kind = req.Kind, Params = req.Params }, ct);
        await Send.OkAsync(new StyleDto(id, req.Name, req.Kind, req.Params), ct);
    }
}

/// <summary>PUT /api/styles/{id} — update a style in place (edits every theme that binds it — a library edit,
/// not a map's applied snapshot).</summary>
public sealed class StyleUpdateEndpoint(ThemeStore store) : Endpoint<StyleSaveRequest>
{
    public override void Configure() { Put("/styles/{id}"); AllowAnonymous(); }

    public override async Task HandleAsync(StyleSaveRequest req, CancellationToken ct)
    {
        var n = await store.UpdateStyleAsync(Route<long>("id"), req.Name, req.Kind, req.Params, ct);
        if (n == 0) { await Send.NotFoundAsync(ct); return; }
        await Send.NoContentAsync(ct);
    }
}

/// <summary>DELETE /api/styles/{id} — forget a style. Refused by the FK while a theme still binds it.</summary>
public sealed class StyleDeleteEndpoint(ThemeStore store) : EndpointWithoutRequest
{
    public override void Configure() { Delete("/styles/{id}"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await store.DeleteStyleAsync(Route<long>("id"), ct);
        await Send.NoContentAsync(ct);
    }
}

// ── themes ────────────────────────────────────────────────────────────────────

/// <summary>GET /api/themes — the theme library, newest first (summaries).</summary>
public sealed class ThemeListEndpoint(ThemeStore store) : EndpointWithoutRequest<List<ThemeSummary>>
{
    public override void Configure() { Get("/themes"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync((await store.ListThemesAsync(ct)).Select(ThemeLibraryMapping.ToSummary).ToList(), ct);
}

/// <summary>GET /api/themes/{id} — a theme with its per-bucket style bindings.</summary>
public sealed class ThemeGetEndpoint(ThemeStore store) : EndpointWithoutRequest<ThemeDetail>
{
    public override void Configure() { Get("/themes/{id}"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<long>("id");
        var row = await store.GetThemeAsync(id, ct);
        if (row is null) { await Send.NotFoundAsync(ct); return; }
        await Send.OkAsync(ThemeLibraryMapping.ToDetail(row, await store.GetBucketsAsync(id, ct)), ct);
    }
}

/// <summary>POST /api/themes — compose a theme from existing styles (the knobs + bucket→style bindings).</summary>
public sealed class ThemeCreateEndpoint(ThemeStore store) : Endpoint<ThemeSaveRequest, ThemeDetail>
{
    public override void Configure() { Post("/themes"); AllowAnonymous(); }

    public override async Task HandleAsync(ThemeSaveRequest req, CancellationToken ct)
    {
        var themeRow = new ThemeRow
        {
            Name = req.Name,
            BedrockRelative = req.BedrockRelative, BedrockValue = req.BedrockValue,
            Closed = req.Closed, WallOnTerrainFaces = req.WallOnTerrainFaces,
        };
        var buckets = req.Buckets.Select(b => new ThemeBucketRow
        { Bucket = b.Bucket, StyleId = b.StyleId, Depth = b.Depth, Enabled = b.Enabled });
        var id = await store.CreateThemeAsync(themeRow, buckets, ct);
        await Send.OkAsync(new ThemeDetail(id, req.Name, req.BedrockRelative, req.BedrockValue, req.Closed, req.WallOnTerrainFaces, req.Buckets), ct);
    }
}

/// <summary>DELETE /api/themes/{id} — forget a theme (its bucket bindings cascade; the styles stay).</summary>
public sealed class ThemeDeleteEndpoint(ThemeStore store) : EndpointWithoutRequest
{
    public override void Configure() { Delete("/themes/{id}"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await store.DeleteThemeAsync(Route<long>("id"), ct);
        await Send.NoContentAsync(ct);
    }
}

/// <summary>GET /api/themes/{id}/json — the theme assembled into the painter's theme JSON (the form the export
/// consumes and a map snapshots when it applies the theme).</summary>
public sealed class ThemeJsonEndpoint(ThemeLibrary library) : EndpointWithoutRequest
{
    public override void Configure() { Get("/themes/{id}/json"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var json = await library.ComposeJsonAsync(Route<long>("id"), ct);
        if (json is null) { await Send.NotFoundAsync(ct); return; }
        await Send.OkAsync(new { themeJson = json }, ct);
    }
}

/// <summary>POST /api/themes/import — lift a whole theme JSON into the library: one style per bucket + a theme
/// binding them. 400 on invalid theme JSON.</summary>
public sealed class ThemeImportEndpoint(ThemeLibrary library) : Endpoint<ThemeImportRequest>
{
    public override void Configure() { Post("/themes/import"); AllowAnonymous(); }

    public override async Task HandleAsync(ThemeImportRequest req, CancellationToken ct)
    {
        long id;
        try { id = await library.ImportAsync(string.IsNullOrWhiteSpace(req.Name) ? "Imported theme" : req.Name, req.ThemeJson, ct); }
        catch (JsonException) { await Send.ResponseAsync(new { error = "Malformed theme JSON" }, 400, ct); return; }
        await Send.OkAsync(new { id }, ct);
    }
}
