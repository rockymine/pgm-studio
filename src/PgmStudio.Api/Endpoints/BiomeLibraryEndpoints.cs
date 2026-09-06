using System.Text.Json;
using FastEndpoints;
using PgmStudio.Api.Services;
using PgmStudio.Contracts;
using PgmStudio.Data.Schema;
using PgmStudio.Data.Theme;
using PgmStudio.Minecraft.Painting;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Endpoints;

// ── the biome library ───────────────────────────────────────────────────────────────
//
// A biome field is a recipe like a material is: a kind, a scale and a palette, worth naming once and reusing.
// So it is browsed and picked rather than authored wherever it is applied — the Theme phase takes one in a
// select, the way it takes a map default theme, and the authoring happens here.

/// <summary>Refusing a body that is not a biome field. What a field <em>is</em> is
/// <see cref="SketchFinishWrite.BiomeStated"/>'s question and is asked there, so a library row and a map's own
/// field are read by one reader; this is only the words this surface refuses in.</summary>
internal static class BiomeBody
{
    /// <summary>The field a request states, or null where the JSON does not read as one.</summary>
    public static BiomeField? Stated(string? json) =>
        string.IsNullOrWhiteSpace(json) ? null : SketchFinishWrite.BiomeStated(json);

    /// <summary>Refuse a body that is not a field, naming the kinds a caller may state.</summary>
    public static Task RefuseAsync(HttpContext http, CancellationToken ct) =>
        Refusals.UnreadableAsync(http, "malformed biome",
            $"`params` is not a biome field: it states `kind` as {string.Join(", ", BiomeKinds.All)}.",
            ct, field: "params");
}

/// <summary>Turning a row into what the library hands back — the row's own fields plus the patch of ground it
/// tints, drawn through the same palette the export places blocks from.</summary>
internal static class BiomeLibraryMapping
{
    public static BiomePatternSummary ToDto(BiomePatternRow row) =>
        new(row.Id, row.Name, row.Kind, row.Params, Picture(row.Params));

    /// <summary>The picture, or an empty string for a row whose stored field will not read — a library that
    /// cannot draw one row still lists the rest.</summary>
    private static string Picture(string paramsJson) =>
        BiomeBody.Stated(paramsJson) is { } field ? StylePreview.BiomeSvg(field) : "";
}

/// <summary>GET /api/biome-patterns[?kind=solid|cell|noise] — the biome library, newest first, each with the
/// patch of ground it tints.</summary>
public sealed class BiomePatternListEndpoint(ThemeStore store) : EndpointWithoutRequest<List<BiomePatternSummary>>
{
    public override void Configure() { Get("/biome-patterns"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var kind = Query<string?>("kind", isRequired: false);
        var rows = await store.ListBiomesAsync(string.IsNullOrWhiteSpace(kind) ? null : kind, ct);
        await Send.OkAsync(rows.Select(BiomeLibraryMapping.ToDto).ToList(), ct);
    }
}

/// <summary>GET /api/biome-patterns/{id} — one pattern.</summary>
public sealed class BiomePatternGetEndpoint(ThemeStore store) : EndpointWithoutRequest<BiomePatternSummary>
{
    public override void Configure()
    { Get("/biome-patterns/{id}"); AllowAnonymous(); Description(b => b.Refuses(404)); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var row = await store.GetBiomeAsync(Route<long>("id"), ct);
        if (row is null) { await Refusals.NotFoundAsync(HttpContext, "biome pattern", ct); return; }
        await Send.OkAsync(BiomeLibraryMapping.ToDto(row), ct);
    }
}

/// <summary>POST /api/biome-patterns — save a new named field.</summary>
public sealed class BiomePatternCreateEndpoint(ThemeStore store)
    : Endpoint<BiomePatternSaveRequest, BiomePatternSummary>
{
    public override void Configure()
    { Post("/biome-patterns"); AllowAnonymous(); Description(b => b.Refuses(400)); }

    public override async Task HandleAsync(BiomePatternSaveRequest req, CancellationToken ct)
    {
        if (BiomeBody.Stated(req.Params) is null) { await BiomeBody.RefuseAsync(HttpContext, ct); return; }
        var row = new BiomePatternRow { Name = req.Name, Kind = req.Kind, Params = req.Params };
        row.Id = await store.CreateBiomeAsync(row, ct);
        await Send.OkAsync(BiomeLibraryMapping.ToDto(row), ct);
    }
}

/// <summary>PUT /api/biome-patterns/{id} — update a pattern in place. A map holds a snapshot rather than a key
/// into this library, so an edit here retints nothing already built.</summary>
public sealed class BiomePatternUpdateEndpoint(ThemeStore store)
    : Endpoint<BiomePatternSaveRequest, BiomePatternSummary>
{
    public override void Configure()
    { Put("/biome-patterns/{id}"); AllowAnonymous(); Description(b => b.Refuses(400, 404)); }

    public override async Task HandleAsync(BiomePatternSaveRequest req, CancellationToken ct)
    {
        if (BiomeBody.Stated(req.Params) is null) { await BiomeBody.RefuseAsync(HttpContext, ct); return; }
        var id = Route<long>("id");
        if (await store.UpdateBiomeAsync(id, req.Name, req.Kind, req.Params, ct) == 0)
        { await Refusals.NotFoundAsync(HttpContext, "biome pattern", ct); return; }
        await Send.OkAsync(new BiomePatternSummary(id, req.Name, req.Kind, req.Params,
            StylePreview.BiomeSvg(BiomeBody.Stated(req.Params)!)), ct);
    }
}

/// <summary>DELETE /api/biome-patterns/{id} — forget a pattern. Nothing is asked first: a map took a copy, so
/// no board changes when the row goes.</summary>
public sealed class BiomePatternDeleteEndpoint(ThemeStore store) : EndpointWithoutRequest
{
    public override void Configure()
    { Delete("/biome-patterns/{id}"); AllowAnonymous(); Description(b => b.Refuses(404)); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await store.DeleteBiomeAsync(Route<long>("id"), ct) == 0)
        { await Refusals.NotFoundAsync(HttpContext, "biome pattern", ct); return; }
        await Send.NoContentAsync(ct);
    }
}

/// <summary>POST /api/biome-patterns/preview — what a draft field draws, saving nothing. Body is a bare
/// <c>BiomeField</c>, unwrapped, the way the material preview takes a bare material.</summary>
public sealed class BiomePatternPreviewEndpoint : EndpointWithoutRequest<StyleCardDto>
{
    public override void Configure()
    {
        Post("/biome-patterns/preview"); AllowAnonymous();
        Description(b => b.Accepts<BiomeField>("application/json")
                          .Produces<StyleCardDto>(200, "application/json").Refuses(400));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var field = BiomeBody.Stated(await RawBody.ReadAsync(HttpContext, ct));
        if (field is null) { await BiomeBody.RefuseAsync(HttpContext, ct); return; }
        await Send.OkAsync(new StyleCardDto(StylePreview.BiomeSvg(field)), ct);
    }
}
