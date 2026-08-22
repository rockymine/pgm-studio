using PgmStudio.Domain;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using FastEndpoints;
using LinqToDB;
using LinqToDB.Async;
using PgmStudio.Analysis.Footprint;
using PgmStudio.Analysis.Playability;
using PgmStudio.Api.Services;
using PgmStudio.Contracts;
using PgmStudio.Data.Features;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Export;
using PgmStudio.Geom.Relief;
using PgmStudio.Minecraft;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Sketch;
using PgmStudio.Minecraft.Dressing;
using PgmStudio.Minecraft.Houses;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Endpoints;

/// <summary>POST /api/sketch — originate a sketch: create a draft (geometry-less) map + its layout artifact.
/// Returns the slug; the client navigates to <c>/maps/{slug}/sketch</c>. Body: optional {name} and an
/// optional working frame {width, depth, mode, centerX, centerZ}. When a frame is given the layout is seeded
/// with a <c>setup</c> (origin-centred bbox + symmetry centre + mode) so the editor frames the canvas on
/// open; without one the layout is empty {} and the editor falls back to its landscape default on load.</summary>
public sealed class SketchCreateEndpoint(MapRepository repo, MapArtifactStore artifacts) : EndpointWithoutRequest<OriginatedDto>
{
    public override void Configure()
    {
        Post("/sketch"); AllowAnonymous();
        Description(b => b.Accepts<SketchOriginateRequest>("application/json"));
    }

    // The default footprint: 2-team landscape (120×80), origin-centred, rotational symmetry — the same
    // default the editor/bridge use, applied to any frame field the body leaves out.
    private const double DefaultWidth = 120, DefaultDepth = 80;
    private const string DefaultMode = "rot_180";
    private static readonly HashSet<string> Modes = ["mirror_x", "mirror_z", "rot_180", "rot_90"];

    public override async Task HandleAsync(CancellationToken ct)
    {
        var name = SketchDiscard.UntouchedName;
        var hasFrame = false;
        double width = DefaultWidth, depth = DefaultDepth, centerX = 0, centerZ = 0;
        var mode = DefaultMode;
        try
        {
            using var doc = await JsonDocument.ParseAsync(HttpContext.Request.Body, cancellationToken: ct);
            var root = doc.RootElement;
            if (root.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String
                && n.GetString() is { } s && !string.IsNullOrWhiteSpace(s)) name = s.Trim();
            if (root.TryGetProperty("width", out var w) && w.ValueKind == JsonValueKind.Number) { width = w.GetDouble(); hasFrame = true; }
            if (root.TryGetProperty("depth", out var d) && d.ValueKind == JsonValueKind.Number) { depth = d.GetDouble(); hasFrame = true; }
            if (root.TryGetProperty("mode", out var m) && m.ValueKind == JsonValueKind.String
                && m.GetString() is { } mm && Modes.Contains(mm)) { mode = mm; hasFrame = true; }
            if (root.TryGetProperty("centerX", out var cx) && cx.ValueKind == JsonValueKind.Number) { centerX = cx.GetDouble(); hasFrame = true; }
            if (root.TryGetProperty("centerZ", out var cz) && cz.ValueKind == JsonValueKind.Number) { centerZ = cz.GetDouble(); hasFrame = true; }
        }
        catch { /* empty / invalid body → default name, no frame */ }

        var (mapId, slug) = await MapOrigin.UnderFreeSlugAsync(repo, name, MapStage.Sketch, ct);
        // Seed so GET works immediately: a framed create writes its setup; a frameless one stays empty {}.
        var seed = hasFrame ? SeedSetup(Math.Max(16, width), Math.Max(16, depth), mode, centerX, centerZ) : "{}"u8.ToArray();
        await artifacts.SaveAsync(mapId, ArtifactKind.SketchLayoutJson, seed, ct);
        await Send.OkAsync(new OriginatedDto(slug), ct);
    }

