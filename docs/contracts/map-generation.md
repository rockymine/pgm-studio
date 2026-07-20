# Map generation — the canonical model, terminology, and pipeline

This is the **single source of truth** for how pgm-studio generates map layouts: the vocabulary,
the pipeline and its order, and how every part fits the next. Every word defined here has **exactly
one meaning**; where a term appears elsewhere it carries this meaning. When another doc and this one
disagree, this one governs.

**What this document owns:** the glossary, the pipeline, the box model, the shape model (bodies,
designations, and the approach families), the two derivers, the evaluator model, and the budget/width
model. **What it defers:**

| Companion | Owns |
|---|---|
| `layout-rules.md` | The frozen rule law and every number (widths, depths, hop counts, heights, the CT / SP / WL / LN / HB / FR / MD / BZ / EL ids). |
| `seed-stats.md` | The measured envelopes the soft evaluator terms score against. |
| `plan-editor.md` | The field-level `*.plan.json` schema and the editor UI. |
| `layout-evaluator.md` | The detailed deriver-measurables and evaluator-metric catalogue. |
| `map-generation-vocabulary.md` | The **living type catalog** — every type as a map concept, by pipeline order. §1 defines the terms, §12 is the code map, this names the types that embody them. Extend it in the same commit a task adds/renames/retires a type. |

---

## 1. Glossary — the locked terms

### 1.1 The five pipeline verbs

Generation is five verbs, not one. **Never say "generate" for the whole thing** — it is ambiguous
(it has been used both for the whole pipeline and for the narrow `intent → map.xml` step). Use the
specific verb:

| Verb | Means | Where it lives |
|---|---|---|
| **emit** | Fill one box with one base shape (forward). | `WoolBoxEmitter` |
| **derive** | Read structure back out of geometry (inverse). Two derivers — see §1.3. | `ShapeClassifier`, `ContactGraph` + `BoardDeriver` |
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
thick leg, a box-shaped bar, or a wide bay is a *wide spot*, never a different family. A family is the
**approach designation** (§1.12) over a terminal-free **body** — one body serves several families (a
Staple body reads **U** with the wool flush on its bar, **H** with the wool lifted onto a stub).
Families are defined in §5.

### 1.3 The two derivers

Two classifiers share the verb *derive*; they read **different things** and are both current:

| Deriver | Reads | Produces | Code |
|---|---|---|---|
| **shape deriver** | one wool box's terrain | the family (§1.2) — the emitter's mirror | `ShapeClassifier.Classify` |
| **board deriver** | the whole board's terrain + markers | connectivity: islands, voids/holes, contacts, build-zone kinds, wool lanes, the mid form | `ContactGraph` (rect layer) + `BoardDeriver` → `BoardStructure` (raster layer) |

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
void** is a *shape-level* enclosed void (§5) — the edge taxonomy's **hole** class (§1.13), a *shape*
negative space distinct from these board-deriver connectivity classes — and a box's opening is an
**interface** (§1.5).

### 1.8 fold and bay — the scythe features

A **fold** is terrain that doubles back on itself — some grid row or column crosses the terrain in
**two runs** (the terrain is not orthogonally convex), width-independent. The **bay** is the open
concavity the fold wraps — formally a **3-wall negative space** (§1.13); the scythe's is the
fold-wrapped instance. The **fold decides exactly one family**: the scythe. The fold, not a
bounding-box read of the bay, is the test — sliding an endpoint off a box corner opens the bay
toward a second edge without unfolding the shape, so the fold read stays stable under the emitter's
entry/wool shifts and under docked neighbour terrain. The gaps in U / H / Clamp are bay-shaped too,
but there the family is fixed by the branch and bridge tests. Fold and bay are *features*, not
families.

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
- **lane** — a simple corridor (bend count `I / L / Z`), the board deriver's `ShapeClassifier.ClassifyOpen` read.
- **approach** — the whole wool-box shape (one of the nine families). *Lane ≠ approach.*
- **menu** — the set of families an interface width makes legal (the width→fill production rule, §4).
- **mid** — the neutral band between the frontlines; its **form is `f(frontline)`** (§9).
- **frontline** — a **join**, not a placement, and a **derived edge attribute**, not a piece (§4, §6).

