# Layout evaluator — derive structure, judge by property (direction doc)

Direction doc for the `G`-series composer track. It reframes layout generation around an **evaluator**
(a critic that scores a `plan.json`) instead of a generate-and-test sampler, and pins the model that
makes the evaluator authorable: **author intent · derive structure · judge by property.** It sits
above `docs/contracts/layout-rules.md` (the frozen rule content the evaluator scores against),
`docs/contracts/layout-generation.md` (the plan/realize split), `docs/contracts/lane-decomposition.md`
(the human labeling rubric the deriver automates), and `docs/seed-stats.md` (the measured envelopes the
soft terms use). It **refines** layout-generation.md §2's role model: structural roles are *derived*,
not authored.

Tags: **[decided]** settled, **[open]** an author call is pending, **[later]** deliberately deferred.

## 1. Why an evaluator — the diagnosis

The composer today is **generate-and-test**: sample structural params in a fixed draw order → local
repair (the grower's shrink/inflate) → reject-and-resample on a hard-invariant failure → an acceptance
gate on the assembled plan. It got us the twelve-seed fidelity and the corpus reading, and for *local*
properties (a lane width, a marker inset) it is fine. But every open failure mode shares one signature —
they are **global, relational** properties a local, sequential generator cannot guarantee and rejection
sampling cannot efficiently repair:

- **G39** (band ↔ frontline must interlock) — a relation between two pieces' edges.
- **G40** (enclosed dead-space ≤ ~10×10) — a property of a *region bounded by several pieces*.
- **G42** (spawn docks, never submerges) — a property of a piece relative to the whole merged mass.
- **G44** (spend surplus structurally, not as length) — not a constraint at all; an **objective**.
- **G45 / G37** (multi-access, no lone dead-end) — a property of the *route graph* (a loop), not a piece.

**Is it "rules not hard enough"?** Partly, and the distinction is load-bearing. G39 genuinely *is* a
too-soft rule (the band may land flush on one edge; the examples never do) — tighten it, no model change.
But G40/G42/G44 are **not** soft rules: they are missing objectives and missing global constraints, and
hardening a *local* rule cannot produce them. Worse, adding them as reject-conditions makes it worse —
every hard constraint bolted onto a fixed sampling distribution drops the acceptance rate and shrinks the
feasible region to a sliver the draw order rarely hits (this is the p5 infeasibility, the hole-hunt, the
draw-order coupling). The tell: **the rules are written as an acceptance oracle — a checker — but a
generator cannot invert a checker.** `layout-rules.md` says beautifully what a *good result* looks like;
the sampler reaches results by local moves and then checks. When the property is global, "check-and-reject"
and "construct-and-hope" both fail. The rules are not too soft; they are in the wrong *form*.

**The inversion.** Move *all* the rules out of the generator and into one **evaluator**: a pure function
`plan → (score, [violations])`. The generator becomes dumb and swappable; the evaluator is the durable
asset. Hard rules become large penalties; "feel" becomes distance from an authored-set envelope; the
objective (spend structurally, not length) becomes a term with a place to live. This subsumes **G43**
(composer ↔ example-set conformance metrics): G43 *is* the evaluator's soft half.

We can *recognize* bad layouts far more reliably than we can *enumerate* good ones. That asymmetry is the
whole reason to build a discriminator (critic) rather than a complete generative grammar: the critic is
**additive and never has to be complete** — you keep adding terms as you find failures, and a new term
never tanks an acceptance rate. No reinforcement learning: the evaluator is a hand-written, inspectable,
deterministic cost function, tuned against a labeled set.

## 2. The architecture — three layers

| Layer | What it is | Where it lives |
| --- | --- | --- |
| **Author intent** | the irreducible input a machine cannot recover | `plan.json` (frozen format) |
| **Derive structure** | the structural roles + topology, computed | the *deriver* (in-memory, never serialized) |
| **Judge by property** | metrics vs rules + authored envelopes | the *evaluator* (score + violations) |

Everything the earlier design wanted to *author* (frontline, hub, lane, mid) moves to **derive**;
everything the rules want to *check* moves to **judge**. `plan.json` stays exactly as the author-intent
layer. The confusion — "am I sure whether this is a frontline or a hub?" — is the signal: the things you
are unsure how to label are exactly the things you should not be labeling; they are derived.

## 3. The substrate — `plan.json` unchanged, read as tiles

`plan.json` is **frozen [decided]**. It already *is* the author-intent layer:

- **Geometry on the 5-block cell grid** (`pieces[].rect` in cells) — every piece is a union of 5×5-block
  tiles by construction.
- **Height** (`pieces[].surface` + `globals.surface`) — full block resolution, per piece.
- **Intent markers** (`placements.wools` / `spawns`) — the objective + spawn anchors.
- **Deliberate voids** (`zones[].holes` + `buffer` pieces) — on-purpose emptiness.
- **Override channels** (`cliffs`, `walls`) — authored refinements over what the deriver would guess.

The evaluator is a **new read-only consumer**: `plan.json → rasterize to 5×5-cell tiles → derive roles →
measure → score`. The tile field and every derived role are computed views, never written back.

### Resolution [decided]

- **Atom = the 5×5-block cell** — the grid `plan.json` already uses. Existing seeds are *already*
  tile-layouts at this resolution; adopting it is a reinterpretation, **not a migration**. A coarser 10×10
  atom would distort odd-sized authored pieces (a 2×3-cell room, a 3-wide turn) and corrupt the exact
  labeled ground truth the evaluator depends on — rejected.
- **The "10×10 piece" survives as a derived region + a soft preference**, not a grid law. Fine grid (5×5)
  = storage; region lens (~10×10, a lane ≈ 2 tiles wide, a room ≈ 2×2) = how the *rules* think. The
  regularity is a cost-function bias, never a constraint that rejects a deliberately irregular piece.
- **Authoring is by shape, not by tile.** The author drags a rectangle; the tool fills the cells under it.
  Granularity is an internal detail the deriver/evaluator/search do not feel (they work on *regions*).

### Height is orthogonal [decided]

Footprint quantization does not touch height. Height is a **per-tile attribute** at full block resolution.
A frontline **tower** is a frontline tile with a tall surface; a **raised wool** is a room tile above its
approach; a **stepped approach** is a monotone run of surfaces. "Purposeful, not random" is an *evaluator*
concern (§6) — height that correlates with a derived role is purposeful; height that does not is what
"random" means. The heights already annotated in the seeds are **training data for the height envelopes**,
not a liability the tile model must swallow.

## 4. Authored vs derived

**Authored** (irreducible intent, or a topological fact only the author knows the *purpose* of):

- filled-vs-empty geometry (the piece rects);
- `wool-room` / `spawn` regions + their objective markers;
- deliberate voids — `zones[].holes` and `buffer` pieces (the author asserting "I meant this void");
- per-tile surface (height);
- overrides — `cliffs`, `walls`, and (§5) any manual role correction.

**Derived** (computed, never authored):

- the **team island(s)**, **lanes**, **hub**, **frontline**, **mid**, **stepping stones** (§5);
- topology — enclosed voids (holes) vs open gaps (spacing), reachability, orbit images;
- height-roles — tower / raised-room / climb (§6).

**Roles stay minimal [decided].** `plan.json` roles are: anonymous `piece`; the intent-bearing
`wool-room` / `spawn`; the annotations `buffer` / `connector`. The retired legacy roles `lane` / `hub` /
`mid` / `frontline` **must not come back** — they are derived, so they must not live in the file. A
minimal role set is the correct expression of "derive structure," not a gap.

The **`connector`** role is a *template-composition* concept, not an evaluator one: in a full layout the
attachment point is derivable (where lane tiles meet hub tiles); a `connector` only earns its keep for a
*fragment* (a reusable lane template with a dangling edge that has nothing to derive its plug-point from).
It stays out of the full-layout labeling loop; it returns if a stamp-templates generator is built (§8).

## 5. The deriver — automating the lane-decomposition rubric

The deriver is the machine version of the human rubric in `docs/contracts/lane-decomposition.md`
("subtract the lanes, the hub is the residual"). Inputs: the tile field + the markers. Pipeline:

1. **Team island** — the land-connected component containing a team's spawn/wool markers (flood fill over
   tile adjacency). Island is **connectivity**, not shortest-path — a land-connected back piece *is*
   island; a piece across a void with its own marker is a *separate* island (the WL4/SP6 isolated
   wool/spawn). Do not conflate island (containment) with route/spine (a shortest path — a different
   derived thing, for traffic).
