using System.Text.Json;
using FastEndpoints;
using PgmStudio.Api.Services;
using PgmStudio.Contracts;
using PgmStudio.Data.Compose;
using PgmStudio.Data.Features;
using PgmStudio.Data.Map;
using PgmStudio.Data.Plan;
using PgmStudio.Data.Schema;
using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Render;

using PgmStudio.Domain;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Endpoints;

/// <summary>
/// GET /api/compose — the browse feed: a page of the composed-board library (<see cref="ComposedBoardLibrary"/>)
/// for the size band <c>players</c> falls in and a symmetry, best score first with the seed breaking ties.
/// <c>wools</c> must each be present, <c>hub</c> and <c>front</c> take any one form named, <c>woolCount</c> any
/// one count named, and <c>maxScore</c> caps the score. Nothing is composed on request, so a page is
/// a read and the feed ends where the library does. Every card is labelled for <c>players</c>. The census counts
/// every board the library holds for the band and symmetry, before the filters, so a chip can say what these
/// settings produce.
/// </summary>
public sealed class ComposeBrowseEndpoint(ComposedBoardStore library) : EndpointWithoutRequest<ComposePage>
{
    private const int MaxPage = 48;

    public override void Configure()
    {
        Get("/compose");
        Description(b => b.Reads(
            new QueryWord("players", "The player count the cards are labelled for, which picks the size band the "
                + "boards come from. Absent is 12, and out of range clamps.",
                Min: 6, Max: SizeBands.Players(SizeBands.Centi).High),
            new QueryWord("teams", $"The team count. The library holds {ComposedBoardLibrary.Teams}-team boards, "
                + "and any other count is refused.", Min: ComposedBoardLibrary.Teams, Max: ComposedBoardLibrary.Teams),
            new QueryWord("symmetry", "The symmetry the boards are laid to. Absent is `rot_180`.",
                [.. ComposedBoardLibrary.Symmetries]),
            new QueryWord("from", "Where the page starts, as the `next` the page before it answered. Absent is 0.",
                Min: 0),
            new QueryWord("count", $"How many cards the page holds. Absent is 12, and out of range clamps.",
                Min: 1, Max: MaxPage),
            new QueryWord("maxScore", "Only the boards scoring at most this. Absent caps nothing.",
                Value: QueryValue.Number),
            new QueryWord("woolCount", "Wool counts between commas, any one of which a board may carry. Absent "
                + "takes any."),
            new QueryWord("wools", "Wool forms between commas, every one of which a board must carry. Absent "
                + "requires none."),
            new QueryWord("hub", "Hub forms between commas, any one of which a board may have. Absent takes any."),
            new QueryWord("front", "Front forms between commas, any one of which a board may have. Absent takes "
                + "any.")));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var players = Math.Clamp(Query<int?>("players", isRequired: false) ?? 12, 6, SizeBands.Players(SizeBands.Centi).High);
        var teams = Query<int?>("teams", isRequired: false) ?? ComposedBoardLibrary.Teams;
        var symmetry = Query<string?>("symmetry", isRequired: false) ?? "rot_180";
        var from = Math.Max(0, Query<int?>("from", isRequired: false) ?? 0);
        var count = Math.Clamp(Query<int?>("count", isRequired: false) ?? 12, 1, MaxPage);

        if (!ComposedBoardLibrary.Symmetries.Contains(symmetry))
        {
            await Refusals.UnreadableAsync(HttpContext, "unsupported symmetry",
                $"symmetry '{symmetry}' is not one of {string.Join(" and ", ComposedBoardLibrary.Symmetries)}, the symmetries "
                + "the layout library holds", ct, field: "symmetry");
            return;
        }
        if (teams != ComposedBoardLibrary.Teams)
        {
            await Refusals.UnreadableAsync(HttpContext, "unsupported team count",
                $"team count '{teams}' is not {ComposedBoardLibrary.Teams}, the count the layout library holds",
                ct, field: "teams");
            return;
        }

        var band = SizeBands.Of(players);
        if (await library.ServedVersionAsync(band, symmetry, ComposerVersion.Current, ComposedBoardLibrary.PerBand, ct)
            is not { } version)
        {
            await Send.OkAsync(new ComposePage([], 0, true, 0, ComposedBoardLibrary.Census([]), BoardKey.Entries), ct);
            return;
        }

        var query = new ComposedBoardQuery(
            version, band, symmetry,
            Query<double?>("maxScore", isRequired: false),
            [.. Csv("woolCount").Select(token => int.TryParse(token, out var count) ? count : -1)],
            Csv("wools"), Csv("hub"), Csv("front"));
        var (rows, matching) = await library.PageAsync(query, from, count, ct);
        var census = ComposedBoardLibrary.Census(await library.FormsAsync(version, band, symmetry, ct));
        var next = from + rows.Count;
        await Send.OkAsync(new ComposePage(
            [.. rows.Select(row => ComposedBoardLibrary.CardOf(row, players))], next, next >= matching, matching, census,
            BoardKey.Entries), ct);
    }