### 1.12 body and designation

Every base shape is two layers. A **body** is a pure rectilinear compound — one or more rectangles
joined **along shared edge intervals** (§1.5; never a bare corner, `Cells.HasDiagonalPinch`),
identified by **topology alone** (voids · arms · bends), width-independent. A **designation** is what
a box kind stamps onto a body to finish it into a placed box:

| Designation | Stamps | Box kinds |
|---|---|---|
| **approach** | an `entry` (the docked rect/edge) + a `terminal` (the room) | wool, spawn |
| **hub** | **interface widths** per free run, no terminal — the *constraint source* (emits first, publishing what each run supports; each neighbour's own grant sets its menu) | hub |
| **frontline** | one edge marked the **`face`** (where the fanned images meet), no terminal — drives `mid = f(frontline)` | frontline |

The body layer is shared across every box kind; the designation is per-kind. **A family (§1.2) is the
approach designation over a body** — one body serves several families (a Staple body reads U with the
wool flush on its bar, H with the wool on an added stub). This is why the shape enum's `Isolated` and
`Clamp` are not bodies but *terminal designations* (§5): Isolated is a body with no terrain reaching
the terminal; the clamp is one compact terminal docked on two **distinct** faces. Letters (I/L/Z/U/…)
name a **placement**, not a topology — a join is free to slide and widen along its edge (the G50–G52
knobs), so the same body reads as different letters as its pieces move; **identity stays topological**,
the letter is notation.

### 1.13 the edge taxonomy — negative spaces and the offerable surface

A body's **negative spaces** — the connected empty regions around and inside it — escalate by **wall
count**, the number of axis directions the body walls the void from:

| Walls | Class | Picture |
|---|---|---|
| 2 | **notch** | the corner an L wraps |
| 3 | **bay** | the staple's recess, the scythe's fold-bay (§1.8) — open one way |
| 4 (enclosed) | **hole** | the ring / donut's void |
| ≤1 | *(open)* | plain outside — not a feature of the shape |

This is a **shape-relative fact read off finished geometry** (`BodyEdges`), total over any rectangle
set. A space carries: its **parts** — a slab decomposition into rectangles, each re-classed by its
*own* walls (so a rule can reach an inset leg the flat class forbade wholesale); its **mouths** — one
per open direction (bay 1, notch 2, hole 0), each an **interval** (§1.5) tapering to a `wN` **width
class** (what may dock *through* the opening); its **wall slots** (the slots of the walling pieces);
and its own compound **form** (the void read as a body). A boundary run is classified on **three
independent axes** — what it **faces**, whether it is **terminal** (the room seals its own wall), and
whether it is **guarded** (inside the room's clearance margin, the corridor minimum). The **offerable
surface** — the free outward surface a neighbour may attach onto — is exactly **open ∧ ¬terminal ∧
¬guarded**. ("hole" here is the *shape-level* negative-space class — the donut's void of §1.7 — **not**
the board-deriver's connectivity hole classes.)

### 1.14 the rule kinds

Every generator rule is one **kind**; naming the kind gives each new rule an address. The one-line
test: does it change what can be **picked** (menu), whether a pick **fits** (fit gate), whether a join
is **legal** (gate over demand / offer / veto), how a legal join **varies** (knob), what a compose
**aims at** (target), or how good the result **reads** (band / term)?

| Kind | Is | Exemplar |
|---|---|---|
| **fact** | an observation off geometry, no policy | `BoxEdgeInterface.Intervals`, the edge taxonomy (§1.13), `FrontlineRuns` |
| **menu** | a generative allowlist — what may be *chosen* (empty = a directed signal) | `FillProfiles.Families`, `FillMenu.Rows` |
| **fit gate** | does the choice fit the box | `ShapeEmitter.MinBox`, `FillProfiles.Fits` |
| **demand** | a shape's requirement *on its environment* (inbound) | *(no live type — `FamilyDock` was retired with the clamp's redefinition; the dual-entry requirement survives only as `TeamUnitAllocator.Overhangs`, stated by exclusion. See the taxonomy §10.1)* |
| **offer** | constraints a shape imposes *outward* — the edges/intervals it invites neighbours onto, in which groupings | the hub's per-run widths (granted per dock on the joint), the frontline's face (G96) |
| **veto** | a never-attach / never-publish mark | `SlotDockRole.NeverDock`, `PublishPolicy`'s bay/hole veto |
| **gate** | the hard legality check applying demand/offer/veto, with a **directed rejection** | `DockingGate` → `DockRejection`, `PublishPolicy` → `PublishVerdict` |
| **knob** | a free parameter *within* legality — never changes identity | entry shift, attachment width, arm placement |
| **target** | a **per-request, prescriptive** constraint a compose holds and verifies | `ComposeTargets` (G98) |
| **band** | a **descriptive** envelope measured off the seeds — advisory, scores distance | `SoftTerm` + the seed envelopes |
| **hard term** | a well-formedness symptom on the derived board — flat penalty | `WoolRingedHole`, `GapHopBand` |
| **law** | the id-bearing author rule the mechanisms implement — a **living** set, amended by protocol | `layout-rules.md` FR6, CT9, BZ8 |
| **doctrine** | a meta-rule about where rules may live | "labels drive, the mirror verifies" |

Two distinctions carry the weight. **demand vs offer** is the direction of the arrow: an approach
*demands* (its entry must find a host); a hub or frontline *offers* (its edges dictate where and how
wide neighbours land — the constraint source of §4). **target vs band** is prescription vs
description: a band says *authored maps run 1–7 frontline runs*; a target says *this compose wants
exactly 2, connected*. Bands score a finished compose; targets steer it first and verify after. (Offer
and target are locked as vocabulary here; their types — `EdgeOffer`, `ComposeTargets` — land with
G96/G98.)

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

**Compose-internal** (a third category, neither authored nor derivable): the slot labels of §5.3.
They exist on generated pieces during composition, drive the compose-side rules, and drop at
`Assemble` — a plan on disk never has slots, and no deriver recovers them from an authored or traced
plan (§5.4).

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

**The box model is a meta-model of the authoring process, not a property of maps.** It abstracts how
a map author actually works (stake out regions, fill them, cut them up) — but boxes exist **only
during composition**. No finished map carries boxes — not the traced corpus maps, not even the
authored seeds — and they are never recovered from geometry (§5.4). A finished map is a plan with
every pipeline move already applied, many times over; the moves compose one-way.

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

**A family may demand more than one interface — and an interface names its valid edges (the clamp
law).** Most families dock through a single `entry`, so one edge interval per box suffices. The
**clamp** does not: it is an authored preset that deliberately clamps the wool between its two entry
bars — the **allowlisted instance of the WL8 motif**. Its bay is a *deliberate* hole granting the
wool two approaches, and the fight rotates around that hole (the closed bay is **not** a published
vacancy, §4.4). Docking one entry to one interface — all a fill can express today — forces the clamp
to rotate and leaves the other entry dangling in the void. A legal dock satisfies **both entries,
along the short entry edge** (`t` tile, `w` wool, `v` void, `h` host):

```
t w t        t w t v
t v t        t v t h
h h h        h v v h
```

Left: the full short-edge host — both entries land and the bay closes into an **intended, declared
hole** (§1.7). Right: the harder corner-wrap — two hosts take one entry each, the bay stays open.
Docking the wool-side edge (`hhh` *above* `twt/tvt`, aligned or offset) is illegal: the entry stubs
dangle again. Generalized: an interface declaration gains **valid edges** (long vs short; a
wool-touching corner never docks), and a family may require a **wider interface or two interfaces**
to be satisfied. The clamp is gated from production not by WL8 — it *is* WL8's allowlisted shape —
but because the fill machinery cannot yet *place* multi-interface docks. The legality is now
expressible: `DockingGate` (G80) resolves each box edge to its slots and applies one table — a dock
is legal iff it lands on an entry, seals no wool, and meets the family's demand (the clamp 2 short
edges, most 1) — so the clamp's full short-edge host and the scythe's single-host edges are decided
declaratively. What still waits on the partitioner (G63) is the **dual-host** placement (the
corner-wrap: two hosts, one entry each) — a partition-graph concern, not a legality one. The docking
modes are enumerable data; more may follow now that they are expressible.

**The scythe's valid connections — and the height deferral.** Valid edges are **shape-relative, not
box-relative**: an entry shift carries the dock with it. The scythe's standard connection is the
`entry`'s **unoccupied edge parallel to the entry ↔ entry-run seam** (the outer side edge opposite
the internal interface). The second is the **combined edge** formed by the colinear head edges of
`entry` + `entry-run` — one wider host touching both (the two heads stay flush under the entry
shift, so the combined edge shifts intact). A host that touches the wool `room` is a **hard
violation — reject**: that is the flush dock that seals the bay into WL8's motif, or (under a shift)
makes the room the door. A **declared bay is only valid at the elevation stage** (§11), where height
fixes what the flat read cannot: with a host touching both entry and wool, the wool must be raised
significantly so the entry-host dock is the *only* approach, the scythe terrain **stepping up from
entry to room** — the height mechanism is noted for later (G81); until then room-host contact simply
rejects.

