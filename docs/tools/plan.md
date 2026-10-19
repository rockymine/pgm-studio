# The plan

## What it is

A plan is a map's board at its coarsest scale: rectangles on a proxy grid, and the play pieces the finished map
is played by, written as one document. It is neither a tool nor a stage. A map's one document is its sketch
(`docs/tools/sketch.md`); a plan comes into a map only through the **one-way compile**, which turns it into the
layout and the intent a map is built from, and the composer writes one internally on the way to the sketch it
hands over (`docs/tools/generator.md`).

A plan is authored as **one symmetry unit**. Everything stated belongs to team 0; the compiler fans it into the
other teams' images by the plan's symmetry mode. Nothing in the document is per-team, and there is no way to
give one team a different board from another.

**Every stored plan is history.** A map that was built from a plan keeps it as its `plan_json` artifact, and the
`plan` table keeps the rows the generator's candidates and imported plans were once stored as. Both are read
back exactly as stored — `GET /map/{slug}/plan`, `GET /plans`, `GET /plans/{id}` and its renders — and nothing
else reads them: a map's findings are asked of its intent and its sketch, never of the plan it came from. A map
that was planned and never drawn is a sketch like a fresh draft — its ground layer with nothing on it, its plan
beside it.

## What it writes

The compile writes nothing. It answers the layout and the intent as `{layout, intent}`, and a caller stores
them as the `layout` and `intent` of the map's source (`PUT /map/{slug}/source`, `docs/tools/flow.md`), which
builds the map in one call.

## The plan document

The wire format is `*.plan.json`, modelled by `PlanModel` (`src/PgmStudio.Pgm/Plan/PlanModel.cs`). It
normalises on load:
unknown piece roles, box kinds and zone kinds fold to their canonical value, and every marker without an id
is given one. A document written under an older vocabulary therefore loads cleanly rather than failing.

Here is one carrying every element the format has, which compiles clean — no errors and no warnings. That
claim is a test: `DocumentedBodyTests` posts this block to the route named on its fence and fails if the
compile ever answers anything else.

