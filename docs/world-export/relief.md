# Relief — interior elevation, solved between placed marks

The other passes in this folder dress a world that already exists: `structures.md` stamps rooms onto the
terrain, `terrain-painting.md` rewrites its materials, `decoration.md` adds its life. This one decides the
ground all three of them land on. **Relief** is the interior half of the sketch's height model — a height
field authored *inside* a group, out of marks placed where the ground should rise and fall, and solved into
a surface between them — and it reaches the world as the column tops the rasterizer emits.

**Status: built.** The solver, the five marks, the push, the block step and its stair repair, the symmetry
fold, the contour overlay, the readback, erected shapes and per-shape participation all ship (`S41`,
`S43`–`S45`, `S48`, `S50`–`S53`). What is still open is named where it arises and collected in §16. This file
owns no rule-id family of its own: the elevation law a relief is measured against is `docs/generator/rules.md`
**EL1–EL7**, the way `terrain-painting.md` owns `TP*` and `structures.md` owns `WX*`.

Every number quoted below is a measurement of the solver described here, taken at the footprint sizes maps
are actually built at — a 45×30 room, a 90×135 team board, a 192×128 whole map. The solver is
`Geom/Relief/`, and it is what a figure disagreeing with this prose is settled against.

Read alongside:

- `docs/tools/sketch.md` — the tool this is a phase of: the layout document a relief rides in, the Relief
  phase's own surface, and the column model (`floor`, `base_height`, `anchor_heights`) it extends.
- `terrain-painting.md` — the pass that dresses the result. It reads elevation already; everything a relief
  produces is a shape the painter's rim/wall/plateau rules were written for.
- `decoration.md` §4, §7 — paths and water channels, which change behaviour once the ground under them is
  not flat (§9).
