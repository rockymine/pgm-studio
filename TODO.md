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

**A second programme rides beside the first (author, 2026-09-02): the boards the driver authored, and what
it had to work around.** The group below is pulled up whole from `BACKLOG.md`. Its ground has just moved —
the reads a model subtracts from landed (`WS19`–`WS22`, `TS81`, `RP64`, `FEATURES.md`), so two of its
entries are the join that is left rather than the whole read. Nothing in it touches the building programme.

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

## Mapgen authoring tasks

Multiple boards were authored through `pgm-studio-mapgen/tools/drive.py`, and the driver is the record of what
the studio does not answer: a statement an author has to make with no field to make it in, and a question
whose answer is inside a refusal, a solver or a palette and which no read returns — or one a read does answer
in a field no driver printed. Not one of these faults was caught by a gate; the export gate was open, the
mirror clean and the traversability whole on every board named below.

- [ ] **TN2 — `structural-integrity` carries one sentence where several refusals fired.** The term folds
  every `PlanValidator` refusal into one hard violation, and where there is more than one its message is
  `"{n} structural errors ({first})"` — so `/plan/evaluate` tells an agent the count and one of them, and the
  other n−1 arrive only at the compile's 422 a stage later. The evidence rides already: `SubjectIds` is the
  union over all of them, so the count and the subjects are right and only the sentences are dropped. Carry
  them — a violation already wraps a `Finding`, so either the term answers the list or the DTO gains the rest
  beside the one it names. `StructuralTerms.cs:22-32`.

- [ ] **B150 — Nothing evaluates a map's own sketch, so `G8` can only ever score the plan.** The term now
  measures ground over the ground's own frame (`FEATURES.md`), which is the whole of the author's rule that a
  plan can answer — but a plan is not where a board's ground is. The sketch's organic `add` shapes push the
  coast past the plan pieces and its `subtract` cuts the holes, and the evaluator never sees either: all three
  callers are plan-tier by construction — `POST /plan/evaluate` takes a bare plan body, `ComposeEndpoints`
  scores a freshly generated one, and the hunt loop's `Gate` runs hard terms only. **The missing thing is a
  route that evaluates a stored map**, reading its `sketch_layout_json` and rasterizing it, so every soft term
  scores the board that exists rather than the sketch of it. `SketchRasterizer.Rasterize` already answers the
  footprint; what has no home is the context that would carry it.

  *measured on `basalt-reach`: the plan is five pieces tiling edge to edge with no hole at all — 522 filled
  cells over a 30×21 bbox, **G8 = 0.829** — while its sketch carries eleven shapes, ten `add` and one
  `subtract`. The plan's bbox is 150 × 105 blocks; the built world is 150 × 204, with 23,417 ground columns
  (0.77 of its own frame) and a void hole through the lower middle.*

- [ ] **B262 — The read-backs have no browser surface, and neither do the ones already taken.**
  `render/topdown`, `surface`, `walk`, `mirror`, `section`, `structures`, `traversability` and `heightmap`
  answer a picture each over HTTP and are fetched by nothing in the client. `docs/world-scan/read-backs.md`
  never claimed a UI, so this is a gap rather than drift — but reviewing what a board looks like is the loop
  the paint work runs on, and today it runs at in-game speed. A page per map, live off the routes.

  **The larger half is that the pictures already exist.** `pgm-studio-mapgen`'s `tools/drive.py` takes all
  eleven world reads over HTTP after every build and writes them beside the documents: 64 renders a map in
  `specs/<name>/renders/`, a `world-surface.png` per board and a `theme-*-surface.png` per theme — which is
  the palette read `WE41` is parked on — and a `world-layer-*.png` per storey where the board is stacked.
  Fifty-odd boards' worth of provenance-backed pictures nobody can see side by side. So the second surface is
  a **contact sheet over a renders directory**: one row per map, one column per view, the view pickable, at a
  size where a whole run is judged in one screen. That is what makes a preference pass over the built boards
  affordable, and it needs no new render.

