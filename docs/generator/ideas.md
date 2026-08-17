# Layout generation — the idea pool

The condensed long tail of the layout-generation track. The task board (`TODO.md`/`BACKLOG.md`) had
accumulated ~40 open G-tasks looking far ahead — many describing machinery that no longer exists after the
old grower path was retired and the box pipeline became *the* composer. This file replaces that backlog
section: **one idea per line-or-three, grouped by theme, ids preserved** (an id here never gets reused; the
full original task text is in git history under `BACKLOG.md`/`TODO.md`). When an idea becomes the focus,
pull it back onto the board by id.

Status markers: *(obsolete)* = described the retired grower path and is settled or moot; *(partial)* = part
landed, the rest is the idea.


## Mid enrichment (the crossing vocabulary, back on the box path)

- **G116** *(partial — the split band shipped)* — richer mids: stone rows, the centre island (single/pair),
  depth variation (the ≥20-player 30-block deep single). All re-enter as `CrossingDesign` forms; the retired
  `SampleCrossing` arithmetic (hops 10..20, sum 30..60, CT7 column alignment, the MD6 lateral grid) is the
  reference design, in git history.
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
- **G105** *(partial — the asymmetric ring, the branch leg sampler and its bounded bay shipped)* — bigger/better hubs: a raised
  depth cap, form→size fit, and the per-piece width knob where it is still uniform — the branch hub's **bar**
  thickness and **leg length** (the emitter's per-arm overload carries both; only the leg's width is sampled) and
  a hub arm count of three (the E/comb the body emitter supports but `HubBoxEmitter` refuses).
- **G130** — **the budget has no price for a widened part.** Ring-wall widening ships (G129): a hub ring may carry
  one side wider than the rest, sampled under the ratio cap. What it costs is currently nothing — widening spends
  the box's *geometric* slack (the hole narrows, the box does not grow), so the two-currency budget sees the same
  footprint and slightly more land without ever having decided to buy it. Scope: whether a widened wall should
  price against the land share (it is real terrain the team must cross) and whether that price should make the
  sampler *choose* the wide side rather than draw it evenly. Until then the widening is free, which is why its
  rate is capped by chance rather than by budget.
- **G131** — **per-part width beyond the ring.** G129 generalized the four walls of a ring; the rest of a board is
  still one lane width (`w` = 2 or 3 from the land budget) with the wool lane and hub wall as fixed constants
  (`WoolLaneCells`, `FillProfiles.HubWallCells`). The remaining cases are the parts *docked onto* a ring, which
  deliberately kept a plain `cw` — the P's overhanging bar, the G's L-upright, the DoubleHole's U — plus the
  approach lanes and the frontline's spine. **Relation to its neighbours:** G105 owns the per-piece width knob for
  hub bodies, G82/G83 own entry widening for wool approaches; this is the generalization those are special cases
  of. Precedent for the knob exists on the approach side already — the donut's sampled hub-entry width
  (`AttachmentWidth`) is per-part width in all but name.
- **G106** *(partial)* — the observed seat/emit failure modes (taxonomy §9 F2–F5): flush lanes at branch-hub
  run ends, the tiny-stub fallback, twin-leg equality, square-on-square.
- **G112** — P-aware neighbour placement so the P hub survives seating.
- **G113** — restore the third wool (near-extinct since the seat gap; bias the spawn/doubling toward the
  wide edge). The wool-count distribution test in `ComposerTests` re-asserts 3-wool occurrence when this lands.
- **G114** — the along-axis mirror so a tight-hub L bends back instead of reverting to I.
- **G107–G111** — the taxonomy audit's five moves: `demand` as a live kind (G107), the `mix` kind (G108),
  budget ladders as derived targets (G109), WL7 separation by construction (G110, the traversal-spread
  half), the frontline offer decisions moving to the allocator (G111 — joint-vs-several is FR6, currently a
  coin flip in the filler).
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
  stone classification) — the prerequisites of the cramming term.
- **G66 / G67 / G68** — rule visualization (illustrated rule catalog + reject inspector), fill-time slot
  invariants, evaluator-side slot terms.
