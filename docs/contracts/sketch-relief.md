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
below is printed by that tool.

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
is a shoulder along here, this bench is flat. A relief takes exactly those statements and nothing else. A
**mark** is one of them — a patch of the footprint held at a chosen height — and the marks are *placed*, in
the same sense the dressing stage means it: a decision about where the high ground is decides how the map
plays, so it belongs to the person making it rather than to a noise field.

Four kinds cover the vocabulary, and they differ only in the shape of the patch each one pins:

| Mark | Pins | Says |
|---|---|---|
| `point` | a disc of a given radius | a summit, a hollow, a spot height |
| `line` | a band along a polyline, optionally with a height per vertex | a ridge, a valley floor, a shoulder falling as it runs |
| `area` | every cell inside a ring | a bench, a mesa top, a sunken floor — a genuinely flat surface |
| `rim` | the footprint's own outer rings | where the land meets the void |

A point mark with a radius of zero pins one cell and reads as a spike; from about two up it reads as a
summit, which is why the radius exists at all. A line mark's per-vertex heights are interpolated along its
arc, so one drawn stroke can be a ridge that descends. The rim is the mark that keeps a hill inside its shape
— without one, marks alone decide the whole surface and a shape with a single high mark rises to that height
everywhere.

Marks resolve in order and the last one wins a contested cell. That is what lets a bench be drawn over a
slope and flatten it, and it is load-bearing in a way worth stating: a rim written after a ridge cuts a
doorway through both ends of the ridge where the two meet, which hands a router a free way around the high
ground the ridge was placed to create.

## 3. The space between the marks is solved, not tweened

Three fills were built and measured against the same statement on a horseshoe — one arm's tip high, the
other's low, the two tips eight blocks apart in a straight line and forty-eight apart along the land.

**Straight-line weighting** (inverse distance over the marks) is the obvious one and the wrong one: it does
not know the slot between the arms exists, so the low arm pulls the high arm down across open void. Measured
at the two facing banks, it leaves them **5 blocks** apart. **On-land weighting** measures the same weights
along paths that stay on the footprint, by a chamfer sweep that only ever steps onto land; the banks come out
**8 blocks** apart, the full statement. **The smoothest surface** — a relaxation that solves for the field
whose curvature is least subject to the marks — also gives **8**, and it is the one to build on, for a reason
the number does not show.

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
between, which is what a sixty-by-ninety board wants. It is the difference between two summits joined by a
broad saddle and two hills standing in a field.

**Grain** is a deterministic value-noise term added after the solve, in blocks of amplitude at a stated
feature size — the wobble that stops a solved surface reading as machined. It is never allowed to override a
mark: a stated height is a statement, and a field that moved it would make the marks advisory. Grain is
hashed from the cell, never drawn from a generator, so a map re-exports identically.

**Step** is the block quantum the finished surface snaps to. One follows the field cell by cell. Two is the
step unit the hand-built corpus uses (`rules.md` EL1) and reads as deliberate terracing — and it is the one
knob that can break a map. Measured on the board above, terracing at two takes a surface that was one
connected piece of walkable ground and leaves **twelve** disconnected regions, the largest holding 53% of the
map: every terrace riser is now a two-block wall. The repair is to cut a stair through the cheapest riser of
each stranded region until one region remains, which restores full connectivity while changing 0.6% of the
cells — visually almost nothing, which is exactly why the failure is worth catching by measurement rather
than by eye.

## 5. Shapes erected out of the field

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
corpus's own cliff law (`rules.md` EL6) discriminates correctly on the result without being told anything:
over one prototype island, the mesa's face measures **17 wide with a 9-block drop** and qualifies as a cliff,
while the monolith's face — **8 wide, the same 9-block drop** — does not, which is right, because a monolith
is a structure and not a landform.

## 6. What the surface costs to walk

Elevation is the one authoring decision that can silently make a map unplayable, so a relief is not finished
when it looks right. Four measurements are taken off the block surface, and they are the same four whether a
hand or a generator produced it.

