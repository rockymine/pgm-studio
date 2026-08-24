using FastEndpoints;
using PgmStudio.Api.Services;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Domain;
using PgmStudio.Export;
using PgmStudio.Minecraft;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Render;
using PgmStudio.Pgm;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Sketch;
using PgmStudio.Analysis.Scan;
using PgmStudio.Analysis.Playability;
using PgmStudio.Geom;
using PgmStudio.Geom.Algorithms;
using PgmStudio.Minecraft.Dressing;

namespace PgmStudio.Api.Endpoints;

/// <summary>
/// Reading a built world back — the eight pictures and one text read that until now existed only behind
/// <c>PgmStudio.RoundTrip</c>'s flags.
///
/// <para><b>Everything an agent does runs through the API and the API describes itself, except the one thing
/// it does after building: look at what it built.</b> A renderer reachable only from a .NET binary is a
/// capability no schema names, so a brief had to carry a table of flags and an agent had to know the binary
/// existed. These are the same renderers, over the same world, answering over HTTP — and what each one draws
/// is written once, as the endpoint description the schema publishes.</para>
///
/// <para><b>The world is built for the request.</b> A map that ships its own region files has one on disk, but
/// a sketch-authored map's world exists only as the layout and the intent it is derived from — the same
/// position <c>GET …/export</c> is in, and it builds one too. The build here runs <b>no gate</b>, deliberately:
/// a board that fails one is exactly the board somebody needs to look at, and a read-back that refuses the
/// broken case is a read-back that is never there when it is wanted.</para>
///
/// <para>The map document is projected from the resolved intent rather than composed through the export, for
/// the same reason: the projection is what the overlays need — the spawns, the goals and the apply rules a
/// picture draws on top of the terrain — and going through the export would lose the world to the first gate
/// that fired.</para>
/// </summary>
/// <param name="Doc">The projected map document as read, which the walk needs for the <c>enter</c> rules a
/// named team is barred by — the same projection <paramref name="Map"/> is deserialized from.</param>
internal sealed record BuiltRead(BuiltWorld Built, MapXml? Map, string Name,
    Dictionary<string, object?>? Doc = null);

/// <summary>How a world read is loaded, once, for the endpoints below to draw from.</summary>
internal static class WorldReads
{
    /// <summary>The world a map's stored documents describe, with the map document projected onto it, or null
    /// when the map has no stored sketch layout — which is every map that ships its own region files, and is
    /// a 404 rather than a fault: there is no world here to build.</summary>
    public static async Task<BuiltRead?> LoadAsync(
        MapRow map, MapReader reader, MapArtifactStore artifacts, CancellationToken ct)
    {
        var layout = await artifacts.LoadAsync(map.Id, ArtifactKind.SketchLayoutJson, ct);
        if (layout is null) return null;

        var intent = await artifacts.LoadJsonOrEmptyAsync<MapIntent>(map.Id, ArtifactKind.MapIntentJson, ct);
        var built = WorldBuilder.Build(System.Text.Encoding.UTF8.GetString(layout), intent);

        // The overlays read a map document, and the one that describes this world is the projection of the
        // intent the build just resolved — spawns snapped to the structures it placed, goal locations filled
        // in from the cubes it cast. Projected here rather than read off the stored document, which was
        // written before any of that was known.
        MapXml? projected = null;
        Dictionary<string, object?>? asRead = null;
        try
        {
            var doc = await reader.ReadDocAsync(map, ct);
            IntentGenerator.Apply(doc, built.ResolvedIntent);
            asRead = doc;
            projected = Deserializer.FromDict(doc);
        }
        catch (Exception fault) when (fault is InvalidOperationException or KeyNotFoundException
                                          or FormatException or ArgumentException)
        {
            // A document that will not project costs the overlays and not the picture. The terrain is what
            // was asked for; the markers on top of it are the part that needs a readable document.
        }

        return new BuiltRead(built, projected, map.Slug, asRead);
    }
}

