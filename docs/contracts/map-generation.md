# Map generation — the canonical model, terminology, and pipeline

This is the **single source of truth** for how pgm-studio generates map layouts: the vocabulary,
the pipeline and its order, and how every part fits the next. Every word defined here has **exactly
one meaning**; where a term appears elsewhere it carries this meaning. When another doc and this one
disagree, this one governs.

**What this document owns:** the glossary, the pipeline, the box model, the shape families, the two
derivers, the evaluator model, and the budget/width model. **What it defers:**

| Companion | Owns |
|---|---|
| `layout-rules.md` | The frozen rule law and every number (widths, depths, hop counts, heights, the CT / SP / WL / LN / HB / FR / MD / BZ / EL ids). |
| `seed-stats.md` | The measured envelopes the soft evaluator terms score against. |
| `plan-editor.md` | The field-level `*.plan.json` schema and the editor UI. |
| `layout-evaluator.md` | The detailed deriver-measurables and evaluator-metric catalogue. |

---

## 1. Glossary — the locked terms

### 1.1 The five pipeline verbs

Generation is five verbs, not one. **Never say "generate" for the whole thing** — it is ambiguous
(it has been used both for the whole pipeline and for the narrow `intent → map.xml` step). Use the
specific verb:

| Verb | Means | Where it lives |
|---|---|---|
| **emit** | Fill one box with one base shape (forward). | `WoolBoxEmitter` |
| **derive** | Read structure back out of geometry (inverse). Two derivers — see §1.3. | `WoolApproachShape`, `PlanDerived` |
| **compose** | Build the plan: `budget → boxes → emit → join → embed`. | the composer (`Composer`) |
| **evaluate** | Validate + score a plan → `(score, [violations])`. | the evaluator |
| **realize** | Compile the plan → sketch + intent → roughen + elevation → export. | the seed pipeline |

`emit` and `derive` are a **forward/inverse pair** at the shape level: compose *emits*, verification
*derives*, and the two must agree (the mirror loop, §5.4).

### 1.2 Family — a wool-approach shape

A **family** is a base-shape class of a wool approach. There are **nine**, and they are an
**escalation** of one another, not a flat set:

`Isolated · I · L · Z · Scythe · Clamp · U · H · Donut`

A family's **identity is its turn count plus the wool's seating**, read **width-independently** — a
thick leg, a box-shaped bar, or a wide bay is a *wide spot*, never a different family. Families are
defined in §5.

### 1.3 The two derivers

Two classifiers share the verb *derive*; they read **different things** and are both current:

| Deriver | Reads | Produces | Code |
|---|---|---|---|
| **shape deriver** | one wool box's terrain | the family (§1.2) — the emitter's mirror | `WoolApproachShape.Classify` |
| **board deriver** | the whole board's terrain + markers | connectivity: islands, voids/holes, contacts, build-zone kinds, wool lanes, the mid form | `PlanDerived` + `tools/deriver/derive-gallery.cs` |

When a doc says "the deriver" without qualification it means the **board deriver**; the shape
deriver is always named as such.

### 1.4 role vs slot

Two different taxonomies, never mixed:

- **role** — a **map-level piece role** in `plan.json`. The **authored** roles are exactly:
  `piece` (anonymous — the default), `wool-room`, `spawn`, `buffer`, `connector`. Everything
  structural — `frontline`, `hub`, `lane`, `mid` — is **derived, never authored**. (`piece`,
  `wool-room`, `spawn` are *generating* roles: they make terrain. `buffer`, `connector` are
  *annotation* roles: informational marks, no terrain, no graph/export effect.)
- **slot** — a **shape-internal role** inside one approach family (`ApproachSlots`, carried on
  `GrownPiece.Slot`). The slots are `entry · run · bar · leg · room`, with `run`/`bar` qualified
  `entry-run` / `room-run` / `entry-bar` / `room-bar` when a family has two. A slot is a **template
  position, not a property of the rectangle** — a scythe's `entry-run` and a donut's `leg` may be
  the very same rectangle in different slots.

