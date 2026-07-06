# Layout evaluator — derive structure, judge by property (direction doc)

Direction doc for the `G`-series composer track. It reframes layout generation around an **evaluator**
(a critic that scores a `plan.json`) instead of a generate-and-test sampler, and pins the model that
makes the evaluator authorable: **author intent · derive structure · judge by property.** It sits
above `docs/contracts/layout-rules.md` (the frozen rule content the evaluator scores against),
`docs/contracts/layout-generation.md` (the plan/realize split), and `docs/seed-stats.md` (the measured
envelopes the soft terms use). It **refines** layout-generation.md §2's role model: structural roles are
*derived*, not authored — and derived cautiously (§5): the evaluator keys off **measurable** quantities,
not named roles the author is not ready to pin.

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
A frontline **tower** is a tile on the frontline edge with a tall surface; a **raised wool** is a room tile above its
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

**Derived** (computed, never authored) — split into locked *measurables* and provisional *labels* (§5):

- measurables — islands + their anchor role, marker branches (lane runs), junctions + per-objective
  approach count, axis position, build-interface counts, void topology (hole vs spacing, declared vs not);
- labels (provisional, over the measurables) — frontline (an *edge attribute*), the residual (unnamed),
  middle islands / stepping stones;
- height-roles — tower / raised-room / climb (§6).

**Roles stay minimal [decided].** `plan.json` roles are: anonymous `piece`; the intent-bearing
`wool-room` / `spawn`; the annotations `buffer` / `connector`. The retired legacy roles `lane` / `hub` /
`mid` / `frontline` **must not come back** — they are derived, so they must not live in the file. A
minimal role set is the correct expression of "derive structure," not a gap.

The **`connector`** role is a *template-composition* concept, not an evaluator one: in a full layout the
attachment point is derivable (where a marker branch meets the rest of the island); a `connector` only
earns its keep for a *fragment* (a reusable lane template with a dangling edge that has nothing to derive
its plug-point from). It stays out of the full-layout labeling loop; it returns if a stamp-templates
generator is built (§8).

## 5. The deriver — structure from markers + geometry

The deriver computes structure from the tile field + the markers ("peel the marker branches; the rest is
the residual"). Its output is **two tiers, and the split is the point:** a small set of **measurables** is
locked (pure topology/position — the evaluator keys off these), and a set of **labels** are provisional
names laid over the measurables for readability. The author can pin some structure crisply and rightly
refuses to pin the rest, so the model leans on what is measurable and treats the names as soft.

### 5.1 Locked measurables (pure functions, no naming judgement)

1. **Island** — a land-connected component (flood fill). Connectivity, **not** shortest-path — a
   land-connected back piece *is* island; a piece across a void with its own marker is a *separate* island
   (the WL4/SP6 isolated wool/spawn). (Route/spine is a different derived thing, for traffic — don't
   conflate.)
2. **Island anchor role** — by the marker it contains: **team** (holds a spawn), **objective** (holds a
   wool but no spawn — the isolated-wool island), **neutral** (anchorless, intersects a build region),
   **decorative** (anchorless, outside any build region — excluded from scoring).
3. **Marker branch** — the maximal ~1-room-wide path from each marker (wool, spawn) inward to the first
   widening/branch (the cutoff, §5.3). One per wool, one per spawn. (The readable name for a branch is a
   "lane"; the branch is the measurable.)
4. **Junction + approach count** — a cell where ≥2 marker branches coincide (share an origin area). Its
   value is a **count, not a name**: the number of distinct branches meeting on the way to an objective is
   that objective's **approach count** — the multi-access measure the evaluator wants (WL8 / G45). *(You
   were clear on "junction = where lanes coincide" while wary of locking the term — so it lives here as a
   measured count, not a role.)*
5. **Axis position** — each piece's distance to / straddle of the symmetry axis, from cell coordinates.
6. **Build interfaces** — per island/piece, the count and total width of edges touching a build region.
7. **Void topology** — a hole is **true void** (empty, non-buildable) the border can't reach without crossing
   **terrain or a build region** (both are walls for the enclosure flood): enclosed → **hole**, border-reachable
   → **spacing**. Build must wall the flood, otherwise a rotation pocket ("rotary device") near the frontline —
   encased by twin frontlines on some sides and the mid build band on the others — leaks to the border through
   the band and is missed. Cross against the authored deliberate-void marks (buffer / `zones[].holes`) to split
   **declared** from **undeclared** (a deliberate CT8 pocket vs an accidental enclosed void — a top evaluator
   term, §6).

