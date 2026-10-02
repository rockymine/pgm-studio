using System.Text.Json;
using FastEndpoints;
using PgmStudio.Api.Services;
using PgmStudio.Contracts;
using PgmStudio.Data.Schema;
using PgmStudio.Data.Theme;
using PgmStudio.Minecraft;
using PgmStudio.Minecraft.Painting;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Endpoints;

/// <summary>Row ↔ wire-DTO mapping for the theme/style library (B44). A style and a theme each carry their card
/// picture on the wire, rendered here through <see cref="StylePreview"/> — see <see cref="StyleDto.Preview"/>
/// for why the picture travels with the row.</summary>
internal static class ThemeLibraryMapping
{
    public static StyleDto ToDto(StyleRow row)
        => new(row.Id, row.Name, row.Kind, row.Params, PreviewOf(row), row.SeedKey is not null);

    public static ThemeDetail ToDetail(ThemeRow row, IReadOnlyList<ThemeBucketRow> buckets) =>
        new(row.Id, row.Name, row.BedrockRelative, row.BedrockValue, RimEdgeModes.Canonical(row.RimEdges),
            row.WallOnTerrainFaces,
            buckets.Select(b => new ThemeBucketDto(
                b.Bucket, b.StyleId ?? 0, Slots.BlockOf(b.BlockId, b.BlockData, b.BlockLaid), b.Depth, b.Enabled))
            .ToList(), row.SeedKey is not null);

    /// <summary>A style's card picture, or an empty string for params that do not form a material this build can
    /// draw. Deliberately catches everything: <c>params_json</c> is a hand-editable leaf, so it can be malformed
    /// in more ways than a parse error — a kind this build does not know, or a pattern missing the palette its
    /// resolver reads. None of those are worth failing a browse over, and a row that shows as pictureless is
    /// visibly the one to go and fix.</summary>
    private static string PreviewOf(StyleRow row)
    {
        try { return Drawings.Svg("style-card/" + row.Kind, row.Params,
                () => StylePreview.CardSvg(row.Kind, TerrainThemeJson.DeserializeMaterial(row.Params))); }
        catch { return ""; }
    }
}

// ── styles ────────────────────────────────────────────────────────────────────

/// <summary>GET /api/styles[?kind=voronoi|noise|…] — the style library, newest first, optionally one kind
/// (the "show every voronoi" browse).</summary>
public sealed class StyleListEndpoint(ThemeStore store) : EndpointWithoutRequest<List<StyleDto>>
{
    public override void Configure()
    {
        Get("/styles");
        Description(b => b.Reads(new QueryWord("kind", "Only the styles of one kind, a material's `kind`. Absent "
            + "lists them all.", [.. Words.Of(typeof(MaterialKind))])));
    }

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
    public override void Configure() { Get("/styles/{id}"); Description(b => b.Refuses(404)); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var row = await store.GetStyleAsync(Route<long>("id"), ct);
        if (row is null) { await Refusals.NotFoundAsync(HttpContext, "style", ct); return; }
        await Send.OkAsync(ThemeLibraryMapping.ToDto(row), ct);
    }
}

/// <summary>POST /api/styles — save a new pattern. 400 (<c>LB1</c>) for one that lays a single block, which a
/// slot holds directly; 409 (<c>LB3</c>) for one the library already holds, naming the row that does.</summary>
public sealed class StyleCreateEndpoint(ThemeStore store) : Endpoint<StyleSaveRequest, StyleDto>
{
    public override void Configure() { Post("/styles"); Description(b => b.Refuses(409)); }

    public override async Task HandleAsync(StyleSaveRequest req, CancellationToken ct)
    {
        if (await LibraryNaming.RefusedAsync(HttpContext, req.Name, null,
            (await store.ListStylesAsync(ct: ct)).Select(row => (row.Id, row.Name)), ct)) return;
        if (await StyleSaving.RefusedAsync(HttpContext, store, req, self: null, ct)) return;
        var row = new StyleRow { Name = req.Name, Kind = req.Kind, Params = req.Params };
        row.Id = await store.CreateStyleAsync(row, ct);
        await Send.OkAsync(ThemeLibraryMapping.ToDto(row), ct);
    }
}

