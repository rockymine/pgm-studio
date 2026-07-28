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
| `rules.md` | The rule law and every number (widths, depths, hop counts, heights, the CT / SP / WL / LN / HB / FR / MD / BZ / EL ids). |
| `seed-stats.md` · `seed-envelopes.md` | The measured envelopes the soft evaluator terms score against. |
| `evaluator.md` | The detailed deriver-measurables and evaluator-metric catalogue. |
| `vocabulary.md` | The **living type catalog** — every type as a map concept, by pipeline order. §1 defines the terms, §14 is the code map, this names the types that embody them. Extend it in the same commit a task adds/renames/retires a type. |
| `audit.md` | The standing measured record of where the implementation and this model **disagree** — the evidence behind the open G-tasks. |
| `ideas.md` | The G-track idea pool: open work not on the board. |
| `../contracts/plan-editor.md` | The field-level `*.plan.json` schema and the editor UI (the Plan tool). |

**The live twin.** `tools/compose/showcase.cs` renders this same model as one self-contained HTML
page with **every figure emitted by the real generator** (`dotnet run tools/compose/showcase.cs`).
Where the prose here and the showcase disagree, suspect the prose: the showcase cannot drift,
because its figures are built by the code being described.

---

## 1. Glossary — the locked terms

### 1.1 The five pipeline verbs

Generation is five verbs, not one. **Never say "generate" for the whole thing** — it is ambiguous
(it has been used both for the whole pipeline and for the narrow `intent → map.xml` step). Use the
specific verb:

| Verb | Means | Where it lives |
|---|---|---|
| **emit** | Fill one box with one base shape (forward). | `ShapeEmitter` + the per-kind bindings |
| **derive** | Read structure back out of geometry (inverse). Two derivers — see §1.3. | `ShapeClassifier`, `ContactGraph` + `BoardDeriver` |
| **compose** | Build the plan: `budget → allocate → fill → carve → assemble`. | the composer (`Composer`) |
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
| **shape deriver** | one box's terrain | the family (§1.2) — the emitter's mirror | `ShapeClassifier.Classify` |
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

The key: **a build zone costs footprint but not land**. Detailed in §10.

### 1.11 The small words

- **box** — a bounding envelope (§4), *not* a fill target.
- **lane** — a simple corridor (bend count `I / L / Z`), the board deriver's `ShapeClassifier.ClassifyOpen` read.
- **approach** — the whole wool-box shape (one of the nine families). *Lane ≠ approach.*
- **menu** — the set of families an interface width makes legal (the width→fill production rule, §4).
- **mid** — the neutral band between the frontlines; its **form is `f(frontline)`** (§11).
- **frontline** — a **join**, not a placement, and a **derived edge attribute**, not a piece (§4, §8).

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
| **demand** | a shape's requirement *on its environment* (inbound) | *(no live type — `FamilyDock` was retired with the clamp's redefinition; the dual-entry requirement survives only as `UnitRequests.Overhangs`, stated **by exclusion**. `audit.md` §1; G107)*<br>The word is now the model's alone: the allocator's outbound sizing type was renamed `NeighbourRequest` so **demand** means only this inbound sense. |
| **offer** | constraints a shape imposes *outward* — the edges/intervals it invites neighbours onto, in which groupings | `EdgeOffer` — the hub's per-run widths (granted per dock on the joint) and the frontline's face |
| **veto** | a never-attach / never-publish mark | `SlotDockRole.NeverDock`, `PublishPolicy`'s bay/hole veto |
| **gate** | the hard legality check applying demand/offer/veto, with a **directed rejection** | `DockingGate` → `DockRejection`, `PublishPolicy` → `PublishVerdict` |
| **knob** | a free parameter *within* legality — never changes identity | entry shift, attachment width, arm placement |
| **target** | a **per-request, prescriptive** constraint a compose holds and verifies | `ComposeTargets` (G98) |
| **band** | a **descriptive** envelope measured off the seeds — advisory, scores distance | `SoftTerm` + the seed envelopes |
| **hard term** | a well-formedness symptom on the derived board — flat penalty | `WoolRingedHole`, `GapHopBand` |
| **law** | the id-bearing author rule the mechanisms implement — a **living** set, amended by protocol | `rules.md` FR6, CT9, BZ8 |
| **doctrine** | a meta-rule about where rules may live | "labels drive, the mirror verifies" |

Two distinctions carry the weight. **demand vs offer** is the direction of the arrow: an approach
*demands* (its entry must find a host); a hub or frontline *offers* (its edges dictate where and how
wide neighbours land — the constraint source of §4). **target vs band** is prescription vs
description: a band says *authored maps run 1–7 frontline runs*; a target says *this compose wants
exactly 2, connected*. Bands score a finished compose; targets steer it first and verify after.

