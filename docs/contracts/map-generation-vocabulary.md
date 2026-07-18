# Map generation — the vocabulary (every type, by concept)

This is the **living catalog of the types** the box-model generator introduces, each described as a
*map-generation concept* — what it **means**, not its fields or signature. It sits between two other things:
`map-generation.md` **§1** defines the abstract *terms* (family, interface, width, budget…); `map-generation.md`
**§12** is the *code map* (which file); **this doc names the types that embody the ideas**, in the order the
concepts appear in the pipeline, so a newcomer can read top-to-bottom and learn the vocabulary once.

**It is a living document.** When a task adds, renames, or retires a type, update the matching row here in the
same commit — the same discipline as the task board. A type without a plain-language meaning here is a type
that will confuse the next person. Planned-but-unbuilt types are listed *italicised* with their task id.

The pipeline these are ordered along (from `map-generation.md` §2):

```
budget → boxes → interfaces → shapes → fill → compose (grown unit) → plan → derive → evaluate → realize
```

---

## 1. The budget — what sizes everything

The budget is **two currencies that must both balance** (§1.10): **land** (walkable terrain area, set by the
player count) and **footprint** (total box area — terrain + build + gap, set by the partition). A build zone
costs footprint but not land; that difference is what fragmentation spends.

| Type | What it means |
|---|---|
| `ComposeEnvelope` | The per-compose budget in one value: player count → land-per-team and footprint targets, plus the symmetry mode and the cell size. Everything downstream is sized from it. |
| `Envelope` | The step that *derives* a `ComposeEnvelope` from a request (the budget anchors — the land-per-player target). |
| `ComposeRequest` | The whole input to a compose: players, teams, symmetry, seed, cell size. |

## 2. The boxes — the coarse scaffold

Before any piece is filled, the budget draws a **coarse partition of typed boxes** (§4). A box is a *bounding
envelope, not a fill target*: its contents must touch its edges and stay connected but need not fill it solid —
that is what lets one shape take many footprints inside a fixed envelope. Boxes exist only during composition;
no finished map carries them.

| Type | What it means |
|---|---|
| `Box` | A typed bounding envelope the partition allocates — a rectangle a fill lives inside — carrying its **footprint** (its rectangle) and its **land target** (how much walkable land it should hold). The two-currency budget's per-box half. |
| `BoxKind` | Which kind of box: **spawn · hub · wools · frontline · mid**. The kind decides the fill profile (what may fill it) and the box's role on the map. |
| `BoxRef` | A piece's **box ownership** — which box's fill it belongs to. With the piece's slot it forms the full label (`wool-a/entry`) every compose-side rule binds to. |
| `WoolBox` | The specific axis-aligned region **one** wool approach is emitted into — the wool box's own geometry, mouth (hub-side) at one edge, dead-end room deep inside. |
| `BoxPartitioner` | The **partition-first allocator** (G63): `budget → BoxPartition`. It makes the partition the first-class artifact a compose produces — boxes allocated (footprint + land-budget half), then filled — rather than letting each footprint fall out of the fill. Ships **parallel** to the grower (not yet the default): today it grows one unit and reads the partition off it (`BoxPartition.Of`), round-tripping through the mirror, and carries the two-currency budget check (`WithinBudget`); the switch that fills the partition and retires the grower is G63-C. (The constraint graph it emits is `BoxPartition`, §3.) |

## 3. The interfaces — how boxes touch

An **interface** is always a shared **edge interval** — a position and a width — where two boxes meet (§1.5);
never a point. The **interface width** is the master variable of generation (§1.6): it sets connectivity,
classifies the joint, and gates the fill menu.