The **step** between a cell and each land neighbour, histogrammed: flat, walkable (≤1), a jump (2–3), and a
drop of four or more, which is the boundary `rules.md` EL3 draws between a `land` interface and something
that needs declaring. **Reachability on foot** — a flood fill that crosses a boundary only where the step is
walkable — answers the question the histogram cannot: whether the relief has cut the map into pieces. One
region is the passing answer, and the terracing failure of §4 is invisible in the histogram and obvious here.
**Scarps** are runs of steep boundary with their width and drop, each tested against EL6's cliff
qualification, so the surface reports its own cliffs rather than having them asserted. And **symmetry** is
the worst height difference between any cell and its mirror image, over every paired cell.

Those four are also the whole reason this model can be driven by something that is not a hand. A relief is a
short, declarative object; the readback is a short, declarative report; between them is a loop a generator or
an agent can close — state marks, read what the surface costs, adjust.

The measurement that most changed the design is the one about how much relief a shape can take. On the
30×20 room a capture-the-wool map is actually made of, the same three marks at three amplitudes give: at
**2 blocks**, 100% walkable and one region; at **4**, 91% walkable, no drops at all, still one region; at
**8**, 78% walkable with 36 cells of four-block drop and six disconnected regions. Four blocks over a room is
the working ceiling, and it is the number an author reaches for by instinct — the measurement says why.

## 7. Symmetry is imposed on the surface, not on the statements

A competitive map's relief is the largest single advantage its terrain can hand one team, so a relief fans
across the symmetry orbit exactly as every placed thing does: authored once, mirrored onto its images. That
much is obvious and it is **not sufficient**, which is the finding that most needs recording.

Solving the whole map at once with mirrored marks gives a field that agrees with its own mirror to within the
relaxation's tolerance — and a tolerance is precisely what rounding turns into a whole block. Two paired
cells settling at 8.499 and 8.501 quantize to 8 and 9, and one team owns a step the other does not. Grain
makes it worse and more obviously: sampled from map coordinates it is simply a different field on each side.
Measured over an 11,408-cell rot_180 board, marks left unmirrored give a worst mirrored difference of **8
blocks**; marks mirrored but the grain drawn per side gives **1 block**, over a large fraction of the map;
mirrored marks with the grain folded through the symmetry *and* the solved field folded before quantizing
gives **0**, exactly, everywhere.

So the rule has two halves. The grain is sampled at the **canonical** member of each mirrored pair, the same
discipline a dressing prop's material follows when it resolves in its own frame. And the solved field is
folded the same way before it is snapped to blocks, which costs one pass and turns a guarantee that is close
into one that is exact. Both fold on the cell's **centre**, not its corner — reflecting the corner pairs each
cell with its image's neighbour, a one-cell shear that looks like symmetry and measures as a full block of
unfairness.

## 8. Routes and water, once the ground is not flat

Both stroke tools in the dressing stage assume a flat plane, and both become more interesting than they were
the moment one exists.

A **path** is a finish laid over ground that already exists, so it drapes over a slope and can run wherever it
is drawn — which is also its problem, because a drawn line does not know what it is climbing. Two operations
close that. **Routing** turns a drawn line into a shortest path whose cost counts climbing far more than
distance and which refuses a step no player could take: over a board with a ridge across it and one pass
through, a straight line climbs 13 blocks and tops out at y19, and the routed line climbs **0** and tops out
at y7, through the pass. **Grading** is the other answer — cut and fill a corridor so its surface never steps
more than one block along its length, with the shoulders blended back into the field over a couple of blocks
so the road does not sit in the landscape as a trench. On the same board terraced at a three-block step, the
drawn line's worst step is 3 and grading takes it to 1. The two are a real choice, not a pipeline: route
around the hill, or cut through it.

**Water** has to run downhill, which turns out to be the one place where the obvious algorithm fails outright.
Steepest descent from a source stops at the first cell with no lower neighbour, and a grained surface is full
of one-block pits — measured, the descent stopped after **2 cells**. Water is therefore routed on a
**depression-filled** copy of the surface, where every basin has been raised to the level of its lowest
outlet, so a strictly-non-climbing path to the edge of the land always exists; the same run then covers **44
cells**. The filled copy is only ever used for routing — a pit an author dug is theirs to keep, and the
carve goes into the real surface.

