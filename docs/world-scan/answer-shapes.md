# The shape of an answer: what a read-back is worth to a model

A model authoring a board reads the studio's answers, and the shape an answer arrives in decides what the
model can do with it. A number is subtracted from another number. A character in a grid is compared with
the character beside it. A shade in a picture is estimated, and an estimate of a height is where every
mis-read board in this repository's history came from. This document measures the surface — every read the
API and the driver offer, by the shape it answers in — and states what the shape costs, from two runs where
the cost was paid.

## The surface, measured

`GET /api/openapi/v1.json` declares **141 routes and 178 operations**. By the content type each answers on
a 200:

| answer shape | operations | what they are |
|---|---|---|
| `application/json` | 147 | every store, every library row, every preview's SVG-in-JSON, every number a rule is stated in |
| `image/png` only | 9 | the eight world renders (`render/topdown`, `section`, `heightmap`, `surface`, `traversability`, `structures`, `mirror`, `walk`) and `plans/{id}/png` |
| `application/json` and `image/png` | 6 | `coverage` and the five document previews (`theme`, `material`, `prop`, `room-styles/preview`, `preview-snapshot`), which answer a picture on `?format=png` |
| `text/plain` | 5 | `column`, `plan/ascii` (three routes), `plan/flow` |
| XML, zip | 2 | the map document and the export |
| nothing declared | 9 | deletes and redirects |

The reads that answer **a built world** — what exists only after the store — sort three ways. Eight
pictures answer only as pictures. One read answers as text a person reads (`column`). Six answer numbers or
grids in JSON: `walk` (a route as cells, and what it costs), `coverage` (class and traffic grids as digit
rows, the dead patches with coordinates), `editability` (digit rows), `sketch/columns` (every run of every
column, with the layer that drew it), `sketch/relief/read` (range, steps, tiers, faces, crossings, the
symmetry error) and `findings`. Before the store the plan tier is nearly all numbers and grids:
`plan/evaluate`, `plan/inspect`, `plan/ascii`, and `plan/flow` as prose.

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

## What the driver now writes, and why the extent is the point

The driver writes the board as text beside every picture, off the exported world, the provenance sidecar
and the columns it drew the isometric from (`tools/render/textreads.py`): a heightmap as one character per
two blocks with the houses, water, spawns and goals overprinted; a slope grid; the two axis sections with
`#` ground, `L` a storey, `~` water, `H` a hall, `M` a made thing; a transect along x and along z through
every spawn, goal, house, water prop, boulder and made thing, its box taken from the document and padded
eight blocks each side; and the profile along each team's walk to each goal with what stands beside it. The
summaries print inline — one line a transect, every step a player cannot walk named with its coordinates —
so they are in the transcript before a file is opened.

The extent is the point. A read whose extent the reader chooses catches a fault only where the reader
already suspects one; a read whose extent is the feature's own box catches the fault the reader thought was
fixed. The Weirbank basin would have printed `BARRIER +13` on its own transect on every drive after the push
that made it, without anyone asking.

## What the API should answer, and in what shape

The driver's text pass is a client-side rendering of reads the API already answers in JSON, plus a world
file. Each of the following moves one of them into the API, where any client and any future tool gets it,
and where the studio's own knowledge of a column — which pass claimed it, which layer drew it, what a goal's
clearance is — is available without a sidecar. They are filed on the board under the ids given.

- **A section and a transect as text and as numbers** (`WS19`). `render/section` answers a PNG; the same
  cut as characters with a y axis and the ground's height under each column, and a `transect` along any
  polyline answering each station's ground, water, top, claim and the step from the station before, classed
  as walked, scrambled, barrier or drop. JSON beside the text, so a tool sums and a reader reads.
- **A heightmap and a slope grid as text** (`WS20`). `render/heightmap?format=text&every=N`, the height band
  per cell with the markers overprinted, and a `slopes` read with the worst step per cell — the two grids a
  relief is judged by, in the shape the plan grid already has.
- **The walk with its profile and its neighbours** (`WS21`). `walk` answers cells; it should also answer the
  ground under each, the steps between them classed, and every claim within a stated distance of the route —
  the read for a thing in the players' way, and for a path that does not work.
- **A theme census** (`WS22`). Cells per theme, distinct materials per theme, adjacency between themes, the
  palette count for the board. The number for a board that mashes its themes, which today has no gate and no
  read.
- **Claimed cells as a raster** (`TS81`). `sketch/dressing` answers `claimedCells` as a count; it should
  answer the claims as digit rows the way `coverage` does, so a placement is looked up rather than tried. A
  board this size took ten preview passes to place eleven trees by trial.
- **A finding states its edit** (`RP64`). Where a finding has a mechanical fix, it carries the edit as a
  document, a path in the document's own spelling, an operation and a value beside its message: `SP8` and
  `EL1` state the line mark that grades their seam, `WX11` the area mark that benches a house on falling
  ground, `DR-ROAD` the move that clears the road. A model applies it rather than re-deriving it, which is
  what the author's runs show works. `docs/refusals.md` § *A finding* has the shape and a worked example;
  the mapgen driver prints an edit under its finding.

And one convention rather than a read: **every picture has a text twin, and every text grid states its
scale, its extent and its key on its first lines.** `coverage` already has both shapes; the eight world
renders have one. The plan grid's first line — one character per cell, a key — is the model to follow, and
the driver's text files follow it.

## What a picture is still for

Three things in these runs no number would have said: that a pale obelisk read as an ornament set down on
the moor rather than a thing of the place; that a tree stood in front of the junction it was meant to frame;
that a bridge read as a viaduct nobody asked for. Those are judgements of belonging, and a picture is the
only read that carries them. The split, then: numbers to measure, and every claim about a shape is measured;
pictures to judge, and a judgement is asked of a picture only after the numbers have said the shape is
right. The two runs had it the other way round, and every fault that cost a round of edits came from a
shape that had been judged rather than measured.
