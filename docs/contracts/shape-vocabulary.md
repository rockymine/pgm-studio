# Map generation — the shape vocabulary

> **Status: draft, co-authored.** The terminal-free shape model that sits *beneath* the wool/spawn
> approach families — the shared layer the hub (**G88**) and frontline (**G89**) boxes build on. Once it
> settles this folds into `map-generation.md` §5 (which today frames shapes as wool-approach-only) and its
> types land as rows in `map-generation-vocabulary.md`. **Nothing here is code yet.** The working system it
> generalizes is the eight wool/spawn families, their `ShapeEmitter` geometry, and the emit↔derive mirror —
> those stay green throughout, and *byte-identical wool/spawn output* is the acceptance bar for any refactor
> this doc motivates.

---

## 1. The principle

Three layers, one direction:

```
rectangle  ──aligned recombination──►  shape  ──designation──►  filled box
 (the atom)       (escalation)        (compound)   (what finishes it)
```

A **shape** is a pure rectilinear body, identified by topology alone. A **designation** is what a box kind
stamps onto it — a *terminal* (approach), a set of *edge interfaces* (hub), or a *meeting face* (frontline).
The shape layer is shared across every box kind; the designation is per-kind. This is the generalization of
today's `ShapeEmitter`, which bakes a wool terminal into every family and so conflates the shape with one of
its designations.

## 2. The atom — the rectangle

There is **one** primitive: a connected axis-aligned rectangle on the cell grid. "Bar," "Box," and "stub"
are not three things — they are aspect-ratio *reads* of the same atom (elongated, square-ish, short). The
solid hub is a Box that is also a Bar; a ring's legs are stubs; a wool lane is a Bar. Everything downstream
is rectangles — which is already true in code, where every emitted piece is an `int[] Rect`. Aspect ratio is
cosmetic; it never decides identity.

## 3. The generative rule — recombination *with alignment*

Shapes escalate by snapping rectangles together — but only **along shared edges**. Alignment is the
load-bearing half of the rule, not a detail: two rectangles join along an **edge interval** (a position and a
width), **never at a bare corner**. A corner-only touch connects nothing and reads as nonsense.

This is not a new law — it is three things we already have, seen as one:

- it is the classifier's **diagonal-pinch / corner guard** (`Cells.HasDiagonalPinch`, G79) — a bare point
  never connects;
- it is the **interface** primitive (`map-generation.md` §1.5) — "always a shared edge interval, never a
  point";
- it is what `ShapeEmitter` already asserts piece-by-piece — "every segment abuts its neighbour along a full
  corridor-width edge."

So **alignment and interface are one concept at two scales** — edge-interval joins all the way down, whether
two rectangles inside a shape or two boxes on the board. The degrees of freedom *within* alignment are
exactly the emitter placement knobs (**G50–G52**): where along the edge a join sits (offset / entry shift)
and how wide it is (attachment width). Slide or widen along an edge — legal. Break the edge contact —
nonsense. That is the whole envelope.

**The letter notation encodes the alignment.** I, L, Z, U, H, E, F are letters because the glyphs *are*
edge-aligned arrangements of bars — the alphabet is a ready-made, human-legible vocabulary for "these
rectangles, aligned this way." Naming a shape by its letter is naming its alignment.

**But a letter names a _placement_, not the topology.** Because a join is free to slide and widen along its
edge (the knobs above), the same body reads as different letters as its pieces move: a Staple's legs slide and
it reads U → Π → F; a scythe's endpoint slides and (before G79) it read Scythe → Z; and the movable stub arm
is why the family we called **H is really Y** — a fixed lowercase-`h` glyph implies an arm that cannot move.
So **identity stays topological** (the classifier keys off bends / fold / branch / void, never a glyph), and
the **letter is notation** — the human-legible name of one canonical placement. This is the G79 fold lesson
generalized: name by the invariant, read the letter off the placement.

## 4. Feature axes (width-independent)

What each added rectangle can contribute — the axes the classifier reads, none of them keyed to absolute
size:

- **bends** — reflex corners of the outline: `0 · 1 · 2 · 3+`
- **fold** — the body doubles back on itself (a row or column crosses it in two runs): `no · yes`
- **branch** — a run splits into legs off a shared bar: `0 · 1 · 2+`
- **void** — an enclosed hole: `0 · 1+`

