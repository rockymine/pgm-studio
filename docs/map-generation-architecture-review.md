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
And the code implements a fourth order (crossing sampled first, unit grown against it). The order
is in fact an **open experimental question** — spawn-first is simply the latest idea and reads in
the doc as if settled — and the architecture should treat it that way: the box model must be
**order-agnostic**. Boxes and typed interfaces form a constraint graph (a hub edge *is* its
neighbour's interface, whichever side is drawn first); the emission order is then a named, pluggable
**growth strategy** over that graph (`spawn-first`, `hub-first`, `mid-out`, …) so candidate orders
can be A/B'd against the evaluator instead of relitigated in prose. The doc should say exactly that:
the constraint relationships are the model; the order is a strategy knob. Design consequences in
§4.2.

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
| `WoolLaneShape`'s string result | `LaneRead` enum { I, L, Z, Complex, Plaza, None } | kills the stringly twin taxonomy; the doc already spends a paragraph disambiguating lane ≠ approach — the types should too (whether the class itself survives at all: §3.6) |
| `PlanDerived` (the rect layer) | `ContactGraph` | it *is* the pairwise contact / interface / gap-link / component graph; "PlanDerived" says only "stuff derived from a plan" |
| the gallery's `Derive()` result (the raster layer) | `BoardStructure`, built by `BoardDeriver.Derive(plan)` | the class name matches the doc's own term ("the board deriver"); the result names the *product* (structure: islands, zone kinds, holes, mid form), not its provenance |

An earlier draft of this review proposed `BoardDerived` as the umbrella; both `PlanDerived` and
`BoardDerived` are odd names for the same reason — "derived" is a participle standing in for a noun,
naming where the data came from instead of what it is. The proposals above name the thing (a graph,
a structure). Alternatives weighed and passed over: `PlanTopology` / `BoardRead` (fine, weaker),
`LayoutStructure` (collides mentally with `SketchLayout`).

### 2.3 Placement — the board deriver extraction

Target state, two layers under one roof (`src/PgmStudio.Pgm/Derive/` — a sibling of `Plan/`, or a
`Plan/Derive` folder; either is fine, the point is *src*):

- **Rect layer** (exact, block coords): today's `PlanDerived`, renamed `ContactGraph` (§2.2) but
  otherwise unchanged — contacts, interfaces, gap links, build regions, frontline edges. It is
  genuinely good code.
- **Raster layer**: `BoardDeriver.Derive(plan) → BoardStructure`, extracted from the gallery's
  `Derive()`, returning the typed record the gallery already builds internally (islands + anchor
  roles, stepping-stone kinds, intra/self bridges, zone kinds + widths, hole classes +
  parallel-ways, wool lanes, mid form). The gallery script becomes **render-only** over
  `BoardStructure` — which is also what §5.4's derive-then-override workflow wants anyway.
- **`ClosureAnalysis`** becomes a thin query over the raster layer *or* stays as a deliberately
  narrow fast path — it runs inside the composer's 60-attempt × hunt loop, so measure before folding
  it in. If it stays, mark it in code as a derived-subset twin of the `BoardStructure` holes (the
  same discipline CLAUDE.md applies to the JS symmetry twin), so the two can't silently diverge.
- **`FannedGraph`** keeps its looser reachability semantics but sources adjacency predicates from
  the rect layer instead of private copies, and resolves the overlap/surface inconsistency (2.3
  above) one way or the other.

**Regression safety.** The consolidation ships with a ready-made no-regression harness: the authored
base seeds (`tools/seeds/*.plan.json`) plus the gallery's fixed generated-case list are a complete
before/after oracle. Protocol for every extraction step: capture `derive-gallery`'s console summary
lines and `out/derive-gallery.html` before the change, re-run after, and require a **byte-identical
diff** — M0/M1 are pure moves, so *identical* is the bar, not "looks the same". The mirror harness
and the Pgm suite ride along. Only the behavioral steps (M2 onward, §4.3) may change gallery output,
and each re-baselines deliberately.

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
deriver already contains two candidate mechanisms — the stack-band trace
(`derive-gallery.cs:387-456`) and `WoolLaneShape`'s junction-stop flood, which §3.6 argues is the
one to promote. So the architecture wants an explicit dependency: **board deriver delimits → shape
deriver classifies**. When the box model lands (§4), a composed plan gets the scope for free (the
wool box); traced/authored plans use the extracted corridor extent. This must be settled before G56
(corpus tracing) can mine families at all.

### 3.6 Is `WoolLaneShape` still needed at all?

Short answer: **not as a separate classifier — but it is not subsumed by `WoolApproachShape` yet,
and deleting it today would lose the one capability the family classifier lacks.**

What it uniquely has: it works on a **full board**. Its width-adaptive junction test (`Thick` — a
cell sitting inside a filled `(W+1)×(W+1)` block, `WoolLaneShape.cs:50-52`) makes the trace
**self-delimiting**: the flood follows the thin corridor out of the room and stops at any
hub/plaza. That is precisely the scope mechanism §3.5 shows `WoolApproachShape` is missing — the
family classifier floods the whole connected component and therefore only works on standalone
fixtures. So today the two are complements, not duplicates: the lane read is the only wool read the
board deriver can run on composed plans, and the gallery report and the `layout-evaluator.md`
measurables consume it.

End state — dissolve it into the shared layer, in two parts:

1. **The junction-stop flood is promoted to the scope delimiter** (`CorridorExtent` in the shape
   layer), parameterized by **stop policy**: *stop at any junction* (the lane read — the corridor up
   to the first hub) vs *continue through same-width forks, stop at hubs/plazas* (the approach read —
   a U/H's second leg is part of the shape, a plaza is not). One flood, two policies — not two
   implementations. (Note the gallery separately traces lane *cells* with a third mechanism, the
   stack-band walk at `derive-gallery.cs:387-456`, used for rendering the orange lane tiles — fold it
   onto the same extent extraction when M1 lands.)
2. **The bend count merges** with the family classifier's outline reflex count —
   `WoolLaneShape.cs:60-68` and `WoolApproachShape.ReflexCount` are near-identical code already —
   via `Geom.Cells.ReflexCorners`.

After that, the *family* classifier run on the approach-policy extent supersedes the lane taxonomy
for shape identity. The board deriver's **wool-lane measurable stays** — §1.11's lane ≠ approach
distinction is real (the lane is the corridor the room owns; the approach is the whole box shape) —
but it becomes the open read (`I/L/Z/Complex/Plaza/None`) of the lane-policy extent, computed
through the same machinery rather than a parallel class. Sequencing: the string taxonomy goes in M0
(enum), `WoolLaneShape` the class retires in the step that lands the scope delimiter (task 5, §7).

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
GrowthOrder    a named strategy: the sequence in which box kinds are drawn and filled
               (spawn-first · hub-first · mid-out · …) — a ComposeRequest parameter
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
- **`GrowthOrder` makes the emission order an experiment, not doctrine** (Finding 1.1). The
  partition is a constraint graph; any order walks it, propagating interface constraints from
  whatever is already fixed onto whatever is drawn next — so `spawn-first`, `hub-first`, and
  `mid-out` are all expressible without touching the types. Orders are *named* so that seeds stay
  comparable within one strategy, and the evaluator (plus the teaching-set conformance metrics, G43)
  can judge orders against each other rather than the doc having to pick a winner up front.

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
  evaluator feedback; `GrowthOrder` strategies (spawn-first / hub-first / mid-out) become the
  experiment axis, judged by the evaluator and the G43 conformance metrics.

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
possible (the evaluator needs `BoardStructure`'s measurables); it should follow directly after.

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
2. **[M1] Board deriver into src.** `BoardDeriver.Derive → BoardStructure` extracted from
   `derive-gallery.cs`; `PlanDerived` renamed `ContactGraph`; gallery render-only; `ClosureAnalysis`
   reconciled; `FannedGraph` predicates unified (6.5). Acceptance gate: byte-identical gallery
   output over the base seeds + generated cases (§2.3 regression protocol). Doc §1.3/§6.2 updated to
   name the class, not the script.
3. **Evaluator skeleton over `BoardStructure`.** Dissolve `Composer.Acceptable` into hard terms;
   score + violations per §7; G43's metrics land here. Detailed design: §9; file placement: §8.
4. **[M2] Wool boxes in the grower.** `Box`/`BoxInterface`/`FillMenu`/`FillResult`; wool arms filled
   via `WoolBoxEmitter`; emitter orientation transform. Unlocks G44's structural spend, G50–G52.
5. **Derive-side slot recovery + classifier scoping.** `AssignSlots` template match;
   `CorridorExtent` scope delimiter with the two stop policies (§3.6) — `WoolLaneShape` the class
   retires here, its lane measurable re-expressed as the open read of the lane-policy extent;
   upgraded mirror test. Prerequisite for G56 corpus mining.
6. **[M3] Open-variant shapes for frontline/hub.** G41 L/Z compositions, HB4; G39's interlock as a
   `BoxInterface` constraint.
7. **[M4] Partitioner-first composition.** `BoxPartition` replaces the `Shape` sampling record;
   directed repair from `FillResult`; `GrowthOrder` strategies as the order experiment. Re-baseline
   gallery cases; then freeze G32-D goldens (per strategy).
8. **Doc pass on `map-generation.md`.** Emission order declared an experimental strategy axis over
   the constraint graph (Finding 1.1), not a fixed sequence; current-vs-target status per stage;
   frontline input/output reconciled; deriver named as code.

The ordering principle: **1–3 are pure consolidation that make every later feature cheaper and can
ship independently; 4 is the first behavior change and the real start of "box creation"; 7 is the
finish line where the doc's §2 pipeline and the code finally describe the same system.**

---

## 8. Target file layout (end state after M0–M4)

The concrete tree the plan converges on. Annotations: **[new]** created, **[ren]** renamed in place,
**[thin]** shrinks to an adapter, **[ret]** retires (deleted once its replacement lands), with the
step that does it.

```
src/PgmStudio.Geom/
  Cells.cs                        [new, M0]  N4 · FloodFill · Components · EnclosedVoids ·
                                             ReflexCorners · Bays · MinRunWidth (cell-set substrate)
  (everything else unchanged)

src/PgmStudio.Pgm/
  Shapes/                         [new namespace, M0 — §3.3 option (b); Geom-clean API]
    ShapeFamily.cs                one enum (absorbs ApproachFamily + ApproachShape, incl. Isolated)
    SlotTemplate.cs               from ApproachSlots; `room` → `terminal` at this layer
    ShapeEmitter.cs               the WoolBoxEmitter switch body, freed of GrownPiece/PlanRoles:
                                  Emit(family, box, cw, options) → slot-typed rects + terminal + marker
    ShapeClassifier.cs            the WoolApproachShape decision tree over (filled, terminal) cells,
                                  + ClassifyOpen(corridor) → LaneRead (absorbs WoolLaneShape's read)
    CorridorExtent.cs             [new, task 5]  the junction-stop flood, two stop policies (§3.6)
    SlotAssignment.cs             [new, task 5]  derive-side family→pieces template match (§3.4)

  Plan/
    PlanModel.cs                  unchanged (the frozen author-intent format)
    PlanValidator.cs              [thin, task 3]  structural errors stay; rule terms migrate to Evaluate/
    PlanCompiler.cs               unchanged
    WoolApproachShape.cs          [ret, M0 → Shapes/ShapeClassifier]
    WoolLaneShape.cs              [ret, task 5 → Shapes/CorridorExtent + ClassifyOpen]

  Derive/                         [new folder, M1 — the board deriver, finally in src]
    ContactGraph.cs               [ren from Plan/PlanDerived.cs]  rect layer: contacts, interfaces,
                                  gap links, build regions, frontline edges, components
    BoardStructure.cs             [new]  the raster-layer result record: islands + anchor roles,
                                  stepping-stone kinds, intra/self bridges, zone kinds + widths,
                                  hole classes + parallel-ways, wool lanes, mid form
    BoardDeriver.cs               [new]  Derive(plan) → BoardStructure (extracted from the gallery)
    FannedGraph.cs                [moved from Plan/]  predicates sourced from ContactGraph

  Evaluate/                       [new folder, task 3 — §9 below]
    LayoutEvaluator.cs            Evaluate(plan | EvalContext, profile) → Evaluation
    EvalContext.cs                (Plan, ContactGraph, BoardStructure, SeedEnvelopes) derived once
    EvaluationProfile.cs          per-term enabled + weight; the criteria on/off switch (§9.1)
    Terms/                        one file per §6-catalogue group of layout-evaluator.md:
      GlobalTerms.cs · MidTerms.cs · FrontlineTerms.cs · ResidualTerms.cs ·
      LaneTerms.cs · SpawnTerms.cs · WoolTerms.cs · HeightTerms.cs
    SeedEnvelopes.cs              loads seed-envelopes.json (embedded resource)
    seed-envelopes.json           [generated — see §9.3; regenerated, never hand-edited]

src/PgmStudio.Contracts/
  EvaluationDto.cs                [new, with the editor surface — §9.5]  wire form of Evaluation
                                  (score, term scores, violations w/ subject ids); DTO only, no
                                  logic — the editor's live-scoring surface (§10 decision 4)

  Compose/
    Boxes/                        [new folder, M2–M4]
      Box.cs                      (Id, Kind, Rect, LandTargetCells)
      BoxInterface.cs             edge interval + width — the master variable as a type
      BoxPartition.cs             boxes + interfaces (the constraint graph)
      FillMenu.cs                 width → legal families (the §4 table as data)
      FillResult.cs               Ok | TooSmall(minBox) | NoFamilyFits
      GrowthOrder.cs              named order strategies (spawn-first / hub-first / mid-out)
      BoxPartitioner.cs           [M4]  budget → BoxPartition, directed repair from FillResult
    WoolBoxEmitter.cs             [thin, M0]  binding over Shapes.ShapeEmitter: terminal → wool-room
                                  role + marker, wraps GrownPiece; public surface unchanged
    SpawnBoxEmitter.cs            [new, M2/M3]  same shapes, terminal → spawn role
    TeamUnitGrower.cs             [thin M2/M3 → ret M4]  wool arms delegate to boxes at M2, hub/
                                  frontline at M3; the Shape sampling record dies with M4
    Composer.cs                   Acceptable dissolved into Evaluate (task 3); orchestration stays
    ClosureAnalysis.cs            query over BoardStructure, or a documented fast-path twin (§2.3)
    MidCarver.cs · IsolationCut.cs · Envelope.cs · Frame.cs · ComposeRng.cs · ComposeRequest.cs
                                  stay (ComposeRequest gains GrowthOrder)

tools/deriver/
  derive-gallery.cs               [thin, M1]  render-only over BoardDeriver
  eval-rank.cs                    [new, task 4-ish]  the labeled-set ranking harness (§9.4)
  envelope-stats.cs               [new]  regenerates seed-envelopes.json (+ seed-stats.md tables)
  shapes-gen.cs · emit-verify.cs · stress-shapes.cs
                                  [ret, M0 → tests/ as TUnit (finding 6.3)]
  lane-audit.cs                   stays until task 5, then reads ClassifyOpen

tools/seeds/
  *.plan.json                     the positives (unchanged)
  teaching/                       unchanged
  negatives/                      [new]  minimal-pair negatives + labels.json (§9.4)

tests/PgmStudio.Pgm.Tests/
  Shapes/                         [new, M0]  ported mirror loop (emit → classify → slots), stress
                                  fixtures, catalog fixtures — the §5.4 harnesses as suite tests
  Derive/                         [new, M1]  ContactGraph/BoardStructure unit tests (synthetic)
  Evaluate/                       [new, task 3]  per-term unit tests on synthetic fixtures
  Compose/                        existing + Boxes coverage as M2–M4 land
```

Two deliberate non-moves: `PlanModel` stays in `Plan/` untouched (the wire format is frozen and
everything references it), and the shape layer stays inside `PgmStudio.Pgm` per §3.3 — if it is
later promoted to `Geom`, the `Shapes/` folder moves wholesale, which is the point of keeping its
API Geom-clean.

---

## 9. The evaluator, concretely

`layout-evaluator.md` §6–§7 fixes the *form* (`score = Σ hard + Σ w·envelope-distance`) and the
term catalogue; this section is the missing *code shape* — what an implementing agent builds. It
introduces no new law: thresholds stay in `layout-rules.md`, envelopes in the seed statistics, and
the evaluator stays **shape-agnostic** (terms read derived measurables, never family names — the
enumeration-trap rule of `layout-evaluator.md` §8).

### 9.1 Types and flow

```csharp
enum TermKind { Hard, Soft }

// one violation, legible and actionable: rule id + the pieces/zones it indicts
sealed record Violation(string TermId, string RuleId, string Message,
                        IReadOnlyList<string> Subjects);      // piece/zone ids, same as PlanFinding

sealed record TermScore(string TermId, TermKind Kind, double Distance, Violation? Violation);

sealed record Evaluation(double Score, IReadOnlyList<TermScore> Terms)
{
    public bool IsValid => Terms.All(t => t.Kind != TermKind.Hard || t.Violation is null);
    public IEnumerable<Violation> Violations => ...;
}

// derived once, shared by every term — a term never re-floods the board
sealed record EvalContext(PlanModel Plan, ContactGraph Contacts, BoardStructure Board,
                          SeedEnvelopes Envelopes);

interface ILayoutTerm
{
    string Id;         // e.g. "band-docks-full-face"
    string RuleId;     // e.g. "G39" / "BZ6" — every term cites exactly one layout-rules.md id
    TermKind Kind;
    TermScore Measure(EvalContext ctx);   // pure; no RNG, no IO
}
```

`LayoutEvaluator.Evaluate(plan)` = build `EvalContext` (derive `ContactGraph` + `BoardStructure`
once), run every registered term, sum:

```
Score = Σ_hard-violated  P_HARD  +  Σ_soft  w_t · Distance_t        (lower is better; 0 = perfect)
```

with `P_HARD` a constant that dominates any realistic soft sum (e.g. 1000 per violation) — a layout
with any hard violation must rank below every merely-ugly one. Weights and the enabled term set
live together in an **`EvaluationProfile`** (per term id: enabled + weight); the default profile is
all terms on at flat 1.0. Toggling a validation criterion on or off is a **profile edit, not a code
change** (decision 6b, §10) — the composer gate, the editor lint, and the ranking harness may each
run a different profile. Weights are tuned only when the labeled set (§9.4) mis-ranks, never by
taste. **Convention: cost, not fitness** — search minimizes.

### 9.2 The distance convention (so weights stay comparable)

Every soft term reduces to a metric `m` against a band `[lo, hi]`:

```
Distance = 0                          if lo ≤ m ≤ hi
         = (lo − m) / halfWidth       if m < lo        where halfWidth = (hi − lo) / 2
         = (m − hi) / halfWidth       if m > hi        (degenerate band: halfWidth = max(|hi|·0.1, ε))
```

Normalizing by the band's half-width makes `Distance = 1.0` mean "as far outside as the authored
band is wide" **regardless of unit** (blocks, counts, ratios) — without this, a flat `w = 1.0` start
is meaningless and every weight becomes a unit-conversion fudge. Counting terms (e.g. "undeclared
enclosed voids") use `[0, 0]` bands with a per-item distance. Hard terms skip distance entirely:
they yield a `Violation` or nothing.

### 9.3 Where the envelope numbers live

`Compose/Envelope.cs` already means *budget* — so the metric bands are **`SeedEnvelopes`** (never
"envelope" bare, to keep the collision out of the code). Source of truth: **generated, checked in,
never hand-edited** (decided — §10.1) — `tools/deriver/envelope-stats.cs` runs the deriver over
`tools/seeds/` (the authored positives), computes each catalogued metric's band (**global first**;
split by symmetry mode only when the ranking harness proves a global band mis-ranks — §10.2), and
writes `Evaluate/seed-envelopes.json`, from
which the human-readable `docs/seed-stats.md` tables are also refreshed. Adding a teaching seed and
re-running the tool *is* how the evaluator learns the author's taste — no code change. Hard
thresholds (`layout-rules.md` numbers) stay as constants in the term that cites them, exactly one
place each.

### 9.4 The labeled set and the ranking harness — the actual deliverable

`layout-evaluator.md` §7: the evaluator is *correct* when it ranks the labeled set the way the
author does. Executable form:

- **Positives**: `tools/seeds/*.plan.json` (+ `teaching/`). Assertion: `IsValid` and soft score in
  the low band (they *define* the envelopes, so near-zero by construction — the check catches term
  bugs, not seeds).
- **Negatives**: `tools/seeds/negatives/*.plan.json`, each a **minimal pair** — a copy of a positive
  with exactly one property broken (band shifted one cell; lane stretched past the norm; an
  undeclared void). A sidecar `labels.json` entry per negative:

  ```json
  { "file": "big-board--band-shifted.plan.json",
    "pairOf": "big-board.plan.json",
    "broken": "band no longer docks the frontline full-face",
    "expectTerms": ["G39"] }
  ```

- **The harness** (`tools/deriver/eval-rank.cs`, a corpus harness per repo convention): for every
  pair, assert `Score(negative) > Score(positive)` **and** the negative's violated/worst terms
  include `expectTerms` — the second clause is what makes a mis-weighted term visible even when the
  ranking accidentally holds. Output: a ranked table + per-term hit/miss, the analogue of the
  gallery's eyeball cards.
- Negatives can be **authored or mutated**: a tiny mutation library (shift a zone, stretch a piece,
  delete a buffer) generates candidate negatives from positives mechanically; the author only
  reviews and labels. Both paths stay open (§10 decision 3) — pick per failure class once the set
  starts growing.

Per-term **unit tests** (synthetic fixtures, `tests/.../Evaluate/`) cover each term's boundary in
isolation; the ranking harness covers the *ensemble*. Both must pass before a term's weight is ever
tuned.

### 9.5 Consumers

- **Composer gate** (dissolves `Composer.Acceptable`): a hard-terms-only profile run in
  **short-circuit mode** — the first hard violation rejects the attempt (decision 6, §10; the full
  soft evaluation runs only on attempts that pass, so the hunt loop's cost stays bounded). Two
  riders from the same decision: **(a) rejected attempts stay inspectable** — attempts are already
  RNG-stable, so the gate appends one line per reject to a **reject log** (JSONL, e.g.
  `tools/compose/out/rejects.jsonl` or a sink on `ComposeRequest`): `{seed, request, attempt,
  stage, termId, ruleId, subjects}`. Re-composing with the logged seed reproduces the failed layout
  exactly, and the log doubles as a frequency report of *which* rule kills most attempts — the
  directed-repair shopping list for M4. **(b)** the profile is the on/off switch for criteria.
  One cheap upgrade at the same time: the hole-hunt loop keeps the **lowest-scoring** acceptable
  attempt instead of the first, which makes new soft terms immediately steer output without any new
  search machinery (`layout-evaluator.md` §8 option 1).
- **Editor (decided direction, §10 decision 4)**: automatic generation will eventually be
  trigger-able from *inside* the editor, so scoring is a first-class editor concern, not just a
  harness one. Wire surface: an **`EvaluationDto`** in `Contracts` (score, per-term scores,
  violations with subject ids — DTO only, no logic, per the Contracts rule), a
  `POST /api/plan/evaluate` endpoint, and the future compose-trigger endpoint returning
  plan + evaluation together. The client renders violations the way `PlanValidator` findings render
  today (subject ids → canvas highlights); the lint and the score are the same records at two
  levels of detail. `PlanValidator` keeps only structural/parse errors.
- **G43 conformance**: a sweep report of soft distances per term over generated boards vs the
  teaching set — it is the same `Evaluation` records, aggregated.
- **Later search** (anneal / CP-SAT, `layout-evaluator.md` §8.2–8.3) minimizes `Score` directly;
  nothing here changes shape for that, which is the point of the cost convention.

---

## 10. Decisions (open questions resolved with the author, 2026-07-14)

The first draft closed with six open questions; the author has ruled on all of them. Recorded here
because the sections above build on them:

1. **Envelope regeneration — decided.** `envelope-stats.cs` owns both artifacts: the evaluator's
   `seed-envelopes.json` and the `docs/seed-stats.md` tables. (No in-tree tool generates the
   current tables — the doc becomes generated output; hand edits to it stop.)
2. **Envelope splits — decided.** Global bands first; bucket **by symmetry mode** only where the
   ranking harness proves a global band mis-ranks a pair. Player-count bucketing stays out until
   evidence demands it.
3. **Negative authoring — deferred.** Both mutation and hand-authoring stay open; the choice is
   made per failure class once the labeled set starts growing. Nothing in §9.4 depends on picking
   now.
4. **Editor surface — decided, and it shapes the architecture.** Automatic generation will be
   trigger-able from *within the editor*, so live scoring there is a requirement, not an option:
   `EvaluationDto` in `Contracts`, `POST /api/plan/evaluate`, and the compose-trigger endpoint
   returning plan + evaluation together (§9.5, §8 tree).
5. **Growth-order scope — decided.** Order experimentation waits for the M4 partitioner. No order
   knob on the current grower — its fixed draw sequence would make orders only superficially
   different and the A/B data misleading.
6. **Composer gate — decided.** Short-circuit on the first hard violation. Two riders: **(a)**
   rejected attempts must stay inspectable — RNG-stable seeds plus the reject log
   (`{seed, request, attempt, stage, termId, ruleId, subjects}`, §9.5) so any failure reproduces
   exactly and the log doubles as a which-rule-kills-most report; **(b)** validation criteria must
   be toggleable down the line — the `EvaluationProfile` (§9.1) is that switch: enabling/disabling
   a term is configuration, not code.
