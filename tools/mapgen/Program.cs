using System.Text.Json;
using PgmStudio.Analysis.Playability;
using PgmStudio.Domain;
using PgmStudio.Export;
using PgmStudio.MapGen;
using PgmStudio.Minecraft;
using PgmStudio.Minecraft.Dressing;
using PgmStudio.Minecraft.Render;
using PgmStudio.Pgm;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Render;
using PgmStudio.Pgm.Sketch;
using Dict = System.Collections.Generic.Dictionary<string, object?>;
using PgmStudio.Geom;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Houses;
using PgmStudio.Minecraft.Painting;
using PgmStudio.Minecraft.Palette;

// mapgen: build a whole map from one JSON spec, through the export path a map is really built through.
//
//   dotnet run --project tools/mapgen -- <spec.json> [more.json ...]
//   dotnet run --project tools/mapgen -- --describe <spec.json>   # compile only, report the board
//   dotnet run --project tools/mapgen -- --stages <spec.json>     # force the stages/ image set on
//
// The spec is a thin addressing layer over PlanModel, SketchLayout and MapIntent (docs/tools/capabilities.md,
// docs/tools/mapgen-review.md MG29) — a convenience field is shorthand for a fragment of one of those, and
// `layout`/`intent` hand a fragment through verbatim where the convenience fields run out. Nothing here
// reaches past SketchWorldBuilder, so a map it writes is a map an author could have drawn.

var describe = args.Contains("--describe");
var forceStages = args.Contains("--stages");
var specPaths = args.Where(a => !a.StartsWith("--")).ToList();
if (specPaths.Count == 0)
{
    Console.Error.WriteLine("usage: mapgen [--describe] [--stages] <spec.json> [...]");
    return 1;
}

var failures = 0;
foreach (var path in specPaths)
{
    try { Build(MapSpec.Parse(File.ReadAllText(path)), describe, forceStages); }
    catch (Exception error)
    {
        failures++;
        Console.Error.WriteLine($"✗ {Path.GetFileName(path)}: {error.Message}");
    }
}
return failures == 0 ? 0 : 1;