| Type | What it means |
|---|---|
| `BoxInterface` | One shared **edge interval** — a box-local edge, an offset along it, and a width. The concrete "here two boxes touch, this wide". |
| `BoxJoint` | One **edge of the partition graph** (G63): the two boxes it connects and the `BoxInterface` (edge interval) they touch along. |
| `BoxPartition` | The **constraint graph** a partition is (G63): the typed boxes (each an allocated footprint + its land-budget half) and the joints between them — what sampling produces once composition allocates boxes first and fills them second, replacing the sample-then-place shape record. **Boxes may overlap** (it allocates budgets, not exclusive area); a joint is only asserted where two footprints truly abut. `BoxPartition.Of` is the **derive-side mirror** — the partition a grown unit implies, so an allocator's partition round-trips through it. |
| `BoxEdgeInterface` | The **facts** about one box edge (the valid-edges data model, G41-B): its span (long/short) and the **template slots on it** — the pieces whose rects reach the edge, the room included. It **observes, it does not judge** — whether the edge may *dock* is a rule (the G80 gate) over these slots, not a field here. Read off the shape, so *shape-relative*, not a fixed box coordinate. (`TouchesRoom`/`HasTerrain` are convenience reads over the slots.) |
| `EdgeSpan` | A box edge's **length class**: `Long` on the box's longer sides, `Short` on the shorter — the distinction rules like "both clamp entries dock the short edge" are stated against. |
| `SlotDockRole` | The **dock role** a slot plays at an edge (the docking law as data, G80): `DockingEdge` (an `entry` — where a host connects) · `NeverDock` (the wool `room` — a dock seals the goal) · `Internal` (a `run`/`bar`/`leg` — shape-internal corridor, neither offers nor forbids a dock). One tag per slot; the gate maps the edge's slots through it. |
| `FamilyDock` | What a family **demands** of its docks (G80): how many distinct entry edges must connect (the clamp 2, its two bars; most 1) and any span constraint (the clamp its short edge). |
| `DockingGate` | The **compose-side gate** (G80): a dock is legal iff the edge lands on a `DockingEdge` slot, touches no `NeverDock` slot, and meets the family's span demand. Not an `ILayoutTerm` — it runs where the box is placed, producing only legal docks (`DockRejection` names an illegal one). Validity is *shape-relative for free*: the slots are read off the shape, so an entry shift moves the edge and the verdict follows. |
| `ContactKind` | The raw **piece-to-piece contact** reading, straight off the terrain rectangles: `Land` (they merge into one walkable mass) · `Narrow` (a thinner walkable seam) · `Corner` (a bare point — never connects) · `Overlap` · `None`. |
| `Contact` | One classified contact between two named pieces (its kind, its border length). |

## 4. The shapes — what fills a box

A wool box is filled with an **approach shape**. Shape identity is its **turn count plus the wool's seating**,
read *width-independently* (§1.2, §5).

| Type | What it means |
|---|---|
| `ShapeFamily` | The **nine** wool-approach families, an escalation of one another: `Isolated · I · L · Z · Scythe · Clamp · U · H · Donut`. The one taxonomy both the emitter (build one) and the classifier (read one back) share. |
| `Compound` · `CompoundRead` | The **terminal-free** compound taxonomy (§5 of shape-vocabulary.md): `Rectangle · SpineArms · Ring · DoubleHole · P · TwoUOnI`, read by topology alone (voids · arms · bends), no terminal. The identity `BodyEmitter` builds and `ClassifyBody` reads back — the body-layer mirror. A compound plus a designation (a terminal) is an approach; `ShapeFamily` is that terminal-capped view. |
| `ApproachSlots` | The **shape-internal roles** (the *slots*), two layers (shape-vocabulary.md §8): **structural slots** `run · bar · leg` (the rectangle's role in the compound, shared by every kind) + **designation marks** `entry · room` (the docking rect and the terminal, stamped by the approach), qualified `entry-run`/`room-run`/`entry-bar`/`room-bar` when a family has two. A slot is a **template position**, not a property of a rectangle — a scythe's `entry-run` and a donut's `leg` can be the same rectangle in different slots. Each family is an ordered template of these. |
| `RoomPlacement` | Where the terminal (wool/spawn room) sits relative to the last segment: `Inline` (caps it straight) or `SideTuck` (ducks off its side — still reads as the straight family). |
| `BoxEdge` | A box-local edge (`Top`/`Bottom`/`Left`/`Right`) — used to name a family's **mouth** (the edge its entry docks a host through). |
| `NegativeSpaceKind` | The **wall-count escalation of negative space**: `Notch` (wrapped by two edges — the L's corner) · `Bay` (three — the staple's recess, the hook's bay) · `Hole` (enclosed — the ring's void) · `Open` (at most one wall — plain outside, not a feature). The classes that decide which voids are publishable and which edges remain offerable. |
| `NegativeSpace` · `ClassifiedEdge` | One connected negative space read off a body (its kind, cells, wall count), and one maximal straight boundary run classified on **two independent axes**: the space it **faces**, and its **owner** — `Terminal` marks a run on the terminal room's own wall (a fact; the never-attach verdict over it is the docking gate's rule). Runs split where ownership changes, so a room capping a lane leaves the line part free, part sealed. The free offerable surface = `Open` ∧ not `Terminal`. |
| `BodyEdges` | The **edge-taxonomy reader**: classifies any rectangle set's negative spaces and boundary edges from geometry alone — the derive-side, shape-relative generalization of the emit-time `ShapeVacancy` publication (works on emissions, terminal-free compounds, future hub bodies). Rendered by `tools/compose/edge-gallery.cs`. |

