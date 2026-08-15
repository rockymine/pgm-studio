#:project ../../src/PgmStudio.Minecraft/PgmStudio.Minecraft.csproj
// house-showcase: the house-generation explainer — one designed, self-contained HTML page covering the four
// things a house style decides beyond its materials: which roof it wears, how its floor is divided, where its
// porch comes from, and what its windows are made of. Every figure is stamped by the real HouseStamper and
// read back out of the VoxelWorld, so a picture cannot promise a building the export would not put down.
// Run from the repo root: dotnet run tools/compose/house-showcase.cs  →  tools/compose/out/house-showcase.html
using System.Text;
using PgmStudio.Domain;
using PgmStudio.Minecraft;
using PgmStudio.Minecraft.Views;
using PgmStudio.Geom;

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

// ── the renders ───────────────────────────────────────────────────────────────────────────────────────
// Thin names over PgmStudio.Minecraft.Views, which the studio's library previews draw with too. The page and
// the studio have to agree about what a building looks like — a picture one gets right and the other gets
// wrong is worse than either being wrong alone — so there is one implementation and these are its local
// spellings, taking the loose bounds the figures below are written in.
string Iso(VoxelWorld world, int minX, int minZ, int maxX, int maxZ, int minY, int maxY, int scale = 10)
    => WorldViews.Isometric(world, new BlockBox(minX, minY, minZ, maxX, maxY, maxZ), scale);

/// The highest block of each column — what the roof does, its hole and its eave, and nothing else.
string Plan(VoxelWorld world, int minX, int minZ, int maxX, int maxZ, int top, int cell = 9)
    => WorldViews.Plan(world, new BlockBox(minX, FloorY - 1, minZ, maxX, top, maxZ), cell);

/// The floor's own course seen from above — the one plan a roof would otherwise hide.
string FloorPlan(VoxelWorld world, int minX, int minZ, int maxX, int maxZ, int cell = 9)
    => WorldViews.Slice(world, new BlockBox(minX, FloorY, minZ, maxX, FloorY, maxZ), FloorY, cell);

/// The building projected onto its front, nearest block first and shaded by how far back it stands.
string Section(VoxelWorld world, int minX, int minZ, int maxX, int maxZ, int top, int cell = 9)
    => WorldViews.Section(world, new BlockBox(minX, FloorY - 2, minZ, maxX, top, maxZ), cell);

/// One wall at the scale of the pieces in it — the only render that draws a block as its own shape.
string Elevation(VoxelWorld world, int fromX, int toX, int fromY, int toY, int z, int cell = 22)
    => WorldViews.Elevation(world, new BlockBox(fromX, fromY, z, toX, toY, z), z, cell);


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
    Wall = new RoomPart(new BandStack([new Band(new SolidMaterial(Blocks.Cobblestone)), new Band(plaster)]), 5),
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
    (RoofForm.Shed, "Shed", "One plane, low at the front wall and climbing to the back. The back wall and both flanks climb with it. On a building longer than it is deep the plane levels off once it has risen the short side's worth, so a hall carries a long roof rather than a tall one."),
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
        Wall = new RoomPart(new BandStack([new Band(new SolidMaterial(Blocks.Cobblestone)), new Band(plaster)]), 18),
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
    var seat = HouseWindows.Seats(windows, new BuildingPlan(0, 0, 16, 10).Segments, style.Wall.Extent, null)
        .First(candidate => candidate.Wall.Facing == RoomEdge.NegZ);
    windowFigures.Append($"<article class='card'><h3>{name}</h3>")
        .Append($"<div class='fig fig--iso'>{Iso(world, -2, -2, 18, 12, FloorY - 1, top, 11)}</div>")
        .Append("<figure><div class='fig'>")
        .Append(Elevation(world, seat.Lo - 2, seat.Lo + seat.Width + 1, FloorY, FloorY + 5, 0, 26))
        .Append("</div><figcaption>The opening at the scale of its pieces</figcaption></figure>")
        .Append($"<p>{blurb}</p></article>");
}

