using System.Text.Json;
using PgmStudio.Api.Services;
using PgmStudio.Domain;
using PgmStudio.MapGen;
using PgmStudio.Minecraft;
using PgmStudio.Minecraft.Dressing;
using PgmStudio.Pgm;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Sketch;
using Dict = System.Collections.Generic.Dictionary<string, object?>;

// mapgen: build a whole map from one JSON spec, through the export path a map is really built through.
//
//   dotnet run --project tools/mapgen -- <spec.json> [more.json ...]
//   dotnet run --project tools/mapgen -- --describe <spec.json>   # compile only, report the board
//
// The spec says what the map is about; the generator answers the rest. Nothing here reaches past
// SketchWorldBuilder, so a map it writes is a map an author could have drawn.

var describe = args.Contains("--describe");
var specPaths = args.Where(a => !a.StartsWith("--")).ToList();
if (specPaths.Count == 0)
{
    Console.Error.WriteLine("usage: mapgen [--describe] <spec.json> [...]");
    return 1;
}

var failures = 0;
foreach (var path in specPaths)
{
    try { Build(MapSpec.Parse(File.ReadAllText(path)), describe); }
    catch (Exception error)
    {
        failures++;
        Console.Error.WriteLine($"✗ {Path.GetFileName(path)}: {error.Message}");
    }
}
return failures == 0 ? 0 : 1;

