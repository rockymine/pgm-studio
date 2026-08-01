# Adaptive structure stamping — footprints, pads, presets (G31)

This folder holds the contracts for the **realized world** — what the export writes into the Anvil
world beyond raw terrain — the sibling of `docs/generator/` (which owns the layout) and
`docs/contracts/` (which owns the API/data contracts). Later passes over the same surface (map
theming and material palettes, G34) belong here too.

**Status: §1 describes the system as built; everything from §2 on is the G31 design and is not
built.** Rule ids here are `WX*` (world export). Read alongside:

- `docs/contracts/sketch-world-export.md` — the current shell contract. Its §2 (the fixed 8×8×8
  template) and §5 (marker-centred anchoring, integer-only X/Z) are the two sections G31 replaces;
  the rest (terrain, chest loadouts, monuments' derivation discipline, `level.dat`) is untouched.
- `docs/generator/rules.md` ST1–ST5 — the stamped-structure law. ST1/ST2 gain the footprint rule via
  the correction protocol when G31 lands.
- `docs/contracts/destroyables-and-cores.md` — the style-as-data precedent (§5 below).

---

## 1. The stamping system as it stands

The export places structures under two different regimes, and the split is the problem G31 fixes.

**Marker-anchored, piece-blind — the cubes.** `SketchWorldBuilder` places the wool cage and the
spawn cube from the marker alone: the wool/spawn point is snapped to whole integers
(`PositionSnap.SnapXZ`) and becomes the anchor of a hardcoded 8×8×8 shell (`CubeStamper.Size = 8`;
anchor = the shared corner of the 2×2 floor-wool pad, footprint `anchor−4 .. anchor+3`). The piece
rect never reaches the stamper. `SpawnCubeStamper` (monument corners), `WoolCageChests` (corner
chests) and `PlanStructurePreview` (the iso-view boxes) all navigate the interior through
`CubeStamper`'s constants (`Half`, `Interior`, `InteriorMax`, the strip/slit/roof layers).

**Piece-driven — the ST structures.** `PlanCompiler.BuildStructures` already listens to the pieces:
the ST1 bedrock room floor is exactly the wool-room piece's fanned rect, the entrance redstone line
comes from the terrain↔wool-room seam in the `ContactGraph`, ST4 walls from marked interfaces, and
the spawn protection / wool room XML regions are the whole piece rect.

The consequence worth building on: **the resolved `MapIntent` already carries everything an
adaptive stamper needs, per orbit image** — `WoolIntent.Room` (fanned piece rect),
`WoolIntent.Spawn` (fanned marker), `SpawnIntent.Protection`/`Point`/`Yaw` — because `FanRect`
carries a non-square rect through a quarter-turn orbit correctly. The world builder throws the rects
away today; nothing new has to be threaded through symmetry.

Two authoring facts frame the design. The plan format is generic over scale: `PlanGlobals.Cell` is
any blocks-per-cell value (default 5), so a "room piece" has no fixed block size. And markers snap
to the **half-cell lattice** (`plan-doc.js snapHalf`): a whole piece-relative offset is a cell
corner, a `.5` offset a cell centre — so a marker's block coordinate is either a **grid line**
(integer) or a **block centre** (`.5`, cell centres at odd cell sizes). The current export destroys
the second case: `PositionSnap.SnapXZ` rounds every marker to an integer because the one hardcoded
pad is 2×2, and a 2×2 pad only aligns on a grid line.

## 2. The footprint rule

- **WX1** *The piece dictates the footprint.* A wool-room or spawn piece is stamped with a shell
  whose footprint is the **piece rect inset by one block on every side** — the one-block ring of
  clean floor is part of the contract, not an accident of sizing. A 10×10 piece carries an 8×8
  shell (today's output, block for block — existing plans export identically), a 12×12 piece a
  10×10 shell, a 10×20 piece an 8×18 shell. The shell's orientation is the rect's own; the fanned
  rect orients the orbit images.

- **WX2** *Minimums are measured in blocks, never cells.* The smallest legal shell is **6×6**
  (a 4×4 interior — four monument corners, chests, and a pad still fit), so the smallest legal
  room/spawn piece is **8×8 blocks**. The plan validator rejects a smaller piece carrying the role;
  the generator's room sizing (`ShapeEmitter.RoomDepthCells`, today the constant 2) becomes
  cell-size-aware — enough cells to reach the block minimum — so a small-cell board cannot emit a
  room its own export would refuse.

## 3. The marker and the pad

The marker stops being the structure anchor and becomes what it semantically is: the wool/player
spawn point inside the room, realized as the floor pad. Placement stays on the half-cell lattice —
the constraint moves from the export (the integer snap) to the authoring lattice, where it already
lives; the marker is never freely placeable.

- **WX3** *Parity picks the pad class.* A marker on a block **grid line** takes the **2×2** pad
  straddling it — the only size for that parity. A marker on a **block centre** takes a **3×3**
  pad when the room affords it (WX4), else **1×1**. Nothing larger than 3×3 exists.

- **WX4** *Clearance and minimal shift.* The pad keeps **at least one block of clear floor to every
  wall**. A marker too close to a wall (the sharp case: cell 4, marker at the centre of a corner
  cell — two blocks from the piece edge, which is the interior's boundary) has its pad shifted
  **inward by the minimum** that restores the clearance, landing it just off the corner, never flush
  in it. A 3×3 is chosen only when it fits with that clearance after the shift.

- **WX5** *The pad is the point.* The exported spawn/wool location is the (possibly shifted) pad
  centre — the world is the ground truth the XML must agree with, the same discipline as the derived
  monument locations. The export-side integer snap is removed; parity flows through from the
  lattice. PGM absorbs both parities: player spawn points are free vectors, and the wool
  `<location>` flooring convention names exactly the block a centre-parity pad occupies
  (`floor(3.5) = 3`). The structure preview draws the shifted pad, and a shift is surfaced as a
  lint note, so the author is never surprised the point moved.

## 4. Entries

- **WX6** *Doors sit on the entry seams.* The wool cage's doors are cut where the terrain↔room
  **land seams** meet the shell — the same interfaces the ST1 redstone line is derived from — not
  centred one per wall: a long room with four centred doors would open two of them into the bedrock
  ring. The spawn cube keeps its single yaw-derived door (the yaw already fans per orbit image).

## 5. The code shape — frame, shell, furnishers, presets

The codebase already contains the pattern to generalize: a destroyable style is an enum plus a
`Dimensions()` table plus one `DestroyableBox()` function that the world build **and** the preview
both call, so the stamped volume, the emitted region and the drawn box cannot disagree (OB8). The
room stampers take the same shape, split into four parts:

1. **The frame** — one pure resolver, `(piece rect, marker, entry seams | yaw) → frame`, producing
   the inset footprint box, the floor Y (highest surface over the footprint, as today), the interior
   box, the pad cell (after WX3/WX4), and the door edges with their intervals. `SketchWorldBuilder`
   and `PlanStructurePreview` both consume the frame and nothing else — the preview cannot lie.
2. **The shell template** — the cube stamper parameterized by the frame's width/depth instead of
   `Size = 8`: floor + perimeter walls + roof, the strip/slit/roof layers staying fixed heights,
   the roof hole proportional with a cap.
3. **The furnishers** — monument and chest placement as functions of the interior box and door
   edges ("the four interior corners", "fill the back wall"), not of the 8×8 constants. A larger
   spawn cube then gains monument capacity from its longer back wall with no new rules.
4. **The preset** — a named style choosing shell materials and decoration, **never the footprint**;
   the footprint always comes from the piece, which is what makes every preset adaptive. Today's
   bedrock-with-colour-strip shell is preset one. Presets stay simple data rows on the
   `DestroyableStyle` model — no schematic format. Theming and material palettes (G34) attach at
   this seam.

Out of scope for G31: height presets (taller shells — trivial later through the frame), and the
destroyable/core stampers themselves — they already follow the style-as-data pattern and stay
anchor-based on purpose, a point structure rather than a room.

## 6. What must move when G31 lands

- `docs/contracts/sketch-world-export.md` — §2 rewritten around the parameterized shell, §5 around
  WX3–WX5 (the integer-only constraint falls).
- `docs/generator/rules.md` — ST1/ST2 amended via the correction protocol: the region *sizes the
  stamp* (WX1), it no longer merely surrounds a fixed one.
- `PlanValidator` — the WX2 minimum-size rule and the WX4 pad-fits check, beside the existing
  `CheckInside`.
- Tests — `CubeStamperTests`, `SpawnCubeStamperTests`, `WoolCageChestsTests`, the world-builder and
  preview assertions that pin the 8×8 geometry.