/// <summary>One world read that answers a picture: the world is loaded once, the view is drawn, and a view
/// that draws nothing is a 422 rather than an empty image.</summary>
internal abstract class WorldRenderEndpoint(MapRepository repo, MapReader reader, MapArtifactStore artifacts)
    : EndpointWithoutRequest
{
    /// <summary>How many pixels a block takes, 1–16, default 4. Bigger than a preview's scale because these
    /// draw a whole board rather than a swatch, and a board at one pixel a block is a thumbnail.</summary>
    protected int Scale => Query<int?>("scale", isRequired: false) is { } asked
        ? Math.Clamp(asked, 1, 16) : 4;

    protected int? OptionalInt(string name) => Query<int?>(name, isRequired: false);

    /// <summary>The `layer` word, for the reads that project a column to one cell. Declared here rather than
    /// per endpoint so the four of them cannot describe the same word four ways.</summary>
    protected static QueryWord Storey => new("layer",
        "Draw one storey of a stacked board: the named sketch layer's own ground and everything standing on "
        + "it, up to whatever the next layer starts at. Absent draws the whole world, which on a stacked "
        + "board is the topmost storey and whatever shows past it.");

    /// <summary>Whether this read narrows to a storey. The reads that keep Y — `section`, `column` — and the
    /// two that walk the ground rather than draw it — `traversability`, `walk` — do not: the first two show
    /// every storey already and the last two answer per storey without being asked.</summary>
    protected virtual bool Storeyed => false;

    protected abstract byte[]? Draw(BuiltRead read);

    /// <summary>What this read cannot draw, for the 422 that says so.</summary>
    protected virtual string Empty => "this world has no column to draw";

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;

        var read = await WorldReads.LoadAsync(map, reader, artifacts, ct);
        if (read is null)
        {
            await Refusals.WriteAsync(HttpContext, 404, "no world to read",
                [new Vocabulary.Finding(RequestRules.NoSuchSubject,
                    "this map has no stored sketch layout, so there is no world for the studio to build and "
                    + "read back — a map that ships its own region files is read from those instead")], ct);
            return;
        }

        if (Storeyed && Query<string?>("layer", isRequired: false) is { Length: > 0 } asked)
        {
            var storey = WorldStorey.Of(read.Built.World, read.Built.Columns, asked);
            if (storey is null)
            {
                var names = WorldStorey.Names(read.Built.Columns);
                await Refusals.WriteAsync(HttpContext, 422, "no such layer",
                    [new Vocabulary.Finding(RequestRules.NoSuchSubject,
                        names.Count == 0
                            ? $"this board was not drawn in layers, so there is no '{asked}' storey to draw"
                            : $"this board has no layer '{asked}' — it carries {string.Join(", ", names)}")], ct);
                return;
            }
            read = read with { Built = read.Built with { World = storey } };
        }

        byte[]? png;
        try { png = Draw(read); }
        catch (Exception fault) when (fault is InvalidOperationException or ArgumentException
                                          or FormatException or OverflowException)
        {
            await Refusals.UnreadableAsync(HttpContext, "cannot draw that", fault.Message, ct);
            return;
        }

        if (png is null)
        {
            await Refusals.WriteAsync(HttpContext, 422, "nothing to draw",
                [new Vocabulary.Finding(RequestRules.Conflict, Empty)], ct);
            return;
        }

        HttpContext.Response.ContentType = "image/png";
        await HttpContext.Response.Body.WriteAsync(png, ct);
    }
}

/// <summary>GET /api/map/{slug}/render/topdown — the board from above, one question per image.
/// <c>subject</c> picks what is drawn: <c>ground</c> the terrain alone, <c>structure</c> what the build recorded itself
/// placing (the provenance sidecar, so it draws the buildings that were authored rather than the blocks that
/// look like buildings), <c>foliage</c> the planting, <c>objectives</c> the goals and spawns, <c>combined</c>
/// all of it. <c>material</c> switches the colouring from category to the real palette. The one read for
/// "what did I build and where"; it keeps no Y, so a riser, a ramp's steps and a room's floor are invisible
/// in it — <c>section</c> and <c>column</c> are the two that do.</summary>
internal sealed class TopDownReadEndpoint(MapRepository repo, MapReader reader, MapArtifactStore artifacts)
    : WorldRenderEndpoint(repo, reader, artifacts)
{
    public override void Configure()
    {
        Get("/map/{slug}/render/topdown");
        AllowAnonymous();
        Summary(s => s.Summary = WorldReadCatalog.Sentence("render/topdown"));
        Description(b => b.Png().Refuses(404, 422).Reads(
            new QueryWord("subject", "What to draw. Absent draws them all together.",
                ["ground", "structure", "foliage", "objectives", "combined"]),
            new QueryWord("material",
                "Present colours by the real block palette instead of by category. The category reading is "
                + "what answers \"what kind of thing is here\"; the material reading answers \"what is it "
                + "made of\"."),
            new QueryWord("ymax", "Ignore everything above this height, for looking under a roof or a canopy."),
            Storey,
            new QueryWord("scale", "Pixels a block takes, 1 to 16. Absent draws at 4, and out of range clamps.", Min: 1, Max: 16)));
    }

    protected override bool Storeyed => true;

    protected override string Empty => "this world has no non-air column, so there is nothing to look down on";

    protected override byte[]? Draw(BuiltRead read) => TopDownRender.Png(
        read.Built.World, read.Map, Scale, OptionalInt("ymax"), read.Name,
        Query<string?>("material", isRequired: false) is not null
            ? TopDownColorMode.Material : TopDownColorMode.Category,
        Enum.TryParse<TopDownSubject>(Query<string?>("subject", isRequired: false), ignoreCase: true, out var subject)
            ? subject : TopDownSubject.Combined,
        read.Built.Provenance);
}