- `docs/generator/ideas.md` — G32-C (the composer's elevation pass), G142 (the roughen pass).

---

## 1. What a shape can state at its outline, and what it cannot

Three surfaces are reachable from a shape's own height fields. A **uniform** `base_height` gives a plate.
**Per-vertex anchors** give a triangulated surface: the shape's outline is ear-clipped and each cell's height
is barycentric across the triangle it falls in. A **fitted tilt** plane is that same anchor array, written by a
plane solved through two or three chosen vertices, so the top is a ruled slope rather than an undulating
triangulation.

Everything they share is the reason none of them makes terrain. Control lives on the **outline**, so the only
way to say something about the middle of a shape is to add a vertex to its edge — which moves the edge. The
maximum of a triangulated surface is always at a vertex, so a hill in the interior is unreachable by
construction. And on a concave footprint the triangulation spans the concavity: an L whose two arms are high
and whose inside corner is low is interpolated across a triangle that crosses the notch, so the surface creases
along the ear-clip's diagonals rather than following the arms.

The alternative without a relief is to cut the elevation into separate shapes — a plate per level, stacked into
terraces. That is genuinely good for a staircase, where the steps *are* the design, and it is the wrong tool
for a hillside, where the author would be hand-drawing a contour map as polygons.

## 2. A relief is a set of placed marks

What an author knows about a piece of ground is local and specific: this end is high, that end is low, there is
a shoulder along here, this bench is flat, and *this* bank is a wall. A relief takes exactly those statements
and nothing else. A **mark** is one of them — a patch of the footprint held at a chosen height — and the marks
are *placed*, in the same sense the dressing stage means it: a decision about where the high ground is decides
how the map plays, so it belongs to the person making it rather than to a noise field.

Five kinds cover the vocabulary, and the first four differ only in the shape of the patch each one pins:

| Mark | Pins | Says |
|---|---|---|
| `point` | a disc of a given radius (`r`) | a summit, a hollow, a spot height |
| `line` | a band reaching `r` either side of a polyline, optionally with a height per vertex | a ridge, a valley floor, a shoulder falling as it runs |
| `area` | every cell inside a ring | a bench, a mesa top, a sunken floor — a genuinely flat surface |
| `rim` | the footprint's own outer rings | where the land meets the void |
| `scarp` | a band either side of a drawn line, at two heights, with the face between them left free | a break of slope — the mark that decides where players can go (§5) |

A point mark with a radius of zero pins one cell and reads as a spike; from about two up it reads as a summit,
which is why the radius exists at all. A line mark states the same quantity under the same name — `r` reaches
either side of the centerline, so the band it writes is **twice** it: `"r": 6` holds a twelve-block strip. A
line stored under `width` reads into the same field and is written back as `r`; the number never meant
anything else, and only the name promised otherwise. Its per-vertex heights are interpolated along its arc, so
one drawn stroke can be a ridge that descends. The rim is optional and it is what keeps a hill inside its shape:
without one, marks alone decide the whole surface, so a shape carrying a single high mark rises to that height
everywhere and simply runs off its own edge — which is usually what a group's interior wants, and never what
a lake wants.

A mark is clipped to the footprint rather than confined to it, so one can be **placed past the edge** and only
its overlap counts. That is not a tolerance, it is the way a hill at the corner of a map is authored: a summit
whose centre sits off the board raises the ground toward the corner and stops there, leaving no back slope
behind it and no strip of map nobody will stand on.

Marks resolve in order and the last one wins a contested cell. That is what lets a bench be drawn over a slope
and flatten it, and it is load-bearing in a way worth stating: a rim written after a ridge cuts a doorway
through both ends of the ridge where the two meet, which hands a router a free way around the high ground the
ridge was placed to create. The rim is therefore written **first** (§15).

### 2.1 A push is not a constraint

Every mark above is a **constraint**: the ground here *is* twelve, and the solver honours it exactly.
Constraints are what a solver needs and not what a hand wants, and the tell is the point mark — stated as a
position and a radius it can only make a round hill, and the roundness is not a style, it is the shape of the
only footprint that could be typed.

A **push** is the other half. It takes a drawn ring and raises the ground inside it by an amount, falling away
outside it over a stated distance, so the landform's plan is whatever was drawn: a spur, a saddleback, a
crescent ridge and a lobed hollow are one operation with a different outline. It applies to the solved surface
rather than into it, and that is what makes it compose — two pushes over the same ground add, where two
constraints over the same ground would have to argue.

One detail decides whether it works. The falloff is distance **from the ring, measured across the land**, not
from a centre. A radial falloff rounds a long thin push off within a few blocks of its own outline; a
distance-from-the-ring falloff keeps the shape the whole way out, and it keeps the hollow inside a crescent's
curve, which no centre-based falloff can produce at all. The sweep only steps onto land, so a push on one arm
of a shape does not lift the arm across the notch from it — the same property the fill has, for the same
reason. A **roughness** wobbles that distance against a noise field so the skirt is not a clean offset of the
outline, which is the difference between a hill and an extruded logo. The distance is `falloff` on the wire and
**skirt** on the inspector, and the canvas draws it as a dashed outline at that distance — which is the
difference between a push and a bench made visible, since a bench ends at its outline where a push is still
moving ground past it.

Which side of the ring counts as "out" comes from the ring's own signed area, so a ring traced either way round
gets its skirt outside it rather than inside, where it would read as a smaller push.

A push composes over ground, and one thing on the board is not ground: a **floor**. A spawn or a wool room is a
level rectangle that can never slope (§11), and the world lays one floor course over a room's whole frame while
filling the bedrock under it column by column — so a room lifted a course on one side is a floor spanning air,
not a landform. The mark that holds such a floor is therefore **rigid**, and the lift steps over it exactly as
the grain already does. Nothing else is: an author's own area or point mark is a statement about the ground, and
a push crossing one sculpts it along with everything around it, which is the composition the two halves of the
vocabulary exist to give. What a rigid mark costs is a step at the room's edge where the push lifted the ground
beside it, and that is the price §11 already names for any flat pad laid across a slope.

The room still **rides with the push** rather than being stranded under it, because the seating in §11 reads its
height off the finished surface — pushes included — before pinning it there. What is frozen is the room's
flatness, not its elevation.

### 2.2 A push's top is a field, not a number

A push that lifts its whole interior by one amount makes a plateau, and a plateau is a landform but not the
common one. Three things an author reaches for next — a ridge whose crest falls along its length, a hollow
rather than a table, and a centre for the lift to pull toward — turn out to be one addition: the amount is a
**field over the push** rather than a scalar.

**A lift per ring vertex** varies the amount around the outline, interpolated along the ring's arc and wrapped
so a closed loop has no seam. That is what makes a drawn ridge a ridge: one end stands sixteen blocks up, the
other six, and the crest falls between them along the line that was drawn. The array is expanded from the
single amount when an author first wants one end lower, and collapses back to that amount when every corner
agrees, so undoing a variation leaves the push it started from rather than an array that happens to be flat. It
is never carried to the next push: it is sized to the ring it was stated on.

**A crown** says how much higher the middle of the push stands than its edge. Zero is the flat top; positive
domes it; negative dishes it into a hollow whose rim is the drawn outline — a corrie, a quarry floor, a pond
basin, depending on what fills it. The record's own default is zero and the **editor's is two**, and the
disagreement is deliberate: a flat top is the least natural of the three settings and is the thing an author
reaches for a push in order to stop making, so a push drawn without touching a knob should already be a
landform. The record keeps zero because a caller that states nothing should get the plain operation.

What "the middle" means is the part worth stating, because it is the one thing that does *not* have to be
authored. It is the deepest point of the outline measured **inward**, which is the shape's medial axis — the
same chamfer sweep the skirt uses, run the other way. For a round push the medial axis is a **point** and the
crown makes a dome; for a long push it is a **line** and the crown makes a ridge whose crest follows the
shape's own spine. The identical setting on different proportions gives a peak or a ridgeline, and a shape with
a fat lobe and a thin arm gets a domed lobe and a narrow crest on the arm. So the centre a push pulls toward
never has to be drawn: the outline already contains it.

That closes the vocabulary rather than extending it. An explicit centre point, a profile curve per push and a
brush stack were all considered and are all covered by these two numbers, and each would have cost the property
that makes the model authorable at all — that a landform is one drawn ring and a few numbers beside it.

Constraints and pushes are both authored and both fan across the symmetry orbit; what separates them is whether
the author is stating a fact about the ground or sculpting it.

One detail of the same family, because it is the difference between a wall and a wall with a gate: a band stops
where its line stops. Measured perpendicular distance alone wraps a half-disc around each end of a drawn line,
which for a scarp means the cliff closes over the gap beside it — the gap being the entire reason the line was
drawn to end there.

## 3. The space between the marks is solved, not tweened

Three fills were built and measured against the same statement on a horseshoe — one arm's tip high, the other's
low, the two tips ten blocks apart in a straight line and about seventy apart along the land.

**Straight-line weighting** (inverse distance over the marks) is the obvious one and the wrong one: it does not
know the eight-block slot between the arms exists, so the low arm pulls the high arm down across open void.
Measured at the two facing banks, it leaves them **6 blocks** apart. **On-land weighting** measures the same
weights along paths that stay on the footprint, by a chamfer sweep that only ever steps onto land; the banks
come out **10 blocks** apart, the full statement. **The smoothest surface** — a relaxation solving for the field
whose curvature is least subject to the marks — also gives **10**, and it is the one the solver is built on, for
a reason the number does not show.

The relaxation reads no cell that is not land. That single property does all the work: the footprint's outline
becomes a no-flow boundary, so the surface neither climbs nor falls as it meets the void unless a mark says so,
and it bends around a notch rather than through it. It also has the property an author actually needs, which
weighting does not: its extremes can only sit where a mark put one. No bump appears that nobody asked for, and
no ridge sags between two marks that were both high. What the author states is what the surface does,
everywhere, and the fill is the least-eventful thing that satisfies it.

## 4. Reach, grain, and the block step

Three knobs turn a solved field into ground, and each answers a question the marks do not.

**Reach** is how far a statement travels before the field falls back to the relief's base level, in blocks. It
enters the relaxation as a screening term pulling every free cell toward the base, so the field decays over a
characteristic length the author states directly. Unlimited reach — stated as zero — means the marks decide the
whole surface, which is what a room-sized group wants; a finite reach makes each mark a local landform with
plain ground between, which is what a ninety-by-a-hundred-and-thirty-five board wants. It is the difference
between two summits joined by a broad saddle and two hills standing in a field.

**Grain** is a deterministic value-noise term added after the solve, in blocks of amplitude at a stated feature
size — the wobble that stops a solved surface reading as machined. It is never allowed to override a mark: a
stated height is a statement, and a field that moved it would make the marks advisory. Grain is hashed from the
cell, never drawn from a generator, so a map re-exports identically.

**Step** is the block quantum the finished surface snaps to. One follows the field cell by cell. Two is the step
unit the hand-built corpus uses (`rules.md` EL1) and reads as deliberate terracing — and it is the one knob that
can genuinely break a map rather than merely make it harder. Measured on the board above, terracing at two turns
a surface that was one connected piece of walkable ground into **six** separate places, the largest holding
49.8%: every terrace riser is now a two-block wall, and the map is in halves.

The repair is a **stair**, and it ships with the step (`StairRepair`). It cuts one stair per stranded place
through that place's **cheapest** riser — the smallest intervention that reconnects — which restores full
connectivity while moving under eight cells and the walkable share only from 86.9% to 87.3%: visually almost
nothing, which is exactly why the failure has to be caught by measurement rather than by eye. A stair that would
run out of footprint is **refused rather than half-cut**, since a riser with a partial cut in it is the same map
with a scar. The repair is a switch on the group (`stairs`) and is worth asking for whenever the step is more
than one.

## 5. Steepness is how terrain decides where players go

A relief that is walkable everywhere is a field, not a map. The reason to author elevation at all is that some
ground should be **harder to reach** — a bank a team cannot climb from the water, a plateau with two ways up
instead of five, a fall that can be taken but not undone. Steepness is the control, and it works because the
game's movement has three thresholds rather than one:

| Step | Costs | What it is on a map |
|---|---|---|
| 0–1 | a jump, and nothing else | open ground |
| 2 | a placed block: slow, and the player stands still in the open to do it | a lip that shapes a route without closing it |
| 3+ | building in earnest | a face; not crossed in a fight |

None of the three is a fault, and a good map is a mix of all three. This is why the readback reports what a
surface costs at each tier instead of scoring it against the flattest one — and it is why the number that
matters is not "is anything unreachable" but *what the terrain charges*.

The **scarp** mark is how that is stated directly. Every other mark gives a height; a scarp gives a **drop** — a
height each side of a drawn line and the width of the face between — so what the author picks is the grade, and
the grade decides the crossing. It pins two bands and leaves the face free, and the relaxation runs a
near-linear ramp between them, which means one number spells a hillside or a wall. Measured with the same
ten-block drop on a 90×135 board, along the 135 rows that cross it:

| Face | Grade | Crossed on foot | With a block | Descended | Walk from side to side |
|---|---|---|---|---|---|
| 10 wide | 1 per block | 122 rows | 132 | 134 | 1.0× the direct distance |
| 5 wide | 2 per block | 1 row | 126 | 134 | 1.1× |
| 2 wide | 5 per block | 0 rows | 0 | 133 | no walkable route at all |

Three things in that table are the whole argument. The middle row is a **soft wall**: it stops a rush and lets a
player who spends a block through, which is the most useful thing terrain can do and the thing a 90° cliff
cannot. The right-hand column says a barrier is usually not a wall but a **detour**, because there is normally a
way round the end of it — so how far round is the measurement, not whether. And the descent column is a
**one-way cliff** (`rules.md` EL5) falling out of the same mark: a drop is not a barrier, since a player walks
off a ledge and takes the fall, so a face that stops a crossing one way lets it through the other. Measuring
both directions is the only way to see that, and it is the difference between a wall and a gradient of pressure.

## 6. How much relief a shape takes

The same three marks were run at four amplitudes on the 45×30 room a capture-the-wool map is made of:

| Relief | Walk (0–1) | Needs a block | Not crossed on foot | Places on foot | Ledges |
|---|---|---|---|---|---|
| 2 blocks | 100% | 0 | 0 | 1 (100%) | 0 |
| 4 blocks | 98.1% | 26 | 0 | 1 (100%) | 0 |
| 8 blocks | 86.1% | 138 | 50 | 1 (99.7%) | 2 |
| 14 blocks | 71.9% | 193 | 187 | 1 (97.1%) | 20 |

The reading that matters is the last two columns, and it is not the reading a walkability score gives. Even at
14 blocks over a room the ground is still **one connected place** holding 97% of it — what the relief adds is
twenty small ledges and a lot of pressure, not a broken map. A count of connected regions alone says
"twenty-one pieces" and is nearly meaningless; separating places from ledges is what makes the measurement
usable, and a place is anything holding a hundredth of the ground.

Four blocks over a room is where the surface stops being flat and stays entirely open. Eight is where it begins
to charge. Fourteen is a room with a hill in it, which is a legitimate thing to build.

### 6.1 What kind of ground it is, and whether it was ever graded

Those numbers are one room's. Across a whole board the same question is asked with two, and both are read back
off the solved surface rather than off what the marks asked for.

**Elevation is read for the board's own size.** The measure is the range over the square root of the group's
cells, because a bare range says nothing on its own: twenty-eight blocks is a mountain on a board a hundred
cells across and a slope on one four hundred across — `opus5-deepcut` carries 28 over 4,736 cells and
`opus5-elderwold` 22 over 13,950, and one is a quarry while the other is a hillside. The four bands are the
gaps between the boards this repository has built, read against the author's own reading of them: a **plain**
below 0.10 (`opus5-whinnymoor` 0.065), **rolling** from 0.15 (`opus5-thornfell` 0.232, called good rolling
hills), **hills** from 0.35 (`opus5-tarnfell` 0.402, called smooth-ish), and a **mountain** from 0.50
(`opus5-sandcaster-ii` 0.524). A third of Thornfell's elevation is what the author put a flatter plain at, and
it lands at 0.077.

**Whether that elevation was graded is a separate number**, and it is what tells a quarry from a mountainside:
the `steps` histogram's **scramble against barrier** — two-block steps against everything taller. Ground that
rolls keeps more of the first; ground where every height change is a wall keeps the reverse, whatever its
range. Thornfell rolls at 7.6 scrambles per barrier and Tarnfell at 3.0; **Deepcut has not one scramble
transition on it** and 7.85% of its steps are barriers, which is a mountain's elevation with none of a
mountain's shaping. At or above two the ground rolls; at or below one it steps.

A group **states which of the four it is meant to be** (`relief.landform`), and the read answers what it
measured. Where the two disagree the response carries an `RL1` complaint, and ground that carries elevation it
never graded carries `RL2` — complaints, never refusals: a relief is authored ground, and the studio's business
is to measure it and say where the measurement and the statement disagree. A plain is never `RL2`, since a
plain has no elevation to have shaped.

## 7. Shapes erected out of the field

A relief makes rolling ground. What makes a map is the thing standing in it, and the shape model is most of the
way there already: shapes composite by "the taller add wins", so a shape drawn over a relief already cuts its
own top through the field. One word on the shape says **how** its top is decided:

- **level** — the top is an absolute height. The shape cuts a flat plateau through whatever it crosses. This is
  a mesa, and its faces are cliffs.
- **raise** — the top is a fixed amount above the ground under it, read at the covered cells' median. The shape
  stands proud of the relief wherever it is dragged, which is what a monolith, a plinth or an outcrop wants.
  What "the ground under it" means needs saying, because the obvious reading is wrong: an erected shape
  contributes its footprint but **not its own thickness** to the surface it is then read against. Otherwise it
  finds its own plate underneath itself and stands proud of that.
- **sink** — the same, downward: a quarry, a sunken arena, a pit.

Absent, a shape is ordinary ground and the relief is what its ground does. The default has to stay the default,
or a drawn board would become a staircase of plates.

The word is orthogonal to the height function a shape already carries, and the two compose in every combination.
Per-vertex anchor heights and the two- and three-point slopes decide what the top surface **looks like** — flat,
tilted, folded along a diagonal — and `height_mode` decides what that surface is **measured from**: an absolute
height for `level`, the median ground under the footprint for `raise` and `sink`. A polygon whose anchors read 8
at its west edge and 20 at its east tilts from y9 to y20 as ordinary ground, and tilts from y9 to y20 again once
it is erected — a tilted mesa rather than a levelled one. Sunk, that same tilt is a quarry whose floor drains
one way, which is what a pit with a flat bottom is missing.

Nothing downstream needs teaching. The painter classifies a column by its neighbours (`terrain-painting.md` §5),
so a mesa's face arrives as a void-facing or terrain-facing edge with a known drop and is painted as a wall
under a rim. And the corpus's own cliff law (`rules.md` EL6) discriminates on the result without being told
anything: over one prototype group the mesa's face measures **27 wide with an 11-block drop** and qualifies as
a cliff, while the monolith's — **8 wide** at a comparable drop — does not, which is right, because a monolith
is a structure and not a landform.

That distinction has a second half, at the shape's own edge. An erected shape meets the ground at whatever step
the two heights happen to differ by, and unqualified that step is sheer: over the same group the mesa's west
face rises **13 blocks between two adjacent cells**, and the worst step anywhere around its outline is **17**.
Sheer is right for a monolith, which wants a hard edge; it is wrong for a mesa, which is a landform and belongs
*in* the terrain rather than on it. A **skirt** of N blocks says which: the top is blended toward the ground it
meets across the outermost N cells of the footprint, weighted by inward distance from the outline. Each cell
blends toward the height immediately outside it rather than toward one number for the whole shape, so where a
mesa crosses a relief its downhill side eases into low ground and its uphill side into high, and it sits in the
slope instead of levelling it. A skirt of 7 takes that mesa's worst edge step from **17 blocks to 2**; the
monolith, left at 0, keeps its 12-block face.

## 8. Symmetry is imposed on the surface, not on the statements

A competitive map's relief is the largest single advantage its terrain can hand one team, so marks fan across
the symmetry orbit exactly as every placed thing does — authored once, mirrored onto their images. That much is
obvious. It is also **not sufficient**, and this was the finding that most changed the design.

Solving the whole map at once with mirrored marks gives a field that agrees with its own mirror to within the
relaxation's tolerance. A tolerance is precisely what rounding turns into a whole block: two paired cells
settling at 8.499 and 8.501 quantize to 8 and 9, and one team owns a step the other does not. Grain makes it
worse and more plainly — sampled from map coordinates it is simply a different field on each side. Measured over
the 24,576-cell designed map of §10, mirrored marks with the fold switched off leave **26.2% of the board**
differing from its own mirror image, by up to **2 blocks**. With the fold on it is **0**, everywhere.

So the rule has two halves. Grain is sampled at the **canonical** member of each mirrored pair — the same
discipline a dressing prop's material already follows when it resolves in its own frame — and the solved field
is folded the same way before it is snapped to blocks, which costs one pass and turns a guarantee that is close
into one that is exact.

Both fold on the cell's **centre**, not its corner. Reflecting the corner pairs each cell with its image's
*neighbour* — a one-cell shear that looks like symmetry and measures as a full block of unfairness.

And the fold is not finished when the solve is. Every pass that runs afterwards decides things by walking the
map — a stair cut picks the cheapest riser it finds first, a carve follows a route from one end, a graded road
smooths along its length — and a walk has a direction the symmetry does not preserve. The stair repair folds
again for exactly that reason (§4). The other two do not yet, which is `S42`: measured on the designed map, a
carve that did not re-fold left the two halves **9 blocks** apart.

## 9. Routes and water, once the ground is not flat

Both stroke tools in the dressing stage were built against a flat plane, and the relief settles what each of
them owes the ground.

A **path** is a finish laid over ground that already exists, so it drapes over a slope and can run wherever it
is drawn. That is **settled as correct**: a path repaints the top block of each column and adds no cell, which
is what lets a road cross a slope without becoming a ramp. Two operations that would make it obey the terrain
were built in the prototype and measured, and both were declined. **Routing** turns a drawn line into a shortest
path whose cost counts climbing far more than distance — over a board with a ridge across it and one pass
through, a straight line climbs 14 blocks and tops out at y21 where the routed line climbs **0** and tops out at
y7. **Grading** cuts and fills a corridor so its surface never steps more than one block along its length; on
the same board terraced at a three-block step, the drawn line's worst step is 3 and grading takes it to 1. Both
work, and both decide where the author's road goes, which is the tool overruling the person. Terrain a route
*emits* is the draw phase's path primitive and the erected modes instead, and a path whose own height varies
along its length is `S56`.

**Water** is the half that is genuinely wrong on a relief, because it has to obey the ground rather than sit on
it, and this is `S46` — designed and measured here, not built. Steepest descent from a source stops at the first
cell with no lower neighbour, and a grained surface is full of one-block pits: measured, the descent stopped
after **2 cells**. Water is therefore routed on a **depression-filled** copy of the surface, where every basin
has been raised to the level of its lowest outlet, so a strictly-non-climbing path to the edge of the land
always exists; the same run then covers **65 cells**. The filled copy is only ever used for routing — a pit an
author dug is theirs to keep, and the carve goes into the real surface.

Two consequences follow. The bed's floor is forced non-increasing downstream, so the channel cannot run uphill
however the centerline wanders. And the water line steps down only where the bed does and holds level in
between, which turns a single water line into a chain of pools — the measured run holds **14 distinct levels**,
which is what a stream down a hillside actually looks like and what `decoration.md` §7's single lowest-surface
line cannot express once the ground has relief. A basin is also where a channel *ends*, alongside the map edge:
a cell the fill had to raise is announcing that it sits below its own way out, which is the definition of a
pond.

There is one case where all of that has to be given up, and it is the common one. **A river on the mirror axis
cannot both fall and be fair.** A half-turn reverses the direction of flow, so a bed that descends from north to
south descends from south to north in its own image, and the two cannot both be the built surface. The
resolution is not a compromise but a different thing: on the axis a river is a **canal** — one water level for
the whole run, a bed the same shape read from either end — which is what a river down the middle of a
competitive map has always had to be. Falling water belongs to the flanks, where it is authored once and fanned
like everything else.

The relationship also runs the other way, and it is the cheapest good idea here: a drawn channel is a **valley
mark**. Hand the route to the solver as a line mark below base level and the terrain forms a valley around it,
rather than the channel being cut into a surface that ignores it.

## 10. A whole map, to see whether the vocabulary is enough

The parts above were each measured alone. The test of the model is whether a map can be *designed* with them, so
one was: a 192×128 rot_180 board, stated once for one team and fanned.

A **spawn hill** in each back corner, its centre eight blocks off the board so the ground rises into the corner
and stops. A **shoulder** it looks over, an **area** mark for the flat a wool room would stand on, a **swale**
the approach runs down, a flank rise behind the front and a hollow behind that. A **river** down the middle,
drawn once as a line that is its own mirror image, carved as a canal. And four **scarps** cliffing both banks in
two stretches each, broken twice — because the breaks are the map.

It solves in **115 ms** and comes out at relief 16, 96.1% of it open ground, one connected place holding 98.9%
with 84 cliff-top ledges besides, and a worst mirrored difference of **0**. The river can be pushed straight
across on foot at two places going east and the mirror-image two going west — the cliffs are one-way, so each
team can drop into the water on their own side and not climb out on the other — and with a block to spend there
are five, covering exactly the three gaps the scarps were drawn to leave. Crossing the northern reach costs a
1.3× detour against the direct line; crossing the middle, 1.1×.

That is the point of the exercise. Every one of those numbers is a design decision the author made and can now
read back, and none of them is a score.

## 11. The group is the unit, and a shape can leave it

A relief is solved over the **group** — the fused footprint of every shape on one landmass — not over a shape.
Solving per shape is not a smaller version of the same thing; it is a different and wrong answer, because a mark
outside a shape says nothing to it and the two sides of a seam settle independently. Measured on a board of
three abutting pieces with a ridge running across all of them, solving per shape leaves steps of **8 and 7
blocks** at the two seams; solving over the fusion leaves **1 and 1**. On a plan-derived sketch this is the
common case rather than an edge one, since equal-level plan pieces fuse into exactly such a group.

The fusion is not always what an author wants, and the case that decides it is a built thing standing on the
ground: a city, a keep, a walled compound. Its floor is not terrain and it is themed as a unit, so a field
rolling through it would put a slope under a wall and land its pattern differently on every stretch. So
participation is a property of the **shape**, and there are three answers:

| | The shape | Does its height reach the land around it? |
|---|---|---|
| **inherit** | is part of the group; the relief flows through it | there is no separate height — one continuous surface (the default) |
| **hold** | keeps one flat height and pins it | **yes** — the surrounding surface is solved knowing where it must arrive, and moves when that height changes. A structural room whose height nobody has corrected takes the ground's height rather than the plan's (below) |
| **exclude** | is taken out of the solve, leaving a hole | **no** — the land is whatever that outline would have produced at any height the shape is finally stamped at |

Two properties of that table are easy to read backwards, and both would mislead whoever builds against it.

**Both kinds change the ground around them.** An excluded shape is not ignored by the terrain: it is a hole, a
hole has an outline, and the relaxation bends around it exactly as it bends around the void. What is different
is not *whether* the land responds but *what it responds to* — a held shape's height travels into the
surrounding surface, an excluded shape's does not. Solved at two very different heights, a held compound moves
the land around it and an excluded one leaves it bit-identical, which is the property the tests assert.

**Neither guarantees a flush meeting.** A flat pad laid across a slope has to pay for its flatness at its edge,
whichever way it is bound — that is arithmetic, not a solver limitation. Hold spreads the payment as far as the
relaxation can spread it; it does not remove it. On one prototype board held met the land at a step of 1 and
excluded at 8, and that pair of numbers is that board's, not a law: the same compound stated ten blocks above
its surroundings steps at its wall under either binding.

An excluded shape is stamped back at its own height after the solve, so the built surface and the surface the
relief was solved over are different objects. That is what lets the compound be moved, re-themed or restyled
without re-solving the ground around it. In the rasterizer the stamping costs nothing to arrange: an excluded
footprint is not in the solved field, so the cells simply keep the column the shape drew, tilt and all.

A held shape pins **one** level, read at its ring's centre. That is what holding means — a floor is flat, and a
floor that followed a per-vertex tilt would be the slope it was declared to replace. An excluded shape has no
such limit, because it never enters the solve: its own column survives however it varies.

The word is not asked of a shape that declares a `height_mode`. Such a shape already stands out of the field,
and `raise`/`sink` read the ground under their own footprint to know where to stand — an excluded footprint
would leave them reading their own plate, which is the failure the erect pass exists to avoid. The inspector
offers whichever question applies: a skirt for a shape standing out of the terrain, a participation for a shape
that is terrain.

**A structural annotation binds by footprint, not by membership, and its held height can be corrected once the
ground is real.** A spawn or wool room the plan compiler projects into the layout is a held shape that is never
listed in its group's own `shapeIds` — that list is read elsewhere as the group's terrain rings, and the
annotation is not terrain. It binds instead by overlapping the ground the group's own shapes already claimed,
so `hold`/`exclude` apply to it exactly as they would an ordinary member shape. Its stated `floor`/`base_height`
starts out as the plan's flat `surface`, because that is the only height a plan-space piece can state before any
terrain exists — and once the relief solved around it is visible, that number is very often wrong.

**So an uncorrected room is seated on the terrain rather than held at the plan's number.** The group is solved
once **without the room's pin** — so what that solve leaves under the room is the relief's own answer and the
plan's number plays no part in it — the room takes a height from it, and the group is solved again holding it
there (warm-started from the first, so the second costs a few sweeps rather than a solve). The room stays
exactly as flat as it was — a spawn or wool room is a level rectangle and can never slope — and the height it is
flat *at* becomes the ground's rather than the plan's. Doing anything else puts a spawn door against a wall
the relief built around it: on `opus5-undercroft` the plan declared every piece at `surface: 14`, a `back-rise`
point mark two blocks in front of the door raised the group to 19, and the pad stayed at 14 — a **five-block
vertical face across the whole door**, walked into by every player leaving spawn, with the objective they are
defending behind it. Nothing complained, and nothing could: `SP8` reads plan-piece surfaces, all of which were
14, so the seam it measures was flat.

**The height is read at the door, not across the room.** A room cannot slope and the ground can, so on an
approach that runs downhill there is no single height that suits every side. Seating on the middle of the
footprint splits the difference and leaves a step at the door *and* at the back; seating on the ground
immediately outside the room's doors leaves the way in and out flush and puts the whole of the difference
behind the room, which is the side nobody walks. Where the ground around a room is level — the common case, and
`opus5-undercroft`'s — the two answers are the same. The measure is the **median** of those door cells rather
than a mean or an extreme: a door spans several cells and one of them may sit on a wrinkle of the grain, which
a mean would carry into the floor and a min or max would take as the answer.

Which sides those are is carried on the shape as `doors`, written by the plan compiler for the authored orbit
image only: a spawn's from the facing its placement states, a wool room's from every entry
`WoolEntrySegments` reports — a wool room is entered by the **attacker**, since the owning team is kept out of
its own wool, so the ground that has to be level with it is the ground the attack arrives across.

**A room stating no door is read on every side**, because a room nobody named a way into is entered from
wherever the ground reaches it. The median under its own footprint is the answer this rule exists to reject —
it splits the difference and leaves a step at the way in as well as at the back — so it is the last resort
only, for a room with no ground outside it at all, which is a room filling its own group. The distinction is
not academic: a stored layout written before `doors` existed carries none, and on `opus5-hollowmarch` reading
the footprint seats the back wool room at **19** against an approach at **21**, while reading the ring outside
seats it at **21** and the way in is flush.

The seating is the *preferred* correction of the two available. The other is re-authoring the relief so it
arrives at the plan's number, which is a larger edit that gives up the terrain the author drew to satisfy a
number stated before that terrain existed.

**The height a room is seated at is read off the finished surface, and its flatness survives everything after
it.** The seat is taken from the first solve, which has already had the pushes and the grain applied, so a room
standing where a push lifts the ground comes up with it. The mark that then pins it is rigid (§2.1): the second
solve's push may not tilt what the seat levelled, and the grain never could. Without that the pin is only as
firm as the last pass over it — on `opus5-hollowmarch` the `under-back` push, whose ring stops one block short
of the back wool room and whose skirt reaches ten blocks further, tilted a room held at 18 across `23 22 22 21
20 20 19 19 19 19`, and the room's own floor course stayed flat over it: bedrock to y19 at the front of the
room, bedrock to y17 at the back, planks at y20 the whole way, and two courses of air under half the floor.

`height_authored: true` marks a shape whose `floor`/`base_height` the author corrected in the sketch rather than
the compiler, and **it is what turns the seating off**: a height stated against real ground is a statement, and
a room the author deliberately sank or raised keeps the height they gave it. A recompile carries those fields
forward matched by the shape's `intentRef` a recompile carries those fields forward matched by the shape's `intentRef`
(`SketchLayout.CarryStructuralHeight`) instead of overwriting them with the plan's own number, since the
compiler regenerates the shape's id and rect every time but a spawn or wool room keeps the same team/
owner:colour identity across a recompile. This is narrower than a relief's own carry: a relief is carried
unconditionally because it cannot be re-derived from anything, while an unmarked structural shape keeps tracking
the plan's `surface` on every compile, so the mechanism never masks a deliberate plan-side height change the
author never asked to keep.

## 12. What the corpus says, and what it says about this

Every number above is self-consistent, which is not the same as being right. The same readback was run over the
built worlds of the destroy-the-monument maps in the community corpus, twice: once over the **built surface** —
the top of everything a player stands on, walls and roofs included — and once over **natural ground**, stepping
past a building's own courses to the terrain underneath. The second is the one a terrain solver can be
calibrated against, and the block roles that decide it were already in the tree: a building is recognised by
*material*, not height, from a set that deliberately excludes stone, cobble, gravel, sand and clay so an outcrop
is not read as architecture.

| | min | p25 | median | p75 | max |
|---|---|---|---|---|---|
| plan width | 36 | 102 | **148** | 196 | 395 |
| plan depth | 41 | 118 | **164** | 230 | 513 |
| height range | 0 | 32 | **48** | 76 | 133 |
| body — 95% of columns | 0 | 11 | **19** | 30 | 88 |
| walk (0–1) | 25% | 62.6% | **72.6%** | 85.2% | 100% |
| scramble (2) | 0% | 1.5% | **5.5%** | 10.2% | 24.4% |
| barrier (3+) | 0% | 8.8% | **18.3%** | 25.9% | 73.9% |
| largest place | 1.9% | 21.2% | **29.4%** | 44.2% | 100% |
| places of 1% or more | 1 | 4 | **8** | 13 | 52 |
| cliffs (EL6) | 0 | 2 | **8** | 13 | 60 |
| under liquid | 0% | 0% | **1.4%** | 7.1% | 75.3% |

*105 maps, natural ground.*

**Filtering the architecture out makes the terrain read steeper, not gentler**, which is the opposite of what it
was expected to do: walk falls from 77.3% to 72.6% and barrier rises from 16.3% to 18.3%. A building's flat roof
and level floor were *smoothing* the reading, and stripping them exposes the ground the building was placed to
stand on. So the corpus's steepness is terrain and not walls, and the tier shares were never the distorted
numbers.

The number that *was* distorted is the cliff count, and heavily. Alpine Mining II reads **36 cliffs** off the
built surface and **13** off natural ground: two thirds of its apparent cliffs were the walls of its own
buildings. A cliff is qualified by width and drop, and a rampart passes both tests, so any rule written against
the built surface would have been counting architecture.

And the gap is the point. The designed map of §10 measures at a 14-block body, 96.1% walk, 2.8% barrier and one
place holding 98.9% — flatter and more open than the corpus at every percentile. **Cedar Crossing**, a 201×131
board and the corpus's most recent addition, measures a 13-block body, 96.1% walk and one place holding 97.9%:
the same profile, and also an outlier against the corpus it belongs to. Two independent attempts at gentle
rolling terrain landing on the same numbers is a result about the *approach*, not about either map — smooth
interpolation between pleasant marks makes pleasant, open, undemanding ground.

Lifting the same design — every mark's departure from the base scaled, the banks steepened with it, nothing
moved — brings the board to a **27-block body**, 8 cliffs and three places with the largest at 45.6%, and turns
the river from something crossable at two fords into a gorge with none on foot and two with a block. That is
what the corpus was asking for, and it took one number to ask.

Drawing with pushes rather than radii closes the rest of it. A 140×260 valley stated as drawn spurs between side
valleys, a river stepping down into a lake and a benched flank measures **83.9% walk** over a 41-block body with
water at 11 distinct levels — the first board here to sit inside the corpus range rather than at its edge.
Against Alpine Mining II's own numbers — 42-block body, 71.7% walk, 13 cliffs, 19.3% under liquid — it is still
the gentler of the two, and by a margin that is now a choice rather than a limit of the vocabulary.

## 13. Where it lives

The solver reads a footprint mask and produces a height grid, and knows nothing about maps, so it sits in the
dependency-free leaf beside the other pure algorithms: **`PgmStudio.Geom.Relief`** — `Footprint`, `Marks`,
`ReliefSpec`, the red-black Gauss-Seidel `ReliefSolver`, the marching-squares `Contours`, and `StairRepair`.
Classification is not pure geometry — a cliff is a corpus rule about play — so the step histogram, the
reachable-place flood, the scarp qualification and the ford/detour measures live in
**`PgmStudio.Analysis.Playability.ReliefReadback`**, where the other derivations that read a surface already are.

`SketchRasterizer` consumes the result: a group carrying a relief takes its columns' **tops** from the solved
field instead of from the per-vertex triangulation, and their **floors** are left alone, because a relief states
where the ground is and not how thick the slab under it is. Nothing else about the rasterizer changes — the
field answers the same question the triangulation did. It solves over the cells the group's add-shapes
contribute that survive the set algebra, so a relief cannot re-add ground a subtract took away, and a mirrored
copy reads its heights back out of the group's own solved surface through the same transform, which makes the
two halves identical by construction rather than to within a second solve's tolerance (§8).

The canvas preview draws the field's **contours**, which is both the readable view of a height field and the
direct-manipulation surface: dragging a contour line is dragging a line mark at that height, so the topographic
reading of the surface and the way it is edited are the same object. The field itself arrives from the server as
traced lines — §15 says why the relaxation is not twinned in JS the way `Geom.Symmetry` is.

A relief rides **top-level on the layout, keyed by group id** (`SketchReliefJson`), because the group is the
unit it is solved over (§11) and because a plan recompile replaces every shape it produced:

```json
"relief": { "group-3": {
  "base": 8,
  "reach": 26,
  "step": 1,
  "stairs": true,
  "grain": { "amplitude": 1.3, "scale": 17, "seed": 21 },
  "marks": [
    { "id": "r1", "kind": "point", "at": [-6, -6], "h": 19, "r": 26 },
    { "id": "r2", "kind": "line",  "points": [[26, 34], [54, 26], [72, 38]], "h": [15, 14, 13], "r": 3.5 },
    { "id": "r3", "kind": "area",  "ring": [[16, 44], [40, 40], [44, 58], [20, 62]], "h": 13 },
    { "id": "r4", "kind": "scarp", "points": [[79, -2], [83, 8], [86, 16]], "high": 15, "low": 6, "face": 2, "band": 5 },
    { "id": "r5", "kind": "rim",   "h": 4, "depth": 1 }
  ],
  "pushes": [
    { "id": "r6", "ring": [[10, -8], [24, -8], [24, 8], [10, 8]],
      "amount": 5, "amounts": [5, 5, 2, 2], "falloff": 10, "roughness": 0.3, "crown": 2, "seed": 1 }
  ]
} }
```

One flat mark shape carries every kind, discriminated by `kind`, and `h` reads a number or an array of them.
That is a property worth stating rather than a shortcut taken: a relief is short enough to write by hand and is
meant to be, so the document a generator or an agent emits is the same one the editor saves. A push carries no
`kind` — the array it sits in already says what it is. And a mark that does not carry what its kind needs is
**dropped rather than defaulted**: a point without `at`, a line or scarp under two points, an area or push under
three ring vertices never reaches the solver.

The shape gains one word for how its own top is decided, and a number for how hard it lands:

```json
"height_mode": "level" | "raise" | "sink",
"skirt": 0
```

and one for whether its ground joins the group's, which is the other question and is asked of the shapes the
first is not:

```json
"relief_scope": "hold" | "exclude"
```

A structural room carries one more, written by the plan compiler rather than by an author, naming which sides
its doors stand on so the seating above knows which ground the room has to be level with:

```json
"doors": ["-z"]
```

`base_height` and `anchor_heights` keep their meaning; a relief supersedes them on the group that carries one.
That matters for more than compatibility: the flat plate and the neat staircase are the right answer often
enough that they should not become special cases of a solver.

## 14. What it costs to edit

A relaxation is iterative, and the number of sweeps a field needs to settle grows with how far across it the
marks have to talk — so a solve that is comfortable on a room is not on a map. The answer is to solve **coarse
first and then refine**: halving the grid halves that distance and quarters the cells, so the long-range
conversation happens on a quarter of the work and the finest level has only local detail left. A coarse cell is
land if any of the four under it is, and pinned if any of them is, so a scarp two blocks wide cannot vanish at
the top of the cascade and reappear as a shock at the bottom.

Measured on the development container, and against a resume from the surface already on screen:

| Footprint | Cells | One grid | Coarse-to-fine | 40 sweeps resumed | Cells off by one |
|---|---|---|---|---|---|
| 45×30 room | 1,350 | 8 ms | 9 ms | 7 ms | 4 |
| 90×135 board | 12,150 | 151 ms | 129 ms | 87 ms | 601 |
| 192×128 whole map | 24,576 | 519 ms | 317 ms | 228 ms | 1,614 |

The cascade and the single grid agree to within a block everywhere, and the cascade's advantage grows with the
footprint, which is the size that was the problem. Resuming is what makes a drag affordable — moving one mark
perturbs the field locally, so a warm-started relaxation has only that perturbation to carry — and it lands, in
blocks, on the settled answer everywhere but the figures in the last column, each off by exactly one. So the
drag warm-starts and the release solves in full.

Each group's preview resumes from the surface its previous preview settled on (`ReliefPreviewCache`, a bounded
LRU keyed by map and group, matched on the exact footprint since a field is an array indexed by the grid it was
solved on). **It cannot change an answer**, and that is the design rather than a hope: the relaxation stops when
the field stops moving, so a resumed run that reaches that tolerance has reached the surface a cold one would,
and a resume that fails to settle is discarded and the cold cascade runs instead. The fallback is deliberately
not held to the resume's sweep budget — a caller offering a head start may cap the attempt cheaply, and
inheriting that cap would answer an unfinished surface in exactly the case the fallback exists for. The cache is
handed the **unshifted** field, since what the rasterizer returns has its layer's `base_y` added and feeding
that back would seed the next solve a whole layer high.

