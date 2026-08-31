# The Plan tool

## What it is

Plan authors a map at its coarsest scale: the board as rectangles on a proxy grid, and the intent the
finished map is played by. It is one of the two tools a map can be started in — the other is Sketch — and the
difference between them is what they write. A sketch alone produces geometry and says nothing about what the
geometry is for; a plan states the gamemode, where the teams spawn, which objectives exist and where they sit,
and lays down the terrain those things stand on.

A plan is authored as **one symmetry unit**. Everything drawn belongs to team 0; the compiler fans it into the
other teams' images by the plan's symmetry mode. Nothing in the document is per-team, and there is no way to
give one team a different board from another.

The tool opens on two routes. `/maps/{slug}/plan` edits the plan artifact of a map row and is the normal
entry: the map carries its plan, its sketch and its configuration on one row, and rebuilding refreshes them in
place. `/plan-editor` opens the generator's candidate pool instead, has no map row behind it, and originates
one when a candidate is built. The Info phase and its rail appear only on the map-backed route; the bare route
stays on the Draw workspace and keeps its settings in the sidebar.

A plan leaves the tool by compiling — one call turns the document into a layout and an intent — and then by
building, which writes both onto a map and rasterizes the layout into world geometry. The map lands at
`stage=configure` with a sketch to refine and an intent to finish.

## What it writes

A map-backed plan is a `map` row at `stage=plan` whose `plan_json` artifact holds the document, read and
written through `MapArtifactStore` like every other artifact. It sits outside the entity-replace codec
that carries the rest of a map, exactly as the sketch layout does. A candidate is a row in the separate `plan`
table with an origin of `generated`, `authored` or `imported`, its composer descriptor, and its own copy of
the same document.

Two things a plan tool edits are *not* in the plan document. The map's display name and its authors live on
the map row and are saved through `PATCH /api/map/{slug}/metadata`; the document's own `meta.name` is synced
alongside them because the compile reads it. The surface stepper — how many blocks one click of a piece's
height control moves — is a browser preference in `localStorage`, never part of a plan.

The document is not cached client-side. The database is its store, and the editor loads whatever the route
names.

## The plan document

The wire format is `*.plan.json`, modelled by `PlanModel` (`src/PgmStudio.Pgm/Plan/PlanModel.cs`) on the
server and mirrored by `plan-doc.js` on the client, which round-trip it identically. Both normalise on load:
unknown piece roles, box kinds and zone kinds fold to their canonical value, and every marker without an id
is given one. A document written under an older vocabulary therefore loads cleanly rather than failing.

Here is one carrying every element the format has, which compiles clean — no errors and no warnings. That
claim is a test: `DocumentedBodyTests` posts this block to the route named on its fence and fails if the
compile ever answers anything else.

```json POST /api/plan/compile
{
  "plan": 1,
  "meta": { "name": "Example board" },
  "globals": { "cell": 5, "symmetry": "rot_180", "maxPlayers": 12, "surface": 9 },
  "pieces": [
    { "id": "spawn",      "role": "spawn",     "rect": [1, 9, 2, 2] },
    { "id": "lane",       "role": "piece",     "rect": [1, 5, 2, 4] },
    { "id": "approach",   "role": "piece",     "rect": [-3, 4, 2, 7] },
    { "id": "wool-room",  "role": "wool-room", "rect": [-3, 11, 2, 2] },
    { "id": "plateau",    "role": "piece",     "rect": [5, 7, 4, 2], "surface": 13 },
    { "id": "bridgehead", "role": "piece",     "rect": [-1, 7, 2, 2] },
    { "id": "gap",        "role": "buffer",    "rect": [3, 5, 2, 2] }
  ],
  "zones": [
    { "id": "mid-band", "rect": [-3, -5, 6, 10], "holes": [] },
    { "id": "lane-e",   "rect": [3, -1, 2, 4], "kind": "water-lane" }
  ],
  "placements": {
    "spawns":       [ { "id": "spawn-1", "piece": "spawn", "at": [1, 1], "facing": "front" } ],
    "wools":        [ { "id": "wool-1", "piece": "wool-room", "at": [1, 1] } ],
    "iron":         [ { "id": "iron-1", "piece": "spawn", "at": [0.5, 0.5] } ],
    "destroyables": [ { "id": "destroyable-1", "piece": "plateau", "at": [2, 1],
                        "style": "cube-3", "materials": "obsidian", "float": 4 } ],
    "cores":        [ { "id": "core-1", "piece": "approach", "at": [1, 5], "layer": "undercroft",
                        "lava": 3, "lavaHeight": 3, "float": 6, "leak": 5 } ]
  },
  "walls":  [ { "a": "approach", "b": "bridgehead" } ],
  "boxes":  [ { "id": "wool-box", "kind": "wool", "rect": [-4, 3, 4, 10] } ]
}
```

Everything optional is shown set here so the shapes are visible; almost all of it can be left out. The wool
names no `color`, so the auto rule gives red's wool the red dye and blue's the blue one — naming one would
give **both** teams that colour, since a stated field is used verbatim on every orbit image. The destroyable
and the core state values that are already their defaults; a bare `{ "id", "piece", "at" }` builds the same
structures. The buffer declares a void the compiler would have declared anyway, and the box is annotation the
compiler ignores entirely.

### Coordinates

Every footprint is in **signed integer proxy cells** relative to the symmetry centre, which is the world
origin `(0, 0)`. One cell spans `globals.cell` blocks, so a cell rect `[x, z, w, h]` covers blocks
`[x·cell, (x+w)·cell)` on X and the same on Z. Heights are blocks, not cells.

Marker positions are stored **piece-relative**: a marker names the piece it rides and an `at` offset in cells
from that piece's minimum corner, snapped to a half-cell lattice. It resolves to the block
`piece.min + at·cell`, so a whole offset lands on a cell corner — the centre of a 2×2-cell room — and a half
offset on a cell centre. A marker that rides no piece cannot exist: the canvas refuses to drop one over empty
grid, and the validator errors on a hand-written marker whose piece is unknown or is a buffer.

**A destroyable and a core are the one exception.** `piece` may be empty, and `at` then reads as an
**absolute** cell offset from the symmetry centre — the same frame a piece's own `rect` is authored in — so a
goal can stand on ground that exists only as an authored sketch shape, with no plan piece manufactured to
carry it (`B128`). The canvas does not yet offer a way to draw this; a hand-written or agent-authored document
can.

### Globals

`globals` is set once and governs the whole board.

