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

## Layout generation (G) — current focus: the composer

Phase 2 of plan-then-realize: the rule-based composer generates `plan.json` seeds under the **frozen**
rule set (`docs/contracts/layout-rules.md`) in the §3 order of `docs/contracts/layout-generation.md`. We
harden it **from the mid outward** — mid band/crossing → the **frontline / build-zone interface** → hub →
spawn → wool lanes → realize/emit — because each layer's shape constrains the next: a settled frontline+
bridge is what the hub, spawn, and wool-lane generation dock onto. Working doc: `docs/handoff-composer.md`.
Every generated board is judged against the **authored example set** (`tools/seeds/teaching/`), not the
rules alone — where a board misfits, suspect a rule that is looser than the examples (the current theme).

Shipped so far (`FEATURES.md`): closure/envelope + team-unit grower, the clean-form (CT1) mid band, centre
islands (CT11 order-2, incl. 10×10 pairs), the MD6 stone grid, the wide frontline (FR6), isolation cuts, and
BZ6–BZ9 build-zone discipline. The layers below are what remains.

> **▶ Priority within this focus — the three upstream tracks.** The author-facing payoff everyone wants to
> reach (the lane / hub *styles* — G37 / G45) sits **downstream** of three tracks that must land first:
>
> 1. **Interface layer (G39 / G40 / G41)** — the frontline↔build-zone must **interlock** before the hub,
>    spawn, or wool lanes can dock onto a settled frontline. G39 is the crux and the most code-grounded (the
>    band just needs the CT7 corner-snap the stones already use); a shifted or too-thin dock poisons every
>    layer above it.
> 2. **Authoring lever (G46 / G47 / G48)** — `mirror = none` + the `connector` piece + the taxonomy resort are
>    what let the author produce the **teaching templates** G37 / G41 / G45 are blocked on. Without them the
>    lane-style work stalls on missing inputs.
>
> The **spawn + wool-room dock now exists** (G49, `FEATURES.md`): the composer carves each lane's terminal into
> a real `spawn` / `wool-room` room the plain lanes dock to — the anchor the spawn/wool-lane grammar (G37 / G45)
> attaches to. What remains upstream of the lane-style payoff: **G39** (the frontline↔build-zone interface, still
> the crux) and **G46–G48** (the authoring lever for the inputs), so they come before the lane track proper.

**Mid — band & crossing (variations remain)**
- [ ] **G38 — Multiple / parallel mid bands + their variations.** The composer ships only the CT1 clean
  form (one band spanning the axis). Add **two-or-more parallel bands** (FR7, rot_180-only, variable-length)
  and the authored **variations**: a **hole in the build zone**, a **stepping stone between the dual bands**,
  the two-sided plaza (`big-board-wool-two-sided-plaza-parallel-mid`). Each band needs its own dock + hop
  arithmetic; the fan/merge must keep the bands distinct. Unshipped feature, not a bug (flagged 2026-07-05).

**Frontline & the build-zone interface — the current failing layer**
- [ ] **G39 — Frontline ↔ build-zone tetris interface (corner/edge match).** *New failure class.* Band and
  frontline must **interlock like tetris pieces**: touching edges coincide at shared corner/edge lines and the
  band **docks the full frontline face** (equal width). Today the band's lateral extent is sampled as
  continuous fractions (`tL`/`tR`) between a minimal 2-cell interface and the hull, snapped only to *flush-or-
  full per edge* (BZ8/BZ9) — so a band can land **flush on one edge, short on the other** (`gen-p30-t2-
  rot_180-s1`: same width but **shifted**) or **narrower than the face it docks** (`gen-p20-t2-mirror_z-s1`:
  build zone **too thin**). Fix: snap band edges to the frontline's **CT7 corner lines** (the stones already
  do this via `CandidateColumns`; the band does not) and require full-face coincidence, not a minimal
  interface. The crux the rest of the interface work sits on.