Two consequences follow. The bed's floor is forced non-increasing downstream, so the channel cannot run
uphill however the centerline wanders. And the water line steps down only where the bed does and holds level
in between, which turns a single water line into a chain of pools — the measured run holds **12 distinct
levels**, which is what a stream down a hillside actually looks like and what `decoration.md` §7's single
lowest-surface water line cannot express once the ground has relief. A basin is also where a channel ends,
alongside the map edge: a cell the depression fill had to raise is announcing that it sits below its own way
out, which is the definition of a pond.

The relationship runs the other way too, and it is the cheapest good idea here: a drawn channel is a **valley
mark**. A route drawn as a river can be handed to the solver as a line mark below the base level, and the
terrain forms a valley around it rather than the channel being cut into a surface that ignores it.

## 9. Where it lives

The solver reads a footprint mask and produces a height grid, and knows nothing about maps, so it belongs in
the dependency-free leaf beside the other pure algorithms: **`PgmStudio.Geom.Relief`** — the mark types, the
spec, and the relaxation. Classification is not pure geometry — a cliff is a corpus rule about play — so the
step histogram, the walkable-region flood and the scarp qualification live in **`PgmStudio.Analysis`**, where
the other derivations that read a surface already are. `SketchRasterizer` consumes the result: a shape
carrying a relief takes its thickness from the solved field instead of from the per-vertex triangulation, and
nothing else about the rasterizer changes, because the field answers the same question the triangulation did.

The canvas preview is the JS twin, the same arrangement `Geom.Symmetry` already has with
`js/studio/geometry/symmetry.js`. What it draws is the field's **contours**, which is both the readable view
of a height field and the direct-manipulation surface: dragging a contour line is dragging a line mark at
that height, so the topographic reading of the surface and the way it is edited are the same object.

A relief rides on the shape, beside the height fields it generalizes:

```json
"relief": {
  "base": 4,
  "fill": "smooth",
  "reach": 18,
  "step": 1,
  "grain": { "amplitude": 1.2, "scale": 11, "seed": 7 },
  "marks": [
    { "kind": "point", "at": [12, 30], "h": 9, "r": 3 },
    { "kind": "line",  "points": [[4, 4], [20, 10], [30, 24]], "h": [7, 9, 5], "width": 2.5 },
    { "kind": "area",  "ring": [[10, 40], [40, 34], [46, 62], [14, 68]], "h": 10 },
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

## 10. What it costs to edit

A relaxation is iterative, so the question the whole authoring story turns on is whether it can run under a
drag. Measured, a full solve takes **7 ms** on a 30×20 room, **171 ms** on a 62×92 board and **624 ms** on a
124×92 whole map — fine on release, far too slow at pointer rate on anything but a room.

Resuming from the surface already on screen is what makes it affordable, and the property is worth stating
plainly: moving one mark perturbs the field locally, so a warm-started relaxation has only that perturbation
to carry rather than the whole surface to build. Forty sweeps from the previous solve costs **151 ms** on the
whole map and lands, in blocks, on the settled answer everywhere but **126 of 11,408 cells**, each of those
off by one. That is a preview: the drag warm-starts, the release solves in full.

## 11. What is open

The **composer's** side. Marks attach to roles and interfaces rather than to coordinates — a raised spawn for
overview, a stepped approach climbing toward a wool room, a low frontline so bridges launch low, a pit
flanking a wool approach (EL7). That mapping is G32-C's own work; this document only says the marks it would
emit are the marks a hand places.

**Relief across shapes.** A relief is solved per shape, and an island is many shapes. Solving each
independently leaves a seam where two adjoining shapes disagree about the height they share. The candidates
are to solve per *island* over the fused footprint, to let a shape inherit its neighbours' edge heights as
marks, or to declare the seam a scarp on purpose — and the first is probably right, since the island is
already the unit theming and detection use.

**Anchors as marks.** Per-vertex anchor heights are exactly a set of point marks on the outline, so a shape
with anchors could be read as a relief with no interior marks and a rim of varying height. Whether to
converge the two representations or keep the plate/staircase path separate is a real decision, not a
formality — §9 argues for keeping it.

**The readback surface.** The four measurements are computed but have no endpoint. What makes a relief
drivable by an agent is that the report is fetchable next to the document it describes.