**"No shape fits" is a signal, not a failure.** An over-constrained box is answered by **changing the
box** (resize, relax an interface, split it) — the Tetris failure feeds back up a level.

---

## 5. The shape model — bodies, designations, and the piece vocabulary

A base shape is two layers (§1.12): a terminal-free **body** (topology alone), finished by a per-kind
**designation**. The body vocabulary is one escalation — each step adds a rectangle (joined along a
shared edge interval, §1.5) and earns a feature; the set is **open** (custom builds recombine, and the
body deriver reads the result without a new special case):

| Body | Feature added | Letters · families it serves |
|---|---|---|
| **Rectangle** (a spine) | — | I · □ · the solid hub |
| **Spine + K arms** | branch | L·T (1) · U·Π·F (2) · E·Comb (3+) — the L, U, and H/Y bodies |
| **Zig** | staircase | Z · S |
| **Hook** | fold (§1.8) | the scythe |
| **Ring** | void | the donut |
| **Double-hole** (Ring + a slid U) | 2nd void | a holed hub |

The branch row is **one** family — a spine plus K perpendicular arms; the letter is a placement-read
of (arm count, where the arms sit) and drifts as the arms slide, so Ell / Staple / Comb are not three
bodies but one. Two further **holed** recombinations sit mostly on the frontline: **P** (a U closed by
a longer overhanging I — a ring with a tail) and **two U's on one I** (twin loops). Width is
orthogonal: the interface width (`w2/w4/w6`) reads a body as corridor or area, never as a different
body. The body vocabulary is `Compound` (`SpineArms · Ring · DoubleHole · P · TwoUOnI · Rectangle`),
built by `BodyEmitter` and read back by `ClassifyBody` — a body-layer mirror separate from the
approach view. (In the body layer the old H is named **Y**: its stub arm slides, so a fixed `h` glyph
misleads; the *approach family* enum keeps `H`.)