    // The browser layout blob's `setup` object — an origin-centred width×depth bbox, the symmetry centre, and
    // the mirror mode. Keys match what the JS bridge's load() / the editor's setup-sync read back.
    private static byte[] SeedSetup(double width, double depth, string mode, double centerX, double centerZ)
    {
        double hx = width / 2, hz = depth / 2;
        return JsonSerializer.SerializeToUtf8Bytes(new
        {
            setup = new
            {
                bbox = new { min_x = -hx, max_x = hx, min_z = -hz, max_z = hz },
                center = new { cx = centerX, cz = centerZ },
                mirror_mode = mode,
            },
        });
    }
}

/// <summary>GET /api/map/{slug}/sketch — the stored sketch layout (the JS-origin blob), or {} if none.</summary>
public sealed class SketchGetEndpoint(MapRepository repo, MapArtifactStore artifacts) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/map/{slug}/sketch");
        AllowAnonymous();
        // Declared rather than sent as the record: the blob is answered exactly as it was stored, and
        // re-serialising it through SketchLayout would drop whatever the reader has no field for — which is
        // the loss RQ3 exists to report on the way in, not to cause on the way out.
        Description(b => b.Produces<SketchLayout>(200, "application/json").Refuses(404));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;
        var data = await artifacts.LoadAsync(map.Id, ArtifactKind.SketchLayoutJson, ct);
        if (await artifacts.RevisionAsync(map.Id, ArtifactKind.SketchLayoutJson, ct) is { } revision)
            Revisions.Answer(HttpContext, revision);
        await Send.OkAsync(JsonSerializer.Deserialize<JsonElement>(data ?? "{}"u8.ToArray()), ct);
    }
}

/// <summary>PUT /api/map/{slug}/sketch — replace the stored layout blob (the bridge's getState()). 400
/// `{error, findings}` when a bound <c>roomStyles.cage</c> or <c>roomStyles.spawn</c> fails
/// <see cref="HouseStyleValidation"/> — this is where those snapshots actually enter the studio, so it is where
/// a wrong block or a see-through roof is refused rather than silently stamped at export.</summary>

/// <summary>PUT /api/map/{slug}/sketch — replace the stored layout blob (the bridge's getState()). 400
/// `{error, findings}` when a bound <c>roomStyles.cage</c> or <c>roomStyles.spawn</c> fails
/// <see cref="HouseStyleValidation"/> — this is where those snapshots actually enter the studio, so it is where
/// a wrong block or a see-through roof is refused rather than silently stamped at export.</summary>
public sealed class SketchPutEndpoint(MapRepository repo, MapArtifactStore artifacts) : EndpointWithoutRequest<AppliedDto>
{
    public override void Configure()
    {
        Put("/map/{slug}/sketch"); AllowAnonymous();
        Description(b => b.Accepts<SketchLayout>("application/json").Refuses(404, 409, 422));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;

        var bytes = await RawBody.BytesAsync(HttpContext, ct);
        var layoutJson = Encoding.UTF8.GetString(bytes);
        var findings = SketchRoomStyleGate.Check(layoutJson);
        if (await Refusals.StopAsync(HttpContext, 400, "invalid house style", findings, ct)) return;

        // The document's own gate: a board too large to realize is refused here, where it is stored, rather
        // than at the preview that would have to walk it. What it merely names and does not have rides back
        // on the success as complaints — the layout is saved, and the author is told what will not be built.
        var layout = SketchLayout.Stated(layoutJson);
        Complaints.Unread(HttpContext, layoutJson, layout);
        var document = SketchLayoutCheck.Check(layout);
        if (await Refusals.StopAsync(HttpContext, 422, "board too large", document, ct)) return;

        var written = await DocumentWrite.StoreAsync(artifacts, map.Id, ArtifactKind.SketchLayoutJson,
            "sketch layout", bytes, Revisions.Expected(HttpContext), ct);
        if (written.Refusal is { } refusal) { await Refusals.WriteAsync(HttpContext, refusal, ct); return; }

        Revisions.Answer(HttpContext, written.Revision!.Value);
        await Send.OkAsync(new AppliedDto(), ct);
    }
}

