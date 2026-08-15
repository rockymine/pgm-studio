# pgm-studio — Backlog (later)

The **long tail** — open work that isn't in the current focus. The active slice is in **`TODO.md`**;
shipped capabilities are in **`FEATURES.md`** (the Done column). Flow: **`BACKLOG.md` → `TODO.md` →
`FEATURES.md`**.

**Holds only open work:** `[ ]` to-do, `[~]` started-but-parked — **never `[x]`.** A task lives in
exactly **one** of the three files; pull one up into `TODO.md` when it becomes now/next (its id does not
change). Sections + ids match `TODO.md` — a task slots into the same section wherever it lives. Parked /
deferred items stay here, flagged inline. Board rules live in `CLAUDE.md` (§ "Status & task board").

Task ids are a section letter + number, **globally unique and stable** across all three files; never
renumber or reuse.

## Authoring (N) — the new-map intent editor (`/maps/{id}/configure`, new maps only)

The guided wizard at `/maps/{id}/configure` (UI label **Configure**) that builds a map from declarative
intent (`docs/pgm/new-map-authoring.md`; backend + every page-order step are landed —
`FEATURES.md`). **Leave the existing Edit editor untouched** — a separate surface, not a refit. Only
the focus-integration polish remains.

- [ ] **N08 — Monument Y via side-view + per-side focus.** The side-view (`SliceView`) already sets Y on
  **spawn** and **wool-spawn** (`SpawnStep`/`WoolSpawnStep`, `FEATURES.md`); the open slice is the rest:
  (a) wire the side-view into **`WoolMonumentsStep`** so a monument's Y is editable, not read-only
  (lift it off y=0 onto terrain); (b) **per-side focus** — `FocusSection` is still a `/concepts` mockup;
  the canvas **fit-island** exists but not per-team quadrant framing — refine the concept so the author
  can frame one team's quadrant while working its unit. (`FocusSection`)
- [ ] **N09 — Team id should track the team's colour.** The team id is seeded from the colour first picked
  (`Id = colour.Replace(' ','-')`), but `TeamAssignStep.SetColor` only updates the colour — so recolouring a
  team (e.g. red → purple) leaves `id="red"` and every id derived from it (`only-red`, `red-spawn-point`,
  the `…-red-monument` blocks, `reds-woolrooms`). Functionally fine (PGM resolves the id) but reads wrong.
  Re-derive the id on colour change and **cascade the rename** across the intent — `teams`, `islandTeams`,
  and `spawns[].team` / `wools[].owner` / `wools[].monuments[].team` — with a guard to skip the rename (just
  recolour) when the new colour-derived id would collide with another team's.
- [~] **N11 — Monument Y must seat on terrain; coord-input moves must re-snap.** The **point tool** now
  seats every spawn it places — team spawns + orbit copies, the observer, and wool spawns — on the target
  column's floor via the shared `ColumnFloor` helper. Still open: monuments aren't seated at all; and moving
  a spawn (team or wool) via the **coord inputs** rewrites X/Z without re-snapping Y to the new column, so
  only the point tool re-seats. Pairs with `N08` (monument Y editing) and `CV11` (the side-view clamp side
  of the same problem).
- [~] **N12 — Configure has no destroyable phase.** Wools and Cores each have one and the objective phases
  are a group sharing one gate (`FEATURES.md`), so this is now the third phase slotting into machinery that
  already exists: add `destroyables` to `ConfigurePhases` + `IsObjective`, a `DestroyableAuthoring` slice
  beside `CoreAuthoring`, and the steps. A destroyable is the core's shape with a different structure — one
  region per defending team, no per-capturing-team monuments — but its knobs are style/materials/float
  rather than a casing. A DTM map authored in the plan tool can already be configured (the slice rides
  through untouched); what it cannot be is *seen* or edited there. Detection is a separate question and is
  `B58`: unlike a core, a destroyable has no signature of its own, so the phase should offer manual
  placement first and adopt candidates when that ranker lands.

## Sketch tool (S) — parked slices

The Sketch depth pass has shipped (`FEATURES.md` — select/drag, rotate, scale/squash, split, selection
highlight); these are the parked / dormant / deferred slices.

- [ ] **S42 — Relief: the carve and the graded road fold too.** The solve folds, and so now does the stair cut
  (`FEATURES.md`) — the first later pass to land, and the one that showed the rule is real rather than
  theoretical. The other two are still open: a carve and a graded road each decide things by **walking** the
  map, and a walk has a direction the half-turn does not preserve, so each folds again or it undoes what the
  solve established (`world-export/relief.md` §8). Measured on the designed map, a carve that did not re-fold left
  the two halves **9 blocks** apart. Belongs with S46, which lands both passes; the fold itself needs no new
  machinery — `ReliefSolver.FoldBlocks` is the shape of it.

- [ ] **S54 — The 3-D preview shows neither the relief nor an erected top.** The iso builds one prism per
  shape at `floor + base_height` (`sketch-bridge.js` `isoSolids`), which was the whole truth when a shape's
  thickness decided its column and is now two-thirds of it: an island's relief replaces those tops, and an
  erected shape's is settled after the relief against the ground under it (`FEATURES.md`). So the one view
  that exists to show height is the one view that does not show the height model — a mesa reads at its own
  thickness and a hillside reads as a plate. The fix is to build the solids from the **rasterizer's columns**
  rather than from the shapes, which is also what makes it agree with the export by construction instead of
  by a second implementation; the cost is that a column set is bigger than a shape list and the iso would
  want run-merging to stay cheap. Pre-dates the relief work — the iso never showed a relief — so this is a
  gap widening rather than a regression.

- [ ] **S46 — Water reads the relief; a river on the axis is a canal.** A dressing path draping over whatever
  it crosses is **settled as correct** — it repaints the top block of each column and adds no cell, which is
  what lets a road cross a slope without becoming a ramp, and routing or grading it would be the tool
  deciding where the author's road goes. Terrain that a route *emits* is the draw phase's path primitive
  (`FEATURES.md`) and the erected-shape modes, not this. Water is the half that is genuinely wrong on a
  relief, because it has to obey the ground rather than sit on it. It needs three things the flat model never
  did: routing on a **depression-filled** copy, because steepest descent stops at the first grain-made pit after 2 cells
  where the filled run covers 65; a bed floor forced non-increasing downstream; and **per-pool** water levels
  replacing `decoration.md` §7's single lowest-surface line — the measured run holds 14 distinct levels, and a
  basin is an outlet alongside the map edge, which is what a pond is. The exception is the case that matters
  most: **a river on the mirror axis cannot both fall and be fair**, because a half-turn reverses the flow, so
  on the axis it is a canal at one level and falling water belongs to the flanks. And the cheapest good idea
  here runs the other way — a drawn channel handed to the solver as a line mark below base level makes the
  terrain form a valley around it (`world-export/relief.md` §9).

- [ ] **S56 — A path's height varies along it.** The path primitive takes a uniform `base_height` over its
  whole band (`FEATURES.md`), so a causeway is one thickness end to end and a ramp cannot be drawn as the
  ramp it is. A polygon already solves the equivalent problem with `anchor_heights` index-aligned to its
  vertices and TIN-interpolated across the footprint, but a path's footprint is not its vertices — the band
  is derived from a smoothed centerline, so the interpolation runs **along the arc** rather than over a
  triangulation: each band point knows how far along it sits (`PathHit.Along` already carries this for the
  stroke), and the height is read between the two authored vertices that bracket it. That gives a graded road
  that is authored, not inferred, which is the distinction that keeps it out of S46. The erected modes then
  compose on top as they do for any other shape, so a sunk tilted path is a cutting and a raised one an
  embankment.

- [ ] **S47 — A pressure budget for relief.** S43 measures what terrain charges; nothing says how much
  charging is too much. The dressing stage has the identical gap (`world-export/ideas.md` G167) and the two
  should share an answer. The materials exist — the share of the board at each passability tier, the detour
  factor between key places, the ford count and direction on a barrier, the reachable share per team side —
  and the corpus pass has now run on the right surface (`world-export/relief.md` §12, 105 maps, natural ground):
  body relief median **19 blocks**, walk median **72.6%**, barrier median **18.3%**, largest walkable place
  median **29.4%**, **8** cliffs. Filtering the architecture out made the terrain read *steeper*, not gentler
  — a building's flat roof was smoothing the reading — so the tier shares were never the distorted numbers;
  the **cliff count** was, and heavily (Alpine Mining II: 36 cliffs off the built surface, 13 off natural
  ground). What is still missing is the shape of a rule: a median is not a target, and a map at the 25th
  percentile for walkable share is not thereby worse than one at the 75th. That needs labelled examples of a
  *bad* map rather than more measurement.

- [ ] **S34 — Reuse a sketch paint's column classification across the edits of one drag.** `TerrainProfile`
  construction is what a paint now costs — ~60 ms of the ~164 ms a 40k-cell board takes (S33, `FEATURES.md`),
  and roughly 35 ms of that is its two `GridComponents.Label` passes: one flood fill for plateaus, a second for
  landmasses, each sorting its seeds and hashing a coordinate pair per neighbour edge. They re-run from
  scratch on every step of a drag, though only the moved shape's neighbourhood changed and the plateau
  components are already a refinement of the landmass ones (equal-top cells are 4-connected), so the second
  pass could be merged out of the first. Whether the rest is worth an incremental cache depends on a number
  nobody has: a typical board is ~93 ms end to end now, so this is the 200×200 case, not the common one.

- [ ] **S40 — Offer "no building" in the Rooms step.** A bound room style has three answers — a style, absent
  (the built-in shell), and an explicit null meaning the pad stands on open ground with nothing over it
  (`docs/world-export/structures.md` §9). The export reads all three and the stampers have always accepted
  the third, but the step can only *bind* or *clear*, and clearing means the built-in rather than none. So a
  map can be authored open only by writing its layout by hand. The step needs a third control per kind, and
  `ReadBindings` needs to tell a null snapshot from a missing one — today the bridge state drops both, so an
  open room displays as unpicked (harmless until the author touches it, since the save preserves what it
  loaded).

- [ ] **S26 — Sweep the now-dead plan-side theme JS.** The Theme phase moved onto the sketch (`FEATURES.md`);
  the plan tool's Theme UI + C# theme path are gone, but the plan **JS** theme code is still present as an
  unreachable, self-consistent island: `plan-bridge.js` theme methods (`themesState`/`assignPiece`/`assignBox`/
  `themeApply`/`getThemes`/… + the `defaultThemeJson` import), `plan-canvas.js` theme mode
  (`setSelectOnly`/`setThemeOverlay`/`setThemePaint`/`getMultiSelection`/`#selSet`/`#themePaint`), and
  `plan-doc.js` theme storage (`defaultThemeJson` — now duplicated in `theme/theme-model.js` — plus the
  `themes`/`mapTheme`/`themeScopes` normalization). Remove them together (they cross-reference), keeping the
  generic `uniqueId`. Check whether plan-canvas multi-select (`#selSet`) has any non-theme use before dropping it.
- [ ] **S25b — Make the surfaced spawn/wool pieces movable, writing the move back to the intent.** S25 landed
  the pieces as **locked** read-only rectangles (`FEATURES.md`; `role`/`intentRef` on `SketchShape`, projected
  by `PlanCompiler`, skipped by the rasterizer, rendered as labelled boxes). The next slice makes them
  draggable: a move writes the new rect back to the intent's `Piece`, from which `Protection`/`Room`/marker
  re-derive, so the sketch and the intent don't diverge. **Resize stays deferred** even here — a spawn/wool's
  `at` is a fractional offset into the piece rect, so resizing shifts the marker and needs its own handling.
  Needs a write path (sketch → intent); the read projection already exists. Then extend beyond spawn/wool to
  the other intent entities (protection / build / monuments / iron) as they each earn a sketch surface.