| Field | Default | Is |
|---|---|---|
| `cell` | 5 | Blocks per proxy cell — the scale everything else is drawn at. |
| `symmetry` | `rot_180` | `rot_180`, `rot_90`, `mirror_x`, `mirror_z` or `none`. Decides the team count (the orbit order) and how the authored unit is fanned. |
| `maxPlayers` | 12 | Carried into the intent; changeable again in Configure. |
| `surface` | 9 | The base island height in blocks — the Y a piece stands at unless it overrides it. |
| `observerY` | `surface + 15` | The observer's height. No control writes it; it is honoured when hand-authored. |

**The build ceiling is not a global, and that is the correction.** It was `headroom`, a slack added to
`surface`, so the cap was computed from the plan's flat nominal world — a ground level the relief solve then
abandons, which produced boards whose ceiling sat below their own terrain. The author's rule measures it where
the answer exists: **twenty blocks over the highest block the world actually builds and a player meets**
(`G6`, amendments 14 and 25) — the terrain and the buildings standing on it, never a made thing hung in the
air and never an objective, which floats by design. It is derived in `WorldBuilder`, after the dressing pass
because the last building on the board is a house that pass places, and written onto the intent as
`MaxHeight`. A plan-level number would be a second source for one value, and the one that gets overwritten.

`surface` stays exactly as it was, per piece and global: it is load-bearing and correct as a plan-space
concept, and it is still what the observer's default height is measured from.

### Pieces

A piece is a rectangle of ground. `rect` is `[x, z, w, h]` in cells; `surface` overrides the global height for
that piece alone, and `mirrors: false` marks an on-axis piece that is not fanned.

**A piece's surface is how a board gets its elevation**, and it carries further than a flat slab with a
plateau on it. `tools/seeds/ruediger.plan.json` is the plan behind the Ruediger-themed CTW map, and its 31
pieces stand at ten different heights over a base of 9: a peak at 16 built from three pieces at 14, 15 and 16;
a hub at 12 approached by two one-cell-wide pieces at 10 and 11; a spawn platform at 14 reached over 13; and,
at the far west edge, a staircase made of six pieces — `piece-15` at 7, then five single-cell pieces climbing
8, 9, 10, 11, 12, one step per cell, of which the one at 9 states no surface at all and simply inherits the
base. A ramp, a terrace and a cliff are all the same instrument — adjacent pieces at different surfaces — and
the compiler turns each distinct height in a component into its own shape.

Roles split in two. The **generating** roles produce terrain and take part in connectivity, validation and
export: `piece` is anonymous ground, `wool-room` is the room region a wool stands in (bedrock floor and
entrance redstone at export), and `spawn` is the spawn region a spawn building stands on (iron inside it
renews). The one **annotation** role, `buffer`, produces no terrain and joins no graph; it states negative
space — lane spacing, a border reservation, the hole inside a ring — which the compiler carves out of the
outline that would otherwise fill it. A buffer over a generating piece is inert, so it can declare a void but
never destroy ground. The retired role names `lane`, `hub`, `mid` and `connector`, and anything unrecognised,
load as a plain `piece`.

Enclosed voids do not have to be drawn. `PlanVoids.Declare` runs on every compile and adds a buffer over every
enclosed void no buffer covers yet, so a ring's hole is declared whether or not the author declared it. The
step is idempotent, and a buffer deleted from a generated plan comes back on the next compile.

**A void is enclosed by a whole component, whatever height its pieces stand at.** A hole ringed by five pieces
at five surfaces is the same hole as one ringed by five pieces at one — it is players who walk round it, and a
stair round a yard is still a yard — so the reading is per component and never per surface. That matters
because stating a surface per piece is how a composed board is made paintable (*What a compile produces*), and
it must not be how the board's negative space stops being stated: the `donut` and `clamp` wool rooms the
composer emits are holes by construction, and the walls a board hangs on them are drawn to guard exactly that
ground. Everything that generates terrain counts against the reading, so a void a stepped plateau seats a
piece into is a plateau and is never declared.

**And a hole is never scenery.** What the buffer compiles to is a `subtract`, which the sketch tool then holds
as the board's own statement of its negative space: it may be *redrawn* — rounded off, narrowed, moved — and
never papered over with an add (`docs/tools/sketch.md`, `SK13`).

### Zones

A zone is a rect over the void saying where players may bridge, with optional no-build `holes` in the same
units. The two kinds differ in *when* they open. A **build zone** (`kind` absent, the default) is open from the
first tick: it joins the buildable region and it is what the gap-connectivity derivation reads. A **water
lane** (`kind: "water-lane"`) is closed at the first tick and opens partway through the match, when water lands
at `y=0` and its columns stop reading as void. The map states only *where* the lanes are, under the id
`water-lanes`; the fill and its timer belong to a shared fragment the server resolves and never ships with a
map. That fragment opens the lanes **45 minutes in** — `<constant id="water-lane-timer">45m</constant>`, a
fallback constant a map may override, announced five minutes ahead — and re-fills them on a 15-minute pulse
so a lane players drain comes back. It is deliberately left out of the buildable
region and out of every derivation that describes the starting board, because treating it as a connection
would tell the lint a map is joined up at a tick when it is not — so a lane can never be the route that
connects two teams' land. A lane's holes are dropped at compile; the shared fragment fills a flat region and
has nowhere to put a cutout.

### Placements

Markers are grouped by kind under `placements`, and each carries an `id` unique across the whole set (minted
as `<kind>-<n>` in the order spawn, wool, iron, destroyable, core when absent), the `piece` it rides and its
`at` offset.

A **spawn** adds `facing` — `front` (−Z), `back` (+Z), `left` (−X) or `right` (+X), absolute board directions
that are fanned per orbit image. The facing picks the wall the spawn room's door opens in, and is overridden
at compile when it would open onto the void from a board-edge piece.

A **wool** may name a `color`, one of the sixteen dyes PGM resolves — `white`, `orange`, `magenta`,
`light_blue`, `yellow`, `lime`, `pink`, `gray`, `silver`, `cyan`, `purple`, `blue`, `brown`, `green`, `red`,
`black`, which `GET /api/objectives/vocabulary` serves under `wool.colors` with each one's label and swatch.
Case and spacing are folded and `light_gray` reads as the `silver` the wire carries, matching what PGM's own
`DyeColors` accepts; a word outside the set is refused as `PL14` rather than substituted, because the
substitution available is the auto rule, which is what an *absent* colour asks for. Empty means auto: a team's
first wool takes the team colour and later wools take distinct dyes from a global cursor, so every wool on the
board keys to a unique colour.

