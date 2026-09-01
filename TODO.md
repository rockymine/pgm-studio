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

- [ ] **B178 — The region and the building are two rects with one name on the intents.** `SpawnIntent.Piece`
  and `Protection[0]` are the same rect, and `WoolIntent.Piece` and `Room[0]` likewise, so the type says twice
  what the placement says once. Rename to the split the document already carries: `WoolIntent.Room` becomes
  `Protection`, the word the spawn uses for the same thing, and the duplicate `Piece` goes — a null `Piece`
  currently means "keep the legacy marker-anchored room", which is a *role* question the compiler already
  answers. Then `ST9`'s 20×20 cap moves onto the stated footprint and the region takes `ST10`, its own
  **20×30**; neither is enforced today, so a 30×30 piece seeds a 28×22 building and nothing says no.

- [ ] **G156 — the stamped-room minimum is one number where it is two.** `WX2` states a single 6×6 floor,
  which is what a **shell** needs — walls plus a 4×4 interior — and applies it to rooms with no shell over
  them, so a small-cell board emits a room its own export refuses. Split it: a **room** floor of 4×4, which is
  what a pad, four chest corners and the monument seats need, and a **shell** floor of 6×6 that binds only
  where a style is bound. The emitters then need no cell-size arithmetic at all — a small piece carries a
  small room rather than a refusal.

- [ ] **B177 — An iron cube stamps outside its piece with nothing watching.** `StructureStamper.StampIronCube`
  centres a cube on its anchor and writes it wherever that lands, with no bounds test of any kind — the
  standalone path a marker takes when its piece carries no spawn marker, which is one deletion away from any
  authored spawn. Gate it on the piece the marker rides, the way `RoomFrames.PlaceIron` already gates the
  spawn-room path, and let an out-of-bounds cube resolve unplaceable rather than stamp into the void. `ST2`
  goes with it: it tests the marker block rather than the cube's footprint, and fires only when a spawn-role
  piece exists somewhere on the board.

  *Reproduced 2026-08-31 · one `lane` piece `x[-24,-8) z[-24,-8)`, an iron marker at `at:[0.5,0.5]`. The cube
  stamps at `x[-26,-22) z[-26,-22)`, two columns into the void, with no finding.*

- [ ] **G262 — Every authored iron marker in the corpus is unplaceable.** Measured across `tools/seeds`: 12 of
  14 spawn-room cubes resolve unplaceable, on five seeds, because a cube and a walled room need `6 + 3 + 3`
  = 12 blocks on one axis and those spawn pieces are 10×10, 15×15 and 20×10. The rule is right and the data
  is stale: re-author each seed's spawn piece so it either has the depth for a yard or states a footprint
  small enough to open one, and re-record whatever `docs/generator/seed-stats.md` measures off them.

- [ ] **TN11 — The seeded footprint has no handle on the canvas.** A drawn `spawn` or `wool-room` piece now
  states its marker and its footprint, but the only way to resize the building is to type the numbers. Draw
  the footprint as a second selectable rectangle inside the piece, wearing the transform box every authoring
  surface uses, its drag constrained to the piece and to containing the marker, with the size pill the piece
  already gets. `PlanCompiler.AppendStructuralShape` projects the piece rect into the sketch as one locked
  annotation and should project **both** — the region as the ground annotation it is now, and the footprint as
  a second tagged rectangle, which is also what `B145` hangs a theme scope on.

  *Seeding a spawn's **iron** waits on `B177`: it is the field that task deletes, so a marker written now is
  written into a shape about to change.*

- [ ] **S40 — Offer "no building" in the Rooms step.** A bound room style has three answers — a style, absent
  (the built-in shell), and an explicit null meaning the pad stands on open ground with nothing over it
  (`docs/world-export/structures.md` §9). `RoomStyleScope` reads all three for both kinds and the stampers
  accept the third, but the step can only *bind* or *clear*, and clearing means the built-in rather than none.
  The step needs a third control per kind — the bridge already stores it, `setRoomStyle(kind, "null")` parsing
  to a stored `null` distinct from `undefined` — and `ReadRoomBindings` needs `TryGetPropertyValue`, since
  `JsonObject["cage"]` answers `null` for an absent key and for a JSON null alike, so an open room displays as
  unpicked.
