using System.Text.Json;
using PatternMap;
using PgmStudio.Domain;
using PgmStudio.Export;
using PgmStudio.Minecraft;
using PgmStudio.Minecraft.Dressing;
using PgmStudio.Minecraft.Render;
using PgmStudio.Pgm;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Sketch;
using Dict = System.Collections.Generic.Dictionary<string, object?>;

// A world you can walk, showing every terrain-paint pattern and the house stamper — built the way a real map
// is built. The plateaus are sketch shapes, the paint is named themes a shape refers to, the trees are
// dressing props, and the objective is a MapIntent: SketchWorldBuilder makes the world from all of it and
// IntentGenerator writes the XML from what it resolved. Nothing here reaches past the export path, so the
// showcase can only contain what an author could draw — which is the point of it.

const int Surface = Shapes.Top - 1;                 // the grass course a building stands on
var (plateaus, themes) = Plateaus.Themed();

// ── the layout: what an author drew ──────────────────────────────────────────────────────────────────────

var layout = Shapes.Layout(plateaus, themes, mapTheme: plateaus[0].ThemeId);
layout.Dressing = JsonDocument.Parse(
    DressingJson.Serialize(new DressingDoc { Props = [.. Trees(), .. Buildings()] })).RootElement;

// The shell every wool room is stamped with, bound onto the layout the way the studio's room-style step
// binds one. A gabled oak house rather than the shipped bedrock lid — which a stored style could not have
// asked for until the roof form became a column of its own.
//
// The spawns bind the third answer: no building. A spawn here is a pad and the monuments around it on open
// plateau, which is what a map wants wherever the ground itself is the room.
var oak = HouseStyle.Wool with
{
    Wall = RoomPart.Of(new SolidMaterial(5, 1), 5),        // spruce planks
    Roof = new RoofStyle
    {
        Form = RoofForm.Gable,
        Pitch = 1,
        Overhang = 1,
        Hole = false,
        Body = new SolidMaterial(5, 1),
        Verge = new SolidMaterial(5, 5),                   // dark oak, so the roof reads an edge
    },
    Post = new SolidMaterial(17, 0),                       // oak log at the corners: a house is framed
    Foundation = new Foundation
    {
        Plate = RoomPart.Of(new SolidMaterial(5, 0)),      // oak planks
        Footing = new SolidMaterial(4),                    // cobble
    },
    Doorway = new Doorway { Height = 3 },
};
layout.RoomStyles = new SketchRoomStyles
{
    Wool = JsonDocument.Parse(HouseStyleJson.Serialize(oak)).RootElement,
    Spawn = SketchRoomStyles.Open,
};

// Half grown, half vanilla template, so the two forms stand side by side and the grown knobs vary plateau to
// plateau rather than repeating one tree twenty-five times.
static List<PlacedProp> Trees()
{
    var props = new List<PlacedProp>();
    var woods = new[] { "oak", "birch", "spruce", "jungle", "acacia", "dark oak" };
    for (var index = 0; index < 25; index++)
    {
        var (ox, oz) = Shapes.Origin(index);
        var spots = new[] { (-14, -10), (14, 10), (-13, 11), (15, -11) };
        for (var slot = 0; slot < spots.Length; slot++)
        {
            var (x, z) = (ox + spots[slot].Item1, oz + spots[slot].Item2);
            props.Add(slot % 2 == 0
                ? new TreeProp
                {
                    X = x, Z = z, Form = TreeForm.Grown, Wood = woods[(index + slot) % woods.Length],
                    Height = 11 + (index + slot) % 9, Stems = 1 + (index + slot) % 3,
                    Levels = 2 + (index / 5) % 2, Leader = 0.35 + 0.12 * ((index + slot) % 4),
                    Flow = 0.25 + 0.15 * (slot % 4), BranchAngle = 0.35 + 0.15 * ((index + slot) % 5),
                    LeafSize = 0.45 + 0.12 * (slot % 4),
                }
                : new TreeProp
                {
                    X = x, Z = z,
                    Species = DressingPalette.Species[(index + slot) % DressingPalette.Species.Count].Name,
                    Height = 9 + (index + slot) % 6,
                });
        }
    }
    return props;
}

