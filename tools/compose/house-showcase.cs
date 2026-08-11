#:project ../../src/PgmStudio.Minecraft/PgmStudio.Minecraft.csproj
// house-showcase: the house-generation explainer — one designed, self-contained HTML page covering the four
// things a house style decides beyond its materials: which roof it wears, how its floor is divided, where its
// porch comes from, and what its windows are made of. Every figure is stamped by the real HouseStamper and
// read back out of the VoxelWorld, so a picture cannot promise a building the export would not put down.
// Run from the repo root: dotnet run tools/compose/house-showcase.cs  →  tools/compose/out/house-showcase.html
using System.Globalization;
using System.Text;
using PgmStudio.Domain;
using PgmStudio.Minecraft;

const int FloorY = 64;

// ── the worlds ────────────────────────────────────────────────────────────────────────────────────────
// One stamp per figure, over a slab of ground so the floor has something to sit on and the sill something to
// ring. The colour is the red a room's tint reads, so a team-tinted course shows as one.
VoxelWorld Build(int width, int depth, HouseStyle style)
{
    var world = new VoxelWorld();
    for (var x = -3; x < width + 3; x++)
        for (var z = -3; z < depth + 3; z++)
            for (var y = FloorY - 3; y < FloorY; y++)
                world.SetBlock(x, y, z, y == FloorY - 1 ? Blocks.Grass : Blocks.Dirt);
    HouseStamper.Stamp(world, 0, 0, width, depth, FloorY, style, color: 14);
    return world;
}

// ── the isometric render ──────────────────────────────────────────────────────────────────────────────
// A block world seen from above one corner. The camera stands off the building's −z side, which is the wall a
// house fronts on — its door, its porch and most of its windows are there, and a view of the back of a house
// is a view of a box. Reading z from the far side and walking it toward the camera is what turns the picture
// round; nothing else in the projection changes.
//
// The view direction is then (−1, −1, +1), so a cell is nearer the camera the larger x + y − z is, which is
// the paint order — and each of the three faces that can possibly face the camera is drawn only when the
// neighbour it points at is air. Face culling rather than block culling is what keeps the page small: a wall
// shows one face per block rather than three, and a building's interior draws nothing at all.
//
// Consecutive faces of one colour then coalesce into a single <path>, because a house is a few materials
// repeated thousands of times and a per-face fill attribute is most of the bytes. Consecutive rather than
// all-of-one-colour: a nearer face still has to paint over a farther one — an eave over the wall behind it —
// so the depth order is what the emission follows and the colour run is only how far it can be carried.
// Scale is even and every projected coordinate a multiple of half a tile, so the whole picture is integer
// arithmetic and no coordinate carries a decimal point.
string Iso(VoxelWorld world, int minX, int minZ, int maxX, int maxZ, int minY, int maxY, int scale = 10)
{
    int halfWide = scale, halfTall = scale / 2, side = scale;   // a 2:1 tile, one cell tall

    int ScreenX(int x, int z) => (x - z) * halfWide;
    int ScreenY(int x, int y, int z) => (x + z) * halfTall - y * side;

    // The drawn extent, so the viewBox fits the building rather than the block box it was read out of.
    int left = (minX - maxZ) * halfWide - halfWide, right = (maxX - minZ) * halfWide + halfWide;
    int top = ScreenY(minX, maxY, minZ), bottom = ScreenY(maxX, minY, maxZ) + 2 * halfTall + side;

    var faces = new List<(int Depth, string Color, string Path)>();
    for (var x = minX; x <= maxX; x++)
        for (var z = minZ; z <= maxZ; z++)
            for (var y = minY; y <= maxY; y++)
            {
                var (id, data) = Block(x, y, z);
                if (id == Blocks.Air) continue;
                var rgb = BlockPalette.Color(id, data);
                int px = ScreenX(x, z) - left, py = ScreenY(x, y, z) - top;

                // Three faces, three shades: the top full, the +z face three-quarters, the +x face half.
                // Depth in a flat picture has to come from the light, and a single fill turns a roof into a
                // silhouette.
                if (!Opaque(x, y + 1, z))
                    Face(x + y + z, rgb, 1.0, px, py, px + halfWide, py + halfTall, px, py + 2 * halfTall, px - halfWide, py + halfTall);
                if (!Opaque(x, y, z + 1))
                    Face(x + y + z, rgb, 0.74, px - halfWide, py + halfTall, px, py + 2 * halfTall, px, py + 2 * halfTall + side, px - halfWide, py + halfTall + side);
                if (!Opaque(x + 1, y, z))
                    Face(x + y + z, rgb, 0.52, px, py + 2 * halfTall, px + halfWide, py + halfTall, px + halfWide, py + halfTall + side, px, py + 2 * halfTall + side);
            }

    // Nearer last, and stably, so the three faces of one block keep the order they were built in.
    var ordered = faces.OrderBy(face => face.Depth).ToList();

    var svg = new StringBuilder();
    int width = right - left, height = bottom - top;
    svg.Append($"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 {width} {height}' ")
       .Append($"width='{width}' height='{height}' role='img'>");
    for (var at = 0; at < ordered.Count;)
    {
        var run = new StringBuilder(ordered[at].Path);
        var color = ordered[at].Color;
        var next = at + 1;
        while (next < ordered.Count && ordered[next].Color == color) run.Append(ordered[next++].Path);
        svg.Append($"<path fill='{color}' d='{run}'/>");
        at = next;
    }
    svg.Append("</svg>");
    return svg.ToString();

    // The one mirror: a drawn cell at z reads the world at its reflection, so the −z wall is the one facing
    // out of the page.
    (int Id, int Data) Block(int x, int y, int z) => world.GetBlock(x, y, minZ + maxZ - z);

    bool Opaque(int x, int y, int z) => Block(x, y, z).Id != Blocks.Air;

    void Face(int depth, BlockPalette.Rgb rgb, double shade, params int[] points)
        => faces.Add((depth, Shade(rgb, shade),
            $"M{points[0]} {points[1]}L{points[2]} {points[3]}L{points[4]} {points[5]}L{points[6]} {points[7]}Z"));
}