**An offer also carries a grouping** (`OfferGrouping`), because *where* and *how wide* do not say
whether neighbours may share: `Several` — each interval takes its own consumer (a hub's four edges;
the twin frontline's two tips) — or `Joint` — one consumer must span the whole group flush (the wide
face across both tips, the inter-tip recess preserved as a hole). Joint-vs-several *is* FR6's wide
vs split frontline.

**Two kinds are vocabulary-only.** `target` is locked as a term but has **no live type**
(`ComposeTargets` was declared pending and never landed) — the budget ladders of §6.1 are what would
become targets. And the allocator's weighted sampling (§6.1) fits **no** declared kind: a `menu` is a
set and carries no frequency, a `band` carries a distribution but is explicitly descriptive. Naming
that kind — provisionally **mix** — is an open author decision. Both are tracked in `audit.md`
(G108/G109).

---

## 2. The pipeline

Generation runs **from the hub outward**, in a relative frame, and embeds late. The composer is
**allocate-then-fill**: every box footprint is positioned *before* any terrain exists, and filling
never grows a box outward.

```
request → budget → allocate (boxes + joints) → fill (hub-first) → carve the mid
        → assemble → gate ──accept──► plan → realize
                      └────reject───► resample
```

- **request** — players per team, team count, symmetry, seed (§3.1).
- **budget** — the player count fixes the land and footprint targets and the board's extent (§3.2).
- **allocate** — the budget draws a **placement plan** (which neighbour sits on which hub side) and
  lays out typed box footprints, seating each on the hub's real free surface (§4, §6).
- **fill** — the hub emits **first** as the constraint source; each neighbour consumes the offer on
  its own joint and fills its footprint with a shape (§5, §7).
- **carve** — the mid band is derived from the frontline faces (§11).
- **assemble** — the labeled pieces become a `plan.json`; slots drop here (§3.3).
- **gate** — the evaluator's hard terms accept or reject the attempt; a rejection **resamples the
  whole attempt** (60 allowed), so "no shape fits" is a signal, never a crash (§9).
- **realize** — the accepted plan is compiled and exported (§2.1).

**Fragment** — converting land to build (isolation cuts, stepping stones), footprint-conserving
(§10) — is part of the model but is **not** in the shipped compose loop: today the only build region
is the mid band.

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
elevation — §12, §13). Once the author takes the sketch into the editor for hand work, the plan
**freezes as provenance**
and sketch + intent become the working artifacts. Recovering plan meaning from edited geometry is
out of scope.

---

## 3. The request, the budget, and the plan

### 3.1 The request — everything a compose is given

A compose takes **four decisions and a seed**, and nothing else. There is no geometry input:

| Parameter | Range | Means |
|---|---|---|
| `playersPerTeam` | clamped **5–32** | the only size input — the land budget and every structural ladder derive from it |
| `teams` | **2** or **4** | 4 teams force `rot_90` |
| `symmetry` | `rot_180` · `mirror_x` · `mirror_z` (2 teams) · `rot_90` (4) | which orbit fans the authored unit; defaults to `rot_180` / `rot_90` |
| `seed` | any `ulong` | drives **every** draw — the same request reproduces the same plan byte-for-byte |
| `cell` | default **5** blocks | the proxy-cell grid scale |

Bad combinations throw at construction rather than failing deep inside generation. **Sampling order
is part of the contract**: the RNG is drawn in one fixed sequence, so inserting a draw anywhere
re-rolls everything downstream of it — which is why a geometry change means a composer version bump
and a fingerprint re-record.

### 3.2 The budget — the envelope

The budget is derived once, before any geometry, from the player count alone:

- **land per player** is a piecewise-linear interpolation over corpus anchors (5 players → 65
  blocks², rising to 32 → 185), so `landPerTeam = players × landPerPlayer`. This is the **land**
  currency of §1.10 and it counts *piece* area only — build zones carry a separate target.
- **the board's extent** comes from a sampled **coverage ratio** (0.28–0.42 — the corpus's measured
  land coverage of the fanned board): `fannedArea = teams × landPerTeam / coverage`. A 4-team board
  is square (side clamped 90–180 blocks); a 2-team board additionally samples an **aspect** 1.0–3.0
  and clamps width to 25–130 and length to 100–280.
- **the unit bounds** — the authored unit gets half the doubled axis, keeping an axis margin of 2
  cells between its frontmost piece and the symmetry axis.

Everything downstream is sized from this one record. The **surface** (9) and **headroom** (11) are
fixed constants, not yet budget-derived.

### 3.3 The plan artifact (`plan.json`)

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
build-zone kinds, and the wool-approach family. These belong to the derivers (§8), not the file.