/// <summary>PUT /api/map/{slug}/sketch/from-plan — replace the stored layout with one a plan compiled,
/// carrying the map's existing finish onto it (<see cref="SketchLayout.CarryFinish"/>). The plan owns the
/// board; the sketch owns its themes, room shells and dressing, and a plan cannot express any of those — so
/// the compile path merges where <see cref="SketchPutEndpoint"/> replaces. A replace here would rebuild a
/// themed map into bare stone.
///
/// <para><b>A relief is carried the same way but refuses rather than merging silently.</b> It is keyed by
/// island, and island identity is derived from the geometry — so a recompile that re-fuses the board does not
/// merely move an island, it produces a different one, and terrain authored against the old fusion has
/// nowhere correct to land. Losing that is losing hours of hand work with no warning, so the endpoint answers
/// <b>409</b> — one <c>SK1</c> finding per orphaned island, the island id riding as the finding's subject —
/// and does not write. Sending <c>?force=true</c> accepts the loss and proceeds, which is the author's call to
/// make and not the server's.</para>
///
/// <para><b>A structural piece's stated height is carried a third way</b>
/// (<see cref="SketchLayout.CarryStructuralHeight"/>): matched by <c>intentRef</c>, not by shape id or
/// island, since the compiler regenerates both of those every time but a spawn or wool room keeps the same
/// team/owner:colour identity across a recompile. Only a shape the author actually corrected
/// (<c>height_authored</c>) carries forward — an untouched piece keeps tracking the plan's own
/// <c>surface</c>, so this never masks a deliberate plan-side height change.</para></summary>
public sealed class SketchFromPlanEndpoint(MapRepository repo, MapArtifactStore artifacts) : EndpointWithoutRequest<SketchFromPlanDto>
{
    public override void Configure()
    {
        Put("/map/{slug}/sketch/from-plan"); AllowAnonymous();
        Description(b => b.Accepts<SketchLayout>("application/json").Refuses(404, 409, 422));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;

        var compiled = await RawBody.ReadAsync(HttpContext, ct);
        try { using var _ = JsonDocument.Parse(compiled); }   // reject non-JSON; don't store garbage
        catch (JsonException fault)
        { await Refusals.UnreadableAsync(HttpContext, "invalid JSON", fault.Message, ct); return; }

        var stored = await artifacts.LoadAsync(map.Id, ArtifactKind.SketchLayoutJson, ct);
        var storedJson = stored is null ? null : Encoding.UTF8.GetString(stored);

        var orphans = SketchLayout.OrphanedRelief(compiled, storedJson);
        if (orphans.Count > 0 && Query<bool>("force", isRequired: false) != true)
        {
            await Refusals.WriteAsync(HttpContext, 409, "relief would be orphaned",
            [.. orphans.Select(island => new Finding(SketchRules.ReliefOrphaned,
                $"the recompiled board has no island for the terrain authored on island {island}; retry "
                + "with ?force=true to discard it",
                Subjects: [island]))], ct);
            return;
        }

        // Over the posted document rather than the merged one: the carry only ever adds keys this map already
        // held, so a field named here is one the caller wrote and can correct.
        Complaints.Unread(HttpContext, compiled, SketchLayout.Stated(compiled));

        var merged = SketchLayout.CarryStructuralHeight(
            SketchLayout.CarryRelief(SketchLayout.CarryFinish(compiled, storedJson), storedJson), storedJson);

        // The same gate the plain PUT runs, over the document that is actually stored — which is the merged
        // one, not the posted one, since the carry is what decides whether a shape's theme has a registry to
        // find. This road is the one an agent drives (compile, patch, put), so a board whose names match
        // nothing is told here rather than only on the road a person takes.
        var document = SketchLayoutCheck.Check(merged);
        if (await Refusals.StopAsync(HttpContext, 422, "board too large", document, ct)) return;

        var written = await DocumentWrite.StoreAsync(artifacts, map.Id, ArtifactKind.SketchLayoutJson,
            "sketch layout", Encoding.UTF8.GetBytes(merged), Revisions.Expected(HttpContext), ct);
        if (written.Refusal is { } stale) { await Refusals.WriteAsync(HttpContext, stale, ct); return; }

        Revisions.Answer(HttpContext, written.Revision!.Value);
        await Send.OkAsync(new SketchFromPlanDto(orphans), ct);
    }
}

