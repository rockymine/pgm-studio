# pgm-studio — TODO (current focus)

The **Now & Next** board — only the *current focus theme* lives here. Everything not in the immediate
slice is in **`BACKLOG.md`** (the long tail); shipped capabilities are in **`FEATURES.md`** (the Done
column). The three move left → right: **`BACKLOG.md` → `TODO.md` → `FEATURES.md`**.

**Holds only open work:** `[ ]` to-do, `[~]` in progress — **never `[x]`.** When a task ships, a commit
lands (its message references the id), the task **leaves this file**, and a line is added to `FEATURES.md`.
When this board drains, pull the next theme up from `BACKLOG.md`. Board rules live in `CLAUDE.md`
(§ "Status & task board").

Task ids are a section letter + number (`S13`, `B10`, `G15`) — **globally unique and stable** across all
three files. Moving a task between files never changes its id; never renumber or reuse.

## Layout generation (G) — current focus: box / deriver / evaluator consolidation

Phase 2 of plan-then-realize. The rule-based composer works and ships seeds, but it is **mid-migration**
and the debt converges exactly where the next features land: `WoolBoxEmitter` is fully built yet has **no
production caller** (only tools + tests invoke it); the board deriver's valuable raster half (~460 lines:
islands, zone kinds, hole classes, mid form) lives in a run-by-hand tool script the evaluator can never
reference; the shape vocabulary exists **~4 times** (emitter families, classifier families, the grower's
inline lane growth, plus stringly-typed `WoolLaneShape`) bridged by a `ToString()` comparison; and the
raster substrate (flood fill, components, enclosed voids, reflex corners) is hand-rolled in **5 sites**.
Full analysis: `docs/map-generation-architecture-review.md`.

We pay this down **refactor-first**, before the interface / hub / lane features and **before the G32-D
goldens freeze** — because the box-model milestones (M2–M4) re-key every seed's RNG, so goldens frozen
first would just re-break, and the consolidation makes every later feature cheaper. The batch below is the
evaluator's soft half (G60, on the landed foundation); output only begins to shift once its soft terms +
lowest-scoring-attempt land, and the seed goldens do not freeze until G63. The interface / hub / lane feature
long-tail and the box milestones M2–M4 (G61 / G62 / G41 / G63) are parked in `BACKLOG.md`, reworded to be
delivered *through* the box model rather than against the current grower.

**M0 + M1 landed (G58, G59, `FEATURES.md`):** the `Geom.Cells` substrate + one `ShapeFamily` enum +
`Shapes/ShapeClassifier` + `LaneRead`/`ClassifyOpen` (M0); and the board deriver in `src` —
`Derive/BoardDeriver.Derive → BoardStructure` (the gallery is render-only over it) + `Plan/PlanDerived` →
`Derive/ContactGraph` (M1). `derive-gallery` byte-identical throughout. The evaluator (G60) can now derive the
board as a library call. One deferred slice — the `FannedGraph` ↔ `ContactGraph` surface-overlap reconcile — is
**G65** in `BACKLOG.md`.

Shipped so far (`FEATURES.md`): closure/envelope + team-unit grower, the CT1 mid band, centre islands
(CT11), the MD6 stone grid, the wide frontline (FR6), isolation cuts, BZ6–BZ9 discipline, the spawn +
wool-room dock (G49), the **box-based wool-approach shape vocabulary + classifier/emitter/deriver**
(G53/G54), the authoring lever (G46–G48), the **M0 shape substrate + family-enum consolidation** (G58), the
**M1 board deriver in `src`** (G59), and the **evaluator engine + `Acceptable` dissolve** (G60 foundation).

**Consolidation — the refactor-first batch (current)**

**G60 foundation landed (`FEATURES.md`):** the `Pgm/Evaluate/` engine — `LayoutEvaluator.Evaluate`/`Gate`,
`ILayoutTerm`, `EvalContext` (lazy `BoardStructure`), `EvaluationProfile`, `SeedEnvelopes` + the `Band` distance
convention — and the **dissolved `Composer.Acceptable`**: seven hard terms (STRUCT, WL2/PC-C/G2, G5, BZ6, WL8) +
the opt-in reject sink, composed output **byte-identical**. The soft half + wiring remain below.

**G60 (2a) landed (`FEATURES.md`):** the soft half — `SoftTerm` (pure `Value` + drawn evidence),
`tools/deriver/envelope-stats.cs` → embedded `seed-envelopes.json` (+ generated `docs/seed-envelopes.md`), and a
first soft-term batch (`fill-ratio`/G8, `island-count`/CT1, `max-chain-length`/LN2, `wool-wool-distance`/WL7,
`spawn-wool-distance`/WL2), scored as `Band` distance. **Distances are rectilinear surface traversal**
(`Geom.Cells.ShortestPath`, 4-connected — routes around voids, no corner-cut), not straight-line; **WL2 moved to
a surface `SpawnWoolFloor` gate term** (byte-identical — WL2 never gates), retired from `PlanValidator`.