/// <summary>What saving a pattern refuses, for a new row and an edited one alike.</summary>
internal static class StyleSaving
{
    public static async Task<bool> RefusedAsync(
        HttpContext http, ThemeStore store, StyleSaveRequest req, long? self, CancellationToken ct)
    {
        if (await Refusals.StopAsync(http, 400, "not a pattern", LibraryGate.Pattern(req.Kind, req.Params), ct))
            return true;
        var content = ThemeLibrary.ContentOf(req.Params);
        var held = (await store.ListStylesAsync(ct: ct))
            .FirstOrDefault(style => style.Id != self && ThemeLibrary.ContentOf(style.Params) == content);
        if (held is null) return false;
        await Refusals.WriteAsync(http, 409, "pattern held",
            [new Finding(LibraryRules.PatternHeld,
                $"the library already holds this pattern as `{held.Name}` (style {held.Id})",
                Field: "params", Subjects: [held.Name])], ct);
        return true;
    }
}

/// <summary>PUT /api/styles/{id} — update a style in place (edits every theme that binds it — a library edit,
/// not a map's applied snapshot). Refuses what <see cref="StyleCreateEndpoint"/> does.</summary>
public sealed class StyleUpdateEndpoint(ThemeStore store) : Endpoint<StyleSaveRequest, StyleDto>
{
    public override void Configure() { Put("/styles/{id}"); Description(b => b.Refuses(404, 409)); }

    public override async Task HandleAsync(StyleSaveRequest req, CancellationToken ct)
    {
        if (await SeededRows.RefusedAsync(HttpContext, (await store.GetStyleAsync(Route<long>("id"), ct))?.SeedKey, "pattern", ct))
            return;
        if (await LibraryNaming.RefusedAsync(HttpContext, req.Name, Route<long>("id"),
            (await store.ListStylesAsync(ct: ct)).Select(row => (row.Id, row.Name)), ct)) return;
        var id = Route<long>("id");
        if (await StyleSaving.RefusedAsync(HttpContext, store, req, self: id, ct)) return;
        if (await store.UpdateStyleAsync(id, req.Name, req.Kind, req.Params, ct) == 0)
        { await Refusals.NotFoundAsync(HttpContext, "style", ct); return; }
        await Send.OkAsync(ThemeLibraryMapping.ToDto(
            new StyleRow { Id = id, Name = req.Name, Kind = req.Kind, Params = req.Params }), ct);
    }
}

