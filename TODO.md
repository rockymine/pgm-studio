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

*(G41 was one monolith bundling four separable concerns; split into A–D so the footprint slice could ship
without the open-variant patterns. A + B shipped; C's open-variant **hub/frontline emission is now promoted
into the current focus** — split into **G88 (hub)** + **G89 (frontline)** below — because those two complete
the team unit (hub + spawn + wool + frontline) the switch fills; D (vacancy publishing) stays parked in
`BACKLOG.md`.)*

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

*(The G88/G89 pre-work and both team-unit box kinds shipped — G94/G95/G92/G93, the hub (G88) and frontline (G89)
boxes carrying the offer type (G96), and the **allocate→fill loop itself** (G63-C.1 filler + the C.2 core
allocator) — all in `FEATURES.md`. What remains of the arc is below: finish C.2's shape set, wire the composer
through it, and retire `TeamUnitGrower`.)*

*(G63 was one monolith — the whole box-driven switch. Split A–D: A shipped the partition data model + mirror,
B shipped the partition-first allocator seam + budget check (both `FEATURES.md`); C flips the switch (fill + wire
+ retire + re-baseline); D — the generic fragment, `GrowthOrder` strategies, and vacancy allocation — is parked
in `BACKLOG.md`.)*

- [~] **G63-C — [M4] The switch (fill the partition, retire the grower, re-baseline).** The allocate→fill loop is
  closed and landed — the gate-wired four-mouth `BoxFiller`, the wool/spawn arms on plan-cell fills, **C.1** (the
  hub-first `TeamUnitFiller`) and **C.2's core** (the `TeamUnitAllocator` placement plan, hub-form choice, seat
  logic, and seven wool shapes) — all recorded in `FEATURES.md`. **Remaining, roughly in order:**
  - **C.2 — the last two wool shapes + the spawn `L` (the immediate next slice).** The deeper `Z` (a compact add
    over the overhang path); the **scythe** (still gated from the production menu — its bay seals a flush dock
    against the host, WL8, so it needs the G80 **shape-relative bay docking**, not just sizing); the **spawn `L`**
    (the same seat-and-shift overhang, on the spawn box). Finishes the shape set → then G104–G106 below.
  - **C.2 — hub-form richness.** `L` **handedness** (its arm sits on a fixed side — choosing it would cover
    back+right as well as back+left); `Double-hole` needs a hub **wider than the current caps** (≥ 9); the vertical
    flip only orients the **z-frames** — `mirror_x` (front = a lateral edge) needs a rotation, not a flip; and an
    **L hub with a frontline** (today: front used ⇒ rectangle fallback) — a wide Bar frontline overlapping the L's
    short front leg is a good layout and wants the frontline to dock a branch hub's front foot.
  - **C.2 — hub-floor refinement.** The frontline / twin-recess / wool-c clearance floors the grower's `HubVFloor`
    encodes (today a simplified `w+2` floor).
  - **C.2 — CT1 / LN2 invariants by construction.** ≥10-block image clearance (orbit images stay separate
    islands) and the 50-block chain cap, baked into placement, failing the attempt if unmet (no repair loop).
  - **C.2 — wire `BoxPartitioner.Partition`** to return the allocation (+ the crossing `design` the frontline
    reach needs), replacing `BoxPartition.Of(grow)`.
  - **C.3 — wire `Composer`** through C.2→C.1 and **retire `TeamUnitGrower`'s authoring** (RNG re-keys, goldens
    churn). The mid consumes `FilledUnit.FrontlineFace` here; `MidCarver` stays — the mid is not a team-unit box,
    it is derived (`f(frontline)`).
  - **C.4 — re-baseline** gallery cases, **then** freeze the G32-D goldens.

  *Deliberately NOT next: the fill-to-`LandTargetCells` **directed repair** (retiring `SolveDepth`/`SolveWidth`/
  `spawnLen`) — it resizes shapes to hit the budget, which grows them further, the opposite of what the seeds
  need; parked in `BACKLOG.md` to be reconsidered as a targeted rule, not a solver.* Depends on G63-B, **G88,
  G89**. (review §4.2, §4.4, §4.5, §7.7)