/// <summary>GET /api/map/{slug}/render/section — a vertical cut with a Y scale, and one of only two reads
/// that keep Y at all. <c>axis</c> is <c>x</c> or <c>z</c> (which way the cut runs), <c>from</c>/<c>to</c> its
/// extent along that axis and <c>at</c> the other coordinate it is taken at. Without <c>depth</c> it samples
/// <b>one plane</b>, so anything a few blocks either side is not in the picture — a cut through a house that
/// misses its walls reads as floor, air, roof, which is a correct reading of that plane rather than a broken
/// building. With <c>depth</c> it projects that many blocks behind the cut, each column taking the nearest
/// block there, drawn in its own material dimmed by how far back it stands.</summary>
internal sealed class SectionReadEndpoint(MapRepository repo, MapReader reader, MapArtifactStore artifacts)
    : WorldRenderEndpoint(repo, reader, artifacts)
{
    public override void Configure()
    {
        Get("/map/{slug}/render/section");
        AllowAnonymous();
        Summary(s => s.Summary = WorldReadCatalog.Sentence("render/section"));
        Description(b => b.Png().Refuses(404, 422).Reads(
            new QueryWord("axis", "Which way the cut runs. Absent runs along x.", ["x", "z"]),
            new QueryWord("from", "Where the cut starts along that axis. Absent is -64."),
            new QueryWord("to", "Where it ends. Absent is 64."),
            new QueryWord("at", "The other coordinate the plane is taken at. Absent is 0."),
            new QueryWord("ymin", "The lowest course drawn. Absent draws from the lowest block in the cut."),
            new QueryWord("ymax", "The highest. Absent draws to the highest block in the cut."),
            new QueryWord("scale", "Pixels a block takes, 1 to 16. Absent draws at 4, and out of range clamps.", Min: 1, Max: 16),
            new QueryWord("depth", "How many blocks behind the plane to project, 0 to 16. Absent samples the "
                + "one-block slice; above 0 each column takes the nearest block at or behind the cut, drawn "
                + "in its own material dimmed by how far back it stands.", Min: 0, Max: 16)));
    }

    /// <summary>Set while drawing when <c>at</c> falls outside the world, so the refusal names the range a
    /// cut can be taken at. A coordinate outside the world is a fault rather than a picture, and a blank
    /// image is the slowest possible way to be told so.</summary>
    private string? _offWorld;

    protected override string Empty => _offWorld ?? "nothing stands along that cut";

    protected override byte[]? Draw(BuiltRead read)
    {
        var axis = string.Equals(Query<string?>("axis", isRequired: false), "z", StringComparison.OrdinalIgnoreCase)
            ? SectionAxis.AlongZ : SectionAxis.AlongX;
        var at = OptionalInt("at") ?? 0;

        // A cut along x is taken at a z and a cut along z at an x, which is the thing about this route
        // easiest to have backwards — so the refusal names the axis as well as the range.
        if (SectionRender.Span(AnvilRegion.FromWorld(read.Built.World), axis) is { } span
            && (at < span.Min || at > span.Max))
        {
            var named = axis == SectionAxis.AlongX ? "z" : "x";
            _offWorld = $"a cut along {(axis == SectionAxis.AlongX ? "x" : "z")} is taken at a {named}, and "
                + $"at={at} is outside this world, which spans {named} {span.Min}..{span.Max}";
            return null;
        }

        return SectionRender.Png(read.Built.World, axis,
            OptionalInt("from") ?? -64, OptionalInt("to") ?? 64, at,
            Scale, OptionalInt("ymin"), OptionalInt("ymax"), depth: OptionalInt("depth") ?? 0);
    }
}