- [ ] **G40 — Enclosed dead-space / hole-size cap (frontline + lanes).** *New.* rot_180 twin frontlines
  extrude **overly long** to spend the land budget (`gen-p30-t2-rot_180-s7`: twin **35-block** frontlines,
  confirmed in the trace). In the authored set the hole enclosed by hub + frontlines + build zones is
  **~10×10, occasionally 10×20 — never 10×40**. Govern the **hole size** (cap the frontline extrusion / hub
  gap so the enclosed hole stays in that band). Generalize the same bound to **any enclosed dead-space an L/U
  lane wraps** — the `gen-p30-t2-rot_180-s7` wool-lane U encloses a giant empty square; a lane must not wrap a
  big void. Route surplus budget to a wider mid / more routes, not a stretched frontline or a void-wrapping
  lane (middle-out budget; the length driver is **G44**).
- [ ] **G41 — L / Z frontline-hub compositions + hub-shape variety (HB4).** The hub is **always one square**
  and the authored **L- and Z-shaped** frontline↔hub combinations are not generated. Implement HB4's L/Z
  composition and multi-piece hub shapes. Blocked partly on the author's frontline/hub teaching set.

**Hub → spawn → wool lanes (follow once the interface is settled)**
- [ ] **G42 — Spawn docks to a piece, never submerges.** *New.* The spawn is meant to **dock** (abut) its
  neighbours; on some boards it is **fully engulfed** by the surrounding pieces (`gen-p20-t2-rot_180-s7` —
  which also surfaced the first accidental terrain hole). Enforce spawn-as-dock (SP): a spawn touches by a
  readable edge and is never interior to the merged land.
- [ ] **G44 — Budget→length decoupling (traced root cause of the lane bloat).** The grower's area gate
  rejects any unit under 80% of `LandPerTeam`, and its only real spend-vocabulary is **longer lanes** — so a
  big budget is absorbed by length, not structure, despite the docstring's "surplus spent structurally, never
  by stretching a lane." Trace (`gen-p30-t2-rot_180-s7`, `LandPerTeam` 5500 blocks² / 220 cells → 217 spent):
  a **95-block L** spawn lane (45+50) and a **95-block U** wool lane (50+45) that wraps a giant empty square,
  putting the wool out of the playable area as a pointless deep dead-end (a defender just holds the mouth).
  Fixes in order: (a) **cap absolute lane lengths** to the authored norms — spawn ≈ 2-3 pieces (20-30 blocks),
  wool lanes bounded, not 95-block chains-of-chains (LN2's 50-block cap is *per collinear chain*, so an L/Z
  stacks two); (b) route surplus into **width / plaza / more pieces** (the richer vocabulary of G37/G41), not
  length; (c) **re-examine the budget** — whether `LandPerPlayer` over-scales past ~p16 and whether the area
  gate's lower bound should relax so a compact unit needn't hit the full target ("too much budget for
  lanes… the whole map").
- [ ] **G45 — Third wool: rarer, and placed as a real route.** A third wool is sampled at **40%** for ≥16
  players and **always** built as a 2-cell dead-end straight back beside the spawn lane (`wool-lane-c`;
  `gen-p20-t2-rot_180-s13` — the wool squeezed next to the spawn). Reality: 2 wools common, **3 rare**, and a
  genuine third wool sits as **its own route**, not crammed against the spawn. Lower the rate and add real
  3-wool placement patterns — needs teaching examples (the current set has none).
- [ ] **G37 — Lane-archetypes track (lane shapes · connections · hub shaping · alt entries).** The real lane
  grammar: authored **lane archetypes**, **what connects to the frontline**, **how hubs shape** (today the hub
  is a dumb square everything smashes into — G41), and **alternative entry points** to a lane (a long dead-end
  is pointless without alt routes — the defender just holds the mouth; not formalized yet). "Lane-heavy is bad"
  is a defect, not an archetype to sample (see the `composer-lane-archetypes-future` memory); the budget-driven
  over-long lanes it produces are traced to **G44**. Blocked on more teaching maps; sequenced **after** the
  interface layer (G39/G40).

**Realize & gate (plan → loadable, validated seed)**
- [~] **G32 — Composer realize + gates.** Skeleton landed (`FEATURES.md`); the `spawn` / `wool-room` piece
  roles now land too (G49, `FEATURES.md`). Remaining: **G32-C markers/heights/walls** — SP3/SP4 spawn
  (facing absolute, raised), SP7 iron, WL5 stepped approach climb, EL1 palette (base 9, step 2, all-odd),
  ST4 walls, EL6 (the rooms are flat at the base surface — the elevation pass raises them). **G32-D gates + goldens + emit** — `PlanValidator` zero-errors with zones present,
  `FannedGraph` full traversability, stat envelopes vs `seed-stats.md`, `plan.json` loadable in `/plan`,
  fixed-RNG goldens under `tests/`. p5/rot_90 stays a known limitation until **G35**.
- [ ] **G43 — Composer ↔ example-set conformance metrics.** Turn "does it match the examples?" into
  **measurements**, not eyeballing: over a seed sweep, measure hub-hole size distribution, band↔frontline
  edge-coincidence / width-match, frontline extrusion length, mid-piece share, and island count, then assert
  the envelopes the authored set (`tools/seeds/teaching/`) implies. The gate that would have caught G39/G40
  before the gallery; feeds the G32-D goldens.

**Unblock (deferred infeasibilities)**
- [ ] **G35 — Composer-side buffer reservation (unblocks p5 / small rot_90).** Have the composer author
  buffers/allotments during generation — reserve a ≥1-cell border on rot_90 boards so the quarter-turn image
  can't self-collapse, hold spacing on small boards — to unblock p5 (BZ6 + spawn ≥2×2 over-budget at 325
  blocks²) and p5/t4/rot_90 (interior-overlap self-weld). Teaching material + a `layout-rules.md` amendment
  first. Never fix p5 by enlarging the board (re-triggers the LN2 arm stretch).
