using System.Text.Json;
using PatternMap;
using PgmStudio.Api.Services;
using PgmStudio.Minecraft;
using PgmStudio.Minecraft.Dressing;
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
layout.Dressing = JsonDocument.Parse(DressingJson.Serialize(new DressingDoc { Props = Trees() })).RootElement;

// The shell every wool room is stamped with, bound onto the layout the way the studio's room-style step
// binds one. A gabled oak house rather than the shipped bedrock lid — which a stored style could not have
// asked for until the roof form became a column of its own.
//
// The spawns bind the third answer: no building. A spawn here is a pad and the monuments around it on open
// plateau, which is what a map wants wherever the ground itself is the room.
var oak = HouseStyle.Wool with
{
    Form = RoofForm.Gable,
    Pitch = 1,
    Overhang = 1,
    Wall = RoomPart.Of(new SolidMaterial(5, 1), 5),        // spruce planks
    Floor = RoomPart.Of(new SolidMaterial(5, 0)),          // oak planks
    Roof = new SolidMaterial(5, 1),
    Verge = new SolidMaterial(5, 5),                       // dark oak, so the roof reads an edge
    Post = new SolidMaterial(17, 0),                       // oak log at the corners: a house is framed
    Sill = new SolidMaterial(4),                           // cobble footing
    RoofHole = false,
    DoorHeight = 3,
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
const int PlateauMinZ = 15, PlateauEndZ = 41;      // end is exclusive — the first void row

static Rect Piece(int minX, int minZ, int width, int depth) =>
    new(minX, minZ, minX + width, minZ + depth);

var (westX, endZ) = Shapes.Origin(10);
var eastX = Shapes.Origin(14).X;

// Wool room at one end of each plateau, spawn at the other, a row of grass between them.
var blueRoom = Piece(westX - 7, PlateauMinZ, 15, 13);              // z 15..27
var blueSpawnPiece = Piece(westX - 6, PlateauEndZ - 13, 13, 13);   // z 28..40
var redSpawnPiece = Piece(eastX - 6, PlateauMinZ, 13, 13);         // z 15..27
var redRoom = Piece(eastX - 7, PlateauEndZ - 13, 15, 13);          // z 28..40

var bluePad = new Pt(westX, Surface + 1, endZ - 7);
var redPad = new Pt(eastX, Surface + 1, endZ + 7);

// The door is the wall the yaw looks out of (0 = +z, 180 = −z), so each spawn faces along its own plateau
// rather than over its edge.
const double FaceNegZ = 180, FacePosZ = 0;

// The entrance row EntranceRow picks: the first block row of the piece on the seam side, which is the bare
// ring outside the shell. Each room is entered from the side its plateau's spawn stands on.
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
    Observer = new ObserverIntent { Point = new Pt(0, Surface + 60, endZ) },
    Spawns =
    [
        new SpawnIntent
        {
            Team = "blue-team", Yaw = FaceNegZ, Point = new Pt(westX, Surface + 1, endZ + 7),
            Piece = blueSpawnPiece, Protection = [blueSpawnPiece],
        },
        new SpawnIntent
        {
            Team = "red-team", Yaw = FacePosZ, Point = new Pt(eastX, Surface + 1, endZ - 7),
            Piece = redSpawnPiece, Protection = [redSpawnPiece],
        },
    ],
    Wools =
    [
        new WoolIntent
        {
            Owner = "blue-team", Color = "red", Room = [blueRoom], Spawn = bluePad, Piece = blueRoom,
            Entries = [new Rect(blueRoom.MinX, blueRoom.MaxZ, blueRoom.MaxX, blueRoom.MaxZ)],
        },
        new WoolIntent
        {
            Owner = "red-team", Color = "blue", Room = [redRoom], Spawn = redPad, Piece = redRoom,
            Entries = [new Rect(redRoom.MinX, redRoom.MinZ, redRoom.MaxX, redRoom.MinZ)],
        },
    ],
    Structures = new StructureIntent
    {
        RoomFloors = [blueRoom, redRoom],
        RedstoneLines = [RowOnMaxZ(blueRoom), RowOnMinZ(redRoom)],
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
doc["objective"] = "Walk the twenty-five plateaus and look at the walls.";
var xml = XmlWriter.ToXml(Deserializer.FromDict(doc));

// ── write it ─────────────────────────────────────────────────────────────────────────────────────────────

var slug = args.Length > 0 ? args[0] : "pattern_test";
var outDir = args.Length > 1 ? args[1] : $"/media/sf_repos/CommunityMaps/ctw/{slug}";
Directory.CreateDirectory(outDir);

AnvilRegionWriter.Write(built.World, Path.Combine(outDir, "region"));
LevelDatWriter.Write(outDir, slug, built.SpawnX, built.SpawnY, built.SpawnZ,
                     DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
File.WriteAllText(Path.Combine(outDir, "map.xml"), xml);

Console.WriteLine($"\nwrote {outDir}\n");
for (var index = 0; index < plateaus.Count; index++)
{
    var (ox, oz) = Shapes.Origin(index);
    Console.WriteLine($"  x {ox,5} z {oz,5}   {plateaus[index].Name}");
}