## 15. The authoring surface

**Relief is a phase, beside Draw, Theme and Dressing.** The sketch tool runs its phases over one canvas,
switched by the rail, and relief is one of exactly that kind: same shapes, same viewport, its own tools and its
own overlay. It sits **between Draw and Theme**, and the order is the dependency — relief is geometry and
changes what the rasterizer emits, so it precedes the two passes that read the built surface. Entering the phase
turns the contour overlay on with it, so the statement and the surface it produced are on screen together, which
is the only way a mark can be tuned by eye.

**Marks are placed things, so they take the Dressing treatment wholesale.** That stage had already solved the
same problem: a document of placed items, a tool set on the canvas with select / delete / update, a list panel
and an inspector bound to the selection, per-kind settings carried across placements, and a bridge surface of
flat methods. Relief has the same five parts with different nouns — `ReliefDoc`, `canvas.reliefTools`,
`SketchReliefList` + `SketchReliefInspector` — and the mark kinds are tool buttons the way the prop kinds are.

Three things differ, and each is the model asserting itself over the borrowed shape. A prop is placed **on the
map**; a mark is placed **in a group**, because that is the unit a relief is solved over — so the group is
fixed by where a trace *starts*, and never revised. Judging it by coverage instead would break the one gesture
the clipping rule exists for: a mark dragged past an edge raises the ground into a corner and stops, and
ownership by area would hand that mark to whichever group the overhang happened to cross. For the same reason a
mark, unlike a prop, may be dragged **off** its group entirely, where a prop's drag stops at the void. And the
**rim** gets no tool at all: it holds the whole outline, so there is nowhere to put it and nothing to drag, and
it is a switch on the group instead — one that writes the rim *first* in the mark list, since a rim written last
cuts a doorway through both ends of every ridge that reaches the outline. A first mark in a group starts at
that group's own **base** rather than at the last mark's height, which would state a cliff nobody asked for.