The designations that finish a body are per box kind (§1.12): **approach** (wool/spawn — §5.1), **hub**
and **frontline** (§5.5). The following subsections are the approach designation — the one with the
complete emit↔derive mirror today — then the hub/frontline menus the box work (G88/G89) builds on.

### 5.1 The approach families (the nine)

Approach-shape identity is `ApproachShape` (`WoolApproachShape`) — the **approach designation** over a
body, the nine of them an **escalation**: an L whose lane doubles back is a scythe; a scythe whose bay
closes is a donut; a clamp whose wool docks flush on one bar is a U; a U that lifts its wool onto a
room-run stub is an H. (`Isolated` and `Clamp` are **terminal designations**, not bodies: Isolated is
any body with no terrain reaching the wool; the clamp is one compact room docked on two distinct
faces.)

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
   - **no branch** — terrain that **doubles back** (some row/column crosses it in two runs — the
     fold wrapping a bay, §1.8) → **Scythe**; a staircase of opposing bends → **Z**.

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

A slot is itself **two layers**, split by what stamps it: a **structural slot** (`run · bar · leg` —
the rectangle's role in the body, shared by every box kind) and a **designation mark** (`entry · room`
— the docked rect and the terminal, stamped by the *approach* designation), qualified
`entry-run`/`room-run`/`entry-bar`/`room-bar` when a family carries two. `ApproachSlots` merges them
because the taxonomy began at wool approaches; splitting them is what lets the identical
shift/widen/tail-follow knobs drive a wool mouth, a spawn mouth, a hub interface edge, or a frontline
face — the hub stamps an `interface` mark per edge, the frontline a `face` mark (G95). The emitted
strings are unchanged, so the mirror stays byte-identical.

