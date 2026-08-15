# The Shape catalog

## What it is

The shape catalog is the generation vocabulary made browsable: every shape the composer can put in a box,
emitted once and drawn. Its route is `/catalog`, and it is reached from the generator's top bar, which is the
relationship between them — the catalog shows the pieces, the generator shows the boards those pieces make.

It is a **read-only** page. Nothing on it is stored, nothing it shows can be edited, and no board is composed
by it. What it produces is a picture and, in its second half, an answer: given a family, a box and a set of
knobs, what does the emitter build — or why does it refuse.

That second half is the reason the page exists rather than a folder of renders. The grid is a gallery and a
gallery can only assert; the knob panel goes through `BoxFiller`, which is the same entry point composition
fills a wool box through, so the profile check and the docking gate run exactly as they do in a real compose
and **a refusal comes back as a result rather than an error**. It is the only place in the studio that
surfaces the fill guards at all.

The shape model itself — what a body is, what a designation adds, why the classifier reads width-free — is
`docs/generator/model.md` §4, which governs. This document is the page: what it shows, what it can be asked,
and what it cannot say.

## What it shows

Three box kinds appear, and the split is by what the shape is *for* rather than by what holds it.

A **wool approach** is a terminal-capped shape: a lane from a host that dead-ends at the room the wool sits
in. A **hub body** is the unit's constraint source — terminal-free, and the widths of its free edges set every
neighbour's menu. A **frontline body** is the join toward the symmetry axis — also terminal-free, with one
edge designated the face. The spawn box is absent as a row of its own because a spawn is an approach too: it
reuses the same families at the map's lane width, differing only in what the room holds and how big it is.

The **nine approach families** are an escalation, not a flat set — an L whose lane doubles back is a scythe, a
scythe whose bay closes is a donut, a clamp whose wool docks flush is a U, a U that lifts its wool onto a stub
is an H:

| Family | Reads as | Minimum box (w2) |
|---|---|---|
| `i` | a terrain lane caps the wool inline | 2×3 |
| `l` | one bend — terrain reaches the wool from two adjacent sides | 5×4 |
| `z` | a staircase of two opposing bends, no bay | 4×8 |
| `scythe` | a fold that wraps an open bay beside the wool | 8×6 |
| `clamp` | the wool bridges two otherwise-separate bars | 6×4 |
| `u` | two legs meet a crossbar, the wool flush on it | 6×6 |
| `h` | two legs meet a crossbar, the wool on a stub lifted off it | 6×8 |
| `donut` | terrain encloses a void — a full loop, multi-access | 5×10 |
| `isolated` | wool ringed by void — a reading, never emitted, and absent from the page | — |

The minimums are the emitter's own, reported by `GET /api/shapes/probe/schema` in the **dock frame** rather
than the emitter's canonical one: the panel fills through a mouth, and a lateral-mouth family like the donut
transposes between the two, so a schema quoting the canonical box would put 10×5 in the chip while a refusal
said 5×10.

They are also the minimum for the family **with no knobs set**, and a knob can move them in either direction.
The donut is the demonstration: bare it wants 5×10, and with `woolAtEnd` — the variant integrating the wool
into the ring's own corner rather than hanging a room off the end — it fits 5×8, because the terminal sits
inside the ring's span instead of beyond it. The `too-small` refusal always reports the minimum **for the
knobs asked for**, so the number to build against comes from the refusal rather than from the schema.

The **bodies** are the two terminal-free menus. A hub may be a solid `bar`, the branch family at one arm
(`single`) or two (`twin`), a `ring`, a `p` (a loop with an overhanging bar), a `double-hole` (a ring plus a
docked U) or a `g` (a ring plus an L, whose open bay a docking frontline seals into a second hole). A
frontline may be a `bar` — the wide face — or the branch family at one arm or two. The two menus overlap in
their tokens and the page's Family filter is one flat list, so picking `bar` shows the hub's and the
frontline's together.

Beside the family sit the **knobs**, the variation the model allows without leaving the family: a `flip`,
available everywhere, turning the shape's handedness; `sideTuck`, which turns the room off the end of the last
segment perpendicular, built for I, Z and the scythe only; `woolAtEnd`, the U/H/clamp/donut variant putting
the terminal on an end rather than the middle; and `attachW`, the donut's and the scythe's hub-entry width.
The schema endpoint reports which knobs each family takes, so the panel greys the rest rather than sending a
combination the emitter would reject.

## Reach — how far a shape actually gets

Every card is badged with the last stage that admits it, and this is the page's whole honesty contract. A
catalog that drew every emittable shape as "what the generator makes" would assert a vocabulary the boards do
not carry.