2. **Lanes** — walk inward from each terminal marker (wool, spawn) until the **cutoff**. Wool lane and
   spawn lane are the same operation.
3. **Hub** — the island residual after subtracting lanes and the frontline strip: the thick core. It may
   be several junctions and may carry interior holes — a complex hub is *expected*, not a defect (this is
   what breaks the "hub is one square, every layout looks the same" degeneracy).
4. **Frontline** — filled tiles adjacent to the **mid void specifically** (not any build zone; a
   spawn-side zone does not make a frontline).
5. **Mid** — the axis-straddling buildable region (the band) + its stones. Defined topologically (the
   buildable region the symmetry axis passes through), not by distance-to-axis.
6. **Stepping stone** — a small filled component *not* land-connected to a marker island, embedded in a
   build zone (PC1).
7. **Voids** — flood-fill the empties: enclosed → **hole**; open gap between regions → **spacing**. Cross
   against the authored deliberate-void marks (a *deliberate* CT8 pocket vs an *accidental* enclosed void —
   see §6).

### The cutoff — the one hard part [open on the threshold]

The only non-trivial step is lane↔hub segmentation (the T-shape: wool at the long end → the stem is the
lane, the crossbar is hub). The rule: **a lane is ~1 room-unit (2 tiles) wide; the hub is wider.**
Equivalently, over the island's tile-adjacency graph, the lane is the maximal path of degree-≤2 tiles from
the terminal until the first tile that **branches** (degree ≥3) or **thickens** (part of a ≥2×2 block) —
classic skeleton + branch-pruning. On the coarse grid it is cheap; "width ≤2 = lane, wider = hub" is
almost the whole rule. The exact width/branch threshold is the **one tunable knob**.

