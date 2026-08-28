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

**The frame is settled, so the fields land in it.** The editor page is an outline, its fields beside it and
the preview as a companion column, and a flat document draws every section at once — so `B261`'s generated
field set and `B260`'s three controls are built once, into the shape that keeps them.

## The library: a browse page, an editor page, and the rail between them

The shape has landed: the rail carries the six kinds, `/library` chooses between them, and an entry opens a
page laid out as an outline, its fields and a preview companion. What is left is what an author can *say* on
it, what they can *see* while saying it, and what is on the shelf to say it about.

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

- [ ] **B47 — A theme copied onto a board loses where it came from.** Copying in matches by name, so the same
  library theme copied twice replaces its own snapshot rather than growing a `meadow-2` — but a board theme
  renamed on either side is two rows with nothing saying they are one theme, and nothing can say whether a
  snapshot is behind the row it came from. Wants a note on the copied theme recording the library row, which
  slots into `B44`'s snapshot record rather than duplicating it.

## The storey a placement rests on

`WE24` gave every placement an optional layer and two resolvers that agree about where a floor is. The export
has read it since the stack landed; nothing in the browser writes it, so a stacked board can only be dressed
and populated on its top surface. The frame is settled — the storey being drawn on is canvas chrome and a
placement takes it — so what is left is each surface reading and writing the layer it is handed.

- [ ] **B263 — A prop's layer can be neither seen nor overridden, and the canvas draws every prop alike.**
  `DressingDoc.add` stamps the storey being drawn on, so a prop placed on an upper layer records it and
  `DressingContext.GroundFor` resolves it (declining `DR-LAYER` where that layer has no ground). What is
  left is the two reads: `SketchDressingInspector` has no field for `PlacedProp.Layer`, so a prop cannot be
  moved to another storey without editing the layout by hand; and `dressing-render.js` draws a
  gallery-floor prop exactly like a roof one, so a stacked board's dressing reads as one plane.

- [ ] **B264 — No intent placement takes the active layer either.** The same optional `Layer` is on all six —
  monument, spawn, wool, iron cube, destroyable, core — and `MapIntent` carries it at six sites, set by no
  Configure step; `SpawnStep` states outright that its canvas is base-layer only. So on a stacked board an
  objective stands on a lower floor only by writing the intent by hand. Under `TS45` a placement takes the
  active layer, and what is left is the six write paths and the field on each inspector.
