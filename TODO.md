# pgm-studio — TODO (current focus)

The **Now & Next** board — only the *current focus theme* lives here. Everything not in the immediate
slice is in **`BACKLOG.md`** (the long tail); shipped capabilities are in **`FEATURES.md`** (the Done
column). The three move left → right: **`BACKLOG.md` → `TODO.md` → `FEATURES.md`**.

**Holds only open work:** `[ ]` to-do, `[~]` in progress — **never `[x]`.** When a task ships, a commit
lands (its message references the id), the task **leaves this file**, and a line is added to `FEATURES.md`.
Board rules live in `CLAUDE.md` (§ "Status & task board").

**One programme, and it is ordered.** The focus is the authoring surface: what the API accepts and the
browser cannot say. The library's entries are a sequence — the client before the shell, the shell before the
controls, the controls before the fields they carry — so this board runs over the soft cap on purpose and
takes nothing new until the shape lands. Anything found while working goes to `BACKLOG.md`.

**The frame comes before the fields it holds.** `TL5` and `TL6` are first because `B261` and `B260` add
controls and the layout they would land in is the one being replaced; building them into the current
inspector means building them twice. The keyboard pair follows for the same reason — every entry after it is
tested by hand, and an editor with no undo is an editor nobody experiments in.

## The library: a browse page, an editor page, and the rail between them

The shape has landed: the rail carries the six kinds, `/library` chooses between them, and an entry opens a
page laid out as the studio's own workspace. What is left is what an author can *say* on it, what they can
*see* while saying it, and what is on the shelf to say it about.

### The frame the fields land in

- [ ] **TL5 — The library editor is a document beside its preview, not a document either side of one.**
  `LibraryEditor.razor` lays every kind out as `Sidebar · workspace-canvas · Inspector`, so an outline row and
  the fields that edit it are one node of the document drawn twice — **824 px apart at 1440, 1304 px at
  1920** — while the picture between them caps at `760px` and leaves 526 px of empty stage at 1920. Reorder to
  **outline · fields · preview**: the two document panes adjacent, the preview a fixed ~326 px right column
  that still never scrolls with the knobs it is judged by. `Workspace`, `Sidebar` and `Inspector` keep their
  meanings; what goes is the right-hand inspector on the library route. The fields column then holds a node's
  controls on one row rather than stacking them in a 280 px pipe, with room under them for the bound style's
  own picture.

  *Both panel widths are `--sidebar-width`/`--inspector-width` = 280 (`tokens.css:131`), the rail 56.*

- [ ] **TL6 — A flat document renders open; a nesting one renders as a disclosure tree.** The six kinds are
  not one shape. A theme is fourteen controls with no nesting, and eleven of them sit behind a selection at
  any moment; a style is a recursive material tree (`MaterialTree.Walk`, nine nodes on a stack whose top layer
  is a voronoi); a house composes five parts. One frame with **one stated parameter — whether the document
  nests** — rather than three editors: a flat document renders every section open in the fields column, a
  nesting one renders its outline as the tree it already walks, and the picked node draws its own controls
  plus a stub per list entry (kind · extent · remove) so a band list is edited where it lives. Both sit under
  `TL5`'s preview companion.

### What the author can say

- [ ] **B261 — The theme editor mirrors a schema the API already publishes.** `GET /api/terrain/patterns`
  answers every material kind and field, typed, as the painter's deserializer takes them — and the client
  never calls it, keeping 422 hand-maintained lines in `Components/Terrain/ThemeVocabulary.cs` instead. A
  kind or field added server-side reaches no editor until someone edits that file. Drive the editor from the
  route; `B200`'s band stack is the first thing that stops being a special case. It also settles the picker
  that offers `laidLog` and silently replaces the material with a stone `solid` when it is chosen — a kind
  the client cannot build stops being offerable.

- [ ] **B260 — Three room-style fields have no control at all.** `Beams`, `GableWindows` and `DoorHead`
  appear nowhere in the client, so a beam, a gable window and a door lintel are authorable only over HTTP.
  `RoofSlab`/`RoofSlabData` are carried through a save but editable only on a bound `RoofStyle` row, never on
  the room. Five of the twenty-five fields, and the five that make a building look built.

### What the author sees while authoring

- [~] **B70 — The card shows the one view its knobs are invisible in.** A library card carries the section
  alone, and a section projected onto the front wall shows a window as a patch of the same colour as the wall
  around it, a porch as nothing at all. Which view a card should carry instead is a look-and-choose question
  rather than a derivable one: the plan reads the roof form, its hole, its overhang and a porch's notch but no
  window; the cutaway reads a window as the opening it is but draws a block as its own shape, which is tens of
  kilobytes per row. The sample is now a parameter, so a card could also be judged at a proportion where more
  reads. Wants the author's eye on which picture picks a house out of a grid.