**G60 (2b) landed (`FEATURES.md`):** catalogue growth + the traced teaching corpus. Six more soft terms —
`lane-width`/LN1 (narrowest wool lane, the goat-path guard) and `enclosed-void-count`/CT8, plus the team-scale
CT split that **retires the blunt `island-count`**: `neutral-stepping-count`+`team-stepping-count`/CT4 (contested
mid stones vs captive movement stones), `band-count`/CT1 (front-front crossings), `isolation-cut-count`/CT5
(intra/self team-side cuts) — team-owned counts normalized ÷ orbit order. `envelope-stats` now folds the traced
real maps (`tools/seeds/traced`, 11 of 12; `3084` held — wools don't attribute); a `SoftTerm.LearnsFromTraced`
opt-out holds `max-chain-length` to authored intent so the traced long-chain maps can't widen it. Byte-identical.

**G60 (2c) landed (`FEATURES.md`):** frontline runs — the deriver groups frontline segments into per-team faces
(`BoardStructure.FrontlineRuns`: team, width, straight/offset profile), and `frontline-count`/FR4 +
`frontline-width`/FR6 score them. Profile is derived but not scored (both styles authored-valid). Byte-identical.

**G60 (2d) landed (`FEATURES.md`):** the rotation half of the frontline composite — `uncrossed-middle-void`/CT9
(a contested middle void with no crossing route is the "no rotation" defect; band `[0,0]`, so any is punished),
plus a contained `Band` `[0,0]`-floor fix (a zero-tolerance band scores O(1), not ~1e9). Calibrated on six
authored teaching seeds (`tools/seeds/teaching/`: 3 crammed/over-stretched negatives + 3 rotation resolutions).
The **cramming** half is parked on **G69**: no `BoardStructure` signal separates it from good real maps (crammed
≡ acapulco across 7 features), and the deriver reads the geometry opposite to the author's visual.

- [~] **G60 — Composer evaluator: finish the catalogue, wiring, harness.** On the landed catalogue:
  **(1)** finish the §6 soft-term catalogue — the rotation half landed (2d); **remaining**: the cramming
  punishment is parked on **G69** (the deriver has no offset-team-mass primitive); approach count/WL8·G45
  (conditional — gated on G62 slots); height/EL1·EL4 (blocked on the G32-C elevation pass). Each reads
  `BoardStructure` measurables only (never a family name — the enumeration trap), scored as `Band` distance with
  `Evidence`. **(2)** the
  hole-hunt loop keeps the
  **lowest-scoring** acceptable attempt — the first point composed output shifts, its own re-baseline. **(3)
  Wiring** — `EvaluationDto` in `Contracts` (carrying the `Evidence` primitives) + `POST /api/plan/evaluate` for
  the editor's live score/lint; the client draws violations **and their evidence** as a JS canvas overlay (§9.7;
  this also restores WL2 to the editor). **(4) Ranking harness** `eval-rank.cs` + minimal-pair negatives
  (`tools/seeds/negatives/` + `labels.json`): `Score(negative) > Score(positive)` **and** the labelled term
  fires; per-term tests. (review §5, §9; `docs/contracts/layout-evaluator.md`)

**Realize & gate (plan → loadable, validated seed)**
- [~] **G32 — Composer realize + gates.** Skeleton landed (`FEATURES.md`); the `spawn` / `wool-room` piece
  roles now land too (G49, `FEATURES.md`). Remaining: **G32-C markers/heights/walls** — SP3/SP4 spawn
  (facing absolute, raised), SP7 iron, WL5 stepped approach climb, EL1 palette (base 9, step 2, all-odd),
  ST4 walls, EL6 (the rooms are flat at the base surface — the elevation pass raises them). **G32-C is a
  second generator, not a checklist line** (review §11.3): the authored feel is substantially elevation
  (~⅓ of the complex seeds are stair treads; every wool tops a climb), so the elevation pass wants its own
  pattern vocabulary — a staircase chain (n treads × step 2 climbing an interface) is a §4.3-style pattern.
  If generated maps ever read valid-but-flat, this is where the missing soul is. **G32-D gates
  + goldens + emit** — `PlanValidator` zero-errors with zones present, `FannedGraph` full traversability,
  stat envelopes vs `seed-stats.md`, `plan.json` loadable in `/plan`, fixed-RNG goldens under `tests/`.
  **G32-D goldens freeze only after G63 (M4):** the box migration (G61/G63) changes RNG consumption and
  would re-break any goldens frozen earlier — G32-C is independent and can proceed now. p5/rot_90 stays a
  known limitation until **G35**.