// One building per house style, on the row of islands after the pattern plateaus. They go down as dressing
// props rather than as rooms: a prop's footprint is a rectangle someone dragged and it carries no objective,
// which is exactly what a house standing on a plateau to be looked at is. The rectangle is stored as two
// opposite corners, the way the canvas stores a drag.
static List<PlacedProp> Buildings()
{
    var props = new List<PlacedProp>();
    for (var slot = 0; slot < HousePresets.All.Count; slot++)
    {
        var house = HousePresets.All[slot];
        var (ox, oz) = Shapes.Origin(Plateaus.All.Count + slot);
        double minX = ox - house.Width / 2, minZ = oz - house.Depth / 2;
        props.Add(new HouseProp
        {
            Wings = [new AuthoredWing([[minX, minZ], [minX + house.Width - 1, minZ + house.Depth - 1]])],
            Front = RoomEdge.NegZ,
            Style = house.Style,
        });
    }
    return props;
}

// ── the intent: what the map is about ────────────────────────────────────────────────────────────────────

// Both ends of the stripe row. A wool room's piece sizes the shell that gets stamped over it and its entries
// are where the doors and the entrance redstone go; a spawn's piece does the same for its room. Asymmetric on
// purpose — nothing here is fanned, so each side is stated outright.
//
// A shell is its piece inset by one block on every side (WX1), so the piece's outer ring is bare plateau —
// which is the ring the bedrock floor fills and the entrance redstone runs along. Sizing a piece to the shell
// leaves no ring, no plateau and nowhere for the line.
//
// Every piece sits wholly on its plateau. A piece that overhangs writes its foundation into the void, where
// there is no surface to reach: StampFoundation reads the terrain's top and finds nothing, so it lays one
// block at y=0 instead of a column. And a spawn's door is the wall its yaw looks out of, so a piece hanging
// off the edge puts the only way out over the drop.
// A rectangle rasterizes as [min, max), so a plateau drawn to z=41 has its last solid row at 40. Reaching a
// piece to 41 is what put a foundation into the void: fifteen lone bedrock blocks at y=0, one per overhanging
// cell, because StampFoundation found no surface to fill up to.
static Rect Piece(int minX, int minZ, int width, int depth) =>
    new(minX, minZ, minX + width, minZ + depth);

/// <summary>A piece of the given size centred on a plateau, which is where one belongs when the plateau
/// carries nothing else: half its width and depth back from the centre the grid put it on.</summary>
static Rect Centred((int X, int Z) origin, int width, int depth) =>
    Piece(origin.X - width / 2, origin.Z - depth / 2, width, depth);

// A room and a spawn share no plateau. Two structures on one 34×26 board leave them abutting along a single
// seam, which reads as one building split rather than two things with ground between them — and a spawn is
// meant to be somewhere a player crosses to the wool from, not a room attached to it. So each takes an island
// of its own: the wool rooms keep the two plateaus they showed off, and the spawns take the plateau on the
// far side of a stretch of void.
//
// Blue's is the plateau directly north of its room. Red's is two rows south of its own rather than one,
// because the plateau one row south is the wedge, and a 13×13 piece on a wedge would hang over the point of
// it — every piece has to sit wholly on its plateau (see below).
var (westX, roomZ) = Shapes.Origin(10);
var eastX = Shapes.Origin(14).X;

var blueRoom = Centred((westX, roomZ), 15, 13);
var redRoom = Centred((eastX, roomZ), 15, 13);
var blueSpawnPiece = Centred(Shapes.Origin(5), 13, 13);      // solid grey stone, north of the blue room
var redSpawnPiece = Centred(Shapes.Origin(24), 13, 13);      // striped wall, south of the red room

var bluePad = new Pt(westX, Surface + 1, roomZ);
var redPad = new Pt(eastX, Surface + 1, roomZ);

// The door is the wall the yaw looks out of (0 = +z, 180 = −z). Each spawn faces the plateau its wool room
// stands on: blue looks south across the void to its room, red looks north to its own.
const double FaceNegZ = 180, FacePosZ = 0;

// The entrance row EntranceRow picks: the first block row of the piece on the seam side, which is the bare
// ring outside the shell. Each room is entered from the side its team's spawn lies on, so the door faces the
// way a player actually arrives.
static RedstoneLine RowOnMaxZ(Rect piece) =>
    new((int)piece.MinX, (int)piece.MaxZ - 1, (int)piece.MaxX - 1, (int)piece.MaxZ - 1);
