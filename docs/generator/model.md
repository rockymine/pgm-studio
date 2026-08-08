# Map generation — the canonical model, terminology, and pipeline

This is the **single source of truth** for how pgm-studio generates map layouts: the vocabulary,
the pipeline and its order, and how every part fits the next. Every word defined here has **exactly
one meaning**; where a term appears elsewhere it carries this meaning. When another doc and this one
disagree, this one governs.

**What this document owns:** the pipeline, the request and the budget that sizes everything, the plan,
the shape model (bodies, designations, and the approach families), composition, the board deriver, and
the rule and scoring model. Terms are **defined where they are first used** rather than collected in a
glossary — a definition sitting next to the mechanism it describes is one that cannot quietly stop
matching it. **What it defers:**

| Companion | Owns |
|---|---|
| `rules.md` | The rule law and every number (widths, depths, hop counts, heights, the CT / SP / WL / LN / HB / FR / MD / BZ / EL ids). |
| `seed-stats.md` · `seed-envelopes.md` | The measured envelopes the soft evaluator terms score against. |
| `evaluator.md` | The detailed deriver-measurables and evaluator-metric catalogue. |
| `vocabulary.md` | The **living type catalog** — every type as a map concept, by pipeline order. This document defines the terms where they are used and §8 is the code map; this names the types that embody them. Extend it in the same commit a task adds/renames/retires a type. |
| `audit.md` | The standing measured record of where the implementation and this model **disagree** — the evidence behind the open G-tasks. |
| `ideas.md` | The G-track idea pool: open work not on the board. |
| `../contracts/plan-editor.md` | The field-level `*.plan.json` schema and the editor UI (the Plan tool). |

**The live twin.** `tools/compose/showcase.cs` renders this same model as one self-contained HTML
page with **every figure emitted by the real generator** (`dotnet run tools/compose/showcase.cs`).
Where the prose here and the showcase disagree, suspect the prose: the showcase cannot drift,
because its figures are built by the code being described.

---

## 1. The pipeline

Generation turns a handful of numbers into a playable map, and it does so in one direction. A request
becomes a budget, the budget becomes a partition of empty rectangles, the rectangles are filled with
terrain, the result is judged, and only then does it become a plan a world can be built from. Nothing
later reopens what something earlier settled — which is what makes the whole thing explicable, since
any property of the output can be traced to the one step that fixed it.

```
request → budget → allocate (boxes + joints) → fill (hub-first) → carve the mid
        → assemble → derive → gate ──accept──► plan → realize
                               └────reject───► resample
```

Five verbs name the stages, and they are worth keeping apart because one of them is routinely
overloaded. **Never say "generate" for the whole thing**: it has been used both for the entire
pipeline and for the narrow step that turns intent into `map.xml`, and a sentence using it says
nothing about which.

| Verb | Means | Where it lives |
|---|---|---|
| **emit** | Fill one box with one base shape (forward). | `ShapeEmitter` + the per-kind bindings |
| **derive** | Read structure back out of geometry (inverse). | `ShapeClassifier`, `ContactGraph` + `BoardDeriver` |
| **compose** | Build the plan: budget → allocate → fill → carve → assemble. | `Composer` |
| **evaluate** | Validate and score a plan into a score plus violations. | the evaluator |
| **realize** | Compile the plan to sketch and intent, then export. | the seed pipeline |

`emit` and `derive` are a **forward/inverse pair**: composition emits, verification derives, and the
two are required to agree. That loop is the shape model's correctness test and is explained where it
is used (§4).

Two commitments shape everything downstream. Generation runs **from the hub outward** in a relative
frame and embeds into world coordinates late, so every box but the hub is positioned against its
neighbour rather than against the map. And composition is **allocate-then-fill**: every footprint is
placed before any terrain exists, and filling never grows a box outward to accommodate what is being
put in it.

The steps, in order. A **request** is players per team, team count, symmetry and a seed — no geometry
(§2). The **budget** turns the player count into land and footprint targets and the board's extent
(§2). **Allocation** draws typed box footprints and seats each on the hub's real free surface,
producing boxes and the joints between them (§5). **Filling** emits the hub first as the constraint
source, and each neighbour consumes the offer on its own joint (§4, §5). The **carve** derives the mid
band from the frontline faces (§5). **Assembly** turns labeled pieces into a plan, and the labels drop
here (§3). What comes next never looks at the plan directly: **derivation** reads it back as terrain
and works out what the board actually is — islands, what links them, which voids are enclosed, where
each front runs (§6). The **gate** judges *that* reading against the evaluator's hard terms, and a
rejection resamples the whole attempt — sixty are allowed — so "nothing fits" is a signal rather than
a crash (§7). **Realize** compiles the accepted plan and exports it.

One step in the model is absent from the loop. **Fragment** — converting land into build zones for
isolation cuts and stepping stones, conserving footprint while spending land — is how the two
currencies are meant to trade against each other (§2). It is not in the shipped composer: today the
only build region on a generated map is the mid band.

### 1.1 Realize — the compile chain

The plan is the **upstream artifact**. It compiles one way into two downstream ones, each with exactly
one consumer:

```
plan.json ──compile──► layout.json (SketchLayout) ──rasterize──► world
        └──compile──► intent.json  (MapIntent)     ──generate───► map.xml
```

| Artifact | Holds | Read by |
|---|---|---|
| **plan** | roles, interfaces, isolation, elevation transitions — the meaning | the composer / evaluator |
| **sketch** (`layout.json`) | realized geometry: polygons, béziers, per-anchor heights, layers | the rasterizer |
| **intent** (`intent.json`) | concrete objectives: block coords, yaws, wool colours, monument wiring | the XML generator |

Sync is one-way while the staged loop runs: edit the plan, recompile, and whatever realize adds is
re-rolled. Once an author takes the sketch into the editor for hand work that stops — the plan freezes
as provenance and sketch and intent become the working artifacts. Recovering plan meaning back out of
edited geometry is deliberately out of scope, for the same reason a finished map's shapes cannot be
classified back to the families that built them: the moves compose in one direction only.

---

## 2. The request, and everything it sizes

A compose is given no geometry. It is given four decisions and a seed, and every rectangle on the
finished board is a consequence of them. That is the strongest claim the generator makes, and this
section is the chain that has to hold it up: from a player count to a land budget, from land to the
board's extent, from extent to how many objectives a team gets and how wide its corridors run, and
from there to the widths that decide what may be built where.

### 2.1 What a compose is given

| Parameter | Range | Means |
|---|---|---|
| `playersPerTeam` | clamped **5–32** | the only size input — the land budget and every structural ladder derive from it |
| `teams` | **2** or **4** | 4 teams force `rot_90` |
| `symmetry` | `rot_180` · `mirror_x` · `mirror_z` (2 teams) · `rot_90` (4) | which orbit fans the authored unit; defaults to `rot_180` / `rot_90` |
| `seed` | any `ulong` | drives **every** draw — the same request reproduces the same plan byte-for-byte |
| `cell` | default **5** blocks | the proxy-cell grid scale |

A bad combination throws where it is made rather than failing deep inside generation, so an
impossible request is never half-composed.

The seed's guarantee is stronger than it looks, and it constrains how the generator may be changed.
**Sampling order is part of the contract**: draws come off the generator in one fixed sequence, so
inserting a draw anywhere re-rolls everything downstream of it. A change to geometry is therefore a
change to every seed's output, which is why it means a composer version bump and a re-recorded
fingerprint. The generator behind it is a small deterministic one chosen for exactly this reason —
never the platform's, whose algorithm carries no stability guarantee and could silently change what a
seed means between runtimes. No wall clock and no identifiers enter it: the same seed produces the
same sequence, permanently.

### 2.2 From players to a budget

Everything is sized once, before any geometry exists, from the player count alone.

**Land per player** is a piecewise-linear interpolation over corpus anchors — five players want about
sixty-five square blocks each, rising to a hundred and eighty-five at thirty-two — so a team's land
budget is its player count times that. This is the **land** currency, and it counts terrain area only.

**The board's extent** comes from land plus a sampled **coverage ratio**, the fraction of the fanned
board that is actually walkable. The corpus measures it between roughly 0.28 and 0.42, and dividing
total land by it gives the fanned area. A four-team board is square with its side clamped to a
sensible range; a two-team board additionally samples an aspect ratio between one and three, clamping
width and length separately, which is what makes some maps long corridors and others broad arenas.

**The unit bounds** follow: the authored unit takes half the doubled axis, less a margin between its
frontmost piece and the symmetry axis.

The budget is **two currencies that must both balance**, and the distinction between them is what
makes fragmentation possible at all. **Land** is walkable terrain area, set by the player count and
spent by every emitted piece. **Footprint** is total box area — terrain, build and gap together — set
by the partition and fixed once a box is placed. A build zone costs footprint but not land, and that
asymmetry is the whole mechanism: converting a terrain piece into a build zone keeps the size and
drops the land, so fragmentation **conserves footprint and spends land**. Nothing is ever removed;
things are replaced. The mid is the same model inverted — footprint-rich and land-poor, since its
purpose *is* the crossing — and needs no special budget, only a low land target.

The two balance at two levels at once: globally, where total land answers to the player count and
total footprint to the map size, and per box under symmetry. Every fragmentation cut spends land
globally while buying difficulty locally, so the budget and the gameplay knob move together rather
than being tuned against each other.

### 2.3 The ladders, and the weights beside them