string Shade(BlockPalette.Rgb rgb, double factor) => Invariant(
    $"#{(int)Math.Round(rgb.R * factor):x2}{(int)Math.Round(rgb.G * factor):x2}{(int)Math.Round(rgb.B * factor):x2}");

// ── the flat renders ──────────────────────────────────────────────────────────────────────────────────
// A grid of coloured cells, one <rect> per run of one colour along a row. The two orthogonal reads an
// isometric cannot give: what the roof does, seen from above, and what the courses are, seen from the side.
string Raster(int columns, int rows, int cell, Func<int, int, string?> colorAt)
{
    var svg = new StringBuilder();
    svg.Append($"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 {columns * cell} {rows * cell}' ")
       .Append($"width='{columns * cell}' height='{rows * cell}' shape-rendering='crispEdges' role='img'>");
    for (var row = 0; row < rows; row++)
    {
        var from = 0;
        string? color = null;
        for (var column = 0; column <= columns; column++)
        {
            var next = column < columns ? colorAt(column, row) : null;
            if (column < columns && next == color) continue;
            if (color is not null)
                svg.Append($"<rect x='{from * cell}' y='{row * cell}' width='{(column - from) * cell}' ")
                   .Append($"height='{cell}' fill='{color}'/>");
            (from, color) = (column, next);
        }
    }
    svg.Append("</svg>");
    return svg.ToString();
}

/// The highest block of each column — what the roof does, its hole and its eave, and nothing else.
string Plan(VoxelWorld world, int minX, int minZ, int maxX, int maxZ, int top, int cell = 9) =>
    Raster(maxX - minX + 1, maxZ - minZ + 1, cell, (column, row) =>
    {
        for (var y = top; y >= FloorY - 1; y--)
        {
            var (id, data) = world.GetBlock(minX + column, y, minZ + row);
            if (id != Blocks.Air) return BlockPalette.Hex(id, data);
        }
        return null;
    });

/// The floor's own course seen from above — the one plan a roof would otherwise hide.
string FloorPlan(VoxelWorld world, int minX, int minZ, int maxX, int maxZ, int cell = 9) =>
    Raster(maxX - minX + 1, maxZ - minZ + 1, cell, (column, row) =>
    {
        var (id, data) = world.GetBlock(minX + column, FloorY, minZ + row);
        return id == Blocks.Air ? null : BlockPalette.Hex(id, data);
    });

/// The building projected onto its front, nearest block first, shaded by how far back it stands — so a
/// doorway reads as an opening rather than as a hole in the picture.
string Section(VoxelWorld world, int minX, int minZ, int maxX, int maxZ, int top, int cell = 9)
{
    var view = BlockSideView.Project(world, minX, maxX, minZ, maxZ, FloorY - 2, top);
    return Raster(view.Columns, top - (FloorY - 2) + 1, cell, (column, row) =>
        view.At(view.FromX + column, top - row) is { } block
            ? Shade(BlockPalette.Color(block.Id, block.Data), 1.0 - 0.45 * block.Depth)
            : null);
}

