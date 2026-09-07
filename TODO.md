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

## A library row copied onto a board: what is kept, and what names it

A map holds a **snapshot** of a library row and never a foreign key, so a library edit can never rebuild a
shipped map. That half is settled. The note beside the snapshot saying *which* row it came from is not.
**Seven bindings copy a row onto a board and they answer in three shapes**: a theme records `themeSources`
(a map, theme id → row id), the biome records `biomeSource` (a scalar), the two room shells —
`roomStyles.cage` and `roomStyles.spawn` — record **nothing at all**, and the three dressing recipes (a tree,
a boulder, a house shell) key themselves on `recipe.Name`, the one field no library table indexes uniquely.
These entries settle the shape before an eighth binding invents a fourth.

**Two rulings come first and the rest are downstream of them.** `TS102` says what a note is and what it is
called; `TS104` says whether an agent wants one at all. `TS101`, `TS103` and `B44` each name the shape they
would record in, so answering the two is what makes the other five small. `docs/tools/sketch.md`'s finish
model is what the group leaves correct.

- [ ] **TS102 — One word and one shape for "the library row this snapshot came from".** *Parked on a ruling.*
  `SketchLayout.FinishKeys` carries `themeSources` (a map, theme id → row id) and `biomeSource` (a scalar),
  and nothing for the two room shells or the three dressing recipes. A note per binding makes four names in
  two shapes for one idea; one `sources` table keyed by a path — `themes/meadow`, `roomStyles/cage`,
  `dressing/oak-10`, `biome` — makes one reader, at the cost of folding the two existing keys in on read the
  way `DressingJson.Upgraded` already folds an older dressing document forward. **Which, and under what
  word:** `Source` is taken twice in the repo and neither is this — `MapOrigin.PlanSourceId` is the plan a map
  was compiled from, and `Region.SourceId` in `XmlWriter`/`Deserializer` is a `map.xml` region reference.

- [ ] **TS104 — No source can be recorded over HTTP, and it is not settled that one should be.** *Parked on a
  ruling.* `grep 'themeSources\|biomeSource' src/PgmStudio.Api` returns nothing: `SketchFinishWrite.WithBiome`
  and `.WithRoomStyle` take no source, and `PUT /map/{slug}/sketch/themes/{themeId}` registers a theme with
  none, so both working cases work only for a human clicking. **Is the note wanted for an agent at all?** An
  agent authoring a board usually has no library to have copied from — it writes documents against a seeded
  database it did not fill, so the id would be absent on nearly every board `pgm-studio-mapgen` has built. The
  case that earns it is the local one: an author who has already made themes asks for a board finished with a
  named one, and the board should say which row that was. If yes, every finish write takes an optional row id.

- [ ] **TS101 — A bound room shell reads as the built-in one.** The Theme phase's room selects show
  `PickedRoom(kind)`, fed only by `pickedRooms` in `SketchThemeInspector.razor.cs:BindRoom`.
  `ReadRoomBindings` fills `boundRooms` and `openRooms` on load and never `pickedRooms`, because
  `roomStyles.{part}` is a bare snapshot with no note beside it — so every reload, and every binding written
  through `PUT /map/{slug}/sketch/room-styles/{part}`, shows `(the built-in shell)` over a board that has one
  bound. Record the row in whatever shape `TS102` settles and resolve it the way the biome select does: the
  recorded row first, a `SameDocument` content match after it. Evidence: bind style 13 to `cage` over HTTP and
  open the Theme phase — the select reads `(the built-in shell)` while the `×` beside it, which keys off
  `boundRooms`, says something is bound.

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
  recipe. Recipes lifted off old placements are already keyed from content; a library pull should be too, with
  identity moving to whatever `TS102` settles. **Weigh against:** the key is what an author reads their recipe
  by in the inspector, so a minted key is a legibility loss the UI has to pay back elsewhere.

- [ ] **TS105 — The wool room's wire word is `cage`.** `SketchRoomStyles` already names the property `Wool`
  and the inspector already offers it as "Wool cages"; only the JSON key is `cage` — `SketchLayout.cs:281`,
  `SketchFinishWrite.RoomParts`, `sketch-bridge.js:953` and `:1154`, `RoomKindInfo`, and the `part` value of
  `PUT /map/{slug}/sketch/room-styles/{part}` — plus four hits in `docs/`. The comment above the property says
  a rename would leave every bound wool style falling back to the built-in shell on load, which is what an
  upgrade on read exists to prevent; rename the route's `part` value in the same commit.

- [~] **B44 — The library cannot see which maps carry a copy of a row.** The tables, the HTTP surface, the
  `/library` page and the sketch's pull/push bridge all shipped (`FEATURES.md`); two slices remain, and the
  first **waits on `TS102`** — what the document records is what the database can index.
  **(1) Apply-as-snapshot** — a map's *applied* theme is still the sketch document's own registry, so
  "the library holds the reusable copy, the map holds a frozen one" holds in the document and nowhere the
  database can see: `themeSources` says which row a board theme came from (B47), but the link lives in the
  layout blob, so the library cannot answer which maps carry a copy of a row or which copies have fallen
  behind it. Give the map's scope store a forked instance with a `parent_id` back-reference, the same doctrine
  the generator's plan persistence uses; whether it is wanted at all is `TS102`'s answer.
  **(2) A data migration** lifting the themes inlined in a map's own `sketch_layout_json`
  registry into styles + themes + bindings, deduping identical materials — today a map themed without pushing
  anything out keeps its blob and the library cannot see it.

- [ ] **TL15 — A copied body records the world it was cut out of.** `copied` means cut from a world
  (`docs/tools/library.md`, the author's ruling) and nothing enforces it, because a body carries no
  provenance: `tools/seed-trees.cs` and a hand-typed array produce the same row. Add the cut to
  `TreeStyleRow` — the world directory, the foot's world coordinates, the date — written by the cutter and
  absent on anything else, and shown on the card so a browse says which trees are real. It is what makes
  "typed in rather than cut" a checkable statement. **Weigh against:** an author who wants a tree the two
  generative forms cannot state has nowhere else to put it, so a record that says "typed in" describes a
  legitimate body rather than a fault — the note is worth having as provenance and not as a gate.
