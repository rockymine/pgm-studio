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
            .Select(b => new PaintBlockDto(b.Id, b.Data, b.Name, b.Group, b.Hex, b.InFamily)).ToList(), ct);
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

/// <summary>POST /api/terrain/prop-preview — one placed prop and the terrain finish it stands on; returns a
/// sample patch the pass actually dressed, from above and cut open. The theme is part of the request because
/// what the paint leaves on top is what decides whether flora grows at all and what a path may repaint.</summary>
public sealed class PropPreviewEndpoint : Endpoint<PropPreviewRequest, DressingPreviewDto>
{
    public override void Configure() { Post("/terrain/prop-preview"); AllowAnonymous(); }

    public override async Task HandleAsync(PropPreviewRequest req, CancellationToken ct)
    {
        PlacedProp prop;
        try { prop = DressingJson.DeserializeProp(req.PropJson); }
        catch (DressingParseException ex)
        {
            AddError(ex.Message);
            await Send.ErrorsAsync(400, ct);
            return;
        }

        TerrainTheme theme;
        try { theme = PropOptionEndpoints.ThemeOf(req.ThemeJson); }
        catch (JsonException)
        {
            AddError("The theme JSON could not be read.");
            await Send.ErrorsAsync(400, ct);
            return;
        }
        await Send.OkAsync(DressingPreview.Views(prop, theme), ct);
    }
}

/// <summary>The three pickers a prop inspector offers, each drawn by the pass rather than described: GET
/// /api/terrain/path-styles, /api/terrain/boulder-forms, /api/terrain/species. Every card is the real
/// algorithm at card size, so a picker can never offer a look the export does not produce.
/// <para>Each takes the theme as a query parameter for the same reason the preview takes it in its body — a
/// gravel path on grass and the same path on sand are different pictures.</para></summary>
internal static class PropOptionEndpoints
{
    public static TerrainTheme ThemeOf(string? json)
        => string.IsNullOrWhiteSpace(json) ? TerrainTheme.Default : TerrainThemeJson.Deserialize(json);

    /// <summary>The material the author already chose for the prop, so a card shows <em>their</em> road or rock
    /// rather than a stock one. A blob that will not parse falls back rather than failing the picker: a card is
    /// a picture of a shape, and a shape is still worth showing in the wrong colour.</summary>
    public static TerrainMaterial MaterialOf(string? json, TerrainMaterial fallback)
    {
        if (string.IsNullOrWhiteSpace(json)) return fallback;
        try { return TerrainThemeJson.DeserializeMaterial(json); }
        catch (JsonException) { return fallback; }
    }
}

/// <summary>GET /api/terrain/path-styles — the five ways a stroke paves the ground it crosses, each drawn.</summary>
public sealed class PathStyleCardsEndpoint : EndpointWithoutRequest<List<PropOptionDto>>
{
    public override void Configure() { Get("/terrain/path-styles"); AllowAnonymous(); }

    public override Task HandleAsync(CancellationToken ct)
    {
        var pave = PropOptionEndpoints.MaterialOf(
            Query<string>("pave", isRequired: false), new SolidMaterial(Blocks.Gravel));
        var template = new PathProp { Radius = 3, Seed = 5, Pave = pave };
        return Send.OkAsync([.. DressingPreview.PathStyleCards(template, TerrainTheme.Default)], ct);
    }
}

/// <summary>GET /api/terrain/water-forms — the three channel forms, each an actual dug channel seen from above.</summary>
public sealed class WaterFormCardsEndpoint : EndpointWithoutRequest<List<PropOptionDto>>
{
    public override void Configure() { Get("/terrain/water-forms"); AllowAnonymous(); }

    public override Task HandleAsync(CancellationToken ct)
        => Send.OkAsync([.. DressingPreview.WaterFormCards(
            new WaterProp { Radius = 3, Depth = 2, Seed = 5 }, TerrainTheme.Default)], ct);
}

/// <summary>GET /api/terrain/boulder-forms — the four rock shapes, each an actual rock.</summary>
public sealed class BoulderFormCardsEndpoint : EndpointWithoutRequest<List<PropOptionDto>>
{
    public override void Configure() { Get("/terrain/boulder-forms"); AllowAnonymous(); }

    public override Task HandleAsync(CancellationToken ct)
    {
        var rock = PropOptionEndpoints.MaterialOf(
            Query<string>("rock", isRequired: false), new SolidMaterial(Blocks.Stone));
        return Send.OkAsync([.. DressingPreview.BoulderFormCards(
            new BoulderProp { Size = 3, Seed = 3, Rock = rock }, TerrainTheme.Default)], ct);
    }
}

/// <summary>GET /api/terrain/species — every vanilla species, each built.</summary>
public sealed class TreeSpeciesEndpoint : EndpointWithoutRequest<List<PropOptionDto>>
{
    public override void Configure() { Get("/terrain/species"); AllowAnonymous(); }

    public override Task HandleAsync(CancellationToken ct)
        => Send.OkAsync([.. DressingPreview.SpeciesCards(TerrainTheme.Default)], ct);
}

/// <summary>GET /api/terrain/woods — the six woods a grown tree can be cut from, each shown on the same tree.
/// The tree drawn is the one being edited, so the cards answer "what would mine look like in that wood"
/// rather than showing a stock shape nobody placed.</summary>
public sealed class TreeWoodEndpoint : EndpointWithoutRequest<List<PropOptionDto>>
{
    public override void Configure() { Get("/terrain/woods"); AllowAnonymous(); }

    public override Task HandleAsync(CancellationToken ct)
    {
        var template = new TreeProp
        {
            Form = TreeForm.Grown, Seed = 5,
            Height = Query<double?>("height", isRequired: false) ?? 18,
            Leader = Query<double?>("leader", isRequired: false) ?? 0.55,
            BranchAngle = Query<double?>("branchAngle", isRequired: false) ?? 1.1,
            Levels = Query<int?>("levels", isRequired: false) ?? 2,
            LeafSize = Query<double?>("leafSize", isRequired: false) ?? 0.6,
            Whorled = Query<bool?>("whorled", isRequired: false) ?? false,
            Stems = Query<int?>("stems", isRequired: false) ?? 1,
            Flow = Query<double?>("flow", isRequired: false) ?? 0.45,
        };
        return Send.OkAsync([.. DressingPreview.WoodCards(template, TerrainTheme.Default)], ct);
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
