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
the second box kind, all in `FEATURES.md`) → **the profile-driven fill spine (G41-A) is next** — it makes
box footprint an enforced, data-driven quantity (`FillProfiles` + `BoxFiller`), the lever the rest turns on
→ the interface data model (G41-B) → docking modes (G80) → the partitioner switch (G63), where
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

*(G41 was one monolith bundling four separable concerns; split into A–D so the footprint slice can ship
without the open-variant patterns. A + B are the near-term tracks below; C + D — the open-variant hub/
frontline emission and vacancy publishing — are parked in `BACKLOG.md`, blocked on the author's teaching set.)*

- [ ] **G41-A — [M3] `FillProfiles` + `BoxFiller`: the profile-driven fill spine (box footprint enforced).**
  The per-`BoxKind` profile becomes a **type**, not just the two data rows G61/G78 landed: `Compose/Boxes/
  FillProfiles` maps `BoxKind` → its legal families + its **footprint/land-target policy** (each restriction
  citing a `layout-rules.md` id), and `Compose/Boxes/BoxFiller` is the **one profile-driven fill entry point** —
  given a `Box` (its footprint `Rect` + `Box.LandTargetCells`) it validates/picks the family against the
  profile and fills to the land target (fragment converts land→build inside the box). The wool and spawn arms
  route through `BoxFiller` instead of their bespoke `SolveDepth`/`SolveWidth`/`spawnLen` sizing, so **box
  footprint stops being a by-product of the budget-share solve and becomes an enforced, data-driven quantity**
  (the two-currency budget the `Box` record already models). Single-mouth docking + the existing families — no
  interface model, no open-variant patterns. This is the slice that lets a wool box's share reach a donut/U/H
  footprint and a spawn's size be governed by its profile. Depends on G61, G78. (review §4.1, §8)
- [ ] **G41-B — [M3] The `BoxInterface` valid-edges data model (the G80-opener).** Land the interface model
  every fill and pattern binds to, replacing today's single-mouth assumption: **valid edges** on
  `BoxInterface` (long vs short; a wool-touching edge/corner never docks) and **per-family multi-interface
  demands**. This is the data model `FillProfiles` binds against and G80's docking modes + G41-C's
  open-variant patterns execute over — the slice that used to "open" G41. Independent of G41-A. Depends on
  G61; unblocks G80. (review §1.5/§1.6, §4.3)
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
  **G41-B** (the valid-edges data model this task executes against). If the clamp's **corner-wrap dual host**
  turns out to need G63's partition graph, split
  rather than stall: the single-host modes (scythe side/combined edge, clamp full short-edge) ship after
  G41-B, the dual-host mode follows G63.
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