Some structure changes discontinuously with the budget. How many objectives a team gets, whether it
has a frontline at all, and how wide its corridors run are not interpolated — they are thresholds,
and crossing one changes the map's shape:

| Ladder | Effect |
|---|---|
| land > 2500 | the map-wide lane width is 3 cells rather than 2 |
| land < 800 | no frontline — there is no budget for one, so the hub fronts the mid directly |
| land < 600 | a single wool; a tiny board cannot hold two |
| players ≥ 16 | a full team: 2–3 wools rather than 1–2 |

These are what the allocator turns into requests: the wool count fixes how many neighbours must be
seated, the frontline threshold decides whether a whole box kind exists, and the lane width is the
figure every corridor and every clearance test is measured in.

Beside the ladders sit roughly a dozen **sampling weights** — how often a wool is bent rather than
straight, how often a bent wool is a donut, how often a big square hub takes the ring, how often the
frontline spans the hub's full front. These steer the output's character more than any other numbers
in the generator, and most of them answer to no law: they were tuned rather than derived. Which are
grounded in the corpus and which are invented is measured in `audit.md`, and the honest position is
that this is the least principled part of the model.

### 2.4 Width, the master variable

Four different quantities get called width, and a rule that conflates them reads as contradictory.
The **interface width** is the width of the interval where two boxes touch. The **corridor width** is
what a shape is built and measured at. The **reported width** is what the classifier measured back —
an output, never an input. And the **attachment width** is the entry piece's own width, which may
exceed the lane it feeds.

Two modes matter more than the four names. **Generation-width** is the grammar: it gates what may be
picked and sets how things connect. **Read-width** is identity, and a family is its turn count read
width-free. Width chooses which family is *legal* and how it *joins*; it never changes what a shape
*is*.

The reference frame is fixed: a **cell** is 5 blocks, a **lane** is 2 cells, and `wN` means N cells —
so `w2` is one lane at 10 blocks, `w4` two lanes, `w6` three. Widths are not strictly quantized; a
touch of 15 or 25 blocks is valid and tapers toward the nearest rung.

The interface width is called the master variable because it does **three things at once**. It **sets
connectivity**: a one-lane touch is a single funnel, a chokepoint, while wider touches admit parallel
or split flow. It **classifies the joint**: a touch of about a lane or less continues a lane and reads
as a bridge, three lanes or more is an area and reads as a hub, and two lanes is the unstable middle
that has to resolve one way or the other. And it **gates the fill menu** — what may be built behind
that touch at all:

| touch | lanes | reads as | legal fills |
|---|---|---|---|
| **w2 (10)** | 1 | chokepoint | one I / L / Z lane; or a pure drain |
| **w4 (20)** | 2 | too wide to stay straight | 10 terrain + 10 build-lane; or a 20 stub that twists to L/I |
| **w6 (30)** | 3 | multi-access | two 10-strands with a hole; terrain-build-terrain; or a funnel splitting into a hole with two approaches |

The w4 and w6 rows resolve into multi-shape patterns the emitters cannot yet build. They are written
down anyway, so the table does not pretend a wide touch is merely a wide lane.

This is also why a wool box carries **two** widths rather than one. There is the width where it docks
its host, which is the interface width above, and the width of the lane running to the wool, which
stays simple. A wide entry tapers or splits into that narrow lane instead of dragging its width along
behind it, which is why the emitter keeps attachment width separate from corridor width. A lane is not
an approach: the lane is the corridor, the approach is the whole shape it belongs to.

---

## 3. The plan

The plan is where composition stops and everything else begins. It is the file a map exists as
between being generated and being built, the thing an author edits by hand, and the only artifact the
rest of the system agrees on. What it holds is therefore a deliberate choice rather than a dump of
state: **only what a machine cannot recover**.

Coordinates in it are proxy cells on the five-block grid — a mini-layout whose real scale is applied
at realize — measured relative to the symmetry centre.

Three categories exist, and the boundaries between them are the point.

**Authored** is the irreducible part, everything a deriver could not work out: the piece rectangles;
the role each piece carries, from the closed set below; the objective and spawn markers; the
deliberate voids, where a `buffer` piece or a zone
hole is an author asserting *I meant this emptiness*; per-piece height at full block resolution; and
the override channels — `cliffs` and `walls` — which exist to overrule what a deriver would otherwise
infer. Those last two are written by no generator today: the elevation pass that would fill them is
not built, and they stand as schema waiting for it.

The authored roles are worth naming exactly, because a second taxonomy sits next to them and the two
must never be mixed. A **role** is a *map-level* label on a piece in the plan, and the authored set is
closed: `piece` — anonymous, the default — plus `wool-room`, `spawn` and `buffer`. The first three are
**generating** roles that make terrain; `buffer` is an **annotation** role that marks something
informational and produces no terrain, no graph effect and no export. Everything else that sounds like a
role — frontline, hub, lane, mid, connector — is **derived and never authored**. A **slot**,
by contrast, is a *shape-internal* label naming a rectangle's position inside one approach, and it
lives only during composition. One is what a piece *is on the map*; the other is what it *does inside
its shape*.

**Derived** is everything structural: islands, the frontline, the hub, lanes, the mid, contacts, void
topology, build-zone kinds, and a wool's approach family. None of it is written back. It is recomputed
from the authored part whenever the plan changes, which is what keeps a hand edit from having to
maintain a dozen consistent secondary facts.

**Compose-internal** is a third category that is neither: the slot labels. They exist on generated
pieces during composition, drive every compose-side rule, and are dropped at assembly. A plan on disk
has no slots, and no deriver recovers them — a shape's slot assignment is knowledge the emitter had
and the file deliberately does not carry.

A small set of **invariants** is checkable on the file alone, with no geometry reasoning: every wool
reachable from every capturing team's spawn across land and gap interfaces; no path to a wool running
through a spawn piece; at least one gap on every inter-team path; interface widths at or above the
corridor minimum; and spawns kept some distance from the nearest frontline interface. These are what
make a hand-edited plan checkable before anything tries to build it.

The field-level schema and the editor that writes it are in `../contracts/plan-editor.md`.

---

## 4. The shape model — bodies, designations, and the piece vocabulary

Everything the generator puts inside a box is built from one atom, the rectangle, by two operations:
recombining rectangles into a **body**, and finishing a body into the thing a particular kind of box
needs. Holding those apart is what keeps the vocabulary small — a handful of bodies serve every box
kind on the map, and the differences between a wool's terrain and a hub's are added afterwards.

This section works up from the atom. First the bodies and the empty space they leave, since both are
read off geometry alone and neither knows what a map is. Then what an emitter does with a body, what
each kind of box adds to it, and which bodies each kind is allowed. Then the wool approach in full,
because it is the elaborate case and the one whose pieces carry labels. Last, reading a finished shape
back, which is how all of it is checked.

### 4.1 Every body is one rectangle, recombined

The atom is a single rectangle. Every other body is that atom recombined: each step in the escalation
adds a rectangle and earns exactly one new feature — a branch, an enclosed void, a second void. The
progression is not a filing system imposed afterwards; it is the order in which features become
available, and a body possesses every feature of the steps below it.

Every figure in this section is drawn the same way: `t` is a cell the shape occupies, `.` a cell it
does not, and `w` the wool where a figure has one. The body layer knows only the first two — what an
empty cell *means*, void a build zone may later span or simply outside, is decided later.

```
Rectangle              SpineArms(1)      SpineArms(2)      SpineArms(3)
                       — one branch      — two branches    — three
  tttttt               t.....            t....t            t..t..t
  tttttt               t.....            t....t            t..t..t
                       tttttt            tttttt            ttttttt
  no feature           reads L or T      reads U or Π      reads E
```

The branch row is **one** body, not three. A spine plus K perpendicular arms is a single form whose
letter is a placement-read of where the arms happen to sit, and that letter drifts as an arm slides
along the spine without the body changing at all. Ell, staple and comb are one thing wearing three
names. The arm count is the identity and it is capped at three.

Enclosing a void is the next feature, and the three one-void bodies differ only in what sits beside
the loop:

```
Ring                   P                      G
— one void             — void + overhang      — void + open bay

  ttttttt              ttttt..                ttttt.t
  t.....t              t...t..                t...t.t
  t.....t              t...t..                t...ttt
  ttttttt              ttttttt                ttttt..

  four bars around     the bottom bar runs    a ring with an L on its
  one enclosed void    past the loop, so      edge: the ring's void plus
                       the loop slides        a three-walled recess a
                       along it               docking frontline can seal
```

Two voids is the last step, and the two forms that reach it are told apart by what lies between the
holes — a solid wall or an open channel:

```
DoubleHole                        TwoUOnI
— two voids, solid wall between   — two voids, open channel between

  ttttttttt                         ttt.ttt
  t...t...t                         t.t.t.t
  t...t...t                         t.t.t.t
  ttttttttt                         ttttttt
```

These seven are `Compound` — the body taxonomy `BodyEmitter` builds and `ClassifyBody` reads back:

| Body | Feature it adds | Told apart by |
|---|---|---|
| `Rectangle` | — | the bounding box is solid |
| `SpineArms` | a branch (K = 1…3) | the arm count; where the arms sit is a knob |
| `Ring` | an enclosed void | one void, no overhang and no bay |
| `P` | a void with an overhang | one void, the loop's bar running past it |
| `G` | a void beside an open bay | one void plus a three-walled bay |
| `DoubleHole` | a second void | two voids, a solid wall between them |
| `TwoUOnI` | a second void | two voids, an open channel between them |

