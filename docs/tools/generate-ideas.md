# Generate — idea & backlog pool

The Generate tool's condensed idea/roadmap pool: where deferred design ideas and known-but-unaddressed
failure modes live, one idea (or failure mode) per few lines, grouped by theme, **ids preserved
verbatim** — an id here is never reused, even one marked `(obsolete)`. Pull an idea back onto the active
board (`TODO.md`/`BACKLOG.md`) by id when it becomes the current focus. See `docs/tools/generate.md` for
the current architecture and locked vocabulary, and `docs/tools/generate-rules.md` for the frozen rule
law these ideas amend or extend.

This file merges two sources. First, the layout-generation idea backlog: the task board had accumulated
~40 open G-tasks looking far ahead — many describing machinery that no longer exists after the old
grower path was retired and the box pipeline became *the* composer — condensed here, one idea per
line-or-three (the full original task text is in git history under `BACKLOG.md`/`TODO.md`). Second, the
still-live tail of the constraint-taxonomy review: the observed failure-mode list and rule-kind audit
that open backlog items still cite. Everything else that review covered — the rule-kind vocabulary, the
edge taxonomy, the offer model, targets, the type sketches, the prompt templates — shipped into
`docs/tools/generate.md` and is not repeated here.

Status markers: *(obsolete)* = described the retired grower path and is settled or moot; *(partial)* =
part landed, the rest is the idea.

## Retired with the old grower *(obsolete — recorded so the ids stay accounted for)*

- **G41-A** — route the production arms through `BoxFiller`: done by construction — the filler is the only
  fill path now; the bespoke `SolveDepth`/`SolveWidth`/`spawnLen` solvers were deleted with the grower.
- **G63-C** — the switch itself (fill the partition, retire the grower, re-baseline): **done** except the
  C.2 richness residue, which lives on below (hub forms, floors, invariants).
- **G44 / G87** — budget→length lane bloat + the fill-to-target directed repair: the growing/stretching
  machinery they policed is gone. The surviving principle: surplus budget buys *structure or count, never
  length*; a box grows only when below its family minimum.
- **G45** — the parallel-lane third wool anti-pattern: the `wool-lane-c` squeeze it banned no longer exists;
  the surviving half (a third wool as a *real route*, e.g. in a hub bay) merges into G41-D + G113.
- **G42** — spawn submerged into the merged land: the box path docks by construction.
- **G36** — residual old-composer polish (off-centre band, over-grown spawn L, frontline-count variety):
  the first two are structurally impossible now; frontline-count variety is a live evaluator/menu idea.
- **G39 / G40** — band full-face dock + hole-size caps: delivered by the hull-exact flush band and the
  WL8/CT8 gate terms; residual question (cap the extent of a bay a frontline wraps) folds into the
  frontline-form menu work.
- **G38** — parallel mid bands: superseded by **G116**'s split-band wording below.

## Mid enrichment (the crossing vocabulary, back on the box path)

- **G116** — richer mids: stone rows, the centre island (single/pair), the **split band** (two build bands
  around a centre hole — the deliberate two-lane mid), depth variation (the ≥20-player 30-block deep
  single). All re-enter as `CrossingDesign` forms; the retired `SampleCrossing` arithmetic (hops 10..20,
  sum 30..60, CT7 column alignment, the MD6 lateral grid) is the reference design, in git history.
- **G100** — holed frontline forms (P, two-U-on-I): needs the "where does the mid meet a loop" face rule first.
- **G81** — the declared-bay scythe via elevation (a flush host sealing the bay is legal only once height
  enforces the approach); parked until the elevation pass exists.

## Allocator & shape richness (the G63-C.2 residue and its follow-ons)

- **C.2 residue** — the deeper Z; the scythe (needs shape-relative bay docking, G80); the spawn L; hub-form
  richness (L handedness, Double-hole at width ≥9, a real rotation for `mirror_x`, an L hub coexisting with
  a frontline); the hub-floor clearance refinement; CT1/LN2 invariants by construction (10-block image
  clearance + the 50-block chain cap baked into placement). 4-team `rot_90` and `mirror_x` support return
  through this work — the composer currently supports 2-team `rot_180`/`mirror_z`.