// ── the elevation ─────────────────────────────────────────────────────────────────────────────────────
// One wall drawn at the scale of the pieces in it, and the only render on the page that draws a block as its
// own shape rather than as a cube. It has to: a stair lattice's whole trick is the quarter each of its four
// stairs is missing, and a renderer that draws every block as a cube shows that window as a solid 2×2 patch —
// exactly the picture the window is designed not to be. The shapes are read out of the block's metadata, so
// the drawing follows the world rather than a second opinion about it.
string Elevation(VoxelWorld world, int fromX, int toX, int fromY, int toY, int z, int cell = 22)
{
    var svg = new StringBuilder();
    int columns = toX - fromX + 1, rows = toY - fromY + 1;
    svg.Append($"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 {columns * cell} {rows * cell}' ")
       .Append($"width='{columns * cell}' height='{rows * cell}' shape-rendering='crispEdges' role='img'>");

    for (var y = toY; y >= fromY; y--)
        for (var x = fromX; x <= toX; x++)
        {
            var (id, data) = world.GetBlock(x, y, z);
            if (id == Blocks.Air) continue;
            int column = x - fromX, row = toY - y;
            var fill = BlockPalette.Hex(id, data);
            foreach (var (left, top, wide, tall, glass) in Pieces(id, data))
                svg.Append(Invariant(
                    $"<rect x='{(column + left) * cell:0.##}' y='{(row + top) * cell:0.##}' ") +
                    Invariant($"width='{wide * cell:0.##}' height='{tall * cell:0.##}' fill='{fill}'") +
                    (glass ? " opacity='0.55'/>" : "/>"));
        }

    svg.Append("</svg>");
    return svg.ToString();

    // What of its cube a block actually fills, in unit squares measured from the cell's top-left. A slab
    // fills one half, a stair a half plus a quarter on the side it climbs toward, a pane a sheet you see
    // through, and everything else the whole cube.
    static IEnumerable<(double Left, double Top, double Wide, double Tall, bool Glass)> Pieces(int id, int data)
    {
        const int oakStairs = 53, cobbleStairs = 67;
        int[] stairs = [oakStairs, cobbleStairs, 108, 109, 114, 128, 134, 135, 136, 163, 164, 180];
        int[] slabs = [44, 126, 182];

        if (stairs.Contains(id))
        {
            var upsideDown = (data & Blocks.StairUpsideDown) != 0;
            // The raised half is on the side the stair climbs toward: east is +x, and x runs rightward here.
            var rightward = (data & 3) is Blocks.StairEast or Blocks.StairSouth;
            yield return (0, upsideDown ? 0 : 0.5, 1, 0.5, false);          // the full half
            yield return (rightward ? 0.5 : 0, upsideDown ? 0.5 : 0, 0.5, 0.5, false);   // the step
            yield break;
        }
        if (slabs.Contains(id))
        {
            yield return (0, (data & Blocks.SlabUpperHalf) != 0 ? 0 : 0.5, 1, 0.5, false);
            yield break;
        }
        if (id is Blocks.GlassPane or Blocks.StainedGlassPane or Blocks.Glass or Blocks.StainedGlass)
        {
            yield return (0, 0, 1, 1, true);
            yield break;
        }
        yield return (0, 0, 1, 1, false);
    }
}

static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

// ── the styles the page is about ──────────────────────────────────────────────────────────────────────
// One base house every figure varies from, so a difference in a picture is the knob and never the fixture.
var oak = new SolidMaterial(Blocks.Planks, 0);
var spruce = new SolidMaterial(Blocks.Planks, 1);
var darkOak = new SolidMaterial(Blocks.Planks, 5);

// White plaster over a cobble plinth, an oak frame, a tiled roof and a dark verge: five materials that read
// apart from each other at a glance, so a difference between two pictures is never a difference in the light.
var plaster = new SolidMaterial(Blocks.StainedClay, 0);
var tile = new SolidMaterial(Blocks.StainedClay, 14);
var basis = new HouseStyle
{
    Wall = new RoomPart([new RoomCourse(new SolidMaterial(Blocks.Cobblestone)), new RoomCourse(plaster)], 5),
    Post = new SolidMaterial(Blocks.Log, 0),
    Sill = new SolidMaterial(Blocks.Cobblestone),
    Roof = tile,
    Verge = darkOak,
    Floor = RoomPart.Of(oak),
    Door = DoorMaterial.Air,
};

var roofNotes = new (RoofForm Form, string Name, string Blurb)[]
{
    (RoofForm.Flat, "Flat", "A lid one course over the walls — the shell every wool cage and spawn cube has always worn, and the only form that can carry a hole to light the room under it."),
    (RoofForm.Gable, "Gable", "Two slopes meeting at a ridge that runs the building's length. The ends are wall carried up to the underside of the slope, not roof."),
    (RoofForm.Hip, "Hip", "A slope off every wall. The ridge keeps whatever length the footprint has left over; on a square there is none, and the roof comes to a point."),
    (RoofForm.Shed, "Shed", "One plane, low at the front wall and climbing to the back. The back wall and both flanks climb with it."),
    (RoofForm.Gambrel, "Gambrel", "A barn: steep for the first courses in from each eave, then shallow to the ridge, so the roof carries a usable volume and still sheds."),
    (RoofForm.Saltbox, "Saltbox", "The two slopes climb at different rates, so they meet off centre — short and steep over the front, long and shallow over the back."),
};