```json POST /api/plan/compile
{
  "plan": 2,
  "meta": { "name": "Example board", "authors": ["Example author"],
            "contributors": [{ "name": "Example helper", "contribution": "relief" }] },
  "globals": { "cell": 4, "symmetry": "rot_180", "maxPlayers": 12, "surface": 9 },
  "pieces": [
    { "id": "spawn",      "role": "spawn",     "rect": [1, 9, 3, 2] },
    { "id": "lane",       "role": "piece",     "rect": [1, 5, 3, 4] },
    { "id": "approach",   "role": "piece",     "rect": [-4, 4, 3, 7] },
    { "id": "wool-room",  "role": "wool-room", "rect": [-4, 11, 3, 2] },
    { "id": "plateau",    "role": "piece",     "rect": [5, 7, 4, 2], "surface": 13 },
    { "id": "bridgehead", "role": "piece",     "rect": [-1, 7, 2, 2] },
    { "id": "gap",        "role": "buffer",    "rect": [4, 5, 2, 2] }
  ],
  "zones": [
    { "id": "mid-band", "rect": [-3, -5, 6, 10], "holes": [] },
    { "id": "lane-e",   "rect": [3, -1, 2, 4], "kind": "water-lane" }
  ],
  "placements": {
    "spawns":       [ { "id": "spawn-1", "piece": "spawn", "at": [6, 4], "facing": "front",
                        "footprint": [1, 1, 10, 6] } ],
    "wools":        [ { "id": "wool-1", "piece": "wool-room", "at": [6, 4],
                        "footprint": [1, 1, 10, 6] } ],
    "iron":         [ { "id": "iron-1", "piece": "spawn", "at": [2.5, 2.5] } ],
    "destroyables": [ { "id": "destroyable-1", "piece": "plateau", "at": [10, 5],
                        "style": "cube-3", "materials": "obsidian", "float": 4 } ],
    "cores":        [ { "id": "core-1", "piece": "approach", "at": [5, 25], "layer": "undercroft",
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

**Cells state the ground and blocks state what stands on it.** A piece, a zone, a buffer and a box are cell
rects — the coarse grid the board is drawn on — and everything placed *inside* a piece is in blocks, because
what stands there is block-resolution and a cell grid cannot say where it goes. A 7-block hall is not
expressible in fifths of itself, and the `WX` rules already state every minimum in blocks and never in cells.

Marker positions are stored **piece-relative**: a marker names the piece it rides and an `at` offset in
**blocks** from that piece's minimum corner, snapped to the **half-block lattice** — the same one the export
snaps to (`PositionSnap.SnapHalfXZ`), so a whole offset is a block grid line and a `.5` a block centre, which
is the parity that sizes the pad (`WX3`). A marker that rides no piece cannot exist: the validator errors on a
marker whose piece is unknown or is a buffer.

**The document states its own shape version**, `plan`, and this build reads **2**. A document stating another
version is refused `PL15` rather than read: version 1 stated a marker's `at` in cells, which is the same
numbers at a different distance, and the unit a coordinate is in is not visible in the coordinate. A document
stating no version at all is read as the current one.

**A destroyable and a core are the one exception.** `piece` may be empty, and `at` then reads as an
**absolute** block offset from the symmetry centre — so a
goal can stand on ground that exists only as an authored sketch shape, with no plan piece manufactured to
carry it (`B128`).

### Globals

`globals` is set once and governs the whole board.

| Field | Default | Is |
|---|---|---|
| `cell` | 5 | Blocks per proxy cell — the scale everything else is drawn at. |
| `symmetry` | `rot_180` | `rot_180`, `rot_90`, `mirror_x`, `mirror_z` or `none`. Decides the team count (the orbit order) and how the authored unit is fanned. |
| `maxPlayers` | 12 | The cap **per team** — the export writes it into every team's `max`, so a board's total is this times the team count. Carried into the intent; changeable again in Configure. |
| `surface` | 9 | The base island height in blocks — the Y a piece stands at unless it overrides it. |
| `observerY` | `surface + 15` | The observer's height, as a nominal starting point: the plan states neither the relief that rolls under the centre nor anything drawn over it, so the export seats the platform clear of what the world actually builds there and writes the height it settled on back into the intent (`EX5`). No control writes it; it is honoured when hand-authored, and a stated height that would be buried is seated the same way. |

**The build ceiling is not a global, and that is the correction.** It was `headroom`, a slack added to
`surface`, so the cap was computed from the plan's flat nominal world — a ground level the relief solve then
abandons, which produced boards whose ceiling sat below their own terrain. The author's rule measures it where
the answer exists: **twenty blocks over the mean top of the built terrain columns** (`G6`, amendments 14, 25
and 42) — the ground the match is played on, taken whole rather than at its highest point, with nothing
standing on it in the measure: not a house, not a tree, not a made thing hung in the air, not an objective.
It is derived in `WorldBuilder` as soon as the terrain is laid, and written onto the intent as `MaxHeight`.
A plan-level number would be a second source for one value, and the one that gets overwritten.

**A shape cannot put itself above that line, and this is the part an author has to know before drawing a
tall one.** An erected shape is one of the columns the cap is measured over, so raising a wall of terrain
raises the ceiling that would have capped it — by the same twenty blocks, and for the whole board. Measured
on `pgm-studio-mapgen`'s `alabaster-rake`: five pillars topping at y43 over ground topping at y14 wrote
`<maxbuildheight>64</maxbuildheight>`, twenty-one blocks of clear air over the picket they were meant to
stand above.

So what a tall shape buys is real and narrower than "a wall nobody passes": ground whose top clears a climb
cannot be climbed or walked through, and it holds the line its outline is drawn along, which costs an
attacker the material and the visible time a bridge takes. It is not a barrier bridging cannot answer. A
made layer raises nothing and is the difference between drawing a wall and hanging a sculpture.

Whether erected terrain should be left out of the derivation — which would make an unbridgeable blocker
possible — is a question about how a map plays, and belongs to the author.

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

A **spawn** adds `facing` — one of eight absolute board directions, fanned per orbit image: the four walls,
`front` (−Z), `back` (+Z), `left` (−X), `right` (+X), and the four corners between them, `front-left`,
`front-right`, `back-left`, `back-right`. It says **where the player looks and nothing else**; a diagonal
carries a 45° yaw, which names no wall, and the room's doors are a separate derivation. It is overridden at
compile when it would look out over the void from a board-edge piece — onto the wall the piece meets the
board on, or onto the corner between them where it meets it on two.

The **doors** are not stated. A spawn hall opens through the walls its piece abuts more board on — a land
seam with a neighbour or a build-zone frontage — widest opening first, **at most two**: a corner spawn opens
on both the ways out its team has, and a piece open on three sides is still a hall rather than a crossroads.
Where two walls open equally wide the facing breaks the tie, so the door the player walks out of is the first
one, which is where the chests, the monument slots and the iron cube go. A piece that abuts nothing opens on
the wall its facing leans into. This is the same rule a wool cage's doors follow (`WX6`), differing only in
the cap — a cage takes one door per entry, because every side an attacker can reach it from is a side they
may come through.

The walls are derived once, on the authored unit, and then **turned onto each orbit image** the way the yaw
beside them is: a door is a direction, so the wall an authored hall opens on names a different wall on every
other image, and the words the intent carries are that image's own. A cage says the same thing in a different
shape — its openings ride as the entry rectangles themselves, which the orbit transform maps without anything
having to name a side.

A **spawn** and a **wool** each state a `footprint`: the building on the piece, as `[x, z, w, h]` in blocks
from the piece's minimum corner — the same corner the marker's `at` is measured from. The piece is the region
holding the room and the bounds every marker on it is held to; the footprint is the room itself, so a wide
protection region can carry a small hall. It is fanned like the piece is, so an orbit image raises the same
building on its own ground. Absent leaves it to the resolver's default (`WX1`): the piece inset by one block
on every side, and by up to five in front of a spawn's door, which is an iron cube plus the standing
room it holds to the wall. A footprint smaller than a room can be (`WX2`) or reaching past its piece (`WX12`)
is refused rather than clamped.

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

**`controlPoints` is a count and names no piece**, which makes it the one placement here that is not a
marker. Every other one belongs to a team and therefore to that team's ground; a capture point belongs to
nobody, so it has to be the same walk for everyone and the only positions that are lie on the board's own
axes of symmetry (`docs/gameplay/approaches.md`). The count and the spawn frame give every anchor: the
centre of symmetry, which is its own orbit image and stays one point, and a side point on the **bisector
between two neighbouring spawns** — 90° off the spawn direction on two teams, 45° on four — at the author's
share of the way out, **0.66** of the centre-to-spawn distance on two teams and **0.90** on four. The orbit
fans that single side primary into the matched pair or the ring of four, the same way it fans every other
marker.

So the counts a board can state are the ones its own symmetry lays out: **1, one per team, or one per team
plus a centre** — 1, 2 or 3 on two teams, with three the ordinary board, and 1, 4 or 5 on four. Any other
count is `PL16`, and the compiler places none rather than rounding to a number it can. A board carrying a
count is a capture board to `PL3` like any other objective, and the score limit the export writes beside the
points defaults to 750 (`docs/pgm/control-points.md`).

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
wall: a bedrock barrier two blocks thick and three courses tall down the middle of the interface, its full
width, which slows a wool raid and gives the defence a prepared line. The compile stands its defence chests one
block out from one face, and which face is **not authored**: it is the **approach** face — the side further
from the wool, which is the side both teams reach the line across.

The wall's top is one level, three courses over the **highest** solved ground along its run. Per column it
would step with the terrain and read as a curve, and from the average it would be buried wherever the ground
rises past the mean, so the highest is the only level that is both straight and never under the terrain. Where
that leaves the wall standing more than four courses over its lowest column the export complains (`ST4`): the
seam's own fall is added to the wall's face, past which it stops reading as a line to hold.

A wall on a pair that shares no land interface is an error (`PL11`), and so is a wall on
the wool room's own interface (`PL13`) — the wall and the room would stamp through each other, so the device
belongs an approach out, around 15 blocks from the room.

A wall with land running past either of its ends draws a complaint (`PL17`) naming the piece that holds it.
The wall spans only the interval the two pieces share, so ground one block beyond an end, on either face, is
ground a player stands on beside the wall and rounds the line from, with one step off the corner instead of
crossing it. That ground is either one of the two walled pieces running past the shared interval, or a third
piece the wall stands against — a wall where an arm meets a hub side of its own width is flanked by the hub's
pieces in front of and behind that side. The fix is a piece the lane's own width between the two, with the wall
on that seam: flanked by nothing, the wall has to be crossed, because going round it means leaving the
ground.

### Boxes

`boxes` groups pieces into the partition they realize — a `wool` or `spawn` approach, the `hub` body they seat
on, the `frontline` that fronts it, the `mid` between the fanned images. A box is drawn around its members
rather than filled by them, boxes may overlap, and membership is by containment (every generating piece wholly
inside the rect) unless `members` names the pieces explicitly.

Boxes are **authoring annotation only**. The compiler, the validator and the derivers ignore them, so drawing
one can never change what a plan builds; they are how a generated board explains itself, and the unit the
producibility read reports against. A hand-authored plan does not need them.

**Boxes are the composer's model, carried and never compiled.** They stay in the document as it was written;
a box with an explicit member list names its pieces, and a containment-grouped box groups by what lies inside
it.

### Reference

`reference` records the real map a plan was traced over — its slug, plus an `offset`, `scale` and `opacity`
for placing its top-down render under the grid. Authoring metadata: the compiler never reads it.

### Fields that are dropped

`themes`, `mapTheme` and `themeScopes` are parsed away and lost. Terrain paint moved onto the sketch model,
where the geometry is final; a plan carrying them compiles exactly as one without them.

## What a compile produces

`PlanCompiler.Compile` is pure and deterministic — the same plan always compiles to the same pair — and
one-way: nothing reads a layout or an intent back into a plan.

**The layout** (`SketchLayout`) is the terrain. Pieces joined by land interfaces form components; within a
component each distinct surface becomes its own shape, so a stepped island emits stacked plateaus, and each
shape is the rectilinear union outline of the rects at that height rather than the rects themselves — which is
why abutting pieces of equal height arrive in the Sketch tool as a single polygon. Buffers become `subtract`
shapes over whatever no generating piece covers. Islands are mirror groups: everything fanned lands in `team`,
everything on-axis in `neutral`. The framing box is the extent of every terrain shape fanned across the orbit,
one cell proud.

**A compiled shape is named for the plan, not for where it was emitted.** Its id is its component's
ordinally first piece and the surface it stands at — `bahnhof-30`, `carpark-24` — with the patches past the
first at one surface numbered (`carpark-24-2`), and a buffer's subtract under the buffer's own name
(`gap-cut`). A piece belongs to exactly one component, so the anchor is unique, and it is ordinal rather than
declared order so moving a piece up the file renames nothing.

The id is an **address**: a theme, a relief scope, a bend and a `PATCH /sketch/shapes/{shapeId}` are all
keyed on it. A positional id makes every one of them name a different shape the moment a piece is inserted
anywhere earlier in the plan, which is silent — the board stores, the export gate opens, and the paint is
simply on something else. The ids also make the board legible: `ruediger.plan.json` compiles to `hub-t2-7`
through `hub-t6-16`, which says two components at ten surfaces without opening the file.

**Once the board is drawn, the sketch answers for it.** A finish is free to move what the plan judged — a
shape drawn across the strait bridges it, a quay narrows it — so a stored plan is asked nothing after the
compile: the strait is the sketch's own `CT12`, measured on the drawn ground (`docs/tools/sketch.md`).

**A room is the intent's, and the layout says only how the ground meets it.** The compiled layout holds no
rectangle for a spawn or a wool room: the Sketch tool draws each one from the intent, with its building — the
placement's footprint, or `WX1`'s default where it states none (`docs/tools/sketch.md`). What the compile writes for the authored image of a
spawn-role or wool-room-role piece is its entry in the layout's `rooms` record, under the piece's id — the
piece's surface as its top, `relief_scope: follow`, and the sides its doors stand on — which pins its group's
relief solve at the piece's own surface, so the ground is solved knowing the floor must arrive there.

**The intent** (`MapIntent`) is the gameplay. Teams come from the red/blue/yellow/green palette in orbit order.
Each spawn fans to one per team, with its yaw fanned from the facing; each wool fans team-outer, its entries
the room's land seams and build-zone frontages. Both carry the same two rectangles, and they say different
things: **`Protection`** is the whole piece — the anti-grief region *and* the ground the room is framed on,
rather than the stamped cube — and **`House`** is the building raised on it, one wing over the rectangle the
placement states, or none where it states none and `WX1` defaults one. That is the split `ST9` and `ST10` cap separately. Destroyables and cores fan the same way but **only at orbit order 2**:
PGM marks a goal shared whenever the team count is not two, and what a shared DTM goal should play like is
undecided, so outside order 2 the validator refuses the plan and the preview declines to draw it. The build
intent carries the fanned build-zone areas and holes, and **no** `MaxHeight` — the ceiling is the world
build's to measure; water lanes fan
into their own list and stay out of the build intent. The observer sits at `(0, observerY, 0)`. The intent's
`meta` carries the plan's `meta.name`, `meta.authors` and `meta.contributors` across unchanged — a person is a
bare name or `{name, contribution}`, as in the intent — so a plan that credits someone compiles to an intent
that does.

The intent also carries the **structure directives** the world export stamps verbatim, computed on the
authored unit and fanned in absolute block coordinates: the entrance redstone row inside each room along
every entry seam, iron cubes, and the approach walls. On the way
out of `POST /api/plan/compile` the endpoint additionally pre-fills island team ownership — a spawn's team owns
its island, else a wool's owner, else neutral — so Configure opens pre-assigned.

## Refusals and complaints

Two questions are asked of a plan at the compile, and only they stop it.

A refusal comes back as a list of findings, each naming the rule where it has one and the ids it indicts —
moving the example's destroyable onto the wool-room piece answers:

```json RefusalDto
{ "error": "plan not compilable",
  "message": "destroyable 'destroyable-1' on 'wool-room' is 3×3 and reaches into the wool room on 'wool-room' — the room's own rules would cover the goal",
  "findings": [
    { "rule": "OB17", "severity": "refusal",
      "message": "destroyable 'destroyable-1' on 'wool-room' is 3×3 and reaches into the wool room on 'wool-room' — the room's own rules would cover the goal",
      "subjects": ["destroyable-1", "wool-room", "wool-room"] } ] }