static void Build(MapSpec spec, bool describeOnly)
{
    if (string.IsNullOrWhiteSpace(spec.Slug)) throw new ArgumentException("the spec needs a slug");

    // PGM reads the objective as a REQUIRED child (MapInfoImpl: Node.fromRequiredChildOrAttr(root,
    // "objective", "description")), so a map without one does not load at all. It is refused here rather
    // than defaulted, because the objective is the one sentence telling players what to do and a generated
    // stand-in would be a lie printed on the join screen.
    if (string.IsNullOrWhiteSpace(spec.Objective))
        throw new ArgumentException("the spec needs an 'objective' — PGM requires one and will not load the map without it");

    // ── the board: generated, or drawn ───────────────────────────────────────────────────────────────────
    PlanModel plan;
    if (spec.Compose is { } ask)
    {
        plan = Composer.Compose(new ComposeRequest(
            ask.PlayersPerTeam, ask.Teams, ask.Symmetry, ask.Seed, ask.Cell));
        Retarget(plan, ask.ObjectiveMode);
    }
    else if (spec.Plan is { } drawn)
    {
        plan = PlanModel.Parse(drawn.GetRawText())
               ?? throw new ArgumentException("the plan document did not parse");
    }
    else throw new ArgumentException("the spec needs either a 'compose' or a 'plan'");

    var (layout, intent) = PlanCompiler.Compile(plan);

    // ── the paint ────────────────────────────────────────────────────────────────────────────────────────
    if (spec.Theme is { } theme) Paint(layout, theme);

    // ── the ground's shape ───────────────────────────────────────────────────────────────────────────────
    if (spec.Relief is { } relief) Elevate(layout, relief);

    // ── what stands on it ────────────────────────────────────────────────────────────────────────────────
    var props = new List<PlacedProp>();
    // The ground as it will actually be built, each column's walking surface with it — read once, because
    // everything that stands on the map has to be put somewhere that ground exists.
    var ground = new Dictionary<(int X, int Z), int>();
    foreach (var (x, z, _, top) in SketchRasterizer.RasterizeColumns(layout.ToJson()))
        ground[(x, z)] = Math.Max(ground.GetValueOrDefault((x, z), int.MinValue), top);

    // The map's own mirror, read the way the dressing pass itself will read it (DressingScope.SymmetryOf) —
    // so a site rejected here because it collides with an already-placed prop is rejected for the same
    // reason Decorator would refuse it after fanning, not a looser guess that only checks a raw anchor.
    var symmetry = new DressingSymmetry(
        layout.Setup?.MirrorMode, layout.Setup?.Center?.Cx ?? 0, layout.Setup?.Center?.Cz ?? 0);

    if (spec.Village is { Count: > 0 } village) props.AddRange(Settle(ground, intent, village, symmetry));
    if (spec.Houses is { Count: > 0 } houses) props.AddRange(Placed(houses));
    if (spec.Trees is { } trees) props.AddRange(Forest(ground, intent, trees, props, symmetry));
    if (props.Count > 0)
    {
        var dressingJson = DressingJson.Serialize(new DressingDoc { Props = props });
        layout.Dressing = JsonDocument.Parse(dressingJson).RootElement;
        var readBack = DressingJson.Deserialize(dressingJson).Props.Count;
        if (readBack != props.Count)
            Console.Error.WriteLine($"  ! dressing round-trip lost props: wrote {props.Count}, read {readBack}");
    }

    if (spec.RoomShell is { } shell) layout.RoomStyles = Shell(shell);

    var islandNames = layout.Layout.Islands.Select(i => i.Id ?? "?").ToList();
    var asked = spec.Trees?.Count ?? 0;
    var found = props.OfType<TreeProp>().Count();
    Console.WriteLine($"{spec.Slug}: {layout.Layout.Shapes.Count} shapes · {islandNames.Count} island(s) "
                    + $"· {intent.Wools?.Count ?? 0} wool(s) · {intent.Spawns?.Count ?? 0} spawn(s) "
                    + $"· {props.OfType<HouseProp>().Count()} building(s) · {found}/{asked} tree site(s)");
    if (describeOnly)
    {
        Console.WriteLine($"  islands: {string.Join(", ", islandNames)}");
        foreach (var shape in layout.Layout.Shapes.Take(8))
            Console.WriteLine($"  shape {shape.Id} {shape.Type} {Bounds(shape)}");
        return;
    }

    // ── build it the way the export does ─────────────────────────────────────────────────────────────────
    var built = SketchWorldBuilder.Build(layout.ToJson(), intent);

    var doc = new Dict();
    IntentGenerator.Apply(doc, built.ResolvedIntent);
    doc["name"] = string.IsNullOrWhiteSpace(spec.Name) ? spec.Slug : spec.Name;
    doc["version"] = "1.0.0";
    if (!string.IsNullOrWhiteSpace(spec.Objective)) doc["objective"] = spec.Objective;
    if (spec.Authors is { Count: > 0 } authors)
        doc["authors"] = authors.Select(a => (object?)new Dict { ["name"] = a }).ToList();
    var xml = XmlWriter.ToXml(Deserializer.FromDict(doc));

    var outDir = spec.OutDir ?? $"/media/sf_repos/CommunityMaps/dtcm/{spec.Slug}";
    Directory.CreateDirectory(outDir);
    AnvilRegionWriter.Write(built.World, Path.Combine(outDir, "region"));
    LevelDatWriter.Write(outDir, spec.Slug, built.SpawnX, built.SpawnY, built.SpawnZ,
                         DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    File.WriteAllText(Path.Combine(outDir, "map.xml"), xml);

    Console.WriteLine($"  → {outDir}  (spawn {built.SpawnX},{built.SpawnY},{built.SpawnZ})  {Census(built.World)}");
}

/// <summary>Turn the goals the generator placed into the kind of goal this map is played for.
///
/// <para>A wool room, a monument and a core occupy the same slot in a board: one team's thing to defend,
/// sited where the generator's budget put it. So a destroy-the-monument map is the same composition with its
/// markers retargeted, not a different generator — which is also why the corpus's destroy categories share a
/// layout vocabulary with the capture ones.</para>
///
/// <para><c>dtcm</c> gives a team <b>both</b>, because that category is not one objective but the two shipped
/// on one map: the monument keeps the goal the generator sited, and the core takes a slot beside it on the
/// same piece. A map with only one of the two is a destroy-the-monument map or a destroy-the-core map, and
/// both of those are already sayable.</para></summary>
static void Retarget(PlanModel plan, string mode)
{
    var word = mode.Trim().ToLowerInvariant();
    if (word is "ctw" or "") return;
    if (word is not ("dtm" or "dtcm"))
        throw new ArgumentException($"no objective mode '{mode}' — have: ctw, dtm, dtcm");

    var goals = plan.Placements.Wools;
    if (goals.Count == 0) return;

    for (var index = 0; index < goals.Count; index++)
    {
        var goal = goals[index];
        plan.Placements.Destroyables.Add(
            new DestroyablePlacement { Id = $"monument-{index}", Piece = goal.Piece, At = goal.At });
        if (word == "dtcm")
            plan.Placements.Cores.Add(new CorePlacement
            {
                Id = $"core-{index}",
                Piece = goal.Piece,
                // Two cells along the piece from the monument: far enough that the two structures are separate
                // things to defend, near enough that they stay one place on the board.
                At = [goal.At[0] + 2, goal.At[1]],
            });
    }
    plan.Placements.Wools = [];
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
        Wall = Materials.Pattern(spec.Pattern, spec.Wall),
        WallEnabled = true,
        Fill = Materials.One(spec.Fill),
        Rim = spec.Rim is { Length: > 0 } rim
            ? new TopBand(Materials.One(rim), 1, Enabled: true)
            : new TopBand(Materials.One(spec.Wall), 1, Enabled: false),
    };
    layout.Themes = new Dictionary<string, JsonElement>
    {
        ["map"] = JsonDocument.Parse(TerrainThemeJson.Serialize(theme)).RootElement.Clone(),
    };
    layout.MapTheme = "map";
}

