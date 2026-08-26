# pgm-studio — TODO (current focus)

The **Now & Next** board — only the *current focus theme* lives here. Everything not in the immediate
slice is in **`BACKLOG.md`** (the long tail); shipped capabilities are in **`FEATURES.md`** (the Done
column). The three move left → right: **`BACKLOG.md` → `TODO.md` → `FEATURES.md`**.

**Holds only open work:** `[ ]` to-do, `[~]` in progress — **never `[x]`.** When a task ships, a commit
lands (its message references the id), the task **leaves this file**, and a line is added to `FEATURES.md`.
Board rules live in `CLAUDE.md` (§ "Status & task board").

**Two groups.** `WE41` is parked on a ruling — the measurement is done and no candidate predicate
reproduces the author's judgement, so nothing is built until one is chosen. The rest is the **library**,
pulled up because the example maps the paint work needs cannot be authored while a building is stated
twenty-five fields at a time down a rail. The group is over the soft cap on purpose.

## What a map is made of, read off the maps

The author reviewed the boards this repository has built and ruled on what is wrong with them. The rulings
are the map **as it is played and looked at**, so they are not derivable from the corpus or the code — they
are recorded here as law, with the measurement that found each one beside it.

### Colour: one family, and the table that names it

- [~] **WE41 — A pattern is a family shown off rather than a ground.** The predicate the author has since
  named is not colour distance but **how much of a family a pattern takes**: two blocks is a texture, three a
  mottle, five a family on display. Complain where a pattern's entry list carries more than two members of one
  `TerrainPalette` family. Beside it, two placements the author states absolutely: a **voronoi** belongs in
  the **fill** and is made of stone — never the surface — and a **field** pattern's two blocks must be near
  shades of one ground, so it carries a texture and never a border between two grounds.
  `docs/world-export/terrain-painting.md`.

  *Measured over the 51 boards in `pgm-studio-mapgen/specs` that carry a theme registry: of **277 patterns**,
  85% carry three entries or more — 51 carry five, 8 carry six or seven — and only 15% carry two. Of **50
  voronois**, 44 are on the surface and none is in the fill. The earlier candidates (one family per pattern:
  157/201; a neutral family mixed with a warm one: 54) are superseded.*

## The library: what exists on a page, what is edited on a page of its own

The author ruled on the shape. The library lists what has been authored; clicking an entry — a style, a theme,
a roof, a storey, a porch, a whole house — opens a **full page** that edits it or starts a new one, with the
board's own 3-D iso in it. The right-hand rail goes. Below is that change, the duplication it uncovers, and
the three things the API accepts that no control can say.

### The shape: a browse page and an editor page

- [ ] **B254 — An entry opens a page of its own, not a rail.** All four halves edit in `aside.lib-rail`
  beside the grid — six occurrences of it in each of `StyleBrowser`, `ThemeComposer`, `HousePartComposer` and
  `RoomStyleComposer` — so a twenty-five-field building is authored down a column. Give every kind
  `/library/{kind}/{id}` and `/library/{kind}/new`, opening the editor full-width with the preview beside the
  fields, and leave `/library/{kind}` as the browse grid. The rail goes with it.

- [ ] **B255 — One browse shell, not four copies of it.** The same markup opens all four: `lib-body` →
  `aside.lib-filters` carrying a New button → `main.lib-main` with an intro, `lib-status`, `lib-count`, a
  `lib-grid` and a `lib-card` whose figure button opens the editor. Extract one browser to `Components/`
  taking the rows, the card figure, the badges and the New label; only `StyleBrowser`'s kind filter is
  genuinely its own.

- [ ] **B256 — One editing state machine, not four.** Seven fields are declared identically in all four —
  `loading`, `note`, `figureHelp`, `editingId`, `draftName`, `preview`, `draft` — with `StylesByKind` and
  `StyleOf` copied verbatim into three of them and `OnAfterRenderAsync → studio.icons` into all four.
  Whatever `B255` extracts owns them.

- [ ] **B257 — One library verb per concept; there are six blocks and four ways to delete.**
  `TerrainLibraryClient` repeats list, get, draft-preview, create, update and delete for room, roof, storey,
  porch, style and theme. The delete half does not agree with itself — `Task`, `Task<HttpResponseMessage>`
  and `Task<StyleInUseDto?>` for one verb, which is the shared-shape-unshared-verb failure `CLAUDE.md` names.
  One generic pair over a per-kind descriptor, and one return.

