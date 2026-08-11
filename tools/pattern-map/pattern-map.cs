#:project ../../src/PgmStudio.Minecraft/PgmStudio.Minecraft.csproj
#:property JsonSerializerIsReflectionEnabledByDefault=true
// A world you can walk, showing every terrain-paint pattern and the house stamper, so both can be looked at
// rather than read about. Twenty-five plateaus in a five-by-five grid, each its own landmass — so each gets
// its own traced perimeter — and each themed with one kind.
using PgmStudio.Domain;
using PgmStudio.Minecraft;
using PgmStudio.Minecraft.Dressing;

const int Top = 26;              // solid to y=25, so every plateau carries a ~24-block wall
const int Surface = Top - 1;     // the grass course a house or a monument stands on
const int Pitch = 56;            // grid spacing, wide enough that no two plateaus touch
const int Cols = 5;

static (int X, int Z) Origin(int index) => ((index % Cols - 2) * Pitch, (index / Cols - 2) * Pitch + Pitch / 2);

// ── shapes ───────────────────────────────────────────────────────────────────────────────────────────────

static IEnumerable<(int X, int Z)> Rect(int width, int depth)
{
    for (var x = -width / 2; x <= width / 2; x++)
        for (var z = -depth / 2; z <= depth / 2; z++) yield return (x, z);
}

static IEnumerable<(int X, int Z)> Disc(int radius)
{
    for (var x = -radius; x <= radius; x++)
        for (var z = -radius; z <= radius; z++)
            if (x * x + z * z <= radius * radius) yield return (x, z);
}

/// A regular octagon: every vertex turns 45 degrees, which is exactly a wall frame's default threshold.
static IEnumerable<(int X, int Z)> Octagon(int radius)
{
    var cut = (int)(radius * 0.62);
    for (var x = -radius; x <= radius; x++)
        for (var z = -radius; z <= radius; z++)
            if (Math.Abs(x) + Math.Abs(z) <= radius + cut) yield return (x, z);
}

/// A cross: four outward right angles and four inward ones, so a frame has to ink both kinds.
static IEnumerable<(int X, int Z)> Cross(int arm, int half)
{
    for (var x = -arm; x <= arm; x++)
        for (var z = -arm; z <= arm; z++)
            if (Math.Abs(x) <= half || Math.Abs(z) <= half) yield return (x, z);
}

/// A wedge whose long edge climbs one in four — a shallow bend a frame must not call a corner.
static IEnumerable<(int X, int Z)> Wedge(int width, int depth)
{
    for (var z = -depth / 2; z <= depth / 2; z++)
    {
        var inset = (z + depth / 2) / 4;
        for (var x = -width / 2 + inset; x <= width / 2; x++) yield return (x, z);
    }
}

// ── palettes, taken from the picker's own tables ─────────────────────────────────────────────────────────

// Grass, podzol and mycelium carry a distinct top over dirt sides, so they read only where exactly one course
// of them is seen from above. Stacked in a layer or turned side-on in a wall they repeat as banded dirt. They
// sit inside the verdant, loam and mauve families, so a pattern filled from a whole family would scatter them
// down a riser without anything complaining — they come out here, once, for every caller.
static bool TopFaced(int id, int data) => (id, data) is (2, 0) or (3, 2) or (110, 0);

static IReadOnlyList<TerrainMaterial> Family(string name, int take = 99)
{
    var family = TerrainPalette.Families.First(entry => entry.Name == name);
    return [.. family.Blocks.Where(block => !TopFaced(block.Id, block.Data)).Take(take)
                            .Select(block => (TerrainMaterial)new SolidMaterial(block.Id, block.Data))];
}

static TerrainMaterial One(string family, int index = 0) => Family(family)[index];

static IReadOnlyList<WallStripe> Stripes(string family, int width, int take = 3) =>
    [.. Family(family, take).Select(material => new WallStripe(material, width))];

// A shade out of one of the three sixteen-shade colour rows. These are not tone families: a family is a
// ground an author reaches for, a shade row is one block in sixteen colours — which is what a pattern wants
// when its two materials must read apart rather than read as one ground.
const int Clay = 159, Wool = 35, Glass = 95;
static TerrainMaterial Shade(int block, int colour) => new SolidMaterial(block, colour);

// The area patterns, each built once and used for both the ground and the riser. A rise makes the field
// three-dimensional, so a wall carries the same fabric its surface does instead of streaking vertically.
static TerrainMaterial Voronoi() =>
    new VoronoiMaterial(1, 9, [.. Family("cobble").Select(material => new VoronoiBand(material, 1))], Rise: 8);