### 1.5 interface, and the three levels of contact

An **interface** is always a shared **edge interval** — a *position and a width* — where two pieces
meet, or where a piece meets a build zone. **Never a point, never a node.** A bare point touch does
not connect (see `Corner` below). Contact is typed at three levels:

**Level 1 — `ContactKind`** (raw piece-to-piece, straight off the terrain rectangles):

| Kind | Is |
|---|---|
| `Land` | shared border ≥ the corridor minimum (10 blocks) — the pieces merge into one walkable mass |
| `Narrow` | a shorter positive border — still walkable (a staircase / ledge seam) |
| `Corner` | a bare point touch — **never connects** |
| `Overlap` | area overlap — a same-surface overlap merges |
| `None` | disjoint — a gap a build region must span |

`Land` + `Narrow` are the **land interfaces** (they connect); union-find over them yields the
**islands**. A piece with *no* land interface is **isolated** — reachable only by building.

**Level 2 — connectivity**: `land` (the pieces merge) vs `gap` (a void a build region spans,
carrying a span distance). An elevation transition — `step` / `ramp` / `cliff` — rides on either.

**Level 3 — build-zone kind** (a build region typed by *what islands it links*):

| Kind | Links |
|---|---|
| `front↔front` | ≥2 teams — the crossing / direct team link (may carry stepping stones inside it) |
| `front↔neutral` | one team + a neutral — a team's bridge toward the mid |
| `neutral↔neutral` | only neutrals — a mid-internal link, usually across the axis |
| `intra` | a team's own `spawn↔wool` route — an isolation cut |
| `self` | a notch in a single island, both walls the same landmass |

### 1.6 interface width — the master variable

The **interface width** is the width of the edge interval where two boxes touch. It is the **master
variable of generation**. The reference frame: **`cell = 5 blocks`; `lane = 2 cells = 10 blocks`;
`wN = N cells`**, so `w2 = 1 lane = 10`, `w4 = 2 lanes = 20`, `w6 = 3 lanes = 30`. Width is **not
strictly quantized** — 15 or 25 are valid and taper toward the nearest rung. One width does three
things at a box touch (§4).

### 1.7 hole — an enclosed void

**Reserve "hole" for an enclosed void**: empty, non-buildable terrain the map border cannot reach
without crossing terrain *or* a build region. The board deriver classes every hole (by what its
boundary touches, **never by size**):

| Class | Boundary touches |
|---|---|
| `encased` | one team's terrain, no build — a bubble deep in a team's land |
| `gap` | one team, build all `intra`/`self` — a void in the team's own isolation cut |
| `frontline` | one team's terrain + frontline build — the exposed edge on the crossing |
| `middle` | ≥2 teams, or pure build — the contested crossing / arena |

Each hole is also **declared** (overlaps a `buffer` or a zone-hole — deliberate) or **undeclared**
(the buffer worklist — a suspected accident). A `middle` hole reports its **parallel ways** (the
count of distinct crossings ringing it).

Two other enclosed voids are **not** a hole in this sense, and must not be called one: the **donut's
void** is a *shape-level* enclosed void (§5), and a box's opening is an **interface** (§1.5).

### 1.8 bay — a scythe feature

A **bay** is a concavity that indents from a **single** bounding-box edge (a notch wrapped by
terrain on its other three sides), width-independent. A corner notch touches two edges and an
enclosed void touches none — neither is a bay. A bay **decides exactly one family**: the scythe (a
fold that wraps a bay). The gaps in U / H / Clamp are bay-shaped too, but there the family is fixed
by the branch and bridge tests and the bay is incidental. Bay is a *feature*, not a family.

### 1.9 width — four distinct things, and two modes

Four quantities are all called "width"; keep them apart:

| Name | Is |
|---|---|
| **interface width** | the master variable (§1.6) — the box-touch width, `w2/w4/w6` |
| **cw / W** | the corridor width a shape is built and measured at, in cells, range `[2,6]` |
| **w (reported)** | what the classifier measured back — an **output**, not an input |
| **attachmentWidth** | the `entry` piece's own width, which may exceed the lane it feeds |