- **G43** — the composer↔teaching-set conformance sweep (aggregate soft distance per term).
- **G127** — the flow graph: junction/lane-chain derivation + route signatures + the first flow terms.
  Revives G24's already-designed substrate, which is worth keeping written down because it is the part
  that was solved and never built (moved here from the retired plan-editor design doc):

  > **Junction regions (hubs)** are computed on the **unioned island footprint**, so how the author cut
  > the plan into pieces cannot change the result. Every access **mouth** — a land interface or a bridge
  > mouth on the boundary — is an *interval* with an inward direction; extrude each mouth's span
  > perpendicular into the land, and a junction region is the intersection of corridors from **three or
  > more** mouths. A four-way "plus" yields the crossing rect, a three-way T likewise, and a two-mouth
  > corner yields nothing, because a corner is not a hub. Areal by construction — interval mouths, region
  > output, no thinning or skeletonization anywhere. A **lane** is then the corridor between junction
  > regions and dead ends, which is what the width and length rules are measured along, so a lane cut into
  > several pieces for elevation or cornering is still one lane.
  >
  > **Climbs** ride on those chains. A climb is a maximal run of land-interface traversals with monotone
  > elevation change; each traversal carries a horizontal direction (interface midpoint to midpoint) and a
  > delta. A climb whose direction reverses past roughly 120° while still monotone is a
  > **switchback** — net displacement far shorter than path length, height packed into a small
  > footprint — against a **straight ramp**, where displacement is about equal to length; a flat piece
  > between two climbing segments is a **landing**. Label a climb by its top-end anchor (nearest wool room
  > → wool approach, a junction or mid piece → mid ascent, else interior) and by use per team (on an
  > enemy-spawn→wool path it is an attacker climb, on an own-spawn→wool path a defender rotation). That
  > gives the composer vocabulary the distinction it currently cannot make: a straight approach against a
  > space-packing switchback against a defensible landing.

  **The flow read exists as a prototype, and it settles three things about this entry** ("Flow in generated
  boards", 210 boards from `Composer.ComposeStages` with `PlanBoxAnnotation.Apply`, hub bodies and wool
  families off `StructureSummary.Derive`; the family census widened to 5200 boards / 9154 wools).

  *It confirms the term order and supplies the numbers.* **Redundancy** reads **zero on 97% of boards** —
  the only families bringing a second door onto the hub are U (0.79%), H (0.69%) and clamp (0.38%), and on
  those the two doors sit **3 cells apart on every board in the corpus**, so one barrier covers both. The
  term measures nothing until the spacing and the sampling weight move. **Spread** has a live distribution
  today: over the 93 boards whose spawn→wool crossing has two ways, the long way is a median **1.40×** the
  short, 22 exactly equal. `match-flow.md` §9 carries this.

  *It enumerates routes at **piece** fidelity, not at the junction/lane substrate above — and that is the
  open question.* Four attack routes on `p30-s374` (`band → front-t2 → front-t1 → hub-t1 → b-t2 → b-t1 →
  b-t4 → b-room` at 15 cells, and three more at 18, 23 and 26) come from two forks multiplying: two
  frontline legs × two doors into the wool. The count is a piece-adjacency fact; only the *lengths* are cell
  fidelity. **That graph is the composer's own piece cut**, which is exactly what junction regions on the
  unioned footprint exist to be independent of. On generated boards the cheap method is right, because the
  composer named the pieces. On a board whose pieces an author cut — a traced CTW plan, which is where this
  reading is most wanted — the same geometry cut differently gives a different route count. So the prototype
  validates the *terms* on the other graph and leaves the substrate's reason for existing untouched.

  *Three of its four measures need a primitive the repo does not have.* Traversal today is
  `Cells.ShortestPath`/`PathLength` over `SurfaceNav.Walkable` — how far, and whether connected. The
  **ribbon** (every cell on a route ≤130% of the shortest) needs the *distance field* `PathLength` computes
  and throws away. The **choke** needs unit-capacity vertex max-flow, which exists nowhere. **Ways round a
  void** has half of what it needs — `Cells.EnclosedVoid` finds the hole — and wants the ray-cut test.
  Route enumeration wants piece adjacency, which `ContactGraph` is, and therefore inherits **`G65`**: while
  `FannedGraph.LandAdjacent` and `ContactGraph` disagree on the overlap case, a route count depends on which
  graph was asked.

  *One negative result is worth carrying into the code when it lands.* Counting the connected components of
  the minimum cut is **not** the ways-round test and gives opposite answers in both directions — it reported
  that rotation between a team's objectives never splits on any ring board, where the homotopy test finds it
  splits on **81 of 84**. Same corpus, opposite answer. `match-flow.md` §2 records the rule; the reimplementer
  is the one who needs it.

  It is the derive side of a **third mirror**: the emit side assembles the intended story from the vocabulary
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

