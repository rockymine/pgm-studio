using PgmStudio.Domain;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using FastEndpoints;
using LinqToDB;
using LinqToDB.Async;
using PgmStudio.Api.Access;
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
using PgmStudio.Minecraft.Palette;
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
        Post("/sketch");
        Description(b => b.Accepts<SketchOriginateRequest>("application/json"));
    }

    // The default footprint: 2-team landscape (120×80), origin-centred, rotational symmetry — the same
    // default the editor/bridge use, applied to any frame field the body leaves out.
    private const double DefaultWidth = 120, DefaultDepth = 80;
    private const string DefaultMode = SymmetryModes.Rot180;

    public override async Task HandleAsync(CancellationToken ct)
    {
        var name = DraftDiscard.SketchName;
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
                && m.GetString() is { } mm && SymmetryModes.All.Contains(mm)) { mode = mm; hasFrame = true; }
            if (root.TryGetProperty("centerX", out var cx) && cx.ValueKind == JsonValueKind.Number) { centerX = cx.GetDouble(); hasFrame = true; }
            if (root.TryGetProperty("centerZ", out var cz) && cz.ValueKind == JsonValueKind.Number) { centerZ = cz.GetDouble(); hasFrame = true; }
        }
        catch { /* empty / invalid body → default name, no frame */ }

        var (mapId, slug) = await MapOrigin.UnderFreeSlugAsync(
            repo, name, MapStage.Sketch, Callers.OriginatorOf(HttpContext), ct);
        var seed = Seed(hasFrame ? Math.Max(16, width) : null, Math.Max(16, depth), mode, centerX, centerZ);
        await artifacts.SaveAsync(mapId, ArtifactKind.SketchLayoutJson, seed, ct);
        await Send.OkAsync(new OriginatedDto(slug), ct);
    }

    /// <summary>The layout a fresh board starts as: its ground layer, and the working frame where one was
    /// given. A board is a stack, and a flat one is a stack of one — so the ground is written here rather
    /// than invented by whichever surface draws on the board first, which is what left an API-made board
    /// with no layer to draw on and a browser-made one with a random id for its ground.</summary>
    private static byte[] Seed(double? width, double depth, string mode, double centerX, double centerZ)
    {
        double hx = (width ?? 0) / 2, hz = depth / 2;
        return JsonSerializer.SerializeToUtf8Bytes(new SketchLayout
        {
            Setup = width is null ? null : new SketchSetup
            {
                Bbox = new SketchBbox { MinX = -hx, MaxX = hx, MinZ = -hz, MaxZ = hz },
                Center = new SketchCenter { Cx = centerX, Cz = centerZ },
                MirrorMode = mode,
            },
            Layers = [SketchLayer.Ground()],
        }, Sparse);
    }

    /// <summary>The layout reader's own options, writing only what the seed states — a fresh board carries a
    /// frame and a ground layer, and a key spelled <c>null</c> is a statement the board has not made.</summary>
    private static readonly JsonSerializerOptions Sparse = new(SketchLayout.Json)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}

/// <summary>GET /api/map/{slug}/sketch — the stored sketch layout (the JS-origin blob), or {} if none.</summary>
public sealed class SketchGetEndpoint(MapRepository repo, MapArtifactStore artifacts) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/map/{slug}/sketch");
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