### Derive-then-override [decided]

The deriver *proposes* every structural label; the author *corrects* only the few it gets wrong (a
`labels` override channel — **[later]**, an optional map, not part of a normal plan). So an ambiguous
frontline-vs-hub is never a decision the author must make up front. The corrections are the **test set for
the deriver itself** — the cutoff's ambiguous cases are the only labels ever produced by hand.

**Payoff:** every existing seed and every future hand-drawing becomes a labeled example with *zero*
annotation — draw geometry, drop two markers, mark deliberate holes, run the deriver. And the deriver is
half the evaluator: most rules are "the *derived* hub has ≤N holes," "the *derived* lane is ≤L tiles" —
once the structures are named, the property checks are one-liners.

## 6. The evaluator — the cost function

Form: `score = Σ hard-penalty(violated well-formedness) + Σ w · envelope-distance(metric)`. Hard rules are
large penalties (a valid layout has none); soft "feel" is each metric's distance outside the authored-set
range from `seed-stats.md`. "Feels right" = "lands in the authored distribution." The evaluator returns the
score **and the list of violated terms** (each citing a `layout-rules.md` id) so a failure is legible and a
generator can act on it.

Starter property terms, grouped by derived structure (each ties to a frozen rule id):

- **Global** — symmetry orbit exact; island count = orbit order (CT1); land budget within ±20% (G8); fill
  ratio in the corpus band (0.32–0.60); **every enclosed void either declared or penalized** (an
  *undeclared* enclosed void = a suspected accidental hole — one of the highest-value terms).
- **Mid** — clean band spans the axis (CT1); a hole per side is the default, holelessness the exception
  (CT8); stones inside the band (MD4), two-column grid on wide fronts (MD6); band clears every wool by ≥2
  cells (BZ6).
- **Frontline** — split-vs-wide, band docks **flush** and **full-face** (FR6); edges snap to the frontline
  corner lines and the shared interval coincides (**G39** — the interlock term); readable connector
  extrusion on long-face docks (BZ8); no void overflow / underfit (BZ9).
- **Hub** — shape variety: penalize the degenerate single square; hub hole-count and aspect in the authored
  range; L/Z compositions allowed (HB4); plaza-widening scales with budget (HB1/HB3).
- **Lane** — width 10 (15 on big maps, LN1); max collinear chain ≤50 blocks (LN2); wool at the far/back end
  inset ~5 (WL1); **largest enclosed void a lane wraps ≤ ~10×10** (**G40**); **absolute length capped to the
  authored norm and surplus routed to width/plaza/more routes, not length** (**G44**); terminal reachable
  from ≥2 sides where multi-access is wanted (WL8 / **G45** / **G37**).
- **Spawn** — wool reachable from a frontline piece *not through* the spawn (SP1); near the back of its lane
  (SP2); faces the enemy by default (SP3); **docks by a readable edge, never interior to the merged land**
  (**G42**); iron beside/ahead, never behind (SP7); isolated-spawn allowed at ≥10/team (SP6).
- **Objective / wool** — wool↔spawn ≥20 (WL2); wool↔wool ≥45 (WL7); flat plateau covering ≥ the 8×8 stamp,
  edge-to-edge (WL3); 1–3 wools, each on a distinct lane (WL6); a third wool is rare and a real route, not
  crammed by the spawn (G45).