**The base is the group's own level, and the editor reads it rather than assuming one.** A relief replaces the
top of every column of its group (§13), so `base` is not a working ground level an author adjusts afterwards —
it is what the whole landmass becomes wherever the marks say nothing. A base that disagrees with the height the
shapes were drawn at therefore moves the group the moment the first mark lands, and it moves it silently: the
contour overlay simply redraws at the new height, and nothing about the gesture says a landmass just fell. So a
relief created in the editor starts at the group's own top — the most common `floor + base_height` among its
add shapes, ties to the tallest, which on a plan-derived group is one number — and the panel states that level
beside the field, with which way the ground moves where the two differ. The number a hand-written document gets
for an absent `base` is the same one, since a second constant would be a second rule. What the editor may not
do is correct a base *after* marks are placed: every mark carries an absolute height, so a base moved under
them leaves them where they were stated, which is a pit or a plateau exactly where the author's first statement
went.

**A relief is stated in a group, and the phase may not reshape one.** Picking a group is how its base,
reach, step, grain and rim are reached, so the phase runs on the canvas as a **selection surface** — the same
mode the Theme phase uses, for the same reason. Offering the edit handles here would make the gesture that
selects a group the gesture that reshapes it, and the group's outline is the footprint the relief was solved
over: moving it re-fuses the board, and a re-fused board is a different group with nowhere for its relief to
land (below). Geometry is Draw's.