- [ ] **B265 — A disk read cannot be given the provenance sidecar, and this repo's worlds never carry one.**
  `TopDownRender.Run(regionDir, …)` finds provenance only by `WorldProvenanceFile.TryRead(regionDir)`, and
  `drive.py` deliberately moves `provenance.json` out to `specs/<name>/` because `maps/<name>/` is uploaded to
  the PGM server and holds only `region/`, `map.xml` and `level.dat`. So every render taken off a shipped
  world after the fact degrades to the material estimate — correctly labelled in the legend (`B133`), and on a
  painted board wrong enough to read terrain as structure across half the map. The HTTP routes are unaffected;
  they build the world and hold `Built.Provenance`. Wants `--provenance <path>` on the reads that take a
  region directory, so a sidecar kept beside the documents can be pointed at.

- [ ] **TS30 — Teach an author to deform a compiled shape, rather than to restate its coast.** The compiler
  emits a staircase of the plan's rectangles, and **a sketch outline disagreeing with the plan outline is the
  point** (author): the plan is a rough model, and the coast is the sketch's to state. What is missing is not
  a way to keep them in step but a way to *move* — `opus5-ravensmere` redrew its ring by hand and
  `opus5-thornfell` bent the compiled ring in forty-two lines of Python, both because nothing in the tool or
  the API says how a compiled polygon is made organic. `PgmStudio.Geom.RingRounding` fits the Catmull-Rom
  handles the driver re-derived and has no caller in `src`, only a test.

  **Extremely cautious about a *stated* bow at compile** (author) — that would put the coast back in the plan.
  The wanted thing is a sketch-side operation an author or an agent reaches for on a shape that is already
  there, and two rules keep it safe: the plan's own vertices never move, and nothing ever moves outward.

  *`opus5-thornfell`: 36 compiled vertices → 99 drawn, strait measured at **26–28 blocks** over 23 transects
  against a plan stating a flat 30. A vertex moved outward would have closed it.*

- [ ] **WE34 — Nothing runs a placement rule forwards; the raster answers only what is free.**
  `sketch/dressing`'s `claims` raster answers which cells nothing holds and no keep-out covers (`TS81`),
  which is where to **try** — but a free cell is not a legal seat. `DR-SITE`, `DR-SLOPE`, `DR-CLAIM` and
  `DR-ROAD` read the prop's own footprint rather than the cell, so the verdict still arrives as a decline.
  Run the same predicates forwards: one read taking a footprint and a prop kind and answering the candidate
  cells. Every predicate is already written. The *shape* of the answer exists three times for goals —
  `monument-suggestions`, `core-suggestions`, `wool-suggestions` — but each scores scanned candidate rows
  rather than asking a placement rule, so this is the first read that runs the rules themselves.
  `docs/world-export/decoration.md`.

  *`opus5-rimegarth` took **fourteen declines** on one pass, and the honest answer for a hundred-block CTW
  board turned out to be **nine legal spots**. `tools/loop.py --candidates` is the trial loop this replaces.*

- [ ] **WS14 — A declared route is never read back.** `route: true` on a stroke makes `DR-ROAD` measure
  every other prop's standoff to it, and nothing reads the stroke itself. Two of the three pieces landed:
  `transect` walks any polyline and classes every step (`WS19`), and the dressing preview answers a stroke's
  `covered` cells. What is missing is the join — a read that resolves a stroke id to the cells the pass
  actually **laid**, which its style, coverage and seed decide and its points do not, and walks those end to
  end for the worst step, the material run and where the road leaves the ground. `GET …/walk` answers a
  journey the walker chooses, which is not the one that was drawn. `docs/world-scan/read-backs.md`.

  *`opus5-thornfell`'s four tracks, **308 cells**, were swept for a step over one by a throwaway column
  script; a transect along the drawn points reads the spine rather than the road.*

- [ ] **B171 — Document how a wool approach attaches to a hub, in the shapes endpoint's terms.** An agent
  placing one **reads `GET /shapes/catalog`** for the valid base shapes and how each attaches, and authors from
  that; it does not run the generator (author). Reach rather than capability, and upstream of `PL13`: a dock
  seated against the hub has a face to wall that is not its own door, so getting the attachment right removes
  the wall fault as a side effect.

  *author, 2026-08-14 · Weirgate's `dock-w` touches only `front` and `lane-w`; `hub` is a lane away, and the
  dock's south edge sits flush on the build region's northern line at `z −20`.*
