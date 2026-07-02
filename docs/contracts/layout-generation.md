# Layout generation — piece-and-interface plans, rule-based composition

Direction doc for the `G`-series: how full map-layout generation becomes possible. Grounded in the
three test seeds (`tools/seeds/`), the sketch/authoring tool vocabulary, and the author's design
experience — which is the source of the rules. The core claim: **separate the map's topology (the
plan) from its geometry (the realization)**, with the plan expressed as **areal pieces joined by
edge-interval interfaces** — never as a thinned skeleton — and grow generation rule-based from the
deliberately boring rectilinear base the seeds establish.

## 1. Ground rules (decided)

- **The corpus is not an oracle.** The extraction pipeline flattens maps to 2D and loses exactly
  what made them read (spawn structures, wool rooms, terrain, decoration); the decompose tool only
  ever worked on the rectilinear cases; and map quality across the ~350 maps is too mixed to treat
  the distribution as a target. Corpus material is reference imagery at most. The rule set is
  **expert-authored** — encoded from the author's map-making experience, not mined.
- **No skeletons.** A medial-axis / thinned-line representation fails twice. Representationally: a
  build region touching the long side of a wide rectangle projects onto *no* node and no labelable
  skeleton edge — side contact is an interval property that a 1D spine cannot carry. Generatively:
  a system that thinks in spines-with-ribbons is structurally biased toward thin maps, because
  width is only ever a decoration on a line. The plan below is areal by construction.
- **Start from the boring case.** The seeds — rectilinear, grid-aligned, minimal — are the real
  starting point. They already encode the load-bearing relations (spawn/wool lane separation,
  contested raised centre, isolation via a bridge-only connection, symmetry fanning one authored
  unit). Generation grows outward from that base by rules; the organic look is a later,
  separate pass.

## 2. The plan: pieces and interfaces

A small, explicit, persisted artifact. Globals + one per-team unit + the neutral middle:

- **Globals:** symmetry mode, team count, board bbox, coarse grid cell (the seeds' 5-block raster
  cell), base corridor width.
- **Piece** — a rectangle on the coarse grid (polygons later), with:
  - a **role**: `spawn`, `hub`, `lane`, `wool-room`, `mid`, `frontline`;
  - a **plateau height** (its floor level);
  - optional **placements**: objective anchors (spawn point, wool, monuments) as piece-local
    offsets — content is pinned to its piece, so later geometry warps around it.
- **Interface** — a shared **edge interval** between two pieces (or between a piece and a build
  zone). Not a point, not a node: it has a position *and a width*. Kinds:
  - `land` — the pieces merge into one walkable landmass;
  - `gap` — void between the pieces; a build region spans it, with a span distance (the bridge
    length). A piece whose interface set contains **no** `land` entries is *isolated* — reachable
    only by building. That single rule expresses every isolation motif: isolated wool room,
    isolated spawn, an isolated frontline piece fed by two bridges.
  - an elevation transition rides on any interface: `step` (walk up), `ramp` (gradual), `cliff`
    (one-way drop — a deliberate flow device).
- **Build zones** are pieces of a special kind (`buildable void`): the open-mid band is one wide
  zone touching many pieces along long intervals; a directed bridge is a narrow zone touching two.

**Why interfaces carry the design.** The width of an interface is the width of the corridor or
front it creates: a 40-block `gap` interface along a rectangle's whole side is an open front; a
5-block one is a chokepoint. *Player-flow control is the distribution of interface widths along
the attack paths.* The old "directed-flow vs open-mid" axis stops being a category and becomes
geometry.

**Plan invariants** (checkable with zero geometry): every wool reachable from every capturing
team's spawn across `land`+`gap` interfaces; no wool path passes through a `spawn` piece; ≥1 `gap`
on every inter-team path; interface widths ≥ the corridor minimum; spawn depth ≥ some distance
from the nearest frontline interface. The export pipeline's traversability/monument gates stay as
the backstop, but bad topology is rejected at the plan in milliseconds.

**The seeds are already plans.** `base-2island`'s H = three rect pieces (two bars + crossbar) with
two `land` interfaces; the raised square = a `mid` piece whose every interface is a `gap`;
`base-2wool`'s L-island = a `wool-room` piece with exactly one interface, a narrow `gap` (the
bridge). Re-expressing the three seeds as plans is the schema's first test.

## 3. Generation: rule-based composition on the coarse grid

1. **Globals** — symmetry, teams, board, grid cell.
2. **Grow the team unit** by attachment rules: place the `spawn` piece; attach the `hub`; grow k
   `lane` pieces, each dead-ending in a `wool-room`; attach `frontline` pieces toward the middle.
   Each rule states which roles may attach to which, on which sides, with what interface width and
   what depth ordering (wool behind hub, spawn off the wool paths, frontline nearest mid).
