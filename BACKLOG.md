# pgm-studio — Backlog (later)

The **long tail** — open work that isn't in the current focus. The active slice is in **`TODO.md`**;
shipped capabilities are in **`FEATURES.md`** (the Done column). Flow: **`BACKLOG.md` → `TODO.md` →
`FEATURES.md`**.

**Holds only open work:** `[ ]` to-do, `[~]` started-but-parked — **never `[x]`.** A task lives in
exactly **one** of the three files; pull one up into `TODO.md` when it becomes now/next (its id does not
change). Sections + ids match `TODO.md` — a task slots into the same section wherever it lives. Parked /
deferred items stay here, flagged inline. Board rules live in `CLAUDE.md` (§ "Status & task board").

Task ids are a section letter + number, **globally unique and stable** across all three files; never
renumber or reuse.

## Authoring (N) — the new-map intent editor (`/maps/{id}/configure`, new maps only)

The guided wizard at `/maps/{id}/configure` (UI label **Configure**) that builds a map from declarative
intent (`docs/contracts/new-map-authoring.md`; backend + every page-order step are landed —
`FEATURES.md`). **Leave the existing Edit editor untouched** — a separate surface, not a refit. Only
the focus-integration polish remains.

- [ ] **N08 — Monument Y via side-view + per-side focus.** The side-view (`SliceView`) already sets Y on
  **spawn** and **wool-spawn** (`SpawnPhase`/`WoolSpawnPhase`, `FEATURES.md`); the open slice is the rest:
  (a) wire the side-view into **`WoolMonumentsPhase`** so a monument's Y is editable, not read-only
  (lift it off y=0 onto terrain); (b) **per-side focus** — `FocusSection` is still a `/concepts` mockup;
  the canvas **fit-island** exists but not per-team quadrant framing — refine the concept so the author
  can frame one team's quadrant while working its unit. (`FocusSection`)
- [ ] **N09 — Team id should track the team's colour.** The team id is seeded from the colour first picked
  (`Id = colour.Replace(' ','-')`), but `TeamsPhase.SetColor` only updates the colour — so recolouring a
  team (e.g. red → purple) leaves `id="red"` and every id derived from it (`only-red`, `red-spawn-point`,
  the `…-red-monument` blocks, `reds-woolrooms`). Functionally fine (PGM resolves the id) but reads wrong.
  Re-derive the id on colour change and **cascade the rename** across the intent — `teams`, `islandTeams`,
  and `spawns[].team` / `wools[].owner` / `wools[].monuments[].team` — with a guard to skip the rename (just
  recolour) when the new colour-derived id would collide with another team's.
- [~] **N11 — Monument Y must seat on terrain; wool spawns must re-snap on move.** `SpawnPhase` seats team
  spawns (point placement + orbit copies) and the observer via `StandingYAsync`, and `WoolObjectivesPhase`
  seeds the wool via `RestingYAsync` — both one block above the `column-floor` block (which is the topmost
  solid block, inclusive, so seating *on* it is floor + 1). Still open: monuments aren't seated at all;
  `WoolSpawnPhase`'s point tool moves a wool's X/Z without re-snapping its Y to the new column; and a team
  spawn's Y isn't re-snapped when moved via the coord inputs (only on point placement). Pairs with `N08`
  (monument Y editing) and `CV11` (the side-view clamp side of the same problem).

## Sketch tool (S) — parked slices

The Sketch depth pass has shipped (`FEATURES.md` — select/drag, rotate, scale/squash, split, selection
highlight); these are the parked / dormant / deferred slices.

