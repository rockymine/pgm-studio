# The shape of an answer: what a read-back is worth to a model

A model authoring a board reads the studio's answers, and the shape an answer arrives in decides what the
model can do with it. A number is subtracted from another number. A character in a grid is compared with
the character beside it. A shade in a picture is estimated, and an estimate of a height is where every
mis-read board in this repository's history came from. This document measures the surface — every read the
API and the driver offer, by the shape it answers in — and states what the shape costs, from two runs where
the cost was paid.

## The surface, measured

`GET /api/openapi/v1.json` declares **144 routes and 181 operations**. By the content type each answers on
a 200:

| answer shape | operations | what they are |
|---|---|---|
| `application/json` | 145 | every store, every library row, every preview's SVG-in-JSON, every number a rule is stated in |
| `image/png` only | 7 | `render/topdown`, `surface`, `traversability`, `structures`, `mirror`, `render/walk` and `plans/{id}/png` |
| `image/png` and `text/plain` | 2 | `render/section` and `render/heightmap`, which answer the same cut and the same relief as characters on `?format=text` |
| `application/json` and `text/plain` | 5 | `slopes`, `transect`, `walk`, `themes/census` and `sketch/dressing` — numbers for a tool, and the same reading as a table or a grid for a reader |
| `application/json` and `image/png` | 6 | `coverage` and the five document previews (`theme`, `material`, `prop`, `room-styles/preview`, `preview-snapshot`), which answer a picture on `?format=png` |
| `text/plain` | 5 | `column`, `plan/ascii` (three routes), `plan/flow` |
| XML, zip | 2 | the map document and the export |
| nothing declared | 9 | deletes and redirects |

The reads that answer **a built world** — what exists only after the store — sort three ways. Six pictures
answer only as pictures, and each but two has a read beside it that answers the same question as numbers:
what `topdown` shows standing somewhere is what `transect` and `walk` list `beside` a line; `surface`'s tone
families are `themes/census`; `traversability` and `render/walk` are `walk`; `structures` and `mirror` stay
pictures, and `column` answers what is actually at a coordinate under either. Two pictures answer as text
too, `section` and `heightmap`. Ten answer numbers or grids in JSON, five of them with a text twin: `walk`
(the route as cells and as places with the storey stood on, its cost, every step that left a walk, and what
stands beside it), `transect` (a polyline as stations), `slopes` (the worst step per cell, and the barrier
runs as faces), `themes/census`, `sketch/dressing` (what the pass would place, and every cell's claim as
digit rows), `coverage` (class and traffic grids as digit rows, the dead patches with coordinates),
`editability` (digit rows), `sketch/columns` (every run of every column, with the layer that drew it),
`sketch/relief/read` (range, steps, tiers, faces, crossings, the symmetry error) and `findings`. Before the
store the plan tier is nearly all numbers and grids: `plan/evaluate`, `plan/inspect`, `plan/ascii`, and
`plan/flow` as prose.

The driver (`pgm-studio-mapgen/tools/drive.py`) writes **37 files** for a board the size of Mossgill: two
text (`00-board.txt`, the plan grid; `01-flow.txt`, the flow account) and 35 PNG — a swatch and a section per
theme, a plan and a section per house style, the coverage map, twelve world renders, two isometrics and two
x-rays. Everything the API answers as JSON it prints to the console as lines — every finding with its rule id
and path, the score, the coverage shares, the export gate's verdict — and nothing it answers as a picture
reaches the console at all.

## What was read, and what was gauged

Two runs are the evidence, one from each side.

**`opus5-weirbank`**, authored by an agent with the author watching the renders live. Six faults passed
every gate: a wall floating over a chasm, a bridge deck seven courses over the quay it left, a pillar
standing in seven courses of air, a spawn terrace sunk five courses into the hill beside it, a terrace
eighteen by sixteen round a ten-block hall, and a stair running down into a hill. Three were found in a
picture by the author, one in the plan grid, and **two only in the game**. The worst was a basin the agent
reported as a six-block pond: it had read single columns inside the water, every number true, and measured
nothing about the wall, which was a sheer thirteen-course face. A PNG section at four pixels a block had
been looked at and misread. What found it was two adjacent columns printed as numbers — `(-42, 38) ground
38` and `(-40, 38) ground 22` — which is a transect, and which made a sixteen-course drop unmissable where
the picture had not.

