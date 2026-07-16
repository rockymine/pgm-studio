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

## Layout generation (G) — current focus: box-driven map generation (box per box, simplicity first)

Phase 2 of plan-then-realize. The consolidation base is in: M0 + M1 (G58/G59) and the evaluator engine
with its hard gate, soft-term foundation, frontline/rotation terms and editor wiring (the G60 foundation —
all in `FEATURES.md`). **This board is the box model, end to end**: one box kind at a time, each step
reusing what the previous one proved — wool boxes **shipped** (G61), the mask-level corner law **shipped**
(G79 — its pinch scan `Cells.HasDiagonalPinch` is the primitive the docking work reuses), the mirror's
slot recovery **shipped** (G62 — `SlotAssignment`), and the spawn box **shipped** (G78 — `SpawnBoxEmitter`,
the second box kind, the profile-driven fill spine **shipped** (G41-A part 1 — `FillProfiles` + `BoxFiller`,
the lever box footprint enforcement turns on), the interface data model **shipped** (G41-B —
`BoxInterfaces`, the valid-edges facts), the docking gate **shipped** (G80 — `DockingGate`, the declarative
slot-edge table the partitioner produces legal docks against), and the partition constraint-graph **shipped**
(G63-A — `BoxPartition` + the `Of` derive mirror), and the partition-first allocator seam **shipped** (G63-B —
`BoxPartitioner` (`budget → BoxPartition`) + the two-currency budget check, parallel to the grower; all in
`FEATURES.md`) → **the switch (G63-C) is next**, where sampling fills the allocated partition through `BoxFiller`,
wires `DockingGate`, retires `TeamUnitGrower` and re-baselines. Each box
kind gets its **shape profile as data** (what shapes a spawn can be:
{I, L}, small boxes per SP; what a wool entry admits: the §4 width menu), and the slot labels the
emissions carry are what every later rule binds to (`docs/contracts/map-generation.md` §5.3: the labels
drive, the mirror only verifies).

Deliberately **not** here: the G60 soft-rule long tail (parked in `BACKLOG.md` — discovering soft rules
for structures the composer can't yet compose is the wrong stage; the crammed-frontline dead end, G69, is
the lesson), G32's remaining realize subtracks (parked — elevation is one of the last pipeline steps),
(the emitter placement knobs G50–G52 shipped — `FEATURES.md`). The G32-D seed goldens freeze only
**after G63** — every milestone below re-keys the RNG, so earlier goldens would just re-break.

**Box model — M2 → M4**

*(G41 was one monolith bundling four separable concerns; split into A–D so the footprint slice can ship
without the open-variant patterns. A + B are the near-term tracks below; C + D — the open-variant hub/
frontline emission and vacancy publishing — are parked in `BACKLOG.md`, blocked on the author's teaching set.)*

- [~] **G41-A — [M3] Route the production arms through `BoxFiller` (part 2 — closes with G63).** Part 1
  shipped (`FEATURES.md`): `FillProfiles` (the per-`BoxKind` profile as a type) + `BoxFiller` (the one
  profile-gated fill entry point over a positioned `Box`, with land-vs-`Box.LandTargetCells` accounting), and
  the wool menu reads `FillProfiles` (byte-identical). **Remaining:** the production wool + spawn arms still
  size their own boxes with the bespoke `SolveDepth`/`SolveWidth`/`spawnLen` solvers and place via `PlaceArm`/
  `PlaceSpawn`'s host-window and (u,v)-frame logic — they do **not** yet route through `BoxFiller`, because
  that needs the arm's box **Rect allocated first** (position + dims), which is exactly the partition-first
  inversion. So retiring the bespoke sizing + the **intra-box fragment** that fills to the land target
  (convert land→build inside the box per the §5.3 slot cut law) land together with **G63-C**'s box-Rect
  allocation — `BoxFiller` is the filler that switch drives. Depends on G63-C. (review §4.1, §8)

*(G63 was one monolith — the whole box-driven switch. Split A–D: A shipped the partition data model + mirror,
B shipped the partition-first allocator seam + budget check (both `FEATURES.md`); C flips the switch (fill + wire
+ retire + re-baseline); D — the generic fragment, `GrowthOrder` strategies, and vacancy allocation — is parked
in `BACKLOG.md`.)*

- [~] **G63-C — [M4] The switch (fill the partition, retire the grower, re-baseline).** *Landed, organ by organ:
  (1) the docking gate is wired into `BoxFiller` — `DockingGate.CheckMouth` over the box's edges (`BoxInterfaces.Of`)
  makes the filler **produce only legal docks**, an illegal one a directed `FillResult.IllegalDock`. (1a) `BoxFiller`
  fills all **four mouths** (Left/Right via a quarter-turn of the mouth-up shape — rects, marker, vacancies). (1b)
  the **wool arm routes through the plan-cell `BoxFiller`** (`FillArm` retires `PlaceArm`): the box footprint is
  allocated first and the fill sized to it (**footprint an input**), seated by its mouth row in the host window; the
  wool arm's RNG re-keys (first churn — suite still green: tests gate invariants/authored seeds, composer goldens
  freeze after G63). (1c) the shared four-mouth orientation is extracted to `MouthOrient`, and the **spawn arm routes
  through the plan-cell fill** too (`FillSpawn`; `SpawnBoxEmitter.Fill` takes a plan-cell `Box`+mouth), the entry run
  re-pinned for the wool-on-spawn dock — byte-identical for I / L-dir≥0 spawns.* **Remaining (the switch's own
  spine):** the `Composer` routes through the partitioner, **`TeamUnitGrower` retires**, the clamp's **dual-host
  corner-wrap** lands, re-baseline gallery cases, **then** freeze the G32-D goldens. *Deliberately NOT next: the
  fill-to-`LandTargetCells` **directed repair** (retiring `SolveDepth`/`SolveWidth`/`spawnLen`) — it resizes shapes
  to hit the budget, which grows them further, the opposite of what the seeds need; parked in `BACKLOG.md` to be
  reconsidered as a targeted rule, not a solver.* The **hub/frontline** open boxes are their own track (G41-C).
  Depends on G63-B. (review §4.2, §4.4, §4.5, §7.7)

**Shape honesty (the box shapes are too large — keep them small, spend the budget on count) — current focus**

*(The porting kept the old grower's "grow the shape to absorb its budget share" sizing, so the spawn/wool shapes
stretch with player count — a spawn can reach ~100 blocks when the docs say a spawn is small, ≤20. The fix is
size **rules** over the box model, not a resize solver. Visual: the sizing explainer artifact.)*

- [ ] **G85 — Flexible spawn attachment.** The spawn always docks the hub's **back** edge (`FillSpawn` picks Top/Right
  by frame). The plan-cell fill already docks any edge (the wool arm proves it), so let the spawn attach at varied
  points/edges like the wools — a sampled host + mouth, not a fixed one. (SP2/SP3; depends on G84 landing first so the
  small box is what moves.)
