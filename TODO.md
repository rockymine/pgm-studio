# pgm-studio — TODO (current focus)

The **Now & Next** board — only the *current focus theme* lives here. Everything not in the immediate
slice is in **`BACKLOG.md`** (the long tail); shipped capabilities are in **`FEATURES.md`** (the Done
column). The three move left → right: **`BACKLOG.md` → `TODO.md` → `FEATURES.md`**.

**Holds only open work:** `[ ]` to-do, `[~]` in progress — **never `[x]`.** When a task ships, a commit
lands (its message references the id), the task **leaves this file**, and a line is added to `FEATURES.md`.
Board rules live in `CLAUDE.md` (§ "Status & task board").

**The building programme has drained.** A room's building and a dressed building are the same thing — a
footprint and a shell — and the first is a special case of the second (`WE71`); what that shape made possible
is now built. The ground under a room is the board's ground and a room can state its own (`B145`), a plan
piece is picked and the height it was compiled at corrected on the canvas (`B107`), and a picked piece drags,
the move written back to the intent so the picture and the world build cannot diverge (`S25b`). All three are
in `FEATURES.md`. Anything found while working goes to `BACKLOG.md`.

**A building's ceiling stays two numbers, for now (author).** A dressed prop is capped at 192 covered cells
(`HP3`) and a room's building at 20×20 by `ST9`; they measure the same concept since `WE71`, and holding them
apart is a deliberate not-yet rather than an oversight.

**Three numbers are the author's and are not to be re-derived.** A protection region is at most **20×30**
blocks (`ST10`), a building footprint at most **20×20** (`ST9`), and the smallest room with no building over
it is **4×4** (`WX2`).

**What is left on this board is the second programme (author, 2026-09-02): the boards the driver authored,
and what it had to work around.** It was pulled up whole from `BACKLOG.md` and rode beside the building one;
it is now the focus alone. Its ground has moved since it was pulled — the reads a model subtracts from landed
(`WS19`–`WS22`, `TS81`, `RP64`, `FEATURES.md`), so two of its entries are the join that is left rather than
the whole read.

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

- [ ] **B171 — Document how a wool approach attaches to a hub, in the shapes endpoint's terms.** An agent
  placing one **reads `GET /shapes/catalog`** for the valid base shapes and how each attaches, and authors from
  that; it does not run the generator (author). Reach rather than capability, and upstream of `PL13`: a dock
  seated against the hub has a face to wall that is not its own door, so getting the attachment right removes
  the wall fault as a side effect.

  *author, 2026-08-14 · Weirgate's `dock-w` touches only `front` and `lane-w`; `hub` is a lane away, and the
  dock's south edge sits flush on the build region's northern line at `z −20`.*
