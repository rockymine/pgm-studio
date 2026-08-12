# pgm-studio — Backlog (later)

The **long tail** — open work that isn't in the current focus. The active slice is in **`TODO.md`**;
shipped capabilities are in **`FEATURES.md`** (the Done column). Flow: **`BACKLOG.md` → `TODO.md` →
`FEATURES.md`**.

**Holds only open work:** `[ ]` to-do, `[~]` started-but-parked — **never `[x]`.** A task lives in
exactly **one** of the three files; pull one up into `TODO.md` when it becomes now/next (its id does not
change). Sections + ids match `TODO.md` — a task slots into the same section wherever it lives. Parked /
deferred items stay here, flagged inline. Board rules live in `CLAUDE.md` (§ "Status & task board").

Task ids are a section letter + number, **globally unique and stable** across all three files; never
renumber or reuse.

## Authoring (N) — the new-map intent editor (`/maps/{id}/configure`, new maps only)

The guided wizard at `/maps/{id}/configure` (UI label **Configure**) that builds a map from declarative
intent (`docs/contracts/new-map-authoring.md`; backend + every page-order step are landed —
`FEATURES.md`). **Leave the existing Edit editor untouched** — a separate surface, not a refit. Only
the focus-integration polish remains.

- [ ] **N08 — Monument Y via side-view + per-side focus.** The side-view (`SliceView`) already sets Y on
  **spawn** and **wool-spawn** (`SpawnStep`/`WoolSpawnStep`, `FEATURES.md`); the open slice is the rest:
  (a) wire the side-view into **`WoolMonumentsStep`** so a monument's Y is editable, not read-only
  (lift it off y=0 onto terrain); (b) **per-side focus** — `FocusSection` is still a `/concepts` mockup;
  the canvas **fit-island** exists but not per-team quadrant framing — refine the concept so the author
  can frame one team's quadrant while working its unit. (`FocusSection`)
- [ ] **N09 — Team id should track the team's colour.** The team id is seeded from the colour first picked
  (`Id = colour.Replace(' ','-')`), but `TeamAssignStep.SetColor` only updates the colour — so recolouring a
  team (e.g. red → purple) leaves `id="red"` and every id derived from it (`only-red`, `red-spawn-point`,
  the `…-red-monument` blocks, `reds-woolrooms`). Functionally fine (PGM resolves the id) but reads wrong.
  Re-derive the id on colour change and **cascade the rename** across the intent — `teams`, `islandTeams`,
  and `spawns[].team` / `wools[].owner` / `wools[].monuments[].team` — with a guard to skip the rename (just
  recolour) when the new colour-derived id would collide with another team's.
- [~] **N11 — Monument Y must seat on terrain; coord-input moves must re-snap.** The **point tool** now
  seats every spawn it places — team spawns + orbit copies, the observer, and wool spawns — on the target
  column's floor via the shared `ColumnFloor` helper. Still open: monuments aren't seated at all; and moving
  a spawn (team or wool) via the **coord inputs** rewrites X/Z without re-snapping Y to the new column, so
  only the point tool re-seats. Pairs with `N08` (monument Y editing) and `CV11` (the side-view clamp side
  of the same problem).
- [~] **N12 — Configure has no destroyable phase.** Wools and Cores each have one and the objective phases
  are a group sharing one gate (`FEATURES.md`), so this is now the third phase slotting into machinery that
  already exists: add `destroyables` to `ConfigurePhases` + `IsObjective`, a `DestroyableAuthoring` slice
  beside `CoreAuthoring`, and the steps. A destroyable is the core's shape with a different structure — one
  region per defending team, no per-capturing-team monuments — but its knobs are style/materials/float
  rather than a casing. A DTM map authored in the plan tool can already be configured (the slice rides
  through untouched); what it cannot be is *seen* or edited there. Detection is a separate question and is
  `B58`: unlike a core, a destroyable has no signature of its own, so the phase should offer manual
  placement first and adopt candidates when that ranker lands.

## Sketch tool (S) — parked slices

The Sketch depth pass has shipped (`FEATURES.md` — select/drag, rotate, scale/squash, split, selection
highlight); these are the parked / dormant / deferred slices.

- [ ] **S34 — Reuse a sketch paint's column classification across the edits of one drag.** `TerrainProfile`
  construction is what a paint now costs — ~60 ms of the ~164 ms a 40k-cell board takes (S33, `FEATURES.md`),
  and roughly 35 ms of that is its two `GridComponents.Label` passes: one flood fill for plateaus, a second for
  landmasses, each sorting its seeds and hashing a coordinate pair per neighbour edge. They re-run from
  scratch on every step of a drag, though only the moved shape's neighbourhood changed and the plateau
  components are already a refinement of the landmass ones (equal-top cells are 4-connected), so the second
  pass could be merged out of the first. Whether the rest is worth an incremental cache depends on a number
  nobody has: a typical board is ~93 ms end to end now, so this is the 200×200 case, not the common one.

- [ ] **S40 — Offer "no building" in the Rooms step.** A bound room style has three answers — a style, absent
  (the built-in shell), and an explicit null meaning the pad stands on open ground with nothing over it
  (`docs/world-export/structures.md` §9). The export reads all three and the stampers have always accepted
  the third, but the step can only *bind* or *clear*, and clearing means the built-in rather than none. So a
  map can be authored open only by writing its layout by hand. The step needs a third control per kind, and
  `ReadBindings` needs to tell a null snapshot from a missing one — today the bridge state drops both, so an
  open room displays as unpicked (harmless until the author touches it, since the save preserves what it
  loaded).

- [ ] **S26 — Sweep the now-dead plan-side theme JS.** The Theme phase moved onto the sketch (`FEATURES.md`);
  the plan tool's Theme UI + C# theme path are gone, but the plan **JS** theme code is still present as an
  unreachable, self-consistent island: `plan-bridge.js` theme methods (`themesState`/`assignPiece`/`assignBox`/
  `themeApply`/`getThemes`/… + the `defaultThemeJson` import), `plan-canvas.js` theme mode
  (`setSelectOnly`/`setThemeOverlay`/`setThemePaint`/`getMultiSelection`/`#selSet`/`#themePaint`), and
  `plan-doc.js` theme storage (`defaultThemeJson` — now duplicated in `theme/theme-model.js` — plus the
  `themes`/`mapTheme`/`themeScopes` normalization). Remove them together (they cross-reference), keeping the
  generic `uniqueId`. Check whether plan-canvas multi-select (`#selSet`) has any non-theme use before dropping it.
- [ ] **S25b — Make the surfaced spawn/wool pieces movable, writing the move back to the intent.** S25 landed
  the pieces as **locked** read-only rectangles (`FEATURES.md`; `role`/`intentRef` on `SketchShape`, projected
  by `PlanCompiler`, skipped by the rasterizer, rendered as labelled boxes). The next slice makes them
  draggable: a move writes the new rect back to the intent's `Piece`, from which `Protection`/`Room`/marker
  re-derive, so the sketch and the intent don't diverge. **Resize stays deferred** even here — a spawn/wool's
  `at` is a fractional offset into the piece rect, so resizing shifts the marker and needs its own handling.
  Needs a write path (sketch → intent); the read projection already exists. Then extend beyond spawn/wool to
  the other intent entities (protection / build / monuments / iron) as they each earn a sketch surface.