A mark carries an **id** on the wire for the same reason a prop does. The solver has no use for it — a mark is a
set of pinned cells — but a placed thing has to survive being selected, moved and edited among its neighbours,
and a relief that renumbered its marks on load would move the selection under the author's hands.

**Colour carries the height, not the kind** — the opposite of the dressing overlay's rule, and the right way
round here. Every mark does the same thing to the ground and differs only in where and how high, so a glance
should answer "is this the high one or the low one" rather than "is this a line or an area", which the drawn
shape already says. Each mark also wears its own number, and the two kinds that state more than one wear both: a
falling ridgeline shows its ends, a scarp shows its drop. The number is **chrome, not ground**: it is sized in
screen pixels, like the hairlines and the contour labels beside it, because what a mark states does not get
bigger as the board is zoomed into — and it is dropped entirely below about eighteen pixels of the mark's own
extent, where a number would be wider than the thing it labels. A spot's rides just clear of its disc, since
a spot's anchor is also where its point grip sits.

**A contour is grabbed and moved to state a height.** A press near a contour grabs it, and moving it writes a
`line` mark at that contour's own level along its new position: a contour is a line of constant height, so
moving one says the ground reaches that height here now. Index lines win a press inside a slack, because several
contours run close together on a steep face — that is what steep means — and the heavily drawn ones are the only
ones an author can aim at. The whole line **moves** rather than bending under the pointer: a contour has hundreds
of points and a drag has one, so bending it locally would need a brush radius, a falloff and a rule for the ends
— three settings to express what is one statement. A placed mark wins the press over a contour beneath it, and a
contour pressed without moving states nothing.