- **G104** — instrument the budget: what should the two-currency budget produce per box kind at each size?
- **G122** — the composer decision trace: optionally record every sampled choice and rejected dock during
  a compose, keyed by the request descriptor — the real backtracking tool for "why does this bucket look
  the way it does", better than re-running and watching. Feeds the studio's detail dialog later.
- **G105** *(partial)* — bigger/better hubs: the per-piece width knob, the asymmetric ring, a raised depth
  cap, form→size fit.
- **G106** *(partial)* — the observed seat/emit failure modes (this file's failure-mode section, F2–F5):
  flush lanes at branch-hub run ends, the tiny-stub fallback, twin-leg equality, square-on-square.
- **G112** — P-aware neighbour placement so the P hub survives seating.
- **G113** — restore the third wool (near-extinct since the seat gap; bias the spawn/doubling toward the
  wide edge). The wool-count distribution test in `ComposerTests` re-asserts 3-wool occurrence when this lands.
- **G114** — the along-axis mirror so a tight-hub L bends back instead of reverting to I.
- **G107–G111** — the taxonomy audit's five moves: `demand` as a live kind (G107), the `mix` kind (G108),
  budget ladders as derived targets (G109), WL7 separation by construction (G110, the traversal-spread
  half), the frontline offer decisions moving to the allocator (G111 — joint-vs-several is FR6, currently a
  coin flip in the filler).
- **G123** — the shifted partial-face frontline (the funnel dock; authored exemplar
  `shifted-u-frontline-attach-g-hub.plan.json`). Relax the frontline demand from the pinned full hub width
  (`Demands`' `faceWidth = max(w, hubV)`) to a sampled width + shift along the front edge, allow lateral
  overhang past the hub bbox (the wools' seat-and-shift, on the frontline), and add the **spanning dock**:
  one face covering run + bay-mouth + run with a ≥cw contact patch per shoulder — sealing a bay-fronted hub's
  bay (G/U/L) into a declared hole. Unblocks G-hub+frontline coexistence outright (today the single-run seat
  fails and the form falls back to the rectangle), and needs `FrontFacesSymmetric` relaxed from per-face
  mirror symmetry to hull symmetry (BZ9's real requirement) so shifted fronts survive rot_180. The payoff:
  the frontline as a flow funnel — two onward routes at asymmetric cost around the sealed hole. Subsumes the
  FR6 half of G111. Second exemplar `shifted-u-frontline-attach-hole-hub.plan.json` (ring hub, donut + side-tuck
  wools): on a solid-front hub only the scalar half is needed (width + shift + overhang + hull guard, one
  contact patch on one run) — the minimum viable slice; the spanning dock is specific to bay-fronted forms.
- **G124** — what the seat-separation law measures: today `TooClose` inflates **box envelopes**
  (corner-inclusive), so a donut's void margins make it over-reject placements whose emitted **terrain**
  keeps the gap (the hole-hub exemplar's donut box passes 1 cell from the spawn box while its terrain keeps
  2). Decide the measurand — envelope, emitted terrain, or terrain-with-margin — and align `SeatOverhang`/
  `SeatInRuns` blocking on it.

## Vacancies, fragments, targets

- **G41-D** — vacancy publishing + spawn-in-hub-bay: fills publish claimable negative space (a U-hub's bay,
  a ring's hole); consumers claim it (the third wool / the spawn in the bay).
- **G63-D** — the generic label-inheriting fragment pass + `GrowthOrder` strategies + vacancy allocation.
  The isolation cut returns here as a slot-aware move (a cut severs a `run`/`bar`, never a `room`/`entry`) —
  its old implementation is in git history.
- **G97** — close the offer↔derive mirror (derived runs/kinds/holes match the offers' intent).
- **G98** — `ComposeTargets`: prescriptive per-request fields (frontline runs, mid form, hub form), sampled
  when unset, held + verified when set. **This is the natural backend for the studio integration's filter
  controls** (G117 on the board).
- **G109** — (also listed above) the budget ladders fold into `ComposeTargets`.

## Evaluator long tail

- **G60** *(partial)* — the soft-term leftovers (cramming, approach count, height terms), the
  keep-lowest-scoring hunt loop, the ranking harness + minimal-pair negatives.
- **G69** — deriver primitives for dense mids (crossing-corridor read, rotation-that-means-rotation, robust
  stone classification) — the prerequisites of the cramming term. The stalemate probe
  (`tools/compose/stalemate-probe.cs`) is the first cut of (b).
- **G66 / G67 / G68** — rule visualization (illustrated rule catalog + reject inspector), fill-time slot
  invariants, evaluator-side slot terms.
- **G43** — the composer↔teaching-set conformance sweep (aggregate soft distance per term).
- **G127** — the flow graph: junction/lane-chain derivation + route signatures + the first flow terms.
  Revives G24's already-designed substrate (plan.md §2: mouths as intervals, corridor extrusion,
  junction regions = ≥3-corridor intersections, lane chains between them — areal, decomposition-free) as
  the derive side of a **third mirror**: the emit side assembles the intended story from the vocabulary
  (each form a known mini-graph — donut a cycle, ring hub a cycle with tangent runs, twin frontline two
  parallel band edges — glued at the joints), and a mismatch is itself the finding (square-on-square: the
  story says hub + frontline, the mask says one many-mouthed blob). Per wool a **route signature** — the
  fork-degree sequence band→wool (`2⇒1⇒2⇒1`) — one legible token for the evaluator, the G118 verdict tags
  (which co-evolve with these terms: plaza · no-funnel · uncatchable-runner), and the B21 agent. First
  terms, one per observed failure: **plaza** (junction area vs mouth widths / corridor coverage — G69
  kin), **spread** (wander area along the route), **interception slack** (runner wool→home vs defender
  spawn→cut-off on the fanned graph — uncatchable when every return route is negative at every node),
  **redundancy** (disjoint wool↔band ways, generalizing the middle hole's parallel-ways). pgmlogger
  traffic (G33) is the eventual ground truth the model is validated against.
- **G75** — score a marker whose stamped structure cannot paste.

## Realize & world

- **G32-C** — structures & elevation (the "second generator": stairs, climbs, heights, walls). The missing
  soul once layouts read valid-but-flat.
- **G32-D** — gates, goldens, emit: freeze fixed-RNG goldens *after* the churn settles. (The author has
  deprioritized golden stability — layouts are expected to keep evolving — so this is a
  release-discipline idea, not a near-term gate.)
- **G31** — scaled structure presets (stamps must fit tiny and huge maps).
- **G34** — theming & styling rules (material palettes + prop stamps).
- **G29 / G24** — climb profiling on lane chains; junction-region derivation + hubs overlay.
- **G33** — traffic ground truth from pgmlogger parquet (flow priors to score candidates).
- **G82 / G83** — entry widening for Z along its bar interface; wool-approach budget law (per-slot caps /
  total path length). Reworded for the box path: the knob is `AttachmentWidth`, the law binds at allocation.

## Docs & hygiene

- **G64** — the `generate.md` doc pass (reconcile with shipped code; §2/§4 still describe the old
  order of operations in places).

## Marker & objective knobs (plan editor)

- **G76** — the marker inspector exposes a structure's knobs (destroyable styles, core size/shell,
  wool colour) instead of silently defaulting.
- **G77** — `bedrockCentre` is a stamp no authoring path can reach: thread it through or delete it.

## Observed failure modes & rule-kind audit

Author-observed over the unit-gallery seeds, each verified against the code and a 4-preset ×
200-seed probe of `Allocate → Fill` (small/mid/big/huge as in `tools/compose/unit-gallery.cs`;
the probe is `tools/compose/seat-probe.cs` — re-run it to re-measure this list).
This list lives here — the live review record — rather than in a new document. An entry **leaves
this list when its fix lands** (the commit references it); the fix work is board task **G106**.
Each entry names the rule kind (`docs/tools/generate.md` §1.14) the fix belongs to, because most of
these are not bugs in a mechanism but **missing rules** the taxonomy has an address for.

**F1 — Neighbour lanes abut: no seat gap. (Spawn/wool part fixed.)** A spawn and a wool box could
share a boundary — no gap between the two lanes. Probe (pre-fix): 31/37/38/**99** units per 200
(small/mid/big/huge; the huge spike was the 3-wool plans). Mechanism: `SeatInRuns` packed seats
against occupied intervals with zero spacing, and `SeatOverhang` rejected only *overlap*, not
touching; the corner inset going to 0 removed the last incidental spacing. **Fixed:** the seat step
now holds an **inter-seat gap = the map lane width** (w2 = 10 blocks, w3 = 15 on wide boards). Each
seated spawn/wool projects onto the edge being seated (`ProjectOntoEdge`) as a forbidden along-interval
`SeatInRuns` inflates by the gap — one pass covering same-edge abut *and* the adjacent-edge corner
meeting (the projection's along + perpendicular conditions reproduce `TooClose`); `SeatOverhang` filters
placements by the same `TooClose`. Probe now: spawn/wool abut **0** across all presets, no-alloc unchanged
(0/0/0/0). Kind: a **law** (lane spacing), applied as a **demand** in the seat step, enforced by the seat
**gate** — distinct from the corner law (corners stay 0; the mass-level pinch gate owns them).
*Consequence:* huge's optional third wool doubles onto an occupied hub edge, which a cap-6 hub cannot hold
with the gap (it only ever fit by touching), so it is **dropped** — huge drops to 2 wools until the hub
caps grow (G105). *Remaining under F1:* a **frontline↔wool** abut still occurs (37/44/58 on mid/big/huge,
none on the frontline-less small) — deliberately left to the build-zone rule (BZ, band-vs-wool), not the
neighbour gap.

**F2 — Lanes flush against a branch hub's legs (frontline-less units).** On L/U hubs without a
frontline the wool/spawn lanes can sit flush against the legs' walls. The hub's remaining free
surface is exactly where build regions attach in later stages, so a build region would land
touching the lane. Mechanism: a dock flush against a **non-corner run end** — a run ends
mid-edge only where the body's mass stops, so that end is a leg's wall and gets no inset by
design; the extreme is a **leg-tip run** (width exactly `cw`) the dock consumes end to end.
Probe: **23/3/1/1** units per 200 (small/mid/big/huge), and **every one of them a branch hub** —
so the attribution is exact but the frequency is a small-board effect (23 of the small preset's
37 branch hubs, ~⅔; near-zero elsewhere). Context: branch hubs are effectively
frontline-less-only today — with a frontline, the branch form's front free run (an arm tip,
`cw`) cannot host the `faceWidth` demand, so the allocator falls back to the rectangle (probe:
37/200 branch hubs on the no-frontline small preset vs 4–6/200 on the frontline presets), which
is why F2 tracks the branch-hub population rather than the board size. Fix direction: a ≥1-cell
margin between a seat and a **mass-adjacent run end**; a tip run narrower than `along + 2`
margins refuses the dock (demote / re-seat). **Cost of that rule, measured: it would refuse
166/495 · 159/505 · 246/505 · 195/673 of all current docks (30–50%)** — far more than the 27+2+1+1
it fixes, because the `along + 2` test also rejects every dock on a *full-edge* run of a small
hub. So the margin must be required only at **non-corner** run ends, not at every run end, or the
rule cascades into re-seats and allocation failures on the small preset. Kind: a **law** — the
build-surface clearance ("a cell between a lane and attachable hub surface"), the compose-side
twin of the room-clearance guard (canonical now in `docs/tools/generate.md` §1.13).

*Adjacent mode the probe separates — measured, and ruled **not a defect** (author).* A lane can
cover a whole hub side end to end, flush at both **box corners**: **101/79/29/37** units per 200,
an order of magnitude more common than F2 proper, and **always a wool** (273/273 whole-side docks
over the four presets; never a spawn, whose length runs outward so it only ever abuts `w` cells).
It is permitted by design — the corner law sets `CornerClearanceCells = 0` precisely so "the
neighbours may use the hub's full edge (which the side-tuck wool and the wide frontline face
want)".

The full dock is **fine in itself**. It reads badly only in a narrow sub-case: when the wool lane
edge and the hub edge run **parallel the whole way** and the two masses combine into a flat slab —
**no bay or notch** formed at their join. Where the wool's own body articulates the join (a w2 lane
widening to its room, the common case — small seed 4) there is nothing wrong with it. The fix, if
that sub-case is ever worth chasing, is a **small frontline** on small boards, not a spacing law.

Not scheduled, and deliberately not an F-entry: it is a **small-board** artifact, and the small
board is low-value — at 700 land the hub is always 4×4 while the smallest wool footprint is
already 4 (side-tuck `I`, `cw + rd × 2·cw`) or 5 (`L`), so the seat is forced, not chosen. It
fades as budgets rise (101 → 79 → 29 → 37) exactly as hubs outgrow the wool minimum. Small-board
layout issues are expected for now.

**F3 — The centred-stub single frontline (the "T").** Two-piece frontlines are always a T: a
tiny stub (reach − cw = 2 cells) centred on the bar — build regions attach poorly around it; a
proper L (arm at an end) or the twin would read better. Mechanism: the single is **centred by
construction** (`SpineArms(cw, [(w−cw)/2], …)` in `FrontlineBoxEmitter.BuildBody`). And the T
*dominates*: probe 126/164 mid, **164/164 big, 161/161 huge** — at `w = 3` the twin needs
spine length ≥ 2·cw = 6 with the arms then adjacent, so it **never fits** the cap-6 face and the
form menu silently collapses to the T (the Bar is never chosen — 0/489 fronts — because
form-answers-form reserves it for branch hubs, which fall back when a frontline exists, F2).
Fix direction: an **arm-placement knob** on the single (end-arm = an L frontline), a twin that
fits `w3` (wider face or an adjacent-arm guard with real separation), and a menu-collapse
**fact** (when only one form fits, that should be visible, not silent). Kind: **menu** content +
**knob**; the collapse is a missing fit-gate fact.

**F4 — Twin legs always equal.** The twin/U frontline's legs are always the same length; an
optional per-leg offset (≤ 2 cells, ~10 blocks, sometimes) would break the symmetry pleasantly.
Mechanism: `BodyEmitter.SpineArms` has one arm length for all arms (`h − cw`). Wrinkle: the face
offer is read off the **box's face edge** (`BoxInterfaces` runs), so a shortened leg's tip
leaves the box edge and its offer would silently vanish — per-leg lengths need per-arm support
in `SpineArms` **plus depth-aware face offers** (or per-tip interfaces). Kind: a **knob** (arm
length; `ClassifyBody` must still read `SpineArms(2)`) + a small offer-model extension.

**F5 — Square hub against a square frontline.** The flat square-on-square pairing still reads
on the mid/big/huge presets. Probe: this is **not** the Bar (never chosen) — it is the near-solid
small forms: at reach `w + 2` and `cw = w` the single-T is a solid bar with a 2-cell stub,
reading almost as a Bar flush on a rect/ring hub (rect+single-T = 109/164 mid fronts;
ring+single-T = 146/164 big). Root: the frontline's **reach and proportions do not scale** with
the board — `frontReach = w + 2` regardless of budget — so on big boards the front is a sliver
whose form barely matters. Fix direction: scale reach with the budget (G104's territory), open
the deeper forms (holed P / two-U-on-I — G100; the F3 L-form single), and make hub-form ×
front-form a real **pairing menu** (today one hard-coded preference in the filler). Kind: budget
**fact** + **menu**. *Small note (author, low weight): a thin front is not always wrong — an
intentional narrow choke that widens again is sometimes wanted on **small** boards (not big
ones), so the eventual rule is per-size, not a blanket minimum. A possible later home is the
fragmentation step (cutting voids into a box shape rather than only converting land→build) —
parked; the rule-based path stays preferred.*

**F6 — The donut wool is a 2:1 sliver.** *(Partly fixed — the `woolAtEnd` half landed; the root
below remains.)* The donut approach reads stretched. Probe was: every donut box exactly **10×5**
(or 5×10) — the min box. `MinBox(Donut)` chains stub (`cw`) + ring (`3·cw`) + trailing wool room
(`rd`) along one axis at minimum height, and the allocator sizes rich wools at exactly the min
box, so the donut was *always* the sliver.

*Landed:* the trailing `rd` is only the **non**-`woolAtEnd` room — the corner-integrated wool sits
inside the ring's own span, costing no width past it — so `MinBox`/`Need` no longer charge it, and
the allocator now picks the corner wool for donuts (`DonutCornerWoolChance`). Those donuts are
**8×5** (aspect 1.6, area 40) instead of 10×5 (2.0, 50); the probe now reports both.

*The other cheap route is closed:* growing the box toward a preferred aspect **cannot be funded**.
Measured per-wool budget share is **4–6 cells (small), 13–24 (mid), 29–44 (big/huge)** against a
donut minimum of 50 — every rich wool is already over its share at its own minimum, on small by
10×. There is no headroom to grow into anywhere, so the only lever that reshapes a rich wool is
its **minimum**. (That gap is itself a finding for **G104**: the two-currency budget badly
under-funds rich wools relative to their footprints.)

*Root, unchanged (author):* the sliver is not *required* — every internal dimension of the shape
(legs, bars, even the hole) is keyed to the **one lane width picked up front** for the whole map,
but in reality widths mix — some areas are 3 wide, some 2 — and **the generation cannot express a
per-area / per-piece width yet** (today's vocabulary is the map `w` plus the single wool-lane
override). Decoupling the non-lane dimensions (the hole, the ring bars) from `cw` shrinks the
donut further. Fix direction: the per-piece width as a **knob** in the emitters (a vocabulary
addition — the same knob **G105**'s asymmetric ring needs). Kind: a missing **knob**.

*(**F7 — the clamp's void too deep** — fixed and removed. `MinBox(Clamp)` had inherited the U's
`2·cw + rd` height, but the clamp has no crossbar to clear: its legs only run from the wool down
to the mouth, so `cw + rd` does it. Void depth below the wool went **4 cells → 2**.)*

### Rule-kind reality check (G103)

The rule-kind audit (`docs/tools/generate.md` §1.14) asked for: with the shapes now placing, do
the placement rules the allocator actually applies map onto the declared rule kinds, or are some
ad-hoc policy sitting in the wrong layer? Audited against `docs/tools/generate.md` §1.13/§1.14 and
`docs/tools/generate-rules.md`, with measurements over 400 seeds × 4 presets.

**The verdict.** The *shape-level* rules map cleanly — offers, fit gates, vetoes, gates and knobs
all sit where the taxonomy says, and the corner clearance / full-mouth / form-fallback mechanisms
are the right kinds in the right places. Two things are wrong, and neither is a rule in the wrong
layer: **one declared kind has rotted out of the code**, and **a whole layer of what the allocator
does has no address in the taxonomy at all**. Ungrounded constants are the third theme: most of the
allocator's ladders trace to no law.

**One kind has no live representative: `demand`.** Every exemplar in the §1.14 table was checked
against `src/`. Fourteen of sixteen are live. `ComposeTargets` is absent but *declared pending*
(§1.14 says its type lands with G98) — fine. `FamilyDock.EntryDemand`, the exemplar for **demand**,
is **gone**: G63-C.2 retired the dual-host `FamilyDock`/span machinery when it redefined the
clamp, and the table still points at it.

So the one kind with nothing behind it is `demand` — one half of the taxonomy's *first* load-bearing
distinction ("**demand vs offer** is the direction of the arrow"). The other half is richly built
out (`EdgeOffer` appears in seven files). The asymmetry is real, not cosmetic: what survives of the
demand concept is `TeamUnitAllocator.Overhangs`, a private predicate that states it **by exclusion**
— `family is L or Donut`, i.e. "these do *not* need two hosts". The clamp's two-entry requirement,
the taxonomy's own exemplar, is now an implicit negation inside the allocator.

**The missing address: the structural sampler.** Most of `TeamUnitAllocator` is not shape-level
rules at all. It is the layer that decides, from a budget, **how many** wools, **whether** a
frontline, **how big** a hub, and **how often** each shape appears. The taxonomy has no kind for
it. It shows up in two faces:

*(a) The mix — a steering distribution.* Nine weights (`BentWoolChance` ·25–·5, `DonutChance`,
`StapleChance`, `ClampAdjacentChance`, `DonutCornerWoolChance`, `SideRoomChance`, `RingChance`,
`ThirdWoolChance`, `NoFrontlineInN`) plus six uniform picks (spawn side, staple family, spawn size,
hub form, seat position, overhang placement). None of the kinds fits. `menu` is a *set* — "a
generative allowlist, what may be chosen" — and carries no frequency. `band` carries a distribution
but is explicitly **descriptive and advisory** ("bands never steer"), which is the opposite of what
these do. A weighted generative distribution is a real, distinct kind and should be named — call it
**mix**: it steers (unlike a band) and it carries frequency (unlike a menu).

Naming it raises the question worth having: should a mix be **authored**, or **derived from a
band**? LN1 is the case in point — it records a measured corpus frequency (width 10 ×81, 15 ×15),
which is exactly a band over the same choice the allocator makes with a hard threshold.

*(b) The ladders — budget→structure thresholds.* `WideLaneLand`, `FrontlineMinLand`,
`TinyBoardLand`, `FullTeamPlayers`, `HubCapCells`. These are not facts (nothing is read off
geometry), not menus, not fit gates. The closest declared kind is **target** — "a per-request,
prescriptive constraint a compose holds and verifies" — and on reflection that is what they *should
be*: they are per-compose prescriptions derived from the envelope. They differ from a target only in
that nothing verifies them afterwards. **File: the ladders become derived `ComposeTargets` (G98).**

**The trace to law — which values are grounded.**

| allocator rule | law | verdict |
|---|---|---|
| `WoolLaneCells = 2` | LN1 ("the lane to the wool is simple, w2") | **grounded** |
| `w = 3` above `WideLaneLand` | LN1 (10 base, 15 larger; corpus 81:15) | values grounded, **threshold invented** — LN1 states a *distribution*, the code makes it deterministic |
| `WoolLengthRatio = 3` (max depth 5 cells = 25 blocks) | LN2 (20–50 before a junction/dead end) | **grounded** on the lower bound; the 50 cap is unimplemented |
| `CornerClearanceCells = 0` | the mass-level corner law | **vestigial** — enforcement moved to `Cells.HasDiagonalPinch`; the constant now documents rather than acts |
| `faceWidth` on the frontline joint | FR6 (split vs wide, band docks flush) | right kind (**offer**), law partially served |
| `RingFitCells = 5` | geometry (`BodyEmitter.Ring` guards it itself) | right kind (**fit gate**), **duplicated** in the caller |
| `HubCapCells` 6/5/4/3 | none — HB1 constrains *width*, not box size | **ungrounded** |
| `FrontlineMinLand` (a unit may have no frontline) | none | **ungrounded** |
| `TinyBoardLand`, `FullTeamPlayers` (wool count) | WL6 gives 1–3; G8 couples players↔land | range grounded, **thresholds invented** |
| the six shape-mix weights | WL8 governs wool approach routes (single choke default, alternatives sometimes) | **ungrounded** — the donut *is* WL8's alternative-route case, but 0.25 derives from nothing |
| `RingChance`, `ThirdWoolChance`, `NoFrontlineInN` | none | **ungrounded** |

**Laws the placement does not honour (measured).** Measured over 400 seeds × 4 presets, on the
**placed rooms** (not the boxes), in blocks:

- **WL7 — wool↔wool separation. Systematically violated.** The law records a corpus of 46–143
  blocks with a working minimum ≈45. The composer produces **min 21, median 41–55, max 87–98**, with
  **31–53% of all wool pairs below 45** (small 100/189, mid 74/200, big 82/200, huge 212/690). The
  closest pairs are less than half the corpus floor, and the whole distribution is compressed — the
  composer's *maximum* (98) sits well below the corpus maximum (143). *Caveat (G1): the plan is a
  mini layout and grid-born distances are resolved downstream by scale + roughen, so an absolute
  block comparison is not decisive on its own. The distributional argument survives it — this is not
  a constant offset, it is a narrower spread sitting at the corpus floor.*
- **WL2 — wool↔spawn ≥ 20. Was violated only on huge; now fixed.** small/mid/big always held (min
  exactly 20). Huge showed **111/930 pairs under 20, min 12** — the third wool doubling onto the spawn's
  own side (`AssignWools`), the same construction behind the F1 spike. The seat-step gap (F1 above)
  resolves it: that third wool can no longer seat within the gap of the spawn, so it drops rather than
  cramming.
- **WL6 — 1–3 wools, each on a distinct lane. Holds.** No unit places two wools on one hub edge
  (0/400 across all presets).
- **HB4 (L/Z hub↔frontline composition) and FR6's wide frontline are unreachable**, not merely
  unimplemented: a branch hub with a frontline falls back to the rectangle, so the Bar is never
  chosen (F3 above measured 0/489). The law describes a composition the code cannot currently produce.

WL7 and WL2 wanted the same lever as **F1** above (the inter-seat gap) — a separation rule in the seat
step. **F1 and WL2 are now landed** with that lever, sized at the **map lane width** (10/15 blocks) —
a body-adjacency floor, deliberately below WL7's ~45. **WL7 remains open**: it is a marker-to-marker
*traversal* spread, not a body-adjacency floor, so the seat gap does not achieve it — the compressed
distribution (min 21 / max well under the corpus 143) is a whole-layout *scale* concern, not a local
seat rule, and belongs with the hub-growth / budget work (G104/G105) that gives the boxes room to spread.

**Rules in the wrong layer.**

- **The offer grouping is decided by the filler, by coin flip.** `TeamUnitFiller` picks
  `rng.NextBool(0.5) ? Joint : Several`. Grouping is part of an **offer** (§1.14: "the edges/intervals
  it invites neighbours onto, **in which groupings**"), and offers are the allocator's plan — it is
  the allocator that writes joints. Worse, this is exactly **FR6**: joint vs several *is* wide vs
  split frontline, an authored law about how the mid band docks. A coin flip stands in for it.
- **The frontline's form choice is also the filler's.** `frontForm` picks Bar-for-branch-hub / else
  staple-or-strand inside `TeamUnitFiller`, but form choice is declared the allocator's
  (`docs/tools/generate.md` §5.5, and the allocator already "owns the hub-form choice"). The two
  halves of one decision — hub form and the form that answers it — sit on opposite sides of the
  allocate/fill seam.
- **`RingFitCells` restates a fit gate the shape already owns** (`BodyEmitter.Ring` throws, and
  `HubBoxEmitter.BuildBody` converts that to a directed null). Harmless today, but it is a fit gate
  duplicated in a caller, which is what the doctrine exists to prevent.

**A hypothesis that did not survive.** The staple's full-mouth check is made in the *demand* step
against the hub's **bbox edge length**, before the form is chosen — while the dock actually lands on
a free **run** of the chosen form, which on a branch or holed hub is shorter. That looked like a fit
gate evaluated against the wrong surface one step too early. Measured: **0 disagreements out of 47
staples**. Staples only survive where the edge is wide, and there run == bbox edge; elsewhere they
demote before it matters. No defect — recorded so it is not re-derived.

**Filed moves.** `G107` the demand kind · `G108` the mix kind · `G109` the ladders as targets ·
`G110` WL7/WL2 by construction · `G111` the frontline's offer decisions move to the allocator. All
in `BACKLOG.md`.