var page = new StringBuilder();

// ── figure 1: the six roofs ───────────────────────────────────────────────────────────────────────────
var roofFigures = new StringBuilder();
foreach (var (form, name, blurb) in roofNotes)
{
    var style = basis with { Form = form, RoofHole = form == RoofForm.Flat, RidgeCap = form != RoofForm.Flat };
    var world = Build(15, 11, style);
    var top = FloorY + style.TopLayerOver(15, 11);
    roofFigures.Append($"<article class='card'><h3>{name}</h3>")
        .Append($"<div class='fig fig--iso'>{Iso(world, -2, -2, 16, 12, FloorY - 1, top)}</div>")
        .Append($"<div class='fig-pair'><figure><div class='fig'>{Plan(world, -2, -2, 16, 12, top, 7)}</div>")
        .Append("<figcaption>From above</figcaption></figure>")
        .Append($"<figure><div class='fig'>{Section(world, -2, -2, 16, 12, top, 7)}</div>")
        .Append("<figcaption>From the front</figcaption></figure></div>")
        .Append($"<p>{blurb}</p></article>");
}

// ── figure 2: pitch and overhang on one form ──────────────────────────────────────────────────────────
var pitchFigures = new StringBuilder();
foreach (var (pitch, overhang, caption) in new[]
         {
             (1, 0, "pitch 1 · flush"),
             (1, 1, "pitch 1 · eave 1"),
             (2, 1, "pitch 2 · eave 1"),
             (2, 3, "pitch 2 · eave 3"),
         })
{
    var style = basis with { Form = RoofForm.Gable, Pitch = pitch, Overhang = overhang, RidgeCap = true };
    var world = Build(13, 9, style);
    var top = FloorY + style.TopLayerOver(13, 9);
    pitchFigures.Append($"<figure><div class='fig'>{Section(world, -4, -4, 16, 12, top, 7)}</div>")
        .Append($"<figcaption>{caption}</figcaption></figure>");
}

// ── figure 3: the floor in zones ──────────────────────────────────────────────────────────────────────
var floorFigures = new StringBuilder();
foreach (var (surface, name, blurb) in new (FloorSurface Surface, string Name, string Blurb)[]
         {
             (FloorSurface.Plain, "Plain",
                 "Unzoned: the floor part's own top course runs wall to wall, which is what every shell was before there were zones."),
             (new FloorSurface { Border = darkOak }, "Border",
                 "A ring one block inside the walls. A material could stripe a floor but could not put a ring there — it resolves from the cell's own coordinates and does not know where the walls are."),
             (new FloorSurface { Border = darkOak, Inlay = new SolidMaterial(Blocks.Wool), InlayInset = 3 }, "Border and inlay",
                 "A plate centred in the room, three blocks in from the walls. Bound to a team-tinted material it takes the room's own colour."),
             (new FloorSurface
                 {
                     Border = new SolidMaterial(Blocks.Cobblestone),
                     Field = new CheckerMaterial(1, oak, darkOak),
                     Inlay = new SolidMaterial(Blocks.Wool),
                     BorderWidth = 2, InlayInset = 4,
                 }, "Zoned and patterned",
                 "The zones say where; a material says what. A checker is a pattern and stays a material, bound to the field."),
         })
{
    var world = Build(15, 13, basis with { Surface = surface, Form = RoofForm.Gable });
    floorFigures.Append($"<article class='card card--tight'><h3>{name}</h3>")
        .Append($"<div class='fig'>{FloorPlan(world, 0, 0, 14, 12, 11)}</div>")
        .Append($"<p>{blurb}</p></article>");
}