**The preview is solved on the server, and there is no JS twin.** The paint preview already establishes the
seam: an edit debounces, the layout is posted, a render comes back, the canvas loads it as a layer, and a
sequence number drops replies overtaken by a newer edit. A relief solve is far cheaper than the whole-map paint
that seam already carries (§14). Porting the relaxation to JS would buy a few milliseconds and cost a second
implementation of a cascade, a chamfer sweep and a symmetry fold, which is the duplication the symmetry leaf
exists to prevent. The **tracing** stays server-side for the same reason — the reply is lines, not a raster — so
the client's whole share of it is stroking points it was handed. Contours are traced over the **continuous**
field, because contouring the block surface returns the outlines of its own treads rather than lines of constant
height. Every fifth block is an **index** line, heavier and the only one labelled, because forty equal lines on
a team board say no more than "there is a slope somewhere". The overlay follows its own toggle rather than a
phase, since a relief is geometry and is worth seeing while the shapes over it are still being drawn — which is
exactly when the paint preview is not.

**The readback sits next to the document it describes.** `POST /map/{slug}/sketch/relief/read` answers, per
group, what the terrain charges at each of the three thresholds, the places that leaves and the ledges stranded
off them, the faces with cliffs qualified, crossings counted both ways, and the symmetry error. It is fetched on
a button rather than on every edit, and it is what makes a relief correctable by a generator or an agent rather
than only by eye.