An **iron** marker is a bare position. Where its piece is a spawn-role piece carrying a spawn marker, it rides
that spawn and is resolved beside the room — which is how an iron marker beside a spawn marker changes the
size of the spawn building. Elsewhere it stamps a standalone iron cube, renewing only if its piece is a spawn.

A **destroyable** and a **core** are the DTM and DTC goals. Both are anchored on a marker with no Y: the
structure floats `float` blocks above the ground the relief actually leaves under it, which is the design — a
core on the ground cannot leak, and a destroyable on the ground is trivially covered. `float` is an offset over
the ground **as built**, not the plan's flat nominal surface, and it is resolved where the ground is actually
known — the world-export path, once the layout is rasterized and the relief solved — never at compile time,
which only knows the plan's rectangles. That is also why `piece` may be empty for these two markers alone
(above): the goal's Y was never the piece's to give. Every structure field is optional and defaulted by the
compiler, so a bare `{ piece, at }` or `{ at }` is a valid, typical goal, and setting a field back to its
default removes the key rather than freezing the number.

| Marker | Optional fields | Defaults (`GET /api/objectives/vocabulary`) |
|---|---|---|
| `wools` | `color` | resolved from the team and the wools before it |
| `destroyables` | `style`, `materials`, `float`, `layer`, `name` | `pillar-3`, `obsidian`, 4, the top surface, `<Team> Monument` |
| `cores` | `lava`, `lavaHeight`, `openTop`, `float`, `leak`, `layer`, `name` | 3, 3, false, 6, 5, the top surface, PGM names it |

**`layer` is which storey the goal stands on**, and it is the one field here that is not a knob on the
structure but a statement about where the structure is. A stacked board carries a surface per layer, so a
goal that names none resolves against the topmost one — which puts a monument stated for a hall on the deck
roofing it. The word is the sketch layer's own id, and the compiler carries it straight through to the
intent, where the export already reads it.