**`fable-millrace-revamp` and `fable-mossgill`**, authored by an agent with the author reading the renders
afterwards. The stone body's fill pattern built as vertical stripes on every cut face, and the author saw it
in the isometric; the agent, reading the same pictures, had not, and the number that names it — the mean run
of one material down a column, 2.5 blocks when the pattern is a volume and the full column height when it is
a plane — did not exist until a probe was written for it. The beck's scarp solved with its shelf on the wrong
side, and that the agent found on its own, from a column profile decoded off `sketch/columns`: the bank at
8 against a stated 14 is a subtraction. Of the 37 files the driver wrote, the agent opened six.

The pattern across both is the same. **A fault that is a number was found when a number was printed, and
missed when only a picture carried it.** The reads that caught faults without being asked were the ones
whose extent came from the thing being checked rather than from the reader deciding where to look: the plan
grid (the whole board, every run) and the findings (every rule, every run). The reads that caught nothing
were the ones a reader had to reach for at the right coordinate on the right day, which after the fifth round
of edits nobody does — the section, the column, the heightmap.

## The recurring faults, and the read each one wants

The author names five things agents get wrong on board after board. Each is placed against the gate that
speaks to it, the shape that gate speaks in, and the read that shows it as a thing to subtract from.

| the fault | what speaks today | in what shape | the read that shows it |
|---|---|---|---|
| a structure thrown in the players' way | `DR-KEEP` on a door's lane, `OB19` on a goal's clearance | a decline naming one column | a profile along each team's route to each goal, with every claim within two blocks of it — the driver's `04-routes.txt`, off `walk` |
| relief overdone | `RL2`: barrier steps counted per group, "never graded" | one count | a slope grid, `#` where the ground steps three or more — the driver's `03-slopes.txt` — which shows a cliff as a line and an overdone relief as a page |
| a path that does not work | `EL1` and `SP8` on the seams between pieces | a step size per seam | the same route profile: rises, falls, the worst step and where, along the cells a player actually walks |
| many themes mashed together | nothing; `render/surface` counts tone families in a legend baked into a PNG | a picture | a theme census — cells per theme, distinct materials, which theme borders which — which no read answers yet |
| a spawn platform raised, and a stair that makes no geometric sense | `SP8` on the egress, `WX11` on a foundation face | a step size, one coordinate | a transect through the spawn along both axes with eight blocks of overshoot, where a platform standing five over the ground beside it is `+5` at a coordinate — the driver's `transect-spawn-*.txt` |
| a pit with a puddle at the bottom | nothing | — | a transect through the water prop's own box: `BARRIER +8 at (-52, 0)` |

Every one of these is a claim about a **shape** — a bank, a wall, a slope, a stair, a basin — and a shape is
a profile, never a point. A single column answers what is at a coordinate; a render answers what a place
looks like; only a run of columns with the steps between them named answers whether a player can walk it.

## What the driver writes, and why the extent is the point

The driver writes the board as text beside every picture, each file the API's own `?format=text` answer
(`tools/render/textreads.py`): the heightmap with the houses, water, spawns and goals overprinted; the slope
grid; the two axis sections with `#` ground, `L` a storey, `~` liquid, `H` a hall, `M` a made thing; a
transect along x and along z through every spawn, goal, house, water prop, boulder and made thing, its box
taken from the documents and padded eight blocks each side, with what stands within two cells of the line;
the profile along each team's walk to each goal with what stands beside it; the theme census; and the
dressing pass's claims. What the driver decides is the extent, and nothing else. The summaries print inline
— one line a transect and a route, every step a player cannot walk named with its coordinates — so they are
in the transcript before a file is opened.

