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
first would just re-break, and the consolidation makes every later feature cheaper. The batch below is
pure refactor + the evaluator foundation (G58 → G59 → G60); it changes no generated output until G61. The
interface / hub / lane feature long-tail and the box milestones M2–M4 (G61 / G62 / G41 / G63) are parked
in `BACKLOG.md`, reworded to be delivered *through* the box model rather than against the current grower.

Shipped so far (`FEATURES.md`): closure/envelope + team-unit grower, the CT1 mid band, centre islands
(CT11), the MD6 stone grid, the wide frontline (FR6), isolation cuts, BZ6–BZ9 discipline, the spawn +
wool-room dock (G49), the **box-based wool-approach shape vocabulary + classifier/emitter/deriver**
(G53/G54), and the authoring lever (G46–G48).

**Consolidation — the refactor-first batch (current)**
- [ ] **G58 — [M0] Shape substrate + one family enum (pure refactor, zero output change).** New
  `PgmStudio.Geom/Cells.cs` for the substrate hand-rolled in 5 sites: N4 neighbours · flood fill ·
  connected components · enclosed-void detection · reflex-corner count · bays · bounding-box ·
  min-run-width. Merge `ApproachFamily` (Compose, 8) + `ApproachShape` (Plan, 9 incl. `Isolated`) into
  one `ShapeFamily` enum so the emit↔derive mirror is `derived == requested` on one type, not a
  `ToString()` bridge. `WoolApproachShape` dissolves into `Shapes/ShapeClassifier` taking **terminal**
  cells (nothing in it is wool-specific); `WoolLaneShape`'s string result → a `LaneRead` enum
  (`ClassifyOpen`). Kill the dead `laneWidth` param; fix stale doc refs (`WoolBoxEmitterTests` §2,
  `ApproachSlots` xmldoc). Port the three mirror harnesses (`shapes-gen`/`emit-verify`/`stress-shapes`)
  from `tools/` → TUnit. Acceptance: `derive-gallery` output **byte-identical** over the base seeds +
  generated cases. (review §3, §7.1)
- [ ] **G59 — [M1] Board deriver into `src`.** Extract the raster-layer `Derive()` (~460 lines run-by-hand
  in `tools/deriver/derive-gallery.cs`) into `Pgm/Derive/BoardDeriver.Derive(plan) → BoardStructure`
  (islands + anchor roles, stepping-stone kinds, intra/self bridges, zone kinds + widths, hole classes +
  parallel-ways, wool lanes, mid form). Rename `Plan/PlanDerived` → `Derive/ContactGraph` (rect layer:
  contacts, interfaces, gap links, build regions, frontline edges, components). Gallery becomes
  render-only over `BoardStructure`. Reconcile `ClosureAnalysis` (a query over the raster layer, or a
  documented fast-path twin — measure first, it runs in the 60-attempt hunt loop); unify `FannedGraph`'s
  private adjacency predicates onto `ContactGraph` and settle the different-surface-overlap disagreement
  (review 2.3 / 6.5). Unblocks the evaluator (G60) + the conformance sweep (G43) as library calls.
  Acceptance: byte-identical gallery output; doc §1.3/§6.2 names the class, not the script. Depends on
  G58. (review §2, §7.2)
- [ ] **G60 — Composer evaluator engine.** `Pgm/Evaluate/`:
  `LayoutEvaluator.Evaluate(plan | EvalContext, profile) → Evaluation`, where
  `Score = Σ hard-penalties + Σ w·envelope-distance` (lower is better; 0 = perfect). `ILayoutTerm` — one
  per `layout-rules.md` id, reading derived measurables only, never family names (the enumeration-trap
  rule). `EvalContext` (Plan + `ContactGraph` + `BoardStructure` + `SeedEnvelopes`, derived once).
  `EvaluationProfile` (per-term enabled + weight — the criteria on/off switch: composer gate, editor
  lint, and sweep each run a profile). `SeedEnvelopes` from a **generated** `seed-envelopes.json`
  (`tools/deriver/envelope-stats.cs` over `tools/seeds/`; global bands first, split by symmetry mode only
  where the ranking harness proves a global band mis-ranks; also regenerates `docs/seed-stats.md`).
  **Dissolve `Composer.Acceptable`** into a hard-terms-only **short-circuit** gate + a reject log
  (`{seed,request,attempt,stage,termId,ruleId,subjects}`, RNG-reproducible); the hole-hunt loop keeps the
  **lowest-scoring** acceptable attempt. `EvaluationDto` in `Contracts` + `POST /api/plan/evaluate` for
  the editor's live score/lint (findings render like `PlanValidator`'s). Ranking harness `eval-rank.cs` +
  minimal-pair negatives (`tools/seeds/negatives/` + `labels.json`): assert
  `Score(negative) > Score(positive)` **and** the labelled term fires. Per-term TUnit tests. Depends on
  G59. (review §5, §9)

**Realize & gate (plan → loadable, validated seed)**
- [~] **G32 — Composer realize + gates.** Skeleton landed (`FEATURES.md`); the `spawn` / `wool-room` piece
  roles now land too (G49, `FEATURES.md`). Remaining: **G32-C markers/heights/walls** — SP3/SP4 spawn
  (facing absolute, raised), SP7 iron, WL5 stepped approach climb, EL1 palette (base 9, step 2, all-odd),
  ST4 walls, EL6 (the rooms are flat at the base surface — the elevation pass raises them). **G32-D gates
  + goldens + emit** — `PlanValidator` zero-errors with zones present, `FannedGraph` full traversability,
  stat envelopes vs `seed-stats.md`, `plan.json` loadable in `/plan`, fixed-RNG goldens under `tests/`.
  **G32-D goldens freeze only after G63 (M4):** the box migration (G61/G63) changes RNG consumption and
  would re-break any goldens frozen earlier — G32-C is independent and can proceed now. p5/rot_90 stays a
  known limitation until **G35**.