The **set of bodies is open, the set of names is not**. A custom build may join rectangles into
something no name covers — a staircase, or a hook doubling back on itself — and the classifier reads
it without a new special case, because it reads topology rather than matching a catalog. That is why
`Compound` carries no zig and no hook entry: those two features are named further up, where they
decide the Z and Scythe wool approaches, and the body layer sees only bends it does not need to count.

Width sits orthogonal to all of this. The interface width reads a body as a corridor or as an area; it
never reads it as a different body. A thick leg is a wide spot, not a new form.

### 4.2 What a body leaves empty

A body's shape is equally the shape of the emptiness around and inside it, and that emptiness is where
neighbours attach. The connected empty regions escalate by **wall count** — how many of the four axis
directions the body walls the region from:

```
notch — 2 walls        bay — 3 walls          hole — enclosed, 4

  tt..                   tt..tt                 tttttt
  tt..                   tt..tt                 tt..tt
  tttt                   tttttt                 tt..tt
  tttt                                          tttttt

  the corner an L        the staple's recess,   the ring's void
  wraps                  the scythe's fold-bay
```

A region walled from at most one direction is plain outside space along a flat side, and is not a
feature of the shape at all.

Negative space is known twice over, from both ends. An emitter **declares** the spaces it means to
leave: every emission carries `Vacancies`, a list of `ShapeVacancy` giving each space's kind, its
rectangle, the edge its mouth opens toward, and the slots of the pieces walling it. `BodyEdges` then
**reads** the same spaces back off the finished rectangles, and describes itself as the derive-side twin
of that emit-time record. The two must agree, for the same reason the family and the body readings must.

The derive side is the richer of the two, because it is a **fact read off finished geometry**, total
over any set of rectangles, and it carries more than a class name. Each space decomposes into
**parts** — a slab decomposition into rectangles, each re-classed by its *own* walls, so a rule can
reach an inset leg that the flat class would have forbidden wholesale. Each space publishes its
**mouths**, one per open direction, so a bay has one, a notch two and a hole none; a mouth is an
interval tapering to a `wN` width class, which is what says how wide a thing may dock *through* the
opening. And each space carries the slots of the pieces walling it, plus its own compound form — the
void read back as though it were a body.

The classification of the boundary runs is what turns all of this into a usable surface. Every run is
read on **three independent axes**: what it faces, whether it is **terminal** (the room seals its own
wall), and whether it is **guarded** (inside the room's clearance margin, the corridor minimum). The
**offerable surface** — the free outward surface a neighbour may actually attach onto — is exactly the
runs that are open, not terminal, and not guarded. This is the surface the allocator seats against;
it is a property of the emitted body, not of its bounding box, which is why a neighbour never docks an
empty stretch of an L's bounding box and meets the real terrain only at a corner.

Not every space a shape leaves is offered onward. Publishing one is an **offer, never a fill** — a
later step may claim it, a third wool seated in a free-standing U's bay or a ring's hole, or nothing
may, and an unclaimed space is simply void. What may be published is a **veto**, and it turns on
whether the shape carries a goal. A **terminal-capped** shape vetoes its bays and its holes, because a
bay walled by the terminal's own path would grant the wool a second approach and an enclosed hole is
the shape's own device; it allows only its notches. A **terminal-free** body vetoes nothing, having no
goal to shelter.

One word needs guarding. The **hole** here is a *shape-level* negative space — the donut's void, four
walls, read off one body's geometry. The board deriver also classes holes (`encased`, `gap`,
`frontline`, `middle`), but those are **connectivity** classes read off the whole finished board by
what their boundary touches. The two are unrelated readings that happen to share a word, and a box's
opening is neither: that is an interface.

### 4.3 A body is not yet a placed shape

A body is geometry with no purpose attached. It has no goal in it, nothing marks which of its edges a
neighbour may land on, and it does not know whether it is a hub in the middle of a team's land or the
arm running out to a wool. Turning it into something a map can use is the job of an **emitter**, and
an emitter always fills a box that the allocator has already positioned. Footprints are never grown
outward from the terrain; the terrain is laid **into** a rectangle whose size and place were settled
before any of it existed.

That rectangle is an **envelope, not a fill target**. Its contents must touch its edges and stay
connected, but they need not fill it solid — an L in a five-by-four box leaves a whole quadrant empty.
One shape can therefore take many footprints inside a fixed rectangle, which is what gives the
generator room to vary without renegotiating the partition.

Two emitters sit underneath everything, split by whether what they build ends in a goal:

- **`BodyEmitter`** builds a body and stops. Its pieces carry only structural labels — a spine or a
  ring's bars are `bar`, its arms and side walls `leg` — because that is all a shape without a goal
  has to say about itself.
- **`ShapeEmitter`** builds a **terminal-capped** shape: a body elaborated so that one rectangle at
  the end of it is a room, and one rectangle at the start of it is where a host connects.

A box carries a **kind**, and there are five: `spawn`, `hub`, `wools`, `frontline` and `mid`. Four of
them are filled by an emitter. The **wool box** and the **spawn box** both bind over `ShapeEmitter`,
and a spawn is a terminal-capped approach exactly as a wool arm is — same emitter, same classifier,
same round-trip check, differing only in what the room holds and how big it is. The **hub box** and the
**frontline box** both bind over `BodyEmitter`, because neither ends in a goal. The fifth, the **mid**,
holds no shape at all: it is the neutral band between the two frontlines, and what fills it is a build
zone rather than terrain.

**Boxes are a model of how an author works, not a property of maps.** Staking out a region, filling it,
and cutting it up is what a person does; the box makes that legible to a machine. But boxes exist only
while a map is being composed. No finished map carries them — not the traced corpus maps, not the
authored seeds — and they are never recovered from geometry afterwards, for the same reason a finished
shape's base plan is not recoverable: the moves compose one way only.

When nothing fits a box, that is a **signal rather than a failure**. An over-constrained box is
answered by changing the box — resize it, relax an interface, split it — so the refusal feeds back up
a level rather than aborting. Every fill therefore refuses with a **directed reason** rather than a
bare nothing: too small, the form does not fit, that family is not on this box's menu, the dock would
be illegal, the requested knobs are unsupported. The reason names which knob to turn, and it is
deliberately one vocabulary across every box kind, since a caller reasoning about why a box will not
fill should not have to know which kind refused it.

**Everything is emitted in one canonical orientation and turned afterwards.** An approach is built
mouth-up and a body spine-up, always, so the per-family geometry is written once rather than four
times. Placing it on the edge a box actually docks is a separate transform — `MouthOrient` for the
terminal-capped kinds, `BodyOrient` for the terminal-free ones — where the top edge is the identity,
the bottom a vertical mirror, and left or right a quarter turn that transposes the box. The piece
rectangles, the marker's offset within its room, and the mouths of every published vacancy all
follow that transform together. This is why the mouth edges differ per family in the canonical frame
— I, L, Z and the scythe enter at the top, the U, H and clamp at the bottom where their legs run
down to the host, the donut at the left — and why that difference never reaches a caller, which asks
for an edge and gets a shape facing it.

### 4.4 What each box kind adds

What a box kind adds on top of a body is its **designation**. There are three, and the split is by
what the shape is *for*, not by which box holds it: wool and spawn share one, because a spawn arm and
a wool arm are the same kind of thing.

The **approach** is the designation with a goal. It finishes a body with a **terminal** — the room the
wool or spawn marker sits in — and an **entry**, which names whichever rectangle a host connects
through. The **hub** designation has no terminal; it publishes, per free run of its body, the width
that run can carry, which is what makes the hub the constraint source every neighbour reads. The
**frontline** designation also has no terminal; it designates one edge of its body the **face**, where
the fanned images meet, and docks its host on the opposite edge.

Having no terminal is the whole of what separates the second two from the first. A hub and a frontline
are passed through; an approach is arrived at. Everything else — that a hub tends to be squat and a
frontline long, that one publishes widths and the other a face — follows from what a player does on
it.

**A terminal and an entry are not the same kind of thing.** The terminal is always a **rectangle**:
real material, carried apart from the shape's other pieces because it is the goal rather than a
corridor. The entry is a **name for whichever rectangle does the docking**, and whether that rectangle
exists for the purpose depends on the family.
In an I the lane that runs the length of the box *is* the entry, and in an L it is the vertical arm —
rectangles the shape would have regardless, labelled for what they do. In a donut the entry is a
rectangle that exists for nothing else, a short attachment stub against the ring's edge, while the
ring's own top bar is separately labelled `entry-bar` beside it. A rule quantified over "the entry"
therefore has to mean that name; it can assume nothing about the rectangle's size, position or whether
removing it would leave the rest of the shape intact.

The two layers are real in the code, but not as a pipeline one stage feeds the next. `ShapeEmitter`
has a single per-family routine that computes a family's terrain, its room and its vacancies together,
and two entry points onto it: `Body` returns the terrain alone, terminal-free, and `Approach` combines
such a body with a room and a marker into a finished emission. So an approach can be taken apart into
body and terminal, and the hub and frontline reuse exactly that terminal-free half — but no approach is
built by first constructing a generic body and then designating it, and the approach path never routes
through `BodyEmitter` at all, borrowing only its ring-wall geometry for the donut. The room is computed
alongside the terrain by a routine that knows the whole family, not bolted onto a finished body.

What the split really buys is at the reading end: identity can be taken in two independent registers,
topology below and terminal placement above, and that is what lets one body serve several finished
shapes.