Destroyable styles are `pillar-1`, `pillar-2`, `pillar-3`, `cube-3`, `cube-4` and `column-plus`; an unknown
slug is an error rather than a silent fallback. A core's `float` and `leak` are one knob — escaping lava
free-falls to the terrain at `float` below the casing while the core leaks one course below `leak` below it
(the leak region is tested against the lava block's own centre), so together they state how far players must
dig, `max(0, leak + 1 − float)` — and authoring one without the other is refused. Either goal's `float` is
capped at **12**: the defaults are floors, and a goal higher than that is reached by building a tower to it
(`OB22`).

### Walls

An interface between two pieces is authored for exactly one reason, and this is it. Everything else a seam
does — a step, a drop, a cliff — is decided by the ground: a plan states a surface per piece, the relief
carves the rest, and what happens where two surfaces meet follows from them rather than from a mark.

`walls` marks a pair whose interface carries a pre-built approach
wall: a bedrock barrier two blocks thick and three courses tall across the full interface width, stamped on
the attack side, which slows a wool raid and gives the defence a prepared line. Which of its two faces carries
the defence chests is **not authored**: the wall is two blocks thick so exactly one face can be opened, and it
is the **approach** face — the side further from the wool, which is the side both teams reach the line across
and the same side the wall takes its height from. A wall on a pair that shares no land interface is an error (`PL11`), and so is a wall on
the wool room's own interface (`PL13`) — the wall and the room would stamp through each other, so the device
belongs an approach out, around 15 blocks from the room.

### Boxes

`boxes` groups pieces into the partition they realize — a `wool` or `spawn` approach, the `hub` body they seat
on, the `frontline` that fronts it, the `mid` between the fanned images. A box is drawn around its members
rather than filled by them, boxes may overlap, and membership is by containment (every generating piece wholly
inside the rect) unless `members` names the pieces explicitly, which a composed partition writes so a picked
board reopens with the grouping that produced it.

Boxes are **authoring annotation only**. The compiler, the validator and the derivers ignore them, so drawing
one can never change what a plan builds; they are how a generated board explains itself, and the unit the
producibility read reports against. A hand-authored plan does not need them.

### Reference

`reference` records the real map a plan was traced over — its slug, plus an `offset`, `scale` and `opacity`
for placing its top-down render under the grid. Authoring metadata: the compiler never reads it.

### Fields that are dropped

`themes`, `mapTheme` and `themeScopes` are parsed away and lost. Terrain paint moved onto the sketch model,
where the geometry is final; a plan carrying them compiles exactly as one without them.

## What a compile produces

`PlanCompiler.Compile` is pure and deterministic — the same plan compiles to the same pair on the server and
in the editor — and one-way: nothing reads a layout or an intent back into a plan.

**The layout** (`SketchLayout`) is the terrain. Pieces joined by land interfaces form components; within a
component each distinct surface becomes its own shape, so a stepped island emits stacked plateaus, and each
shape is the rectilinear union outline of the rects at that height rather than the rects themselves — which is
why abutting pieces of equal height arrive in the Sketch tool as a single polygon. Buffers become `subtract`
shapes over whatever no generating piece covers. Islands are mirror groups: everything fanned lands in `team`,
everything on-axis in `neutral`. The framing box is the extent of every terrain shape fanned across the orbit,
one cell proud.

**The strait the compile hands over is measured again once the board is drawn.** `CT12` judges the direct
crossing between the two team islands of a two-team wool board, and it takes that reading over the plan's
rectangles — before a shape exists. A finish is free to move it: a shape drawn across the gap bridges it, a
quay pushed out from one shore narrows it, a fill closes it altogether, and the plan's verdict then stands
over ground that no longer matches it. So `SketchFinish` re-reads it (`StraitReadback`): the same pairs, over
the rasterized footprint, against the same 15–40 band, and a **complaint** carrying both numbers — what the
plan put between the two islands and what the drawn board does.

**The pairs come from the plan and never from the raster.** Which crossing is the strait is a fact about the
board's roles and its build regions, and a rasterized footprint carries neither: `opus5-aerie` has six
landmasses and fifteen gaps between them, of which one is a strait and the rest are authored water — so
judging every pair against the band would report eleven faults on a board that has none. Taking the plan's own
answer is what keeps this to the crossing `CT12` judged. Only a pair the plan **passed** is re-read, since a
strait already out of band is the plan's own finding and saying it twice would read as two faults; and a board
with no wool, or a symmetry that is not a mirror pair, is not this rule's to judge in the first place.

Spawn-role and wool-room-role pieces are additionally emitted as **structural shapes** — locked annotation
rectangles tagged with their role and an `intentRef` back to the entity they belong to — so a room stays
visible in the Sketch tool instead of dissolving into the fused group. The authored image also pins its
group's relief solve at the piece's own surface, so the ground is solved knowing the floor must arrive there.

**The intent** (`MapIntent`) is the gameplay. Teams come from the red/blue/yellow/green palette in orbit order.
Each spawn fans to one per team, protected by its whole piece rather than the stamped cube, with its yaw fanned
from the facing. Each wool fans team-outer, its room region the whole wool-room piece, its entries the room's
land seams and build-zone frontages. Destroyables and cores fan the same way but **only at orbit order 2**:
PGM marks a goal shared whenever the team count is not two, and what a shared DTM goal should play like is
undecided, so outside order 2 the validator refuses the plan and the preview declines to draw it. The build
intent carries the fanned build-zone areas and holes, and **no** `MaxHeight` — the ceiling is the world
build's to measure; water lanes fan
into their own list and stay out of the build intent. The observer sits at `(0, observerY, 0)`.

The intent also carries the **structure directives** the world export stamps verbatim, computed on the
authored unit and fanned in absolute block coordinates: the entrance redstone row inside each room along
every entry seam, iron cubes, and the approach walls. On the way
out of `POST /api/plan/compile` the endpoint additionally pre-fills island team ownership — a spawn's team owns
its island, else a wool's owner, else neutral — so Configure opens pre-assigned.

## Phases

### Info

Two steps, and only on the map-backed route. **Identity** is the display name and the authors, loaded once from
`GET /api/map/{slug}` and saved with `PATCH /api/map/{slug}/metadata`; the name is live-synced into the plan
document as it is typed because the compile reads it. **Settings** is the globals form — symmetry, cell size,
base surface, surface step, max players — writing straight through to the live document. Continue on
the last step advances to Draw. A blank map-backed plan opens here (`?phase=info`); an existing one opens on
Draw.

### Draw

The canvas, and where a plan is actually authored. The Draw workspace stays mounted while Info is up, so the
document and the zoom survive the trip.

Four families of drawing tool, one armed at a time, each remembering the option last picked from it. **Terrain**
draws a piece in the armed role; **technical** draws a build zone, a water lane, or a buffer; **markers** drop a
spawn, wool, iron, destroyable, core, or cycle a wall; **boxes** draw an envelope in the armed kind. The
destroyable and core tools are offered only when the symmetry's order is 2, and switching to a symmetry that is
not order 2 disarms them. `V`, `H`, `R`, `Z`, `G` and `W` arm select, pan, piece, zone, box and wall
respectively, and `F` fits the plan to the window.

Drawing a piece, zone or box is a click-drag over cells, always at least 1×1; the id is minted from the role
(`piece`, `spawn`, `wool`, `buffer`, `zone`, `lane`, `<kind>-box`) and the tool reverts to select. A marker is
placed by clicking a piece — a click over empty grid does nothing — and also reverts to select. The wall tool
stays armed, and each click toggles the nearest land interface within one cell between no wall and a wall —
which face its chests open on is derived from the seam, not chosen here.

Selection is two-level, the same scope model the Sketch tool's groups use. A plain click picks the marker
under the cursor first (markers paint on top and have a small hit radius), then the smallest containing box,
then the piece, then the zone. `Ctrl`/`⌘`+click reaches the piece past the box, entering the box as the scope
in the same motion; `Alt`+click leaves any scope and resolves at the box, never drilling into what it groups.
`Enter` enters a selected box as a scope from the keyboard; once entered, a click reaches the pieces it groups
and a click outside it leaves the scope before landing normally. `Escape` leaves an entered scope, selecting
the box itself, and a further press clears the selection; Delete or Backspace removes it outright. Nothing
here is a double-click. Dragging a box carries the pieces it groups, with membership resolved at
grab time so nothing falls out mid-drag.

The tools are armed from the keyboard as well as the toolbar — `V` select, `H` pan, `R` piece, `Z` zone, `G`
box, `W` wall — and `F` fits the plan, `Ctrl`/`⌘`+`S` saves it. `?` lists every chord that is live and
`Ctrl`/`⌘`+`K` runs one by name; both are drawn from the registry described in
`docs/client/canvas-interaction.md` §9, so neither can name a chord this tool does not answer. A selected piece or zone wears the transform
box every authoring surface uses (`docs/client/canvas-interaction.md` §5) — an anchor on each corner and a
grab band along each edge that stretches that one axis — each keeping the extent at least one cell, and how
big it is as a pill under it, the same pill Configure draws under a region and the sketch under a shape. Clicking an already-selected spawn cycles its facing. Deleting a piece takes its
markers, its cliff and wall marks, and its name out of any box member list with it.

The inspector edits the selection: a piece's id, role, surface (stepped by the surface-step preference) and
mirror flag; a zone's id; a box's id, kind and whether its membership is frozen to a list; a marker's position,
a spawn's facing, a wool's dye, and every structure field of a destroyable or a core — each shown at its
*effective* value, so an unset field renders as the number the stamper will use rather than as blank. The dye
is the one field with no effective value to show: a wool that states no colour has one resolved against its
team and the wools placed before it, which no single marker knows, so the picker offers *auto* as a word
beside the sixteen dyes rather than naming a colour the compiler might not pick.

Three panels share the sidebar. **Settings** holds the globals, the tracing reference and the overlay toggles
(land interfaces, frontline edges, labels, and a height-map fill that tints pieces by surface).
**Validation** shows the evaluator's score and every fired rule, and clicking a row isolates that rule's
evidence on the canvas. **Feasibility** shows the producibility read per box, and clicking a box that nothing
reproduces paints its nearest miss — the cells a candidate emits that the box does not, and the cells the box
has that it does not. Each panel owns its overlay and drops it on leaving. All three feeds are debounced by
300 ms after an edit and guard against stale responses.

A read-only 3-D preview draws **the world the plan compiles to**, not an extrusion of its pieces: entering it
posts the document to `/api/plan/columns`, which compiles and builds it and answers every column's solid runs,
and the browser meshes those. A compiled plan carries a full intent, so the wool cages, spawn cubes, monuments
and build region stand in the picture along with the painted ground — a plan's preview shows more than a
sketch's, because a sketch states no objectives to show. It can be rotated in 90° steps, and falls back to 2-D
and disables itself where WebGL is unavailable.

The compile and build cost about a second, so the fetch happens on entering the preview rather than on every
edit; the view swaps at once and fills in when the columns land. It shares its renderer and its payload with
the sketch tool's, which `sketch.md` describes.

The compile drawer is the exit. It posts the document to `/api/plan/compile` and shows the plan, the compiled
layout and the compiled intent as downloadable panes, or the structural findings that blocked it — each
clickable to pulse its subjects on the canvas. Below them, the build button runs the whole chain. On a map that
already holds a sketch or a world it asks first, because the same click means either originating the map or
replacing a board someone has since been working on.

**The button under the panes reads the compile, not the map.** *Rebuild this map* / *Build the map* / *Create
draft* is what it says in the one state where it can act; a compile that has not run yet reads *Compile
first*, one that was refused reads *Fix N blocking problems first* — the count of the findings listed
directly above it — and one that failed to answer reads *The compile failed*. The hammer goes with the word:
an icon for the act is wrong on a label that refuses it.

**Compile does not open until the plan document has arrived.** The canvas and the toolbar are in the DOM
before the document is, so both entries — the topbar button and the Draw phase's Next — read *Loading…* and
stay disabled until the load settles. Without that gate a fast click on a map reached by an in-app hop posts
the editor's blank default and is answered `422` `PL1`, *this plan has no pieces*, about a board that has
them.

## Refusals and complaints

Four separate questions are asked of a plan, and they are kept apart on purpose. Only the first two stop
anything.

A refusal comes back as a list of findings, each naming the rule where it has one and the ids it indicts —
moving the example's destroyable onto the wool-room piece answers:

```json
{ "error": "plan not compilable",
  "message": "destroyable 'destroyable-1' on 'wool-room' is 3×3 and reaches into the wool room on 'wool-room' — the room's own rules would cover the goal",
  "findings": [
    { "rule": "OB17", "severity": "refusal",
      "message": "destroyable 'destroyable-1' on 'wool-room' is 3×3 and reaches into the wool room on 'wool-room' — the room's own rules would cover the goal",
      "subjects": ["destroyable-1", "wool-room", "wool-room"] } ] }
```

Every gate in the studio answers in that shape, and the rule ids are catalogued in `docs/refusals.md`.

**Structural errors** (`PlanValidator.Check`) block a compile with 422, each citing a `PL*` rule where the check is the plan's own and the document's own id where it is not — `DC1`/`DC2` for a core, `OB14` for a two-team goal, `OB17` for a goal footprint, `WX*` for a room frame. They are: overlapping pieces at
different surfaces; a placement referencing an unknown piece, a buffer, or a position outside its piece — a
destroyable or a core with an empty `piece` is not this error, since an absolute goal names no piece to be
unknown or outside of; a core with `float` set without `leak` or the reverse; a core lava footprint or height
outside the range a core is chosen from; a destroyable style that names nothing; a wool colour that names no
dye; destroyables or cores on a symmetry that is not order 2; a
wall on a pair that shares no land interface; a connected landmass mixing fanned and non-fanned pieces — the
fan copies whole groups, so a `mirrors: false` piece must form its own group rather than touch mirrored
land; a room-frame refusal on a role piece (too small for its shell, a
non-square pad, a wool room
with no entry, a spawn room that cannot seat every monument its team will capture); a wool unreachable from a
capturing team's spawn; and a wool reachable only through a spawn piece.

The goal-placement rules deserve naming separately, because they are judged on the structure's **footprint**
rather than its marker, so a marker legally inside its piece can still be refused. A destroyable or a core may
stand on any piece where ground exists, or on none at all — it needs no room, no dead-end lane and no
protection region — but its footprint may not overhang the void, where the build slice's deny rule would make
its blocks unbreakable; may not reach into a spawn room, whose protection denies breaking to every team
including the attackers, making the map unwinnable with nothing anywhere reporting it; and may not reach into a
wool room, whose own rules would cover it. An absolutely-placed goal carries no plan-level ground truth to
judge this against, so the compile gate is silent about it and the export gate — over the ground the
rasterizer actually produced — is the one that answers.

**Completeness** (`PlanValidator.Completeness`) asks whether the plan carries what a map cannot exist without,
and is checked only at the compile gate — a plan under construction is legitimately incomplete. No generating
piece is an error and is reported alone. No spawn is an error: PGM would have nowhere to put a player. No
objective of any kind is a **lint**, returned on the success response as a warning, because which goal a map
carries is the author's and one can still be set in Configure.

**Lint** never blocks. The table is `PlanValidator.LintRules`, each entry citing a rule id: `PC-C` (a corner
contact between otherwise-separate areas is not a connection), `G2` (a zone narrower than the 10-block corridor
minimum), `G5` (a void hop outside 10–20), `SP2` (a spawn not near the back of its lane), `BZ5` (a zone of
either kind touching a spawn piece), `WL1` (a water lane covering terrain instead of void), `EL1` (a piece's
surface delta from the base not a multiple of 2), `ST2` (iron outside the spawn piece on a board that has one), `WX4` (a pad
shifted inward for wall clearance, moving the exported point with it) and `WX8` (an iron marker beside a spawn
room that cannot be placed at all). The piece-interface set quantifies over one shared read
(`PieceInterfaces`, aggregating the contact graph and the board deriver) rather than private geometry:
`SP8` (a spawn egress stepping Δ≥2 ahead of the door), `WL11` (the same step at a wool room's entry, measured
at every one of them since a room has no facing, and met by the **attacker** — a team is kept out of its own
wool), `SP9` (a door with under 15 blocks of ground or bridgeable zone before the void), `ST8` (an approach wall over an interface outside 10–20 blocks, or seated
outside ~15 in front of the wool room's entrance), `ST9` (a wool-room or spawn piece over 20×20 blocks),
`BZ11` (several zones stitching one rectangular region a single zone would have drawn), `FR9` (a crossing
under fifteen blocks of frontline), `FR8` (a crossing turned into frontline over less than a third of the
face it docks against) and `CT12` (a two-team wool
board's direct team-island strait outside 15–40 blocks — read here over the plan's rectangles, and again over
the drawn board at the finish, *What a compile produces*). The whole lint table now rides `/api/plan/evaluate`'s response as `lint[]` beside
the scored terms — the compile endpoint returns errors alone, so the one call an authoring loop already makes
is where every complaint, an unplaceable iron included, becomes visible.

**The evaluator's `valid` is not the compile's.** `/api/plan/evaluate` is a critic built to rank composed
candidates, and it promotes some of the lint above to hard terms — so a board that compiles cleanly can come
back `valid: false` with a large score, and nothing about that stops it being built. The Ruediger map is the
demonstration: no structural error, and an evaluation of `score` 2002 with four fired terms — `G2` and `G5`
hard, `FR4` (frontline count 8 outside the authored band 1–7) and `LN1` (lane width 30 outside 10–20) soft.
An agent should read the score as advice about a board's *shape* and the compile's 422 as the only refusal.

**The one document they must not disagree about is the empty one.** `{"pieces": []}` used to come back
`score 0, valid: true, violations: []` from the evaluator and `producible: true` from the feasibility read,
while `/plan/compile` refused the same body outright with `PL1` — *this plan has no pieces, there is no land to
build*. Every check in both reads quantifies over geometry, so with none they are all vacuously satisfied, and
the shape of that answer is the shape of a perfect plan. Both now answer `PL1` in the validator's own sentence,
cited rather than restated. It is `Completeness`'s finding rather than `Check`'s, and only that one: a plan
under construction is legitimately missing a spawn and an objective, and saying so on every keystroke is what
the split between the two exists to avoid. `PL1` is the finding that is not about being unfinished — it says
the document is empty — and `Completeness` reports it alone, returning before it asks anything else.

## The API

Every endpoint is anonymous and rooted at `/api`. Inspect, evaluate, feasibility and compile all take a plan
document as the body and need no map, which is what lets a plan be checked before it is stored anywhere.

**Originating and storing**

| Endpoint | Body | Answers | Fails with |
|---|---|---|---|
| `POST /plan` | `{name?}` | `{slug}` — a new `map` row at `stage=plan`, gamemode `ctw`, empty plan artifact | — |
| `POST /plan/{planId}/author` | — | `{slug}` — a map row seeded from a generator candidate | 404 unknown candidate |
| `GET /map/{slug}/plan` | — | the stored document, or `{}` | 404 unknown map |
| `PUT /map/{slug}/plan` | the document | `{}` — a verbatim replace; `warnings` carries any field the plan reader has nowhere to keep (`RQ3`), which the blob would otherwise store and nothing downstream would read. The `ETag` is the revision it landed at | 400 non-JSON · **409 `RQ5`** an `If-Match` naming a revision the plan is no longer at · 404 unknown map |
| `GET /map/{slug}/state` | — | `{stage, artifacts, moves[]}` — where the map has got to, which documents it holds, and what may be done to it from here. Each move is `{does, route, next}`; several are open at once and `next` marks the ones the stage is waiting on | 404 |
| `GET /map/{slug}/findings` | — | `{stage, findings[], unasked[], refuses}` — everything wrong with the map right now, from every gate its stored documents can answer, plus the gates a read cannot reach and the route that does pay for them | 404 |
| `GET /map/{slug}/plan/ascii[?every=N]` | — | `text/plain` — the fanned board as a grid of characters, one per proxy cell, with a key. `every` draws one character per N cells for a board wider than a terminal | 404 unknown map or no plan · 422 stored plan unreadable |
| `GET /map/{slug}/plan/flow` | — | `text/plain` — how the board is come at and what that leaves unused: each objective's two walks and the ratio between them, where the ways in part and meet, whether the defence shares the attackers' road, and the ground no journey reaches, named with its pieces | 404 · 422 |
| `GET /map/{slug}` · `PATCH /map/{slug}/metadata` | `{name, authors[]}` | the map's identity | 404 |

**The candidate pool** (the bare `/plan-editor` route)

| Endpoint | Body | Answers | Fails with |
|---|---|---|---|
| `GET /plans[?origin=generated\|authored\|imported]` | — | summaries, newest touched first, each with its composer descriptor and whether that descriptor still reproduces it | — |
| `GET /plans/{id}` | — | the row plus its `planJson` | 404 |
| `GET /plans/{id}/ascii[?every=N]` | — | the same grid for a candidate, by id rather than slug | 404 · 422 |
| `POST /plans` | `{planJson, sourceId?}` | the saved row — an authored source is updated in place, a generated or imported one forks into a new authored row; `warnings` carries any field the plan reader has nowhere to keep (`RQ3`) | 400 malformed plan |
| `DELETE /plans/{id}` | — | 204; forks survive with a null parent | — |

**The flow.** A render says *where* ground is dead; only the flow says *why*, and a model handed a red
rectangle with no account of it will move the rectangle rather than the reason. `GET /map/{slug}/plan/flow`
answers that account in prose, off the plan alone, so it costs no build and can be read before the picture.

For each enemy objective it states the attacker's walk and the defender's, and the ratio between them — the
quantity the corpus reads match length off before any other geometry (`match-flow.md` §6.10). It says how many
ways in there are, where they part and where they meet again, and how far short of the objective that merge
sits. Then the relation that decides whether a board can be held: whether the defender's own shortest walk
runs **through** that merge, so both sides come up the same road and the defence must be pushed forward to
hold it, or whether going round is shorter, so the defence arrives **from behind** the objective while the
attack arrives at its front. It flags a split or a merge narrower than `TightPassage` (8 blocks), which is a
doorway rather than a place to fight over.

Routes are a **capture board's** question. A wool is carried back, so the same ground is walked out and in and
the two sides meet somewhere definite. A destroy board has no carry, so it gets the ground read and no
invented flow.

**The grid.** A plan is a list of rectangles measured in cells, and most of what goes wrong with one is a
relation between two of them — a landform wider than the band that reaches it, a wall on the only throat, a
room whose door opens onto its own apron. A render of the built world cannot show a relation between two
rectangles, because by then they are terrain; a grid puts them on the same rows and the relation is one
glance. `GET /map/{slug}/plan/ascii` answers it as `text/plain`, which is also the one render a caller with
no image reader can act on. Terrain takes a letter per piece, a build zone is `+`, a water lane `~`, an
enclosed void `o`, open void a space, and markers overprint their ground.

**The grid and the flow read the stored plan, and neither refuses anything**, which is what makes them the
two an authoring loop forgets: a call that cannot fail is a call nobody is made to run. So they are on the
driver rather than left to be remembered — `pgm-studio-mapgen`'s `tools/drive.py` prints both between the
`PUT …/plan` and the compile, which is the first moment either can answer and the last one before a board's
shape is committed to a world.

**Reading a plan.** All of these take **the plan document itself as the body** — unwrapped, exactly the shape
`GET /map/{slug}/plan` hands back — and store nothing, so they can be asked of a document that has never been
posted anywhere. That is what makes them the cheapest way to find out whether a board is well formed.

**Six routes take a posted plan, and they answer three kinds of thing.** Reading five summaries in a row
does not say which, so it is said here. One **transforms**: `compile` turns a plan into the layout and intent
a map is built from, and is the only one whose answer a caller acts on. Two **judge**:
`evaluate` scores the board against the rule law, and `feasibility` judges the *composer* — could it have
produced this plan — which is a statement about what the generator cannot do yet rather than about the board,
and is why it is tagged `Diagnostics` and kept out of the list a driver iterates through. Three **project**:
`inspect` derives the geometry a canvas draws, `columns` builds the world the plan would make, and `ascii`
draws the board as characters.

```json POST /api/plan/inspect
{"globals": {"cell": 5, "symmetry": "rot_180"}, "pieces": []}
```

| Endpoint | Answers | Fails with |
|---|---|---|
| `POST /plan/inspect` | `{interfaces, gapLinks, frontline, frontages, frontlineRuns, islandGaps, structures, goalDistances, goalPairs}` — the derived geometry, already in block coordinates: each interface with its `delta` (the surface step across it) and wall mark; the per-piece-side `frontages` (exposed blocks, frontline blocks, share — FR8's read); the `frontlineRuns` with widths in blocks (`FR9`'s fifteen-block floor is read off the same frontages); the `islandGaps` (each bridged pair's strait in blocks, `direct` when no third landmass shares the region — CT12's read); plus the destroy-goal walks, all of them blocks over the fanned closure: `goalDistances` is each goal's walk to its own and the enemy's spawn with the enemy÷own ratio — the numbers `goal-spawn-ratio` scores against GO1's band [3.0, 4.0] and `goal-spawn-distance` against GO4's [40, 90] — and `goalPairs` is the walk between the goals themselves, each unordered pair once, `opposing` false for a pair one team defends (GO2, [35, 65]) and true for a goal against one the other team defends (GO3, [85, 150]), a monument against its own mirror being the pair every symmetric board carries. Never withholds over structural errors; a failure degrades `structures` and the board aggregations to empty rather than failing the feed | 400 malformed or unreadable |
| `POST /plan/evaluate` | `{score, valid, violations[], lint[]}` — score summed and lower-is-better, `valid` true when no hard term fired, violations hard-first with subjects and drawable evidence, and `lint` the structural validator's complaints (an unplaceable iron `WX8`, a mid-lane spawn `SP2`, an odd elevation step `EL1`, …), which never move the score. A plan with no generating piece answers `valid: false` carrying `PL1`, not an error and not an empty evaluation | 400 malformed |
| `POST /plan/feasibility` | **a diagnostic, not a verdict on the board.** `{producible, boxes[], unit[]}` — per-box producibility, each naming the parameter tuple that reproduces it or the nearest miss and why, and findings citing the task that would unblock each gap. A plan without boxes reads empty; a plan without pieces reads `producible: false` with `PL1` in `unit`. Acting on one of these as though it were a fault in the plan means editing a board to satisfy a limitation that is the studio's | 400 malformed |
| `POST /plan/ascii[?every=N]` | `text/plain` — the fanned board as a grid of characters, one per proxy cell, with a key. **The read that shows a relation between two rectangles**, which no number can: a sixteen-cell bar reached by a four-cell build zone is a landform 60% dead, visible at a glance here and invisible in every other read of the same board. `every` draws one character per N cells for a board wider than a terminal | 400 malformed |
| `POST /plan/columns` | `{palette, cols, min_x, min_z, max_x, max_z}` — the world the plan compiles to, as per-column runs, in the encoding `sketch.md` documents, plus, under `warnings`, every prop the dressing pass declined (`DR-*`) and everything the compiled layout names that the studio does not have (`SK3`/`SK4`/`SK5`). It compiles and builds, so it is the heaviest read here and the only one that answers what stands above the ground. It does not gate: `/plan/compile` is where a plan is refused, and a preview of an incoherent plan is still worth looking at | 400 malformed or unbuildable |
| `GET /objectives/vocabulary` | the destroyable styles and materials, the wool dyes with their labels and swatches, and every objective default | — |

**Compiling and building**

| Endpoint | Body | Answers | Fails with |
|---|---|---|---|
| `POST /plan/compile` | the document | `{layout, intent}`, each half serialized with its consumer's options so both can be posted on verbatim; `warnings` rides beside them where the compile is complete enough to succeed and incomplete enough to remark on (today `PL3`, a map with no objective), and where the posted plan carried a field the reader has nowhere to keep (`RQ3`) | 422 `{findings}` structural or completeness errors · 400 malformed |
| `POST /sketch` | `{name}` | `{slug}` — originates a map; only needed off the bare route | — |
| `PUT /map/{slug}/sketch/from-plan` | the compiled `layout` | `{orphaned}` — merges rather than replaces: the sketch's themes, room shells and dressing are carried onto the new board, and a structural piece's author-corrected height is carried by `intentRef`. `warnings` rides beside them: what the merged document names and does not have (`SK3`/`SK4`/`SK5`), the same complaints the plain write answers, and any field of the **posted** layout the reader had nowhere to keep (`RQ3`) | 409 one `SK1` finding per orphaned group, subject = group id (`?force=true` accepts the loss) · 400 · 404 |
| `POST /map/{slug}/sketch/finish` | — | `{slug, configureUrl}` — rasterizes the layout into world geometry and moves the map to `stage=configure`, answering the stored document's own complaints under `warnings` on the way through | 404 unknown map · 422 the layout rasterizes to no ground · 422 `SK2` |
| `PUT /map/{slug}/intent/from-plan` | the compiled `intent` | the projected map — carries the stored **authors and contributors** onto it and nothing else. `symmetry` and `islandTeams` are deliberately not carried, so a rebuild clears both | 404 · **409 `RQ5`** a stale `If-Match` · 422 the stored map will not carry the projection |
| `GET /map/{slug}/export` | — | the world ZIP | 404 unknown map · 409 and 422 as `/xml`, plus non-2xx with a message on a zip/IO failure |

## Driving it without the UI

An agent authoring a plan writes the document itself and never touches the canvas. The whole loop is six calls.

```
POST   /api/plan                      {"name": "Voidwatch"}      → {"slug": "voidwatch"}
PUT    /api/map/voidwatch/plan        <the plan document>        → {}
POST   /api/plan/compile              <the plan document>        → {layout, intent} (+ warnings)
PUT    /api/map/voidwatch/sketch/from-plan   <layout verbatim>
POST   /api/map/voidwatch/sketch/finish
PUT    /api/map/voidwatch/intent/from-plan   <intent verbatim>
```

`POST /api/plan/evaluate` and `POST /api/plan/feasibility` may be called on the document at any point before
the compile, with no map in existence, and are the cheapest way to find out whether a board is well-formed.
`GET /api/map/{slug}/state` before the build says whether this is an origination or a rebuild, and what
else may be done from here. `GET /api/map/{slug}/export` afterwards returns the world.

**Two reads make the loop act-then-ask rather than act-and-hope.** Every other gate is reached through the
step it lives behind, so a fault authored at one step is heard at another — a plan whose objectives cannot be
placed is heard at the compile, a layout that rasterizes to nothing at the finish.
`GET /api/map/{slug}/findings` asks all of them at once, off the stored documents, and calls the same methods
those steps call rather than restating them. It does not build: the export gates need the rasterized world,
which is seconds a read would spend on every call, so each is named in `unasked` with the route that does pay
— a list silent about what it skipped would read as *nothing is wrong*. `GET /api/map/{slug}/state` is the
other half: what may be done next, with the route for each.

**Two further calls belong after the intent, and the order is load-bearing.**
`POST /api/map/{slug}/sketch/columns` answers every prop the dressing pass declined, under `warnings` — and
`DR-KEEP` among them reads the spawn doors' approaches and the goal rings, which come off the **intent**, so
the same call asked before it answers a shorter list. `PATCH /api/map/{slug}/metadata` is where the map's
authors are set, and it has to follow for a different reason: storing an intent projects the document from
the intent's own `meta`, whose `authors` a compiled intent leaves empty, so a name written earlier is
overwritten rather than kept. `intent/from-plan` carries authors from a **previously stored intent**, which a
first build does not have.

The smallest plan that survives the gate needs one generating piece, one spawn marker, and — for a CTW map —
a wool that is reachable from every capturing team's spawn by a route that does not pass through a spawn piece.
`tools/seeds/base-2wool.plan.json` is that plan at its plainest.

**`tools/seeds/ruediger.plan.json` is the one to read first.** It is the plan behind a hand-authored CTW map
that was carried the whole way to a finished world, and it sits beside the two files it produced —
`ruediger.layout.json` and `ruediger.intent.json`. Thirty-one pieces at ten surfaces, seven build zones, one
spawn-role piece with a spawn marker facing `left`, one wool-room piece with a wool marker, one approach wall
between `wool-a-t1` and `wool-a-t3`, `rot_180` at 30 players. It compiles to 25 shapes on one group framed
`−70..70 × −130..130`, two teams, two spawns, two wools, 14 build areas, the observer at y 24, and four
structure directives — two entrance redstone rows, two walls. It states no build ceiling,
because none of them do: that is measured off the terrain the world build produces.

Its `ruediger.layout.json` is worth reading for the **theme** as much as the geometry, and it is the reason
the two are kept together. All three of its themes were written in shapes the model has since retired, and
one of them cost something: the surface voronoi stated a `palette` of fills to pick between, which the reader
upgrades by carrying the first entry and dropping the rest, so a three-material surface painted one flat band
of sand for as long as the file said so. Nothing failed — a stored document that needs an upgrade to load is
exactly the thing that keeps loading. It now states its own bands: sand on the cell boundary, sandstone the
ring, birch planks the middle, over twelve-block cells with a two-deep rim, which is what it takes for a ramp
measured inward from the boundary to read as cells at all.

That plan also shows what lint is worth. It carries **sixteen findings and no errors**: eleven `EL1` (its
elevation steps in ones, not the twos the stepper defaults to), two `G2` (five-block zones) and one `G5` (a
35-block hop). None of them stopped a good map being
built, which is the difference between the lint table and the refusals above.

Reading the trio together shows exactly where this tool stops. The compile emitted the twenty-one polygons
`s0`–`s20` and the four structural rectangles `spawn-red`, `spawn-blue`, `wool-red-red` and `wool-blue-blue`;
everything else in `ruediger.layout.json` was added afterwards in the Sketch tool — one carved shape with a
sketch-minted id, Bézier controls on five outlines, and a theme named on seventeen of the twenty-six shapes.
The tiers are the plan's. The curves, the carve and the paint are not, and no plan can state them.

Two things bite an agent writing a document by hand. Rect arithmetic is in cells and pieces must genuinely
abut to connect — a corner touch is not a connection, and a seam under 10 blocks is a narrow one. And a wool
room is a cage: it wants an entry seam, a pad that fits its shell, and a spawn room large enough to seat one
monument per wool its team will capture, all of which are refusals rather than warnings.

## Limits

**Plan draws rectangles on a coarse grid, and that is the whole of its geometry.** Every footprint is an
axis-aligned cell rect, so the smallest thing a plan can say is one cell wide — five blocks at the default
scale — and nothing it draws has a diagonal, a curve or a notch. There is no polygon, no cut, no lasso.
Refining the shape of the ground is the Sketch tool's, which takes the compiled layout and works on the fused
outline. Height is the one property a plan states finely, and it states one flat surface per piece: valleys,
slopes, contours and any interior elevation belong to the relief phase downstream.

**A plan states no stack.** The plan model is flat by decision rather than by omission — a storey is a fact
about ground that has been drawn, and the sketch is where ground is drawn — so `layers[]` has no counterpart
here and a compiled plan emits exactly one, id `ground`, at `base_y` 0. That the walk's node is a *place*
rather than a cell costs a plan board nothing: it simply has one place per cell.

It authors no paint and no dressing. Themes, styles, trees, paths, ponds, rocks and houses are all the Sketch
tool's; a plan carrying theme keys has them dropped on parse.

It does not place the observer spawn. The compiled intent puts the observer at `x=0, z=0` at `observerY`, and
the Configure tool is where an observer spawn is actually authored — there is no observer marker on the plan
canvas, and no control writes `observerY`.

It does not validate the finished map. The compile gate refuses the structural faults listed above and nothing
else; the map's real pre-flight, its region tree and its XML are Configure's.

Destroy objectives are offered only at symmetry order 2, and a hand-written plan that places them elsewhere is
refused rather than compiled.

Once a plan has been built, the structural pieces it projected into the sketch are read-only there. A shape's
stated height can be corrected and survives a recompile, but selecting, moving or reshaping a spawn or wool
room in the Sketch tool is not yet reachable (`B107`), and a destroy objective has no sketch presence at all.