- [ ] **S9b — Angle/parallel snapping + droppable guide lines (parked).** S9 landed **position** alignment
  (edges/centres snap to other shapes + the symmetry centre, with guides). The remaining picture-editor bits:
  **angle/parallel** snapping (rotate a shape so its edges run parallel to another's — "hold two lanes
  parallel"), and **manually droppable** guide lines shapes snap to (vs the current auto-from-shapes). Both
  are their own work; park until needed.
- [ ] **S12 — Pin the Islands tree to the top of the sketch sidebar (UI polish, parked).** Most of the weight
  the review named is gone: the shape palette was retired outright and Setup moved into its own Info phase, so
  the only panel still above **Islands** is **Layers**. Collapse it behind a `<details>` accordion, or pin the
  tree above it — the tree is read on every edit and the layer list is set once. (`docs/sketch-tool-ux-review.md`
  P0#1; `docs/contracts/sketch-creation-flow.md` follow-on.)

## Editor & canvas infrastructure (C / CV)

Shared infra for **both** the Configure wizard (`/maps/{id}/configure`) and the frozen Edit editor
(`/maps/{id}/edit`). `C12`/`C14` are cross-cutting (serve both surfaces); `C9`/`C11`
are Edit-specific. Full canvas spec: `docs/contracts/canvas-interaction.md`.

- [ ] **C9 — Kits editing UI (Teams) + per-activity status dots.** Spawn `kit` is read/sent but has no
  edit UI; there is no status-dot system. *(Two sub-items — split if priorities diverge.)*
- [ ] **C11 — Wire + verify inspector edits across activities.** `OnDelete`/`OnRename` are wired only
  in Build Regions; the Regions/Teams/Objective inspectors are **unwired** (rename/delete silently
  no-op). Wire all three + verify rename/delete/coord-patch end-to-end.
- [~] **C12 — Build the shared component vocabulary (atoms → sections → shells).** The studio has a
  consistent CSS design system but **no Blazor layer that renders it** — the canonical skeleton
  (`panel-section` → `section-header` → `section-title`) is hand-typed across 44 of 64 razor files and
  the app shell is copy-pasted 11×. Full audit, atomic tree, API conventions (foldered under
  `Components/`, param-first + slot override; global CSS, no `.razor.css`), and the class→component map
  are the **contract in `docs/contracts/ui-conventions.md`** — follow it; `/design` is the
  zero-visual-diff regression oracle (components emit the same classes). **Phases A–C + D.1–D.2 shipped**
  (`FEATURES.md`): the atoms + `Section`, the shell (`StudioShell` + topbar/rail/footer), the workspace
  shells (`Workspace`/`Sidebar`/`Inspector`/`ContentColumn`), and — across every production surface (0
  raw markup outside the `/concepts` + `/design` leave-raw zone) — `Section` (D.1) plus the atomic
  vocabulary `Field`/`Button`/`Badge`/`ListRow`/`Chip` (D.2). Remaining:

  **D.3 — build + adopt the new components.** `CoordField`, `DetailHeader` **done** (`FEATURES.md`); the
  `/design` gallery **regenerated** to render the real components. `FlowBar` — once deferred as
  single-use — **shipped** (C21) once the Editor/Configure shell-convergence work needed it in a
  second consumer; it backs both `ConfigureLayout` and Edit's stepped activities (Setup, Build).
  `Console` stays single-use (the pre-flight log in `ReviewPreflightStep`) — not worth componentizing
  yet, left raw (same call as `CoordRow`, dropped because `ctrl-row` triples vary XYZ/XZ/R·H).
  `Card`/`CardGrid` **deferred** (only ~8 landing cards; low payoff).

  **Open — `Icon` adoption.** `Components/Primitives/Icon.razor` is **built but unadopted**: `<i
  data-lucide="@Name" @key="@Name">`, centralizing the lucide reconciler gotcha (recreate-on-glyph-change
  rather than patch a lucide-mutated `<svg>`). The ~156 raw `<i data-lucide>` across components and pages
  still stand — adopt incrementally (the icon-bearing components `Button`/`DetailHeader`/`Chip`, then the
  re-rendering page sites) when picked up. High churn, subtle benefit, so parked by choice, not blocked.

  **Open — polish**: fold the 1 `section-heading` use into `SectionHeader`; drop the inline `style=`
  occurrences now expressible as component params (`Align`/`MaxWidth`/`Fill`).
- [ ] **C14 — Dedupe activity code-behind.** The repeated `Post/Patch/Delete/Send` http trio
  (Build/Objective/Teams) + the `Index`/`CollectDescendants` region-tree walkers (3–4 activities) →
  a shared `MapApiClient` and/or `EditorActivityBase` / static `RegionNode` helpers.
- [~] **C28 — The client's remaining test layers (smoke has landed).** The **smoke layer + runner shipped**
  as `C31` (`tools/e2e.sh`, `tests/e2e/`) — every route is swept for "renders and raises nothing", seeded
  from a composed board; `icons.mjs` (C30) added the first *positive* render assertion on top of it.
  `PgmStudio.Client` is still **absent from the coverage report** (no test project
  references it), and two layers are still open:
  **(a) mount/interop** — per canvas tool, assert the bridge mounted and the surface has a real size; this
  is the C29 class of bug (a canvas at 45% of its workspace for weeks, in two tools) and it is assertable
  without knowing user intent.
  **(b) scenarios** — one flow per tool, specifically *the path that creates the artifact*, where a break is
  unrecoverable rather than cosmetic: Sketch `New → name → draw → Finish → Configure`; Plan
  `New → globals → piece → Compile`. The seed already proves that chain works headlessly.
  Deliberately **not** e2e: field-level inspector behaviour and anything asserting where geometry lands —
  those rot; extract the decidable logic instead (`CV12`). A bUnit project for the phase/step state machines
  is still worth considering, and is independent of the above.
- [ ] **CV12 — Two thirds of the JS layer is never loaded by a test.** `npm test --
  --experimental-test-coverage` reports 82.8% over the 15 modules the 148 tests import (several at 100%:
  `transform`, `symmetry`, `islands`, `polygon`, `plan-inspect`), but **26 of 41 files / ~6,900 lines are
  absent from the report** — they are never imported, which the coverage output shows as silence rather
  than zero. The untested set is the whole interactive layer: every canvas (`world-canvas` 1046,
  `plan-canvas` 1017, `sketch-canvas` 871, `sideview-canvas`, `canvas-base`), every bridge, every
  controller, `iso-webgl`, and `studio.js`. The split is coherent — pure geometry is tested, DOM/canvas
  code is not — so the win is not "test the canvases" wholesale but **extracting the decidable logic they
  contain** (hit-testing, snapping, viewport/transform maths, selection resolution) into pure modules the
  existing `node --test` setup can reach without a DOM. Pairs with the JS consolidation review.
- [ ] **CV21 — the world canvas has a `build` layer nothing paints into.** Stating the layer stack once
  (`CV19`) surfaced two layers with no content. One was removed there — a `block-highlight` rect created
  `visibility:hidden` whose only handle was assigned and never read. This is the other: the `build` group is
  created empty, no painter ever appends to it, and its toggle `setBuildVisible` has no caller outside the
  class — not the bridge, not any of the sixteen hosts. So it is an empty group with a visibility switch
  nobody throws. Removing it takes `setBuildVisible`, `#showBuild`, `#paintBuildRegion` and one line of the
  documented public surface with it, which is why it was left in place rather than swept during a
  behaviour-preserving refactor. Check first whether a Build phase was *meant* to fill it (the name suggests
  the Build-Regions work) — if so the task is to wire it, not delete it, and that is a different task in the
  feature section.

- [ ] **CV15 — The bridge invoke wrapper is inconsistent.** `plan-bridge` and `sketch-bridge` wrap
  `dotnetRef.invokeMethodAsync` in a local `fire()` that swallows the throw when the host hasn't wired a
  callback; `world-bridge` calls it unguarded, so an unwired callback surfaces as a console error instead
  of a no-op. Settle on one helper next to `fetch-json.js`. Tiny, but it is the only thing the five bridges
  genuinely share — the rest of their apparent repetition is per-tool document semantics and should stay
  separate.
## Backend, pipeline & internals (B / P / A)

- [~] **B70 — The room-style *card* cannot show a porch or a window.** The open editor draws four views now
  (B71), the cutaway among them, so a style's porch and its windows read there. A library **card** still
  carries the section alone, and a section projected onto the front wall shows a window as a patch of the same
  colour as the wall around it. The sample is the other half: `RoomStylePreview` stamps the shipped 10×10
  piece's 8×8 shell, which is small enough that a porch leaves little room behind it. The library therefore
  still has knobs whose *card* does not change when they are turned, which is the one thing the preview exists
  to prevent. Wants a larger sample footprint, and a card that is not the one view those knobs are invisible in.

- [ ] **B72 — Two roof-thickness columns nothing reads.** `room_style.roof_thickness` (M0012) and
  `roof_style.thickness` (M0018) are written, clamped and round-tripped through the DTOs, and no stamper has
  ever read either: a roof's depth at a cell comes from the height field's own step down to its neighbours
  (`RoofField.Riser`), so there is nothing for a stored number to say. The composer offers no knob for it,
  which is the only reason it has not misled an author yet. Drop both columns and their DTO fields, or give
  the number a meaning under B69 — but not leave a third state where a row carries it and nothing looks.

- [ ] **B54 — A rebuild has no undo.** The rebuild now carries the finish and the credits across (B49, B52)
  and says what it trades before it runs (S39), so what it still replaces is replaced *on purpose*: the
  board, and the teams/spawns/wools/build zones the plan states. What is missing is a way back from a
  deliberate press that turns out to have been wrong. The mechanism is cheap, because both authored blobs
  are already rows in `map_artifact` keyed by a 64-char `kind` with no unique constraint: before each
  from-plan write, copy the current blob to a `…_prior` kind, and add a restore that puts both back and
  re-runs the pipeline from them (restore layout → `sketch/finish` → restore intent, the same chain the
  build uses, so the world cannot end up disagreeing with the layout). The finish step wants extracting out
  of `SketchFinishEndpoint` first so both callers share it. Surface it where the loss would be noticed: a
  one-shot *Undo this rebuild* in the plan editor's success panel. Deliberately not built with S39 — with
  the carries landed, the remaining exposure is a mis-click rather than silent data loss, and the
  confirmation already covers a mis-click at a fraction of the cost. This is the belt to that pair of
  braces, worth having once the studio is used by someone who did not write it.

- [~] **B44 — Theme + style library: the map's applied theme is still an inline blob.** The tables, the HTTP
  surface, the `/library` page and the sketch's pull/push bridge all shipped (`FEATURES.md`); two slices
  remain. **(1) Apply-as-snapshot** — a map's *applied* theme is still the sketch document's own registry, so
  "the library holds the reusable copy, the map holds a frozen one" is true only by convention: pulling a
  library theme into a sketch copies its JSON and nothing links them, but there is no snapshot record saying
  *which* library theme a map's paint came from, and no way to re-pull one when the library moves on. Give the
  map's scope store a forked instance with a `parent_id` back-reference, the same doctrine the generator's plan
  persistence uses. **(2) A data migration** lifting existing inline-blob themes (`plan_json`,
  `map_intent_json` on `map_artifact`) into styles + themes + bindings, deduping identical materials — today
  every map authored before the library keeps its blob and the library cannot see it. The cross-tool scoping
  and dressing sections of `docs/world-export/finishing-model.md` stay draft.

- [ ] **B47 — The library has no search, and the sketch's theme names are its own.** Two small gaps the
  library page left open, worth doing once it has enough rows to hurt. The style browser filters by kind but
  not by name, so a library of forty styles is a scroll; the theme half has no filter at all. And a theme
  pulled into a sketch takes the library's name as its sketch-side id, which the bridge uniquifies — pull the
  same theme twice and the second is `meadow-2` with nothing saying they are the same theme. A name search box
  on both halves, and a note on the pulled theme recording where it came from (which slots into B44's
  snapshot record rather than duplicating it).

- [ ] **B9 — Re-import a world into an existing map (keep the authored intent).** When an author tweaks the
  terrain (e.g. adds iron inside the spawns so the renewable populates) they currently have to import the
  updated world as a *new* map and hand-copy the intent across. Add a "re-import / update world" action on
  an intent-authored map that re-scans a chosen folder/zip in place — refreshing only the world-derived
  data (`islands_json`, `resource_block`, surface/layer parquets, monument candidates) and **preserving the
  `map_intent_json`**, then regenerating. Safe while island detection stays stable (the intent references
  islands by id, and spawns/wools are world coordinates); flag the author when the island set changes so a
  stale `islandTeams` mapping can be re-checked. (Manual procedure today: copy the `map_intent_json`
  artifact + re-scan, then `PUT /map/{slug}/intent`.)
- [ ] **B35 — Endpoint coverage: half the API is exercised by nothing.** `PgmStudio.Api` sits at **42.8%**
  lines (`tools/coverage.sh`), and the shortfall is not spread evenly — a long tail of endpoint files is
  effectively untouched while the tested ones are fine: `PreflightEndpoint` 2.6%, `ImportEndpoints` 3.6%,
  `IslandRolesEndpoint` 3.6%, `MonumentEndpoints` 5.3%, `LayersEndpoints` 5.5%, `ConfigureEndpoints` 6.2%,
  `AuthoringEndpoint` 8.2%, `IslandReviewEndpoints` 8.6%, `MapPlanEndpoints` 12.3%, `AnalysisEndpoints`
  13.0%, `RegionEndpoints` 15.6%. `ApiTestFactory` (B20) already gives schema-isolated MariaDB, so the
  marginal cost per endpoint is one happy path plus its error contract; these are cheap tests, not a
  redesign. Prioritise the ones that write: import, configure, region and map-plan.
- [ ] **B36 — The region/filter authoring-and-editing path is half covered.** A coherent cluster sits
  around 40–58% while its neighbours are high: `RegionAuthoringEncoder` 43.8% (370 uncovered lines),
  `RegionParser` 52.0% (295), `RegionEditor` 57.5% (180), `FilterParser` 48.9%, `RegionGeometry2d` 39.5%,
  `RegionBuilder` 43.7%, `FilterEditor` 41.9%, `WoolEditor` 58.5%. This matters more than the endpoint tail
  because it is map-contract logic, not glue — a silent regression here changes generated `map.xml` rather
  than returning a wrong status code, and `--authoring` is a manual harness, not a gate. Note the
  neighbours prove the standard is reachable: `MapParser` 92.9%, `XmlWriter` 88.1%, `RegionCategorizer`
  91.4%. Cover the type-specific region/filter branches first — that is where the uncovered lines are.
- [ ] **B37 — Objective separation via WX9 placeability: unify the resolutions, not the stampers.** The
  stamped-structure placeability attribute (`docs/world-export/structures.md` WX9, shipped for spawn iron)
  generalizes to the objectives: a **core or destroyable too close to a wool monument** merges structures
  that must read apart, and one **inside a spawn piece** is worse than ugly — the spawn's protection region
  makes an enemy goal unbreakable, so the map is silently unwinnable. The intended shape: the stampers stay
  heterogeneous (their inputs are irreducibly different — a wall owns a seam, a room a piece + marker +
  entries, an objective a marker + style), but every family's resolver produces one **resolved-stamp
  record** — kind, footprint box, `Placeable`, source marker — the uniform currency the pairwise separation
  rules run over (minimum distances to be decided from the corpus). `IronResolution` is the first instance;
  the preview's `StructureBox` is the drawing-side proof of the shape and would consume the record instead
  of assembling its own (placeability then reaches the iso view for free; `StructureBox` stays a separate
  type from `BlockBox` — exclusive maxes plus `Kind`/`Color`, a drawing frame rather than a volume). Stamp only placeable structures, flag the rest with the same
  marker-stays-visible discipline. Editor half: surface unplaceable markers on the plan canvas (the
  highlight ring the validation tab already uses for pieces), not only in the findings list.
  **Measured since filing.** The unwinnable case is worse than "unbreakable by the enemy": spawn protection
  emits `block="never"` on the shared `spawns` union (`TeamsGenerator`), so an objective inside it cannot be
  broken by *anyone*, the attacking team included. The wool path already solves exactly this —
  `WoolGenerator.SubtractMonumentsFromSpawns` folds each monument block out of the union so capturing a wool
  does not trip the rule — and cores and destroyables have no equivalent, which is why they need the
  separation rule rather than a fold: a goal inside enemy spawn is a design error, not a case to work
  around. `Placeable` still exists only on `IronResolution` (four consumers, all iron), and **preflight
  checks round-trip, mirror, buildability and traversability only** — so the Configure path, which can now
  author a core (`N12`), has no separation check at all while the plan path at least errors at compile.
  Three pieces landed since that make the record cheaper to build: `BlockBox` is now the one inclusive AABB
  (`B33`), a core's footprint is genuinely variable so a rule may no longer assume 5×5 (`G160`), and
  `CoreIntent.Box`/`DestroyableIntent.Box` are populated on **both** orbit images (`B53`), so a rule reading
  the expanded intent sees the real pair. **The objective half of the separation rules has since shipped as
  `OB17`** (`FEATURES.md`) — void, spawn room and wool room, as compile-blocking errors over a shared
  `ObjectiveFootprint`. What that leaves for B37 is the part it was really about: the uniform
  resolved-stamp record every family's resolver produces (`OB17` computes footprints inline rather than
  through one), the objective↔objective and objective↔monument minimum distances still to be drawn from the
  corpus, `StructureBox` consuming the record instead of assembling its own, and the editor half — an
  unplaceable marker outlined on the plan canvas — **which has since shipped end to end** (`B59`, `C44`,
  `FEATURES.md`): markers carry a persisted id, a finding names one, and clicking that finding rings the
  marker on the board. What is left of the editor half is only its timing: structural findings do not run in
  the live feed (`G161`), so a refusal appears at Compile rather than as the marker is dragged.

- [ ] **B34 — The two map-list endpoints disagree on sort order, and the dashboard gets the noisy one.**
  `MapsListEndpoint` branches on the `stage` query param onto two differently-ordered repository methods:
  `MapRepository.ListAsync` sorts `OrderBy(Slug)`, `ListByStageAsync` sorts
  `OrderByDescending(UpdatedAt).ThenBy(Slug)`. The dashboard always requests `?stage=…`, so it always gets
  recency order — and on the imported Edit corpus `updated_at` records when the *pipeline* last wrote the
  row, not when the author last worked on the map, so it carries no authoring signal. The 349 Edit rows hold
  only 29 distinct timestamps (a re-processing pass stamped them in ~22-map batches a second apart), so the
  list renders as 29 alphabetical runs concatenated — it reads as scrambled, with the three maps outside the
  supported range (`3084`, `allure`, `lost_haven`, never re-processed) parked at the bottom. Recency earns
  its keep on Sketches/Plans/Configuring, where the timestamps are real edits and the lists are short.
  Preferred fix: slug order for the Edit stage, recency for the other three (one line); alternatives are
  slug everywhere, or leave it and let recency come good once maps are edited in the studio. Cosmetic — no
  data is wrong, and both orders are deterministic.
- [ ] **B40 — The three dock styles are implicit; make them a type.** `Seat` picks between three seating
  rules using three *different discriminators* — `d.Wool is { } rich && Overhangs(rich.Family)` (shape
  family), `d.Kind == BoxKind.Frontline` (box kind), and falling through (everything else) — so there is
  nowhere in the code to ask which style a demand uses. The asymmetry runs deeper than the selector:
  two styles are named functions and the third is ~30 lines of inline loop body with no name; and the three
  are at different altitudes — `SeatOverhang`/`SeatFront` return a placed `(CellRect, BoxInterface)` while
  `SeatInRuns` returns a bare `int?` seat, leaving the caller to build the rect and the joint. That is why
  `boxes.Add`/`joints.Add` is written out three times with different arguments.
  **Scope:** (a) extract `SeatFullMouth` returning `(CellRect, BoxInterface)?` like its siblings, then (b)
  add an explicit `DockStyle { FullMouth, Overhang, ContactPatch }` **derived** from the demand — it is never
  sampled, it follows from the family roll — and dispatch on it. **Leave the failure policies alone**: they
  genuinely differ in kind (overhang demotes via `Compact`, the frontline kills the attempt, a wool may be
  dropped if another remains), and flattening them into flags loses more than it gains.
  The doc comment writes itself, because the three styles are indexed by *how much is known about the
  shape's entries*: full mouth knows nothing (require the whole mouth on one run and every entry lands —
  which is why the two-entry `U`/`H`/`Clamp` dock here); overhang knows there is exactly one and where it
  is; contact patch has no entry at all because a frontline is a face, not a corridor.
  **Oracle + the real constraint:** `ComposerFingerprint` + `ComposerVersionTests` must stay byte-identical.
  All three styles consume `rng`, so the invariant is not "does it compile" but **does the draw order
  survive** — hoisting one call above another moves the stream and every fingerprint goes red.

