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
reaches the ceiling a pgmvox board does. Phase 0 has shipped and Phase 1 is open: the sketch becomes the map's one
document, and the box plan stops being a level (D8). Its entries run in the order below, and each leaves the
studio usable.

### Phase 1 — one document: the sketch, its play pieces and its places

#### Draw, reviewed: what its three steps get wrong

- [ ] **TS193 — Undo takes back a piece.** Placing, moving, resizing, turning or deleting a play piece is one undo
  step, and the room ground a placement brings rides the same step. The undo stack holds the layout alone
  (`sketch-bridge.js`), while a piece writes the intent through `…/intent/pieces`; a step restores both.
  Evidence: a build area placed on a fresh sketch left Undo disabled, and `Ctrl+Z` left 16 pieces at 16.
  `docs/tools/sketch.md`.

- [ ] **TS194 — A room is picked before what stands on it.** A press inside a room's region picks the room
  first, whatever marker or building is under the pointer; the marker and the house are reached once the room
  is picked (`structuralAt`, `shared/pick.js`). Evidence: two presses at a wool room's centre each picked its
  *Wool spawn* marker and never the room. `docs/tools/sketch.md`.

- [ ] **TS195 — A room's height says it follows the ground.** A room stating no height reads *Surface 0*
  under "the height the layout was written with" while it stands on ground 9 high
  (`SketchStructuralInspector`); the rail says the room sits on the ground under it, and offers the number as a
  correction. `docs/tools/sketch.md`.

- [ ] **TS196 — The phase and step are in the address.** A Draw step and every phase write `?phase=` and a step
  word, so a reload opens where the author was. Evidence: a new sketch kept `?phase=info` through Draw and into
  Terraform, and a reload in Layout opened on Ground. `docs/tools/sketch.md`.

- [ ] **TS197 — A piece names itself only when picked.** The bold name inside every room goes
  (`render/sketch-render.js`); a picked piece shows its name small and left-aligned above its box and its size
  pill centred below, the one function Configure's selected region already draws (`world-canvas.js
  #updateOverlay`), moved beside `renderDimensionPill` in `render/canvas-chrome.js`. Colour is not part of it.
  `docs/tools/sketch.md`, `docs/client/canvas-interaction.md`.

- [ ] **TS198 — Ground is the ground and Layout is the pieces.** Ground picks, moves and edits shapes and no
  piece; Layout picks, moves, resizes, turns and deletes pieces and no shape, each drawing the other's work
  as context. Evidence: a click on land in Layout opened the shape panel with *Delete*, and a spawn picked in
  Ground read *It can't be resized*. `docs/tools/sketch.md`.

- [ ] **TS199 — Draw's first step is Places.** The step that draws the places is named *Places* in the bar, the
  keyboard sheet and the document, since *plan* names the retired board document. `docs/tools/sketch.md`.

- [ ] **TS200 — `sketch.md` names the phases as the app does.** Terraform, Palette and Decoration, where the
  document says Relief, Theme and Dressing; the code keeps its names. `docs/tools/sketch.md`.

- [ ] **TS190 — A place states the numbers it is held to.** A place carries targets — a value and the band it
  must fall in — and each is measured off the sketch's ground and shown beside its band, a miss as a complaint,
  as pgmvox's checker prints one line per number (`freeform/lib/AGENT-GUIDE.md` § the plan). The walks the
  sketch already measures (`BoardWalks`, `PlanNav.Of`) carry the first kinds: a team's walk from its spawns to a
  place, and which team arrives first. Lands on `PlacesDocument`'s `Place` (`Contracts/PlacesDtos.cs`) and
  `PlacesCheck`. **Open, the author's:** which kinds of target a place may state beyond walks and arrivals —
  a width or a height reads the place's shape against the ground. `docs/tools/sketch.md`.

- [ ] **TS191 — `sight`: who can see whom on the sketch's ground.** A line between an eye 1.62 blocks over the
  feet and a target's chest 0.9 over its feet is clear when every block it passes, sampled each quarter block,
  is above the ground the sketch builds and outside its houses; answered as each target's share of eyes that
  see it, and the targets none sees, over HTTP with a `?format=text` twin (pgmvox `read/sight.py`). **Open, the
  author's:** what the eyes and targets are — fixed points (spawns, goals, places) or every cell a team stands
  on. `docs/tools/sketch.md`.
