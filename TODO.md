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
reaches the ceiling a pgmvox board does. Phase 0 has shipped and Phase 1 has closed, its two agent-facing
entries parked, and Phase 2 is open: shapes, modifiers and the ground. Its entries run in the order below, and each leaves the studio usable.

### Phase 2 — shapes, modifiers and the ground

The new ground document replaces the sketch's ground (D2), with no beta beside the live studio (D3). Its model is
settled (D16–D23) and Terraform's concept page has the author's approval. The modifier stack's first slice runs:
`modifiers[]` with Hill, Hollow, Level and Roughness, listed and edited in Terraform. The entries below finish
it in the order the concept page builds it, each against pgmvox's own operations (`pgmvox/document/layers.py`).

- [ ] **TS205 — A mask is drawn and dragged on the board.** The picked modifier's areas are outlined
  (`paintMasks`) and edited as numbers only. Arm a modifier's button with Draw's ellipse, polygon and lasso
  tools so the mask is drawn where it goes, and give each area the handles a shape has, a shift-draw adding an
  area to the picked modifier. `docs/tools/sketch.md`, *The modifier stack*.
- [ ] **TS207 — The board steps through the stack, and each modifier says what it changed.** Add `?step=` to
  `sketch/relief`, `sketch/columns` and the reads, the strip at the canvas foot that sets it, and a count per
  modifier of the columns it moved, zero a complaint. Every intermediate state is keyed by a hash of the
  modifiers before it (*Every modifier is kept as it ran*, `docs/pgmvox-port.md`). `docs/tools/sketch.md`.
- [ ] **TS208 — A shape follows, holds or rides the modifiers.** Today only a room's ground holds
  (`SketchRasterizer`, the stack's wiring). Add the shape field D18 names, the Shapes list in Terraform that
  sets it, and a mask's leave-outs by shape id. `docs/tools/sketch.md`.
- [ ] **TS206 — The relief becomes modifiers.** A FluentMigrator migration carries each stored group's marks,
  pushes and grain to the modifier each already is (the concept page's table), writes what it approximated into
  the map's history, and runs dry over every deployed map after the archive (D20); `relief` and its solver
  leave the document, and `WE28`, `B264` and `TS64` retire in the same commit. `docs/tools/sketch.md`.
- [ ] **TS209 — Ridge, River and the rest of the ground operations.** Port them from `pgmvox/ground/landform.py`
  in the inventory's order, each a new `ModifierOps` word with its row in *The modifier stack*.
  `docs/tools/sketch.md`.