    private List<string> Csv(string key) =>
        [
            .. (Query<string?>(key, isRequired: false) ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(token => token.ToLowerInvariant()),
        ];
}

/// <summary>
/// POST /api/compose/map — make a map of a browse card: the library board its descriptor names, whichever composer
/// version made it, labelled for the descriptor's player count and stored as a map's source would be
/// (<see cref="MapSource"/>) — its layout and intent as the base, each island given its team — under the first free
/// slug its name gives. Answers the slug; the Sketch tool opens it. 404 for a
/// board the library does not hold.
/// </summary>
public sealed class ComposeMapEndpoint(
    ComposedBoardStore library, MapRepository repo, MapReader reader, MapWriter writer, MapArtifactStore artifacts,
    WorldFeatureWriter features, PgmDb db, PlayerLookup players, MapChangeLog log, LibraryNames names)
    : Endpoint<ComposeRequestDto, OriginatedDto>
{
    public override void Configure() { Post("/compose/map"); Description(b => b.Refuses(404, 422)); }

    public override async Task HandleAsync(ComposeRequestDto req, CancellationToken ct)
    {
        try { _ = new ComposeRequest(req.Players, req.Teams, req.Symmetry, req.Seed, req.Cell); }
        catch (ArgumentException fault)
        { await Refusals.UnreadableAsync(HttpContext, "invalid descriptor", fault, ct); return; }

        var band = SizeBands.Of(req.Players);
        if (await library.GetAsync(req.ComposerVersion, band, req.Symmetry, req.Cell, req.Seed, ct) is not { } board)
        {
            await Refusals.NotFoundAsync(HttpContext, "library layout", ct,
                $"{band} {req.Symmetry} seed {req.Seed} by composer {req.ComposerVersion}");
            return;
        }

        var sketch = ComposedBoardLibrary.SketchOf(board, req.Players);
        PlanCompile.AssignIslands(sketch.Layout, sketch.Intent);
        var source = JsonSerializer.Serialize(new
        {
            layout = sketch.Layout,
            intent = sketch.Intent,
            note = $"composed: {band} {board.Symmetry} seed {board.Seed} by composer {board.ComposerVersion}",
        }, MapArtifactStore.Json);
        var slug = await repo.UniqueSlugAsync(Slugs.Of(sketch.Name), ct);
        var applied = await MapSource.ApplyAsync(HttpContext, slug, source, dry: false, discarding: null,
            repo, reader, writer, artifacts, features, db, players, log, names, ct);
        if (applied.Refusal is { } refusal)
        {
            await Refusals.WriteAsync(HttpContext, refusal, ct);
            return;
        }
        await Send.OkAsync(new OriginatedDto(slug), ct);
    }
}

/// <summary>GET /api/plans/{id}/svg — draw a stored plan row as the board SVG a plan is drawn in. 404 when the plan
/// is missing.</summary>
public sealed class PlanSvgEndpoint(PlanStore store) : EndpointWithoutRequest<SvgDto>
{
    public override void Configure() { Get("/plans/{id}/svg"); Description(b => b.Refuses(404, 422)); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var row = await store.GetByIdAsync(Route<long>("id"), ct);
        if (row is null) { await Refusals.NotFoundAsync(HttpContext, "stored plan", ct); return; }
        var plan = PlanModel.Parse(row.PlanJson);
        if (plan is null) { await Refusals.StoredUnreadableAsync(HttpContext, "plan", ct); return; }
        await Send.OkAsync(new SvgDto(PlanBoardSvg.Render(plan)), ct);
    }
}

/// <summary>GET /api/plans/{id}/ascii — the same fanned board as a grid of characters, one per proxy cell.
///
/// <para>A plan is a list of rectangles measured in cells, and most of what goes wrong with one is a
/// <b>relation between two of them</b> — a landform wider than the band that reaches it, a wall on the only
/// throat. A picture of the built world cannot show a relation between two rectangles, because by then they
/// are terrain; a grid puts them on the same rows. It is also the one render a caller with no image reader
/// can act on, which is why it answers <c>text/plain</c> rather than a JSON wrapper.</para>
///
/// <para><c>?every=N</c> draws one character per N cells, for a board wider than a terminal. 404 when the
/// plan is missing, 422 when the stored document cannot be read.</para></summary>
public sealed class PlanAsciiEndpoint(PlanStore store) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/plans/{id}/ascii");
        Description(b => b.PlainText().Refuses(404, 422).Reads(PlanAsciiPostEndpoint.Every));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var row = await store.GetByIdAsync(Route<long>("id"), ct);
        if (row is null) { await Refusals.NotFoundAsync(HttpContext, "stored plan", ct); return; }
        var plan = PlanModel.Parse(row.PlanJson);
        if (plan is null) { await Refusals.StoredUnreadableAsync(HttpContext, "plan", ct); return; }