static void Build(MapSpec spec, bool describeOnly, bool forceStages)
{
    if (string.IsNullOrWhiteSpace(spec.Slug)) throw new ArgumentException("the spec needs a slug");

    // PGM reads the objective as a REQUIRED child (MapInfoImpl: Node.fromRequiredChildOrAttr(root,
    // "objective", "description")), so a map without one does not load at all. It is refused here rather
    // than defaulted, because the objective is the one sentence telling players what to do and a generated
    // stand-in would be a lie printed on the join screen.
    if (string.IsNullOrWhiteSpace(spec.Objective))
        throw new ArgumentException("the spec needs an 'objective' — PGM requires one and will not load the map without it");

    // ── the board: generated, drawn, or laid out as a catalogue ─────────────────────────────────────────
    // The first two produce a plan and compile it; a grid produces its layout directly, because a plot is a
    // disc or a cross and a plan piece is a rectangle. A grid map therefore carries no plan at all, and every
    // reader of one below is guarded rather than handed a manufactured stand-in.
    PlanModel? plan = null;
    SketchLayout layout;
    MapIntent intent;
    if (spec.Grid is { } grid)
    {
        if (spec.Compose is not null || spec.Plan is not null)
            throw new ArgumentException("a spec states its board once: 'grid', 'compose' or 'plan', not two");
        layout = IslandGrid.Lay(Plots(grid), new IslandGrid.Geometry(grid.Columns, grid.Pitch, grid.Floor, grid.Top));
        intent = new MapIntent();
    }
    else
    {
        if (spec.Compose is { } ask)
        {
            plan = Composer.Compose(new ComposeRequest(
                ask.PlayersPerTeam, ask.Teams, ask.Symmetry, ask.Seed, ask.Cell));
        }
        else if (spec.Plan is { } drawn)
        {
            plan = PlanModel.Parse(drawn.GetRawText())
                   ?? throw new ArgumentException("the plan document did not parse");
        }
        else throw new ArgumentException("the spec needs a 'compose', a 'plan' or a 'grid'");

        (layout, intent) = PlanCompiler.Compile(plan);
    }

    // ── the paint ────────────────────────────────────────────────────────────────────────────────────────
    if (spec.Theme is { } theme) Paint(layout, theme);

    // ── the ground's shape ───────────────────────────────────────────────────────────────────────────────
    if (spec.Relief is { } relief) Elevate(layout, relief);

    if (spec.RoomShell is { } shell) layout.RoomStyles = Shell(shell);

    // ── the addressing layer: a document fragment, handed through verbatim ──────────────────────────────
    // Merged after every convenience field above, so an author reaching for `layout`/`intent` can still see
    // and extend what they produced — a second theme entry beside `theme`'s, a shape added to what the plan
    // compiled, a destroyable's own defence wall.
    if (spec.Layout is { } layoutOverlay)
        layout = SketchLayout.Parse(DocumentOverlay.Merge(layout.ToJson(), layoutOverlay.GetRawText()))
                 ?? throw new ArgumentException("the layout overlay did not parse");
    if (spec.Intent is { } intentOverlay)
        intent = JsonSerializer.Deserialize<MapIntent>(
                     DocumentOverlay.Merge(JsonSerializer.Serialize(intent, IntentWire.Options), intentOverlay.GetRawText()),
                     IntentWire.Options)
                 ?? throw new ArgumentException("the intent overlay did not parse");

    // Read back rather than tracked, because a prop can arrive only through the layout overlay now: the
    // studio has no sampler, so dressing is exactly what was authored (docs/tools/sketch.md).
    var dressing = layout.Dressing is { } dressingJson
        ? DressingJson.Deserialize(dressingJson.GetRawText()) : DressingDoc.Empty;

    var islandNames = layout.Layout.Islands.Select(i => i.Id ?? "?").ToList();
    Console.WriteLine($"{spec.Slug}: {layout.Layout.Shapes.Count} shapes · {islandNames.Count} island(s) "
                    + $"· {intent.Wools?.Count ?? 0} wool(s) · {intent.Destroyables?.Count ?? 0} destroyable(s) "
                    + $"· {intent.Cores?.Count ?? 0} core(s) · {intent.Spawns?.Count ?? 0} spawn(s) "
                    + $"· {dressing.Props.OfType<HouseProp>().Count()} building(s) "
                    + $"· {dressing.Props.OfType<TreeProp>().Count()} tree(s)");
    if (describeOnly)
    {
        Console.WriteLine($"  islands: {string.Join(", ", islandNames)}");
        foreach (var shape in layout.Layout.Shapes.Take(8))
            Console.WriteLine($"  shape {shape.Id} {shape.Type} {Bounds(shape)}");
        return;
    }

    // ── build it through the export's own chain ──────────────────────────────────────────────────────────
    // One call, the same gate order the HTTP export runs — OB17 over the rasterized ground (wool monuments
    // included), OB19 goal clearance, and the playability judgement (B140) — so this driver cannot ship a
    // map the studio's own export would refuse. The decoration hook is where the spec states the identity
    // the intent does not carry.
    var composition = MapExportComposer.ComposeSketch(new Dict(), layout.ToJson(), intent, doc =>
    {
        doc["name"] = string.IsNullOrWhiteSpace(spec.Name) ? spec.Slug : spec.Name;
        doc["version"] = "1.0.0";
        if (!string.IsNullOrWhiteSpace(spec.Objective)) doc["objective"] = spec.Objective;
        if (spec.Authors is { Count: > 0 } authors)
            doc["authors"] = authors.Select(a => (object?)new Dict { ["name"] = a }).ToList();

        // Everything CtwStandards derives (itemkeep/toolrepair/itemremove) sits behind a kit; a map with
        // none still exports, but with its loadout rules silently empty rather than missing outright.
        if (doc.GetValueOrDefault("kits") is not List<object?> { Count: > 0 })
            Console.Error.WriteLine($"  ! {spec.Slug}: no kit — itemkeep/toolrepair/itemremove derive from "
                                   + "the spawn kit and will be empty");

        // The author element is how a map is sorted later, and a model that omits it makes its maps
        // unattributable. A bare name is what belongs there — the writer emits <author>Name</author> with no
        // uuid attribute, which is exactly the pseudonym form PGM reads — never an invented uuid.
        if (spec.Authors is not { Count: > 0 })
            Console.Error.WriteLine($"  ! {spec.Slug}: no author — add \"authors\": [\"<your model name>\"] "
                                   + "to the spec (a bare name, e.g. \"Fable 5\"; never a uuid)");
    });
    if (composition.IsError)
        throw new ArgumentException(
            $"{spec.Slug}: {composition.ErrorBody?.GetValueOrDefault("message") ?? composition.ErrorBody?.GetValueOrDefault("error")}");
    var built = composition.World!;
    var xml = composition.Xml!;

    var outDir = spec.OutDir ?? $"/media/sf_repos/CommunityMaps/dtcm/{spec.Slug}";
    Directory.CreateDirectory(outDir);
    var regionDir = Path.Combine(outDir, "region");
    AnvilRegionWriter.Write(built.World, regionDir);
    // Beside the voxels, not inside them (B133) — a block carries no provenance byte, so what claimed each
    // column travels as this sidecar rather than being lost the moment the world round-trips through disk.
    WorldProvenanceFile.Write(built.Provenance, regionDir);
    DressingReportFile.Write(built.DroppedProps, regionDir);
    foreach (var drop in built.DroppedProps ?? [])
        Console.Error.WriteLine($"  ! {spec.Slug}: dropped {drop.Kind} '{drop.Id}' — {drop.Reason}");

    // A tree wants soil — dirt, grass, sand or snow, never bare stone (the author's rule). A complaint
    // rather than a drop: the tree stands and the line names the ground to repaint.
    foreach (var (treeX, treeZ, _) in DressingScope.TreeFootprints(layout.ToJson()))
    {
        var groundId = GroundBlockAt(built.World, treeX, treeZ);
        if (groundId is { } id && id != Blocks.Log && id != Blocks.Log2
            && DressingPalette.SoilShare(id, 0) == 0 && id != 78 && id != 80)   // 78/80: snow layer/block — soil to a tree
            Console.Error.WriteLine($"  ! {spec.Slug}: tree at ({treeX}, {treeZ}) stands on block {id}, "
                                   + "not soil — trees want dirt, grass, sand or snow beneath them");
    }
    LevelDatWriter.Write(outDir, spec.Slug, built.SpawnX, built.SpawnY, built.SpawnZ,
                         DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    File.WriteAllText(Path.Combine(outDir, "map.xml"), xml);

    Console.WriteLine($"  → {outDir}  (spawn {built.SpawnX},{built.SpawnY},{built.SpawnZ})  {Census(built.World)}");

    // ── the coverage read — where the ground is lived on, and where it is dead ───────────────────────────
    // Printed on every build rather than only with the stages, because the dead share is the number that says
    // whether a board is too big for what it plays, and a driver should see it before anyone loads the map.
    var (surfaceColumns, y0Columns) = WorldColumns.Membership(built.World);
    var coverage = GroundCoverage.Read(
        composition.Doc!, surfaceColumns, y0Columns, DressingScope.DecorCells(layout.ToJson()));
    Console.WriteLine(
        $"  coverage: {Share(coverage.ReachedCells)} reached · {Share(coverage.DecoratedCells)} decorated · "
        + $"{Share(coverage.DeadCells)} dead over {coverage.GroundCells} ground cells");
    foreach (var patch in coverage.DeadPatches.Take(5))
        Console.WriteLine($"    dead: {patch.Area} cells at ({patch.CentroidX}, {patch.CentroidZ}), "
                        + $"{patch.NearestReachedBlocks} blocks off the match");
    string Share(int cells) => coverage.GroundCells == 0 ? "0%" : $"{100.0 * cells / coverage.GroundCells:0}%";

    // ── one named picture per stage ──────────────────────────────────────────────────────────────────────
    // Off by default (a batch run over many specs should not pay for pictures it will not look at); the spec
    // or the CLI's --stages flag turns it on. The world is already built and held in memory, so every world
    // read-back below draws over `built.World` itself — no second load off the region files just written.
    if (spec.Stages || forceStages)
        EmitStages(outDir, plan, built.World, built.Provenance, xml, layout.ToJson(), coverage);
}

/// <summary>The block a column's ground shows just under a tree's trunk base — the highest non-air block
/// that is not the tree's own wood.</summary>
static int? GroundBlockAt(VoxelWorld world, int x, int z)
{
    for (var y = VoxelWorld.MaxHeight - 1; y >= 1; y--)
    {
        var (id, _) = world.GetBlock(x, y, z);
        if (id == 0 || id is Blocks.Log or Blocks.Log2 or Blocks.Leaves or Blocks.Leaves2) continue;
        return id;
    }
    return null;
}

/// <summary>The spec's plots as the emitter's own, which is the whole of the translation: the spec names the
/// shape vocabulary and the emitter draws it.</summary>
static List<GridPlot> Plots(GridSpec grid)
{
    if (grid.Plots.Count == 0) throw new ArgumentException("a 'grid' needs at least one plot");
    return [.. grid.Plots.Select(plot => new GridPlot(
        plot.Kind, plot.Theme, plot.Name, plot.Width, plot.Depth, plot.Radius, plot.Vertices))];
}

/// <summary>The eight named stage images, into <c>stages/</c> beside <c>region/</c> and <c>map.xml</c> — the
/// set an agent asks for by name rather than remembers to have rendered. Every world
/// read-back draws over the <see cref="VoxelWorld"/> the build just produced, via
/// <see cref="AnvilRegion.FromWorld"/>, not a re-read of the region files just written.
///
/// <para><b>plan</b> is the board before it was built, off the compiled <see cref="PlanModel"/> — the same
/// geometry <c>GET /plans/{id}/png</c> serves, called directly with no HTTP round trip. <b>heightmap</b> and
/// <b>contour</b> are one renderer read twice: elevation alone, then the same ramp with contour lines added,
/// so the ground's shape is checkable with and without the third reading layered on. <b>surface</b> is what
/// the paint actually laid, by material family rather than by height. <b>dressing</b> is the finished terrain
/// and props read from directly above, before the objective is drawn on top of it — <b>topdown</b> is the same
/// view again with the map.xml goal boxes overlaid, so a prop placed through a room shows up in the first and
/// a goal standing over void shows up in the second; both are the false-coloured category reading
/// (<see cref="TopDownColorMode.Category"/>), not the map's real materials, so foliage and structure separate
/// from the ground at a glance. <b>foliage</b> isolates the canopy as each tree's own point and measured crown
/// radius (docs/world-export/decoration.md §6) rather than the leaf mass, and <b>objectives</b> isolates where
/// the declared goals sit; neither has to be picked out of the full picture by eye.
/// <b>traversability</b> answers a question neither top-down can: whether the
/// navigable ground actually joins spawn to every goal. <b>structures</b> is what the world stamped,
/// independent of theme.</para></summary>
static void EmitStages(string outDir, PlanModel? plan, VoxelWorld world, WorldProvenance provenance, string xml,
    string layoutJson, GroundCoverage.Result coverage)
{
    var dir = Path.Combine(outDir, "stages");
    Directory.CreateDirectory(dir);

    // A grid map has no plan to draw — its layout is emitted rather than compiled — so the stage set is the
    // other eight. Absent rather than blank: an empty board would read as a compile that produced nothing.
    if (plan is { } board) File.WriteAllBytes(Path.Combine(dir, "plan.png"), PlanBoardPng.Render(board));

    TopDownRender.Run(world, Path.Combine(dir, "dressing.png"), map: null, scale: 3, yMax: null, name: "dressing",
        provenance: provenance);
    HeightProfileRender.Run(world, Path.Combine(dir, "heightmap.png"), scale: 3, contourInterval: 0,
        greyscale: false, markWater: true, drawContours: false, name: "heightmap");
    HeightProfileRender.Run(world, Path.Combine(dir, "contour.png"), scale: 3, contourInterval: 0,
        greyscale: false, markWater: true, drawContours: true, name: "contour");
    SurfaceReport.Run(world, Path.Combine(dir, "surface.png"), scale: 3);
    StructureFinder.Run(world, Path.Combine(dir, "structures.png"), scale: 3, minimumArea: 12, provenance: provenance);
    // The point-and-radius reading comes straight off the dressing document, not the world, so this caller
    // reaches for the layout it already holds rather than anything the build produced.
    TopDownRender.Run(world, Path.Combine(dir, "foliage.png"), map: null, scale: 3, yMax: null, name: "foliage",
        layer: TopDownLayer.Foliage, provenance: provenance, treePoints: DressingScope.TreeFootprints(layoutJson));

    // The overlay reads the map document already built in memory — parsed back from the XML string rather
    // than the file just written, so this too costs no extra disk read.
    MapXml? map = null;
    try { map = MapParser.ParseXmlString(xml); }
    catch (Exception error) { Console.Error.WriteLine($"  ! stages: map.xml overlay unavailable ({error.Message})"); }

    TraversabilityRender.Run(world, Path.Combine(dir, "traversability.png"), map, scale: 3);
    // The coverage stage draws the measure's own grid — the corridors the match walks, the rings the fights
    // claim, the decorated fringe, and the dead ground the numbers above already named.
    File.WriteAllBytes(Path.Combine(dir, "coverage.png"), CoverageRender.Png(coverage));
    TopDownRender.Run(world, Path.Combine(dir, "topdown.png"), map, scale: 3, yMax: null, name: "topdown",
        provenance: provenance);
    TopDownRender.Run(world, Path.Combine(dir, "objectives.png"), map, scale: 3, yMax: null, name: "objectives",
        layer: TopDownLayer.Objectives, provenance: provenance);

    Console.WriteLine($"  stages → {dir}");
}

/// <summary>What actually reached the world, counted out of the voxels rather than off the props that were
/// asked for. A prop is a request — it is dropped where it finds no ground, lands on a protected cell or is
/// pruned by the pass — so the only honest report of a forest is the wood standing in it.</summary>
static string Census(VoxelWorld world)
{
    int wood = 0, leaf = 0, top = 0, woodTop = 0, woodLow = int.MaxValue, bedrock = 0, bedrockTop = 0;
    for (var x = -400; x <= 400; x++)
        for (var z = -400; z <= 400; z++)
            for (var y = 1; y < VoxelWorld.MaxHeight; y++)
            {
                var (id, _) = world.GetBlock(x, y, z);
                if (id == 0) continue;
                if (id is Blocks.Log or Blocks.Log2)
                {
                    wood++;
                    if (y > woodTop) woodTop = y;
                    if (y < woodLow) woodLow = y;
                }
                else if (id is Blocks.Leaves or Blocks.Leaves2) leaf++;
                // Above the floor only. Every column of a map carries a bedrock block at its base so players
                // cannot dig out, and counting those says nothing; what matters is bedrock standing in the
                // map, which is the built-in room shell that a bound house is meant to replace.
                else if (id == Blocks.Bedrock && y > 2) { bedrock++; if (y > bedrockTop) bedrockTop = y; }
                if (y > top) top = y;
            }
    // Logs count a building's corner posts as well as a trunk, so the log count alone cannot say whether a
    // forest was planted — only that wood was laid. Leaves can: nothing but a tree lays one, which makes the
    // leaf count the forest's only honest measure and the number to read when a map looks bare.
    var span = wood == 0 ? "no logs" : $"logs y {woodLow}..{woodTop}";
    // Bedrock is reported because a room raised as a building must not leave any: the built-in shell is a
    // bedrock lid, so a count above zero means a room fell back to it rather than taking its house.
    return $"{wood} logs · {leaf} leaves · {bedrock} standing bedrock (to y {bedrockTop}) · highest y {top} · {span}";
}

// ── paint ────────────────────────────────────────────────────────────────────────────────────────────────

/// <summary>One theme over the whole board. A map of one material reads as one place; a shape that wants its
/// own paint names it, and nothing here stops that.</summary>
static void Paint(SketchLayout layout, ThemeSpec spec)
{
    var theme = new TerrainTheme
    {
        Surface = new TopBand(Surface(spec), 3, Enabled: true),
        Wall = MaterialRecipes.Pattern(spec.Pattern, spec.Wall),
        WallEnabled = true,
        Fill = MaterialRecipes.One(spec.Fill),
        Rim = spec.Rim is { Length: > 0 } rim
            ? new TopBand(MaterialRecipes.One(rim), 1, Enabled: true)
            : new TopBand(MaterialRecipes.One(spec.Wall), 1, Enabled: false),
    };
    layout.Themes = new Dictionary<string, JsonElement>
    {
        ["map"] = JsonDocument.Parse(TerrainThemeJson.Serialize(theme)).RootElement.Clone(),
    };
    layout.MapTheme = "map";
}

static TerrainMaterial Surface(ThemeSpec spec) =>
    spec.Surface.Equals("grass", StringComparison.OrdinalIgnoreCase)
        ? MaterialRecipes.Grass
        : MaterialRecipes.Pattern(spec.Pattern, spec.Surface);

// ── relief ───────────────────────────────────────────────────────────────────────────────────────────────

/// <summary>The stated elevation, bound to every island the board compiled to. A relief is keyed by island id
/// because the island is the unit it is solved over — solved per shape it would seam wherever two shapes of
/// one landmass meet and disagree about the height they share.</summary>
static void Elevate(SketchLayout layout, ReliefSpec spec)
{
    var relief = new Dictionary<string, SketchReliefJson>();
    foreach (var island in layout.Layout.Islands)
    {
        if (island.Id is not { Length: > 0 } id) continue;
        var entry = new SketchReliefJson
        {
            Base = spec.Base,
            Reach = spec.Reach,
            Step = Math.Max(1, spec.Step),
            Stairs = spec.Stairs,
            Grain = spec.Grain is { } grain
                ? new ReliefGrainJson { Amplitude = grain.Amplitude, Scale = grain.Scale, Seed = grain.Seed }
                : null,
            Marks = Marks(spec, layout, island),
            Pushes = spec.Pushes is { } pushes
                ? JsonSerializer.Deserialize<List<ReliefPushJson>>(pushes.GetRawText(), MapSpec.Options)
                : null,
        };
        relief[id] = entry;
    }
    if (relief.Count > 0) layout.Relief = relief;
}

static List<ReliefMarkJson>? Marks(ReliefSpec spec, SketchLayout layout, SketchIsland island)
{
    var marks = spec.Marks is { } stated
        ? JsonSerializer.Deserialize<List<ReliefMarkJson>>(stated.GetRawText(), MapSpec.Options) ?? []
        : [];

    if (spec.Scatter is { Count: > 0 } scatter)
    {
        var box = IslandBounds(layout, island);
        if (box is { } bounds)
        {
            // Deterministic per island, so the same spec rebuilds the same ground. The island's name is
            // folded in by hand rather than through string.GetHashCode, which .NET randomises per process:
            // seeding from it made every run of the same spec a different map, and turned every measurement
            // taken across two runs into noise.
            var random = new Random(scatter.Seed * 7919 + StableHash(island.Id!));
            for (var index = 0; index < scatter.Count; index++)
            {
                var x = bounds.MinX + random.NextDouble() * (bounds.MaxX - bounds.MinX);
                var z = bounds.MinZ + random.NextDouble() * (bounds.MaxZ - bounds.MinZ);
                var height = scatter.MinHeight + random.NextDouble() * (scatter.MaxHeight - scatter.MinHeight);
                marks.Add(new ReliefMarkJson
                {
                    Kind = "point",
                    At = [Math.Round(x), Math.Round(z)],
                    Radius = Math.Max(2, scatter.Radius * (0.5 + random.NextDouble())),
                    Heights = [Math.Round(height, 1)],
                });
            }
        }
    }
    return marks.Count > 0 ? marks : null;
}

/// <summary>A hash of a name that is the same in every process — what the relief scatter's per-island seed
/// needs and what <see cref="string.GetHashCode()"/> deliberately is not.</summary>
static int StableHash(string text)
{
    var hash = 17;
    foreach (var character in text) hash = unchecked(hash * 31 + character);
    return hash & 0x7fffffff;
}

// ── the rooms ────────────────────────────────────────────────────────────────────────────────────────────

/// <summary>One preset by name.</summary>
static HousePresets.House Preset(string name)
{
    var house = HousePresets.All.FirstOrDefault(h => h.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    if (house.Name is null)
        throw new ArgumentException(
            $"no house preset '{name}' — have: {string.Join(", ", HousePresets.All.Select(h => h.Name))}");
    return house;
}

/// <summary>The buildings the wool and spawn rooms are stamped as, snapshotted onto the layout the way the
/// studio's room-style step binds a library row — so editing the preset later can never rebuild a shipped
/// map's rooms.</summary>
static SketchRoomStyles Shell(RoomShellSpec spec) => new()
{
    Wool = Stamped(spec.Wool) ?? default,
    Spawn = Stamped(spec.Spawn) ?? SketchRoomStyles.Open,
};

/// <summary>One preset serialised as the shell a room is raised in, or null to leave the room as it was.
/// <c>open</c> is its own answer: no building, the ground itself.</summary>
static JsonElement? Stamped(string? preset)
{
    if (preset is not { Length: > 0 }) return null;
    if (preset.Equals("open", StringComparison.OrdinalIgnoreCase)) return SketchRoomStyles.Open;
    return JsonDocument.Parse(HouseStyleJson.Serialize(Preset(preset).Style)).RootElement;
}

// ── geometry the spec does not have to state ─────────────────────────────────────────────────────────────

static Rect? IslandBounds(SketchLayout layout, SketchIsland island)
{
    Rect? box = null;
    foreach (var id in island.ShapeIds)
    {
        var shape = layout.Layout.Shapes.FirstOrDefault(s => s.Id == id);
        if (shape is null) continue;
        var bounds = Bounds(shape);
        if (bounds is not { } b) continue;
        box = box is { } have
            ? new Rect(Math.Min(have.MinX, b.MinX), Math.Min(have.MinZ, b.MinZ),
                      Math.Max(have.MaxX, b.MaxX), Math.Max(have.MaxZ, b.MaxZ))
            : b;
    }
    return box;
}

static Rect? Bounds(SketchShape shape)
{
    if (shape.Vertices is { Length: > 0 } vertices)
        return new Rect(vertices.Min(v => v[0]), vertices.Min(v => v[1]),
                       vertices.Max(v => v[0]), vertices.Max(v => v[1]));
    if (shape.CenterX is { } cx && shape.CenterZ is { } cz && shape.Radius is { } r)
        return new Rect(cx - r, cz - r, cx + r, cz + r);
    if (shape.MinX is { } minX && shape.MinZ is { } minZ && shape.MaxX is { } maxX && shape.MaxZ is { } maxZ)
        return new Rect(minX, minZ, maxX, maxZ);
    return null;
}

/// <summary>The <c>MapIntent</c> wire shape — the same Web-default options every other reader/writer of the
/// artifact uses (`GET·PUT /map/{slug}/intent`), so an <see cref="MapSpec.Intent"/> overlay names its fields
/// the way the studio itself would read or write them.</summary>
static class IntentWire
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
