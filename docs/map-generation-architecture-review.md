# Map generation — architectural review & plan (2026-07)

A critical review of the layout-generation architecture against its canonical contract
(`docs/contracts/map-generation.md`), plus a plan for the remaining features: the **box creation**
step, a **shared base-shape family layer** (wool approaches today; frontline / hub / spawn
compositions tomorrow), and a hard look at the **two derivers** — their naming, their placement, and
what is actually duplicated between them.

Everything here was read from the code as of `deb6296`; file:line references are to that state.

---

## 0. Verdict in one paragraph

The *conceptual* model is in excellent shape: the glossary discipline, the five-verbs vocabulary, the
width-independent family classifier, the slot-template-as-data idea, and the two-currency budget are
all genuinely good design, and the emit↔derive mirror harness is the right correctness anchor. The
*implementation* is mid-migration and the doc does not say so: the §2 pipeline (`budget → boxes →
emit → …`) describes a target, while the shipped composer is a different, older architecture
(`TeamUnitGrower` grows lanes inline and never calls `WoolBoxEmitter`). The board deriver's most
valuable half lives in a tool script the evaluator can never reference. The shape vocabulary exists
**three times** (emitter families, classifier families, the grower's inline lane growth) plus a
fourth stringly-typed cousin (`WoolLaneShape`), bridged in one place by `ToString()` comparison. The
raster substrate (flood fill, components, enclosed voids, reflex corners) is hand-rolled in at least
five sites. None of this is fatal — but the box-creation feature is exactly the point where these
debts converge, so they should be paid down *first*, and the moment is cheap: the G32-D goldens are
not frozen yet, so seed-breaking restructuring is still free.

---

## 1. Doc vs code — the honest pipeline table

`map-generation.md` §2 declares:

```
budget → boxes → emit / fill → compose / join → embed → evaluate → fragment → realize
```

What ships:

| Verb | Doc says | Code is | Status |
|---|---|---|---|
| **budget** | player count → land + footprint targets | `Envelope.Derive` (G8 anchors, coverage, aspect) | ✅ matches |
| **boxes** | budget draws typed boxes (`spawn/hub/wools/frontline/mid`), each side typed with an interface width that gates the fill menu | **does not exist.** `WoolBox` is a bare rect (`WoolBoxEmitter.cs:61`); there is no partition, no typed side, no `w2/w4/w6` menu in code | ❌ missing — *the* remaining feature |
| **emit** | fill one box with one family | `WoolBoxEmitter.Emit` — complete for all 8 non-isolated families with variants | ⚠️ built but **orphaned**: no production caller. Only `tools/deriver/*` and `WoolBoxEmitterTests` invoke it; `TeamUnitGrower` references it in doc comments only |
| **compose / join** | spawn → hub → wool boxes → frontline; frontline = where the fanned images meet (an *output*) | `TeamUnitGrower.TryGrow`: hub placed first, wool lanes grown by its **own inline segment grammar** (1–3 segments, I/L/Z only), frontline reach and axis margin taken **as inputs** from the pre-sampled `CrossingDesign` (`TeamUnitGrower.cs:247`) | ⚠️ different architecture |
| **embed** | relative frame placed late | `Frame` (u,v) → cell coords at `Place` time; effectively works | ✅ close enough |
| **evaluate** | score + violated-term list | `PlanValidator` (findings with rule ids — good) **plus** an inline mini-evaluator in `Composer.Acceptable` (`Composer.cs:77`); no scoring, no envelopes | ⚠️ split, no soft layer |
| **fragment** | land→build conversion, footprint-conserving, per-box land targets | `IsolationCut` (one move) + the mid's low land share via `MidCarver` | ⚠️ two special cases, no general pass, no per-box budget (budget exists only per-team) |
| **realize** | plan → sketch + intent | `PlanCompiler` | ✅ |

**Finding 1.1 — the doc states the emission order three different ways.** §2: "generation runs from
the spawn outward". §4: "it [the hub] emits first", then two paragraphs later "Order: spawn → hub →
wool boxes → frontline". `TODO.md` (the working strategy): "we harden it **from the mid outward**".
And the code implements a fourth order (crossing sampled first, unit grown against it). Each is
defensible; four at once is not. The doc should pick one target order and mark the mid-outward
hardening as the *migration* strategy, not the model.

**Finding 1.2 — frontline as output vs input.** §4's "the frontline is where the fanned images meet —
its position … is an *output*" is contradicted by `TeamUnitGrower.cs:247`
(`axisMargin = design.HalfGapCells`): the crossing is sampled up front and the unit grows to meet it.
That inversion was a deliberate choice (the mid-outward hardening), but the canonical doc presents
the un-shipped direction as fact. A reader implementing against the doc cannot tell which parts are
built. **Recommendation:** add a short *status* row per pipeline stage to §2 (current vs target), or
a one-line "implementation note" per section. A canonical doc that silently mixes description and
aspiration will keep producing G39-class surprises.

**Finding 1.3 — the doc names a tool script as half a component.** §1.3/§6.2 define the board deriver
as "`PlanDerived` + `tools/deriver/derive-gallery.cs`". A contract that points at a `#:project`
script for a core component has normalized a placement bug (see §2 below).

---

## 2. The two derivers — a critical look

### 2.1 There aren't two derivers; there are five and a half

The doc's model — one *shape* deriver, one *board* deriver — is right. The code has:

1. **`WoolApproachShape.Classify`** (`Plan/WoolApproachShape.cs`) — the shape deriver. Sound.
2. **`PlanDerived`** (`Plan/PlanDerived.cs`) — the board deriver's **rect layer**: pairwise contacts,
   land interfaces, build regions, gap links, frontline edges, components. Clean, exact, block-space.
3. **`Derive()` inside `tools/deriver/derive-gallery.cs:115-577`** — the board deriver's **raster
   layer**, ~460 lines in a run-by-hand script: islands with anchor roles, captive/team/neutral
   stepping stones, intra/self bridges, **build-zone kinds**, zone widths + interface widths (BZ3/BZ8),
   **hole classes** (`encased/gap/frontline/middle`) with declared/undeclared and parallel-ways, wool
   lanes (the stack-band trace), and **the CT mid-form**. This is the majority of §6.2's contract.
4. **`ClosureAnalysis`** (`Compose/ClosureAnalysis.cs`) — a third, independent raster flood
   (fan → rasterize → outside flood → enclosed components) computing the same §1.7 "hole" concept
   with its own inclusion rules.
5. **`WoolLaneShape`** (`Plan/WoolLaneShape.cs`) — its own raster trace with its own width read and
   its own reflex-corner counter.
6. (half) **`FannedGraph`** re-implements land adjacency and zone touching privately
   (`FannedGraph.cs:99-114`) rather than reusing `PlanDerived.Classify`.

**Finding 2.1 — the evaluator's substrate is unreachable.** §7's whole model is *judge derived
structure*, and `layout-evaluator.md` §5 catalogues the measurables — but the measurables' reference
implementation is the gallery script. A `#:project` script cannot be referenced by the evaluator, the
composer's acceptance gate, the API, or tests. Concretely: `Composer.Acceptable` needed hole
detection and got a **re-implementation** (`ClosureAnalysis`) instead of a call; G43 (conformance
metrics) will need zone kinds and mid-form and will face the same fork. Every future consumer either
re-implements or waits for the extraction. **This is the single highest-leverage refactor in the
whole area.**

**Finding 2.2 — duplicated raster substrate.** Counted across the five sites: `N4` neighbour
iterators ×4 (`WoolApproachShape.cs:215`, `WoolLaneShape.cs:72`, `derive-gallery.cs:581`, plus
inline arrays in `ClosureAnalysis.cs:73`), BFS flood fill ×5, enclosed-void detection ×3
(`WoolApproachShape.HasEnclosedVoid`, `ClosureAnalysis.Analyze`, gallery's outside-flood), connected
components ×3, reflex-corner counting ×2 (`WoolApproachShape.ReflexCount` and
`WoolLaneShape.Classify:60-68` are near-identical code). None of it is hard code, but five copies of
flood fill is how the two hole implementations have already begun to drift (different wall rules,
different margins).

**Finding 2.3 — two definitions of "island".** `PlanDerived.ComputeComponents` unions rect contacts
(Land/Narrow, plus **same-surface** overlaps); the gallery rasterizes and takes 4-connected cell
components (surface-blind). They agree only because the composer authors only clean `Land` contacts —
an undocumented equivalence. Worse, `FannedGraph.LandAdjacent` counts **any** area overlap as same
landmass regardless of surface delta (`FannedGraph.cs:103`), while `PlanDerived` components require
`SurfaceDelta == 0` — so the reachability graph and the island read can disagree on a cut+raise
refinement (§11's own elevation vocabulary). Pick one authority and document it.

### 2.2 Naming

The user asked specifically; here is the critique:

- **`PlanDerived`** says "stuff derived from a plan" — it does not say *board*, and the doc has to
  legislate ("when a doc says 'the deriver' it means the board deriver"). Legislated glossary entries
  that fight the type names lose eventually.
- The shape mirror is **four names for two concepts across two namespaces**: `ApproachFamily`
  (`Pgm.Compose`, 8 members) vs `ApproachShape` (`Pgm.Plan`, 9 members — adds `Isolated`) are the
  *same taxonomy*; `WoolApproachShape` is a classifier named like a data type; `WoolLaneShape`
  returns raw strings (`"I"/"L"/"Z"/"complex"/"plaza"/"none"`). The forward/inverse pair the doc
  celebrates (§5.4) is invisible to the type system: `emit-verify.cs:28` closes the mirror with
  `s.ToString() == want`. One renamed enum member would pass compilation and fail a hand-run script.
- Both namespaces are in **one assembly** (`PgmStudio.Pgm`), so the split isn't even a project
  boundary — it is pure organization debt, which makes it cheap to fix.

**Proposed renames** (mechanical, no behavior change):

| Today | Proposed | Why |
|---|---|---|
| `ApproachFamily` + `ApproachShape` | one `ShapeFamily` enum (with `Isolated`); emitter refuses `Isolated` explicitly | one taxonomy, one type; the mirror becomes `derived == requested` on the same enum |
| `WoolApproachShape` (static class) | `ApproachClassifier` (or `ShapeClassifier` once generalized, §3) | it classifies; it is not a shape |
| `WoolLaneShape`'s string result | `LaneRead` enum { I, L, Z, Complex, Plaza, None } | kills the stringly twin taxonomy; the doc already spends a paragraph disambiguating lane ≠ approach — the types should too |
| `PlanDerived` | keep as the rect layer, but introduce `BoardDerived` as the umbrella (below) | the doc's own term |

### 2.3 Placement — the board deriver extraction

Target state, two layers under one roof (`src/PgmStudio.Pgm/Derive/` — a sibling of `Plan/`, or a
`Plan/Derive` folder; either is fine, the point is *src*):

- **Rect layer** (exact, block coords): today's `PlanDerived` unchanged — contacts, interfaces, gap
  links, build regions, frontline edges. It is genuinely good code.
- **Raster layer**: `BoardDerived.Build(plan)` extracted from the gallery's `Derive()`, returning the
  typed record the gallery already builds internally (islands + anchor roles, stepping-stone kinds,
  intra/self bridges, zone kinds + widths, hole classes + parallel-ways, wool lanes, mid form). The
  gallery script becomes **render-only** over `BoardDerived` — which is also what §5.4's
  derive-then-override workflow wants anyway.
- **`ClosureAnalysis`** becomes a thin query over the raster layer *or* stays as a deliberately
  narrow fast path — it runs inside the composer's 60-attempt × hunt loop, so measure before folding
  it in. If it stays, mark it in code as a derived-subset twin of `BoardDerived` holes (the same
  discipline CLAUDE.md applies to the JS symmetry twin), so the two can't silently diverge.
- **`FannedGraph`** keeps its looser reachability semantics but sources adjacency predicates from
  `PlanDerived` instead of private copies, and resolves the overlap/surface inconsistency (2.3 above)
  one way or the other.

---

## 3. Shape families — one vocabulary, many bindings

### 3.1 The observation is correct: a family is a base shape + a terminal attached

Nothing in the nine-family identity is wool-specific. Read the classifier's tests: "no terrain
touches the *wool*" is "…the *terminal*"; "the *wool* is a cut cell" likewise; turn count, fork, bay,
enclosed void never mention wool at all. The wool-ness lives entirely in the binding: which slot is
the `room`, how the marker docks, and the `PlanRoles.WoolRoom` role stamped at the end
(`WoolBoxEmitter.cs:251`).

And the codebase already contains the other consumers the user predicts:

- **Spawn lanes** are I or L — `TeamUnitGrower`'s `spawnL`/`spawnSegCount` logic
  (`TeamUnitGrower.cs:399-435`) is a hand-rolled I/L emitter with `room = spawn`.
- **Frontline** singles are I/Z chains (`FrontForm.Single` segments), and **G41** explicitly wants
  L/Z frontline↔hub compositions.
- **Hubs** want multi-piece shapes (HB4, G41 again).
- **Wool lanes in the grower** are a third implementation of the I/L/Z subset — the inline 1–3
  segment growth (`TeamUnitGrower.cs:441-496`) with its own bend logic, no slots, and a hard ceiling
  at Z (never scythe/clamp/U/H/donut — one reason generated boards read monotonous, and why G44's
  "surplus spent structurally" has no vocabulary to spend into).
- **`WoolLaneShape`** is the *open-corridor* read of the same vocabulary (I/L/Z without a seated
  terminal).

So the shape model generalizes on two axes: **terminal binding** (wool room / spawn room / none) and
**openness** (terminal-capped approach vs a through-corridor with two entries — what a frontline or
hub join needs). The nine families are the terminal-capped set; the open set is the I/L/Z(/complex)
subfamily `WoolLaneShape` already names. One model covers both: the classifier already excludes the
room from the bend count, so the open read is the same machinery with an empty terminal.

### 3.2 Proposed layer

```
ShapeFamily   — one enum: Isolated · I · L · Z · Scythe · Clamp · U · H · Donut
SlotTemplate  — Template(family) → entry/run/bar/leg/terminal sequence (today's ApproachSlots,
                `room` renamed `terminal` at this layer)
ShapeEmitter  — Emit(family, box, cw, options) → slot-typed rects + terminal rect + marker offset
                (today's WoolBoxEmitter body, minus GrownPiece/PlanRoles)
ShapeClassifier — Classify(filledCells, terminalCells) → (family, width)
                 ClassifyOpen(corridorCells) → LaneRead   (absorbs WoolLaneShape)
```

Thin bindings stay in `PgmStudio.Pgm`:

- `WoolBoxEmitter` → adapter: calls `ShapeEmitter`, maps `terminal` → `PlanRoles.WoolRoom` + wool
  marker, wraps as `GrownPiece`s. Public surface unchanged; the mirror harnesses keep passing.
- `SpawnBoxEmitter` (new, small): same shapes, `terminal` → `PlanRoles.Spawn` — replaces the grower's
  inline spawn-lane geometry when the box migration lands (§4).
- Frontline/hub composition (G41): open-variant emission — same templates, two entries, no terminal.

### 3.3 Where the shapes live — the "more general location" question

Two real options, judged against the CLAUDE.md placement rule (*lowest project that has the deps and
that every consumer reaches*):

**(a) `PgmStudio.Geom` now.** The classifier and templates are pure cell math with zero deps once the
`PlanModel`/`GrownPiece` adapters are peeled off — Geom-clean by construction. Precedent: `Symmetry`
moved to Geom exactly this way. The cost: `entry`/`terminal`/family semantics are map-*design*
language, and Geom is today a pristine scalar/polygon leaf with documented JS twins; also **no second
project needs the shapes yet** (every consumer — composer, deriver tools, evaluator-to-be — lives in
or references `PgmStudio.Pgm`; canvas previews of families would be JS twins per the
hot-path-stays-in-JS doctrine, not C# client code).

**(b) `PgmStudio.Pgm.Shapes` now, Geom later if ever.** One namespace inside the assembly both
`Compose` and `Plan` already share; the enum unification (§2.2) happens here for free. Keep the core
API Geom-clean (cell sets in, rects out, no plan types) so a future promotion is a file move, not a
disentangling.

**Recommendation: (b) for the family layer, but push the *substrate* to Geom immediately.** The
raster substrate is generic geometry with five duplicated copies today (Finding 2.2):

```
PgmStudio.Geom/Cells.cs  —  N4 · FloodFill · Components · EnclosedVoids ·
                            ReflexCorners · Bays · BoundingBox · MinRunWidth
```

That single move deletes the worst duplication (shape classifier, lane read, gallery deriver,
closure analysis all rewrite onto it), is unambiguously Geom material, and leaves the
domain-flavoured family model where its consumers are. If the author prefers the stronger form —
family model in Geom too — nothing above blocks it; rename `room → terminal` and it is Geom-clean.
The wrong outcome is only the status quo: the same vocabulary in three places with a `ToString()`
bridge.

### 3.4 The slot mirror is half-built

§5.3 promises "composition rules stated over slots". Today slots exist **only in memory during
composition**: `GrownPiece.Slot` is set by the emitter, but `Composer.Assemble` drops it
(`PlanPiece` has no slot field) and `AsPlan` drops it too. Meanwhile the shape deriver returns only
the family — it cannot recover slots. So the moment a plan is saved, loaded, hand-authored, or traced
(G56), the slot vocabulary is gone, and no composition rule can actually be evaluated over slots.

Persisting slots into `plan.json` would violate §3's authored/derived split (slots are derived).
**The right completion is on the derive side**: after `Classify` returns the family, template-match
the family's slot sequence onto the classified pieces — `AssignSlots(family, pieces) →
piece→slot map`. This also upgrades `emit-verify` from "slot sequence equals template" (testing the
emitter against itself) to a true mirror: emit → classify → re-derive slots → compare.

### 3.5 The classifier only works on fixtures — the scoping gap

`WoolApproachShape.Classify` floods the terrain component reachable from the room
(`WoolApproachShape.cs:62-65`) and then runs the donut/branch/bay tests on **that whole component**.
On a standalone `AsPlan` fixture the component *is* the shape — fine. On any real composed or traced
plan, the wool's approach is welded to the hub, spawn lane, and frontline, so the component is the
entire team unit: a hub + twin frontline that enclose a pocket would make **every wool on the unit
read Donut**. Today this is latent only because no production code calls `Classify` on a full plan.

The mirror's derive side needs a **scope**: "which cells are *this wool's* approach". The board
deriver already answers it — the stack-band wool-lane trace (`derive-gallery.cs:387-456`) walks from
the room to the first crossbar/hub, which is exactly the approach extent. So the architecture wants
an explicit dependency: **board deriver delimits → shape deriver classifies**. When the box model
lands (§4), a composed plan gets the scope for free (the wool box); traced/authored plans use the
stack-band extent. This must be settled before G56 (corpus tracing) can mine families at all.

---

## 4. Box creation — design for the missing middle

### 4.1 What exists / what's missing

Exists: the `WoolBox` rect, a complete family emitter into it, the doc's §4 model. Missing: the
partition (budget → typed boxes), typed sides carrying interface widths, the width→menu production
rule, endpoint-to-side matching, per-box land targets, and the "no shape fits → change the box"
feedback loop. `TeamUnitGrower` currently plays partitioner, emitter, and joiner at once via the
`Shape` record's ~20 sampled fields and the shrink/inflate repair loop.

### 4.2 Proposed types (in `Compose/`)

```
BoxKind        { Spawn, Hub, Wool, Frontline, Mid }
Box            (Id, Kind, Rect /*cells*/, LandTargetCells)
BoxInterface   (A, B, EdgeInterval /*position+width — §1.5's "always an interval"*/, WidthCells)
BoxPartition   (Boxes, Interfaces)
FillMenu       For(widthCells) → legal (family, options) set   // the §4 w2/w4/w6 table as data
FillResult     Ok(pieces) | TooSmall(family, minBox) | NoFamilyFits(menu)
```

Notes on each:

- **`BoxInterface` is the master variable made first-class.** Today interface width exists only as an
  emergent property the deriver measures afterwards (BZ8). Making it an input type is what lets G39's
  "band docks the full frontline face" be a *constraint on the partition* instead of a post-hoc lint.
- **`FillMenu` is where `layout-rules.md` numbers enter generation** — one table, cited by rule id,
  instead of magic numbers scattered through the grower (`hubCap`, `seg1Cap`, `FrontCapCells`…).
- **`FillResult` replaces exception control flow.** `WoolBoxEmitter.Need` throwing `ComposeException`
  is fine for a harness but forces the composer into catch-and-resample. §4 says "no shape fits is a
  signal, not a failure" — a signal needs a data channel: `TooSmall` carries the family's minimum box
  so the partitioner can resize/relax/split *directedly* rather than re-rolling 60 attempts.
- **Orientation**: the emitter hardcodes the mouth at the top edge (`z = Z`) with a horizontal flip
  only. A partition needs all four dockings. Emit in the canonical frame and apply a rect transform
  (reuse `Geom.Symmetry.Apply` on the emitted rects) rather than teaching every family case four
  orientations.
- **Budget**: `Box.LandTargetCells` is where the two-currency model (§8) finally gets its per-box
  half; **fragment** then becomes a generic pass over the partition (convert emitted land inside a
  box until it meets its target), of which `IsolationCut` and the mid's low target are the two
  existing special cases.

### 4.3 Migration sequence — do not big-bang the grower

The grower encodes real, hard-won invariants (fixed draw order, LN2 chains, image clearance, the
repair loops) and currently *works*. Replace it organ by organ:

- **M0 — substrate + unification** (pure refactor): `Geom.Cells`; one `ShapeFamily` enum; classifier
  generalized to terminal-cells; `WoolLaneShape` → `ClassifyOpen`. Mirror harness + Pgm tests green,
  zero output change.
- **M1 — board deriver into src**: `BoardDerived` extracted from the gallery; gallery renders it;
  `ClosureAnalysis` reconciled (query or documented twin). Unblocks the evaluator and G43's
  conformance metrics as library calls.
- **M2 — wool arms become wool boxes**: inside `TeamUnitGrower`, the inline 1–3-segment wool-lane
  growth is replaced by: partition the arm's region into a `Box(Wool)` with a typed entry interface →
  `FillMenu` → `WoolBoxEmitter`. This is the first production caller of the emitter, kills the third
  shape implementation, makes G50–G52 (entry shift, attachment width, docking modes) reachable from
  generation at last, and gives G44 its structural spend vocabulary (escalate family instead of
  stretching length).
- **M3 — hub + frontline as shape consumers**: open-variant emission for G41's L/Z compositions and
  HB4 multi-piece hubs; G39's corner/edge interlock enforced on `BoxInterface` at partition time.
- **M4 — the partitioner as the first artifact**: `BoxPartition` replaces the `Shape` record as what
  sampling produces; the composer's resample loop becomes partition-repair driven by `FillResult` and
  evaluator feedback.

**Timing warning (load-bearing):** every one of M2–M4 changes RNG consumption and therefore every
seed's output — the gallery's negative-set cases re-key and any goldens break. G32-D (fixed-RNG
goldens) is still open: **land this restructuring before freezing goldens**, or accept regenerating
them at each step. The "sampling order is part of the golden contract" comments make this a
deliberate decision, not an accident to discover later.

---

## 5. Evaluator & rules — stop the third copy

Rule knowledge currently lives in three places: `layout-rules.md` (law), `PlanValidator` (findings
with rule ids — the right shape), and `Composer.Acceptable` (`Composer.cs:77-110`) — an inline
mini-evaluator hardcoding G5's hop band, BZ6's clearance re-check with hand-fanned rects, WL8's
hole-ring test, and a lint-reject list (`"WL2" or "PC-C" or "G2" or "G5"`). §7's evaluator will be a
fourth unless `Acceptable` is dissolved into it: hard terms return large penalties, the composer
keeps only `score == 0` (or a threshold) as its gate. Same engine for the editor lint, the composer
gate, and the eventual scored search — which is precisely §7's three-layer model. M1 makes this
possible (the evaluator needs `BoardDerived`'s measurables); it should follow directly after.

---

## 6. Smaller findings

| # | Finding | Fix |
|---|---|---|
| 6.1 | `Classify`'s `laneWidth` param is dead (`_ = laneWidth;`, kept "for call compatibility") | remove on the M0 rename |
| 6.2 | Stale doc refs: `WoolBoxEmitterTests.cs:7` cites `docs/contracts/layout-generation.md §2` (file no longer exists post-consolidation); `ApproachSlots` xmldoc cites "the §2 piece-vocabulary table" (now §5.3 of `map-generation.md`) | sweep with M0 |
| 6.3 | The §5.4 mirror harnesses (`shapes-gen`, `emit-verify`, `stress-shapes`) are **synthetic** fixtures run by hand in `tools/`, while the repo convention (CLAUDE.md) puts synthetic tests in `tests/` (TUnit) and only corpus harnesses in `tools/`. The mirror is the correctness anchor of the whole shape layer and doesn't run in the suite | port the three to TUnit; the gallery stays a tool (it renders the seed corpus) |
| 6.4 | `emit-verify` closes the mirror via `ToString()` across two enums | dissolved by the M0 enum unification |
| 6.5 | `FannedGraph` vs `PlanDerived` disagree on different-surface overlaps (Finding 2.3) | pick one rule, add a test |
| 6.6 | The width report in `WoolApproachShape.Classify` and the width read in `WoolLaneShape` are two near-identical min-cross-section computations with slightly different clamps | one `Cells.MinRunWidth` in M0 |
| 6.7 | `Composer.Assemble` silently drops `GrownPiece.Slot` — surprising given §5.3's emphasis | resolved by §3.4's derive-side slot recovery (don't persist) |

---

## 7. Suggested task breakdown

Not filed into `BACKLOG.md` — ids and curation belong to the author; these are cut to slot into the
G section as-is. Dependencies point up the list.

1. **[M0] Shape substrate + one family enum.** `Geom.Cells` (N4/flood/components/enclosed-voids/
   reflex/bays/min-run-width); merge `ApproachFamily`+`ApproachShape` → `ShapeFamily`; classifier
   takes terminal cells; `WoolLaneShape` → `ClassifyOpen` returning a `LaneRead` enum; kill the dead
   param; fix stale doc refs (6.2); port the three mirror harnesses to TUnit (6.3). Pure refactor —
   gallery output byte-identical.
2. **[M1] Board deriver into src.** `BoardDerived.Build` extracted from `derive-gallery.cs`; gallery
   render-only; `ClosureAnalysis` reconciled; `FannedGraph` predicates unified (6.5). Doc §1.3/§6.2
   updated to name the class, not the script.
3. **Evaluator skeleton over `BoardDerived`.** Dissolve `Composer.Acceptable` into hard terms;
   score + violations per §7; G43's metrics land here.
4. **[M2] Wool boxes in the grower.** `Box`/`BoxInterface`/`FillMenu`/`FillResult`; wool arms filled
   via `WoolBoxEmitter`; emitter orientation transform. Unlocks G44's structural spend, G50–G52.
5. **Derive-side slot recovery + classifier scoping.** `AssignSlots` template match; approach-extent
   scope (stack-band or box); upgraded mirror test. Prerequisite for G56 corpus mining.
6. **[M3] Open-variant shapes for frontline/hub.** G41 L/Z compositions, HB4; G39's interlock as a
   `BoxInterface` constraint.
7. **[M4] Partitioner-first composition.** `BoxPartition` replaces the `Shape` sampling record;
   directed repair from `FillResult`. Re-baseline gallery cases; then freeze G32-D goldens.
8. **Doc pass on `map-generation.md`.** One emission order; current-vs-target status per stage;
   frontline input/output reconciled; deriver named as code.

The ordering principle: **1–3 are pure consolidation that make every later feature cheaper and can
ship independently; 4 is the first behavior change and the real start of "box creation"; 7 is the
finish line where the doc's §2 pipeline and the code finally describe the same system.**
