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

## The library: a browse page, an editor page, and the rail between them

The shape has landed: the rail carries the six kinds, `/library` chooses between them, and an entry opens a
page laid out as the studio's own workspace. What is left is what an author can *say* on it, what they can
*see* while saying it, and what is on the shelf to say it about.

### What the author can say

- [ ] **B259 — One dropdown, not twenty-four hand-rolled selects.** The authoring surface carries 24
  `<select class="field-input field-input--slim">` and 51 `<input>` with no shared control behind any of
  them — 12 selects in `RoomStyleComposer` alone. A `Components/Forms` select taking options, a value and a
  help blurb, grouped by what the options vary with and carrying a swatch per row where the option names a
  material. The `Forms` tier of `docs/client/ui-conventions.md` names it when it lands.

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

- [ ] **B221 — The style libraries preview a stamped world, and the cut follows the selected row.**
  Authoring a **whole style** — a house, a wool cage, a spawn shell — wants the building as it will stand, so
  the library builds a small world with the house in it and draws that: the path `B165` was found down, and
  now that the 3-D preview draws the world the export builds (`S54`) the library can show the real thing
  rather than a stamp of a fixed sample. Authoring a **part** wants a **section** through that world at the
  part, and `B254`'s outline is what says which: `RoomStylePreview.Views` takes `Outer(style)`, the entire
  shell, whichever part is open, and nothing on that path asks which part is being edited.

  Where a Y range is the right cut the bands are public: a storey is `LevelBases[i]` to `+ Clear`, a roof is
  `WallCourses` upward; a porch is an XZ restriction instead. Stamping the part alone is the wrong design — a
  roof's eave sits on the summed storey stack and the porch decides the front the body is split on, so an
  isolated part synthesises the context that decides its geometry anyway.

  *One trap: `WorldViews.Isometric`'s `Opaque()` reads `world.GetBlock` unbounded, so a face at the cut plane
  sees solid beyond it and is not drawn — a box restriction leaves the cut open unless out-of-box reads as
  air.*

- [~] **B70 — The card shows the one view its knobs are invisible in.** A library card carries the section
  alone, and a section projected onto the front wall shows a window as a patch of the same colour as the wall
  around it, a porch as nothing at all. Which view a card should carry instead is a look-and-choose question
  rather than a derivable one: the plan reads the roof form, its hole, its overhang and a porch's notch but no
  window; the cutaway reads a window as the opening it is but draws a block as its own shape, which is tens of
  kilobytes per row. The sample is now a parameter, so a card could also be judged at a proportion where more
  reads. Wants the author's eye on which picture picks a house out of a grid.

- [ ] **B258 — The library draws the iso the map draws.** `iso-webgl.js` renders the world the export builds,
  meshed by `column-mesh.js` from per-column runs — and both routes that answer those runs, `POST
  /plan/columns` and `POST /map/{slug}/sketch/columns`, are map-scoped, so no library editor can ask for one.
  Answer columns for a stamped style world and drive the existing bridge from it, so a house can be turned in
  3-D where it is authored. Supersedes the server-rendered `Iso` SVG in `HouseViews`.

### What is on the shelf

- [ ] **B47 — A theme pulled into a sketch loses where it came from.** The pull takes the library's name as
  the sketch-side id, which the bridge uniquifies — pull the same theme twice and the second is `meadow-2`
  with nothing saying they are the same theme. Wants a note on the pulled theme recording the row it was
  copied from, which slots into `B44`'s snapshot record rather than duplicating it. The library's own name
  search shipped with the browse strip.


## The storey a placement rests on

`WE24` gave every placement an optional layer and two resolvers that agree about where a floor is. The export
has read it since the stack landed; nothing in the browser writes it, so a stacked board can only be dressed
and populated on its top surface.

- [ ] **B263 — The Dressing phase cannot say which storey a prop rests on.** `PlacedProp.Layer` names the
  layer whose surface a prop sits on, resolved by `DressingContext.GroundFor` and declined as `DR-LAYER` where
  that layer has no ground. The UI writes it nowhere: `SketchLayers` renders only in the Draw branch of
  `SketchTool.razor`, and `defaultProp` mints `{kind, id, seed}`, so every prop placed in the browser takes
  the top surface. An edit preserves a layer set over HTTP — every path spreads `{...prop}` — but neither
  shows nor changes it, and `dressing-render.js` draws a gallery-floor prop exactly like a roof one. Wants the
  layer rail in the Dressing sidebar, the field on the prop inspector, and the canvas drawing the storey a
  prop is on.

- [ ] **B264 — No intent placement can be given a storey either.** The same optional `Layer` is on all six —
  monument, spawn, wool, iron cube, destroyable, core — and `MapIntent` carries it at six sites. No Configure
  step sets any of them, and `SpawnStep` states outright that its canvas is base-layer only. So on a stacked
  board an objective stands on a lower floor only by writing the intent by hand.

## Looking at a board that was built

- [ ] **B262 — The read-backs have no browser surface, and neither do the ones already taken.**
  `render/topdown`, `surface`, `walk`, `mirror`, `section`, `structures`, `traversability` and `heightmap`
  answer a picture each over HTTP and are fetched by nothing in the client. `docs/world-scan/read-backs.md`
  never claimed a UI, so this is a gap rather than drift — but reviewing what a board looks like is the loop
  the paint work runs on, and today it runs at in-game speed. A page per map, live off the routes.

  **The larger half is that the pictures already exist.** `pgm-studio-mapgen`'s `tools/drive.py` takes all
  eleven world reads over HTTP after every build and writes them beside the documents: 64 renders a map in
  `specs/<name>/renders/`, a `world-surface.png` per board and a `theme-*-surface.png` per theme — which is
  the palette read `WE41` is parked on — and a `world-layer-*.png` per storey where the board is stacked.
  Fifty-odd boards' worth of provenance-backed pictures nobody can see side by side. So the second surface is
  a **contact sheet over a renders directory**: one row per map, one column per view, the view pickable, at a
  size where a whole run is judged in one screen. That is what makes a preference pass over the built boards
  affordable, and it needs no new render.

- [ ] **B265 — A disk read cannot be given the provenance sidecar, and this repo's worlds never carry one.**
  `TopDownRender.Run(regionDir, …)` finds provenance only by `WorldProvenanceFile.TryRead(regionDir)`, and
  `drive.py` deliberately moves `provenance.json` out to `specs/<name>/` because `maps/<name>/` is uploaded to
  the PGM server and holds only `region/`, `map.xml` and `level.dat`. So every render taken off a shipped
  world after the fact degrades to the material estimate — correctly labelled in the legend (`B133`), and on a
  painted board wrong enough to read terrain as structure across half the map. The HTTP routes are unaffected;
  they build the world and hold `Built.Provenance`. Wants `--provenance <path>` on the reads that take a
  region directory, so a sidecar kept beside the documents can be pointed at.

- [ ] **B266 — The read-back help documents a flag the CLI does not parse.** `--help` prints
  `--topdown --layer …` for every subject, because the text is generated from `WorldReadCatalog`, which is
  written for the HTTP route — where the query parameter really is `layer`. The CLI parses `--subject`, so
  the documented form fails as `no region dir: --layer`, naming the wrong argument. One of the two words has
  to give; the route's is the published one, so the CLI should take `--layer` (keeping `--subject` is a second
  accepted spelling, which is what rots).