**Compose-internal** (a third category, neither authored nor derivable): the slot labels of §5.3.
They exist on generated pieces during composition, drive the compose-side rules, and drop at
`Assemble` — a plan on disk never has slots, and no deriver recovers them from an authored or traced
plan (§5.4).

**Plan invariants** (checkable with zero geometry): every wool reachable from every capturing team's
spawn across `land` + `gap` interfaces; no wool path through a `spawn` piece; ≥1 `gap` on every
inter-team path; interface widths ≥ the corridor minimum; spawn depth ≥ some distance from the
nearest frontline interface.

The field-level schema and the editor are in `../contracts/plan-editor.md`.

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
- **mid** — the neutral band between the two frontlines (§11).

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
vacancy — §4.1). Docking one entry to one interface — all a fill can express today — forces the clamp
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
makes the room the door. A **declared bay is only valid at the elevation stage** (§13), where height
fixes what the flat read cannot: with a host touching both entry and wool, the wool must be raised
significantly so the entry-host dock is the *only* approach, the scythe terrain **stepping up from
entry to room** — the height mechanism is noted for later (G81); until then room-host contact simply
rejects.

**"No shape fits" is a signal, not a failure.** An over-constrained box is answered by **changing the
box** (resize, relax an interface, split it) — the Tetris failure feeds back up a level. Every fill
reports its refusal as a **directed reason** (`FillRejection`: `TooSmall` · `FormDoesNotFit` ·
`NotOnMenu` · `IllegalDock` · `UnsupportedKnobs`) rather than a bare null, so the caller knows which
knob to turn.

### 4.1 Boxes may overlap; fills publish vacancies

A box allocates a **budget, not exclusive area** — two footprints may overlap, and a joint is
asserted only where two footprints genuinely abut. Because a fill need not fill its box solid, what
it leaves over is published as a **vacancy**, exact by construction and classed by the edge taxonomy
(§1.13): a `bay` (open toward one edge — claimable by a later box), a `notch` (a corner remainder),
or a `hole` (enclosed by the shape).