static TerrainMaterial Surface(ThemeSpec spec) =>
    spec.Surface.Equals("grass", StringComparison.OrdinalIgnoreCase)
        ? Materials.Grass
        : Materials.Pattern(spec.Pattern, spec.Surface);

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

// ── what stands on the ground ────────────────────────────────────────────────────────────────────────────

/// <summary>Trees over the board, kept off the objectives. A tree is scenery, and scenery that grows through
/// a wool room is a bug the map only shows once it is walked.
///
/// <para>Placed against the <b>rasterized ground</b> rather than against an island's bounding box. A board's
/// islands are polygons, and the box round one is mostly void: sampling the box puts most of a forest over
/// the edge, where a tree has no surface to stand on and simply is not built. The rasterizer is also what
/// knows each column's top, which is what lets a tree be kept off ground too steep to stand a trunk on.</para></summary>
static List<PlacedProp> Forest(Dictionary<(int X, int Z), int> ground, MapIntent intent, TreeSpec spec,
                               List<PlacedProp> standing, DressingSymmetry symmetry)
{
    var props = new List<PlacedProp>();
    var woods = spec.Woods is { Count: > 0 } named ? named : ["oak", "birch", "spruce", "dark oak"];
    var keepOut = KeepOut(intent, spec.Clearance);
    var random = new Random(spec.Seed);

    if (ground.Count == 0) return props;
    // Sampled from one representative half of the orbit rather than the whole board. Drawing candidates from
    // both halves independently is what let two unrelated trees pick sites that turned out to be *each
    // other's* mirror image — a coincidence the old site-only check never saw and the new fanned one caught
    // only after paying for it in rejected attempts. A canonical site's mirror is never sampled again: it is
    // fanned, deterministically, into the same tree — so two different trees cannot collide with each
    // other's reflection by chance, only a real neighbour can.
    var cells = ground.Keys.Where(cell => symmetry.IsCanonical(cell.X, cell.Z)).ToList();
    if (cells.Count == 0) cells = ground.Keys.ToList();

    // Buildings are already placed, and a tree through a roof is the same fault as a tree through a room.
    // Every image of every building counts, not just the one an author actually dragged — the dressing pass
    // fans a building across the map's mirror, and a site only checked against the original rectangle can
    // still land squarely on its mirrored copy.
    var built = standing.OfType<HouseProp>()
        .SelectMany(house => FannedAround(house, symmetry, margin: 4))
        .ToList();

    // A tree's height is held to 24: past that a grown crown is a volume the export spends real time filling,
    // and a tree taller than the drop it stands on stops reading as scenery.
    var minHeight = Math.Clamp(spec.MinHeight, 5, 24);
    var maxHeight = Math.Clamp(spec.MaxHeight, minHeight, 24);

    // A GROWN tree is held lower still, because past this it stops being placed at all. The dressing pass
    // drops a prop if any cell it occupies — at any height — falls on a protected column, and a grown crown
    // is wide: measured over one board at twenty-four sites, a grown oak lands 590 leaves at height 8, 364 at
    // 12 and nothing whatever at 20, while a template oak on the same sites climbs 1584 → 3424 → 7194. So a
    // tall grown tree is not a tall tree, it is an absent one, and asking for one silently empties a forest.
    var grownCeiling = Math.Min(maxHeight, 14);

    var placed = 0;
    for (var attempt = 0; attempt < spec.Count * 60 && placed < spec.Count; attempt++)
    {
        var (x, z) = cells[random.Next(cells.Count)];
        if (keepOut.Any(rect => Inside(rect, x, z))) continue;
        // Every image of this candidate has to clear the keepout too — a mirror board fans the site as well
        // as the building, so a tree whose *reflection* lands on a roof is exactly as much a fault as one
        // planted on the roof directly.
        var candidateImages = FannedPoints(x, z, symmetry).ToList();
        if (built.Any(rect => candidateImages.Any(image => Inside(rect, image.X, image.Z)))) continue;
        // Only the raw site is checked for spacing, not its fanned images: candidates are already drawn from
        // one canonical half (below), so two different trees' own reflections cannot collide with each
        // other — only a canonical tree's mirror and a *different* canonical tree very near the mirror line
        // itself could, a narrow edge case Decorator's own post-fan check still catches if it ever happens.
        if (props.OfType<TreeProp>().Any(t => Math.Abs(t.X - x) < 5 && Math.Abs(t.Z - z) < 5)) continue;

        var grown = spec.Form.Equals("grown", StringComparison.OrdinalIgnoreCase)
                 || (spec.Form.Equals("mixed", StringComparison.OrdinalIgnoreCase) && random.Next(2) == 0);
        var ceiling = grown ? grownCeiling : maxHeight;
        var height = Math.Min(minHeight, ceiling) + random.NextDouble() * Math.Max(0, ceiling - minHeight);

        // The disc is the tree's FOOT, not its crown. A prop seats on the cells at its lowest level, and a
        // tree is narrow down there whatever it does higher up — a few blocks of stem — so sizing this off
        // the crown asks for ground the tree never rests on and rejects most of a board's real estate.
        if (!Clear(ground, x, z, grown ? 3 : 2, ground[(x, z)])) continue;
        props.Add(grown
            ? new TreeProp
            {
                X = x, Z = z, Form = TreeForm.Grown,
                Wood = woods[random.Next(woods.Count)],
                Height = Math.Round(height),
                Stems = 1 + random.Next(3),
                Levels = 2 + random.Next(2),
                Leader = 0.35 + random.NextDouble() * 0.35,
                Flow = 0.25 + random.NextDouble() * 0.4,
                BranchAngle = 0.8 + random.NextDouble() * 0.6,
                LeafSize = 0.45 + random.NextDouble() * 0.35,
                Whorled = spec.Whorled ?? random.Next(3) == 0,
            }
            : new TreeProp
            {
                X = x, Z = z, Form = TreeForm.Template,
                Species = woods[random.Next(woods.Count)],
                Height = Math.Round(height),
            });
        placed++;
    }
    return props;
}