/// <summary>POST /api/map/{slug}/sketch/paint — the sketch's terrain paint as a palette-indexed block-pixel
/// payload (<c>xs</c>/<c>zs</c>/<c>palette</c>/<c>color_idx</c> + bounds), which the client expands into the
/// <c>colors</c> array the block-overlay bitmap path already decodes (docs/world-export/terrain-painting.md TP10). The body is the <em>live</em> layout — the
/// bridge's <c>getState()</c>, not the stored blob — so the overlay tracks unsaved edits; the stored intent
/// supplies team ownership, which is what a team-tinted material reads. Empty payload when nothing is
/// drawn; 400 on unparseable JSON.</summary>
public sealed class SketchPaintEndpoint(MapRepository repo, MapArtifactStore artifacts) : EndpointWithoutRequest<BlockPixelsDto>
{
    public override void Configure()
    {
        Post("/map/{slug}/sketch/paint"); AllowAnonymous();
        Description(b => b.Accepts<SketchLayout>("application/json").Refuses(404, 422));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;

        var layoutJson = await RawBody.ReadAsync(HttpContext, ct);

        if (await Refusals.StopAsync(HttpContext, 422, "board too large",
                SketchLayoutCheck.Check(layoutJson), ct)) return;

        IReadOnlyList<SurfaceCell> cells;
        try { cells = TerrainPreview.SketchPaintCells(layoutJson, await artifacts.LoadJsonOrEmptyAsync<MapIntent>(map.Id, ArtifactKind.MapIntentJson, ct)); }
        catch (Exception fault) when (fault is JsonException or ArgumentException
                                          or InvalidOperationException or FormatException
                                          or OverflowException or KeyNotFoundException)
        { await Refusals.UnreadableAsync(HttpContext, "could not paint layout", fault.Message, ct); return; }

        await Send.OkAsync(cells.Count == 0 ? LayerData.EmptyPixels() : LayerData.PalettePixels(cells), ct);
    }
}