/// <summary>GET /api/map/{slug}/render/heightmap — elevation as tone, with contour lines every
/// <c>contour</c> blocks (default 4). The read for whether a relief solved into the shape it was drawn as,
/// and the one that shows a flat pad butted against a hill as the ruled edge it is.</summary>
internal sealed class HeightmapReadEndpoint(MapRepository repo, MapReader reader, MapArtifactStore artifacts)
    : WorldRenderEndpoint(repo, reader, artifacts)
{
    public override void Configure()
    {
        Get("/map/{slug}/render/heightmap");
        AllowAnonymous();
        Summary(s => s.Summary = WorldReadCatalog.Sentence("render/heightmap"));
        Description(b => b.Png().Refuses(404, 422).Reads(
            new QueryWord("contour", "Blocks between contour lines. Absent draws one every 4."),
            new QueryWord("grey", "Present draws elevation in grey rather than in tone, for a board whose own "
                + "palette fights the height reading."),
            Storey,
            new QueryWord("scale", "Pixels a block takes, 1 to 16. Absent draws at 4, and out of range clamps.", Min: 1, Max: 16)));
    }

    protected override bool Storeyed => true;

    protected override string Empty => "this world has no ground column, so it has no elevation to draw";

    protected override byte[]? Draw(BuiltRead read) => HeightProfileRender.Png(
        read.Built.World, Scale, OptionalInt("contour") ?? 4,
        Query<string?>("grey", isRequired: false) is not null,
        markWater: true, drawContours: true, read.Name);
}

/// <summary>GET /api/map/{slug}/render/surface — the paint, read as the tone families
/// <c>TerrainPalette.Families</c> names, so a board can be checked against the palette it was authored from.
/// Magenta is the honest answer for a block no family claims, and the legend says how many.</summary>
internal sealed class SurfaceReadEndpoint(MapRepository repo, MapReader reader, MapArtifactStore artifacts)
    : WorldRenderEndpoint(repo, reader, artifacts)
{
    public override void Configure()
    {
        Get("/map/{slug}/render/surface");
        AllowAnonymous();
        Summary(s => s.Summary = WorldReadCatalog.Sentence("render/surface"));
        Description(b => b.Png().Refuses(404, 422).Reads(
            Storey,
            new QueryWord("scale", "Pixels a block takes, 1 to 16. Absent draws at 4, and out of range clamps.", Min: 1, Max: 16)));
    }

    protected override bool Storeyed => true;

    protected override string Empty => "this world decodes to no column, so it has no surface to read";

    protected override byte[]? Draw(BuiltRead read) => SurfaceReport.Png(read.Built.World, Scale);
}

/// <summary>GET /api/map/{slug}/render/traversability — the navigable components, with the spawns and goals
/// drawn on them. Headroom is what a player's body passes through rather than air, so a flower, a torch, a
/// carpet and an approach wall's cobweb course leave a column navigable while a fence, a wall and a chest
/// stop it.</summary>
internal sealed class TraversabilityReadEndpoint(MapRepository repo, MapReader reader, MapArtifactStore artifacts)
    : WorldRenderEndpoint(repo, reader, artifacts)
{
    public override void Configure()
    {
        Get("/map/{slug}/render/traversability");
        AllowAnonymous();
        Summary(s => s.Summary = WorldReadCatalog.Sentence("render/traversability"));
        Description(b => b.Png().Refuses(404, 422).Reads(new QueryWord("scale", "Pixels a block takes, 1 to 16. Absent draws at 4, and out of range clamps.", Min: 1, Max: 16)));
    }

    protected override string Empty => "this world has no ground column, so there is nothing to walk";

    protected override byte[]? Draw(BuiltRead read) =>
        TraversabilityRender.Png(read.Built.World, read.Map, Scale);
}