- [ ] **S9b — Angle/parallel snapping + droppable guide lines (parked).** S9 landed **position** alignment
  (edges/centres snap to other shapes + the symmetry centre, with guides). The remaining picture-editor bits:
  **angle/parallel** snapping (rotate a shape so its edges run parallel to another's — "hold two lanes
  parallel"), and **manually droppable** guide lines shapes snap to (vs the current auto-from-shapes). Both
  are their own work; park until needed.
- [ ] **S12 — Pin the Islands tree to the top of the sketch sidebar (UI polish, parked).** Most of the weight
  the original review named is gone: the shape palette was retired outright and Setup moved into its own Info
  phase, so the only panel still above **Islands** is **Layers**. Collapse it behind a `<details>` accordion,
  or pin the tree above it — the tree is read on every edit and the layer list is set once.

- [ ] **S57 — The measure tool is a ruler where the question is a gap.** `sketch-canvas.js` measures the raw
  drag between two freely-placed points and reports `Math.hypot` as "N blocks" (`#renderMeasureLabel`). The
  question it exists to answer is how wide a void gap is — the 10–15 block lane, the jump a player can make —
  and a free ruler eyeball-aimed at two edges is barely better than reading the cursor twice. Three parts,
  each usable alone: **snap the endpoints** to nearby shape and island edges, reusing the `#snapTargets` /
  `bestSnap` machinery the drag already has; **decompose the readout** into ΔX × ΔZ beside the diagonal, since
  a lane's width is an axis extent and not a hypotenuse; and, the real target, **measure the gap rather than
  the drag** — hover or select two islands and draw the nearest-point line between their outlines with the
  block count on it. The islands are already computed live in JS (`computeIslands`), so the nearest-point
  pass is the only new geometry.

- [ ] **S58 — The sketch canvas has no shortcut surface, and its best affordances are secrets.** Escape,
  Delete, `P` to promote, arrows to nudge (Shift for sixteen), double-click to close a polygon, **Ctrl-drag a
  vertex handle for a Bézier tangent** and **Alt to bypass snapping** are all live, and they are spread across
  `sketch-canvas.js`, `sketch-bridge.js` and `sketch-edit-controller.js` with nothing in the UI naming them —
  a grep for "shortcut" across `Features/Sketch` and `Components` returns nothing. The last two matter most:
  Bézier editing is the thing that makes an outline stop being rectilinear, and it is reachable only by
  someone who already knows. A "?" popover on the canvas chrome listing the set, plus tool-contextual hints
  where a tool has a non-obvious step ("click to add · double-click to close" while the polygon tool is
  armed), is the whole of it.

- [ ] **S59 — Per-vertex height is the headline feature and is found by accident.** The path is: select a
  polygon, read the one conditional sentence in the inspector, click a vertex on the canvas without moving it,
  then type into a field that appears in the panel. On the canvas a vertex handle looks exactly like a drag
  handle and its height is a bare text label, so nothing says a click-without-drag does something a drag does
  not. Make the height labels read as interactive (a pill or a hover state), and ideally let the label itself
  be edited or scrolled in place rather than round-tripping to the inspector. The shift-click 2–3 vertices
  slope-fit has the same problem and the same fix. Pairs with `S54`, since the 3-D preview is where a height
  edit is actually legible and it is a modal swap rather than a companion view.

- [ ] **S60 — A building prop can state more than one wing; the canvas can still only drag one.** `HouseProp`
  carries `wings`, a list of touching rectangles, and `Decorator` composes them into one `Footprint` and stamps
  once (`G177`) — an L, a T or a U is authorable today by anyone writing the document directly. The dressing
  tool itself still only ever drags a single rectangle: there is no way on the canvas to add a second wing to a
  placed building, drag one of several independently, or see a proper L/T/U outline rather than one rectangle
  per wing (`wingRings` in `dressing-render.js` draws each wing's own box rather than the traced silhouette a
  build actually stamps). Wants a second interaction — add-a-wing, probably a drag that starts touching an
  existing wing's edge — and a handle set that knows which wing a grip belongs to.

## Editor & canvas infrastructure (C / CV)

Shared infra for **both** the Configure wizard (`/maps/{id}/configure`) and the frozen Edit editor
(`/maps/{id}/edit`). `C12`/`C14` are cross-cutting (serve both surfaces); `C9`/`C11`
are Edit-specific. Full canvas spec: `docs/client/canvas-interaction.md`.

- [ ] **C9 — Kits editing UI (Teams) + per-activity status dots.** Spawn `kit` is read/sent but has no
  edit UI; there is no status-dot system. *(Two sub-items — split if priorities diverge.)*
- [ ] **C11 — Wire + verify inspector edits across activities.** `OnDelete`/`OnRename` are wired only
  in Build Regions; the Regions/Teams/Objective inspectors are **unwired** (rename/delete silently
  no-op). Wire all three + verify rename/delete/coord-patch end-to-end.
- [~] **C12 — The last of the component vocabulary: the icon, the generator, the inline styles.** The
  vocabulary is built and adopted — the atoms, `Section`, the shell and the workspace shells are across every
  production surface, with two raw `action-btn`s and two raw `list-row`s left as genuine exceptions
  (`FEATURES.md`; the reference is `docs/client/ui-conventions.md`). Three slices remain, and `/design` is the
  zero-visual-diff oracle for all of them since a component emits the classes the markup did.

  **`Icon` is built and unadopted.** `Components/Primitives/Icon.razor` centralizes the lucide reconciler
  gotcha — recreate on a glyph change rather than patch a node lucide has already replaced with an `<svg>` —
  and **156 raw `<i data-lucide>` still stand**. Adopt incrementally: the icon-bearing components
  (`Button`/`DetailHeader`/`Chip`) first, then the page sites that re-render. High churn, subtle benefit, so
  parked by choice rather than blocked.

  **The `gen-*` set is the last real drift**, and the largest thing left here: `/generator`'s filter rail, card
  grid, candidate cards, badges, tray and census tables are around forty classes in `generator.css`
  re-implementing `workspace-sidebar`, `card-grid`, `badge` and `filter-chip` under their own names. The atoms
  inside them have been picked up where they fit; the layout has not.

  **Polish**: fold the one `section-heading` use into `SectionHeader`, and drop the 84 inline `style=`
  occurrences now expressible as component params (`Fill`, `Full`, a modifier `Class`).

- [ ] **C14 — Dedupe activity code-behind.** The repeated `Post/Patch/Delete/Send` http trio
  (Build/Objective/Teams) + the `Index`/`CollectDescendants` region-tree walkers (3–4 activities) →
  a shared `MapApiClient` and/or `EditorActivityBase` / static `RegionNode` helpers.
- [~] **C28 — The client's remaining test layers (smoke has landed).** The **smoke layer + runner shipped**
  as `C31` (`tools/e2e.sh`, `tests/e2e/`) — every route is swept for "renders and raises nothing", seeded
  from a composed board; `icons.mjs` (C30) added the first *positive* render assertion on top of it.
  `PgmStudio.Client` is still **absent from the coverage report** (no test project
  references it), and two layers are still open:
  **(a) mount/interop** — per canvas tool, assert the bridge mounted and the surface has a real size; this
  is the C29 class of bug (a canvas at 45% of its workspace for weeks, in two tools) and it is assertable
  without knowing user intent.
  **(b) scenarios** — one flow per tool, specifically *the path that creates the artifact*, where a break is
  unrecoverable rather than cosmetic: Sketch `New → name → draw → Finish → Configure`; Plan
  `New → globals → piece → Compile`. The seed already proves that chain works headlessly.
  Deliberately **not** e2e: field-level inspector behaviour and anything asserting where geometry lands —
  those rot; extract the decidable logic instead (`CV12`). A bUnit project for the phase/step state machines
  is still worth considering, and is independent of the above.
- [ ] **CV12 — Two thirds of the JS layer is never loaded by a test.** `npm test --
  --experimental-test-coverage` reports 82.8% over the 15 modules the 148 tests import (several at 100%:
  `transform`, `symmetry`, `islands`, `polygon`, `plan-inspect`), but **26 of 41 files / ~6,900 lines are
  absent from the report** — they are never imported, which the coverage output shows as silence rather
  than zero. The untested set is the whole interactive layer: every canvas (`world-canvas` 1046,
  `plan-canvas` 1017, `sketch-canvas` 871, `sideview-canvas`, `canvas-base`), every bridge, every
  controller, `iso-webgl`, and `studio.js`. The split is coherent — pure geometry is tested, DOM/canvas
  code is not — so the win is not "test the canvases" wholesale but **extracting the decidable logic they
  contain** (hit-testing, snapping, viewport/transform maths, selection resolution) into pure modules the
  existing `node --test` setup can reach without a DOM. Pairs with the JS consolidation review.
- [ ] **CV21 — the world canvas has a `build` layer nothing paints into.** Stating the layer stack once
  (`CV19`) surfaced two layers with no content. One was removed there — a `block-highlight` rect created
  `visibility:hidden` whose only handle was assigned and never read. This is the other: the `build` group is
  created empty, no painter ever appends to it, and its toggle `setBuildVisible` has no caller outside the
  class — not the bridge, not any of the sixteen hosts. So it is an empty group with a visibility switch
  nobody throws. Removing it takes `setBuildVisible`, `#showBuild`, `#paintBuildRegion` and one line of the
  documented public surface with it, which is why it was left in place rather than swept during a
  behaviour-preserving refactor. Check first whether a Build phase was *meant* to fill it (the name suggests
  the Build-Regions work) — if so the task is to wire it, not delete it, and that is a different task in the
  feature section.

- [ ] **CV15 — The bridge invoke wrapper is inconsistent.** `plan-bridge` and `sketch-bridge` wrap
  `dotnetRef.invokeMethodAsync` in a local `fire()` that swallows the throw when the host hasn't wired a
  callback; `world-bridge` calls it unguarded, so an unwired callback surfaces as a console error instead
  of a no-op. Settle on one helper next to `fetch-json.js`. Tiny, but it is the only thing the five bridges
  genuinely share — the rest of their apparent repetition is per-tool document semantics and should stay
  separate.

## Backend, pipeline & internals (B / P / A)

- [ ] **B210 — Eighteen declarations of "a box with things in it", and the two that matter are forced by a
  project boundary.** Filed as "four rect types", which was too narrow and carried one wrong claim: `Geom`
  **does** hold plan-space measures — `Cells.PathLength`, `ShortestPath`, `BoundingBox`, `MinRunWidth`, all on
  `(int, int)` cell sets. The landing site exists. What is actually duplicated is the *shape*, and three of the
  declarations are deliberate and settled and must stay that way.

  **Already settled, do not touch.** `BlockBox` (`Domain`) is the one inclusive integer 3-D AABB for every role
  a block volume plays (`B33`). `StructureBox` (`Api.Services`) is a drawing frame — exclusive maxes plus
  `Kind`/`Color` — and `B33` and `B37` both record that it is deliberately not a third copy. `CellRect`
  (`Geom`) is the 2-D integer plan rect, and the compose side already standardised on it: `Compose/Boxes/Box.cs`
  carries `CellRect Rect` and `BoxKind`, so the partition scaffold, the plan model and the layout composer all
  read one type.

  **What is actually duplicated, in two groups.**

  *The 2-D integer min/max rect, twice.* `BlockRect(MinX, MinZ, MaxX, MaxZ)` in `Pgm/Derive/ContactGraph.cs`,
  and a field-identical **private** `Rect` inside `Geom/RectilinearUnion.cs` — a second min/max rect in the leaf
  that already exports `CellRect`, which has `FromBounds`/`FromInclusive` and would have served.

  *The 2-D double min/max rect, **nine** times.* `MapIntent.Rect` is the wire shape and is the one that should
  survive; `ResourceRenewables.Box` is a private copy of it in the same project; `tools/mapgen/Extent` is a
  third; and **six more live in `Client`** — `WoolAuthoring.Rect`, `WoolObjectivesStep.Rect`,
  `ReviewPreflightStep.Box`, `BuildLayerStep.Box`, and `ProtectionStep` alone declaring `Rect`, `ProtRect` and
  `Row` carrying the same four doubles three times in one file.

  **The client copies are not sloppiness — the boundary forces them, and that is the finding.** `Client` is
  WASM and references only `Contracts` and `Geom`, so `MapIntent.Rect` (in `Pgm`) is unreachable from it and
  every configure step re-declares it. The same boundary produces `CoreAuthoring.Box(int MinX, MinY, MinZ,
  MaxX, MaxY, MaxZ)` — field-identical to `BlockBox`, which `B33` consolidated in `Domain`, which `Client`
  cannot see. So `B33`'s shared type is shared everywhere except the half of the codebase that draws it.
  `CLAUDE.md`'s own rule names the fix: the shape both sides need goes in the leaf both sides reach, which is
  **`PgmStudio.Geom`** — the same reasoning that put `Symmetry` there.

  **Scope, smallest first:** the double 2-D rect into `Geom`, with `MapIntent.Rect` and all six client copies
  reading it; `RectilinearUnion` using `CellRect`; `BlockRect` folded into `CellRect` or kept with its
  relationship stated rather than implied; `CoreAuthoring.Box` reading a `Geom` 3-D box that `BlockBox` also
  reads. **Not** a distance-measure task — that is `B37`, below.

  *found reviewing dispatch readiness, corrected 2026-08-15 against `B33` and `Compose/Boxes/Box.cs` ·
  18 declarations across `src/` and `tools/`.*

- [ ] **B212 — Two units are both called "blocks apart", and bucket 3 is calibrated in the one the code does
  not use.** `WoolWoolDistance` (`WL7`, `Evaluate/Terms/ObjectiveTerms.cs`) is the studio's one implemented
  separation rule, and its docstring states the measure outright: distance is **rectilinear traversal over the
  walkable surface**, 4-connected, routing around voids — "and correspondingly higher than a straight-line
  reading of `WL7`'s ~45".

  Every number bucket 3 is calibrated against was read the other way. `B175`'s "at least 35 blocks" comes from
  regions in a shipped `map.xml`; `B179`'s "nearest enemy goal at 95–110" from the same; `B188`'s whole
  164-map table — spawn→own goal 49.4 median, ratio 2.9 — was parsed "from the XML alone", which is centres and
  extents, not a walk. **And the sweep that produced that table is not in the repository**, so the metric behind
  the one corpus band calibrating buckets 2 and 3 cannot be recovered from the code; it survives only as
  numbers in a backlog entry.

  So an agent handed `B175` does one of two things, and neither fails a test: writes a second, straight-line
  measure beside the traversal one, or reaches for `Cells.PathLength` — correctly, it is right there in `Geom`
  — and applies a threshold calibrated in the other unit. `B175` even points at the collision without seeing
  it: "the rule shape exists on the wool side" is exactly `WL7`, and `WL7` is the measure that disagrees.

  **Scope:** state the metric on every distance in buckets 2 and 3, decide which one the goal-separation rules
  use (the author's call — a straight line and a walk answer different questions about a board), and re-derive
  the corpus band in that unit with the sweep committed this time. `B188`'s own closing note is the reason not
  to skip it: it concluded the ratio fault is board *shape* rather than goal placement, which is a claim about
  what the number means and cannot be checked without knowing how it was taken.

  **The flow read is already in the right unit, which narrows this.** `G127`'s prototype measures in proxy
  cells over the walkable mask (×5 = blocks) — `WL7`'s unit, not the corpus band's. So the two *derivations*
  agree and it is bucket 3's authored thresholds that stand alone in straight-line blocks.

  *found reviewing dispatch readiness, 2026-08-15 · `Evaluate/Terms/ObjectiveTerms.cs:5-10` ·
  `Geom/Cells.cs:48,77` · bucket 3's `B175`/`B179`/`B188`.*

- [ ] **B211 — One gate still answers a bare list and asks its question by counting.** `B191`/`B192` gave
  every gate one `Findings` and one `Refuses`, and `Producibility` did not move with them:
  `BoxProducibility.Findings` and `PlanProducibility.Unit` are both `IReadOnlyList<Finding>`, and
  `PlanProducibility.IsProducible` reads `Unit.Count == 0`.

  **It is not currently wrong, and that is what makes it worth fixing before the rule buckets run.** Every
  finding a producibility read emits is passed through `Complaint()` at both boundaries by construction — the
  type's own docstring says a read is "**complaints**, never refusals" — so counting them is asking the right
  question today only because the severity is constant. `CLAUDE.md` names this exact shape as the half-fix
  that bites: *"every caller re-derived 'was anything refused' from a `Count`, which is right for a gate
  reporting only refusals and silently wrong for one reporting complaints too."* Producibility is the mirror
  case, one severity change away from the same silence.

  Buckets 1, 2, 3 and 10 all add rules and one of them will reach this file. What an agent finds there is a
  `List<Finding>` and a `Count`, which is the shape the vocabulary was consolidated to remove, and it will be
  copied because it is what the surrounding code does. **Scope:** both fields become `Findings`, `IsProducible`
  reads the question by name, `FeasibilityDto`'s `Unit` follows, and `Refusals.Dtos` keeps working unchanged.

  *found reviewing the dispatch readiness of the rule buckets, 2026-08-15 ·
  `Pgm/Compose/Producibility.cs:37,47,50,178`.*

- [ ] **B207 — `doc-status.md`'s churn ranking was measured over three days and reads as if it were history.**
  The container that produced §3.4 saw 197 commits over three days, so every "last edited" date and every
  churn count in it is accurate only for that window: a document showing zero churn was not necessarily
  untouched for months, and one showing high churn was not necessarily edited recently in the way the number
  implies. §3.4 is the ranking a reader would use to decide which documents are drifting, and it cannot see
  drift older than the window — which is most drift. The opening note says so, which stops it misleading a
  careful reader and does not make the numbers usable.

  Re-run it against the full history and restate §3.4. Worth doing when the ranking is next going to be acted
  on rather than on its own, since the numbers only matter as an input to choosing what to fix.

  *carried out of `B111`, where it was a trailing note on an otherwise finished task (board rule 6) ·
  `docs/doc-status.md` §3.4 and its opening note.*
- [ ] **B189 — The authoring apparatus: art direction, named briefs, and a reviewer that is not the author.**
  Three runs asked three models for "a map of your own design" and got, three times over, one street of
  identical houses behind the spawn on a square board under a palette nobody checked. The brief is where that
  came from: it asked for a **visual identity you can name in a sentence** and then supplied no vocabulary for
  saying one, so every model reached for the same defaults, and it asked each model to review its own work,
  which is how a report came to describe two empty shells as "verified working with 2 destroyables".

  The replacement is four documents in `pgm-studio-mapgen`. **`ART-DIRECTION.md`** is the visual law, and it is
  deliberately **literal** — the last brief asked for "a visual identity you can name in a sentence" and
  supplied no vocabulary for saying one, so it names blocks. It carries the nineteen hand-authored tone
  families with their members, and the rule the boards all missed: **a pattern takes two or three members of a
  family, never the whole family** — Kilnrow's `cell` over five near-identical whites is why that board reads
  as clashing though every block in it is pale. It carries the material rules the audit measured (one course of
  grass, a slab only on a half-course rise, no log in a roof, no footing on terrain, obsidian at three blocks),
  and **five omissions nothing ever flagged because they were absences rather than mistakes**: nobody planned
  the board's silhouette or aspect ratio before drawing; **no board in twenty-one has ever authored a path**,
  which is the circulation diagram drawn and the thing that keeps the ground along a route clean; everybody
  turned the rim on, including on relief-solved ground, where it terraces a rolling hill into contour lines;
  landforms never flowed into each other, the named fault being a flat 20×20 pad butted against a hill with no
  `skirt` and no tilt between them; and the only built thing any board has ever themed is a village behind the
  spawn. It points at the ten shipped `HousePresets` as the starting point, since `Desert` and `Diorite` between
  them model two of the most-broken rules.

  **`MAP-BRIEFS.md`** replaces "a map of your own design" with **named briefs** — each stating the tone families
  to build from, the preset to fork, the routes to draw as paths, how its landforms meet, and the one thing it
  tests. Two exist because they have never been attempted at all: a **desert** board, and a **four-team**
  `rot_90` board, both fully expressible and neither ever authored. **`REVIEWER-BRIEF.md`** is a **second
  agent**, given the board and not the author's intent, whose checklist is the author rules with their numbers
  in it and whose output is a per-item table with coordinates. **`AUTHORING-BRIEF.md`** is the author's own
  brief rewritten around the other three.

  What this entry owns is keeping them true. Every rule in `ART-DIRECTION.md` and every check in
  `REVIEWER-BRIEF.md` is a `B141`–`B188` id that has not shipped yet, so **each one is a debt with a due
  date**: when its bucket lands the check moves from the reviewer's list into the pipeline's refusals, and the
  reviewer document loses a row. A reviewer still enforcing `B163` after `B163` ships is a second copy of the
  system, which is exactly the fault `B118` undid in `tools/`.

- [ ] **B140 — A map with no objective, no spawn and no team exports 200 and looks like a map.** Two boards
  from the second authoring trial (`haiku-r2-canonical-8`, `haiku-r2-ctw-mid`) built a world, wrote region
  files and a provenance sidecar, and exported clean. Their `map.xml` is **ten lines**: a name, an empty
  `<version>`, a `<gamemode>`, an empty `<objective>`, one include and a hunger rule. No `<team>`, no
  `<spawn>`, no `<destroyables>`, no `<wools>`, no `<cores>`, no regions, no filters.

  **Every gate passed vacuously.** `OB17` asks whether a goal stands in void, `OB18`'s successor asks nothing
  now, `OB19` asks whether a prop crowds a goal, and the traversability check asks whether spawn and wool
  points reach each other. All four quantify over collections that are **empty**, so a board with nothing on
  it satisfies all of them — the pipeline's whole refusal surface is built to catch a map that says something
  wrong and none of it catches a map that says nothing at all. That is the more serious half of this entry: a
  map cannot be played without a team and a spawn, so a map lacking both is not a rough map, it is not a map.

  **The `ctw` label is the smaller half and has its own cause.** `MetaGenerator.DeclaredGamemode` derives the
  label from what the intent carries and correctly yields the empty string for a map carrying none (`B131`),
  but it only runs on the intent path — and these maps have no stored intent, so the export takes the plain
  branch and the label survives from `docs/pgm/template.xml:5`, which hardcodes `<gamemode>ctw</gamemode>`.
  So the template still asserts what `B131` stopped the generator asserting, on exactly the maps least able
  to correct it.

  What the refusal should ask is the question no current gate does: whether the map has an objective at all,
  and a team and a spawn to contest it. It belongs with the others on the export composer, carrying a rule id,
  and it wants stating in terms of the intent rather than the document, since the intent is what an author
  actually failed to write.

  **This entry is understated on its own evidence, and the correction changes the fix.** It frames the empty
  boards as a map whose author *failed to write* an intent. But `specs/haiku-r2-canonical-8/plan.json` carries
  **two spawns and two destroyables**. The author did state them; they were **lost between the plan and the
  export**, and every stage answered 200. That is a harder failure than an author writing nothing, and the
  refusal proposed above — stated against the intent — would still not catch it, **because the intent was
  never stored**. So the gate wants asking at the last point that can see both: what the plan declared, and
  what the export is about to write. `B141` is the same family one level down, on the sketch document.

- [ ] **B136 — The two features that make a shape stop looking drawn are reached almost never.** **The census
  below is stale and wants re-running before anything is concluded from it:** it covers **eleven** maps and
  there are now **nineteen**, and the two boards it does not see are the strongest ones — Opus 5's
  `marlstone-steps` and `basalt-reach` move every row, **including the Bézier column it records as zero**.
  Re-run it over all nineteen `specs/` folders first; the gradient below may or may not survive. Measured over
  the eleven, counting non-null uses in the authored specs rather than serialized nulls:

  | | per-shape `theme` | `anchor_heights` | Bézier `controls` | `height_mode` | `skirt` | relief `marks` |
  |---|---|---|---|---|---|---|
  | Opus, three boards | 5 · 6 · 8 | 2 · 1 · 1 | **0 · 0 · 0** | 2 · 2 · 3 | 2 · 2 · 3 | 5 · 3 · 3 |
  | Sonnet, three boards | 2 · 4 · 4 | **0 · 0 · 0** | **1 · 0 · 0** | **0 · 0 · 0** | **0 · 0 · 0** | 4 · 5 · 3 |
  | Haiku, three boards | **0** | **0** | **0** | **0** | **0** | **0** |
  | `ashen_quarry` (earlier run) | 1 | 2 | **0** | 1 | 2 | 6 |

  **A Bézier curve has been authored once, on one shape, across every map this repository holds.** Per-vertex
  `anchor_heights` — the slant control, where an outline's corners each take a height and the surface solves
  between them — is used only by the strongest model and only once or twice a board. `height_mode` and
  `skirt` follow the same line. Every other outline on every map is a straight-edged polygon at one height.

  The gradient is the finding. Per-shape themes and relief marks are reached by two models out of three and
  are the features the documents lead with; the shape-level height and curvature controls are reached by one
  model, barely, and the weakest model reaches **nothing** — its three boards are compiled plan rectangles
  under a single blanket theme, which is the exact output the first fifteen generated boards had. So the
  documents are not the binding constraint for the top of the range and are clearly binding at the bottom.

  What this is **not** is a request for a new capability: all six columns are shipped, documented in
  `capabilities.md` and `sketch.md`, and demonstrated on a committed map. It is a question about **reach** —
  why a control that changes how a board looks more than any other is the one an author does not get to. The
  candidates worth testing rather than assuming: the fields sit on `SketchShape` and a compile emits shapes
  with them null, so an author edits a document rather than asking for a shape; nothing previews a slant or a
  curve without building a world; and the worked examples in the documents are rectangles, so the first thing
  a reader copies has straight edges and one height.

- [ ] **B135 — The paired core defaults leak on the first break, with nothing to dig.** `ObjectiveDefaults`
  carries `CoreFloat = 6` and `CoreLeak = 5` and documents them as a pair (DC2). Read against PGM, that pair
  leaks immediately. `Core.java` builds a leak region whose top is `coreRegion.min.y − leakLevel` and sets
  `leakRequired = lavaRegion.min.y − max.y + 1`, so with the lava sitting at the casing's floor the lava must
  descend **`leak + 1` = 6** blocks below itself to count as leaked. Six blocks of authored air sit under the
  casing, so the lava falls exactly that far with no terrain in the way: the core leaks the moment it is
  opened, and the dig that is supposed to be the second half of the task does not exist.

  **The corpus settles it, and a zero dig is legitimate — that half of the entry is withdrawn.** Ten `dtcm`
  maps carrying cores use `leak` 3–6, median 5, so the studio's `CoreLeak = 5` is the corpus norm exactly.
  Probing two of them with `--column` finds two opposite designs, both shipped:

  | map | `leak` | casing floor | air beneath | dig required |
  |---|---|---|---|---|
  | `stone_fields` | 5 | y23 (obsidian), lava y24–26 | 4 (y19–22), chest y18, solid y17 | **2 blocks** |
  | `fungi_grove` | 6 | y15 (obsidian), lava y16–19 | 11 (y4–14), floor y3 | **none** — it hangs over a chasm |

  So a core that leaks the moment its casing opens is a real design: `fungi_grove` suspends one over a drop
  and the whole task is breaking the shell. The studio's `float 6` / `leak 5` reproduces that pattern, which
  makes it a **default**, not a defect.

  **The "no way to ask for it" half is false and is withdrawn.** An earlier version of this entry said
  `CoreFloat` and `CoreLeak` are paired to a single outcome "with no per-core control on the marker or the
  intent … no way to ask for it". There is: `basalt-reach` authors `"float": 5, "leak": 8` on its core marker
  and ships `leak="8"` in its `map.xml`, so per-core control exists on the plan marker, the intent, the
  validator and the XML writer, and a board can express `stone_fields`' shell-then-dig today. The `const` pair
  is a **default** that is reachable past, not a ceiling. What remains of this entry is the off-by-one below —
  and it is not cosmetic, because it is what the studio *tells an author their dig is*: on `basalt-reach` the
  studio reported 3 and the real dig is 4.

  **And the arithmetic the studio shows an author is off by one.** `PlanTool` computes
  `CoreDigDepth => Math.Max(0, CoreLeak - CoreFloat)`. PGM sets `leakRequired = lavaBottom − (coreBottom −
  leak) + 1`, and the lava sits one course above the casing floor, so `leakRequired = leak + 2` and the lava
  must reach `coreBottom − leak − 1`. The true depth is therefore **`leak + 1 − float`**. Both formulas give
  0 at the shipped pair, so the error is invisible at the default and wrong everywhere else — at `leak 5`,
  `float 4` the studio says 1 and `stone_fields` measures 2.


- [ ] **B129 — The section renderer cuts one plane, so everything behind the cut is missing.**
  `SectionRender` samples a **single one-block-thick slice** and paints each cell with the block that stands
  exactly on the plane. That is the right reading for checking a `layered` material, which is what it was
  built for, and it is the wrong one for looking at a map: a cut through solid ground is a solid slab,
  because a solid slab is genuinely what sits on that plane. A cut through Ashen Quarry's town at z=60 is
  two courses of stone brick over forty-seven of andesite over bedrock, measured by `--column` and rendered
  faithfully — and it shows none of the buildings standing a few blocks either side of it, none of the room
  interiors, and nothing of the town's silhouette. The picture is accurate and nearly uninformative, which
  is a harder fault to notice than a wrong one.

  **The studio already computes exactly the missing quantity, on the other side of the house.**
  `Analysis/Layer/SideView.Build` projects a map's vertical solid segments onto a `(primary × y)` grid as a
  **depth map** — for each cell, the distance from the viewer to the nearest solid along the perpendicular
  axis, `0` nearest and `-1` for a cell nothing occupies — for four viewing directions (`nz`/`pz`/`nx`/`px`,
  with the positive-side ones mirroring left-to-right). `GET /map/{slug}/segments` serves it and
  `js/studio/canvas/sideview-canvas.js` paints it as a depth-tinted cross-section. So a section that shows
  what stands behind the plane is not a new idea here; it is an existing one the block-level renderer never
  reached, and the two want the same projection.

  Two differences are real and have to be settled rather than glossed. `SideView` reads `layer_segments`
  rows, which exist for a map the studio has **scanned**, while `SectionRender` reads a region directory or
  a `VoxelWorld` — so the projection wants doing over voxels rather than over segments, and the shared thing
  is the algorithm, not the input. And a depth map answers *how far* rather than *what*, so a depth-only
  section loses the material identity that makes the current one worth having: the two are complementary
  modes of one renderer, not a replacement.

  **The existing instance is greyscale, and colour is the half it never got.**
  `sideview-canvas.js` ramps nearest to farthest across light stone to very dark, so depth reads and category
  does not. A block-level section drawing the same projection can carry both — distance as value, material or
  category as hue — which is the pairing that makes a building behind the cut legible as a building rather
  than as a lighter smudge.

- [ ] **B104 — A destroy goal is stamped above the build cap.** On `duskfell` the gold destroyable stands at
  y21–23 and `max_build_height` is 20; on `corvale` the emerald stands at y18–20 against the same cap. Blocks
  above the cap can still be broken, so this does not make the goal unbreakable — but a destroyable or a core
  belongs **below** the cap, and neither does. The cap itself is the cause rather than the placement: it is
  `plan.Globals.Surface + Headroom`, both halves of the plan's flat nominal world, so it is computed from a
  ground level the relief later abandons and lands under the terrain it is supposed to sit over. `B105` is the
  fix; what this entry owns is the check that the goal ends up under whatever cap that produces.

  **A floating goal is not the fault, and an earlier version of this entry said it was.** A destroyable and a
  core **float a few blocks above the terrain by design**, and have since PGM's beginning: a core that sits on
  the ground cannot leak, so attackers would have to mine the terrain out from under it first, and a
  destroyable flat on the ground is trivially covered and hidden. The four-block gap measured under
  `duskfell`'s goal is therefore correct behaviour, not a defect, and the same gap in the pre-existing build
  is correct too. What a goal needs beneath it is **terrain, somewhere below** — which is what `B82` already
  checks and checks correctly. The earlier claim that `B82` should compare the goal's height against the
  ground's was wrong and is withdrawn.

- [ ] **B105 — Retire `headroom`; a plan states a build ceiling, it does not derive one.** `PlanGlobals`
  carries `Headroom` (board-wide, default 11 — not per piece, despite the field reading like one) and
  `PlanCompiler` turns it into the map's only build cap with `plan.Globals.Surface + plan.Globals.Headroom`.
  Both halves of that sum are the plan's **flat nominal** world, so the cap is computed from a ground level
  the relief then abandons — which is the root `B104` names, and it produced boards whose ceiling sits below
  their own terrain. Derivation is the wrong shape here regardless of the numbers: a build ceiling is a
  decision about how high a player may build, and it should be **stated**, not inferred from a base plus a
  slack. `MapIntent`'s `BuildIntent.MaxHeight` is already the real field and the export already honours it;
  what is missing is a plan-level value that sets it, and an author or agent knowing it exists.

  So: remove `Headroom` from `PlanGlobals` and everything reading it, add a stated maximum build height in
  its place, and keep **per-piece `Surface`** exactly as it is — that one is load-bearing and correct as a
  plan-space concept. Two things travel with it. The compiler must stop reading a piece's `Surface` as a
  literal world Y for **spawns and wool rooms** (`PlanCompiler` lines 205, 260) — the destroyable/core half of
  this is done (`B128`): `float` now counts from the ground the world build actually solves rather than
  `piece.Surface`, and the marker itself may name no piece at all. Spawns and wool rooms still bake their
  room floor from `piece.Surface` at compile time, the same flat-world mistake wearing a different field; the
  anchor wants resolving against the ground as built. And the ceiling wants a sane relationship to the
  finished terrain rather than to the plan's base, since a map whose highest ground is y20 and whose cap is
  y20 permits no building at all.

- [ ] **B109 — Nothing checks a plan before it costs a build.** Authoring a plan by hand is arithmetic over
  rectangles in cells, and the repository offers no way to ask whether the arithmetic worked short of running
  the whole pipeline. Two pieces that overlap, a land interface too narrow to connect, a stray corner touch —
  none of these is reported until a world has been built. An author writing two boards by hand had to
  re-implement `ContactGraph.Classify` in a throwaway script to check adjacency before spending a build cycle,
  which shortened iteration enough to be worth the detour and is a tool the repository should have.

  **Half the premise is stale and the correction narrows the work.** `POST /plan/inspect` and
  `POST /plan/evaluate` both exist and are documented at `plan.md:417`, so a plan *can* be asked about without
  a build. What survives is that **nothing an author reaches calls them**: no driver invokes the validator
  ahead of a build, and an agent authoring by hand found the endpoints only by reading source. That is reach,
  not absence — the same shape as `B177`, where `SP2` and `SP7` are written law that nothing applies to an
  authored plan.

  **This entry is the home the audit's plan-space rules need, and it is why it is worth doing before them.**
  Buckets 1–3 in `BACKLOG.md` are fourteen findings that are all geometry over plan rectangles — what a spawn
  door faces (`B158` `B169` `B172` `B177` `B180`), how big a piece is and how far apart (`B156` `B157` `B167`
  `B170` `B178` `B186`), how far apart the goals are (`B175` `B179` `B188`) — and each one is a rule with a
  number in it that `PlanValidator` is the natural place for. Landing this entry first means those fourteen
  are findings added to a reachable validator rather than fourteen separate checks looking for a home. The
  findings name rules rather than describing symptoms, which is what an agent needs and what a human reviewer
  can check a board against.

- [ ] **B96 — Density wants measuring as canopy share, not as a leaf count.** The leaf count is the only
  honest measure of *whether a forest was planted* — nothing but a tree lays a leaf, and a building's corner
  posts are logs — but it is a poor measure of **how wooded a board reads**, and two measurements on one board
  size prove it: a spruce forest at 17,600 leaves over many sites rendered as one solid mass with the routes
  buried, while `thornwake` at 17,897 leaves over 72 trees renders as a wood a player walks through. Nearly
  the same count, opposite maps, because the leaves are divided among a tenth as many trees. The number that
  would decide it is the share of ground columns standing under a leaf, which is a cheap read over the same
  voxels the census already walks, and it is scale-free in a way a raw count is not — a 120×240 board and a
  240×240 board do not want the same leaf count to read the same. Report it beside the leaf count and give the
  README a band in those terms; the two numbers disagreeing is itself informative, since a high count at a low
  share is a few enormous trees and a low count at a high share is scrub.

- [ ] **B99 — An objective reads as cut off from the board, and it is not yet known whether it is.** Three
  `dtcm` specs built for the first time once `B94` landed, and `goldhollow` and `spinebreak` rendered four and
  eight objective markers isolated from the board's navigable component — real ground beneath them, no walkable
  join. A goal nobody can reach is unwinnable exactly as a goal nobody can mine is, and it had been hidden
  behind the void refusal.

  A second run then found the same reading on **every** composed `dtm` board it tried — ten-plus seeds across
  both symmetries, before touching anything — which changes what the likeliest explanation is. Ten broken
  boards in a row is a worse hypothesis than one broken measurement, and there is a specific mechanism to
  suspect: `TraversabilityRender` snaps a marker to the component under the goal's own block, and that block
  is solid, so it is never itself navigable. A search that starts there can fail to find the component the
  ground beside it belongs to. That would also explain why the corpus convention the composer follows — a goal
  at the far end of a dead-end lane, inset about five — reads as isolation rather than as a dead end.

  A hand-authored board then settled it further: its **wool** markers read isolated too, and rebuilding the
  repository's own `tools/seeds/base-2wool.plan.json` through the same pipeline reported all four of *its*
  wool markers isolated as well, on a seed nobody suspects. The land interface was flush and the floor under
  the wool solid, so the reading is a property of the cage stamp against the renderer's strict two-cell
  headroom test rather than of any board's geometry. That is a measurement fault on a second objective kind,
  which makes the renderer the likely cause rather than a possibility.

  So the first move is to tell the two apart, and the cheap way is to ask the question from the ground rather
  than from the goal: take the walkable cells immediately around the marker and test whether *they* join the
  spawn's component. If they do, the render is at fault and the fix is in the snap. If they do not, the fault
  is real, it is in the composer's seating or the compiler's build regions, and it is the more serious of the
  two. Do not fix either until the measurement says which.

  **The measurement has now been made, without being asked for, and it points at the renderer.** Sonnet's
  second run rendered `sable-marsh` and **two of its four wool markers read isolated — and they are exactly
  the two walled rooms**; the two open ones read connected. A wall in front of a room is not a board fault, so
  the discriminator this entry asks for has answered: the reading tracks the cage rather than the geometry,
  which matches the ClayClay precedent and the strict two-cell headroom test named above. That is evidence
  rather than proof — it is one board — but it is the right shape of evidence and it should be the first thing
  reproduced when this is picked up.

- [ ] **B103 — The top-down leaves real ground blank on a narrow board.** On a board whose goal sits on a
  narrow dead-end spur, `TopDownRender` drew that spur — the most important corner of the map — as empty
  margin at every scale tried, while `HeightProfileRender` and `StructureFinder` showed real, populated
  terrain there in the same build. The ground was confirmed present by reading the region files directly. A
  renderer that omits ground is worse than one that is merely hard to read, because the omission is
  indistinguishable from a board that genuinely has nothing there — and the top-down is the view everything
  gets judged from first. Suspect the bounds computation rather than the drawing: the spur is at the extreme
  of the board's extent, which is where an off-by-one or an early bbox clamp would bite. It is also a second
  instance of the fault `mapgen-review.md` MG13 names, found on a newer renderer than the one that entry describes.
- [~] **B107 — The sketch still cannot place or move an objective; only its height sticks.** The storage
  question is settled and the backend half is landed (`FEATURES.md`): a structural shape's stated height now
  survives a recompile, marked per field and carried by `intentRef`. What remains is the reach.

  **The canvas half.** `sketch-canvas.js` documents structural pieces as render-only — never hit-tested,
  never selected, never edited — so nothing can write the flag a user's correction would set. Unlocking
  selection, a drag, and an inspector row for the stated height is its own slice of the canvas and render
  layers, and it is what turns a proven mechanism into something an author can reach.

  **The destroy objectives.** A destroyable and a core carry **no rect in the plan** — `Anchor` is a bare
  point, unlike a spawn or a wool room — and that is correct rather than missing: neither has a footprint, and
  neither wants one. They sit anywhere terrain exists beneath them, floating a few blocks clear of it. So a
  sketch presence for them is a **movable point with a stated height**, not a rect to drag, and the height is
  the interesting half because it is the one thing the plan cannot know before the relief runs — `B128` landed
  that half in the document (`float` counts from solved ground, and the marker itself may name no plan piece
  at all); what is still missing is a way to draw and drag that point on the canvas.

  **Position, separately.** Moving a piece rather than raising it is `S25b`, and the design here deliberately
  leaves rect and position tracking the plan so that a recompile stays authoritative about *where* while the
  author stays authoritative about *how high*.

  **And the raster does not draw an absolutely-placed goal at all**, which folds in here because this entry
  owns the canvas half. `GET /plans/{id}/png` draws `tallow-mirefast`'s five pieces, both spawns and the
  legend, and nothing at `(0, −50)` where the wardstone stands. `B128`'s empty-`piece` marker is the most
  useful thing on the board for an agent — it is how a landform carries an objective without a tier
  manufactured to hold it — and **the one picture the plan layer offers cannot show what it produced**, so an
  agent authoring from the render has no way to see its own goal.

### The mapgen audit's forty-eight, bucketed for dispatch

Six agent runs authored nineteen loadable boards against the `B120` brief, and the author's review of twelve
of them — with every claim re-measured against the plans, the layouts and the built worlds — produced
**forty-eight findings and one correction to this board**. They are `B141`–`B188` and they are filed here
whole, because the audit is evidence and a finding that lives only in a report is a finding nobody acts on.

**Twenty of them enforce rules that are the author's** and that nothing in the system checks. Those are law
under `CLAUDE.md`'s oracle clause — not derived from the corpus, not derived from the code — and the numbers
in them (20×20, 15 blocks, 35 blocks, three obsidian, one course of grass) are stated by the author rather
than measured. Do not re-derive them and do not soften them; where a bucket adds a corpus figure it is
calibration for a rule that already exists, not a substitute for one.

**The buckets are a dispatch grouping, not a second board.** Each task still lives in exactly one place —
here — and moves to `TODO.md` and then `FEATURES.md` on its own. What a bucket says is: *these belong to one
agent, in one pass, because they land in the same code and answer the same question.* A bucket is finished
when every id in it has a `FEATURES.md` line and no document still names it as a gap.

#### What each bucket spends

A bucket names the code an agent opens. It does not name the **concept** the agent spends, and two buckets
spending one concept cannot be dispatched at once however file-disjoint they look — each agent fixes the
instance in front of it, correctly and locally, and leaves the other copies differing by neglect. Six concepts
run across ten of the twelve buckets and each has one place it may land; buckets 11 and 12 share nothing with
anything, which is a label too. Read against the bodies rather than the titles, which is what moved three
tasks out of bucket 4 and relabelled bucket 10.

**Occupancy — which columns a stamp owns.** The rule is already written down in this repository, one function
above the defect: `DressingScope.GoalGroundAt` takes a goal's ground from *"the box the stamper wrote where
there is one … by construction rather than by two derivations agreeing"*, and `StructureFootprints` directly
below it rebuilds a house's footprint from `layoutJson` instead. So nothing has to be designed, only applied —
**a claim is taken from the placement, never rebuilt beside it.**

**All of it has landed** (`B202`, `B203`, `FEATURES.md`), and a bucket adding a placement adopts it rather than
inventing a claim: `StructureClaim` is the shape, and the stamper answers what it covered
(`HouseStamper.StampedCells`, `StructureStamper.IronCubeFootprint`, `FoundationCells`, `RedstoneLineCells`).
The count of sites was wrong when it was written and the correction is worth carrying: of the calls this
paragraph listed, only the room floor and the redstone line derived their claim separately — the wall, the goal
box, the platform and both room frames compute one value and hand it to the stamper and the claim alike, which
was always right. **Lands in** `WorldProvenance`, fed from footprints the stampers return. Buckets 6 and 7 both
add placements and are dispatchable.

**Extent and distance in plan space.** Buckets 1, 2 and 3 are fourteen rules over four measures: how far a
door stands from a void, how far two islands stand apart, how large a piece may be, and how far a goal stands
from the other goals and from each spawn. **Lands in `PgmStudio.Geom` for the measure and `PlanValidator` for
the finding**, and that split is forced rather than stylistic: `Geom` references nothing and `Finding` lives in
`Domain`, so a shared measure cannot make its own refusal. A measure answers a number or a rect; the rule that
reads it names the id.

**The inward axis — bands read along a distance.** `B199`, `B200` and `B201` are one concept and were filed in
bucket 4 because they mention materials. They are not block-kind rules: two of them are one walk asked for on
two rasters, and the third is the same bands read along an axis that is continuous rather than stepped.
**The axis has landed** (`FEATURES.md`): `GridBoundary.StepsInward` is the walk, beside `TracePerimeter` where
the arc already came from, `BuildingPlan` reads it instead of the private copy it had, and `ColumnProfile`
carries an `Inset` beside its `PerimeterArc`. The author's call is answered — the walk **crosses an elevation
step** — so what is left in this bucket is the authored shape that spends the axis, and none of it is blocked.
**Lands in** `BucketContext` (carrying the inset the way it already carries the arc), `BandStack` read along
it, and the theme JSON and Theme phase that let a rim state a sequence rather than one material of width N.

**Block kind by role.** What is left of bucket 4 (`B165`, `B190`) and all of bucket 5. **The table exists**:
`BlockFamilies` names each id family once — stairs, single slabs, double slabs, panes, logs, leaves, soil —
and `BlockRoles` composes from it rather than restating it (`B203`'s block half, `FEATURES.md`). So a rule
about what a field may hold is a lookup against a family, and a bucket adding one **does not add a predicate
of its own**; that is the failure this concept exists to stop, and it was already two-thirds of the way there
before anyone noticed, since the two vocabularies' lists were byte-identical and agreed only by nobody having
edited one. Two tables that were counted here are not duplicates and stay as they are:
`DressingPalette.IsStamp` is a closed list of what the stampers make — a claim about passes, not blocks — and
`BlockPalette` is colour built from texture means. `B190` additionally wants a `roof_slab` column and a
migration, so it is not cheap and should go with other `roof_style` schema work.

**The refusal vocabulary.** Every bucket that adds a rule. `Finding`, `Findings` and `Check` shipped as
`B191`/`B192` **after** these forty-eight were written, so not one entry names them and an agent reading a
bucket cold will invent a return shape. **Lands in** `PgmStudio.Domain` — a gate answers `Findings`, never a
bare list, never a count, and `Findings.Refuses` is the question rather than `Count > 0`. Two standing
instructions above apply here and are the reason the vocabulary matters: a rule lands as a finding carrying a
rule id, and nothing quietly moves an author's geometry to satisfy a check.

**Document drift.** Bucket 8 alone — four prose documents, no shared unit, genuinely parallel and dispatchable
as they stand. Bucket 10 is **not** document work despite its title: "document" there is the authored JSON, and
`B141`/`B143`/`B144` are required-field validation, a refusal that misreports its own cause, and an overlap
rule. It spends the refusal vocabulary and belongs beside buckets 1–3.

| # | Bucket | Ids | Concept it spends | Lands in |
|---|---|---|---|---|
| **1** | What a spawn door faces | `B158` `B169` `B172` `B177` `B180` | extent and distance | `Geom` (the frontage rect) · `PlanValidator` (five rules over it) |
| **2** | How big a piece is, and how far apart | `B156` `B157` `B167` `B170` `B178` `B186` | extent and distance | `PlanValidator` · `PlanCompiler` |
| **3** | How far apart the goals are | `B175` `B179` `B188` | extent and distance | `Geom` (the spacing deriver) · `PlanValidator` · `Analysis` |
| **4** | A block must be the kind of block its role needs | `B165` `B190` | block kind by role | `HouseStamper` · `HouseStyleValidation` + a `roof_style` migration |
| **5** | What ground and a goal are made of | `B162` `B163` `B183` | block kind by role | `Themes` · `DestroyKitPairing` · read-back |
| **6** | What may stand where | `B142` `B146` `B166` `B187` | occupancy | `WorldProvenance` from placement · `Decorator` · `DressingScope` |
| **7** | What the world build seats | `B145` `B159` `B176` `B184` `B185` | occupancy | `WorldProvenance` from placement · `SketchWorldBuilder` · the stampers |
| **8** | The documents that taught the fault | `B153` `B171` `B173` `B181` | document drift — no shared unit | `docs/gameplay/` · `docs/tools/` |
| **10** | A document describing nothing still answers 200 | `B141` `B143` `B144` | the refusal vocabulary | `SketchLayout` · `PlanValidator` · the solver |
| **11** | The evaluator over an authored board | `B150` `B151` | reads the plan where the board is in the sketch | `ClosureTerms` · `G8` |
| **12** | Four that stand alone | `B152` `B154` `B174` `B182` | nothing shared | scattered |
| **13** | The inward axis | `B199` `B200` `B201` | bands read along a distance | `Geom.Algorithms` · `ColumnProfile` · `BandStack` |

**Bucket 9 is finished and its row is gone.** `B147`, `B148` and `B149` all carry a `FEATURES.md` line; the
table listed them as work to hand out for as long as their section had already been deleted from the body.

**Order and collision, by concept rather than by file.** The label PGM refused shipped first and alone, ahead
of every bucket below (`B155` — three committed maps had not parsed, and every other finding was reachable
without a client while that one was not). What follows is stated in bucket numbers; they are **not** task ids,
and an earlier version of this paragraph wrote them as `B1`–`B12`, which are not ids that exist.

- Buckets **1, 2, 3 and 10** all spend the refusal vocabulary and all land in `PlanValidator`. One agent, or
  strictly in that order.
- Buckets **6 and 7** both spend occupancy and both would add a claim rebuilt beside a stamp. They wait on
  `B202`, which decides the direction of the derivation; dispatched before it they entrench the fault.
- Buckets **4 and 5** share block-kind and want the same table. Adjacent rather than parallel.
- Bucket **13** wants its author call answered first, and all three of its entries settled together.
- Buckets **8, 11 and 12** share nothing with anything and may run at once.

**These are written to be implemented by a Sonnet agent, and that shapes what an entry has to carry.** Every
entry names its file and its line where the audit found one, states its rule with the number in it, and gives
the coordinates the fault was probed at — so an implementer confirms rather than re-derives. Three standing
instructions follow from that, and they are not optional:

- **Do not re-derive an author's number.** 20×20, 15 blocks, 35 blocks, 5×5, three obsidian, one course of
  grass, 2.5 blocks of door — these are stated by the author under the oracle clause, not measured from the
  corpus or the code. An implementation that computes a different number from the corpus and uses it has
  substituted a measurement for law. Where an entry also carries a corpus figure it says so explicitly and the
  figure is calibration, not the rule.
- **A rule lands as a finding with a rule id, not as a silent correction.** The pipeline's job is to refuse or
  to complain, naming the id, so an author knows what was wrong. Nothing in these buckets may quietly move an
  author's geometry to satisfy a check.
- **The fix goes in `src/`, never in `tools/`.** A refusal, a placement rule or a validation living in a
  driver is the exact defect `B116` and `B118` undid, and a bucket that reintroduces it has made the problem
  worse than the fault it fixed.

**Two folds and one already-shipped**, recorded so nobody files them a second time:

- *An absolutely-placed goal is invisible in the only raster of a plan* folds into **`B107`**, which owns the
  canvas half. `GET /plans/{id}/png` draws Mirefast's five pieces, both spawns and the legend, and nothing at
  `(0, −50)` where the wardstone stands.
- *A walled wool room reads as an isolated traversability marker* folds into **`B99`** as the discriminating
  measurement that entry asks for: on `sable-marsh` two of four wool markers read isolated and they are
  exactly the two walled rooms, which is the renderer's snap and not the board's geometry.
- *A sketch-built map's water lanes never reach `map.xml`* shipped at `10e031d4` with a regression test, and
  is the only board-rule slip the audit found: no task id, no `FEATURES.md` line. The line is added; whether
  `tallow-weirgate` is rebuilt against the fix is an authoring decision, not a task.

#### Bucket 1 — what a spawn door faces

Five rules about the same rectangle: the ground immediately in front of a spawn door, in the direction the
spawn's yaw points. Nothing checks any of them, and four separate boards broke a different one. They want one
derivation — *the frontage rect* — and five findings over it, in `PlanValidator` where `B109` already wants a
reachable home.

- [ ] **B158 — Nothing checks what a spawn door faces, or how far two CTW islands stand apart.**
  `sable-marsh`'s spawn sits at `(0, 12, −90)` with yaw 0, so it faces `+z` — and the `entrance-void` buffer
  occupies `x −15…15, z −80…−55`, starting at the door face. A player leaving spawn walks into a 25-deep void
  with zero blocks of ground. Separately `hub-ring` spans `z −55…0` and fans under `rot_180` to `z 0…55`, so
  the two halves meet flush and the board is one landmass.

  **Two rules (author):** a spawn door stands at least **15 blocks** from the nearest void; and on a CTW board
  two islands sit at least **15** and at most **40** blocks apart. Both are plan-space geometry over
  rectangles. Note that a whole island assigned to a single team is the *right* shape for CTW and is not a
  finding — assignment is downstream of separation, so an agent measuring island ownership on a board with no
  gap will find one island holding both teams and file this entry under a second, wrong name.

  *author, 2026-08-14 · plan pieces `spawn`, `entrance-void`, `hub-ring`.*

- [ ] **B169 — The ground under a spawn has no size relationship to the spawn, and came out 80 blocks wide for
  a 20-block building.** Weirgate's `yard` spans `x −40…40` against a spawn piece of `x −10…10` — sixty blocks
  of platform nothing stands on and nothing contests. Mirefast's `steading` is 92 wide for the same 20-block
  spawn, over a two-block checkerboard of coarse dirt and gravel.

  **The rule, as corrected by the author: raw size is not the test.** A spawn seated on a large rectangle in a
  corner that *is* the map is fine. What these boards do is place the spawn at the back — correct in itself —
  and then surround it with flat dead area. So this is about ground that carries nothing and contests nothing,
  not about a width limit; the 15-block figure is a rule of thumb for the common case rather than the rule.
  Mirefast's at least carries nine houses and two ramps, which is the distinction the correction draws. It has
  a rule id already: **`SP2`** in `rules.md`, "a spawn sits near the back of its lane, because the space behind
  a spawn is dead space". It composes with `B157` (bounds the building) and `B172` (requires the first 20×20 of
  that ground to be open), and between them the question becomes *what is this ground for* rather than *how
  wide is it*.

  **The corpus does not support the neighbouring claim, and the entry says so.** Spawn isolation is not where
  the generated boards fail: the `dtcm` corpus puts a spawn a median of 7.5 blocks from the board edge and the
  generated spawns sit 5–15 out, squarely normal. This entry stands on the author's judgment, not on that sweep.

  *author, 2026-08-14, corrected 2026-08-14 · `yard` and `steading` shapes against their compiled spawn pieces.*

- [ ] **B172 — The ground in front of a spawn must be open 20×20, and nothing checks what stands there.**
  Mirefast's clear corridor is **12 blocks wide**: `h3` occupies `x −15…−8` and `h4` `x 4…14`, both at
  `z −84…−80`, inside the required box. The axis itself is clear, which is why that board has the sightline to
  its monument the author singles out as important — and the other boards do not: Sable Marsh opens onto void,
  Weirgate onto a `subtract`, Corvid Hollow has a house against the door.

  **The rule (author):** at least **20 × 20** of open ground in front of a spawn. The only props permitted are
  a single tree at the edge of that area, boulders and fauna. **No houses.** A clear line from the spawn to the
  objective is what the rule protects.

  *author, 2026-08-14 · house props measured against the compiled spawn piece and yaw.*

- [ ] **B177 — `SP2` and `SP7` are written law, `MG31` says a hand-built board must carry them, and nothing
  checks.** Haiku CTW Rush's spawn piece is 40×30 with the spawn point 11 blocks from its back edge and 19 from
  its front — against `SP2`. Its iron cube stands at `(−10, −65)`, five blocks *behind* the spawn point —
  against `SP7`, "iron goes beside or ahead of a spawn, since players face forward". And the map's own
  `red-spawn` rectangle (`−20,−70` to `20,−40`) encloses that iron, so it sits inside the region the enemy may
  not enter: a contested resource nobody can contest.

  `docs/tools/mapgen-review.md` `MG31` already names both rules and states outright that they bind a hand-built
  board and not only the composer.

  **The two halves are not the same fault, and the entry's original "nothing checks" was wrong about one of
  them.** `SP2` **is** implemented — `PlanValidator.LintSp2` measures the spawn against the back half of its
  piece and yields a finding. What it is not is *reached*: no `src/` caller runs the validator over a plan an
  author posts, which is `B109` exactly, and a lint is a complaint an author may ignore even when it is. `SP7`
  is the half with no code at all — it appears in `rules.md` and `mapgen-review.md` and nowhere in `src/`. An
  agent handed this entry as first written adds a second `SP2` beside the first, which is the fifth-table
  failure this bucketing exists to stop.

  *author, 2026-08-14 · `mapgen-review.md` MG31 · `rules.md` SP2/SP7 · plan, `map.xml` regions and probes.*
  Premise corrected 2026-08-15 against `PlanValidator.LintSp2` and a grep of `SP7` across `src/`.

- [ ] **B180 — A spawn opens onto a face players cannot climb back up.** Kilnrow's `crest` stands at y17 and
  the `works` it opens onto at y14 — probed at `(0, −76)` and `(0, −74)`. A player leaving spawn steps down
  three blocks and cannot return without placing them. Basalt Reach does the same and adds the other half:
  probed out of its door at `z 75`, three walkable blocks at y17 then `works` at y14, a three-block fall three
  blocks ahead — and its stair `ramp-back` runs `x 16…28` while the spawn occupies `x −10…10`, so the route out
  is sixteen blocks off the centre line and nobody leaving the door meets it.

  **The rule (author):** the ground a spawn opens onto is reachable again from where a player lands, and the
  way up is where players actually leave. It joins `B158` and `B172` as the third thing nothing checks about
  what is directly in front of a spawn door, and all three want checking against the same rectangle. Rotating a
  spawn to meet its stair is **not** the fix: on Basalt both objectives sit forward on opposite flanks and
  turning the door toward the ramp would put one behind the player, costing the clear view of both that `B172`
  exists to protect. The route moves to the door.

  *author, 2026-08-14 · probed across the `crest`/`works` interface on Kilnrow and `back`/`works` on Basalt.*

#### Bucket 2 — how big a piece is, and how far apart

- [ ] **B156 — A wall may be authored across a wool room's only entry, and the compiler builds it.** A `walls`
  entry naming a wool-room piece and its neighbour seats the wall on their shared interface — which for a wool
  room *is* its `entries` face, because a room has exactly one. On `sable-marsh` the wall runs `x −66…−64` over
  `z −40…−10` to `topY 11` and the declared entry is `x = −65, z −40…−10`: complete coverage, and the room is
  sealed.

  **The rule (author):** a defence wall may never sit on the interface between a wool-room piece and the piece
  next to it. It belongs at the next joint out — on this board `approach-a │ hub-ring` at `x −55`, ten blocks
  further, taking the wool-to-wall run from 15 to 25 and putting the wall across a 10-block lane mouth instead
  of a 30-block room face. Distance was never the problem: 15 blocks is close to the corpus median of thirteen
  that `match-flow.md` §6.2 records. The board is wrong about *which* boundary, not about how far. Refuse it at
  authoring time in `PlanValidator` with a rule id: a wall whose two pieces include a wool room is invalid.

  *author, 2026-08-14 · measured against `specs/sable-marsh/*.plan.json` and `*.intent.json`.*

- [ ] **B157 — A plan piece has no maximum size, so a wool room came out 30 × 30 and a spawn hall 90 × 15.** A
  stamped building is sized by its plan piece, so piece size *is* building size and nothing bounds it.
  `sable-marsh`'s wool rooms are 6 × 6 cells at cell 5 — 30 × 30 blocks — and its spawn piece is 18 × 3 cells,
  a 90-block hall.

  **The rule (author):** **20 × 20 blocks maximum** for a wool-room piece and for a spawn piece, for now. A
  ceiling in `PlanValidator`, stated in blocks and checked after the cell multiply, since an author writes cells
  and the limit is about the building. It is the workaround for `B178` until that coupling is broken, and it
  pairs with `B167`'s floor.

  *author, 2026-08-14 · `wool-a [−19,−8,6,6]`, `spawn [−9,−19,18,3]`, cell 5.*

- [ ] **B167 — A placed building's footprint has no minimum, and eight of fourteen came out four blocks
  deep.** Seeds 60–67 on `tallow-weirgate` measure 10×4, 9×4, 11×4, 9×4, 9×4, 11×4, 9×4 and 10×4; the remaining
  six are 5 deep, so the same run produced both on one board.

  **The rule (author):** a placed building is at least **5 × 5** blocks. It pairs with `B157`'s ceiling —
  together they bound a building at both ends, which is what a prop validation wants to check when the dressing
  document is posted rather than after a build.

  *author, 2026-08-14 · all 14 house props measured from `specs/tallow-weirgate/*.layout.json`.*

- [ ] **B170 — A contested middle island is the best composition any run invented, and it merged with the
  approach it should have been separated from.** Weirgate's `flat` spans `x −70…70` at `z ≈ −13…13`, sitting
  across the symmetry axis so neither team owns it — nothing else in twenty-one boards does this. But
  `causeway` ends at `z −10` while `flat`'s wavy north edge runs `z −13…−9`, so they overlap; probing `x = −35`
  from `z −14` to `−8` finds solid ground the whole way and no gap at all. Mirefast has the same shape of
  fault: `fen` spans `z −70…+8`, so its `rot_180` image overlaps it across a 16-block band at mid.

  Filed separately from `B158` because the fix is not "add a gap" generically: **a deliberately shared middle is
  a shape the island rule has to permit while still requiring separation on both of its sides.** Recording the
  intent matters as much as the fault — this is the one composition an agent invented that the author wants
  kept. Work it with the Mirefast half; the two are the same entry seen twice.

  *author, 2026-08-14 · shape vertices plus six column probes across the seam.*

- [ ] **B178 — A spawn building's size is its plan piece's size, and an author cannot say otherwise.** Ashfall
  Scar's spawn piece is `x −40…40` — 80 blocks — so the hall is 80 blocks. The piece is doing two jobs at once:
  stating the ground a spawn stands on, and dictating the building on it. An author who wants a wide platform
  and a small hall has no way to ask.

  **The class (author):** the same fault as the iron marker's relationship to a spawn piece — something welded
  to a plan rectangle drawn for a different purpose. The building wants its own stated footprint, with the piece
  describing only the ground. Distinct from `B157` and both are wanted: that entry caps the piece at 20×20,
  suppressing the symptom by forbidding large pieces; this one breaks the coupling so a large piece stops
  implying a large building. The cap is the workaround until it is.

  *author, 2026-08-14 · compiled spawn piece against the built hall · same pattern on `sable-marsh` (90) and `tallow-weirgate` (80 platform).*

- [ ] **B186 — A bedrock wall has no stated geometry, and one board's can be walked around.**
  `quillon-saltworks`' wall runs `x −65…−55, z −56…−54` — 10 wide — while the room it defends is
  `x −80…−65, z −80…−65` with its entry on the east face at `x −65`. The wall stands perpendicular to that
  entry, about ten blocks north of the room, so a player walks around its western end. It is also buried in the
  terrain rather than standing clear of it.

  **The rules (author):** a bedrock wall **spans the full piece intersection it bars**, is at least **10** and
  at most **20** blocks wide, and stands about **15 blocks in front of** the wool room's entrance. Today
  `PlanCompiler` derives the footprint from the declared wall interface and nothing checks that the result
  actually separates the two sides. `docs/gameplay/approaches.md` §85 and `match-flow.md` §168–177 describe what
  the device is *for* — a pre-built bedrock line defenders hold and build on — and `match-flow.md` §383 measures
  its effect (rush rate 6.3% with a bedrock line against 18.1% without). Neither states a geometry, which is the
  gap.

  *author, 2026-08-14 · wall and room rects from `specs/quillon-saltworks/*.intent.json`.*

#### Bucket 3 — how far apart the goals are

Three entries and one deriver. Two of them are the author's law; the third is the corpus band that calibrates
them, and it is the cheapest check available for a generated destroy board — two spawn points, one goal, one
division, no build required.

**This bucket is `B37`'s deferred half and is blocked on a unit, not on a measurement.** `B37` parks "the
objective↔objective and objective↔monument minimum distances still to be drawn from the corpus"; `B188` below
has drawn them. What is unsettled is that every number here is straight-line off `map.xml` regions while the
studio's one implemented separation rule, `WL7`, measures walkable-surface traversal and says so — `B212`.
Settle that before an agent opens these three, or it will write the second measure.

- [ ] **B175 — Two goals of the same team may stand eight blocks apart, and one board does.** Haiku DTM Tower
  seats a destroyable and a core on one piece, both red's: `red-monument-region` ends at `x −9` and
  `red-core-region` begins at `x −1` — eight blocks of clear ground, ten centre to centre. Both carry their own
  sky marker, so the two markers are ten apart as well.

  **The rule (author):** where a team carries two destroyables or cores, at least **35 blocks** between them. It
  is the destroy-side counterpart of `WL7`, which already requires a measured separation between a team's wools
  — so the rule shape exists on the wool side and is simply absent for goals. Two well-spaced boards give the
  band it should sit against: `tallow-kilnrow` 70, `basalt-reach` 74.3.

  *author, 2026-08-14 · regions read from the shipped `map.xml`, both goals column-probed.*

- [ ] **B179 — Nothing states how far opposing goals stand apart, or how much of a board the contest has to
  use.** On a board measuring 240 × 190, every Ashfall Scar objective sits on `x = 0` and the whole objective
  set spans `z −37 … 38`. Opposing monuments are **19 blocks apart with no obstacle between them**. The map is
  240 blocks wide and the contest uses six of them.

  Two things are missing and they are different: `B175` covers a team's own two goals; nothing covers the
  distance between **opposing** goals, and nothing relates the objective spread to the size of the board it is
  drawn on. A board can be authored enormous and played on a line, and every gate passes. The two good boards
  give the target — nearest enemy goal at 95–110 blocks — where Ashfall runs 19.

  **The author's reading, recorded as a reading:** goals this close are likely to produce a stalemate rather
  than a rush, and the board is too large for what it uses. That is a gameplay judgment and is the author's
  under `CLAUDE.md`; the numbers are the measurement it rests on and do not by themselves establish it. The good
  half is real and worth preserving in any fix — the goals are visible from spawn, which `B172` exists to
  protect. The fault is that they are visible and nineteen blocks away on an empty line.

  *author, 2026-08-14 · `map.xml` objective regions · board extent from the layout's shape vertices.*

- [ ] **B188 — A destroy goal should sit about three times as far from the enemy's spawn as from its own, and
  three boards are near 1.25.** Swept across the `dtcm` corpus — 164 of 313 maps parsed from `CommunityMaps` and
  `PublicMaps`, 149 skipped where a spawn or goal region could not be resolved from the XML alone:

  | Measure | p10 | median | p90 |
  |---|---|---|---|
  | Board width | 54 | 118 | 242 |
  | Board depth | 68 | 149 | 279 |
  | Spawn → own goal | 25.5 | 49.4 | 81.4 |
  | Enemy spawn → that goal | 85.1 | 135.2 | 233.7 |
  | **Ratio (enemy ÷ own)** | **1.4** | **2.9** | **5.0** |
  | Same-team goal separation (64 maps) | 16 | 48.9 | 95.4 |
  | Spawn to nearest board edge | 1.5 | 7.5 | 23 |

  Only 27 of 164 maps fall under 2.0. The generated boards split cleanly along it: `quillon-barrow` 3.22,
  `tallow-mirefast` 3.50 and `tallow-kilnrow` 3.24 sit at the corpus median; `quillon-foundry` 1.41 is at p10,
  and `corvid-hollow` and `ashfall-scar` are 1.25, below it. **Those are the same three boards judged worst on
  play, arrived at independently** — which makes the author's reading and 164 published maps agree.

  **The likely cause is board shape rather than goal placement.** Six of the eight `minuyo` boards have one
  dimension under 90 — a destroy board is a **lane**. Every generated board is roughly square (240×190, 170×220,
  136×190), and on a square board every goal is nearly equidistant from both spawns, so a flat ratio is what the
  geometry produces. Fixing the ratio by moving goals on a square board treats the symptom.

  *2026-08-14 · 164 of 313 `dtcm` maps parsed from `CommunityMaps` and `PublicMaps`.*

#### Bucket 4 — a block must be the kind of block its role needs

The largest single class of visible fault in the repository, and it is one shape repeated: a style names a
**block id** for a field whose geometry needs a particular **kind** of block, nothing checks, and the stamper
builds something else. Four models made it on five boards. `B160`, `B161` and `B168` — the block-kind gate, the
door's clear height, and a roof's own materials — shipped as the house-style gate (`FEATURES.md`); `B164`'s
footing shipped beside them. Two shapes are left. `B165` is a style stating something the stamper then does
differently, with nothing reporting the divergence, and it wants the stamper itself rather than a gate beside
the style. `B190` is the same rule the gate already enforces, unable to run where a roof is saved on its own,
and closing it takes a column and a migration rather than a predicate — so the two are not one pass, and only
`B165` is cheap.

- [~] **B165 — A gable roof at `pitch: 2` is overridden by its own wall.** Reported by the author: a gable
  rising two blocks a step disagrees with the wall under it, and the wall wins where they conflict — so the roof
  the style asks for is not the roof that stands. Two of Corvid Hollow's houses carry `form: "gable"` with
  `pitch: 2`, and all nine of Ashfall Scar's do, and two of Kilnrow's.

  **Traced and not yet found.** Two places a wall could plausibly outrank a roof were checked directly against
  every block a stamp actually writes, compared cell by cell to `RoofField`'s own promise (its `Crown`,
  `Underside`, `OnBorder`, `OnRidge`) rather than eyeballed: the roof's own sloped surface, and the gable fill
  between the wall top and that surface (`HouseStamper`'s "walls climb to meet the roof" pass). Both matched
  exactly, on Corvid Hollow's own `d2` house (single storey) and both of Kilnrow's `k3`/`k5` (two storeys, a
  per-level wall override, a bound `Gable`), run through `Decorator.Decorate` rather than a bare `Stamp` call so
  the real front-derived doorway and window seating were in play — and on 700 synthetic trials sweeping pitch
  1–4, every roof form, overhang 0–2, wall extent 3–8, every front edge, a ridge cap on and off, and an unbound
  `Gable` falling back to the wall's own top course. Nothing disagreed anywhere in that sweep.

  So the mechanism is not a material one component wrote over another's cell: whatever the author saw, it is not
  the roof surface or the gable triangle losing to a wall block in `HouseStamper`. What is still unchecked: the
  window seating cutting into gable or roof territory at a steep pitch specifically (not swept), and whatever
  the author was actually looking at — a real build's lighting or block orientation reading as "wall" where the
  block id is in fact the roof's own, which a synthetic reproduction would not show. Re-open with the actual
  in-game coordinates the observation was made at, or a screenshot, rather than re-sweeping the same space.

  Worth pairing with the house-style gate (`FEATURES.md`, `B160`/`B161`/`B168`) — both are a style stating
  something the stamper then does differently, with nothing reporting the divergence, though this one is the
  stamper overriding a value it was given rather than a block of the wrong kind, so it belongs in
  `HouseStamper.cs` rather than in the gate beside the style.

  *author, 2026-08-14 · configuration confirmed in three layouts.* Traced 2026-08-14: roof surface and gable
  fill verified against `RoofField` on the real Corvid Hollow / Kilnrow houses and 700 fuzzed configurations;
  no discrepancy found in either.

- [ ] **B190 — A roof style cannot say which slab it steps in, so the see-through-roof check cannot run at the
  part level.** `HS3` refuses a `Roof` named as a slab while `RoofSlab` is unset — the fault that gave six
  Weirgate houses a roof you can see straight through — but it can only run where a **whole house style** is
  posted. `RoofStyleRow` carries no `roof_slab` column, so a roof style saved on its own has no column for the
  one field the check reads, and pairing them there would false-positive on a roof meant to pair with a
  `RoofSlab` set on the house later. Only the log/ground half of the rule (`CheckRoofFamily`) can run at the
  part level today.

  Found while building the gate rather than by a run, and stated as **missing from the system**: closing it
  means a `roof_slab` column on `roof_style` and a migration, which is why it was correctly left out of the
  batch that found it. Worth doing with any other `roof_style` schema work rather than alone.

  *found implementing `B168`, 2026-08-14 · `RoofStyleRow` · `HouseStyleValidation.CheckRoofFamily`.*

#### Bucket 5 — what ground and a goal are made of

- [ ] **B162 — An obsidian destroyable may carry at most three obsidian blocks, and three maps carry 27, 27 and
  15.** `corvid-hollow` and `basalt-reach` both pair `cube-3` with obsidian — 27 blocks, unhollowed, confirmed
  by probing each cube's own centre; `tallow-mirefast` pairs `column-plus` with obsidian, five a layer over
  three layers, 15 blocks, probed at y16–18. Of seven destroyable boards only `ashfall-scar` is right with
  obsidian, on `pillar-3`.

  | map | style | material | blocks | |
  |---|---|---|---|---|
  | `basalt-reach` | `cube-3` | obsidian | 27 | **over** |
  | `corvid-hollow` | `cube-3` | obsidian | 27 | **over** |
  | `tallow-mirefast` | `column-plus` | obsidian | 15 | **over** |
  | `ashfall-scar` | `pillar-3` | obsidian | 3 | ok |
  | `quillon-barrow` · `sonnet-holdfast` | `cube-3` | ender stone | 27 | ok |
  | `tallow-kilnrow` | `cube-4` | ender stone | 64 | ok |
  | `quillon-foundry` | `column-plus` | ender stone | 15 | ok |

  **The rule (author):** obsidian caps at **three blocks**, so only the pillar styles may carry it; the cube
  styles — and by the same count `column-plus` — are for **end stone, gold and emerald**. This is a missing
  *pairing* rule rather than a bad default: `Pillar3` ships as the default and is exactly three. All three
  violations are an agent choosing a larger style and carrying the default material along unchanged, which
  nothing questions. It belongs beside `DestroyKitPairing`, which already reasons about a goal's material.

  Also worth deleting while here: the bedrock centre a cube can be filled with is unreachable — see `B176`.

  *author, 2026-08-14 · swept over all 21 folders by parsing the intent JSON · probed on all three offending maps.*

- [ ] **B163 — A surface material that is not layered repeats down every course, so a palette holding grass
  stacks it.** `rookwood` sets `surface.material.kind: "cell"` at `depth: 3` over a palette including grass. A
  cell is a **pick, not a stack**, so the chosen block is written to all three courses: `(−55, −5)` is grass at
  y7, y8 and y9. The same map's `hollow-turf` uses `kind: "layered"` with grass at thickness 1 and is correct —
  two idioms side by side on one board, one right, and nothing marks the difference.

  **The rule (author):** a grass course is **exactly one block thick** and grass never appears in a course below
  it. A palette containing grass is therefore invalid at any depth over one unless it is the top layer of a
  layered stack — checkable at the theme, **before a build**.

  **Swept, this is repository-wide and is the single most repeated authoring mistake here.** Four maps carry a
  non-layered surface at depth 3 whose palette holds grass, written by three different models: `corvid-hollow`
  (`rookwood`), `sable-marsh` (`sable-reeds`), `sonnet-briarlock` (`briarlock`) and `tallow-weirgate`
  (`weir-silt`). Probed and confirmed on two — `(−55, −5)` on Corvid, `(30, −35)` on Weirgate, grass at all three
  courses in each.

  *author, 2026-08-14 · swept over every layout · column-probed on two maps.*

- [ ] **B183 — A board's palette is unchecked in both directions, and the two worst boards fail opposite ways.**
  **Basalt Reach vanishes**: all five themes sit on Stone (1:0) or Andesite (1:5), and three of its five houses
  are walled in Stone with an Andesite roof and Polished Andesite verge — built from the same rock as the ground
  under them. **Tallow Kilnrow clashes**: `kiln-stack` is a `cell` over Quartz, Chiselled Quartz, White Clay,
  Smooth Sandstone and Light Grey Clay, with Red Sandstone two blocks away and a Red Stained Clay boulder in it.
  **Tallow Mirefast checkerboards**: `mire-timber`'s top layer is `{"kind":"checker","size":2}` in coarse dirt
  and gravel, spread over the 92-block spawn approach.

  Three boards, three ways of being unreadable, and no measure that would have caught any of them. The nearest
  thing that exists is `--surface`'s tone families (`B147` closed the coverage gap that left half the
  stained-clay ramp reading as unnamed magenta), but nothing yet turns the reading into a **distribution over
  tone families** an author could check either failure against. **A board that reads as one material and a
  board whose materials fight are the same missing check seen from two sides**, and that distribution — not a
  picture — is the read-back that would answer it.

  Filed as composition rather than defect, with `B173`: nothing here should refuse, and what would move it is a
  measure an author can look at before building.

  *author, 2026-08-14 · themes and house styles read from three layouts, cross-checked against column probes.*

#### Bucket 6 — what may stand where

Four ways a thing is placed and the system says nothing. Three of them are silent skips and one is a silent
build; all four want the same answer — **a placement report the export can refuse on**, rather than a document
count standing in for what reached the world.

**That report is `B37`'s resolved-stamp record**, wanted there for the separation rules and here for what
reached the world — kind, footprint, whether it was placed, what asked for it. `IronResolution` is the only
instance today. Whichever task builds it must be told about the other, and it composes with `StructureClaim`
(`B202` — which columns a stamp owns) rather than replacing it: one says what was placed, the other what it
covered.

- [ ] **B142 — A prop authored over void is skipped, and the foliage read-back counts the document rather than
  the world.** A tree where no ground stands does not appear — no refusal, no warning, no count.
  `--topdown --layer foliage --dressing` reported "34 tree(s)" from the dressing document while one of them
  stood nowhere. `OB19` refuses a prop inside a goal's clearance; nothing refuses a prop over nothing. The cheap
  half is the read-back telling the truth; the real half is the placement report.

  *`opus5-run2` §5 #4 · `basalt-reach` `s1` at `(−46, 74)`, 0 solid blocks.*

- [ ] **B146 — A path's claimed band drops any building touching it, silently.** Four of five houses on
  `basalt-reach`'s first build vanished because they touched a path band — on both orbit images, with no refusal
  and no warning. Three commits landed during that same run about what a building claims — its eaves, its roof
  ring, two buildings that touch — and none touched the path side of the question.

  It has been misattributed once already: Sonnet run 1 filed the same symptom as a camelCase enum-parsing fault.
  That is the durable kind of error, because the symptom keeps confirming the wrong cause — a 53-prop village
  that shipped zero props still reads as evidence for it.

  *`opus5-run2` §5 #3 · coordinates for all four houses in the report.*

- [ ] **B166 — Nothing complains when two buildings touch, or when a roof overhang reaches inside another
  building.** Corvid Hollow's spawn piece is `x −15…15, z −90…−75`; the house at `x −18…−6, z −74…−66` stands
  flush against its face with zero gap and carries `"overhang": 2`, putting its eaves at `z −76` — two blocks
  inside the spawn building's wall. Visible in `renders/05-structures.png` and not acted on. It is **not** the
  same-style merge case the L/T-shape work (`G172`) will absorb: spawn is `hip`, the house is `gable`, and their
  pieces stand two blocks apart in height.

  **The rule (author):** a building keeps at least **one block of clearance** from another, eaves included, and
  a placement breaking it draws a complaint rather than a silent build. The geometry is already computed —
  `DressingScope` holds a stamped building's extent and `B137` taught a building to claim the eaves past its
  walls. What is missing is comparing two claims and refusing.

  *author, 2026-08-14 · plan piece `spawn` against the third house prop.*

- [ ] **B187 — A house may be stamped over void, and eight of one building's eleven columns stand on nothing.**
  `quillon-saltworks`' `h1` spans `x −80…−70, z −60…−55`. Probed at `(−78,−58)`, `(−75,−58)` and `(−78,−56)`:
  two solid blocks each — the house's own floor with no terrain under it. Ground does not begin until about
  `x −72`.

  Distinct from `B142`, where a prop over void is silently *skipped*. Here the building is **fully stamped and
  hangs**. A stamped structure needs the same ground test a prop gets, and it needs it as a **refusal** rather
  than a skip, since half a building on solid ground is a worse outcome than none.

  *author, 2026-08-14 · five column probes across the footprint.*

#### Bucket 7 — what the world build seats

- [ ] **B145 — A spawn or wool shape's interior is not painted by its theme.** Reported independently by four
  runs and never filed. Re-probed on `marlstone-steps`: the column under the red wool at `(0, 85)` is raw `1:0`
  Stone from y24 down to y1, on a board whose `crest` theme is quartz.

  **This is not the known bedrock case, and conflating them is why it keeps being dismissed.**
  `WoolStructureStamper.StampFoundation` fills a wool-room plan piece with bedrock from y0, which is documented
  and intended. Here there is **no foundation at all** — the shape is simply unthemed. Two mechanisms, one
  symptom, and the second has no owner.

  *`opus5-run2` §5 #10 · `opus-run2` §1.6 · two earlier runs · re-probed this session.*

- [ ] **B159 — A goal's sky marker is seated on the build cap, so any building taller than the cap swallows
  it.** `ST7` floats a goal marker clear of `BuildIntent.MaxHeight` "so it sits out of build reach by
  construction", and `SketchWorldBuilder.cs:51` does exactly that. The premise fails on a **stamped building**,
  which is authored geometry and not subject to the cap: on `sable-marsh` the cap is 20, the marker seats at
  y24–26, and the wool building's roof is at y31 — the marker is five blocks *inside* the room it is meant to
  advertise.

  **The fix (author), corrected:** a floating marker must clear **both** — it stays above `BuildIntent.MaxHeight`
  so players cannot build to it, *and* above what was actually built over the goal's footprint, roof included.
  So the seat is `max(maxHeight, tallestBuiltBlock) + clearance`, **not either one alone**. An earlier version of
  this entry proposed replacing the cap with the built height, which would have put the marker back in reach on
  a low building — Corvid Hollow's marker satisfies the rule today at y24–26 against a cap of 20, and that fix
  would have broken it. Withdrawn. The existing no-cap fallback does not help either way: it reads
  `terrain.SurfaceTop.Values.Max()`, which is terrain and excludes every stamped structure.

  The cap here is `Surface + Headroom` = 9 + 11, so this is the **third symptom of `B105`'s derived ceiling**,
  after `B104`.

  *author, 2026-08-14 · `rules.md` ST7 · `SketchWorldBuilder.cs:48–54` · column-probed at `(−80, −25)`.*

- [ ] **B176 — Bedrock is the spawn room's fallback, and a second bedrock feature is unreachable dead code.**
  **The live half:** both Haiku boards build the spawn as a bedrock floor with a bedrock lid eight courses above
  it — probed at `(−10, −50)` and `(−10, −45)` on DTM Tower and `(−10, −60)` on CTW Rush. DTM Tower *did* author
  room styles (`specs/haiku-dtm-tower/spawn.json` describes stone-brick walls under a plank roof) and the world
  is bedrock anyway, so either they never reached the stamper or they were ignored. Whichever it is, the fallback
  when nothing binds is bedrock, and **a bedrock box is never a wanted outcome**: it should refuse, or fall back
  to a plain shell in the map's own palette.

  **The dead half, worth stating because it changes the work.** `ObjectiveStamper.StampDestroyable`'s
  `bool bedrockCentre = false` fills a cube's inset with bedrock. Its only caller — `SketchWorldBuilder.cs:324`
  — never passes it. So it is not a default and no agent can author it; that half is **deleting unreachable
  code**, not changing a behaviour. (Confirmed against `basalt-reach`: probed at the cube's own centre there is
  no bedrock, only 27 obsidian, which is `B162`.)

  *author, 2026-08-14 · `ObjectiveStamper.cs:50,60` · callers grepped · both boards probed.*

- [ ] **B184 — The goal's bedrock plate sits one course down and wants three, and the space that opens up is
  where its defence chest goes.** `StructureStamper.StampPlatform` lays a fixed 5×5 bedrock plate
  (`PlatformSize = 5`) at `plateY = groundTop − 2`, where `groundTop` is the highest solid surface block over the
  footprint. So one course of ground separates the surface from the plate.

  **The change (author):** seat it **three blocks below the ground** instead — `groundTop − 4`. The stamper is
  the only place it is computed and the doc comment above it states the current depth in prose, so both move
  together.

  **And the second half follows from the first.** A defence chest belongs under the core and under the
  destroyable, and it can be the chest that already exists: `WallDefenseChest` stamps a 27-slot supply — planks
  and crafting tables, end stone and a redstone block, two Efficiency pickaxes — set into a face with the block
  above carved to air so the lid opens. Deepening the plate is what creates the room to put one there: at one
  course down there is nowhere to stand a chest between the surface and the bedrock, and at three there is.
  `WallDefenseChest.Stamp` is written against a wall — a thin/long footprint and an `onMinFace` side — so the
  reusable part is `Embed` and the chest's contents, not `Stamp` itself.

  *author, 2026-08-14 · `StructureStamper.cs:93–116` · `SketchWorldBuilder.cs:327–331` · `WallDefenseChest.cs`.*

- [ ] **B185 — A wall's defence chest can open into the wool room it seals, and one map does it.** The mechanism
  is deliberate and mostly works: `WallStructure.ChestOnMinFace` picks which of the wall's two faces is opened —
  the wall is two thick precisely so one face can be cut while the other stays solid — and
  `PlanCompiler.cs:446–449` recomputes it per orbit image, since a reflection swaps the faces. Swept over every
  board with a wall, three of four are right:

  | board | wall | chest | room | |
  |---|---|---|---|---|
  | `sable-marsh` | `x −66…−64` | `x −64` | `x −95…−65` | outside the room, ok |
  | `sonnet-briarlock` | `x 69…71` | `x 71` | `x 90…130` | room-ward, ok |
  | `marlstone-steps` | mid-board, no room | — | — | n/a |
  | `tallow-weirgate` | `x −51…−49` | `x −51` | `x −70…−50` | **inside the room** |

  On Weirgate the supply sits inside the sealed cage rather than on the side a defence is held from.

  **The rule (author):** where a wall stands on a wool room, its chest face is rotated the same way the room's
  door faces, so a defender arriving at the door meets it. Today the face is derived from the wall's own
  geometry (`PlanWall.Side` fanned per orbit) and never consulted against the room's `entries`, which is why it
  can come out backwards without anything noticing.

  *author, 2026-08-14 · all intents swept, wall faces computed against each room's declared entry.*

#### Bucket 8 — the documents that taught the fault

Four documents, and the first of them is **law**. An agent reading `approaches.md` today is being told to build
the thing `B181` names, which makes the document upstream of the boards rather than a description of them.

- [ ] **B181 — The void is drawn across the approach instead of between the teams, and `approaches.md` is what
  taught it.** Kilnrow's `flue` subtracts `x −44…44` — 88 blocks of a 136-block board, 65% of its width — at
  `z −39…−16` and its mirror, sitting between the objectives and the middle and leaving two 24-wide side
  channels. Meanwhile `z −16…16`, where the two sides actually meet, is solid ground. **The hole is where the
  join belongs and the join is where the hole belongs.**

  **The doc change is done.** `approaches.md` now withdraws the middle-of-terrain hole for `dtm`/`dtc` boards
  and replaces it with a **depression or a pond**, keeps void at the seam between the two teams' lands, and
  carries the overshoot warning Basalt Reach supplies — four holes around a connected middle draw play into the
  centre and leave the flanks unused, so where the void goes is a composition decision about which ground
  should be contested rather than a geometry one about how much board is missing. Its claims carry the
  `[author]` mark and are settled, so that was an amendment to law and it was the first thing to change: the
  author notes the fault as a recurring Opus habit rather than a one-board slip, which makes the document the
  cause rather than the model.

  **What remains is that nothing measures it.** A `subtract` that separates the two teams and a `subtract` that
  cuts across one team's own approach are the same operation, and no check distinguishes them — the question is
  which side of the symmetry axis the cut sits on and what it disconnects, both computable from the layout
  before a build. Whether `tallow-kilnrow` is restated against the new law is an authoring decision, not this
  task.

  *author, 2026-08-14 · shape extents against the board bbox and the compiled objectives.*

- [ ] **B171 — Nothing tells an author how a wool approach attaches to a hub, so the dock was tucked into the
  wrong neighbour.** Weirgate's `dock-w` touches only `front` (the `weir-silt` sand/clay/gravel piece) and
  `lane-w`; `hub` — the `weir-plaza` voronoi piece with the rim — is a lane away, and the dock's south edge sits
  flush on the build region's northern line at `z −20`. It should stand against the hub instead.

  **The direction (author):** an agent placing a wool approach **reads the shapes endpoint** for the valid base
  shapes and how each attaches to a hub, and authors from that. **It does not run the generator.** This is reach
  rather than capability, and it is upstream of `B156`: a dock seated against the hub has a face to wall that is
  not its own door, so getting the attachment right removes the wall fault as a side effect.

  *author, 2026-08-14 · piece adjacency computed from the plan; themes matched to the author's description.*

- [ ] **B173 — Every board builds one street of identical houses behind its spawn, and nothing else.** Swept over
  twelve maps with three or more houses, ten put every house on **two or three z-rows** while spreading freely
  across x — Weirgate 14 houses on 2 rows, Mirefast 9 on 2, Kilnrow 7 on 2 with a single orientation, Basalt 5 on
  2 with a single orientation, Ashfall 9 identical houses on 2. Only `sonnet-cinderreach` and `sonnet-holdfast`
  vary, and they are the same two boards that turned off the building footing in `B164`.

  **The observation (author):** the houses are always in a line, always the same orientation, always on a "town
  square" themed area, and always clustered behind the spawn. There is never a watchtower in a field, never a
  barn away from the town. The building vocabulary and its placement are both one idiom repeated.

  This is a **composition gap rather than a defect** — no refusal is wanted. What would move it is a worked
  example of a building placed for a reason other than "the village goes here", and it connects to `B92`, which
  wants a scenery building that is a mass rather than a place: a watchtower and a sealed edge-of-board run are
  the two things that entry is for. Note the tension with `B172`, which bars houses from the 20×20 in front of
  the spawn — the street has to move, not merely spread.

  *author, 2026-08-14 · all house props on all 21 folders, grouped by position and facing.*

- [ ] **B153 — Six fields an author got wrong because the unit or the scope lives only in the source.** Each cost
  a build cycle, on three different runs. `branchAngle` is **radians** and clamped to `[0.2, 1.5]`, so `40`
  becomes a plausible-looking wrong tree rather than a refusal (`sketch.md:463` names the field and gives no
  unit). `bedrock.relative: true` means "keep the top N blocks as terrain", not "N blocks of bedrock". `sink`
  reads the **median** ground under its footprint, so a pit straddling two tiers is shallower than asked.
  `voidEnforcement`'s "independent" was read as "safely additive" and denied void inside a declared build area.
  Bézier `controls` are **absolute** and keyed by vertex index, `out` bending the edge that *leaves*. And
  `voidEnforcement` appears in neither `plan.md` nor `capabilities.md` — the two documents the `B120` brief
  pushes hardest — so two separate runs filed it as missing.

  One documentation pass, six fields. **Every one is stated correctly at the source**, which is the finding: the
  docstrings are right and nothing an author reads first carries them.

  *`opus-run2` §1.3, §1.4, §5 #10 · `sonnet-run2` #1, #2, #6 · `opus5-run2` §2, §5 #5.*

#### Bucket 10 — a document describing nothing still answers 200

- [ ] **B141 — A hand-authored shape missing `type`, `operation` or `floor` rasterizes to nothing, and every
  stage reports success.** `SketchShape.Type` defaults to `""` and `RingOf` returns `[]` for an unknown type, so
  the shape contributes no cells at all. `PUT …/sketch` answers `{"ok": true}`, `GET …/sketch` returns the
  document intact, and `POST …/sketch/relief/read` answers 200 with `{"islands": []}` — an empty array that
  points the reader at the relief rather than at the geometry that caused it. **A document whose entire purpose
  is to describe geometry has no required-field validation.** Same family as `B140`.

  *`opus5-run2` §5 #1 · reproduce by deleting `"type"` from any shape in `specs/marlstone-steps/*.shapes.json`.*

- [ ] **B143 — `PlanValidator`'s `SP1` refuses a zone-less plan for the wrong reason.** `SP1` needs a declared
  build zone before it has any frontline pieces to start from, so a plan declaring no zones has **all** its wools
  reported unreachable regardless of actual reachability. Adding one `zones` entry cleared all eight findings on a
  board whose geometry never changed. The rule is correct about itself and wrong about what it tells the author —
  and an agent reads it as a geometry fault and redesigns the board.

  *`sonnet-run2` #5 · `sable-marsh` first plan.*

- [ ] **B144 — Height and paint resolve an overlap by opposite rules, and the documented technique puts them in
  conflict.** Height takes the **taller** add-shape (`MergeCell`); paint takes the **smallest-area** shape
  (`ShapeScopeOwners`, "the most specific scope"). The documented way to give a tier an organic edge is to let the
  tier below run *under* it — and where that lower tier is the smaller shape, it keeps its own paint over ground
  the upper tier owns. No field scopes paint to the visible surface rather than to a shape, and nothing warns.

  Re-probed on `marlstone-steps`: `(0, 58)` is sandstone at y21 where `(0, 70)` — the same shelf at the same
  height — is quartz.

  *`opus5-run2` §5 #2 · re-probed against the committed region files.*

#### Bucket 11 — the evaluator over an authored board

- [ ] **B150 — `G8` fill-ratio cannot see a layout `subtract`.** `basalt-reach` evaluates at **0.811** —
  near-solid — over a built board carrying two large void channels cut with `subtract`. The term reads the plan's
  rectangles, and holes cut in the sketch are invisible to it, so the one number describing how much board there
  is describes a different board. Worth settling before any evaluator score is trusted on a sketch-authored map,
  which is every map an agent authors through the documented loop.

  *`opus5-run2` §5 #7.*

- [ ] **B151 — `WL8` wool-ringed-hole is a hard term over the motif `rules.md` calls a device.**
  `ClosureTerms.cs:5–9` makes it hard; `rules.md` § "Function is read from the hole's ring" describes the same
  arrangement as the two-approaches device, and the same plan compiles 200. Either the term is soft or the rule
  document is wrong, and nothing currently says which — so an author reads a refusal for a shape the law
  recommends.

  *`opus-run2` §1.7 and §5 #7.*

#### Bucket 12 — four that stand alone

- [ ] **B152 — The objective line pluralizes across both teams.** One destroyable per team gives
  `intent.Destroyables.Count == 2`, so `MetaGenerator.Objective` writes "monument**s**" on a board where each team
  destroys one. There is no per-team divisor. Cosmetic — and it is the first line a player reads.

  *`opus5-run2` §5 #8 · `basalt-reach/map.xml`.*

- [ ] **B154 — `species: "dark_oak"` builds a tree from oak blocks.** `basalt-reach` at `(−60, 12)` reads `17:12`
  log and `18:4` leaves under a nine-block trunk and a broad crown, so the species selects the right template and
  the wrong material. Filed as measured rather than diagnosed — `docs/world-export/tree-corpus.md` was not read and
  it may be intended. Cheap to settle.

  *`opus5-run2` §5 #9.*

- [ ] **B174 — The whorled tree is four parts wood to one part leaf, and the note that cleared it counted the
  wrong thing.** Over one board, one species, one build:

  | form | trees | logs | leaves | leaves per log |
  |---|---|---|---|---|
  | `grown` + `whorled` | 5 | 228 | 287 | **1.26** |
  | template spruce | 3 | 42 | 222 | 5.29 |

  Forty-six logs per whorled tree against fourteen per template one. "Mainly logs and no leaves" is the right
  description and it is now a number.

  **The measurement that closed this was the wrong measurement.** `MG28` records the trunks-with-no-crown report as
  "does not reproduce" because "whorled lands 1136 leaves". An absolute leaf count cannot distinguish a leafy tree
  from a wooden one — 287 leaves reads healthy until the 228 logs under them are counted. This is `B96`'s fault
  exactly, a count standing in for a ratio, recurring on a different subject and closing a real defect. `G173` and
  `MG28` both exist already; this entry is the evidence to reopen them with, and the rule that **neither closes
  again on a leaf count**.

  *author, 2026-08-14 · block census over both groves in `maps/tallow-mirefast/region`.*

- [ ] **B182 — A literal `<Team>` placeholder ships to players on five maps, and nothing substitutes or refuses
  it.** Swept over every board: `basalt-reach` ("Stack", "Sump"), `quillon-barrow` ("Barrow"), `quillon-foundry`
  ("Furnace", "Gantry Stack"), `tallow-kilnrow` ("Lime Kiln", "Slake Pit") and `tallow-mirefast` ("Wardstone") all
  ship `name="&lt;Team&gt; …"`. **Every one is Opus, across three separate runs** — an idiom of one model rather
  than a general mistake.

  There is no substitution anywhere and there was never meant to be: PGM reads the attribute verbatim
  (`CoreModule.java:113–123`), and when it is absent names the core "Core", then "Core 2". So players see the
  literal text, on both teams, identically. The studio's own fallbacks are already what the author asks for —
  `PlanCompiler` writes "Red Monument" for an unnamed destroyable and passes the empty string for a core so PGM
  names it. Nothing is missing but a refusal: **a goal name containing `<` or `>` is a placeholder an author
  forgot to fill in**, and the export composer is where that is caught.

  *author, 2026-08-14 · all 21 `map.xml` files swept · `PGM/…/core/CoreModule.java`.*

#### Bucket 13 — the inward axis

Three entries and one walk. They were filed under bucket 4 because each names materials, and they are not
block-kind rules: a floor divided into concentric zones, a terrain top course banded inward from its rim, and
a band stack whose axis is continuous are the same question asked on three rasters — **how far in from the
edge does this cell stand, and which band claims that distance.**

**The axis itself is built and nothing here is blocked.** `GridBoundary.StepsInward` is the walk, beside
`TracePerimeter` where the arc already came from; `BuildingPlan` reads it in place of the private copy it had,
and `ColumnProfile` carries an `Inset` beside its `PerimeterArc` — how far *in* from the void-facing edge a
column stands, against how far *round* it. Nothing paints from it yet, which is the whole of what is left
here: the **authored shape** that spends the axis. `B199` is the house floor's border becoming a stack read by
ring, and `B200` the terrain's top course banded inward — both a schema, DTO and editor change rather than a
traversal, since the traversal now exists.

**The walk crosses an elevation step** (author, 2026-08-15). One walk inward from the geometric outer edge,
numbering across a tread rather than reseeding at it — `B200`'s `Void` reading, not `RimEdges.Drop`. The
concept is reached for on flat ground most of the time, where the two readings coincide anyway, and the
answer is stated so that varying heights are **not ruled out**: a staircase of plateaus gets bands running
across the treads and up the hill, which is a shape an author may want and would have no way to ask for if
each tread seeded its own ring 0.

- [ ] **B199 — A floor's zoning is three named fields, so a floor cannot be concentric.** `FloorSurface` is
  `Border`/`BorderWidth` + `Field` + `Inlay`/`InlayInset` — three fixed zones where the border is one material
  of width N rather than a sequence, and the inlay is measured from the opposite end. A cobble ring, then two
  rings of stone brick, then a grass field is not sayable on a house floor either. The traversal is the one
  `BandStack` now owns (`B195`) and the input already exists — `BuildingPlan.Ring(x, z)` — so what is missing is
  the *authored* shape: the border becomes a stack read by ring with `BandEnding.HandOver`, which is what its
  `At(ring)` returning null already means by hand.

  It was left out of `B195` on purpose: the three zones are **named**, and the whole part-binding vocabulary
  binds courses by those names (`RoomParts.Border` / `Field` / `Inlay`), so this is a schema + DTO + editor
  change across `room_style_course`, `RoomStyleSaveRequest`, `HousePartComposer` and `RoomStyleComposer` —
  about thirteen files — rather than a traversal swap. The row schema may already carry it: a course names its
  part *and an ordinal*, so a border of several bands is expressible without a migration.

  **It is the same concept as `B200`, on the other raster**: the top course of a plan divided into bands by how
  far in from the edge a cell stands. The house has the axis (`BuildingPlan.Ring`) and three fixed zones over it;
  the terrain has neither. Whichever lands first should leave the other holding a band stack read along an
  inset, and the two should be reviewed together rather than in sequence.

  *found reading the house model after `B194`; the extraction it waited on landed as `B195`.*

- [ ] **B200 — The top course of the terrain is not themeable inward, and the walk that would do it is
  already written for houses.** A theme paints a column *downward*: the rim or the surface claims `Depth`
  courses from the top, the wall takes the riser, fill takes the middle. There is no **plan-direction** axis at
  all, so "a cobble rim, then two rings of stone brick, then a grass field" — the author's own words, reported
  while theming a board — cannot be said. This is theming of the **topmost course only**, walked inward; it is
  not the `Rim` bucket becoming concentric, and filing it that way was wrong.

  **The axis is built; what is missing is the authored shape that spends it.** `GridBoundary.StepsInward` is
  the walk, shared with the house raster, and `ColumnProfile` carries an `Inset` beside its `PerimeterArc` —
  how far *in* from the void-facing edge a column stands, against how far *round* it, measured in the same
  pass over the same landmass (`FEATURES.md`). Nothing paints from it.

  **What remains, in two moves.** Carry the inset on `BucketContext` the way the arc already is, and make the
  bands a `BandStack` read along it with `BandEnding.HandOver` — cobble, two of stone brick, then nothing
  claimed and the surface shows. Then the authoring: a `Rim` that states a sequence rather than one material of
  width N, through the theme JSON and the Theme phase. Restricting it to the top course composes rather than
  needing a knob — a `layered` stack whose first band is the ring-banded material and whose rest is dirt.

  **The walk crosses an elevation step, and that is settled** (author, 2026-08-15). Only the geometric outer
  face seeds; the numbering carries across a tread rather than restarting at it, so a staircase of plateaus
  gets one band set running across the treads and up the hill rather than a set per tread. The flat case — what
  the concept is reached for most of the time — reads the same either way, and the answer is stated in this
  direction so that varying heights stay available rather than being ruled out by the seeding rule. The seed is
  the geometric edge, not the `Rim` bucket's toggle: the walk starts "from the rim or if there is no rim".

  *reported by the author while theming a board; corrected 2026-08-15 after reading `TerrainProfile` ·
  `BuildingPlan.Step` · `GridBoundary` · `docs/world-export/terrain-painting.md`.*

- [ ] **B201 — `VoronoiMaterial.Bands` is a band stack that cannot use the band stack.** Its element is
  `(material, depth)` — the same pair a `Band` is — but its axis is the **continuous** Worley `F2 − F1` gap
  rather than an integer step, and its last band's stated depth is deliberately ignored. So it reads as a
  fourth copy of the rule and is not one: the same words over a different traversal. Either `BandStack.At`
  grows a `double` axis, or the element is shared and the traversal is not, or it stays as it is and the
  duplication is only in the naming (`VoronoiBand.Depth` against `Band.Thickness`). **Which of the three is a
  design call, not a defect** — filed so the audit does not re-find it as one.

  *found doing `B195` · `TerrainPatterns.VoronoiMaterial`.*


- [~] **B70 — The room-style *card* cannot show a porch or a window.** The open editor draws four views now
  (B71), the cutaway among them, so a style's porch and its windows read there. A library **card** still
  carries the section alone, and a section projected onto the front wall shows a window as a patch of the same
  colour as the wall around it. The sample is the other half: `RoomStylePreview` stamps the shipped 10×10
  piece's 8×8 shell, which is small enough that a porch leaves little room behind it. The library therefore
  still has knobs whose *card* does not change when they are turned, which is the one thing the preview exists
  to prevent. Wants a larger sample footprint, and a card that is not the one view those knobs are invisible in.

- [ ] **G178 — A wing has no doorway into its neighbour.** Where two wings meet the plan is simply open between
  them, which is right; where one projects into another its gable end is a wall from the ground up, which is
  also right and leaves the projecting wing reachable only from outside. A doorway cut between two wings —
  through the shared wall a projecting wing's gable end stands in, or through the wall a taller wing's storey
  carries above a stopped neighbour (`structures.md` §7.6) — wants a run to sit in and a rule for which wall it
  is cut through, and belongs with the openings work rather than with the roof (`G172`).

- [ ] **G171 — A building's reported height is its reservation, not its highest block.** `TopLayerOver` adds
  up every storey's headroom and answers where the roof would sit, which is right for a building whose storeys
  are rooms and wrong for one whose top storey is a roof terrace (`structures.md` §7.6): a parapet storey
  states the clear of three a storey may not go under, writes one course of wall and leaves two courses of air,
  and the answer overshoots the highest block laid by exactly those two. Nothing is stamped up there, so the
  building is correct — what is wrong is every consumer of the number. The dressing prop clamps its placement
  against the world ceiling with it, so a tall terraced building is refused a little sooner than it needs to
  be, and the preview views frame to it, so a terrace is drawn with a band of empty sky over it. The fix is to
  answer from what the stamp would actually write rather than from what the stack reserves — the wall stack
  already knows which of its courses resolve to air — which also makes the number right for the stilt house,
  whose ground storey is air for the same reason.

- [ ] **B72 — Two roof-thickness columns nothing reads.** `room_style.roof_thickness` (M0012) and
  `roof_style.thickness` (M0018) are written, clamped and round-tripped through the DTOs, and no stamper has
  ever read either: a roof's depth at a cell comes from the height field's own step down to its neighbours
  (`RoofField.Riser`), so there is nothing for a stored number to say. The composer offers no knob for it,
  which is the only reason it has not misled an author yet. Drop both columns and their DTO fields, or give
  the number a meaning under B69 — but not leave a third state where a row carries it and nothing looks.

- [ ] **B54 — A rebuild has no undo.** The rebuild now carries the finish and the credits across (B49, B52)
  and says what it trades before it runs (S39), so what it still replaces is replaced *on purpose*: the
  board, and the teams/spawns/wools/build zones the plan states. What is missing is a way back from a
  deliberate press that turns out to have been wrong. The mechanism is cheap, because both authored blobs
  are already rows in `map_artifact` keyed by a 64-char `kind` with no unique constraint: before each
  from-plan write, copy the current blob to a `…_prior` kind, and add a restore that puts both back and
  re-runs the pipeline from them (restore layout → `sketch/finish` → restore intent, the same chain the
  build uses, so the world cannot end up disagreeing with the layout). The finish step wants extracting out
  of `SketchFinishEndpoint` first so both callers share it. Surface it where the loss would be noticed: a
  one-shot *Undo this rebuild* in the plan editor's success panel. Deliberately not built with S39 — with
  the carries landed, the remaining exposure is a mis-click rather than silent data loss, and the
  confirmation already covers a mis-click at a fraction of the cost. This is the belt to that pair of
  braces, worth having once the studio is used by someone who did not write it.

- [~] **B44 — Theme + style library: the map's applied theme is still an inline blob.** The tables, the HTTP
  surface, the `/library` page and the sketch's pull/push bridge all shipped (`FEATURES.md`); two slices
  remain. **(1) Apply-as-snapshot** — a map's *applied* theme is still the sketch document's own registry, so
  "the library holds the reusable copy, the map holds a frozen one" is true only by convention: pulling a
  library theme into a sketch copies its JSON and nothing links them, but there is no snapshot record saying
  *which* library theme a map's paint came from, and no way to re-pull one when the library moves on. Give the
  map's scope store a forked instance with a `parent_id` back-reference, the same doctrine the generator's plan
  persistence uses. **(2) A data migration** lifting the themes inlined in a map's own `sketch_layout_json`
  registry into styles + themes + bindings, deduping identical materials — today a map themed without pushing
  anything out keeps its blob and the library cannot see it.

- [ ] **B47 — The library has no search, and the sketch's theme names are its own.** Two small gaps the
  library page left open, worth doing once it has enough rows to hurt. The style browser filters by kind but
  not by name, so a library of forty styles is a scroll; the theme half has no filter at all. And a theme
  pulled into a sketch takes the library's name as its sketch-side id, which the bridge uniquifies — pull the
  same theme twice and the second is `meadow-2` with nothing saying they are the same theme. A name search box
  on both halves, and a note on the pulled theme recording where it came from (which slots into B44's
  snapshot record rather than duplicating it).

- [ ] **B9 — Re-import a world into an existing map (keep the authored intent).** When an author tweaks the
  terrain (e.g. adds iron inside the spawns so the renewable populates) they currently have to import the
  updated world as a *new* map and hand-copy the intent across. Add a "re-import / update world" action on
  an intent-authored map that re-scans a chosen folder/zip in place — refreshing only the world-derived
  data (`islands_json`, `resource_block`, surface/layer parquets, monument candidates) and **preserving the
  `map_intent_json`**, then regenerating. Safe while island detection stays stable (the intent references
  islands by id, and spawns/wools are world coordinates); flag the author when the island set changes so a
  stale `islandTeams` mapping can be re-checked. (Manual procedure today: copy the `map_intent_json`
  artifact + re-scan, then `PUT /map/{slug}/intent`.)
- [ ] **B35 — Endpoint coverage: half the API is exercised by nothing.** `PgmStudio.Api` sits at **42.8%**
  lines (`tools/coverage.sh`), and the shortfall is not spread evenly — a long tail of endpoint files is
  effectively untouched while the tested ones are fine: `PreflightEndpoint` 2.6%, `ImportEndpoints` 3.6%,
  `IslandRolesEndpoint` 3.6%, `MonumentEndpoints` 5.3%, `LayersEndpoints` 5.5%, `ConfigureEndpoints` 6.2%,
  `AuthoringEndpoint` 8.2%, `IslandReviewEndpoints` 8.6%, `MapPlanEndpoints` 12.3%, `AnalysisEndpoints`
  13.0%, `RegionEndpoints` 15.6%. `ApiTestFactory` (B20) already gives schema-isolated MariaDB, so the
  marginal cost per endpoint is one happy path plus its error contract; these are cheap tests, not a
  redesign. Prioritise the ones that write: import, configure, region and map-plan.
- [ ] **B36 — The region/filter authoring-and-editing path is half covered.** A coherent cluster sits
  around 40–58% while its neighbours are high: `RegionAuthoringEncoder` 43.8% (370 uncovered lines),
  `RegionParser` 52.0% (295), `RegionEditor` 57.5% (180), `FilterParser` 48.9%, `RegionGeometry2d` 39.5%,
  `RegionBuilder` 43.7%, `FilterEditor` 41.9%, `WoolEditor` 58.5%. This matters more than the endpoint tail
  because it is map-contract logic, not glue — a silent regression here changes generated `map.xml` rather
  than returning a wrong status code, and `--authoring` is a manual harness, not a gate. Note the
  neighbours prove the standard is reachable: `MapParser` 92.9%, `XmlWriter` 88.1%, `RegionCategorizer`
  91.4%. Cover the type-specific region/filter branches first — that is where the uncovered lines are.
- [ ] **B37 — Objective separation via WX9 placeability: unify the resolutions, not the stampers.** The
  stamped-structure placeability attribute (`docs/world-export/structures.md` WX9, shipped for spawn iron)
  generalizes to the objectives: a **core or destroyable too close to a wool monument** merges structures
  that must read apart, and one **inside a spawn piece** is worse than ugly — the spawn's protection region
  makes an enemy goal unbreakable, so the map is silently unwinnable. The intended shape: the stampers stay
  heterogeneous (their inputs are irreducibly different — a wall owns a seam, a room a piece + marker +
  entries, an objective a marker + style), but every family's resolver produces one **resolved-stamp
  record** — kind, footprint box, `Placeable`, source marker — the uniform currency the pairwise separation
  rules run over (minimum distances to be decided from the corpus). `IronResolution` is the first instance;
  the preview's `StructureBox` is the drawing-side proof of the shape and would consume the record instead
  of assembling its own (placeability then reaches the iso view for free; `StructureBox` stays a separate
  type from `BlockBox` — exclusive maxes plus `Kind`/`Color`, a drawing frame rather than a volume). Stamp only placeable structures, flag the rest with the same
  marker-stays-visible discipline. Editor half: surface unplaceable markers on the plan canvas (the
  highlight ring the validation tab already uses for pieces), not only in the findings list.
  **Measured since filing.** The unwinnable case is worse than "unbreakable by the enemy": spawn protection
  emits `block="never"` on the shared `spawns` union (`TeamsGenerator`), so an objective inside it cannot be
  broken by *anyone*, the attacking team included. The wool path already solves exactly this —
  `WoolGenerator.SubtractMonumentsFromSpawns` folds each monument block out of the union so capturing a wool
  does not trip the rule — and cores and destroyables have no equivalent, which is why they need the
  separation rule rather than a fold: a goal inside enemy spawn is a design error, not a case to work
  around. `Placeable` still exists only on `IronResolution` (four consumers, all iron), and **preflight
  checks round-trip, mirror, buildability and traversability only** — so the Configure path, which can now
  author a core (`N12`), has no separation check at all while the plan path at least errors at compile.
  Three pieces landed since that make the record cheaper to build: `BlockBox` is now the one inclusive AABB
  (`B33`), a core's footprint is genuinely variable so a rule may no longer assume 5×5 (`G160`), and
  `CoreIntent.Box`/`DestroyableIntent.Box` are populated on **both** orbit images (`B53`), so a rule reading
  the expanded intent sees the real pair. **The objective half of the separation rules has since shipped as
  `OB17`** (`FEATURES.md`) — void, spawn room and wool room, as compile-blocking errors over a shared
  `ObjectiveFootprint`. What that leaves for B37 is the part it was really about: the uniform
  resolved-stamp record every family's resolver produces (`OB17` computes footprints inline rather than
  through one), the objective↔objective and objective↔monument minimum distances still to be drawn from the
  corpus, `StructureBox` consuming the record instead of assembling its own, and the editor half — an
  unplaceable marker outlined on the plan canvas — **which has since shipped end to end** (`B59`, `C44`,
  `FEATURES.md`): markers carry a persisted id, a finding names one, and clicking that finding rings the
  marker on the board. What is left of the editor half is only its timing: structural findings do not run in
  the live feed (`G161`), so a refusal appears at Compile rather than as the marker is dragged.

  **This entry is the parent of buckets 2, 3 and 6, and none of the three says so.** Read against the bucket
  bodies rather than the titles:

  - *The resolved-stamp record is bucket 6's own answer, filed independently.* Bucket 6's header asks for "a
    placement report the export can refuse on, rather than a document count standing in for what reached the
    world" — kind, footprint, whether it was placed, what asked for it. That is this entry's record, wanted for
    a different reason. `IronResolution(MarkerX, MarkerZ, MinX, MinZ, Size, Placeable)` is still the only
    instance and still has four consumers, all iron. Whichever of the two runs first builds it, and it must be
    told about the other.
  - *The distances are bucket 3, and the corpus half is no longer blocking.* This entry defers on "minimum
    distances still to be drawn from the corpus"; `B188` has since drawn them over 164 of 313 `dtcm` maps.
    What is blocking instead is which unit they are in — `B212`.
  - *`StructureClaim` is adjacent and is not the same record.* `B202` shipped it as occupancy — which columns a
    stamp owns, taken from the placement — and it answers "what was covered", where this record answers "what
    was placed, and may it be". An agent building either meets the other; the two compose (a record carrying a
    claim) rather than merging.

  `G65` is the third thing in this neighbourhood and is genuinely separate: it reconciles `FannedGraph`
  adjacency against `ContactGraph` for the *overlap* case, which is a question about whether two pieces touch,
  not about how far apart two placements stand. It shares `ContactGraph` with the measures above and shares no
  rule with them.

