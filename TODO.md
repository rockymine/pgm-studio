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

*(The G88/G89 pre-work has all shipped — the language (G94), the binding (G95), the edge taxonomy + publish
policy (G92), interval facts (G93), and **both team-unit box kinds**: the **hub box (G88)** and the **frontline
box (G89)**, together carrying the **offer type + both producer halves (G96)** — see `FEATURES.md`. What remains
of the arc is the offer **consumer**: the spine of **G63-C** below — the composer routes through the partitioner,
the hub emits first setting the neighbour menus, the mid consumes the frontline face offer, and
`TeamUnitGrower`'s team-unit authoring retires. That is where the language finally shrinks the code.)*

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
  re-pinned for the wool-on-spawn dock — byte-identical for I / L-dir≥0 spawns.* **Remaining (the switch's own spine),
  broken into sub-steps:** with all four **team-unit** kinds now boxes (G88/G89), invert grow-then-derive to
  **allocate-then-fill**, hub first — **C.1 ✓ landed** (`TeamUnitFiller.Fill`: fills an allocated partition hub-first
  into a `FilledUnit` — hub/spawn/wool/frontline, each neighbour consuming the hub's `EdgeOffer` width as its `cw`,
  the frontline's face offer carried out for the mid; the one board-frame input is the spawn facing; synthetic
  fixtures, no golden churn) → **C.2 (in progress)** the allocator (`TeamUnitAllocator`). *Landed:* the placement
  plan (`SamplePlan`/`UnitPlan` — spawn on the back **or a lateral side**, wools assigned around it: free sides
  first, back preferred, a 3rd doubling onto the spawn's side); the hub + spawn + wool box-Rect geometry from the
  budget (generic share sizing, under-budget OK); the per-edge `EdgeOffer` plan on the joints (offered width = the
  lane `w`); **the allocator owns the hub-form choice** — it samples a form and seats neighbours on that form's
  **real free-edge intervals** (the offerable surface, §1.13, read off the hub's own emitted offers), falling back
  to the solid rectangle when a form's free edges cannot host the plan; the chosen form rides on `Box.Form` for the
  filler to re-emit (so allocator and filler agree on the body and every dock lands on real terrain); and **no
  diagonal pinches**, now gated by the **mass-level corner law** (`Cells.HasDiagonalPinch` over the composed mask,
  not the coarse rect-pair proxy — a multi-piece hub's ¾-solid bridged corner reads clean); and **the allocator
  orients the form** — a vertical flip (`Box.FlipV`, replicated by the filler) turns the branch/holed forms'
  solid spine toward the demanded **back** and their open feet toward the unused **front**, so `L` and `U` land
  on the used sides (symmetric forms are unaffected); and **the frontline is allocated** — when the plan carries
  one (the default when there is budget) it seats on the front side, its reach pushing the hub back so it sits
  between the hub and the axis, and the filler fills it as a **join** (spine docking the hub via the shared
  `BodyOrient`, face toward the axis) whose face offer flows into `FilledUnit.FrontlineFace` (its mid consumer is
  C.3). The allocate→fill loop closes end-to-end and `tools/compose/unit-gallery.cs` renders `L`/`U`/`Ring`/rectangle
  hubs with frontlines, 0 pinches. *Remaining, roughly in order:*
    - **Hub-form richness — handedness, size, `mirror_x`.** `L`'s two solid edges are adjacent (spine + one arm),
      so it covers back + **one** lateral, and the arm sits on a fixed side — choosing its **handedness** to match
      the demanded lateral would let it cover back+right as well as back+left. `Double-hole` still needs a hub
      **wider than the current caps** (≥ 9). And the vertical flip only orients the **z-frames** (front = top edge);
      `mirror_x` (front = a lateral edge) needs a rotation, not a flip, so its `L`/`U` stay canonical for now.
    - **Wool & spawn shape richness** — layouts are "very basic" (mostly `I`): the **spawn is I-only** (the L's
      overhanging foot needs the entry-seat-and-shift — seat by the entry width `w`, let the box overhang free
      space), and the **wool** boxes are generic-share-sized so the fill mostly lands `I`; give them family/size
      variety (L/Z/scythe/…).
    - **Hub-floor refinement** — the frontline / twin-recess / wool-c clearance floors the grower's `HubVFloor`
      encodes (today a simplified `w+2` floor).
    - **CT1 / LN2 invariants by construction** — ≥10-block image clearance (orbit images stay separate islands)
      and the 50-block chain cap, baked into placement, failing the attempt if unmet (no repair loop).
    - **Wire** `BoxPartitioner.Partition` to return the allocation (+ the crossing `design` the frontline reach
      needs), replacing `BoxPartition.Of(grow)`.
  → **C.3** wire `Composer` through C.2→C.1 and **retire `TeamUnitGrower`'s authoring** (RNG re-keys, goldens churn)
  → **C.4** the clamp's **dual-host corner-wrap** → **C.5** re-baseline gallery cases, **then** freeze the G32-D
  goldens. `MidCarver` stays — the mid is not a team-unit box, it is derived (`f(frontline)`, consuming the frontline
  face offer). *Deliberately NOT next: the
  fill-to-`LandTargetCells` **directed repair** (retiring `SolveDepth`/`SolveWidth`/`spawnLen`) — it resizes shapes
  to hit the budget, which grows them further, the opposite of what the seeds need; parked in `BACKLOG.md` to be
  reconsidered as a targeted rule, not a solver.* Depends on G63-B, **G88, G89**. (review §4.2, §4.4, §4.5, §7.7)