/// <summary>GET /api/map/{slug}/render/structures — the building census by block material, for a world the
/// studio did <b>not</b> build. On one it did, <c>render/topdown?subject=structure</c> is the read to take:
/// this one finds roofs by material and cannot see a town in stone and quartz (`B149`), while the structure
/// layer draws what the build recorded itself placing.</summary>
internal sealed class StructuresReadEndpoint(MapRepository repo, MapReader reader, MapArtifactStore artifacts)
    : WorldRenderEndpoint(repo, reader, artifacts)
{
    public override void Configure()
    {
        Get("/map/{slug}/render/structures");
        AllowAnonymous();
        Summary(s => s.Summary = WorldReadCatalog.Sentence("render/structures"));
        Description(b => b.Png().Refuses(404, 422).Reads(
            new QueryWord("minarea", "The smallest footprint counted as a structure, in blocks. Absent is 16."),
            Storey,
            new QueryWord("scale", "Pixels a block takes, 1 to 16. Absent draws at 4, and out of range clamps.", Min: 1, Max: 16)));
    }

    protected override bool Storeyed => true;

    protected override string Empty => "this world decodes to no column, so it holds no structure to find";

    protected override byte[]? Draw(BuiltRead read) => StructureFinder.Png(
        read.Built.World, Scale, OptionalInt("minarea") ?? 16, provenance: read.Built.Provenance);
}

/// <summary>GET /api/map/{slug}/render/mirror — what the board looks like against its own symmetry: the
/// columns that agree with their image, and the ones that do not. The read for whether a board a caller
/// believes is symmetric actually is.</summary>
internal sealed class MirrorReadEndpoint(MapRepository repo, MapReader reader, MapArtifactStore artifacts)
    : WorldRenderEndpoint(repo, reader, artifacts)
{
    public override void Configure()
    {
        Get("/map/{slug}/render/mirror");
        AllowAnonymous();
        Summary(s => s.Summary = WorldReadCatalog.Sentence("render/mirror"));
        Description(b => b.Png().Refuses(404, 422).Reads(
            new QueryWord("mode", "Which symmetry to compare against. Absent uses the one the map states.",
                ["none", "mirror_x", "mirror_z", "mirror_d1", "mirror_d2", "rot_90", "rot_180"]),
            new QueryWord("scale", "Pixels a block takes, 1 to 16. Absent draws at 4, and out of range clamps.", Min: 1, Max: 16)));
    }

    protected override string Empty => "this world decodes to no column, so it has no image to compare";

    protected override byte[]? Draw(BuiltRead read) => MirrorReport.Png(
        read.Built.World, Scale,
        Query<string?>("mode", isRequired: false) ?? read.Built.ResolvedIntent.Symmetry?.Mode,
        read.Built.ResolvedIntent.Symmetry?.CenterX ?? 0,
        read.Built.ResolvedIntent.Symmetry?.CenterZ ?? 0);
}

/// <summary>
/// GET /api/map/{slug}/column?at=x,z&amp;at=x,z — one or more columns bedrock-to-sky, every block named, as
/// <c>text/plain</c>.
///
/// <para><b>The workhorse, and the only honest answer.</b> Every picture beside it is a projection: a layer
/// stack, a wall's courses, a stamped room's floor, a goal's clearance and a void column are none of them
/// visible from above, and a section shows only the plane it cuts. This shows what is actually at a
/// coordinate — which is why it is the read to reach for when a picture and a document disagree.</para>
///
/// <para>Text rather than JSON because it is read by a person or an agent rather than parsed, and because it
/// is the one read a caller with no image reader can act on — the same reason the plan grid and the flow
/// account answer as characters.</para>
/// </summary>
internal sealed class ColumnReadEndpoint(MapRepository repo, MapReader reader, MapArtifactStore artifacts)
    : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/map/{slug}/column");
        AllowAnonymous();
        Summary(s => s.Summary = WorldReadCatalog.Sentence("column"));
        Description(b => b.PlainText().Refuses(404, 422).Reads(
            new QueryWord("at", "A column to read, as two whole numbers: `at=x,z`. Repeat it for more than "
                + "one, and they are answered in the order asked. At least one is required.")));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;

        var wanted = new List<(int X, int Z)>();
        foreach (var at in HttpContext.Request.Query["at"])
        {
            var pair = (at ?? "").Split(',', StringSplitOptions.TrimEntries);
            if (pair.Length != 2 || !int.TryParse(pair[0], out var x) || !int.TryParse(pair[1], out var z))
            {
                await Refusals.UnreadableAsync(HttpContext, "unreadable column",
                    $"'{at}' is not a column — each `at` is two whole numbers, `at=x,z`", ct, field: "at");
                return;
            }
            wanted.Add((x, z));
        }
        if (wanted.Count == 0)
        {
            await Refusals.UnreadableAsync(HttpContext, "no column asked for",
                "name at least one column to read: `?at=x,z`, repeated for more than one", ct, field: "at");
            return;
        }

        var read = await WorldReads.LoadAsync(map, reader, artifacts, ct);
        if (read is null)
        {
            await Refusals.WriteAsync(HttpContext, 404, "no world to read",
                [new Vocabulary.Finding(RequestRules.NoSuchSubject,
                    "this map has no stored sketch layout, so there is no world for the studio to build and "
                    + "read back")], ct);
            return;
        }

        var stacks = ColumnReport.Render(
            PgmStudio.Minecraft.Anvil.AnvilRegion.FromWorld(read.Built.World), wanted);

        var written = new System.Text.StringBuilder();
        foreach (var cell in wanted)
        {
            var stack = stacks[cell];
            written.AppendLine($"=== column ({cell.X}, {cell.Z}) — {stack.Count} solid block(s) ===");
            if (stack.Count == 0)
            {
                written.AppendLine("  (void — no block recorded at any height)");
                continue;
            }
            foreach (var block in stack)
                written.AppendLine($"  y{block.Y,3}  {block.Id,4}:{block.Data,-2}  {block.Name}");
        }

        await Send.StringAsync(written.ToString(), contentType: "text/plain; charset=utf-8", cancellation: ct);
    }
}