Why this is load-bearing: the composition rules become properties of a **slot**, defined once per
family. Entry widening and entry shift live on the `entry` slot; wool docking (extend vs side-dock)
lives on the `room` slot; which pieces may split into build zones is stated per slot (a `run`/`bar`
can be cut into lane + build-lane; an `entry`/`room` typically stays whole).

**The labels drive; the deriver only verifies.** Slots exist for generated maps and nowhere else —
they are the mechanism that makes every later pipeline move rule-governed:

- **Labels survive the whole compose pipeline.** Every compose move after emission (mid carve,
  isolation cut, repair, fragment) runs on labeled pieces — the moves all run **before `Assemble`**,
  so the labels are in hand exactly where the rules need them. A shape that is already attached to
  another shape is **never re-read**: the mirror (§5.4) proves the emitter placed the right thing;
  it is not how the composer knows what a piece is.
- **Ownership is part of the label.** A slot names a position *within one box's fill*, so the full
  label is (box id, box kind, slot) — `wool-a/entry`, `hub-a/bar` — letting connection and
  fragmentation rules bind per box kind, not just per slot. (Target state; today the box id lives
  informally in the piece-id prefix.)
- **Products of a move inherit the label.** When fragment splits or converts a piece, its products
  keep the (box, slot) ownership — a build zone knows it replaced `wool-a/entry-run` — which is what
  makes the per-slot cut law above enforceable *at the cut* instead of re-derived afterwards.
- **`Assemble` is the boundary.** Labels drop from the written plan (`plan.json` has no slots —
  they are §3's *compose-internal* category); the evaluator receives them in-memory via
  `EvalContext`. A plan on disk is label-free by design.

### 5.4 The emit ↔ derive mirror

`emit` (build a family) and `derive` (classify a family) are a forward/inverse pair, and asserting
they agree is the **correctness test**. **The mirror's scope is the generator's own artifacts** —
emissions, synthetic fixtures, and composed pre-fragment units, where the wool box bounds what is
read. Classifying **finished maps** (traced corpus maps, hand-authored plans) is **out of scope by
decision**: fragmentation moves family identity onto the play surface (terrain + build links), a
finished map's base plan is not recoverable, and full-map decoding is a trap — the human oracle
hypothesizes the fragmentation/mutation moves instead
(`docs/wool-approach-read-investigation.md`). Harnesses (`tools/deriver/`, run with
`dotnet run tools/deriver/<file>.cs`):

- `shapes-gen.cs` — builds the §5.1 catalog fixtures and classifies each against its family.
- `emit-verify.cs` — the mirror loop: emit every family × size × width, derive back, assert equal + no
  overlap, and assert the emitted slot sequence equals `ApproachSlots.Template`.
- `stress-shapes.cs` — every family's pieces pushed to extremes at a fixed width; each must read its
  own family (the width-independence proof).

The **body layer has its own mirror**: `BodyEmitter` emits a terminal-free `Compound` and
`ClassifyBody` reads it back by topology (void count strongest, then arm count, then bends) — separate
from the approach mirror so the wool/spawn path stays byte-identical. Void count splits **two voids**
on whether an open channel (two-U-on-I) or a solid wall (double-hole) sits between them, **one void**
on P's overhang vs a clean ring, **none** on the solid rectangle vs the arm count (an F and a Π both
read `SpineArms(2)` — placement-independent).

### 5.5 Designations — hub and frontline

The approach (§5.1–§5.4) is one designation; the box model adds two more, each a body plus a per-kind
mark, **no terminal** (§1.12). Both consume a **body** from the vocabulary above and are the forward
twin of a derived read (the mirror doctrine, one level up): the designation drives, the deriver
verifies.

