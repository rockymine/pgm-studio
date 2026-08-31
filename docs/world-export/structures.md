# Adaptive structure stamping — footprints, pads, styles (G31)

This folder holds the contracts for the **realized world** — what the export writes into the Anvil
world beyond raw terrain — the sibling of `docs/generator/` (which owns the layout) and
`docs/pgm/` (which owns the map contract). Later passes over the same surface (map
theming and material palettes, G34) belong here too.

**Status: implemented (G31).** Rule ids here are `WX*` (world export); the resolver is
`PgmStudio.Domain.RoomFrames`, and one `RoomFrame` per room feeds the stampers
(`CubeStamper`/`SpawnStructureStamper`/`WoolChests`), the structure preview, and the exported points
alike. Read alongside:

- `docs/world-export/sketch-world-export.md` — the shell contract (layer scheme, chest loadouts,
  monuments' derivation discipline, `level.dat`); its §2/§5 defer to this file for sizing and
  anchoring.
- `docs/generator/rules.md` ST1–ST5 — the stamped-structure law; ST1/ST2 carry the footprint rule.
- `docs/pgm/destroyables-and-cores.md` — the style-as-data precedent (§5 below).
- `docs/tools/sketch.md` — the tool that binds the shells (its Theme phase) and stamps the same house as a
  dressing prop.
- `docs/tools/capabilities.md`'s renderer section — every structural stamp here, and the house `decoration.md`
  §8 stamps, claims `WorldProvenance`'s `Structure` layer as it is placed, which is what lets a stage image
  and `--structures` read "built" from a recorded fact rather than from a block's material (`B133`).

**Two stamp concepts, not one.** A *structural* stamp — the spawn building, the wool cage, the bedrock
approach wall, an objective marker — is objective-defining and generator-emittable, so it is authored in the
Plan and Configure tools and precedes the `map.xml`. A *dressing* stamp — a house, a boulder, a tree — is
placed in the Sketch tool's Dressing phase and defines nothing. The finishing passes therefore **read** the
structural stamps rather than owning them: the painter touches only stone, so a room is never repainted (TP6);
the dressing pass keeps off what the map is played through — the intent's spawns, objectives and the
structures stamped for them, plus any column whose surface is not the terrain's own (`DressingScope`); and the
sketch canvas shows the pieces as locked annotations, so the ground around one can be refined without the
piece being moved. What a stamp is made of is shared — both kinds come from the same room-style library and
the same `HouseStamper` — but who authors one is not.

**Every `WX` id is declared in `RoomFrameRules` (`PgmStudio.Domain.RoomFrames`), and this section is the
account rather than the declaration.** The constant is where a rule's own two sentences live — what it holds
and what to do about it — because that is what `GET /api/rules?family=WX` answers and what a finding's id
resolves to; a document that declared a rule the code did not would put the law somewhere no caller can
reach. What stays here is the part a docstring has no room for: the negotiation `WX8` runs, the parities
`WX3` turns on, the reasoning behind each number.

---

## 1. The frame

Every wool cage and spawn room is stamped from one resolved **`RoomFrame`**: the shell footprint,
the interior, the pad, and the doors, derived by `RoomFrames.Resolve` from three authored inputs —
the **piece rect**, the **marker**, and the **entry interfaces** (a spawn substitutes its
yaw-derived door edge). The world builder and the plan editor's structure preview consume the same
frame (`WorldBuilder.WoolFrame`/`SpawnFrame`), so the drawn box and the stamped shell cannot
disagree — the OB8 discipline the destroyable/core boxes established.

The frame's inputs ride the intent per orbit image: the compiler attaches the role piece's fanned
rect (`WoolIntent.Piece`/`SpawnIntent.Piece`) and the wool room's fanned entry interfaces
(`WoolIntent.Entries` — terrain↔room land seams plus build-zone frontline edges, as degenerate
rects on the piece boundary). A wool or spawn marker on a **plain** piece carries no `Piece` and
keeps the legacy marker-anchored default room — the frame a 10×10 piece would resolve to, with a
door per wall on the cage — so hand-authored and sketch-origin intents behave as they always did.

Two authoring facts frame the rules. The plan format is generic over scale: `PlanGlobals.Cell` is
any blocks-per-cell value (default 5), so a room piece has no fixed block size and every minimum
below is stated in blocks. And markers snap to the **half-cell lattice** (`plan-doc.js snapHalf`):
a whole piece-relative offset is a cell corner, a `.5` offset a cell centre — so a marker's block
coordinate is either a **grid line** (integer) or a **block centre** (`.5`, cell centres at odd
cell sizes), and the export carries that parity through (`PositionSnap.SnapHalfXZ`) instead of
rounding it away.

## 2. The footprint rule

- **WX1** *The footprint is stated, and its default keeps ground in front of the door.* A wool-room or spawn
  piece carries a shell whose footprint is the **room the plan states**. Where it states none the piece is
  inset by **one block** on every side — the ring of clean floor a piece promises — and by up to **seven** on
  the side the door opens through, which is the largest iron cube plus the standing room it holds to the wall.
  A 20×20 spawn piece facing −z therefore opens as an **18×12** room with somewhere for its iron to stand,
  rather than as an 18×18 room the cube has to make the shell shrink for. A wool room takes the plain
  inset: its entries come from whichever sides abut it, so it has no one side to keep clear, and no iron.

  **The door's gap yields to the marker.** A marker is where a player arrives and the pad is derived from it,
  so a default that pushed the room off its own marker would move the exported spawn point on a board nobody
  had touched. The gap takes only what leaves the marker seated where it already sat, down to the one-block
  ring — which is why an existing 10×10 spawn piece keeps its point and simply gets a shallower room.

  The shell's orientation is the rect's own; the fanned rect orients the orbit images.

- **WX2** *Minimums are measured in blocks, never cells, and a wall is what the second one buys.* The
  smallest room there is measures **4×4** — a 2×2 pad and the block of clear floor it keeps on every side,
  which is the same ring its four chest corners seat in. A **shell** adds the one course of wall it stands in
  on each side, so a footprint carrying one is at least **6×6** (a 4×4 interior — four monument corners, the
  chests, and a pad). The two are one derivation rather than two numbers: `MinRoomSpan` plus `WallCost` where
  a wall stands. Which of them binds is therefore a question about the **binding** (§9) and not about the
  plan, so the plan gate refuses only what no binding could save and the smaller floor is the one it asks.

- **WX12** *A footprint stays inside the piece it stands on.* A piece is one rectangle at one surface, so a
  footprint inside it is on ground by construction and crosses no interface; one reaching past it is over
  whatever the neighbour happens to be, or over the void. This is what makes "may not overhang the void" and
  "may not cross an interface" one containment test rather than two derivations.

## 3. The marker and the pad

The marker is not the structure anchor; it is what it semantically reads as — the wool/player spawn
point inside the room, realized as the floor pad. Placement lives on the half-cell lattice; the
marker is never freely placeable.

- **WX3** *Parity picks the pad class, and the pad is always square.* A marker on a block
  **grid line** takes the **2×2** pad straddling it — the only size for that parity. A marker on a
  **block centre** takes a **3×3** pad when the room affords it (WX4), else **1×1** — the degrade
  applies to both axes together, so a 1×3 never exists. Nothing larger than 3×3 exists. Parity must
  match on both axes: a mixed-parity marker (a grid line in x, a block centre in z) has no square
  pad and is **refused at validation**, pointing at the two legal lattice choices. The composer
  never emits one: a room an odd number of blocks across on one axis has a mixed centre, so
  assembly nudges that axis half a block onto a grid line (`Composer.MarkerOffset`).

- **WX4** *Clearance and minimal shift.* The pad keeps **at least one block of clear floor to every
  wall**. A marker too close to a wall (the sharp case: cell 4, marker at the centre of a corner
  cell — two blocks from the piece edge, which is the interior's boundary) has its pad shifted
  **inward by the minimum** that restores the clearance, landing it just off the corner, never flush
  in it. A 3×3 is chosen only when it fits with that clearance after the shift.

- **WX5** *The pad is the point.* The exported spawn/wool location is the (possibly shifted) pad
  centre — the world is the ground truth the XML must agree with, the same discipline as the derived
  monument locations. The export snaps markers to the half-block lattice, never to integers, so
  parity flows through. PGM absorbs both parities: player spawn points are free vectors, and the wool
  `<location>` flooring convention names exactly the block a centre-parity pad occupies
  (`floor(3.5) = 3`). The structure preview draws the shifted pad, and a shift is surfaced as a
  lint note, so the author is never surprised the point moved.

## 4. Entries

- **WX6** *Doors sit on the entry interfaces, and a build zone is one.* The wool cage's doors are
  cut where the room is actually entered: a terrain↔room **land seam**, or an **abutting build
  zone** — players bridge in through the build region, so that interface carries a door and the ST1
  entrance redstone line exactly as a land seam does. Doors are never centred one per wall: a long
  room with four centred doors would open two of them into the bedrock ring. A room with **neither**
  interface is genuinely unreachable and is refused at validation. The spawn cube keeps its single
  yaw-derived door (the yaw already fans per orbit image).

- **WX7** *Door width follows the door wall — one rule for both kinds.* An **odd** interior wall
  centres an odd **3-wide** door — never 1-wide, and wider odd doors (5) are a later decision,
  capped at 3 for now. An **even** wall takes the common **4-wide** door once the interior is at
  least **6 across**, narrowing to **2** at the 4-across minimum. There is no per-kind canonical
  width: the wool cage's shipped 2-wide doors widen to 4 wherever the room affords it. The
  invariant behind the numbers: the door is always at least one block narrower than the interior on
  each side (width ≤ interior − 2), so the door-wall corner cells — where a spawn cube seats
  monuments — are never exposed to the outside. Door *height* and room height are the style's, not
  the frame's — §7.

## 5. Iron and placeability

Iron is spawn family — a renewable resource the spawn room exists beside — but it is a separate
structure, and structures never fuse.

- **WX8** *The room yields, the iron degrades, the room wins.* An iron marker on a spawn piece
  stamps its cube **outside the room shell**, inside the piece, with **three blocks of clear air** to
  the wall — the standing room a player has to get round it, never flush, never merged into a
  corner. Fitting is a two-sided negotiation with a fixed priority: the shell pulls **one edge**
  back from its WX1 footprint by the minimum that clears the cube — legal while the shell holds the
  WX2 minimum and the spawn marker stays inside the interior (the pad may still clamp with a WX4
  shift); among legal shrinks the largest retained area wins, ties breaking toward the edge
  farthest from the spawn marker, so orbit images shrink
  mirror-consistently. The cube itself degrades by marker parity — a **grid-line** marker centres
  **4×4**, falling back to **2×2**; a **block-centre** marker centres **3×3**. A marker whose two axes
  disagree centres no square at all, so it takes the whole ladder (**4×4 · 3×3 · 2×2**) and settles half
  a block off centre on the odd axis; refusing it instead is what turned a column of three iron markers
  down one side of a spawn piece into three lints and no ore. The low corner is always the marker less
  half the span, rounded away from zero, which is what keeps a half-block landing symmetric across the
  orbit. The renewables wiring covers exactly the resolved footprints. `RoomFrames.ResolveRoom` owns the
  negotiation; `Composer`-emitted plans and authored plans go through the same resolver.

- **WX9** *Placeability is an attribute, not an exception.* Every iron marker resolves to
  **placeable or not** (`IronResolution.Placeable`) rather than to a refusal. An unplaceable marker stamps
  **nothing** — the room takes its full WX1 footprint — but the marker itself stays on the board:
  validation flags it with the clearance requirement (the WX8 lint), and the preview draws **only placeable**
  structures, so the iso view never shows a cube the export refuses to place.

  Placeability is iron's attribute and not a general one, because iron is the only family whose
  placement is a **negotiation**: the cube walks a size ladder and the room's shell yields an edge to
  clear it, so whether anything can stand there is the resolver's answer rather than the author's. The
  objective-separation rules answer the same question a different way and were never going to carry this
  attribute — a goal against a spawn or a wool room is `OB17`, refused at the gate over the goal's
  footprint, and how far two goals stand apart is `GO2`/`GO3`/`WL7`, reported by `GoalDistances` and
  judged by the evaluator terms.

  The stampers themselves stay heterogeneous on purpose — their inputs are irreducibly different (a
  wall owns a *seam between two pieces*, a room a *piece + marker + entry interfaces*, an entrance
  line a *piece and its neighbours*, an objective a *marker + style over terrain*), so no shared
  base type sits over them. What every family shares is the **output**: an axis-aligned block volume on
  a surface-derived floor.

  Three records state that volume, and they are three questions rather than one concept spelled three
  ways. `IronResolution` is a negotiation's answer — which size won, where the cube landed, whether the
  shell could yield at all. `PlacedGoal` is the gate's — the footprint a rule is quantified over, before
  anything is built. `PlacementClaim` is the world's — the columns a stamp actually wrote, taken from the
  placement rather than rebuilt beside it, which is what lets a claim know what a placement refused. What
  remains of B37 is neither a fourth record nor a merge of these, but that the iron cube is gated on its
  marker block (`ST2`) where a goal of the same size is gated on its footprint (`OB17`).

- **WX10** *A bound shell stands under the build ceiling.* A room style is authored geometry subject to no
  cap of its own, while the goal marker over it hangs `BuildCeiling.MarkerOver` blocks above a ceiling
  `BuildCeiling.OverGround` over the ground — so a tall storey stack swallows the very sign that says where
  the goal is. The shell's height is measured on the **smallest** room there is (WX2's 6×6), since every
  sloped form only climbs further on a bigger footprint: a style refused here has no footprint it could have
  been stamped on. It refuses where the style is **bound** — `PUT /api/map/{slug}/sketch`, 400 — rather than
  correcting at stamp time, because silently shortening a building the author drew is the worse answer.

## 6. The code shape — frame, shell, furnishers, style

The layering follows the destroyable/core precedent — one box function that the world build **and**
the preview both call, so the stamped volume, the emitted region and the drawn box cannot disagree
(OB8) — split into four parts:

1. **The frame** — `RoomFrames.Resolve` (`PgmStudio.Domain`), a pure resolver
   `(piece rect, marker, entry rects | yaw edge) → RoomFrame`: the inset footprint, the interior,
   the pad (after WX3/WX4), and the doors. `WorldBuilder.WoolFrame`/`SpawnFrame` derive it
   (with the legacy default for piece-less markers) and `PlanStructurePreview` consumes the same
   derivation — the preview cannot lie. The floor is the highest surface over the footprint
   (`WorldBuilder.FrameFloor`), which is its own mirror, so orbit images rest level.
2. **The shell template** — `CubeStamper` stamps the frame's footprint: floor + perimeter walls +
   roof, each a course stack its `RoomStyle` supplies (§7), the roof hole proportional with a cap
   (`RoofHoleSpan` — the 8-wide shell keeps its 4×4 hole), then the pad and the doors stamped over
   them.
3. **The furnishers** — `RoomFrames.InteriorCorners` seats the chest stacks and
   `RoomFrames.MonumentSlots` the monuments (door-wall corners, back-wall corners, then the walls
   fill, skipping the door opening). A larger spawn room gains monument capacity from its longer
   walls with no new rules; the validator refuses a plan whose captured-wool count exceeds the
   seats.
4. **The style** — `RoomStyle`, choosing shell materials and decoration, **never the footprint**; the
   footprint always comes from the piece, which is what makes every style adaptive. §7 is the model.

Out of scope for G31: the destroyable/core stampers themselves — they already follow the
style-as-data pattern and stay anchor-based on purpose, a point structure rather than a room.

## 7. The room style

A shell is **parts and overrides**. A wall and a foundation's plate are each a `RoomPart`: a `BandStack` —
bands of material and thickness, read along a distance — plus an `Extent` saying how far the part goes. Over them go
the two things a style may not decide — the **pad**, because the exported wool/spawn location is derived from
it (WX5), and the **doorway**, because it is the entry contract (WX6/WX7).

**A statement about one piece of the building lives on that piece.** What a building stands on is a
`Foundation`, what stands above the eave is a `RoofStyle`, and the way in is a `Doorway` — each one type
carrying every statement about itself rather than a run of fields beside everything else, so a caller asking
about the roof asks the roof. Where a piece has exactly one thing to say it stays a field: a post is a
material and nothing else.

**A field that only looks like part of one is the trap.** Which wall the doorway is cut through is the
building's `Front`, not the doorway's: it is the same wall a `RoofForm.Shed` falls toward and a porch stands
against, so filing it under the doorway would leave a roof asking the way in which way it falls. What decides
a piece's home is the responsibility it answers to, never the prefix it shares.

A stack is read from its part's own base outward: a floor **downward** from the course players stand on, a
wall and a roof **upward**. The direction is the load-bearing part of the model. A floor that grew upward
would lift the pad and move the exported point; walls indexed from the floor are what keep a band at eye level
and a slit near the top when the room's height changes, where the layer indices they replace would have slid.

The **last band repeats** past the end of the stack, so `Extent` moves without the stack being re-authored: a
taller wall grows in whatever its top course is. That is what makes height a knob at all — §4's note that room
height stays out of G31 is answered here rather than through the frame.

**Repeating is the stack's own statement, not the axis's.** Bands along a distance is one rule and the studio
reads it along three: depth from the top of a bucket for a layer stack, courses from the part's own base for a
room part, rings from the edge for a floor's zoning. What is *not* shared between those is what happens where
the bands run out, and assuming it is repeating builds the other case wrong. Repeating is right where the
stack **owns the whole space**, which is what a wall or a plate is: there is nothing under it to fall through
to. Handing over is right where the stack is a band **inside** a larger space — a rim two blocks in and then
the surface, rather than two blocks of rim and then rim forever. So a `BandStack` states its ending
(`BandEnding`) and the caller states the axis, and neither is implied by the other.

**A repeating band does not reseed a pattern.** A pattern's seed lives on the material and its field is
sampled from world coordinates, so the second block of a repeat is one field read a step further along rather
than a second field starting over.

Three fixed layers are ordinary courses, and that is the whole simplification. The coloured band was layer 4
and is a `TeamTintedMaterial` course; the light slit was a skipped layer 6 and is a course of air; and a wool
cage and a spawn cube are two bound styles rather than two code paths. The room's colour reaches a tinted
course through `BucketContext.TeamData`, the tint channel, so one material paints the wool's colour in a cage
and the team's in a spawn.

**Air is a gap, not a block.** A part's air course is skipped rather than written, so no style can erase what
another stamp already placed. A doorway's air *is* written — it cuts the opening out of the wall the same pass
just built. Windows follow the doorway's rule for the same reason. The two are different operations and the
code keeps them apart.

### 7.1 The roof is a height field

Every roof — `Flat`, `Gable`, `Hip`, `Gambrel`, `Shed`, `Saltbox` — is one `RoofField`: for each cell of the
roof's plan, how far the roof's own surface stands above its base there, and how many courses that column
writes to close the step down to its neighbours. The stamper walks that plan once. The forms differ in a single formula over the same two
distances — how far the cell stands from each wall line — and in nothing else:

| Form | Rise over a cell, per block travelled |
|---|---|
| `Flat` | the base course, everywhere |
| `Gable` | the smaller distance across the building's **shorter** side, times the pitch |
| `Hip` | the smallest of all four wall distances, times the pitch |
| `Shed` | the distance from the front wall, **held to the shorter side's run**, times the pitch |
| `Gambrel` | as `Gable`, at pitch + 1 for the first quarter-span in from each eave, then at the pitch |
| `Saltbox` | as `Gable`, but the two distances **across the shorter side** climb at different rates — the front's side at pitch + 1, the other at the pitch |

A `Hip` over a square footprint is a pyramid: the ridge is the run the longer side has left over, and a square
leaves none. A `Shed` and a `Saltbox` need to know which wall is the front; every other form is symmetric and
ignores it. The front is the wall the doors are cut through.

**No roof climbs with the long side of a building.** A gable, a hip and a gambrel are measured across the
shorter side by construction, so the law costs them nothing. The two that face a *front* are the ones it is
written for, because the front is wherever the doors happen to be — which on a hall is as likely to be an end
as a side. Left alone, a 10×60 building would carry a lean-to sixty courses over its wall: a tower with a door
in it, from a footprint an author drew flat.

They are held two different ways, and the difference is what each form is *for*. A `Shed` is one plane falling
toward a chosen wall, so the fall is kept and the climb is bounded: it rises at its pitch until it has risen
the shorter side's worth, then **runs flat** the rest of the way — a lean-to that levels off. A `Saltbox` is a
gable whose slopes climb at different rates, so it is measured across the shorter side exactly as a gable is,
and the front decides only which of the two is the steep one. Bounding it from the front instead does not work,
and the reason is worth keeping: on a hall both of its slopes run the length of the building, each stops at its
own bound long before reaching the other, and two slopes that never meet leave a ridge that is the long side's
after all.

The height that survives is the one the author asked for when they set the pitch. What the long side decides is
how far the roof runs, and nothing about how high it stands.

**Distances are measured from the wall line and are allowed to go negative**, which is what makes the eave
part of the slope: the course over the wall rests on the wall, and every course outward from there keeps
falling at the same rate. Holding the overhang level with the wall line instead — the obvious way to stop the
roof floating a course above it — runs the last blocks flat exactly where the roof is most visible.

Two things follow from the field rather than from any one form. A column writes as many courses as its deepest
step down to a neighbour, so a pitch of two does not leave the slope open between its treads. And **the walls
climb to meet the roof** wherever the roof stands above them: the gable's two ends, the shed's back wall and
both its flanks, and nothing at all under a hip, whose slopes come down to the wall line on every side. That
one rule replaced the gable's own end-wall pass.

What they climb in is the **gable**, a part of its own. Unbound it is the wall's top course carried up, which
is what every shell was before the face had a name — and note that it is the wall's top course rather than the
stack continuing to count, because the courses have run out by then: a wall that bands as it rises goes flat
the moment it turns into a gable, and there is nothing left in the stack to say otherwise. Naming the face
separately is what makes a timbered or shingled gable over a plain wall sayable, which is what nearly every
hand-built house on the corpus does. The **verge** is a different piece again: it is the roof's own outermost
ring, so on a flush roof it is the raking edge directly over the gable, and under an eave it moves out to the
overhang and the rake at the wall line is plain roof.

The roof's remaining knobs are its **hole** — a flat lid only; a sloped roof has a volume of its own and a hole
in a slope is a leak rather than a light — and its **ridge cap**, the line the slopes meet on laid in the verge
rather than in the roof's own material. The hole is measured and centred on the **shell**, never on the roof
plane, because it lights the interior.

**The field is measured in halves, and that is what a slab roof is.** A roof laid in cubes steps two halves per
block travelled, which is the whole of the difference: naming a **roof slab** on the style puts it on one, so it
climbs half a block per block and lays that slab on every odd step with the roof's own material filling the
cubes between. It is the slope a slab is actually for — at a whole block of rise a course of slabs leaves an
open half between every pair and the roof can be seen straight through, which is why the roof's own material
documents stairs and slabs as different roofs. Measuring in halves rather than branching is what leaves the six
forms untouched: they answer in blocks travelled and the roof decides what a block travelled is worth, so a
roof laid in cubes answers exactly what it always did. The halving has to **floor** rather than truncate, since
the eave's rise goes negative below the base plane and rounding those cells back up lifts the overhang clear of
the slope it belongs to. The slab is a block id rather than a material, for the reason a window's is: which
half of its cube a slab fills is geometry.

**A roof is one material, and the slab is that material in halves.** The body and the verge are each a single
block — a pattern is refused in either (`HS3`), because a roof is read as one plane from below and a voronoi
across it is several blocks in one surface — and the slab continues the body, so it is the body's own
material: a sandstone slab under a brick roof builds alternating courses of two materials and reads as
neither. Body and verge may be the same block, which is a whole brick roof, or they may differ, which is how a
dark oak verge trims one; what neither may be is a **bare** log or a ground material.

**A laid log is a roof material, and the difference is the axis.** A log's data nibble is which way it lies, so
a log named as a solid has none: every one of them stands upright and shows a sawn face out at the slope, which
is the fault the ban was written for. A `laidLog` takes the axis the surface it is on is going, and for a roof
that is **the ridge** — so the logs run the length of the building, their ends buried in the gable at each end
and only bark showing on the slope. The roof passes its ridge rather than the wall ring beneath it, because the
ring is a different surface going a different way and off a wall it has no answer to give at all. A laid log
carries its own whole-course rise and no slab is cut from one, so `roofSlab` over a log roof is refused.

The **gable is not in this rule**:
it is the end wall carried up and it follows the wall, which is why a stone brick gable under a spruce roof is
a house rather than a fault.

Every roof here is a height field over **one rectangle**, and a building is not always one. A `BuildingPlan` is
one or more touching **wings**; everything below the eave — the foundation, the walls, the
window runs, the doorways, the slab and the beams — reads the plan's own cells, so an L, a T or a U is built as
one house on one outline. A wall stands wherever the plan is exposed, **diagonals included**: where two
wings meet, the two walls running into the turn touch along one vertical edge and nothing else, and the
cell behind that edge — building on all four sides of it — is the corner. Without a block there the
building has no block where it turns and the room shows through the seam. It takes a post like any
corner, so an L stands on six.

**A storey is a plan of its own**, and that is the whole of how a building of unequal wings works. A wing
carries a storey count, the plan at a storey is the wings still standing there, and every pass a storey runs —
its walls, its posts, its corners of both kinds, its window runs, the steps in from its wall, its slab — is
asked of that plan. Where a one-storey hall meets a two-storey cross wing, the ground is one plan over both and
the storey above is a plan over the wing alone, so the wall the cross wing needs against the hall's roof is not
a rule about neighbours: at that height the hall is not there to be met, and the line between them is simply
the upper storey's own outline. A plan only ever loses wings on the way up, which is why the way up is seated
against the topmost storey's front — a cell inside the highest storey is inside every storey under it.

**A building's roof is the union of its wings' roofs**, and never a max of their crowns: a max blends two
surfaces into one and drags roof material down the wall between wings of unequal height. Each wing is extruded
as the whole building it would be alone — its own rectangle, its own eave from its own storey count, its own
ridge axis from its own proportions — and the volumes are laid one after another, each closing its own riser
against itself. `RoofField` needs no changes to serve it, which is the finding the whole arrangement rests on.

Three rules carry the rest. A wing's roof reaches **its own walls plus its own overhang and no further**, so no
stub of roof hangs outside a wall it never touched. **No roof block sits below the wall top of a wing whose
roof it is not** — under someone else's wall is inside their building — which is what makes a one-storey wing
stop against a two-storey one instead of pushing a slope through its standing wall. And **walls outrank
roofs**: every volume is laid before any wall is, so a wing standing against another does not have the other's
slope written over it.

**Over its own wall, though, the eave wins.** A column writes down to its underside, which is its crown less
the drop to the deepest neighbour the roof covers — and at the eave line that neighbour is the overhang, a
pitch lower, so the eave reaches `pitch − 1` courses under its own wall top by construction. Those courses are
the roof coming down to meet the wall. Clamped away and then written over by the wall pass, they came out wall
material instead, and the stripe of wall showing under the overhang grew a course with every unit of pitch:
nothing was missing, so no gate caught it. So the clamp exempts the wing the roof belongs to, and the wall pass
stops under whatever course the roof has already claimed there. Wings never share a cell, so nothing else
moves — a marched or projected column stands inside *another* wing and is clamped exactly as before.

**Only the highest roof over a cell is written there**, and that one comparison is what makes the union a
building rather than two roofs in the same place. Where two wings' plans overlap the lower surface stands
*inside* the higher one, and a roof block inside a building is not a roof — it is an obstruction in the attic.
So a wing lays nothing at a cell another wing's field crowns higher. This is the cut a projecting wing makes in
the roof it pushes into: the hall's eave course stops at the wing's opening instead of running over the room
behind it, and the two lofts are one space. It is not a max of crowns — no surface is blended and no field is
touched; each wing still answers for itself, and the comparison decides only which of them is the one showing.

The same comparison keeps a gable's overhang open, which is the other thing it is for. **A verge climbs and an
eave does not**: the cells beneath a verge overhang are air, because nothing sheds onto them, while an eave
overhang is a solid course running the length. Where a wing's eave overhang reaches the column another wing's
gable oversails, the verge crowns higher and the eave gives way. Laid the other way round — and it was — the
eave fills the triangle and a gable end reads as a filled panel instead of a roof hanging past its wall.

**A face rises only where the building is outside it.** The walls climbing to meet the roof are built on the
**body's** perimeter, not on each wing's own rectangle: the side of a wing that stands against a neighbour is
not an outside face but a doorway between two halves of one building, open at the storey and open above it.
Filled anyway it walls the wing's loft off from the hall's. So **a marching T carries three gable faces and a
projecting one carries four** — the difference between the two junctions, stated as something countable.

**A verge is the outer rim of a roof, so no cell inside the outline is one.** A building of several wings has a
single outline however many rectangles drew it, and the rim is read from the roof plan as a whole — a cell with
a neighbour outside it. Read from the wing instead, a march's first step lands exactly on that wing's own
overhang line and stamps verge in the middle of the roof it has just run into.

**A wing may state which way its ridge runs, because its own proportions cannot know whether it crosses
anything.** A roof pitches across the shorter side, so by default the ridge lies along the longer one — and
read from each wing alone, two that touch may easily come out parallel. A 10 × 5 hall beside a 7 × 6 wing is
the ordinary case: both are wider than deep, both ridges run along x, and the roofs meet in a **gutter** rather
than a valley, which is the very thing a march exists to prevent. A **square** wing is the sharper one — it has
no longer side at all, the comparison ties toward x, and it can therefore never cross anything whatever was
wanted. `Wing.Ridge` overrides the proportions where an author needs the axis stated; the rise then follows the
span actually crossed, which is the point, since a ridge forced along the shorter side gives the longer one its
slope.

**Wings touch and never overlap, and where they touch they share the edge whole.** A plan states its ground
once, so two rectangles claiming the same cell have no account of whose walls stand there or whose roof covers
it — and the joint a building is made at *is* the edge between them, which an overlap does not have. Two
rectangles drawn a few blocks apart are two buildings. Drawn edge to edge they are one, provided the shorter
edge lies within the longer: where they merely graze, part of the wing's end meets its neighbour and the rest
hangs over open ground, and neither joint can happen along it.

**Which rectangle is the hall follows from the ridges, and nothing is named by an author.** The hall's ridge
runs *along* the shared edge and the wing's runs *into* it, which is what makes one of them a range and the
other a cross wing. Both along it is two ranges side by side meeting in a gutter; both into it is one longer
range that wants drawing as one rectangle. Neither is a junction. One more rule bounds the pair: **a wing
reaches no further along the shared edge than the hall reaches across it**, because a gable's height follows
the span its slopes cross, so a wing wider than the hall is deep stands taller than the roof it is meant to run
into and comes out the far side instead of forming a valley. Equal is legal and meets exactly at the hall's own
ridge — two 5 × 5 squares with crossing ridges are a building, and their plan is a single 5 × 10 box whose roof
levels are what tell them apart.

`WingJoints` derives all of that from the rectangles alone, and every way a pair can fail carries a rule id, so
a refused plan says which rule it broke rather than declining to stamp in silence:

| Rule | Refused |
|---|---|
| `HJ1` | the two rectangles share blocks |
| `HJ2` | they touch over part of an edge only |
| `HJ3` | both ridges run along the shared edge — two ranges side by side |
| `HJ4` | both ridges run into it — one longer range |
| `HJ5` | the wing stands taller than the hall it meets — the refusal names which rectangle it derived as which, because the roles follow from the ridges (the wing's runs *into* the shared edge) and a stated `ridge` can swap them from what the drawing suggests |

`HouseProp.Fault()` reports the id and a sentence in the terms the rectangles were drawn in; `Plan()`
answers null for the same plans, so a build gets the plan or nothing.

**March and project are the wing's own choice, and it is the one thing about a joint the rectangles cannot
say.** The same two rectangles make an L that closes and an L that pushes through: a marching wing steps its
roof into the hall's until that roof already stands as tall, and a projecting one carries its roof clean across
the hall to the far wall and shows a second gable standing on it. `Wing.Projects` picks between them, and
marching is what a wing does unless it says otherwise, because it is the shape that reads as one house. A
projection lengthens the **roof** and not the walls — the far wall is the hall's, and the gable simply stands
on it — and the lengthening runs along the wing's own ridge, which is the axis a gable's rise is not measured
over, so the wing arrives over that wall at exactly the height it left its own.

A meet is not left as two roofs abutting, because a wing that merely stops at the wall drops its ridge to the
neighbour's eave and climbs again, which is a gutter cut across the middle of a roof. It **marches**: each
course of the meeting wing steps on along its own ridge into the other for as long as its own crown still
stands taller than the neighbour's own roof does at that cell, and stops the moment the neighbour's surface
would already be the higher of the two — the point a valley forms at. The courses nearest the ridge travel
furthest and the ones nearest the eave stop at once, which is what draws the crossing as a diagonal valley. No
overhang is carried in — an overhang is what a roof has outside a wall, and inside another wing there is no
outside — so a marching end emits none of its own and the march takes those columns over.

**A course also never marches further than its own distance from its own eave**, whatever it meets. That bound
does not depend on meeting anything taller: a course this many blocks from its own wall is exactly as far as
its own roof plane would still be climbing were nothing there to meet, so it cannot need more room than that to
find a taller surface. Without it, a course whose crown never meets one — a steeper wing over a shallower hall,
or any wing over a flat one, which has no rising surface to meet at all — would march the length of whatever it
crosses and come out its far overhang, the shape of a wing drawn through rather than one that stopped at a
wall. Bounded, such a course still marches a little way past the wall — the excursion a valley cannot avoid
where the geometry says meet but the heights do not converge — and stops there rather than running on.

**The march is solved before any roof is laid, because it is part of the answer to which roof is showing.** A
course a wing marches into the hall is a course of the wing's roof standing over ground the hall's roof also
covers, and only the highest roof over a cell is written there. Laid afterwards, the wing's ridge goes in over
a hall course already written *under* it, and that course is a floor across the valley: it seals the wing's
loft off from the hall's, which is the one thing a junction exists to prevent. Solved first, the comparison
above can see it and the hall gives way.

There are only these two shapes, and a wing whose roof is shallower than the one it meets marches until the
neighbour's own surface catches up and stops there; one that projects comes out the far side with its own gable
and its own overhang, landing on the row the neighbour's overhang lands on.

**A wing's two gable ends carry the same triangle**, and that is what the acceptance test asserts: the end that
closes the building and the end on the neighbour's far wall are the same gable, block for block, above the eave.
Below the eave they are not alike and are not made so. One is a corner the building turns away at and stands on
posts; the other is a stretch of the neighbour's wall, which turns at that neighbour's own corners further
along. A gable is a roof part and a post is a building part, and no post is added where a wall runs straight on.

A porch is refused on a plan of more than one wing: a deck is a strip the walls give up, and giving one up
means taking cells out of a shape rather than moving one side of a rectangle in.

A roof has **no thickness knob**, and the height field is why: a column writes as many courses as the step down
to its neighbours needs, so how deep the roof runs at a given cell is answered by the slope rather than by a
number beside it. A flat lid is one course because a flat lid has no step to close. Two columns did carry such
a number — `room_style.roof_thickness` and `roof_style.thickness` — and nothing ever read either; both are
dropped. A roof laid half a block at a time is a `roofSlab` and its own pitch, which the roof part states.

### 7.2 The floor is divided in plan as well as in depth

The floor's stack divides it in depth. Its **top course** — the one players stand on — is divided across the
room by how far a cell stands from the walls: a `Border` ring as wide as `BorderWidth`, a `Field` across the
rest, and an `Inlay` centred in it starting `InlayInset` blocks in. A cell on the wall line is ring 0 and is
never zoned: the walls stand on it, so what it is made of is the floor part's business.

Zoning is a property of the shell rather than of a material, and the reason is that **a material does not know
the room**. A checker, a noise field and a wall run all resolve from the cell's own coordinates, so they can
pattern a floor but cannot put a ring one block inside a wall that moves with the footprint. Anything that is
a pattern therefore stays a material and is bound to the field; only what needs the room's own bounds is a
zone. Each zone is unbound until a course names it, and an unbound zone is not a zone — the floor part shows
through.

### 7.3 A porch is taken out of the footprint

The footprint comes from the piece (WX1) and a style may never change it, so a porch that grew outward would
be a style deciding a footprint. Taken inward it is not. The foundation still covers the whole
footprint; the walls stand `Depth` blocks back from one wall of it; and the strip they gave up is a deck
carrying posts, a rail and its own canopy. `Inset` pulls the deck in from each end of that wall, which makes
the porch a feature of the front rather than the front itself.

**The porch is the part that gives way.** It is trimmed to whatever the room can spare beyond the three blocks
that hold two walls and an inside, and where the room can spare nothing there is no porch at all.

Two details make it a porch rather than a hole in a wall. The doorway is **carried onto the wall's new line**,
so a frame's entry contract survives the wall moving; and the rail **breaks exactly where that doorway crosses
it**, because a rail running unbroken across the front would be a porch with no way onto the step.

The canopy is seated by its own **lowest course** rather than by where its plane starts or by the eave above
it: that course has to clear the doorway the porch fronts, and where it lands the ridge follows by however far
the form happens to fall. One statement for all six, and a statement about the thing that matters — a canopy
resting on the door head is a porch nobody can walk under.

Seating it under the house's eave instead is wrong for a reason worth keeping. On a house the two agree, since
a five-course wall puts its eave about where a porch wants its roof anyway. On a **tower** they do not: a wall
is a `RoomPart` extent and its last course repeats, so a building twenty-four courses tall is an ordinary
style, and a canopy chasing that eave rides the wall the whole way up — a colonnade of posts with a roof at the
top and the doorway it fronts left open to the sky twenty courses below.

### 7.4 Windows are cut, and chosen as a block

Five forms. A **stair lattice** is four stairs in a 2×2 hole, each with its raised half toward the outside of
the group, so the quarter each is missing meets in the middle and the window is open — there is no glass in
it. A **slab band** is a slab sill, an upside-down slab lintel and the course between them cut clean through;
the two half-blocks make the opening read taller than the one course actually removed. **Panes** are the
ordinary glazed window. **Open** is the hole and nothing in it — cut and left, which is not the same as asking
for no windows at all. An **arched** opening carries an upside-down stair in each of its two top corners and
nothing anywhere else: it is the door head's trick (§7.5) on a window, the same two stairs taking the
squareness out of the same square hole. Size belongs to the form as much as to the author: a lattice is 2×2
because the four missing quarters are the whole trick, a band is three courses because a sill and a lintel with
nothing between them is not a window, and an arch is two wide and two tall at the least — two corners are the
whole of an arch and one cell cannot hold both, and a head that took the only course would be an arch over
nothing. An open one is entirely the author's, since no form is imposing a shape on it.

Seating is the half that has to be right, because a window is cut out of a wall that already stands. A wall is
a **run** — one straight stretch of the plan's outline, ending wherever the building turns, away from itself or
back into itself — so a rectangle stands in four of them and an L in six, and a run rather than a compass
direction is what a window is seated in and a doorway is cut through. Two of an L's walls look the same way, so
naming a wall by the direction it faces stops being an identity the moment a building turns a corner. Each run
is seated **between its two corners**, the windows are spread evenly and centred on that run —
a wall reads as symmetric rather than as windows starting at one end and stopping when they run out — and any
seat that would meet a doorway, or the block of wall either side of it, is **dropped rather than shifted**.
Shifting one would break the spacing of every window after it to save it, and the gap where a door is reads as
intended. An opening that will not fit between the sill and the wall's last course is not cut at all.

**A window may also belong to a material rather than only to a wall.** Where the wall is one thing the two are
the same question and spacing seats a window well enough. Where it bands — a run of acacia logs against a run
of planks — they come apart: a seat chosen by spacing lands half in one band and half in the next, and an
opening cut across that seam reads as damage rather than as a window. A style may therefore name a **host
block**, and then the material divides the run instead of the spacing doing it: the seater walks the wall,
finds each unbroken panel of the host, and **spreads that panel exactly as it would spread a whole wall**. On a
wall whose bands are four cells and whose spacing is five, the two almost never agree on their own.

The panel is spread rather than given one centred window, and a **uniform** wall is why. A host names a block,
not a band, so a wall that is one material at the sill course resolves to a single panel the length of the
whole run — and one window centred in that is one window on a twenty-one-block hall, which is the wall a row is
most the point of. Spreading each panel gives the banded wall exactly what it had, since a two-cell band holds
one two-wide window and no more, and gives the uniform wall the row it was always asking for. Only the seam
between two panels can then be too tight, because within one the spread has already left a clear spacing
between neighbours.

The seater is told none of this. It takes the host as a **question** — may a window be cut at this cell? — and
the stamper answers by resolving the wall exactly as the pass that laid it did, same course, same arc, same
run. So the rule works for any pattern that puts the block there, a stripe or a checker or a noise stop, and
the seater keeps knowing only where a window goes rather than what a wall is made of. Naming a band the wall
never resolves to seats nothing, rather than seating it somewhere else.

A window's material is a **block id**, not a bound style, and it is the one place a shell departs from the
library's shape. A stair's metadata is which way it climbs and a slab's is which half it fills; the four
stairs of a lattice differ from each other by geometry alone, and a material resolving its data from where the
cell sits would turn all four the same way — a solid 2×2 patch of wall rather than a window. The block is
therefore chosen from the block picker and the stamper supplies the nibble.

### 7.5 The door is a closed set, and the reason is in the XML

`Domain.DoorMaterials` names the four choices (air, cobweb, stained glass, stained-glass panes) and, for each,
both the block the stamper places and the PGM material the wool-room block rule must whitelist. The wool
room's `block` rule is a whitelist (`WoolGenerator`), so a door made of anything it does not name cannot be
broken — the cage would be stamped with an entrance nobody can open, and nothing else in the pipeline would
catch it. One row read by both sides is what prevents that; `Pgm` and `Minecraft` are siblings, so `Domain` is
the lowest place both reach. A spawn's door is pinned to air whatever its style says: a player spawning in has
to walk straight out, and the spawn protection rule already keeps enemies from walking in.

Windows are deliberately **not** on that list, and the distinction is worth stating. A door is the way an
attacker gets in and so is governed by a filter; a window is a hole a player can see through and, in a
lattice's case, shoot through, but it is never the entrance the block rule is about.

**Every opening keeps a block of wall clear of each corner, and the opening is what gives way.** Clearing the
corner cell is not enough: an opening starting in the very next cell still meets the corner, and one hard
against it reads as a wall that failed rather than as a way through. The margin costs four blocks of the face,
so a five-wide face has one cell left and carries a **centred single opening** rather than a two-wide one
against the turn — a building too narrow for the door it asked for says so by having a narrow door. Only a
face with no seat at all falls back to the run between the corners, because a building nobody can walk into is
worse than one with a tight door. It is the same margin §7.4 keeps for a window, and it is the same number.

**It does not depend on a post standing there.** A post makes the reason easy to see — a column wants a block
of wall beside it before anything is taken out — but the rule is about the *corner*, which is where two walls
meet and turn, and that turn is in a plain shell exactly as it is in a framed house. Making it conditional
would also mean one building gained and lost the margin as its corners were bound and unbound, which is a
style deciding where a door goes; the footprint decides that, and a style may not (WX1).

So a frame's doors keep the wall and the place the frame chose, and are then fitted to the same margin like
any other opening. That costs a wool cage nothing, and WX7 is why: it already holds a door to at least one
block narrower than the interior on each side, which is exactly the length of the seat run — so a frame's door
fits without being narrowed, and only one pushed hard against a corner moves at all. This is the path a
**library preview** takes as well as a wool room, which is where the fault was visible: every card was drawing
its door against the pillar.

**A style may name the wall it fronts on**, and a hall is what wanted it. Windows are spread and centred on a
wall's run (§7.4) and a doorway is centred on the same run, so on a long building the two land on each other —
and a seat a door meets is dropped rather than shifted. A twenty-one-wide wall entered in the middle loses the
two windows either side of its door and reads as a row with a hole punched in it; entered at the gable end it
keeps its whole row, and a hall is entered at the end anyway. Unset, the front is the long side, which is what
a building with nothing to say about it has always fronted on. A porch's own edge outranks it, since a porch
names the wall it fronts; a frame's doors outrank both.

**An arch is two upside-down stairs in the top corners of an opening, and one shape serves both holes.** Over
a doorway the middle between them is spanned — a beam or an upside-down slab reading as one line with the
raised halves either side of it — because the head is carrying a wall. Over a window it stays open, because
there is no wall there to carry and the light runs the opening's width. That **span** is the whole of the
difference, and `Arch` is the one place either is laid.

Which way each stair faces is `BlockGeometry`, and nothing in it is named for what it is used on. A block's
metadata *is* its geometry in this format — two bits of facing and an upside-down flag on a stair, one bit of
half on a slab — so turning one is arithmetic, and arithmetic written at a call site is arithmetic written
again at the next. It is the writing half of what `Views/BlockShapes` reads back to draw, and it sits beside
`Blocks` rather than beside a window, so a terrain material banding a wall in upper slabs reaches the same
vocabulary an opening does.

A block with a **front** reads a second table, `Fronting`, and it is one table rather than one per block: the
nibble a wall sign takes to look north is the nibble a ladder, a chest and a furnace take, and only the stair's
two bits count from anywhere else. What a block hangs *on* is the opposite of what it looks toward — the block
behind a ladder is what holds it up — so a caller holding the wall rather than the view passes the wall's
opposite, which `RoomEdge` answers for itself.

Neither is the **head**, the beam that carries the wall over the opening. An arched one puts an upside-down
stair in each corner of the doorway's top course, raised half outward so the quarter each is missing faces
into the opening and the two of them round its top off — the upper half of a stair lattice doing the same
trick for a different hole. Where the opening is wider than its two corners the middle is spanned, by an
upside-down slab that keeps the head reading as one line or by a whole cube for a head that wants weight. It
is dressing *on* the opening rather than a change *to* it: the doorway is cut as it always was and the head is
written into its top course, which is why it is only laid on an opening at least two wide and three tall.
Below that it would take the last of the clear and leave a doorway nobody walks through. And it is a knob on
the style rather than on the door, because the door is what fills the opening — the closed set above — while
the head is what stands over it and is never walked through.

### 7.6 A building is a stack of storeys

A building's height is not a wall height but a stack of rooms, and the two are different numbers. A `Storey`
states its **clear** — the blocks of air a player stands in — and the courses follow from it: a storey carries
one more course than its clear when something stands over it, for the slab that separates the two, and the top
storey carries none, because the roof is its lid. Three storeys of three clear is eleven courses of wall, not
nine. Measuring by the air rather than by the masonry is what makes the number an author decides the number
that is true: three is the least a room may be, because a room a player cannot stand up in is not a room, and
a clear asked for under three is read as three.

A plain shell is the same building said differently, and `Levels` is where the two meet. A style that names no
storeys resolves to the single one its wall, windows and floor describe, so nothing downstream has to know
which of the two it was handed. That fallback storey is marked as a **shell**, and the mark matters: a wall
height is literal where a clear is not, so a two-course shed stays two courses instead of being rounded up to
the three a room would need. Every style saved before storeys existed is therefore exactly the building it
always was.

What a storey owns, it owns in **its own frame**. The walls are laid storey by storey, each counting its
courses up from its own floor, so a band written at a storey's fourth course lands at the fourth course of
every storey and a taller ground floor does not slide the one above it. Windows are seated the same way: a
sill of two is two blocks over *this* floor whichever storey it is, and the seater needed nothing new for
that, because it already takes a sill and a wall height and a storey is simply a shorter wall to it. Only the
ground storey is told about the doorway; there is no door to avoid on the ones above. A storey may name its
own wall, windows, floor zoning and deck, and falls back to the building's where it does not — a stack of
identical storeys is a count rather than a repeated description, and every one of those fallbacks is resolved
in `HouseStyle.Levels` rather than at the point each is used.

**The foundation levels the ground a cell has and never invents one.** It fills **upward only**, from each
column's own surface to the floor course the room reads off the footprint's highest column, so a room over
dipping ground has no holes in its floor and flat ground costs nothing. It fills in **stone**, which is what
the painter rewrites (TP6), so the ground under a room is finished like the ground around it. A footprint cell
with no ground at all is left alone — there is nothing under it to level against, and a column built there
would stand in open sky.

**And where the cell beside a building has no ground to meet it on, `WX11` says so** — a complaint at the
columns tier, since a building on a ledge is a real thing to draw and the world builds either way. The
foundation levels its fill at the footprint's own highest column, so a neighbour that is void, or more than
a step below the floor, is met by a sheer face of that fill: a wall a player cannot climb, at a height
nobody chose. It is read off the **provenance** rather than off the intent, so it covers everything a pass
stamped — a wool cage, a spawn cube, a placed building — by the identity each already recorded.

**A building stands on a foundation**, and it is one thing rather than three fields beside each other. The
**plate** claims downward from the course players walk on, so a thicker one digs into the ground the house
sits on rather than lifting its inside off it, and its top course is the ground storey's deck — which is why a
ground storey names none of its own. The plate's **surface** divides that top course across the room, a border
and a field and an inlay. The **footing** rings the plate one block proud on every side, and it is the
optional one — and **absent is the default**: the walls meet the ground flush, which is what a building seated
into finished terrain wants (author). Absent is a state and not a block that happens to be air — the air
material that used to stand in for it made "does this building have a footing" a comparison against a
sentinel rather than a question the style could answer.

**A footing belongs to a plate of two or three courses**, which is the foundation it is the foot of; over a
plate of one it is a one-block rim round a building with no foundation under it, and it reads as noise at
every wall of every house (author). `HS7` complains where the two disagree — a complaint, since the building
stands either way and what the rim costs is how it looks. The five village presets carry a plinth and now
carry the depth that earns one.

Every storey stands on a **deck**: one course infilled across the interior, the perimeter being wall already.
It is an infill rather than a lid — the walls already span that course, which is what a floor is when a
building is put up rather than drawn — and it belongs to the storey standing on it. So a building may floor
its shop in flagstone and the flat over it in boards; unbound, a deck is the house floor's own top material.
The ground storey's deck is the building's floor, and the topmost storey has nothing over it, since the roof
is what closes that one.

**One plate, one owner**, and that is the whole of why the deck is named for the storey above rather than the
one below. The course between two storeys is the ceiling of the lower seen from below and the floor of the
upper seen from above, and a block has only one identity. Named the other way it had two: its material came
from the storey below and its zoning — the border and inlay a player actually walks on — from the storey
above, which won wherever it bothered to speak. Both now come from the storey standing there, so a deck stays
a single material divided by that storey's own zones rather than two storeys answering a settled question
twice.

A deck needs a way through it or an upper storey is a sealed volume — a picture of a house rather than a
house. That way is a **ladder**, standing in the storey below and reaching the slab, so a player steps off it
onto the new floor rather than into its underside. It hangs on the **door wall**, one cell along from an
interior corner, and both halves of that are about what else claims those cells. The chests and the wool
monuments fill a room's corners first and then the far wall inward (`MonumentSlots`), so the door wall is
untouched until a room carries more monuments than a team ever captures — six wools is the ceiling in
practice, and one cell off the door wall's corner is free. The corner itself is not, which is why the ladder
sits one along from it. Where the doorway reaches that end the ladder takes the other one instead: a ladder in
the doorway is a ladder in the way.

Storeys are not a house-only feature. A wool room takes a `HouseStyle` like any other building, so a
multi-storey wool room is the same stack with a monument in it.

A **roof terrace** is a storey stack and nothing else, and it is worth spelling out because it looks like it
would need a form the roof does not have. Air is a gap rather than a block everywhere in a style (§7), so a
storey whose wall stack is one course of fence over two courses of air is a building with a **parapet** and
nothing above it; that storey's post takes air for the same reason, or four columns stand on the deck at the
corners. What closes the storey underneath is its ceiling, which is the deck. And the lid is taken off the
same way the walls were: a `Flat` roof laid in air writes nothing, so the stack's top storey is open to the
sky. The ladder is already there, because a slab has to have a way through it.

One thing about it costs something. The storey carrying the parapet still **states a clear of three** — a room has
to be stood up in and a storey cannot say it is not a room — so the building reserves courses it never writes.
What it does not cost is the number every caller reserves against: `TopLayerOver` answers where the highest
block **lands**, walking the wall stack down past whatever courses resolve to air and dropping the roof's own
contribution where all three of its materials are air. The reservation is still what the roof is seated on, so
nothing about the geometry moves; what moves is that a terraced building is no longer refused headroom it does
not need, and no longer previewed under a band of empty sky. A storey's post answers beside its wall, because
four columns standing on the deck at the corners are blocks laid at that course even where the wall between
them is air.

A parapet is the wall's own stack rather than a rail of its own, so it is as wide as the wall line and sits
over it, which is what a parapet is and is not what a rail set in from the edge would be.

### 7.7 The beams a seam leaves long

Where two storeys meet there is a course the floor is carried on, and a building made by laying logs against
each other leaves the ends of it long. A **beam style** runs log ends out past each of the four corners — two
per corner, one along each axis, eight in all — and each shows its **sawn end** rather than its bark, because
that is what the end of a log is. It is the one place on a building where a cut face pointing outward is the
point rather than the mistake. In plan the seam then reads as a **hash**: the walls are the square in the
middle of it and the eight ends stand outside.

The course *inside* the wall is a different thing and needs no machinery of its own. It is an ordinary course
of the wall's own stack, laid in a material that lies a log **along** the wall — the log checkerboard with one
of its two states taken away, reading the same wall run, so the sawn ends are buried in the neighbouring wall
blocks and only bark shows. At a corner it stands upright, since no lying log can show bark to two faces at
once. Keeping the course and the ends apart is what lets a beam run in one material and its ends in another,
and lets a building have either without the other.

The ends are **the one thing a house writes outside its own footprint**. Everything else a style lays falls
inside the walls plus the roof's overhang, which is what makes a shell safe to stamp onto finished terrain — so
these are asked for rather than assumed, and a style naming none leaves the ring around the building exactly as
it found it.

What a style never touches: the **platform** under a room (`StampFoundation`) and the **entrance redstone
line** (ST1) belong to the plan-derived structures, not to a shell. The platform is level, at the highest
column of the footprint it fills, because the room standing on it takes one floor course read from that same
column — a platform following the ground would stop under the floor wherever the ground falls away and leave
it spanning air. It fills **upward only, in stone**: what keeps a room from being entered from below is its
region rather than its floor, so there is no plinth under it, and stone is what the painter finishes (TP6), so
the ground a room stands on is painted like the ground around it.

Check this section against the studio: the house-part library previews stamp a draft through the real
`HouseStamper` and read it back out of the world, so where the prose here and the preview disagree, suspect
the prose.


## 8. The library

A room style is a **library row**, browsed and composed the way a terrain theme is (M0012, `room_style` +
`room_style_course`, served at `/api/room-styles`, authored at `/library/houses`). It binds the same
`style` shelf a theme binds — a `Band`'s material *is* a `TerrainMaterial`, so the two libraries share
their leaf rather than each keeping one — and the third tab exists because what is composed out of styles
differs: a theme composes a terrain finish, a room style a shell.

One difference in shape carries the whole distinction. A `theme_bucket` binds **one** style to a bucket; a
`room_style_course` binds one style to a part **at a position in that part's stack**, because a wall is a band
over bedrock over a slit and that is a stack rather than a material. The stack is stored under a unique
(room style, part, ordinal) index and rewritten wholesale on save — the courses are what the author edited, so
a diff would only be a slower way to the same rows.

A part with **no** courses keeps the built-in finish rather than resolving to nothing, exactly as an unbound
theme bucket does. That is what makes the library worth having for a style that only changes its roof.

Every picture is stamped by the real `HouseStamper` over a sample frame and read back (`RoomStylePreview`), so
a card cannot promise a shell the export would not build. There are four, because a building varies along more
axes than one picture holds: an **isometric** of what it looks like, a **plan** of what the roof does (its hole,
and whether its eave oversails the walls), a **section** of the course stack and the doorway through it — a
`BlockSideView` projection rather than a cut, so the near wall's doorway does not hide the wall behind it — and
a **cutaway** of one plane drawn at the scale of the pieces in it.

The cutaway is the one that earns its place twice over. It is the only view that draws a block as its own shape
rather than as a cube, read out of the block's own metadata (`BlockShapes`), so a stair lattice appears as the
opening it is instead of as a solid patch of wall; and, taken on the plane the ladder stands in, it is the only
view that shows a storey's slab, the clear under it and the way through it at once.

A library **card** carries the section alone, because the course stack is what a room style *is* — and because
an isometric runs tens of kilobytes, which is nothing for the one style an editor has open and megabytes for a
grid of them. The four views are drawn for the open editor only.

The renderers themselves live in `PgmStudio.Minecraft.Views`, below both things that draw with them: the
studio's library previews. Anything else drawing a building draws with them too — a picture one renderer gets
right and another gets wrong is worse than either being wrong alone.

The door picker is **served, never restated** (`/api/room-styles/doors` from `Domain.DoorMaterials`). A
client-side copy of the four choices is the one way a door could come to be offered that the wool-room filter
never whitelists, which §7 explains would seal the cage.

### 8.1 A house is composed from parts

The level above a style and below a house (M0018, `/library/parts`). A room style holding every knob of a whole
building gave a **part** no identity: a shingled roof with its pitch, its overhang and its capped ridge could
not be reused, only re-entered house by house, and a stack of storeys could only be a count of identical ones.
Three rows fix that — a `roof_style`, a `storey_style` and a `porch_style`, each owning the knobs of its part
plus that part's materials, and a `room_style` becomes the thing that binds them.

The split is by **what owns a coherent set of decisions**, not by what happens to be a nameable piece. A roof
is everything above the eave: the form and its numbers, and the body, the verge and the gable face. A storey is
one room: its clear, its wall, its corner posts, its windows, how its own floor is divided in plan, and the
ceiling it closes with where something stands on it. A porch
is the strip of footprint the walls give up, and it carries no materials at all — a porch's deck is the house's
own floor and its canopy is laid in the roof's material, so what is left to it is its shape. The rest of a
house's parts are a single material each, and a row wrapping one style would add a name and nothing else.

**Only a storey stacks.** A course stack counts upward from its part's own base and is what pins a band to the
fourth course of a wall however tall that wall grows. A roof has no such base to count from: how deep it runs
at a cell is however many courses close the step down to its neighbour (§7.1), so the body is one pass, the
verge is one and the gable face is one. Each of a roof's three pieces is therefore a single material, and the
editor offers one picker rather than a stack — offering a stack offered courses the stamper read the first of
and dropped the rest of, which is a knob whose preview never moves.

**A log is a post and a beam, and nothing else a house is made of.** The corner posts and the beam ends that
dock against them are the two places a log belongs; a log in a roof, a verge or a wall band is refused
(`HS3`, `HS1`). The rest of the pairs a house is built of are held to one material each: a door head's stairs
and the slab between them, a window and the host it is seated in (`HS4`). And no part of a house is built of
an **ore** (`HS5`) — an ore is ground a map is dug out of.

What the house keeps is what belongs to no part: its foundation — the footing and the plate's depth — and its
door. Everything else it names is a **fallback**. A bound part takes over from the columns on the house that
describe the same part, and only those, which is what made the level free to add: a house that binds nothing is
exactly the building its own columns always described, so no stored row had to move.

The stack is where the ordering lives. `room_style_storey` carries (house, ordinal, storey style, clear), the
ordinal assigned from the position in the list rather than trusted from the caller — the stack *is* an order,
and a caller free to number it could save a house with two ground floors and no first. Its `clear` overrides
the storey style's own where it is set, so one preset is a tall ground floor in one house and an ordinary room
in another without a second row of it: a shop under two flats is two presets bound three times.

Roofs and storeys keep **their own binding tables** rather than sharing one polymorphic table, so each keeps a
real foreign key to the row it belongs to and dies with it. A roof's rows are one per piece where a storey's
wall is a stack, but a *binding* means the same thing in all three, so the act of resolving one is written once
(`PartCourses`) and the three tables convert into it. That is the line: duplicated *schema shape* is cheap and
duplicated *resolution logic* is how two libraries come to disagree about what a stack means.

A part a building still wears cannot be forgotten — the delete answers 409 with the names of the houses wearing
it — which is the answer a style already gives a theme, and for the same reason: forgetting it would silently
change every one of them.

## 9. What a map binds

A map binds **two** styles, one per kind: the shell every wool cage is stamped with, and the shell every spawn
cube is. They may be the same style; nothing else is offered. There is no per-room override, and the reason is
§1's: a room is fanned across the symmetry orbit, so one team's cage and the other's are the same building seen
from the other side. A shell that differed between them would be a sightline one team has and the other does
not — a fairness break of exactly the kind the orbit exists to prevent, and one an author would have no way to
see while choosing it.

The binding lives on the **sketch layout**, under a `roomStyles` key beside the geometry, because that is what
the export reads: `WorldBuilder` is handed the layout JSON and nothing else about the map. It is a
**snapshot**, not a library reference (`docs/tools/library.md`) — picking a style copies its JSON in, so editing
that library row later cannot rebuild a shipped map's rooms. The `style_id` a map picked from is not stored,
which is the point: there is no reference to go stale, and the picker's "from *Slate cage*" selection is a
note on the session rather than a fact about the map.

`RoomStyleScope` is the read side, `TerrainThemeScope`'s sibling — with one shape difference that says the
whole thing. A theme resolves **per cell**, because a theme is scoped to a footprint; a room style resolves
**per map**, so `StylesOf` takes a layout and returns the pair. There is no `StyleAt`, and there is nothing for
one to take.

An **absent or unreadable** snapshot falls back to the built-in shell for its kind. A map that never opened the
step exports byte-identical to how it did before the step existed, and a hand-edited layout that broke its
snapshot loses its chosen shell rather than its export.

A snapshot that is present and **null** is the third answer: no building. The pad and its monuments are stamped
on whatever ground the plan already shaped, and nothing is raised over them — which is what a spawn wants
wherever the terrain is the room, and what the stampers have always accepted through their nullable `Shell`.
Absence and null are therefore different questions, and the wire keeps them apart: the two snapshots are bare
`JsonElement`s rather than nullable ones, so *undefined* means never picked and *null* means picked as nothing.
Collapsing them would not merely blur a distinction — loading a map that bound nothing and saving it again
would write the null back and turn every room it has into open ground.

The step is **Theme's third**, after Create and Apply, because a room shell is finishing: it is what the map is
*made of*, decided once for the whole map, next to the terrain finish that is decided the same way.