```

Every gate in the studio answers in that shape, and the rule ids are catalogued in `docs/refusals.md`.

**Structural errors** (`PlanValidator.Check`) block a compile with 422, each citing a `PL*` rule where the check is the plan's own and the document's own id where it is not — `DC1`/`DC2` for a core, `OB14` for a two-team goal, `OB17` for a goal footprint, `WX*` for a room frame. `PL15` comes first and alone: a document naming a shape version this build does not read has every coordinate below it measured in the wrong unit, so nothing else is asked of it. The rest are: overlapping pieces at
different surfaces; a placement referencing an unknown piece, a buffer, or a position outside its piece — a
destroyable or a core with an empty `piece` is not this error, since an absolute goal names no piece to be
unknown or outside of; a core with `float` set without `leak` or the reverse; a core lava footprint or height
outside the range a core is chosen from; a destroyable style that names nothing; a wool colour that names no
dye; destroyables or cores on a symmetry that is not order 2; a
wall on a pair that shares no land interface; a connected landmass mixing fanned and non-fanned pieces — the
fan copies whole groups, so a `mirrors: false` piece must form its own group rather than touch mirrored
land; a room-frame refusal on a role piece (too small to hold a room, a
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

**Completeness** (`PlanValidator.Completeness`) asks whether the plan carries what a map cannot exist without.
No generating piece is an error (`PL1`) and is reported alone. No spawn is an error: PGM would have nowhere to
put a player. No objective of any kind is a complaint (`PL3`), returned on the success response under
`warnings`, because which goal a map carries is the author's and one can still be set in Configure.

**The lint is not asked at the compile.** `PlanValidator.LintRules` — `PC-C`, `G2`, `G5`, `SP2`, `BZ5`, `BZ12`,
`EL1`, `ST2`, `WX4`, `WX8`, `SP8`, `WL11`, `SP9`, `ST8`, `ST11`, `ST9`, `ST10`, `BZ11`, `BZ9`, `FR9`, `FR8`,
`CT12` — is the composer's and the evaluator's, which rank candidates by it. A compiled board is asked the same
ids of its own ground once it is a map, off the sketch and the intent (`GET /map/{slug}/findings`,
`docs/tools/sketch.md`), which is where a coast moved after the compile is heard.

## The API

Every endpoint is rooted at `/api`, and every one here is open to anyone: the compile stores nothing, and the
rest read history.

```json POST /api/plan/compile
{
  "plan": 2, "meta": {"name": "Weirgate"},
  "globals": {"cell": 5, "symmetry": "rot_180", "maxPlayers": 8, "surface": 9},
  "pieces": [
    {"id": "spawn", "role": "spawn", "rect": [1, 9, 2, 2]},
    {"id": "dale", "role": "piece", "rect": [-3, 4, 6, 5]},
    {"id": "ford", "role": "piece", "rect": [-1, -4, 2, 8]},
    {"id": "wool", "role": "wool-room", "rect": [-3, 9, 2, 2]}],
  "placements": {"spawns": [{"piece": "spawn", "at": [5, 5], "facing": "front"}],
                 "wools": [{"piece": "wool", "at": [5, 5]}]}
}
```

| Endpoint | Body | Answers | Fails with |
|---|---|---|---|
| `POST /plan/compile` | the document | `{layout, intent}`, each half serialized with its consumer's options so both can be posted on verbatim as a source's `layout` and `intent`; `warnings` rides beside them where the compile is complete enough to succeed and incomplete enough to remark on (today `PL3`, a map with no objective), and where the posted plan carried a field the reader has nowhere to keep (`RQ3`) | 422 `{findings}` structural or completeness errors · 400 malformed |
| `GET /map/{slug}/plan` | — | the plan the map was built from, as stored, or `{}`. The `ETag` is its revision | 404 unknown map |
| `GET /plans[?origin=generated\|authored\|imported]` | — | the stored plan rows' summaries, newest touched first, a generated row with the composer descriptor it was composed from | — |
| `GET /plans/{id}` | — | the row plus its `planJson` | 404 |
| `GET /plans/{id}/ascii[?every=N]` · `/svg` · `/png` | — | the same board drawn, by id | 404 · 422 |
| `GET /objectives/vocabulary` | — | the destroyable styles and materials, the wool dyes with their labels and swatches, and every objective default | — |

## Driving it without the UI

**A plan becomes a map in two calls**: the compile, and the source that stores what it answered, with the
refinement stating what a plan cannot.

```
POST   /api/plan/compile              <the plan document>   → {layout, intent} (+ warnings)
PUT    /api/map/voidwatch/source      {layout, intent, refinement, origin, note}
                                      → {slug, change, replaced, edits, cells, islands, configureUrl}