// ── figure 6: the storeys ─────────────────────────────────────────────────────────────────────────────
// A building's height is a stack of rooms rather than a wall height, so the figures are about the air: how
// much of it each storey has, what closes it, and how a player gets to the one above.
var storeyFigures = new StringBuilder();
var storeyBasis = basis with
{
    Form = RoofForm.Gable,
    RidgeCap = true,
    Overhang = 1,
    Windows = WindowStyle.Glazed with { Block = Blocks.GlassPane, Sill = 2, Spacing = 4 },
};

foreach (var (storeys, name, blurb) in new (Storey[] Storeys, string Name, string Blurb)[]
         {
             ([new Storey { Clear = 3 }], "One storey",
                 "Three blocks of air and the roof on top of them. The same building a style with no storeys at all resolves to."),
             ([new Storey { Clear = 3 }, new Storey { Clear = 3 }, new Storey { Clear = 3 }], "Three of them",
                 "Eleven courses, not nine: each storey but the top one spends one more course than its clear on the slab that carries the next. The windows repeat because each storey seats its own in its own frame."),
             ([new Storey { Clear = 6 }, new Storey { Clear = 3 }], "A tall ground floor",
                 "A storey's courses are counted from its own floor, so raising the one below moves the storey above it whole rather than sliding its windows up the wall."),
         })
{
    var style = storeyBasis with { Storeys = storeys };
    var world = Build(13, 11, style);
    var top = FloorY + style.TopLayerOver(13, 11);
    storeyFigures.Append($"<article class='card'><h3>{name}</h3>")
        .Append($"<div class='fig fig--iso'>{Iso(world, -2, -2, 14, 12, FloorY - 1, top, 10)}</div>")
        .Append($"<p>{blurb}</p></article>");
}

// The cutaway: a slice down the plane the ladder stands in, which is the only view that shows all three of a
// slab, the clear under it and the way through it at once. The plane is found by looking for the ladder in
// the world rather than by working out where it ought to be.
{
    var style = storeyBasis with { Storeys = [new Storey { Clear = 3 }, new Storey { Clear = 4 }, new Storey { Clear = 3 }] };
    var world = Build(13, 11, style);
    var top = FloorY + style.TopLayerOver(13, 11);
    var climb = (
        from x in Enumerable.Range(0, 13)
        from z in Enumerable.Range(0, 11)
        from y in Enumerable.Range(FloorY, top - FloorY)
        where world.GetBlock(x, y, z).Id == Blocks.Ladder
        select (x, z)).First();

    storeyFigures.Append("<article class='card card--wide'><h3>Cut through the way up</h3>")
        .Append("<figure><div class='fig'>")
        .Append(Elevation(world, -1, 13, FloorY - 1, top, climb.z, 15))
        .Append($"</div><figcaption>The plane at z={climb.z}, the one the ladder stands in</figcaption></figure>")
        .Append("<p>Three storeys of three, four and three. Each slab covers the interior only — the perimeter ")
        .Append("is wall already — and each carries a hole with a ladder standing in it, reaching the slab so a ")
        .Append("player steps off onto the new floor rather than into its underside. Without one an upper ")
        .Append("storey is a sealed volume: a picture of a house rather than a house.</p></article>");
}

// ── figure 7: one house wearing all of it ─────────────────────────────────────────────────────────────
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
<meta charset="utf-8">
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
/* A cutaway is as wide as the building it slices, so it takes the whole row rather than a column of it. */
.card--wide { grid-column: 1 / -1; }

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
    .Append("and nothing else. Every one of those distances is measured over the building's <strong>shorter ")
    .Append("side</strong>: the forms taken across the building get that for free, and the two taken from the ")
    .Append("front wall are held to it, so no roof ever climbs with the long side of a hall.</p></div>")
    .Append($"<div class='grid grid--wide'>{roofFigures}</div>")
    .Append("<p class='note'>What generalized with them is the wall. The gable's end walls used to be a pass of ")
    .Append("their own; now every wall simply climbs to meet the roof wherever the roof stands above it — which ")
    .Append("is the gable's two ends, the shed's back wall and both its flanks, and nothing at all under a hip.</p>")
    .Append("</section>");

