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

## Layout generation (G) — current focus: the box model (box per box, simplicity first)

Phase 2 of plan-then-realize. The consolidation base is in: M0 + M1 (G58/G59) and the evaluator engine
with its hard gate, soft-term foundation, frontline/rotation terms and editor wiring (the G60 foundation —
all in `FEATURES.md`). **The box model is now the focus**: G61 (M2, wool boxes in production) then G62
(slot recovery for the mirror), with G41 (hub/frontline patterns, M3) and G63 (partitioner, M4) queued in
`BACKLOG.md`. Box per box, with simplicity in mind — each box kind gets its **shape profile as data**
(what shapes a spawn can be: {I, L}, small boxes per SP; what a wool entry admits: the §4 width menu), and
the slot labels those emissions carry are what every later rule binds to
(`docs/contracts/map-generation.md` §5.3: the labels drive, the mirror only verifies).

The **G60 soft-term long tail is deliberately parked** in `BACKLOG.md`: discovering and labelling soft
rules for structures the composer cannot yet compose is the wrong stage — the crammed-frontline dead end
(G69) is the lesson — so rule discovery resumes as the box model gives each rule its vocabulary. The
G32-D seed goldens still freeze only **after G63** (M2–M4 re-key every seed's RNG). G50–G52 (the emitter
placement knobs) stay in `BACKLOG.md` until G61 makes them reachable from generation; their variant
grammar is already classifier-stable (`ShapeVariantTests`).

**Box model — M2 (current)**

- [ ] **G61 — [M2] Wool arms become wool boxes (first production caller of the emitter).** Inside
  `TeamUnitGrower`, replace the inline 1–3-segment wool-lane growth (its own I/L/Z grammar — the third
  shape implementation) with: partition the arm region into a `Box(Wool)` carrying a typed entry
  `BoxInterface` → `FillMenu` (interface-width → legal patterns; the §4 `w2/w4/w6` table as **data**,
  cited by rule id) → `WoolBoxEmitter` (thinned to a binding over `Shapes.ShapeEmitter`: terminal →
  `WoolRoom` role + wool marker). `FillResult` (`Ok(pieces, vacancies) | TooSmall(minBox) |
  NoFamilyFits`) replaces exception control flow, so a bad fit is a directed signal, not a 60-attempt
  re-roll; `Ok` already carries the fill's **vacancies** — its emit-side negative space (a U's bay, a
  donut's hole) as a `Vacancy` (kind bay/notch/hole + mouth `BoxInterface` + bounding walls; §4.4) —
  shaped from the start so the type doesn't churn even though *claiming* lands at M3. Emitter orientation
  via a rect transform (`Geom.Symmetry.Apply`) instead of the hardcoded top-edge mouth. Every emitted
  piece carries **structured ownership** — (box id, box kind, slot) on `GrownPiece`, rendered
  `wool-a/entry` (the id prefix is its serialization, not its source of truth) — and **every compose move
  after emission is label-preserving**: labels live through carve/cut/repair up to `Assemble`, the one
  boundary where they drop from the written plan (the evaluator receives them via `EvalContext` — G62/G68;
  a shape already attached to another shape is never re-read, the labels drive and the mirror only
  verifies). Kills the third
  shape impl; gives G44 its structural-spend vocabulary and makes G50–G52 reachable from generation.
  **Changes RNG consumption** (goldens re-key). Depends on G58. (review §4, §4.4, §7.4)
- [ ] **G62 — Slot recovery for the generated mirror (generated plans only).** `Shapes/SlotAssignment`:
  after `Classify` returns the family, template-match the slot sequence onto the classified pieces
  (`AssignSlots(family, pieces) → piece→slot map`) — matched on **path order and adjacency** (the entry is
  the piece on the mouth interface, the room is the terminal, run/bar/leg the chain between), never on
  canonical rect positions, so the G50–G52 variant geometries survive (their grids are the acceptance
  fixtures — `ShapeVariantTests`). Upgrades `emit-verify` to a true mirror (emit → classify → re-derive
  slots → compare); slot terms on composed plans get their slots from the emitter via `EvalContext`
  (`GrownPiece.Slot`), and the classifier's scope on a composed plan is the wool box itself (G61) — there
  is **no derive-side recovery of authored/traced plans**, retired by decision
  (`docs/wool-approach-read-investigation.md`: post-fragment maps carry family identity on the play
  surface; decoding finished maps is a trap). `WoolLaneShape` the class retires here (its lane measurable
  is already the `ClassifyOpen` read). Depends on G58, G59. (review §3.4, §7.5; the §3.5/§3.6
  full-plan-scoping half is retired)

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
