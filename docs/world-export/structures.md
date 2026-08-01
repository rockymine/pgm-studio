# Adaptive structure stamping — footprints, pads, presets (G31)

This folder holds the contracts for the **realized world** — what the export writes into the Anvil
world beyond raw terrain — the sibling of `docs/generator/` (which owns the layout) and
`docs/contracts/` (which owns the API/data contracts). Later passes over the same surface (map
theming and material palettes, G34) belong here too.

**Status: implemented (G31).** Rule ids here are `WX*` (world export); the resolver is
`PgmStudio.Domain.RoomFrames`, and one `RoomFrame` per room feeds the stampers
(`CubeStamper`/`SpawnCubeStamper`/`WoolCageChests`), the structure preview, and the exported points
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
  monuments — are never exposed to the outside. Door *height* is untouched; room height stays out
  of G31's scope entirely.

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
  **4×4**, falling back to **2×2**; a **block-centre** marker centres **3×3**; mixed parity centres
  nothing (the same square law as the pad). The renewables wiring covers exactly the resolved
  footprints. `RoomFrames.ResolveRoom` owns the negotiation; `Composer`-emitted plans and authored
  plans go through the same resolver.

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

## 6. The code shape — frame, shell, furnishers, presets

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
   roof, the strip/slit/roof layers at fixed heights, the roof hole proportional with a cap
   (`RoofHoleSpan` — the 8-wide shell keeps its 4×4 hole), doors cut per the frame.
3. **The furnishers** — `RoomFrames.InteriorCorners` seats the chest stacks and
   `RoomFrames.MonumentSlots` the monuments (door-wall corners, back-wall corners, then the walls
   fill, skipping the door opening). A larger spawn room gains monument capacity from its longer
   walls with no new rules; the validator refuses a plan whose captured-wool count exceeds the
   seats.
4. **The preset** — a named style choosing shell materials and decoration, **never the footprint**;
   the footprint always comes from the piece, which is what makes every preset adaptive. Today's
   bedrock-with-colour-strip shell is the one preset; further presets stay simple data rows on the
   `DestroyableStyle` model — no schematic format. Theming and material palettes (G34) attach at
   this seam.

Out of scope for G31: height presets (taller shells — trivial later through the frame), and the
destroyable/core stampers themselves — they already follow the style-as-data pattern and stay
anchor-based on purpose, a point structure rather than a room.