- [ ] **B34 — The two map-list endpoints disagree on sort order, and the dashboard gets the noisy one.**
  `MapsListEndpoint` branches on the `stage` query param onto two differently-ordered repository methods:
  `MapRepository.ListAsync` sorts `OrderBy(Slug)`, `ListByStageAsync` sorts
  `OrderByDescending(UpdatedAt).ThenBy(Slug)`. The dashboard always requests `?stage=…`, so it always gets
  recency order — and on the imported Edit corpus `updated_at` records when the *pipeline* last wrote the
  row, not when the author last worked on the map, so it carries no authoring signal. The 349 Edit rows hold
  only 29 distinct timestamps (a re-processing pass stamped them in ~22-map batches a second apart), so the
  list renders as 29 alphabetical runs concatenated — it reads as scrambled, with the three maps outside the
  supported range (`3084`, `allure`, `lost_haven`, never re-processed) parked at the bottom. Recency earns
  its keep on Sketches/Plans/Configuring, where the timestamps are real edits and the lists are short.
  Preferred fix: slug order for the Edit stage, recency for the other three (one line); alternatives are
  slug everywhere, or leave it and let recency come good once maps are edited in the studio. Cosmetic — no
  data is wrong, and both orders are deterministic.
- [ ] **B40 — The three dock styles are implicit; make them a type.** `Seat` picks between three seating
  rules using three *different discriminators* — `d.Wool is { } rich && Overhangs(rich.Family)` (shape
  family), `d.Kind == BoxKind.Frontline` (box kind), and falling through (everything else) — so there is
  nowhere in the code to ask which style a demand uses. The asymmetry runs deeper than the selector:
  two styles are named functions and the third is ~30 lines of inline loop body with no name; and the three
  are at different altitudes — `SeatOverhang`/`SeatFront` return a placed `(CellRect, BoxInterface)` while
  `SeatInRuns` returns a bare `int?` seat, leaving the caller to build the rect and the joint. That is why
  `boxes.Add`/`joints.Add` is written out three times with different arguments.
  **Scope:** (a) extract `SeatFullMouth` returning `(CellRect, BoxInterface)?` like its siblings, then (b)
  add an explicit `DockStyle { FullMouth, Overhang, ContactPatch }` **derived** from the demand — it is never
  sampled, it follows from the family roll — and dispatch on it. **Leave the failure policies alone**: they
  genuinely differ in kind (overhang demotes via `Compact`, the frontline kills the attempt, a wool may be
  dropped if another remains), and flattening them into flags loses more than it gains.
  The doc comment writes itself, because the three styles are indexed by *how much is known about the
  shape's entries*: full mouth knows nothing (require the whole mouth on one run and every entry lands —
  which is why the two-entry `U`/`H`/`Clamp` dock here); overhang knows there is exactly one and where it
  is; contact patch has no entry at all because a frontline is a face, not a corridor.
  **Oracle + the real constraint:** `ComposerFingerprint` + `ComposerVersionTests` must stay byte-identical.
  All three styles consume `rng`, so the invariant is not "does it compile" but **does the draw order
  survive** — hoisting one call above another moves the stream and every fingerprint goes red.