/// <summary>Whether a tree can stand at a site. Two different questions over two different discs, because the
/// pass asks two different things.
///
/// <para><b>Ground must exist</b> under everything the crown rests on — that is the test that actually drops
/// a prop, and it is why the disc is sized off the tree rather than off the trunk. <b>Levelness</b> is only
/// asked close in: a prop seats on the lowest column beneath it, so a tree over falling ground sits into the
/// slope rather than failing, and demanding a flat disc the width of a crown bans trees from every hillside
/// on the map. Asking the wide disc to be level is what left two of the eight boards with no tree on
/// them.</para></summary>
static bool Clear(Dictionary<(int X, int Z), int> ground, int x, int z, int span, int here)
{
    for (var dz = -span; dz <= span; dz++)
        for (var dx = -span; dx <= span; dx++)
        {
            if (dx * dx + dz * dz > span * span) continue;
            if (!ground.TryGetValue((x + dx, z + dz), out var top)) return false;
            if (dx * dx + dz * dz <= 4 && Math.Abs(top - here) > 2) return false;
        }
    return true;
}

/// <summary>The rectangles scenery must stay out of: every room and spawn piece, grown by a clearance.</summary>
static List<Extent> KeepOut(MapIntent intent, int clearance)
{
    var boxes = new List<Extent>();
    void Add(Rect? rect)
    {
        if (rect is not { } r) return;
        boxes.Add(new Extent(r.MinX - clearance, r.MinZ - clearance, r.MaxX + clearance, r.MaxZ + clearance));
    }
    foreach (var wool in intent.Wools ?? []) { Add(wool.Piece); foreach (var room in wool.Room ?? []) Add(room); }
    foreach (var spawn in intent.Spawns ?? []) Add(spawn.Piece);
    return boxes;
}