There is a fourth thing the designations were meant to carry and do not yet. The docking law is
tabulated per designation: for a hub the `interface` mark is the docking edge, for a frontline the
`face` mark, every structural slot internal, and nothing never-docks since neither carries a terminal.
Both marks exist as constants and `DockingGate.Role` already maps them. But no emitter writes either
onto a piece — the hub publishes offers and the frontline returns its face edge directly — and the
gate's live check still scopes to the approach. Binding the two designations to that table is G88/G89.

### 4.5 Which bodies each kind may take

A designation does not admit every body. Each kind draws from an authored menu, and the menus differ
because the three shapes do different work on a map.

The **wool and spawn** menu is the nine **approach families** — the terminal-capped shapes, drawn in
full under *How a wool approach is built*. They are the widest menu because an approach is the most
varied thing on the board: it must reach a goal from a host, and there are many ways to make that
walk interesting.

The **hub** menu is **Rectangle · L · U · Ring · P · Double-hole · G**: the compact bodies, plus the
wide holed ones once the hub is laterally elongated. Zig, hook, the higher combs and `TwoUOnI` are
deliberately excluded, because a hub stays rectangle-ish. The hub grows **wider, not squarer** — its
lateral span is drawn from a larger cap than its depth — and the long edge does double duty: it gives
neighbours room to attach, and it reaches the width the holed forms need. A P is a loop with an
overhanging bar; a double-hole is a ring plus a full-height U, giving two equal holes; a G is a ring
plus an L, whose open bay a docking frontline seals into a taller hole, for a deliberately asymmetric
pair. Built by `HubBoxEmitter`.

The **frontline** menu is a plain **Bar** for the wide face (FR6), the **branch family** — a spine
plus K arms, single or twin or more — and the **holed** forms `P` and `TwoUOnI`, which present a
closed recess. Rotation is fixed by the designation rather than sampled: the body docks the hub with
its spine and its arm-tips are the face toward the axis. The face offer carries a **grouping** —
`Joint`, where one mid consumer must span every tip flush, or `Several`, where each tip takes its own
consumer and the inter-tip recess is simply not offered, surviving as a deliberate hole. Joint against
several *is* FR6's wide against split frontline. Built by `FrontlineBoxEmitter`.

Two arrangements that get called shapes are not bodies at all, but docking patterns in the designation
layer. The **clamp** is one compact terminal docked on two distinct faces — opposite faces give a
centred I plus I, adjacent faces a corner L plus I, the bend forced because two straight bars would
only corner-touch. The **twin frontline** is two Bars docked to one host individually, the gap between
them being the face recess of CT8.

### 4.6 How a wool approach is built

An approach is a body walked from a host to a goal, and the nine of them are an escalation rather than
a flat set. An L whose lane doubles back is a scythe; a scythe whose bay closes is a donut; a clamp
whose wool docks flush on one bar is a U; a U that lifts its wool onto a stub is an H. Read in that
order the catalog is four steps and two special cases, not nine unrelated pictures.

The two special cases are not bodies of their own. `Isolated` is any body at all with no terrain
reaching the wool — a reading, never something emitted. `Clamp` is the two-faced docking pattern seen
as a family: the wool bridging two otherwise-separate bars.

The figures below are the shapes `ShapeEmitter` builds, drawn at one cell per corridor width. The
mouth — the edge a host connects through — is at the top for I, L, Z and the scythe, at the bottom for
the U, H and clamp whose legs run down to the host, and at the left for the donut.

```
Isolated        I               L               Z
  ..              ttw             t..            t.
  w.              ...             ttw            tt
  ..                                             .t
                                                 .w

Scythe          Clamp           U               H
  tt.w            twt             .w.            .w.
  .t.t            t.t             ttt            .t.
  .ttt                            t.t            ttt
                                                 t.t
```

The same family can seat its terminal in more than one way. All three of these are donuts:

```
Donut                  Donut                  Donut
— wool off the ring    — wool at the corner   — wool held out

  tttt.                  tttt                   tttt..
  .t.t.                  .t.t                   .t.t..
  .tttw                  .ttw                   .ttttw
```

The scythe is the one worth reading twice, because it is the only family the **fold** decides. It
enters at the top-left tail, drops a spine, runs a bottom bar, and climbs a return leg to the wool at
the top right — three bends, with a tight bay between the spine and the return leg. It is not a
symmetric U with the wool moved: a U's two legs meet a crossbar and branch, while the scythe's path
never branches, it doubles back.

The three donut figures are the same family under different room placements, and they show what the
terminal is free to do. In the first the room hangs off the ring's edge. In the second the bottom bar
stops short and the **wool takes the ring's own corner**, integrated into the loop rather than
attached to it — which is why that variant needs a narrower box than the others. In the third a short
run holds the room out away from the ring. The wool is not obliged to sit against the ring at all,
and a rule that assumed it would be wrong on two of these three.

What each family *means* on a map, which is the reason the nine are worth telling apart:

| Family | Reads as |
|---|---|
| **Isolated** | wool ringed by void — no terrain approach; reachable only by building |
| **I** | a terrain lane caps the wool inline (a solid body with no bends also reads I) |
| **L** | one bend — terrain reaches the wool from two adjacent sides |
| **Z** | a staircase of two opposing bends, no bay |
| **Scythe** | a fold that wraps an **open bay** beside the wool |
| **Clamp** | the wool **bridges** two otherwise-separate bars — remove it and the terrain splits |
| **U** | two legs meet a crossbar and the wool docks **flush** on it, the bar overhanging the wool |
| **H** | two legs meet a crossbar and the wool caps a **room-run stub** its own width, lifted off the bar |
| **Donut** | terrain **encloses** a void — a full loop, multi-access |

**The pieces are labelled as they are laid.** The emitter does not build a shape and then work out
what its parts are; each rectangle is added already carrying its **slot**, because the emitter is the
only thing that knows the answer for certain. A family is therefore an ordered template of slot-typed
pieces, fixed per family and only resized:

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

Two invariants hold the templates together. A family emits a **stable piece count** — collinear pieces
are never merged, because a stable set is what makes "the entry is piece N" a usable rule. And a
**slot is a template position, not a property of the rectangle**: a scythe's `entry-run` and a donut's
`leg` may be the very same rectangle occupying different slots. The table is realized as data in
`ApproachSlots.Template(family)`, and each emitted piece carries its slot on `GrownPiece.Slot`.

Those are the base configurations. The knobs add pieces without changing the family: a second donut
attachment is another `entry`, and the wool-extend run of the third donut figure above is a `run`.

**The knobs are the variation the model allows without leaving the family.** Beyond the room placements
already drawn, a shape can be flipped so its turn goes the other way; the room can tuck off the side of
the last segment instead of capping it; the donut can take a second attachment, slide an attachment along
the ring's edge, or widen it; and the scythe can slide either of its two endpoints down the docking edge.
That last one propagates rather than merely moving a rectangle: a shifted entry takes the top of the
spine it docks with it, and a shifted wool shortens the return leg the same way, because a full-height
spine standing over a dropped tail is a different shape wearing the same name.

`RingWalls` is the widest of them. Every ring-bodied form — the `Ring`, the `P`'s loop, the `G`'s ring,
the `DoubleHole` and the donut — takes its four wall widths independently, so one bar or leg can run
wider than the rest where more play should flow through that side. The hole is whatever the walls leave,
so widening spends the box's slack, and a wall vector too fat for its box is **refused outright** rather
than quietly squashing the ring into something thinner than asked for.

Every family also has a **minimum box**, computed from the corridor width and whichever knobs are set,
below which the shape cannot be drawn at all — a donut integrating its wool at the ring corner needs a
narrower box than one hanging the room off the end, because the terminal sits inside the ring's span
instead of beyond it. Asking for a box under that minimum is refused, not shrunk to fit.

A slot is itself two layers, split by what supplies it. The **structural slot** — `run`, `bar`, `leg`
— is what the rectangle *does* in the body, and is shared by every box kind. The **designation mark**
— `entry`, `room` — is the docking position and the terminal, supplied by the approach, and qualified
`entry-run` / `room-run` / `entry-bar` / `room-bar` where a family carries two of a segment.
`ApproachSlots` merges the two because the taxonomy began at wool approaches; splitting them is what
would let one set of shift, widen and tail-follow knobs drive a wool mouth, a spawn mouth, a hub edge
and a frontline face without four implementations.

The payoff is that composition rules become properties of a **slot**, defined once per family. Entry
widening and entry shift live on the `entry` slot. Wool docking — extend against side-dock — lives on
the `room` slot. Which pieces may split into build zones is stated per slot: a `run` or `bar` can be
cut into a lane plus a build-lane, while an `entry` or `room` typically stays whole.

**The labels drive; the deriver only verifies.** Slots exist for generated maps and nowhere else, and
they are the mechanism that makes every later pipeline move rule-governed rather than geometric
guesswork:

- **Labels survive the whole compose pipeline.** Every compose move after emission — mid carve,
  isolation cut, repair, fragment — runs on labeled pieces, and all of them run before `Assemble`, so
  the labels are in hand exactly where the rules need them. A shape already attached to another shape
  is **never re-read**: classification proves the emitter placed the right thing, but it is not how
  the composer knows what a piece is.