**Publishing is an offer, never a fill.** A later pipeline step may claim a vacancy (a third wool in
a bare hub's bay) or nothing may — an unclaimed vacancy is simply void, or a `buffer` when the void
is deliberate. What may be published is a **veto** (`PublishPolicy`): a terminal-capped shape vetoes
its bays and holes and allows only its notches, because its bay shelters the goal; a terminal-free
body allows everything.

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

Approach-shape identity is `ShapeFamily` — the **approach designation** over a
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

All nine live on one enum, `ShapeFamily` — the single taxonomy the emit side and the derive side
share, so the mirror closes as `derived == requested`. The emitter builds the eight non-isolated
families; `Isolated` is a **derive-only** reading with no terrain to emit.

### 5.2 The width-independent classifier

`ShapeClassifier.Classify` is one decision tree over the terrain, **strongest signal first**, and
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
(`wool-approach-read.md`). **The mirror is enforced by the test suite**, in
`tests/PgmStudio.Pgm.Tests/Shapes/` — emit every family × size × width, derive back, assert equal
with no overlap, and assert the emitted slot sequence equals `ApproachSlots.Template`; a companion
set pushes each family's pieces to extremes at a fixed width so every one must still read its own
family (the width-independence proof). The §5.1 catalog itself is checked in as fixtures under
`tools/deriver/shapes/*.plan.json`.

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
  and frontline boxes attach onto — the solid rectangle's "four full edges" is just the degenerate
  case. Built by `HubBoxEmitter`.
- **Frontline** — a body + one edge marked the **`face`** (where the fanned images meet), docking the
  hub on the opposite edge and driving `mid = f(frontline)` (§11). **Rotation is fixed by the
  designation**: the body docks the hub with its **spine**, and its **arm-tips are the face** toward
  the axis. Form menu: a plain **Bar** (the wide face, FR6), the **branch family** (spine + K arms —
  single = K1 / twin = K2 / more), and the **holed** forms (P, two-U-on-I — a closed recess). Its
  face offer carries the **grouping** of §1.14 — `Joint` (one mid consumer spans every tip flush) or
  `Several` (one per tip, the inter-tip recess simply not offered and surviving as a deliberate
  hole). Built by `FrontlineBoxEmitter`.

Two things once called shapes are **not bodies** but shared docks in the designation layer (§5.1):
the **clamp** — one compact terminal on two distinct faces (opposite → centered I+I; adjacent →
corner L+I, the bend forced because two straight bars would corner-touch) — and the **twin frontline**
— two Bars docked to one host individually, the gap between them the face/CT8 recess.

---

## 6. Allocate — from a budget to a placed partition

Allocation is the half of composition that decides structure and position, before any terrain exists.
It begins with nothing but a budget and ends with a `BoxPartition`: typed boxes, the joints between
them, and the spawn's facing. Everything it produces is a rectangle in plan cells; nothing it produces
is terrain.

The order matters more than any individual rule in it, because each step consumes what the previous
one settled and never reopens it. The hub is placed. Its form is chosen and emitted, which is what
turns a footprint into material and so decides where anything may dock. Separately, and in ignorance
of all of that, the unit works out what it needs hung off the hub. Only then does seating put the two
together, and seating's entire output is one integer per neighbour.

### 6.1 The hub goes down first

The hub is the only box in the unit that ever receives absolute coordinates. Its rectangle is drawn in
the symmetry frame — so many cells out from the axis, so many across — and every other box is
positioned relative to it. Nothing downstream re-opens that decision.

Its depth toward the axis and its lateral span are drawn from different caps, so a hub grows wider
rather than squarer. The long lateral edge is what gives the spawn and the wools room to attach with a
gap between them, and past nine cells it affords the wide holed bodies, whose bar and ring runs are
long stretches of free surface. Where the plan carries a frontline, the frontline's reach pushes the
hub's front edge back, so the frontline ends up between the hub and the axis rather than beside it.

The allocator, not the filler, owns the choice of hub form, because the form decides where neighbours
can sit. It emits the body once to read what that body offers, and the chosen form — with its wall
widths and arm layout, where it has them — rides on the hub box so the filler re-emits exactly the
same body. A body sampled twice would not agree with itself.

### 6.2 The form decides where anything may dock

Emitting is what turns a footprint into material. The form decides which cells inside the hub's
rectangle are terrain and which are holes, and that distinction matters immediately, because the next
thing read off the emitted body is where the hub actually has material along each of its four edges.
Those stretches are its **runs**.

A run is not an edge and not a side. It is one contiguous interval of real material on one edge,
measured in cells from that edge's origin. A solid rectangle offers a single run per edge, spanning it
end to end. A ring or a U offers an edge broken into two runs with a hole between them, and a bay
yields no run at all over its stretch. This is what stops a neighbour docking an empty stretch of an
L-shaped hub's bounding box and meeting the real body only at a corner.

Each run is published as an **offer**, carrying the width that run can support — a capacity derived
from its own length. The offer bounds the search rather than filtering its output: a seat that would
put a neighbour where the hub has no material is never proposed, not proposed and rejected.

### 6.3 What the unit asks for

Independently of the hub, and before any position exists, the allocator works out what the unit needs.
The player count fixes how many wool boxes there are, whether there is a frontline, and how large the
spawn is. Each of those becomes a `NeighbourRequest`.

A request is a request, not a rectangle. It names which side of the hub the neighbour belongs on, what
kind of box it is, and two extents: its **depth**, how far it reaches away from the hub, and its
**along**, how far it runs parallel to the hub's edge. Nothing in a request is a coordinate. A wool's
request also carries the shape family rolled for it, because the family is what set those two extents
in the first place.

Depth and along are named from the edge, not from the world. The same pair means an x-extent on a
neighbour docked to the hub's top and a z-extent on one docked to its left. That is inherent to an
edge-relative frame, and it is the single most common source of confusion in the allocator.

The along-extent is checked against the hub's edge length, and the overhang families are deliberately
exempt from that check. A staple whose mouth is wider than the edge demotes to an L — which is to say,
demotes into the overhang path — while an L or a donut may be born wider than the edge it will dock,
because the overhang rule only ever needs its entry to land. This exemption is the whole permission
for a box to exceed the run it sits on, and it is one negation in one condition.

How many neighbours there are, and whether a frontline exists at all, comes from thresholds on
land-per-team and player count:

| Ladder | Effect |
|---|---|
| land > 2500 | the map-wide lane width is 3 cells rather than 2 |
| land < 800 | no frontline — there is no budget for one, so the hub fronts the mid directly |
| land < 600 | a single wool; a tiny board cannot hold two |
| players ≥ 16 | a full team: 2–3 wools rather than 1–2 |

Alongside these sit roughly a dozen sampling weights — how often a wool is bent rather than straight,
how often a bent wool is a donut, how often a big-square hub takes the ring, how often the frontline
spans the hub's full front width. These steer the output's character more than any other numbers in
the generator, and most of them trace to no law in `rules.md`: they were tuned, not derived. Which are
grounded and which are invented is measured in `audit.md`.

### 6.4 The seat

Seating's entire job is to turn a request into a position, and the position is a single integer: the
**seat**, the offset in the hub's edge-local coordinates at which the neighbour's along-extent begins.

Once a seat is chosen the rectangle follows mechanically. `NeighbourRect` takes the hub, steps outward
from the chosen edge by the request's depth, and runs its along parallel to that edge. That function is
the only place a neighbour's rectangle is ever built, and it contains no branch beyond the four edges.

A seat may be negative, or may run past the far end of the edge. This is not an edge case: it is how a
box comes to hang past the hub's corner, over empty space. "The neighbour only grows outward" is true,
and it describes the depth direction alone — a box never penetrates the hub and never floats free of
it. It says nothing about the along direction, where the box's size was fixed before the hub's edge was
consulted.

Because the search runs in a single integer, nothing is ever built and then moved. Every adjustment in
the allocator — the front guard's backward slide included — is arithmetic on the seat, and the
rectangle is derived once, at the end, from the value that survived.

The rectangle that results is an **envelope, not a fill target**. Its contents must touch its edges and
stay connected, but need not fill it solid; an L in a five-by-four box leaves a whole quadrant empty.
That is what lets one shape take many footprints inside a fixed rectangle.

### 6.5 The three dock rules

Which seats are legal depends on the dock style, and the style is never sampled. It follows from the
family roll that has already happened, which makes it a derived property of the request rather than a
decision of its own.

The three are not an arbitrary list. They are indexed by how much is known about where the shape's
entries are.

**Full mouth** knows nothing. Requiring the whole along-extent to sit inside one free run guarantees
every entry lands, however many there are and wherever they sit. It is the conservative, shape-agnostic
rule, and it is why the two-legged staples dock this way — an overhang would strand a second entry off
the host, which is a pinch.

**Overhang** knows there is exactly one entry and where it sits. The shape is emitted into a probe box
to find the entry's interval, and the seat is then solved so that interval — and only that interval —
lands inside a run. The body is free to hang past the run's end, over the bay or past the hub's corner.
Both handednesses are tried and one legal placement is sampled.

**Contact patch** applies to the frontline alone, which is a face rather than a corridor and so has no
entry at all. Its face may be narrower than the hub's edge or wider than it, and what must hold is that
every stretch where it meets a run is at least a lane wide. Every, not any: a face spanning a bay rests
on a shoulder each side of the hole, and a shoulder thinner than a corridor leaves the face cantilevered
over the hole, held by one side. A second rule rejects a face whose end lands exactly where a run stops,
because the face's end cell and the hub's last filled cell would then touch only at a corner.

| Style | Who | What must land on a run |
|---|---|---|
| full mouth | spawn, plain wools, the two-legged staples | the whole along-extent |
| overhang | `L` and `Donut` wools | one narrow entry interval |
| contact patch | the frontline | every contact, each at least a lane wide |

### 6.6 What keeps neighbours apart

The runs constrain a neighbour against its host. A second, independent constraint holds neighbours
apart from each other, and a seat must satisfy both.

No spawn or wool may seat within the map's lane width of another. Every already-seated neighbour
projects onto the edge being seated as a forbidden along-interval, so a legal seat is sampled directly
from what remains rather than proposed and rejected. The projection is what makes one mechanism cover
two cases: a neighbour on the same edge projects to its own dock interval, and a neighbour on an
adjacent edge projects only when it hugs the shared corner, which is exactly when it could collide.
The paths that bypass this sampling — the overhang and the frontline — test the same clearance
directly, by rectilinear nearest approach, so a diagonal corner meeting is caught and not only a shared
edge. The frontline keeps no separation from a wool; its clearance is a build-zone rule, not this one.

The separation is enforced between **boxes**, never between the shapes inside them. Since a shape is
contained in its box, box separation implies shape separation — a sound over-approximation, and a
conservative one. Two shapes may end up far further apart than the lane width and can never end up
closer, at the cost of refusing arrangements whose boxes collide before their terrain would.

### 6.7 When a seat cannot be found

Failure is a ladder, not a cliff, and each rung is a different answer.

A rich wool that finds no legal overhang demotes to the compact inline `I` — the always-seatable shape
— and re-enters as a full mouth. A wool whose full mouth then finds no run demotes the same way. A wool
that still cannot clear the separation gap is **dropped**, provided another wool has already seated:
the unit keeps its objectives, one fewer, rather than failing entirely. The spawn and the frontline are
not droppable, because a spawn or frontline that cannot seat is a genuine too-small signal.

That signal propagates upward. The allocator retries the whole seating on the solid rectangle hub,
whose four full edges usually hold a lawful seat the chosen form's runs could not, and only when that
also fails does the attempt return nothing and the composer resample.

One further pass runs where a unit has no frontline. A lateral seat left flush with the hub's empty
front would extend that face into one long flat frontier, so the seat slides backward — deterministically,
consuming no draw — to the nearest position that clears the front. Seats that no backward position can
hold are collected and resolved after every neighbour is placed, when the full set is known and an
earlier drop may have freed the very blocker.

### 6.8 What a joint records

When a seat survives, two things are written into the partition. The box goes in, carrying its
rectangle and its share of the land budget. A joint records the **abutment** — the interval where the
two rectangles actually touch, obtained by intersecting them — and the **grant**, the corridor width
this consumer was given across that abutment.

The grant is not the host's offer travelling forward, and the two carry different quantities. An offer
publishes a **capacity**, whose width comes from the length of the run it sits on; a grant records a
**selection**, whose width is chosen per consumer kind — a wool reads the narrow wool lane, a spawn or
frontline the map's lane width. One run can carry two docks at two widths, which is why the grant is
per joint and why the filler reads a neighbour's width from its own joint rather than from the edge.

Four along-extents therefore exist at a single dock, and they coincide only in the simple case: the
request's along, the shape's entry, the geometric abutment, and the granted width. They come apart at
exactly the two docks worth understanding — an overhang wool, whose entry is narrower than its box,
and the frontline, whose face may exceed the hub's edge so the abutment is clipped narrower than the
box. Of the four, the abutment is the only one describing something a player can walk through.

## 7. Fill — hub-first, and the offer consumed

Filling takes the allocated partition and puts terrain in it. Footprints are never grown outward;
the geometry is filled **into** what the allocator positioned.

**The hub emits first**, at the form the allocator chose, and publishes **one offer per free run** at
the width that run can support. Then each neighbour box fills, docking the hub with the edge facing
it, and the **offered width *is* the neighbour's corridor width**. That is the whole constraint-source
contract: the hub sources the width, the neighbour builds to it.

**The grant is per joint** — not per edge, and not even per run. A third wool doubling onto the
spawn's side shares the spawn's edge, on a solid hub the very same run, at a different width; an
edge- or run-keyed lookup would hand one of the two the other's corridor width.

The neighbours differ in what they re-decide. A **wool** re-emits the exact family the allocator
seated (the allocator positioned the box for *that* family's entry, so the filler must not re-pick).
A **spawn** picks among the profiles that fit at the granted width. A **frontline** picks a form that
answers the hub's: a branch hub takes the wide Bar across its front, but a square or holed hub
prefers a staple or strand, because a solid Bar flush against an already-square hub reads flat.

Two decisions sit on the **wrong side of this seam**, and are known defects rather than design: the
frontline's **form choice** and its offer **grouping** (joint vs several — which is FR6, an authored
law) are both made here in the filler, the grouping by coin flip. Form choice is declared the
allocator's, and grouping is part of an offer, which is the allocator's plan. See `audit.md` (G111).

---

## 8. The two derivers

### 8.1 The shape deriver

`ShapeClassifier.Classify` reads **one box's terrain** and returns its `ShapeFamily` (§5),
width-independently; the reported width is an output, kept for the width report only. `ClassifyBody`
is the body-layer twin, reading a terminal-free `ShapeBody` back to its `Compound`. Both are the
emitter's mirror.

### 8.2 The board deriver

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
- **the CT mid-form** — falls straight out of the build-zone kinds (§11).

The detailed measurables catalogue — every derived quantity, its exact definition, and its
validation against the seed corpus — is in `layout-evaluator.md §5`.

---

## 9. The evaluator

The emitter can make anything; the maps' character comes from **what evaluation refuses to let
through**. The rules do not *produce* good maps — they *punish* bad ones, and the residue is the
style.

**The model is three layers:**

| Layer | What it is | Where |
|---|---|---|
| **author intent** | the irreducible input | `plan.json` (§3) |
| **derive structure** | the roles + topology, computed | the derivers (§8), in-memory |
| **judge by property** | metrics vs rules + envelopes | the evaluator |

Everything the file cannot recover is authored; everything structural is derived; everything the
rules check is judged. The form:

```
score = Σ hard-penalty(violated well-formedness) + Σ w · envelope-distance(metric)
```

Hard rules are large penalties (a valid layout has none); "feel" is each metric's distance outside
the authored envelope (`seed-stats.md`). The evaluator returns the score **and the list of violated
terms** (each citing a `rules.md` id) so a failure is legible and actionable. It is
**additive and never has to be complete** — new terms are added as failures are found, and a new term
never tanks an acceptance rate.

**The evaluation set is the real deliverable.** The evaluator is correct when it ranks a labeled set
the way the author does: **positives** (authored good layouts, auto-labeled by the deriver),
**negatives** (flagged bad layouts — the most valuable are **minimal pairs** differing in exactly one
property), and **coverage** (examples per sub-problem × per symmetry mode). The property-term
catalogue and the labeled set live in `evaluator.md` §6–§7.

**The seeds sit at final-pipeline fidelity.** The authored seeds are what the *whole* pipeline
should output — never what an early stage can produce on its own. A stage is therefore judged only
against the rules that stage owns (fill/slot invariants at emit, envelope terms on the assembled
board, elevation feel at realize) — comparing an intermediate artifact to a seed wholesale is a
category error, the same one as classifying a finished map (§5.4) in the other direction.

---

## 10. Budget and width

### 10.1 The two currencies

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

### 10.2 Width, disentangled

The four "widths" and the two modes are in §1.9. The distinction to hold when a rule reads
contradictory: **generation-width** is the master variable (it gates the menu and sets connectivity);
**read-width** is orthogonal to family (the family is the turn count, read width-free). Width chooses
which family is legal and how it joins; it never changes what a given shape *is*.

---

## 11. The mid

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

## 12. The roughen pass

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

## 13. Elevation

Elevation attaches to **roles and interfaces, not to geometry**. The vocabulary: per-shape `floor` /
`base_height` (plateaus); splitting a shape along a seam and offsetting the piece (**cut + raise** —
at the plan level, refining one piece into two joined by a `land` interface with a height delta);
`anchor_heights` gradients (ramps); stacked layers.

The role/interface rules: a **raised spawn** (overview); a **stepped approach** climbing toward a wool
room (a harder push); a **low frontline** (bridges launch low, defenders hold the high ground); a
**`cliff` interface** where one-way flow is wanted. Constraint: walkable steps along any `land` path
unless the plan says `cliff`. The exact height numbers are the `EL` rules in `rules.md`.

---

## 14. Code map

Where each concept lives (paths under `src/PgmStudio.Pgm/` unless noted):

**The shape mirror — emit ↔ derive**

| Piece | Path | What |
|---|---|---|
| `ShapeEmitter` | `Shapes/ShapeEmitter.cs` | **emit**, in two stages: `Body` builds the terminal-free compound, a designation finishes it (`Emit` = the approach designation, stamping the terminal room + marker). No roles, no ids, no plan types. |
| `BodyEmitter` · `Compound` | `Shapes/BodyEmitter.cs`, `Shapes/Compound.cs` | the terminal-free body vocabulary (`Rectangle · SpineArms · Ring · DoubleHole · P · G · TwoUOnI`) and its emitter. |
| `BodyEdges` | `Shapes/BodyEdges.cs` | the edge-taxonomy reader (§1.13): negative spaces by wall count, their parts, mouths and the offerable surface — from geometry alone. |
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
| `Composer` | `Compose/Composer.cs` | `Compose(ComposeRequest)` — the entry point: envelope → band-only crossing → allocate → fill → carve → assemble, gated by the evaluator's hard terms. |
| `TeamUnitAllocator` | `Compose/TeamUnitAllocator.cs` | the allocate entry point: hub size, hub position (the unit's only absolute rect) and hub-form choice → `BoxPartition` + spawn facing. |
| `UnitTuning` | `Compose/UnitTuning.cs` | the size ladders, the shape mix, the seat clearances, and the placement plan (`UnitPlan`) they feed. |
| `UnitRequests` · `NeighbourRequest` · `DockStyle` | `Compose/UnitRequests.cs` | what hangs off the hub, sized coordinate-free, and the dock style each request implies. |
| `UnitSeating` · `FullMouthDock` | `Compose/UnitSeating.cs` | requests → seats, under the three dock rules (full mouth · overhang · contact patch). |
| `SeatGeometry` | `Compose/SeatGeometry.cs` | `NeighbourRect` and the edge arithmetic around it: projection onto an edge, clearance, the hub joint each dock records. |
| `TeamUnitFiller` | `Compose/TeamUnitFiller.cs` | fills the allocated partition hub-first (offer consumption) → `FilledUnit` (a `GrownUnit` + the frontline face offers). |
| `GrownUnit` · `GrownPiece` | `Compose/GrownUnit.cs` | the composed unit records (pieces with `Slot`/`Box` labels + spawn/wool placements). |
| `Envelope` → `ComposeEnvelope` | `Compose/Envelope.cs` | the budget: player count → land-per-team, board extent, unit bounds (§3.2). |
| `ComposeRequest` | `Compose/ComposeRequest.cs` | the compose input, validated at construction (§3.1). |
| `FrontGuard` | `Compose/FrontGuard.cs` | the no-frontline seat post-pass: slide/relocate/drop a seat left flush with the empty front. |
| `UnitPlacement` | `Compose/UnitPlacement.cs` | re-anchors the finished unit on its **face** before the band is derived. |
| `Producibility` | `Compose/Producibility.cs` | "could the composer have produced this?" — answered by search over the declared menus, not by inverse. |
| `WoolBoxEmitter` | `Compose/WoolBoxEmitter.cs` | the wool binding over `ShapeEmitter` — fills a wool box, terminal → wool room + marker. |
| `SpawnBoxEmitter` | `Compose/SpawnBoxEmitter.cs` | the spawn binding: profile {I, L} + `Fill`, terminal → `Spawn` room + marker. |
| `HubBoxEmitter` → `EmittedHub` | `Compose/HubBoxEmitter.cs` | the **hub** designation: a terminal-free body plus the per-run `EdgeOffer`s it publishes as the constraint source. |
| `FrontlineBoxEmitter` → `EmittedFrontline` | `Compose/FrontlineBoxEmitter.cs` | the **frontline** designation: spine docks the hub, arm-tips are the face, carrying the face offers the mid consumes. |
| `EdgeOffer` · `OfferGrouping` | `Compose/Boxes/EdgeOffer.cs` | the **offer**: where a neighbour may attach, at what width, in which grouping (§1.14). |
| `FillProfiles` | `Compose/Boxes/FillProfiles.cs` | the per-`BoxKind` profile as data: legal families + the footprint fit gate. |
| `BoxFiller` | `Compose/Boxes/BoxFiller.cs` | the one profile-gated fill entry point over a positioned `Box` + land-vs-target accounting (the spine G63 drives). |
| `BoxInterfaces` | `Compose/Boxes/BoxInterfaces.cs` | the valid-edges data model: `Of` reads a box's edges off the shape as `BoxEdgeInterface` **facts** (span + the template slots on each edge) — it observes; the docking *rules* over the facts are the `DockingGate`. |
| `DockingGate` | `Compose/Boxes/DockingGate.cs` | the compose-side docking gate: `SlotDockRole` (room→never-dock, entry→docking, rest→internal) + the verdict over the `BoxEdgeInterface` slots. A dock is legal iff it lands on an entry and seals no wool — no per-family imperative code, shape-relative. Every family now docks through a **single mouth**, so the verdict reads only the edge's slots, never a family name. Not an `ILayoutTerm`. |
| `BoxPartition` | `Compose/Boxes/BoxPartition.cs` | the partition constraint graph: typed `Box`es + `BoxJoint`s, with hard invariants (`Valid`) and `Of` the derive-side mirror reading the partition a grown unit implies (`SharedEdge` finds the abutment intervals). The typed target the partition-first allocator (G63) emits; boxes may overlap, joints assert only real abutments. |
| `MidCarver` | `Compose/MidCarver.cs` | the mid: the flush, hull-exact build band (band-only today; richer crossings layer back in here). |
| `ClosureAnalysis` | `Compose/ClosureAnalysis.cs` | closure hole raster (`HoleSizes`, `AnyHoleRingedBy`). |
| `ComposeGeometry` | `Compose/ComposeGeometry.cs` | fanning + the fanned-separation invariant. |
| `PlanModel` · `PlanRoles` | `Plan/PlanModel.cs` | the plan format + the authored role set. |

**Harnesses and galleries** (`dotnet run <path>` — a file-based script; see `CLAUDE.md` on the
runfile cache before measuring a `src/` change)

| Piece | Path | What |
|---|---|---|
| the shape mirror | `tests/PgmStudio.Pgm.Tests/Shapes/` | emit↔derive + slot-template + width-independence, as tests (§5.4). |
| `showcase.cs` | `tools/compose/showcase.cs` | **the explainer** — this document's live twin, every figure emitted by the real generator. |
| `unit-gallery.cs` · `box-gallery.cs` · `board-gallery.cs` | `tools/compose/` | composed units / box partitions / whole boards, rendered. |
| `body-gallery.cs` · `edge-gallery.cs` | `tools/compose/` | the terminal-free bodies; the edge taxonomy read off them. |
| `seat-probe.cs` | `tools/compose/seat-probe.cs` | the 4-preset × 200-seed `Allocate → Fill` probe behind `audit.md`. |
| `reproduction-gate.cs` · `fingerprints.cs` | `tools/compose/` | the determinism gate + the recorded composer fingerprints. |
| `derive-gallery.cs` | `tools/deriver/derive-gallery.cs` | `BoardStructure` rendered → `out/derive-gallery.html`. |
| `lane-audit.cs` | `tools/deriver/lane-audit.cs` | the `ClassifyOpen`/`LaneName` derive-then-override training harness. |

---

## 15. Boundaries

This document does not restate the rules or the numbers. The **rule law** — every CT / SP / WL / LN /
HB / FR / MD / BZ / EL id, with its exact widths, depths, hop counts, and heights — is `rules.md`,
and it grows only through its correction protocol. The **measured envelopes** the soft evaluator
terms score against are `seed-stats.md` / `seed-envelopes.md`. The **detailed deriver-measurable and
evaluator-metric catalogue** is `evaluator.md`. Where the implementation is known to **disagree**
with this model, the measured record is `audit.md`. The **plan schema and editor** are
`../contracts/plan-editor.md`.