- [ ] **B47 — The library has no search, and the sketch's theme names are its own.** Two small gaps the
  library page left open, worth doing once it has enough rows to hurt. The style browser filters by kind but
  not by name, so a library of forty styles is a scroll; the theme half has no filter at all. And a theme
  pulled into a sketch takes the library's name as its sketch-side id, which the bridge uniquifies — pull the
  same theme twice and the second is `meadow-2` with nothing saying they are the same theme. A name search box
  on both halves, and a note on the pulled theme recording where it came from (which slots into B44's
  snapshot record rather than duplicating it).

### What the author sees while authoring

- [ ] **B221 — The style libraries preview a stamped world, and a part editor frames a section of it.**
  Authoring a **whole style** — a house, a wool cage, a spawn shell — wants the building as it will stand, so
  the library builds a small world with the house in it and draws that: the path `B165` was found down, and
  now that the 3-D preview draws the world the export builds (`S54`) the library can show the real thing
  rather than a stamp of a fixed 10×10 sample. Authoring a **part** — `RoofStyle`, `Storey`, `PorchStyle`,
  `Foundation`, each a record of its own — wants a **section** through that world at the part, because the
  part is currently lost inside the whole: `RoomStylePreview.Views` takes `Outer(style)`, the entire shell,
  whichever of the three part libraries is open, and nothing on that path asks which part is being edited.

  Where a Y range is the right cut the bands are public: a storey is `LevelBases[i]` to `+ Clear`, a roof is
  `WallCourses` upward; a porch is an XZ restriction instead. Stamping the part alone is the wrong design — a
  roof's eave sits on the summed storey stack and the porch decides the front the body is split on, so an
  isolated part synthesises the context that decides its geometry anyway.

  *One trap: `WorldViews.Isometric`'s `Opaque()` reads `world.GetBlock` unbounded, so a face at the cut plane
  sees solid beyond it and is not drawn — a box restriction leaves the cut open unless out-of-box reads as
  air.*

- [~] **B70 — The room-style *card* cannot show a porch or a window.** The open editor draws four views now
  (B71), the cutaway among them, so a style's porch and its windows read there. A library **card** still
  carries the section alone, and a section projected onto the front wall shows a window as a patch of the same
  colour as the wall around it. The sample is the other half: `RoomStylePreview` stamps the shipped 10×10
  piece's 8×8 shell, which is small enough that a porch leaves little room behind it. The library therefore
  still has knobs whose *card* does not change when they are turned, which is the one thing the preview exists
  to prevent. Wants a larger sample footprint, and a card that is not the one view those knobs are invisible in.

  **And one footprint is the wrong number, not merely a small one.** `Sample` is a single `static readonly`
  field, so every style in the library is judged at 10×10 and at no other proportion — while a style states
  nothing about the footprint it will be stamped over, only storey heights and a roof's pitch. That would be a
  gap even if the shapes agreed, and they do not: `Wing.RidgeAlongX` derives the ridge from the rectangle's own
  proportions, so one style on 10×10 and on 5×10 is two different roofs rather than one roof stretched, and an
  author has no way to see the second. So the sample wants to be a parameter with a few proportions behind it —
  square, long, narrow — rather than one bigger square.

- [ ] **B258 — The library draws the iso the map draws.** `iso-webgl.js` renders the world the export builds,
  meshed by `column-mesh.js` from per-column runs — and both routes that answer those runs, `POST
  /plan/columns` and `POST /map/{slug}/sketch/columns`, are map-scoped, so no library editor can ask for one.
  Answer columns for a stamped style world and drive the existing bridge from it, so a house can be turned in
  3-D where it is authored. Supersedes the server-rendered `Iso` SVG in `HouseViews`.

### What the author can say

- [ ] **B259 — One dropdown, not twenty-four hand-rolled selects.** The authoring surface carries 24
  `<select class="field-input field-input--slim">` and 51 `<input>` with no shared control behind any of
  them — 12 selects in `RoomStyleComposer` alone. A `Components/Forms` select taking options, a value and a
  help blurb, so a field can be grouped, labelled and previewed once rather than twenty-four times.

- [ ] **B260 — Three room-style fields have no control at all.** `Beams`, `GableWindows` and `DoorHead`
  appear nowhere in the client, so a beam, a gable window and a door lintel are authorable only over HTTP.
  `RoofSlab`/`RoofSlabData` are carried through a save but editable only on a bound `RoofStyle` row, never on
  the room. Five of the twenty-five fields, and the five that make a building look built.

- [ ] **B261 — The theme editor mirrors a schema the API already publishes.** `GET /api/terrain/patterns`
  answers every material kind and field, typed, as the painter's deserializer takes them — and the client
  never calls it, keeping 422 hand-maintained lines in `Components/Terrain/ThemeVocabulary.cs` instead. A
  kind or field added server-side reaches no editor until someone edits that file. Drive the editor from the
  route; `B200`'s band stack is the first thing that stops being a special case.

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