// ── figure 4: the porch ───────────────────────────────────────────────────────────────────────────────
var porchFigures = new StringBuilder();
foreach (var (porch, roof, name, blurb) in new (PorchStyle? Porch, RoofForm Roof, string Name, string Blurb)[]
         {
             (null, RoofForm.Gable, "No porch",
                 "The walls stand on the whole footprint. Everything below takes a strip out of this same 15×11 building — the footprint never grows."),
             (new PorchStyle { Depth = 2 }, RoofForm.Gable, "Two blocks, full width",
                 "The front wall stands two blocks back. The strip it gave up keeps the sill and the floor it always had, and gains posts, a rail and a lean-to."),
             (new PorchStyle { Depth = 3, Inset = 3 }, RoofForm.Gable, "Deeper, pulled in",
                 "Pulling the deck in from each end makes the porch a feature of the front rather than the front itself. The room behind it loses the depth either way."),
             (new PorchStyle { Depth = 3, Inset = 2, Roof = RoofForm.Gable, RailBlock = 0 }, RoofForm.Hip, "Its own gable, no rail",
                 "The canopy is seated by its own lowest course clearing the doorway, so any form fronts the building at porch height without fighting the roof above it."),
         })
{
    var style = basis with { Porch = porch, Form = roof, RidgeCap = true, Windows = WindowStyle.Glazed };
    var world = Build(15, 11, style);
    var top = FloorY + style.TopLayerOver(15, 11);
    porchFigures.Append($"<article class='card'><h3>{name}</h3>")
        .Append($"<div class='fig fig--iso'>{Iso(world, -2, -2, 16, 12, FloorY - 1, top)}</div>")
        .Append($"<p>{blurb}</p></article>");
}

// The case that decided how a canopy is seated. Height is only the wall part's extent, so a tower is an
// ordinary style — and a porch that chased the eave up one would be a colonnade rather than a porch.
{
    var tower = basis with
    {
        Wall = new RoomPart([new RoomCourse(new SolidMaterial(Blocks.Cobblestone)), new RoomCourse(plaster)], 18),
        Form = RoofForm.Hip,
        RidgeCap = true,
        Windows = WindowStyle.Lattice with { Block = 164, Sill = 2 },
        Porch = new PorchStyle { Depth = 2, Inset = 1 },
    };
    var world = Build(9, 9, tower);
    var top = FloorY + tower.TopLayerOver(9, 9);
    porchFigures.Append("<article class='card'><h3>On a tower</h3>")
        .Append($"<div class='fig fig--iso'>{Iso(world, -2, -2, 10, 10, FloorY - 1, top, 8)}</div>")
        .Append("<p>The same porch on an eighteen-course wall. The canopy is seated by its own lowest course ")
        .Append("clearing the doorway rather than by the eave, so it stays a porch instead of riding the wall ")
        .Append("to the roof as a colonnade with the door left open to the sky beneath it.</p></article>");
}

// ── figure 5: the windows ─────────────────────────────────────────────────────────────────────────────
var windowFigures = new StringBuilder();
foreach (var (windows, name, blurb) in new (WindowStyle Windows, string Name, string Blurb)[]
         {
             (WindowStyle.Lattice with { Block = 164, Spacing = 3 }, "Stair lattice",
                 "Four stairs in a 2×2 hole, each with its raised half toward the outside of the group, so the quarter each is missing meets in the middle. Open — there is no glass in it."),
             (WindowStyle.Band with { Block = Blocks.WoodenSlab, Data = 5, Width = 3, Spacing = 3 }, "Slab band",
                 "A slab sill, an upside-down slab lintel, and the course between them cut clean through. The two half-blocks make the opening read taller than the one course actually removed."),
             (WindowStyle.Glazed with { Block = Blocks.StainedGlassPane, Data = 3, Width = 2, Height = 3, Spacing = 3 }, "Panes",
                 "The ordinary window: glazed rather than open, and the only one of the three whose size is entirely the author's."),
         })
{
    var style = basis with { Windows = windows, Form = RoofForm.Gable, RidgeCap = true };
    var world = Build(17, 11, style);
    var top = FloorY + style.TopLayerOver(17, 11);
    // Cropped around a real seat rather than around a guess at where one lands, so the close-up is of the
    // window and not of the wall next to it.
    var seat = HouseWindows.Seats(windows, 0, 0, 16, 10, style.Wall.Extent, null)
        .First(candidate => candidate.Edge == RoomEdge.NegZ);
    windowFigures.Append($"<article class='card'><h3>{name}</h3>")
        .Append($"<div class='fig fig--iso'>{Iso(world, -2, -2, 18, 12, FloorY - 1, top, 11)}</div>")
        .Append("<figure><div class='fig'>")
        .Append(Elevation(world, seat.Lo - 2, seat.Lo + seat.Width + 1, FloorY, FloorY + 5, 0, 26))
        .Append("</div><figcaption>The opening at the scale of its pieces</figcaption></figure>")
        .Append($"<p>{blurb}</p></article>");
}

