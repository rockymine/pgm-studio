# Relief — interior elevation for a sketch shape

A sketch shape states one thing about its height and states it at its outline: a uniform thickness, or a
thickness per vertex interpolated across the footprint. That is enough for the geometry the tool was built
for — flat plates, neat staircases cut as separate shapes, a whole surface tilted to an angle — and it is the
reason every map the tool produces reads as machined. Ground that rolls cannot be said at all, because the
only place a shape can make a statement about height is a corner of its own outline.

**Relief** is the interior half of that model: a height field authored *inside* a shape, out of marks placed
where the ground should rise and fall, and solved into a surface between them. It is the sketch-side answer
to the elevation pass parked as **G32-C**, and the two meet at the same data — the composer emits marks, an
author drags them, and both arrive at the rasterizer as the same field.

The model was worked out against a live prototype, `tools/relief`, whose every figure is rendered by the
algorithms described here. Read it alongside this document the way `tools/decorate/prototype.html` reads
alongside `decoration.md`: when the prose and the prototype disagree, suspect the prose. Every number quoted
below is printed by that tool, at the footprint sizes maps are actually built at — a 45×30 room, a 90×135
team board, a 192×128 whole map.

Read alongside:

- `docs/contracts/sketch-tool-improvements.md` §3 — the rasterizer this feeds, and the column model
  (`floor`, `base_height`, `anchor_heights`) it extends.
- `docs/world-export/terrain-painting.md` — the pass that dresses the result. It reads elevation already;
  everything a relief produces is a shape the painter's rim/wall/plateau rules were written for.
- `docs/world-export/decoration.md` §4, §7 — paths and water channels, which change behaviour once the
  ground under them is not flat.