- [~] **B41 — Should a host's published capacity bound the grant it hands out?** The naming half has landed
  (`FEATURES.md`): `BoxJoint.Grant` is now distinct from a host's `EdgeOffer`, and the docstrings state the
  split — an offer's `WidthClass` is a *capacity* derived from the run's length, a grant's is a *selection*
  made per consumer kind. What remains is the behaviour question the rename deliberately did not answer.
  Today the two are entirely unlinked: `Seat` reads the hub's offers, keeps only `(Start, LengthCells)` as its
  **runs** and drops the published width, then `HubJoint` grants a width taken from the demand's kind
  (`WoolLaneCells` for a wool, `w` otherwise). So a hub can grant a corridor **wider than the run it sits on
  claims to support** and nothing objects. Either that is intended — capacity is advisory, the consumer knows
  its own lane — or the grant should be clamped to the offer, in which case a narrow run would demote a
  consumer's `cw` and some docks that succeed today would not.
  **Measure before deciding**: how often does the granted width actually exceed the published capacity of the
  run it lands on? If never, this is documentation; if often, it is a real gate the composer is missing.
  Changes what the filler builds, so it needs a before/after gallery and will move fingerprints.

- [ ] **B21 — MCP server: agent-drivable map authoring over the plan layer.** A thin MCP head (official
  C# SDK, `ModelContextProtocol` NuGet; new `PgmStudio.Mcp` project or a proxy over the running `:7894`
  API) so an AI agent can build a map end-to-end. The plan layer is the agent surface — `plan.json` is
  small, semantic, and the validator/evaluator return rule-id findings, giving the agent a compiler-style
  submit→lint→fix loop. **The gate this waited on is open: G119, G117 and G125 have all landed** (with
  G126's typed boxes), so the MCP head now wraps endpoints that exist rather than duplicating their
  plumbing. Of the three genuinely-new pieces, **`emit_family` is half-built**: G144's
  `GET /api/shapes/probe` already emits a canonical family through `BoxFiller` — profile check and
  docking gate included — and answers with the shape or a directed `FillRejection`, and
  `/api/shapes/probe/schema` serves the per-family knob surface and minimum box in the dock frame, which
  is the payload a tool-description needs to constrain its parameter enum. What is left of it is the
  *stamp*: placing the emission into an existing plan's typed box instead of returning a standalone
  `symmetry:none` plan. So the remaining new work is the PNG path for `plan_render`, that stamp, and the
  resource curation. Tools: `compose` (the G117 endpoint —
  request in; plan + canonical descriptor + derived facts + score out; starting material to mutate) ·
  `plan_validate` (errors + rule lint + full evaluator readout — the response must flag empty
  `placements`, which leave the feel terms vacuously green) · **`plan_feasibility`** (the G125
  read-back: mask → derived params → emit-compare, directed verdicts citing rule/task ids — the oracle
  that makes the loop converge; the validator alone passes plans the composer cannot produce, proven by
  the funnel exemplars scoring 0) · **`emit_family`** (stamp a canonical shape through the real emitters
  into a typed G126 box — agents never hand-cut rectangles) · `plan_render` (image content — agents
  self-correct far better seeing the board) · `plan_save`/`plan_get`/`plan_list` (the G119 store, with
  an agent-authored origin marking so agent output never contaminates the human-labeled corpus) ·
  `create_draft`/`export` (existing chain; return the export **link**, never the world zip inline). MCP
  resources: `generator/rules.md` + `generator/model.md` as the design brief, **`GET /api/shapes/catalog`
  as the machine-readable half of that brief** (G144 — the same vocabulary computed from the emitters, so
  unlike the prose it cannot drift, and each entry carries the tier that says whether the composer really
  produces it), `tools/seeds/*.plan.json` (incl. the funnel exemplars) as few-shot examples, and the G118
  verdict JSONL once it exists. **G146 blocks this, as correctness rather than tidiness**: the design
  brief describes Z as a production family and no sampler draws one, so an agent that reads the prose and
  asks for a Z authors a plan `plan_feasibility` then rejects — a loop that cannot converge, caused by a
  doc/code disagreement the catalog's tiering made visible. Either land G146 first or restrict
  `emit_family` to the catalog's in-mix tier. The same reasoning applies to the verdict JSONL: few-shot
  examples inherit whatever the sampler over-produces (measured at G144: the donut is 73 of 89 wool
  cards, U/H/L one apiece), so an unstratified corpus teaches an agent to write donuts. Scope
  is the **author agent** only; the **analyst agent** (mine verdicts/reject logs for rule + envelope
  refinements — read-only `verdicts_export`/`rejects_query`) is a small follow-on once the corpus has
  data, not before.

**DTM / DTC objectives (destroyables + cores).** The contract is `docs/pgm/destroyables-and-cores.md`
— it owns the XML surface, the **world-measured** structure families, the schema, and the two-team scope;
its rule ids (`OB*`/`DT*`/`DC*`) are cited below. Filed here (not `N`/`G`) because the bulk of each is
pipeline — parser, writer, schema, intent, stamper — with the plan-editor placement as the last mile.
**Both objectives now author end to end** (`FEATURES.md`): parse/write/codec, the schema, the world stamps,
and plan → intent → world → `map.xml` for destroyables (`B24`) and cores (`B25`). What is left below is the
import diagnostic (`B24e`), detection (`B26`), and the island-floor work the phantom classifier unblocked
(`B31`). The other thing it unblocked — water lanes — has shipped (`FEATURES.md`, `docs/pgm/water-lanes.md`).

- [ ] **B24e — Flag an *imported* map whose objective region holds none of its material (a warning, not a
  gate).** Scoped down: the authored half of this is **already covered by tests** — `DestroyableWorldTests`
  and `CoreWorldTests` walk each emitted region with PGM's `[min, max)` and count the blocks, which is
  exactly the assertion this task was filed to add. For a generated map the region *is* the stamper's box
  (OB8), so a runtime gate would re-check something true by construction. **What has no cover is the import
  side**: the corpus sweep found **10 destroyables whose region contains none of its declared material**.
  Those are the author's own maps, already broken before we touched them — so this is a **diagnostic on
  import**, not a block on re-export. Blocking someone's export over a pre-existing dud is the studio
  overreaching; telling them is the value.
  Never "the region is full": by OB12 a region is legitimately mostly air (a 3×3×3 region holding a 1×3×1
  pillar is correct and common), so anything stricter flags most of the corpus.
  **Note the category difference** before extending `MapValidity`: its one existing rule (a wool needs a
  monument) is *"PGM refuses to load this map"* — an `InvalidXMLException`, so the map is unloadable. This
  one is *"PGM loads it fine and the goal has zero health"*, which PGM itself only logs a warning for. Two
  different severities of truth; do not blur them into one list without saying which is which. World access
  is **not** the blocker it was originally filed as — 14 test files already read blocks out of a built
  world. (OB3, OB11, OB12)
- [ ] **B57 — `layer_segment` counts a build-region marker as solid ground.** Island detection now separates
  terrain from markers and from what a map erases before play (`FEATURES.md`,
  `docs/world-scan/terrain-ground-truth.md`), but that runs on `CleanColumns` → `islands_json` only. The other
  ingest derivation, `FeatureExtractors.Segments` → `layer_segment`, has its own exclusion set and applies
  neither rule, so a floor sheet at `y=0` persists as a solid span. Everything reading it at query time
  (`SegmentIndex.BaseColumns` → `IslandDetector.CleanedBaseFootprint`) therefore walks on a marker. Narrower
  than it sounds — that path feeds kit-reach, not the island picture the configure tool draws — which is why
  it is filed rather than fixed alongside. The two derivations should agree on what ground is, and the fix is
  to route the floor-marker rule through both. **Blocked in practice by re-import**: `layer_segment` is
  written once at ingest from a world that is then discarded, so changing it reaches existing maps only when
  a map can be re-imported.

- [ ] **B55 — Decide which API paths read a map *as played*, then wire `Includes:Root`.** `IncludeLibrary`
  and the resolved parse are in and gated by tests (`FEATURES.md`), and the harness uses them
  (`--resolve-includes`, `--water-lanes --includes-dir`). The API does not, because which reading each
  endpoint wants is a real question and the wrong answer corrupts data: **a resolved document must never be
  written back** (the include references are still emitted, so the fragments' content would be applied
  twice). Safe by construction today — nothing in the API passes a library — and the work is to choose per
  path rather than flip a global. Rule-level analysis (region categorisation, filter wiring, apply-rule
  order) wants resolved; anything that saves, exports or re-emits a document wants written; geometry
  (islands, layout, the seed corpus) does not care, since maps declare their own regions. Add
  `Includes:Root` beside `MapsRoots` in `Program.cs`, thread it only to the chosen paths, and make the
  distinction unmissable at the call site. (`docs/pgm/include-resolution.md` §2)

- [ ] **B56 — Parse `<score>` and `<flags>` so an include-supplied objective is actually read.** Include
  resolution landed (`FEATURES.md`) and measured its own limit: **82 corpus maps take their objective from a
  fragment** (`bridge`, `touchdown`, `ffb`, `flag-battles`, `5cp`), and resolving them changes nothing about
  what the studio reports, because `<score>` (TDM) and `<flags>` (CTF) have no parser here. Splicing is not
  what closes that; a parser is. Until one exists those maps read as objective-less — which the
  supported-range gate deliberately tolerates, since a module arriving from a fragment round-trips through the
  include reference and cannot be silently lost. Add each tag to `ParsedObjectiveModules` as its parser lands.
  (`docs/pgm/include-resolution.md` §4)

- [~] **B58 — Finish the destroyable ranker.** The core half has shipped — gathered at ingest, stored in
  `core_candidate`, and confirmed in the Cores phase (`FEATURES.md`). What remains is the other objective,
  and it is measured but unbuilt (`docs/world-scan/objective-suggestion.md`).
  **Destroyables: the discriminating signals are measured, the detector is not written.** They are not
  identified by anything about the structure — size spans 1 to 31,105 blocks and fill is uninformative — but by
  their **neighbourhood**, dumped 10 blocks outward and down to `y=0` for all 614 declared structures.
  *Isolation*: a declared destroyable has a median of 6 same-material blocks within 10, against 65+ for a false
  cluster, because decoration repeats and a goal is placed once. *Elevation*: it sits a median +5 blocks above
  the surrounding terrain, against −2 for false clusters. Together, with **no size cap and no air-face test**
  (both of which were discarding truth), `same ≤ 8 & elevation ≥ +2` keeps 553 of 1,062 true clusters against
  600 false — 48% precision at 52% recall, a four-fold precision gain on the previous best. `same ≤ 0 &
  elevation ≥ +2` reaches 65.6% precision if a stricter list is wanted.
  Build the detector at those operating points, gather at ingest into a `destroyable_candidate` table beside
  `core_candidate`, and validate the same two ways cores are (corpus + a composed plan). **Scope honestly to
  84%**: obsidian, emerald, gold and ender stone carry that share of declared destroyables, and the wool /
  stained-clay / stained-glass remainder must stay out — admitting wool takes the candidate set from 15,488
  clusters to 439,440, because a CTW map is made of wool.

- [ ] **CV16 — the authoring canvases have no frame budget, only habits.** The zoom stall (fixed in
  `FEATURES.md`) was two unrelated per-event costs that happened to land on the same handler, and neither was
  visible until measured: a grid rebuild whose memo was written for pan, and a `.NET` interop call per wheel
  tick. Both are the same class of mistake — doing work per *input event* rather than per *frame* — and
  nothing in the canvases prevents the next one. Two guards worth having: a debug overlay (or an e2e probe)
  that reports main-thread ms per interaction burst, so a regression shows up as a number rather than as
  someone noticing the picture go soft; and a rule that anything crossing into Blazor from a canvas handler
  goes through the frame coalescer, since interop is the expensive edge and its cost is invisible from the JS
  side. The screenshot approach does **not** work for this class of bug — `page.screenshot()` forces a fresh
  raster, so a transient compositor artifact never appears in the capture; measure the handler, not the pixels.

- [ ] **G158 — seed the library with a curated set.** An author can now build a style once and reuse it, and a
  theme that binds only the buckets it changes (`FEATURES.md`), but a fresh install's library is empty — so the
  first desert or snowfield is still built by hand. Ship a curated set of styles and themes as seed rows: the
  shipping finish decomposed, plus a handful of biomes (desert, tundra, mesa, nether) each reusing the same rim
  and fill. A preset is just a library theme, so this is a seeding step, not a second mechanism — the open
  question is only *when* it seeds (a migration, or a "restore the starter set" action that cannot clobber
  edits).

- [ ] **G156 — cell-size-aware generator room sizing (WX2's generator half).** The stamped-room minimum is
  8×8 **blocks** (`docs/world-export/structures.md` WX2) but the emitters size rooms in **cells**
  (`ShapeEmitter.RoomDepthCells` = 2, corridor widths in cells), so a small-cell board (cell ≤ 3) can emit
  a wool room or spawn its own export refuses. Make the room depth/width floors cell-size-aware — enough
  cells to reach the block minimum — through `MinBox` and the spawn profile; the composer's cell-5 boards
  already clear it by construction, so this binds only when boards go small-cell.

- [ ] **G163 — `map-layers`' rebuild-confirmation step flakes about one run in three.** The step drives
  Compile on a freshly-opened plan and reads the drawer; when the plan document has not reached the client
  yet it compiles an empty plan, which is a 422 by design, so the drawer never opens and the following
  `page.click` times out at 30s. The spec guards it with a fixed `waitForTimeout(1500)` — a duration
  standing in for a condition, and the wrong guess about a third of the time. Measured 1-in-3 both with and
  without the `OB17` rule, so it is timing rather than validation. Waiting on the first piece id label
  (`.map-canvas-svg text`, the overlay's proof the document arrived) was tried and did **not** fix it, so
  the stall is later than the document load. **A caught failure now names the click, and it is not the one
  the paragraph above blames.** The step got as far as reading the drawer's button label ("the button names
  a rebuild" passed on `Rebuild this map`) and then timed out on `page.click("Rebuild this map")` — the
  *second* click, on a compile that answered 200, long before the empty-plan compile the 1500ms guard is
  aimed at. The recorded 422 is an earlier fault on the same page, not this one. A 30s timeout on a button
  whose text was just read means the element was found but never became actionable, which points at a
  drawer that keeps re-rendering rather than at a document that has not arrived — so the fix is a wait on
  the drawer settling, and the 1500ms guard may be guarding nothing. A flake in the browser gate costs more
  than the step is worth, because it makes every unrelated run ambiguous.

- [ ] **G161 — the casing panel lets an author build a core the compiler will refuse.** `G160` put the
  casing knobs in the plan's marker panel, clamped independently — size 1..64, shell 1..16 — so a 5×5 casing
  with a shell of 3 is one keystroke away and leaves `size − 2·shell = −1`, a solid block of obsidian that
  can never leak. `PlanValidator` catches it with a good message, but only at the **compile** gate (422):
  the live inspect/evaluate feed does not run `Validate`, so the author sets the number, sees nothing, and
  finds out a phase later. Either state the interior in the panel as the two numbers move (it is the honest
  readout — "3×3×3 lava inside", or the reason there is none), or run the structural findings in the live
  feed so every rule reports where the edit happened. The second is the general fix and covers the
  destroyable style and float/leak rules too.

- [ ] **G173 — the conifer's bulk sits at mid-height, not in the bottom third.** The whorled tree now rings
  its whole trunk (three to five branches per ring, 5.2 courses apart, each ring shorter than the last, none
  forking, a spire at the apex) and clears the first of the two measures the corpus separates families on:
  **63% of its foliage in the lower half against the staggered form's 49%**, inside the hand-built conifers'
  60–77%. The second is still out: its **widest tenth is #4.4** where a hand-built conifer's is #1–#3. The
  ring-length taper and the apex are already spent; what is left to try is the ring *spacing* (a hand-built
  conifer's tiers are evenly spaced but its lowest tiers are much the longest, which a linear taper across a
  short axis cannot express) and the leaf cluster's own size per ring. `tools/tree-corpus/crown-profile.cs`
  measures the corpus side.

- [ ] **G176 — a grown tree still carries twice an author's wood at mid heights.** The corpus is nearly flat
  in height — 23 blocks of wood at 5–9 courses, 36 at 10–13, then 51, 53, 53 to 40 — and the grower now runs
  13, 19, 47, 82, 113, 173, 221, 322 over heights 6 to 40. The trunk radius and the lateral count no longer
  scale steeply, so what remains is limb *count*: an author's median tree carries three limbs off its stem
  where a 20-course generated one carries thirteen. Cutting them further trades against crown coverage, since
  each tip is a cluster, so the honest next step is to let a cluster cover more than one tip rather than to
  starve the crown. `tools/tree-corpus/grower-gate.cs --by-height` against `census.cs` is the reading.
- [ ] **G150 — stamp a catalog shape into a drawn box.** The plan editor can draw a typed box and then ask
  whether the composer could have produced what is in it (G125's feasibility panel), but there is no way to
  go the other direction and *place* something known-producible: nothing in `Features/Plan/` references the
  catalog or the emitters. So an author hand-cuts rectangles and finds out afterwards. Give a selected box a
  **family picker** — the in-mix tier of `GET /api/shapes/catalog` (G144), which is exactly the set the
  composer really samples — plus the knobs `GET /api/shapes/probe` already serves per family, and stamp the
  emission into the box as its members. Producible **by construction**, so the feasibility panel goes green
  without the author aiming at it.
  Most of this exists. The probe endpoint already emits through `BoxFiller` (profile check and docking gate
  included) and answers with the shape or a directed `FillRejection`; `/api/shapes/probe/schema` already
  serves the per-family knob surface and minimum box in the dock frame. What is new is the *stamp*: writing
  the emission into an existing plan's box rather than returning a standalone `symmetry:none` plan, which
  means placing pieces at the box's origin, giving them ids under the box, and replacing whatever was there.
  This is the editor half of **B21's `emit_family`** — build it here and the MCP tool wraps it rather than
  reimplementing it. It also pairs with G149: placing known-producible shapes and watching the G148 land
  readout move is the most direct way to find out what the budget is actually worth.

- [ ] **G151 — a box's rect should be the bounding box of its members.** The members inspector offers
  "Fix these members" / "Follow containment", and the fixed half behaves oddly on purpose-built-for-something-
  else grounds: named membership (`PlanBoxes.MembersOf`, the `Members` list) ignores geometry entirely, so a
  piece can be dragged *out* of its box and still be carried when the box moves (`plan-canvas.js`, the box
  drag translates `d.carried` in both modes). That is not an authoring mode — named membership exists for
  **provenance**, so a pinned board can record the grouping that actually produced it off `BoxPartition.KeyOf`
  rather than having it re-derived approximately. Exposing it as a toggle asks the author to edit with a
  mechanism built to preserve history.
  The fix is not a third mode, it is separating two questions that are currently one button. **Which pieces
  are members** is legitimately two modes (named vs containment) and both should stay. **What the rect is**
  should not be a mode at all: it should always be the bounding box of the members. That is not a new rule —
  `BoxPartition.Of` already computes a box's rect as `Bbox(members)`, and `Box`'s own contract says a box's
  contents "must touch its edges", which is the same statement. So dragging a member extends the box,
  dragging the box moves its members, and a member outside its own box becomes unrepresentable rather than
  merely strange. One case to decide: an empty box has no bounding box — keep its drawn rect until it has a
  member, which also leaves the draw-then-fill flow working as it does now.

- [ ] **G153 — the feasibility read is per box and is reached through a list.** `G125` computes producibility
  **per box** and renders it in a left-panel list, so after clicking a box on the canvas the author has to find
  that same box again, by name, in a sidebar — for a read that is already about the thing under the cursor. The
  inspector on the right already opens on that box with its id, kind and members; the verdict belongs there,
  beside them: the parameter tuple that reproduces it or the nearest candidate, its directed findings, and the
  rule or task id each finding cites, with the click that paints the missing/extra cells kept as it is. The
  unit-level arrangement findings are genuinely **not** per box (parallel fronts, the frontline's pinned face,
  seat separation) and stay in the left panel, which leaves that panel one coherent job instead of a mixed list.

- [ ] **G154 — one plan editor, two bindings, two different tools.** `PlanTool` serves `/plan-editor` and
  `/maps/{slug}/plan` from a single component through five `@if (MapBacked)` branches, and the two render as
  different products. Map-backed gets the phase rail (Info · Draw), the flow bar, and the three panels as chips;
  the bare route gets no flow bar, no phases, the same three panels as **rail buttons**, and a collapsible
  sidebar the map-backed one cannot have (`SidebarOpen => MapBacked || leftOpen`). Same panels, two navigation
  models, one file — the thing the tool-consistency alignment exists to prevent.
  Unify on the phase-rail + flow-bar + chips structure and keep the collapsible sidebar for both. The route may
  change **only** the topbar — its crumbs and which actions exist — because that is where the binding genuinely
  differs: a map-backed plan saves into its map's artifact, while a plan row saves as a row and forks when it
  was generated or imported. Rename the bare route to `/plans/{id}` (and `/plans/new`), which says what it is
  bound to where `/plan-editor` says nothing, updating the generator hand-off, the smoke sweep's route list and
  the plan schema doc with it.
  **Do not delete the route.** It is the only surface that opens a **plan row**, which is what the generator
  hands a candidate off as and what `G119`'s fork-on-edit rule operates on; routing candidates through
  `/maps/{slug}/plan` would mint a map per candidate looked at, and New, Import, Open and the origin badge have
  no home on a map-backed plan.

- [ ] **G149 — the land budget is a number the composer reads and then overshoots.** The first thing the
  G148 readout showed, measured over 40 boards at 12 players/team (budget 50 cells): land runs **63% to 222%**
  of budget, median **115%**, and **28 of 40 boards are over**. So the budget is not a cap — it is an input
  the allocator consults for its decisions (lane width, whether there is a frontline, the hub caps, the wool
  count — `TeamUnitAllocator.cs:46-52`, `UnitTuning.WoolCount`) and then nothing reconciles the result
  against it. `BoxFiller.WithinLandTarget` exists and no production path calls it.
  Nothing caught this because nothing measures it: the only land-ish term is `fill-ratio` (G8), which is land
  over the board's **bounding box**, not land against the **budget** — a different quantity that can sit
  happily in its band while the unit is at double its target.
  Decide what the budget means before changing anything, because both readings are defensible: either it is a
  target the fill should be reconciled against (then something must spend the overshoot down — the
  two-currency accounting says fragment converts surplus land to build, so the question is whether fragment
  ever runs), or it is only a sizing heuristic (then rename it, drop the "budget" framing, and stop implying
  a contract that does not exist). What is not defensible is the current state, where a number named budget is
  exceeded by half again on a typical board and nothing says so. Note G138 is adjacent but distinct: that one
  is about the composer taking the first acceptable plan rather than ranking; this is about the plan it takes
  not honouring its own sizing input.

- [ ] **G147 — verdict coverage on the catalog: which buckets has nobody judged?** *(sequenced after G118 —
  there is nothing to count until verdicts exist.)* The browse feed hands the author whatever the composer
  samples, so collection is passive: the corpus ends up shaped like the sampler, and the parts of the space
  the sampler rarely visits stay permanently unjudged. The measured skew makes that concrete — the donut is
  73 of the 89 wool cards while U, H and L are one apiece (G144) — so scrolling will produce a donut corpus
  and silence everywhere else, which is exactly the wrong input for rule refinement.
  The fix is to show the denominator. A `StructureNames.Canonical()` key is a triple
  (`wools:… | hub:… | front:…`) and the catalog already renders each component of that triple as a card, over
  a space now small enough to enumerate: 81 wool classes up to rotation/reflection, 7 hub forms, 3 frontline
  forms. So add a third filter facet beside *kind* and *reach* — **coverage** — with three states: *judged*
  (this bucket has verdicts), *thin* (one or two, not enough to trust), *unjudged* (nobody has ever looked at
  a board shaped like this). "Find me something nobody has judged" then becomes a chip click, and collection
  turns from an infinite scroll into covering a space with visible edges.
  Needs one query — verdict counts grouped by the structure key — which is a `GROUP BY` on the column G118
  already stores, so **design it with G118's schema rather than bolting it on after**. The UI is small: a chip
  row and a count badge over the same tally plumbing the catalog's `ByTier`/`ByFamily`/`ByKind` already use.
  **A card is a component of a bucket, not a bucket** — a board reading `wools:donut,l` touches both the donut
  and the L card — so per-card coverage is an aggregate over every bucket that card participates in. Build the
  aggregate first (cheap, and enough to spot a blind spot); add a per-bucket drill-down only if the aggregate
  proves too coarse to act on.

- [ ] **G145 — five emitter knobs are unreachable from the composer.** `ShapeEmitter.Emit` takes
  `attachments`, `woolExtend`, `entryShift`, `woolShift` and `attachmentOffset`, and `WoolBoxEmitter.Emit`
  passes all five through — but `WoolBoxEmitter.Fill`, the only path `BoxFiller` and therefore the whole
  compose pipeline uses, forwards none of them (`WoolBoxEmitter.cs`, the `ShapeEmitter.Emit` call inside
  `Fill`). Their only callers in the tree are `tools/compose/box-gallery.cs` (the two-attachment and
  moved-attachment donut cards). So the two-attachment donut, the extended-wool donut and both scythe
  endpoint shifts are built, tested, drawn in the galleries, and **cannot appear on a generated board**.
  Decide per knob rather than in bulk: the donut's second attachment is a genuine multi-access shape the
  hub could dock twice and is the strongest candidate to plumb; the scythe shifts are moot until the
  scythe itself is admitted (G146). Plumbing one means widening `WoolFill` (it already carries
  `AttachmentWidth` and `RingWalls`, so the shape of the change is settled) and giving `UnitRequests` a
  draw for it. **Do not "fix" this by deleting the knobs** — `EmitterPlacementKnobTests` gates them and
  `model.md` §4 describes them; the gap is the plumbing, not the geometry.

- [ ] **G146 — two families are in the vocabulary but never on a board.** The emitter builds eight
  terminal-capped families; the composer puts six on boards. **Z** is listed in
  `FillMenu.ProductionFamilies` and `BoxFiller` fills it happily, but `UnitRequests.WoolRequest` never
  draws it — the rich branch picks donut, then U/H/clamp, else L, and the fallbacks are I. The only caller
  that could reach it is the roll-indexed `BoxFiller.Fill(box, mouth, cw, roll, …)` overload, which has no
  caller in `src/` at all. So the menu advertises a family the sampler cannot produce, and the browse
  tool's own filter chip for it can never match. **Scythe** is the honest case: excluded from
  `ProductionFamilies` with a stated reason (its bay's mouth is its docking edge, so a flush dock seals it
  into WL8's forbidden enclosed void), with the elevation-stage alternative already parked as G81.
  Two separate decisions: either give the sampler a Z draw or drop Z from `ProductionFamilies` so the menu
  stops advertising it (the second is a one-line honesty fix and should not wait on the first); and leave
  the scythe out until G81 lands. Either way the catalog page (G144) will render both under a
  *reachable* / *emitter-only* badge, so the gap becomes visible rather than folklore.

- [ ] **G143 — the board deriver calls segments "edges", which is the one word the model reserves.**
  `model.md` fixes the vocabulary: an **edge** is one full side end to end, a **run** is a contiguous
  stretch along a boundary, an **interval** is where two things touch. `BoardStructure` breaks it —
  `FrontEdges`, `IntraEdges`, `SelfEdges` and `RedstoneEdges` are all `List<(X1,Z1,X2,Z2)>` of
  **cell-boundary segments**, not edges, and the deriver's own comments already call the grouped result
  a *run* (`GroupFrontlineRuns` → `FrontlineRuns`). So the code contradicts itself in one file: the raw
  list is named for a full extent, the grouped one for what it actually is. Rename the four to
  `FrontSegments` / `IntraSegments` / `SelfSegments` / `RedstoneSegments` (or `…BoundarySegments`), and
  sweep the comments that call a segment an edge. Mechanical — the type is a tuple list with a handful
  of consumers (`BoardDeriver`, the deriver gallery, the evaluator terms reading front edges) — but it
  has to land in one commit with the doc, or `model.md` will assert a rule the code visibly breaks.
  Check `BoxEdgeInterface`/`EdgeSpan`/`EdgeInterval` in the same pass: those name a genuine full edge
  and its sub-intervals, so they are correct and should stay, which is exactly why the deriver's misuse
  is worth removing rather than tolerating.
- [ ] **G138 — The composer accepts, it never chooses: a soft score has nowhere to act.** `Composer` takes
  the **first** plan that clears the gate and `break`s (`Composer.cs:59-84`) — no ranking, no comparison, no
  best-of-K. It contains zero references to `Evaluate` or `Score`, only `Gate`, and `Gate` runs hard terms
  only (`LayoutEvaluator.cs:86`). Worse, the compose path builds its context as `EvalContext.Build(plan)`
  with no envelopes, which defaults to `SeedEnvelopes.**Empty**` (`EvalContext.cs:34-38`) — so every soft
  term looks its band up, gets null, and stays dormant by design. **The authored envelopes have no causal
  influence on generated output whatsoever**; they only score plans after the fact, via the
  `Evaluate(PlanModel, …)` overload the API endpoints and galleries call.
  So any soft rule derived from `G118`'s verdicts is **inert until this lands**: generate K candidates,
  score all K, return the best. The loop already generates and discards candidates, so the change is small —
  but it converts the composer from *first-acceptable* to *best-of-K*, which is a real behaviour change and
  will move every fingerprint.
  **Sequence it after the bands are calibrated, not before.** Measured over 560 composed plans, a ranking
  today would be almost entirely a `spawn-wool-ratio` contest (outside its band on 44% of applicable plans
  at median distance 1.64) while four terms score nothing at all — see the `LEARNING.md` debt entry.
  Ranking before recalibration just amplifies one badly-fitted band. Order: `G118` collect → calibrate /
  gate the vacuous terms → this → soft rules become causal.

What stays here is the concrete non-design work on *imported* maps (island detection + playability):

- [ ] **G9 — Re-scan the corpus with stair-aware detection (remaining slice).** The over-split
  **detection fix landed** (`FEATURES.md`: `CleanColumns` + `DetectStairAware`), as did the review
  flag + role classifier. What remains: (a) **re-scan the corpus** so the stored `islands.json` /
  `island_sketch_json` reflect stair-aware (the live DB + `pgm-studio-output` were generated with the legacy
  detection — needs the source worlds, `OvercastCommunity/CommunityMaps`+`PublicMaps` `ctw/`), and decide
  whether to refresh the `--islands` Python-parity oracle to match; (b) the residual `a_new_day` **isolated
  raised-decor specks** (≈37-block grid bits with no walkable connection — correctly `small` via
  `IslandClassifier`, but a per-island prune could drop them); (c) any **under-split / merged** read beyond
  `abstract` (whose stained-glass build-floor is now excluded — `FEATURES.md`): `LooksUnderSplit` is the
  catch-all flag; the residual lever if one is found is to fall through to surface-based detection when a
  cleaned-base component is a map-spanning low-Y slab. Serves the shipped island-health / analysis
  features; the decompose-queue UI slice was dropped with the corpus-mining flywheel.
- [ ] **G12 — Re-prune flying blobs above terrain (stair-aware regression).** Stair-aware connectivity fixed
  the over-split (disconnected islands) but **re-introduced** the stark-y-jump / flying-island problem:
  decorative masses floating above the map (dragons/birds) now merge back into the islands when a near-vertical
  surface chain bridges them (e.g. **Duality**, **mame_i_shrunk_the_pvpers**). Re-add a guard: stop joining
  across a **really big y-increase**, and/or identify & **prune blobs whose base sits well above the terrain
  band** (the old float-prune did this on `DetectHeightAware`; the stair surfaces now leak past it).
  **`max_build_height`** is a natural cut/prune ceiling — anything whose mass is above it is non-playable
  decor. Re-validate the over-split fixes (a_new_day/thunder) still hold after re-adding the ceiling.
- [ ] **G65 — FannedGraph ↔ ContactGraph adjacency reconcile (deferred from G59).** `FannedGraph.LandAdjacent`
  (reachability) still diverges from the rect-layer authority `ContactGraph` on one count: any area overlap
  connects regardless of surface delta, while `Components` unions an overlap only at `SurfaceDelta == 0`.
  (The corridor-width half was reconciled — `LandAdjacent` now accepts Narrow seams, matching `Components`.)
  Pick one rule for the overlap case and add a test; needs per-node surface carried into the fanned graph and
  validation against the traversability harness (`tools/PgmStudio.RoundTrip --traversability`).

  **It gates route enumeration, which raises it from a consistency chore.** `G127`'s flow read counts attack
  routes at piece fidelity — four on `p30-s374`, from two frontline legs × two wool doors — and a route count
  is an enumeration over piece adjacency. While the two graphs disagree about what "adjacent" means for an
  overlap, the count depends on which one was asked, and nothing at the call site would say so. Whichever
  rule is picked, the route reader must name the graph it read.
- [ ] **G2 — Protection-aware reachability port (memory stage S4).** `MapValidity` (every-wool-needs-a-monument)
  and the `NVAL` export gate (`PreflightEndpoint`) already shipped (`FEATURES.md`). The open slice is to **port
  protection-aware reachability** from `scripts/generator/validate_play.py` to C# `Analysis/Playability`:
  today's `Traversability.Check` only tests connectivity, **not** spawn-protection-as-wall, so it passes maps
  the generator's Python validator would fail. Feed it into the `NVAL` / preflight gate.

- [ ] **G164 — interference: how much of one side's route the other side's route covers.** Every flow
  measure so far reads one traversal at a time, and a single route cannot express tension. Tension is two
  corridors laid over each other: the attacker pushing from a captured wool room toward the remaining
  objective, and the defender travelling from spawn to the same objective. The measurable is the fraction of
  the defender's corridor that the attacker's corridor also covers, computed on the cell mask the same way
  the corridors already are. Measured over 453 two-wool boards at `marker-id-1`: median **34%**, half or more
  on 27%, and **no board reaches zero** — passing the reinforcement lane is unavoidable on generated output.
  This is the term that gives a hub void a purpose the ways-round-a-void count cannot: on a holed hub the
  near way leaves 76% interference and the far way 37%, and the far way measurably reduces the collision on
  74% of the boards offering one, so a layout whose two ways collide equally has bought nothing. Derive side
  belongs beside `BoardDeriver`; the term belongs in `Evaluate/Terms`. It reads a pair of routes rather than
  one, so the origin "a captured wool room" comes from G168's post-capture state — until that exists,
  computing it once per wool treated as captured is the honest stand-in. Background and the full numbers:
  `docs/gameplay/match-flow.md` §2, §4.9.

- [ ] **G165 — dock arrangement belongs in the structure summary.** Which face of the hub each box seats on
  is a board property with measured consequences and no representation anywhere: it is not the hub's body
  form and not the approach family. With the compass rotated so the frontline is *front*, generated boards
  split **canonical** (spawn *back*, wools *left*+*right*) 27% against **lopsided** (spawn lateral, one wool
  on *back*) 73%, and the split predicts two things — the median spawn-distance imbalance is 0.18 against
  0.40, and the second-wool rotation runs within ten blocks of the spawn on 63% of canonical boards against
  2% of lopsided ones. The faces fall straight out of the mouth positions the box read already computes, so
  the work is small: add them to `StructureSummary` and to `StructureSummary.Canonical()`, which makes the
  arrangement a browse-sieve filter and a verdict/duel bucket key for free. **Land it before verdicts
  accumulate**: `Canonical()` is persisted on a pinned plan as that bucket key, so extending the string
  reshapes every bucket already stored, and a later change needs a key version rather than an edit.

- [ ] **G166 — seating should prefer the canonical arrangement.** `UnitSeating` chooses which hub edge each
  neighbour request seats on, and takes no view on the combination; the result is that three boards in four
  come out lopsided (G165). Prefer the spawn on the edge opposite the frontline with the wools on the two
  lateral edges — the arrangement built maps converge on. The measured payoff is the imbalance halving (0.40
  → 0.18) and the restoration of the rotation-past-spawn dynamic that the lopsided arrangement removes. This
  changes where boxes sit, so it is a geometry change: composer version bump, re-recorded fingerprints, and a
  before/after gallery. Constraining the seat choice can only raise the rejection rate, so measure that
  alongside the arrangement split rather than assuming it stays flat.

- [ ] **G167 — a holed hub should seat its docks across the hole.** A ring, double-hole, P or G hub only
  offers two ways across when the two docks straddle its void, and today that is left to chance: ring hubs
  deliver two ways on 163 of 224 spawn-to-wool crossings, and the ones that do not are dead by seating rather
  than by shape — the same body form with both docks on one side is a wide room with a decorative hole in it.
  When the sampled hub body encloses a void, prefer opposite walls for the two docks. The value is not the
  extra distance but what G164 measures: the far way round drops interference from 76% to 37%, which is
  the difference between an alternative and an alternative worth taking. Geometry change, so the same
  fingerprint and gallery costs as G166, and the two should land together or in a known order since both
  touch the same seat choice.

- [ ] **G168 — a board is worth evaluating in two game states.** A two-wool map is not one arrangement but
  two in sequence: before the first capture both objectives are defended from the spawn, and after it one
  room is the attacking team's forward node — a place worth travelling to for the chest gear the generator
  emits — and the wool-to-wool route becomes the live one. Terms that are vacuous in the first state carry
  the whole second phase, so evaluating only the opening scores half a match. This is a change to the
  evaluator's shape rather than a new term: `EvalContext` carries which state is being read, and the terms
  that only apply post-capture (G164's interference, rotation between objectives) declare it. Decide
  early whether the two states produce two scores or one combined figure — a single number that averages a
  strong opening against a hopeless second phase describes neither. The played account is in
  `docs/gameplay/match-flow.md` §4.8.

### The generator in the studio (G117–G120) — parked while the authoring loop is the focus

The box pipeline is **the** composer and the emitted layouts are good enough to work *with*, so the
bottleneck sits in the feedback loop rather than in the grammar. This slice integrates the generator into
the studio itself — compose interactively, filter what to see, and **collect annotated keep/discard
verdicts** that become the labeled positive/negative corpus every later refinement feeds on. The showcase
(G121), the persistence foundation (G119), browse mode (G117), its structural sieve (G128) and the shape
catalog page (G144) have shipped (`FEATURES.md`); verdicts are next when the theme resumes.

**Persistence doctrine for the whole slice: the feed is ephemeral; only human attention persists.** A plan
enters the database exactly when it is voted on, pinned, or saved from the editor — never while scrolling.
Generated rows are **immutable**: editing one forks a new `authored` row with a `parent_id` back-reference,
so the labeled corpus cannot be contaminated after the fact. Browse votes (absolute) and duel results
(pairwise preference) are **separate datasets**, unified only at analysis time.

- [~] **G159 — a composed plan should carry its voids before it is compiled.** The compiler declares them on
  every compile (`PlanVoids`, `FEATURES.md`), so a board's holes are correct wherever it is built. What a
  freshly composed plan does not yet carry is the declaration itself: `Composer.Compose` returns pieces only,
  and the buffers appear when something compiles it. Running the same step at the end of `Composer.Assemble`
  makes a generated plan self-describing from birth — one line, no new geometry, and it cannot disagree with
  the compiler because it is the same step. The cost is the reason it is not folded in already: the composer
  fingerprint digests the plan JSON, so every board's digest moves, which means a `ComposerVersion` bump and a
  re-record of `tools/compose/composer-fingerprints.json`. Worth doing on the next version bump rather than
  spending one on annotation.

- [ ] **G118 — Verdict collection.** Tap-chip annotation tags (large toggleable pills, multi-select —
  never checkboxes) available on both vote directions, both optional; the tag set seeded from the
  layout-rules vocabulary (wools-too-close · wools/spawns-should-swap · flat-front · crammed-mid ·
  no-rotation · great-hub · …, extendable), each tag carrying its rule id where one exists — a
  downvote tagged with a rule whose term did *not* fire is a ready-made evaluator bug report. Persist
  {plan ref, descriptor, verdict, tags, free-text note, evaluator score + per-term snapshot, evaluator
  version} via G119; JSONL export so the labeled examples drive rule refinement, envelope
  regeneration, and AI-assisted analysis.

- [ ] **G120 — Duel mode (the tournament).** Bucket-scoped side-by-side comparison: a **bucket** is a
  filter combination (e.g. 2 wools · F frontline · double-hole hub · one L + one donut), so both
  boards made broadly the same structural decisions — the closest thing to a controlled comparison,
  and a minimal-pair factory for the evaluator's labeled set. Two big renders, pick the better; the
  result is a **preference pair** `(winner, loser, bucket)` — never converted into a downvote — with a
  per-bucket ranking (Bradley-Terry/Elo-style) derived at analysis time. A separate dataset from the
  browse votes by design.

- [ ] **B79 — `map-layers` e2e: the plan editor's Compile button never arrives (13/14).** The suite drives to
  `/maps/{slug}/plan` on the seed's built map, then clicks `button:has-text("Compile")` to check that a
  *rebuild* states the trade before replacing a board someone has worked on. The click times out at 30s and
  the page records `HTTP 422 /api/plan/compile`, so the confirmation half of the suite never runs. **Not a
  regression from the island gate or the author fix**: it reproduces identically at `f42ec58`, the commit
  before either landed, and it is not the corpus either — re-running with `MapsRoots` pointed at an empty
  directory fails the same way, so the 15 generated maps under `CommunityMaps/ctw` are not the cause. It is
  also not contention: it reproduces with the suite run alone and no dev server up. It did pass once, on the
  first post-merge run, which is what makes it worth a task rather than a revert. The 422 is the plan
  **validator** refusing what the page posts, while the same map's stored plan compiles 200 through curl —
  so the first thing to find is what the editor sends that the stored document does not, and the finding
  ids in the 422 body name the rule.

## Lower priority / parked

Existing-Edit (`/maps/{id}/edit`) authoring features — **not** used by the intent generator (which
auto-wires), and Edit is frozen. Resume when the existing-map authoring path is picked up. Their
*backends* are done (`FEATURES.md`).

- [ ] **Wire-after-group + filter-wiring UI** (ex-`N4` + ex-`F1`). Group regions in Edit → apply
  a wiring template by role; cross-step carve-out (complement) detection; canvas Ctrl-click
  multi-select. The wiring backend (`FilterWiring` appliers + `POST /wiring/apply`) is done.
- [ ] **Symmetry counterpart accept/reject UI + IoU equivalence** (ex-`F3` + ex-`A2`). Canvas
  preview/confirm for orbit-created counterparts + `regions_equivalent`/`is_counterpart` detection for
  dedup + symmetry-violation review. The counterpart + orbit-fill backend is done (the authoring
  generator already uses orbit-fill automatically).
- [ ] **3D / side-depth selection view** (ex-`F8` 3D half). The flat side-view slice is done (→ `N08`);
  a true 3D selection view (monument point/block + cuboid Y) needs design. Later.
- [ ] **Comment hygiene sweep — the task ids.** Code comments must describe behaviour only, and the
  attribution half is done (B43 swept every "port of X.py" / "matches Python" reference out of `src/` and
  `tests/`). What remains is **implementation-phase and task ids** (`NS`, `N00`, `B8`, `P5`, `ND2`, …) in
  about nineteen places — a comment that says *when* something was built rather than what it does, which
  reads as noise to anyone who was not there. New code already follows the rule (`CLAUDE.md`).

**Deprioritized — may be dropped in a later pass.** Optional/deferred slices parked out of the active
long-tail so they stop competing with real work. Re-evaluate (or delete) when their area is next touched.

- [ ] **S10 — Auto-promote rectangles on Bézier (parked, optional).** Today S4 promotes via the inspector
  button / `P`; a rectangle keeps its 8-handle resize and has no Bézier affordance. If we ever want a
  rectangle's corner to sprout a Bézier handle that *implicitly* converts it to a polygon, it needs rect
  vertex/tangent handles in `sketch-edit-controller.js` (a UX decision on resize-handles vs vertex-handles).
  Low priority — explicit promotion already covers the need.
- [ ] **P8 — Pipeline re-run on config change (parked escape hatch, world-present only).** A
  parameterized re-scan honouring a bespoke `scan_layer`/`exclude_blocks` → re-detect islands → rewrite
  **layer-tagged** `layer.parquet` / `islands.json`. The per-map scan-layer + custom block-exclusion UI
  has been **removed** from both editors (detection is the fixed cleaned base; the world-scanning
  endpoints are gone), so there is no longer a config-change to honour from the UI — this remains only as
  a rare, local-only override path outside the hosted flow. (Island-exclusion →
  symmetry re-run already works without a re-scan, B7.)
- [ ] **P7 — [Deferred decision] Consolidate the layer extractors / scan passes.** **`ND2` settles the
  "consolidate vs keep" half: KEEP the exact per-layer extractors** — the World step uses them in distinct
  roles (cleaned `Base` = detection · `Surface` = visual aid · `Segments` = vertical), so they're a feature,
  not duplication; their per-layer default ignored-block sets (`Base` gets the expanded ND2 noise set;
  Surface/Y0 = air-only) are the solid-policy. Still open: the byte-parity sub-question — a segment-derived
  surface would **not** be byte-parity with the reference (endpoint-only runs also can't honour user
  `exclude_blocks`). Pairs with A4.
- [ ] **A7 — `Minecraft` is the last unfolded project: 47 files at its root.** Every other project has been
  sub-foldered by concern (`Pgm`, `Analysis`, `Data` — A5/A6); `Minecraft` grew to 61 files and 11k lines with
  47 of them flat at the root and only `Dressing/`, `Render/` and `Views/` broken out. The concerns are
  separable without touching a boundary and are visible from the filenames: **world reading** (Anvil regions,
  chunks, the block palette), **stamping** (rooms, cubes, objectives, houses and their frames), **painting**
  (the terrain painter and its profile/theme family), and the **suggesters** that read a built world back
  (monument, core, objective). Folders-only and behaviour-preserving, the same discipline as A5/A6: namespaces
  match folders, no file changes beyond the namespace line, build clean and every suite green before it lands.
  Map: `docs/project-structure.md` §3.

- [ ] **A8 — [Decision, parked] Should the layout generator be its own project?** `Pgm` holds two charters:
  the `map.xml` codec (48 files) and the layout generator (`Compose`/`Evaluate`/`Shapes`/`Derive`/`Plan`, 85
  files and 11.5k lines, touching no XML). The generator references only `Domain` and `Geom`, so
  `PgmStudio.Compose` would add **no dependency edge** — the split is free in graph terms, and it would make
  `Pgm`'s charter true again while making the generator's own dependencies enforceable (today it can reach the
  codec and nothing notices). Against it: a rename across every citation, and `PlanCompiler` — the plan →
  layout + intent seam — would sit on a project boundary rather than inside one. **The blocking question is
  whether `PlanCompiler` belongs to the generator or to authoring**, and that is answerable only when the
  generator next needs a structural change; doing it as a standalone refactor buys nothing today.
  See `docs/project-structure.md` §6.1.

- [ ] **A3 — Buildability endpoint perf (verify, then optimise if needed).** Per-cell NTS over the grid
  was flagged slow; the endpoint is now live and user-visible (`N03`'s buildability overlay landed).
  **First profile it under the Configure overlay** — only optimise (spatial index / batch) if it's
  actually slow in use; otherwise close.
- [ ] **A4 — [Consider, not perf] Vector-boolean island outlines (drop the rasterize→polygon round-trip).**
  Today island outlines come from a pixel round-trip: vector shapes → rasterize to cells → BFS → `BlocksToPolygon`
  (cells back to a polygon), done only to **avoid a C# polygon-boolean lib**. We
  already depend on NTS, so the sketch-finish island polygons *could* be computed by NTS vector boolean
  directly off the shapes (union adds, difference subs), dropping `BlocksToPolygon` + the BFS for the
  *polygon*. **Not a perf task** — the row-run fix already removed the hotspot, and the cell rasterize must
  still run for `layer_segment`/`layer.parquet` (Configure height side-view + analysis). Payoff is cleanliness
  + exact (smooth) outlines; cost is NTS boolean on the authoring path and a **staircase→smooth** outline
  divergence from scanned maps. Weigh before doing.