And two **modes** of the concept:

- **generation-width** — the grammar: it gates the fill menu and sets connectivity (§4).
- **read-width** — identity: a family is its turn count, read **width-free** (§5).

Width chooses which family is *legal* and how it *joins*; it does not change what a shape *is*.

### 1.10 budget — two currencies

Budget is **two currencies that must both balance**:

| Currency | Is | Set by | Spent by |
|---|---|---|---|
| **land** | walkable terrain area (capacity) | player count (`G8`) | every emitted piece |
| **footprint** | total box area (terrain + build + gap) | the box partition | the box's size, fixed once |

The key: **a build zone costs footprint but not land**. Detailed in §8.

### 1.11 The small words

- **box** — a bounding envelope (§4), *not* a fill target.
- **lane** — a simple corridor (bend count `I / L / Z`), the board deriver's `WoolLaneShape` read.
- **approach** — the whole wool-box shape (one of the nine families). *Lane ≠ approach.*
- **menu** — the set of families an interface width makes legal (the width→fill production rule, §4).
- **mid** — the neutral band between the frontlines; its **form is `f(frontline)`** (§9).
- **frontline** — a **join**, not a placement, and a **derived edge attribute**, not a piece (§4, §6).

---

## 2. The pipeline

Generation runs from the spawn outward and embeds late:

```
budget → boxes → emit / fill → compose / join → embed → evaluate → fragment → realize
```

- **budget** — player count fixes the land and footprint targets (§8).
- **boxes** — the budget draws a handful of typed boxes (§4).
- **emit / fill** — each box is filled with a base shape (§5).
- **compose / join** — the boxes are joined; under symmetry only one half is grown and **fanned**,
  and the **frontline is where the fanned images meet** (§4).
- **embed** — the relative frame is placed into absolute coordinates.
- **evaluate** — the plan is scored, and "no shape fits" feeds a box change (§7).
- **fragment** — land is converted to build (isolation cuts, stepping stones) — footprint-conserving
  (§8).
- **realize** — the plan is compiled and exported (§2.1).

### 2.1 realize — the compile chain

The plan is the **upstream artifact**; it compiles one-way into two downstream artifacts, each with
exactly one consumer:

```
plan.json ──compile──► layout.json (SketchLayout) ──rasterize──► world
        └──compile──► intent.json  (MapIntent)     ──generate───► map.xml
```

| Artifact | Holds | Read by |
|---|---|---|
| **plan** | roles, interfaces, isolation, elevation transitions — the meaning | the composer / evaluator |
| **sketch** (`layout.json`) | realized geometry: polygons, béziers, per-anchor heights, layers | the rasterizer |
| **intent** (`intent.json`) | concrete objectives: block coords, yaws, wool colours, monument wiring | the XML generator |

Sync is **one-way** while the staged loop runs (edit plan → recompile → re-roll roughening and
elevation — §10, §11). Once the author takes the sketch into the editor for hand work, the plan
**freezes as provenance**
and sketch + intent become the working artifacts. Recovering plan meaning from edited geometry is
out of scope.

---

## 3. The plan artifact (`plan.json`)

`plan.json` is the **author-intent layer**: only what a machine cannot recover. Everything
structural is **derived** from it and never written back. Coordinates are **proxy cells** on the
5-block grid (a mini-layout whose real scale is applied at realize), relative to the symmetry centre.

**Authored** (irreducible):