/// <summary>What one walk over a built board answers, in the units each part is stated in.</summary>
/// <param name="Reachable">Whether there is a way at all.</param>
/// <param name="Distance">How far it is, in blocks — the octile measure a player actually walks.</param>
/// <param name="Blocks">How many blocks the player must place: the climb, and the void bridged.</param>
/// <param name="Drops">How many falls over the free height it takes.</param>
/// <param name="WorstDrop">The deepest of them, in blocks.</param>
/// <param name="Aim">Which question was asked — <c>travel</c> for the short way, <c>reach</c> for the cheap one.</param>
/// <param name="Cells">The route itself, as <c>[x, z]</c> pairs.</param>
public sealed record WalkReadDto(bool Reachable, int Distance, int Blocks, int Drops, int WorstDrop,
    string Aim, IReadOnlyList<int[]> Cells);

/// <summary>How a walk read finds its ground and its two ends, shared by the numbers and the picture.</summary>
internal static class WalkReads
{
    /// <summary>The built board as ground a walk runs over: the world's own solid runs for what can be
    /// stood on and how high it is, the intent's build zones for what a block can be laid across, and the
    /// dressing's own water for what is swum. Nothing here is scanned — the world was built for this request.
    ///
    /// <para>The runs come off the <b>world</b> rather than off <c>Built.Columns</c>, which is the
    /// rasterizer's read of the terrain a build stood on — one span per cell, with no house, tree or
    /// structure in it. A walk over that set crosses a building as though it were not there.</para></summary>
    public static WalkGround Ground(BuiltRead read, string layoutJson)
    {
        var areas = (read.Built.ResolvedIntent.Build?.Areas ?? [])
            .Select(a => ((int)Math.Floor(a.MinX), (int)Math.Floor(a.MinZ),
                          (int)Math.Ceiling(a.MaxX), (int)Math.Ceiling(a.MaxZ)));
        var spans = PgmStudio.Minecraft.Anvil.WorldColumns.Of(read.Built.World)
            .SelectMany(column => column.Runs.Select(run => (column.X, column.Z, run.YBottom, run.YTop)));
        return WorldWalk.OfBuilt(spans, areas, Water(layoutJson));
    }

    /// <summary>The team a walk is measured for, checked against the ones the map spawns so a misspelling
    /// answers as itself rather than silently as everybody. Null where none was asked for.</summary>
    public static string? Team(string? asked, BuiltRead read)
        => asked is { Length: > 0 } && read.Doc is { } doc
            && EntryDenials.Teams(doc).FirstOrDefault(
                team => string.Equals(team, asked, StringComparison.OrdinalIgnoreCase)) is { } known
            ? known
            : null;

    /// <summary>Where the board's water is, carved by the same bed the decorator lays it with. A dressing
    /// that states none answers null, which is what a plan and an undressed board both are.</summary>
    private static HashSet<(int X, int Z)>? Water(string layoutJson)
    {
        var dressing = SketchLayout.Parse(layoutJson)?.Dressing;
        if (dressing is not { } element) return null;

        var cells = new HashSet<(int X, int Z)>();
        foreach (var prop in DressingJson.Deserialize(element.ToString()).Props.OfType<WaterProp>())
            foreach (var cell in WaterBed.Cells(prop.Points, prop.Radius, prop.Depth, prop.Form, prop.Edge,
                                                unchecked((uint)prop.Seed)))
                cells.Add((cell.X, cell.Z));
        return cells.Count == 0 ? null : cells;
    }