// ── figure 1a: the long hall ──────────────────────────────────────────────────────────────────────────
// The one case the short-side hold exists for, drawn at the proportion that shows it: a building three times
// as long as it is deep, where the front wall is an end rather than a side.
var hallFigures = new StringBuilder();
foreach (var (form, name, blurb) in new (RoofForm Form, string Name, string Blurb)[]
         {
             (RoofForm.Gable, "Gable over a hall",
                 "Taken across the building already, so the length changes nothing: the ridge simply runs further. This is the shape the other two are held to."),
             (RoofForm.Shed, "Shed over a hall",
                 "The plane climbs from the front wall at its pitch and then runs flat — a lean-to that levels off. Unheld it would climb the whole length of the building and stand taller than the hall is deep."),
             (RoofForm.Saltbox, "Saltbox over a hall",
                 "A gable whose two slopes climb at different rates, so it is measured across the short side exactly as a gable is. The front decides which of them is the steep one and nothing about how high the roof stands."),
         })
{
    var style = basis with { Form = form, Overhang = 1, Pitch = 1 };
    // The door on an end wall, which is what turns a shed's fall onto the long axis.
    var world = new VoxelWorld();
    for (var x = -3; x < 27; x++)
        for (var z = -3; z < 11; z++)
            for (var y = FloorY - 3; y < FloorY; y++)
                world.SetBlock(x, y, z, y == FloorY - 1 ? Blocks.Grass : Blocks.Dirt);
    HouseStamper.Stamp(world, 0, 0, 24, 8, FloorY, style, color: 14, doors: [new RoomDoor(RoomEdge.NegX, 3, 2)]);
    var top = FloorY + style.TopLayerOver(24, 8, RoomEdge.NegX);
    hallFigures.Append($"<article class='card'><h3>{name}</h3>")
        .Append($"<div class='fig fig--iso'>{Iso(world, -1, -1, 24, 8, FloorY - 1, top, 8)}</div>")
        .Append($"<p>{blurb}</p></article>");
}

page.Append("<section><div class='section-head'><span class='label'>One, continued</span>")
    .Append("<h2>A long building is a long building, not a tall one</h2>")
    .Append("<p>A gable, a hip and a gambrel are measured across the shorter side, so a hall only makes their ")
    .Append("ridge longer. A shed and a saltbox are measured from the <em>front</em> wall — and the front is ")
    .Append("wherever the doors are, which on a hall is as likely to be an end as a side. Left alone they ")
    .Append("would climb the long way, and a 24&times;8 building would carry a roof two dozen courses over its ")
    .Append("wall from a footprint drawn flat on the canvas. Every rise is therefore measured over the ")
    .Append("<strong>shorter side</strong>, whichever wall the slope falls toward.</p></div>")
    .Append($"<div class='grid grid--wide'>{hallFigures}</div>")
    .Append("<p class='note'>The height that survives is the one the pitch asked for. What the footprint's ")
    .Append("long side decides is how far the roof runs, and nothing about how high it stands.</p></section>");