- **Ownership is part of the label.** A slot names a position within *one box's* fill, so the full
  label is (box id, box kind, slot) — `wool-a/entry`, `hub-a/bar` — which lets connection and
  fragmentation rules bind per box kind and not merely per slot. Today the box id rides informally in
  the piece-id prefix; carrying it properly is the target state.
- **Products of a move inherit the label.** When fragment splits or converts a piece, its products
  keep the (box, slot) ownership, so a build zone knows it replaced `wool-a/entry-run`. That is what
  makes the per-slot cut law enforceable *at the cut* rather than re-derived afterwards.
- **`Assemble` is the boundary.** Labels drop from the written plan — `plan.json` has no slots, since
  they are a compose-internal category — while the evaluator receives them in memory via
  `EvalContext`. A plan on disk is label-free by design.

### 4.7 Derivation and classification

Every shape the generator builds can be read back from its geometry alone, and the reading is what
proves the building. `ShapeClassifier.Classify` takes one box's terrain and returns its family. It is
a single decision tree, ordered **strongest signal first**, and nothing in it keys off an absolute
width:

1. **No terrain touches the wool** → **Isolated**.
2. **Terrain encloses a void** → **Donut**. A loop may carry a thick corner and still be a donut.
3. **The wool is a cut cell** — removing it disconnects the terrain, so it is the closing wall
   bridging two otherwise-separate bars → **Clamp**.
4. Otherwise the open path, by **bend count** — the reflex corners of the terrain *outline*, taken as
   the approach minus the room so the count is width-invariant. **0 → I**, **1 → L**. At **two or
   more**, one fork remains: is there a **branch**?
   - A branch means two terrain runs meeting a shared bounding-box edge the wool is *not* on. The
     wool's own edge is excluded, which is what stops a fold's two path-ends reading as a fork.
     With a branch, the wool's seating decides: **flush on the crossbar → U**, on **its own room-run
     stub → H**. U and H differ by exactly one piece.
   - With no branch, terrain that **doubles back** — some row or column crossing it in two runs, the
     fold wrapping a bay — is a **Scythe**; a staircase of opposing bends is a **Z**.

Because no step consults the reference width, an H with one box leg and one thin leg still reads H, a
uniformly widened Z stays Z, and a wide-bay scythe stays a scythe. Width chooses which family is
*legal* and how it *joins*; it never changes what a shape *is*. This is the single most useful
property of the taxonomy, because it means a shape can be widened for play reasons without silently
becoming a different thing.

The **fold** carries its own weight in that tree, because it is the test that decides exactly one
family. A fold is terrain that doubles back on itself, width-independently — and it, not a
bounding-box read of the bay it wraps, is what the classifier asks. Sliding an endpoint off a box
corner opens the bay toward a second edge without unfolding the shape, so the fold read stays stable
under the emitter's entry and wool shifts and under docked neighbour terrain. The gaps in U, H and
Clamp are bay-shaped too, but there the family is already fixed by the branch and bridge tests. Fold
and bay are *features*, never families.

**Building and reading are a forward/inverse pair, and asserting they agree is the correctness test of
the whole shape model.** Emit a family at every size and width, read it back, and require the answer
to equal what was asked for. Nothing else checks the emitter: there is no independent oracle for
"is this the shape it was supposed to be", so a disagreement between the two directions is the only
signal that either is wrong, and a change that breaks one usually breaks it silently.

The scope of that check is **the generator's own artifacts** — emissions, synthetic fixtures, and
composed pre-fragment units, where the wool box bounds what is read. Classifying **finished maps** —
traced corpus maps, hand-authored plans — is out of scope **by decision**, not by omission.
Fragmentation moves family identity onto the play surface of terrain plus build links, a finished
map's base plan is not recoverable from what survives, and full-map decoding is a trap; the human
oracle hypothesizes the fragmentation and mutation moves instead (`wool-approach-read.md`).

The check runs in `tests/PgmStudio.Pgm.Tests/Shapes/`: emit every family × size × width, read back,
assert equality with no overlap, and assert that the emitted slot sequence equals
`ApproachSlots.Template`. A companion set pushes each family's pieces to extremes at a fixed width, so
every one must still read its own family — the width-independence proof. The nine-family catalog
itself is checked in as fixtures under `tools/deriver/shapes/*.plan.json`.

**Three readings are checked this way, not one.** The family reading is the one just described. The
**slot** reading is a second: `SlotAssignment` re-derives each piece's slot from topology alone — path
order for the chain families, adjacency for the branches, hole-edge geometry for the donut — and never
from where a rectangle happens to sit, so every placement knob above survives it. And the **vacancy**
reading is a third: what an emitter declared it was leaving empty must match what `BodyEdges` reads back
off the finished rectangles. Emitter labels, deriver recovers, and the two are asserted equal at each
level.

The **body layer is checked the same way and separately**, so the wool and spawn path stays
byte-identical: `BodyEmitter` emits a terminal-free compound and `ClassifyBody` reads it back by
topology, strongest signal first — void count, then arm count, then bends. Void count does most of the
work. **Two voids** split on whether an open channel or a solid wall separates them, giving `TwoUOnI`
against `DoubleHole`. **One void** splits three ways on what sits beside the loop: an open bay is a
`G`, an overhanging bar a `P`, and neither a plain `Ring`. **No void** splits on whether the bounding
box is solid — a `Rectangle` — or branches, in which case only the arm count matters, so an F and a Π
both read as two arms. Placement is deliberately invisible to it.

The figures on this page are held to the same standard. `tools/deriver/figure-check.cs` parses every
labelled grid out of this section and pushes it through the classifier that names that kind of thing —
a body through `ClassifyBody`, a family through `ShapeClassifier`, a negative space through
`BodyEdges` — so a figure that does not read as labelled is a failure rather than something a reader
has to catch. It reads the figures from the document rather than holding copies, which is what stops
the two drifting apart.

**Classification says what a shape is; it does not say the generator could have made it.** Those are
different questions, and the second one — *could the composer have produced this?* — is what an author
asking why their hand-drawn box will not reproduce actually needs. Identity is only a hint toward it: a
ring with one-cell walls still reads as the `G` it topologically is, even at a width no `G` is emittable
at. `Producibility` answers the real question, and it is worth understanding how, because the method is
what keeps it honest.

It answers **by search, never by inverse**. The parameter space is already written down as data — the
hub and frontline form menus, the wool production families, the spawn sizes, the wall and lane widths —
and every emitter is a pure function of an explicit tuple with no sampling inside it, since the sampling
lives one level up in the allocator. So the search enumerates the tuples those tables admit, calls **the
real emitters**, and compares the resulting cell masks against the box. Nothing about a rule is restated
here, which is the point: adding a hub form makes it producible for free, where a hand-written parameter
recovery would have to be taught the new form and could silently disagree with the emitter it was
imitating.

When nothing reproduces a box, the enumeration has already produced the answer to *why*. The **nearest
miss** is the candidate differing in the fewest cells, reported as the cells it emits that the box lacks
and the cells the box has that it does not — which says where the drawn geometry left the parameter space
without needing a bespoke analyser per family. Alongside it sit the emitters' own refusal reasons and a
measurement of the mask against the very constants the emitters read. Terrain and room are compared
**separately**, because a box whose corridor reproduces but whose room does not is a different answer
worth stating on its own: the room carries export semantics rather than being more terrain.

Some findings are properties of the **arrangement** rather than of any one box — the parallel-fronts
guard, the frontline's face demand, the seat-separation law — and those are asked of the composer's own
predicates rather than reimplemented. Both halves are reported, since a plan can have every box
individually producible and still be arranged in a way the composer would never draw.

One number in it is a coverage guarantee rather than a tuning choice. Where a sampler is the only thing
that knows its own laws, its **range** is collected by running it rather than by restating the laws, and
the sweep draws fifty thousand seeds. Most spaces saturate within fifty; the frontline's two-leg layout
does not, because its bay, end recess, offset and split are drawn independently — on a twenty-cell spine
that is some three hundred and ninety outcomes whose rarest first appears around seed five thousand.
Under-drawing it would report a layout the composer really draws as unproducible, which is the one
failure this must not have.

---

## 5. Allocate and fill — from a budget to a built unit

Composition takes a budget and returns one team unit with terrain in it. It runs in two halves that
must not be confused. **Allocation** decides structure and position while nothing is yet made of
anything: it begins with a budget and ends with a `BoxPartition` — typed boxes, the joints between
them, and the spawn's facing — and everything it produces is a rectangle in plan cells. **Filling**
then puts terrain inside those rectangles, deciding only what allocation left open.

The order matters more than any individual rule in it, because each step consumes what the previous
one settled and never reopens it. The hub is placed. Its form is chosen and emitted, which is what
turns a footprint into material and so decides where anything may dock. Separately, and in ignorance
of all of that, the unit works out what it needs hung off the hub. Only then does seating put the two
together, and seating's entire output is one integer per neighbour. The boxes are filled, the finished
unit is placed against the axis, and only then does a board exist to be judged.

### 5.1 The frame the unit is built in

Allocation never lays out a board. It lays out **one team unit**, and the board is that unit repeated —
reflected or rotated into its symmetry images, so a two-team map is one authored half and its mirror. The
frontline is where those images meet. Allocation therefore describes a single image throughout, and any
claim about the whole board is a claim about what happens when the images are placed side by side.