static TerrainMaterial Cells() => new CellMaterial(2, 10, 55, 4, Family("sand"), Rise: 8);
static TerrainMaterial Noise() => new NoiseMaterial(3, 14, 3, Family("verdant"), Rise: 8);
static TerrainMaterial Turbulence() => new TurbulenceMaterial(4, 14, 3, Family("rust"), Rise: 8);
static TerrainMaterial Electric() => new ElectricMaterial(5, 16, 3, Family("grey stone"), Rise: 8);

// Ink and panel are meant to read apart, so a frame takes two shades of one colour row.
static TerrainMaterial Frame(int angle) =>
    new WallFrameMaterial(Shade(Clay, 15), Shade(Clay, 0), angle, Thickness: 1);

// A voronoi whose boundary band is one block — so it draws a clean grid of lines — and whose middle is handed
// to whatever is passed in. The grid stays legible because a single material cannot itself vary.
static TerrainMaterial RimAround(TerrainMaterial inside) =>
    new VoronoiMaterial(7, 13, [new VoronoiBand(Shade(Clay, 15), 1), new VoronoiBand(inside, 1)], Rise: 8);

var grass = new LayeredMaterial([
    new MaterialLayer(new SolidMaterial(2, 0), 1),       // one course of grass, and only one
    new MaterialLayer(new SolidMaterial(3, 0), 2)]);

// ── the twenty-five plateaus ─────────────────────────────────────────────────────────────────────────────

var shapes = new (string Name, IEnumerable<(int X, int Z)> Cells,
                  TerrainMaterial Wall, TerrainMaterial? Surface)[]
{
    // area patterns, on the ground and down the riser both
    ("voronoi — cobble family",          Rect(34, 26), Voronoi(), Voronoi()),
    ("cell — sand family",               Rect(34, 26), Cells(), Cells()),
    ("noise — verdant family",           Rect(34, 26), Noise(), Noise()),
    ("turbulence — rust family",         Rect(34, 26), Turbulence(), Turbulence()),
    ("electric — grey stone family",     Rect(34, 26), Electric(), Electric()),

    // the plain kinds, and the board
    ("solid — grey stone",               Rect(34, 26), One("grey stone"), null),
    ("layered — verdant/loam/stone",     Rect(34, 26),
        new LayeredMaterial([new MaterialLayer(One("verdant"), 2), new MaterialLayer(One("loam"), 3),
                             new MaterialLayer(One("grey stone"), 1)]), null),
    ("team tint — clay by team",         Rect(34, 26), new TeamTintedMaterial(Clay, One("ash")), null),
    ("checkerboard 1 — clay black/white", Rect(34, 26),
        new CheckerMaterial(1, Shade(Clay, 15), Shade(Clay, 0)),
        new CheckerMaterial(1, Shade(Clay, 15), Shade(Clay, 0))),
    ("checkerboard 3 — wool cyan/white", Rect(34, 26),
        new CheckerMaterial(3, Shade(Wool, 9), Shade(Wool, 0)),
        new CheckerMaterial(3, Shade(Wool, 9), Shade(Wool, 0))),

    // stripes along the wall, upright and sheared
    ("wall stripes — azure family",      Rect(34, 26), new WallRunMaterial(Stripes("azure", 3)), null),
    ("diagonal slope 1 — clay yellow/black", Rect(34, 26),
        new WallDiagonalMaterial([new WallStripe(Shade(Clay, 4), 2), new WallStripe(Shade(Clay, 15), 2)], 1), null),
    ("diagonal slope 2 — wool red/white", Rect(34, 26),
        new WallDiagonalMaterial([new WallStripe(Shade(Wool, 14), 3), new WallStripe(Shade(Wool, 0), 3)], 2), null),
    ("diagonal slope -1 — glass magenta/lime", Rect(34, 26),
        new WallDiagonalMaterial([new WallStripe(Shade(Glass, 2), 2), new WallStripe(Shade(Glass, 5), 2)], -1), null),
    ("diagonal slope 3 — loam family",   Rect(34, 26), new WallDiagonalMaterial(Stripes("loam", 3, 4), 3), null),

    // the frame, and what decides a corner
    ("frame 45 — rectangle, 4 corners",  Rect(34, 26), Frame(45), null),
    ("frame 45 — disc, no corner",       Disc(14),     Frame(45), null),
    ("frame 45 — octagon, at threshold", Octagon(15),  Frame(45), null),
    ("frame 45 — cross, in and out",     Cross(15, 5), Frame(45), null),
    ("frame 80 — only the sharpest",     Wedge(32, 30), Frame(80), null),

    // patterns stacked inside patterns: every band, stop, stripe and square is itself a material
    ("voronoi rim, noise inside",        Rect(34, 26), RimAround(Noise()), RimAround(Noise())),
    ("voronoi rim, cells inside",        Rect(34, 26), RimAround(Cells()), RimAround(Cells())),
    ("frame ink, turbulence panel",      Rect(34, 26),
        new WallFrameMaterial(Shade(Clay, 15), Turbulence(), 45, Thickness: 1), null),
    ("checker: plain against voronoi",   Rect(34, 26),
        new CheckerMaterial(6, Shade(Clay, 0), Voronoi()), new CheckerMaterial(6, Shade(Clay, 0), Voronoi())),
    ("stripes, each a different kind",   Rect(34, 26),
        new WallRunMaterial([new WallStripe(Shade(Clay, 15), 2), new WallStripe(Noise(), 6),
                             new WallStripe(Shade(Clay, 0), 2), new WallStripe(Turbulence(), 6)]), null),
};