/// <summary>POST /api/map/{slug}/sketch/columns — the whole built world as per-column runs, which is what the
/// 3-D preview draws (docs/tools/sketch.md). The body is the <em>live</em> layout, the same as the paint and
/// contour previews take, and the stored intent rides along so the structures a map has stated are standing in
/// the picture.
///
/// <para>This is the paint overlay widened from the surface to the whole column. That preview resolves every
/// block and then keeps only each column's top because resolving the rest was the bulk of the call; here the
/// rest is the answer, so nothing is thrown away. The cost is the build rather than the payload — measured at
/// roughly a second on a full board against forty milliseconds to read the columns out of it — which is why
/// the client fetches this on entering the preview and not on every edit.</para>
///
/// <para>A map begun in Sketch has no intent, and an empty one is the right answer rather than a gap: it
/// states no objectives, so a preview showing none is showing what is there. 400 on a layout that cannot be
/// built.</para>
///
/// <para><b>What did not land comes back with what did</b>, under <c>warnings</c>: every prop the dressing
/// pass declined, as a <c>DR-*</c> finding naming the rule, the cell and the prop. The build succeeded, so
/// these are complaints rather than refusals — but a caller looking at a preview with no tree in it needs to
/// be told the tree was declined, not left to notice.</para></summary>
public sealed class SketchColumnsEndpoint(MapRepository repo, MapArtifactStore artifacts) : EndpointWithoutRequest<WorldColumnsDto>
{
    public override void Configure()
    {
        Post("/map/{slug}/sketch/columns"); AllowAnonymous();
        Description(b => b.Accepts<SketchLayout>("application/json").Refuses(404, 422));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;

        var layoutJson = await RawBody.ReadAsync(HttpContext, ct);

        Findings document;
        try { document = SketchLayoutCheck.Check(layoutJson); }
        catch (JsonException fault)
        { await Refusals.UnreadableAsync(HttpContext, "invalid layout", fault.Message, ct); return; }
        if (await Refusals.StopAsync(HttpContext, 422, "board too large", document, ct)) return;

        WorldColumnsDto payload;
        try
        {
            var built = WorldBuilder.Build(layoutJson, await artifacts.LoadJsonOrEmptyAsync<MapIntent>(map.Id, ArtifactKind.MapIntentJson, ct));
            payload = WorldColumnPayload.Of(built.World);
            Complaints.Add(HttpContext, built.Declines);

            // OB17, asked here because this build already paid for everything it needs — the same ground the
            // export reads and the same resolved goals. A refusal at the export door is the last place to
            // learn a goal stands over the void; carried here it reaches an author while they are still
            // drawing, as a complaint, since nothing about this request is being refused.
            Complaints.Add(HttpContext,
                MapExportComposer.CheckGoalPlacement(built.Columns!, built.ResolvedIntent).AsComplaints());
        }
        // A dressing document that will not read is refused by name, exactly as the export refuses it — the
        // preview and the export cannot disagree about what a malformed prop list is.
        catch (DressingParseException fault)
        { await Refusals.WriteAsync(HttpContext, 422, "dressing document invalid", [fault.Finding], ct); return; }
        catch (Exception fault) when (fault is JsonException or ArgumentException
                                          or InvalidOperationException or FormatException
                                          or OverflowException or KeyNotFoundException)
        { await Refusals.UnreadableAsync(HttpContext, "could not build layout", fault.Message, ct); return; }

        await Send.OkAsync(payload, ct);
    }
}