3. **Optional isolation moves** — convert a `land` interface to a `gap` (+ its bridge zone), or
   attach a new piece by `gap` only.
4. **Neutral middle** — place `mid` pieces and build zones between the frontlines.
5. **Validate** the plan invariants; reject/repair.
6. **Heights** — per-role plateau defaults (spawn raised for overview, wool approach stepping up
   toward the room, frontline lowest so the interior overlooks the crossing), transitions chosen
   per interface.
7. **Fan by symmetry, emit** `SketchLayout` + `MapIntent` — the existing seed pipeline
   (rasterizer, auto-wired monuments, spawn cubes, wool cages, export) unchanged.
8. **Roughen pass** (separate, last) — see §4.

Mutation operators fall out of the same vocabulary, at two levels: *plan operators* change what
the map is (add a wool branch, isolate a piece, widen/narrow an interface, re-side the spawn,
raise a plateau); *distortion operators* change how it reads. Each is small, rule-checked, and
leaves a valid plan — so "mutating default seeds" is the generator's inner loop, not a rival
approach.

**Why not WFC.** WFC solves local-adjacency plausibility on a grid; CTW quality is global and
relational (symmetry, spawn/wool separation, isolation, interface-width flow control), which WFC
can only enforce by post-hoc rejection — and its native output is exactly the tile-grid look the
roughen pass exists to escape. Possible later niche: intra-piece block-level detailing once a
layout exists. Not the skeleton of the system.

## 4. The roughen pass: rectilinear plan → organic read

Applied to the realized polygons of the authored unit only (symmetry re-fans the images); plan
meaning is frozen, objective placements are pinned. Operators:

- **anchor jitter** — displace existing vertices by bounded noise;
- **edge subdivision + displacement** — insert mid-edge anchors, push along the edge normal (1–2
  fractal levels): organic outlines, zero topology change;
- **pull-to-polygon** — one strong anchor displacement that breaks a rectangle into a believable
  quad (the "twist");
- **width profile** — vary a lane piece's width along its length (thin necks, wide rooms);
- **45° chamfer** — soften right-angle corners into diagonal pairs;
- **piece shear/rotate** — a few degrees around the centroid.

Invariants per operator: minimum corridor width preserved (offset test), no self-intersection,
placements stay interior with margin, `gap` interface spans stay within the bridgeable range,
interfaces stay covered (distorted neighbours still overlap their shared interval). Output is
ordinary `SketchShape`s — every intermediate stays hand-editable in the sketch editor.

## 5. Elevation

Vocabulary from tools that already ship: per-shape `floor`/`base_height` (plateaus), splitting a
shape along a seam and offsetting the piece (**cut + raise** — at plan level this is refining one
piece into two joined by a `land` interface with a height delta), `anchor_heights` gradients
(ramps), stacked layers. Rules attach elevation to roles and interfaces, not to geometry: raised
spawn (overview), stepped approach toward a wool room (harder push), low frontline (bridges launch
low, defenders hold high ground), `cliff` interfaces where one-way flow is wanted. Constraint:
walkable steps along any `land` path unless the plan says `cliff`.

## 6. Open questions

1. **Plan resolution.** Is the coarse grid cell the seeds' 5-block raster cell, and are all piece
   bounds + interface intervals authored at that resolution?
2. **The rule set itself.** The attachment rules, minimum widths, depth orderings, and elevation
   defaults must come from the author's experience. Fastest capture: a working session that turns
   "years of making these" into an explicit checklist (per role: what it may touch, how wide, how
   deep, how high) — that checklist *is* the generator's content. When?
3. **Build zones as pieces** — confirm modeling build regions as first-class plan pieces (vs
   attributes on `gap` interfaces). Pieces make the open-mid band and multi-piece contact natural;
   it's the current lean.
4. **Interaction model.** One-shot generate-then-edit, or staged: approve/edit the plan (a small
   form/canvas over rects), then re-roll roughening and elevation independently? Lean: staged —
   every layer stays editable and re-rollable.
5. **v1 scope.** Plateaus + steps only, or ramps and cliffs too?

## 7. Phasing

1. **Plan schema + validator**; re-express the three seeds as plans; a plan→rect realizer that
   reproduces today's seeds exactly (the regression anchor).
2. **Rule capture** (the §6.2 checklist) → attachment rules + invariants encoded.
3. **Composer** — rule-based unit growth + neutral middle + isolation moves; every output passes
   the validator and the existing export gates.
4. **Roughen pass** operators + invariant checks, iterated visually in the sketch editor.
5. **Elevation rules** (plateaus, cut&raise, role defaults; ramps/cliffs if in scope).
6. **Mutation surfaced in the UI** — plan ops + re-roll buttons per layer.