// ── build the terrain ────────────────────────────────────────────────────────────────────────────────────

var columns = new List<(int X, int Z, int YFloor, int YTop)>();
var owner = new Dictionary<(int X, int Z), int>();
for (var index = 0; index < shapes.Length; index++)
{
    var (ox, oz) = Origin(index);
    foreach (var (x, z) in shapes[index].Cells)
    {
        var cell = (ox + x, oz + z);
        if (!owner.TryAdd(cell, index)) continue;
        columns.Add((cell.Item1, cell.Item2, 1, Top));
    }
}

var terrain = SketchTerrainBuilder.Build(columns);
Console.WriteLine($"terrain: {columns.Count} columns over {shapes.Length} plateaus");

var themes = shapes.Select(shape => new TerrainTheme
{
    Rim = new TopBand(One("ash", 2), 1, Enabled: true),
    Surface = new TopBand(shape.Surface ?? grass, 3, Enabled: true),
    Wall = shape.Wall,
    WallEnabled = true,
    Fill = One("grey stone"),
}).ToArray();

TerrainTheme ThemeAt(int x, int z) => themes[owner.TryGetValue((x, z), out var index) ? index : 0];

// The team-tint plateau needs an owner per cell to have anything to tint: split it down the middle.
var tinted = Array.FindIndex(shapes, shape => shape.Name.StartsWith("team tint"));
var tintX = Origin(tinted).X;
int TeamAt(int x, int z) =>
    owner.TryGetValue((x, z), out var index) && index == tinted ? (x < tintX ? 11 : 14) : -1;

TerrainPainter.Paint(terrain.World, terrain.SurfaceTop, ThemeAt, TeamAt);
Console.WriteLine("painted");

// ── dress it, both kinds of tree ─────────────────────────────────────────────────────────────────────────

var props = new List<PlacedProp>();
var woods = new[] { "oak", "birch", "spruce", "jungle", "acacia", "dark oak" };
for (var index = 0; index < shapes.Length; index++)
{
    var (ox, oz) = Origin(index);
    var spots = new[] { (-14, -10), (14, 10), (-13, 11), (15, -11) };
    for (var slot = 0; slot < spots.Length; slot++)
    {
        var cell = (ox + spots[slot].Item1, oz + spots[slot].Item2);
        if (!owner.TryGetValue(cell, out var at) || at != index) continue;

        // Half grown, half vanilla template, so the two forms stand side by side and the grown knobs vary
        // plateau to plateau rather than repeating one tree twenty-five times.
        if (slot % 2 == 0)
            props.Add(new TreeProp
            {
                X = cell.Item1, Z = cell.Item2,
                Form = TreeForm.Grown,
                Wood = woods[(index + slot) % woods.Length],
                Height = 11 + (index + slot) % 9,
                Stems = 1 + (index + slot) % 3,
                Levels = 2 + (index / 5) % 2,
                Leader = 0.35 + 0.12 * ((index + slot) % 4),
                Flow = 0.25 + 0.15 * (slot % 4),
                BranchAngle = 0.35 + 0.15 * ((index + slot) % 5),
                LeafSize = 0.45 + 0.12 * (slot % 4),
            });
        else
            props.Add(new TreeProp
            {
                X = cell.Item1, Z = cell.Item2,
                Species = DressingPalette.Species[(index + slot) % DressingPalette.Species.Count].Name,
                Height = 9 + (index + slot) % 6,
            });
    }
}

var tally = Decorator.Decorate(terrain.World, new DressingContext(terrain.SurfaceTop, props));
Console.WriteLine($"dressed: {tally.Trees} trees " +
                  $"({props.Count(prop => prop is TreeProp { Form: TreeForm.Grown })} grown)");

// ── the objective, built rather than only declared ───────────────────────────────────────────────────────

// Both ends of the stripe row: each team gets a wool-room house and a monument on its own plateau, mirrored,
// so the two are a short walk apart and both are visible from the spawn between them.
var (westX, endZ) = Origin(10);
var eastX = Origin(14).X;