    /// <summary>A stated <c>x,z</c> or <c>x,z,y</c>, snapped onto ground a walk can reach. A marker's own
    /// coordinates are a block in a room rather than a cell of terrain, so they land inside a wall as often
    /// as on it. Snapped on the <b>shared</b> ground: a barred cell must stay where it is and answer
    /// unreachable, rather than slide until it finds one the team may stand on.
    ///
    /// <para>A stated <c>y</c> picks which storey of a stacked column is meant — the gallery under a deck
    /// and the deck over it are the same cell and different places. Stating none takes the lowest, which is
    /// where a player walking in at terrain level ends up.</para></summary>
    public static WalkPlace? Seat(string? asked, WalkGround ground)
    {
        var parts = (asked ?? "").Split(',');
        if (parts.Length is not (2 or 3)
            || !int.TryParse(parts[0], out var x) || !int.TryParse(parts[1], out var z)) return null;
        if (Cells.SnapToWalkable((x, z), ground.Footprint, 24) is not { } cell) return null;
        return parts.Length == 3 && int.TryParse(parts[2], out var y)
            ? ground.Nearest(cell, y)
            : ground.Stand(cell);
    }

    public static WalkAim Aim(string? asked) => asked?.ToLowerInvariant() switch
    {
        "reach" => WalkAim.Reach,
        "comfort" => WalkAim.Comfort,
        _ => WalkAim.Travel,
    };

    /// <summary>What a field may be shaded by. A comfort route is bounded by the journey's own length, so it
    /// has no field of its own; the picture shades the travel cost and draws the comfort route over it, which
    /// is the pairing that shows what the standoff bought.</summary>
    public static WalkAim Fieldable(WalkAim aim) => aim == WalkAim.Comfort ? WalkAim.Travel : aim;
}

/// <summary>GET /api/map/{slug}/walk — what one journey over this board costs. The read that says whether a
/// place can be got to, how far it is, how many blocks a player must place to get there and what it falls
/// down on the way; four answers in four units, none of them weighed against the others.</summary>
internal sealed class WalkReadEndpoint(MapRepository repo, MapReader reader, MapArtifactStore artifacts)
    : EndpointWithoutRequest<WalkReadDto>
{
    public override void Configure()
    {
        Get("/map/{slug}/walk");
        AllowAnonymous();
        Summary(s => s.Summary = WorldReadCatalog.Sentence("walk"));
        Description(b => b.Refuses(404, 422).Reads(
            new QueryWord("from", "Where the journey starts, as `x,z`, or `x,z,y` to pick which storey of a "
                               + "stacked column is meant. Snapped onto the nearest ground."),
            new QueryWord("to", "Where it ends, as `x,z`, or `x,z,y`."),
            new QueryWord("aim", "Which route to take: `travel` is the shortest, `reach` the one asking for "
                               + "the fewest placed blocks, `comfort` the least edge-hugging of the routes "
                               + $"within {Walk.Detour} blocks of the shortest. They differ, and the "
                               + "difference is the point.",
                ["travel", "reach", "comfort"]),
            new QueryWord("team", "Whose walk this is. Ground an `enter` rule bars that team from is taken "
                               + "out of it, so a route through an enemy protection is not offered. Absent "
                               + "walks the ground every team shares.")));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;
        var layout = await artifacts.LoadAsync(map.Id, ArtifactKind.SketchLayoutJson, ct);
        var read = await WorldReads.LoadAsync(map, reader, artifacts, ct);
        if (read is null || layout is null)
        {
            await Refusals.WriteAsync(HttpContext, 404, "no world to walk",
                [new Vocabulary.Finding(RequestRules.NoSuchSubject,
                    "this map has no stored sketch layout, so there is no board to walk over")], ct);
            return;
        }

        var shared = WalkReads.Ground(read, System.Text.Encoding.UTF8.GetString(layout));
        var team = WalkReads.Team(Query<string?>("team", isRequired: false), read);
        var ground = WorldWalk.For(shared, read.Doc, team);
        var aim = WalkReads.Aim(Query<string?>("aim", isRequired: false));
        var from = WalkReads.Seat(Query<string?>("from", isRequired: false), shared);
        var to = WalkReads.Seat(Query<string?>("to", isRequired: false), shared);
        if (from is null || to is null)
        {
            await Refusals.WriteAsync(HttpContext, 422, "nowhere to walk between",
                [new Vocabulary.Finding(RequestRules.Conflict,
                    "give `from` and `to` as `x,z` (or `x,z,y` to pick a storey); both must lie within 24 "
                    + "blocks of ground this board has")], ct);
            return;
        }

        var path = Walk.Between(from.Value, to.Value, ground, aim);
        var name = aim switch { WalkAim.Reach => "reach", WalkAim.Comfort => "comfort", _ => "travel" };
        await Send.OkAsync(path is null
            ? new WalkReadDto(false, -1, -1, 0, 0, name, [])
            : new WalkReadDto(true, path.Cost.Distance, path.Cost.Blocks, path.Cost.Drops, path.Cost.WorstDrop,
                name, [.. path.Cells.Select(cell => new[] { cell.X, cell.Z })]), ct);
    }
}