        HttpContext.Response.ContentType = "text/plain; charset=utf-8";
        await HttpContext.Response.WriteAsync(PlanBoardAscii.Render(plan, every: Query<int?>("every", false) ?? 1), ct);
    }
}

/// <summary>GET /api/plans/{id}/png — the same fanned board as <see cref="PlanSvgEndpoint"/>, rasterized: a
/// picture an image reader can actually open, where the vector card cannot be. Draws off
/// <see cref="PlanBoardScene"/>, the geometry the SVG endpoint shares, so the two can never disagree about
/// what the plan looks like. 404 when the plan is missing.</summary>
public sealed class PlanPngEndpoint(PlanStore store) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/plans/{id}/png");
        Description(b => b.Png().Refuses(404, 422));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var row = await store.GetByIdAsync(Route<long>("id"), ct);
        if (row is null) { await Refusals.NotFoundAsync(HttpContext, "stored plan", ct); return; }
        var plan = PlanModel.Parse(row.PlanJson);
        if (plan is null) { await Refusals.StoredUnreadableAsync(HttpContext, "plan", ct); return; }

        var png = PlanBoardPng.Render(plan);
        HttpContext.Response.ContentType = "image/png";
        await HttpContext.Response.Body.WriteAsync(png, ct);
    }
}

/// <summary><see cref="PlanBoardPalette.Key"/> on the wire: what a page of board pictures is read by.</summary>
internal static class BoardKey
{
    public static readonly IReadOnlyList<BoardKeyEntry> Entries =
        [.. PlanBoardPalette.Key.Select(entry => new BoardKeyEntry(
            entry.Label, entry.Ink.Name, PlanBoardPalette.Hex(entry.Ink.Fill), PlanBoardPalette.Hex(entry.Ink.Edge), entry.Dashed))];
}