Width is **orthogonal**: the interface width (`w2/w4/w6`) decides whether a body reads as a corridor or an
area (a plaza/block is just a Bar at area-width) — it never changes which shape it is.

## 5. The escalation — named compounds

Each step adds one rectangle (or a few) and earns a feature. The set is **open** — F/E and the double-hole
are simply the next builds, expressible because the atom composes.

| Built from | Feature added | Compound | Letter | Current family |
|---|---|---|---|---|
| one rectangle | — | **Rectangle** (a spine) | I · □ | I · the solid hub |
| a spine + **K perpendicular arms** | branch | **Spine + arms** | L·T (1) · U·Π·F (2) · E·Comb (3+) | L · the U · Y bodies |
| a spine with two opposing bends | staircase | **Zig** | Z · S | Z |
| a bar folding back over a bay | fold | **Hook** | — | Scythe |
| four bars around a gap | void | **Ring** | O · ◻ | Donut |
| Ring + a **U** (both legs on the ring) | 2nd void | **Double-hole** | — | hub |

Three representative bodies (terrain `t`, void `·`), terminal-free:

```
 Spine+2 arms (U/Y)     Ring (donut body)     Double-hole (Ring + U)
   t t t                 t t t t                  t t t t t
   t · t                 t · · t                  t · t · t
   t · t                 t t t t                  t t t t t
```

The double-hole is a Ring with a **U** set inside it — both of the U's legs on the ring edge, the U free to be
smaller than the ring and to slide along that edge as long as both legs keep contact — the U's bay becoming the
second void. The Lego point: custom shapes recombine into greater ones, and the classifier reads the result
(two enclosed voids) without a new special case.

**Further holed recombinations (mostly future frontline).** A **P** is a U closed by a *longer* I whose ends
overhang the legs — a ring with a tail (`P`/`b`/`d`), the I always in contact with both legs. **Two U's on one
I** share a baseline into two loops. Both are the same pieces (U, I) recombined into holes; these matter for
the frontline more than the hub.

**The branch row is one family — a spine plus K perpendicular arms.** L/T are one arm (at the end → L, in the
middle → T), U/Π/F two, E/Comb three or more; the letter is a placement-read of (arm count, where the arms
sit), so it drifts as the arms slide (§3) while the family holds. That is why Ell, the old Staple, and Comb
are the single "Spine + arms" row above, not three.

## 6. The designation layer

A designation finishes a compound into a placed box. **The terminal-relative distinctions live here, not in
the shape** — which is why one Staple serves both U and H.

- **Approach** (wool, spawn) — the compound + an **`entry`** (the rectangle/edge a host docks) + a
  **`terminal`** (the room it leads to). Direction is entry → terminal. Refinements:
  - Staple + terminal *flush on the bar* → **U**; + terminal *on an added, slidable stub* → **Y** (renamed
    from H — the stub arm moves, so a fixed `h` glyph misleads).
  - any body + *no terrain reaching the terminal* → **Isolated**.

  **Terminal faces — a designation knob of its own.** A terminal is a compact room with four faces; the
  approach designation records **how many, and which, _distinct_ faces terrain docks onto**. One face is the
  ordinary approach (a lane caps the room on one side). Two distinct faces is the **clamp** (§7); three a
  **T-terminal**, four a **cross** — legitimate escalations (a wool reached from three or four sides), the
  same multi-dock idea as the hub's per-edge interfaces, applied to the terminal. It is always _distinct_
  faces, never one face docked at several points (§7). This is **kind-agnostic**: a **spawn** room can be
  clamped exactly as a wool can (the spawn is a terminal-capped approach too), so the spawn's profile is not
  just {I, L} — the clamp is a property of the terminal, not of the objective that sits on it.
- **Hub** — the compound + **per-edge interface widths**, *no terminal*. It is the constraint source: it
  emits first, and its edge widths set the neighbours' fill menus. Working form menu: **Rectangle** (the solid
  hub today), **L**, **U** (a hub with a bay), **Ring** (HB4, the ring-with-hole), and **Double-hole** —
  compact, optionally-holed bodies. Deliberately *not* Zig, Hook, or the higher combs: a hub stays
  rectangle-ish.