| Tier | Means |
|---|---|
| **in the mix** | A sampler draws it, so generated boards really contain it. |
| **reachable** | `BoxFiller` fills it and the menu lists it, but no sampler ever asks for one. |
| **emitter only** | Only a direct emitter call builds it — the family is off the production menu, or the knob is one the fill path drops. |

The in-mix set is **collected by running the sampler**, never by restating its laws: the catalog sweeps the
composer's own wool request over four thousand seeds and a grid of the two inputs it reads, and keeps the
distinct requests. Retuning a chance or a cap in `UnitTuning` therefore moves the catalog with it, and there
is no second copy of the mix to drift. The reachable set is whatever the production menu lists that the sweep
never asked for, drawn at its minimum box; the emitter-only set is the families the menu excludes plus the
five knobs the composer's fill path does not forward — and each of those carries the reason it stops where it
does, so the badge is never bare.

Two entries are worth reading for what they say about the composer. The **Z** is reachable and never drawn: on
the fill menu, filled correctly, asked for by nothing (`G146`). The **scythe** is emitter-only for a stated
reason rather than a taste — its bay's mouth is its own docking edge, so a flush dock seals the bay against
the host into a walled void, which is exactly the motif `WL8` forbids; the elevation alternative that would
admit it is `G81`.

## The grid

Cards are laid out in pipeline-legibility order — straight, bent, branch, enclosing, then the bodies — so the
grid reads as a progression rather than alphabetically. Each carries its family and tier as badges, the shape
itself, and a foot naming the box it was emitted at, the corridor width, and any knobs set. A non-in-mix card
adds the note explaining its tier.

Three filters narrow it: box kind, reach, and family. Each is any-of within itself and all-of across facets.
The counts on the chips always describe the **whole** catalog rather than the filtered slice, so a chip says
what it would show before it is picked.

**Everything is drawn mouth-up.** The composer docks all four box edges, but the other three orientations are
rigid motions of what is drawn and would pad the grid fourfold without adding a shape. Both handednesses are
kept, because a flip is an authored variant rather than a viewing angle. Approaches are then deduplicated by
cell pattern, so two parameterizations that occupy the same cells are one card.

The catalog is a **bounded set** — a pure function of the emitters and the tuning constants — so the whole of
it is fetched once and filtered in the page, which is what makes every chip instant and needs no cursor. Today
that is 98 cards: 89 wool approaches, 6 hub bodies and 3 frontline bodies, of which 91 are in the mix, 1
reachable and 6 emitter-only. Those numbers move whenever the tuning does.

## The knob panel

Clicking a card opens the panel seeded from that exact shape, so editing starts from a known-good state rather
than a guessed box. It carries the family, box width and height, corridor width, the mouth edge, the three
boolean knobs and the attachment width, and re-emits on every change. Requests are serialized by generation
counter, so a fast drag cannot land an older answer over a newer one.

The emitted shape stays pinned at the top while the knobs scroll under it, because every knob is judged by
what it does to that shape. Switching family drops any knob the new family does not take rather than carrying
it along, so a refusal is always one the author asked for; a `too-small` refusal offers a *Resize* button that
takes the emitter's stated minimum verbatim rather than nudging one axis at a time.

On success the panel reports the land the fill produced, in cells, and the **slot sequence** of the pieces it
laid — `bar · entry · entry · room` for a U, `entry-bar · leg · leg · entry · room-bar · room` for a donut.
That sequence is the family's template, fixed per family and only resized, and it is the most useful thing on
the panel for anyone reasoning about a shape: a slot is a template position rather than a property of a
rectangle, and it is what every composition rule is quantified over.

## What it refuses

A refusal is a first-class answer, carried in the same shape as a success and rendered in place of the shape.
The detail is the emitter's or the gate's own words rather than a rewording, since a reworded guard is a
second copy free to disagree with the one that fired.

| Code | Means | Carries |
|---|---|---|
| `too-small` | Below the family's minimum box at this corridor width | `minW`, `minH` — the panel offers a *Resize to w×h* button that takes them verbatim |
| `not-on-menu` | The family is not on this box kind's fill menu | the menu it does offer |
| `illegal-dock` | The docking gate refused this mouth for this family | the gate's reason |
| `unsupported-knobs` | The emitter rejects this knob combination | the emitter's detail |

In practice only the first two are reachable from the page, and that is worth knowing rather than guessing at:
the emitter orients every shape to face the mouth asked for, so no family-and-edge combination the panel can
express produces an illegal dock. The gate is wired through and runs on every emission; the panel simply
cannot pose it a question it answers no to.