// ── figure 6: one house wearing all of it ─────────────────────────────────────────────────────────────
var finished = basis with
{
    Form = RoofForm.Saltbox,
    Pitch = 1,
    Overhang = 1,
    RidgeCap = true,
    Wall = RoomPart.Of(spruce, 6),
    Windows = WindowStyle.Lattice with { Sill = 2, Spacing = 4 },
    Porch = new PorchStyle { Depth = 3, Inset = 2, Roof = RoofForm.Shed },
    Surface = new FloorSurface
    {
        Border = darkOak,
        Field = new CheckerMaterial(1, oak, spruce),
        Inlay = new SolidMaterial(Blocks.Wool),
        InlayInset = 4,
    },
};
var heroWorld = Build(17, 13, finished);
var heroTop = FloorY + finished.TopLayerOver(17, 13);

// ── the page ──────────────────────────────────────────────────────────────────────────────────────────
page.Append("""
<title>House generation — roofs, floors, porches, windows</title>
<style>
:root {
  color-scheme: light;
  --paper: #eceef1;      --panel: #f7f8fa;   --sunk: #e2e5ea;
  --ink: #14181f;        --ink-soft: #4a525f; --ink-faint: #757f8d;
  --rule: #ccd1d9;       --accent: #7a5b2e;   --accent-soft: #a8823f;
  --shadow: 0 1px 2px rgb(20 24 31 / .07), 0 8px 24px rgb(20 24 31 / .06);
}
@media (prefers-color-scheme: dark) {
  :root:not([data-theme="light"]) {
    color-scheme: dark;
    --paper: #0f1319;    --panel: #161b23;   --sunk: #0a0d12;
    --ink: #e7ebf1;      --ink-soft: #a6b0bf; --ink-faint: #7c8797;
    --rule: #262d38;     --accent: #d3a464;   --accent-soft: #a8823f;
    --shadow: 0 1px 2px rgb(0 0 0 / .5), 0 8px 24px rgb(0 0 0 / .35);
  }
}
:root[data-theme="dark"] {
  color-scheme: dark;
  --paper: #0f1319;      --panel: #161b23;   --sunk: #0a0d12;
  --ink: #e7ebf1;        --ink-soft: #a6b0bf; --ink-faint: #7c8797;
  --rule: #262d38;       --accent: #d3a464;   --accent-soft: #a8823f;
  --shadow: 0 1px 2px rgb(0 0 0 / .5), 0 8px 24px rgb(0 0 0 / .35);
}
* { box-sizing: border-box; }
body {
  margin: 0; background: var(--paper); color: var(--ink);
  font: 400 16px/1.65 "Iowan Old Style", "Palatino Linotype", Palatino, Georgia, serif;
  -webkit-font-smoothing: antialiased;
}
.wrap { max-width: 1180px; margin: 0 auto; padding: 0 24px 96px; }
h1, h2, h3, .label {
  font-family: ui-sans-serif, "Helvetica Neue", "Segoe UI", system-ui, sans-serif;
  text-wrap: balance;
}
h1 { font-size: clamp(2rem, 5vw, 3.1rem); line-height: 1.04; letter-spacing: -.03em; font-weight: 700; margin: 0; }
h2 { font-size: 1.55rem; letter-spacing: -.02em; font-weight: 650; margin: 0; }
h3 { font-size: .95rem; letter-spacing: .01em; font-weight: 650; margin: 0; }
p { margin: 0; max-width: 68ch; }
code, .mono { font-family: ui-monospace, "SF Mono", "Cascadia Mono", Menlo, monospace; font-size: .86em; }
.label {
  font-size: .68rem; letter-spacing: .17em; text-transform: uppercase;
  color: var(--accent); font-weight: 600;
}

header { padding: 72px 0 44px; display: grid; gap: 20px; }
.lede { font-size: 1.16rem; color: var(--ink-soft); max-width: 62ch; }
.hero {
  margin-top: 12px; background: var(--panel); border: 1px solid var(--rule); border-radius: 3px;
  box-shadow: var(--shadow); padding: 28px 20px; display: grid; place-items: center; overflow-x: auto;
}
.hero-legend {
  display: flex; flex-wrap: wrap; gap: 6px 22px; margin-top: 14px;
  font-size: .82rem; color: var(--ink-faint);
}
.hero-legend b { color: var(--ink-soft); font-weight: 600; }

section { padding-top: 60px; display: grid; gap: 22px; }
.section-head { display: grid; gap: 8px; border-top: 2px solid var(--ink); padding-top: 14px; }
.grid { display: grid; gap: 20px; grid-template-columns: repeat(auto-fit, minmax(310px, 1fr)); }
.grid--wide { grid-template-columns: repeat(auto-fit, minmax(360px, 1fr)); }
.grid--four { grid-template-columns: repeat(auto-fit, minmax(230px, 1fr)); }

.card {
  background: var(--panel); border: 1px solid var(--rule); border-radius: 3px;
  padding: 16px 16px 18px; display: grid; gap: 12px; align-content: start; box-shadow: var(--shadow);
}
.card h3 { color: var(--accent); }
.card p { font-size: .93rem; color: var(--ink-soft); }
.card--tight { gap: 10px; }

.fig {
  background: var(--sunk); border-radius: 2px; padding: 12px;
  display: grid; place-items: center; overflow-x: auto;
}
.fig--iso { padding: 6px; }
.fig svg { display: block; max-width: 100%; height: auto; }
.fig-pair { display: grid; grid-template-columns: 1fr 1fr; gap: 10px; }
figure { margin: 0; display: grid; gap: 6px; }
figcaption {
  font-family: ui-sans-serif, system-ui, sans-serif; font-size: .7rem; letter-spacing: .09em;
  text-transform: uppercase; color: var(--ink-faint); text-align: center;
}

.note {
  border-left: 3px solid var(--accent-soft); padding: 2px 0 2px 16px;
  color: var(--ink-soft); font-size: .95rem;
}
table { border-collapse: collapse; width: 100%; font-size: .88rem; }
.table-wrap { overflow-x: auto; border: 1px solid var(--rule); border-radius: 3px; background: var(--panel); }
th, td { text-align: left; padding: 9px 14px; border-bottom: 1px solid var(--rule); vertical-align: top; }
th {
  font-family: ui-sans-serif, system-ui, sans-serif; font-size: .68rem; letter-spacing: .13em;
  text-transform: uppercase; color: var(--ink-faint); font-weight: 600;
}
tbody tr:last-child td { border-bottom: none; }
td:first-child { white-space: nowrap; color: var(--accent); font-family: ui-monospace, Menlo, monospace; font-size: .84rem; }
footer { margin-top: 72px; padding-top: 18px; border-top: 1px solid var(--rule); color: var(--ink-faint); font-size: .85rem; }
@media (max-width: 560px) { .fig-pair { grid-template-columns: 1fr; } }
</style>
<div class="wrap">
<header>
  <span class="label">PGM Studio · world export</span>
  <h1>A house is a footprint, four decisions, and a pile of materials</h1>
  <p class="lede">The footprint comes from the piece the building sits on and a style may never change it. What a
  style does decide is the roof it wears, how its floor is divided, whether it gives a strip of that footprint
  up for a porch, and what its windows are made of. Every picture below is stamped by the real
  <code>HouseStamper</code> and read back out of the world, so none of them can promise a building the export
  would not put down.</p>
""");
page.Append($"<div class='hero'>{Iso(heroWorld, -2, -2, 18, 14, FloorY - 1, heroTop, 13)}</div>")
    .Append("<div class='hero-legend'><span><b>17×13</b> footprint</span><span><b>Saltbox</b> roof, capped ridge</span>")
    .Append("<span><b>Stair-lattice</b> windows</span><span><b>3-block</b> porch, inset 2, lean-to</span>")
    .Append("<span><b>Zoned</b> floor: border, checker field, wool inlay</span></div>")
    .Append("</header>");