- [ ] **S9b — Angle/parallel snapping + droppable guide lines (parked).** S9 landed **position** alignment
  (edges/centres snap to other shapes + the symmetry centre, with guides). The remaining picture-editor bits:
  **angle/parallel** snapping (rotate a shape so its edges run parallel to another's — "hold two lanes
  parallel"), and **manually droppable** guide lines shapes snap to (vs the current auto-from-shapes). Both
  are their own work; park until needed.
- [ ] **S12 — Pin the Islands tree to the top of the sketch sidebar (UI polish, parked).** The residual weight
  above **Islands** is the **Layers** panel + the 12-tile **Library** palette. Collapse both behind `<details>`
  accordions (Library default-collapsed once the map has shapes), or move the Library to a toolbar popover (it's a
  "reach for a primitive" action, not persistent state). (`docs/sketch-tool-ux-review.md` P0#1;
  `docs/contracts/sketch-creation-flow.md` follow-on.)

## Editor & canvas infrastructure (C / CV)

Shared infra for **both** the Configure wizard (`/maps/{id}/configure`) and the frozen Edit editor
(`/maps/{id}/edit`). `C12`/`C14` are cross-cutting (serve both surfaces); `C9`/`C11`
are Edit-specific. Full canvas spec: `docs/contracts/canvas-interaction.md`.

- [ ] **C9 — Kits editing UI (Teams) + per-activity status dots.** Spawn `kit` is read/sent but has no
  edit UI; there is no status-dot system. *(Two sub-items — split if priorities diverge.)*
- [ ] **C11 — Wire + verify inspector edits across activities.** `OnDelete`/`OnRename` are wired only
  in Build Regions; the Regions/Teams/Objective inspectors are **unwired** (rename/delete silently
  no-op). Wire all three + verify rename/delete/coord-patch end-to-end.
- [ ] **C12 — Extract shared Blazor components.** (`Toast`/ErrorToast already done.) No `Shared/`
  component directory exists yet. Remaining, by payoff: **`AuthorDisplay`** (cross-tool reuse with S2 —
  bundle the name↔uuid resolve), the **`Workspace`** layout shell (sidebar/canvas/inspector slots,
  repeated in 6 activities), **`SectionHeader`** (ruled title + "+ Add", ~17 uses), **`ActivityRail`**
  (extract when S2 lands).
- [ ] **C14 — Dedupe activity code-behind.** The repeated `Post/Patch/Delete/Send` http trio
  (Build/Objective/Teams) + the `Index`/`CollectDescendants` region-tree walkers (3–4 activities) →
  a shared `MapApiClient` and/or `EditorActivityBase` / static `RegionNode` helpers.

## Backend, pipeline & internals (B / P / A)

- [ ] **B9 — Re-import a world into an existing map (keep the authored intent).** When an author tweaks the
  terrain (e.g. adds iron inside the spawns so the renewable populates) they currently have to import the
  updated world as a *new* map and hand-copy the intent across. Add a "re-import / update world" action on
  an intent-authored map that re-scans a chosen folder/zip in place — refreshing only the world-derived
  data (`islands_json`, `resource_block`, surface/layer parquets, monument candidates) and **preserving the
  `map_intent_json`**, then regenerating. Safe while island detection stays stable (the intent references
  islands by id, and spawns/wools are world coordinates); flag the author when the island set changes so a
  stale `islandTeams` mapping can be re-checked. (Manual procedure today: copy the `map_intent_json`
  artifact + re-scan, then `PUT /map/{slug}/intent`.)
- [ ] **B21 — MCP server: agent-drivable map authoring over the plan layer.** A thin MCP head (official
  C# SDK, `ModelContextProtocol` NuGet; new `PgmStudio.Mcp` project or a proxy over the running `:7894`
  API) so an AI agent can build a map end-to-end. The plan layer is the agent surface — `plan.json` is
  small, semantic, and `PlanValidator` returns rule-id findings, giving the agent a compiler-style
  submit→lint→fix loop. Tools: `plan_validate` · `plan_compile` (summary, not blobs) · `plan_render`
  (image content — agents self-correct far better seeing the board) · `compose` (a G32 plan as starting
  material to mutate) · `create_draft`/`export` (existing chain; return the export **link**, never the
  world zip inline). MCP resources: the frozen `layout-rules.md` as the design brief + `tools/seeds/*.plan.json`
  as few-shot examples — tool-description curation is the real work, not plumbing. Fast-follow after the
  composer (G32) lands; its gates (validator, stat envelopes, renderer) are exactly the tools this exposes.

## Layout generation (G)

Two tracks share this section. **The headline is the composer** (plan-then-realize): rule-based
composition of `plan.json` seeds under the frozen rules (`docs/contracts/map-generation.md` +
`layout-rules.md` + `plan-editor.md`). Its current focus — the **box / deriver / evaluator consolidation**
(refactor-first, `docs/map-generation-architecture-review.md`) — is the batch in `TODO.md` (G58–G60); the
box-model milestones M2–M4 and the interface / hub / lane feature long-tail are parked here until that
lands, **reworded to be delivered *through* the box model** rather than against the current grower. The
island-detection / validation work follows. (The older / parallel **lane sketch generator** track — the
archetype starters that seeded a draft map from lane primitives — has been **retired** in favour of the
plan-then-realize direction; see `FEATURES.md` § Layout generation.) Landed so far (`FEATURES.md`): the
composer core + box-based wool-approach vocabulary (G49/G53/G54), island-outline simplification (`G6`),
the `island-roles` hook (`G11`), and the layout-generation design that resolved `G15`.
Builds on the Sketch tool (`S2`) and the intent model (`N`).

**Composer — box-model milestones (M2–M4 + doc)**
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
  via a rect transform (`Geom.Symmetry.Apply`) instead of the hardcoded top-edge mouth. Kills the third
  shape impl; gives G44 its structural-spend vocabulary and makes G50–G52 reachable from generation.
  **Changes RNG consumption** (goldens re-key). Depends on G58. (review §4, §4.4, §7.4)
- [ ] **G62 — Derive-side slot recovery + classifier scoping.** `Shapes/SlotAssignment`: after `Classify`
  returns the family, template-match the slot sequence onto the classified pieces
  (`AssignSlots(family, pieces) → piece→slot map`) — slots survive save/load/author/trace **without**
  persisting them (they are derived, not authored; §3's split). `Shapes/CorridorExtent`: the junction-stop
  flood promoted from `WoolLaneShape`, parameterised by **stop policy** — *lane read* (stop at any
  junction) vs *approach read* (continue through same-width forks, stop at hubs/plazas) — giving the
  classifier a **scope** so it works on full composed/traced plans, not just standalone `AsPlan` fixtures
  (today it floods the whole component and would read every wool on a unit as `Donut`). `WoolLaneShape`
  the class retires here; its lane measurable becomes the open read of the lane-policy extent. Upgrades
  `emit-verify` to a true mirror (emit → classify → re-derive slots → compare). **Prerequisite for G56.**
  Depends on G58, G59. (review §3.4–§3.6, §7.5)
- [ ] **G41 — [M3] Open-variant emission for frontline & hub (delivers L/Z compositions + HB4).** Today
  the hub is always one square and the authored L/Z frontline↔hub combinations aren't generated. Build the
  **open-variant** shape layer over the shared family machinery: `Compose/Boxes/FillPattern` (arrangements
  of family shapes in a box — the terminal-less / through-corridor read), `FillProfiles` (per-`BoxKind`
  legal patterns × families × binding, each restriction citing its `layout-rules.md` id), `BoxFiller` (the
  one profile-driven fill entry point). `FrontForm` retires into frontline patterns (none · single-chain
  I/Z · wide-face · twin-strands+recess — FR3/FR4/FR6/CT8); hub open patterns (solid I · L · Z ·
  ring-with-hole — HB1/HB3/HB4). **G39's** corner/edge interlock is expressed here as a `BoxInterface`
  constraint. Fills start **publishing vacancies** (§4.4): boxes may overlap (piece-disjointness, not
  box-disjointness, is the invariant), so a fill's residual envelope is published as claimable negative
  space — a **U-hub publishes its bay**, a twin frontline its recess (the CT8 recess generalized). Emit-side
  and exact (families are fixed templates), so no derive pass finds them. `FillProfiles` gates claims
  (a spawn may claim a hub bay whose mouth faces away from the axis) — this is what makes the
  **spawn-in-hub-bay** layout expressible (three wools L/T/R + the spawn in the U's bay) instead of
  forcing the G45 parallel-lane anti-pattern. `emit-verify` grows per-kind pattern mirrors (twin → closure
  hole ringed by two strands; L hub → one-bend junction outline) — no new `*Shape` classes. Blocked partly
  on the author's frontline/hub teaching set. Depends on G61. (review §3.1, §4.3, §4.4, §7.6)
- [ ] **G63 — [M4] Partitioner-first composition.** `Compose/Boxes/BoxPartition` (boxes + interfaces =
  a constraint graph) replaces the `Shape` sampling record as what sampling produces; `BoxPartitioner`
  (budget → partition, **directed repair** from `FillResult` instead of 60-attempt re-rolls). **Boxes may
  overlap** — the partition allocates budgets and constraints, not exclusive area (piece-disjointness +
  image clearance is the real invariant); the partitioner allocates later boxes **from published vacancies**
  (§4.4) as well as fresh space, so a bay-seated spawn docks up to three walls for free (`spawn-first`
  inverts it — the hub's fill must wrap a staked pocket). `GrowthOrder` named strategies (`spawn-first` /
  `hub-first` / `mid-out`) make the emission order an **experiment axis** judged by the evaluator + G43, not
  doctrine. `Box.LandTargetCells` gives the
  two-currency budget its per-box half, so **fragment** becomes a generic pass over the partition
  (`IsolationCut` + the mid's low target are its two existing special cases). `TeamUnitGrower` retires.
  Re-baseline gallery cases; **then** freeze the G32-D goldens (per strategy). Depends on G61.
  (review §4.2, §4.4, §4.5, §7.7)
- [ ] **G64 — Doc pass on `map-generation.md` (reconcile with shipped code).** The canonical doc silently
  mixes description and aspiration. Declare the emission order an **experimental strategy axis over the
  constraint graph** (`spawn-first`/`hub-first`/`mid-out` are `GrowthOrder` knobs), not a fixed sequence —
  §2/§4 currently state it three different ways (Finding 1.1). Add **current-vs-target status per pipeline
  stage** (Finding 1.2). Reconcile "the frontline is an output" against the shipped mid-outward input.
  Name the board deriver as **code** (`BoardDeriver` / `ContactGraph` / `BoardStructure`), not the
  `tools/deriver` script (Finding 1.3). Trails the code that makes each statement true. (review §1, §7.8)

**Rule visualization & slot-relation rules (§9.7/§9.8 — terms already return drawable `Evidence`)**
- [ ] **G66 — Rule-visualization renderers (illustrated catalog + reject inspector).** Generic passes over the
  `Evidence` primitives every term already returns — **zero per-term drawing code**. (1) `tools/deriver/rule-cards.cs`:
  reuse the `derive-gallery` SVG card machinery to render, per term, a **pass** card + a **violated** card with
  its evidence overlaid — the fixtures *are* the per-term unit tests — outputting an **illustrated
  `layout-rules.md`** (one HTML page: prose + do/don't card per rule id). A term is not *done* until its card
  renders, so the fixture doubles as the documentation and neither drifts. (2) **Reject inspector**: a logged
  `{seed, termId}` re-composes the failed attempt and renders the killing term's evidence (why-it-died becomes a
  picture). (3) **Minimal-pair visual diffs**: the G60 ranking harness renders each pair side by side with the
  negative's expected-term evidence. (The editor overlay is folded into G60's DTO wiring.) Depends on G60.
  (review §9.7)
- [ ] **G67 — Fill-time slot invariants.** Slot-relation rules checked **when a box is filled / in `emit-verify`**,
  where the emitter's slots are in hand: "only a `run`/`bar` splits into lane + build-lane, an `entry`/`room`
  stays whole", "the entry ≥ the lane it feeds", "the room-run stub stays shorter than its bar" — each a fill
  invariant citing its slot rule (`map-generation.md` §5.3), visualized through the same card machinery
  (`Evidence` tagged `slot:*`). This is where the majority of slot rules live. Depends on G61. (review §9.8)
- [ ] **G68 — Evaluator-side slot-relation terms.** Slot rules as ordinary `ILayoutTerm`s over **any** plan,
  gated on **G62's `SlotAssignment`** (a loaded/authored/traced plan has no slots until derive recovers them;
  `EvalContext` gains the recovered `pieceId → family + slot` map). **Conditional-fire**: a term runs only where
  a family was **confidently recovered**, and *failure to recover a family is never itself a violation* (a
  hand-drawn blob that plays well scores clean; a recognized scythe with an underfed entry is flagged) — keeping
  slot rules off the enumeration trap (`layout-evaluator.md` §8). Evidence carries `slot:*`-tagged rects/measures;
  the **slot legend card per family** (the §5.3 template table drawn from `SlotTemplate` + `ShapeEmitter`) joins
  the `rule-cards.cs` output as the shared key. Depends on G62, G66. (review §9.8)
- [ ] **G69 — The deriver mis-reads dense mids: crossing-corridor + rotation primitives, then the cramming
  term.** The frontline-cramming negatives (`tools/seeds/teaching/crammed-frontline-*`) can't be scored because
  the deriver's structural reading systematically contradicts the play-experience on saturated mids — nine
  measurables tried over the Slice-C investigation (stones−crossings, stones/frontline-width, per-stone
  void-exposure, stone density, crossing aspect, stone spacing, opposing-frontline overlap, band-length/team-
  footprint, uncrossed-void) and none expresses cramming. The diagnosis, from the author's models + the
  reconciliation gallery (artifact `faf3ffcc`):
  - **acapulco is NOT a clean positive** — the author confirms it "can read bad" (its crossing runs nearly as
    long as the team footprint too). So the goal is **not** to separate crammed from acapulco; both should read
    mildly-to-badly crammed. The old "crammed ≡ acapulco" paradox dissolves — they're similar because they're
    both long-band-crammed.
  - **Band length isn't the separator** — `bandSpan/teamSpan` is 0.67 on `crammed-single` *and* on the good
    resolutions `rotation-stone`/`move-closer`; positives run higher (aether 0.84). The resolutions kept the long
    band and are good because they added **rotation**, not because the band shrank.
  - **The deriver's mid reads fight the eye, repeatedly:** per-stone void-exposure reads acapulco's stones *more*
    exposed than the crammed seeds' (opposite); opposing-frontline overlap reads acapulco *more* aligned
    (opposite to "offset masses"); `crossRoutes=2` on `crammed-double-band` claims rotation the author says isn't
    there ("you just hop between islands, no way to switch"); and its two far-end islands are mis-classed **team**
    stones because the (deliberately bad) spawn↔wool path routes through them (captivity/route rule bends to bad
    marker placement).
  The real work is **deriver primitives**, then the term: **(a)** a **crossing-corridor** read — the mid modelled
  as a corridor with a cross-section and a length, so "band as long as the team footprint" (single-band) and
  "band-width = stone-size, N hops, no lateral switch" (double-band) become expressible; **(b)** **rotation that
  means rotation** — a measure of "can a player actually switch sides here", not the ring-count `crossRoutes`
  (crammed-double must read *no rotation* despite two ringing zones); **(c)** **robust stone classification** —
  team-vs-neutral must not flip on degenerate spawn/wool routing (offset team masses / mid-rectangle stagger is a
  real element that falls out here — "a real element maps can use"); **(d)** only then the cramming term, likely
  FR4's "one crossing is fine only if it rotates / isn't over-long vs the footprint", with acapulco landing
  mildly bad by construction. Until (a)–(c), cramming is not expressible. (G60 §6; from the Slice-C
  investigation, artifact `faf3ffcc`)

**Composer — mid / frontline / interface (reframed as evaluator terms + partition constraints)**
- [ ] **G38 — Multiple / parallel mid bands + their variations.** The composer ships only the CT1 clean
  form (one band spanning the axis). Add **two-or-more parallel bands** (FR7, rot_180-only, variable-length)
  and the authored **variations**: a **hole in the build zone**, a **stepping stone between the dual bands**,
  the two-sided plaza (`big-board-wool-two-sided-plaza-parallel-mid`). Each band needs its own dock + hop
  arithmetic; the fan/merge must keep the bands distinct. Unshipped feature, not a bug (flagged 2026-07-05).
- [ ] **G39 — Frontline↔build-zone full-face dock (requirement — delivered via the box model, not worked
  standalone).** The band must dock the **full frontline face** at shared corner/edge lines: no
  flush-on-one-edge-short-on-the-other (`gen-p30-t2-rot_180-s1`), no too-thin build zone
  (`gen-p20-t2-mirror_z-s1`). Under refactor-first this is delivered as **(1)** a hard evaluator term in
  G60 (`band-docks-full-face`) that *catches* violations and **(2)** a `BoxInterface` full-face constraint
  at emission / partition time (G41/G63) that *prevents* them — the original standalone fix (teach the
  current band the CT7 corner-snap the stones use) is **dropped as throwaway**, since M2–M4 replace that
  band. Anchor requirement; do not attack directly. (review §1.2, §4.2, §9.6)
- [ ] **G40 — Enclosed dead-space / hole-size cap (requirement — evaluator term + partition constraint).**
  The hole enclosed by hub + frontlines + build zones stays **~10×10** (occasionally 10×20, never 10×40 —
  `gen-p30-t2-rot_180-s7`'s twin 35-block frontlines); generalize to **any** void an L/U lane wraps.
  Delivered as a **soft** G60 term (hole-extent band from the seeds) plus a `BoxInterface` / partition
  constraint on frontline extrusion (G41/G63); surplus routes to width / more routes via G61, never a
  stretched frontline or a void-wrapping lane (the length driver is **G44**). Anchor requirement.
  (review §4.2, §9)
- [ ] **G42 — Spawn docks to a piece, never submerges.** *New.* The spawn is meant to **dock** (abut) its
  neighbours; on some boards it is **fully engulfed** by the surrounding pieces (`gen-p20-t2-rot_180-s7` —
  which also surfaced the first accidental terrain hole). Enforce spawn-as-dock (SP): a spawn touches by a
  readable edge and is never interior to the merged land.
- [ ] **G43 — Composer ↔ example-set conformance sweep (consumer of the evaluator, G60).** Over a
  generated-board sweep, aggregate the evaluator's soft-distance-per-term against the teaching set
  (`tools/seeds/teaching/`) into a report — the eyeball-cards analogue, and the gate-in-aggregate that
  would have caught G39/G40 before the gallery. The measurables (hub-hole size, band↔frontline
  edge-coincidence / width-match, extrusion length, mid-piece share, island count) are **G60 terms**, not
  defined here; the hard gate is G60's hard terms. Feeds the G32-D goldens. Depends on G60. (review §9.5)

**Composer — hub / spawn / wool lanes**
- [ ] **G44 — Budget→length decoupling (traced root cause of the lane bloat).** The grower's area gate
  rejects any unit under 80% of `LandPerTeam`, and its only real spend-vocabulary is **longer lanes** — so
  a big budget is absorbed by length, not structure. Trace (`gen-p30-t2-rot_180-s7`, 220 cells → 217
  spent): a **95-block L** spawn lane and a **95-block U** wool lane that wraps a giant empty square,
  putting the wool out of play as a dead-end a defender just holds. Fixes in order: **(a)** cap absolute
  lane lengths to the authored norms (spawn ≈ 20–30 blocks; wool lanes bounded — LN2's 50-block cap is
  *per collinear chain*, so an L/Z stacks two); **(b)** route surplus into **structure, not length** — the
  wool-box migration (G61) supplies the vocabulary: **escalate the family** (I→L→Z→U/H/scythe) and widen,
  rather than stretch, with directed budget repair once the partitioner lands (G63); **(c)** re-examine the
  budget (whether `LandPerPlayer` over-scales past ~p16 and whether the area gate's lower bound should
  relax so a compact unit needn't hit the full target).
- [ ] **G45 — Third wool: rarer, and placed as a real route.** A third wool is sampled at **40%** for ≥16
  players and **always** built as a 2-cell dead-end straight back beside the spawn lane (`wool-lane-c`;
  `gen-p20-t2-rot_180-s13` — the wool squeezed next to the spawn). That parallel-lane placement is the **G45
  anti-pattern** — a failure mode of the square-hub model, **never a pattern to sample**. Reality: 2 wools
  common, **3 rare**, and a genuine third wool sits as **its own route**. The missing **positive** is the
  three-wool **L/T/R + spawn in a U-hub's bay** layout (a real third-wool route enabled by a claimed vacancy,
  §4.4) — author it as the G45 teaching seed the current set lacks. Lower the rate and add real 3-wool
  placement patterns. Depends on the G41/G63 vacancy mechanism (spawn claims the hub bay).
- [ ] **G37 — Lane-archetypes track (lane shapes · connections · hub shaping · alt entries).** The real
  lane grammar: authored **lane archetypes**, **what connects to the frontline**, **how hubs shape** (today
  the hub is a dumb square everything smashes into — G41), and **alternative entry points** to a lane (a
  long dead-end is pointless without alt routes — the defender just holds the mouth; not formalized yet).
  "Lane-heavy is bad" is a defect, not an archetype to sample (see the `composer-lane-archetypes-future`
  memory); the budget-driven over-long lanes it produces are traced to **G44**. Blocked on more teaching
  maps; sequenced **after** the interface layer (G39/G40).

**Composer — realize & unblock**
- [ ] **G35 — Composer-side buffer reservation (unblocks p5 / small rot_90).** Have the composer author
  buffers/allotments during generation — reserve a ≥1-cell border on rot_90 boards so the quarter-turn
  image can't self-collapse, hold spacing on small boards — to unblock p5 (BZ6 + spawn ≥2×2 over-budget at
  325 blocks²) and p5/t4/rot_90 (interior-overlap self-weld). Teaching material + a `layout-rules.md`
  amendment first. Never fix p5 by enlarging the board (re-triggers the LN2 arm stretch).
- [ ] **G36 — Residual composer polish (from the B2 review).** What's left after the mid-feel slice shipped
  and (2)/(3) moved to G37/G41: **(1)** confirm the rot_180 mid-band asymmetry (`p30-s7`/`s13`) is a real
  off-centre band vs a render artefact; **(4)** cap spawn-lane growth (`p30-s13` over-grown L); **(6)**
  frontline-**count** variety (not every board double-frontline).

The remaining generator / detection / validation work sorts into three domains:

**Generator (lane algorithm → Configure)**

- [ ] **G56 — Trace a real-map corpus + mine it for missed shapes.** With the plan editor's reference
  backdrop shipped, trace a batch of real maps into `tools/seeds/*.plan.json` (each carrying its `reference`
  provenance), then run the deriver (`WoolApproachShape.Classify` + `PlanDerived` junctions/lanes) over them to
  surface **WoolApproachShapes / hub shapes the current vocabulary misses or misreads** — feeding corrections
  back into `docs/contracts/map-generation.md` §5. Author-driven tracing (manual), mechanical
  classify/report can be a small harness under `tools/`. **Depends on G62** — the classifier needs a
  scope to read a wool's approach on a full traced plan, not just standalone fixtures; by then the deriver
  is `Shapes/ShapeClassifier` + `Derive/ContactGraph`/`BoardDeriver` (the G58/G59 renames).
- [ ] **G50 — Wool-box emitter: shift the entry/attachment off the box corner.** `WoolBoxEmitter` pins each
  shape's docking point to a box corner flush against the interface edge, so exactly 3 corners always fill; in
  a real plan the docking point slides along that edge. Applies to **donut and scythe only** (Z stays corner-
  pinned). The two behave differently:
  - **donut** — the attachment slides along the ring's edge; **only the attachment moves, the ring is
    unchanged.** Standard `ttttb / btvtb / btttw` → moved `btttb / ttvtb / btttw` (attachment drops from the
    top-left corner down to the left leg; `b` = box/buffer cell, `v` = ring hole).
  - **scythe** — has **two independently-offsettable endpoints**, entry and wool-end. The **entry** (tail)
    shifts off the corner and **propagates inward**: the piece the tail docks to (the spine) **shrinks from the
    top** so only the wool still reaches the edge. Standard `ttbw / btbt / bttt` → shifted-entry
    `bbbw / ttbt / bttt`. Shifting *only* the entry cell while leaving the spine full-height is **wrong**
    (`btbw / ttbt / bttt`) — the attached piece must resize with the shift. The **wool end** shifts the same
    way: standard → shifted-wool `ttbb / btbw / btbt / bttt`. All three verified `Scythe·w2` (classifier-
    transparent). Source plans: `scythenotboxaligned`, `scythewoolattachments`.
  Add offset parameters for both endpoints (offset along the interface edge, clamp rules TBD). Source plans
  incl. `smalldonutattach`. Sibling of G51/G52. **G50–G52 all become reachable from *generation* only once
  G61 (M2) lands** — today `WoolBoxEmitter` has no production caller, so these emitter knobs are exercised
  by tools/tests alone.
- [ ] **G51 — Wool-box emitter: variable attachment width on the scythe (parallel to the docked edge).** The
  attachment's interface width — measured **along** the edge it docks to (it stacks *parallel* to the shape it
  attaches to, never sticking away perpendicular) — is a knob wired **only on the donut** today
  (`attachmentWidth`, the ring-leg-parallel `aw`, `w2/w4/w6 = cw/2·cw/3·cw`). Wire the same on the **scythe**
  entry (widen the tail along the spine it docks to), same grammar. Donut done; scythe missing. Pairs with G50
  (a shifted, widened entry is the general case).
- [ ] **G52 — Wool-box emitter: wool-room docking mode (extend vs side-dock) for Z and scythe.** Today the
  wool always **extends** the terminal piece in-line (the lane runs straight into the room). Add the option
  for the wool to **dock the side** — perpendicular off the terminal piece, exactly like the `I` family's
  `RoomPlacement.SideTuck` — which the `I` already has but `Z`/`scythe` don't. Grants greater variance (the
  wool needn't always poke out the end). When side-docked, **the terminal piece the wool attaches to is
  shortened** (it no longer has to run out to hold the room). Verified classifier-transparent when the rest of
  the shape is standard: `Z` extend / side-dock-up / side-dock-down all read `Z` (`ttbbb/btttw`,
  `ttbb/btbw/bttt`, `ttbb/bttt/bbbw`); a clean scythe side-dock reads `Scythe` (`ttbb/btbtw/bttt`). **Caveat:**
  keep the terminal tail at normal width — the `scythewoolattachments` wool-2 example reads `H` only because
  it *also* thickened the tail to 2 tall (a tail wider than a lane branches, independent of docking). Generalise
  `RoomPlacement` beyond `I`. Source plans: `zwoolattachments`, `scythewoolattachments`. Pairs with G50/G51.
- [ ] **G29 — Climb profiling on lane chains (straight ramps vs switchbacks, approach labeling).** On the
  seam graph, detect *climbs* (maximal monotone-elevation traversal runs), classify straight ramp vs
  switchback/hairpin (direction reversal while climbing; displacement ≪ path length) and landings, and label
  each climb by its top-end anchor (wool approach / mid ascent / interior) and per-team use (attacker climb
  vs defender rotation). Spec: `docs/contracts/plan-editor.md` §2 "Climbs". Composer vocabulary for WL5/FR3
  (straight approach vs space-packing switchback vs defensible landing). Depends on `G24`'s chains.
- [ ] **G33 — Traffic ground truth: logs-only graph generator + flow priors + recovered footprints.**
  Input contract: **one zip of raw pgmlogger parquet per map** — nothing else (formats + the validated
  derivation live in `docs/contracts/traffic-ground-truth.md`; no external analysis project involved).
  Build: (a) the **logs-only `traffic_graph.json` generator** (occupancy/edges from 2 s positions, POIs
  from spawn/wool/capture event clusters, void via the symmetry-pooled fall-share signal at **block
  resolution** — the rim-aliasing fix — islands as traffic-minus-void components; ingwaz validation:
  islands 6/6, void recall 1.0); (b) a plan-editor overlay rendering traffic heat + the **emergent build
  regions**; (c) flow priors to score composer candidates (mid/team occupancy split, approach usage,
  void share, frontline band); (d) recovered footprints as CT test articles (first pair:
  `tools/traffic/ingwaz.*`). The author supplies per-map log zips (uploaded like ingwaz's, or batch-
  collected in a local session); only zips, graph JSONs, and priors enter the repo.
- [ ] **G31 — Scaled structure presets (stamps must fit tiny and huge maps).** The spawn cube / wool
  cage / iron cube stamps are fixed-size (8×8 footprints); on `mirror-tiny-map-cliff` (1-cell pieces,
  markers at block centres) the stamps overlap the piece bounds, and 30+/team boards could take larger
  presets. Scale the stamp presets with map class (the G8 coupling) or the carrier piece size; author
  request from the tiny seed.
- [ ] **G24 — Junction-region derivation + Hubs overlay + lane chains.** Derive hubs as *internal* computed
  structure on the unioned island footprint (mouth-extrusion intersections of ≥3 access mouths — see
  `docs/contracts/plan-editor.md` §2 "Junction regions"; corners yield nothing), expose them through
  `/api/plan/inspect` and a "Hubs" editor overlay, and build **lane chains** on top (corridors between
  junctions/dead-ends) so width/length lint and depth checks measure along a whole lane rather than per
  piece. The anchor for composer-side junction placement/verification later. (layout-rules.md PC1)
- [ ] **G34 — Theming & styling rules: material palettes + prop stamps (trees etc.).** Generated maps are
  100% playable and 100% bare stone — extend the meaning→structure move to the world's *read*. A rule-driven
  theming pass at rasterize/export: **theme = material palette** (per role/stratum: surface cap, body,
  cliff faces, wool-approach accents) + a **prop stamp library** (trees, rocks, lamps) with placement rules
  (density per piece kind, never inside corridor-min footprints or the spawn/wool stamp plateaus, respect
  build zones and G6 headroom, seeded-deterministic like the composer). The stamp machinery is precedent
  (spawn cubes / wool cages / ST1–ST4; `G31` scales them); capture theming rules the same way
  `layout-rules.md` was captured — expert-authored, correction-by-id, a `theming-rules.md` contract.
  **Deliberately moves the division-of-labour boundary** (baseline theming vs the author's "always manual"
  post-detach polish): generator does layout + *baseline* theming; character/set pieces/themed identity stay
  the author's (Tier 3 unchanged).

**Island detection**
- [ ] **G9 — Re-scan the corpus with stair-aware detection (remaining slice).** The over-split
  **detection fix landed** (`FEATURES.md`: `CleanColumns` + `DetectStairAware`), as did the review
  flag + role classifier. What remains: (a) **re-scan the corpus** so the stored `islands.json` /
  `island_sketch_json` reflect stair-aware (the live DB + `pgm-studio-output` were generated with the legacy
  detection — needs the source worlds, `OvercastCommunity/CommunityMaps`+`PublicMaps` `ctw/`), and decide
  whether to refresh the `--islands` Python-parity oracle to match; (b) the residual `a_new_day` **isolated
  raised-decor specks** (≈37-block grid bits with no walkable connection — correctly `small` via
  `IslandClassifier`, but a per-island prune could drop them); (c) any **under-split / merged** read beyond
  `abstract` (whose stained-glass build-floor is now excluded — `FEATURES.md`): `LooksUnderSplit` is the
  catch-all flag; the residual lever if one is found is to fall through to surface-based detection when a
  cleaned-base component is a map-spanning low-Y slab. Serves the shipped island-health / analysis
  features; the decompose-queue UI slice was dropped with the corpus-mining flywheel.
- [ ] **G12 — Re-prune flying blobs above terrain (stair-aware regression).** Stair-aware connectivity fixed
  the over-split (disconnected islands) but **re-introduced** the stark-y-jump / flying-island problem:
  decorative masses floating above the map (dragons/birds) now merge back into the islands when a near-vertical
  surface chain bridges them (e.g. **Duality**, **mame_i_shrunk_the_pvpers**). Re-add a guard: stop joining
  across a **really big y-increase**, and/or identify & **prune blobs whose base sits well above the terrain
  band** (the old float-prune did this on `DetectHeightAware`; the stair surfaces now leak past it).
  **`max_build_height`** is a natural cut/prune ceiling — anything whose mass is above it is non-playable
  decor. Re-validate the over-split fixes (a_new_day/thunder) still hold after re-adding the ceiling.

**Validation / playability**
- [ ] **G65 — FannedGraph ↔ ContactGraph adjacency reconcile (deferred from G59).** `FannedGraph.LandAdjacent`
  (reachability) diverges from the rect-layer authority `ContactGraph` (`Classify` + `Components`) on two
  counts: **(1)** any area overlap connects regardless of surface delta, while `Components` unions an overlap
  only at `SurfaceDelta == 0`; **(2)** an edge connects only at full corridor width (`Land`), while `Components`
  also unions `Narrow` seams. Pick one rule for reachability and add a test (review 2.3 / 6.5). Needs per-node
  surface carried into the fanned graph (not there yet) and validation against the traversability harness
  (`tools/PgmStudio.RoundTrip --traversability`), so G59 left it behavior-unchanged with the divergence
  documented in `FannedGraph.LandAdjacent`.
- [ ] **G2 — Protection-aware reachability port (memory stage S4).** `MapValidity` (every-wool-needs-a-monument)
  and the `NVAL` export gate (`PreflightEndpoint`) already shipped (`FEATURES.md`). The open slice is to **port
  protection-aware reachability** from `scripts/generator/validate_play.py` to C# `Analysis/Playability`:
  today's `Traversability.Check` only tests connectivity, **not** spawn-protection-as-wall, so it passes maps
  the generator's Python validator would fail. Feed it into the `NVAL` / preflight gate.

## Lower priority / parked

Existing-Edit (`/maps/{id}/edit`) authoring features — **not** used by the intent generator (which
auto-wires), and Edit is frozen. Resume when the existing-map authoring path is picked up. Their
*backends* are done (`FEATURES.md`).

- [ ] **Wire-after-group + filter-wiring UI** (ex-`N4` + ex-`F1`). Group regions in Edit → apply
  a wiring template by role; cross-step carve-out (complement) detection; canvas Ctrl-click
  multi-select. The wiring backend (`FilterWiring` appliers + `POST /wiring/apply`) is done.
- [ ] **Symmetry counterpart accept/reject UI + IoU equivalence** (ex-`F3` + ex-`A2`). Canvas
  preview/confirm for orbit-created counterparts + `regions_equivalent`/`is_counterpart` detection for
  dedup + symmetry-violation review. The counterpart + orbit-fill backend is done (the authoring
  generator already uses orbit-fill automatically).
- [ ] **3D / side-depth selection view** (ex-`F8` 3D half). The flat side-view slice is done (→ `N08`);
  a true 3D selection view (monument point/block + cuboid Y) needs design. Later.
- [ ] **Comment hygiene sweep — purely functional comments.** Code comments must describe behaviour
  only: **no** references to the Python reference app ("port of", "mirrors the reference", parity/oracle)
  and **no** implementation-phase / task ids (`NS`, `N00`, `B8`, `P5`, `ND2`, …). New code already
  follows this (CLAUDE.md). ~19 task-id references + ~41 parity/"port of" references remain across
  `src/` + `tests/` (e.g. `ImportEndpoints`, `WorldScanPhase`, `WorldFeatureWriter`) — sweep them.

**Deprioritized — may be dropped in a later pass.** Optional/deferred slices parked out of the active
long-tail so they stop competing with real work. Re-evaluate (or delete) when their area is next touched.

- [ ] **S10 — Auto-promote rectangles on Bézier (parked, optional).** Today S4 promotes via the inspector
  button / `P`; a rectangle keeps its 8-handle resize and has no Bézier affordance. If we ever want a
  rectangle's corner to sprout a Bézier handle that *implicitly* converts it to a polygon, it needs rect
  vertex/tangent handles in `sketch-edit-controller.js` (a UX decision on resize-handles vs vertex-handles).
  Low priority — explicit promotion already covers the need.
- [ ] **S16 — Resize library primitives after placement (mostly resolved; deferred).** `S21`'s island scale
  handles now resize a **placed** polyomino / n-gon — a single non-rectangle member gets the 8 bbox scale handles —
  so the after-placement resize is **covered**. The only remaining slice is optional **drag-to-size during
  placement** (`geometry/shape-library.js` `instantiate` drops at a fixed `defaultCell`). Low priority.
- [ ] **P8 — Pipeline re-run on config change (parked escape hatch, world-present only).** A
  parameterized re-scan honouring a bespoke `scan_layer`/`exclude_blocks` → re-detect islands → rewrite
  **layer-tagged** `layer.parquet` / `islands.json`. The per-map scan-layer + custom block-exclusion UI
  has been **removed** from both editors (detection is the fixed cleaned base; the world-scanning
  endpoints are gone), so there is no longer a config-change to honour from the UI — this remains only as
  a rare, local-only override path outside the hosted flow (new-map-authoring.md §6a). (Island-exclusion →
  symmetry re-run already works without a re-scan, B7.)
- [ ] **P7 — [Deferred decision] Consolidate the layer extractors / scan passes.** **`ND2` settles the
  "consolidate vs keep" half: KEEP the exact per-layer extractors** — the World step uses them in distinct
  roles (cleaned `Base` = detection · `Surface` = visual aid · `Segments` = vertical), so they're a feature,
  not duplication; their per-layer default ignored-block sets (`Base` gets the expanded ND2 noise set;
  Surface/Y0 = air-only) are the solid-policy. Still open: the byte-parity sub-question — a segment-derived
  surface would **not** be byte-parity with the reference (endpoint-only runs also can't honour user
  `exclude_blocks`). Pairs with A4.
- [ ] **A3 — Buildability endpoint perf (verify, then optimise if needed).** Per-cell NTS over the grid
  was flagged slow; the endpoint is now live and user-visible (`N03`'s buildability overlay landed).
  **First profile it under the Configure overlay** — only optimise (spatial index / batch) if it's
  actually slow in use; otherwise close.
- [ ] **A4 — [Consider, not perf] Vector-boolean island outlines (drop the rasterize→polygon round-trip).**
  Today island outlines come from a pixel round-trip: vector shapes → rasterize to cells → BFS → `BlocksToPolygon`
  (cells back to a polygon), done only to **avoid a C# polygon-boolean lib** (sketch-authoring.md §6). We
  already depend on NTS, so the sketch-finish island polygons *could* be computed by NTS vector boolean
  directly off the shapes (union adds, difference subs), dropping `BlocksToPolygon` + the BFS for the
  *polygon*. **Not a perf task** — the row-run fix already removed the hotspot, and the cell rasterize must
  still run for `layer_segment`/`layer.parquet` (Configure height side-view + analysis). Payoff is cleanliness
  + exact (smooth) outlines; cost is NTS boolean on the authoring path and a **staircase→smooth** outline
  divergence from scanned maps. Weigh before doing.