*(The next-session arc, once the wool shape set is complete (Z/scythe above): with every shape now placeable,
stop adding and instead **consolidate, reality-check, and enrich** — clean the allocator, verify the placement
rules sit in the right layer, understand the budget, and grow the hubs. G104 → G106, roughly in order; the
G102 cleanup and the G103 rule-kind audit that opened the arc have landed — G103's five filed moves
(G107–G111) are in `BACKLOG.md`.)*

- [ ] **G106 — Fix the observed seat/emit failure modes (taxonomy doc §9).** The author-observed defect list,
  verified + quantified in `docs/map-generation-constraint-taxonomy.md` §9 (re-measure with
  `tools/compose/seat-probe.cs` — 4 presets × 200 seeds) — F1–F7,
  each with mechanism and fix direction: **F1** no inter-seat gap (neighbour lanes abut — up to 99/200 units
  on huge), **F2** lanes flush against a branch hub's legs at mass-adjacent run ends (the build-surface
  clearance law — measured low-volume, 23/3/1/1 units, all branch hubs; the naive `along + 2` form of the rule
  would refuse 30–50% of all docks, so it must bind at **non-corner** run ends only. The higher-volume adjacent
  mode §9 separates — a wool owning a whole hub side — is **ruled not a defect**: a forced small-board artifact,
  bad only where hub and lane combine into a flat slab with no bay/notch, and then a small-board frontline
  question, not a spacing law),
  **F3** the single frontline is a centred tiny-stub T and the form menu silently collapses to
  it at `w3` (the twin never fits), **F4** twin legs always equal (per-arm length + depth-aware face offers),
  **F5** square-on-square hub+frontline (reach doesn't scale — pairs with G104/G100), **F6** the donut sliver
  (the `woolAtEnd` half landed — corner-wool donuts are 8×5; what remains is the root: every dimension keys to
  the one map-wide lane width, so **per-piece width is the missing knob**, shared with G105. The preferred-aspect
  route is closed — the per-wool budget share, 4–44 cells, is below the donut's own 50-cell minimum everywhere,
  a finding for G104). *(F7, the clamp's over-deep void, is fixed and gone.)* F1/F2 are the spacing laws — G103
  named them and measured what F1's gap must be sized against (**G110**, WL7/WL2); F3–F5 are emitter/menu work.
  An entry leaves §9 when its fix lands.

- [ ] **G104 — Investigate the budget.** The two-currency budget (land vs build, §1.10) drives every box's
  size, but the seeds still under/over-fill and the fill-to-`LandTargetCells` repair is parked as
  counterproductive. Instrument the allocator's budget accounting (`flexible` / `woolShare` / the hub-cap
  ladder) and work out what the budget *should* produce per box kind at each team size — sensible hub / wool /
  frontline proportions — **before** wiring any repair. This is the input G105 needs (how much hub a budget
  warrants).

- [~] **G105 — Bigger, better hub shapes.** Landed (`FEATURES.md`): the hub now **elongates laterally** (a
  larger lateral cap than the depth cap — wider, not squarer, so the long edge gives seats room and reaches the
  width ≥ 9 the wide forms need), and **`P` + `Double-hole` joined the hub form menu** (the Double-hole's U made
  full-height to fit a shallow-wide hub), chosen for wide boxes. **Remaining:** the **per-piece width knob**
  (taxonomy §9 F6 — today "bigger" only stretches the whole shape `cw × factor`); the named target shape (author),
  the **asymmetric ring** — a `P`-class body whose dominant bar is also **wider** than the loop's own bars (e.g. a
  5×5 whose right bar is a 2-wide full-height slab: `vvvii / tttii / ttvii / tttii / tttii`) — a solid spine
  offering long free runs, the void a feature; raise the depth cap for richer big-team forms; and improve the
  **form → size fit** so a large budget reads as an interesting hub rather than a bland rectangle. Depends on
  **G104** (the budget decides how much hub is warranted).