- `docs/generator/rules.md` §EL — the measured elevation law the readback is checked against.
- `docs/generator/ideas.md` — G32-C (the composer's elevation pass), G142 (the roughen pass).

---

## 1. What a shape can state today, and what it cannot

Three surfaces are reachable now. A **uniform** `base_height` gives a plate. **Per-vertex anchors** give a
triangulated surface: the shape's outline is ear-clipped and each cell's height is barycentric across the
triangle it falls in. A **fitted tilt** plane is that same anchor array, written by a plane solved through
two or three chosen vertices, so the top is a ruled slope rather than an undulating triangulation.

Everything they share is the reason none of them makes terrain. Control lives on the **outline**, so the only
way to say something about the middle of a shape is to add a vertex to its edge — which moves the edge. The
maximum of a triangulated surface is always at a vertex, so a hill in the interior is unreachable by
construction. And on a concave footprint the triangulation spans the concavity: an L whose two arms are high
and whose inside corner is low is interpolated across a triangle that crosses the notch, so the surface
creases along the ear-clip's diagonals rather than following the arms.

The alternative to all of it today is to cut the elevation into separate shapes — a plate per level, stacked
into terraces. That is genuinely good for a staircase, where the steps *are* the design, and it is the wrong
tool for a hillside, where the author would be hand-drawing a contour map as polygons.

## 2. A relief is a set of placed marks

What an author knows about a piece of ground is local and specific: this end is high, that end is low, there
is a shoulder along here, this bench is flat, and *this* bank is a wall. A relief takes exactly those
statements and nothing else. A **mark** is one of them — a patch of the footprint held at a chosen height —
and the marks are *placed*, in the same sense the dressing stage means it: a decision about where the high
ground is decides how the map plays, so it belongs to the person making it rather than to a noise field.

Five kinds cover the vocabulary, and the first four differ only in the shape of the patch each one pins:

| Mark | Pins | Says |
|---|---|---|
| `point` | a disc of a given radius | a summit, a hollow, a spot height |
| `line` | a band along a polyline, optionally with a height per vertex | a ridge, a valley floor, a shoulder falling as it runs |
| `area` | every cell inside a ring | a bench, a mesa top, a sunken floor — a genuinely flat surface |
| `rim` | the footprint's own outer rings | where the land meets the void |
| `scarp` | a band either side of a drawn line, at two heights, with the face between them left free | a break of slope — the mark that decides where players can go (§5) |

A point mark with a radius of zero pins one cell and reads as a spike; from about two up it reads as a
summit, which is why the radius exists at all. A line mark's per-vertex heights are interpolated along its
arc, so one drawn stroke can be a ridge that descends. The rim is optional and it is what keeps a hill inside
its shape: without one, marks alone decide the whole surface, so a shape carrying a single high mark rises to
that height everywhere and simply runs off its own edge — which is usually what an island's interior wants,
and never what a lake wants.

A mark is clipped to the footprint rather than confined to it, so one can be **placed past the edge** and
only its overlap counts. That is not a tolerance, it is the way a hill at the corner of a map is authored: a
summit whose centre sits off the board raises the ground toward the corner and stops there, leaving no back
slope behind it and no strip of map nobody will stand on.

Marks resolve in order and the last one wins a contested cell. That is what lets a bench be drawn over a
slope and flatten it, and it is load-bearing in a way worth stating: a rim written after a ridge cuts a
doorway through both ends of the ridge where the two meet, which hands a router a free way around the high
ground the ridge was placed to create.

One detail of the same family, because it is the difference between a wall and a wall with a gate: a band
stops where its line stops. Measured perpendicular distance alone wraps a half-disc around each end of a
drawn line, which for a scarp means the cliff closes over the gap beside it — the gap being the entire reason
the line was drawn to end there.

## 3. The space between the marks is solved, not tweened

Three fills were built and measured against the same statement on a horseshoe — one arm's tip high, the
other's low, the two tips ten blocks apart in a straight line and about seventy apart along the land.

**Straight-line weighting** (inverse distance over the marks) is the obvious one and the wrong one: it does
not know the eight-block slot between the arms exists, so the low arm pulls the high arm down across open
void. Measured at the two facing banks, it leaves them **6 blocks** apart. **On-land weighting** measures the
same weights along paths that stay on the footprint, by a chamfer sweep that only ever steps onto land; the
banks come out **10 blocks** apart, the full statement. **The smoothest surface** — a relaxation solving for
the field whose curvature is least subject to the marks — also gives **10**, and it is the one to build on,
for a reason the number does not show.

The relaxation reads no cell that is not land. That single property does all the work: the footprint's
outline becomes a no-flow boundary, so the surface neither climbs nor falls as it meets the void unless a
mark says so, and it bends around a notch rather than through it. It also has the property an author actually
needs, which weighting does not: its extremes can only sit where a mark put one. No bump appears that nobody
asked for, and no ridge sags between two marks that were both high. What the author states is what the
surface does, everywhere, and the fill is the least-eventful thing that satisfies it.

## 4. Reach, grain, and the block step

Three knobs turn a solved field into ground, and each answers a question the marks do not.

**Reach** is how far a statement travels before the field falls back to the relief's base level, in blocks. It
enters the relaxation as a screening term pulling every free cell toward the base, so the field decays over a
characteristic length the author states directly. Unlimited reach means the marks decide the whole surface,
which is what a room-sized shape wants; a finite reach makes each mark a local landform with plain ground
between, which is what a ninety-by-a-hundred-and-thirty-five board wants. It is the difference between two
summits joined by a broad saddle and two hills standing in a field.

**Grain** is a deterministic value-noise term added after the solve, in blocks of amplitude at a stated
feature size — the wobble that stops a solved surface reading as machined. It is never allowed to override a
mark: a stated height is a statement, and a field that moved it would make the marks advisory. Grain is
hashed from the cell, never drawn from a generator, so a map re-exports identically.

**Step** is the block quantum the finished surface snaps to. One follows the field cell by cell. Two is the
step unit the hand-built corpus uses (`rules.md` EL1) and reads as deliberate terracing — and it is the one
knob that can genuinely break a map rather than merely make it harder. Measured on the board above, terracing
at two turns a surface that was one connected piece of walkable ground into **six** separate places, the
largest holding 49.8%: every terrace riser is now a two-block wall, and the map is in halves. The repair is
to cut a stair through the cheapest riser of each stranded place until one remains, which restores full
connectivity while moving the walkable share only from 86.9% to 87.3% — visually almost nothing, which is
exactly why the failure has to be caught by measurement rather than by eye.

## 5. Steepness is how terrain decides where players go

A relief that is walkable everywhere is a field, not a map. The reason to author elevation at all is that
some ground should be **harder to reach** — a bank a team cannot climb from the water, a plateau with two
ways up instead of five, a fall that can be taken but not undone. Steepness is the control, and it works
because the game's movement has three thresholds rather than one:

| Step | Costs | What it is on a map |
|---|---|---|
| 0–1 | a jump, and nothing else | open ground |
| 2 | a placed block: slow, and the player stands still in the open to do it | a lip that shapes a route without closing it |
| 3+ | building in earnest | a face; not crossed in a fight |

None of the three is a fault, and a good map is a mix of all three. This is why the readback reports what a
surface costs at each tier instead of scoring it against the flattest one — and it is why the number that
matters is not "is anything unreachable" but *what the terrain charges*.

The **scarp** mark is how that is stated directly. Every other mark gives a height; a scarp gives a **drop** —
a height each side of a drawn line and the width of the face between — so what the author picks is the
grade, and the grade decides the crossing. It pins two bands and leaves the face free, and the relaxation
runs a near-linear ramp between them, which means one number spells a hillside or a wall. Measured with the
same ten-block drop on a 90×135 board, along the 135 rows that cross it:

| Face | Grade | Crossed on foot | With a block | Descended | Walk from side to side |
|---|---|---|---|---|---|
| 10 wide | 1 per block | 122 rows | 132 | 134 | 1.0× the direct distance |
| 5 wide | 2 per block | 1 row | 126 | 134 | 1.1× |
| 2 wide | 5 per block | 0 rows | 0 | 133 | no walkable route at all |

Three things in that table are the whole argument. The middle row is a **soft wall**: it stops a rush and
lets a player who spends a block through, which is the most useful thing terrain can do and the thing a
90° cliff cannot. The right-hand column says a barrier is usually not a wall but a **detour**, because there
is normally a way round the end of it — so how far round is the measurement, not whether. And the descent
column is a **one-way cliff** (`rules.md` EL5) falling out of the same mark: a drop is not a barrier, since a
player walks off a ledge and takes the fall, so a face that stops a crossing one way lets it through the
other. Measuring both directions is the only way to see that, and it is the difference between a wall and a
gradient of pressure.

## 6. How much relief a shape takes

The same three marks were run at four amplitudes on the 45×30 room a capture-the-wool map is made of:

| Relief | Walk (0–1) | Needs a block | Not crossed on foot | Places on foot | Ledges |
|---|---|---|---|---|---|
| 2 blocks | 100% | 0 | 0 | 1 (100%) | 0 |
| 4 blocks | 98.1% | 26 | 0 | 1 (100%) | 0 |
| 8 blocks | 86.1% | 138 | 50 | 1 (99.7%) | 2 |
| 14 blocks | 71.9% | 193 | 187 | 1 (97.1%) | 20 |

The reading that matters is the last two columns, and it is not the reading a walkability score gives. Even
at 14 blocks over a room the ground is still **one connected place** holding 97% of it — what the relief adds
is twenty small ledges and a lot of pressure, not a broken map. A count of connected regions alone says
"twenty-one pieces" and is nearly meaningless; separating places from ledges is what makes the measurement
usable, and a place is anything holding a hundredth of the ground.

Four blocks over a room is where the surface stops being flat and stays entirely open. Eight is where it
begins to charge. Fourteen is a room with a hill in it, which is a legitimate thing to build.

## 7. Shapes erected out of the field

A relief makes rolling ground. What makes a map is the thing standing in it, and the existing shape model is
most of the way there already: shapes composite by "the taller add wins", so a shape drawn over a relief
already cuts its own top through the field. What is missing is one word on the shape saying **how** its top is
decided:

- **level** — the top is an absolute height. The shape cuts a flat plateau through whatever it crosses. This
  is a mesa, and its faces are cliffs.
- **raise** — the top is a fixed amount above the ground under it, read at the covered cells' median. The
  shape stands proud of the relief wherever it is dragged, which is what a monolith, a plinth or an outcrop
  wants.
- **sink** — the same, downward: a quarry, a sunken arena, a pit.

Nothing downstream needs teaching. The painter classifies a column by its neighbours (`terrain-painting.md`
§5), so a mesa's face arrives as a void-facing or terrain-facing edge with a known drop and is painted as a
wall under a rim — the machinery for cliffs is already shipped and has simply had nothing to paint. And the
corpus's own cliff law (`rules.md` EL6) discriminates on the result without being told anything: over one
prototype island the mesa's face measures **27 wide with an 11-block drop** and qualifies as a cliff, while
the monolith's — **8 wide** at a comparable drop — does not, which is right, because a monolith is a structure
and not a landform.

## 8. Symmetry is imposed on the surface, not on the statements

A competitive map's relief is the largest single advantage its terrain can hand one team, so marks fan across
the symmetry orbit exactly as every placed thing does — authored once, mirrored onto their images. That much
is obvious. It is also **not sufficient**, and this was the finding that most changed the design.

Solving the whole map at once with mirrored marks gives a field that agrees with its own mirror to within the
relaxation's tolerance. A tolerance is precisely what rounding turns into a whole block: two paired cells
settling at 8.499 and 8.501 quantize to 8 and 9, and one team owns a step the other does not. Grain makes it
worse and more plainly — sampled from map coordinates it is simply a different field on each side. Measured
over the 24,576-cell designed map of §10, mirrored marks with the fold switched off leave **26.2% of the
board** differing from its own mirror image, by up to **2 blocks**. With the fold on it is **0**, everywhere.

So the rule has two halves. Grain is sampled at the **canonical** member of each mirrored pair — the same
discipline a dressing prop's material already follows when it resolves in its own frame — and the solved
field is folded the same way before it is snapped to blocks, which costs one pass and turns a guarantee that
is close into one that is exact.

Both fold on the cell's **centre**, not its corner. Reflecting the corner pairs each cell with its image's
*neighbour* — a one-cell shear that looks like symmetry and measures as a full block of unfairness.

And the fold is not finished when the solve is. Every pass that runs afterwards decides things by walking the
map — a carve follows a route from one end, a graded road smooths along its length, a stair cut picks the
cheapest riser it finds first — and a walk has a direction the symmetry does not preserve. Each such pass has
to be folded again, or it quietly undoes what the solve established.

## 9. Routes and water, once the ground is not flat

Both stroke tools in the dressing stage assume a flat plane, and both become more interesting the moment one
exists.

A **path** is a finish laid over ground that already exists, so it drapes over a slope and can run wherever it
is drawn — which is also its problem, because a drawn line does not know what it is climbing. Two operations
close that. **Routing** turns a drawn line into a shortest path whose cost counts climbing far more than
distance and which refuses a step no player could take: over a board with a ridge across it and one pass
through, a straight line climbs 14 blocks and tops out at y21, and the routed line climbs **0** and tops out
at y7, through the pass. **Grading** is the other answer — cut and fill a corridor so its surface never steps
more than one block along its length, with the shoulders blended back into the field over a couple of blocks
so the road does not sit in the landscape as a trench. On the same board terraced at a three-block step, the
drawn line's worst step is 3 and grading takes it to 1. The two are a real choice, not a pipeline: route
around the hill, or cut through it.

**Water** has to run downhill, which turns out to be the one place where the obvious algorithm fails
outright. Steepest descent from a source stops at the first cell with no lower neighbour, and a grained
surface is full of one-block pits — measured, the descent stopped after **2 cells**. Water is therefore routed
on a **depression-filled** copy of the surface, where every basin has been raised to the level of its lowest
outlet, so a strictly-non-climbing path to the edge of the land always exists; the same run then covers **65
cells**. The filled copy is only ever used for routing — a pit an author dug is theirs to keep, and the carve
goes into the real surface.

Two consequences follow. The bed's floor is forced non-increasing downstream, so the channel cannot run
uphill however the centerline wanders. And the water line steps down only where the bed does and holds level
in between, which turns a single water line into a chain of pools — the measured run holds **14 distinct
levels**, which is what a stream down a hillside actually looks like and what `decoration.md` §7's single
lowest-surface line cannot express once the ground has relief. A basin is also where a channel *ends*,
alongside the map edge: a cell the fill had to raise is announcing that it sits below its own way out, which
is the definition of a pond.

There is one case where all of that has to be given up, and it is the common one. **A river on the mirror
axis cannot both fall and be fair.** A half-turn reverses the direction of flow, so a bed that descends from
north to south descends from south to north in its own image, and the two cannot both be the built surface.
The resolution is not a compromise but a different thing: on the axis a river is a **canal** — one water
level for the whole run, a bed the same shape read from either end — which is what a river down the middle of
a competitive map has always had to be. Falling water belongs to the flanks, where it is authored once and
fanned like everything else.

The relationship also runs the other way, and it is the cheapest good idea here: a drawn channel is a
**valley mark**. Hand the route to the solver as a line mark below base level and the terrain forms a valley
around it, rather than the channel being cut into a surface that ignores it.

## 10. A whole map, to see whether the vocabulary is enough

The parts above were each measured alone. The test of the model is whether a map can be *designed* with them,
so one was: a 192×128 rot_180 board, stated once for one team and fanned.

A **spawn hill** in each back corner, its centre eight blocks off the board so the ground rises into the
corner and stops. A **shoulder** it looks over, an **area** mark for the flat a wool room would stand on, a
**swale** the approach runs down, a flank rise behind the front and a hollow behind that. A **river** down the
middle, drawn once as a line that is its own mirror image, carved as a canal. And four **scarps** cliffing
both banks in two stretches each, broken twice — because the breaks are the map.

It solves in **115 ms** and comes out at relief 16, 96.1% of it open ground, one connected place holding
98.9% with 84 cliff-top ledges besides, and a worst mirrored difference of **0**. The river can be pushed
straight across on foot at two places going east and the mirror-image two going west — the cliffs are
one-way, so each team can drop into the water on their own side and not climb out on the other — and with a
block to spend there are five, covering exactly the three gaps the scarps were drawn to leave. Crossing the
northern reach costs a 1.3× detour against the direct line; crossing the middle, 1.1×.

That is the point of the exercise. Every one of those numbers is a design decision the author made and can
now read back, and none of them is a score.

## 11. Where it lives

The solver reads a footprint mask and produces a height grid, and knows nothing about maps, so it belongs in
the dependency-free leaf beside the other pure algorithms: **`PgmStudio.Geom.Relief`** — the mark types, the
spec, and the relaxation. Classification is not pure geometry — a cliff is a corpus rule about play — so the
step histogram, the reachable-place flood, the scarp qualification and the ford/detour measures live in
**`PgmStudio.Analysis`**, where the other derivations that read a surface already are. `SketchRasterizer`
consumes the result: a shape carrying a relief takes its thickness from the solved field instead of from the
per-vertex triangulation, and nothing else about the rasterizer changes, because the field answers the same
question the triangulation did.

The canvas preview is the JS twin, the same arrangement `Geom.Symmetry` already has with
`js/studio/geometry/symmetry.js`. What it draws is the field's **contours**, which is both the readable view
of a height field and the direct-manipulation surface: dragging a contour line is dragging a line mark at
that height, so the topographic reading of the surface and the way it is edited are the same object.

A relief rides on the shape, beside the height fields it generalizes:

```json
"relief": {
  "base": 8,
  "fill": "smooth",
  "reach": 26,
  "step": 1,
  "grain": { "amplitude": 1.3, "scale": 17, "seed": 21 },
  "marks": [
    { "kind": "point", "at": [-6, -6], "h": 19, "r": 26 },
    { "kind": "line",  "points": [[26, 34], [54, 26], [72, 38]], "h": [15, 14, 13], "width": 7 },
    { "kind": "area",  "ring": [[16, 44], [40, 40], [44, 58], [20, 62]], "h": 13 },
    { "kind": "scarp", "points": [[79, -2], [83, 8], [86, 16]], "high": 15, "low": 6, "face": 2, "band": 5 },
    { "kind": "rim",   "h": 4, "depth": 1 }
  ]
}
```

and the shape gains one word for how its own top is decided:

```json
"height_mode": "level" | "raise" | "sink"
```

`base_height` and `anchor_heights` stay exactly as they are and keep their meaning; a relief supersedes them
on the shape that carries one. That matters for more than compatibility: the flat plate and the neat
staircase are the right answer often enough that they should not become special cases of a solver.

## 12. What it costs to edit

A relaxation is iterative, and the number of sweeps a field needs to settle grows with how far across it the
marks have to talk — so a solve that is comfortable on a room is not on a map. The fix is to solve **coarse
first and then refine**: halving the grid halves that distance and quarters the cells, so the long-range
conversation happens on a quarter of the work and the finest level has only local detail left. A coarse cell
is land if any of the four under it is, and pinned if any of them is, so a scarp two blocks wide cannot
vanish at the top of the cascade and reappear as a shock at the bottom.

Measured on the development container, and against a resume from the surface already on screen:

| Footprint | Cells | One grid | Coarse-to-fine | 40 sweeps resumed | Cells off by one |
|---|---|---|---|---|---|
| 45×30 room | 1,350 | 8 ms | 9 ms | 7 ms | 4 |
| 90×135 board | 12,150 | 151 ms | 129 ms | 87 ms | 601 |
| 192×128 whole map | 24,576 | 519 ms | 317 ms | 228 ms | 1,614 |

The cascade and the single grid agree to within a block everywhere, and the cascade's advantage grows with
the footprint, which is the size that was the problem. Resuming is what makes a drag affordable — moving one
mark perturbs the field locally, so a warm-started relaxation has only that perturbation to carry — and it
lands, in blocks, on the settled answer everywhere but the figures in the last column, each off by exactly
one. So the drag warm-starts and the release solves in full.

## 13. What is open

**Relief across shapes.** A relief is solved per footprint, and an island is many shapes. Solving each
independently leaves a seam where two adjoining shapes disagree about the height they share. The candidates
are to solve per *island* over the fused footprint, to let a shape inherit its neighbours' edge heights as
marks, or to declare the seam a scarp on purpose — and the first is probably right, since the island is
already the unit theming and detection use.

**Anchors as marks.** Per-vertex anchor heights are exactly a set of point marks on the outline, so a shape
with anchors could be read as a relief with no interior marks and a rim of varying height. Whether to
converge the two representations or keep the plate/staircase path separate is a real decision, not a
formality — §11 argues for keeping it.

**The composer's side.** Marks attach to roles and interfaces rather than to coordinates — a raised spawn for
overview, a stepped approach climbing toward a wool room, a low frontline so bridges launch low, a pit
flanking a wool approach (EL7), a scarped bank where the plan wants a one-way lane. That mapping is G32-C's
own work; this document only establishes that what it would emit is what a hand places.

**A budget for pressure.** The readback measures what terrain charges but nothing yet says how much charging
is too much — the dressing stage has the same gap (`world-export/ideas.md` G167). The materials for one are
here: the share of the map at each tier, the detour factor between key places, the ford count on a barrier.
What is missing is what those should be, which is a corpus question rather than a design one.

**The readback surface.** The measurements are computed and have nowhere to be fetched from. What makes a
relief drivable by a generator or an agent is that the report sits next to the document it describes.