/// <summary>DELETE /api/styles/{id} — forget a pattern. Refused with 409 and the names of the themes, houses,
/// roofs and storeys still binding it, since a pattern is shared by all four and its bindings are what the
/// foreign key would otherwise complain about.</summary>
public sealed class StyleDeleteEndpoint(ThemeStore store, RoomStyleStore rooms, HousePartStore parts) : EndpointWithoutRequest
{
    public override void Configure() { Delete("/styles/{id}"); Description(b => b.Refuses(409)); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await SeededRows.RefusedAsync(HttpContext, (await store.GetStyleAsync(Route<long>("id"), ct))?.SeedKey, "pattern", ct))
            return;
        var id = Route<long>("id");
        var users = (await store.ThemesUsingStyleAsync(id, ct))
            .Concat(await rooms.UsingStyleAsync(id, ct))
            .Concat(await parts.UsingStyleAsync(id, ct)).ToList();
        if (users.Count > 0)
        {
            await Refusals.ConflictAsync(HttpContext, "style in use",
                $"{users.Count} theme(s), house(s), roof(s) and storey(s) still bind this pattern — unbind them "
                + "before forgetting it", ct, holding: users);
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
    public override void Configure() { Get("/themes"); }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync((await library.ComposeAllAsync(ct))
            .Select(entry => new ThemeSummary(entry.Row.Id, entry.Row.Name,
                Drawings.Svg("theme-card", TerrainThemeJson.Serialize(entry.Theme), () => StylePreview.ThemeSectionSvg(entry.Theme))))
            .ToList(), ct);
}

/// <summary>GET /api/themes/{id} — a theme with its per-bucket style bindings.</summary>
public sealed class ThemeGetEndpoint(ThemeStore store) : EndpointWithoutRequest<ThemeDetail>
{
    public override void Configure() { Get("/themes/{id}"); Description(b => b.Refuses(404)); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<long>("id");
        var row = await store.GetThemeAsync(id, ct);
        if (row is null) { await Refusals.NotFoundAsync(HttpContext, "theme", ct); return; }
        await Send.OkAsync(ThemeLibraryMapping.ToDetail(row, await store.GetBucketsAsync(id, ct)), ct);
    }
}

/// <summary>POST /api/themes — compose a theme from existing styles (the knobs + bucket→style bindings).</summary>
public sealed class ThemeCreateEndpoint(ThemeStore store) : Endpoint<ThemeSaveRequest, ThemeDetail>
{
    public override void Configure() { Post("/themes"); Description(b => b.Refuses(409)); }

    public override async Task HandleAsync(ThemeSaveRequest req, CancellationToken ct)
    {
        if (await LibraryNaming.RefusedAsync(HttpContext, req.Name, null,
            (await store.ListThemesAsync(ct)).Select(row => (row.Id, row.Name)), ct)) return;
        if (await Refusals.StopAsync(HttpContext, 400, "invalid theme", LibraryGate.Buckets(req.Buckets), ct)) return;
        var id = await store.CreateThemeAsync(ThemeRowOf(req), BucketRowsOf(req), ct);
        await Send.OkAsync(new ThemeDetail(id, req.Name, req.BedrockRelative, req.BedrockValue,
            RimEdgeModes.Canonical(req.RimEdges), req.WallOnTerrainFaces, req.Buckets), ct);
    }

    internal static ThemeRow ThemeRowOf(ThemeSaveRequest req) => new()
    {
        Name = req.Name,
        BedrockRelative = req.BedrockRelative, BedrockValue = req.BedrockValue,
        RimEdges = RimEdgeModes.Canonical(req.RimEdges), WallOnTerrainFaces = req.WallOnTerrainFaces,
    };

    // A bucket naming neither a block nor a pattern (style id 0) is "bound to nothing": both columns take null,
    // and the row survives to carry the bucket's depth and toggle.
    internal static IEnumerable<ThemeBucketRow> BucketRowsOf(ThemeSaveRequest req)
        => req.Buckets.Select(b => new ThemeBucketRow
        {
            Bucket = b.Bucket, StyleId = b.Block is not null || b.StyleId == 0 ? null : b.StyleId,
            BlockId = b.Block?.Id, BlockData = b.Block?.Data ?? 0, BlockLaid = b.Block?.Laid ?? false,
            Depth = b.Depth, Enabled = b.Enabled,
        });
}

/// <summary>PUT /api/themes/{id} — replace a theme's knobs and its whole set of bucket bindings.</summary>
public sealed class ThemeUpdateEndpoint(ThemeStore store) : Endpoint<ThemeSaveRequest, ThemeDetail>
{
    public override void Configure() { Put("/themes/{id}"); Description(b => b.Refuses(404, 409)); }