- [~] **B41 — Should a host's published capacity bound the grant it hands out?** The naming half has landed
  (`FEATURES.md`): `BoxJoint.Grant` is now distinct from a host's `EdgeOffer`, and the docstrings state the
  split — an offer's `WidthClass` is a *capacity* derived from the run's length, a grant's is a *selection*
  made per consumer kind. What remains is the behaviour question the rename deliberately did not answer.
  Today the two are entirely unlinked: `Seat` reads the hub's offers, keeps only `(Start, LengthCells)` as its
  **runs** and drops the published width, then `HubJoint` grants a width taken from the demand's kind
  (`WoolLaneCells` for a wool, `w` otherwise). So a hub can grant a corridor **wider than the run it sits on
  claims to support** and nothing objects. Either that is intended — capacity is advisory, the consumer knows
  its own lane — or the grant should be clamped to the offer, in which case a narrow run would demote a
  consumer's `cw` and some docks that succeed today would not.
  **Measure before deciding**: how often does the granted width actually exceed the published capacity of the
  run it lands on? If never, this is documentation; if often, it is a real gate the composer is missing.
  Changes what the filler builds, so it needs a before/after gallery and will move fingerprints.

- [ ] **B21 — MCP server: agent-drivable map authoring over the plan layer.** A thin MCP head (official
  C# SDK, `ModelContextProtocol` NuGet; new `PgmStudio.Mcp` project or a proxy over the running `:7894`
  API) so an AI agent can build a map end-to-end. The plan layer is the agent surface — `plan.json` is
  small, semantic, and the validator/evaluator return rule-id findings, giving the agent a compiler-style
  submit→lint→fix loop. **The gate this waited on is open: G119, G117 and G125 have all landed** (with
  G126's typed boxes), so the MCP head now wraps endpoints that exist rather than duplicating their
  plumbing. Of the three genuinely-new pieces, **`emit_family` is half-built**: G144's
  `GET /api/shapes/probe` already emits a canonical family through `BoxFiller` — profile check and
  docking gate included — and answers with the shape or a directed `FillRejection`, and
  `/api/shapes/probe/schema` serves the per-family knob surface and minimum box in the dock frame, which
  is the payload a tool-description needs to constrain its parameter enum. What is left of it is the
  *stamp*: placing the emission into an existing plan's typed box instead of returning a standalone
  `symmetry:none` plan. So the remaining new work is the PNG path for `plan_render`, that stamp, and the
  resource curation. Tools: `compose` (the G117 endpoint —
  request in; plan + canonical descriptor + derived facts + score out; starting material to mutate) ·
  `plan_validate` (errors + rule lint + full evaluator readout — the response must flag empty
  `placements`, which leave the feel terms vacuously green) · **`plan_feasibility`** (the G125
  read-back: mask → derived params → emit-compare, directed verdicts citing rule/task ids — the oracle
  that makes the loop converge; the validator alone passes plans the composer cannot produce, proven by
  the funnel exemplars scoring 0) · **`emit_family`** (stamp a canonical shape through the real emitters
  into a typed G126 box — agents never hand-cut rectangles) · `plan_render` (image content — agents
  self-correct far better seeing the board) · `plan_save`/`plan_get`/`plan_list` (the G119 store, with
  an agent-authored origin marking so agent output never contaminates the human-labeled corpus) ·
  `create_draft`/`export` (existing chain; return the export **link**, never the world zip inline). MCP
  resources: `generator/rules.md` + `generator/model.md` as the design brief, **`GET /api/shapes/catalog`
  as the machine-readable half of that brief** (G144 — the same vocabulary computed from the emitters, so
  unlike the prose it cannot drift, and each entry carries the tier that says whether the composer really
  produces it), `tools/seeds/*.plan.json` (incl. the funnel exemplars) as few-shot examples, and the G118
  verdict JSONL once it exists. **G146 blocks this, as correctness rather than tidiness**: the design
  brief describes Z as a production family and no sampler draws one, so an agent that reads the prose and
  asks for a Z authors a plan `plan_feasibility` then rejects — a loop that cannot converge, caused by a
  doc/code disagreement the catalog's tiering made visible. Either land G146 first or restrict
  `emit_family` to the catalog's in-mix tier. The same reasoning applies to the verdict JSONL: few-shot
  examples inherit whatever the sampler over-produces (measured at G144: the donut is 73 of 89 wool
  cards, U/H/L one apiece), so an unstratified corpus teaches an agent to write donuts. Scope
  is the **author agent** only; the **analyst agent** (mine verdicts/reject logs for rule + envelope
  refinements — read-only `verdicts_export`/`rejects_query`) is a small follow-on once the corpus has
  data, not before.

**DTM / DTC objectives (destroyables + cores).** The contract is `docs/contracts/destroyables-and-cores.md`
— it owns the XML surface, the **world-measured** structure families, the schema, and the two-team scope;
its rule ids (`OB*`/`DT*`/`DC*`) are cited below. Filed here (not `N`/`G`) because the bulk of each is
pipeline — parser, writer, schema, intent, stamper — with the plan-editor placement as the last mile.
**Both objectives now author end to end** (`FEATURES.md`): parse/write/codec, the schema, the world stamps,
and plan → intent → world → `map.xml` for destroyables (`B24`) and cores (`B25`). What is left below is the
import diagnostic (`B24e`), detection (`B26`), and the island-floor work the phantom classifier unblocked
(`B31`). The other thing it unblocked — water lanes — has shipped (`FEATURES.md`, `docs/contracts/water-lanes.md`).

- [ ] **B24e — Flag an *imported* map whose objective region holds none of its material (a warning, not a
  gate).** Scoped down: the authored half of this is **already covered by tests** — `DestroyableWorldTests`
  and `CoreWorldTests` walk each emitted region with PGM's `[min, max)` and count the blocks, which is
  exactly the assertion this task was filed to add. For a generated map the region *is* the stamper's box
  (OB8), so a runtime gate would re-check something true by construction. **What has no cover is the import
  side**: the corpus sweep found **10 destroyables whose region contains none of its declared material**.
  Those are the author's own maps, already broken before we touched them — so this is a **diagnostic on
  import**, not a block on re-export. Blocking someone's export over a pre-existing dud is the studio
  overreaching; telling them is the value.
  Never "the region is full": by OB12 a region is legitimately mostly air (a 3×3×3 region holding a 1×3×1
  pillar is correct and common), so anything stricter flags most of the corpus.
  **Note the category difference** before extending `MapValidity`: its one existing rule (a wool needs a
  monument) is *"PGM refuses to load this map"* — an `InvalidXMLException`, so the map is unloadable. This
  one is *"PGM loads it fine and the goal has zero health"*, which PGM itself only logs a warning for. Two
  different severities of truth; do not blur them into one list without saying which is which. World access
  is **not** the blocker it was originally filed as — 14 test files already read blocks out of a built
  world. (OB3, OB11, OB12)
- [ ] **B57 — `layer_segment` counts a build-region marker as solid ground.** Island detection now separates
  terrain from markers and from what a map erases before play (`FEATURES.md`,
  `docs/contracts/terrain-ground-truth.md`), but that runs on `CleanColumns` → `islands_json` only. The other
  ingest derivation, `FeatureExtractors.Segments` → `layer_segment`, has its own exclusion set and applies
  neither rule, so a floor sheet at `y=0` persists as a solid span. Everything reading it at query time
  (`SegmentIndex.BaseColumns` → `IslandDetector.CleanedBaseFootprint`) therefore walks on a marker. Narrower
  than it sounds — that path feeds kit-reach, not the island picture the configure tool draws — which is why
  it is filed rather than fixed alongside. The two derivations should agree on what ground is, and the fix is
  to route the floor-marker rule through both. **Blocked in practice by re-import**: `layer_segment` is
  written once at ingest from a world that is then discarded, so changing it reaches existing maps only when
  a map can be re-imported.

- [ ] **B55 — Decide which API paths read a map *as played*, then wire `Includes:Root`.** `IncludeLibrary`
  and the resolved parse are in and gated by tests (`FEATURES.md`), and the harness uses them
  (`--resolve-includes`, `--water-lanes --includes-dir`). The API does not, because which reading each
  endpoint wants is a real question and the wrong answer corrupts data: **a resolved document must never be
  written back** (the include references are still emitted, so the fragments' content would be applied
  twice). Safe by construction today — nothing in the API passes a library — and the work is to choose per
  path rather than flip a global. Rule-level analysis (region categorisation, filter wiring, apply-rule
  order) wants resolved; anything that saves, exports or re-emits a document wants written; geometry
  (islands, layout, the seed corpus) does not care, since maps declare their own regions. Add
  `Includes:Root` beside `MapsRoots` in `Program.cs`, thread it only to the chosen paths, and make the
  distinction unmissable at the call site. (`docs/contracts/include-resolution.md` §2)

- [ ] **B56 — Parse `<score>` and `<flags>` so an include-supplied objective is actually read.** Include
  resolution landed (`FEATURES.md`) and measured its own limit: **82 corpus maps take their objective from a
  fragment** (`bridge`, `touchdown`, `ffb`, `flag-battles`, `5cp`), and resolving them changes nothing about
  what the studio reports, because `<score>` (TDM) and `<flags>` (CTF) have no parser here. Splicing is not
  what closes that; a parser is. Until one exists those maps read as objective-less — which the
  supported-range gate deliberately tolerates, since a module arriving from a fragment round-trips through the
  include reference and cannot be silently lost. Add each tag to `ParsedObjectiveModules` as its parser lands.
  (`docs/contracts/include-resolution.md` §4)

- [~] **B58 — Finish the destroyable ranker.** The core half has shipped — gathered at ingest, stored in
  `core_candidate`, and confirmed in the Cores phase (`FEATURES.md`). What remains is the other objective,
  and it is measured but unbuilt (`docs/contracts/objective-suggestion.md`).
  **Destroyables: the discriminating signals are measured, the detector is not written.** They are not
  identified by anything about the structure — size spans 1 to 31,105 blocks and fill is uninformative — but by
  their **neighbourhood**, dumped 10 blocks outward and down to `y=0` for all 614 declared structures.
  *Isolation*: a declared destroyable has a median of 6 same-material blocks within 10, against 65+ for a false
  cluster, because decoration repeats and a goal is placed once. *Elevation*: it sits a median +5 blocks above
  the surrounding terrain, against −2 for false clusters. Together, with **no size cap and no air-face test**
  (both of which were discarding truth), `same ≤ 8 & elevation ≥ +2` keeps 553 of 1,062 true clusters against
  600 false — 48% precision at 52% recall, a four-fold precision gain on the previous best. `same ≤ 0 &
  elevation ≥ +2` reaches 65.6% precision if a stricter list is wanted.
  Build the detector at those operating points, gather at ingest into a `destroyable_candidate` table beside
  `core_candidate`, and validate the same two ways cores are (corpus + a composed plan). **Scope honestly to
  84%**: obsidian, emerald, gold and ender stone carry that share of declared destroyables, and the wool /
  stained-clay / stained-glass remainder must stay out — admitting wool takes the candidate set from 15,488
  clusters to 439,440, because a CTW map is made of wool.

- [ ] **CV16 — the authoring canvases have no frame budget, only habits.** The zoom stall (fixed in
  `FEATURES.md`) was two unrelated per-event costs that happened to land on the same handler, and neither was
  visible until measured: a grid rebuild whose memo was written for pan, and a `.NET` interop call per wheel
  tick. Both are the same class of mistake — doing work per *input event* rather than per *frame* — and
  nothing in the canvases prevents the next one. Two guards worth having: a debug overlay (or an e2e probe)
  that reports main-thread ms per interaction burst, so a regression shows up as a number rather than as
  someone noticing the picture go soft; and a rule that anything crossing into Blazor from a canvas handler
  goes through the frame coalescer, since interop is the expensive edge and its cost is invisible from the JS
  side. The screenshot approach does **not** work for this class of bug — `page.screenshot()` forces a fresh
  raster, so a transient compositor artifact never appears in the capture; measure the handler, not the pixels.

- [ ] **G158 — seed the library with a curated set.** An author can now build a style once and reuse it, and a
  theme that binds only the buckets it changes (`FEATURES.md`), but a fresh install's library is empty — so the
  first desert or snowfield is still built by hand. Ship a curated set of styles and themes as seed rows: the
  shipping finish decomposed, plus a handful of biomes (desert, tundra, mesa, nether) each reusing the same rim
  and fill. A preset is just a library theme, so this is a seeding step, not a second mechanism — the open
  question is only *when* it seeds (a migration, or a "restore the starter set" action that cannot clobber
  edits).

- [ ] **G156 — cell-size-aware generator room sizing (WX2's generator half).** The stamped-room minimum is
  8×8 **blocks** (`docs/world-export/structures.md` WX2) but the emitters size rooms in **cells**
  (`ShapeEmitter.RoomDepthCells` = 2, corridor widths in cells), so a small-cell board (cell ≤ 3) can emit
  a wool room or spawn its own export refuses. Make the room depth/width floors cell-size-aware — enough
  cells to reach the block minimum — through `MinBox` and the spawn profile; the composer's cell-5 boards
  already clear it by construction, so this binds only when boards go small-cell.

- [ ] **G163 — `map-layers`' rebuild-confirmation step flakes about one run in three.** The step drives
  Compile on a freshly-opened plan and reads the drawer; when the plan document has not reached the client
  yet it compiles an empty plan, which is a 422 by design, so the drawer never opens and the following
  `page.click` times out at 30s. The spec guards it with a fixed `waitForTimeout(1500)` — a duration
  standing in for a condition, and the wrong guess about a third of the time. Measured 1-in-3 both with and
  without the `OB17` rule, so it is timing rather than validation. Waiting on the first piece id label
  (`.map-canvas-svg text`, the overlay's proof the document arrived) was tried and did **not** fix it, so
  the stall is later than the document load. **A caught failure now names the click, and it is not the one
  the paragraph above blames.** The step got as far as reading the drawer's button label ("the button names
  a rebuild" passed on `Rebuild this map`) and then timed out on `page.click("Rebuild this map")` — the
  *second* click, on a compile that answered 200, long before the empty-plan compile the 1500ms guard is
  aimed at. The recorded 422 is an earlier fault on the same page, not this one. A 30s timeout on a button
  whose text was just read means the element was found but never became actionable, which points at a
  drawer that keeps re-rendering rather than at a document that has not arrived — so the fix is a wait on
  the drawer settling, and the 1500ms guard may be guarding nothing. A flake in the browser gate costs more
  than the step is worth, because it makes every unrelated run ambiguous.

- [ ] **G161 — the casing panel lets an author build a core the compiler will refuse.** `G160` put the
  casing knobs in the plan's marker panel, clamped independently — size 1..64, shell 1..16 — so a 5×5 casing
  with a shell of 3 is one keystroke away and leaves `size − 2·shell = −1`, a solid block of obsidian that
  can never leak. `PlanValidator` catches it with a good message, but only at the **compile** gate (422):
  the live inspect/evaluate feed does not run `Validate`, so the author sets the number, sees nothing, and
  finds out a phase later. Either state the interior in the panel as the two numbers move (it is the honest
  readout — "3×3×3 lava inside", or the reason there is none), or run the structural findings in the live
  feed so every rule reports where the edit happened. The second is the general fix and covers the
  destroyable style and float/leak rules too.

- [ ] **G171 — the grown crown is seated beyond the branch it hangs on.** `TreeCrown.Clusters` puts a
  cluster centre at `tip + outward*1.6 + (0, outward.Y*1.4 + height*0.25, 0)`, a median 3.0–3.3 blocks of
  lift against a vertical half-height of only 1.2–3.0, so the branch tip lies outside its own ellipsoid in
  ~100% of clusters and **1,516 of 4,020 (37.7%) never touch their branch at all** over an 8×5×12 sweep
  (`tools/tree-corpus/grower-tip-gap.cs`, `grower-crown-check.cs`). The crown coheres only because
  neighbours overlap and rescue each other, which is the same reason it reads as one merged mass; at height
  6–9 it does not cohere and 42–76% of the foliage is stranded. Seat the cluster so its ellipsoid contains
  its tip. The hand-built corpus puts 30.3% of leaves in direct contact with wood, so the docstring premise
  that leaves sit only at branch ends and not on the wood is what has to give
  (`docs/world-export/tree-corpus.md`).

- [ ] **G172 — nothing gates how a generated leaf attaches.** The corpus supports three thresholds
  measurable straight off the emitted cells: ≥99% of leaves reach wood through a chain of leaves, ≥25%
  touch wood directly, and fewer than ~9 occupied neighbours per leaf. Hand-built sits at 99.95% / 30.3% /
  6.2, the grower at 98.7% / 18.7% / 13.1. `tools/tree-corpus/leaf-contact.cs` is the reading; a test over a
  seed sweep is the gate. Without it G171 can be "fixed" and silently regress.

- [ ] **G173 — the grower has no tiered crown, so a conifer preset cannot be one.** `decoration.md` §6
  already names the six-presets-one-silhouette problem; the corpus now says what the missing shape is worth.
  Four of its fourteen families are conifers and separate from the other ten with no overlap on two
  measures — the widest tenth of the crown sits in the bottom third, and the crown is built in 3–7 tiers
  against never more than 2 — with **tier spacing of 4.6–5.8 courses** across all four. The grower produces
  no tiers at any setting.

- [ ] **G174 — a generated limb leaves the trunk too steep and too short, and its wood is too solid.** The
  wood network of all 75 hand-built trees, read against the same reading of the grower's own swept wood
  (`tools/tree-corpus/wood-skeleton.cs`, `--grower`), puts three numbers on it. A first-order limb leaves the
  stem at **59°** off vertical where a generated one sits at **41°** (`BranchAngle = 0.55 rad`), and reaches
  **0.40 of its stem's reach** where a generated one reaches **0.20** — so relative to its own trunk the
  generated branch is half the length, even though `LengthFactor = 0.62` is larger than the wool tree's 0.29,
  because the grown axis is so much longer than an author's bole. The proportion to aim at is the one measured
  in blocks. Third, generated wood carries **9.6** occupied neighbours per block against the corpus's **6.3**
  and thins far less on the way out (at ten steps from the stem: 3.4 neighbours and 21% ends, against 2.1 and
  35%) — the same solid-versus-lace finding G172 gates for foliage, and gateable the same way.

- [ ] **G175 — a thin sweep sample can place no block at all, so generated twigs break up.** `SweptVolume.Ball`
  tests the distance from the limb's continuous centre to a candidate cell's **integer coordinate**, so a
  centre near a cell corner is √3/2 = 0.866 from every candidate, while the only floor —
  `radius < 0.5` fills the containing cell — sits below that. In the band between, a sample fills nothing: 33%
  of positions at radius 0.55, 20% at 0.60, 48% at exactly 0.5. `TreeSkeleton` floors a limb's end radius at
  0.55 and the axis's at 0.5, so every twig is in that band — **5,322 of 25,392 sweep samples (21.0%) place no
  block** over an 8 height × 12 seed sweep, and **18 of 96 grown trees emit wood in more than one piece** with
  10 blocks touching no wood at all (the corpus: 3 trees, 2 blocks). Fill the containing cell whenever a ball
  would otherwise be empty — or test against cell centres rather than corners — and the twigs stop dissolving.
  The path sampling is not the cause: on the limbs that break, the widest step between samples is 0.51 blocks.
  Measured by `tools/tree-corpus/wood-skeleton.cs --grower`; `docs/world-export/tree-corpus.md`.

- [ ] **G150 — stamp a catalog shape into a drawn box.** The plan editor can draw a typed box and then ask
  whether the composer could have produced what is in it (G125's feasibility panel), but there is no way to
  go the other direction and *place* something known-producible: nothing in `Features/Plan/` references the
  catalog or the emitters. So an author hand-cuts rectangles and finds out afterwards. Give a selected box a
  **family picker** — the in-mix tier of `GET /api/shapes/catalog` (G144), which is exactly the set the
  composer really samples — plus the knobs `GET /api/shapes/probe` already serves per family, and stamp the
  emission into the box as its members. Producible **by construction**, so the feasibility panel goes green
  without the author aiming at it.
  Most of this exists. The probe endpoint already emits through `BoxFiller` (profile check and docking gate
  included) and answers with the shape or a directed `FillRejection`; `/api/shapes/probe/schema` already
  serves the per-family knob surface and minimum box in the dock frame. What is new is the *stamp*: writing
  the emission into an existing plan's box rather than returning a standalone `symmetry:none` plan, which
  means placing pieces at the box's origin, giving them ids under the box, and replacing whatever was there.
  This is the editor half of **B21's `emit_family`** — build it here and the MCP tool wraps it rather than
  reimplementing it. It also pairs with G149: placing known-producible shapes and watching the G148 land
  readout move is the most direct way to find out what the budget is actually worth.

- [ ] **G151 — a box's rect should be the bounding box of its members.** The members inspector offers
  "Fix these members" / "Follow containment", and the fixed half behaves oddly on purpose-built-for-something-
  else grounds: named membership (`PlanBoxes.MembersOf`, the `Members` list) ignores geometry entirely, so a
  piece can be dragged *out* of its box and still be carried when the box moves (`plan-canvas.js`, the box
  drag translates `d.carried` in both modes). That is not an authoring mode — named membership exists for
  **provenance**, so a pinned board can record the grouping that actually produced it off `BoxPartition.KeyOf`
  rather than having it re-derived approximately. Exposing it as a toggle asks the author to edit with a
  mechanism built to preserve history.
  The fix is not a third mode, it is separating two questions that are currently one button. **Which pieces
  are members** is legitimately two modes (named vs containment) and both should stay. **What the rect is**
  should not be a mode at all: it should always be the bounding box of the members. That is not a new rule —
  `BoxPartition.Of` already computes a box's rect as `Bbox(members)`, and `Box`'s own contract says a box's
  contents "must touch its edges", which is the same statement. So dragging a member extends the box,
  dragging the box moves its members, and a member outside its own box becomes unrepresentable rather than
  merely strange. One case to decide: an empty box has no bounding box — keep its drawn rect until it has a
  member, which also leaves the draw-then-fill flow working as it does now.

- [ ] **G153 — the feasibility read is per box and is reached through a list.** `G125` computes producibility
  **per box** and renders it in a left-panel list, so after clicking a box on the canvas the author has to find
  that same box again, by name, in a sidebar — for a read that is already about the thing under the cursor. The
  inspector on the right already opens on that box with its id, kind and members; the verdict belongs there,
  beside them: the parameter tuple that reproduces it or the nearest candidate, its directed findings, and the
  rule or task id each finding cites, with the click that paints the missing/extra cells kept as it is. The
  unit-level arrangement findings are genuinely **not** per box (parallel fronts, the frontline's pinned face,
  seat separation) and stay in the left panel, which leaves that panel one coherent job instead of a mixed list.

- [ ] **G154 — one plan editor, two bindings, two different tools.** `PlanTool` serves `/plan-editor` and
  `/maps/{slug}/plan` from a single component through five `@if (MapBacked)` branches, and the two render as
  different products. Map-backed gets the phase rail (Info · Draw), the flow bar, and the three panels as chips;
  the bare route gets no flow bar, no phases, the same three panels as **rail buttons**, and a collapsible
  sidebar the map-backed one cannot have (`SidebarOpen => MapBacked || leftOpen`). Same panels, two navigation
  models, one file — the thing `docs/contracts/tool-consistency.md` exists to prevent.
  Unify on the phase-rail + flow-bar + chips structure and keep the collapsible sidebar for both. The route may
  change **only** the topbar — its crumbs and which actions exist — because that is where the binding genuinely
  differs: a map-backed plan saves into its map's artifact, while a plan row saves as a row and forks when it
  was generated or imported. Rename the bare route to `/plans/{id}` (and `/plans/new`), which says what it is
  bound to where `/plan-editor` says nothing, updating the generator hand-off, the smoke sweep's route list and
  `plan-editor.md` with it.
  **Do not delete the route.** It is the only surface that opens a **plan row**, which is what the generator
  hands a candidate off as and what `G119`'s fork-on-edit rule operates on; routing candidates through
  `/maps/{slug}/plan` would mint a map per candidate looked at, and New, Import, Open and the origin badge have
  no home on a map-backed plan.

- [ ] **G149 — the land budget is a number the composer reads and then overshoots.** The first thing the
  G148 readout showed, measured over 40 boards at 12 players/team (budget 50 cells): land runs **63% to 222%**
  of budget, median **115%**, and **28 of 40 boards are over**. So the budget is not a cap — it is an input
  the allocator consults for its decisions (lane width, whether there is a frontline, the hub caps, the wool
  count — `TeamUnitAllocator.cs:46-52`, `UnitTuning.WoolCount`) and then nothing reconciles the result
  against it. `BoxFiller.WithinLandTarget` exists and no production path calls it.
  Nothing caught this because nothing measures it: the only land-ish term is `fill-ratio` (G8), which is land
  over the board's **bounding box**, not land against the **budget** — a different quantity that can sit
  happily in its band while the unit is at double its target.
  Decide what the budget means before changing anything, because both readings are defensible: either it is a
  target the fill should be reconciled against (then something must spend the overshoot down — the
  two-currency accounting says fragment converts surplus land to build, so the question is whether fragment
  ever runs), or it is only a sizing heuristic (then rename it, drop the "budget" framing, and stop implying
  a contract that does not exist). What is not defensible is the current state, where a number named budget is
  exceeded by half again on a typical board and nothing says so. Note G138 is adjacent but distinct: that one
  is about the composer taking the first acceptable plan rather than ranking; this is about the plan it takes
  not honouring its own sizing input.

- [ ] **G147 — verdict coverage on the catalog: which buckets has nobody judged?** *(sequenced after G118 —
  there is nothing to count until verdicts exist.)* The browse feed hands the author whatever the composer
  samples, so collection is passive: the corpus ends up shaped like the sampler, and the parts of the space
  the sampler rarely visits stay permanently unjudged. The measured skew makes that concrete — the donut is
  73 of the 89 wool cards while U, H and L are one apiece (G144) — so scrolling will produce a donut corpus
  and silence everywhere else, which is exactly the wrong input for rule refinement.
  The fix is to show the denominator. A `StructureNames.Canonical()` key is a triple
  (`wools:… | hub:… | front:…`) and the catalog already renders each component of that triple as a card, over
  a space now small enough to enumerate: 81 wool classes up to rotation/reflection, 7 hub forms, 3 frontline
  forms. So add a third filter facet beside *kind* and *reach* — **coverage** — with three states: *judged*
  (this bucket has verdicts), *thin* (one or two, not enough to trust), *unjudged* (nobody has ever looked at
  a board shaped like this). "Find me something nobody has judged" then becomes a chip click, and collection
  turns from an infinite scroll into covering a space with visible edges.
  Needs one query — verdict counts grouped by the structure key — which is a `GROUP BY` on the column G118
  already stores, so **design it with G118's schema rather than bolting it on after**. The UI is small: a chip
  row and a count badge over the same tally plumbing the catalog's `ByTier`/`ByFamily`/`ByKind` already use.
  **A card is a component of a bucket, not a bucket** — a board reading `wools:donut,l` touches both the donut
  and the L card — so per-card coverage is an aggregate over every bucket that card participates in. Build the
  aggregate first (cheap, and enough to spot a blind spot); add a per-bucket drill-down only if the aggregate
  proves too coarse to act on.

- [ ] **G145 — five emitter knobs are unreachable from the composer.** `ShapeEmitter.Emit` takes
  `attachments`, `woolExtend`, `entryShift`, `woolShift` and `attachmentOffset`, and `WoolBoxEmitter.Emit`
  passes all five through — but `WoolBoxEmitter.Fill`, the only path `BoxFiller` and therefore the whole
  compose pipeline uses, forwards none of them (`WoolBoxEmitter.cs`, the `ShapeEmitter.Emit` call inside
  `Fill`). Their only callers in the tree are `tools/compose/box-gallery.cs` (the two-attachment and
  moved-attachment donut cards). So the two-attachment donut, the extended-wool donut and both scythe
  endpoint shifts are built, tested, drawn in the galleries, and **cannot appear on a generated board**.
  Decide per knob rather than in bulk: the donut's second attachment is a genuine multi-access shape the
  hub could dock twice and is the strongest candidate to plumb; the scythe shifts are moot until the
  scythe itself is admitted (G146). Plumbing one means widening `WoolFill` (it already carries
  `AttachmentWidth` and `RingWalls`, so the shape of the change is settled) and giving `UnitRequests` a
  draw for it. **Do not "fix" this by deleting the knobs** — `EmitterPlacementKnobTests` gates them and
  `model.md` §4 describes them; the gap is the plumbing, not the geometry.

- [ ] **G146 — two families are in the vocabulary but never on a board.** The emitter builds eight
  terminal-capped families; the composer puts six on boards. **Z** is listed in
  `FillMenu.ProductionFamilies` and `BoxFiller` fills it happily, but `UnitRequests.WoolRequest` never
  draws it — the rich branch picks donut, then U/H/clamp, else L, and the fallbacks are I. The only caller
  that could reach it is the roll-indexed `BoxFiller.Fill(box, mouth, cw, roll, …)` overload, which has no
  caller in `src/` at all. So the menu advertises a family the sampler cannot produce, and the browse
  tool's own filter chip for it can never match. **Scythe** is the honest case: excluded from
  `ProductionFamilies` with a stated reason (its bay's mouth is its docking edge, so a flush dock seals it
  into WL8's forbidden enclosed void), with the elevation-stage alternative already parked as G81.
  Two separate decisions: either give the sampler a Z draw or drop Z from `ProductionFamilies` so the menu
  stops advertising it (the second is a one-line honesty fix and should not wait on the first); and leave
  the scythe out until G81 lands. Either way the catalog page (G144) will render both under a
  *reachable* / *emitter-only* badge, so the gap becomes visible rather than folklore.

- [ ] **G143 — the board deriver calls segments "edges", which is the one word the model reserves.**
  `model.md` fixes the vocabulary: an **edge** is one full side end to end, a **run** is a contiguous
  stretch along a boundary, an **interval** is where two things touch. `BoardStructure` breaks it —
  `FrontEdges`, `IntraEdges`, `SelfEdges` and `RedstoneEdges` are all `List<(X1,Z1,X2,Z2)>` of
  **cell-boundary segments**, not edges, and the deriver's own comments already call the grouped result
  a *run* (`GroupFrontlineRuns` → `FrontlineRuns`). So the code contradicts itself in one file: the raw
  list is named for a full extent, the grouped one for what it actually is. Rename the four to
  `FrontSegments` / `IntraSegments` / `SelfSegments` / `RedstoneSegments` (or `…BoundarySegments`), and
  sweep the comments that call a segment an edge. Mechanical — the type is a tuple list with a handful
  of consumers (`BoardDeriver`, the deriver gallery, the evaluator terms reading front edges) — but it
  has to land in one commit with the doc, or `model.md` will assert a rule the code visibly breaks.
  Check `BoxEdgeInterface`/`EdgeSpan`/`EdgeInterval` in the same pass: those name a genuine full edge
  and its sub-intervals, so they are correct and should stay, which is exactly why the deriver's misuse
  is worth removing rather than tolerating.
- [ ] **G138 — The composer accepts, it never chooses: a soft score has nowhere to act.** `Composer` takes
  the **first** plan that clears the gate and `break`s (`Composer.cs:59-84`) — no ranking, no comparison, no
  best-of-K. It contains zero references to `Evaluate` or `Score`, only `Gate`, and `Gate` runs hard terms
  only (`LayoutEvaluator.cs:86`). Worse, the compose path builds its context as `EvalContext.Build(plan)`
  with no envelopes, which defaults to `SeedEnvelopes.**Empty**` (`EvalContext.cs:34-38`) — so every soft
  term looks its band up, gets null, and stays dormant by design. **The authored envelopes have no causal
  influence on generated output whatsoever**; they only score plans after the fact, via the
  `Evaluate(PlanModel, …)` overload the API endpoints and galleries call.
  So any soft rule derived from `G118`'s verdicts is **inert until this lands**: generate K candidates,
  score all K, return the best. The loop already generates and discards candidates, so the change is small —
  but it converts the composer from *first-acceptable* to *best-of-K*, which is a real behaviour change and
  will move every fingerprint.
  **Sequence it after the bands are calibrated, not before.** Measured over 560 composed plans, a ranking
  today would be almost entirely a `spawn-wool-ratio` contest (outside its band on 44% of applicable plans
  at median distance 1.64) while four terms score nothing at all — see the `LEARNING.md` debt entry.
  Ranking before recalibration just amplifies one badly-fitted band. Order: `G118` collect → calibrate /
  gate the vacuous terms → this → soft rules become causal.

What stays here is the concrete non-design work on *imported* maps (island detection + playability):

- [ ] **G9 — Re-scan the corpus with stair-aware detection (remaining slice).** The over-split
  **detection fix landed** (`FEATURES.md`: `CleanColumns` + `DetectStairAware`), as did the review
  flag + role classifier. What remains: (a) **re-scan the corpus** so the stored `islands.json` /
  `island_sketch_json` reflect stair-aware (the live DB + `pgm-studio-output` were generated with the legacy
  detection — needs the source worlds, `OvercastCommunity/CommunityMaps`+`PublicMaps` `ctw/`), and decide
  whether to refresh the `--islands` Python-parity oracle to match; (b) the residual `a_new_day` **isolated
  raised-decor specks** (≈37-block grid bits with no walkable connection — correctly `small` via
  `IslandClassifier`, but a per-island prune could drop them); (c) any **under-split / merged** read beyond
  `abstract` (whose stained-glass build-floor is now excluded — `FEATURES.md`): `LooksUnderSplit` is the
  catch-all flag; the residual lever if one is found is to fall through to surface-based detection when a
  cleaned-base component is a map-spanning low-Y slab. Serves the shipped island-health / analysis
  features; the decompose-queue UI slice was dropped with the corpus-mining flywheel.
- [ ] **G12 — Re-prune flying blobs above terrain (stair-aware regression).** Stair-aware connectivity fixed
  the over-split (disconnected islands) but **re-introduced** the stark-y-jump / flying-island problem:
  decorative masses floating above the map (dragons/birds) now merge back into the islands when a near-vertical
  surface chain bridges them (e.g. **Duality**, **mame_i_shrunk_the_pvpers**). Re-add a guard: stop joining
  across a **really big y-increase**, and/or identify & **prune blobs whose base sits well above the terrain
  band** (the old float-prune did this on `DetectHeightAware`; the stair surfaces now leak past it).
  **`max_build_height`** is a natural cut/prune ceiling — anything whose mass is above it is non-playable
  decor. Re-validate the over-split fixes (a_new_day/thunder) still hold after re-adding the ceiling.
- [ ] **G65 — FannedGraph ↔ ContactGraph adjacency reconcile (deferred from G59).** `FannedGraph.LandAdjacent`
  (reachability) still diverges from the rect-layer authority `ContactGraph` on one count: any area overlap
  connects regardless of surface delta, while `Components` unions an overlap only at `SurfaceDelta == 0`.
  (The corridor-width half was reconciled — `LandAdjacent` now accepts Narrow seams, matching `Components`.)
  Pick one rule for the overlap case and add a test; needs per-node surface carried into the fanned graph and
  validation against the traversability harness (`tools/PgmStudio.RoundTrip --traversability`).
- [ ] **G2 — Protection-aware reachability port (memory stage S4).** `MapValidity` (every-wool-needs-a-monument)
  and the `NVAL` export gate (`PreflightEndpoint`) already shipped (`FEATURES.md`). The open slice is to **port
  protection-aware reachability** from `scripts/generator/validate_play.py` to C# `Analysis/Playability`:
  today's `Traversability.Check` only tests connectivity, **not** spawn-protection-as-wall, so it passes maps
  the generator's Python validator would fail. Feed it into the `NVAL` / preflight gate.

- [ ] **G164 — interference: how much of one side's route the other side's route covers.** Every flow
  measure so far reads one traversal at a time, and a single route cannot express tension. Tension is two
  corridors laid over each other: the attacker pushing from a captured wool room toward the remaining
  objective, and the defender travelling from spawn to the same objective. The measurable is the fraction of
  the defender's corridor that the attacker's corridor also covers, computed on the cell mask the same way
  the corridors already are. Measured over 453 two-wool boards at `marker-id-1`: median **34%**, half or more
  on 27%, and **no board reaches zero** — passing the reinforcement lane is unavoidable on generated output.
  This is the term that gives a hub void a purpose the ways-round-a-void count cannot: on a holed hub the
  near way leaves 76% interference and the far way 37%, and the far way measurably reduces the collision on
  74% of the boards offering one, so a layout whose two ways collide equally has bought nothing. Derive side
  belongs beside `BoardDeriver`; the term belongs in `Evaluate/Terms`. It reads a pair of routes rather than
  one, so the origin "a captured wool room" comes from G168's post-capture state — until that exists,
  computing it once per wool treated as captured is the honest stand-in. Background and the full numbers:
  `docs/match-flow.md` §2, §4.9.

- [ ] **G165 — dock arrangement belongs in the structure summary.** Which face of the hub each box seats on
  is a board property with measured consequences and no representation anywhere: it is not the hub's body
  form and not the approach family. With the compass rotated so the frontline is *front*, generated boards
  split **canonical** (spawn *back*, wools *left*+*right*) 27% against **lopsided** (spawn lateral, one wool
  on *back*) 73%, and the split predicts two things — the median spawn-distance imbalance is 0.18 against
  0.40, and the second-wool rotation runs within ten blocks of the spawn on 63% of canonical boards against
  2% of lopsided ones. The faces fall straight out of the mouth positions the box read already computes, so
  the work is small: add them to `StructureSummary` and to `StructureSummary.Canonical()`, which makes the
  arrangement a browse-sieve filter and a verdict/duel bucket key for free. **Land it before verdicts
  accumulate**: `Canonical()` is persisted on a pinned plan as that bucket key, so extending the string
  reshapes every bucket already stored, and a later change needs a key version rather than an edit.

- [ ] **G166 — seating should prefer the canonical arrangement.** `UnitSeating` chooses which hub edge each
  neighbour request seats on, and takes no view on the combination; the result is that three boards in four
  come out lopsided (G165). Prefer the spawn on the edge opposite the frontline with the wools on the two
  lateral edges — the arrangement built maps converge on. The measured payoff is the imbalance halving (0.40
  → 0.18) and the restoration of the rotation-past-spawn dynamic that the lopsided arrangement removes. This
  changes where boxes sit, so it is a geometry change: composer version bump, re-recorded fingerprints, and a
  before/after gallery. Constraining the seat choice can only raise the rejection rate, so measure that
  alongside the arrangement split rather than assuming it stays flat.

- [ ] **G167 — a holed hub should seat its docks across the hole.** A ring, double-hole, P or G hub only
  offers two ways across when the two docks straddle its void, and today that is left to chance: ring hubs
  deliver two ways on 163 of 224 spawn-to-wool crossings, and the ones that do not are dead by seating rather
  than by shape — the same body form with both docks on one side is a wide room with a decorative hole in it.
  When the sampled hub body encloses a void, prefer opposite walls for the two docks. The value is not the
  extra distance but what G164 measures: the far way round drops interference from 76% to 37%, which is
  the difference between an alternative and an alternative worth taking. Geometry change, so the same
  fingerprint and gallery costs as G166, and the two should land together or in a known order since both
  touch the same seat choice.

- [ ] **G168 — a board is worth evaluating in two game states.** A two-wool map is not one arrangement but
  two in sequence: before the first capture both objectives are defended from the spawn, and after it one
  room is the attacking team's forward node — a place worth travelling to for the chest gear the generator
  emits — and the wool-to-wool route becomes the live one. Terms that are vacuous in the first state carry
  the whole second phase, so evaluating only the opening scores half a match. This is a change to the
  evaluator's shape rather than a new term: `EvalContext` carries which state is being read, and the terms
  that only apply post-capture (G164's interference, rotation between objectives) declare it. Decide
  early whether the two states produce two scores or one combined figure — a single number that averages a
  strong opening against a hopeless second phase describes neither. The played account is in
  `docs/match-flow.md` §4.8.

## Lower priority / parked

Existing-Edit (`/maps/{id}/edit`) authoring features — **not** used by the intent generator (which
auto-wires), and Edit is frozen. Resume when the existing-map authoring path is picked up. Their
*backends* are done (`FEATURES.md`).

- [ ] **Wire-after-group + filter-wiring UI** (ex-`N4` + ex-`F1`). Group regions in Edit → apply
  a wiring template by role; cross-step carve-out (complement) detection; canvas Ctrl-click
  multi-select. The wiring backend (`FilterWiring` appliers + `POST /wiring/apply`) is done.
- [ ] **Symmetry counterpart accept/reject UI + IoU equivalence** (ex-`F3` + ex-`A2`). Canvas
  preview/confirm for orbit-created counterparts + `regions_equivalent`/`is_counterpart` detection for
  dedup + symmetry-violation review. The counterpart + orbit-fill backend is done (the authoring
  generator already uses orbit-fill automatically).
- [ ] **3D / side-depth selection view** (ex-`F8` 3D half). The flat side-view slice is done (→ `N08`);
  a true 3D selection view (monument point/block + cuboid Y) needs design. Later.
- [ ] **Comment hygiene sweep — purely functional comments.** Code comments must describe behaviour
  only: **no** references to the Python reference app ("port of", "mirrors the reference", parity/oracle)
  and **no** implementation-phase / task ids (`NS`, `N00`, `B8`, `P5`, `ND2`, …). New code already
  follows this (CLAUDE.md). ~19 task-id references + ~41 parity/"port of" references remain across
  `src/` + `tests/` (e.g. `ImportEndpoints`, `WorldScanStep`, `WorldFeatureWriter`) — sweep them.

**Deprioritized — may be dropped in a later pass.** Optional/deferred slices parked out of the active
long-tail so they stop competing with real work. Re-evaluate (or delete) when their area is next touched.

- [ ] **S10 — Auto-promote rectangles on Bézier (parked, optional).** Today S4 promotes via the inspector
  button / `P`; a rectangle keeps its 8-handle resize and has no Bézier affordance. If we ever want a
  rectangle's corner to sprout a Bézier handle that *implicitly* converts it to a polygon, it needs rect
  vertex/tangent handles in `sketch-edit-controller.js` (a UX decision on resize-handles vs vertex-handles).
  Low priority — explicit promotion already covers the need.
- [ ] **P8 — Pipeline re-run on config change (parked escape hatch, world-present only).** A
  parameterized re-scan honouring a bespoke `scan_layer`/`exclude_blocks` → re-detect islands → rewrite
  **layer-tagged** `layer.parquet` / `islands.json`. The per-map scan-layer + custom block-exclusion UI
  has been **removed** from both editors (detection is the fixed cleaned base; the world-scanning
  endpoints are gone), so there is no longer a config-change to honour from the UI — this remains only as
  a rare, local-only override path outside the hosted flow (new-map-authoring.md §6a). (Island-exclusion →
  symmetry re-run already works without a re-scan, B7.)
- [ ] **P7 — [Deferred decision] Consolidate the layer extractors / scan passes.** **`ND2` settles the
  "consolidate vs keep" half: KEEP the exact per-layer extractors** — the World step uses them in distinct
  roles (cleaned `Base` = detection · `Surface` = visual aid · `Segments` = vertical), so they're a feature,
  not duplication; their per-layer default ignored-block sets (`Base` gets the expanded ND2 noise set;
  Surface/Y0 = air-only) are the solid-policy. Still open: the byte-parity sub-question — a segment-derived
  surface would **not** be byte-parity with the reference (endpoint-only runs also can't honour user
  `exclude_blocks`). Pairs with A4.
- [ ] **A3 — Buildability endpoint perf (verify, then optimise if needed).** Per-cell NTS over the grid
  was flagged slow; the endpoint is now live and user-visible (`N03`'s buildability overlay landed).
  **First profile it under the Configure overlay** — only optimise (spatial index / batch) if it's
  actually slow in use; otherwise close.
- [ ] **A4 — [Consider, not perf] Vector-boolean island outlines (drop the rasterize→polygon round-trip).**
  Today island outlines come from a pixel round-trip: vector shapes → rasterize to cells → BFS → `BlocksToPolygon`
  (cells back to a polygon), done only to **avoid a C# polygon-boolean lib** (sketch-authoring.md §6). We
  already depend on NTS, so the sketch-finish island polygons *could* be computed by NTS vector boolean
  directly off the shapes (union adds, difference subs), dropping `BlocksToPolygon` + the BFS for the
  *polygon*. **Not a perf task** — the row-run fix already removed the hotspot, and the cell rasterize must
  still run for `layer_segment`/`layer.parquet` (Configure height side-view + analysis). Payoff is cleanliness
  + exact (smooth) outlines; cost is NTS boolean on the authoring path and a **staircase→smooth** outline
  divergence from scanned maps. Weigh before doing.