/// <summary>POST /api/map/{slug}/sketch/relief — the contour overlay for whatever relief the posted layout
/// carries, one entry per relief-bearing island: its traced lines, its height range, and its bounds. The body
/// is the <em>live</em> layout, the same as the paint preview takes, so the overlay tracks unsaved edits.
///
/// <para>The solve is the build's own (<see cref="SketchRasterizer.ReliefFields"/>), so a previewed surface
/// cannot differ from the surface that gets built — the only property that makes a preview worth drawing.
/// Contours are traced from the <b>continuous</b> field rather than the block one, because contouring a
/// staircase returns the outlines of its treads instead of lines of constant height (docs/world-export/relief.md §15).
/// <c>?interval=</c> sets the spacing in blocks; a layout carrying no relief answers an empty list rather
/// than a 404, so the client can draw nothing through the same path.</para>
///
/// <para>Each island's solve <b>resumes</b> from the surface its last preview settled on
/// (<see cref="ReliefPreviewCache"/>). Every preview is one small edit after the last, so the relaxation has
/// that edit left to carry rather than the whole surface to build — and because it stops when the field stops
/// moving, a resumed solve that settles has settled on the same answer. Nothing about the reply depends on
/// whether a head start was available.</para></summary>
public sealed class SketchReliefEndpoint(MapRepository repo, ReliefPreviewCache warm)
    : EndpointWithoutRequest<ReliefContoursDto>
{
    public override void Configure()
    {
        Post("/map/{slug}/sketch/relief"); AllowAnonymous();
        Description(b => b.Accepts<SketchLayout>("application/json").Refuses(404, 422));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;

        var layoutJson = await RawBody.ReadAsync(HttpContext, ct);

        var interval = Query<double>("interval", isRequired: false);
        if (interval <= 0) interval = 1;

        // Each island resumes from the surface its last preview settled on. The relaxation stops when the
        // field stops moving and discards a resume that fails to reach that tolerance, so this can only ever
        // save sweeps — never change the answer, which is what keeps a previewed surface the built one.
        if (await Refusals.StopAsync(HttpContext, 422, "board too large",
                SketchLayoutCheck.Check(layoutJson), ct)) return;

        Dictionary<string, HeightField> fields;
        try
        {
            fields = SketchRasterizer.ReliefFields(layoutJson,
                (island, footprint) => warm.WarmStart(map.Id, island, footprint),
                (island, solved) => warm.Remember(map.Id, island, solved));
        }
        catch (Exception fault) when (fault is JsonException or ArgumentException
                                          or InvalidOperationException or FormatException
                                          or OverflowException or KeyNotFoundException)
        { await Refusals.UnreadableAsync(HttpContext, "could not solve relief", fault.Message, ct); return; }

        // Points go out as one flat [x, z, x, z, …] run per line — see ContourLineDto.
        var islands = fields.Select(entry => new ReliefIslandContoursDto(
            entry.Key, entry.Value.Min, entry.Value.Max,
            entry.Value.Footprint.MinX,
            entry.Value.Footprint.MinZ,
            entry.Value.Footprint.MinX + entry.Value.Footprint.Width - 1,
            entry.Value.Footprint.MinZ + entry.Value.Footprint.Depth - 1,
            [.. Contours.Of(entry.Value, interval).Select(line => new ContourLineDto(
                line.Level, line.Closed,
                [.. line.Points.SelectMany(point => new[] { point.X, point.Z })]))])).ToList();

        await Send.OkAsync(new ReliefContoursDto(interval, islands), ct);
    }
}

/// <summary>POST /api/map/{slug}/sketch/relief/read — what the relief a posted layout carries <b>charges</b>,
/// per island. Not a walkability score: a relief that is walkable everywhere is a field rather than a map, and
/// a single number ranks every deliberate barrier as a defect. The report states reachability at each of the
/// game's three thresholds (a jump, a placed block, building in earnest), separates <b>places</b> from
/// <b>ledges</b>, qualifies faces as cliffs by the corpus rule, measures crossings in <b>both</b> directions
/// (a drop is free the way it falls) and reports the symmetry error, which nothing else would show.
///
/// <para>It sits next to the document it describes, which is what makes a relief correctable by a generator or
/// an agent rather than only by eye. Same body as the contour endpoint — the live layout.</para></summary>
public sealed class SketchReliefReadEndpoint(MapRepository repo, ReliefPreviewCache warm)
    : EndpointWithoutRequest<ReliefReadDto>
{
    public override void Configure()
    {
        Post("/map/{slug}/sketch/relief/read"); AllowAnonymous();
        Description(b => b.Accepts<SketchLayout>("application/json").Refuses(404, 422));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;

        var layoutJson = await RawBody.ReadAsync(HttpContext, ct);

        if (await Refusals.StopAsync(HttpContext, 422, "board too large",
                SketchLayoutCheck.Check(layoutJson), ct)) return;

        Dictionary<string, HeightField> fields;
        SketchLayout? state;
        try
        {
            state = SketchLayout.Parse(layoutJson);
            fields = SketchRasterizer.ReliefFields(layoutJson,
                (island, footprint) => warm.WarmStart(map.Id, island, footprint),
                (island, solved) => warm.Remember(map.Id, island, solved));
        }
        catch (Exception fault) when (fault is JsonException or ArgumentException
                                          or InvalidOperationException or FormatException
                                          or OverflowException or KeyNotFoundException)
        { await Refusals.UnreadableAsync(HttpContext, "could not solve relief", fault.Message, ct); return; }

        var mode = state?.Setup?.MirrorMode;
        var cx = state?.Setup?.Center?.Cx ?? 0;
        var cz = state?.Setup?.Center?.Cz ?? 0;

        var islands = fields.Select(entry =>
        {
            var read = ReliefReadback.Read(entry.Value, mode, cx, cz);
            return new ReliefIslandReadDto(
                entry.Key, read.Cells, read.Low, read.High, read.Relief, read.Steps,
                [.. read.Tiers.Select(t => new ReliefTierDto(
                    t.Name, t.MaxStep, t.Share, t.Places, t.LargestPlace, t.Ledges))],
                // the whole list is long and the tail is all banks, so only the head is sent
                [.. read.Faces.Take(12).Select(f => new ReliefFaceDto(f.Facing, f.Width, f.Drop, f.Cliff))],
                read.Faces.Count, read.Cliffs,
                new ReliefFordsDto(read.AcrossX.Rows, read.AcrossX.OnFoot, read.AcrossX.WithBlock, read.AcrossX.Descended),
                new ReliefFordsDto(read.AcrossZ.Rows, read.AcrossZ.OnFoot, read.AcrossZ.WithBlock, read.AcrossZ.Descended),
                read.SymmetryError);
        }).ToList();

        await Send.OkAsync(new ReliefReadDto(islands), ct);
    }
}

