# Adaptive structure stamping — footprints, pads, styles (G31)

This folder holds the contracts for the **realized world** — what the export writes into the Anvil
world beyond raw terrain — the sibling of `docs/generator/` (which owns the layout) and
`docs/contracts/` (which owns the API/data contracts). Later passes over the same surface (map
theming and material palettes, G34) belong here too.

**Status: implemented (G31).** Rule ids here are `WX*` (world export); the resolver is
`PgmStudio.Domain.RoomFrames`, and one `RoomFrame` per room feeds the stampers
(`CubeStamper`/`SpawnStructureStamper`/`WoolChests`), the structure preview, and the exported points
alike. Read alongside:

- `docs/contracts/sketch-world-export.md` — the shell contract (layer scheme, chest loadouts,
  monuments' derivation discipline, `level.dat`); its §2/§5 defer to this file for sizing and
  anchoring.
- `docs/generator/rules.md` ST1–ST5 — the stamped-structure law; ST1/ST2 carry the footprint rule.
- `docs/contracts/destroyables-and-cores.md` — the style-as-data precedent (§5 below).

---

## 1. The frame

Every wool cage and spawn room is stamped from one resolved **`RoomFrame`**: the shell footprint,
the interior, the pad, and the doors, derived by `RoomFrames.Resolve` from three authored inputs —
the **piece rect**, the **marker**, and the **entry interfaces** (a spawn substitutes its
yaw-derived door edge). The world builder and the plan editor's structure preview consume the same
frame (`SketchWorldBuilder.WoolFrame`/`SpawnFrame`), so the drawn box and the stamped shell cannot
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

- **WX1** *The piece dictates the footprint.* A wool-room or spawn piece is stamped with a shell
  whose footprint is the **piece rect inset by one block on every side** — the one-block ring of
  clean floor is part of the contract, not an accident of sizing. A 10×10 piece carries an 8×8
  shell (the shipped footprint — existing plans keep their geometry, with only the wool-door width
  changing per WX7), a 12×12 piece a 10×10 shell, a 10×20 piece an 8×18 shell. The shell's orientation is the rect's own; the fanned
  rect orients the orbit images.

- **WX2** *Minimums are measured in blocks, never cells.* The smallest legal shell is **6×6**
  (a 4×4 interior — four monument corners, chests, and a pad still fit), so the smallest legal
  room/spawn piece is **8×8 blocks**. The plan validator rejects a smaller piece carrying the role.
  The composer's boards clear the minimum by construction (its 2-cell rooms at cell 5 are 10×10);
  making the generator's room sizing (`ShapeEmitter.RoomDepthCells`) cell-size-aware, so a
  small-cell board cannot emit a room its own export refuses, is G156.

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
  assembly nudges that axis half a cell onto a grid line (`Composer.LegalizeMarker`).

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
  stamps its cube **outside the room shell**, inside the piece, with **one block of clear air** to
  the wall — never flush, never merged into a corner. Fitting is a two-sided negotiation with a
  fixed priority: the shell pulls **one edge** back from its WX1 footprint by the minimum that
  clears the cube — legal while the shell holds the WX2 minimum and the spawn marker stays inside
  the interior (the pad may still clamp with a WX4 shift); among legal shrinks the largest retained
  area wins, ties breaking toward the edge farthest from the spawn marker, so orbit images shrink
  mirror-consistently. The cube itself degrades by marker parity — a **grid-line** marker centres
  **4×4**, falling back to **2×2**; a **block-centre** marker centres **3×3**. A marker whose two axes
  disagree centres no square at all, so it takes the whole ladder (**4×4 · 3×3 · 2×2**) and settles half
  a block off centre on the odd axis; refusing it instead is what turned a column of three iron markers
  down one side of a spawn piece into three lints and no ore. The low corner is always the marker less
  half the span, rounded away from zero, which is what keeps a half-block landing symmetric across the
  orbit. The renewables wiring covers exactly the resolved footprints. `RoomFrames.ResolveRoom` owns the
  negotiation; `Composer`-emitted plans and authored plans go through the same resolver.

- **WX9** *Placeability is an attribute, not an exception.* Every structure marker resolves to
  **placeable or not** (`IronResolution.Placeable`). An unplaceable marker stamps **nothing** — the
  room takes its full WX1 footprint — but the marker itself stays on the board: validation flags it
  with the clearance requirement (the WX8 lint), and the structure preview draws **only placeable**
  structures, so the iso view never shows a cube the export refuses to place. This is the general
  contract for structure-vs-structure conflicts; the objective-separation rules (a core or
  destroyable against monuments, or inside a spawn piece — where spawn protection would make an
  enemy goal unbreakable) take the same attribute when they land (B37).

  The stampers themselves stay heterogeneous on purpose — their inputs are irreducibly different (a
  wall owns a *seam between two pieces*, a room a *piece + marker + entry interfaces*, an entrance
  line a *piece and its neighbours*, an objective a *marker + style over terrain*), so no shared
  base type sits over them. What every family shares is the **output**: an axis-aligned block volume
  on a surface-derived floor, placeable or not. The B37 generalization is therefore a common
  **resolved-stamp record** (kind · footprint · placeable · source marker) that each family's own
  resolver produces — the currency pairwise separation rules and the preview read —
  never a common stamper interface. `IronResolution` is that record's first instance.

## 6. The code shape — frame, shell, furnishers, style

The layering follows the destroyable/core precedent — one box function that the world build **and**
the preview both call, so the stamped volume, the emitted region and the drawn box cannot disagree
(OB8) — split into four parts:

1. **The frame** — `RoomFrames.Resolve` (`PgmStudio.Domain`), a pure resolver
   `(piece rect, marker, entry rects | yaw edge) → RoomFrame`: the inset footprint, the interior,
   the pad (after WX3/WX4), and the doors. `SketchWorldBuilder.WoolFrame`/`SpawnFrame` derive it
   (with the legacy default for piece-less markers) and `PlanStructurePreview` consumes the same
   derivation — the preview cannot lie. The floor is the highest surface over the footprint
   (`SketchWorldBuilder.FrameFloor`), which is its own mirror, so orbit images rest level.
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

A shell is **parts and overrides**. Floor, walls and roof are each a `RoomPart`: a stack of `RoomCourse`
(material + how many courses it runs) plus an `Extent` saying how far the part goes. Over them go the two
things a style may not decide — the **pad**, because the exported wool/spawn location is derived from it
(WX5), and the **doorway**, because it is the entry contract (WX6/WX7).

A stack is read from its part's own base outward: a floor **downward** from the course players stand on, a
wall and a roof **upward**. The direction is the load-bearing part of the model. A floor that grew upward
would lift the pad and move the exported point; walls indexed from the floor are what keep a band at eye level
and a slit near the top when the room's height changes, where the layer indices they replace would have slid.

The **last course repeats** past the end of the stack, the rule `LayeredMaterial` already holds, so `Extent`
moves without the stack being re-authored: a taller wall grows in whatever its top course is. That is what
makes height a knob at all — §4's note that room height stays out of G31 is answered here rather than through
the frame.

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

Every roof here is a height field over **one rectangle**, because the stamper builds a building of one
rectangle. The plan underneath it has outgrown that: a `Footprint` is one or more touching **wings**, and it
answers its outline, its corners of both kinds and its steps in from the wall by walking its own cells rather
than by arithmetic on a min and a max, so an L, a T or a U is already one plan with one closed ring. What is
not built is the roof over one (G172): a wing's roof is one of these fields and a building's roof is the union
of the wing volumes, which `RoofField` needs no changes to serve. The rules that composition turns on, and the
invariant that gates it, are on the task.

A roof has **no thickness knob**, and the height field is why: a column writes as many courses as the step down
to its neighbours needs, so how deep the roof runs at a given cell is answered by the slope rather than by a
number beside it. A flat lid is one course because a flat lid has no step to close. The `roof_thickness` and
`thickness` columns are read by nothing (B72); a roof laid half a block at a time is B69, and that is the shape
of the knob if one is ever wanted.

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
be a style deciding a footprint. Taken inward it is not. The sill and the floor still cover the whole
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

Seating is the half that has to be right, because a window is cut out of a wall that already stands. Each wall
is seated on the run **between its two corner posts**, the windows are spread evenly and centred on that run —
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
own wall, windows and floor zoning, and falls back to the building's where it does not — a stack of identical
storeys is a count rather than a repeated description.

Each storey but the last is closed by a **slab** across its interior only, the perimeter being wall already.
What it is laid in is that storey's own **ceiling**, so a building may close its shop floor in flagstone and
the flat over it in boards; unbound it is the house floor's own top material, which is what every storey stack
was before the slab had a name. The ceiling belongs to the storey below because that is the storey the slab
closes — the top one names none, since the roof is what closes that one.

The slab's top course is zoned by the storey **above** it, so an upper floor takes a border and an inlay
exactly as the ground one does — it is that storey's floor, not the ceiling of the one below, and the author
who divided the ground floor into a bordered field means the same thing one storey up. The two do not
compete, and which decides which is what keeps the ceiling a single material rather than a stack: the ceiling
says what the slab is made of and the zones above divide the one course of it a player actually stands on, so
a stack here would be a second answer to a question already settled.

A slab needs a way through it or an upper storey is a sealed volume — a picture of a house rather than a
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

Two things about it are not free. The storey carrying the parapet still **states a clear of three** — a room
has to be stood up in and a storey cannot say it is not a room — so the building reserves two courses it never
writes, and `TopLayerOver` answers for the reservation rather than for the highest block actually laid (G171).
And a parapet is the wall's own stack rather than a rail of its own, so it is as wide as the wall line and
sits over it, which is what a parapet is and is not what a rail set in from the edge would be.

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

What a style never touches: the **platform** under a room (`StampFoundation`'s bedrock column, ST1) and the
**entrance redstone line** (ST1) belong to the plan-derived structures, not to a shell.

`tools/compose/house-showcase.cs` is this section's live twin — every figure in it is stamped by the real
`HouseStamper` and read back out of the world, so when the prose and the showcase disagree, suspect the prose.


## 8. The library

A room style is a **library row**, browsed and composed the way a terrain theme is (M0012, `room_style` +
`room_style_course`, served at `/api/room-styles`, authored on the `/library/rooms` tab). It binds the same
`style` shelf a theme binds — a `RoomCourse`'s material *is* a `TerrainMaterial`, so the two libraries share
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
studio's library previews and `tools/compose/house-showcase.cs`. They have to agree about what a building looks
like, and a picture one gets right and the other gets wrong is worse than either being wrong alone.

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

What the house keeps is what belongs to no part: its foundation — the sill and the floor's depth — and its
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
the export reads: `SketchWorldBuilder` is handed the layout JSON and nothing else about the map. It is a
**snapshot**, not a library reference (finishing-model.md §3.3) — picking a style copies its JSON in, so editing
that library row later cannot rebuild a shipped map's rooms. The `style_id` a map picked from is not stored,
which is the point: there is no reference to go stale, and the Rooms step's "from *Slate cage*" caption is a
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
