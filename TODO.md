# pgm-studio — TODO (current focus)

The **Now & Next** board — only the *current focus theme* lives here. Everything not in the immediate
slice is in **`BACKLOG.md`** (the long tail); shipped capabilities are in **`FEATURES.md`** (the Done
column). The three move left → right: **`BACKLOG.md` → `TODO.md` → `FEATURES.md`**.

**Holds only open work:** `[ ]` to-do, `[~]` in progress — **never `[x]`.** When a task ships, a commit
lands (its message references the id), the task **leaves this file**, and a line is added to `FEATURES.md`.
Board rules live in `CLAUDE.md` (§ "Status & task board").

**One programme, and its foundation has landed.** A room's building and a dressed building are the same thing
— a footprint and a shell — and the first is a **special case** of the second (`WE71`, `FEATURES.md`): one
least span, one ink on all three canvases, and the prose that said otherwise corrected. What is left is the
interaction that shape makes possible. Anything found while working goes to `BACKLOG.md`.

**A building's ceiling stays two numbers, for now (author).** A dressed prop is capped at 192 covered cells
(`HP3`) and a room's building at 20×20 by `ST9`; they measure the same concept since `WE71`, and holding them
apart is a deliberate not-yet rather than an oversight. Nothing below depends on it.

**Three numbers are the author's and are not to be re-derived.** A protection region is at most **20×30**
blocks (`ST10`), a building footprint at most **20×20** (`ST9`), and the smallest room with no building over
it is **4×4** (`WX2`).

**A second programme rides beside the first (author, 2026-09-02): the read-backs a model subtracts from.**
The group below is pulled up whole from `BACKLOG.md`; `docs/world-scan/answer-shapes.md` is its ground.
Nothing in it touches the building programme, and the building programme takes nothing new until this one
drains.

## Read-backs a model subtracts from: the shape an answer arrives in

`docs/world-scan/answer-shapes.md` measures the surface — of the reads that answer a built world, eight
answer only as pictures, one as text, six as numbers — and places the faults agents repeat against the read
that would show each as a number. The driver already writes the board as text beside every picture
(`pgm-studio-mapgen/tools/render/textreads.py`); these move that into the API, where the studio's own
knowledge of a column is available without a sidecar.

- [ ] **WS19 — A section and a transect as text and as numbers.** `render/section` answers a PNG; answer the
  same cut on `?format=text` as one character per block with a y axis and the ground's height under each
  column, and add `GET /map/{slug}/transect?points=x,z;x,z…[&every=N]` answering each station's ground,
  water line, top, provenance claim and the step from the station before, classed as walked (0–1), scrambled
  (2), barrier (3+) or drop (4+ down), as JSON with the text beside it. The extent is the caller's polyline,
  so a feature's own box drives it. The categories are `WorldProvenance`'s and the storeys the layers'.
  `SectionRender.Render` already holds the columns of the cut; `docs/world-scan/read-backs.md` gets the two
  rows.
  *Weirbank's basin: `(-42, 38) ground 38` beside `(-40, 38) ground 22`, a thirteen-course wall read as a
  six-block pond off single columns and a PNG.*

- [ ] **WS20 — A heightmap and a slope grid as text.** `render/heightmap?format=text&every=N` answers the
  ground's height band per cell (`0-9a-z` above the board's lowest ground) with the spawns, goals, houses
  and water overprinted, in the plan grid's shape (scale, extent and key on the first lines). Beside it a
  `slopes` read: the worst step to a neighbour per cell, `.` walked, `:` a block, `#` a barrier, so a cliff
  reads as a line and an overdone relief as a page. `HeightProfileRender` already computes the grid.

- [ ] **WS21 — The walk answers its profile and its neighbours.** `walk` answers the route as cells; answer
  beside them the surface a walker stands on at each cell (the storey, not the ground under a deck), the
  step between consecutive cells classed as `WS19` classes them, and every claim within a stated distance
  of the route (`?beside=2`). It is the read for a thing thrown in the players' way and for a path that
  does not work, and the drive's route profile is a client-side copy of it that cannot know which storey the
  walk chose. `docs/world-scan/read-backs.md` § *What a walk costs*.

- [ ] **WS22 — A theme census.** `GET /map/{slug}/themes/census`: cells per theme, distinct materials per
  theme, which theme borders which over how many cells, and the board's palette count. A board that mashes
  its themes has no gate and no read today; `render/surface` counts tone families into a legend baked into a
  PNG. `TerrainThemeScope` knows every cell's theme.

- [ ] **TS81 — The dressing preview answers its claims as a raster.** `sketch/dressing` answers
  `claimedCells` as a count; answer the claims as digit rows over a bounding box the way `coverage` answers
  its classes — one digit per claim kind (water, structure, door lane, goal clearance, road band, house,
  tree) — so a placement is looked up rather than tried. Placing eleven trees on `fable-mossgill` took ten
  preview passes of trial; `tools/loop.py --candidates` is the workaround. `docs/tools/sketch.md` § the
  dressing preview.

## A building is a footprint and a shell, wherever it came from

- [ ] **B145 — A spawn or wool room's ground carries no theme.** A role piece reaches the sketch as a
  role-tagged annotation and `SketchRasterizer` skips it outright (`line 1027`), so it is never a shape a
  theme can be scoped to: the ground under a room is whatever its fused component paints, and there is no way
  to state a room floor. It is also what `StructureStamper.StampFoundation` levels in, so the material that
  fills a dip under a footprint is the same question. The shape to hang it on is there — a room projects a
  `building` annotation carrying its footprint. Wants a theme scope on it, and the levelling fill reading it.

  *re-probed on `marlstone-steps`: the column under the red wool at `(0, 85)` is raw `1:0` Stone from y24 down
  to y1, on a board whose `crest` theme is quartz. Reported independently by four runs.*

- [~] **B107 — A structural shape cannot be selected, so its stated height cannot be corrected.** The backend
  half is landed (`FEATURES.md`): a shape's stated height survives a recompile, marked per field and carried
  by `intentRef`. `sketch-canvas.js` keeps structural shapes render-only — never hit-tested, never selected —
  so nothing can write the `height_authored` flag a correction sets. Wants selection and an inspector row for
  the stated height. The `building` shape has no height of its own and takes no such row; the region does.

- [ ] **S25b — Make the surfaced spawn/wool shapes movable, writing the move back to the intent.** S25 landed
  them as **locked** read-only rectangles (`FEATURES.md`), and `TN11` added the second one. The next slice
  makes both draggable: moving the region writes `Protection`, moving the building writes `Footprint`, so the
  sketch and the intent cannot diverge. **Resize stays deferred** — a spawn/wool's `at` is a fractional offset
  into the region, so resizing shifts the marker and needs its own handling. Needs a write path
  (sketch → intent); the read projection already exists for both shapes. Then extend beyond spawn/wool to the
  other intent entities (build / monuments / iron) as they each earn a sketch surface.
