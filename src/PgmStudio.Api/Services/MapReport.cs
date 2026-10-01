using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using PgmStudio.Analysis.Playability;
using PgmStudio.Api.Endpoints;
using PgmStudio.Contracts;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Export;
using PgmStudio.Geom;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Dressing;
using PgmStudio.Minecraft.Houses;
using PgmStudio.Minecraft.Render;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Derive;
using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Render;
using PgmStudio.Pgm.Sketch;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Services;

/// <summary>
/// Everything a drive reads back about a stored map, off one build (<c>docs/world-scan/read-backs.md</c>): the
/// three numbers a board is wrong or right by, then the findings, the plan's grid and flow, the relief, what the
/// build could not do, pre-flight, coverage and every text read of the built world — a transect each way
/// through every thing on it and a walk from every spawn to every goal — and the pictures, named by the routes
/// that draw them and drawn too where they are asked for.
///
/// <para>Each reading is the one its own route answers, made by the same call with the same words, and the
/// report names that route beside it: a reading can be asked again alone, and the two cannot say different
/// things.</para>
/// </summary>
internal sealed class MapReport(
    MapReader reader, MapArtifactStore artifacts, FeatureData feature, MapChangeLog log,
    ReliefPreviewCache warm, BlockTextureStore textures)
{
    /// <summary>How far a transect runs past the thing it is taken through, so the way up to it is in it.</summary>
    private const int Overshoot = 8;

    /// <summary>How far either side of a line or a route the things standing beside it are named.</summary>
    private const int Beside = 2;

    /// <summary>The smallest roofed void an x-ray is drawn for. Below it the picture is the isometric drawn one
    /// shade paler, which costs a look and answers nothing.</summary>
    private const int RoomWorthAnXRay = 200;

    /// <summary>The most characters a section runs across; a longer cut samples every few blocks.</summary>
    private const int SectionWidth = 200;

    /// <summary>The footprint the house seats are asked for — the smallest building the corpus stands up.</summary>
    private const int HouseWidth = 9, HouseDepth = 7;

    private const string NoPlan = "this map holds no plan that reads";
    private const string NoGround = "this world has no ground column";

    /// <summary>The report of <paramref name="map"/>, or null where it holds no sketch layout and so no world
    /// to read. <paramref name="pictures"/> draws every picture as well as naming it.</summary>
    public async Task<MapReportDto?> OfAsync(MapRow map, bool pictures, CancellationToken ct)
    {
        if (await WorldReads.LoadAsync(map, reader, artifacts, ct) is not { } read) return null;
        var layoutJson = Encoding.UTF8.GetString(
            await artifacts.LoadAsync(map.Id, ArtifactKind.SketchLayoutJson, ct) ?? []);
        var layout = SketchLayout.Stated(layoutJson);
        var built = read.Built;
        var surface = built.Surface;
        var reads = new List<MapReportReadDto>();

        var findings = await MapFindings.OfAsync(artifacts, feature, reader, map, ct);
        reads.Add(Answered("findings", "findings", () => FindingsText(findings)));

        var plan = await artifacts.LoadAsync(map.Id, ArtifactKind.PlanJson, ct) is { } planBytes
            ? PlanModel.Parse(Encoding.UTF8.GetString(planBytes)) : null;
        reads.Add(Answered("grid", "plan/ascii",
            () => plan is null ? null : PlanBoardAscii.Render(plan, every: 1), NoPlan));
        reads.Add(Answered("flow", "plan/flow",
            () => plan is null ? null : PlanFlow.Describe(PlanFlow.Read(plan)), NoPlan));

        reads.Add(Answered("relief", "POST sketch/relief/read",
            () => ReliefText(ReliefReads.Of(layoutJson, map.Id, warm))));
        reads.Add(Answered("declines", "POST sketch/columns",
            () => DeclinesText(built, SketchPreviews.Of(built, layout, layoutJson))));

        var preflight = await Preflights.OfAsync(map, reader, feature, artifacts, ct);
        reads.Add(Answered("preflight", "preflight", () => string.Join('\n', preflight.Log) + "\n"));

        // Judged by the stored document, as the coverage route judges it, rather than by the one projected onto
        // the world: the two can disagree about where a goal stands.
        var intent = await artifacts.LoadJsonOrEmptyAsync<MapIntent>(map.Id, ArtifactKind.MapIntentJson, ct);
        GroundCoverage.Result? coverage = null;
        string? unread = null;
        try
        {
            var doc = await reader.ReadDocAsync(map, ct);
            coverage = await CoverageReads.OfAsync(map.Id, doc, DeclaredGoals.Of(doc, intent), feature, ct);
        }
        catch (Exception fault) when (fault is InvalidOperationException or ArgumentException or FormatException
                                          or KeyNotFoundException or JsonException)
        {
            unread = fault.Message;
        }
        reads.Add(Answered("coverage", "coverage",
            () => coverage is null ? null : CoverageText(coverage), unread ?? "this map's document does not read"));

        reads.Add(Answered("heightmap", "render/heightmap?format=text", () =>
            HeightmapText.Render(surface, built.Provenance, GridReads.Markers(read.Map),
                                 GridReads.DefaultEvery(surface))));
        var slopes = SlopeGrid.Build(surface, 1);
        reads.Add(Answered("slopes", "slopes?format=text",
            () => slopes is null ? null : SlopeGrid.Render(slopes), NoGround));
        reads.Add(Answered("reach", "reach", () =>
        {
            var (chunks, bridgeable) = WorldReads.Reach(read);
            return TraversabilityRender.Read(chunks, read.Map, bridgeable) is { } walked
                ? ReachText.Render(walked, read.Map?.MaxBuildHeight) : null;
        }, NoGround));

        var extent = Extent(surface);
        if (extent is { } box)
        {
            var every = Math.Clamp(
                (int)Math.Ceiling(Math.Max(box.MaxX - box.MinX, box.MaxZ - box.MinZ) / (double)SectionWidth), 1, 8);
            reads.Add(Section(read, SectionAxis.AlongX, box.MinX, box.MaxX, (box.MinZ + box.MaxZ) / 2, every));
            reads.Add(Section(read, SectionAxis.AlongZ, box.MinZ, box.MaxZ, (box.MinX + box.MaxX) / 2, every));

            foreach (var (name, thing) in Things(built, layout))
            {
                var (centreX, centreZ) = ((thing.MinX + thing.MaxX) / 2, (thing.MinZ + thing.MaxZ) / 2);
                reads.Add(TransectRead($"transect {name} along x", read, box,
                    (thing.MinX - Overshoot, centreZ), (thing.MaxX + Overshoot, centreZ)));
                reads.Add(TransectRead($"transect {name} along z", read, box,
                    (centreX, thing.MinZ - Overshoot), (centreX, thing.MaxZ + Overshoot)));
            }
        }

        var routes = Routes(read, layout).ToList();
        reads.AddRange(routes.Select(route => route.Read));

        reads.Add(Answered("themes", "themes/census?format=text",
            () => ThemeCensus.Render(ThemeCensus.Compute(built, layoutJson))));
        var claims = DressedBoard.Claims(built, layoutJson);
        reads.Add(Answered("claims", "POST sketch/dressing?format=text",
            () => ClaimRaster.Render(claims, built.Dressing.Placements.Count, built.Dressing.Declines)));
        foreach (var (kind, width, depth) in new[] { ("tree", 1, 1), ("boulder", 1, 1), ("house", HouseWidth, HouseDepth) })
            reads.Add(Answered($"seats {kind}",
                kind == "house"
                    ? $"POST sketch/seats?kind=house&width={width}&depth={depth}&format=text"
                    : $"POST sketch/seats?kind={kind}&format=text",
                () => Seats(built, claims, kind, width, depth)));

        var (cavities, blocks) = BoardIsometric.Scan(built);
        reads.Add(Answered("voids", "render/xray?format=text",
            () => blocks == 0 ? null : BoardIsometric.VoidText(cavities, blocks), NoGround));

        var (worstRoute, worstStep) = routes.Where(route => route.WorstStep is not null)
            .OrderByDescending(route => route.WorstStep)
            .Select(route => ((string?)route.Read.Name, route.WorstStep)).FirstOrDefault();
        var headline = new MapReportHeadlineDto(
            slopes?.Walked ?? 0, slopes?.Scrambled ?? 0, slopes?.Barrier ?? 0,
            built.Dressing.Placements.Count, built.Dressing.Declines.Count, worstStep, worstRoute);

        var named = Pictures(read, extent, cavities.Count > 0 && cavities[0].Cells >= RoomWorthAnXRay, coverage,
                             await KeptViews.LoadAsync(artifacts, map.Id, ct));
        var drawn = pictures ? await DrawAsync(read, named, ct) : [.. named.Select(Unasked)];

        return new MapReportDto(map.Slug, await log.LatestAsync(map.Slug, ct), headline, reads, drawn);
    }

    /// <summary>The report as one document a reader scans top to bottom: the three numbers, every reading under
    /// the route that answers it alone, and the pictures by route.</summary>
    public static string Text(MapReportDto report)
    {
        var text = new StringBuilder();
        text.Append(CultureInfo.InvariantCulture, $"REPORT  {report.Slug}  change {report.Change}\n\n");
        foreach (var line in Headline(report.Headline)) text.Append("  ").Append(line).Append('\n');
        foreach (var reading in report.Reads)
        {
            text.Append(CultureInfo.InvariantCulture, $"\n== {reading.Name}   ({reading.Route})\n");
            text.Append(reading.Text ?? $"(none: {reading.Missing})\n");
            if (reading.Text is { } said && !said.EndsWith('\n')) text.Append('\n');
        }
        text.Append("\n== pictures\n");
        foreach (var picture in report.Pictures)
            text.Append(CultureInfo.InvariantCulture,
                $"  {picture.Name,-24} {picture.Route}{(picture.Missing is { } why ? $"   (not drawn: {why})" : "")}\n");
        return text.ToString();
    }

    /// <summary>The three numbers as the three lines a reader takes first.</summary>
    public static IEnumerable<string> Headline(MapReportHeadlineDto headline)
    {
        var cells = headline.Walked + headline.Scrambled + headline.Barrier;
        yield return cells == 0
            ? "ground   none to step on"
            : string.Create(CultureInfo.InvariantCulture,
                $"ground   {headline.Walked} walked, {headline.Scrambled} scrambled, {headline.Barrier} barrier — "
                + $"{100.0 * (headline.Scrambled + headline.Barrier) / cells:0.0}% steps further than a player walks");
        yield return string.Create(CultureInfo.InvariantCulture,
            $"props    {headline.Placed} placed, {headline.Declined} declined");
        yield return headline.WorstStep is { } step
            ? string.Create(CultureInfo.InvariantCulture, $"routes   worst step {step}, on {headline.WorstRoute}")
            : "routes   no route walked between a spawn and a goal";
    }

    // ── the readings ───────────────────────────────────────────────────────────────

    /// <summary>One reading, or why there is none: <paramref name="empty"/> where it answers nothing, and the
    /// fault's own sentence where it could not be read at all.</summary>
    private static MapReportReadDto Answered(string name, string route, Func<string?> read, string empty = "nothing to read")
    {
        try
        {
            return read() is { } text ? new(name, route, text, null) : new(name, route, null, empty);
        }
        catch (Exception fault) when (fault is InvalidOperationException or ArgumentException or FormatException
                                          or OverflowException or KeyNotFoundException or JsonException)
        {
            return new(name, route, null, fault.Message);
        }
    }

    private static string FindingsText(MapFindingsDto findings)
    {
        var text = new StringBuilder();
        text.Append(CultureInfo.InvariantCulture, $"stage {findings.Stage}\n");
        if (findings.Findings.Count == 0) text.Append("nothing wrong that the stored documents can say\n");
        foreach (var finding in findings.Findings) text.Append(Line(finding)).Append('\n');
        foreach (var gate in findings.Unasked)
            text.Append(CultureInfo.InvariantCulture, $"not judged: {gate.Gate} — {gate.Why}; ask {gate.Ask}\n");
        return text.ToString();
    }

    private static string Line(Finding finding) =>
        $"{finding.Rule} {finding.Severity.ToString().ToLowerInvariant()}: {finding.Message}"
        + (finding.Field is { } field ? $" ({field})" : "");

    private static string ReliefText((ReliefReadDto Read, IReadOnlyList<Finding> Complaints) relief)
    {
        var text = new StringBuilder();
        if (relief.Read.Groups.Count == 0) text.Append("no relief group\n");
        foreach (var group in relief.Read.Groups)
            text.Append(CultureInfo.InvariantCulture,
                $"group {group.Group}: {group.Cells} cells, low {group.Low}, high {group.High}, relief {group.Relief}, "
                + $"symmetry error {group.SymmetryError}\n");
        foreach (var finding in relief.Complaints) text.Append(Line(finding)).Append('\n');
        return text.ToString();
    }

    private static string DeclinesText(BuiltWorld built, SketchPreview preview)
    {
        List<Finding> said =
            [.. preview.Layout.AsComplaints(), .. built.Declines, .. preview.Goals.AsComplaints(), .. preview.Sites];
        return said.Count == 0 ? "nothing declined\n" : string.Join('\n', said.Select(Line)) + "\n";
    }

    private static string CoverageText(GroundCoverage.Result coverage)
    {
        if (!coverage.HaveRoutes)
            return string.Create(CultureInfo.InvariantCulture,
                $"no routes to walk, so no dead share — {coverage.GroundCells} ground cells unclassed\n");
        var text = new StringBuilder();
        text.Append(CultureInfo.InvariantCulture,
            $"reached {coverage.ReachedCells}  decorated {coverage.DecoratedCells}  dead {coverage.DeadCells}  "
            + $"of {coverage.GroundCells} = {coverage.DeadShare * 100:0.0}% dead\n");
        foreach (var patch in coverage.DeadPatches)
            text.Append(CultureInfo.InvariantCulture,
                $"dead patch {patch.Area,5} cells at ({patch.CentroidX}, {patch.CentroidZ}), "
                + $"{patch.NearestReachedBlocks} blocks from used ground\n");
        return text.ToString();
    }

    private static MapReportReadDto Section(BuiltRead read, SectionAxis axis, int from, int to, int at, int every)
    {
        var along = axis == SectionAxis.AlongX ? "x" : "z";
        var across = axis == SectionAxis.AlongX ? "z" : "x";
        return Answered($"section along {along} at {across} {at}",
            Invariant($"render/section?axis={along}&from={from}&to={to}&at={at}&every={every}&format=text"),
            () => SectionText.Render(read.Built.World, read.Built.Provenance, read.Built.Surface, read.Built.Columns,
                SketchLayer.GroundId, axis, from, to, at, null, null, 0, every));
    }

    private static MapReportReadDto TransectRead(string name, BuiltRead read, (int MinX, int MinZ, int MaxX, int MaxZ) box,
                                                (int X, int Z) start, (int X, int Z) end)
    {
        List<(int X, int Z)> points = [Within(start, box), Within(end, box)];
        return Answered(name,
            Invariant($"transect?points={points[0].X},{points[0].Z};{points[1].X},{points[1].Z}&beside={Beside}&format=text"),
            () => Transect.Render(Transect.Walk(read.Built.World, read.Built.Provenance, read.Built.Surface,
                read.Built.Columns, points, 1, Beside), points, 1, Beside));
    }

    private static (int X, int Z) Within((int X, int Z) point, (int MinX, int MinZ, int MaxX, int MaxZ) box) =>
        (Math.Clamp(point.X, box.MinX, box.MaxX), Math.Clamp(point.Z, box.MinZ, box.MaxZ));

    private static string? Seats(BuiltWorld built, ClaimRaster.Grid claims, string kind, int width, int depth)
    {
        if (PlacedProp.PavingStandoffOf(kind) is not { } standoff) return null;
        var level = kind == "house" ? new ClaimRaster.Level(built.Surface, SiteLevel.Limit(new HouseStyle())) : null;
        return ClaimRaster.RenderSeats(
            ClaimRaster.Seat(claims, kind, standoff, width, depth, level, SurfaceSoil.Of(built)));
    }

    /// <summary>The board's own extent, the ground's least and greatest x and z.</summary>
    private static (int MinX, int MinZ, int MaxX, int MaxZ)? Extent(IReadOnlyDictionary<(int X, int Z), int> surface) =>
        surface.Count == 0 ? null
            : (surface.Keys.Min(cell => cell.X), surface.Keys.Min(cell => cell.Z),
               surface.Keys.Max(cell => cell.X), surface.Keys.Max(cell => cell.Z));

    /// <summary>Every thing on the board a transect is worth taking through, by the box it stands in: the
    /// spawns and the goals as the build placed them, every house, fluid and boulder the dressing placed — its
    /// authored image, the others being the same thing turned — and every made thing.</summary>
    private static IEnumerable<(string Name, (int MinX, int MinZ, int MaxX, int MaxZ) Box)> Things(
        BuiltWorld built, SketchLayout? layout)
    {
        var intent = built.ResolvedIntent;
        foreach (var (spawn, index) in intent.Spawns.Select((spawn, index) => (spawn, index)))
            yield return ($"spawn-{index}", (spawn.Footprint ?? Bounds(spawn.Protection)) is { } room
                ? Box(room) : Around(spawn.Point, 6));
        foreach (var (wool, index) in (intent.Wools ?? []).Select((wool, index) => (wool, index)))
            yield return ($"wool-{index}", Around(wool.Spawn, 4));
        foreach (var (goal, index) in (intent.Destroyables ?? []).Select((goal, index) => (goal, index)))
            yield return ($"destroyable-{index}", goal.Box is { } stamped ? Box(stamped) : Around(goal.Anchor, 4));
        foreach (var (core, index) in (intent.Cores ?? []).Select((core, index) => (core, index)))
            yield return ($"core-{index}", core.Box is { } stamped ? Box(stamped) : Around(core.Anchor, 4));
        foreach (var (point, index) in (intent.ControlPoints ?? []).Select((point, index) => (point, index)))
            yield return ($"point-{index}", point.PadBox is { } pad ? Box(pad) : Around(point.Anchor, 4));

        foreach (var claim in built.Dressing.Placements.Where(claim =>
                     claim.Owner.Image == 0 && claim.Cells.Count > 0
                     && claim.Owner.Kind is PropKinds.House or PropKinds.Fluid or PropKinds.Boulder))
            yield return ($"{claim.Owner.Kind} {claim.Owner.Unit}",
                (claim.Cells.Min(cell => cell.X), claim.Cells.Min(cell => cell.Z),
                 claim.Cells.Max(cell => cell.X), claim.Cells.Max(cell => cell.Z)));

        var made = built.MadeLayers ?? new HashSet<string>();
        var partOf = (layout?.Layers ?? []).Where(layer => layer.Id is { } id && made.Contains(id))
                                           .ToDictionary(layer => layer.Id!, layer => layer.PartOf ?? layer.Id!);
        foreach (var thing in (built.Columns ?? []).Where(segment => partOf.ContainsKey(segment.Layer))
                                                   .GroupBy(segment => partOf[segment.Layer]))
            yield return ($"made {thing.Key}",
                (thing.Min(segment => segment.X), thing.Min(segment => segment.Z),
                 thing.Max(segment => segment.X), thing.Max(segment => segment.Z)));
    }

    private static Rect? Bounds(IReadOnlyList<Rect> rects) => rects.Count == 0 ? null
        : new Rect(rects.Min(rect => rect.MinX), rects.Min(rect => rect.MinZ),
                   rects.Max(rect => rect.MaxX), rects.Max(rect => rect.MaxZ));

    private static (int MinX, int MinZ, int MaxX, int MaxZ) Box(Rect rect) =>
        ((int)Math.Floor(rect.MinX), (int)Math.Floor(rect.MinZ), (int)Math.Floor(rect.MaxX), (int)Math.Floor(rect.MaxZ));

    private static (int MinX, int MinZ, int MaxX, int MaxZ) Box(BlockBox box) => (box.MinX, box.MinZ, box.MaxX, box.MaxZ);

    private static (int MinX, int MinZ, int MaxX, int MaxZ) Around(Pt point, int reach)
    {
        var (x, z) = ((int)Math.Floor(point.X), (int)Math.Floor(point.Z));
        return (x - reach, z - reach, x + reach, z + reach);
    }

    /// <summary>A walk from every spawn to every goal, each the reading <c>walk</c> answers for the same two
    /// ends, with its worst step. An end that names a storey is walked to that storey's floor, so a route to a
    /// goal on a viaduct is not the route to the street under it.</summary>
    private static IEnumerable<(MapReportReadDto Read, int? WorstStep)> Routes(BuiltRead read, SketchLayout? layout)
    {
        var intent = read.Built.ResolvedIntent;
        var floors = (layout?.Layers ?? []).Where(layer => layer.Id is not null)
                                           .GroupBy(layer => layer.Id!)
                                           .ToDictionary(group => group.Key, group => (int)group.First().BaseY);
        string End(Pt point, string? layer) =>
            layer is not null && floors.TryGetValue(layer, out var floor)
                ? Invariant($"{(int)Math.Floor(point.X)},{(int)Math.Floor(point.Z)},{floor}")
                : Invariant($"{(int)Math.Floor(point.X)},{(int)Math.Floor(point.Z)}");

        var goals = new List<(string Name, string At)>();
        foreach (var (wool, index) in (intent.Wools ?? []).Select((wool, index) => (wool, index)))
            goals.Add(($"wool-{index}", End(wool.Spawn, wool.Layer)));
        foreach (var (goal, index) in (intent.Destroyables ?? []).Select((goal, index) => (goal, index)))
            goals.Add(($"destroyable-{index}", End(goal.Anchor, goal.Layer)));
        foreach (var (core, index) in (intent.Cores ?? []).Select((core, index) => (core, index)))
            goals.Add(($"core-{index}", End(core.Anchor, core.Layer)));
        foreach (var (point, index) in (intent.ControlPoints ?? []).Select((point, index) => (point, index)))
            goals.Add(($"point-{index}", End(point.Anchor, point.Layer)));
        if (goals.Count == 0) yield break;

        var shared = WalkReads.Ground(read);
        var ground = WorldWalk.For(shared, read.Doc, null);
        foreach (var (spawn, index) in intent.Spawns.Select((spawn, index) => (spawn, index)))
        {
            var start = End(spawn.Point, spawn.Layer);
            foreach (var (goal, at) in goals)
            {
                var name = $"route spawn-{index} to {goal}";
                var route = $"walk?from={start}&to={at}&beside={Beside}&format=text";
                if (WalkReads.Seat(start, shared) is not { } from || WalkReads.Seat(at, shared) is not { } to)
                {
                    yield return (new MapReportReadDto(name, route, null,
                        "an end lies more than 24 blocks from any ground"), null);
                    continue;
                }
                var path = Walk.Between(from, to, ground, WalkAim.Travel);
                if (path is null)
                {
                    yield return (new MapReportReadDto(name, route,
                        WalkProfile.RenderUnreachable(from, to, "travel"), null), null);
                    continue;
                }
                var profile = WalkProfile.Of(path, read.Built.Provenance);
                var beside = WalkProfile.Beside(path, read.Built.Provenance, Beside);
                yield return (new MapReportReadDto(name, route,
                    WalkProfile.Render(from, to, "travel", path, profile, beside, read.Built.Provenance), null),
                    profile.WorstStep);
            }
        }
    }

    // ── the pictures ───────────────────────────────────────────────────────────────

    /// <summary>A picture by name, the route that draws it, and the draw the route takes — null for an eye view,
    /// which is drawn under the eye's own turn.</summary>
    private sealed record Picture(string Name, string Route, Func<byte[]?>? Draw, EyeAim? Eye = null);

    /// <summary>Every picture the report names: the board in the round from two opposite corners, and in x-ray
    /// where it roofs a room worth seeing; the board from above, one question at a time; the two sections, the
    /// coverage, and every view of it from a player's eye the board keeps or suggests.</summary>
    private static List<Picture> Pictures(BuiltRead read, (int MinX, int MinZ, int MaxX, int MaxZ)? extent,
                                          bool roomWorthAnXRay, GroundCoverage.Result? coverage,
                                          List<WorldView> stored)
    {
        var built = read.Built;
        const int Cube = 3, Block = 4;
        List<Picture> named =
        [
            new("isometric", "render/isometric", () => BoardIsometric.Isometric(built, 0, Cube, read.Name)),
            new("isometric north-west", "render/isometric?corner=north-west",
                () => BoardIsometric.Isometric(built, 2, Cube, read.Name)),
        ];
        if (roomWorthAnXRay)
            named.AddRange(
            [
                new("xray", "render/xray", () => BoardIsometric.XRay(built, 0, Cube, read.Name)?.Png),
                new("xray north-west", "render/xray?corner=north-west",
                    () => BoardIsometric.XRay(built, 2, Cube, read.Name)?.Png),
            ]);
        foreach (var (name, query, mode, subject) in new (string, string, TopDownColorMode, TopDownSubject)[]
                 {
                     ("topdown", "", TopDownColorMode.Category, TopDownSubject.Combined),
                     ("material", "?material=1", TopDownColorMode.Material, TopDownSubject.Combined),
                     ("structure", "?subject=structure", TopDownColorMode.Category, TopDownSubject.Structure),
                     ("made", "?subject=made", TopDownColorMode.Category, TopDownSubject.Made),
                     ("foliage", "?subject=foliage", TopDownColorMode.Category, TopDownSubject.Foliage),
                     ("objectives", "?subject=objectives", TopDownColorMode.Category, TopDownSubject.Objectives),
                 })
            named.Add(new(name, "render/topdown" + query, () => TopDownRender.Png(
                built.World, read.Map, Block, null, read.Name, mode, subject, built.Provenance)));
        named.AddRange(
        [
            new("heightmap", "render/heightmap", () => HeightProfileRender.Png(
                built.World, Block, 4, false, markFluid: true, drawContours: true, read.Name)),
            new("surface", "render/surface", () => SurfaceReport.Png(built.World, Block)),
            new("traversability", "render/traversability", () =>
            {
                var (chunks, bridgeable) = WorldReads.Reach(read);
                return TraversabilityRender.Png(chunks, read.Map, bridgeable, Block);
            }),
            new("mirror", "render/mirror", () => MirrorReport.Png(
                built.World, Block, read.LaidTo?.Mode, read.LaidTo?.CenterX ?? 0, read.LaidTo?.CenterZ ?? 0)),
        ]);
        if (extent is { } box)
        {
            var (middleX, middleZ) = ((box.MinX + box.MaxX) / 2, (box.MinZ + box.MaxZ) / 2);
            named.Add(new($"section along x at z {middleZ}",
                Invariant($"render/section?axis=x&from={box.MinX}&to={box.MaxX}&at={middleZ}"),
                () => SectionRender.Png(built.World, SectionAxis.AlongX, box.MinX, box.MaxX, middleZ,
                                        Block, null, null, depth: 0)));
            named.Add(new($"section along z at x {middleX}",
                Invariant($"render/section?axis=z&from={box.MinZ}&to={box.MaxZ}&at={middleX}"),
                () => SectionRender.Png(built.World, SectionAxis.AlongZ, box.MinZ, box.MaxZ, middleX,
                                        Block, null, null, depth: 0)));
        }
        named.Add(new("coverage", "coverage?format=png",
            () => coverage is null ? null : CoverageRender.Png(coverage, 1)));
        foreach (var view in KeptViews.Of(stored, built).Concat(WorldViews.Suggested(built)).DistinctBy(view => view.Id))
        {
            var words = QueryHelpers.ParseQuery(view.Query);
            named.Add(new(view.Id, $"render/eye?{view.Query}", null,
                EyeAim.Read(word => words.TryGetValue(word, out var value) ? value.ToString() : null)));
        }
        return named;
    }

    private static MapReportPictureDto Unasked(Picture picture) => new(picture.Name, picture.Route, null, null);

    /// <summary>Every picture drawn, each as its own route draws it — the eye views under the eye's turn, with
    /// the block sprites.</summary>
    private async Task<List<MapReportPictureDto>> DrawAsync(BuiltRead read, List<Picture> named, CancellationToken ct)
    {
        var drawn = new List<MapReportPictureDto>(named.Count);
        foreach (var picture in named.Where(picture => picture.Draw is not null))
            drawn.Add(Drawn(picture, picture.Draw!, "nothing to draw"));

        var eyes = named.Where(picture => picture.Eye is not null).ToList();
        if (eyes.Count == 0) return drawn;
        var (set, reason) = await textures.GetAsync(ct);
        if (set is null)
        {
            drawn.AddRange(eyes.Select(picture =>
                new MapReportPictureDto(picture.Name, picture.Route, null, reason ?? "no block textures")));
            return drawn;
        }
        using (await EyeRenders.TurnAsync(ct))
            foreach (var picture in eyes)
                drawn.Add(Drawn(picture, () => EyeReadEndpoint.Shot(read.Built, set, picture.Eye!)?.Png,
                                picture.Eye!.Empty));
        return drawn;
    }

    private static MapReportPictureDto Drawn(Picture picture, Func<byte[]?> draw, string empty)
    {
        try
        {
            return draw() is { } png
                ? new(picture.Name, picture.Route, Convert.ToBase64String(png), null)
                : new(picture.Name, picture.Route, null, empty);
        }
        catch (Exception fault) when (fault is InvalidOperationException or ArgumentException or FormatException
                                          or OverflowException or KeyNotFoundException)
        {
            return new(picture.Name, picture.Route, null, fault.Message);
        }
    }

    private static string Invariant(FormattableString text) => FormattableString.Invariant(text);
}