/// <summary>PUT /api/map/{slug}/sketch — replace the stored layout blob (the bridge's getState()).
///
/// <para><b>A drawing in progress is stored whatever it says.</b> The board's own geometry is checked here
/// and every finding rides back as a complaint, refusals included: a sketch is the author's working document,
/// and the orders an edit arrives in run through states the finished board would not allow — a floor drawn
/// under a hole before the hole goes, a wall raised before the ground it stands on. Refusing the store there
/// loses the work, and the client cannot even see that it did. <see cref="SketchFinishEndpoint"/> is where
/// the same check becomes fatal, which is the stage that declares the drawing done.</para>
///
/// <para>400 `{error, findings}` when a bound <c>roomStyles.wool</c> or <c>roomStyles.spawn</c> fails
/// <see cref="HouseStyleValidation"/>. That one still refuses: a style snapshot enters the studio here and
/// nowhere else, so a wrong block or a see-through roof caught anywhere later is caught at export.</para></summary>
public sealed class SketchPutEndpoint(MapRepository repo, MapArtifactStore artifacts) : EndpointWithoutRequest<AppliedDto>
{
    public override void Configure()
    {
        Put("/map/{slug}/sketch");
        Description(b => b.Accepts<SketchLayout>("application/json").Refuses(400, 404, 409));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;

        var bytes = await RawBody.BytesAsync(HttpContext, ct);
        var layoutJson = Encoding.UTF8.GetString(bytes);
        var findings = SketchMaterialGate.Check(layoutJson);
        if (await Refusals.StopAsync(HttpContext, 400, "invalid style or theme", findings, ct)) return;

        // The document's own gate, read but never fatal: the layout is stored and the author is told what
        // will not be built, which is what a working document owes them.
        var layout = SketchLayout.Stated(layoutJson);
        Complaints.Unread(HttpContext, layoutJson, layout);
        var document = SketchLayoutCheck.Check(layout);
        Complaints.Add(HttpContext, document.AsComplaints());

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
/// group, and group identity is derived from the geometry — so a recompile that re-fuses the board does not
/// merely move a group, it produces a different one, and terrain authored against the old fusion has
/// nowhere correct to land. Losing that is losing hours of hand work with no warning, so the endpoint answers
/// <b>409</b> — one <c>SK1</c> finding per orphaned group, the group id riding as the finding's subject —
/// and does not write. Sending <c>?force=true</c> accepts the loss and proceeds, which is the author's call to
/// make and not the server's.</para>
///
/// <para><b>A structural piece's stated height is carried a third way</b>
/// (<see cref="SketchLayout.CarryStructuralHeight"/>): matched by <c>intentRef</c>, not by shape id or
/// group, since the compiler regenerates both of those every time but a spawn or wool room keeps the same
/// team/owner:colour identity across a recompile. Only a shape the author actually corrected
/// (<c>height_authored</c>) carries forward — an untouched piece keeps tracking the plan's own
/// <c>surface</c>, so this never masks a deliberate plan-side height change.</para></summary>
public sealed class SketchFromPlanEndpoint(MapRepository repo, MapArtifactStore artifacts) : EndpointWithoutRequest<SketchFromPlanDto>
{
    public override void Configure()
    {
        Put("/map/{slug}/sketch/from-plan");
        Description(b => b.Accepts<SketchLayout>("application/json").Refuses(400, 404, 409).Reads(
            new QueryWord("force", "Recompile even where the stored relief is authored on a group the new board has "
                + "none of, discarding that terrain. Absent refuses 409, naming each group.", Value: QueryValue.Flag)));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;

        var compiled = await RawBody.ReadAsync(HttpContext, ct);
        try { using var _ = JsonDocument.Parse(compiled); }   // reject non-JSON; don't store garbage
        catch (JsonException fault)
        { await Refusals.UnreadableAsync(HttpContext, "invalid JSON", fault, ct); return; }

        var stored = await artifacts.LoadAsync(map.Id, ArtifactKind.SketchLayoutJson, ct);
        var storedJson = stored is null ? null : Encoding.UTF8.GetString(stored);

        var orphans = SketchLayout.OrphanedRelief(compiled, storedJson);
        if (orphans.Count > 0 && Query<bool>("force", isRequired: false) != true)
        {
            await Refusals.WriteAsync(HttpContext, 409, "relief would be orphaned",
            [.. orphans.Select(group => new Finding(SketchRules.ReliefOrphaned,
                $"group '{group}' holds terraform in the stored layout, and the recompiled layout has no such group",
                Subjects: [group]))], ct);
            return;
        }

        // Over the posted document rather than the merged one: the carry only ever adds keys this map already
        // held, so a field named here is one the caller wrote and can correct.
        Complaints.Unread(HttpContext, compiled, SketchLayout.Stated(compiled));

        // A relief in the posted body loses to the stored one, which is what the carry is for — and is a
        // silence for the caller that compiled, patched a relief on and posted the result, since that is the
        // road this route documents. Named rather than dropped.
        foreach (var group in SketchLayout.ReliefReplaced(compiled, storedJson))
            Complaints.Add(HttpContext, [new Finding(SketchRules.ReliefReplaced,
                $"the terraform posted for group '{group}' is not the terraform stored",
                Severity.Complaint, Field: $"relief.{group}", Subjects: [group])]);

        // Geometry is the plan's, so a shape drawn in the sketch is carried by nothing — and said so.
        var dropped = SketchLayout.DroppedShapes(compiled, storedJson);
        if (dropped.Count > 0)
            Complaints.Add(HttpContext, [new Finding(SketchRules.ShapeDropped,
                $"{(dropped.Count == 1 ? "shape" : "shapes")} {Wording.Ids(dropped)} of the sketch "
                + $"{(dropped.Count == 1 ? "has" : "have")} no counterpart in the plan the rebuild compiles",
                Severity.Complaint, Field: "layers", Subjects: dropped)]);

        var merged = SketchLayout.CarryStructuralHeight(
            SketchLayout.CarryRelief(SketchLayout.CarryFinish(compiled, storedJson), storedJson), storedJson);

        // Both gates the plain PUT runs, over the document that is actually stored — which is the merged
        // one, not the posted one, since the carry is what decides whether a shape's theme has a registry to
        // find, and whether a room style came across. This road is the one an agent drives (compile, patch,
        // put), so a board whose names match nothing, or whose houses are built of the wrong blocks, is told
        // here rather than only on the road a person takes.
        if (await Refusals.StopAsync(HttpContext, 400, "invalid style or theme",
                SketchMaterialGate.Check(merged), ct)) return;

        var document = SketchLayoutCheck.Check(merged);
        Complaints.Add(HttpContext, document.AsComplaints());

        var written = await DocumentWrite.StoreAsync(artifacts, map.Id, ArtifactKind.SketchLayoutJson,
            "sketch layout", Encoding.UTF8.GetBytes(merged), Revisions.Expected(HttpContext), ct);
        if (written.Refusal is { } stale) { await Refusals.WriteAsync(HttpContext, stale, ct); return; }

        Revisions.Answer(HttpContext, written.Revision!.Value);
        await Send.OkAsync(new SketchFromPlanDto(orphans, dropped), ct);
    }
}

/// <summary>POST /api/map/{slug}/sketch/paint — the sketch's terrain paint as a palette-indexed block-pixel
/// payload (<c>xs</c>/<c>zs</c>/<c>palette</c>/<c>color_idx</c> + bounds), which the client expands into the
/// <c>colors</c> array the block-overlay bitmap path already decodes (docs/world-export/terrain-painting.md TP10). The body is the <em>live</em> layout — the
/// bridge's <c>getState()</c>, not the stored blob — so the overlay tracks unsaved edits; the stored intent
/// supplies team ownership, which is what a team-tinted material reads. Empty payload when nothing is
/// drawn; 400 on unparseable JSON.</summary>
[CostlyRead]
public sealed class SketchPaintEndpoint(MapRepository repo, MapArtifactStore artifacts) : EndpointWithoutRequest<BlockPixelsDto>
{
    public override void Configure()
    {
        Post("/map/{slug}/sketch/paint");
        Description(b => b.Accepts<SketchLayout>("application/json").Refuses(400, 404));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;

        var layoutJson = await RawBody.ReadAsync(HttpContext, ct);

        Complaints.Add(HttpContext, SketchLayoutCheck.Check(layoutJson).AsComplaints());

        TerrainPreview.SketchPaint painted;
        try { painted = TerrainPreview.SketchPaintCells(layoutJson, await artifacts.LoadJsonOrEmptyAsync<MapIntent>(map.Id, ArtifactKind.MapIntentJson, ct)); }
        catch (Exception fault) when (fault is JsonException or ArgumentException
                                          or InvalidOperationException or FormatException
                                          or OverflowException or KeyNotFoundException)
        { await Refusals.UnreadableAsync(HttpContext, "could not paint layout", fault, ct); return; }

        // Grass, leaves and water take the colour of the ground they stand on, so the swatch is the block's
        // and the column's biome together — which is what makes a painted biome visible in the studio at all.
        await Send.OkAsync(painted.Cells.Count == 0
            ? BlockPixels.EmptyPixels()
            : BlockPixels.PalettePixels(painted.Cells, cell => BlockPalette.Hex(
                  cell.BlockId, cell.BlockData, painted.BiomeAt(cell.X, cell.Z), cell.X, cell.Z)), ct);
    }
}

/// <summary>POST /api/map/{slug}/sketch/columns — the whole built world as per-column runs, which is what the
/// 3-D preview draws (docs/tools/sketch.md). The body is the <em>live</em> layout, the same as the paint and
/// contour previews take, and the stored intent rides along so the structures a map has stated are standing in
/// the picture.
///
/// <para>This is the paint overlay widened from the surface to the whole column. That preview resolves every
/// block and then keeps only each column's top because resolving the rest was the bulk of the call; here the
/// rest is the answer, so nothing is thrown away. The cost is the build, seconds on a full board, which is why
/// the client fetches this on entering the preview and not on every edit; the answer is kept with the world
/// (<see cref="SketchPreviews"/>), so a board previewed again unchanged is read rather than checked and walked
/// again.</para>
///
/// <para>A map begun in Sketch has no intent, and an empty one is the right answer rather than a gap: it
/// states no objectives, so a preview showing none is showing what is there. 400 on a layout that cannot be
/// built.</para>
///
/// <para><b>What did not land comes back with what did</b>, under <c>warnings</c>: every prop the dressing
/// pass declined, as a <c>DR-*</c> finding naming the rule, the cell and the prop. The build succeeded, so
/// these are complaints rather than refusals — but a caller looking at a preview with no tree in it needs to
/// be told the tree was declined, not left to notice.</para></summary>
[CostlyRead]
[Queued]
public sealed class SketchColumnsEndpoint(MapRepository repo, MapArtifactStore artifacts) : EndpointWithoutRequest<WorldColumnsDto>
{
    public override void Configure()
    {
        Post("/map/{slug}/sketch/columns");
        Description(b => b.Accepts<SketchLayout>("application/json").Refuses(400, 404, 422));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;

        var layoutJson = await RawBody.ReadAsync(HttpContext, ct);

        SketchLayout? layout;
        try { layout = SketchLayout.Stated(layoutJson); }
        catch (JsonException fault)
        { await Refusals.UnreadableAsync(HttpContext, "invalid layout", fault, ct); return; }

        SketchPreview preview;
        try
        {
            var built = BuiltWorlds.Of(layoutJson, await artifacts.LoadJsonOrEmptyAsync<MapIntent>(map.Id, ArtifactKind.MapIntentJson, ct));
            preview = SketchPreviews.Of(built, layout, layoutJson);
            Complaints.Add(HttpContext, preview.Layout.AsComplaints());
            Complaints.Add(HttpContext, built.Declines);
            Complaints.Add(HttpContext, preview.Goals.AsComplaints());
            Complaints.Add(HttpContext, preview.Sites);
        }
        // A dressing document that will not read is refused by name, exactly as the export refuses it — the
        // preview and the export cannot disagree about what a malformed prop list is.
        catch (DressingParseException fault)
        { await Refusals.WriteAsync(HttpContext, 422, "dressing document invalid", [fault.Finding], ct); return; }
        catch (Exception fault) when (fault is JsonException or ArgumentException
                                          or InvalidOperationException or FormatException
                                          or OverflowException or KeyNotFoundException)
        { await Refusals.UnreadableAsync(HttpContext, "could not build layout", fault, ct); return; }

        await Send.OkAsync(preview.Columns, ct);
    }
}

/// <summary>POST /api/map/{slug}/sketch/dressing — what the dressing pass would place, run against the posted
/// layout and stopped before anything is written. The body is the <em>live</em> layout, the same as the paint
/// preview and the columns read take.
///
/// <para>A prop's placement is computed inside the pass and reachable only by building and exporting a world,
/// so a keep-out has to be reasoned about instead: how wide is the band, where does the seed put the gaps,
/// which cells does a worn stroke actually keep. Every one of those is a guess, and the correction loop for a
/// guess is drive, read the declines, move the prop. This answers the claim itself — per prop the columns it
/// covers, where it rests and the height it resolved to, and every decline as the <c>DR-*</c> finding it
/// draws — off the same pass the export runs, so what it says is what will happen.</para>
///
/// <para>The cost is the build, roughly a second on a full board, which is why this is asked for rather than
/// pushed. 422 on a dressing document that will not read; a layout that cannot be built is drawn anyway and
/// its findings ride back as complaints, by the same names
/// the export refuses them under.</para></summary>
[CostlyRead]
[Queued]
public sealed class SketchDressingEndpoint(MapRepository repo, MapArtifactStore artifacts)
    : EndpointWithoutRequest<DressingRunDto>
{
    /// <summary>How many of a prop's columns are named. A stroke over a long board covers thousands and a
    /// keep-out is measured near its ends and its bends; a caller needing every cell can read the provenance
    /// sidecar off a build.</summary>
    private const int NamedCells = 100;

    public override void Configure()
    {
        Post("/map/{slug}/sketch/dressing");
        Description(b => b.Accepts<SketchLayout>("application/json").AlsoText().Refuses(400, 404, 422));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;

        if (await DressedBoard.OfAsync(HttpContext, map.Id, artifacts, ct) is not { } board) return;
        var (built, layoutJson, raster) = board;

        // The height each prop resolved to, read off the world this pass just built rather than re-derived
        // from the document: a second derivation is free to disagree with the one that placed the blocks.
        var tops = new Dictionary<(int X, int Z), int>();
        foreach (var column in PgmStudio.Minecraft.Anvil.WorldColumns.Of(built.World))
            if (column.Runs.Count > 0) tops[(column.X, column.Z)] = column.Runs.Max(run => run.YTop);

        var props = built.Dressing.Placements.Select(claim =>
        {
            var seat = claim.Cells.Count > 0 ? claim.Cells[0] : (X: 0, Z: 0);
            return new DressingPropDto(
                claim.Owner.Kind, claim.Owner.Unit, claim.Owner.Image,
                claim.Pass == PgmStudio.Minecraft.Anvil.ProvenancePass.Structure ? "structure" : "prop",
                claim.Cells.Count, seat.X, seat.Z, tops.GetValueOrDefault(seat, 0),
                [.. claim.Cells.Take(NamedCells).Select(cell => new CellDto(cell.X, cell.Z))]);
        }).ToList();

        if (TextAnswer.Wanted(HttpContext))
        {
            await TextAnswer.WriteAsync(HttpContext, ClaimRaster.Render(raster, props.Count, built.Dressing.Declines), ct);
            return;
        }

        var claims = new ClaimRasterDto(
            new Bounds2dDto(raster.MinX, raster.MinZ, raster.MinX + raster.Width - 1, raster.MinZ + raster.Height - 1),
            raster.Width, raster.Height, ClaimRaster.Classes, raster.Rows);
        await Send.OkAsync(new DressingRunDto(
            props, built.Dressing.Declines, props.Sum(prop => prop.Cells), claims), ct);
    }
}

/// <summary>The board a posted layout builds and what the dressing pass would say about every cell of it —
/// the one build the preview and the seat read both take, so a placement and the ground it is looked up on
/// cannot come from two boards. Null where the refusal has already been written to the response.</summary>
internal static class DressedBoard
{
    public static async Task<(BuiltWorld Built, string LayoutJson, ClaimRaster.Grid Claims)?> OfAsync(
        HttpContext http, long mapId, MapArtifactStore artifacts, CancellationToken ct)
    {
        var layoutJson = await RawBody.ReadAsync(http, ct);

        Findings document;
        try { document = SketchLayoutCheck.Check(layoutJson); }
        catch (JsonException fault)
        { await Refusals.UnreadableAsync(http, "invalid layout", fault, ct); return null; }
        Complaints.Add(http, document.AsComplaints());

        BuiltWorld built;
        try
        {
            built = BuiltWorlds.Of(layoutJson,
                await artifacts.LoadJsonOrEmptyAsync<MapIntent>(mapId, ArtifactKind.MapIntentJson, ct));
        }
        catch (DressingParseException fault)
        { await Refusals.WriteAsync(http, 422, "dressing document invalid", [fault.Finding], ct); return null; }
        catch (Exception fault) when (fault is JsonException or ArgumentException
                                          or InvalidOperationException or FormatException
                                          or OverflowException or KeyNotFoundException)
        { await Refusals.UnreadableAsync(http, "could not build layout", fault, ct); return null; }

        return (built, layoutJson, Claims(built, layoutJson));
    }

    /// <summary>What the dressing pass says about every cell of <paramref name="built"/>: each placement's claim
    /// and what the pass keeps clear, by the same two functions the pass itself asks a candidate site, read off
    /// the resolved intent — the goals' boxes as the build actually stamped them.</summary>
    public static ClaimRaster.Grid Claims(BuiltWorld built, string layoutJson) =>
        ClaimRaster.Read(built.Dressing.Placements, built.Surface,
            DressingScope.KeptClearAt(built.World, built.Surface, built.ResolvedIntent, built.Shells, layoutJson),
            DressingScope.GoalClearanceAt(built.ResolvedIntent));
}

/// <summary>POST /api/map/{slug}/sketch/seats — where a prop of a stated kind and footprint <b>may</b> stand,
/// which the five declines only ever answer backwards. The dressing pass refuses a placement for the ground
/// it rests on — a goal's clearance, the map's keep-out, another prop's claim, a route's standoff, no ground
/// at all — and every one of those is a predicate over a cell, so running them forwards over the whole board
/// answers where to put the thing instead of whether one guess landed.
///
/// <para>The footprint is a box, <c>width</c> by <c>depth</c> blocks, and a cell answers for the box laid
/// with its minimum corner there. A tree or a boulder is 1×1 at its trunk and needs no more; a building
/// states its <b>walls</b>, and the way past it is asked too (<c>DR-PASS</c>): the passage is measured from
/// the roof over those walls, and a candidate joins the group of any building standing within a passage of
/// it, exactly as the pass groups them. Its site is asked to be level (<c>DR-SLOPE</c>) against the style
/// <c>style</c> names — a house recipe in the posted layout's dressing, or the default building. What is
/// still the pass's to raise is named in the answer's <c>unasked</c>: <c>DR-CROSS</c> and <c>DR-WAY</c>, which
/// walk the board's routes and waypoints with the footprint taken out.</para>
///
/// <para>Body: the layout, as <c>sketch/dressing</c> takes it. The cost is the same build.</para></summary>
[CostlyRead]
[Queued]
public sealed class SketchSeatsEndpoint(MapRepository repo, MapArtifactStore artifacts)
    : EndpointWithoutRequest<SeatsDto>
{
    /// <summary>The largest footprint worth asking about: a building is capped at 20×20 by `ST9`, and a box
    /// wider than the board is a question with one answer.</summary>
    private const int WidestFootprint = 32;

    public override void Configure()
    {
        Post("/map/{slug}/sketch/seats");
        Description(b => b.Accepts<SketchLayout>("application/json").AlsoText().Refuses(400, 404, 422).Reads(
            new QueryWord("kind", "Whose placement rules to run — the standoff a route is kept at is the "
                + "kind's own. Absent runs `tree`'s.", [.. PlacedProp.Kinds]),
            new QueryWord("width", $"The footprint across, in blocks, 1 to {WidestFootprint}. Absent is 1. "
                + "For a building this is its walls: the roof over them reaches a block further, and the "
                + "passage beside it is measured from there.",
                Min: 1, Max: WidestFootprint),
            new QueryWord("depth", $"The footprint down, in blocks, 1 to {WidestFootprint}. Absent is "
                + "`width`, so one number asks about a square.", Min: 1, Max: WidestFootprint),
            new QueryWord("style", "For a building, the key of the house recipe in the posted layout's "
                + "`dressing.styles` it is built in: its height is what a site's rise is asked against "
                + "(`DR-SLOPE`). Absent is the default building.")));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;

        var kind = Query<string?>("kind", isRequired: false) is { Length: > 0 } asked ? asked : "tree";
        if (PlacedProp.PavingStandoffOf(kind) is not { } standoff)
        {
            await Refusals.WriteAsync(HttpContext, 422, "no such prop kind",
                [new Finding(RequestRules.NoSuchSubject,
                    $"the request's `kind` '{kind}' is not one of "
                    + string.Join(", ", PlacedProp.Kinds), Field: "kind")], ct);
            return;
        }

        var width = Math.Clamp(Query<int?>("width", isRequired: false) ?? 1, 1, WidestFootprint);
        var depth = Math.Clamp(Query<int?>("depth", isRequired: false) ?? width, 1, WidestFootprint);

        if (await DressedBoard.OfAsync(HttpContext, map.Id, artifacts, ct) is not { } board) return;

        ClaimRaster.Level? level = null;
        if (kind == "house")
        {
            var key = Query<string?>("style", isRequired: false);
            HouseStyle style = new();
            if (key is { Length: > 0 })
            {
                if (DressingScope.DocOf(board.LayoutJson).Styles.GetValueOrDefault(key) is not HouseStyleRef recipe)
                {
                    await Refusals.WriteAsync(HttpContext, 422, "no such house recipe",
                        [new Finding(RequestRules.NoSuchSubject,
                            $"the `style` of the request is '{key}', which names no recipe in `dressing.styles` of the layout",
                            Field: "style")], ct);
                    return;
                }
                style = recipe.Shell;
            }
            level = new ClaimRaster.Level(board.Built.Surface, SiteLevel.Limit(style));
        }
        var seating = ClaimRaster.Seat(board.Claims, kind, standoff, width, depth, level, SurfaceSoil.Of(board.Built));

        if (TextAnswer.Wanted(HttpContext))
        {
            await TextAnswer.WriteAsync(HttpContext, ClaimRaster.RenderSeats(seating), ct);
            return;
        }

        await Send.OkAsync(new SeatsDto(
            new Bounds2dDto(seating.MinX, seating.MinZ,
                            seating.MinX + seating.Width - 1, seating.MinZ + seating.Height - 1),
            seating.Width, seating.Height, seating.Kind, seating.Standoff,
            seating.FootprintWidth, seating.FootprintDepth, seating.Rows, seating.Seats,
            [.. seating.Refused.Select(because => new SeatRefusalDto(because.Rule, because.Cells))],
            seating.Unasked, seating.SlopeLimit), ct);
    }
}

/// <summary>What the surface block at a cell is rooted in, for the one placement rule that reads it
/// (<c>DR-ROOT</c>): the block under the standing level of the built board, asked of the same world the
/// dressing pass writes into. A cell off the board is not soil, which refuses it as the ground rule would.
/// </summary>
internal static class SurfaceSoil
{
    public static Func<int, int, bool> Of(BuiltWorld built) => (x, z) =>
        built.Surface.TryGetValue((x, z), out var standing) && standing > 0
        && DressingPalette.RootsInto(built.World.GetBlock(x, standing - 1, z).Id);
}

/// <summary>POST /api/map/{slug}/sketch/probe-footprint — whether a ring stands on ground, asked of the
/// <b>rasterised</b> footprint the build actually produces rather than of a model of the coast rebuilt outside
/// the studio. The two disagree by a cell or two, and a cell or two is the whole failure: a lift with no
/// ground under it reads no terrain, falls back to the shape's own floor and stands a stub of cobble in open
/// void, which nothing declines because a shape is terrain and terrain over void is a spur.
///
/// <para>The ring is not a shape the layout carries — that is the point, since the question is asked before a
/// shape is built on it. The answer separates <c>void</c> past the coast from a <c>hole</c> the footprint
/// encloses: a hub's slots and a U-shaped room's notch are made by <b>arrangement</b>, so no region marks one
/// and a shape dropped on top fills in the gap the layout was composed to have, silently.</para>
///
/// <para>Body: <c>{ "layout": {…}, "ring": [[x, z], …] }</c>, three points or more. 422 on a ring with fewer,
/// on a layout that will not read, and on a board too large.</para></summary>
[CostlyRead]
public sealed class SketchProbeFootprintEndpoint(MapRepository repo) : EndpointWithoutRequest<FootprintProbeDto>
{
    public override void Configure()
    {
        Post("/map/{slug}/sketch/probe-footprint");
        Description(b => b.Accepts<FootprintProbe.Request>("application/json").Refuses(404, 422));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;

        var body = await RawBody.ReadAsync(HttpContext, ct);
        string layoutJson;
        List<double[]> ring;
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (!root.TryGetProperty("layout", out var layout) || !root.TryGetProperty("ring", out var points)
                || points.ValueKind != JsonValueKind.Array)
            {
                await Refusals.UnreadableAsync(HttpContext, "invalid probe",
                    "the request's body lacks a `layout` or a `ring` array", ct);
                return;
            }
            layoutJson = layout.GetRawText();
            ring = [.. points.EnumerateArray()
                .Where(point => point.ValueKind == JsonValueKind.Array && point.GetArrayLength() >= 2)
                .Select(point => new[] { point[0].GetDouble(), point[1].GetDouble() })];
        }
        catch (Exception fault) when (fault is JsonException or InvalidOperationException or FormatException)
        { await Refusals.UnreadableAsync(HttpContext, "invalid probe", fault, ct); return; }

        // Three points is a triangle and the least a ring can be; two is a line, which covers no cell and
        // would answer "nothing stands on it" about a question nobody asked.
        if (ring.Count < 3)
        {
            await Refusals.WriteAsync(HttpContext, 422, "ring too short",
                [new Vocabulary.Finding(RequestRules.Unreadable,
                    $"the request's `ring` has {Wording.Count(ring.Count, "point")}, less than 3")], ct);
            return;
        }
        Complaints.Add(HttpContext, SketchLayoutCheck.Check(layoutJson).AsComplaints());

        FootprintProbe.Result probe;
        try { probe = FootprintProbe.Of(layoutJson, ring); }
        catch (Exception fault) when (fault is JsonException or ArgumentException
                                          or InvalidOperationException or FormatException
                                          or OverflowException or KeyNotFoundException)
        { await Refusals.UnreadableAsync(HttpContext, "could not read layout", fault, ct); return; }

        await Send.OkAsync(new FootprintProbeDto(
            probe.Cells, probe.Land, probe.Void, probe.Hole,
            [.. probe.VoidCells.Select(cell => new CellDto(cell.X, cell.Z))],
            [.. probe.HoleCells.Select(cell => new CellDto(cell.X, cell.Z))]), ct);
    }
}

/// <summary>POST /api/map/{slug}/sketch/relief — the contour overlay for the ground the posted layout builds,
/// one entry per group: its traced lines, its height range, and its bounds. Every group answers the surface the
/// build gives it (<see cref="SketchRasterizer.BuiltSurfaces"/>), traced along its relief's own field where it
/// carries one, so ground shaped by anchor heights, a height mode or a layer's own <c>base_y</c> is drawn too. The body
/// is the <em>live</em> layout, the same as the paint preview takes, so the overlay tracks unsaved edits.
///
/// <para>The solve is the build's own (<see cref="SketchRasterizer.ReliefFields"/>), so a previewed surface
/// cannot differ from the surface that gets built — the only property that makes a preview worth drawing.
/// Contours are traced from the <b>continuous</b> field rather than the block one, because contouring a
/// staircase returns the outlines of its treads instead of lines of constant height (docs/world-export/relief.md §15).
/// <c>?interval=</c> sets the spacing in blocks; a layout with no ground answers an empty list rather than a
/// 404, so the client can draw nothing through the same path.</para>
///
/// <para>Each group's solve <b>resumes</b> from the surface its last preview settled on
/// (<see cref="ReliefPreviewCache"/>). Every preview is one small edit after the last, so the relaxation has
/// that edit left to carry rather than the whole surface to build — and because it stops when the field stops
/// moving, a resumed solve that settles has settled on the same answer. Nothing about the reply depends on
/// whether a head start was available.</para></summary>
[CostlyRead]
public sealed class SketchReliefEndpoint(MapRepository repo, ReliefPreviewCache warm)
    : EndpointWithoutRequest<ReliefContoursDto>
{
    public override void Configure()
    {
        Post("/map/{slug}/sketch/relief");
        Description(b => b.Accepts<SketchLayout>("application/json").Refuses(400, 404).Reads(
            new QueryWord("interval", "The height between contours, in blocks. Absent, or not above 0, is 1.",
                Value: QueryValue.Number),
            new QueryWord("heights", "Answer each group's height field beside its contours. Absent answers the "
                + "contours alone.", Value: QueryValue.Flag)));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;

        var layoutJson = await RawBody.ReadAsync(HttpContext, ct);

        var interval = Query<double>("interval", isRequired: false);
        if (interval <= 0) interval = 1;

        // Each group resumes from the surface its last preview settled on. The relaxation stops when the
        // field stops moving and discards a resume that fails to reach that tolerance, so this can only ever
        // save sweeps — never change the answer, which is what keeps a previewed surface the built one.
        Complaints.Add(HttpContext, SketchLayoutCheck.Check(layoutJson).AsComplaints());

        Dictionary<string, HeightField> fields;
        Dictionary<string, HeightField> built;
        try
        {
            fields = SketchRasterizer.ReliefFields(layoutJson,
                (group, footprint) => warm.WarmStart(map.Id, group, footprint),
                (group, solved) => warm.Remember(map.Id, group, solved));
            built = SketchRasterizer.BuiltSurfaces(layoutJson, fields);
        }
        catch (Exception fault) when (fault is JsonException or ArgumentException
                                          or InvalidOperationException or FormatException
                                          or OverflowException or KeyNotFoundException)
        { await Refusals.UnreadableAsync(HttpContext, "could not solve relief", fault, ct); return; }

        // Points go out as one flat [x, z, x, z, …] run per line — see ContourLineDto. The solved surface goes
        // with them: contours say where the ground changes height and not which way, and the field they are
        // traced from is already in hand, so sending it costs the serialization and nothing else.
        var withHeights = Query<bool>("heights", isRequired: false);
        ReliefGroupContoursDto Entry(string group, HeightField field, bool solved) => new(
            group, solved, field.Min, field.Max,
            field.Footprint.MinX,
            field.Footprint.MinZ,
            field.Footprint.MinX + field.Footprint.Width - 1,
            field.Footprint.MinZ + field.Footprint.Depth - 1,
            [.. Contours.Of(field, interval).Select(line => new ContourLineDto(
                line.Level, line.Closed,
                [.. line.Points.SelectMany(point => new[] { point.X, point.Z })]))],
            withHeights ? Grid(field) : null);
        var groups = built.OrderBy(entry => fields.ContainsKey(entry.Key) ? 0 : 1)
            .Select(entry => Entry(entry.Key, entry.Value, solved: fields.ContainsKey(entry.Key)))
            .ToList();

        await Send.OkAsync(new ReliefContoursDto(interval, groups), ct);
    }

    /// <summary>The field's block heights over its own box, row-major, with null where the footprint holds no
    /// land. The box is rectangular and a landmass is not, so the holes have to be in the answer rather than
    /// left for a reader to infer from a shape it does not have.</summary>
    private static IReadOnlyList<int?> Grid(HeightField field)
    {
        var footprint = field.Footprint;
        var cells = new int?[footprint.Width * footprint.Depth];
        for (var index = 0; index < cells.Length; index++)
            if (footprint.InsideAt(index)) cells[index] = field.Blocks[index];
        return cells;
    }
}

/// <summary>POST /api/map/{slug}/sketch/relief/read — what the relief a posted layout carries <b>charges</b>,
/// per group. Not a walkability score: a relief that is walkable everywhere is a field rather than a map, and
/// a single number ranks every deliberate barrier as a defect. The report states reachability at each of the
/// game's three thresholds (a jump, a placed block, building in earnest), separates <b>places</b> from
/// <b>ledges</b>, qualifies faces as cliffs by the corpus rule, measures crossings in <b>both</b> directions
/// (a drop is free the way it falls) and reports the symmetry error, which nothing else would show.
///
/// <para>It sits next to the document it describes, which is what makes a relief correctable by a generator or
/// an agent rather than only by eye. Same body as the contour endpoint — the live layout.</para></summary>
[CostlyRead]
public sealed class SketchReliefReadEndpoint(MapRepository repo, ReliefPreviewCache warm)
    : EndpointWithoutRequest<ReliefReadDto>
{
    public override void Configure()
    {
        Post("/map/{slug}/sketch/relief/read");
        Description(b => b.Accepts<SketchLayout>("application/json").Refuses(400, 404));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;

        var layoutJson = await RawBody.ReadAsync(HttpContext, ct);

        Complaints.Add(HttpContext, SketchLayoutCheck.Check(layoutJson).AsComplaints());

        (ReliefReadDto Read, IReadOnlyList<Finding> Complaints) relief;
        try { relief = ReliefReads.Of(layoutJson, map.Id, warm); }
        catch (Exception fault) when (fault is JsonException or ArgumentException
                                          or InvalidOperationException or FormatException
                                          or OverflowException or KeyNotFoundException)
        { await Refusals.UnreadableAsync(HttpContext, "could not solve relief", fault, ct); return; }

        Complaints.Add(HttpContext, relief.Complaints);
        await Send.OkAsync(relief.Read, ct);
    }
}

/// <summary>POST /api/map/{slug}/sketch/finish — rasterize the stored layout into the world geometry
/// artifacts (layer.parquet / groups.json / segments) so the draft flows into the Configure wizard.
/// 422 only if the layout rasterizes to no ground at all.
///
/// <para>It does <b>not</b> ask for two groups. A group is a connected landmass, not a side: over the 320
/// readable worlds of the destroy-the-monument corpus, 17% are a single group and 26% carry a single major
/// one, so the commonest shape in that category — one continent both teams stand on — is exactly what a
/// two-group floor rejected. Symmetry decides whether a board has two sides, and it is stated in the setup
/// rather than counted in the ground.</para></summary>
public sealed class SketchFinishEndpoint(MapRepository repo, MapArtifactStore artifacts, WorldFeatureWriter writer)
    : EndpointWithoutRequest<SketchFinishedDto>
{
    public override void Configure() { Post("/map/{slug}/sketch/finish"); Description(b => b.Refuses(404, 422)); }

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