page.Append("<section><div class='section-head'><span class='label'>One</span><h2>Six roofs, one loop</h2>")
    .Append("<p>A roof is a <strong>height field over its plan</strong>: for every cell it covers, the course ")
    .Append("that column tops out at, and how many courses it has to write to close the step down to its ")
    .Append("neighbours. The stamper walks that plan once and lays what it is told. Each form is a different ")
    .Append("answer to the same question — the smaller distance across the building is a gable, the smallest of ")
    .Append("all four is a hip, the distance from one wall alone is a shed — so the six differ in one formula ")
    .Append("and nothing else.</p></div>")
    .Append($"<div class='grid grid--wide'>{roofFigures}</div>")
    .Append("<p class='note'>What generalized with them is the wall. The gable's end walls used to be a pass of ")
    .Append("their own; now every wall simply climbs to meet the roof wherever the roof stands above it — which ")
    .Append("is the gable's two ends, the shed's back wall and both its flanks, and nothing at all under a hip.</p>")
    .Append("</section>");

page.Append("<section><div class='section-head'><span class='label'>Two</span><h2>Pitch, and where the eave goes</h2>")
    .Append("<p>Pitch is courses of rise per block travelled inward: one is the vanilla 45°, two an alpine roof. ")
    .Append("A step of more than one course would leave the slope open between its treads, so each column ")
    .Append("carries its own riser as well as its tread. The overhang is measured from the <em>wall line</em> and ")
    .Append("allowed to fall past it, which is what keeps the eave part of the slope instead of a lip tacked onto ")
    .Append("the end of it.</p></div>")
    .Append($"<div class='grid grid--four'>{pitchFigures}</div></section>");