- **Frontline** — the compound + one edge marked the **`face`** (where the fanned images meet), *no
  terminal*, docking the hub on the opposite edge and driving `mid = f(frontline)`. **Rotation is fixed by the
  designation**: the shape docks the hub with its **spine**, and its **arm-tips point toward the axis** —
  those tips are the face (unlike an approach, which reorients its mouth freely). Forms:
  - a plain **Bar** — the wide face (FR6): docks the hub on one long edge, the far edge is the face, no arms.
  - the **branch family** (Spine + K arms, §5): I-bars standing off the hub — single strand (FR3/FR4) is
    K=1, twin (CT8) is K=2, generalizing to K arms; the spine docks the hub (or, when the bars stand
    individually, the hub itself is their crossbar), the arm-tips are the face.
  - the **holed forms** (§5): **1–2 U's on an I** (P / twin-loop) — a *closed* recess where the twin's is open.

  How the **mid** attaches to the face is a separate rule-set, deferred — that is where `mid = f(frontline)`
  gets cashed out.

## 7. Shared docks — the clamp and the twin (not shapes)

Most compounds are **self-connected** — their rectangles abut each other directly. Two things we have called
shapes are not: they are **several docks onto a shared piece**, and they live in the designation layer, not
the shape taxonomy.

### The clamp — a compact terminal docked on two *distinct* faces

A clamp is **one compact wool room docked on two distinct faces**. The wool is genuinely *clamped* — a cut
cell: remove it and the terrain falls apart. There are three legal forms, by which pair of the room's faces
the terrain takes (`t` terrain, `w` wool room, `·` void):

```
  opposite faces (centered)      adjacent faces (corner)      adjacent faces (corner)
        t · t                          t · t                        t · t
        t w t                          w t t                        t t w
```

- **two opposite faces** → the centered clamp — a straight bar into each side (**I-SideTuck + I**);
- **two adjacent faces** → a corner clamp — and *this* is why the corner case is **L + I**: two straight bars
  on adjacent faces would meet at the room's corner and corner-touch (nonsense, §3), so one bends into an L to
  reach around.

**The room is never a long bar docked along one edge** — the case the old prose blurred, and it must be crisp:

```
   NOT a clamp — one face docked twice
        t · t
        w w w
```

Here the room is a *long bar* and both stubs sit on its single top face: **one face docked at two points**, so
the wool is perched on two stubs, not clamped. A long wool room is not wanted, and this is not the clamp
topology. The emitter and deriver already do the right thing — they only ever emit and read the compact,
distinct-face clamp; this note only holds the *words* to the same standard as the code.

So the clamp is not a body to place — it is a **terminal-face designation** (§6): a shared compact room, two
distinct faces docked. It escalates with no new family to **three faces (a T-terminal)** and **four (a cross)**
— a wool reached from three or four sides. The code already treats it this way: `FamilyDock.Of(Clamp)` demands
two entries, and G63/G80's *corner-wrap* is two hosts taking one entry each.

### The twin frontline — two bars sharing a host

The twin frontline is two separate **Bars**, each docked to the host **individually, not to each other**; the
gap between them is the face/CT8 recess. It shares a *host* where the clamp shares a *terminal*, but both are
the same kind of thing — multiple docks the partition graph places (`FamilyDock`, the corner-wrap), not a new
kind of shape.

## 8. Slots — structural vs designation

Slots are load-bearing: the `entry`/terminal abstraction is what made the placement knobs and docking
expressible in the first place (how an entry shifts, widens, and drags the tail with it). We keep the leap
and split what it conflated:

