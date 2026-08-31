# pgm-studio — TODO (current focus)

The **Now & Next** board — only the *current focus theme* lives here. Everything not in the immediate
slice is in **`BACKLOG.md`** (the long tail); shipped capabilities are in **`FEATURES.md`** (the Done
column). The three move left → right: **`BACKLOG.md` → `TODO.md` → `FEATURES.md`**.

**Holds only open work:** `[ ]` to-do, `[~]` in progress — **never `[x]`.** When a task ships, a commit
lands (its message references the id), the task **leaves this file**, and a line is added to `FEATURES.md`.
Board rules live in `CLAUDE.md` (§ "Status & task board").

**One programme, and it is ordered.** A `spawn` or `wool-room` piece is doing four jobs at once — the ground,
the protection region, the building raised on it, and the bounds every marker on it is held to — and the
entries below are the one split that separates them, in the order each phase leaves the next one small. The
board runs at the soft cap on purpose and takes nothing new until the split lands; anything found while
working goes to `BACKLOG.md`. The library programme is pushed back down for the duration.

**Three numbers are the author's and are not to be re-derived.** A protection region is at most **20×30**
blocks, a building footprint at most **20×20**, and the smallest room with no building over it is **4×4**.

## The piece that does four jobs: the region, the marker, and the building on it

- [ ] **TN10 — A marker moves in 2.5-block steps, so a spawn cannot stand at block 3.** `at` is an offset in
  **cells** on a half-cell lattice while everything downstream is blocks: in a 20-block piece that is nine
  legal positions per axis out of the 41 the half-block lattice the export snaps to
  (`PositionSnap.SnapHalfXZ`) would allow. Make `at` **blocks relative to the piece's minimum corner**, on the
  half-block lattice, for every marker kind; `plan-doc.js snapHalf` follows. `WX3`'s parity is unchanged — a
  whole offset is a grid line, a `.5` a block centre. Bump `PlanModel.Version` to **2** and refuse version 1
  rather than guess a unit. The conversion is `at × globals.cell`, exact on the half-block lattice, so no
  marker moves: convert the 49 seeds under `tools/seeds/` and the stored `plan_json` rows.

- [ ] **B178 — The plan piece is the building, so a wide platform with a small hall cannot be asked for.**
  Give the spawn and wool placements a stated **`footprint`**: `[x, z, w, h]` in blocks from the piece's
  minimum corner, `4×4` to `20×20`, inside the piece and containing its marker. `RoomFrames.ResolveRoom` takes
  it instead of insetting the piece by one, so the pad, the chests, the monument seats and the doors all
  follow the building rather than the region; absent, it defaults to an inset that leaves a ring rather than a
  single block. On the intents, `Piece` becomes `Footprint` and `WoolIntent.Room` becomes `Protection` — the
  same word the spawn already uses for the same thing. `ST9`'s 20×20 cap moves onto the footprint and the
  region takes `ST10`, its own **20×30**.

- [ ] **G156 — the stamped-room minimum is one number where it is two.** `WX2` states a single 6×6 floor,
  which is what a **shell** needs — walls plus a 4×4 interior — and applies it to rooms with no shell over
  them, so a small-cell board emits a room its own export refuses. Split it: a **room** floor of 4×4, which is
  what a pad, four chest corners and the monument seats need, and a **shell** floor of 6×6 that binds only
  where a style is bound. The emitters then need no cell-size arithmetic at all — a small piece carries a
  small room rather than a refusal.

- [ ] **B37 — An iron cube is stamped with no gate at all.** `StructureStamper.StampIronCube` centres a 4×4
  on its anchor and writes it wherever that lands, with no bounds test of any kind; only the `ST2` lint
  watches, it tests the **marker block** rather than the footprint, and it fires only when a spawn-role piece
  exists somewhere on the board. Both go when `B177` derives the cube — there is no marker left to gate and no
  second stamping path to reach.

  *Reproduced 2026-08-31 · one `lane` piece `x[-24,-8) z[-24,-8)`, a `cube-4` destroyable and an iron marker
  both at `at:[0.5,0.5]`. The destroyable is refused `OB17`; the iron cube stamps at `x[-26,-22) z[-26,-22)`,
  two columns into the void, with no finding.*

- [ ] **B177 — Iron is authored on a lattice too coarse to place it and resolved by a negotiation nobody
  asked for.** Derive it: delete `PlanPlacements.Iron`, `SpawnIntent.Iron`, `StructureIntent.IronCubes` and
  the `IronCube` record, and let a spawn placement carry an **`iron`** count. The resolver seats each cube
  **inside the protection region**, forward of the pad along the door axis and clear of the **door
  corridor** — the door's own opening projected out to the region's edge — which is `SP7` as geometry rather
  than as a complaint. Parity picks the size from the seated position, so nothing walks a ladder and nothing
  resolves unplaceable: `WX8`'s negotiation and `WX9`'s placeability go with it.

  *All 13 iron markers across the 49 seeds are spawn-family; the three on plain pieces are traced maps that
  put the cube on the piece ahead of the door. `<renewable region>` is the cube's own footprint, so renewal
  never depended on the protection region.*

- [ ] **TN11 — The footprint has no handle on the canvas.** Draw it as a second selectable rectangle inside a
  role piece, wearing the transform box every authoring surface uses, its drag constrained to the piece and to
  containing the marker, with the size pill the piece already gets.
  `PlanCompiler.AppendStructuralShape` projects the piece rect into the sketch as one locked annotation and
  should project **both** — the region as the ground annotation it is now, and the footprint as a second
  tagged rectangle, which is also what `B145` hangs a theme scope on.

- [ ] **S40 — Offer "no building" in the Rooms step.** A bound room style has three answers — a style, absent
  (the built-in shell), and an explicit null meaning the pad stands on open ground with nothing over it
  (`docs/world-export/structures.md` §9). `RoomStyleScope` reads all three for both kinds and the stampers
  accept the third, but the step can only *bind* or *clear*, and clearing means the built-in rather than none.
  The step needs a third control per kind — the bridge already stores it, `setRoomStyle(kind, "null")` parsing
  to a stored `null` distinct from `undefined` — and `ReadRoomBindings` needs `TryGetPropertyValue`, since
  `JsonObject["cage"]` answers `null` for an absent key and for a JSON null alike, so an open room displays as
  unpicked.