The unit is not drawn in world coordinates either. It is drawn in a frame of two axes: **`u`**, the
distance out from the symmetry axis, increasing away from it, and **`v`**, the cross-axis coordinate,
centred near zero. Which real axis `u` rides on, and in which direction, is a property of the symmetry
mode — `z` for the rotations and the z-mirror, whose unit occupies the far half or wedge, `x` for the
x-mirror. Working in `u` and `v` is what lets one piece of layout code serve every symmetry mode: without
it, each of the modes would need its own copy of every placement rule, differing only in which coordinate
counted as "toward the axis".

Because only one unit is authored, the board's overall extent is an **output** rather than an input:
the frontline lands where the images meet, and how far out that is depends on how much each half grew.
Nothing sets the map's length directly.

One arithmetic decision is already made before allocation begins. The **crossing is fixed first** — the
gap from one team's front to the other's is settled while the board is still empty — and its half-gap
*is* the margin the allocator must leave against the axis. Allocation therefore starts with the axis
already spoken for, and the unit grows back from a boundary it does not get to move.

### 5.2 The hub goes down first

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

### 5.3 The form decides where anything may dock

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

### 5.4 What the unit asks for

Independently of the hub, and before any position exists, the allocator works out what the unit needs.
It does not decide the counts: how many wool boxes there are, whether there is a frontline at all, and
how large the spawn is all come off the budget ladders, and the allocator reads them as given. Its work
here is to turn each into a `NeighbourRequest` — one per wool, one for the spawn, and one for the
frontline where the budget affords it. The spawn is the one box whose size barely moves:
roughly ten blocks square where it docks the hub directly, ten by twenty where it wants a run-up, and
twenty square for an L. It is never large, because a spawn is somewhere a player leaves rather than
somewhere a fight happens.

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

### 5.5 The seat

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

What a seat produces is an envelope, not terrain. What goes inside it — and how much of it is left
empty — is settled later, by a filler that cannot move the rectangle it was handed.

### 5.6 The three dock rules

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

All three assume a shape docks through **one** mouth, and one family does not. The **clamp** clamps its
wool between two entry bars, so a legal dock has to satisfy both entries at once, along the short entry
edge (`t` terrain, `w` wool, `.` void, `h` host):

```
t w t          t w t .
t . t          t . t h
h h h          h . . h
```

On the left the whole short edge is one host: both entries land, and the bay closes into an intended,
declared hole. On the right two hosts take one entry each — the harder corner-wrap — and the bay stays
open. Docking the wool-side edge instead is illegal either way, because the entry stubs then dangle in
the void. Whether such a dock is **legal** is now decidable: the gate resolves a box edge to its slots
and applies one table, so a dock is legal exactly when it lands on an entry and seals no wool. What is
still missing is the ability to **place** the two-host case, which is a partition-graph problem rather
than a legality one, so the clamp stays out of production until the partitioner can express it.

Which edges are legal is a property of the **shape, not the box**: an entry shift carries its dock
along with it. The scythe shows why that matters. It docks on the outer side edge opposite its internal
seam, or on the combined edge its entry and entry-run present when their head edges line up — and those
heads stay flush under a shift, so the combined edge moves intact. A host touching the wool room, by
contrast, is a hard rejection in every case: that is the flush dock that would seal the bay and make the
room itself the door.

### 5.7 What keeps neighbours apart

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

A third law governs the distance between **images**, and it is stricter than either. Terrain divides into
two classes: **land**, the connected mass of one team unit, whose pieces are free to touch each other
because they are meant to be one island; and **isolated** pieces — mid stones, severed plateaus — that
are reachable only by building and must never weld to anything. Any pair drawn from different symmetry
images, and any pair at all involving an isolated piece, has to keep a fixed clearance on some axis. The
reason is that a shared border joins terrain no matter how narrow it is: one cell of contact between a
unit and its own mirror image turns two islands into one and quietly deletes the crossing the whole map
is built around. The same measurement also supplies the length of a straight unbroken run, which is the
unit of account for the rule against long flat frontiers.

### 5.8 When a seat cannot be found

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
front would extend that face into one long flat frontier, so the seat slides backward —
deterministically, consuming no draw — to the nearest position that clears the front. Seats that no
backward position can hold are collected and resolved after every neighbour is placed, when the full
set is known and an earlier drop may have freed the very blocker.

### 5.9 What a joint records

A box allocates a **budget, not exclusive area**, so two footprints are free to overlap — the
partition is a set of claims on land and space, not a tiling. What that costs is that adjacency can no
longer be assumed from the rectangles alone: a joint is asserted **only where two footprints genuinely
abut**, and an overlap by itself connects nothing.

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

### 5.10 What the filler still decides

Allocation settles structure and position; filling puts terrain inside what it settled. The seam
between them is not a formality, because a decision taken on the wrong side of it is taken without the
information that should have governed it.

Most of the fill is already determined. The hub re-emits the body the allocator chose, at the same wall
widths and arm layout, because a body sampled twice would not agree with itself. A **wool** re-emits
the exact family it was seated as, and must not re-pick: the box was positioned for *that* family's
entry, so a different family of the same footprint would put its entry somewhere the seat never
cleared. What each neighbour builds to is likewise fixed — the width its own joint was granted.

Two kinds genuinely choose at fill time, and both choose within what was granted. A **spawn** picks
among the profiles that fit at its granted width. A **frontline** picks a form that answers the hub's:
a branch hub takes the wide Bar across its front, while a square or holed hub prefers a staple or a
strand, because a solid Bar flush against an already-square hub reads flat.

The frontline's choice is where the seam currently leaks, and it is a known defect rather than a
design. Its **form choice** is declared the allocator's, since form is what decides where anything may
dock. Its offer **grouping** — one consumer spanning every tip against one consumer per tip, which is
FR6, an authored law — is part of an offer, and an offer is the allocator's plan. Both are made in the
filler today, the grouping by coin flip. Tracked in `audit.md` as G111.

### 5.11 Placing the finished unit

Building the unit and placing it across the axis are two steps, and the second one exists because the
first anchors on the wrong thing. Everything above is laid out around the hub, whose lateral span is
centred on the axis; the frontline is then seated onto whatever run the hub happened to offer. So the
**face** — the front-most row of terrain, and the only part of the unit the mid actually docks — ends up
wherever seating put it, which need not be centred at all.

Under a symmetry that flips laterally that is the wrong anchor, and the consequence is concrete. The
opposing image reflects the cross-axis coordinate, so an off-centre face and its own image land on
opposite sides of the axis. The crossing then has to span the hull of both, and most of the band it
carves borders neither front — a wide dead crossing produced by nothing more than where a seat landed.

The fix is to re-anchor the finished unit on its face: translate the whole unit across the axis until the
face is centred on the axis point. The face and its image then coincide, and the band becomes exactly the
face rather than the hull of two offset copies. Nothing inside the unit moves relative to anything else,
so every seat, joint and clearance settled above survives the translation untouched — which is what makes
this safe to do last rather than a constraint seating would have had to carry.

Once both images are in place, the arrangement is measured as one board. The **closure** — every terrain
piece and build zone across all images — is rasterized and its enclosed voids found by flooding inward
from outside. Each such void is a hole, and holes are the device the corpus uses to give a map rotation:
a loop around one offers routes between lanes that do not retreat through a single chokepoint. What rings
a hole matters as much as its existence, since a hole ringed by a wool plateau is a motif the composer
declines to author. This measurement is deliberately a fast, narrow twin of the whole-board deriver's
void classification, kept separate because it runs inside the composer's attempt loop where re-deriving
the entire board every attempt would be waste — and the two are required to agree, so a change to the
board deriver's hole rules is a change to this twin.

---

## 6. Deriving the board

A plan says what an author meant. It does not say what a player will find. Between the two sits
**derivation**: reading a finished plan back as terrain and working out, from geometry alone, what the
board actually is — where the islands are, what links them, which voids are enclosed and by whom, and
where each team's exposed front runs. Nothing here is authored and nothing here is stored. It is
recomputed whenever a plan changes, which is what lets a rule be stated over structure the author never
has to maintain by hand.

Two things share the verb. The **shape deriver** reads one box's terrain back to a family, and belongs
to the shape model rather than here. The **board deriver** reads the whole plan, and when this document
says *the deriver* without qualification it means this one. It works in two layers over the same plan:
a rect layer that compares piece rectangles pairwise, and a raster layer that fans every piece and zone
to the full board in cell space and computes structure from geometry plus markers. The rect layer
answers *do these two pieces touch, and how*; the raster layer answers everything that needs a picture
of the whole board at once.

### 6.1 Islands, and what anchors them

The first question is which terrain is one place, and answering it needs the word for how two pieces
meet. An **interface** is always a shared **interval** — a position and a width — where two pieces
touch, or where a piece meets a build zone. It is never a point and never a node, and that is a rule
rather than a convention: two rectangles meeting only at a corner share no walkable ground, so a point
touch connects nothing.

Contact off the raw rectangles reads as one of five kinds:

| Kind | Is |
|---|---|
| `Land` | a shared border at or above the corridor minimum — the pieces merge into one walkable mass |
| `Narrow` | a shorter positive border — still walkable, a staircase or ledge seam |
| `Corner` | a bare point touch — **never connects** |
| `Overlap` | area overlap — a same-surface overlap merges |
| `None` | disjoint — a gap a build region must span |

`Land` and `Narrow` are the **land interfaces**, the two that connect, and taking the union over every
one of them splits the board into **islands**. A piece with no land interface at all is isolated,
reachable only by building. Keeping `Narrow` as a connection is deliberate: a five-block shared step is
one island rather than two, so connectivity is decided separately from whether a route is wide enough
to fight through, which is judged later on the assembled footprint.

