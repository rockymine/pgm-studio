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

## The room shells and the dressing key: what a copy is keyed by, and who sees it is there

A map holds a **snapshot** of a library row and never a foreign key, so a library edit can never rebuild a
shipped map — and nothing beside the snapshot names the row it came from. `themeSources` and `biomeSource`
are read in two places, both in `SketchThemeInspector`, and no route accepts either;
`docs/design-decisions.md` says why that is the whole model rather than a gap. What is left is what a copy is
**keyed by** and who can **see** it is there: a bound room shell that reads as the built-in one, ten callers
guessing whether one is bound at all, a dressing recipe keyed by a name the library does not make unique, and
a wire word that calls the wool room a cage. `docs/tools/sketch.md`'s finish model is what the group leaves
correct.

- [ ] **TS101 — A bound room shell reads as the built-in one.** The Theme phase's room selects show
  `PickedRoom(kind)`, fed only by `pickedRooms` in `SketchThemeInspector.razor.cs:BindRoom`.
  `ReadRoomBindings` fills `boundRooms` and `openRooms` on load and never `pickedRooms`, so every reload, and
  every binding written through `PUT /map/{slug}/sketch/room-styles/{part}`, shows `(the built-in shell)` over
  a board that has one bound. Resolve it by content, the way `HeldBiome` already does one line up: match the
  snapshot against each library row's document with `SameDocument`, and say "off-library" where none of them
  is it. Evidence: bind style 13 to `cage` over HTTP and open the Theme phase — the select reads `(the
  built-in shell)` while the `×` beside it, which keys off `boundRooms`, says something is bound.

- [ ] **WE70 — Ten callers guess whether a shell is bound, and the export is the only one that knows.**
  `WoolFrame`/`SpawnRoom`/`RoomFrames.Resolve` take `shellBound`, which sizes the default footprint (`WX1`)
  and the wall inset; `WorldBuilder:148,179` pass the map's real binding (`RoomStyleScope.StylesOf`) and
  **ten** sites hardcode `true` — `PlanStructurePreview:60,74`, `RoomStylePreview:42`, `PieceRoom:75,98`,
  `WorldBuilder:761`, `MapExportComposer:534,539`, `DressingScope:223,225` — while `PlanValidator:404` passes
  `false`. Where a placement states no `footprint`, the guess changes the rectangle, so the previewed box, the
  `map.xml` region, the frontage and the stamped world can each name a different room on one map. Thread the
  layout's bound pair to the callers in `Export`, `Api` and `Pgm`, which all reach the layout already; the
  validator's `false` is correct and stays. `grep -rn "shellBound: true" src --include=*.cs` is the count.

  *Swept over spawn pieces 6×6–24×24 facing −z with no stated footprint, 94 sizes resolve a different
  default: a 20×14 piece frames `(1,4)..(19,13)` bound and `(1,5)..(19,13)` open, and a 6×12 piece frames
  `(1,1)..(5,11)` against `(1,5)..(5,11)`.*

- [ ] **TS103 — The dressing registry is keyed by the library row's name, which the library does not make
  unique.** `SketchDressingInspector.PickRecipe`/`PickShell` call `pullRecipe(recipe.Name, json)` and drop
  `recipe.Id`; `dressing-doc.js:pull` writes `this.#styles[key] = recipe`, replacing whatever that key held.
  No library table indexes `name` uniquely (`M0011`, `M0012`) — identity is the identity column and the name
  is free text, which is what lets **Save as copy** mint "X copy" beside "X". So two rows sharing a name
  collide on pull, and a row named `oak-10` overwrites the key `DressingJson.KeyFor` mints for a lifted
  recipe. Recipes lifted off old placements are already keyed from content; a library pull should be too.
  **Weigh against:** the key is what an author reads their recipe by in the inspector, so a minted key is a
  legibility loss the UI has to pay back by showing the name beside it.

- [ ] **TS105 — The wool room's wire word is `cage`.** The two shells a board binds are a **wool** room and a
  **spawn** room. `SketchRoomStyles` already names the property `Wool` and the inspector already offers it as
  "Wool cages"; only the wire word is left, in six places — `SketchLayout.cs:281`,
  `SketchFinishWrite.RoomParts`, `sketch-bridge.js:1051,1242`, `RoomKindInfo`, and the `part` value of
  `PUT /map/{slug}/sketch/room-styles/{part}` — plus four tests and four lines in `docs/`. A stored board
  lives in one place, the `map_artifact` rows of kind `sketch_layout_json`, so a migration rewrites them
  **once**, the way `M0024` already rewrites that same blob; the reader gains nothing — no upgrade on read
  and no second accepted key. `pgm-studio-mapgen` carries the key in 61 spec documents and 14 build
  scripts, which a sweep in that repo fixes in the same change.