static RedstoneLine RowOnMinZ(Rect piece) =>
    new((int)piece.MinX, (int)piece.MinZ, (int)piece.MaxX - 1, (int)piece.MinZ);

var intent = new MapIntent
{
    Teams =
    [
        new TeamDef { Id = "blue-team", Name = "Blue", Color = "blue" },
        new TeamDef { Id = "red-team", Name = "Red", Color = "red" },
    ],
    MaxPlayers = 8,
    Observer = new ObserverIntent { Point = new Pt(0, Surface + 60, roomZ) },
    Spawns =
    [
        new SpawnIntent
        {
            Team = "blue-team", Yaw = FacePosZ,
            Point = new Pt(westX, Surface + 1, Shapes.Origin(5).Z),
            Piece = blueSpawnPiece, Protection = [blueSpawnPiece],
        },
        new SpawnIntent
        {
            Team = "red-team", Yaw = FaceNegZ,
            Point = new Pt(eastX, Surface + 1, Shapes.Origin(24).Z),
            Piece = redSpawnPiece, Protection = [redSpawnPiece],
        },
    ],
    Wools =
    [
        new WoolIntent
        {
            Owner = "blue-team", Color = "red", Room = [blueRoom], Spawn = bluePad, Piece = blueRoom,
            Entries = [new Rect(blueRoom.MinX, blueRoom.MinZ, blueRoom.MaxX, blueRoom.MinZ)],
        },
        new WoolIntent
        {
            Owner = "red-team", Color = "blue", Room = [redRoom], Spawn = redPad, Piece = redRoom,
            Entries = [new Rect(redRoom.MinX, redRoom.MaxZ, redRoom.MaxX, redRoom.MaxZ)],
        },
    ],
    Structures = new StructureIntent
    {
        RoomFloors = [blueRoom, redRoom],
        RedstoneLines = [RowOnMinZ(blueRoom), RowOnMaxZ(redRoom)],
    },
};

// ── build it the way the export does ─────────────────────────────────────────────────────────────────────

var layoutJson = layout.ToJson();
var built = SketchWorldBuilder.Build(layoutJson, intent);
Console.WriteLine($"world built from {plateaus.Count} shapes; spawn {built.SpawnX},{built.SpawnY},{built.SpawnZ}");

var doc = new Dict();
IntentGenerator.Apply(doc, built.ResolvedIntent);
doc["name"] = "Pattern Test";
doc["version"] = "1.0.0";
doc["objective"] = "Walk the twenty-five plateaus and look at the walls, then the house row after them.";
var xml = XmlWriter.ToXml(Deserializer.FromDict(doc));

// ── write it ─────────────────────────────────────────────────────────────────────────────────────────────

var slug = args.Length > 0 ? args[0] : "pattern_test";
var outDir = args.Length > 1 ? args[1] : $"/media/sf_repos/CommunityMaps/ctw/{slug}";
Directory.CreateDirectory(outDir);

var patternRegionDir = Path.Combine(outDir, "region");
AnvilRegionWriter.Write(built.World, patternRegionDir);
WorldProvenanceFile.Write(built.Provenance, patternRegionDir);
LevelDatWriter.Write(outDir, slug, built.SpawnX, built.SpawnY, built.SpawnZ,
                     DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
File.WriteAllText(Path.Combine(outDir, "map.xml"), xml);

// A top-down of what was painted, in the map's **real block colours** rather than the category diagram: the
// question this tool exists to answer is what a pattern looks like, and a category render answers "ground"
// for every one of them. Off by default because it costs a full surface pass; `--render` turns it on.
if (args.Contains("--render"))
{
    var png = Path.Combine(outDir, "topdown.png");
    TopDownRender.Run(built.World, png, map: null, scale: 3, yMax: null, name: slug,
        colorMode: TopDownColorMode.Material, provenance: built.Provenance);
    Console.WriteLine($"wrote {png}");
}

Console.WriteLine($"\nwrote {outDir}\n");
for (var index = 0; index < plateaus.Count; index++)
{
    var (ox, oz) = Shapes.Origin(index);
    Console.WriteLine($"  x {ox,5} z {oz,5}   {plateaus[index].Name}");
}