Above the raw kinds sits a coarser reading the plan itself uses: two pieces are either **land**, having
merged, or separated by a **gap** a build region spans, which carries a span distance. An elevation
transition — a step, a ramp or a cliff — rides on either.

An island on its own is just a shape. What gives it meaning is what stands on it, so each is tagged by
its **anchor**, in a fixed order of precedence: an island holding a spawn is a **team** island; one
holding a wool but no spawn is an **objective** island, the isolated-wool case; one holding neither but
touching a build region is **neutral**, a stepping stone in the crossing; and one holding neither and
touching nothing is **decorative**, excluded from play analysis entirely.

The neutral case splits once more, because two very different things read as neutral. A stone is
**captive** to a team when every build region touching it stays single-team — no opponent can reach it,
so it is really part of that team's own internal route rather than contested ground. A stone that any
opponent can reach is genuinely neutral. The distinction is geometric, not authored, and it decides
whether a link across that stone counts as a team's private bridge or as part of the crossing.

### 6.2 Build regions, and what they link

A build region is where a player may place blocks, and its meaning comes entirely from **what it
connects**. Once islands and their anchors exist, each region is typed by the anchors at its ends:

| Kind | Links |
|---|---|
| `front↔front` | two or more teams — the crossing, the direct team-to-team link |
| `front↔neutral` | one team and a neutral — that team's bridge toward the middle |
| `neutral↔neutral` | only neutrals — a mid-internal link, usually spanning the axis |
| `intra` | one team's own spawn-to-wool route — an isolation cut |
| `self` | a notch in a single island, both its walls the same landmass |

The last two are the ones worth naming carefully, because they look like damage and are not. An
**isolation cut** is a deliberate internal gap: a piece chopped off the main mass and bridged back
across a slow-down void, so a defender pays time to cross their own ground. A **self-bridge notch** is
a pocket carved into one landmass, both walls belonging to it. Both are authored patterns, and keeping
them as their own signals rather than folding them into the crossing is what stops a team's private
inconvenience from reading as contested ground.

### 6.3 Boundary runs, and what a frontline is

Where terrain meets build, the deriver records the boundary. This is the part of the model where
careless vocabulary does the most damage, so the terms are fixed:

- an **edge** is one full side of a box or a body, end to end — never a portion of one;
- a **run** is one contiguous stretch along a boundary;
- an **interval** is the stretch where two things actually touch.

The deriver works in the middle term. It walks every terrain cell, looks at each of its four
neighbours, and records a **segment** wherever a terrain cell borders a build cell. Segments are then
grouped into runs: contiguous stretches on the same island, joined end to end. A run, not a segment and
not an edge, is the unit every frontline measurement is taken over.

Which kind of run it is follows from the build region on the other side. If that region is one of the
team's own internal cuts, the segment belongs to the isolation cut or the self-bridge notch. Otherwise
it faces shared void, and it is part of the team's **front** — but only if the island it sits on is
genuinely exposed, meaning more of its boundary faces void than faces build. An island whose boundary
is mostly build is a stepping stone sitting inside the crossing, not a shore facing it.

That gives the **frontline** a definition in terms a machine can check. It is not a piece, and nothing
in the plan declares one. It is the set of boundary runs where a team's exposed land faces the void the
crossing spans. Each run carries the team that owns it, its **width** — the longer extent of the run,
in cells — and its **profile**: *straight* when every segment lies on one line, *offset* when the face
steps in and out. A team's run count, its face widths and its profiles are the frontline measurables,
and they are what the laws about wide against split fronts are actually stated over.

### 6.4 Enclosed voids

A **hole** is empty ground the outside cannot reach. The flood that finds them starts beyond the
board's bounding box and spreads through void only, treating **both terrain and build as walls** —
which is the one decision that makes the classification useful. A rotation pocket near the front, walled
by two frontlines on some sides and by the mid band on the others, is enclosed in every sense that
matters to a player; letting the flood leak through the build zone would report it as open ground.

Every enclosed void is reported, at any size. The authored corpus carries intended holes as small as a
couple of cells, so no size threshold is applied — a rule that discarded small holes would discard real
authoring.

A hole is classified by **what its boundary touches**, never by how big it is, which places it on a
spectrum from interior to contested:

| Class | Boundary touches |
|---|---|
| `encased` | one team's terrain, no build at all — a bubble deep inside a team's land |
| `gap` | one team, and the build it touches is all that team's own cut — a void inside an isolation cut |
| `frontline` | one team's terrain plus frontline build — that team's exposed edge on the crossing |
| `middle` | two or more teams, or pure build — the contested crossing, the arena |

Team ownership here is conferred only by **anchored** terrain — a spawn or a wool. A neutral stepping
stone confers none, because its team label is arbitrary: a centre island shared by both images carries
one fixed value, and letting that count would break the symmetry of the reading.

Each hole is also **declared** when it overlaps an authored buffer or zone-hole, and **undeclared**
otherwise. Undeclared holes are not errors; they are a worklist. And a contested hole reports how many
distinct build regions ring it, which is the count of parallel ways around it — the measurement behind
the observation that a loop around a hole gives a map rotation, since it offers routes between lanes
that do not retreat through one chokepoint.

### 6.5 What falls out of the rest

Two further readings come free once the above exists, and both are outputs rather than decisions.

A **wool lane** is the corridor a wool room owns. It is found by taking the edge where the room meets
terrain — the line the generator stamps the objective's redstone along — and stacking a fixed-width
band straight outward from it, cell by cell, until it reaches void, build, or a crossbar. A crossbar
means terrain reaching past the band on **both** sides; a one-sided jut is a side branch and does not
stop the stack. A room open on two sides stacks both ways. Where the forward stack dies immediately
into void — the room docked against the *side* of a lane rather than the end of it — the stack runs
along that lane's own axis instead, so the whole corridor the room hangs off becomes the lane. Only
wools stack a lane; a spawn never does. The lane's own topology is then read as a bend count, which is
a board-level corridor reading and deliberately not the same taxonomy as a wool box's shape family.

The **mid form** is the last of them, and it is entirely derived from the build regions already typed.
Any `neutral↔neutral` region at all makes the mid a **hash** — fractured into interlinked islands. With
none, two or more `front↔front` crossings make it **parallel**, and exactly one makes it **channelled**.
Nothing samples this. The halves grow, the join fixes where the fronts land, the fronts fix which
regions exist, and the mid's form is whatever those regions imply — which is why the middle of a map
cannot be chosen directly and has to be obtained by building the sides that produce it.

The exhaustive catalogue — every derived quantity, its exact definition, and its validation against the
seed corpus — is in `evaluator.md`. What is here is the model: what the deriver looks for, and why each
reading is defined the way it is.

---

## 7. Rules and scoring

The emitter can make almost anything. What gives the generator's output a character is not what it
can build but **what it refuses to let through** — the rules do not produce good maps, they punish bad
ones, and the residue is the style. That makes the rule set the most consequential part of the model
and the one most easily misused, so this section is about what a rule *is* before it is about what any
particular rule says.

### 7.1 Every rule is one kind

A rule that cannot be classified cannot be placed, and a rule in the wrong place is enforced at the
wrong time. So every rule in the generator is exactly one **kind**, and naming the kind is what gives
a new rule an address. The one-line test asks what the rule changes: what can be **picked**, whether a
pick **fits**, whether a join is **legal**, how a legal join **varies**, what a compose **aims at**, or
how good the result **reads**.

| Kind | Is | Exemplar |
|---|---|---|
| **fact** | an observation off geometry, no policy | `BoxEdgeInterface.Intervals`, the negative-space classes, `FrontlineRuns` |
| **menu** | a generative allowlist — what may be *chosen* (empty = a directed signal) | `FillProfiles.Families`, `FillMenu.Rows` |
| **fit gate** | does the choice fit the box | `ShapeEmitter.MinBox`, `FillProfiles.Fits` |
| **demand** | a shape's requirement *on its environment* (inbound) | no live type — see below |
| **offer** | what a shape imposes *outward*: the intervals it invites neighbours onto, and in which groupings | `EdgeOffer` |
| **veto** | a never-attach or never-publish mark | `SlotDockRole.NeverDock`, the publish policy's bay and hole veto |
| **gate** | the hard legality check applying demand, offer and veto, with a **directed rejection** | `DockingGate` → `DockRejection` |
| **knob** | a free parameter *within* legality — never changes identity | entry shift, attachment width, arm placement |
| **target** | a **per-request, prescriptive** constraint a compose holds and verifies | none live |
| **band** | a **descriptive** envelope measured off the seeds — advisory, scores distance | `SoftTerm` and the seed envelopes |
| **hard term** | a well-formedness symptom on the derived board — flat penalty | `WoolRingedHole`, `GapHopBand` |
| **law** | the id-bearing author rule the mechanisms implement | `rules.md` — FR6, CT9, BZ8 |
| **doctrine** | a meta-rule about where rules may live | "the labels drive, the deriver verifies" |

Two of those distinctions carry most of the weight.

**Demand against offer is the direction of the arrow.** An approach *demands*: its entry has to find a
host, and that requirement points outward at the world. A hub or a frontline *offers*: its runs
dictate where neighbours may land and how wide, which is what makes it the constraint source. Confusing
the two inverts who is obliged to whom. The word demand is now the model's alone — the allocator's
outbound sizing type is a request, renamed precisely so that demand keeps only the inbound sense.