## 5. The fill — emitting a shape into a box

**Emit** fills one box with one base shape (forward); **fill** is the profile-gated entry point over it. "No
shape fits" is a *signal*, answered by changing the box — never a crash.

| Type | What it means |
|---|---|
| `FillProfiles` | The **per-`BoxKind` profile as data**: which families a kind admits at a given width (wool = the width menu; spawn = {I, L}), plus the footprint gate (a family's minimum box must hold the footprint). The single source the menu and the footprint budget read. |
| `FillMenu` · `FillMenuRow` | The **width→fill production rule** (§4): the `w2/w4/w6` table saying which families an interface width makes legal. `FillProfiles` composes this for the wool box. |
| `BoxFiller` | The **one profile-gated fill entry point** over a positioned `Box`: validate/pick a legal family that fits, emit it, and report the **land** the fill spent against the box's land target (the two-currency balance). The spine the partitioner (G63) drives. |
| `ShapeEmitter` | The **pure family geometry** in two stages: `Body` builds the terminal-free compound, a **designation** finishes it (`Emit` = the approach designation, stamping the terminal room + marker). No roles, no ids, no plan types. |
| `ShapeBody` | The **terminal-free compound**: structural-slotted rectangles + vacancies, with no terminal, marker, or id — the shared layer every box kind's designation builds on (approach's room, hub's per-edge interfaces, frontline's face). What `ShapeEmitter.Body` returns. |
| `BodyEmitter` | Emits the **new terminal-free compounds** the vocabulary names but `ShapeEmitter` can't build, as `ShapeBody`: `SpineArms` (the branch family generalized to K arms — T/Π/F/E, capped at 3, arm placement a knob), `Ring`, `DoubleHole` (a ring + a docked U — holes equal or variant, the U slides), `P` (a ring on a longer bar — the loop slides), `TwoUOnI`. Each classifies back through `ClassifyBody` — the body mirror. Standalone (not docked or composed); the shared bodies the hub/frontline designations reuse. |
| `WoolBoxEmitter` | The **wool binding** over the emitter: stamps the terminal as a `wool-room` piece carrying the wool marker, wraps each piece with its slot and box label. |
| `SpawnBoxEmitter` | The **spawn binding** (the second box kind): the spawn's shape profile as data ({I, L}, small boxes) + `Fill`, terminal → a `spawn`-role room + marker, mapped into the growth frame. |
| `EmittedShape` | An **approach emission**: a `ShapeBody` finished by the approach designation — the terminal room rect + marker (with `Terrain`/`Vacancies` reading through to the body). What `ShapeEmitter.Emit` returns. |
| `EmittedApproach` | A **wool emission wrapped**: the terrain pieces + the wool room + the marker + the published vacancies, ready to place. |
| `EmittedSpawn` | A **spawn emission**: the spawn pieces + the `spawn`-role room + the marker + the **entry-run length** a wool box may dock along. |
| `ShapeVacancy` · `Vacancy` | **Published negative space** a fill leaves inside its box, exact by construction: `bay` (open toward one edge — claimable by a later box) · `notch` (a corner remainder) · `hole` (enclosed by the shape). `ShapeVacancy` is box-local; `Vacancy` is placed into the board frame. |
| `FillResult` | The **outcome of a fill** as a data channel: `Ok` (the emission + its vacancies) · `TooSmall` (the family's minimum box, so the caller resizes) · `NoFamilyFits` (the menu came up empty). |

## 6. The grown unit — the composed pieces

Composition produces one team's **grown unit** in a relative frame, which the symmetry then fans into every
orbit image. Pieces carry their labels here and drop them at assembly.

| Type | What it means |
|---|---|
| `GrownPiece` | **One rectangle** with its map-level **role**, its shape **slot**, and its **box** ownership — `(box, slot)` is the full label (`wool-a/entry`) the compose-side rules bind to. Labels ride every compose move and drop only at assembly. |
| `GrownUnit` | **One team's** pieces plus its objective placements (spawn, wools), in plan cell coordinates — what the grower produces for the composer to assemble. |
| `GrownSpawn` · `GrownWool` | An **objective marker**: which piece it sits on, its piece-relative offset, and (spawn) the facing toward the enemy. |

## 7. The plan — the authored artifact

The **plan** is the author-intent layer (§3): only what a machine cannot recover. Everything structural is
*derived* from it, never written back.

| Type | What it means |
|---|---|
| `PlanModel` | The whole **plan**: the pieces, their roles and heights, the deliberate voids, and the objective/spawn markers — the upstream artifact the whole pipeline compiles from. |
| `PlanPiece` | **One piece** in the plan: its rectangle (proxy cells), its role, its height. |
| `PlanRoles` | The **map-level piece roles** (the *role* taxonomy, distinct from slots): `piece` (anonymous) · `wool-room` · `spawn` · `buffer` · `connector`. `piece`/`wool-room`/`spawn` make terrain; `buffer`/`connector` are annotations. |
| `PlanZone` | A **build region** in the plan (the mid band, a bridge) and its declared **holes** — the negative-space the author asserts is deliberate. |
| `PlanPlacements` · `SpawnPlacement` · `WoolPlacement` · `IronPlacement` … | Where each **objective/spawn marker** sits (piece + offset + facing). |
| `PlanGlobals` · `PlanMeta` | The plan's **frame**: cell size, symmetry, player cap, surface height (globals); name/metadata (meta). |
| `PlanValidator` · `PlanFinding` · `PlanSeverity` | The **plan-level lint**: findings (errors vs lint) checkable with little or no geometry. |

## 8. The derived reads — reading structure back

**Derive** reads structure back out of geometry (the inverse of emit). Two derivers read *different things*
(§1.3): the **shape deriver** reads one wool box's family; the **board deriver** reads the whole board's
connectivity.

| Type | What it means |
|---|---|
| `ShapeClassifier` | The **shape deriver**: reads one box's terrain back to its `ShapeFamily` (width-independently); `ClassifyOpen` reads the corridor's bend as a `LaneRead`; `ClassifyBody` reads a terminal-free `ShapeBody` back to its `Compound` (voids · arms · bends). The emitter's mirror, on both the approach and body layers. |
| `LaneRead` | The **open corridor read**: `I / L / Z / Complex / Plaza / None` — the board-level bend of the lane a wool room caps (distinct from the wool-box *family*). |
| `SlotAssignment` | The **slot deriver**: re-derives every emitted piece's slot from topology alone (path order, adjacency, hole-edge geometry), closing the mirror at the slot level. |
| `ContactGraph` | The **board deriver, rect layer**: the connectivity primitives — every `Contact`, the build regions, the gap links, the islands. |
| `BuildRegion` · `GapLink` · `InterfaceSegment` · `FrontlineEdge` | The connectivity pieces a board carries: a **build region** (typed by what islands it links), a **gap link** (a void a build region spans, with its hop distance), an **interface segment** (a shared edge), a **frontline edge** (where fanned images meet — a derived edge attribute, not a piece). |
| `BoardDeriver` · `BoardStructure` | The **board deriver, raster layer**: islands (team/objective/neutral), hole classes (encased/gap/frontline/middle), build-zone kinds, the intra/self bridges, the wool lanes, and the mid form — the whole read of a board. |

## 9. The composer — the moves

The **composer** runs the pipeline: budget → grow one unit → carve the mid → optional cut → assemble → gate.

| Type | What it means |
|---|---|
| `Composer` | The **entry point** — composes a full `PlanModel` from a request, running the design-doc order and gating every attempt against the evaluator. |
| `TeamUnitGrower` | Grows **one team's unit** today (hub + spawn + wools + frontline + stones). Retires at G63 when the partitioner takes over. |
| `MidCarver` · `MidResult` · `MidStone` · `StoneRow` · `CrossingDesign` | The **mid**: the neutral band between frontlines and its stepping-stone islands — its form is `f(frontline)`, so it is structured, not carved from solid. |
| `IsolationCut` · `CutResult` | The **isolation-cut** fragmentation move: sever one marker piece behind a build bridge (a team's own `spawn↔wool` cut). **Out of the compose loop (G86)** — it cut a clean wool approach before fragmentation had slot-carving rules; kept intact, returns as a slot-aware fragment pass (cuts a `run`/`bar`, never a `room`/`entry`). |
| `SpawnWoolRooms` | The **terminal-room carve**: turn a lane's dead-end into a real role-bearing room (only `wool-lane-c` needs it now — the wool and spawn boxes emit their rooms pre-carved). |
| `Frame` | The **growth frame**: the `(u, v)` axis-normal coordinate frame a symmetry mode grows its unit in — `u` outward from the axis, `v` cross — so one grower serves every symmetry mode. |
| `ComposeGeometry` | The **fanned-separation rule**: pieces of different orbit images stay ≥ the minimum hop apart (team territories stay separate islands). |
| `ComposeRng` | The **deterministic RNG**: a fixed draw order makes the same request reproduce byte-for-byte. |
| `ComposedStages` | Everything **one compose attempt produced**, kept apart so tests gate each step. |
| `ClosureAnalysis` | The **closure-hole** read: where a frontline's recess seals into a rotation pocket (CT8). |

## 10. The evaluator — judging

The emitter can make anything; the maps' character is **what the evaluator refuses** (§7). `score = Σ
hard-penalty(violated well-formedness) + Σ w · envelope-distance(metric)`.

| Type | What it means |
|---|---|
| `LayoutEvaluator` | The **judge**: validates + scores a plan and returns the score with the **list of violated terms** (each citing a rule id) — the hard gate every composed plan must clear. |
| `Violation` · `TermScore` · `Evaluation` | A **failed term** (why, which rule, the subjects), a single term's **score**, and the whole **evaluation** of a plan. |
| `ILayoutTerm` · `TermKind` | A **scoring term** — a hard well-formedness rule or a soft envelope-distance metric — and its kind. Terms are additive; the set never has to be complete. **Terms read the derived board (`EvalContext`) only — never a shape/family name or a box/interface** (those are compose-internal and gone by evaluation): docking validity is a *compose-side* gate, not a term; the evaluator's hard terms (WL8, the corner law) verify the *symptom* on derived topology instead. |
| `Evidence…` (`EvidenceRect`/`Marker`/`Segment`/`Measure`) | The **evidence** a term attaches so a failure is legible on the board (the rectangle/marker/segment/number that shows *why*). |
| `SeedEnvelopes` | The **measured envelopes** the soft terms score distance against (from the seed corpus). |

## 11. Realize — the compile chain

The plan compiles one-way into two downstream artifacts (§2.1), each with one consumer:

| Type | What it means |
|---|---|
| `SketchLayout` | The **sketch** (`layout.json`): the realized geometry — polygons, béziers, per-anchor heights, layers — read by the rasterizer into a world. |
| `MapIntent` | The **intent** (`intent.json`): the concrete objectives — block coordinates, yaws, wool colours, monument wiring — read by the XML generator into `map.xml`. |
| `PlanCompiler` | The **compiler**: `plan → (sketch, intent)`. |

---

## The substrate (below it all)

Pure integer-grid geometry the whole stack reads cell topology through — no map concepts, referenced by
everything:

| Type | What it means |
|---|---|
| `Cells` | The **rectilinear cell-set primitives**: neighbours, flood fill, connected components, enclosed-void detection, reflex-corner (bend) counting, fold detection, the diagonal-pinch corner law, min run width. |
| `Symmetry` | The **orbit math**: reflect/rotate a point/rect, the orbit axes and order per symmetry mode — the one canonical copy every C# site routes through. |
| `Polygon` · `Skeleton` · `RectilinearUnion` · `RingRounding` · `CatmullRom` · … | The **sketch/realize geometry** helpers (polygon simplification, straight skeleton, unions, rounding, splines) the roughen and rasterize passes use. |