- **Hub** — a body + **interface widths**. It is the **constraint source**: it emits first, publishing one
  offer per free run at the width that run can *support*, and each neighbour fills at the width **its own
  joint** was granted (that grant is the `cw` the neighbour reads). The grant is per dock, not per edge and
  not per run — two neighbours can share one run at two widths (a third wool doubling onto the spawn's side),
  so an edge- or run-keyed width would hand one of them the other's `cw`.
  Form menu (authored): **Rectangle · L · U · Ring · P · Double-hole · G** — compact and (on a laterally
  elongated hub) **wide holed** bodies; deliberately *not* Zig, Hook, the higher combs, or TwoUOnI (a hub
  stays rectangle-ish). The hub grows **wider, not squarer** — the lateral span uses a larger cap than the
  depth, so the long edge gives neighbours room to attach and reaches the width ≥ 9 the P (loop + overhanging
  bar), Double-hole (ring + full-height U, two equal holes), and G (ring + an L, the ring's hole plus an open
  bay the docking frontline seals into a taller hole — asymmetric holes) need. Its edges' free surface (§1.13) is what the spawn, wool,
  and frontline boxes attach onto — generalizing today's "four edges of the solid rect" to any body's
  offerable surface. (G88; wide forms + elongation G105.)
- **Frontline** — a body + one edge marked the **`face`** (where the fanned images meet), docking the
  hub on the opposite edge and driving `mid = f(frontline)` (§9). **Rotation is fixed by the
  designation**: the body docks the hub with its **spine**, and its **arm-tips are the face** toward
  the axis. Form menu: a plain **Bar** (the wide face, FR6), the **branch family** (spine + K arms —
  single = K1 / twin = K2 / more), and the **holed** forms (P, two-U-on-I — a closed recess). How the
  mid attaches to the face is a deferred rule-set. (G89.)

Two things once called shapes are **not bodies** but shared docks in the designation layer (§5.1):
the **clamp** — one compact terminal on two distinct faces (opposite → centered I+I; adjacent →
corner L+I, the bend forced because two straight bars would corner-touch) — and the **twin frontline**
— two Bars docked to one host individually, the gap between them the face/CT8 recess. Both are
multiple docks the partition graph places (`FamilyDock`, the corner-wrap), not new bodies.

---

## 6. The two derivers

### 6.1 The shape deriver

`WoolApproachShape.Classify(plan, woolPieceId, laneWidth) → (ApproachShape, Width)` — reads **one wool
box** and returns its family (§5). The reported `Width` is an output, kept for the width report only.
This is the emitter's mirror.

### 6.2 The board deriver

`ContactGraph` (the rect layer) + `BoardDeriver.Derive → BoardStructure` (the raster layer, in
`Pgm/Derive/`) read the **whole board** and compute connectivity; `tools/deriver/derive-gallery.cs`
renders `BoardStructure` to `out/derive-gallery.html`. Its outputs:

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
- **wool lanes** — the corridor a wool room owns, and its topology via `ShapeClassifier.ClassifyOpen`, whose
  `LaneRead` maps to the bend read `I` / `L` / `Z` / `complex` / `plaza` / `none` (via `LaneName`). (This is
  the board-level corridor read — distinct from the wool-box shape identity of §5.)
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

**The seeds sit at final-pipeline fidelity.** The authored seeds are what the *whole* pipeline
should output — never what an early stage can produce on its own. A stage is therefore judged only
against the rules that stage owns (fill/slot invariants at emit, envelope terms on the assembled
board, elevation feel at realize) — comparing an intermediate artifact to a seed wholesale is a
category error, the same one as classifying a finished map (§5.4) in the other direction.

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
| `ShapeClassifier` | `Shapes/ShapeClassifier.cs` | **shape deriver**: `Classify` → `ShapeFamily` (9 families), width-independent; `ClassifyOpen` → `LaneRead` is the board-level corridor bend read (`LaneName` → string `I/L/Z/complex/plaza/none`; the retired `WoolLaneShape` was a thin adapter over it). |
| `SlotAssignment` | `Shapes/SlotAssignment.cs` | **slot deriver**: `AssignSlots(family, pieces, roomId)` → piece→slot, re-derived from topology — the emitter's slot mirror (§5.3/§5.4). |

**The board deriver — islands / voids / interfaces**

| Piece | Path | What |
|---|---|---|
| `ContactGraph` | `Derive/ContactGraph.cs` | connectivity primitives (rect layer): `ContactKind`, `Contact`, `BuildRegion` (with `Holes`), `GapLink`, `InterfaceSegment`, `FrontlineEdge`, islands. |
| `BoardDeriver` | `Derive/BoardDeriver.cs` → `BoardStructure` | the board reader (raster layer): hole classes, build-zone kinds, intra/self, wool lanes, the CT mid-form. `derive-gallery.cs` renders it → `out/derive-gallery.html`. |
| `FannedGraph` | `Plan/FannedGraph.cs` | fanned-board reachability (looser than the straight-span gap links; its `LandAdjacent` differs from `ContactGraph` on different-surface overlaps — reconcile pending). |

**The composer**

| Piece | Path | What |
|---|---|---|
| `Composer` | `Compose/Composer.cs` | `Compose(ComposeRequest)` — the entry point. |
| `TeamUnitGrower` | `Compose/TeamUnitGrower.cs` | budget→counts, spawn + hub, grow, frontline, stones; holds `GrownPiece` (with `Slot`), `GrownUnit`. |
| `WoolBoxEmitter` | `Compose/WoolBoxEmitter.cs` | the wool binding over `ShapeEmitter` — fills a wool box, terminal → wool room + marker. |
| `SpawnBoxEmitter` | `Compose/SpawnBoxEmitter.cs` | the spawn binding (second box kind): profile {I, L} + `Fill`, terminal → `Spawn` room + marker. |
| `FillProfiles` | `Compose/Boxes/FillProfiles.cs` | the per-`BoxKind` profile as data: legal families + the footprint fit gate. |
| `BoxFiller` | `Compose/Boxes/BoxFiller.cs` | the one profile-gated fill entry point over a positioned `Box` + land-vs-target accounting (the spine G63 drives). |
| `BoxInterfaces` | `Compose/Boxes/BoxInterfaces.cs` | the valid-edges data model: `Of` reads a box's edges off the shape as `BoxEdgeInterface` **facts** (span + the template slots on each edge) — it observes; the docking *rules* over the facts are the `DockingGate`. |
| `DockingGate` | `Compose/Boxes/DockingGate.cs` | the compose-side docking gate: `SlotDockRole` (room→never-dock, entry→docking, rest→internal) + `FamilyDock` (per-family demand/span) + the verdict (`Check`/`DockingEdges`/`MeetsDemand`) over the `BoxEdgeInterface` slots. A dock is legal iff it lands on an entry, seals no wool, meets the span demand — no per-family imperative code, shape-relative. Not an `ILayoutTerm`. |
| `BoxPartition` | `Compose/Boxes/BoxPartition.cs` | the partition constraint graph: typed `Box`es + `BoxJoint`s, with hard invariants (`Valid`) and `Of` the derive-side mirror reading the partition a grown unit implies (`SharedEdge` finds the abutment intervals). The typed target the partition-first allocator (G63) emits; boxes may overlap, joints assert only real abutments. |
| `BoxPartitioner` | `Compose/Boxes/BoxPartitioner.cs` | the partition-first allocator: `budget → BoxPartition`. Ships parallel to `TeamUnitGrower` (not the default — the switch that fills the partition and retires the grower is G63-C): `Partition` grows one unit and reads its partition off `BoxPartition.Of`, round-tripping through the mirror, and carries the two-currency budget accounting (`BudgetCells` the land currency, `WithinBudget` the balance check). |
| `SpawnWoolRooms` | `Compose/SpawnWoolRooms.cs` | the wool-lane-c terminal carve (box rooms arrive pre-carved). |
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
| `lane-audit.cs` | `tools/deriver/lane-audit.cs` | the `ClassifyOpen`/`LaneName` derive-then-override training harness. |

---

## 13. Boundaries

This document does not restate the rules or the numbers. The **frozen rule law** — every CT / SP /
WL / LN / HB / FR / MD / BZ / EL id, with its exact widths, depths, hop counts, and heights — is
`layout-rules.md`, and it grows only through its correction protocol. The **measured envelopes** the
soft evaluator terms score against are `seed-stats.md`. The **plan schema and editor** are
`plan-editor.md`. The **detailed deriver-measurable and evaluator-metric catalogue** is
`layout-evaluator.md`.