/// <summary>A hash of a name that is the same in every process — what seeding needs and what
/// <see cref="string.GetHashCode()"/> deliberately is not.</summary>
static int StableHash(string text)
{
    var hash = 17;
    foreach (var character in text) hash = unchecked(hash * 31 + character);
    return hash & 0x7fffffff;
}

/// <summary>Whether a cell falls inside a rectangle.</summary>
static bool Inside(Extent rect, int x, int z) =>
    x >= rect.MinX && x <= rect.MaxX && z >= rect.MinZ && z <= rect.MaxZ;

/// <summary>Every image of a placed building's ground round the map's mirror, each grown by a margin. A
/// site-selection check that only ever asked about the rectangle an author's own click landed on never saw
/// the mirrored building standing on the other half of the map.</summary>
static IEnumerable<Extent> FannedAround(HouseProp house, DressingSymmetry symmetry, int margin)
{
    for (var k = 0; k < symmetry.Order; k++)
    {
        var corners = symmetry.ImageRing(house.Points, k);
        var (a, b) = (corners[0], corners[1]);
        yield return new Extent(Math.Min(a[0], b[0]) - margin, Math.Min(a[1], b[1]) - margin,
                                Math.Max(a[0], b[0]) + margin, Math.Max(a[1], b[1]) + margin);
    }
}

/// <summary>Every image of a candidate site round the map's mirror — what a site's own reflection lands on,
/// which a check against the raw anchor alone never sees. The dressing pass fans a prop's position exactly
/// this way (<see cref="PgmStudio.Minecraft.Dressing.DressingSymmetry.ImageCell"/>), so a site-selection
/// check that skips it is testing a different board than the one that gets built.</summary>
static IEnumerable<(int X, int Z)> FannedPoints(int x, int z, DressingSymmetry symmetry)
{
    for (var k = 0; k < symmetry.Order; k++) yield return symmetry.ImageCell(x, z, k);
}

/// <summary>Buildings scattered onto ground flat enough to stand them on. A building is stamped as a
/// rectangle and takes the lowest column under it, so unlike a tree it wants ground that is genuinely level:
/// dropped on a slope it either buries its own doorway or stands on a plinth of fill.</summary>
static List<PlacedProp> Settle(
    Dictionary<(int X, int Z), int> ground, MapIntent intent, VillageSpec spec, DressingSymmetry symmetry)
{
    var props = new List<PlacedProp>();
    if (ground.Count == 0) return props;

    var names = spec.Presets is { Count: > 0 } chosen
        ? chosen
        : [.. HousePresets.Village.Select(house => house.Name)];
    var keepOut = KeepOut(intent, spec.Clearance);
    var random = new Random(spec.Seed);
    // One representative half of the orbit, the same reason Forest() draws from it: a house's mirror is
    // fanned from its own site, not sampled again as if it were an independent building, so two different
    // houses cannot land on each other's reflection by chance.
    var cells = ground.Keys.Where(cell => symmetry.IsCanonical(cell.X, cell.Z)).ToList();
    if (cells.Count == 0) cells = ground.Keys.ToList();

    var placed = 0;
    for (var attempt = 0; attempt < spec.Count * 400 && placed < spec.Count; attempt++)
    {
        var name = names[random.Next(names.Count)];
        var preset = Preset(name);
        var (width, depth) = Footprint(preset.Width, preset.Depth);

        var (x, z) = cells[random.Next(cells.Count)];
        if (keepOut.Any(rect => Inside(rect, x, z))) continue;

        // Level under the whole rectangle, plus a block of skirt, or the doorway ends up in a bank.
        var here = ground[(x, z)];
        var half = Math.Max(width, depth) / 2 + 1;
        if (!Level(ground, x, z, half, here)) continue;
        // Both sides of the check have to be fanned: an earlier house's mirrored copy is as real a building
        // as the one drawn, and this candidate's own mirrored copy is as real a building as the one about to
        // be accepted — a site-against-site test that only looked at raw anchors never sees either.
        var candidateImages = FannedPoints(x, z, symmetry).ToList();
        if (props.OfType<HouseProp>().Any(house => FannedAround(house, symmetry, margin: 6)
                .Any(rect => candidateImages.Any(image => Inside(rect, image.X, image.Z)))))
            continue;

        double minX = x - width / 2.0, minZ = z - depth / 2.0;
        props.Add(new HouseProp
        {
            Points = [[Math.Round(minX), Math.Round(minZ)],
                      [Math.Round(minX) + width - 1, Math.Round(minZ) + depth - 1]],
            Front = (RoomEdge)random.Next(4),
            Style = preset.Style,
        });
        placed++;
    }
    return props;
}