```

The store applies the refinement onto the pair, originates the map under the slug the route names, runs the
finish that rasterizes the layout, stores the intent and projects the document from it, and writes the authors
— so a caller sequences nothing, and a re-drive under the same slug **replaces** the map rather than leaving a
second one behind it. `?dry=true` answers what a re-drive would change and stores nothing. A source stating a
`plan` is refused naming the compile. The source, the refinement's members and what each refuses are
`docs/tools/flow.md`'s § *A map's source is the way in, and the way back in*. The map is a sketch from then
on, and it is edited as one.

The map's authors reach the intent one of two ways: the plan states `meta.authors` and the compile carries
them, or the refinement states them.

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

Reading the trio together shows exactly where a plan stops. The compile emitted twenty-one polygons —
`hub-t2-7` through `hub-t2-14` and `hub-t6-9` through `hub-t6-16`, two components across ten surfaces — and
the four structural rectangles `spawn-red`, `spawn-blue`, `wool-red-red` and `wool-blue-blue`;
everything else in `ruediger.layout.json` was added afterwards in the Sketch tool — one carved shape with a
sketch-minted id, Bézier controls on five outlines, and a theme named on seventeen of the twenty-six shapes.
The tiers are the plan's. The curves, the carve and the paint are not, and no plan can state them.

Two things bite an agent writing a document by hand. Rect arithmetic is in cells and pieces must genuinely
abut to connect — a corner touch is not a connection, and a seam under 10 blocks is a narrow one. And a wool
room is a cage: it wants an entry seam, a pad that fits its shell, and a spawn room large enough to seat one
monument per wool its team will capture, all of which are refusals rather than warnings.

## Limits

**A plan is not edited in the studio.** The Plan tool retired with the plan as a level (`RP131`, D8 and D9 in
`docs/pgmvox-port.md`): a plan is written by hand, by an agent or by the composer, and a map built from one is
a sketch from then on.

**A plan draws rectangles on a coarse grid, and that is the whole of its geometry.** Every footprint is an
axis-aligned cell rect, so the smallest thing a plan can say is one cell wide — five blocks at the default
scale — and nothing it draws has a diagonal, a curve or a notch. Refining the shape of the ground is the Sketch
tool's, which takes the compiled layout and works on the fused outline. Height is the one property a plan
states finely, and it states one flat surface per piece: valleys, slopes, contours and any interior elevation
belong to the relief downstream.

**A plan states no stack.** The plan model is flat by decision rather than by omission — a storey is a fact
about ground that has been drawn, and the sketch is where ground is drawn — so `layers[]` has no counterpart
here and a compiled plan emits exactly one, id `ground`, at `base_y` 0.

It states no paint and no dressing. Themes, styles, trees, paths, ponds, rocks and houses are all the Sketch
tool's; a plan carrying theme keys has them dropped on parse.

It does not place the observer spawn. The compiled intent puts the observer at `x=0, z=0` at `observerY`, and
the Configure tool is where an observer spawn is actually authored.

Destroy objectives are compiled only at symmetry order 2, and a plan that places them elsewhere is refused.
