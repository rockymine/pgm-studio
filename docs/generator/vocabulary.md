# Map generation — the vocabulary (every type, by concept)

This is the **living catalog of the types** the box-model generator introduces, each described as a
*map-generation concept* — what it **means**, not its fields or signature. It sits between two other things:
`model.md` **§1** defines the abstract *terms* (family, interface, width, budget…); `model.md`
**§12** is the *code map* (which file); **this doc names the types that embody the ideas**, in the order the
concepts appear in the pipeline, so a newcomer can read top-to-bottom and learn the vocabulary once.

**It is a living document.** When a task adds, renames, or retires a type, update the matching row here in the
same commit — the same discipline as the task board. A type without a plain-language meaning here is a type
that will confuse the next person. Planned-but-unbuilt types are listed *italicised* with their task id.

The pipeline these are ordered along (from `model.md` §2):

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

Before any piece is filled, the budget draws a **coarse partition of typed boxes** (§5). A box is a *bounding
envelope, not a fill target*: its contents must touch its edges and stay connected but need not fill it solid —
that is what lets one shape take many footprints inside a fixed envelope. Boxes exist only during composition;
no finished map carries them.

| Type | What it means |
|---|---|
| `Box` | A typed bounding envelope the partition allocates — a rectangle a fill lives inside — carrying its **footprint** (its rectangle) and its **land target** (how much walkable land it should hold). The two-currency budget's per-box half. |
| `BoxKind` | Which kind of box: **spawn · hub · wools · frontline · mid**. The kind decides the fill profile (what may fill it) and the box's role on the map. |
| `BoxRef` | A piece's **box ownership** — which box's fill it belongs to. With the piece's slot it forms the full label (`wool-a/entry`) every compose-side rule binds to. |
| `WoolBox` | The specific axis-aligned region **one** wool approach is emitted into — the wool box's own geometry, mouth (hub-side) at one edge, dead-end room deep inside. |

## 3. The interfaces — how boxes touch

An **interface** is always a shared **edge interval** — a position and a width — where two boxes meet (§1.5);
never a point. The **interface width** is the master variable of generation (§1.6): it sets connectivity,
classifies the joint, and gates the fill menu.

