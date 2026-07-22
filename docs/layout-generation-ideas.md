# Layout generation — the idea pool

The condensed long tail of the layout-generation track. The task board (`TODO.md`/`BACKLOG.md`) had
accumulated ~40 open G-tasks looking far ahead — many describing machinery that no longer exists after the
old grower path was retired and the box pipeline became *the* composer. This file replaces that backlog
section: **one idea per line-or-three, grouped by theme, ids preserved** (an id here never gets reused; the
full original task text is in git history under `BACKLOG.md`/`TODO.md`). When an idea becomes the focus,
pull it back onto the board by id.

Status markers: *(obsolete)* = described the retired grower path and is settled or moot; *(partial)* = part
landed, the rest is the idea.

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

- **G64** — the `map-generation.md` doc pass (reconcile with shipped code; §2/§4 still describe the old
  order of operations in places).
- **G99** — delete the superseded `shape-vocabulary.md` + repoint its code citations.

## Marker & objective knobs (plan editor)

- **G76** — the marker inspector exposes a structure's knobs (destroyable styles, core size/shell,
  wool colour) instead of silently defaulting.
- **G77** — `bedrockCentre` is a stamp no authoring path can reach: thread it through or delete it.
