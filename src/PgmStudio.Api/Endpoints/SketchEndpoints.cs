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
using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Api.Endpoints;

/// <summary>
/// Sketch tool persistence (docs/tools/sketch.md): the <c>sketch_layout_json</c>
/// artifact that backs a draft map. Mirrors <see cref="IntentStore"/> — it lives outside the
/// entity-replace codec, so it survives <c>MapWriter.SaveDocAsync</c>. The blob is the browser's
/// JS-origin layout ({setup, layout:{shapes, islands}}), stored verbatim (authoring source, not the
/// canonical document), so it is kept as raw bytes here rather than parsed into typed C#.
/// </summary>
internal static class SketchStore
{
    public static async Task<byte[]?> LoadAsync(PgmDb db, long mapId, CancellationToken ct)
    {
        var art = await db.Artifacts.FirstOrDefaultAsync(a => a.MapId == mapId && a.Kind == ArtifactKind.SketchLayoutJson, ct);
        return art?.Data;
    }

    /// <summary>Whether the map has a sketch layout — the durable "was a sketch" signal (the stage advances
    /// to <c>configure</c> on finish, so it can't distinguish origin).</summary>
    public static Task<bool> HasAsync(PgmDb db, long mapId, CancellationToken ct) =>
        db.Artifacts.AnyAsync(a => a.MapId == mapId && a.Kind == ArtifactKind.SketchLayoutJson, ct);

    public static async Task SaveAsync(PgmDb db, long mapId, byte[] data, CancellationToken ct)
    {
        await db.Artifacts.Where(a => a.MapId == mapId && a.Kind == ArtifactKind.SketchLayoutJson).DeleteAsync(ct);
        await db.InsertAsync(new MapArtifactRow { MapId = mapId, Kind = ArtifactKind.SketchLayoutJson, Data = data }, token: ct);
    }
}

/// <summary>POST /api/sketch — originate a sketch: create a draft (geometry-less) map + its layout artifact.
/// Returns the slug; the client navigates to <c>/maps/{slug}/sketch</c>. Body: optional {name} and an
/// optional working frame {width, depth, mode, centerX, centerZ}. When a frame is given the layout is seeded
/// with a <c>setup</c> (origin-centred bbox + symmetry centre + mode) so the editor frames the canvas on
/// open; without one the layout is empty {} and the editor falls back to its landscape default on load.</summary>
public sealed class SketchCreateEndpoint(MapRepository repo, PgmDb db) : EndpointWithoutRequest
{
    public override void Configure() { Post("/sketch"); AllowAnonymous(); }

    /// <summary>The name a blank "New sketch" draft is created with. A draft still carrying it (never
    /// renamed) is one signal the draft is pristine — see <see cref="SketchDiscardIfEmptyEndpoint"/>.</summary>
    public const string DefaultName = "Untitled sketch";

    // The default footprint: 2-team landscape (120×80), origin-centred, rotational symmetry — the same
    // default the editor/bridge use, applied to any frame field the body leaves out.
    private const double DefaultWidth = 120, DefaultDepth = 80;
    private const string DefaultMode = "rot_180";
    private static readonly HashSet<string> Modes = ["mirror_x", "mirror_z", "rot_180", "rot_90"];