- **Structural slot** — a rectangle's role *in the compound*: `spine/run · crossbar · leg · ring-arm ·
  stub`. Intrinsic to the shape; shared by every designation.
- **Designation mark** — stamped by the box kind: `entry` (the docking rect/edge), `terminal` (the room —
  approach only), `face` (the meeting edge — frontline only).

Today's `ApproachSlots` (`entry · run · bar · leg · room`) is `run`/`bar`/`leg` (structural) + `entry`/`room`
(designation), merged because the taxonomy started from wool approaches. The knobs currently hang off
`entry`; re-expressed, they become operations parameterized by *whichever rect the designation marks as
entry* plus its structural neighbour (the spine it feeds) — so the identical shift/widen/tail-follow logic
drives a wool mouth, a spawn mouth, a hub interface edge, or a frontline face. Byte-identical wool/spawn is
the test that it stayed general.

## 9. Sketch — the clean emitter (proposal, not built)

Today `ShapeEmitter.Emit(family, W, H, cw, …knobs)` returns an `EmittedShape` with the room baked in. The
clean move is two stages:

1. **`Body(compound, W, H, cw, …)`** → structural-slotted rectangles + vacancies. Pure recombination with
   alignment; no room, no marker, no ids.
2. **A designation pass** — `Approach(body, mouth, …)` stamps the terminal + entry and runs the placement
   knobs; `Hub(body, edgeWidths)` reads the interfaces off the edges; `Front(body, faceEdge)` marks the face.

Pressure-testing this on the hard cases:

- **U vs Y** — one Body (a Staple: crossbar + two legs). The approach designation makes it **U** (terminal
  flush on the bar) or **Y** (terminal on an added stub). U vs Y becomes a *designation parameter*, matching
  the classifier's own "one extra piece is the whole difference." ✓
- **Clamp** — it is **not a Body at all** (§7): there is no clamp shape to emit, only a compact terminal docked
  on two distinct faces by two ordinary bars (I + I opposite, L + I adjacent). So it never enters the `Body`
  stage — it is a **terminal-face designation** the partition places once the multi-dock corner-wrap (G63/G80)
  lands; until then the single-emit clamp stays the buildable preset for the opposite-face case. This retires
  the "two-bar body with a connector slot" framing of the previous draft: every Body is now genuinely
  self-connected, and the clamp's structural terminal lives entirely in the designation layer where it belongs.

## 10. Lineage to the code

- `ShapeFamily` (enum) — stays as the compound taxonomy; the question is only naming (letters vs descriptive,
  §11). `Isolated` and `Clamp` are the exceptions: both are *terminal designations*, not compounds, and
  migrate out of the shape enum into the designation layer when the multi-dock work (G63/G80) lands.
- `ShapeEmitter.Emit` — splits into `Body` (§9 stage 1) + the approach designation (stage 2).
- `ApproachSlots` — splits into structural slots (shared) + designation marks (per kind).
- `ShapeClassifier` / `SlotAssignment` — the mirror reads the Body's topology; it already keys off
  width-independent features (bends/fold/branch/void), so it needs no shape-specific additions to read new
  compounds (the double-hole reads as two voids for free).
- **G88 (hub)** adds the Hub designation + its form menu; **G89 (frontline)** adds the Front designation +
  its form menu. Both reuse the Body stage and the structural slots unchanged.

## 11. Open decisions

1. **Naming** — *decided:* keep the letters; add descriptive names for the non-letter compounds (Hook, Ring,
   Double-hole); **rename H → Y**; and **collapse Ell / Staple / Comb into one "Spine + K arms" branch family**
   (§5), the letters L/T/U/Π/F/E placement-reads of (arm count, arm placement).
2. **Hub form menu** — *working set (§6):* Rectangle · L · U · Ring · Double-hole (Ring + a slidable U).
   Open: whether HB4 is a plain Ring or a Ring with a walkable interior (a different void class), and the
   hub's per-edge interface-width rules (the constraint it sources).
3. **Frontline form menu** — *decided:* the **branch family** (Spine + K arms — single = K1 / twin = K2 /
   more) plus the **holed forms** (P, two-U-on-I), plus a plain **Bar** for the wide face (FR6). Rotation is
   fixed by the designation — the **spine docks the hub, the arm-tips are the face** toward the axis. How the
   **mid** attaches to the face is a separate, deferred rule-set.
4. **Comb / Snake** — do we emit Comb (E/F) and Snake (3+ bends) now, or park them until a kind needs them?
5. **The clamp** — *decided (§6/§7/§9):* the clamp is **not a shape** but a **terminal-face designation** — a
   compact room docked on two *distinct* faces (opposite → centered / I+I, adjacent → corner / L+I), escalating
   to three (T) and four (cross). U and Y stay shape families (a Staple + a one-face terminal). Open only: when
   to schedule the multi-dock corner-wrap (G63/G80) that replaces the single-emit preset.
6. **Mirror scope for hub/frontline** — do their shapes close the emit↔derive loop, or do they only ever emit
   (label-drives, no derive), as `MidCarver` already treats frontline faces?