| Type | What it means |
|---|---|
| `BoxAbutment` | One shared **edge interval** — a box-local edge, an offset along it, and a width. The concrete "here two boxes touch, this wide". Named for what it *is*: `Interface` promised a C# language construct, and every docstring that had to explain the old name reached for "abutment" anyway. (B42) |
| `BoxJoint` | One **edge of the partition graph** (G63): the two boxes it connects, the `BoxAbutment` they touch along, and the `Grant` the consumer was given across it. |
| `BoxPartition` | The **constraint graph** a partition is (G63): the typed boxes (each an allocated footprint + its land-budget half) and the joints between them — what sampling produces once composition allocates boxes first and fills them second, replacing the sample-then-place shape record. **Boxes may overlap** (it allocates budgets, not exclusive area); a joint is only asserted where two footprints truly abut. `BoxPartition.Of` is the **derive-side mirror** — the partition a grown unit implies, so an allocator's partition round-trips through it. |
| `BoxEdgeInterface` · `EdgeInterval` | The **facts** about one box edge (the valid-edges data model, G41-B; intervals G93): its span (long/short) and its **intervals** — the per-piece stretches on the edge, ordered along it, each an `EdgeInterval` (start + length + the piece's slot, the room included), so two pieces on one edge are two disjoint intervals with the gap between them (the twin-face fact). It **observes, it does not judge** — whether the edge may *dock* is a rule (the G80 gate) over these facts, not a field here. Read off the shape, so *shape-relative*. (`Slots`/`TouchesRoom`/`HasTerrain` are convenience reads over the intervals.) |
| `EdgeSpan` | A box edge's **length class**: `Long` on the box's longer sides, `Short` on the shorter — an observational edge fact (the twin-interval reads distinguish by it). It no longer gates docking: every family, the clamp included, docks through a single mouth. |
| `SlotDockRole` | The **dock role** a slot plays at an edge (the docking law as data, G80): `DockingEdge` (an `entry` — where a host connects) · `NeverDock` (the wool `room` — a dock seals the goal) · `Internal` (a `run`/`bar`/`leg` — shape-internal corridor, neither offers nor forbids a dock). One tag per slot; the gate maps the edge's slots through it, scoped per `Designation`. |
| `Designation` · `DesignationMarks` | Which box kind's **designation** finishes a body (`model.md` §4, G95): `Approach` (wool/spawn — a `room` terminal rectangle plus the `entry` docking role), `Hub` (no terminal — the constraint source, publishing what each free run carries), `Frontline` (no terminal — one edge designated the face). `DesignationMarks` are the non-approach marks (`interface`, `face`) — the siblings of the approach's `entry`/`room`. It is the **designation, not the box kind**, that decides a mark's `SlotDockRole` (`DockingGate.Role(designation, slotOrMark)`) — wool and spawn are both `Approach`. The marks are constants the gate already maps, but **no emitter stamps them yet**: the hub publishes `EdgeOffer`s and the frontline returns its face edge directly. Stamping them is G88/G89; the approach table is unchanged. |
| `DockingGate` | The **compose-side gate** (G80): a dock is legal iff the edge lands on a `DockingEdge` slot and touches no `NeverDock` slot. Every family docks through a **single mouth** (the clamp too — its two legs meet the host on one edge, the wool clamped inside as a cut cell), so the verdict reads only the edge's slots — no family name. Not an `ILayoutTerm` — it runs where the box is placed, producing only legal docks (`DockRejection` names an illegal one). Validity is *shape-relative for free*: the slots are read off the shape, so an entry shift moves the edge and the verdict follows. |
| `EdgeOffer` | An **offer** (`model.md` §1.14) — the outward constraint a designation publishes: **where** a neighbour may attach (the edge interval, shape-relative so it moves with every knob), **at what width** (the `w2/w4/w6` rung the consumer reads as its `cw`), and **in which grouping**. Produced by the hub designation (one per free run — the constraint source) and the frontline designation (the face offer the mid consumes). The record serves **two roles carrying different quantities** — read one as a copy of the other and you will be wrong. As an *offer* its width is a **capacity**, derived from the length of the run it sits on; as a `BoxJoint.Grant` it is a **selection**, chosen per consumer kind (a wool takes the w2 wool lane, a spawn or frontline the map lane width). One run can carry two docks at two widths, which is exactly why a grant is not the offer travelling forward — and why the width a neighbour builds to is the one **its own joint** was granted. (B41) |
| `OfferGrouping` | How the intervals of one offer group may be consumed: `Several` — each interval takes its own consumer (a hub's four edges, the twin frontline's two tips) — or `Joint` — one consumer must span the whole group flush (FR6's wide face across both tips, the inter-tip recess preserved as a hole). Joint-vs-several *is* wide-vs-split frontline. |
| `ContactKind` | The raw **piece-to-piece contact** reading, straight off the terrain rectangles: `Land` (they merge into one walkable mass) · `Narrow` (a thinner walkable seam) · `Corner` (a bare point — never connects) · `Overlap` · `None`. |
| `Contact` | One classified contact between two named pieces (its kind, its border length). |

## 4. The shapes — what fills a box

A wool box is filled with an **approach shape**. Shape identity is its **turn count plus the wool's seating**,
read *width-independently* (§1.2, §5).

| Type | What it means |
|---|---|
| `ShapeFamily` | The **nine** wool-approach families, an escalation of one another: `Isolated · I · L · Z · Scythe · Clamp · U · H · Donut`. The one taxonomy both the emitter (build one) and the classifier (read one back) share. |
| `Compound` · `CompoundRead` | The **terminal-free** compound taxonomy (`model.md` §4): `Rectangle · SpineArms · Ring · DoubleHole · P · G · TwoUOnI`, read by topology alone (voids · arms · bends), no terminal. `G` is a ring + an L — one enclosed void plus a three-walled bay (`ClassifyBody` reads it as one void with a bay, distinct from Ring/P). The identity `BodyEmitter` builds and `ClassifyBody` reads back — the body-layer mirror. A compound plus a designation (a terminal) is an approach; `ShapeFamily` is that terminal-capped view. |
| `ApproachSlots` | The **shape-internal roles** (the *slots*), two layers (`model.md` §5): **structural slots** `run · bar · leg` (the rectangle's role in the compound, shared by every kind) + **designation marks** `entry · room` (the docking rect and the terminal, stamped by the approach), qualified `entry-run`/`room-run`/`entry-bar`/`room-bar` when a family has two. A slot is a **template position**, not a property of a rectangle — a scythe's `entry-run` and a donut's `leg` can be the same rectangle in different slots. Each family is an ordered template of these. |
| `RoomPlacement` | Where the terminal (wool/spawn room) sits relative to the last segment: `Inline` (caps it straight) or `SideTuck` (ducks off its side — still reads as the straight family). |
| `BoxEdge` | A box-local edge (`Top`/`Bottom`/`Left`/`Right`) — used to name a family's **mouth** (the edge its entry docks a host through). |
| `MouthOrient` | The **orientation** step for the terminal-capped kinds (`model.md` §5): a shape is always emitted mouth-up, and this turns it onto whichever box edge it actually docks — Top the identity, Bottom a vertical mirror, Left/Right a quarter turn. Piece rects, the marker's offset inside its rotated room, and every published vacancy mouth follow the same transform, so per-family geometry is written once instead of four times. |
| `BodyOrient` | The body twin of `MouthOrient`, for the terminal-free kinds: a body is emitted spine-up and this moves that reference edge to the one it docks (the hub's front-flip, the frontline's spine dock), transposing the box on a quarter turn. Same transform, same followers — piece and vacancy rects, and vacancy mouths. |
| `NegativeSpaceKind` | The **wall-count escalation of negative space**: `Notch` (wrapped by two edges — the L's corner) · `Bay` (three — the staple's recess, the hook's bay) · `Hole` (enclosed — the ring's void) · `Open` (at most one wall — plain outside, not a feature). The classes that decide which voids are publishable and which edges remain offerable. |
| `NegativeSpace` · `ClassifiedEdge` | One connected negative space read off a body (its kind, cells, wall count, parts, and its **own compound `Form`** — the void read as a body: the uneven branch's bay is a `SpineArms(2)`), and one maximal straight boundary run classified on **three independent axes**: the space it **faces**, its **owner** (`Terminal` — the room's own wall), and the **guard** (`Guarded` — inside the room's clearance margin). Runs split where any axis changes; the free offerable surface = `Open` ∧ ¬`Terminal` ∧ ¬`Guarded`. Ownership/guard are facts; the never-attach verdicts over them are the docking gate's rules. |
| `NegativeSpacePart` | One rectangle of a space's **slab decomposition**, classed by its **own** body walls (siblings count as open) — the layer that lets rules reach an inset feature: the uneven branch's six-edge bay is a U whose mouth-spanning bar part reads notch-grade and borders the short arm's tip, while the space-level bay class stays correct. Under a clearance read a part also splits against the room's margin, the covered piece `Guarded` (non-publishable). |
| `SpaceMouth` | One **mouth** of a negative space — the open side, the interval along it (position + width in cells), and the `wN` **width class** it tapers to (w2 chokepoint · w4 unstable · w6 multi-access; ties small, the fill-menu convention). One per open direction: bay 1, notch 2, hole 0 — the uniform derive-side twin of the published vacancy's `BoxAbutment` mouth (which exists for bays only). What "may dock through the opening" rules read. |
| `PublishPolicy` · `PublishVerdict` | The **publish policy** as a `DockingGate`-style table: which negative spaces a shape **offers onward** — terminal-capped shapes veto their bays and holes and allow their notches (proximity is the clearance guard's job); terminal-free bodies allow everything (the hole's size gate pending). The publishable region = the front, unguarded parts (a hole offers all its parts). **Publishing is an offer, never a fill** — a later pipeline step claims it once the base is built (a third wool in a bare U's bay), or nothing does. |
| `BodyEdges` | The **edge-taxonomy reader**: classifies any rectangle set's negative spaces and boundary edges from geometry alone — the derive-side, shape-relative generalization of the emit-time `ShapeVacancy` publication (works on emissions, terminal-free compounds, future hub bodies). Rendered by `tools/compose/edge-gallery.cs`. |

## 5. The fill — emitting a shape into a box

**Emit** fills one box with one base shape (forward); **fill** is the profile-gated entry point over it. "No
shape fits" is a *signal*, answered by changing the box — never a crash.

| Type | What it means |
|---|---|
| `FillProfiles` | The **per-`BoxKind` profile as data**: which families a kind admits at a given width (wool = the width menu; spawn = {I, L}), plus the footprint gate (a family's minimum box must hold the footprint). The single source the menu and the footprint budget read. |
| `FillMenu` · `FillMenuRow` | The **width→fill production rule** (§8): the `w2/w4/w6` table saying which families an interface width makes legal. `FillProfiles` composes this for the wool box. |
| `BoxFiller` | The **one profile-gated fill entry point** over a positioned `Box`: validate/pick a legal family that fits, emit it, and report the **land** the fill spent against the box's land target (the two-currency balance). The spine the partitioner (G63) drives. |
| `ShapeEmitter` | The **pure family geometry** in two stages: `Body` builds the terminal-free compound, a **designation** finishes it (`Emit` = the approach designation, stamping the terminal room + marker). No roles, no ids, no plan types. |
| `ShapeBody` | The **terminal-free compound**: structural-slotted rectangles + vacancies, with no terminal, marker, or id — the shared layer every box kind's designation builds on (approach's room, hub's per-edge interfaces, frontline's face). What `ShapeEmitter.Body` returns. |
| `BodyEmitter` | Emits the **new terminal-free compounds** the vocabulary names but `ShapeEmitter` can't build, as `ShapeBody`: `SpineArms` (the branch family generalized to K arms — T/Π/F/E, capped at 3, arm placement a knob), `Ring`, `DoubleHole` (a ring + a docked U — holes equal or variant, the U slides), `P` (a ring on a longer bar — the loop slides), `G` (a ring + an L — the ring's hole plus a frontline-sealed bay, the asymmetric cousin of DoubleHole), `TwoUOnI`. Each classifies back through `ClassifyBody` — the body mirror. Standalone (not docked or composed); the shared bodies the hub/frontline designations reuse. |
| `RingWalls` | The **four wall widths of a ring**, in cells (top/right/bottom/left) — the widening knob every ring-bodied form shares (`Ring`, the `P`'s loop, the `G`'s ring, the `DoubleHole`'s, the donut's). `Uniform(cw)` is the equal-walled ring; a wider side is how an author says more play flows through there. Widening **spends the box's slack**: the walls thicken and the hole narrows, the box does not grow. Emitters bound it by geometry only (every wall ≥ 2, walls must leave a hole); how far the widths may differ is the **sampling law** the composer owns — `TeamUnitAllocator.ChooseHubWalls` widens **one** side, drawn evenly from the four, capped so the widest is never more than twice the narrowest (the frontline arm law's spread), and only where the hole keeps a corridor's width. Attached parts keep a plain `cw` (the `P`'s bar overhang, the `G`'s L-upright, the `DoubleHole`'s U), which is why the widened overloads take both. `Producibility` collects the admissible vectors by *running* that sampler over seeds, so the search accepts exactly what the composer can draw — a ring widened past the cap is legal geometry and still not producible. |
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
| `GrownUnit` | **One team's** pieces plus its objective placements (spawn, wools), in plan cell coordinates — what the allocate→fill pipeline produces for the composer to assemble. |
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
| `PlanBox` · `PlanBoxKinds` · `PlanBoxes` | The **authored box annotation**: a typed envelope (`spawn`/`hub`/`wool`/`frontline`/`mid`) grouping the pieces that realize one part of the partition, the kind vocabulary, and the membership rule (named members, else containment). Authoring-only — ignored by the compiler, validator and derivers, like the tracing `reference`. `PlanBoxAnnotation` writes a composed unit's partition into it when a generated board is kept. |
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
| `StructureSummary` · `StructureNames` | The **unit's structural read as one small fact**: the sorted wool approach families (`ShapeClassifier.Classify` per wool box), the hub body form, and the frontline form or `none` (`ClassifyBody` per hub/frontline box). Read off the labeled grown unit (box + slot), never a finished map, so it works uniformly on a bare unit and needs no labels stored back. `StructureNames` maps it to lowercase display/filter tokens (`donut`, `ring`, `twin`, `bar`, `none`…); `Canonical()` is the stable bucket key (`wools:donut,l\|hub:ring\|front:none`). The **filter fact** for the browse sieve (G117) and the bucket key for verdicts (G118) / duels (G120). |

## 9. The composer — the moves

The **composer** runs the pipeline: budget → grow one unit → carve the mid → optional cut → assemble → gate.

| Type | What it means |
|---|---|
| `ComposeRequest` | The **input**: players per team (5–32), team count (2 or 4), symmetry, seed, cell size. Validated at construction — nothing else is given, and there is no geometry input (`model.md` §3.1). |
| `Composer` | The **entry point** — composes a full `PlanModel` from a request, running the design-doc order and gating every attempt against the evaluator. |
| `TeamUnitAllocator` | The **allocate half's entry point**: from the budget alone it sizes and places the hub — the only box in the unit ever given absolute coordinates — chooses the hub's **form**, and hands the rest to the three types below. Emits a `BoxPartition` + the spawn facing (`model.md` §6). Everything after the hub is positioned *relative to it*. (B42 split the other three out.) |
| `UnitTuning` | The **numbers the structure turns on**: the size ladders (where a frontline becomes affordable, where the lane widens from 2 cells to 3, where a unit becomes a full team), the shape mix (how often a wool is bent, a donut, a staple), the clearances the seat step builds to, and the frame-independent **placement plan** they feed — the wool count and which hub side each neighbour takes. Every value here is a frozen taste judgement; none is measured at runtime. |
| `UnitRequests` · `NeighbourRequest` | **What the unit needs hung off its hub, decided before any position exists.** A `NeighbourRequest` is a *request, not a rectangle*: which hub side, what kind of box, and two extents — its **depth** (outward from the hub edge it will dock) and its **along** (parallel to that edge). Nothing in it is a coordinate. A wool's request also carries the shape family rolled for it, because the family is what set those two extents. The along-extent is checked against the hub's edge length — and the *overhang* families are deliberately exempt from that check, which is the whole permission for a box to be born wider than the edge it docks. |
| `DockStyle` | **How a neighbour docks its host** — derived from the request, never sampled; the style follows from the family roll that already happened. `FullMouth`: nothing is known about where the shape's entries are, so the **whole** along-extent must sit inside one free run and every entry then lands wherever they are (which is why the dual-entry staples dock here — an overhang would strand the second entry). `Overhang`: exactly one entry and the emitter can say where, so only that interval must land and the body may hang past the run. `ContactPatch`: a frontline is a face, not a corridor, so it has no entry at all — what must hold is that *every* stretch where it meets a run is at least a lane wide. (B40) |
| `UnitSeating` · `FullMouthDock` | **Turning demands into positions.** Seating's entire output is one integer per neighbour — the **seat**, an offset in the hub's edge-local coordinates at which the neighbour's along-extent begins — from which the rectangle follows mechanically. A seat may be **negative**, which is how a box comes to hang past the hub's corner over empty space. `FullMouthDock` is what the shape-agnostic path returns: the placed rect, the abutment, the request *as it ended up* (a wool whose mouth no run holds is demoted to the compact `I` inside), and the flush seat handed to `FrontGuard` when no backward position was free. |
| `SeatGeometry` | The **rect and edge arithmetic** every seat reads: `NeighbourRect` — the one and only constructor of a neighbour's rectangle, stepping outward from a hub edge by the request's depth and running its along parallel to it — plus projecting already-seated boxes onto the edge being seated, the rectilinear clearance tests that keep neighbours a lane apart, and the joint each dock records. |
| `UnitPlan` · `UnitSide` | The frame-independent **placement plan**: which hub side each neighbour sits on — `Front` (toward the axis), `Back`, `Left`, `Right`. The decision layer under the allocator's geometry; the spawn takes the back or a lateral side, the wools go around it, the front is the frontline's. |
| `TeamUnitFiller` · `FilledUnit` | The **fill half**: fills an allocated partition **hub-first**, each neighbour consuming the offer on its own joint as its corridor width, and assembles the pieces + placements. `FilledUnit` carries the `GrownUnit` plus the frontline face offers the mid consumes (`model.md` §5). |
| `FrontGuard` · `FlushSeat` | The **no-frontline seat post-pass**: a lateral seat left flush with the empty front is slid backward, relocated, or dropped — deterministically, no draws — so lanes do not spike across the no-man's-land in front of the hub. |
| `UnitPlacement` | Re-anchors the finished unit on its **face** before the band is derived: the allocator anchors on the hub, but the face is what the mid docks. |
| `ComposeDescriptor` · `ComposerFingerprint` · `ComposerVersion` | The **reproducibility set**: what a compose was asked for, the recorded structural fingerprint of what it produced, and the version stamp that must be bumped whenever composer geometry moves (a moved geometry with an unbumped version silently invalidates the reproduction gate). |
| `MidCarver` · `MidResult` · `MidStone` · `CrossingDesign` | The **mid**: the neutral build band between frontlines — its form is `f(frontline)`, so it is structured, not carved from solid. Band-only today (flush, hull-exact, stone-free); stones / centre islands / the split band re-enter as richer `CrossingDesign` forms. |
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
| `Producibility` · `PlanProducibility` · `BoxProducibility` · `ProducibleAs` · `NearestMiss` · `ProducibilityFinding` | **Could the composer have produced this?** — the emit↔derive mirror as an authoring answer, and the question the evaluator does not ask (a plan scores 0 and can still be unbuildable). Answers by **search, not inverse**: enumerates the tuples the declared menus admit, calls the real emitters, compares masks. Per box it reports the derived `Identity` (a hint, never a verdict), the tuple that reproduces it (`ProducibleAs`) or the `NearestMiss` and its differing cells; `PlanProducibility` adds the **unit-level** findings (the arrangement rules — parallel fronts, the frontline face demand, seat separation). Each `ProducibilityFinding` cites a rule id or the task that would unblock it. |
| `FillRejection` | **Why** a box fill was refused — the one directed-reason vocabulary every box kind reports through (`TooSmall` · `FormDoesNotFit` · `NotOnMenu` · `IllegalDock` · `UnsupportedKnobs`). Carries no form/family: the caller passed that in. `FillResult.Rejection` projects the approach channel's own cases onto it. |

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
| `CellRect` | The **plan rectangle** — an origin plus two *exclusive* extents, in plan cells (`X`, `Z`, `Width`, `Height`, with `MaxX`/`MaxZ` computed). Every box, piece, zone and vacancy is one. Distinct from `Rect`, the authoring type, and the split is the point: `Rect` is **world blocks**, fractional, a min/max corner pair; `CellRect` is **plan cells**, integer, origin + extent. A `JsonConverter` keeps the `plan.json` wire form a four-element array, so the representation changed and the serialized form did not. (B37) |
| `Cells` | The **rectilinear cell-set primitives**: neighbours, flood fill, connected components, enclosed-void detection, reflex-corner (bend) counting, fold detection, the diagonal-pinch corner law, min run width. |
| `Symmetry` | The **orbit math**: reflect/rotate a point/rect, the orbit axes and order per symmetry mode — the one canonical copy every C# site routes through. |
| `Polygon` · `Skeleton` · `RectilinearUnion` · `RingRounding` · `CatmullRom` · … | The **sketch/realize geometry** helpers (polygon simplification, straight skeleton, unions, rounding, splines) the roughen and rasterize passes use. |
