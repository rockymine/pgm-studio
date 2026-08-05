using System.Text.Json;
using FastEndpoints;
using PgmStudio.Api.Services;
using PgmStudio.Contracts;
using PgmStudio.Data.Schema;
using PgmStudio.Data.Theme;
using PgmStudio.Minecraft;

namespace PgmStudio.Api.Endpoints;

/// <summary>Row ↔ wire-DTO mapping for the theme/style library (B44). A style and a theme each carry their card
/// picture on the wire, rendered here through <see cref="StylePreview"/> — see <see cref="StyleDto.Preview"/>
/// for why the picture travels with the row.</summary>
internal static class ThemeLibraryMapping
{
    public static StyleDto ToDto(StyleRow row) => new(row.Id, row.Name, row.Kind, row.Params, PreviewOf(row));

    public static ThemeDetail ToDetail(ThemeRow row, IReadOnlyList<ThemeBucketRow> buckets) =>
        new(row.Id, row.Name, row.BedrockRelative, row.BedrockValue, row.Closed, row.WallOnTerrainFaces,
            buckets.Select(b => new ThemeBucketDto(b.Bucket, b.StyleId, b.Depth, b.Enabled)).ToList());

    /// <summary>A style's card picture, or an empty string for params that do not form a material this build can
    /// draw. Deliberately catches everything: <c>params_json</c> is a hand-editable leaf, so it can be malformed
    /// in more ways than a parse error — a kind this build does not know, or a pattern missing the palette its
    /// resolver reads. None of those are worth failing a browse over, and a row that shows as pictureless is
    /// visibly the one to go and fix.</summary>
    private static string PreviewOf(StyleRow row)
    {
        try { return StylePreview.CardSvg(row.Kind, TerrainThemeJson.DeserializeMaterial(row.Params)); }
        catch { return ""; }
    }
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
        var row = new StyleRow { Name = req.Name, Kind = req.Kind, Params = req.Params };
        row.Id = await store.CreateStyleAsync(row, ct);
        await Send.OkAsync(ThemeLibraryMapping.ToDto(row), ct);
    }
}

/// <summary>PUT /api/styles/{id} — update a style in place (edits every theme that binds it — a library edit,
/// not a map's applied snapshot).</summary>
public sealed class StyleUpdateEndpoint(ThemeStore store) : Endpoint<StyleSaveRequest, StyleDto>
{
    public override void Configure() { Put("/styles/{id}"); AllowAnonymous(); }

    public override async Task HandleAsync(StyleSaveRequest req, CancellationToken ct)
    {
        var id = Route<long>("id");
        if (await store.UpdateStyleAsync(id, req.Name, req.Kind, req.Params, ct) == 0) { await Send.NotFoundAsync(ct); return; }
        await Send.OkAsync(ThemeLibraryMapping.ToDto(
            new StyleRow { Id = id, Name = req.Name, Kind = req.Kind, Params = req.Params }), ct);
    }
}

/// <summary>DELETE /api/styles/{id} — forget a style. Refused with 409 and the names of the themes and room
/// styles still binding it, since a style is shared by both libraries and its bindings are what the foreign key
/// would otherwise complain about.</summary>
public sealed class StyleDeleteEndpoint(ThemeStore store, RoomStyleStore rooms) : EndpointWithoutRequest
{
    public override void Configure() { Delete("/styles/{id}"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<long>("id");
        var users = (await store.ThemesUsingStyleAsync(id, ct))
            .Concat(await rooms.UsingStyleAsync(id, ct)).ToList();
        if (users.Count > 0)
        {
            await Send.ResponseAsync(new StyleInUseDto("The style is bound by a theme or room style.", users), 409, ct);
            return;
        }
        await store.DeleteStyleAsync(id, ct);
        await Send.NoContentAsync(ct);
    }
}

// ── themes ────────────────────────────────────────────────────────────────────

/// <summary>GET /api/themes — the theme library, newest first, each with the sample plateau it finishes.</summary>
public sealed class ThemeListEndpoint(ThemeLibrary library) : EndpointWithoutRequest<List<ThemeSummary>>
{
    public override void Configure() { Get("/themes"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync((await library.ComposeAllAsync(ct))
            .Select(entry => new ThemeSummary(entry.Row.Id, entry.Row.Name, StylePreview.ThemeSectionSvg(entry.Theme)))
            .ToList(), ct);
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
        var id = await store.CreateThemeAsync(ThemeRowOf(req), BucketRowsOf(req), ct);
        await Send.OkAsync(new ThemeDetail(id, req.Name, req.BedrockRelative, req.BedrockValue, req.Closed,
            req.WallOnTerrainFaces, req.Buckets), ct);
    }

    internal static ThemeRow ThemeRowOf(ThemeSaveRequest req) => new()
    {
        Name = req.Name,
        BedrockRelative = req.BedrockRelative, BedrockValue = req.BedrockValue,
        Closed = req.Closed, WallOnTerrainFaces = req.WallOnTerrainFaces,
    };

    internal static IEnumerable<ThemeBucketRow> BucketRowsOf(ThemeSaveRequest req)
        => req.Buckets.Select(b => new ThemeBucketRow
        { Bucket = b.Bucket, StyleId = b.StyleId, Depth = b.Depth, Enabled = b.Enabled });
}

/// <summary>PUT /api/themes/{id} — replace a theme's knobs and its whole set of bucket bindings.</summary>
public sealed class ThemeUpdateEndpoint(ThemeStore store) : Endpoint<ThemeSaveRequest, ThemeDetail>
{
    public override void Configure() { Put("/themes/{id}"); AllowAnonymous(); }

    public override async Task HandleAsync(ThemeSaveRequest req, CancellationToken ct)
    {
        var id = Route<long>("id");
        var updated = await store.UpdateThemeAsync(
            id, ThemeCreateEndpoint.ThemeRowOf(req), ThemeCreateEndpoint.BucketRowsOf(req), ct);
        if (!updated) { await Send.NotFoundAsync(ct); return; }
        await Send.OkAsync(new ThemeDetail(id, req.Name, req.BedrockRelative, req.BedrockValue, req.Closed,
            req.WallOnTerrainFaces, req.Buckets), ct);
    }
}

/// <summary>POST /api/themes/preview — the theme a set of bindings composes to, previewed without saving any of
/// it. What the library's theme editor re-renders as buckets are bound and knobs are turned.</summary>
public sealed class ThemeDraftPreviewEndpoint(ThemeLibrary library) : Endpoint<ThemeSaveRequest, ThemePreviewDto>
{
    public override void Configure() { Post("/themes/preview"); AllowAnonymous(); }

    public override async Task HandleAsync(ThemeSaveRequest req, CancellationToken ct)
        => await Send.OkAsync(StylePreview.ThemeViews(await library.ComposeDraftAsync(req, ct)), ct);
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
/// binding them. 400, never 500, on invalid theme JSON.</summary>
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