- **Height (purposeful, not random)** — surface deltas are multiples of 2 (EL1); ≤2 raised sections per
  island (EL4); a Δ≥4 full-width seam is marked a cliff only when it qualifies (EL6); **wool room ≥ its
  approach** (a real climb, WL5); a **tower** is a tall frontline tile that clears the void; and the
  cross-cutting term — **every raised tile must be explained by a derived height-role** (room / tower /
  step); unexplained elevation is the definition of "random" and is penalized. Match the authored
  raised-wool and tower-height distributions.

The thresholds are **not** invented here — they come from `layout-rules.md` (hard) and `seed-stats.md`
(envelopes). This doc fixes the *form* and the *catalogue*; the numbers stay in those two files.

## 7. The evaluation set — the real deliverable

The evaluator is *correct* when it ranks a labeled set the way the author does. That labeled set is the
asset you keep growing:

- **Positives** — authored good layouts (the seeds, plus new ones), auto-labeled by the deriver.
- **Negatives** — flagged bad layouts. The most valuable are **minimal pairs**: a good layout and a
  near-identical bad one differing in *exactly one* property (a band shifted one tile; a lane wrapping one
  too-big void). A minimal pair isolates the single term the cost function is missing or mis-weighting —
  worth more than ten unrelated positives.
- **Coverage** — the gap the author already senses: examples per **sub-problem** (mid / frontline / hub /
  lane / spawn / objective) × per **rotation mode** (rot_180 / rot_90 / mirror_x / mirror_z / none). The
  authored frontline + bridge sets exist; the other cells are the shopping list.

The 350-map corpus is **not** this set: it is unlabeled in plan-model semantics and its quality is mixed
(layout-generation.md §1). The authored examples — small, high-quality, labeled by intent — are the ground
truth. The traffic pipeline (`docs/contracts/traffic-ground-truth.md`) may promote a *few* real maps into
labeled layouts; do not block on it.

## 8. Where the generator fits [later]

Build the evaluator **before** the generator — the opposite of today, where the rules are entangled in the
generator. `plan.json` is the interface for the whole loop: the generator *emits* it, the evaluator *scores*
it. It is the chromosome (search) or the solution (CP). Once the evaluator is trustworthy, the generator can
be, in increasing order of effort:

1. the current constructive grower, **ranked** by the evaluator instead of gated by hard rejects;
2. **local search** (hill-climb / anneal) over structural params using the evaluator as cost — the smallest
   change that attacks G44/G40 at the root (search *toward* the good region, not reject-and-hope);
3. **CP-SAT** over the relational skeleton (non-overlap, full-corridor adjacency, interlock equalities,
   void caps, reachability as constraints; budget as objective) — turns G39/G40/G42 from improbable into
   impossible; deterministic under a fixed seed, so goldens survive;
4. a **cyclic graph grammar** for multi-access — loops/alt-routes by construction (fixes the lone-dead-end
   G37/G45 at the source, rather than sampling and hoping).

**Shapes are a generator concern, never an evaluator one.** Named lane shapes (I/L/U/Z) are a palette to
*propose* from; the evaluator stays shape-agnostic (§6) so it can also bless a good layout no template
produced. Do not encode a shape whitelist in the evaluator — that is the enumeration trap in a new hat.

## 9. Build order

1. **Tile reading** — rasterize `plan.json` to the 5×5-cell field (occupancy + role + surface + buildable).
2. **Deriver** — structures from markers + geometry, with the width/branch cutoff; a debug render of the
   derived labels over a plan.
3. **Property terms** — the §6 catalogue as pure functions over the derived structures, each citing a rule
   id and returning a distance + a violation record.
4. **Evaluation set** — auto-label the seeds; add minimal-pair negatives per §7 coverage; assert the
   evaluator ranks them the way the author does. This is the test for the rules.
5. **Generator, later** — start with option 8.1, escalate as the evaluator earns trust.

## 10. Open questions

- **[open]** The cutoff threshold (§5): width ≤2 = lane vs a branch/degree rule — settle against the T-shape
  test cases once a few are hand-labeled.
- **[open]** Declared vs computed voids (§6): does the author *assert* every deliberate hole, or only the
  ones the topology would otherwise flag? Leaning: author asserts, evaluator flags undeclared enclosed
  voids.
- **[open]** Whether the deriver's `labels` override channel (§5) ever needs to be persisted in `plan.json`
  or stays a side-file test fixture. Leaning: side-file, keep the plan format frozen.
- **[later]** The generator family (§8) — not chosen until the evaluator is trustworthy.
