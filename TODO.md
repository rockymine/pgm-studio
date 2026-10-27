# pgm-studio — TODO (current focus)

The **Now & Next** board — only the *current focus theme* lives here. Everything not in the immediate
slice is in **`BACKLOG.md`** (the long tail); shipped capabilities are in **`FEATURES.md`** (the Done
column). The three move left → right: **`BACKLOG.md` → `TODO.md` → `FEATURES.md`**.

**Holds only open work:** `[ ]` to-do, `[~]` in progress — **never `[x]`.** When a task ships, a commit
lands (its message references the id), the task **leaves this file**, and a line is added to `FEATURES.md`.
Board rules live in `CLAUDE.md` (§ "Status & task board").

**Three numbers are the author's and are not to be re-derived.** A protection region is at most **20×30**
blocks (`ST10`), a building footprint at most **20×20** (`ST9`), and the smallest room with no building over
it is **4×4** (`WX2`). A dressed prop's 192-cell ceiling (`HP3`) and a room building's 20×20 measure the same
concept since `WE71`, and holding them apart is a deliberate not-yet.

## The programme: bringing pgmvox into the studio
**The author put this programme first.** `docs/pgmvox-port.md` is the programme: pgmvox's capabilities move
into the studio one phase at a time, each proven against a pgmvox fixture, until a board built on pgmstudio.de
reaches the ceiling a pgmvox board does. Phase 0 has shipped, Phase 1 has closed with its two agent-facing
entries parked, and Phase 2 has shipped: shapes, eighteen ground operations in the modifier stack, and the water
they make. Phase 3 has shipped too: soil by slope, paint rules, beds that dip, fold and cycle, a water's own bed
and its fall off the edge, and a layer's underside. Phase 4, space in three dimensions, has shipped its carves and solids; dragging a solid out in the dock (`TS212`)
is parked. Phase 5 has shipped its pieces and stamps; its upload (`TC23`) is parked. Phase 6 is open, starting
with the house fields.

### Phase 6 — built things: the house fields pgmvox has

The house a style describes gains what makes pgmvox's houses read as lived in
(`pgm-studio-mapgen/analysis/freeform-vs-studio/houses/HOUSES.md` §4–§6). Each field lands on `HouseStyle` or
`Storey` (`Houses/HouseStyle.cs`), round-trips through `HouseStyleJson`, is stamped by `HouseStamper`, is edited
in the library's house editor, and leaves `docs/world-export/structures.md` and `docs/tools/library.md` correct.

- [ ] **WE175 — Timber bays along a storey's wall.** `Storey.Bays`: a post every `period` blocks along a wall
  (pgmvox: 4, upper storeys, long walls only), counted from the corners so both ends land on a post, in its own
  material or the storey's `Post`. Stamped after the wall and before the windows, which `HouseWindows` then
  keeps off a bay as it keeps them off a corner (`HouseStamper.cs:320`).
- [ ] **WE176 — A head course on every storey.** `Storey.Head`: one ring course over the clear, the top storey
  included, so a log seam under the roof and between storeys is stated rather than left to a wall stack's last
  band repeating (the double-log seam, `HOUSES.md` §2). Unset keeps today's slab course.
- [ ] **WE177 — A jetty, a setback and an eave per storey.** `Storey.Jetty` (reach 1, joists as a laid log
  course) pushes an upper storey out over the one below; `Storey.Setback` steps it in; `Storey.Eave` hangs a
  course out at its head. The tiered tower and the half-timbered overhang of the Brittlebush boards. A reach
  that leaves the 20×20 footprint (`ST9`) is refused as a new house rule.
- [ ] **WE178 — A chimney.** `HouseStyle.Chimney`: a body, a cap, which wall it rises against (`rear` by
  default) and how far it stands above the ridge (pgmvox: 2, `build.py:476-481`), placed clear of the doorway
  and of a roof cut. It changes the silhouette from across a board, which is why pgmvox puts one on every house.
- [ ] **WE179 — A wooden door on a placed house.** Spruce, oak and dark oak leaves as `DoorMaterial` values,
  allowed on a house prop and refused on a wool or spawn room's style, whose break rule whitelists the leaf
  (`DoorMaterials.cs`). Today a placed house's doorway is air or glass.
- [ ] **WE180 — A heading on a placed house.** `HouseProp.Heading` in degrees, so a hamlet stops standing in a
  row (the author's review of Slatefold). A second stamper path over a turned frame, as pgmvox's `Frame` and
  framed `RoofField` do; a turned roof drops to cubes and slabs, and the cell outside the door is kept clear
  at any heading. Measured against `HouseStamper` before it is promised.