/// <summary>POST /api/map/{slug}/sketch/finish — rasterize the stored layout into the world geometry
/// artifacts (layer.parquet / islands.json / segments) so the draft flows into the Configure wizard.
/// 422 only if the layout rasterizes to no ground at all.
///
/// <para>It does <b>not</b> ask for two islands. An island is a connected landmass, not a side: over the 320
/// readable worlds of the destroy-the-monument corpus, 17% are a single island and 26% carry a single major
/// one, so the commonest shape in that category — one continent both teams stand on — is exactly what a
/// two-island floor rejected. Symmetry decides whether a board has two sides, and it is stated in the setup
/// rather than counted in the ground.</para></summary>
public sealed class SketchFinishEndpoint(MapRepository repo, MapArtifactStore artifacts, WorldFeatureWriter writer)
    : EndpointWithoutRequest<SketchFinishedDto>
{
    public override void Configure() { Post("/map/{slug}/sketch/finish"); AllowAnonymous(); Description(b => b.Refuses(404, 422)); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;

        var finished = await SketchFinish.RunAsync(map.Id, repo, artifacts, writer, ct);
        if (finished.Refusal is { } refusal)
        {
            await Refusals.WriteAsync(HttpContext, refusal, ct);
            return;
        }

        // What the board names and does not have rides on the success — this is the last stage that can say so.
        Complaints.Add(HttpContext, finished.Complaints?.Complaints ?? []);
        await Send.OkAsync(new SketchFinishedDto(map.Slug, $"/maps/{map.Slug}/configure"), ct);
    }
}

/// <summary>DELETE /api/map/{slug}/sketch/discard-if-empty — drop a still-pristine sketch draft (the row
/// "New sketch" creates up front, then abandoned). The client calls this best-effort when it leaves the
/// Sketch tool. Discards only a draft that is genuinely untouched: sketch stage, still carrying the default
/// name, no authors, and nothing drawn — anything else is real work and is left alone. Returns
/// <c>{discarded}</c>; a missing map or a non-pristine one is a no-op success.</summary>
public sealed class SketchDiscardIfEmptyEndpoint(MapRepository repo, PgmDb db, MapArtifactStore artifacts)
    : EndpointWithoutRequest<DiscardedDto>
{
    public override void Configure() { Delete("/map/{slug}/sketch/discard-if-empty"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var discarded = await SketchDiscard.IfUntouchedAsync(
            repo, db, artifacts, Route<string>("slug")!, ct);
        await Send.OkAsync(new DiscardedDto(discarded), ct);
    }
}