/// <summary>Whether every column of a square is ground at the same height — what a stamped rectangle wants
/// under it, as against the two blocks of slack a tree is happy with.</summary>
static bool Level(Dictionary<(int X, int Z), int> ground, int x, int z, int half, int here)
{
    for (var dz = -half; dz <= half; dz++)
        for (var dx = -half; dx <= half; dx++)
            if (!ground.TryGetValue((x + dx, z + dz), out var top) || top != here) return false;
    return true;
}

/// <summary>One preset by name.</summary>
static HousePresets.House Preset(string name)
{
    var house = HousePresets.All.FirstOrDefault(h => h.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    if (house.Name is null)
        throw new ArgumentException(
            $"no house preset '{name}' — have: {string.Join(", ", HousePresets.All.Select(h => h.Name))}");
    return house;
}

/// <summary>A footprint held to 20 a side and to the 192 blocks a placed building is allowed. The two bind
/// differently — 20×20 is 400 — so a shape has to satisfy both.</summary>
static (int Width, int Depth) Footprint(int width, int depth)
{
    width = Math.Clamp(width, 3, 20);
    depth = Math.Clamp(depth, 3, 20);
    while (width * depth > 192) { if (width >= depth) width--; else depth--; }
    return (width, depth);
}

/// <summary>The buildings the spec placed by hand, each held to what a placed building is allowed to cover.</summary>
static List<PlacedProp> Placed(List<HouseSpec> specs)
{
    var props = new List<PlacedProp>();
    foreach (var spec in specs)
    {
        var preset = Preset(spec.Preset);
        var (width, depth) = Footprint(spec.Width ?? preset.Width, spec.Depth ?? preset.Depth);

        double minX = spec.X - width / 2.0, minZ = spec.Z - depth / 2.0;
        props.Add(new HouseProp
        {
            Points = [[Math.Round(minX), Math.Round(minZ)],
                      [Math.Round(minX) + width - 1, Math.Round(minZ) + depth - 1]],
            Front = Facing(spec.Front),
            Style = preset.Style,
        });
    }
    return props;
}

static RoomEdge? Facing(string? word) => word?.ToLowerInvariant() switch
{
    "negz" or "north" => RoomEdge.NegZ,
    "posz" or "south" => RoomEdge.PosZ,
    "negx" or "west"  => RoomEdge.NegX,
    "posx" or "east"  => RoomEdge.PosX,
    _ => null,
};

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

static Extent? IslandBounds(SketchLayout layout, SketchIsland island)
{
    Extent? box = null;
    foreach (var id in island.ShapeIds)
    {
        var shape = layout.Layout.Shapes.FirstOrDefault(s => s.Id == id);
        if (shape is null) continue;
        var bounds = Bounds(shape);
        if (bounds is not { } b) continue;
        box = box is { } have
            ? new Extent(Math.Min(have.MinX, b.MinX), Math.Min(have.MinZ, b.MinZ),
                      Math.Max(have.MaxX, b.MaxX), Math.Max(have.MaxZ, b.MaxZ))
            : b;
    }
    return box;
}

static Extent? Bounds(SketchShape shape)
{
    if (shape.Vertices is { Length: > 0 } vertices)
        return new Extent(vertices.Min(v => v[0]), vertices.Min(v => v[1]),
                       vertices.Max(v => v[0]), vertices.Max(v => v[1]));
    if (shape.CenterX is { } cx && shape.CenterZ is { } cz && shape.Radius is { } r)
        return new Extent(cx - r, cz - r, cx + r, cz + r);
    if (shape.MinX is { } minX && shape.MinZ is { } minZ && shape.MaxX is { } maxX && shape.MaxZ is { } maxZ)
        return new Extent(minX, minZ, maxX, maxZ);
    return null;
}