**Target against band is prescription against description.** A band says *authored maps run one to
seven frontline runs*; a target says *this compose wants exactly two, connected*. Bands score a
finished compose from the outside; targets steer it first and verify afterwards. They are not
interchangeable, and a band pressed into service as a target silently turns an observation about the
corpus into a requirement on the generator.

An offer carries one thing beyond where and how wide, because those two do not say whether neighbours
may **share**. Its grouping is either *several* — each interval takes its own consumer, as a hub's four
edges do — or *joint*, where one consumer must span the whole group flush, as a wide face across both
tips of a twin frontline does, preserving the recess between them as a deliberate hole. Joint against
several *is* the law about wide against split frontlines, which is why the grouping belongs to the
offer rather than to whoever consumes it.

Three of the kinds are currently vocabulary without machinery, and saying so is part of the model
rather than an apology for it. **Demand** has no live type: the dual-entry requirement survives only
as an exclusion in the allocator's request list. **Target** has none either — the budget ladders are
what would become targets if it gained one. And the allocator's weighted sampling fits **no** declared
kind at all: a menu is a set and carries no frequency, while a band carries a distribution but is
explicitly descriptive. Naming that kind is an open decision, and all three are tracked in `audit.md`.

### 7.2 Where a rule may live

The kinds are not a filing convenience; they say *when* a rule runs, and that is a stronger constraint
than it appears.

A **menu**, a **fit gate** and a **gate** run during composition, where the thing being judged still
has a name — a family, a form, a slot. The evaluator cannot enforce any of them, because by the time it
sees a board those names are gone: the plan carries no slots and no family, only geometry and markers.
That is why the docking law is a compose-side gate and not an evaluator term, and why a rule stated as
"an L may not dock here" has to be enforced where an L is still called one.

A **hard term** and a **band** run the other way round. They read the derived board and nothing else,
which is what lets them judge a hand-authored map the composer never touched. The cost is that they can
only see symptoms — a wool ringed by a hole, a hop outside the bridgeable range — and never intent.

The two halves catch each other. The compose-side gates emit only legal structure; the derived-board
terms catch any symptom that appears anyway, which is exactly the case where a gate was wrong.

### 7.3 What the score is

Judgement runs over three layers, and each answers a different question. **Author intent** is the
irreducible input, the plan. **Derived structure** is the roles and topology, computed and held in
memory, never written back. **The judgement** is metrics against rules and envelopes. Everything the
file cannot recover is authored, everything structural is derived, everything the rules check is judged.

The score itself is a sum of two unlike things:

```
score = Σ hard-penalty(violated well-formedness) + Σ w · envelope-distance(metric)
```

The first term is well-formedness, and a valid layout has none of it: hard rules carry large penalties
because they mark a board that is broken rather than unfashionable. The second is feel, measured as
each metric's distance outside the envelope the authored corpus occupies. A board inside every envelope
scores zero on it and is, by construction, unremarkable in the way the corpus is unremarkable.

The evaluator returns the score **and the list of violated terms**, each citing a rule id, so a
refusal is legible and actionable rather than a number. And it is **additive and never has to be
complete**: terms are added as failures are found, and a new term never tanks an acceptance rate,
because a term only fires on the symptom it names.

### 7.4 What makes the evaluator correct

An evaluator is not correct because its terms look reasonable. It is correct when it **ranks a labeled
set the way the author does**, which makes the evaluation set the real deliverable and the terms merely
the means.

That set needs three things. **Positives** — authored good layouts, which the deriver can label
automatically. **Negatives** — flagged bad layouts, of which the most valuable by far are **minimal
pairs**, two layouts differing in exactly one property, since a pair like that isolates the term
responsible in a way a hundred unrelated failures cannot. And **coverage**: examples per sub-problem
and per symmetry mode, so a term is not validated only on the one arrangement it was written against.
The catalogue and the labeled set live in `evaluator.md`.

One discipline governs how the seeds may be used. **They sit at final-pipeline fidelity** — an
authored seed is what the *whole* pipeline should produce, never what any single stage produces on its
own. A stage is therefore judged only against the rules that stage owns: fill and slot invariants at
emission, envelope terms on the assembled board, feel at realize. Comparing an intermediate artifact
to a seed wholesale is a category error, and it is the same error as classifying a finished map back
to a family — reading an artifact against a standard that belongs to a different stage of its life.

The laws themselves — every id, its number, its evidence, and the protocol for amending it — are in
`rules.md`. What is here is the frame that makes those ids mean something: what kind of thing each one
is, when it can be enforced, and what it would take to know it is right.

---

## 8. Code map

Where each concept lives (paths under `src/PgmStudio.Pgm/` unless noted):

**The shape mirror — emit ↔ derive**

| Piece | Path | What |
|---|---|---|
| `ShapeEmitter` | `Shapes/ShapeEmitter.cs` | **emit**, in two stages: `Body` builds the terminal-free compound, a designation finishes it (`Emit` = the approach designation, stamping the terminal room + marker). No roles, no ids, no plan types. |
| `BodyEmitter` · `Compound` | `Shapes/BodyEmitter.cs`, `Shapes/Compound.cs` | the terminal-free body vocabulary (`Rectangle · SpineArms · Ring · DoubleHole · P · G · TwoUOnI`) and its emitter. |
| `BodyEdges` | `Shapes/BodyEdges.cs` | the edge-taxonomy reader (§4): negative spaces by wall count, their parts, mouths and the offerable surface — from geometry alone. |
| `ShapeClassifier` | `Shapes/ShapeClassifier.cs` | **shape deriver**: `Classify` → `ShapeFamily` (9 families), width-independent; `ClassifyOpen` → `LaneRead` is the board-level corridor bend read (`LaneName` → string `I/L/Z/complex/plaza/none`; the retired `WoolLaneShape` was a thin adapter over it). |
| `SlotAssignment` | `Shapes/SlotAssignment.cs` | **slot deriver**: `AssignSlots(family, pieces, roomId)` → piece→slot, re-derived from topology — the emitter's slot mirror (§4). |

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
| `Envelope` → `ComposeEnvelope` | `Compose/Envelope.cs` | the budget: player count → land-per-team, board extent, unit bounds (§2.2). |
| `ComposeRequest` | `Compose/ComposeRequest.cs` | the compose input, validated at construction (§2.1). |
| `FrontGuard` | `Compose/FrontGuard.cs` | the no-frontline seat post-pass: slide/relocate/drop a seat left flush with the empty front. |
| `UnitPlacement` | `Compose/UnitPlacement.cs` | re-anchors the finished unit on its **face** before the band is derived. |
| `Producibility` | `Compose/Producibility.cs` | "could the composer have produced this?" — answered by search over the declared menus, not by inverse. |
| `WoolBoxEmitter` | `Compose/WoolBoxEmitter.cs` | the wool binding over `ShapeEmitter` — fills a wool box, terminal → wool room + marker. |
| `SpawnBoxEmitter` | `Compose/SpawnBoxEmitter.cs` | the spawn binding: profile {I, L} + `Fill`, terminal → `Spawn` room + marker. |
| `HubBoxEmitter` → `EmittedHub` | `Compose/HubBoxEmitter.cs` | the **hub** designation: a terminal-free body plus the per-run `EdgeOffer`s it publishes as the constraint source. |
| `FrontlineBoxEmitter` → `EmittedFrontline` | `Compose/FrontlineBoxEmitter.cs` | the **frontline** designation: spine docks the hub, arm-tips are the face, carrying the face offers the mid consumes. |
| `EdgeOffer` · `OfferGrouping` | `Compose/Boxes/EdgeOffer.cs` | the **offer**: where a neighbour may attach, at what width, in which grouping (§7). |
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
| the shape mirror | `tests/PgmStudio.Pgm.Tests/Shapes/` | emit↔derive + slot-template + width-independence, as tests (§4, *Derivation and classification*). |
| `showcase.cs` | `tools/compose/showcase.cs` | **the explainer** — this document's live twin, every figure emitted by the real generator. |
| `unit-gallery.cs` · `box-gallery.cs` · `board-gallery.cs` | `tools/compose/` | composed units / box partitions / whole boards, rendered. |
| `body-gallery.cs` · `edge-gallery.cs` | `tools/compose/` | the terminal-free bodies; the edge taxonomy read off them. |
| `seat-probe.cs` | `tools/compose/seat-probe.cs` | the 4-preset × 200-seed `Allocate → Fill` probe behind `audit.md`. |
| `reproduction-gate.cs` · `fingerprints.cs` | `tools/compose/` | the determinism gate + the recorded composer fingerprints. |
| `derive-gallery.cs` | `tools/deriver/derive-gallery.cs` | `BoardStructure` rendered → `out/derive-gallery.html`. |
| `lane-audit.cs` | `tools/deriver/lane-audit.cs` | the `ClassifyOpen`/`LaneName` derive-then-override training harness. |

---

## 9. Boundaries

This document does not restate the rules or the numbers. The **rule law** — every CT / SP / WL / LN /
HB / FR / MD / BZ / EL id, with its exact widths, depths, hop counts, and heights — is `rules.md`,
and it grows only through its correction protocol. The **measured envelopes** the soft evaluator
terms score against are `seed-stats.md` / `seed-envelopes.md`. The **detailed deriver-measurable and
evaluator-metric catalogue** is `evaluator.md`. Where the implementation is known to **disagree**
with this model, the measured record is `audit.md`. The **plan schema and editor** are
`../contracts/plan-editor.md`.