### 5.2 Provisional labels (readability over the measurables — the evaluator prefers the measurable)

- **Frontline — a boundary *attribute*, not a piece** [decided]. The **contested edge**: where a team
  landmass meets the middle build zone, "where players mainly meet." It is fuzzy by nature (the author's own
  caveat) and depends on the mid — if the middle is one open build area with no islands, the frontline is
  simply whatever team land touches it; if the middle is islands-in-build, it is the team land facing the
  crossing. Modelled as a per-edge flag (which team-land edges face the mid void), so it composes with any
  piece: no "frontline piece" to segment, and no conflict when a wide face — or the residual's own bulk —
  borders the void. It is strictly an **outside** edge: the neighbouring cell must be buildable **and empty**
  (the crossing void) — an interior seam between two pieces is never a frontline, even where an author draws a
  big build rectangle that overlaps the terrain on both sides (a zone overlap must not manufacture a
  frontline).
- **Residual — deliberately undefined** [decided]. Whatever land remains once the marker branches are
  peeled. The model does **not** name it "hub" or fix its identity: it can be a plain square, a square with
  a hole, a square with several holes (an "Eight"), or something else. The evaluator only *bounds its shape
  properties* (§6) — it never requires a shape. *(Per the author: "I would not define hub at all yet — it's
  literally the remainder.")*
- **Middle island / stepping stone** — a standalone island sitting in / touching a build region (spoken of
  as just "an island"; the term "stepping stone" is fine). Two provisional sub-kinds, told apart by **axis
  proximity + build-interface count** (neither alone — a middle stone can touch a build region on just two
  edges): a **middle island** on/straddling the symmetry axis (position-derivable; the CT11 centre island
  when it is a mid stone, any size), vs a **lane stepping stone** out along a marker branch's path (the
  artifact of cutting a lane segment and swapping it for a build zone). Provisional — the evaluator keys off
  axis-distance and interface-count, not the sub-kind name.

The **mid** itself ranges from *one open build rectangle over the void* (players bridge freely) to
*islands nested in / bordering the build regions* (channelled crossings); the residual may legitimately
border the build region in the open case, which is exactly why "frontline" is an edge attribute and the
residual stays unnamed rather than being split at that border.

### 5.3 The cutoff — the one hard knob [open on the threshold]

The only non-trivial step is branch↔residual segmentation (the T-shape: wool at the long end → the stem is
the branch, the crossbar is residual). The rule: **a branch is ~1 room-unit (2 tiles) wide; the residual is
wider.** Equivalently, over the island's tile-adjacency graph, a branch is the maximal path of degree-≤2
tiles from the marker until the first tile that **branches** (degree ≥3 — that tile is a junction, §5.1.4)
or **thickens** (part of a ≥2×2 block) — classic skeleton + branch-pruning. On the coarse grid it is cheap;
"width ≤2 = branch, wider = residual" is almost the whole rule. The exact width/branch threshold is the
**one tunable knob**, settled against the hand-labeled T-shape cases (§5.4).

### 5.4 Derive-then-override [decided]

The deriver *proposes* every label; the author *corrects* only the few it gets wrong (a `labels` override
channel — **[later]**, an optional side-fixture, not part of a normal plan). So an ambiguous
branch-vs-residual is never a decision the author must make up front. The corrections are the **test set for
the deriver itself** — the cutoff's ambiguous cases are the only labels ever produced by hand.

**Payoff:** every existing seed and every future hand-drawing becomes a labeled example with *zero*
annotation — draw geometry, drop two markers, mark deliberate holes, run the deriver. And the deriver is
half the evaluator: most rules are "the residual has ≤N holes," "the branch is ≤L tiles," "the objective's
approach count ≥2" — once the measurables are computed, the property checks are one-liners.

## 6. The evaluator — the cost function

Form: `score = Σ hard-penalty(violated well-formedness) + Σ w · envelope-distance(metric)`. Hard rules are
large penalties (a valid layout has none); soft "feel" is each metric's distance outside the authored-set
range from `seed-stats.md`. "Feels right" = "lands in the authored distribution." The evaluator returns the
score **and the list of violated terms** (each citing a `layout-rules.md` id) so a failure is legible and a
generator can act on it.

Starter property terms, grouped by the measurable they read (each ties to a frozen rule id):