- [ ] **G36 — Residual composer polish (from the B2 review).** What's left in
  `docs/composer-review-findings.md` after the mid-feel slice shipped and (2)/(3) moved to G37/G41:
  **(1)** confirm the rot_180 mid-band asymmetry (`p30-s7`/`s13`) is a real off-centre band vs a render
  artefact; **(4)** cap spawn-lane growth (`p30-s13` over-grown L); **(6)** frontline-**count** variety (not
  every board double-frontline).

**Authoring tooling — the teaching-material lever (plan editor)**
Several tasks above are blocked on more authored teaching maps (G37 / G41 / G45). These plan-editor primitives
are what let the author design them — especially reusable lane / spawn **templates**.
- [ ] **G46 — Plan tool: `mirror = none` symmetry mode.** Add a `none` symmetry (order 1, no orbit axes) so
  the author can design a **single freeform unit** — a wool-lane / spawn-lane shape — without the mirror
  fanning fighting the design. Touches `Geom.Symmetry` (order 1 / empty axes), the editor mirror preview
  (`setAuthorMirror` draws nothing), and the plan globals / compose envelope accepting `none`.
- [ ] **G47 — Connector technical piece (attachment-point annotation).** A new non-generating role
  `connector` alongside `buffer` (extends `PlanRoles.Annotations`): marks "**other structure attaches / overrides
  here**" — a hub, a frontline, the mid. Produces no terrain, no graph/export effect. With `buffer` (reserved
  spacing / holes) it lets the author build reusable **templates**: a wool-lane template = the lane pieces + a
  `connector` where it docks + `buffer`s for spacing.
- [ ] **G48 — Piece / marker taxonomy restructure + UI resort.** Regroup the palette (today: pieces / build /
  markers) into three labeled kinds — **true pieces** (`piece`, `spawn`, `wool-room`, `build`), **true markers**
  (`wool`, `spawn`, `iron`, `wall`), **technical pieces** (`buffer`, `connector`) — and re-sort the plan editor
  UI to match. Reconciles the model split (build is a `PlanZone`, wall a `PlanWall` list, buffer/connector are
  annotation roles) with the author's conceptual grouping.