### What is on the shelf

- [ ] **B47 — A theme pulled into a sketch loses where it came from.** The pull takes the library's name as
  the sketch-side id, which the bridge uniquifies — pull the same theme twice and the second is `meadow-2`
  with nothing saying they are the same theme. Wants a note on the pulled theme recording the row it was
  copied from, which slots into `B44`'s snapshot record rather than duplicating it. The library's own name
  search shipped with the browse strip.

## The keyboard, and the way back

Six bindings, four files, three copies of the same guard, and no undo anywhere in the client. Both entries are
tool-agnostic and both make every entry after them cheaper to try.

- [ ] **C53 — One keymap, one owner, and the help is generated from it.** The live set is `Ctrl/⌘+G`
  (`studio.js:138`), Escape and Delete (`sketch-canvas.js:932`/`941`, `plan-canvas.js:1110`/`1113`), `P` to
  promote (`sketch-canvas.js:944`) and the arrow nudge, implemented twice (`sketch-bridge.js:388`,
  `world-edit-controller.js:104`). Build `studio.keys`: one document listener, one registry of
  `{id, keys, when, run, label, group}` that the active tool fills and drops on dispose. `label` and `group`
  are **required**, so a binding cannot exist undocumented, and the `?` sheet and the `Ctrl/⌘+K` palette both
  render from the registry rather than from a hand-written list. Then bind the map: `V`/`H`/`F`,
  `R`/`P`/`L`/`M`/`X`/`B`, `Shift+P` for promote (freeing `P` for polygon), `1`–`5` for the phases, `Ctrl+D`
  duplicate, `Alt+1`–`6` for the overlay chips. It names the two affordances reachable today only by knowing
  them: `Alt` bypasses snapping, and a Ctrl-drag on a vertex handle is a Bézier tangent.

- [ ] **C54 — Undo and redo, over the two calls the bridge already answers.** There is no undo stack anywhere
  in the client, in a tool whose whole job is drawing. `sketch-bridge` already has both halves —
  `getState()` (`:903`) returns the document the host persists and `load(state)` (`:863`) restores one — so the
  stack is a ring buffer of those two. Two details are the cost: `load` ends in `canvas.fitToBbox()`, so it
  needs a keep-view flag, because an undo that reframes the board is disorienting; and a drag fires
  `markDirty` on every frame, so the snapshot is taken at `_moveStart` rather than per `_moveBy` — one drag,
  one entry. `plan-bridge` owns its document the same way, so one stack serves both surfaces. Bound in
  `C53`'s registry.

## The storey a placement rests on

`WE24` gave every placement an optional layer and two resolvers that agree about where a floor is. The export
has read it since the stack landed; nothing in the browser writes it, so a stacked board can only be dressed
and populated on its top surface. The cause is one frame, not two missing fields: `TS45` first, and the two
entries under it are what is left once it exists.

- [ ] **TS45 — The active layer is canvas chrome, and whatever is placed goes on it.** `SketchLayers` renders
  in exactly one branch of `SketchTool.razor` — the `else` that is Draw — so Relief, Theme and Dressing carry
  no layer control at all. A layer is a property of the board being looked at, like the mirror axis and the
  chunk grid, and both of those live in `CanvasLayerBar` and are therefore present in every phase without a
  phase having to remember them. Move the switcher there: the active layer is the one drawn on, the others
  ghost, and the 3-D chip row — which already *is* the storey list — becomes the same control in the other
  view. A placement then stamps the active layer id the way a stroke lands on the active layer in any paint
  program, which is what `B263` and `B264` are left needing.

- [ ] **B263 — A placed prop takes no layer, and the canvas draws it as though it had none.** `defaultProp`
  mints `{kind, id, seed}`, so a prop placed in the browser never carries `PlacedProp.Layer` — which
  `DressingContext.GroundFor` resolves and declines as `DR-LAYER` where that layer has no ground. An edit
  preserves a layer set over HTTP (every path spreads `{...prop}`) but neither shows nor changes it. Under
  `TS45` the mint takes the active layer; what is left is the field on the prop inspector as an override, and
  `dressing-render.js` drawing a gallery-floor prop differently from a roof one instead of identically.

- [ ] **B264 — No intent placement takes the active layer either.** The same optional `Layer` is on all six —
  monument, spawn, wool, iron cube, destroyable, core — and `MapIntent` carries it at six sites, set by no
  Configure step; `SpawnStep` states outright that its canvas is base-layer only. So on a stacked board an
  objective stands on a lower floor only by writing the intent by hand. Under `TS45` a placement takes the
  active layer, and what is left is the six write paths and the field on each inspector.