page.Append("<section><div class='section-head'><span class='label'>Three</span><h2>The floor, divided</h2>")
    .Append("<p>The courses below the floor are a stack read downward and are the same everywhere. The one course ")
    .Append("players stand on is the one that varies across the room, and it is divided by how far a cell stands ")
    .Append("from the walls: a border ring, a field across the rest, and a plate centred in it.</p>")
    .Append("<p class='note'>Zoning lives here rather than inside a material because <strong>a material does not ")
    .Append("know the room</strong>. A checker, a noise field and a wall run all resolve from the cell's own ")
    .Append("coordinates, so they can pattern a floor but cannot put a ring one block inside a wall that moves ")
    .Append("with the footprint. Anything that is a pattern stays a material and is bound to the field.</p></div>")
    .Append($"<div class='grid'>{floorFigures}</div></section>");

page.Append("<section><div class='section-head'><span class='label'>Four</span><h2>A porch is taken out, never added on</h2>")
    .Append("<p>The footprint comes from the piece, so a porch that grew outward would be a style deciding a ")
    .Append("footprint. Taken inward it is not: the sill and the floor still cover the whole of it, the walls ")
    .Append("simply stand back from one side, and the strip they gave up becomes a deck with posts, a rail and ")
    .Append("its own roof. Where the room cannot spare the depth, the porch is the part that gives way.</p></div>")
    .Append($"<div class='grid grid--wide'>{porchFigures}</div>")
    .Append("<p class='note'>Two details make it a porch rather than a hole in a wall: the doorway is carried onto ")
    .Append("the wall's new line, and the rail breaks exactly where that doorway crosses it — a rail running ")
    .Append("unbroken across the front would be a porch with no way onto the step. The canopy is seated by its ")
    .Append("own <strong>lowest</strong> course, which has to clear that doorway; the ridge follows by however ")
    .Append("far the form happens to fall. One statement for all six, and the one that survives a tower.</p></section>");

page.Append("<section><div class='section-head'><span class='label'>Five</span><h2>Three windows, two of them open</h2>")
    .Append("<p>Windows are cut out of the wall the pass has just built, so seating is the half that has to be ")
    .Append("right: each wall is seated on the run between its two corner posts, the windows are spread evenly ")
    .Append("and centred on that run, and any seat that would meet a doorway is dropped rather than shifted — ")
    .Append("shifting one would break the spacing of every window after it to save it.</p>")
    .Append("<p class='note'>They are chosen as a <strong>block id</strong>, not as a bound material, and that is ")
    .Append("the one place the room-style library departs from its own shape. A stair's metadata is which way it ")
    .Append("climbs and a slab's is which half it fills; the four stairs of a lattice differ from each other by ")
    .Append("geometry alone, and a material resolving its data from where the cell sits would turn all four the ")
    .Append("same way — a solid 2×2 patch of wall rather than a window.</p></div>")
    .Append($"<div class='grid grid--wide'>{windowFigures}</div></section>");

page.Append("""
<section><div class='section-head'><span class='label'>Reference</span><h2>What a style now carries</h2></div>
<div class='table-wrap'><table>
<thead><tr><th>Knob</th><th>Is</th></tr></thead>
<tbody>
<tr><td>Form</td><td>Flat · Gable · Hip · Gambrel · Shed · Saltbox. Every one a height field over the same plan.</td></tr>
<tr><td>Pitch</td><td>Courses of rise per block travelled inward. Ignored by a flat lid.</td></tr>
<tr><td>Overhang</td><td>How far the roof reaches past the walls, measured from the wall line so the eave keeps falling.</td></tr>
<tr><td>RoofHole</td><td>The gap a flat lid carries to light the room. A sloped roof has a volume of its own and never takes one.</td></tr>
<tr><td>RidgeCap</td><td>The line the slopes meet on, laid in the verge rather than the roof's own material.</td></tr>
<tr><td>Surface</td><td>The floor's top course in plan: Border and its width, Field, Inlay and its inset.</td></tr>
<tr><td>Windows</td><td>Form, block, sill, size and spacing. StairLattice is 2×2 and SlabBanded three courses, because that is what makes each one work.</td></tr>
<tr><td>Porch</td><td>Depth, inset, which wall, the canopy's own form, and the rail block. Depth 0 is no porch.</td></tr>
</tbody></table></div>
</section>
<footer>Every figure stamped by <code>PgmStudio.Minecraft.HouseStamper</code> and read back out of the
<code>VoxelWorld</code> — colours from the export's own block palette. Regenerate with
<code>dotnet run tools/compose/house-showcase.cs</code>.</footer>
</div>
""");

Directory.CreateDirectory("tools/compose/out");
var outPath = Path.Combine("tools/compose/out", "house-showcase.html");
File.WriteAllText(outPath, page.ToString());
Console.WriteLine($"wrote {outPath} ({new FileInfo(outPath).Length / 1024} KB)");