**A count says a board is broken; the pieces say where.** Beside the place and ledge counts each tier carries
`parts`, one entry per piece of surface, largest first: how many cells it holds, what share of the group that
is, where its middle sits, the box it spans, and whether it is a place or a ledge. The flood that produces the
counts already has them, so this costs nothing, and without it a read of *places 3, largest 0.95* leaves the
missing five percent to be found by guessing a coordinate and taking a column transect. The list is cut at
sixteen; a place is at least one percent of the group so the cap only ever bites on ledges, whose tail is
slivers along a brink, and the `ledges` count still counts them all. The centroid is the mean of a piece's
cells, so a horseshoe answers with a point outside itself — a fact about the piece rather than an error, and
the box beside it is what bounds a search. One measure needed a rule the prose above does not state: a face is grouped by **which way it
looks** before its runs are joined, because a face is a thing that faces a direction — joined without regard to
it, a small block's four sides become one run whose cell count passes the cliff width, which is the exact call
EL6 exists to get right.

**A recompile refuses rather than guesses.** A relief is expensive hand work and it is *geometry*, so a recompile
from a plan would replace it — the same rule that already replaces hand-drawn shapes, and a much worse loss. Two
things follow, and the second is the one with teeth. A relief is stored top-level on the layout, keyed by group,
rather than nested inside the shapes a recompile discards, and is carried across the compile under its own rule
rather than as a finish key: theming is a finish, terrain is not. But group identity is itself derived from the
geometry, so a board that re-fuses does not move a group — it produces a different one, and a relief authored
against the old fusion has nowhere correct to land. Neither of the alternatives survives that. A stable authored
id would keep the key alive while the ground under it changed shape, which is worse than losing it, because the
terrain would still be applied. Re-binding by footprint overlap decides by area what the author decided by
intent, and the case it gets wrong — one group split in two, most of the relief landing on the larger half — is
exactly the case that matters. So `PUT /map/{slug}/sketch/from-plan` answers **409** in the refusal envelope,
one `SK1` finding per group whose relief it cannot place — the group id riding as the finding's subject —
and writes nothing; `?force=true` accepts the loss. Discarding hours of terrain is a decision, and it belongs
to the author.