// ── figure 1b: the gable face ─────────────────────────────────────────────────────────────────────────
var gableFigures = new StringBuilder();
foreach (var (gable, name, blurb) in new (TerrainMaterial? Gable, string Name, string Blurb)[]
         {
             (null, "Unbound",
                 "The wall's top course carried up. Note that it is the top course rather than the stack going on to count: the courses have run out by then, so a wall that bands as it rises goes flat the moment it turns into a gable."),
             (new SolidMaterial(Blocks.Log, 0), "Bound to a timber",
                 "The face named separately — here the oak the corner posts are, so the frame reads as carrying on into the gable. This is what nearly every hand-built house does and what the wall's own stack had no way to say."),
         })
{
    var style = basis with
    {
        Form = RoofForm.Gable, Overhang = 0, RidgeCap = true, Gable = gable,
        Wall = new RoomPart(new BandStack([new Band(new SolidMaterial(Blocks.Cobblestone)), new Band(plaster)]), 5),
    };
    var world = Build(13, 9, style);
    var top = FloorY + style.TopLayerOver(13, 9);
    gableFigures.Append($"<article class='card'><h3>{name}</h3>")
        .Append($"<div class='fig fig--iso'>{Iso(world, -1, -1, 13, 9, FloorY - 1, top, 12)}</div>")
        .Append($"<p>{blurb}</p></article>");
}

page.Append("<section><div class='section-head'><span class='label'>One and a half</span>")
    .Append("<h2>The triangle the slopes leave behind</h2>")
    .Append("<p>Where the roof stands above the wall, the wall climbs to meet it — and the triangle that ")
    .Append("leaves at each end of a gable is the <strong>gable face</strong>, a part of its own. It is wall ")
    .Append("rather than roof, so the roof's material never appears in it; and it is one material rather than ")
    .Append("a stack, because a stack has nothing left to say by the time the wall has ended.</p>")
    .Append("<p class='note'>The <strong>verge</strong> is a different piece again — the roof's own outermost ")
    .Append("ring. On a flush roof that is the raking edge directly over the gable, as drawn here; give the ")
    .Append("roof an eave and the verge moves out to the overhang, leaving plain roof along the wall line.</p>")
    .Append("</div>")
    .Append($"<div class='grid grid--wide'>{gableFigures}</div></section>");

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

page.Append("<section><div class='section-head'><span class='label'>Six</span><h2>A height is a stack of rooms</h2>")
    .Append("<p>A storey states the <strong>clear</strong> — the blocks of air a player stands in — and the ")
    .Append("courses follow from it: one more than the clear where something stands over it, for the slab that ")
    .Append("separates the two, and none on the top storey, because the roof is its lid. Measuring the air ")
    .Append("rather than the masonry is what makes the number an author decides the number that is true, and it ")
    .Append("is why three is the least a room may be. A style that names no storeys resolves to the single one ")
    .Append("its wall describes, so a wall height stays a wall height and every shell built before storeys ")
    .Append("existed is the building it always was.</p></div>")
    .Append($"<div class='grid grid--wide'>{storeyFigures}</div>")
    .Append("<p class='note'>The ladder hangs on the <strong>door wall</strong>, one cell along from an interior ")
    .Append("corner, and both halves of that are about what else claims those cells. Chests and wool monuments ")
    .Append("fill a room's corners first and then the far wall inward, so the door wall is untouched until a ")
    .Append("room carries more monuments than a team ever captures — six wools is the ceiling in practice. The ")
    .Append("corner itself is always taken, which is why the ladder sits one along from it; where the doorway ")
    .Append("reaches that end it takes the other one instead.</p></section>");

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
<tr><td>Gable</td><td>The triangle a sloped roof leaves at each end. Unbound, the wall's top course carried up.</td></tr>
<tr><td>Surface</td><td>The floor's top course in plan: Border and its width, Field, Inlay and its inset.</td></tr>
<tr><td>Windows</td><td>Form, block, sill, size and spacing. StairLattice is 2×2 and SlabBanded three courses, because that is what makes each one work.</td></tr>
<tr><td>Porch</td><td>Depth, inset, which wall, the canopy's own form, and the rail block. Depth 0 is no porch.</td></tr>
<tr><td>Storeys</td><td>The rooms stacked inside, each by its clear — never under three — with its own wall, windows and floor zoning where it wants them. Empty is the one storey the flat parts describe.</td></tr>
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