- **Global** — symmetry orbit exact; island count = orbit order (CT1); land budget within ±20% (G8); fill
  ratio in the corpus band (0.32–0.60); **every enclosed void either declared or penalized** (an
  *undeclared* enclosed void = a suspected accidental hole — one of the highest-value terms).
- **Mid** — clean band spans the axis (CT1); a hole per side is the default, holelessness the exception
  (CT8); stones inside the band (MD4), two-column grid on wide fronts (MD6); band clears every wool by ≥2
  cells (BZ6).
- **Frontline (the void-facing edge)** — the team land's void-facing edge docks the band **flush** and
  **full-face**, split-vs-wide (FR6); its edge snaps to the mid corner lines and the shared interval
  coincides (**G39** — the interlock term); readable connector extrusion on a long-face dock (BZ8); no void
  overflow / underfit (BZ9). All measured on the edge attribute, not a "frontline piece."
- **Residual (unnamed)** — bound its shape *properties* only, never require a shape: hole count and aspect
  in the authored range; L/Z compositions allowed (HB4); plaza-widening scales with budget (HB1/HB3).
  Penalise nothing for *being* a plain square — only for landing outside the authored shape envelope.
- **Branch / lane** — width 10 (15 on big maps, LN1); max collinear chain ≤50 blocks (LN2); wool at the
  far/back end inset ~5 (WL1); **largest enclosed void a branch wraps ≤ ~10×10** (**G40**); **absolute
  length capped to the authored norm, surplus routed to width/plaza/more routes, not length** (**G44**).
- **Approach count (from junctions)** — each objective's branch-count on the way in ≥2 where multi-access is
  wanted; a lone dead-end (count 1) is the defender-holds-the-mouth anti-pattern (WL8 / **G45** / **G37**).
- **Spawn** — wool reachable from the frontline edge *not through* the spawn (SP1); near the back of its lane
  (SP2); faces the enemy by default (SP3); **docks by a readable edge, never interior to the merged land**
  (**G42**); iron beside/ahead, never behind (SP7); isolated-spawn allowed at ≥10/team (SP6).
- **Objective / wool** — wool↔spawn ≥20 (WL2); wool↔wool ≥45 (WL7); flat plateau covering ≥ the 8×8 stamp,
  edge-to-edge (WL3); 1–3 wools, each on a distinct lane (WL6); a third wool is rare and a real route, not
  crammed by the spawn (G45).
- **Height (purposeful, not random)** — surface deltas are multiples of 2 (EL1); ≤2 raised sections per
  island (EL4); a Δ≥4 full-width seam is marked a cliff only when it qualifies (EL6); **wool room ≥ its
  approach** (a real climb, WL5); a **tower** is a tall tile on the frontline edge that clears the void; and the
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
- **Coverage** — the gap the author already senses: examples per **sub-problem** (mid / frontline / residual /
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
   derived labels over a plan. *(Status: v1 landed as a review tool — `tools/deriver/derive-gallery.cs` fans
   each seed to the full board and renders islands + anchor roles, the branch/residual erosion split, per-wool
   approach counts, the frontline edge, and undeclared voids. Known-rough: the branch/residual cutoff
   over-calls residual on big/wide boards — the §5.3 knob to settle first — and approaches are counted for
   wools only. Promote into `Analysis` once the cutoff is tuned.)*
3. **Property terms** — the §6 catalogue as pure functions over the derived structures, each citing a rule
   id and returning a distance + a violation record.
4. **Evaluation set** — auto-label the seeds; add minimal-pair negatives per §7 coverage; assert the
   evaluator ranks them the way the author does. This is the test for the rules.
5. **Generator, later** — start with option 8.1, escalate as the evaluator earns trust.

## 10. Open questions

- **[open]** The cutoff threshold (§5): width ≤2 = branch vs a branch/degree rule — settle against the T-shape
  test cases once a few are hand-labeled.
- **[open]** Declared vs computed voids (§6): does the author *assert* every deliberate hole, or only the
  ones the topology would otherwise flag? Leaning: author asserts, evaluator flags undeclared enclosed
  voids.
- **[open]** Whether the deriver's `labels` override channel (§5) ever needs to be persisted in `plan.json`
  or stays a side-file test fixture. Leaning: side-file, keep the plan format frozen.
- **[later]** The generator family (§8) — not chosen until the evaluator is trustworthy.