The extent is the point. A read whose extent the reader chooses catches a fault only where the reader
already suspects one; a read whose extent is the feature's own box catches the fault the reader thought was
fixed. The Weirbank basin would have printed `BARRIER +13` on its own transect on every drive after the push
that made it, without anyone asking.

## What the API answers, and in what shape

Each of these is a read the studio answers itself, so any client and any future tool gets it, and the
studio's own knowledge of a column — which pass claimed it, which layer drew it, what a goal keeps clear —
is in the answer without a sidecar. `read-backs.md` carries each one's row and query words; the ids are
the tasks that delivered them.

- **A section and a transect as text and as numbers** (`WS19`). `render/section?format=text` answers the
  same cut as characters — `#` ground below the surface course, `L` a storey, every claim by its pass — with
  a y axis, a ruler and the ground's height band under each column. `transect?points=x,z;x,z…` walks any
  polyline and answers each station's ground, the storey stood on, the water, the top, what stands there and
  the step from the station before, classed walked, scrambled, barrier or drop, with the totals and every
  non-walk step as an event; `beside=N` lists what stands within N cells. JSON beside the text, so a tool
  sums and a reader reads. Every height is the first free course above a block, so two subtract to blocks.
- **A heightmap and a slope grid as text** (`WS20`). `render/heightmap?format=text&every=N` answers the
  height band per cell with the spawns, goals, houses and water overprinted; `slopes` answers the worst
  step to a neighbour per cell — `.` walked, `:` scrambled, `#` a barrier — and names the barrier runs as
  faces, largest first, which is where an overdone relief is.
- **The walk with its profile and its neighbours** (`WS21`). `walk` answers, beside its cells, the storey it
  stood on at every place, every step that left a walk with the totals, and with `?beside=N` everything
  recorded within N cells of the route; `?format=text` is the station table — the read for a thing in the
  players' way, and for a path that does not work.
- **A theme census** (`WS22`). `themes/census` answers cells and share per theme, the distinct materials
  each spends, which theme borders which over how many cells, and the board's palette count — the number
  for a board that mashes its themes.
- **Claimed cells as a raster** (`TS81`). `sketch/dressing` answers `claims`: digit rows over the board the
  way `coverage` answers its classes — a prop's claim, a goal's clearance, a keep-out, or free — and
  `?format=text` prints them with the key, so a placement is looked up rather than tried.
- **A finding states its edit** (`RP64`). Where a finding has a mechanical fix, it carries the edit as a
  document, a path in the document's own spelling, an operation and a value beside its message: `SP8` and
  `EL1` state the line mark that grades their seam, `WX11` the area mark that benches a house on falling
  ground, `DR-ROAD` the move that clears the road. A model applies it rather than re-deriving it, which is
  what the author's runs show works. `docs/refusals.md` § *A finding* has the shape and a worked example;
  the mapgen driver prints an edit under its finding.

And one convention rather than a read: **a picture has a text twin or a numbers read beside it, and every
text grid states its scale, its extent and its key on its first lines.** `coverage`, `section` and
`heightmap` have both shapes; every other world render has a read beside it that answers the same question
as numbers. The plan grid's first line — one character per cell, a key — is the model, and `TextGrid`
draws every grid's ruler and rows the same way, so a coordinate is read off the picture rather than
counted from an edge.

## What a picture is still for

Three things in these runs no number would have said: that a pale obelisk read as an ornament set down on
the moor rather than a thing of the place; that a tree stood in front of the junction it was meant to frame;
that a bridge read as a viaduct nobody asked for. Those are judgements of belonging, and a picture is the
only read that carries them. The split, then: numbers to measure, and every claim about a shape is measured;
pictures to judge, and a judgement is asked of a picture only after the numbers have said the shape is
right. The two runs had it the other way round, and every fault that cost a round of edits came from a
shape that had been judged rather than measured.