- **G82 / G83** — entry widening for Z along its bar interface; wool-approach budget law (per-slot caps /
  total path length). Reworded for the box path: the knob is `AttachmentWidth`, the law binds at allocation.

## Docs & hygiene

- **G139** — the seat-separation law has **no rule id**. It is a live, load-bearing composer law
  (`model.md` §5, *What keeps neighbours apart*, enforced in the allocator's seat step, surfaced
  through `Producibility` and a
  Contracts DTO) that `rules.md` does not mention, so nothing can cite it and no evaluator term
  scores it. Give it an id in the LN or WL family, or state why it is mechanism rather than law.
  (G124 is the sibling question — what the law *measures*; this one is whether it is a law at all.)


## Layout generation (G)

- [ ] **G158 — seed the library with a curated set.** An author can now build a style once and reuse it, and a
  theme that binds only the buckets it changes (`FEATURES.md`), but a fresh install's library is empty — so the
  first desert or snowfield is still built by hand. Ship a curated set of styles and themes as seed rows: the
  shipping finish decomposed, plus a handful of biomes (desert, tundra, mesa, nether) each reusing the same rim
  and fill. A preset is just a library theme, so this is a seeding step, not a second mechanism — the open
  question is only *when* it seeds (a migration, or a "restore the starter set" action that cannot clobber
  edits).