/// <summary>GET /api/map/{slug}/render/walk — the same walk, drawn. Every passable cell shaded by what
/// reaching it costs from `from`, with the route to `to` over the top. `field` picks which of the walk's
/// answers is shaded, because a picture ramps one number at a time.</summary>
internal sealed class WalkRenderEndpoint(MapRepository repo, MapReader reader, MapArtifactStore artifacts)
    : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/map/{slug}/render/walk");
        AllowAnonymous();
        Summary(s => s.Summary = WorldReadCatalog.Sentence("render/walk"));
        Description(b => b.Png().Refuses(404, 422).Reads(
            new QueryWord("from", "Where the field is measured from, as `x,z`, or `x,z,y` to pick which "
                               + "storey of a stacked column is meant."),
            new QueryWord("to", "Where the drawn route ends, as `x,z` or `x,z,y`. Absent draws the field "
                               + "alone."),
            new QueryWord("field", "Which answer to shade by. Absent shades the blocks a player must place.",
                WalkRender.Fields),
            new QueryWord("aim", "Which route to draw, and which cost the field prices. `comfort` has no "
                               + "field of its own — the field shades the travel cost and the comfort route "
                               + "is drawn on it, which is the pairing that shows what the standoff bought.",
                ["travel", "reach", "comfort"]),
            new QueryWord("scale", "Pixels a block takes, 1 to 8.", Min: 1, Max: 8),
            new QueryWord("team", "Whose walk this is. Ground an `enter` rule bars that team from is taken "
                               + "out of it, so a route through an enemy protection is not offered. Absent "
                               + "walks the ground every team shares.")));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await repo.OfRouteAsync(HttpContext, ct) is not { } map) return;
        var layout = await artifacts.LoadAsync(map.Id, ArtifactKind.SketchLayoutJson, ct);
        var read = await WorldReads.LoadAsync(map, reader, artifacts, ct);
        if (read is null || layout is null)
        {
            await Refusals.WriteAsync(HttpContext, 404, "no world to walk",
                [new Vocabulary.Finding(RequestRules.NoSuchSubject,
                    "this map has no stored sketch layout, so there is no board to walk over")], ct);
            return;
        }

        var shared = WalkReads.Ground(read, System.Text.Encoding.UTF8.GetString(layout));
        var team = WalkReads.Team(Query<string?>("team", isRequired: false), read);
        var ground = WorldWalk.For(shared, read.Doc, team);
        var aim = WalkReads.Aim(Query<string?>("aim", isRequired: false));
        var from = WalkReads.Seat(Query<string?>("from", isRequired: false), shared);
        if (from is null)
        {
            await Refusals.WriteAsync(HttpContext, 422, "nowhere to walk from",
                [new Vocabulary.Finding(RequestRules.Conflict,
                    "give `from` as `x,z` (or `x,z,y` to pick a storey), within 24 blocks of ground this "
                    + "board has")], ct);
            return;
        }

        var to = WalkReads.Seat(Query<string?>("to", isRequired: false), shared);
        var field = Walk.Field(from.Value, ground, WalkReads.Fieldable(aim));
        var route = to is { } target ? Walk.Between(from.Value, target, ground, aim) : null;
        var what = Query<string?>("field", isRequired: false) is { } asked
                   && WalkRender.Fields.Contains(asked) ? asked : "blocks";
        var scale = Query<int?>("scale", isRequired: false) is { } size ? Math.Clamp(size, 1, 8) : 2;

        var picture = WalkRender.Png(ground, field, what, from, to, route, scale);
        HttpContext.Response.ContentType = "image/png";
        await HttpContext.Response.Body.WriteAsync(picture.Pixels, ct);
    }
}