## 16. What is open

**The later passes that walk the map do not all fold** (`S42`). The solve folds and so does the stair cut; a
carve and a graded road still decide things by walking, and each has to fold again or it undoes what the solve
established (§8).

**Water does not read the relief** (`S46`). §9 is the design and the measurements; the dressing stage's channel
still assumes a flat plane, and what it needs is depression-filled routing, a bed floor forced non-increasing
downstream, and per-pool water levels in place of one lowest-surface line.

**A path's height does not vary along it** (`S56`). The path primitive takes one `base_height` over its whole
band, so a causeway is one thickness end to end and a ramp cannot be drawn as the ramp it is.

**A budget for pressure** (`S47`). §12 supplies the numbers, on the right surface: a terrain-only reading of 105
maps. What is still missing is the shape of the rule — a median is not a target, and a map at the 25th
percentile for walkable share is not thereby worse than one at the 75th. What a budget needs is which of these
measures a *bad* map fails, which means labelled examples rather than more measurement. The dressing stage has
the identical gap (`ideas.md` G167) and the two should share one answer.

**Anchors as marks.** Per-vertex anchor heights are exactly a set of point marks on the outline, so a shape with
anchors could be read as a relief with no interior marks and a rim of varying height. Whether to converge the two
representations or keep the plate/staircase path separate is a real decision, not a formality — §13 argues for
keeping it.

**The composer's side** (G32-C). Marks attach to roles and interfaces rather than to coordinates — a raised
spawn for overview, a stepped approach climbing toward a wool room, a low frontline so bridges launch low, a pit
flanking a wool approach (EL7), a scarped bank where the plan wants a one-way lane. That mapping is G32-C's own
work; this document only establishes that what it would emit is what a hand places.

**Moving a structural piece, and editing its height on the canvas** (`B107`). §11's `height_authored` carry is
the storage half only: the shape it applies to is locked in the canvas — rendered, never hit-tested, never
selected — so nothing in the editor writes `floor`/`base_height`/`height_authored` on one yet. What unlocks it is
an inspector affordance on a selected structural annotation restricted to its height, writing through the
sketch's ordinary save rather than a new endpoint. Moving the piece itself, and giving a destroyable or a core
the same treatment, are larger and are not this: a destroyable/core marker has no rect in the plan to surface at
all, so netting it into the sketch is downstream of deciding what its rect *is* — a plan-space question, not a
sketch one.
