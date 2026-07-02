# Layout generation — plan-then-realize design

Direction doc for the `G`-series: how full map-layout generation becomes possible. Grounded in the
three test seeds (`tools/seeds/`), the corpus studies (`docs/generator-archetypes.md`,
`docs/contracts/lane-decomposition.md`), the Organic generator
(`docs/contracts/organic-lane-generation.md`), and the sketch/authoring tool vocabulary. The core
claim: **separate the map's topology (the plan) from its geometry (the realization)** — generation,
mutation, corpus mining, and validation all become tractable at the plan layer, and the "boring
seeds" problem is revealed to be a realization problem, not a topology problem.

## 1. What the seeds get right — and what they miss

The three seeds (`base-2island`, `base-2wool`, `base-4team`) encode the *relational* skeleton of a
normal CTW map correctly:

- **Spawn/wool lane separation.** Spawn sits on one bar of the distorted H, the wool on the other;
  an attacker entering from the frontline crosses the crossbar and never squeezes past spawn
  protection. This is the corpus rule (wools at lane tips, spawn on its own spur/hub — 10 of 13
  maps ≤ ~5 blocks wool→tip; spawn never in a wool's lane).
- **The contested middle.** Raised neutral squares + a build band spanning the void = the
  **open-mid** pole of the documented build-region topology axis. The `base-2wool` bridge rect that
  is the *only* connection to the L-shaped wool island = the **directed-flow** pole. Both poles are
  present across the seed set.
- **Isolation via buildable space.** The L island reachable only through a bridge region is the
  general motif: *a typed gap on the path to an objective*. The same device relocates freely — an
  isolated spawn, two bridges feeding one isolated frontline piece, a mid island reachable from
  both sides.
- **Symmetry as the team fan.** One authored unit, `rot_180`/`rot_90` images for the other teams;
  spawn/wool/build coords are orbit images. Exactly how the studio (and real maps) work.
- **Elevation as plateaus.** Flat per-shape heights (H at 9, squares at 13) — the raised contested
  centre is already a flow statement.

What they miss, corpus-quantified:

| dimension | seeds | corpus (N = 347 / 13-map lane study) |
|---|---|---|
| islands per map | 2–3 pieces per side | median **9**; 91% have ≥1 neutral island, median 4–6 gameplay-sized |
| neutral mid-set | 1 square per side | several small/medium pieces (median piece ≈ 4% of team island), 66% mirror-paired, 38% central / 62% flanking |
| bend vocabulary | 100% right angles | **45°-family dominates (42%+21%+20%)**; right angles only 12% |
| outline | grid-aligned polyomino | variable width, jittered edges, straight runs median ~10 blocks between bends |
| holes | none | ~10% of islands carry a hole (diamond lane-loop) |
| elevation | flat plateaus only | raised spawns, ramped wool approaches, within-island cuts |

**Verdict: the seeds are a good indication of how a normal CTW map is *wired*, and a poor
indication of how one *looks*.** That factoring is not a defect — it is the architecture. The seeds
are hand-instantiated examples of the plan layer described next.

## 2. The four layers

### Layer A — the plan (topology graph)

A small, explicit, persisted artifact. Globals + one per-team unit graph:

- **Globals:** symmetry mode, team count, flow axis (`directed` | `open-mid` — the
  lane-decomposition axis), board scale, base lane width, void distance.
- **Unit graph nodes** (roles from the decompose rubric): `hub`, `spawn`, `wool-tip` × k,
  `trunk-tip` (frontline contact) × m, plus the neutral mid-set (standalone `stepping-stone`/`mid`
  nodes with size-class, central/flanking position, mirrored-pair flag).
- **Edges, typed:** `lane` (land — a walkable corridor) or `gap` (void — crossable only through a
  build region). Every "reachable only via build" fact is a `gap` edge on that path. Bridging count
  per team = number of `gap` edges reaching the frontline = angles of attack.
- **Elevation attributes** live on nodes/edges (plateau level per piece, `ramp` flag per lane edge,
  optional `cliff` = one-way drop), not in the geometry.

Plan invariants are checkable *before any geometry exists*: every wool reachable from every
capturing team's spawn; no wool path through a spawn node; ≥1 `gap` on every inter-team path; the
mid-set respects the flow axis. This is where the traversability/monument gates the export
pipeline already enforces move upstream — reject bad topology in milliseconds, not after
rasterization.

The three seeds re-expressed as plans become the first three entries of the **plan library**.

### Layer B — geometric realization

Turning the graph into `SketchLayout` polygons. Two realizers, both half-existing:

1. **Skeleton realizer** — place nodes (hub placement + farthest-tip spreading, as `OrganicLane`
   does today), route edges as spines using the corpus alphabet (straight runs ~4–28 blocks, 45°
   family bends, rare right angles), ribbon them with variable width (`Geom.Lane.Ribbon`), stamp
   hub plazas and ~10%-rate holes. This is `OrganicLane` refactored to *consume a plan* instead of
   implying one — its ribbon/hub/hole primitives are the realization vocabulary; its current
   weakness ("interesting but lacking context") is precisely that its topology is implicit,
   single-island, and uncontrollable.
2. **Seed-distortion realizer** — realize the plan rectilinearly (what the seeds are today), then
   apply a **roughen pass** of distortion operators:
   - *anchor jitter* — displace existing vertices by bounded noise;
   - *edge subdivision + displacement* — insert mid-edge anchors, push them along the edge normal
     (1–2 fractal levels) — organic outlines with zero topology change;
   - *pull-to-polygon* — one strong anchor displacement that breaks a rectangle into a believable
     quad (the "twist");
   - *width profile* — per-station width multiplier along a lane spine (thin necks, wide rooms);
   - *45° chamfer* — convert right-angle corners into 45° pairs, driving the bend histogram toward
     the corpus distribution;
   - *piece shear/rotate* — a few degrees around the centroid.

   Every operator carries invariants: minimum corridor width preserved (offset test), no
   self-intersection, objective anchors stay interior with margin, `gap` widths stay within the
   bridgeable range, and operators touch only the authored unit (symmetry re-fans the images).

Both realizers emit ordinary `SketchShape`s, so every generated intermediate is hand-editable in
the sketch editor — the generator is a collaborator, not a black box.

### Layer C — elevation

Vocabulary drawn from tools that already ship:

- **plateau** — per-shape `floor`/`base_height` (rasterizes today);
- **cut + raise** — split a piece along a seam (the decompose-cut seam machinery) and offset the
  cut piece's floor — "sections of an island raised";
- **ramp** — `anchor_heights` gradient along a lane (TIN-interpolated) — "elevation changes toward
  the wool";
- **layer** — `base_y`-stacked slabs for genuinely overlapping structure.

Grammar defaults driven by node roles: spawn +2..+4 (overview), wool approach ramps up toward the
tip (harder approach) *or* a raised attacker shelf beside it (high ground for the push — both are
corpus-real, choose per plan), trunk tips lowest (bridges launch low, the raised interior
overlooks the crossing), mid pieces at varied levels. Constraint: along any `lane` edge the
walkable step stays ≤1 block per cell unless the plan marks a `cliff` — an intentional one-way
drop is a *flow-control device*, not an error.

### Layer D — intent wiring

Exists. `LaneMapGenerator` + the export pipeline (auto-wired monuments, spawn cubes, wool cages,
observer platform) already turn objective positions into a valid, gated `map.xml` + world. Plan
nodes map 1:1 onto intent objects; `gap` edges become bridge build rects (`AcrossBridge` /
`AutoBridge` today).

## 3. Why plan-then-realize beats WFC and beats pure seed mutation

- **WFC** solves local-adjacency plausibility on a grid. CTW quality is *global and relational* —
  symmetry, spawn/wool separation, typed gaps on objective paths, angles of attack. WFC output
  would need all of those enforced by rejection afterwards, and its native output is exactly the
  tile-grid look we are trying to escape. Verdict: wrong tool for the skeleton. Possible later
  niche: intra-piece block-level detailing/theming once a layout exists.
- **Pure seed mutation** is tractable and editor-friendly but never leaves the seeds'
  neighbourhood, and it needs the operator + invariant machinery anyway.
- **Plan-then-realize subsumes both.** Seeds = hand-authored plans. Mutation = operators at either
  layer — *plan operators* (add a wool branch, re-type a lane→gap to isolate a piece, add a
  mirrored mid pair, flip the flow axis, move the spawn onto an isolated piece) change what the
  map *is*; *realization operators* (the distortions) change how it *reads*. Generation = sample
  plan → realize → elevate → wire. One re-rolls each layer independently: same plan, new geometry;
  same geometry, new elevation.

**The corpus flywheel.** The decompose surface (manual cut tool + role rubric) extracts exactly
this plan graph from real maps — lane pieces, roles, build-region contacts. Finishing that
labeling turns ~348 corpus maps into a mined plan library plus empirical parameter distributions
(lane widths, branch counts, mid-set sizes, elevation deltas). And validation closes the loop the
same way the intent model does (generator = mirror of the categorizer): **generate → rasterize →
island detection + skeleton decomposition → recover a plan → compare with the input plan**, plus
distribution scoring against the corpus stats (island count, bend histogram, wool→tip distance,
neutral-piece size mix). A layout that round-trips and scores in-distribution *reads as a map* by
construction.

## 4. Open questions (decide before building)

1. **Interaction model.** One-shot generate-then-edit, or staged — approve/edit the *plan* first,
   then re-roll realization/elevation independently? Staged needs a small plan UI but matches the
   wizard philosophy and makes iteration cheap. Lean: staged.
2. **Fitness.** When two candidate layouts exist, what ranks them? Corpus-likeness
   (distribution match) vs playability proxies (attacker path length vs defender response
   distance, choke width, bridge exposure length, high-ground coverage of crossings). Which 2–3
   metrics does the author actually trust? This decides whether sampling suffices or search
   (annealing over plan/realization operators) sits on top.
3. **Persist the plan?** Third artifact beside layout + intent JSON, shared by generated and
   corpus-mined plans. Lean: yes — it is also the mutation substrate and the round-trip oracle.
4. **Elevation scope for v1.** Plateaus + cut&raise only, or ramps too? Are intentional cliffs
   (one-way drops) v1 flow devices?
5. **Distortion authority.** Are objective anchors pinned (geometry warps around them) or may a
   warp re-inset a wool/spawn afterwards? Pinning is simpler and keeps plan↔geometry agreement.
6. **Plan-library bootstrap.** Hand-write ~8–12 archetype plans now from the corpus studies
   (fast, curated), or finish decompose labeling first and mine them (slow, empirical)? Lean:
   hand-write now, mine to enrich later.

## 5. Phasing sketch

1. **Plan schema + validator**; re-express the three seeds as plans; a plan→rectilinear realizer
   that reproduces today's seeds exactly (the regression anchor).
2. **Distortion operators + invariants**; roughen the seed realizations into organic-reading
   variants; iterate visually in the sketch editor.
3. **Elevation grammar** (plateaus, cut&raise, role defaults; ramps if in scope).
4. **Plan-driven skeleton realizer** (fold `OrganicLane` into it) + the neutral mid-set the
   corpus demands.
5. **Round-trip validation harness** + corpus-stat scoring; then sampling/mutation surfaced in
   the UI.