- [ ] **G150 — stamp a catalog shape into a drawn box.** The plan editor can draw a typed box and then ask
  whether the composer could have produced what is in it (G125's feasibility panel), but there is no way to
  go the other direction and *place* something known-producible: nothing in `Features/Plan/` references the
  catalog or the emitters. So an author hand-cuts rectangles and finds out afterwards. Give a selected box a
  **family picker** — the in-mix tier of `GET /api/shapes/catalog` (G144), which is exactly the set the
  composer really samples — plus the knobs `GET /api/shapes/probe` already serves per family, and stamp the
  emission into the box as its members. Producible **by construction**, so the feasibility panel goes green
  without the author aiming at it.
  Most of this exists. The probe endpoint already emits through `BoxFiller` (profile check and docking gate
  included) and answers with the shape or a directed `FillRejection`; `/api/shapes/probe/schema` already
  serves the per-family knob surface and minimum box in the dock frame. What is new is the *stamp*: writing
  the emission into an existing plan's box rather than returning a standalone `symmetry:none` plan, which
  means placing pieces at the box's origin, giving them ids under the box, and replacing whatever was there.
  This is the editor half of **B21's `emit_family`** — build it here and the MCP tool wraps it rather than
  reimplementing it. It also pairs with G149: placing known-producible shapes and watching the G148 land
  readout move is the most direct way to find out what the budget is actually worth.

- [ ] **G151 — a box's rect should be the bounding box of its members.** The members inspector offers
  "Fix these members" / "Follow containment", and the fixed half behaves oddly on purpose-built-for-something-
  else grounds: named membership (`PlanBoxes.MembersOf`, the `Members` list) ignores geometry entirely, so a
  piece can be dragged *out* of its box and still be carried when the box moves (`plan-canvas.js`, the box
  drag translates `d.carried` in both modes). That is not an authoring mode — named membership exists for
  **provenance**, so a pinned board can record the grouping that actually produced it off `BoxPartition.KeyOf`
  rather than having it re-derived approximately. Exposing it as a toggle asks the author to edit with a
  mechanism built to preserve history.
  The fix is not a third mode, it is separating two questions that are currently one button. **Which pieces
  are members** is legitimately two modes (named vs containment) and both should stay. **What the rect is**
  should not be a mode at all: it should always be the bounding box of the members. That is not a new rule —
  `BoxPartition.Of` already computes a box's rect as `Bbox(members)`, and `Box`'s own contract says a box's
  contents "must touch its edges", which is the same statement. So dragging a member extends the box,
  dragging the box moves its members, and a member outside its own box becomes unrepresentable rather than
  merely strange. One case to decide: an empty box has no bounding box — keep its drawn rect until it has a
  member, which also leaves the draw-then-fill flow working as it does now.

- [ ] **G153 — the feasibility read is per box and is reached through a list.** `G125` computes producibility
  **per box** and renders it in a left-panel list, so after clicking a box on the canvas the author has to find
  that same box again, by name, in a sidebar — for a read that is already about the thing under the cursor. The
  inspector on the right already opens on that box with its id, kind and members; the verdict belongs there,
  beside them: the parameter tuple that reproduces it or the nearest candidate, its directed findings, and the
  rule or task id each finding cites, with the click that paints the missing/extra cells kept as it is. The
  unit-level arrangement findings are genuinely **not** per box (parallel fronts, the frontline's pinned face,
  seat separation) and stay in the left panel, which leaves that panel one coherent job instead of a mixed list.

- [ ] **G149 — the land budget is a number the composer reads and then overshoots.** The first thing the
  G148 readout showed, measured over 40 boards at 12 players/team (budget 50 cells): land runs **63% to 222%**
  of budget, median **115%**, and **28 of 40 boards are over**. So the budget is not a cap — it is an input
  the allocator consults for its decisions (lane width, whether there is a frontline, the hub caps, the wool
  count — `TeamUnitAllocator.cs:46-52`, `UnitTuning.WoolCount`) and then nothing reconciles the result
  against it. `BoxFiller.WithinLandTarget` exists and no production path calls it.
  Nothing caught this because nothing measures it: the only land-ish term is `fill-ratio` (G8), which is land
  over the board's **bounding box**, not land against the **budget** — a different quantity that can sit
  happily in its band while the unit is at double its target.
  Decide what the budget means before changing anything, because both readings are defensible: either it is a
  target the fill should be reconciled against (then something must spend the overshoot down — the
  two-currency accounting says fragment converts surplus land to build, so the question is whether fragment
  ever runs), or it is only a sizing heuristic (then rename it, drop the "budget" framing, and stop implying
  a contract that does not exist). What is not defensible is the current state, where a number named budget is
  exceeded by half again on a typical board and nothing says so. Note G138 is adjacent but distinct: that one
  is about the composer taking the first acceptable plan rather than ranking; this is about the plan it takes
  not honouring its own sizing input.

- [ ] **G147 — verdict coverage on the catalog: which buckets has nobody judged?** *(sequenced after G118 —
  there is nothing to count until verdicts exist.)* The browse feed hands the author whatever the composer
  samples, so collection is passive: the corpus ends up shaped like the sampler, and the parts of the space
  the sampler rarely visits stay permanently unjudged. The measured skew makes that concrete — the donut is
  73 of the 89 wool cards while U, H and L are one apiece (G144) — so scrolling will produce a donut corpus
  and silence everywhere else, which is exactly the wrong input for rule refinement.
  The fix is to show the denominator. A `StructureNames.Canonical()` key is a triple
  (`wools:… | hub:… | front:…`) and the catalog already renders each component of that triple as a card, over
  a space now small enough to enumerate: 81 wool classes up to rotation/reflection, 7 hub forms, 3 frontline
  forms. So add a third filter facet beside *kind* and *reach* — **coverage** — with three states: *judged*
  (this bucket has verdicts), *thin* (one or two, not enough to trust), *unjudged* (nobody has ever looked at
  a board shaped like this). "Find me something nobody has judged" then becomes a chip click, and collection
  turns from an infinite scroll into covering a space with visible edges.
  Needs one query — verdict counts grouped by the structure key — which is a `GROUP BY` on the column G118
  already stores, so **design it with G118's schema rather than bolting it on after**. The UI is small: a chip
  row and a count badge over the same tally plumbing the catalog's `ByTier`/`ByFamily`/`ByKind` already use.
  **A card is a component of a bucket, not a bucket** — a board reading `wools:donut,l` touches both the donut
  and the L card — so per-card coverage is an aggregate over every bucket that card participates in. Build the
  aggregate first (cheap, and enough to spot a blind spot); add a per-bucket drill-down only if the aggregate
  proves too coarse to act on.

- [ ] **G145 — five emitter knobs are unreachable from the composer.** `ShapeEmitter.Emit` takes
  `attachments`, `woolExtend`, `entryShift`, `woolShift` and `attachmentOffset`, and `WoolBoxEmitter.Emit`
  passes all five through — but `WoolBoxEmitter.Fill`, the only path `BoxFiller` and therefore the whole
  compose pipeline uses, forwards none of them (`WoolBoxEmitter.cs`, the `ShapeEmitter.Emit` call inside
  `Fill`). Their only callers in the tree are `tools/compose/box-gallery.cs` (the two-attachment and
  moved-attachment donut cards). So the two-attachment donut, the extended-wool donut and both scythe
  endpoint shifts are built, tested, drawn in the galleries, and **cannot appear on a generated board**.
  Decide per knob rather than in bulk: the donut's second attachment is a genuine multi-access shape the
  hub could dock twice and is the strongest candidate to plumb; the scythe shifts are moot until the
  scythe itself is admitted (G146). Plumbing one means widening `WoolFill` (it already carries
  `AttachmentWidth` and `RingWalls`, so the shape of the change is settled) and giving `UnitRequests` a
  draw for it. **Do not "fix" this by deleting the knobs** — `EmitterPlacementKnobTests` gates them and
  `model.md` §4 describes them; the gap is the plumbing, not the geometry.

- [ ] **G146 — two families are in the vocabulary but never on a board.** The emitter builds eight
  terminal-capped families; the composer puts six on boards. **Z** is listed in
  `FillMenu.ProductionFamilies` and `BoxFiller` fills it happily, but `UnitRequests.WoolRequest` never
  draws it — the rich branch picks donut, then U/H/clamp, else L, and the fallbacks are I. The only caller
  that could reach it is the roll-indexed `BoxFiller.Fill(box, mouth, cw, roll, …)` overload, which has no
  caller in `src/` at all. So the menu advertises a family the sampler cannot produce, and the browse
  tool's own filter chip for it can never match. **Scythe** is the honest case: excluded from
  `ProductionFamilies` with a stated reason (its bay's mouth is its docking edge, so a flush dock seals it
  into WL8's forbidden enclosed void), with the elevation-stage alternative already parked as G81.
  Two separate decisions: either give the sampler a Z draw or drop Z from `ProductionFamilies` so the menu
  stops advertising it (the second is a one-line honesty fix and should not wait on the first); and leave
  the scythe out until G81 lands. Either way the catalog page (G144) will render both under a
  *reachable* / *emitter-only* badge, so the gap becomes visible rather than folklore.

- [ ] **G138 — The composer accepts, it never chooses: a soft score has nowhere to act.** `Composer` takes
  the **first** plan that clears the gate and `break`s (`Composer.cs:59-84`) — no ranking, no comparison, no
  best-of-K. It contains zero references to `Evaluate` or `Score`, only `Gate`, and `Gate` runs hard terms
  only (`LayoutEvaluator.cs:86`). Worse, the compose path builds its context as `EvalContext.Build(plan)`
  with no envelopes, which defaults to `SeedEnvelopes.**Empty**` (`EvalContext.cs:34-38`) — so every soft
  term looks its band up, gets null, and stays dormant by design. **The authored envelopes have no causal
  influence on generated output whatsoever**; they only score plans after the fact, via the
  `Evaluate(PlanModel, …)` overload the API endpoints and galleries call.
  So any soft rule derived from `G118`'s verdicts is **inert until this lands**: generate K candidates,
  score all K, return the best. The loop already generates and discards candidates, so the change is small —
  but it converts the composer from *first-acceptable* to *best-of-K*, which is a real behaviour change and
  will move every fingerprint.
  **Sequence it after the bands are calibrated, not before.** Measured over 560 composed plans, a ranking
  today would be almost entirely a `spawn-wool-ratio` contest (outside its band on 44% of applicable plans
  at median distance 1.64) while four terms score nothing at all — see the `LEARNING.md` debt entry.
  Ranking before recalibration just amplifies one badly-fitted band. Order: `G118` collect → calibrate /
  gate the vacuous terms → this → soft rules become causal.

- [ ] **G165 — dock arrangement belongs in the structure summary.** Which face of the hub each box seats on
  is a board property with measured consequences and no representation anywhere: it is not the hub's body
  form and not the approach family. With the compass rotated so the frontline is *front*, generated boards
  split **canonical** (spawn *back*, wools *left*+*right*) 27% against **lopsided** (spawn lateral, one wool
  on *back*) 73%, and the split predicts two things — the median spawn-distance imbalance is 0.18 against
  0.40, and the second-wool rotation runs within ten blocks of the spawn on 63% of canonical boards against
  2% of lopsided ones. The faces fall straight out of the mouth positions the box read already computes, so
  the work is small: add them to `StructureSummary` and to `StructureSummary.Canonical()`, which makes the
  arrangement a browse-sieve filter and a verdict/duel bucket key for free. **Land it before verdicts
  accumulate**: `Canonical()` is persisted on a pinned plan as that bucket key, so extending the string
  reshapes every bucket already stored, and a later change needs a key version rather than an edit.

- [ ] **G166 — seating should prefer the canonical arrangement.** `UnitSeating` chooses which hub edge each
  neighbour request seats on, and takes no view on the combination; the result is that three boards in four
  come out lopsided (G165). Prefer the spawn on the edge opposite the frontline with the wools on the two
  lateral edges — the arrangement built maps converge on. The measured payoff is the imbalance halving (0.40
  → 0.18) and the restoration of the rotation-past-spawn dynamic that the lopsided arrangement removes. This
  changes where boxes sit, so it is a geometry change: composer version bump, re-recorded fingerprints, and a
  before/after gallery. Constraining the seat choice can only raise the rejection rate, so measure that
  alongside the arrangement split rather than assuming it stays flat.

- [ ] **G167 — a holed hub should seat its docks across the hole.** A ring, double-hole, P or G hub only
  offers two ways across when the two docks straddle its void, and today that is left to chance: ring hubs
  deliver two ways on 163 of 224 spawn-to-wool crossings, and the ones that do not are dead by seating rather
  than by shape — the same body form with both docks on one side is a wide room with a decorative hole in it.
  When the sampled hub body encloses a void, prefer opposite walls for the two docks. The value is not the
  extra distance but what G164 measures: the far way round drops interference from 76% to 37%, which is
  the difference between an alternative and an alternative worth taking. Geometry change, so the same
  fingerprint and gallery costs as G166, and the two should land together or in a known order since both
  touch the same seat choice.

- [ ] **G168 — a board is worth evaluating in two game states.** A two-wool map is not one arrangement but
  two in sequence: before the first capture both objectives are defended from the spawn, and after it one
  room is the attacking team's forward node — a place worth travelling to for the chest gear the generator
  emits — and the wool-to-wool route becomes the live one. Terms that are vacuous in the first state carry
  the whole second phase, so evaluating only the opening scores half a match. This is a change to the
  evaluator's shape rather than a new term: `EvalContext` carries which state is being read, and the terms
  that only apply post-capture (G164's interference, rotation between objectives) declare it. Decide
  early whether the two states produce two scores or one combined figure — a single number that averages a
  strong opening against a hopeless second phase describes neither. The played account is in
  `docs/gameplay/match-flow.md` §4.8.

### The generator in the studio (G117–G120) — parked while the authoring loop is the focus

The box pipeline is **the** composer and the emitted layouts are good enough to work *with*, so the
bottleneck sits in the feedback loop rather than in the grammar. This slice integrates the generator into
the studio itself — compose interactively, filter what to see, and **collect annotated keep/discard
verdicts** that become the labeled positive/negative corpus every later refinement feeds on. The showcase
(G121), the persistence foundation (G119), browse mode (G117), its structural sieve (G128) and the shape
catalog page (G144) have shipped (`FEATURES.md`); verdicts are next when the theme resumes.

**Persistence doctrine for the whole slice: the feed is ephemeral; only human attention persists.** A plan
enters the database exactly when it is voted on, pinned, or saved from the editor — never while scrolling.
Generated rows are **immutable**: editing one forks a new `authored` row with a `parent_id` back-reference,
so the labeled corpus cannot be contaminated after the fact. Browse votes (absolute) and duel results
(pairwise preference) are **separate datasets**, unified only at analysis time.

- [~] **G159 — a composed plan should carry its voids before it is compiled.** The compiler declares them on
  every compile (`PlanVoids`, `FEATURES.md`), so a board's holes are correct wherever it is built. What a
  freshly composed plan does not yet carry is the declaration itself: `Composer.Compose` returns pieces only,
  and the buffers appear when something compiles it. Running the same step at the end of `Composer.Assemble`
  makes a generated plan self-describing from birth — one line, no new geometry, and it cannot disagree with
  the compiler because it is the same step. The cost is the reason it is not folded in already: the composer
  fingerprint digests the plan JSON, so every board's digest moves, which means a `ComposerVersion` bump and a
  re-record of `tools/compose/composer-fingerprints.json`. Worth doing on the next version bump rather than
  spending one on annotation.

- [ ] **G118 — Verdict collection.** Tap-chip annotation tags (large toggleable pills, multi-select —
  never checkboxes) available on both vote directions, both optional; the tag set seeded from the
  layout-rules vocabulary (wools-too-close · wools/spawns-should-swap · flat-front · crammed-mid ·
  no-rotation · great-hub · …, extendable), each tag carrying its rule id where one exists — a
  downvote tagged with a rule whose term did *not* fire is a ready-made evaluator bug report. Persist
  {plan ref, descriptor, verdict, tags, free-text note, evaluator score + per-term snapshot, evaluator
  version} via G119; JSONL export so the labeled examples drive rule refinement, envelope
  regeneration, and AI-assisted analysis.

- [ ] **G120 — Duel mode (the tournament).** Bucket-scoped side-by-side comparison: a **bucket** is a
  filter combination (e.g. 2 wools · F frontline · double-hole hub · one L + one donut), so both
  boards made broadly the same structural decisions — the closest thing to a controlled comparison,
  and a minimal-pair factory for the evaluator's labeled set. Two big renders, pick the better; the
  result is a **preference pair** `(winner, loser, bucket)` — never converted into a downvote — with a
  per-bucket ranking (Bradley-Terry/Elo-style) derived at analysis time. A separate dataset from the
  browse votes by design.

  - [ ] **B40 — The three dock styles are implicit; make them a type.** `Seat` picks between three seating
  rules using three *different discriminators* — `d.Wool is { } rich && Overhangs(rich.Family)` (shape
  family), `d.Kind == BoxKind.Frontline` (box kind), and falling through (everything else) — so there is
  nowhere in the code to ask which style a demand uses. The asymmetry runs deeper than the selector:
  two styles are named functions and the third is ~30 lines of inline loop body with no name; and the three
  are at different altitudes — `SeatOverhang`/`SeatFront` return a placed `(CellRect, BoxInterface)` while
  `SeatInRuns` returns a bare `int?` seat, leaving the caller to build the rect and the joint. That is why
  `boxes.Add`/`joints.Add` is written out three times with different arguments.
  **Scope:** (a) extract `SeatFullMouth` returning `(CellRect, BoxInterface)?` like its siblings, then (b)
  add an explicit `DockStyle { FullMouth, Overhang, ContactPatch }` **derived** from the demand — it is never
  sampled, it follows from the family roll — and dispatch on it. **Leave the failure policies alone**: they
  genuinely differ in kind (overhang demotes via `Compact`, the frontline kills the attempt, a wool may be
  dropped if another remains), and flattening them into flags loses more than it gains.
  The doc comment writes itself, because the three styles are indexed by *how much is known about the
  shape's entries*: full mouth knows nothing (require the whole mouth on one run and every entry lands —
  which is why the two-entry `U`/`H`/`Clamp` dock here); overhang knows there is exactly one and where it
  is; contact patch has no entry at all because a frontline is a face, not a corridor.
  **Oracle + the real constraint:** `ComposerFingerprint` + `ComposerVersionTests` must stay byte-identical.
  All three styles consume `rng`, so the invariant is not "does it compile" but **does the draw order
  survive** — hoisting one call above another moves the stream and every fingerprint goes red.

- [~] **B41 — Should a host's published capacity bound the grant it hands out?** The naming half has landed
  (`FEATURES.md`): `BoxJoint.Grant` is now distinct from a host's `EdgeOffer`, and the docstrings state the
  split — an offer's `WidthClass` is a *capacity* derived from the run's length, a grant's is a *selection*
  made per consumer kind. What remains is the behaviour question the rename deliberately did not answer.
  Today the two are entirely unlinked: `Seat` reads the hub's offers, keeps only `(Start, LengthCells)` as its
  **runs** and drops the published width, then `HubJoint` grants a width taken from the demand's kind
  (`WoolLaneCells` for a wool, `w` otherwise). So a hub can grant a corridor **wider than the run it sits on
  claims to support** and nothing objects. Either that is intended — capacity is advisory, the consumer knows
  its own lane — or the grant should be clamped to the offer, in which case a narrow run would demote a
  consumer's `cw` and some docks that succeed today would not.
  **Measure before deciding**: how often does the granted width actually exceed the published capacity of the
  run it lands on? If never, this is documentation; if often, it is a real gate the composer is missing.
  Changes what the filler builds, so it needs a before/after gallery and will move fingerprints.