## The API

Both endpoints are anonymous, rooted at `/api`, and take no map.

| Endpoint | Answers | Fails with |
|---|---|---|
| `GET /shapes/catalog[?kind=&tier=&family=]` | `{shapes, total, byTier, byFamily, byKind}` — every card as `{id, kind, family, tier, boxW, boxH, corridorCells, knobs, note, svg}`. Filters are CSV and narrow the returned cards; the tallies always describe the whole catalog | — |
| `GET /shapes/probe?family=&w=&h=&cw=&mouth=&flip=&sideTuck=&woolAtEnd=&attachW=` | `{svg, rejection, landCells, slots}` — one emission through `BoxFiller`. A refusal is a 200 with `rejection` set and `svg` null | 400 unknown family |
| `GET /shapes/probe/schema` | `{families, mouths, minCorridorCells, maxCorridorCells}` — per family its token, its minimum box in the dock frame, whether the production menu admits it, and which knobs it takes | — |

The probe clamps rather than refuses out-of-range numbers: box width and height to 1–64, corridor width to
1–8, attachment width to 0–8, and an unrecognised mouth falls back to `Top`.

## Driving it without the UI

The catalog is the cheaper of the two to read from a script, because one request is the whole vocabulary:

```
GET /api/shapes/catalog                         → every card, plus tallies by tier, family and kind
GET /api/shapes/catalog?tier=in-mix&kind=wool   → only what boards actually carry
```

The probe is the one to reach for when a question is about whether something *can* be built. Asking it is
cheaper and more reliable than reading the emitter's guards, because it is the emitter:

```
GET /api/shapes/probe?family=donut&w=3&h=3&cw=2
  → { "svg": null, "landCells": 0, "slots": [],
      "rejection": { "code": "too-small", "minW": 5, "minH": 10,
                     "detail": "Below Donut's minimum box of 5×10 cells at this corridor width." } }

GET /api/shapes/probe?family=u&w=8&h=8&cw=2&mouth=Top
  → { "landCells": 36, "slots": ["bar", "entry", "entry", "room"], "rejection": null, "svg": "<svg …>" }
```

Fetch `/shapes/probe/schema` once and drive from it rather than from a hard-coded family list: it names every
family the emitter builds, its minimum box, and the knobs it accepts, so a family gaining a knob does not need
a matching edit in the caller. Both endpoints answer **SVG text inside JSON**, so an agent gets markup it must
render to look at; the numbers — `landCells`, `slots`, and the rejection — are what can be read directly.

The question this page does *not* answer is whether the composer could have produced a particular box. That is
`POST /api/plan/feasibility`, which takes a plan document as its body, searches the declared menus by calling
the real emitters, and reports the nearest miss when nothing reproduces a box. `plan.md` has it.

## Limits

**The grid's proportions are not the boards' proportions.** Cards are distinct *shapes*, and the sampler that
varies a donut's box dimensions and attachment width most produces the most distinct shapes — 73 of the 98
cards are donuts. The census the generator reports says the opposite about real boards: an I is on three
boards in four at twelve players and nine in ten at twenty, and it gets five cards. The page is a vocabulary,
never a frequency.

**The hub's two-arm form has no card.** `SpineArms(2)` is on the hub menu and the generator's own census finds
it on 28 of 400 boards at twenty players, but the catalog emits every body at a fixed 16×12 box with no arm
layout, and the default layout on a 16-wide spine wants two 6-cell legs where the leg-width cap allows 5 — so
the form refuses and the card is dropped. The grid therefore shows six hub forms where the composer builds
seven. The frontline's twin is unaffected because its own sampler admits end recesses.

**The corridor slider spans widths the menu does not fill.** It runs 2 to 5, but the width-to-menu table has
families only on its `w2` row: `w3` tapers back to `w2` and fills, while `w4` and `w5` answer `not-on-menu`
with an *empty* menu — rendered as "it offers ." The refusal is correct and says so badly. The wide rows are
recorded in the data on purpose, since a wide touch resolves into multi-shape patterns rather than a wider
lane, and those patterns are not emittable.

**Only the wool box is probeable.** The panel builds a wool box and fills it, so hub and frontline bodies can
be looked at in the grid but not driven — there is no knob panel for a ring's wall widths or a frontline's arm
layout, which are exactly the axes that multiply those forms.

**Nothing here says a shape is good.** The catalog states what is buildable and how far it reaches; whether a
family suits a board is the evaluator's, the rules', and past those a human's.
