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
reusing what the previous one proved — wool boxes **shipped** (G61, `FEATURES.md`) → spawn (G78) → the
mirror's slot recovery (G62) → hub/frontline patterns (G41) → the partitioner switch (G63), where
`TeamUnitGrower` retires and sampling produces a `BoxPartition`. Each box kind gets its **shape profile as data** (what shapes a spawn can be:
{I, L}, small boxes per SP; what a wool entry admits: the §4 width menu), and the slot labels the
emissions carry are what every later rule binds to (`docs/contracts/map-generation.md` §5.3: the labels
drive, the mirror only verifies).

Deliberately **not** here: the G60 soft-rule long tail (parked in `BACKLOG.md` — discovering soft rules
for structures the composer can't yet compose is the wrong stage; the crammed-frontline dead end, G69, is
the lesson), G32's remaining realize subtracks (parked — elevation is one of the last pipeline steps),
(the emitter placement knobs G50–G52 shipped — `FEATURES.md`). The G32-D seed goldens freeze only
**after G63** — every milestone below re-keys the RNG, so earlier goldens would just re-break.

**Box model — M2 → M4**

- [ ] **G78 — Spawn boxes: the spawn emits through the shared emitter (the second box kind).** After G61,
  replace `TeamUnitGrower`'s hand-rolled spawn-lane geometry (its inline I/L growth with the spawn room as
  terminal) with a `Box(Spawn)` filled through the same machinery: a small `SpawnBoxEmitter` role binding
  over `Shapes.ShapeEmitter` (~20 lines — terminal → `spawn` role + marker) and the spawn's **shape
  profile as plain data** — families {I, L} only, small boxes (SP: ~10×10 direct, 10×20 with a run-up,
  20×20 for an L). This starts the per-kind profile table two rows early (wool, spawn) as data; the
  `FillProfiles` *type machinery* still lands at G41. No new shape machinery otherwise: the spawn is
  terminal-capped, so the same classifier, the same mirror, and G61's label invariant apply unchanged
  (`spawn-a/entry`…). **Changes RNG consumption** (goldens re-key — free before G63). Depends on G61.
  (review §4.3, §8 — resolves `SpawnBoxEmitter`'s "M2/M3" ambiguity into its own slice)
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
- [ ] **G79 — The corner law reads the mask, not the pair (un-gates the donut).**
  `TeamUnitGrower.ValidateContacts` rejects any pairwise `Corner` verdict, but a corner whose diagonal is
  bridged by a third piece is a ¾-solid inside corner of one connected mass — harmless, and the editor's
  `PC-C` lint already suppresses exactly that case (same land component). Replace the pairwise Corner
  rejection with the cell-level law: scan the composed mask for **diagonal pinch windows** (two tiles
  meeting only at a point with void or build zone on both opposite diagonals); ¾-solid corners pass.
  Narrow/Overlap stay pairwise-rejected. The donut's mask holds zero pinch windows, so this admits
  `Donut` to `FillMenu.ProductionFamilies` (changes RNG consumption — goldens re-key, free before G63).
- [ ] **G41 — [M3] Open-variant emission for frontline & hub (delivers L/Z compositions + HB4).** Today
  the hub is always one square and the authored L/Z frontline↔hub combinations aren't generated. Build the
  **open-variant** shape layer over the shared family machinery: `Compose/Boxes/FillPattern` (arrangements
  of family shapes in a box — the terminal-less / through-corridor read), `FillProfiles` (per-`BoxKind`
  legal patterns × families × binding, each restriction citing its `layout-rules.md` id), `BoxFiller` (the
  one profile-driven fill entry point). `FrontForm` retires into frontline patterns (none · single-chain
  I/Z · wide-face · twin-strands+recess — FR3/FR4/FR6/CT8); hub open patterns (solid I · L · Z ·
  ring-with-hole — HB1/HB3/HB4). **G39's** corner/edge interlock is expressed here as a `BoxInterface`
  constraint. Hub/frontline pattern pieces carry **slots with box-kind ownership** (`hub-a/bar`,
  `front-a/…`) — the first labels outside the wool box, extending G61's label-preservation invariant to
  every box kind. Fills start **publishing vacancies** (§4.4): boxes may overlap (piece-disjointness, not
  box-disjointness, is the invariant), so a fill's residual envelope is published as claimable negative
  space — a **U-hub publishes its bay**, a twin frontline its recess (the CT8 recess generalized). Emit-side
  and exact (families are fixed templates), so no derive pass finds them. `FillProfiles` gates claims
  (a spawn may claim a hub bay whose mouth faces away from the axis) — this is what makes the
  **spawn-in-hub-bay** layout expressible (three wools L/T/R + the spawn in the U's bay) instead of
  forcing the G45 parallel-lane anti-pattern. `emit-verify` grows per-kind pattern mirrors (twin → closure
  hole ringed by two strands; L hub → one-bend junction outline) — no new `*Shape` classes. Blocked partly
  on the author's frontline/hub teaching set. Depends on G61. (review §3.1, §4.3, §4.4, §7.6)
- [ ] **G80 — Docking modes as per-family data: the clamp's two entries, the scythe's entry edges.**
  Valid connections are **shape-relative, not box-relative** (an entry shift carries its dock with it),
  declared per family as enumerable docking modes (map-generation.md §4). **Clamp** (the authored,
  allowlisted-WL8 preset: wool deliberately clamped, its bay a deliberate hole granting two approaches, the
  fight rotating around it — *not* a published vacancy): today a fill satisfies exactly one entry through
  one interface, forcing rotation with the other entry dangling; production = **both entries satisfied
  along the short entry edge** — full short-edge host (closes the bay into a declared hole) or the
  corner-wrap dual host (bay stays open); wool-side docking illegal (stubs dangle). **Scythe**: standard =
  a host on the entry's **unoccupied edge parallel to the entry ↔ entry-run seam**; second = a wider host
  across the **combined colinear head edges of entry + entry-run**; a host touching the wool `room` is a
  **hard violation and rejects** (the declared-bay alternative is parked as G81 — elevation-stage only).
  Introduces **valid edges** on `BoxInterface` (long vs short; a wool-touching edge/corner never docks) and
  per-family multi-interface demands — more modes may follow now that they are expressible. Depends on
  G41's interface machinery.
- [ ] **G63 — [M4] Partitioner-first composition (the box-driven generation switch).** `Compose/Boxes/
  BoxPartition` (boxes + interfaces = a constraint graph) replaces the `Shape` sampling record as what
  sampling produces; `BoxPartitioner` (budget → partition, **directed repair** from `FillResult` instead
  of 60-attempt re-rolls). **Boxes may
  overlap** — the partition allocates budgets and constraints, not exclusive area (piece-disjointness +
  image clearance is the real invariant); the partitioner allocates later boxes **from published vacancies**
  (§4.4) as well as fresh space, so a bay-seated spawn docks up to three walls for free (`spawn-first`
  inverts it — the hub's fill must wrap a staked pocket). `GrowthOrder` named strategies (`spawn-first` /
  `hub-first` / `mid-out`) make the emission order an **experiment axis** judged by the evaluator + G43, not
  doctrine. `Box.LandTargetCells` gives the
  two-currency budget its per-box half, so **fragment** becomes a generic pass over the partition
  (`IsolationCut` + the mid's low target are its two existing special cases) — and a **label-inheriting**
  one: a piece the pass splits or converts hands its (box, slot) ownership to its products, so a build
  zone knows which slot it replaced (`wool-a/entry-run`) and the §5.3 per-slot cut law (a `run`/`bar` may
  split, an `entry`/`room` stays whole) is enforced *at the cut* against the label, never re-derived;
  `IsolationCut`'s connector extrusion (today born unlabeled) is labeled the same way. This is what lets
  fragmentation and connection mutation be driven off labeled pieces to the limit — the moves cite slots,
  the mirror only verifies. `TeamUnitGrower` retires.
  Re-baseline gallery cases; **then** freeze the G32-D goldens (per strategy). Depends on G61.
  (review §4.2, §4.4, §4.5, §7.7)