    public override async Task HandleAsync(ThemeSaveRequest req, CancellationToken ct)
    {
        if (await SeededRows.RefusedAsync(HttpContext, (await store.GetThemeAsync(Route<long>("id"), ct))?.SeedKey, "theme", ct))
            return;
        if (await LibraryNaming.RefusedAsync(HttpContext, req.Name, Route<long>("id"),
            (await store.ListThemesAsync(ct)).Select(row => (row.Id, row.Name)), ct)) return;
        var id = Route<long>("id");
        if (await Refusals.StopAsync(HttpContext, 400, "invalid theme", LibraryGate.Buckets(req.Buckets), ct)) return;
        var updated = await store.UpdateThemeAsync(
            id, ThemeCreateEndpoint.ThemeRowOf(req), ThemeCreateEndpoint.BucketRowsOf(req), ct);
        if (!updated) { await Refusals.NotFoundAsync(HttpContext, "theme", ct); return; }
        await Send.OkAsync(new ThemeDetail(id, req.Name, req.BedrockRelative, req.BedrockValue,
            RimEdgeModes.Canonical(req.RimEdges), req.WallOnTerrainFaces, req.Buckets), ct);
    }
}

/// <summary>POST /api/themes/preview — the theme a set of bindings composes to, previewed without saving any of
/// it. What the library's theme editor re-renders as buckets are bound and knobs are turned.</summary>
public sealed class ThemeDraftPreviewEndpoint(ThemeLibrary library) : Endpoint<ThemeSaveRequest, ThemePreviewDto>
{
    public override void Configure() { Post("/themes/preview"); }

    public override async Task HandleAsync(ThemeSaveRequest req, CancellationToken ct)
        => await Send.OkAsync(StylePreview.ThemeViews(await library.ComposeDraftAsync(req, ct)), ct);
}

/// <summary>DELETE /api/themes/{id} — forget a theme (its bucket bindings cascade; the styles stay).</summary>
public sealed class ThemeDeleteEndpoint(ThemeStore store) : EndpointWithoutRequest
{
    public override void Configure() { Delete("/themes/{id}"); Description(b => b.Refuses(409)); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await SeededRows.RefusedAsync(HttpContext, (await store.GetThemeAsync(Route<long>("id"), ct))?.SeedKey, "theme", ct))
            return;
        await store.DeleteThemeAsync(Route<long>("id"), ct);
        await Send.NoContentAsync(ct);
    }
}

/// <summary>GET /api/themes/{id}/json — the theme assembled into the painter's theme JSON (the form the export
/// consumes and a map snapshots when it applies the theme).</summary>
public sealed class ThemeJsonEndpoint(ThemeLibrary library) : EndpointWithoutRequest<ThemeJsonDto>
{
    public override void Configure() { Get("/themes/{id}/json"); Description(b => b.Refuses(404)); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var json = await library.ComposeJsonAsync(Route<long>("id"), ct);
        if (json is null) { await Refusals.NotFoundAsync(HttpContext, "theme", ct); return; }
        await Send.OkAsync(new ThemeJsonDto(json), ct);
    }
}

/// <summary>POST /api/themes/import — lift a whole theme JSON into the library: a block or a pattern per bucket + a
/// theme binding them. 400, never 500, on invalid theme JSON. A theme named nothing is "Imported theme", counted on
/// where the library holds one.</summary>
public sealed class ThemeImportEndpoint(ThemeLibrary library, ThemeStore store) : Endpoint<ThemeImportRequest, CreatedDto>
{
    public override void Configure() { Post("/themes/import"); Description(b => b.Refuses(409)); }

    public override async Task HandleAsync(ThemeImportRequest req, CancellationToken ct)
    {
        var held = (await store.ListThemesAsync(ct)).Select(row => (row.Id, row.Name)).ToList();
        var name = string.IsNullOrWhiteSpace(req.Name)
            ? PatternNames.Unique("Imported theme", held.Select(row => row.Name).ToHashSet(StringComparer.OrdinalIgnoreCase))
            : req.Name;
        if (await LibraryNaming.RefusedAsync(HttpContext, name, null, held, ct)) return;
        long id;
        try { id = await library.ImportAsync(name, req.ThemeJson, ct); }
        catch (JsonException ex) { await Refusals.UnreadableAsync(HttpContext, "malformed theme JSON", ex, ct); return; }
        await Send.OkAsync(new CreatedDto(id), ct);
    }
}