// A 45 degree slope, so the roof is whole blocks: a slab there fills half its cube and the slope leaks.
var oak = new HouseStyle
{
    Sill = new SolidMaterial(4), Post = new SolidMaterial(17, 0),
    Wall = RoomPart.Of(new SolidMaterial(5, 1), 5),
    Floor = RoomPart.Of(new SolidMaterial(5, 0)),
    Roof = new SolidMaterial(5, 1), Verge = new SolidMaterial(5, 5),
    Overhang = 1, Pitch = 1, DoorWidth = 2, DoorHeight = 3,
};

// The wool is the pad the room stamper has always laid: a flat square set into the floor rather than standing
// on it, three blocks a side, which is the size a room takes wherever it fits.
void WoolRoom(int houseX, int houseZ, int woolColour)
{
    HouseStamper.Stamp(terrain.World, houseX, houseZ, 11, 9, Surface, oak, woolColour);
    for (var x = houseX + 4; x <= houseX + 6; x++)
        for (var z = houseZ + 3; z <= houseZ + 5; z++)
            terrain.World.SetBlock(x, Surface, z, Blocks.Wool, woolColour);
}

var (blueHouseX, blueHouseZ) = (westX - 5, endZ - 11);
var (redHouseX, redHouseZ) = (eastX - 5, endZ + 3);
WoolRoom(blueHouseX, blueHouseZ, 14);       // blue team captures red wool
WoolRoom(redHouseX, redHouseZ, 11);         // and red team blue

// The shipped monument — pedestal on the floor, placement cell, wool-coloured cap and its sign. The wall it
// backs onto is chosen so the sign turns toward the spawn each team walks in from.
var blueMonument = MonumentStamper.Place(terrain.World, westX, Surface, endZ + 8, "red", RoomEdge.PosZ);
var redMonument = MonumentStamper.Place(terrain.World, eastX, Surface, endZ - 8, "blue", RoomEdge.NegZ);

// ── write it ─────────────────────────────────────────────────────────────────────────────────────────────

var slug = args.Length > 0 ? args[0] : "pattern_test";
var outDir = args.Length > 1 ? args[1] : $"/media/sf_repos/CommunityMaps/ctw/{slug}";
Directory.CreateDirectory(outDir);

AnvilRegionWriter.Write(terrain.World, Path.Combine(outDir, "region"));
LevelDatWriter.Write(outDir, slug, 0, Top, endZ, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

// A wools objective rather than a score one: the studio's parser refuses a map whose objective module it
// cannot read, and score (TDM) is one of those. Every coordinate below comes off what was actually stamped.
File.WriteAllText(Path.Combine(outDir, "map.xml"), $"""
<?xml version="1.0"?>
<map proto="1.4.2">
    <name>Pattern Test</name>
    <version>1.0.0</version>
    <objective>Walk the twenty-five plateaus and look at the walls.</objective>
    <authors>
        <author uuid="fe3608b7-d105-4029-8800-34b3147065b6"/>
    </authors>
    <teams>
        <team id="blue-team" color="blue" max="8">Blue</team>
        <team id="red-team" color="red" max="8">Red</team>
    </teams>
    <kits>
        <kit id="spawn-kit">
            <clear/>
            <item slot="0" material="diamond pickaxe" unbreakable="true"/>
            <game-mode>creative</game-mode>
        </kit>
    </kits>
    <spawns>
        <spawn team="blue-team" kit="spawn-kit" region="blue-spawn"/>
        <spawn team="red-team" kit="spawn-kit" yaw="180" region="red-spawn"/>
        <default region="obs-spawn"/>
    </spawns>
    <regions>
        <point id="blue-spawn">{westX},{Surface + 1},{endZ}</point>
        <point id="red-spawn">{eastX},{Surface + 1},{endZ}</point>
        <point id="obs-spawn">0,{Surface + 60},{endZ}</point>
    </regions>
    <wools craftable="false">
        <wool team="blue-team" color="red" location="{blueHouseX + 5},{Surface + 1},{blueHouseZ + 4}">
            <monument>
                <block>{blueMonument.X},{blueMonument.Y},{blueMonument.Z}</block>
            </monument>
        </wool>
        <wool team="red-team" color="blue" location="{redHouseX + 5},{Surface + 1},{redHouseZ + 4}">
            <monument>
                <block>{redMonument.X},{redMonument.Y},{redMonument.Z}</block>
            </monument>
        </wool>
    </wools>
</map>
""");

Console.WriteLine($"\nwrote {outDir}\n");
for (var index = 0; index < shapes.Length; index++)
{
    var (ox, oz) = Origin(index);
    Console.WriteLine($"  x {ox,5} z {oz,5}   {shapes[index].Name}");
}