    public override async Task HandleAsync(CancellationToken ct)
    {
        var name = DefaultName;
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

        var slug = await SketchSlug.UniqueAsync(repo, SketchSlug.Slugify(name), ct);
        var now = DateTime.UtcNow;
        var mapId = await repo.InsertAsync(new MapRow { Slug = slug, Name = name, Gamemode = "ctw", Stage = MapStage.Sketch, CreatedAt = now, UpdatedAt = now });
        // Seed so GET works immediately: a framed create writes its setup; a frameless one stays empty {}.
        var seed = hasFrame ? SeedSetup(Math.Max(16, width), Math.Max(16, depth), mode, centerX, centerZ) : "{}"u8.ToArray();
        await SketchStore.SaveAsync(db, mapId, seed, ct);
        await Send.OkAsync(new { slug }, ct);
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

/// <summary>Slug derivation shared by the sketch-origination endpoints (create blank / generate).</summary>
internal static class SketchSlug
{
    public static string Slugify(string s)
    {
        var slug = Regex.Replace(s.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        return slug.Length > 0 ? slug : "sketch";
    }

    public static async Task<string> UniqueAsync(MapRepository repo, string baseSlug, CancellationToken ct)
    {
        if (await repo.GetBySlugAsync(baseSlug, ct) is null) return baseSlug;
        for (var i = 2; ; i++)
        {
            var s = $"{baseSlug}-{i}";
            if (await repo.GetBySlugAsync(s, ct) is null) return s;
        }
    }
}

/// <summary>GET /api/map/{slug}/sketch — the stored sketch layout (the JS-origin blob), or {} if none.</summary>
public sealed class SketchGetEndpoint(MapRepository repo, PgmDb db) : EndpointWithoutRequest
{
    public override void Configure() { Get("/map/{slug}/sketch"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var map = await repo.GetBySlugAsync(Route<string>("slug")!, ct);
        if (map is null) { await Send.NotFoundAsync(ct); return; }
        var data = await SketchStore.LoadAsync(db, map.Id, ct);
        await Send.OkAsync(JsonSerializer.Deserialize<JsonElement>(data ?? "{}"u8.ToArray()), ct);
    }
}

/// <summary>PUT /api/map/{slug}/sketch — replace the stored layout blob (the bridge's getState()). 400
/// `{error, findings}` when a bound <c>roomStyles.cage</c> or <c>roomStyles.spawn</c> fails
/// <see cref="HouseStyleValidation"/> — this is where those snapshots actually enter the studio, so it is where
/// a wrong block or a see-through roof is refused rather than silently stamped at export.</summary>
public sealed class SketchPutEndpoint(MapRepository repo, PgmDb db) : EndpointWithoutRequest
{
    public override void Configure() { Put("/map/{slug}/sketch"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var map = await repo.GetBySlugAsync(Route<string>("slug")!, ct);
        if (map is null) { await Send.NotFoundAsync(ct); return; }

        using var ms = new MemoryStream();
        await HttpContext.Request.Body.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();
        try { using var _ = JsonDocument.Parse(bytes); }   // reject non-JSON; don't store garbage
        catch { await Send.ResponseAsync(new { error = "invalid JSON" }, 400, ct); return; }

        var findings = SketchRoomStyleGate.Findings(Encoding.UTF8.GetString(bytes));
        if (findings.Count > 0)
        {
            await Send.ResponseAsync(new { error = "invalid house style", findings }, 400, ct);
            return;
        }

        await SketchStore.SaveAsync(db, map.Id, bytes, ct);
        await Send.OkAsync(new { ok = true }, ct);
    }
}

/// <summary>The house-style gate a sketch's bound <c>roomStyles</c> runs through before the layout is stored —
/// the wool cage and the spawn against the same universal checks, since neither wears a rule the other
/// doesn't.</summary>
internal static class SketchRoomStyleGate
{
    public static IReadOnlyList<Finding> Findings(string layoutJson)
    {
        // A layout the room-style shape does not parse against is not this gate's business — the blob is
        // authoring-source JSON of arbitrary shape, and only a well-formed roomStyles snapshot is checked, the
        // same leniency RoomStyleScope already gives export.
        (HouseStyle? Wool, HouseStyle? Spawn) styles;
        try { styles = RoomStyleScope.StylesOf(layoutJson); }
        catch (JsonException) { return []; }

        var findings = new List<Finding>();
        if (styles.Wool is { } wool)
            findings.AddRange(Prefixed("roomStyles.cage", HouseStyleValidation.Check(wool)));
        if (styles.Spawn is { } spawn)
            findings.AddRange(Prefixed("roomStyles.spawn", HouseStyleValidation.Check(spawn)));
        return findings;
    }

    private static IEnumerable<Finding> Prefixed(string root, IReadOnlyList<Finding> findings)
        => findings.Select(finding => finding with { Field = $"{root}.{finding.Field}" });
}

/// <summary>PUT /api/map/{slug}/sketch/from-plan — replace the stored layout with one a plan compiled,
/// carrying the map's existing finish onto it (<see cref="SketchLayout.CarryFinish"/>). The plan owns the
/// board; the sketch owns its themes, room shells and dressing, and a plan cannot express any of those — so
/// the compile path merges where <see cref="SketchPutEndpoint"/> replaces. Rebuilding a themed map from its
/// plan used to hand back bare stone.
///
/// <para><b>A relief is carried the same way but refuses rather than merging silently.</b> It is keyed by
/// island, and island identity is derived from the geometry — so a recompile that re-fuses the board does not
/// merely move an island, it produces a different one, and terrain authored against the old fusion has
/// nowhere correct to land. Losing that is losing hours of hand work with no warning, so the endpoint answers
/// <b>409</b> listing the islands whose relief would be orphaned and does not write. Sending
/// <c>?force=true</c> accepts the loss and proceeds, which is the author's call to make and not the
/// server's.</para>
///
/// <para><b>A structural piece's stated height is carried a third way</b>
/// (<see cref="SketchLayout.CarryStructuralHeight"/>): matched by <c>intentRef</c>, not by shape id or
/// island, since the compiler regenerates both of those every time but a spawn or wool room keeps the same
/// team/owner:colour identity across a recompile. Only a shape the author actually corrected
/// (<c>height_authored</c>) carries forward — an untouched piece keeps tracking the plan's own
/// <c>surface</c>, so this never masks a deliberate plan-side height change.</para></summary>
public sealed class SketchFromPlanEndpoint(MapRepository repo, PgmDb db) : EndpointWithoutRequest
{
    public override void Configure() { Put("/map/{slug}/sketch/from-plan"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var map = await repo.GetBySlugAsync(Route<string>("slug")!, ct);
        if (map is null) { await Send.NotFoundAsync(ct); return; }

        using var reader = new StreamReader(HttpContext.Request.Body);
        var compiled = await reader.ReadToEndAsync(ct);
        try { using var _ = JsonDocument.Parse(compiled); }   // reject non-JSON; don't store garbage
        catch { await Send.ResponseAsync(new { error = "invalid JSON" }, 400, ct); return; }

        var stored = await SketchStore.LoadAsync(db, map.Id, ct);
        var storedJson = stored is null ? null : Encoding.UTF8.GetString(stored);

        var orphans = SketchLayout.OrphanedRelief(compiled, storedJson);
        if (orphans.Count > 0 && Query<bool>("force", isRequired: false) != true)
        {
            await Send.ResponseAsync(new
            {
                error = "relief would be orphaned",
                islands = orphans,
                hint = "the recompiled board has no island for this authored terrain; retry with ?force=true to discard it",
            }, 409, ct);
            return;
        }

        var merged = SketchLayout.CarryStructuralHeight(
            SketchLayout.CarryRelief(SketchLayout.CarryFinish(compiled, storedJson), storedJson), storedJson);
        await SketchStore.SaveAsync(db, map.Id, Encoding.UTF8.GetBytes(merged), ct);
        await Send.OkAsync(new { ok = true, orphaned = orphans }, ct);
    }
}

/// <summary>POST /api/map/{slug}/sketch/paint — the sketch's terrain paint as a palette-indexed block-pixel
/// payload (<c>xs</c>/<c>zs</c>/<c>palette</c>/<c>color_idx</c> + bounds), which the client expands into the
/// <c>colors</c> array the block-overlay bitmap path already decodes (docs/world-export/terrain-painting.md TP10). The body is the <em>live</em> layout — the
/// bridge's <c>getState()</c>, not the stored blob — so the overlay tracks unsaved edits; the stored intent
/// supplies team ownership, which is what a team-tinted material reads. Empty payload when nothing is
/// drawn; 400 on unparseable JSON.</summary>
public sealed class SketchPaintEndpoint(MapRepository repo, PgmDb db) : EndpointWithoutRequest
{
    public override void Configure() { Post("/map/{slug}/sketch/paint"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var map = await repo.GetBySlugAsync(Route<string>("slug")!, ct);
        if (map is null) { await Send.NotFoundAsync(ct); return; }

        using var reader = new StreamReader(HttpContext.Request.Body);
        var layoutJson = await reader.ReadToEndAsync(ct);

        IReadOnlyList<SurfaceCell> cells;
        try { cells = TerrainPreview.SketchPaintCells(layoutJson, await IntentStore.LoadAsync(db, map.Id, ct)); }
        catch { await Send.ResponseAsync(new { error = "could not paint layout" }, 400, ct); return; }

        await Send.OkAsync(cells.Count == 0 ? LayerData.EmptyPixels() : LayerData.PalettePixels(cells), ct);
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
public sealed class SketchReliefEndpoint(MapRepository repo, ReliefPreviewCache warm) : EndpointWithoutRequest
{
    public override void Configure() { Post("/map/{slug}/sketch/relief"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var map = await repo.GetBySlugAsync(Route<string>("slug")!, ct);
        if (map is null) { await Send.NotFoundAsync(ct); return; }

        using var reader = new StreamReader(HttpContext.Request.Body);
        var layoutJson = await reader.ReadToEndAsync(ct);

        var interval = Query<double>("interval", isRequired: false);
        if (interval <= 0) interval = 1;

        // Each island resumes from the surface its last preview settled on. The relaxation stops when the
        // field stops moving and discards a resume that fails to reach that tolerance, so this can only ever
        // save sweeps — never change the answer, which is what keeps a previewed surface the built one.
        Dictionary<string, HeightField> fields;
        try
        {
            fields = SketchRasterizer.ReliefFields(layoutJson,
                (island, footprint) => warm.WarmStart(map.Id, island, footprint),
                (island, solved) => warm.Remember(map.Id, island, solved));
        }
        catch { await Send.ResponseAsync(new { error = "could not solve relief" }, 400, ct); return; }

        var islands = fields.Select(entry => new
        {
            island = entry.Key,
            min = entry.Value.Min,
            max = entry.Value.Max,
            min_x = entry.Value.Footprint.MinX,
            min_z = entry.Value.Footprint.MinZ,
            max_x = entry.Value.Footprint.MinX + entry.Value.Footprint.Width - 1,
            max_z = entry.Value.Footprint.MinZ + entry.Value.Footprint.Depth - 1,
            // Points go out as one flat [x, z, x, z, …] run per line. A line is hundreds of points and there
            // are dozens of lines on a board, so a pair of objects each would multiply the payload by the
            // length of the words "x" and "z" — and the client strokes them in pairs either way.
            lines = Contours.Of(entry.Value, interval).Select(line => new
            {
                level = line.Level,
                closed = line.Closed,
                points = line.Points.SelectMany(point => new[] { point.X, point.Z }).ToArray(),
            }).ToArray(),
        }).ToArray();

        await Send.OkAsync(new { interval, islands }, ct);
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
public sealed class SketchReliefReadEndpoint(MapRepository repo, ReliefPreviewCache warm) : EndpointWithoutRequest
{
    public override void Configure() { Post("/map/{slug}/sketch/relief/read"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var map = await repo.GetBySlugAsync(Route<string>("slug")!, ct);
        if (map is null) { await Send.NotFoundAsync(ct); return; }

        using var reader = new StreamReader(HttpContext.Request.Body);
        var layoutJson = await reader.ReadToEndAsync(ct);

        Dictionary<string, HeightField> fields;
        SketchLayout? state;
        try
        {
            state = SketchLayout.Parse(layoutJson);
            fields = SketchRasterizer.ReliefFields(layoutJson,
                (island, footprint) => warm.WarmStart(map.Id, island, footprint),
                (island, solved) => warm.Remember(map.Id, island, solved));
        }
        catch { await Send.ResponseAsync(new { error = "could not solve relief" }, 400, ct); return; }

        var mode = state?.Setup?.MirrorMode;
        var cx = state?.Setup?.Center?.Cx ?? 0;
        var cz = state?.Setup?.Center?.Cz ?? 0;

        var islands = fields.Select(entry =>
        {
            var read = ReliefReadback.Read(entry.Value, mode, cx, cz);
            return new
            {
                island = entry.Key,
                read.Cells, read.Low, read.High, read.Relief,
                read.Steps,
                tiers = read.Tiers,
                faces = read.Faces.Take(12),          // the whole list is long and the tail is all banks
                faceCount = read.Faces.Count,
                read.Cliffs,
                acrossX = read.AcrossX, acrossZ = read.AcrossZ,
                symmetryError = read.SymmetryError,
            };
        }).ToArray();

        await Send.OkAsync(new { islands }, ct);
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
public sealed class SketchFinishEndpoint(MapRepository repo, PgmDb db, WorldFeatureWriter writer) : EndpointWithoutRequest
{
    public override void Configure() { Post("/map/{slug}/sketch/finish"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var map = await repo.GetBySlugAsync(Route<string>("slug")!, ct);
        if (map is null) { await Send.NotFoundAsync(ct); return; }

        var data = await SketchStore.LoadAsync(db, map.Id, ct);
        if (data is null) { await Send.ResponseAsync(new { error = "No sketch layout to finish." }, 422, ct); return; }

        var cells = SketchRasterizer.RasterizeColumns(Encoding.UTF8.GetString(data));
        var islands = IslandDetector.Detect(cells.Select(c => (c.X, c.Z)), minIslandSize: 1);
        if (islands.Count == 0)
        {
            await Send.ResponseAsync(new { error = "Nothing is drawn: the layout rasterizes to no ground. Draw a shape first." }, 422, ct);
            return;
        }

        await writer.WriteSketchAsync(map.Id, cells, islands, ct);
        await repo.SetStageAsync(map.Id, MapStage.Configure, ct);   // the draft now has geometry → ready to configure
        await Send.OkAsync(new { slug = map.Slug, configureUrl = $"/maps/{map.Slug}/configure" }, ct);
    }
}

/// <summary>DELETE /api/map/{slug}/sketch/discard-if-empty — drop a still-pristine sketch draft (the row
/// "New sketch" creates up front, then abandoned). The client calls this best-effort when it leaves the
/// Sketch tool. Discards only a draft that is genuinely untouched: sketch stage, still carrying the default
/// name, no authors, and nothing drawn — anything else is real work and is left alone. Returns
/// <c>{discarded}</c>; a missing map or a non-pristine one is a no-op success.</summary>
public sealed class SketchDiscardIfEmptyEndpoint(MapRepository repo, PgmDb db) : EndpointWithoutRequest
{
    public override void Configure() { Delete("/map/{slug}/sketch/discard-if-empty"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var map = await repo.GetBySlugAsync(Route<string>("slug")!, ct);
        if (map is null) { await Send.OkAsync(new { discarded = false }, ct); return; }

        var pristine = map.Stage == MapStage.Sketch
            && string.Equals(map.Name?.Trim(), SketchCreateEndpoint.DefaultName, StringComparison.Ordinal)
            && !await db.Authors.AnyAsync(a => a.MapId == map.Id, ct)
            && !await SketchHasShapesAsync(db, map.Id, ct);

        if (pristine) await repo.DeleteMapAsync(map.Id, ct);   // FK cascade removes the layout artifact
        await Send.OkAsync(new { discarded = pristine }, ct);
    }

    // The layout blob is {setup?, layers:[{layout:{shapes,islands}}]} (or a legacy single {layout:{…}}, or
    // {} / setup-only for a fresh draft). "Empty" = no shapes in any layer.
    private static async Task<bool> SketchHasShapesAsync(PgmDb db, long mapId, CancellationToken ct)
    {
        var data = await SketchStore.LoadAsync(db, mapId, ct);
        if (data is null || data.Length == 0) return false;
        try { using var doc = JsonDocument.Parse(data); return HasShapes(doc.RootElement); }
        catch { return false; }
    }

    private static bool HasShapes(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object) return false;
        if (root.TryGetProperty("layers", out var layers) && layers.ValueKind == JsonValueKind.Array)
            foreach (var l in layers.EnumerateArray())
                if (LayoutHasShapes(l)) return true;
        return LayoutHasShapes(root);   // legacy top-level {layout:{shapes}}
    }

    private static bool LayoutHasShapes(JsonElement el)
        => el.ValueKind == JsonValueKind.Object
           && el.TryGetProperty("layout", out var layout) && layout.ValueKind == JsonValueKind.Object
           && layout.TryGetProperty("shapes", out var shapes) && shapes.ValueKind == JsonValueKind.Array
           && shapes.GetArrayLength() > 0;
}