- **geometry** — the piece rectangles (`pieces[].rect`, in cells).
- **roles** — the authored set of §1.4: `piece`, `wool-room`, `spawn`, `buffer`, `connector`.
- **objective + spawn markers** — `placements.wools` / `placements.spawns`.
- **deliberate voids** — `zones[].holes` and `buffer` pieces (the author asserting "I meant this
  void"; a **hole is an enclosed `buffer`**).
- **height** — `pieces[].surface` (+ `globals.surface`), full block resolution, per piece.
- **override channels** — `cliffs`, `walls` — refinements over what the deriver would otherwise infer.

**Derived** (computed, never authored): islands, frontline, hub, lane, mid, contacts, void topology,
build-zone kinds, and the wool-approach family. These belong to the derivers (§6), not the file.

**Plan invariants** (checkable with zero geometry): every wool reachable from every capturing team's
spawn across `land` + `gap` interfaces; no wool path through a `spawn` piece; ≥1 `gap` on every
inter-team path; interface widths ≥ the corridor minimum; spawn depth ≥ some distance from the
nearest frontline interface.

The field-level schema and the editor are in `plan-editor.md`.

---

## 4. Boxes — the scaffold

Before any piece is filled, the budget draws a **coarse partition of typed boxes**. A box is a
**bounding envelope, not a fill target**: its contents must touch its edges and stay connected but
need not fill it solid. That is what lets one family take many footprints inside a fixed envelope.

**The typed boxes:** `spawn`, `hub`, `wools`, `frontline`, `mid`.

- **spawn** — small, fixed-width (~10×10 direct, 10×20 with a run-up, 20×20 for an L); never large.
- **hub** — the remainder rectangle: narrow-ish, need not be square, may carry holes. It is the
  **constraint source** — its edges *are* the interfaces every neighbour must match, so filling it
  decides the menu for the wools, spawn, and frontline. It emits **first**.
- **wools** — one box per wool, filled with an approach family (§5).
- **frontline** — a **join, not a placement** (below).
- **mid** — the neutral band between the two frontlines (§9).

**Each box side is typed and carries an interface width**, and that width does three things at once:

1. **sets connectivity** — a `w2` touch is a single funnel (a chokepoint); `w4`/`w6` admit parallel
   or split flow.
2. **classifies the joint** — a touch ≤ ~1 lane *continues* a lane (a **bridge**); a touch ≥ ~3
   lanes *is an area* (a **hub**); `w4` is the unstable middle that must resolve (twist to an L/I, or
   split into lane + build-lane).
3. **gates the fill menu** — the width→fill production rule:

| touch | lanes | reads as | legal fills |
|---|---|---|---|
| **w2 (10)** | 1 | chokepoint | one I / L / Z lane; or a pure drain |
| **w4 (20)** | 2 | too wide to stay straight | 10 terrain + 10 build-lane; or a 20 stub that twists to L/I |
| **w6 (30)** | 3 | multi-access | two 10-strands with a hole; terrain-build-terrain; or a funnel splitting into a hole with two approaches |

**Placement is endpoint-to-side matching.** A family's endpoints are typed — `entry` docks the hub,
the dead-end (`room`) points away — so "which way an L faces" is not a tuned rule; it is the only
legal placement.

**Generation runs from the spawn outward, in a relative frame, then embeds.** Order:
**spawn → hub → wool boxes → frontline**, in local coordinates with no fixed origin. Under symmetry
**only one half is grown and fanned**, so the **frontline is where the fanned images meet** — its
position, and therefore the map's overall length, is an *output* of how much each half generated, not
an input. Only once the join resolves is the relative frame embedded into absolute coordinates.

**Lane ≠ approach at a wool box.** A wool box has two widths: the `entry`/interface (where it docks —
`w2/w4/w6`) and the *lane* to the wool (simple, `w2`). A wide entry tapers or splits into the narrow
lane — which is why the emitter separates `attachmentWidth` from `cw`.

**"No shape fits" is a signal, not a failure.** An over-constrained box is answered by **changing the
box** (resize, relax an interface, split it) — the Tetris failure feeds back up a level.

---

## 5. The shape families and the piece vocabulary

### 5.1 The nine families

Shape identity is `ApproachShape` (`WoolApproachShape`). The families are an **escalation**: an L
whose lane doubles back is a scythe; a scythe whose bay closes is a donut; a clamp whose wool docks
flush on one bar is a U; a U that lifts its wool onto a room-run stub is an H.

The base vocabulary is a character grid — **`t` terrain (walkable), `v` void (a build zone may later
span it), `w` wool**, rows top to bottom. These are scale-independent *shapes*; build zones subdivide
them afterward, so the catalog is the terrain/void topology *before* cutting.

| Family | Example(s) | Reads as |
|---|---|---|
| **Isolated** | `vv / wv / vv` | wool ringed by void — no terrain approach; reachable only by building |
| **I** | `tttw / vvvv` | a terrain lane caps the wool inline (a solid body with no bends also reads I) |
| **L** | `tw / vt / tt` | one bend — terrain reaches the wool from two adjacent sides |
| **Z** | two opposing bends | an S with no bay |
| **Scythe** | `tttv / tvtw` | a fold that wraps an **open bay** beside the wool |
| **Clamp** | `tt / vw / tt` | the wool **bridges** two otherwise-separate bars — remove it and the terrain splits (a cut cell) |
| **U** | `ttv / vtw / ttv` | two legs meet a crossbar and the wool docks **flush** on it (the bar overhangs the wool) |
| **H** | `ttvv / vtvv / tttw` | two legs meet a crossbar and the wool caps a **room-run stub** its own width, lifting it off the bar |
| **Donut** | `ttttv / vtvtv / vtttw` | terrain **encloses** a void — a full loop, multi-access |

The emitter builds the eight non-isolated families (`ApproachFamily { I, L, Z, Scythe, Clamp, U, H,
Donut }`); `Isolated` is a build-only case with no terrain to emit.

### 5.2 The width-independent classifier

`WoolApproachShape.Classify` is one decision tree over the terrain, **strongest signal first**, and
**nothing keys off an absolute width**:

1. **No terrain touches the wool?** → **Isolated**.
2. **Terrain encloses a void?** → **Donut** (a loop may carry a thick corner and still be a donut).
3. **Wool is a cut cell** — removing it disconnects the terrain (it is the closing wall bridging two
   otherwise-separate bars) → **Clamp**.
4. else the open path by **bend count** — reflex corners of the terrain **outline** (the approach
   minus the room, so the count is width-invariant): **0 → I**, **1 → L**; **≥2** forks:
   - **branch?** (two terrain runs meet a shared bounding-box edge the wool is *not* on — the wool's
     own edge is excluded, so a fold's two path-ends never read as a fork):
     - **wool flush on the crossbar** (the bar overhangs the wool) → **U**.
     - **wool on its own room-run stub** → **H**. (U and H differ by exactly one piece — the stub.)
   - **no branch** — a fold that wraps a **bay** → **Scythe**; two opposing bends with no bay → **Z**.

Because none of these consult the reference width, an H with a box leg and a thin leg still reads H, a
uniformly widened Z stays Z, and a wide-bay scythe stays a scythe.

### 5.3 The piece vocabulary — families as slot templates

The emitter lays each family as the **same fixed set of rectangles, only resized**, so a family is an
ordered **template of slot-typed pieces** (§1.4). Naming the slots lets composition rules be stated
over slots, not raw geometry:

| Family | Template |
|---|---|
| **I** | `entry · room` |
| **L** | `entry · run · room` |
| **Z** | `entry · bar · room-run · room` |
| **Scythe** | `entry · entry-run · bar · room-run · room` |
| **Clamp** | `entry · entry · room` |
| **U** | `bar · entry · entry · room` |
| **H** | `bar · entry · entry · room-run · room` |
| **Donut** | `entry-bar · leg · leg · entry · room-bar · room` |

(U and H differ by exactly the `room-run` stub — the emit side of the classifier's overhang test.)

Two invariants: a family emits a **stable piece count** (never merge collinear pieces — a stable set
is what makes "the entry is piece N" a usable rule); and a **slot is a template position, not a
property of the rectangle**. The table is realized as data in `ApproachSlots.Template(family)`, and
each emitted piece carries its slot on `GrownPiece.Slot`.

Why this is load-bearing: the composition rules become properties of a **slot**, defined once per
family. Entry widening and entry shift live on the `entry` slot; wool docking (extend vs side-dock)
lives on the `room` slot; which pieces may split into build zones is stated per slot (a `run`/`bar`
can be cut into lane + build-lane; an `entry`/`room` typically stays whole).

### 5.4 The emit ↔ derive mirror

`emit` (build a family) and `derive` (classify a family) are a forward/inverse pair, and asserting
they agree is the **correctness test**. Deriving is also how *external* shapes are read (a traced
corpus map, an uploaded plan). Harnesses (`tools/deriver/`, run with `dotnet run tools/deriver/<file>.cs`):

- `shapes-gen.cs` — builds the §5.1 catalog fixtures and classifies each against its family.
- `emit-verify.cs` — the mirror loop: emit every family × size × width, derive back, assert equal + no
  overlap, and assert the emitted slot sequence equals `ApproachSlots.Template`.
- `stress-shapes.cs` — every family's pieces pushed to extremes at a fixed width; each must read its
  own family (the width-independence proof).

---

## 6. The two derivers

### 6.1 The shape deriver

`WoolApproachShape.Classify(plan, woolPieceId, laneWidth) → (ApproachShape, Width)` — reads **one wool
box** and returns its family (§5). The reported `Width` is an output, kept for the width report only.
This is the emitter's mirror.

### 6.2 The board deriver

`PlanDerived` + `tools/deriver/derive-gallery.cs` read the **whole board** and compute connectivity.
Its outputs:

- **islands** — components of union-find over the land interfaces (`ContactKind.Land` + `Narrow`),
  each tagged by anchor role: **team** (holds a spawn), **objective** (holds a wool, no spawn — the
  isolated-wool island), **neutral** (anchorless, in a build region), **decorative** (excluded).
- **contacts** — every `ContactKind` between pieces (§1.5, level 1).
- **build regions + their kinds** — `front↔front` / `front↔neutral` / `neutral↔neutral` / `intra` /
  `self` (§1.5, level 3), plus zone width and interface width per zone.
- **intra-team bridge** and **self-bridge notch** — a team's own internal `spawn↔wool` cut (direct or
  chained through a *captive* stepping stone), and a pocket carved into one landmass.
- **void topology + hole classes** — enclosed voids classed `encased`/`gap`/`frontline`/`middle`,
  declared vs undeclared, with parallel-ways for `middle` holes (§1.7).
- **wool lanes** — the corridor a wool room owns, and its topology via `WoolLaneShape.Classify`, which
  returns a **string** bend read: `I` / `L` / `Z` / `complex` / `plaza` / `none`. (This is the
  board-level corridor read — distinct from the wool-box shape identity of §5.)
- **the CT mid-form** — falls straight out of the build-zone kinds (§9).

The detailed measurables catalogue — every derived quantity, its exact definition, and its
validation against the seed corpus — is in `layout-evaluator.md §5`.

---

## 7. The evaluator

The emitter can make anything; the maps' character comes from **what evaluation refuses to let
through**. The rules do not *produce* good maps — they *punish* bad ones, and the residue is the
style.

**The model is three layers:**

| Layer | What it is | Where |
|---|---|---|
| **author intent** | the irreducible input | `plan.json` (§3) |
| **derive structure** | the roles + topology, computed | the derivers (§6), in-memory |
| **judge by property** | metrics vs rules + envelopes | the evaluator |

Everything the file cannot recover is authored; everything structural is derived; everything the
rules check is judged. The form:

```
score = Σ hard-penalty(violated well-formedness) + Σ w · envelope-distance(metric)
```

Hard rules are large penalties (a valid layout has none); "feel" is each metric's distance outside
the authored envelope (`seed-stats.md`). The evaluator returns the score **and the list of violated
terms** (each citing a `layout-rules.md` id) so a failure is legible and actionable. It is
**additive and never has to be complete** — new terms are added as failures are found, and a new term
never tanks an acceptance rate.

**The evaluation set is the real deliverable.** The evaluator is correct when it ranks a labeled set
the way the author does: **positives** (authored good layouts, auto-labeled by the deriver),
**negatives** (flagged bad layouts — the most valuable are **minimal pairs** differing in exactly one
property), and **coverage** (examples per sub-problem × per symmetry mode). The property-term
catalogue and the labeled set live in `layout-evaluator.md §6–§7`.

---

## 8. Budget and width

### 8.1 The two currencies

A per-box budget is `(footprint, land-target)`. **emit** fills the footprint as all-land;
**fragment** converts land→build until the box hits its land target. Because a build zone costs
footprint but not land:

- **Fragmentation conserves footprint and spends land** — a terrain piece → a build-zone piece keeps
  the size and drops the land. The box size is fixed once, at partition; only the land↔build
  composition changes. This is the **"never remove, just replace"** invariant.
- **The mid is the same model, inverted** — footprint-rich, land-poor. Its purpose *is* the build
  crossing, so its land-target is low and only stepping-stone islands remain. No special mid budget —
  just a low land-target.

The two currencies balance at **two levels at once**: global (total land = players; total footprint =
map size) and per-box, under symmetry. Every fragmentation cut spends land *globally* while buying
difficulty (isolation, risk) in the same move — so the land budget and the gameplay knob move
together.

### 8.2 Width, disentangled

The four "widths" and the two modes are in §1.9. The distinction to hold when a rule reads
contradictory: **generation-width** is the master variable (it gates the menu and sets connectivity);
**read-width** is orthogonal to family (the family is the turn count, read width-free). Width chooses
which family is legal and how it joins; it never changes what a given shape *is*.

---

## 9. The mid

The mid is the gap between the frontlines, and its character is **build bands / islands** — additive
structure. You **structure** the mid; you do not carve it from a solid. Its form is not a free choice:

```
mid form = f(frontline)
```

Two parallel frontline edges → a parallel build band (+ islands); a single wide frontline → clean or
hash. Since the frontline is itself the symmetry join (§4), the mid form is an **output**. The form
derives straight from the build-zone kinds:

- any `neutral↔neutral` zone ⇒ **hash** (the mid is fractured into interlinked islands);
- else ≥2 `front↔front` crossings ⇒ **parallel**;
- exactly one ⇒ **channelled**.

The mid's target vocabulary comes from parallel-band detection; that is a *test-article* source, not a
generation method. The order for the middle: the halves grow → the join fixes the frontline → the
frontline dictates the mid form → the form + the mid's low land-target produce the bands/islands → the
flow priors score it.

---

## 10. The roughen pass

The roughen pass turns the plan's clean rectilinear geometry into an organic read. It runs **last**,
inside realize (§2.1), on the realized polygons of the **authored unit only** — symmetry re-fans the
images, plan meaning is frozen, and objective placements are pinned. Its output is ordinary
`SketchShape`s, so every intermediate stays hand-editable in the sketch editor.

Operators:

- **anchor jitter** — displace existing vertices by bounded noise.
- **edge subdivision + displacement** — insert mid-edge anchors and push along the edge normal (1–2
  fractal levels): organic outlines, zero topology change.
- **pull-to-polygon** — one strong anchor displacement that breaks a rectangle into a believable quad
  (the "twist").
- **width profile** — vary a lane piece's width along its length (thin necks, wide rooms).
- **45° chamfer** — soften right-angle corners into diagonal pairs.
- **piece shear / rotate** — a few degrees around the centroid.

Invariants (per operator): minimum corridor width preserved (offset test); no self-intersection;
placements stay interior with margin; `gap` interface spans stay within the bridgeable range;
interfaces stay covered (distorted neighbours still overlap their shared interval).

---

## 11. Elevation

Elevation attaches to **roles and interfaces, not to geometry**. The vocabulary: per-shape `floor` /
`base_height` (plateaus); splitting a shape along a seam and offsetting the piece (**cut + raise** —
at the plan level, refining one piece into two joined by a `land` interface with a height delta);
`anchor_heights` gradients (ramps); stacked layers.

The role/interface rules: a **raised spawn** (overview); a **stepped approach** climbing toward a wool
room (a harder push); a **low frontline** (bridges launch low, defenders hold the high ground); a
**`cliff` interface** where one-way flow is wanted. Constraint: walkable steps along any `land` path
unless the plan says `cliff`. The exact height numbers are the `EL` rules in `layout-rules.md`.

---

## 12. Code map

Where each concept lives (paths under `src/PgmStudio.Pgm/` unless noted):

**The shape mirror — emit ↔ derive**

| Piece | Path | What |
|---|---|---|
| `WoolBoxEmitter` | `Compose/WoolBoxEmitter.cs` | **emit**: `Emit(family, box, cw, …)` → terrain pieces. Holds `ApproachFamily`, `ApproachSlots`, `WoolBox`, `RoomPlacement`, `EmittedApproach`, `AsPlan`. |
| `WoolApproachShape` | `Plan/WoolApproachShape.cs` | **shape deriver**: `Classify` → `ApproachShape` (9 families), width-independent. |
| `WoolLaneShape` | `Plan/WoolLaneShape.cs` | the board-level corridor bend read: `Classify` → string `I/L/Z/complex/plaza/none`. |

**The board deriver — islands / voids / interfaces**

| Piece | Path | What |
|---|---|---|
| `PlanDerived` | `Plan/PlanDerived.cs` | connectivity primitives: `ContactKind`, `Contact`, `BuildRegion` (with `Holes`), `GapLink`, `InterfaceSegment`, `FrontlineEdge`, islands. |
| `derive-gallery.cs` | `tools/deriver/derive-gallery.cs` | the board reader: hole classes, build-zone kinds, intra/self, wool lanes, the CT mid-form → `out/derive-gallery.html`. |
| `FannedGraph` | `Plan/FannedGraph.cs` | fanned-board reachability (looser than the straight-span gap links). |

**The composer**

| Piece | Path | What |
|---|---|---|
| `Composer` | `Compose/Composer.cs` | `Compose(ComposeRequest)` — the entry point. |
| `TeamUnitGrower` | `Compose/TeamUnitGrower.cs` | budget→counts, spawn + hub, grow, frontline, stones; holds `GrownPiece` (with `Slot`), `GrownUnit`. |
| `SpawnWoolRooms` | `Compose/SpawnWoolRooms.cs` | the spawn / wool-room terminal carve. |
| `Envelope` | `Compose/Envelope.cs` | the budget anchors (`bp`; the land-per-player target). |
| `MidCarver` | `Compose/MidCarver.cs` | the mid: bands, stone grids, the recess. |
| `ClosureAnalysis` | `Compose/ClosureAnalysis.cs` | closure hole raster (`HoleSizes`, `AnyHoleRingedBy`). |
| `IsolationCut` | `Compose/IsolationCut.cs` | the isolation-cut fragmentation move. |
| `ComposeGeometry` | `Compose/ComposeGeometry.cs` | fanning + the fanned-separation invariant. |
| `PlanModel` · `PlanRoles` | `Plan/PlanModel.cs` | the plan format + the authored role set. |

**Harnesses**

| Piece | Path | What |
|---|---|---|
| `shapes-gen.cs` | `tools/deriver/shapes-gen.cs` | the §5.1 catalog fixtures. |
| `emit-verify.cs` | `tools/deriver/emit-verify.cs` | the emit↔derive mirror loop + slot-template check. |
| `stress-shapes.cs` | `tools/deriver/stress-shapes.cs` | width / edge-case stress fixtures. |
| `lane-audit.cs` | `tools/deriver/lane-audit.cs` | the `WoolLaneShape` derive-then-override training harness. |

---

## 13. Boundaries

This document does not restate the rules or the numbers. The **frozen rule law** — every CT / SP /
WL / LN / HB / FR / MD / BZ / EL id, with its exact widths, depths, hop counts, and heights — is
`layout-rules.md`, and it grows only through its correction protocol. The **measured envelopes** the
soft evaluator terms score against are `seed-stats.md`. The **plan schema and editor** are
`plan-editor.md`. The **detailed deriver-measurable and evaluator-metric catalogue** is
`layout-evaluator.md`.
